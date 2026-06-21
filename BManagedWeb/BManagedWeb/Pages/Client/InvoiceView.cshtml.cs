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
            int sessionUserId = HttpContext.Session.GetInt32("UserId") ?? 0;
            Invoice = _srv.GetInvoiceById(id);
            // Guard: only show invoices whose customerId matches the logged-in
            // client's session UserId. Without this check any authenticated Client
            // could view any other client's invoice by changing the id in the URL
            // (IDOR — insecure direct object reference).
            if (Invoice != null && Invoice.CustomerId != sessionUserId)
                Invoice = null;
            if (Invoice != null) Lines = (_srv.GetInvoiceLines(id) ?? new InvoiceLine[0]).ToList();
            return Page();
        }
    }
}
