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
            }
            catch { }
            return Page();
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
