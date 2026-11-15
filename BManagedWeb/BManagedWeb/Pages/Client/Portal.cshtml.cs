using System.Collections.Generic;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Client
{
    public class PortalModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();

        public string Username { get; set; }
        public List<Invoice> Invoices { get; set; } = new();

        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Client") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            Username = HttpContext.Session.GetString("Username") ?? "";
            try
            {
                // TODO(audit): GetInvoicesByCustomer expects a customerId (Customers.id),
                // but `id` here is a userId (Users.id). These are from separate
                // auto-increment sequences and will not match after the first few rows.
                // In the seed data: acme user has id=3, Acme Studios customer has id=1 —
                // so this call always returns an empty list for the seeded demo Client.
                // Fix requires either: (a) adding a [userId] FK to the Customers table
                // and querying by that, or (b) looking up the Customer by the Client's
                // email, then fetching invoices by that customerId. See InvoiceView.cshtml.cs
                // for the related IDOR guard that relies on the same convention.
                var list = _srv.GetInvoicesByCustomer(id);
                if (list != null) Invoices = list.OrderByDescending(i => i.IssueDate).ToList();
            }
            catch { }
            return Page();
        }
    }
}
