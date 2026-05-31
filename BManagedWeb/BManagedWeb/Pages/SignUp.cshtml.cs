using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages
{
    // =========================================================================
    // SignUpModel — Razor PageModel for /SignUp.
    // -------------------------------------------------------------------------
    // Two-step flow (driven by SelectedRole):
    //   STEP 1: empty SelectedRole → SignUp.cshtml shows the role-chooser cards.
    //           Owner / Employee buttons set a hidden input via JS (pickRole).
    //   STEP 2: form is filled out and posted with SelectedRole = 'Owner' or
    //           'Employee'. Validation runs both client-side (live JS, see
    //           SignUp.cshtml) and here in OnPost as a defence-in-depth.
    // Owner path:
    //   * BusinessType ∈ {Patur, Murshe} — Israeli VAT registration.
    //   * IsZair flag — Israeli Osek-Zair income-tax status (independent).
    //   * Auto-generated invite code (PREFIX-XXXX) so the new Owner can share
    //     it with employees right away. Shown on the Login page after redirect.
    // Employee path:
    //   * Invite code resolved via GetOwnerByInviteCode BEFORE creating the
    //     user — no orphan rows if the code is wrong.
    //   * SetOwnerId on the new user row links them to the company. The
    //     server-side SetOwnerId also drops a JoinRequest notification to
    //     that Owner so they can approve.
    // SOAP ops touched:
    //   CheckUserExist, AddUser, GetUserId, SetBusinessType, SetIsZair,
    //   SetBusinessName, SetInviteCode, SetOwnerId, GetOwnerByInviteCode.
    // =========================================================================
    public class SignUpModel : PageModel
    {
        // _srv talks to the WCF service over BasicHttpBinding (port 8733).
        // Service1Client is the hand-rolled web proxy in Connected Services
        // \bsrv\Reference.cs — equivalent to svcutil-generated WPF proxy.
        private readonly Service1Client _srv = new Service1Client();

        private static readonly Regex UsernameRx = new Regex(@"^[A-Za-z0-9_.]{4,20}$");
        private static readonly Regex EmailRx    = new Regex(@"^[\w\.\-]+@[\w\-]+\.[\w\-\.]+$");
        private static readonly Regex PhoneRx    = new Regex(@"^\+?\d{7,15}$");
        private static readonly Regex PasswordRx = new Regex(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$");

        // 'Owner' or 'Employee' — empty until the user picks a card.
        [BindProperty] public string SelectedRole { get; set; } = "";

        [BindProperty] public string Username { get; set; }
        [BindProperty] public string Password { get; set; }
        [BindProperty] public string Email { get; set; }
        [BindProperty] public string Phone { get; set; }
        [BindProperty] public string Currency { get; set; } = "ILS";

        // Owner-only
        [BindProperty] public string BusinessName { get; set; }
        [BindProperty] public string BusinessType { get; set; } = "Patur";
        [BindProperty] public bool   IsZair { get; set; } = false;

        // Employee-only — invite code (no public company list).
        [BindProperty] public string InviteCode { get; set; }
        // Kept on the model for backwards-compat with admin tooling, but the
        // signup form does not surface a public list anymore.
        [BindProperty] public int    OwnerId { get; set; } = 0;

        public string ErrorMessage { get; set; }
        public List<User> AvailableOwners { get; set; } = new();

        // Display-only — when the Owner-side signup completes, we surface the
        // freshly generated invite code so the new Owner can share it with
        // their employees.
        public string GeneratedInviteCode { get; set; }

        public void OnGet() { }

        public IActionResult OnPost()
        {
            if (SelectedRole != "Owner" && SelectedRole != "Employee")
            { ErrorMessage = "Pick a role first."; return Page(); }

            if (string.IsNullOrEmpty(Username) || !UsernameRx.IsMatch(Username))
            { ErrorMessage = "Username must be 4–20 letters / digits / _ / ."; return Page(); }
            if (string.IsNullOrEmpty(Password) || !PasswordRx.IsMatch(Password))
            { ErrorMessage = "Password must be 8+ chars and include at least one letter and one digit."; return Page(); }
            if (string.Equals(Password, Username, StringComparison.OrdinalIgnoreCase))
            { ErrorMessage = "Password cannot be the same as username."; return Page(); }
            if (!EmailRx.IsMatch(Email ?? "")) { ErrorMessage = "Email looks invalid (e.g. name@example.com)."; return Page(); }
            if (!PhoneRx.IsMatch(Phone ?? "")) { ErrorMessage = "Phone must be 7–15 digits, optionally + country code."; return Page(); }

            // Resolve Employee invite code BEFORE creating the user — no point
            // creating an orphaned employee row.
            int? resolvedOwnerId = null;
            if (SelectedRole == "Employee")
            {
                var code = (InviteCode ?? "").Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(code))
                { ErrorMessage = "Please enter the company invite code."; return Page(); }
                try
                {
                    var owner = _srv.GetOwnerByInviteCode(code);
                    if (owner == null)
                    { ErrorMessage = "Invite code not recognised. Ask the business owner to share the code from their settings."; return Page(); }
                    resolvedOwnerId = owner.Id;
                }
                catch (Exception ex)
                { ErrorMessage = "Invite-code lookup failed: " + ex.Message; return Page(); }
            }

            try
            {
                if (_srv.CheckUserExist(Username))
                { ErrorMessage = "Username already taken."; return Page(); }

                bool ok = _srv.AddUser(Username, Password, Email, Phone, SelectedRole, Currency ?? "ILS");
                if (!ok) { ErrorMessage = "Server rejected the request."; return Page(); }

                int newId = _srv.GetUserId(Username);

                if (SelectedRole == "Owner")
                {
                    var bt = (BusinessType == "Murshe") ? "Murshe" : "Patur";
                    try { _srv.SetBusinessType(newId, bt); } catch { }
                    if (IsZair)
                    { try { _srv.SetIsZair(newId, true); } catch { } }
                    if (!string.IsNullOrWhiteSpace(BusinessName))
                    { try { _srv.SetBusinessName(newId, BusinessName.Trim()); } catch { } }

                    // Auto-generate a fresh invite code so the new Owner can
                    // share it with employees right away.
                    var code = NewInviteCode(BusinessName ?? Username);
                    try { _srv.SetInviteCode(newId, code); GeneratedInviteCode = code; } catch { }
                }
                else // Employee
                {
                    if (resolvedOwnerId.HasValue)
                    { try { _srv.SetOwnerId(newId, resolvedOwnerId.Value); } catch { } }
                }

                if (SelectedRole == "Owner" && !string.IsNullOrEmpty(GeneratedInviteCode))
                {
                    TempData["NewOwnerCode"] = GeneratedInviteCode;
                }
                return RedirectToPage("/Login");
            }
            catch (Exception ex)
            { ErrorMessage = "Error: " + ex.Message; return Page(); }
        }

        // Format: PREFIX-XXXX where PREFIX is 4 alpha-numeric chars from the
        // business name and XXXX is 4 random alpha-numerics. 9 chars total
        // including the dash. Easy to read / type.
        private static string NewInviteCode(string seed)
        {
            string prefix = new string((seed ?? "")
                .ToUpperInvariant()
                .Where(char.IsLetterOrDigit)
                .Take(4)
                .ToArray());
            if (prefix.Length < 2) prefix = "BMNG";
            const string alpha = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // skip ambiguous I/O/0/1
            var tail = new string(Enumerable.Range(0, 4).Select(_ => alpha[RandomNumberGenerator.GetInt32(alpha.Length)]).ToArray());
            return prefix + "-" + tail;
        }
    }
}
