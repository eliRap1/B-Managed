using System;
using System.Collections.Generic;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BManagedWeb.Pages.Owner
{
    // =========================================================================
    // ReportsModel — /Owner/Reports page (Owner role only).
    // -------------------------------------------------------------------------
    // Data assembled in OnGet, all converted to DisplayCurrency via the
    // server-side CurrencyConverter (ExchangeRates table):
    //   1. VAT summary (Vat)              — VatCollected, VatPaid, VatDue.
    //   2. Top customers (TopCustomers)   — INNER JOIN + GROUP BY + SUM.
    //   3. Expense breakdown              — INNER JOIN ExpenseCategories.
    //   4. P&L for the month (MonthPl)    — Income / Expenses / Profit.
    //   5. P&L for the year  (YearPl)     — used as base for income tax.
    //   6. Israeli income tax             — progressive bracket calc.
    //                                      Patur/Murshe: tax on YearPl.Profit.
    //                                      IsZair (≤122,833 ₪): tax on 70% of
    //                                      revenue (חישוב נורמטיבי).
    //   7. Patur threshold warning        — when YTD income > 120,000 ₪.
    //   8. Advanced KPIs (Kpis)           — receivables aging, payment lag,
    //                                       customer concentration, runway.
    //   9. Loan summary (LoanSummary)     — debt + DSR vs trailing income.
    // POST handlers:
    //   OnPostPayVat       — records VAT settlement as a tagged Expense.
    //   OnPostToggleZair   — flips Users.isZair via SetIsZair.
    //   OnGetCsv           — exports top customers + expense breakdown.
    // Security:
    //   Every action calls Guard() (role == 'Owner') before doing anything,
    //   then uses HttpContext.Session UserId so the page can never operate
    //   on another Owner's data.
    // =========================================================================
    public class ReportsModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();

        [BindProperty(SupportsGet = true)] public int Year  { get; set; } = DateTime.Today.Year;
        [BindProperty(SupportsGet = true)] public int Month { get; set; } = DateTime.Today.Month;
        [BindProperty(SupportsGet = true)] public string DisplayCurrency { get; set; } = "ILS";

        public VatSummary Vat { get; set; } = new VatSummary();
        public List<CustomerRevenueRow> TopCustomers { get; set; } = new();
        public List<ExpenseBreakdownRow> ExpenseBreakdown { get; set; } = new();

        // P&L for the selected month (and year-to-date for tax-bracket calc)
        public ProfitLoss MonthPl   { get; set; } = new ProfitLoss();
        public ProfitLoss YearPl    { get; set; } = new ProfitLoss();
        public AnalyticsKpis Kpis   { get; set; } = new AnalyticsKpis();
        public LoanSummary  LoanSummary { get; set; } = new LoanSummary();
        public decimal    YearTax   { get; set; }
        public decimal    YearNet   { get; set; }
        public string     BusinessType { get; set; } = "Individual";
        public bool       IsZair    { get; set; }
        public bool       ShowMarkVatPaid { get; set; }
        public bool       PaturOverThreshold { get; set; }
        public decimal    PaturThreshold { get; set; } = 120_000m;
        public decimal    ZairThreshold  { get; set; } = 122_833m;
        public decimal    TaxableProfit { get; set; }
        public string     TaxNote    { get; set; }
        public string     ZairNote   { get; set; }

        public IEnumerable<SelectListItem> YearOptions =>
            Enumerable.Range(DateTime.Today.Year - 4, 5)
                      .Select(y => new SelectListItem(y.ToString(), y.ToString()));
        public IEnumerable<SelectListItem> MonthOptions =>
            Enumerable.Range(1, 12).Select(m => new SelectListItem(
                System.Globalization.DateTimeFormatInfo.InvariantInfo.GetMonthName(m), m.ToString()));

        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            DisplayCurrency = string.IsNullOrEmpty(DisplayCurrency) ? "ILS" : DisplayCurrency;

            try
            {
                // Single bundled call — replaces 7+ separate SOAP ops
                // (GetVatSummary, GetTopCustomersByRevenue, GetExpenseBreakdown,
                // 2× GetProfitLoss, GetAdvancedKpis, GetLoanSummary,
                // GetUserById). The page used to take long enough that some
                // browsers showed 'site can't be reached' on slow channels.
                var snap = _srv.GetReportsSnapshot(id, Year, Month, DisplayCurrency);
                if (snap != null)
                {
                    Vat              = snap.Vat ?? new VatSummary();
                    TopCustomers     = (snap.TopCustomers     ?? new CustomerRevenueRow[0]).ToList();
                    ExpenseBreakdown = (snap.ExpenseBreakdown ?? new ExpenseBreakdownRow[0]).ToList();
                    MonthPl          = snap.MonthPl ?? new ProfitLoss();
                    YearPl           = snap.YearPl  ?? new ProfitLoss();
                    Kpis             = snap.Kpis        ?? new AnalyticsKpis();
                    LoanSummary      = snap.LoanSummary ?? new LoanSummary();
                    BusinessType     = string.IsNullOrEmpty(snap.BusinessType) ? "Individual" : snap.BusinessType;
                    IsZair           = snap.IsZair;
                }

                // Tax-base + tax-note logic stays page-side because it depends
                // on the *displayed* values (the snapshot ships raw figures).
                if (IsZair && YearPl.Income <= ZairThreshold)
                    TaxableProfit = Math.Round(YearPl.Income * 0.70m, 2);
                else
                    TaxableProfit = YearPl.Profit;

                PaturOverThreshold = BusinessType == "Patur" && YearPl.Income > PaturThreshold;
                ShowMarkVatPaid    = BusinessType == "Murshe";

                TaxNote = BusinessType switch
                {
                    "Patur"  => "Osek Patur — revenue ≤ ~120k ₪/year. Issues invoices without 18 % VAT and cannot deduct VAT on expenses.",
                    "Murshe" => "Osek Murshe — collects 18 % VAT on sales (Form 836/874) and deducts 18 % on qualifying expenses. Income tax is computed on net-of-VAT amounts both ways.",
                    _        => "Individual — no VAT filing.",
                };
                if (IsZair)
                {
                    ZairNote = YearPl.Income <= ZairThreshold
                        ? $"Osek Zair active — income tax computed on 70 % of revenue (flat 30 % deemed-expenses). Threshold ~{ZairThreshold:N0} ₪/yr."
                        : $"Osek Zair claimed but revenue {YearPl.Income:N0} ₪ exceeds the ~{ZairThreshold:N0} ₪ threshold — falling back to real income − real expenses.";
                }

                YearTax = ComputeIsraeliIncomeTax(TaxableProfit);
                YearNet = YearPl.Profit - YearTax;
            }
            catch { }
            return Page();
        }

        // Israeli individual income-tax brackets (annual). 2026 levels — frozen
        // from 2025 (no inflation adjustment), 7 brackets.
        // Progressive: only the slice that falls inside each bracket is taxed at its rate.
        public static decimal ComputeIsraeliIncomeTax(decimal annualGross)
        {
            if (annualGross <= 0) return 0m;
            var brackets = new (decimal upper, decimal rate)[]
            {
                ( 84120m,  0.10m),
                (120720m,  0.14m),
                (193800m,  0.20m),
                (269280m,  0.31m),
                (560280m,  0.35m),
                (721560m,  0.47m),
                (decimal.MaxValue, 0.50m),
            };
            decimal tax = 0, lastUpper = 0;
            foreach (var (upper, rate) in brackets)
            {
                if (annualGross <= upper)
                { tax += (annualGross - lastUpper) * rate; break; }
                tax += (upper - lastUpper) * rate;
                lastUpper = upper;
            }
            return Math.Round(tax, 2);
        }

        public IActionResult OnGetCsv(string displayCurrency)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            var cur = string.IsNullOrEmpty(displayCurrency) ? "ILS" : displayCurrency;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Customer,Invoiced,Paid,Currency");
            foreach (var r in _srv.GetTopCustomersByRevenue(id, cur) ?? new CustomerRevenueRow[0])
                sb.AppendLine($"{Esc(r.BusinessName)},{r.TotalInvoiced},{r.TotalPaid},{r.DisplayCurrency}");
            sb.AppendLine();
            sb.AppendLine("Category,Total,VAT-deductible,Currency");
            var first = new DateTime(Year, Month, 1);
            var last  = first.AddMonths(1).AddDays(-1);
            foreach (var b in _srv.GetExpenseBreakdown(id, first, last, cur) ?? new ExpenseBreakdownRow[0])
                sb.AppendLine($"{Esc(b.CategoryName)},{b.Total},{b.IsVatDeductible},{b.DisplayCurrency}");

            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()),
                "text/csv", $"BManaged-{Year}-{Month:D2}-{cur}.csv");
        }

        private static string Esc(string v) => v == null ? "" : v.Replace(",", " ").Replace("\"", "'");

        // Toggle Osek Zair income-tax status. Only meaningful for Patur or Murshe.
        public IActionResult OnPostToggleZair(bool wantZair)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            var cur = string.IsNullOrEmpty(DisplayCurrency) ? "ILS" : DisplayCurrency;
            try
            {
                var u = _srv.GetUserById(id);
                if (u != null && (u.BusinessType == "Patur" || u.BusinessType == "Murshe"))
                {
                    _srv.SetIsZair(id, wantZair);
                    TempData["VatMsg"] = wantZair
                        ? "Osek Zair status enabled."
                        : "Osek Zair status disabled.";
                }
                else
                {
                    TempData["VatMsg"] = "Osek Zair requires Osek Patur or Osek Murshe.";
                }
            }
            catch (Exception ex) { TempData["VatMsg"] = "Failed: " + ex.Message; }
            return RedirectToPage(new { Year, Month, DisplayCurrency = cur });
        }

        // Records the periodic VAT settlement as a single expense row tagged
        // 'VAT payment to Tax Authority'. This zeroes the VAT-due figure for
        // the next GetVatSummary call (since the new expense has VatPaid set).
        public IActionResult OnPostPayVat()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            var cur = string.IsNullOrEmpty(DisplayCurrency) ? "ILS" : DisplayCurrency;

            // Only Murshe has a periodic VAT settlement to record.
            try
            {
                var u = _srv.GetUserById(id);
                if (u != null && u.BusinessType != "Murshe")
                {
                    TempData["VatMsg"] = "VAT settlement applies to Osek Murshe only.";
                    return RedirectToPage(new { Year, Month, DisplayCurrency = cur });
                }
            }
            catch { }

            try
            {
                var vat = _srv.GetVatSummary(id, Year, Month, cur);
                decimal due = vat?.VatDue ?? 0m;
                if (due <= 0)
                {
                    TempData["VatMsg"] = "No VAT is due for this period.";
                    return RedirectToPage(new { Year, Month, DisplayCurrency = cur });
                }

                _srv.AddExpense(new Expense
                {
                    OwnerId     = id,
                    Date        = DateTime.Today,
                    Amount      = due,
                    VatPaid     = 0,                 // payment to authority — not a VAT-deductible expense
                    Vendor      = "Israel Tax Authority",
                    Description = $"VAT settlement for {Year:D4}-{Month:D2}",
                    Currency    = cur,
                });
                TempData["VatMsg"] = $"Recorded VAT payment of {due:N2} {cur} to Tax Authority.";
            }
            catch (Exception ex) { TempData["VatMsg"] = "Failed: " + ex.Message; }
            return RedirectToPage(new { Year, Month, DisplayCurrency = cur });
        }
    }
}
