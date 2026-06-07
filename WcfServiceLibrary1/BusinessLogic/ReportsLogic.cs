using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using ViewDB;

namespace BusinessLogic
{
    /// <summary>
    /// Business logic for reporting and analytics: VAT, profit/loss, customer and
    /// expense breakdowns, KPIs, cashflow forecast, and the composite report /
    /// owner-dashboard snapshots. Aggregators delegate to LoanLogic and
    /// NotificationLogic where needed.
    /// </summary>
    public class ReportsLogic
    {
        private readonly ReportsDB      reportsDB = new ReportsDB();
        private readonly InvoiceDB      invDB     = new InvoiceDB();
        private readonly CustomerDB     custDB    = new CustomerDB();
        private readonly NotificationDB notifDB   = new NotificationDB();
        private readonly ProjectDB      projDB    = new ProjectDB();
        private readonly UserDB         userDB    = new UserDB();

        public VatSummary GetVatSummary(int ownerId, int year, int month, string displayCurrency)
            => reportsDB.VatSummary(ownerId, year, month, displayCurrency ?? "ILS");

        public decimal GetMonthlyTaxSetAside(int ownerId, int year, int month, string displayCurrency)
            => reportsDB.MonthlyTaxSetAside(ownerId, year, month, displayCurrency ?? "ILS");

        public ProfitLoss GetProfitLoss(int ownerId, DateTime from, DateTime to, string displayCurrency)
            => reportsDB.ProfitLoss(ownerId, from, to, displayCurrency ?? "ILS");

        public List<CustomerRevenueRow> GetTopCustomersByRevenue(int ownerId, string displayCurrency)
            => reportsDB.TopCustomersByRevenue(ownerId, displayCurrency ?? "ILS");

        public List<ExpenseBreakdownRow> GetExpenseBreakdown(int ownerId, DateTime from, DateTime to, string displayCurrency)
            => reportsDB.ExpenseBreakdown(ownerId, from, to, displayCurrency ?? "ILS");

        public List<EmployeeRevenueRow> GetEmployeeRevenueReport(int ownerId, string displayCurrency)
            => reportsDB.EmployeeRevenueReport(ownerId, displayCurrency ?? "ILS");

        // Cashflow forecast: trailing 3-month average for income/expenses,
        // projected forward for `months` periods. Adds outstanding invoices
        // due in the projection window to the income side of the matching month.
        public List<ProfitLoss> GetCashFlowForecast(int ownerId, int months, string displayCurrency)
        {
            var cur = displayCurrency ?? "ILS";
            decimal sumInc = 0, sumExp = 0;
            int n = 3;
            var anchor = DateTime.Today;
            for (int i = 1; i <= n; i++)
            {
                var first = new DateTime(anchor.Year, anchor.Month, 1).AddMonths(-i);
                var last  = first.AddMonths(1).AddDays(-1);
                var pl = reportsDB.ProfitLoss(ownerId, first, last, cur);
                if (pl == null) continue;
                sumInc += pl.Income;
                sumExp += pl.Expenses;
            }
            decimal avgInc = sumInc / n;
            decimal avgExp = sumExp / n;

            // Outstanding invoices boost the month their dueDate falls in.
            var outstanding = invDB.GetUnpaidForOwner(ownerId);

            var result = new List<ProfitLoss>();
            for (int i = 1; i <= months; i++)
            {
                var first = new DateTime(anchor.Year, anchor.Month, 1).AddMonths(i);
                var last  = first.AddMonths(1).AddDays(-1);

                decimal extraIncome = 0;
                if (outstanding != null)
                {
                    foreach (var inv in outstanding)
                    {
                        if (inv.DueDate.Date >= first && inv.DueDate.Date <= last)
                            extraIncome += inv.Total;
                    }
                }

                result.Add(new ProfitLoss
                {
                    Income          = avgInc + extraIncome,
                    Expenses        = avgExp,
                    Profit          = (avgInc + extraIncome) - avgExp,
                    DisplayCurrency = cur,
                });
            }
            return result;
        }

        public AnalyticsKpis GetAdvancedKpis(int ownerId, string displayCurrency)
            => reportsDB.AdvancedKpis(ownerId, string.IsNullOrEmpty(displayCurrency) ? "ILS" : displayCurrency);

