using System;
using System.Collections.Generic;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Owner
{
    // =========================================================================
    // HomeModel — /Owner/Home (Owner dashboard).
    // -------------------------------------------------------------------------
    // First page after Owner login. Pulls a snapshot of every "is-the-business
    // healthy?" signal in one OnGet pass, all in DisplayCurrency:
    //   * CustomersCount / ActiveProjects / UnpaidCount / OverdueCount —
    //     simple counters off the per-Owner queries.
    //   * UnpaidTotal — sum of open invoices (status != Paid).
    //   * VatDue + TaxSetAside — month-scoped via VatSummary +
    //     MonthlyTaxSetAside.
    //   * RecentInvoices — last 6 invoices across customers (capped).
    //   * Forecast — 3-month cashflow projection.
    //   * Smart insights:
    //       - top expense category this month;
    //       - revenue vs prior-month % change;
    //       - account-type info pill (Patur/Zair).
    //   * Auto-creates overdue notifications via EnsureOverdueNotifications
    //     (idempotent server-side — checks for existing notif first).
    //   * Kpis (AnalyticsKpis) — receivables aging + on-time rate +
    //     concentration; rendered as bento tiles.
    //   * Loans (LoanSummary) — outstanding principal + DSR; bento tile.
    // JSON helpers:
    //   OnGetSparkline — 6-month profit per month (used by Chart.js).
    //   OnGetStats     — lightweight polling endpoint (Unpaid / Overdue /
    //                    ActiveProjects) for the live counter refresh.
    // =========================================================================
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

        public List<BManagedWeb.bsrv.RecentInvoice> RecentInvoices { get; set; } = new();
        public List<ProfitLoss> Forecast { get; set; } = new();

        public AnalyticsKpis Kpis  { get; set; } = new AnalyticsKpis();
        public LoanSummary  Loans  { get; set; } = new LoanSummary();

        // RecentInvoice is now a server-side DTO defined in bsrv.Reference.cs
        // (DataContract round-tripped from Model.RecentInvoice).

        public string  TopExpenseCategory { get; set; }
        public decimal TopExpenseAmount   { get; set; }
        public decimal RevenueChangePct   { get; set; }
        public bool    HasInsights        { get; set; }
        public string  BusinessType       { get; set; } = "Individual";
        public bool    IsZair             { get; set; } = false;

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
                if (u != null)
                    IsZair = u.IsZair;
            }
            catch { }

            try
            {
                // Single SOAP round-trip — server bundles every counter, KPI,
                // P&L derivative, top-expense, recent-invoices preview into
                // one OwnerDashboardSnapshot. Pre-May-2026 this OnGet ran
                // 12+ separate SOAP ops and felt sluggish on every dashboard
                // load.
                var snap = _srv.GetOwnerDashboardSnapshot(id, Currency);
                if (snap != null)
                {
                    CustomersCount     = snap.CustomersCount;
                    UnpaidCount        = snap.UnpaidCount;
                    OverdueCount       = snap.OverdueCount;
                    ActiveProjects     = snap.ActiveProjectsCount;
                    UnpaidTotal        = snap.UnpaidTotal;
                    VatDue             = snap.VatDue;
                    TaxSetAside        = snap.MonthlyTaxSetAside;
                    TopExpenseCategory = snap.TopExpenseCategory;
                    TopExpenseAmount   = snap.TopExpenseAmount;
                    RevenueChangePct   = snap.RevenueChangePct;
                    if (snap.CashFlowForecast != null) Forecast = snap.CashFlowForecast.ToList();
                    if (snap.RecentInvoices  != null) RecentInvoices = snap.RecentInvoices.ToList();
                    Kpis  = snap.Kpis  ?? new AnalyticsKpis();
                    Loans = snap.LoanSummary ?? new LoanSummary();
                }
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
                var currency = HttpContext.Session.GetString("Currency") ?? "ILS";
                var snapshot = _srv.GetOwnerDashboardSnapshot(id, currency);
                return new JsonResult(new
                {
                    UnpaidCount    = snapshot?.UnpaidCount ?? 0,
                    OverdueCount   = snapshot?.OverdueCount ?? 0,
                    ActiveProjects = snapshot?.ActiveProjectsCount ?? 0,
                });
            }
            catch { return new JsonResult(new { }); }
        }
    }
}
