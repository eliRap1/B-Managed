using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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

        // Returns true when the target user's OwnerId matches the current
        // session owner's UserId, preventing cross-company IDOR mutations.
        private bool BelongsToCurrentOwner(int targetId)
        {
            int currentOwnerId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var target = _srv.GetUserById(targetId);
            return target != null && target.OwnerId.HasValue && target.OwnerId.Value == currentOwnerId;
        }

        // Generates a cryptographically random 12-character temporary password.
        private static string GenerateTempPassword()
        {
            // Use RandomNumberGenerator (crypto-safe) rather than System.Random.
            byte[] bytes = new byte[9]; // 9 bytes → 12 Base64 chars (no padding)
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "A").Replace("/", "B").Replace("=", "C");
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
            if (!BelongsToCurrentOwner(id))
            { Message = "User not found or not in your company."; IsSuccess = false; Reload(); return Page(); }
            try { _srv.SetUserActive(id, true); Message = "Approved."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }

        public IActionResult OnPostToggle(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            if (!BelongsToCurrentOwner(id))
            { Message = "User not found or not in your company."; IsSuccess = false; Reload(); return Page(); }
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
            if (!BelongsToCurrentOwner(id))
            { Message = "User not found or not in your company."; IsSuccess = false; Reload(); return Page(); }
            try { _srv.UpdateUserRole(id, newRole); Message = "Role updated."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }

        public IActionResult OnPostReset(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            if (!BelongsToCurrentOwner(id))
            { Message = "User not found or not in your company."; IsSuccess = false; Reload(); return Page(); }
            try
            {
                // Generate a unique, cryptographically random temporary password
                // rather than the previous hardcoded "reset1234" which was both
                // guessable and, combined with the former IDOR, allowed any
                // Owner to take over any user account.
                string tempPwd = GenerateTempPassword();
                _srv.ResetPassword(id, tempPwd);
                Message = $"Password reset. Temporary password: {tempPwd}";
                IsSuccess = true;
            }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }

        public IActionResult OnPostDelete(int id)
        {
            var g = GuardOwner(); if (g != null) return g;
            if (!BelongsToCurrentOwner(id))
            { Message = "User not found or not in your company."; IsSuccess = false; Reload(); return Page(); }
            try { _srv.DeleteUser(id); Message = "Deleted."; IsSuccess = true; }
            catch (System.Exception ex) { Message = ex.Message; IsSuccess = false; }
            Reload(); return Page();
        }
    }
}
