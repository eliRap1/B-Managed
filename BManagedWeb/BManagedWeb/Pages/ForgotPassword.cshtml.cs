using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();

        [BindProperty] public string Username { get; set; }
        public string Message { get; set; }
        public bool IsSuccess { get; set; }

        public void OnGet() { }

        public IActionResult OnPost()
        {
            if (string.IsNullOrEmpty(Username))
            { Message = "Enter a username."; return Page(); }

            // Always return the same success message regardless of whether the
            // username exists — revealing "User not found." lets attackers enumerate
            // valid accounts. The operation is best-effort; failures are silent.
            const string generic = "If that username is registered, the company Owner has been notified and will reset your password to 'reset1234'.";
            try
            {
                int uid = _srv.GetUserId(Username);
                if (uid > 0)
                {
                    var user = _srv.GetUserById(uid);
                    if (user != null)
                    {
                        // Notify only the Owner of the company this user belongs to —
                        // not every Owner on the server (which leaked the request
                        // across tenants).
                        int? ownerId = user.Role == "Owner" ? (int?)user.Id : user.OwnerId;
                        if (ownerId.HasValue && ownerId.Value > 0)
                        {
                            _srv.SendNotification(new Notification
                            {
                                UserId = ownerId.Value,
                                Title = "Password reset request",
                                Message = $"User '{user.Username}' ({user.Role}) asked for a password reset. " +
                                          "Open ManageUsers > Reset PW to issue 'reset1234'.",
                                NotificationType = "ResetRequest",
                                IsRead = false,
                                CreatedAt = System.DateTime.Now,
                            });
                        }
                    }
                }
            }
            catch { /* best-effort — do not surface errors that reveal account existence */ }
            Message = generic;
            IsSuccess = true;
            return Page();
        }
    }
}
