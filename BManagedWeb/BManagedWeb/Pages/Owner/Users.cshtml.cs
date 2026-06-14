using System.Collections.Generic;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Owner
{
    public class UsersModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();

        public List<User> AllUsers { get; set; } = new();
        public List<User> Pending  { get; set; } = new();
        public string Message { get; set; }
        public bool IsSuccess { get; set; }

        private IActionResult GuardOwner()
        {
            if (HttpContext.Session.GetString("Role") != "Owner") return RedirectToPage("/Login");
            return null;
        }

        public IActionResult OnGet()
        {
            var g = GuardOwner(); if (g != null) return g;
            Reload();
            return Page();
        }

        private void Reload()
        {
            try
            {
                // Tenant-scoped: only users belonging to this Owner's company.
                int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;
                var all = _srv.GetUsersForOwner(ownerId);
                AllUsers = (all != null) ? new List<User>(all) : new List<User>();
                var pending = _srv.GetPendingForOwner(ownerId);
                Pending = (pending != null) ? new List<User>(pending) : new List<User>();
            }
            catch (System.Exception ex)
            { Message = "Load failed: " + ex.Message; IsSuccess = false; }
        }

        public IActionResult OnPostApprove(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            try { _srv.SetUserActive(id, true); Message = "Approved."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }

        public IActionResult OnPostToggle(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            try
            {
                var u = _srv.GetUserById(id);
                _srv.SetUserActive(id, !u.IsActive);
                Message = u.IsActive ? "Blocked." : "Unblocked.";
                IsSuccess = true;
            }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }

        public IActionResult OnPostPromote(int id, string newRole)
        {
            var g = GuardOwner(); if (g != null) return g;
            if (newRole != "Owner" && newRole != "Employee" && newRole != "Client")
            { Message = "Invalid role."; IsSuccess = false; Reload(); return Page(); }
            try { _srv.UpdateUserRole(id, newRole); Message = "Role updated."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }

        public IActionResult OnPostReset(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            // TODO(audit): "reset1234" is a well-known hardcoded temporary password.
            // Replace with a randomly generated token (e.g. 8-char alphanumeric) and
            // display it once to the Owner so they can pass it to the user securely.
            // Coordinate with ManageUsers.xaml.cs (WPF) and ForgotPassword.cshtml.cs
            // which also embed "reset1234" literally.
            try { _srv.ResetPassword(id, "reset1234"); Message = "Password reset to 'reset1234'."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }

        public IActionResult OnPostDelete(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            try { _srv.DeleteUser(id); Message = "Deleted."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }
    }
}
