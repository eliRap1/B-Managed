using System;
using System.Collections.Generic;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Owner
{
    // =========================================================================
    // LoansModel — /Owner/Loans (Owner role only).
    // -------------------------------------------------------------------------
    // Owns three responsibilities:
    //   1. Display: pulls every Loan + per-loan PaymentHistory + LoanSummary
    //      KPI roll-up in OnGet, all converted to DisplayCurrency.
    //   2. CRUD: add a loan, record a payment, delete a loan. Each is a
    //      handler method (OnPostAdd / OnPostPay / OnPostDelete).
    //   3. Domain logic: state-backed (Keren) loans get a flag, monthly
    //      debt-service ratio drives a coloured warning banner at >=25%
    //      and >=40% (industry thresholds for 'watch' and 'high').
    // SOAP ops touched:
    //   GetLoansForOwner, GetLoanSummary, GetLoanPayments,
    //   AddLoan, RecordLoanPayment, DeleteLoan.
    // Used by the Reports page (Loan summary card) and Owner home (tile).
    // =========================================================================
    /// <summary>
    /// Loans page — track principal (קרן), monthly payment, debt-to-income.
    /// Highlights state-backed (Keren) loans separately.
    /// </summary>
    public class LoansModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();

        [BindProperty(SupportsGet = true)] public string DisplayCurrency { get; set; } = "ILS";

        [BindProperty] public string  NewLender { get; set; }
        [BindProperty] public decimal NewPrincipal { get; set; }
        [BindProperty] public double  NewInterestRate { get; set; }
        [BindProperty] public decimal NewMonthlyPayment { get; set; }
        [BindProperty] public int     NewTermMonths { get; set; }
        [BindProperty] public DateTime NewStartDate { get; set; } = DateTime.Today;
        [BindProperty] public string  NewCurrency { get; set; } = "ILS";
        [BindProperty] public string  NewPurpose { get; set; }
        [BindProperty] public bool    NewIsKerenBacked { get; set; }
        [BindProperty] public bool    NewHasStandingOrder { get; set; }

        public List<Loan> Loans { get; set; } = new();
        public LoanSummary Summary { get; set; } = new LoanSummary();
        public Dictionary<int, List<LoanPayment>> PaymentHistory { get; set; } = new();

        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (string.IsNullOrEmpty(DisplayCurrency)) DisplayCurrency = "ILS";

            try
            {
                Loans = (_srv.GetLoansForOwner(id) ?? new Loan[0]).ToList();
                Summary = _srv.GetLoanSummary(id, DisplayCurrency) ?? new LoanSummary();
                foreach (var l in Loans)
                {
                    var hist = _srv.GetLoanPayments(l.Id);
                    if (hist != null) PaymentHistory[l.Id] = hist.ToList();
                }
            }
            catch (Exception ex) { TempData["LoanMsg"] = "Load failed: " + ex.Message; }

            NewCurrency = DisplayCurrency;
            return Page();
        }

        public IActionResult OnPostAdd()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;

            if (string.IsNullOrWhiteSpace(NewLender) || NewPrincipal <= 0)
            { TempData["LoanMsg"] = "Lender + principal are required."; return RedirectToPage(); }
            if (NewMonthlyPayment <= 0)
            { TempData["LoanMsg"] = "Monthly payment must be greater than zero."; return RedirectToPage(); }

            try
            {
                _srv.AddLoan(new Loan
                {
                    OwnerId          = id,
                    Lender           = NewLender,
                    Principal        = NewPrincipal,
                    RemainingBalance = NewPrincipal,
                    InterestRatePct  = NewInterestRate,
                    MonthlyPayment   = NewMonthlyPayment,
                    StartDate        = NewStartDate == default ? DateTime.Today : NewStartDate,
                    TermMonths       = NewTermMonths,
                    NextPaymentDate  = (NewStartDate == default ? DateTime.Today : NewStartDate).AddMonths(1),
                    Currency         = NewCurrency ?? "ILS",
                    Purpose          = NewPurpose,
                    IsKerenBacked    = NewIsKerenBacked,
                    HasStandingOrder = NewHasStandingOrder,
                    IsActive         = true,
                    CreatedAt        = DateTime.Now,
                });
                TempData["LoanMsg"] = "Loan added.";
            }
            catch (Exception ex) { TempData["LoanMsg"] = "Failed: " + ex.Message; }
            return RedirectToPage(new { DisplayCurrency });
        }

        public IActionResult OnPostPay(int loanId, DateTime paidDate, decimal amount, decimal principalPortion)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            if (loanId <= 0 || amount <= 0)
            { TempData["LoanMsg"] = "Invalid payment."; return RedirectToPage(); }

            try
            {
                decimal pp = principalPortion > 0 ? principalPortion : Math.Round(amount * 0.7m, 2);
                if (pp > amount) pp = amount;
                _srv.RecordLoanPayment(new LoanPayment
                {
                    LoanId           = loanId,
                    PaidDate         = paidDate == default ? DateTime.Today : paidDate,
                    Amount           = amount,
                    PrincipalPortion = pp,
                    InterestPortion  = amount - pp,
                });
                TempData["LoanMsg"] = $"Recorded {amount:N2} payment.";
            }
            catch (Exception ex) { TempData["LoanMsg"] = "Failed: " + ex.Message; }
            return RedirectToPage(new { DisplayCurrency });
        }

        public IActionResult OnPostDelete(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            // TODO(audit): DeleteLoan does not verify the loan belongs to this Owner.
            // An Owner could delete another company's loan by guessing its id (IDOR).
            // Add a DeleteLoanForOwner(id, ownerId) WCF method that checks
            // LoanDB.GetById(id)?.OwnerId == ownerId before deleting. Touches
            // IService1, Service1, LoanLogic, LoanDB — deferred per audit rules.
            try { _srv.DeleteLoan(id); TempData["LoanMsg"] = "Loan deleted."; }
            catch (Exception ex) { TempData["LoanMsg"] = "Failed: " + ex.Message; }
            return RedirectToPage(new { DisplayCurrency });
        }
    }
}
