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
            try
            {
                // Resolve this Client's Customer record by email so the independent
                // auto-increment Customer.Id (not User.Id) is used for the ownership check.
                var me = _srv.GetUserById(userId);
                if (me?.OwnerId == null) return Page();
                var customers = _srv.GetCustomersForOwner(me.OwnerId.Value) ?? new Customer[0];
                var customer = customers.FirstOrDefault(c => c.Email == me.Email);
                if (customer == null) return Page();

                Invoice = _srv.GetInvoiceById(id);
                if (Invoice == null || Invoice.CustomerId != customer.Id)
                {
                    Invoice = null;
                    return Page();
                }
                Lines = (_srv.GetInvoiceLines(id) ?? new InvoiceLine[0]).ToList();
            }
            catch { }
            return Page();
        }
    }
}
