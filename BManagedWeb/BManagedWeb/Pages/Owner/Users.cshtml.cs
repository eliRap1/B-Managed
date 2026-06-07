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

        // TODO(audit): OnPostApprove/Toggle/Promote/Reset/Delete all operate on an arbitrary
        // `id` from POST form data without verifying that the target user belongs to this
        // Owner's company (IDOR). The service-layer operations (SetUserActive, DeleteUser,
        // ResetPassword, UpdateUserRole) also lack an ownerId parameter. Until
        // owner-scoped variants are added to IService1, add a page-side fence that
        // validates the target id is in this Owner's user list before acting.

        private bool UserBelongsToOwner(int userId)
        {
            int ownerId = HttpContext.Session.GetInt32("UserId") ?? 0;
            // Re-use the already-tenant-scoped query — if the id isn't in the list it
            // doesn't belong to this owner.
            try
            {
                var all = _srv.GetUsersForOwner(ownerId);
                return all != null && all.Any(u => u.Id == userId);
            }
            catch { return false; }
        }

        public IActionResult OnPostApprove(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            if (!UserBelongsToOwner(id)) { Message = "User not found."; IsSuccess = false; Reload(); return Page(); }
            try { _srv.SetUserActive(id, true); Message = "Approved."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }

        public IActionResult OnPostToggle(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            if (!UserBelongsToOwner(id)) { Message = "User not found."; IsSuccess = false; Reload(); return Page(); }
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
            if (!UserBelongsToOwner(id)) { Message = "User not found."; IsSuccess = false; Reload(); return Page(); }
            try { _srv.UpdateUserRole(id, newRole); Message = "Role updated."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }

        public IActionResult OnPostReset(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            if (!UserBelongsToOwner(id)) { Message = "User not found."; IsSuccess = false; Reload(); return Page(); }
            try { _srv.ResetPassword(id, "reset1234"); Message = "Password reset to 'reset1234'."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }

        public IActionResult OnPostDelete(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            if (!UserBelongsToOwner(id)) { Message = "User not found."; IsSuccess = false; Reload(); return Page(); }
            try { _srv.DeleteUser(id); Message = "Deleted."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }
    }
}
