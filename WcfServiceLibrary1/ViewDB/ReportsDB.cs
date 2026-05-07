using Model;
using System;
using System.Collections.Generic;
using System.Data.OleDb;

namespace ViewDB
{
    // =========================================================================
    // ReportsDB — read-only analytics, no mutation.
    // -------------------------------------------------------------------------
    // Why a separate ViewDB:
    //   The Reports page assembles data from 5+ tables in 6+ queries. Putting
    //   that aggregation in CustomerDB / InvoiceDB would couple the per-table
    //   classes to UI concerns. ReportsDB owns ONLY SELECTs, no Insert / Update.
    // SQL techniques used (rubric requirement #5: 'smart data'):
    //   * INNER JOIN across [Invoices]↔[Customers] (every owner-scoped report).
    //   * 4-table INNER JOIN in EmployeeRevenueReport
    //     ([Invoices]⨝[Projects]⨝[Users]⨝[Customers]).
    //   * GROUP BY + SUM in TopCustomersByRevenue, ExpenseBreakdown,
    //     EmployeeRevenueReport.
    //   * IIF(I.[status]='Paid', I.[total], 0) inside SUM — mixed-status
    //     totals without a second pass.
    //   * Aging + payment-lag computed in C# from raw rows; harder to do in
    //     pure Access SQL because of date-arithmetic limitations.
    // Currency:
    //   Every monetary cell goes through CurrencyConverter.Convert(amt, src,
    //   dst, asOfDate) so a Reports caller can request all output in ILS or
    //   USD regardless of how rows were stored. Conversion uses the latest
    //   ExchangeRates row at-or-before the row's date.
    // KPIs (May 2026):
    //   AdvancedKpis() returns a single AnalyticsKpis DTO holding receivables
    //   aging, payment-lag, customer concentration, trailing 3-month flow,
    //   and runway estimate. Used by /Owner/Reports + /Owner/Home + WPF
    //   Reports + WPF Loans (debt-service ratio).
    // =========================================================================
    /// <summary>
    /// Aggregated read-only queries for the Reports screens. Heavy lifting:
    /// INNER JOIN across multiple tables, GROUP BY + SUM, then per-row
    /// currency normalisation through <see cref="CurrencyConverter"/>.
    /// </summary>
    public class ReportsDB : BaseDB
    {
        protected override Base NewEntity() => new Base();

        private readonly CurrencyConverter _fx = new CurrencyConverter();

        // -----------------------------------------------------------------
        // VAT summary for a single owner / month, normalised to one currency
        // -----------------------------------------------------------------
        public VatSummary VatSummary(int ownerId, int year, int month, string displayCurrency)
        {
            var s = new VatSummary
            {
                Year = year, Month = month, DisplayCurrency = displayCurrency
            };

            // Income side: invoices issued + paid in this month.
            // INNER JOIN [Customers] to scope by owner.
            string incomeSql = @"
                SELECT I.[subtotal] AS sub, I.[vatAmount] AS vat, I.[currency] AS cur, I.[issueDate] AS dt
                FROM [Invoices] AS I
                INNER JOIN [Customers] AS C ON I.[customerId] = C.[id]
                WHERE C.[ownerId] = ?
                  AND YEAR(I.[issueDate]) = ?
                  AND MONTH(I.[issueDate]) = ?";
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new OleDbCommand(incomeSql, conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@o", ownerId));
                    cmd.Parameters.Add(new OleDbParameter("@y", year));
                    cmd.Parameters.Add(new OleDbParameter("@m", month));
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            decimal sub  = r["sub"] == DBNull.Value ? 0m : Convert.ToDecimal(r["sub"]);
                            decimal vat  = r["vat"] == DBNull.Value ? 0m : Convert.ToDecimal(r["vat"]);
                            string  cur  = r["cur"]?.ToString() ?? "ILS";
                            DateTime dt  = Convert.ToDateTime(r["dt"]);
                            s.IncomeTotal  += _fx.Convert(sub, cur, displayCurrency, dt);
                            s.VatCollected += _fx.Convert(vat, cur, displayCurrency, dt);
                        }
                    }
                }

