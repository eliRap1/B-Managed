using System;
using System.Runtime.Serialization;

namespace Model
{
    /// <summary>
    /// Business loan held by an Owner.
    ///   Principal (Hebrew: קרן) is the original borrowed amount.
    ///   IsKerenBacked marks loans from a state-backed business loan fund
    ///   (קרן הלוואות בערבות מדינה / קרן שמש / קרן קורת etc.) which usually
    ///   carry a lower interest rate and a state guarantee.
    /// </summary>
    [DataContract]
    public class Loan : Base
    {
        [DataMember] public int OwnerId { get; set; }
        [DataMember] public string Lender { get; set; }
        [DataMember] public decimal Principal { get; set; }              // קרן
        [DataMember] public decimal RemainingBalance { get; set; }
        [DataMember] public double  InterestRatePct { get; set; }        // annualised, e.g. 6.5 = 6.5%
        [DataMember] public decimal MonthlyPayment { get; set; }
        [DataMember] public DateTime StartDate { get; set; } = DateTime.Today;
        [DataMember] public int     TermMonths { get; set; }
        [DataMember] public DateTime? NextPaymentDate { get; set; }
        [DataMember] public string Currency { get; set; } = "ILS";
        [DataMember] public string Purpose { get; set; }                 // free text — equipment, working capital, marketing
        [DataMember] public bool   IsKerenBacked { get; set; }           // state-backed business fund
        [DataMember] public bool   HasStandingOrder { get; set; }        // הוראת קבע — bank auto-charges every month
        [DataMember] public bool   IsActive { get; set; } = true;
        [DataMember] public string Notes { get; set; }
        [DataMember] public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>Single payment recorded against a Loan.</summary>
    [DataContract]
    public class LoanPayment : Base
    {
        [DataMember] public int LoanId { get; set; }
        [DataMember] public DateTime PaidDate { get; set; } = DateTime.Today;
        [DataMember] public decimal Amount { get; set; }
        [DataMember] public decimal PrincipalPortion { get; set; }
        [DataMember] public decimal InterestPortion { get; set; }
        [DataMember] public string Notes { get; set; }
    }

    /// <summary>
    /// Aggregate snapshot of an Owner's loan exposure, joined against trailing
    /// income for a debt-to-income reading. All amounts in DisplayCurrency.
    /// </summary>
    [DataContract]
    public class LoanSummary
    {
        [DataMember] public int    LoanCount { get; set; }
        [DataMember] public decimal TotalPrincipal { get; set; }
        [DataMember] public decimal TotalRemaining { get; set; }
        [DataMember] public decimal MonthlyPaymentTotal { get; set; }
        [DataMember] public int    KerenBackedCount { get; set; }

        /// <summary>Total remaining balance ÷ projected annual income (avg trailing-3-month × 12). 0 if no income.</summary>
        [DataMember] public double DebtToAnnualIncomePct { get; set; }
        /// <summary>Total monthly loan payment ÷ avg monthly income. 0 if no income.</summary>
        [DataMember] public double MonthlyDebtServiceRatioPct { get; set; }

        [DataMember] public DateTime? NextPaymentDate { get; set; }
        [DataMember] public decimal NextPaymentAmount { get; set; }
        [DataMember] public string  DisplayCurrency { get; set; } = "ILS";
    }
}
