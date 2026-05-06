using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Owner
{
    /// <summary>
    /// Bulk import: bank-statement CSV (date, description, amount) → Expense rows.
    /// Receipt-text paste: free-text → regex extract vendor + total → single Expense.
    /// </summary>
    public class ImportModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();

        public List<ExpenseCategory> Categories { get; set; } = new();
        [BindProperty] public IFormFile CsvFile { get; set; }
        [BindProperty] public string ReceiptText { get; set; }

        public string Message { get; set; }
        public List<string> Imported { get; set; } = new();

        // Vendor keyword → category-name lookup. Same dictionary as the JS one
        // on the Expenses page, but server-side because CSV import has no UI.
        private static readonly (Regex rx, string cat)[] Rules = new[]
        {
            (new Regex(@"שופרסל|רמי לוי|אושר עד|יוחננוף|מגה|ויקטורי|טיב טעם|shufersal|coffee|cafe|מסעדה|פיצה|קפה",
                       RegexOptions.IgnoreCase), "Food"),
            (new Regex(@"דלק|פז|סונול|דור אלון|delek|paz|sonol|חניון|parking",          RegexOptions.IgnoreCase), "Fuel"),
            (new Regex(@"שכירות|נדל[״""׳]?ן|rent",                                       RegexOptions.IgnoreCase), "Rent"),
            (new Regex(@"חשמל|מים|בזק|hot|cellcom|partner|פלאפון|electricity|water",     RegexOptions.IgnoreCase), "Utilities"),
            (new Regex(@"facebook|instagram|google\s*ads|tiktok|לינקדאין|מיתוג|פרסום|advertising|marketing",
                       RegexOptions.IgnoreCase), "Marketing"),
            (new Regex(@"אל[־-]על|רכבת|מטוס|מלון|airbnb|booking|train|flight|נסיעה",     RegexOptions.IgnoreCase), "Travel"),
            (new Regex(@"ציוד|לפטופ|מחשב|hp|dell|apple|equipment|hardware|כלים",          RegexOptions.IgnoreCase), "Equipment"),
        };

        private int? Classify(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (var r in Rules)
            {
                if (!r.rx.IsMatch(text)) continue;
                var cat = Categories.FirstOrDefault(c =>
                    string.Equals(c.Name, r.cat, StringComparison.OrdinalIgnoreCase));
                return cat?.Id;
            }
            return null;
        }

        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            Categories = (_srv.GetExpenseCategories() ?? new ExpenseCategory[0]).ToList();
            return Page();
        }

        public IActionResult OnPostCsv()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;
            string cur  = HttpContext.Session.GetString("Currency") ?? "ILS";

            Categories = (_srv.GetExpenseCategories() ?? new ExpenseCategory[0]).ToList();
            if (CsvFile == null || CsvFile.Length == 0)
            { Message = "No file."; return Page(); }

            try
            {
                using (var sr = new StreamReader(CsvFile.OpenReadStream()))
                {
                    string firstLine = sr.ReadLine();         // header
                    string line;
                    int line0 = 0;
                    while ((line = sr.ReadLine()) != null)
                    {
                        line0++;
                        var cells = SplitCsv(line);
                        if (cells.Count < 3) continue;
                        if (!DateTime.TryParse(cells[0], out var date))
                            DateTime.TryParseExact(cells[0], new[] { "dd/MM/yyyy", "yyyy-MM-dd" },
                                CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
                        if (date == default) continue;
                        var description = cells[1];
                        if (!decimal.TryParse(cells[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                            continue;
                        if (amount >= 0) continue;             // ignore credits
                        amount = Math.Abs(amount);

                        var catId = Classify(description);
                        _srv.AddExpense(new Expense
                        {
                            OwnerId     = ownerId,
                            CategoryId  = catId,
                            Date        = date,
                            Amount      = amount,
                            VatPaid     = Math.Round(amount * 17m / 117m, 2),
                            Vendor      = description.Length > 60 ? description.Substring(0, 60) : description,
                            Description = description,
                            Currency    = cur,
                        });
                        Imported.Add(date.ToString("dd/MM/yyyy") + " · " + description + " · " + amount.ToString("N2"));
                    }
                }
                Message = $"Imported {Imported.Count} expense rows.";
            }
            catch (Exception ex) { Message = "Failed: " + ex.Message; }
            return Page();
        }

        public IActionResult OnPostReceipt()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;
            string cur  = HttpContext.Session.GetString("Currency") ?? "ILS";

            Categories = (_srv.GetExpenseCategories() ?? new ExpenseCategory[0]).ToList();
            if (string.IsNullOrWhiteSpace(ReceiptText))
            { Message = "Paste receipt text first."; return Page(); }

            // Total: look for "Total" / "סה״כ" / "סה\"כ" near a number.
            decimal total = 0;
            var totalMatch = Regex.Match(ReceiptText,
                @"(total|סה[""״״]?כ|לתשלום)\D{0,12}(\d{1,3}(?:[,.\s]\d{3})*(?:[.,]\d{1,2})?)",
                RegexOptions.IgnoreCase);
            if (totalMatch.Success)
                decimal.TryParse(totalMatch.Groups[2].Value.Replace(",", "").Replace(" ", ""),
                                 NumberStyles.Any, CultureInfo.InvariantCulture, out total);
            if (total == 0)
            {
                // Fallback: largest number in text
                foreach (Match m in Regex.Matches(ReceiptText, @"\d+(?:[.,]\d{1,2})?"))
                {
                    if (decimal.TryParse(m.Value.Replace(",", "."),
                                         NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                        if (v > total) total = v;
                }
            }
            string vendor = ReceiptText.Split('\n').FirstOrDefault()?.Trim() ?? "";
            if (vendor.Length > 60) vendor = vendor.Substring(0, 60);

            var catId = Classify(vendor + " " + ReceiptText);

            try
            {
                _srv.AddExpense(new Expense
                {
                    OwnerId     = ownerId,
                    CategoryId  = catId,
                    Date        = DateTime.Today,
                    Amount      = total,
                    VatPaid     = Math.Round(total * 17m / 117m, 2),
                    Vendor      = string.IsNullOrEmpty(vendor) ? "(parsed receipt)" : vendor,
                    Description = ReceiptText.Length > 200 ? ReceiptText.Substring(0, 200) : ReceiptText,
                    Currency    = cur,
                });
                Message = $"Created expense — vendor: {vendor}, total: {total:N2} {cur}.";
            }
            catch (Exception ex) { Message = "Failed: " + ex.Message; }
            return Page();
        }

        private static List<string> SplitCsv(string line)
        {
            var cells = new List<string>();
            bool inQ = false;
            var cur = new System.Text.StringBuilder();
            foreach (var ch in line)
            {
                if (ch == '"') { inQ = !inQ; continue; }
                if (ch == ',' && !inQ) { cells.Add(cur.ToString()); cur.Clear(); continue; }
                cur.Append(ch);
            }
            cells.Add(cur.ToString());
            return cells;
        }
    }
}
