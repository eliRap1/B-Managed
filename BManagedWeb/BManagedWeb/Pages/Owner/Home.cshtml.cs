using System;
using System.Collections.Generic;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Owner
{
    public class HomeModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();

        public string Username { get; set; }
        public string Currency { get; set; } = "ILS";
        public string TodayLabel => DateTime.Today.ToString("MMM yyyy");

        public int CustomersCount  { get; set; }
        public int ActiveProjects  { get; set; }
        public int UnpaidCount     { get; set; }
        public int OverdueCount    { get; set; }
        public decimal UnpaidTotal { get; set; }
        public string UnpaidTotalDisplay => UnpaidTotal.ToString("N0");

        public decimal VatDue { get; set; }
        public string VatDueDisplay => VatDue.ToString("N0");
        public decimal TaxSetAside { get; set; }
        public string TaxSetAsideDisplay => TaxSetAside.ToString("N0");

        public List<RecentInvoice> RecentInvoices { get; set; } = new();

        public class RecentInvoice
        {
            public string InvoiceNumber { get; set; }
            public string CustomerName  { get; set; }
            public decimal Total        { get; set; }
            public string Currency      { get; set; }
            public string Status        { get; set; }
        }

        public string  TopExpenseCategory { get; set; }
        public decimal TopExpenseAmount   { get; set; }
        public decimal RevenueChangePct   { get; set; }
        public bool    HasInsights        { get; set; }
        public string  BusinessType       { get; set; } = "Individual";

        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            Username = HttpContext.Session.GetString("Username") ?? "";
            Currency = HttpContext.Session.GetString("Currency") ?? "ILS";

            try
            {
                var u = _srv.GetUserById(id);
                if (u != null && !string.IsNullOrEmpty(u.BusinessType))
                    BusinessType = u.BusinessType;
            }
            catch { }

            try
            {
                var customers = _srv.GetCustomersForOwner(id);
                CustomersCount = customers?.Length ?? 0;

                var unpaid = _srv.GetUnpaidInvoices(id);
                UnpaidCount  = unpaid?.Length ?? 0;
                UnpaidTotal  = unpaid?.Sum(i => i.Total) ?? 0m;

                var overdue = _srv.GetOverdueInvoices(id);
                OverdueCount = overdue?.Length ?? 0;

                var active = _srv.GetProjectsByStatus("Active", id);
                ActiveProjects = active?.Length ?? 0;

                var now = DateTime.Today;
                var vat = _srv.GetVatSummary(id, now.Year, now.Month, Currency);
                if (vat != null) { VatDue = vat.VatDue; }
                TaxSetAside = _srv.GetMonthlyTaxSetAside(id, now.Year, now.Month, Currency);

                // recent 6 invoices across customers (cap)
                var custLookup = customers.ToDictionary(c => c.Id, c => c.BusinessName);
                foreach (var c in customers.Take(6))
                {
                    var ix = _srv.GetInvoicesByCustomer(c.Id);
                    foreach (var i in ix.Take(3))
                    {
                        RecentInvoices.Add(new RecentInvoice
                        {
                            InvoiceNumber = i.InvoiceNumber,
                            CustomerName  = c.BusinessName,
                            Total         = i.Total,
                            Currency      = i.Currency,
                            Status        = i.Status,
                        });
                    }
                }
                RecentInvoices = RecentInvoices.OrderByDescending(x => x.InvoiceNumber).Take(6).ToList();

                // Smart insight #1: biggest expense category this month
                var first = new DateTime(now.Year, now.Month, 1);
                var last  = first.AddMonths(1).AddDays(-1);
                var brk = _srv.GetExpenseBreakdown(id, first, last, Currency);
                if (brk != null && brk.Length > 0)
                {
                    var top = brk.OrderByDescending(b => b.Total).First();
                    TopExpenseCategory = top.CategoryName;
                    TopExpenseAmount   = top.Total;
                }

                // Smart insight #2: revenue change vs previous month
                var prevFirst = first.AddMonths(-1);
                var prevLast  = first.AddDays(-1);
                var thisPl = _srv.GetProfitLoss(id, first, last, Currency);
                var prevPl = _srv.GetProfitLoss(id, prevFirst, prevLast, Currency);
                if (thisPl != null && prevPl != null && prevPl.Income > 0)
                    RevenueChangePct = Math.Round(((thisPl.Income - prevPl.Income) / prevPl.Income) * 100m, 1);

                HasInsights = !string.IsNullOrEmpty(TopExpenseCategory) || RevenueChangePct != 0;
            }
            catch { }
            return Page();
        }

        // Sparkline endpoint — last 6 months of profit (revenue - expenses) per month.
        public IActionResult OnGetSparkline()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return new JsonResult(new { });
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            var currency = HttpContext.Session.GetString("Currency") ?? "ILS";

            var labels = new List<string>();
            var data = new List<decimal>();
            try
            {
                var anchor = DateTime.Today;
                for (int i = 5; i >= 0; i--)
                {
                    var d = anchor.AddMonths(-i);
                    var first = new DateTime(d.Year, d.Month, 1);
                    var last  = first.AddMonths(1).AddDays(-1);
                    var pl = _srv.GetProfitLoss(id, first, last, currency);
                    labels.Add(first.ToString("MMM"));
                    data.Add(pl != null ? pl.Profit : 0m);
                }
            }
            catch { }
            return new JsonResult(new { labels, data, currency });
        }

        // Polling endpoint — returns lightweight JSON for live counter refresh.
        public IActionResult OnGetStats()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return new JsonResult(new { });
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            try
            {
                var unpaid  = _srv.GetUnpaidInvoices(id);
                var overdue = _srv.GetOverdueInvoices(id);
                var active  = _srv.GetProjectsByStatus("Active", id);
                return new JsonResult(new
                {
                    UnpaidCount    = unpaid?.Length    ?? 0,
                    OverdueCount   = overdue?.Length   ?? 0,
                    ActiveProjects = active?.Length    ?? 0,
                });
            }
            catch { return new JsonResult(new { }); }
        }
    }
}
