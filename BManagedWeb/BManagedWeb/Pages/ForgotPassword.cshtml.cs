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
            try
            {
                // Resolve the user without revealing whether the username exists.
                // Both "not found" and "no owner linked" show the same generic
                // message to prevent username enumeration.
                int uid = _srv.GetUserId(Username);
                var user = uid > 0 ? _srv.GetUserById(uid) : null;

                // Notify only the Owner of the company this user belongs to —
                // not every Owner on the server (which leaked the request
                // across tenants).
                int? ownerId = user?.Role == "Owner" ? (int?)user.Id : user?.OwnerId;

                // Use a single generic response for all failure cases (user not
                // found, no owner linked) so the form cannot be used to enumerate
                // valid usernames via different error text.
                if (user == null || !ownerId.HasValue || ownerId.Value <= 0)
                {
                    Message = "If an account exists for that username, its Owner has been notified.";
                    IsSuccess = true;
                    return Page();
                }

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
                Message = "If an account exists for that username, its Owner has been notified.";
                IsSuccess = true;
            }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            return Page();
        }
    }
}
