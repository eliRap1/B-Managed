using Model;
using Model.Helpers;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using ViewDB;

namespace BusinessLogic
{
    /// <summary>
    /// Business logic for authentication and users/employees/owners: login checks,
    /// registration with pending-approval rules, role/profile management, owner
    /// linking (with join-request notification) and invite codes.
    /// </summary>
    public class UserLogic
    {
        private readonly UserDB         userDB  = new UserDB();
        private readonly NotificationDB notifDB = new NotificationDB();

        public bool CheckUserPassword(string u, string p)
            => userDB.VerifyPassword(u, p);

        public bool CheckUserExist(string u)
            => userDB.UserExists(u);

        public User GetUserById(int id)
            => userDB.GetById(id);

        public int GetUserId(string username)
            => userDB.GetIdByUsername(username);

        public bool AddUser(string username, string password, string email,
                            string phone, string role, string preferredCurrency)
        {
            if (!SecurityHelper.IsSafeString(username, 50)) return false;
            if (string.IsNullOrEmpty(password) || password.Length < 4) return false;
            if (userDB.UserExists(username)) return false;

            string r = role ?? "Client";
            // Pending approval: Clients/Employees signing up default to inactive
            // until an Owner approves them. Owner-tier signup (admin seed) is
            // always active.
            bool active = r == "Owner";

            var u = new User
            {
                Username = username,
                PasswordHash = SecurityHelper.HashPassword(password),
                Email = email,
                Phone = phone,
                Role = r,
                IsActive = active,
                CreatedAt = DateTime.Now,
                PreferredCurrency = preferredCurrency ?? "ILS"
            };
            return userDB.Insert(u) > 0;
        }

        public List<User> GetPendingUsers()
            => userDB.GetInactive();

        public void SetUserActive(int userId, bool isActive)
            => userDB.SetActive(userId, isActive);

        public void DeleteUser(int userId)
            => userDB.Delete(userId);

        public void ResetPassword(int userId, string newPassword)
        {
            try { userDB.SetPassword(userId, SecurityHelper.HashPassword(newPassword)); }
            catch (Exception ex) { throw new FaultException("ResetPassword failed: " + ex.Message); }
        }

        public void UpdateUserRole(int userId, string newRole)
        {
            try { userDB.UpdateRole(userId, newRole); }
            catch (Exception ex) { throw new FaultException("UpdateUserRole failed: " + ex.Message); }
        }

        public bool IsOwner(string username) => userDB.IsRole(username, "Owner");

        public AllUsers GetAllUsers() => userDB.GetAll();

        public List<User> GetAllEmployees() => userDB.GetByRole("Employee");

        public List<User> GetUsersForOwner(int ownerId)     => userDB.GetUsersForOwner(ownerId);
        public List<User> GetPendingForOwner(int ownerId)   => userDB.GetPendingForOwner(ownerId);
        public List<User> GetEmployeesForOwner(int ownerId) => userDB.GetEmployeesForOwner(ownerId);

        public void UpdateUserProfile(int userId, string email, string phone, string preferredCurrency)
            => userDB.UpdateProfile(userId, email, phone, preferredCurrency);

        public void SetBusinessType(int userId, string businessType)
            => userDB.SetBusinessType(userId, businessType);

        public void SetIsZair(int userId, bool isZair)
            => userDB.SetIsZair(userId, isZair);

        public void SetOwnerId(int userId, int ownerId)
        {
            userDB.SetOwnerId(userId, ownerId > 0 ? ownerId : (int?)null);
            // When linking a brand-new (still inactive) Employee/Client to an
            // Owner, drop a notification on that Owner's inbox so they know
            // someone joined and can approve them.
            try
            {
                if (ownerId > 0)
                {
                    var u = userDB.GetById(userId);
                    if (u != null && !u.IsActive)
                    {
                        notifDB.Insert(new Notification
                        {
                            UserId           = ownerId,
                            Title            = "New " + (u.Role ?? "user") + " request",
                            Message          = "'" + (u.Username ?? "?") +
                                               "' joined your company with the invite code. " +
                                               "Open Manage Users to approve.",
                            NotificationType = "JoinRequest",
                            IsRead           = false,
                            CreatedAt        = DateTime.Now,
                        });
                    }
                }
            }
            catch (Exception ex)
            { System.Diagnostics.Debug.WriteLine("SetOwnerId notify: " + ex.Message); }
        }

        public List<User> GetActiveOwners()
            => userDB.GetActiveOwners();

        public void SetBusinessName(int userId, string businessName)
            => userDB.SetBusinessName(userId, businessName);

        public string SetInviteCode(int userId, string inviteCode)
            => userDB.SetInviteCode(userId, inviteCode);

        public User GetOwnerByInviteCode(string code)
            => userDB.GetOwnerByInviteCode(code);
    }
}
