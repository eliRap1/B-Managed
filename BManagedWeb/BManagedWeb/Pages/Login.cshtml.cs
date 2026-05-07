using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BManagedWeb.bsrv;

namespace BManagedWeb.Pages
{
    public class LoginModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();

        [BindProperty] public string Username { get; set; }
        [BindProperty] public string Password { get; set; }
        public string ErrorMessage { get; set; }

        public void OnGet() { }

        public IActionResult OnPost()
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            { ErrorMessage = "Please fill all fields"; return Page(); }

            // Fail-fast for unknown user — skips the expensive PBKDF2 path entirely
            // when the username does not exist, so the spinner only spins for real
            // verification work.
            if (!_srv.CheckUserExist(Username))
            { ErrorMessage = "Invalid username or password"; return Page(); }

            bool ok = _srv.CheckUserPassword(Username, Password);
            if (!ok)
            {
                // VerifyPassword filters by [isActive]=true, so an unapproved
                // Employee/Client gets the same 'wrong password' result as a
                // real wrong-password attempt. Look up the user once more to
                // tell them the actual reason.
                try
                {
                    int probeId = _srv.GetUserId(Username);
                    if (probeId > 0)
                    {
                        var probe = _srv.GetUserById(probeId);
                        if (probe != null && !probe.IsActive)
                        {
                            ErrorMessage = "Account awaiting Owner approval. Ask the Owner of your company to approve you in Manage Users.";
                            return Page();
                        }
                    }
                }
                catch { }
                ErrorMessage = "Invalid username or password";
                return Page();
            }

            int id = _srv.GetUserId(Username);
            var user = _srv.GetUserById(id);

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role);
            HttpContext.Session.SetString("Currency", user.PreferredCurrency ?? "ILS");

            return user.Role switch
            {
                "Owner"    => RedirectToPage("/Owner/Home"),
                "Employee" => RedirectToPage("/Employee/Home"),
                _          => RedirectToPage("/Client/Portal"),
            };
        }
    }
}
