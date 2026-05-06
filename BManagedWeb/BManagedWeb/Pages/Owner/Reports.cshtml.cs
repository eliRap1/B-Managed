using System;
using System.Collections.Generic;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BManagedWeb.Pages.Owner
{
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
        public decimal    YearTax   { get; set; }
        public decimal    YearNet   { get; set; }
        public string     BusinessType { get; set; } = "Individual";
        public decimal    TaxableProfit { get; set; }
        public string     TaxNote    { get; set; }

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
                Vat = _srv.GetVatSummary(id, Year, Month, DisplayCurrency) ?? new VatSummary();
                var tc = _srv.GetTopCustomersByRevenue(id, DisplayCurrency);
                if (tc != null) TopCustomers = new List<CustomerRevenueRow>(tc);
                var first = new DateTime(Year, Month, 1);
                var last  = first.AddMonths(1).AddDays(-1);
                var bd = _srv.GetExpenseBreakdown(id, first, last, DisplayCurrency);
                if (bd != null) ExpenseBreakdown = new List<ExpenseBreakdownRow>(bd);

                // P&L — month + full year (year is needed for progressive tax bracket calc)
                MonthPl = _srv.GetProfitLoss(id, first, last, DisplayCurrency) ?? new ProfitLoss();
                var yearStart = new DateTime(Year, 1, 1);
                var yearEnd   = new DateTime(Year, 12, 31);
                YearPl = _srv.GetProfitLoss(id, yearStart, yearEnd, DisplayCurrency) ?? new ProfitLoss();

                // BusinessType-aware taxable profit:
                //   Osek Zair / Patur: presumptive 30% of income treated as expenses for
                //                       income-tax purposes (חישוב נורמטיבי). No VAT either way.
                //   Osek Murshe / Individual: real expenses count.
                try
                {
                    var u = _srv.GetUserById(id);
                    if (u != null && !string.IsNullOrEmpty(u.BusinessType)) BusinessType = u.BusinessType;
                }
                catch { }

                if (BusinessType == "Zair" || BusinessType == "Patur")
                {
                    TaxableProfit = YearPl.Income * 0.70m;
                    TaxNote = "Osek " + (BusinessType == "Zair" ? "Zair" : "Patur") +
                              " — 30 % presumptive expense deduction on income (independent of actual expenses). " +
                              "Invoices issued without 17 % VAT. VAT on expenses cannot be deducted.";
                }
                else
                {
                    TaxableProfit = YearPl.Profit;
                    TaxNote = BusinessType == "Murshe"
                        ? "Osek Murshe — collects 17 % VAT on sales, deducts 17 % on qualifying expenses, taxable profit = real income − real expenses."
                        : "Individual — taxable profit = income − expenses, no VAT filing.";
                }

                YearTax = ComputeIsraeliIncomeTax(TaxableProfit);
                YearNet = YearPl.Profit - YearTax;
            }
            catch { }
            return Page();
        }

        // Israeli individual income-tax brackets (annual). Approximated 2025 levels.
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

        // Records the periodic VAT settlement as a single expense row tagged
        // 'VAT payment to Tax Authority'. This zeroes the VAT-due figure for
        // the next GetVatSummary call (since the new expense has VatPaid set).
        public IActionResult OnPostPayVat()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            var cur = string.IsNullOrEmpty(DisplayCurrency) ? "ILS" : DisplayCurrency;
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
