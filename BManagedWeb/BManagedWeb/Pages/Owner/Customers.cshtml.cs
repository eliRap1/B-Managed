using System.Collections.Generic;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Owner
{
    // =========================================================================
    // CustomersModel — /Owner/Customers (Owner role only).
    // -------------------------------------------------------------------------
    // Surfaces the per-Owner CRM list. Two layers:
    //   * Search bar + add-customer form (top of page).
    //   * Quick-view modal (HTML <dialog>-style, hidden until clicked) with
    //     outstanding balance, lifetime paid, recent invoices, projects, and
    //     an inline edit / delete form.
    // Handlers:
    //   OnGet          — list + search via SearchCustomers / GetCustomersForOwner.
    //   OnPostAdd      — server-side validation (HTML form validation already
    //                    caught the obvious cases) + AddCustomer.
    //   OnPostUpdate   — modal Save button → UpdateCustomer.
    //   OnPostDelete   — modal Delete button → DeleteCustomer (linked
    //                    invoices/projects keep their customerId but the row
    //                    is gone, so they show as 'unlinked' until cleanup).
    //   OnGetDetail    — JSON handler hit by the modal opener; returns
    //                    customer + outstanding/lifetime + last invoices +
    //                    projects in one round-trip so the modal renders fast.
    //   OnGetCsv       — exports the current owner's customer book as CSV.
    // Tenant scoping:
    //   GetCustomersForOwner / SearchCustomers always pass the session
    //   UserId so company A never sees company B's customers.
    // =========================================================================
    public class CustomersModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();

        [BindProperty(SupportsGet = true)] public string Q { get; set; }

        [BindProperty] public string NewBusinessName { get; set; }
        [BindProperty] public string NewEmail { get; set; }
        [BindProperty] public string NewPhone { get; set; }
        [BindProperty] public string NewContactName { get; set; }
        [BindProperty] public string NewTaxId { get; set; }

        public List<Customer> Customers { get; set; } = new();
        public string Message { get; set; }
        public bool IsSuccess { get; set; }

        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            Customer[] list = string.IsNullOrWhiteSpace(Q)
                ? _srv.GetCustomersForOwner(id)
                : _srv.SearchCustomers(Q, id);
            Customers = new List<Customer>(list ?? new Customer[0]);
            return Page();
        }

        public IActionResult OnPostAdd()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            var currency = HttpContext.Session.GetString("Currency") ?? "ILS";

            if (string.IsNullOrWhiteSpace(NewBusinessName))
            { Message = "Business name required"; IsSuccess = false; return OnGet(); }

            try
            {
                _srv.AddCustomer(new Customer
                {
                    BusinessName      = NewBusinessName,
                    ContactName       = NewContactName,
                    Email             = NewEmail,
                    Phone             = NewPhone,
                    TaxId             = NewTaxId,
                    OwnerId           = id,
                    PreferredCurrency = currency,
                });
                Message = $"Customer {NewBusinessName} added.";
                IsSuccess = true;
                NewBusinessName = NewEmail = NewPhone = NewContactName = NewTaxId = "";
            }
            catch (System.Exception ex) { Message = "Add failed: " + ex.Message; IsSuccess = false; }
            return OnGet();
        }

        public IActionResult OnPostUpdate(int id, string businessName, string contactName, string email, string phone, string taxId)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;
            try
            {
                var c = _srv.GetCustomerById(id);
                if (c == null) { Message = "Customer not found"; IsSuccess = false; return OnGet(); }
                c.BusinessName = businessName ?? c.BusinessName;
                c.ContactName  = contactName;
                c.Email        = email;
                c.Phone        = phone;
                c.TaxId        = taxId;
                _srv.UpdateCustomer(c);
                Message = "Customer updated."; IsSuccess = true;
            }
            catch (System.Exception ex) { Message = "Update failed: " + ex.Message; IsSuccess = false; }
            return OnGet();
        }

        public IActionResult OnGetCsv()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var list = _srv.GetCustomersForOwner(ownerId) ?? new Customer[0];

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Id,BusinessName,ContactName,Email,Phone,TaxId,Currency,Address");
            foreach (var c in list)
            {
                sb.AppendLine(string.Join(",",
                    c.Id, Csv(c.BusinessName), Csv(c.ContactName), Csv(c.Email),
                    Csv(c.Phone), Csv(c.TaxId), Csv(c.PreferredCurrency), Csv(c.Address)));
            }
            byte[] bytes = new System.Text.UTF8Encoding(true).GetBytes(sb.ToString());
            return File(bytes, "text/csv", "BManaged_Customers.csv");
        }

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public IActionResult OnPostDelete(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Owner") return RedirectToPage("/Login");
            try { _srv.DeleteCustomer(id); Message = "Customer removed."; IsSuccess = true; }
            catch (System.Exception ex) { Message = "Delete failed: " + ex.Message; IsSuccess = false; }
            return OnGet();
        }

        // Quick-view modal — returns JSON with customer + projects + invoices + balance.
        public IActionResult OnGetDetail(int customerId)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return new JsonResult(new { error = "unauthorized" });
            try
            {
                var c = _srv.GetCustomerById(customerId);
                if (c == null) return new JsonResult(new { error = "not found" });

                var projects = _srv.GetProjectsByCustomer(customerId) ?? new Project[0];
                var invoices = _srv.GetInvoicesByCustomer(customerId) ?? new Invoice[0];
                decimal outstanding = invoices.Where(i => i.Status != "Paid").Sum(i => i.Total);
                decimal lifetime = invoices.Where(i => i.Status == "Paid").Sum(i => i.Total);

                return new JsonResult(new
                {
                    id           = c.Id,
                    businessName = c.BusinessName,
                    contactName  = c.ContactName ?? "",
                    email        = c.Email ?? "",
                    phone        = c.Phone ?? "",
                    taxId        = c.TaxId ?? "",
                    currency     = c.PreferredCurrency ?? "ILS",
                    outstanding  = outstanding,
                    lifetime     = lifetime,
                    projects = projects.Select(p => new
                    {
                        id       = p.Id,
                        title    = p.Title,
                        status   = p.Status,
                        budget   = p.TotalBudget,
                    }).ToArray(),
                    invoices = invoices.OrderByDescending(i => i.IssueDate).Take(8).Select(i => new
                    {
                        invoiceNumber = i.InvoiceNumber,
                        total         = i.Total,
                        currency      = i.Currency,
                        status        = i.Status,
                        issueDate     = i.IssueDate.ToString("dd MMM yyyy"),
                        dueDate       = i.DueDate.ToString("dd MMM yyyy"),
                    }).ToArray(),
                });
            }
            catch (System.Exception ex) { return new JsonResult(new { error = ex.Message }); }
        }
    }
}
