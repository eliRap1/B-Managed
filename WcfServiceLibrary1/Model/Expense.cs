using System;
using System.Runtime.Serialization;

namespace Model
{
    /// <summary>
    /// Business expense logged by the owner (or an employee on the owner's
    /// behalf). VAT-deductible flag comes from the linked category.
    /// </summary>
    [DataContract]
    public class Expense : Base
    {
        [DataMember] public int OwnerId { get; set; }
        [DataMember] public int? CategoryId { get; set; }
        [DataMember] public DateTime Date { get; set; } = DateTime.Today;
        [DataMember] public decimal Amount { get; set; }
        [DataMember] public decimal VatPaid { get; set; }
        [DataMember] public string Vendor { get; set; }
        [DataMember] public string Description { get; set; }
        [DataMember] public int? ProjectId { get; set; }
        [DataMember] public string ReceiptPath { get; set; }
        [DataMember] public string Currency { get; set; } = "ILS";
        // Recurring marker — null/"" = one-time, "Fixed" = recurring fixed-amount
        // (rent, insurance), "Variable" = recurring variable-amount (electricity,
        // utilities). Used for hora'at-keva tracking and budget forecasting.
        [DataMember] public string RecurringKind { get; set; }
    }
}
