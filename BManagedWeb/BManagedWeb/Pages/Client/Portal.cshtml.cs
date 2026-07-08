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
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            Username = HttpContext.Session.GetString("Username") ?? "";
            try
            {
                // Resolve the Customer row via the logged-in user's email so the
                // correct Customer.id (an independent auto-increment PK) is used
                // instead of User.id, which only coincidentally matches in the
                // seeded demo data and diverges for any real deployment.
                var me = _srv.GetUserById(userId);
                if (me?.OwnerId != null)
                {
                    var customers = _srv.GetCustomersForOwner(me.OwnerId.Value) ?? new Customer[0];
                    var customer = customers.FirstOrDefault(c => c.Email == me.Email);
                    if (customer != null)
                    {
                        var list = _srv.GetInvoicesByCustomer(customer.Id);
                        if (list != null) Invoices = list.OrderByDescending(i => i.IssueDate).ToList();
                    }
                }
            }
            catch { }
            return Page();
        }
    }
}
