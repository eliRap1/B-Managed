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
        // TODO(audit): PasswordHash and InviteCode must NOT be returned to clients.
        // Remove [DataMember] from both fields and strip them server-side in every
        // query that returns User objects (GetAllUsers, GetById, GetUsersForOwner, …).
        // Touching this requires regenerating the svcutil proxy in BManagedClient and
        // updating BManagedWeb/Connected Services/bsrv/Reference.cs — do as a
        // dedicated PR touching those 3 files together.
        [DataMember] public string PasswordHash { get; set; }
        [DataMember] public string Email { get; set; }
        [DataMember] public string Phone { get; set; }
        [DataMember] public string Role { get; set; } = "Client";
        [DataMember] public bool IsActive { get; set; } = true;
        [DataMember] public DateTime CreatedAt { get; set; } = DateTime.Now;
        [DataMember] public string PreferredCurrency { get; set; } = "ILS";

        /// <summary>
        /// Israeli VAT classification (registration with VAT authority):
        ///   "Patur"      = Osek Patur — VAT-exempt, revenue ≤ ~120k₪/yr.
        ///   "Murshe"     = Osek Murshe — collects/deducts 18% VAT.
        ///   "Individual" = private (no VAT filing).
        /// (Osek Zair is NOT a VAT classification — it is a separate income-tax
        ///  status that can be opted into on top of Patur or Murshe; see IsZair.)
        /// </summary>
        [DataMember] public string BusinessType { get; set; } = "Individual";

        /// <summary>
        /// Osek Zair income-tax status (חישוב נורמטיבי) — 2024 reform.
        /// Available to Patur or Murshe with annual revenue ≤ ~122,833₪.
        /// When on, income tax = revenue × 70 % through the progressive
        /// brackets (flat 30 % deemed-expenses, no receipts required).
        /// Does NOT change VAT obligations — those follow BusinessType.
        /// </summary>
        [DataMember] public bool IsZair { get; set; } = false;

        /// <summary>
        /// Multi-tenant link: when Role == "Employee" or "Client", this is the
        /// Owner-user-id they belong to (chosen at signup via invite code).
        /// Null/0 for Owner rows themselves.
        /// </summary>
        [DataMember] public int? OwnerId { get; set; }

        /// <summary>
        /// Owner-only display name shown across the system instead of the
        /// raw username (e.g. "Acme Studio" rather than "admin"). Optional —
        /// falls back to Username when blank.
        /// </summary>
        [DataMember] public string BusinessName { get; set; }

        /// <summary>
        /// Owner-only short invite code used by Employees / Clients to join
        /// the company at signup. Auto-generated on Owner creation; rotatable
        /// from Settings. Treated as a shared secret — not a user-visible PII.
        /// </summary>
        [DataMember] public string InviteCode { get; set; }
    }
}
