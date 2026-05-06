using BManagedClient.BMsrv;
using System;

namespace BManagedClient
{
    /// <summary>
    /// Session-scoped DTO that holds the currently signed-in user
    /// in WPF memory. Set in LogIn after a successful CheckUserPassword.
    /// </summary>
    public class Sign
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public int Id { get; set; }
        public string Role { get; set; } = "";              // "Owner" / "Employee" / "Client"
        public string PreferredCurrency { get; set; } = "ILS"; // "ILS" / "USD"
        public bool IsActive { get; set; } = true;

        public bool IsOwner    => Role == "Owner";
        public bool IsEmployee => Role == "Employee";
        public bool IsClient   => Role == "Client";
    }
}
