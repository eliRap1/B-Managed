using System.Collections.Generic;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Client
{
    // TODO(audit): invoice is fetched without verifying it belongs to the current client's owner; IDOR vulnerability.
    public class InvoiceViewModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();
        public Invoice Invoice { get; set; }
        public List<InvoiceLine> Lines { get; set; } = new();

        public IActionResult OnGet(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Client") return RedirectToPage("/Login");
            Invoice = _srv.GetInvoiceById(id);
            if (Invoice != null) Lines = (_srv.GetInvoiceLines(id) ?? new InvoiceLine[0]).ToList();
            return Page();
        }
    }
}
