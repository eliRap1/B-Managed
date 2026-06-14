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
        // Password field removed — storing plaintext credentials in a long-lived
        // static object exposes them to crash dumps and memory inspection.
        public string Email { get; set; }
        public string Phone { get; set; }
        public int Id { get; set; }
        public string Role { get; set; } = "";              // "Owner" / "Employee" / "Client"
        public string PreferredCurrency { get; set; } = "ILS"; // "ILS" / "USD"
        public bool IsActive { get; set; } = true;

        // Israeli VAT classification: "Patur" / "Murshe" / "Individual".
        public string BusinessType { get; set; } = "Individual";
        // Osek Zair income-tax status flag (independent of BusinessType).
        public bool   IsZair { get; set; } = false;

        public bool IsOwner    => Role == "Owner";
        public bool IsEmployee => Role == "Employee";
        public bool IsClient   => Role == "Client";

        public bool IsPatur  => BusinessType == "Patur";
        public bool IsMurshe => BusinessType == "Murshe";
    }
}
