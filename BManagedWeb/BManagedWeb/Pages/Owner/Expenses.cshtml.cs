using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Owner
{
    public class ExpensesModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();

        public List<Expense> Expenses { get; set; } = new();
        public List<ExpenseCategory> Categories { get; set; } = new();

        [BindProperty] public int NewCategoryId { get; set; }
        [BindProperty] public string NewVendor { get; set; }
        [BindProperty] public decimal NewAmount { get; set; }
        [BindProperty] public decimal NewVat { get; set; }
        [BindProperty] public string NewDescription { get; set; }
        [BindProperty] public string NewCurrency { get; set; } = "ILS";
        [BindProperty] public IFormFile NewReceipt { get; set; }
        [BindProperty(SupportsGet = true)] public string Q { get; set; }

        public string Message { get; set; }
        public bool IsSuccess { get; set; }

        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;

            Categories = (_srv.GetExpenseCategories() ?? new ExpenseCategory[0]).ToList();
            Expenses   = (_srv.GetExpensesByOwner(id) ?? new Expense[0]).ToList();
            if (!string.IsNullOrWhiteSpace(Q))
            {
                var q = Q.Trim().ToLowerInvariant();
                Expenses = Expenses.Where(e =>
                    (e.Vendor ?? "").ToLowerInvariant().Contains(q) ||
                    (e.Description ?? "").ToLowerInvariant().Contains(q)).ToList();
            }
            return Page();
        }

        public IActionResult OnPostAdd()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (NewAmount <= 0) { Message = "Amount required"; IsSuccess = false; return OnGet(); }

            try
            {
                int newId = _srv.AddExpense(new Expense
                {
                    OwnerId = id,
                    CategoryId = NewCategoryId > 0 ? NewCategoryId : (int?)null,
                    Date = DateTime.Today,
                    Amount = NewAmount,
                    VatPaid = NewVat,
                    Vendor = NewVendor ?? "",
                    Description = NewDescription ?? "",
                    Currency = NewCurrency ?? "ILS"
                });

                if (NewReceipt != null && NewReceipt.Length > 0 && NewReceipt.Length <= 5 * 1024 * 1024)
                {
                    using (var ms = new MemoryStream())
                    {
                        NewReceipt.CopyTo(ms);
                        _srv.UploadReceipt(newId, ms.ToArray(), NewReceipt.FileName);
                    }
                }
                Message = "Expense logged.";
                IsSuccess = true;
            }
            catch (Exception ex) { Message = "Failed: " + ex.Message; IsSuccess = false; }
            return OnGet();
        }

        public IActionResult OnGetCsv()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var cats = _srv.GetExpenseCategories() ?? new ExpenseCategory[0];
            var list = _srv.GetExpensesByOwner(ownerId) ?? new Expense[0];

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Id,Date,Category,Vendor,Description,Amount,VatPaid,Currency");
            foreach (var e in list)
            {
                var catName = cats.FirstOrDefault(c => c.Id == (e.CategoryId ?? 0))?.Name ?? "";
                sb.AppendLine(string.Join(",",
                    e.Id, e.Date.ToString("yyyy-MM-dd"), Csv(catName), Csv(e.Vendor),
                    Csv(e.Description), e.Amount, e.VatPaid, Csv(e.Currency)));
            }
            byte[] bytes = new System.Text.UTF8Encoding(true).GetBytes(sb.ToString());
            return File(bytes, "text/csv", "BManaged_Expenses.csv");
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
            try { _srv.DeleteExpense(id); } catch { }
            return RedirectToPage();
        }
    }
}
