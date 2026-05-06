using System;
using System.Runtime.Serialization;

namespace Model
{
    /// <summary>
    /// System user. Roles: "Owner" (full admin), "Employee" (works on
    /// assigned projects + logs expenses), "Client" (view own invoices
    /// + projects).
    /// </summary>
    [DataContract]
    public class User : Base
    {
        [DataMember] public string Username { get; set; }
        [DataMember] public string PasswordHash { get; set; }
        [DataMember] public string Email { get; set; }
        [DataMember] public string Phone { get; set; }
        [DataMember] public string Role { get; set; } = "Client";
        [DataMember] public bool IsActive { get; set; } = true;
        [DataMember] public DateTime CreatedAt { get; set; } = DateTime.Now;
        [DataMember] public string PreferredCurrency { get; set; } = "ILS";

        /// <summary>
        /// Israeli business classification — drives default VAT behaviour.
        /// "Patur" = Osek Patur (exempt from VAT, < ~120k₪/yr).
        /// "Zair"  = Osek Zair (small new business, also exempt).
        /// "Murshe" = Osek Murshe (regular VAT-collecting business).
        /// "Individual" = private user (no business filing).
        /// </summary>
        [DataMember] public string BusinessType { get; set; } = "Individual";
    }
}
