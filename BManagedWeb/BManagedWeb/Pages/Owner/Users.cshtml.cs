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

        // Verify that targetId is the requesting owner themselves or a member of
        // their company (Employee/Client with ownerId == sessionOwnerId).
        // Uses GetUsersForOwner which returns the Owner row plus all linked users
        // regardless of isActive, so pending users are included.
        private bool IsUserInMyTenant(int ownerId, int targetId)
        {
            try
            {
                var members = _srv.GetUsersForOwner(ownerId);
                return members != null && members.Any(u => u.Id == targetId);
            }
            catch { return false; }
        }

        public IActionResult OnPostApprove(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (!IsUserInMyTenant(ownerId, id))
            { Message = "Access denied."; IsSuccess = false; Reload(); return Page(); }
            try { _srv.SetUserActive(id, true); Message = "Approved."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }

        public IActionResult OnPostToggle(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (!IsUserInMyTenant(ownerId, id))
            { Message = "Access denied."; IsSuccess = false; Reload(); return Page(); }
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
            int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (!IsUserInMyTenant(ownerId, id))
            { Message = "Access denied."; IsSuccess = false; Reload(); return Page(); }
            if (newRole != "Owner" && newRole != "Employee" && newRole != "Client")
            { Message = "Invalid role."; IsSuccess = false; Reload(); return Page(); }
            try { _srv.UpdateUserRole(id, newRole); Message = "Role updated."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }

        public IActionResult OnPostReset(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (!IsUserInMyTenant(ownerId, id))
            { Message = "Access denied."; IsSuccess = false; Reload(); return Page(); }
            try { _srv.ResetPassword(id, "reset1234"); Message = "Password reset to 'reset1234'."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }

        public IActionResult OnPostDelete(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (!IsUserInMyTenant(ownerId, id))
            { Message = "Access denied."; IsSuccess = false; Reload(); return Page(); }
            try { _srv.DeleteUser(id); Message = "Deleted."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }
    }
}
