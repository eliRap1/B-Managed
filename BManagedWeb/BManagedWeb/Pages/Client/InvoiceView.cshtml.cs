using System.Collections.Generic;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Client
{
    public class InvoiceViewModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();
        public Invoice Invoice { get; set; }
        public List<InvoiceLine> Lines { get; set; } = new();

        public IActionResult OnGet(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Client") return RedirectToPage("/Login");
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            Invoice = _srv.GetInvoiceById(id);
            // IDOR guard: only allow access when the invoice's customerId matches this
            // Client's userId (the existing convention used by Portal.cshtml.cs).
            // TODO(audit): when a proper User↔Customer FK is added, replace this
            // comparison with a customer-ownership check instead.
            if (Invoice == null || Invoice.CustomerId != userId)
                return RedirectToPage("/Client/Portal");
            Lines = (_srv.GetInvoiceLines(id) ?? new InvoiceLine[0]).ToList();
            return Page();
        }
    }
}
