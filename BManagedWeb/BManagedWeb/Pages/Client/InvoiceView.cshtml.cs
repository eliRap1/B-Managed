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

            // SECURITY: Verify the invoice belongs to this Client before loading it.
            // Previously GetInvoiceById(id) was called without any ownership scope,
            // allowing any authenticated Client to read any invoice by iterating IDs (IDOR).
            // We now fetch all invoices for this client and match by id so a Client can
            // only ever see their own invoices.
            int clientId = HttpContext.Session.GetInt32("UserId") ?? 0;
            try
            {
                var owned = _srv.GetInvoicesByCustomer(clientId);
                Invoice = owned?.FirstOrDefault(i => i.Id == id);
            }
            catch { }

            if (Invoice == null) return RedirectToPage("/Client/Portal");
            try { Lines = (_srv.GetInvoiceLines(id) ?? new InvoiceLine[0]).ToList(); } catch { }
            return Page();
        }
    }
}
