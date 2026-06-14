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
            // Constant success message regardless of whether the username exists,
            // to prevent username enumeration via the forgot-password endpoint.
            const string safeSuccessMsg = "If that username is registered, its company Owner has been notified and will reset the password.";
            try
            {
                if (!_srv.CheckUserExist(Username))
                { Message = safeSuccessMsg; IsSuccess = true; return Page(); }

                int uid = _srv.GetUserId(Username);
                var user = _srv.GetUserById(uid);
                if (user == null)
                { Message = safeSuccessMsg; IsSuccess = true; return Page(); }

                // Notify only the Owner of the company this user belongs to —
                // not every Owner on the server (which leaked the request
                // across tenants).
                int? ownerId = user.Role == "Owner" ? (int?)user.Id : user.OwnerId;
                if (!ownerId.HasValue || ownerId.Value <= 0)
                { Message = "No company Owner is linked to this account. Ask an admin."; IsSuccess = false; return Page(); }

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
                Message = safeSuccessMsg;
                IsSuccess = true;
            }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            return Page();
        }
    }
}
