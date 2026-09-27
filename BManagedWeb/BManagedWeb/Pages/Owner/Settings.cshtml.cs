using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Owner
{
    // =========================================================================
    // SettingsModel — /Owner/Settings (Owner role only).
    // -------------------------------------------------------------------------
    // Four POST handlers, each independent so a partial save (e.g. just the
    // password) doesn't cascade into the others:
    //   OnPostProfile     — email / phone / preferred currency
    //                       (validates email regex + phone regex; defends
    //                        even if HTML5 'required' was bypassed).
    //   OnPostPassword    — password change. Same regex as SignUp so an
    //                       existing account can't downgrade to a weak
    //                       password from this page.
    //   OnPostCompany     — business name (display), VAT registration
    //                       (Patur / Murshe), Osek-Zair flag.
    //                       Three SOAP ops on the same channel.
    //   OnPostRotateInvite — generates a fresh PREFIX-XXXX code (4 chars
    //                       from BusinessName + 4 random alphanumerics from
    //                       a confusable-free alphabet). Old code stops
    //                       working immediately because GetOwnerByInviteCode
    //                       does an exact match.
    // Guard():
    //   Every handler short-circuits to /Login if the session role isn't
    //   'Owner'. Defence-in-depth — the nav already hides the link, but
    //   handlers must enforce it themselves (don't trust the client).
    // =========================================================================
    public class SettingsModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();
        private static readonly Regex EmailRx    = new Regex(@"^[\w\.\-]+@[\w\-]+\.[\w\-\.]+$");
        private static readonly Regex PhoneRx    = new Regex(@"^\+?\d{7,15}$");
        private static readonly Regex PasswordRx = new Regex(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$");

        [BindProperty] public string Email { get; set; }
        [BindProperty] public string Phone { get; set; }
        [BindProperty] public string Currency { get; set; } = "ILS";
        [BindProperty] public string NewPassword { get; set; }

        // Owner-only company section
        [BindProperty] public string BusinessName { get; set; }
        [BindProperty] public string BusinessType { get; set; } = "Patur";
        [BindProperty] public bool   IsZair { get; set; }
        public string InviteCode { get; set; }

        private IActionResult Guard()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            return null;
        }

        public IActionResult OnGet()
        {
            var g = Guard(); if (g != null) return g;
            Reload();
            return Page();
        }

        private void Reload()
        {
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            try
            {
                var u = _srv.GetUserById(id);
                if (u == null) return;
                Email          = u.Email;
                Phone          = u.Phone;
                Currency       = u.PreferredCurrency ?? "ILS";
                BusinessName   = u.BusinessName ?? "";
                BusinessType   = u.BusinessType == "Murshe" ? "Murshe" : "Patur";
                IsZair         = u.IsZair;
                InviteCode     = u.InviteCode;
            }
            catch (Exception ex) { TempData["SetMsg"] = "Load failed: " + ex.Message; }
        }

        public IActionResult OnPostProfile()
        {
            var g = Guard(); if (g != null) return g;
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;

            if (!EmailRx.IsMatch(Email ?? "")) { TempData["SetMsg"] = "Email looks invalid."; return RedirectToPage(); }
            if (!PhoneRx.IsMatch(Phone ?? "")) { TempData["SetMsg"] = "Phone must be 7–15 digits."; return RedirectToPage(); }

            try
            {
                _srv.UpdateUserProfile(id, Email, Phone, Currency ?? "ILS");
                HttpContext.Session.SetString("Currency", Currency ?? "ILS");
                TempData["SetMsg"] = "Profile saved.";
            }
            catch (Exception ex) { TempData["SetMsg"] = "Save failed: " + ex.Message; }
            return RedirectToPage();
        }

        public IActionResult OnPostPassword()
        {
            var g = Guard(); if (g != null) return g;
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;

            if (!PasswordRx.IsMatch(NewPassword ?? ""))
            { TempData["SetMsg"] = "Password must be 8+ chars and include a letter and a digit."; return RedirectToPage(); }
            try
            {
                _srv.ResetPassword(id, NewPassword);
                TempData["SetMsg"] = "Password updated.";
            }
            catch (Exception ex) { TempData["SetMsg"] = "Failed: " + ex.Message; }
            return RedirectToPage();
        }

        public IActionResult OnPostCompany()
        {
            var g = Guard(); if (g != null) return g;
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            try
            {
                if (!string.IsNullOrWhiteSpace(BusinessName))
                    _srv.SetBusinessName(id, BusinessName.Trim());
                _srv.SetBusinessType(id, BusinessType == "Murshe" ? "Murshe" : "Patur");
                _srv.SetIsZair(id, IsZair);
                TempData["SetMsg"] = "Company settings saved.";
            }
            catch (Exception ex) { TempData["SetMsg"] = "Failed: " + ex.Message; }
            return RedirectToPage();
        }

        public IActionResult OnPostRotateInvite()
        {
            var g = Guard(); if (g != null) return g;
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;
            try
            {
                var u = _srv.GetUserById(id);
                string seed = string.IsNullOrWhiteSpace(u?.BusinessName) ? (u?.Username ?? "BMNG") : u.BusinessName;
                string code = NewInviteCode(seed);
                var result = _srv.SetInviteCode(id, code);
                TempData["SetMsg"] = "New invite code: " + (result ?? code);
            }
            catch (Exception ex) { TempData["SetMsg"] = "Rotate failed: " + ex.Message; }
            return RedirectToPage();
        }

        private static string NewInviteCode(string seed)
        {
            string prefix = new string((seed ?? "")
                .ToUpperInvariant().Where(char.IsLetterOrDigit).Take(4).ToArray());
            if (prefix.Length < 2) prefix = "BMNG";
            const string alpha = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            var tail = new string(Enumerable.Range(0, 4)
                .Select(_ => alpha[RandomNumberGenerator.GetInt32(alpha.Length)]).ToArray());
            return prefix + "-" + tail;
        }
    }
}
