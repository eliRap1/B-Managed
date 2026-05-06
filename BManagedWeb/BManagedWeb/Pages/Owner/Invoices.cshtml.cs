using System;
using System.Collections.Generic;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Owner
{
    public class InvoicesModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();

        public Invoice Selected { get; set; }
        public List<InvoiceLine> Lines { get; set; } = new();
        public List<Customer> Customers { get; set; } = new();
        public List<(Invoice, string)> AllInvoices { get; set; } = new();

        [BindProperty] public int    NewCustomerId { get; set; }
        [BindProperty] public DateTime NewDueDate  { get; set; } = DateTime.Today.AddDays(30);
        [BindProperty] public string NewCurrency   { get; set; } = "ILS";

        [BindProperty] public string  LineDescription { get; set; }
        [BindProperty] public double  LineQuantity    { get; set; } = 1.0;
        [BindProperty] public decimal LineUnitPrice   { get; set; }

        [BindProperty(SupportsGet = true)] public string Q { get; set; }
        [BindProperty(SupportsGet = true)] public string StatusFilter { get; set; }
        [BindProperty(SupportsGet = true)] public int? ContractId { get; set; }
        public Contract LinkedContract { get; set; }

        public IActionResult OnGet(int? id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;

            var custArr = _srv.GetCustomersForOwner(ownerId);
            Customers = custArr?.ToList() ?? new List<Customer>();

            // Prefill from contract if ?contractId=N
            if (ContractId.HasValue && ContractId.Value > 0)
            {
                LinkedContract = _srv.GetContractById(ContractId.Value);
                if (LinkedContract != null)
                {
                    NewCustomerId = LinkedContract.CustomerId;
                    NewCurrency   = LinkedContract.Currency;
                }
            }

            if (id.HasValue && id.Value > 0)
            {
                Selected = _srv.GetInvoiceById(id.Value);
                Lines = (_srv.GetInvoiceLines(id.Value) ?? new InvoiceLine[0]).ToList();
            }
            else
            {
                foreach (var c in Customers)
                {
                    var arr = _srv.GetInvoicesByCustomer(c.Id) ?? new Invoice[0];
                    foreach (var inv in arr) AllInvoices.Add((inv, c.BusinessName));
                }
                if (!string.IsNullOrWhiteSpace(Q))
                {
                    var q = Q.Trim().ToLowerInvariant();
                    AllInvoices = AllInvoices
                        .Where(x => (x.Item1.InvoiceNumber ?? "").ToLowerInvariant().Contains(q) ||
                                    (x.Item2 ?? "").ToLowerInvariant().Contains(q))
                        .ToList();
                }
                if (!string.IsNullOrEmpty(StatusFilter))
                    AllInvoices = AllInvoices.Where(x => x.Item1.Status == StatusFilter).ToList();
                AllInvoices = AllInvoices.OrderByDescending(x => x.Item1.IssueDate).Take(40).ToList();
            }
            return Page();
        }

        public IActionResult OnPostCreate()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            if (NewCustomerId <= 0) return RedirectToPage();
            // VAT-exempt business types (Patur / Zair) issue invoices without VAT.
            int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;
            double vatRate = 0.17;
            try
            {
                var owner = _srv.GetUserById(ownerId);
                if (owner != null && (owner.BusinessType == "Patur" || owner.BusinessType == "Zair"))
                    vatRate = 0;
            }
            catch { }

            int newId = _srv.CreateInvoice(new Invoice
            {
                CustomerId = NewCustomerId,
                IssueDate  = DateTime.Today,
                DueDate    = NewDueDate,
                Currency   = NewCurrency ?? "ILS",
                Status     = "Draft",
                VatRate    = vatRate,
                ContractId = ContractId,
            });
            // If a contract was linked and it has a Total, seed an initial line.
            if (ContractId.HasValue && ContractId.Value > 0)
            {
                try
                {
                    var ctr = _srv.GetContractById(ContractId.Value);
                    if (ctr != null && ctr.TotalAmount > 0)
                    {
                        _srv.AddInvoiceLine(new InvoiceLine
                        {
                            InvoiceId   = newId,
                            Description = ctr.Title,
                            Quantity    = 1,
                            UnitPrice   = ctr.TotalAmount,
                            LineTotal   = ctr.TotalAmount,
                            Currency    = ctr.Currency ?? "ILS",
                        });
                    }
                }
                catch { }
            }
            return RedirectToPage("/Owner/Invoices", new { id = newId });
        }

        public IActionResult OnPostAddLine(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Owner") return RedirectToPage("/Login");
            _srv.AddInvoiceLine(new InvoiceLine
            {
                InvoiceId = id,
                Description = LineDescription ?? "",
                Quantity = LineQuantity,
                UnitPrice = LineUnitPrice,
                LineTotal = (decimal)LineQuantity * LineUnitPrice,
                Currency = "ILS"
            });
            return RedirectToPage("/Owner/Invoices", new { id });
        }

        public IActionResult OnPostMarkSent(int id)
        {
            _srv.UpdateInvoiceStatus(id, "Sent");
            return RedirectToPage("/Owner/Invoices", new { id });
        }

        public IActionResult OnPostMarkPaid(int id)
        {
            _srv.MarkInvoicePaid(id, DateTime.Today);
            return RedirectToPage("/Owner/Invoices", new { id });
        }

        public IActionResult OnGetPdf(int id)
        {
            var bytes = _srv.GenerateInvoicePdf(id);
            return File(bytes, "application/pdf", $"INV-{id}.pdf");
        }
    }
}
