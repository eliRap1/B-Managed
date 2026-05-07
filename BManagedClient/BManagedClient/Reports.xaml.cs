using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace BManagedClient
{
    // =========================================================================
    // Reports page (WPF) — Owner-only analytics dashboard.
    // -------------------------------------------------------------------------
    // Mirrors /Owner/Reports on the Web, including:
    //   * VAT triple (collected / paid / due) — month + year + currency
    //     selectable via three ComboBoxes at the top.
    //   * Top customers + Expense breakdown lists.
    //   * P&L year card with Israeli income-tax bracket calc; respects the
    //     IsZair flag for חישוב נורמטיבי (taxable base = 70% of revenue).
    //   * Operating KPIs strip: outstanding aging buckets, avg days to
    //     payment, top-customer concentration, avg monthly profit, runway.
    //   * Loan summary card (visible only when Loans.LoanCount > 0).
    //   * CSV export (ExportCsv_Click) for top customers + expenses.
    // Performance:
    //   Reload() batches 6 SOAP ops into a single ServiceGateway.Use() call
    //   so the shared WCF channel only opens once — pre-fix this page paid
    //   six channel-opens per refresh.
    // =========================================================================
    public partial class Reports : Page
    {
        private bool _ready;

        public Reports()
        {
            InitializeComponent();
            if (!ClientSession.IsOwner) { NavigationService?.Navigate(new LogIn()); return; }
            for (int y = DateTime.Today.Year; y >= DateTime.Today.Year - 4; y--) yearCombo.Items.Add(y);
            yearCombo.SelectedIndex = 0;
            for (int m = 1; m <= 12; m++) monthCombo.Items.Add(m);
            monthCombo.SelectedIndex = DateTime.Today.Month - 1;
            _ready = true;
            Reload();
        }

        private void Filter_Changed(object s, SelectionChangedEventArgs e) { if (_ready) Reload(); }

        private void Reload()
        {
            try
            {
                int year  = (int)(yearCombo.SelectedItem ?? DateTime.Today.Year);
                int month = (int)(monthCombo.SelectedItem ?? DateTime.Today.Month);
                string cur = (curCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ILS";
                int id = LogIn.sign.Id;
                var first = new DateTime(year, month, 1);
                var last  = first.AddMonths(1).AddDays(-1);
                var yearStart = new DateTime(year, 1, 1);
                var yearEnd   = new DateTime(year, 12, 31);

                // Single channel for the whole reload — saves N-1 WCF channel
                // open/close round-trips compared with the old per-call
                // ServiceGateway.Use pattern.
                ServiceGateway.Use(c =>
                {
                    var v = c.GetVatSummary(id, year, month, cur);
                    if (v != null)
                    {
                        vatColl.Text = v.VatCollected.ToString("N0") + " " + cur;
                        vatPaid.Text = v.VatPaid.ToString("N0") + " " + cur;
                        vatDue.Text  = v.VatDue.ToString("N0") + " " + cur;
                    }

                    topCustomers.ItemsSource = c.GetTopCustomersByRevenue(id, cur);
                    breakdown.ItemsSource    = c.GetExpenseBreakdown(id, first, last, cur);

                    // P&L year + tax based on BusinessType + IsZair
                    var pl = c.GetProfitLoss(id, yearStart, yearEnd, cur);
                    if (pl != null)
                    {
                        plIncome.Text   = pl.Income.ToString("N0")   + " " + cur;
                        plExpenses.Text = pl.Expenses.ToString("N0") + " " + cur;
                        plProfit.Text   = pl.Profit.ToString("N0")   + " " + cur;

                        decimal taxableBase;
                        if (LogIn.sign != null && LogIn.sign.IsZair && pl.Income <= 122_833m)
                            taxableBase = Math.Round(pl.Income * 0.70m, 2);
                        else
                            taxableBase = pl.Profit;

                        decimal tax = ComputeIsraeliIncomeTax(taxableBase);
                        plTax.Text     = tax.ToString("N0") + " " + cur;
                        plTaxBase.Text = "Base: " + taxableBase.ToString("N0") + " " + cur;
                        plNet.Text     = (pl.Profit - tax).ToString("N0") + " " + cur;

                        string biz = LogIn.sign?.BusinessType ?? "Individual";
                        string note = biz switch
                        {
                            "Patur"  => "Osek Patur — invoices issued without VAT.",
                            "Murshe" => "Osek Murshe — 18% VAT both directions; income tax on net-of-VAT.",
                            _        => "Individual — no VAT filing.",
                        };
                        if (LogIn.sign != null && LogIn.sign.IsZair)
                            note += " · Osek Zair active — taxable base = 70% of revenue.";
                        taxNote.Text = note;
                    }

                    // KPIs
                    var k = c.GetAdvancedKpis(id, cur);
                    if (k != null)
                    {
                        kpiOutstanding.Text = k.TotalOutstanding.ToString("N0") + " " + cur;
                        kpiAging.Text       = "Current " + k.AgingCurrent.ToString("N0")
                                            + " · 1-30 " + k.Aging1To30.ToString("N0")
                                            + " · 31-60 " + k.Aging31To60.ToString("N0")
                                            + " · 61+ " + k.Aging61Plus.ToString("N0");
                        kpiDays.Text        = k.AvgDaysToPayment <= 0 ? "—" : k.AvgDaysToPayment.ToString("N1");
                        kpiOnTime.Text      = k.OnTimeRatePct <= 0 ? "" : k.OnTimeRatePct.ToString("N1") + "% on-time";
                        kpiTopShare.Text    = k.TopCustomerSharePct <= 0 ? "—" : k.TopCustomerSharePct.ToString("N1") + "%";
                        kpiTopName.Text     = k.TopCustomerName ?? "";
                        kpiMonthlyProfit.Text = k.AvgMonthlyProfit.ToString("N0") + " " + cur;
                        kpiRunway.Text      = k.RunwayMonths < 0 ? "Profitable" : k.RunwayMonths.ToString("N1") + " months";
                    }

                    // Loan summary card
                    var ls = c.GetLoanSummary(id, cur);
                    if (ls != null && ls.LoanCount > 0)
                    {
                        loanCard.Visibility = Visibility.Visible;
                        loanCount.Text     = ls.LoanCount.ToString();
                        loanRemaining.Text = ls.TotalRemaining.ToString("N0") + " " + cur;
                        loanMonthly.Text   = ls.MonthlyPaymentTotal.ToString("N0") + " " + cur;
                        loanDsr.Text       = ls.MonthlyDebtServiceRatioPct <= 0
                            ? "—"
                            : ls.MonthlyDebtServiceRatioPct.ToString("N1") + "%";
                    }
                    else
                    {
                        loanCard.Visibility = Visibility.Collapsed;
                    }
                });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        private void OpenLoans_Click(object s, RoutedEventArgs e)
            => NavigationService?.Navigate(new Loans());

        // Israeli individual income-tax brackets (2026, 7 brackets, frozen).
        private static decimal ComputeIsraeliIncomeTax(decimal annualGross)
        {
            if (annualGross <= 0) return 0m;
            var brackets = new (decimal upper, decimal rate)[]
            {
                ( 84120m,  0.10m), (120720m, 0.14m), (193800m, 0.20m),
                (269280m,  0.31m), (560280m, 0.35m), (721560m, 0.47m),
                (decimal.MaxValue, 0.50m),
            };
            decimal tax = 0, lastUpper = 0;
            foreach (var (upper, rate) in brackets)
            {
                if (annualGross <= upper) { tax += (annualGross - lastUpper) * rate; break; }
                tax += (upper - lastUpper) * rate;
                lastUpper = upper;
            }
            return Math.Round(tax, 2);
        }

        private void Back_Click(object s, RoutedEventArgs e) => NavigationService?.Navigate(new OwnerHome());

        private void ExportCsv_Click(object s, RoutedEventArgs e)
        {
            try
            {
                int year  = (int)(yearCombo.SelectedItem ?? DateTime.Today.Year);
                int month = (int)(monthCombo.SelectedItem ?? DateTime.Today.Month);
                string cur = (curCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ILS";

                var v   = ServiceGateway.Use(c => c.GetVatSummary(LogIn.sign.Id, year, month, cur));
                var top = ServiceGateway.Use(c => c.GetTopCustomersByRevenue(LogIn.sign.Id, cur));
                var first = new DateTime(year, month, 1);
                var last  = first.AddMonths(1).AddDays(-1);
                var brk = ServiceGateway.Use(c => c.GetExpenseBreakdown(LogIn.sign.Id, first, last, cur));

                var sb = new StringBuilder();
                sb.AppendLine("B-Managed report — " + year + "-" + month.ToString("D2") + " (" + cur + ")");
                sb.AppendLine();
                sb.AppendLine("VAT collected,VAT paid,VAT due,Income,Expenses");
                if (v != null)
                {
                    sb.AppendLine(string.Join(",",
                        v.VatCollected, v.VatPaid, v.VatDue, v.IncomeTotal, v.ExpensesTotal));
                }
                sb.AppendLine();
                sb.AppendLine("Top customers");
                sb.AppendLine("Customer,Invoiced,Paid");
                if (top != null)
                    foreach (var r in top)
                        sb.AppendLine(Csv(r.BusinessName) + "," + r.TotalInvoiced + "," + r.TotalPaid);
                sb.AppendLine();
                sb.AppendLine("Expense breakdown");
                sb.AppendLine("Category,Total,VAT-deductible");
                if (brk != null)
                    foreach (var r in brk)
                        sb.AppendLine(Csv(r.CategoryName) + "," + r.Total + "," + r.IsVatDeductible);

                var dlg = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = "BManaged_Report_" + year + "_" + month.ToString("D2") + ".csv"
                };
                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                    MessageBox.Show("Saved.\n" + dlg.FileName, "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string Csv(string s)
        {
            if (s == null) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