        public ReportsSnapshot GetReportsSnapshot(int ownerId, int year, int month, string displayCurrency)
        {
            string cur = string.IsNullOrEmpty(displayCurrency) ? "ILS" : displayCurrency;
            var first = new DateTime(year, month, 1);
            var last  = first.AddMonths(1).AddDays(-1);
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd   = new DateTime(year, 12, 31);

            var snap = new ReportsSnapshot { DisplayCurrency = cur };
            try { snap.Vat              = reportsDB.VatSummary(ownerId, year, month, cur); } catch { }
            try { snap.TopCustomers     = reportsDB.TopCustomersByRevenue(ownerId, cur); } catch { }
            try { snap.ExpenseBreakdown = reportsDB.ExpenseBreakdown(ownerId, first, last, cur); } catch { }
            try { snap.MonthPl          = reportsDB.ProfitLoss(ownerId, first, last, cur); } catch { }
            try { snap.YearPl           = reportsDB.ProfitLoss(ownerId, yearStart, yearEnd, cur); } catch { }
            try { snap.Kpis             = reportsDB.AdvancedKpis(ownerId, cur); } catch { }
            try { snap.LoanSummary      = new LoanLogic().GetLoanSummary(ownerId, cur); } catch { }
            try
            {
                var u = userDB.GetById(ownerId);
                if (u != null)
                {
                    snap.BusinessType = string.IsNullOrEmpty(u.BusinessType) ? "Individual" : u.BusinessType;
                    snap.IsZair = u.IsZair;
                }
            }
            catch { }
            return snap;
        }

        public OwnerDashboardSnapshot GetOwnerDashboardSnapshot(int ownerId, string displayCurrency)
        {
            string cur = string.IsNullOrEmpty(displayCurrency) ? "ILS" : displayCurrency;
            try { new NotificationLogic().EnsureOverdueNotifications(ownerId); } catch { }

            var now = DateTime.Today;
            var monthFirst = new DateTime(now.Year, now.Month, 1);
            var monthLast  = monthFirst.AddMonths(1).AddDays(-1);
            var prevFirst  = monthFirst.AddMonths(-1);
            var prevLast   = monthFirst.AddDays(-1);

            // Pull invoices once via a single JOINed query, derive both the
            // unpaid total and the recent-invoices preview from the same list.
            var allInvoices = invDB.GetForOwner(ownerId) ?? new List<Invoice>();
            decimal unpaidTotal = allInvoices
                .Where(i => i.Status != "Paid")
                .Sum(i => i.Total);
            var customers = custDB.GetByOwner(ownerId) ?? new List<Customer>();
            var custLookup = customers.ToDictionary(c => c.Id, c => c.BusinessName ?? "");
            var recent = allInvoices
                .OrderByDescending(i => i.IssueDate)
                .Take(6)
                .Select(i => new RecentInvoice
                {
                    InvoiceNumber = i.InvoiceNumber,
                    CustomerName  = custLookup.TryGetValue(i.CustomerId, out var n) ? n : "",
                    Total         = i.Total,
                    Currency      = i.Currency,
                    Status        = i.Status,
                }).ToList();

            // VAT + tax set-aside (current month).
            var vat = reportsDB.VatSummary(ownerId, now.Year, now.Month, cur);
            decimal taxSetAside = reportsDB.MonthlyTaxSetAside(ownerId, now.Year, now.Month, cur);

            // Top expense category this month + revenue trend vs prior month.
            string topCat = null; decimal topAmt = 0m;
            try
            {
                var brk = reportsDB.ExpenseBreakdown(ownerId, monthFirst, monthLast, cur);
                if (brk != null && brk.Count > 0)
                {
                    var top = brk.OrderByDescending(b => b.Total).First();
                    topCat = top.CategoryName; topAmt = top.Total;
                }
            }
            catch { }

            decimal revPct = 0m;
            try
            {
                var thisPl = reportsDB.ProfitLoss(ownerId, monthFirst, monthLast, cur);
                var prevPl = reportsDB.ProfitLoss(ownerId, prevFirst, prevLast, cur);
                if (thisPl != null && prevPl != null && prevPl.Income > 0)
                    revPct = Math.Round(((thisPl.Income - prevPl.Income) / prevPl.Income) * 100m, 1);
            }
            catch { }

            return new OwnerDashboardSnapshot
            {
                CustomersCount = customers.Count,
                UnpaidCount = allInvoices.Count(i => i.Status != "Paid"),
                OverdueCount = invDB.GetOverdueForOwner(ownerId)?.Count ?? 0,
                ActiveProjectsCount = projDB.GetByStatus("Active", ownerId)?.Count ?? 0,
                UnreadNotificationsCount = notifDB.UnreadCount(ownerId),
                CashFlowForecast = GetCashFlowForecast(ownerId, 3, cur),
                Kpis = reportsDB.AdvancedKpis(ownerId, cur),
                LoanSummary = new LoanLogic().GetLoanSummary(ownerId, cur),
                UnpaidTotal = unpaidTotal,
                VatDue = vat?.VatDue ?? 0m,
                MonthlyTaxSetAside = taxSetAside,
                TopExpenseCategory = topCat,
                TopExpenseAmount = topAmt,
                RevenueChangePct = revPct,
                RecentInvoices = recent,
                DisplayCurrency = cur,
            };
        }
    }
}
