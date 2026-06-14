using System;
using System.Collections.Generic;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Owner
{
    public class ContractsModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();

        public List<Contract> Contracts { get; set; } = new();
        public List<Customer> Customers { get; set; } = new();
        public List<Project>  Projects  { get; set; } = new();

        [BindProperty] public int     NewCustomerId { get; set; }
        [BindProperty] public int     NewProjectId  { get; set; }
        [BindProperty] public string  NewTitle      { get; set; }
        [BindProperty] public string  NewBody       { get; set; }
        [BindProperty] public decimal NewAmount     { get; set; }
        [BindProperty] public string  NewCurrency   { get; set; } = "ILS";

        public string Message { get; set; }
        public bool   IsSuccess { get; set; }

        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;

            try
            {
                var arr = _srv.GetContractsForOwner(id);
                Contracts = (arr ?? new Contract[0]).ToList();
                Customers = (_srv.GetCustomersForOwner(id) ?? new Customer[0]).ToList();
                var projList = new List<Project>();
                foreach (var c in Customers)
                    projList.AddRange(_srv.GetProjectsByCustomer(c.Id) ?? new Project[0]);
                Projects = projList;
            }
            catch (Exception ex) { Message = "Load failed: " + ex.Message; IsSuccess = false; }
            return Page();
        }

        public IActionResult OnPostAdd()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            if (NewCustomerId <= 0 || string.IsNullOrWhiteSpace(NewTitle))
            { Message = "Customer + title required."; return OnGet(); }

            try
            {
                var c = new Contract
                {
                    CustomerId  = NewCustomerId,
                    ProjectId   = NewProjectId,
                    Title       = NewTitle,
                    Body        = NewBody ?? "",
                    TotalAmount = NewAmount,
                    Currency    = NewCurrency ?? "ILS",
                    Status      = "Draft",
                    CreatedAt   = DateTime.Now,
                };
                int newId = _srv.CreateContract(c);
                TempData["ContractMsg"] = "Contract created (#" + newId + ").";
            }
            catch (Exception ex) { TempData["ContractMsg"] = "Failed: " + ex.Message; }
            return RedirectToPage();
        }

        public IActionResult OnPostMarkSigned(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Owner") return RedirectToPage("/Login");
            // TODO(audit): MarkContractSigned does not verify the contract belongs to this
            // Owner — any Owner can sign another Owner's contract by guessing its id (IDOR).
            // Add a MarkContractSignedForOwner(id, ownerId) WCF operation that checks
            // ContractDB.BelongsToOwner(id, ownerId) before mutating, mirroring the pattern
            // used by MarkInvoicePaidForOwner. Needs changes in IService1, Service1,
            // ContractLogic, and ContractDB — more than 5 files, deferred per audit rules.
            try { _srv.MarkContractSigned(id, DateTime.Today); TempData["ContractMsg"] = "Marked as signed."; }
            catch (Exception ex) { TempData["ContractMsg"] = "Failed: " + ex.Message; }
            return RedirectToPage();
        }

        public IActionResult OnPostDelete(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Owner") return RedirectToPage("/Login");
            // TODO(audit): DeleteContract does not verify ownership — IDOR risk (see above).
            try { _srv.DeleteContract(id); TempData["ContractMsg"] = "Deleted."; }
            catch (Exception ex) { TempData["ContractMsg"] = "Failed: " + ex.Message; }
            return RedirectToPage();
        }

        public IActionResult OnGetPdf(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Owner") return RedirectToPage("/Login");
            try
            {
                var bytes = _srv.GenerateContractPdf(id);
                return File(bytes, "application/pdf", "Contract-" + id + ".pdf");
            }
            catch (Exception ex)
            {
                TempData["ContractMsg"] = "PDF failed: " + ex.Message;
                return RedirectToPage();
            }
        }
    }
}
