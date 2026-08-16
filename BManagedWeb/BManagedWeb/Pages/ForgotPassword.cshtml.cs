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
            // Use a single generic message for all non-success outcomes to prevent
            // username enumeration.
            const string genericOk = "If that username exists, your company's Owner has been notified to reset your password.";
            try
            {
                if (!_srv.CheckUserExist(Username))
                { Message = genericOk; IsSuccess = true; return Page(); }

                int uid = _srv.GetUserId(Username);
                var user = _srv.GetUserById(uid);
                if (user == null)
                { Message = genericOk; IsSuccess = true; return Page(); }

                // Notify only the Owner of the company this user belongs to —
                // not every Owner on the server (which leaked the request
                // across tenants).
                int? ownerId = user.Role == "Owner" ? (int?)user.Id : user.OwnerId;
                if (!ownerId.HasValue || ownerId.Value <= 0)
                { Message = genericOk; IsSuccess = true; return Page(); }

                _srv.SendNotification(new Notification
                {
                    UserId = ownerId.Value,
                    Title = "Password reset request",
                    Message = $"User '{user.Username}' ({user.Role}) asked for a password reset. " +
                              "Open ManageUsers > Reset PW to issue a temporary password.",
                    NotificationType = "ResetRequest",
                    IsRead = false,
                    CreatedAt = System.DateTime.Now,
                });
                Message = genericOk;
                IsSuccess = true;
            }
            catch (System.Exception) { Message = genericOk; IsSuccess = true; }
            return Page();
        }
    }
}