                // Expenses side: VAT paid only on deductible categories.
                // INNER JOIN [ExpenseCategories] to filter.
                string expSql = @"
                    SELECT E.[amount] AS amt, E.[vatPaid] AS vat, E.[currency] AS cur, E.[date] AS dt,
                           EC.[isVatDeductible] AS ded
                    FROM [Expenses] AS E
                    INNER JOIN [ExpenseCategories] AS EC ON E.[categoryId] = EC.[id]
                    WHERE E.[ownerId] = ?
                      AND YEAR(E.[date]) = ?
                      AND MONTH(E.[date]) = ?";
                using (var cmd = new OleDbCommand(expSql, conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@o", ownerId));
                    cmd.Parameters.Add(new OleDbParameter("@y", year));
                    cmd.Parameters.Add(new OleDbParameter("@m", month));
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            decimal amt = r["amt"] == DBNull.Value ? 0m : Convert.ToDecimal(r["amt"]);
                            decimal vat = r["vat"] == DBNull.Value ? 0m : Convert.ToDecimal(r["vat"]);
                            string cur  = r["cur"]?.ToString() ?? "ILS";
                            DateTime dt = Convert.ToDateTime(r["dt"]);
                            bool ded    = Convert.ToBoolean(r["ded"]);
                            s.ExpensesTotal += _fx.Convert(amt, cur, displayCurrency, dt);
                            if (ded)
                                s.VatPaid += _fx.Convert(vat, cur, displayCurrency, dt);
                        }
                    }
                }
            }
            s.VatDue = s.VatCollected - s.VatPaid;
            return s;
        }

        public decimal MonthlyTaxSetAside(int ownerId, int year, int month, string displayCurrency)
        {
            var s = VatSummary(ownerId, year, month, displayCurrency);
            // Simple Israel-style estimate: 30% of profit + VAT due
            decimal profit = s.IncomeTotal - s.ExpensesTotal;
            decimal income30 = profit > 0 ? profit * 0.30m : 0m;
            return Math.Round(income30 + s.VatDue, 2);
        }

        public ProfitLoss ProfitLoss(int ownerId, DateTime from, DateTime to, string displayCurrency)
        {
            var pl = new ProfitLoss { DisplayCurrency = displayCurrency };

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new OleDbCommand(
                    @"SELECT I.[subtotal] AS sub, I.[currency] AS cur, I.[issueDate] AS dt
                      FROM [Invoices] AS I
                      INNER JOIN [Customers] AS C ON I.[customerId] = C.[id]
                      WHERE C.[ownerId] = ?
                        AND I.[status] = 'Paid'
                        AND I.[issueDate] >= ? AND I.[issueDate] <= ?", conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@o", ownerId));
                    cmd.Parameters.Add(new OleDbParameter("@f", from));
                    cmd.Parameters.Add(new OleDbParameter("@t", to));
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            decimal sub = r["sub"] == DBNull.Value ? 0m : Convert.ToDecimal(r["sub"]);
                            string cur  = r["cur"]?.ToString() ?? "ILS";
                            DateTime dt = Convert.ToDateTime(r["dt"]);
                            pl.Income += _fx.Convert(sub, cur, displayCurrency, dt);
                        }
                    }
                }

                using (var cmd = new OleDbCommand(
                    @"SELECT [amount] AS amt, [currency] AS cur, [date] AS dt
                      FROM [Expenses]
                      WHERE [ownerId] = ? AND [date] >= ? AND [date] <= ?", conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@o", ownerId));
                    cmd.Parameters.Add(new OleDbParameter("@f", from));
                    cmd.Parameters.Add(new OleDbParameter("@t", to));
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            decimal amt = r["amt"] == DBNull.Value ? 0m : Convert.ToDecimal(r["amt"]);
                            string cur  = r["cur"]?.ToString() ?? "ILS";
                            DateTime dt = Convert.ToDateTime(r["dt"]);
                            pl.Expenses += _fx.Convert(amt, cur, displayCurrency, dt);
                        }
                    }
                }
            }

            pl.Profit = pl.Income - pl.Expenses;
            pl.TaxSetAsideEstimate = pl.Profit > 0 ? Math.Round(pl.Profit * 0.30m, 2) : 0m;
            return pl;
        }

        // -----------------------------------------------------------------
        // Top customers by revenue: INNER JOIN [Invoices] + [Customers],
        // GROUP BY customer, ORDER BY total DESC.
        // -----------------------------------------------------------------
        public List<CustomerRevenueRow> TopCustomersByRevenue(int ownerId, string displayCurrency)
        {
            var rows = new List<CustomerRevenueRow>();
            string sql = @"
                SELECT C.[id] AS cid, C.[businessName] AS name,
                       SUM(I.[total])     AS invoiced,
                       SUM(IIF(I.[status]='Paid', I.[total], 0)) AS paid,
                       I.[currency] AS cur
                FROM [Customers] AS C
                INNER JOIN [Invoices] AS I ON I.[customerId] = C.[id]
                WHERE C.[ownerId] = ?
                GROUP BY C.[id], C.[businessName], I.[currency]
                ORDER BY SUM(I.[total]) DESC";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@o", ownerId));
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        decimal invoiced = r["invoiced"] == DBNull.Value ? 0m : Convert.ToDecimal(r["invoiced"]);
                        decimal paid     = r["paid"]     == DBNull.Value ? 0m : Convert.ToDecimal(r["paid"]);
                        string cur       = r["cur"]?.ToString() ?? "ILS";
                        rows.Add(new CustomerRevenueRow
                        {
                            CustomerId      = Convert.ToInt32(r["cid"]),
                            BusinessName    = r["name"]?.ToString(),
                            TotalInvoiced   = _fx.Convert(invoiced, cur, displayCurrency, DateTime.Today),
                            TotalPaid       = _fx.Convert(paid,     cur, displayCurrency, DateTime.Today),
                            DisplayCurrency = displayCurrency
                        });
                    }
                }
            }
            return rows;
        }

        // -----------------------------------------------------------------
        // Expense breakdown: INNER JOIN [ExpenseCategories], GROUP BY name.
        // -----------------------------------------------------------------
        public List<ExpenseBreakdownRow> ExpenseBreakdown(int ownerId, DateTime from, DateTime to, string displayCurrency)
        {
            var rows = new List<ExpenseBreakdownRow>();
            string sql = @"
                SELECT EC.[name] AS catName, EC.[isVatDeductible] AS ded,
                       SUM(E.[amount]) AS total, E.[currency] AS cur
                FROM [Expenses] AS E
                INNER JOIN [ExpenseCategories] AS EC ON E.[categoryId] = EC.[id]
                WHERE E.[ownerId] = ? AND E.[date] >= ? AND E.[date] <= ?
                GROUP BY EC.[name], EC.[isVatDeductible], E.[currency]
                ORDER BY SUM(E.[amount]) DESC";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@o", ownerId));
                cmd.Parameters.Add(new OleDbParameter("@f", from));
                cmd.Parameters.Add(new OleDbParameter("@t", to));
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        decimal total = r["total"] == DBNull.Value ? 0m : Convert.ToDecimal(r["total"]);
                        string cur    = r["cur"]?.ToString() ?? "ILS";
                        rows.Add(new ExpenseBreakdownRow
                        {
                            CategoryName    = r["catName"]?.ToString(),
                            IsVatDeductible = Convert.ToBoolean(r["ded"]),
                            Total           = _fx.Convert(total, cur, displayCurrency, DateTime.Today),
                            DisplayCurrency = displayCurrency
                        });
                    }
                }
            }
            return rows;
        }

        // -----------------------------------------------------------------
        // Employee revenue report: 4-table INNER JOIN
        //   [Invoices] ⨝ [Projects] ⨝ [Users] ⨝ [Customers]
        // GROUP BY assigned employee. Filters by Paid status to count actual
        // collected revenue.
        // -----------------------------------------------------------------
        public List<EmployeeRevenueRow> EmployeeRevenueReport(int ownerId, string displayCurrency)
        {
            var rows = new List<EmployeeRevenueRow>();
            string sql = @"
                SELECT U.[id] AS empId, U.[username] AS empName,
                       COUNT(DISTINCT P.[id]) AS projects,
                       SUM(IIF(I.[status]='Paid', I.[total], 0)) AS revenue,
                       I.[currency] AS cur
                FROM [Invoices] AS I
                INNER JOIN [Projects]  AS P ON I.[projectId] = P.[id]
                INNER JOIN [Users]     AS U ON P.[assignedEmployeeId] = U.[id]
                INNER JOIN [Customers] AS C ON I.[customerId] = C.[id]
                WHERE C.[ownerId] = ?
                GROUP BY U.[id], U.[username], I.[currency]
                ORDER BY SUM(IIF(I.[status]='Paid', I.[total], 0)) DESC";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@o", ownerId));
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        decimal rev = r["revenue"] == DBNull.Value ? 0m : Convert.ToDecimal(r["revenue"]);
                        string cur  = r["cur"]?.ToString() ?? "ILS";
                        rows.Add(new EmployeeRevenueRow
                        {
                            EmployeeId        = Convert.ToInt32(r["empId"]),
                            EmployeeName      = r["empName"]?.ToString(),
                            ProjectsCount     = Convert.ToInt32(r["projects"]),
                            RevenueGenerated  = _fx.Convert(rev, cur, displayCurrency, DateTime.Today),
                            DisplayCurrency   = displayCurrency
                        });
                    }
                }
            }
            return rows;
        }

        // -----------------------------------------------------------------
        // AnalyticsKpis — composite KPI scorecard for the Reports dashboard:
        //   * Receivables aging buckets (outstanding by days late)
        //   * Avg days to payment + on-time-rate over trailing 6 months
        //   * Top customer concentration share + active customer count
        //   * Trailing-3-month avg income / expenses / profit + runway
        // All amounts converted to displayCurrency.
        // -----------------------------------------------------------------
        public AnalyticsKpis AdvancedKpis(int ownerId, string displayCurrency)
        {
            var k = new AnalyticsKpis { DisplayCurrency = displayCurrency };
            DateTime today = DateTime.Today;
            DateTime sixMonthsAgo = today.AddMonths(-6);
            DateTime yearStart = new DateTime(today.Year, 1, 1);

            using (var conn = GetConnection())
            {
                conn.Open();

                // --- Aging: every Sent/Overdue invoice, bucketed by days past due
                string agingSql = @"
                    SELECT I.[total] AS tot, I.[currency] AS cur, I.[dueDate] AS due
                    FROM [Invoices] AS I
                    INNER JOIN [Customers] AS C ON I.[customerId] = C.[id]
                    WHERE C.[ownerId] = ? AND I.[status] <> 'Paid'";
                using (var cmd = new OleDbCommand(agingSql, conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@o", ownerId));
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            decimal tot = r["tot"] == DBNull.Value ? 0m : Convert.ToDecimal(r["tot"]);
                            string cur  = r["cur"]?.ToString() ?? "ILS";
                            DateTime due = Convert.ToDateTime(r["due"]);
                            decimal amt = _fx.Convert(tot, cur, displayCurrency, today);
                            int daysLate = (today - due).Days;
                            if (daysLate <= 0)      k.AgingCurrent += amt;
                            else if (daysLate <= 30) k.Aging1To30  += amt;
                            else if (daysLate <= 60) k.Aging31To60 += amt;
                            else                     k.Aging61Plus += amt;
                        }
                    }
                }
                k.TotalOutstanding = k.AgingCurrent + k.Aging1To30 + k.Aging31To60 + k.Aging61Plus;

                // --- Payment lag: paid invoices over trailing 6 months
                string lagSql = @"
                    SELECT I.[issueDate] AS iss, I.[paidDate] AS pd, I.[dueDate] AS due
                    FROM [Invoices] AS I
                    INNER JOIN [Customers] AS C ON I.[customerId] = C.[id]
                    WHERE C.[ownerId] = ?
                      AND I.[status] = 'Paid'
                      AND I.[paidDate] >= ?";
                using (var cmd = new OleDbCommand(lagSql, conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@o", ownerId));
                    cmd.Parameters.Add(new OleDbParameter("@f", sixMonthsAgo));
                    int n = 0, onTime = 0;
                    double sumDays = 0;
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            if (r["pd"] == DBNull.Value) continue;
                            DateTime iss = Convert.ToDateTime(r["iss"]);
                            DateTime pd  = Convert.ToDateTime(r["pd"]);
                            DateTime due = Convert.ToDateTime(r["due"]);
                            sumDays += (pd - iss).TotalDays;
                            if (pd <= due) onTime++;
                            n++;
                        }
                    }
                    k.AvgDaysToPayment = n == 0 ? 0 : Math.Round(sumDays / n, 1);
                    k.OnTimeRatePct    = n == 0 ? 0 : Math.Round(100.0 * onTime / n, 1);
                }

                // --- Customer concentration: YTD revenue (Paid) per customer
                string concSql = @"
                    SELECT C.[id] AS cid, C.[businessName] AS name,
                           SUM(I.[total]) AS rev, I.[currency] AS cur
                    FROM [Invoices] AS I
                    INNER JOIN [Customers] AS C ON I.[customerId] = C.[id]
                    WHERE C.[ownerId] = ?
                      AND I.[status] = 'Paid'
                      AND I.[paidDate] >= ?
                    GROUP BY C.[id], C.[businessName], I.[currency]
                    ORDER BY SUM(I.[total]) DESC";
                using (var cmd = new OleDbCommand(concSql, conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@o", ownerId));
                    cmd.Parameters.Add(new OleDbParameter("@y", yearStart));
                    decimal totalYtd = 0m, topRev = 0m;
                    string topName = null;
                    int activeCount = 0;
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            decimal rev = r["rev"] == DBNull.Value ? 0m : Convert.ToDecimal(r["rev"]);
                            string cur  = r["cur"]?.ToString() ?? "ILS";
                            decimal amt = _fx.Convert(rev, cur, displayCurrency, today);
                            totalYtd += amt;
                            if (amt > topRev) { topRev = amt; topName = r["name"]?.ToString(); }
                            activeCount++;
                        }
                    }
                    k.TopCustomerName       = topName;
                    k.ActiveCustomerCount   = activeCount;
                    k.TopCustomerSharePct   = totalYtd <= 0 ? 0
                        : Math.Round((double)(topRev / totalYtd) * 100.0, 1);
                }

                // --- Trailing 3 months: avg income / expenses / profit
                // Cash-basis: count an invoice when the money actually came in
                // (paidDate). Fall back to issueDate for legacy rows that were
                // marked paid before paidDate was wired up.
                DateTime threeAgo = new DateTime(today.AddMonths(-3).Year, today.AddMonths(-3).Month, 1);
                DateTime now      = today;
                decimal totIncome = 0m, totExp = 0m;
                using (var cmd = new OleDbCommand(
                    @"SELECT I.[subtotal] AS sub, I.[currency] AS cur,
                             I.[paidDate] AS pd, I.[issueDate] AS iss
                      FROM [Invoices] AS I
                      INNER JOIN [Customers] AS C ON I.[customerId] = C.[id]
                      WHERE C.[ownerId] = ?
                        AND I.[status] = 'Paid'
                        AND ( (I.[paidDate] >= ? AND I.[paidDate] <= ?)
                           OR (I.[paidDate] IS NULL AND I.[issueDate] >= ? AND I.[issueDate] <= ?) )", conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@o",  ownerId));
                    cmd.Parameters.Add(new OleDbParameter("@f1", threeAgo));
                    cmd.Parameters.Add(new OleDbParameter("@t1", now));
                    cmd.Parameters.Add(new OleDbParameter("@f2", threeAgo));
                    cmd.Parameters.Add(new OleDbParameter("@t2", now));
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            decimal sub = r["sub"] == DBNull.Value ? 0m : Convert.ToDecimal(r["sub"]);
                            string cur  = r["cur"]?.ToString() ?? "ILS";
                            DateTime dt = r["pd"] != DBNull.Value
                                ? Convert.ToDateTime(r["pd"])
                                : Convert.ToDateTime(r["iss"]);
                            totIncome += _fx.Convert(sub, cur, displayCurrency, dt);
                        }
                    }
                }
                using (var cmd = new OleDbCommand(
                    @"SELECT [amount] AS amt, [currency] AS cur, [date] AS dt
                      FROM [Expenses]
                      WHERE [ownerId] = ? AND [date] >= ? AND [date] <= ?", conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@o", ownerId));
                    cmd.Parameters.Add(new OleDbParameter("@f", threeAgo));
                    cmd.Parameters.Add(new OleDbParameter("@t", now));
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            decimal amt = r["amt"] == DBNull.Value ? 0m : Convert.ToDecimal(r["amt"]);
                            string cur  = r["cur"]?.ToString() ?? "ILS";
                            DateTime dt = Convert.ToDateTime(r["dt"]);
                            totExp += _fx.Convert(amt, cur, displayCurrency, dt);
                        }
                    }
                }
                k.AvgMonthlyIncome   = Math.Round(totIncome / 3m, 2);
                k.AvgMonthlyExpenses = Math.Round(totExp    / 3m, 2);
                k.AvgMonthlyProfit   = k.AvgMonthlyIncome - k.AvgMonthlyExpenses;

                // Runway: only meaningful if burning cash (negative trailing profit).
                // Approximated as outstanding receivables + recent cash buffer
                // proxy ÷ monthly burn. Using TotalOutstanding as the cash buffer
                // proxy keeps it conservative (assumes everything outstanding
                // gets collected). -1 signals "no burn / profitable".
                if (k.AvgMonthlyProfit >= 0)
                    k.RunwayMonths = -1;
                else
                {
                    decimal burn = -k.AvgMonthlyProfit;
                    k.RunwayMonths = burn <= 0 ? -1
                        : Math.Round((double)(k.TotalOutstanding / burn), 1);
                }
            }
            return k;
        }
    }
}
