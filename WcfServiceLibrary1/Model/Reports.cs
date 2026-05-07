using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Model
{
    /// <summary>
    /// VAT summary returned by GetVatSummary. Server normalises every row
    /// to the requested DisplayCurrency before aggregating.
    /// </summary>
    [DataContract]
    public class VatSummary
    {
        [DataMember] public int Year { get; set; }
        [DataMember] public int Month { get; set; }
        [DataMember] public decimal VatCollected { get; set; }
        [DataMember] public decimal VatPaid { get; set; }
        [DataMember] public decimal VatDue { get; set; }
        [DataMember] public decimal IncomeTotal { get; set; }
        [DataMember] public decimal ExpensesTotal { get; set; }
        [DataMember] public string DisplayCurrency { get; set; } = "ILS";
    }

    /// <summary>P&amp;L for a date range.</summary>
    [DataContract]
    public class ProfitLoss
    {
        [DataMember] public decimal Income { get; set; }
        [DataMember] public decimal Expenses { get; set; }
        [DataMember] public decimal Profit { get; set; }
        [DataMember] public decimal TaxSetAsideEstimate { get; set; }
        [DataMember] public string DisplayCurrency { get; set; } = "ILS";
    }

    /// <summary>Single row in the expense breakdown report (per-category).</summary>
    [DataContract]
    public class ExpenseBreakdownRow
    {
        [DataMember] public string CategoryName { get; set; }
        [DataMember] public decimal Total { get; set; }
        [DataMember] public bool IsVatDeductible { get; set; }
        [DataMember] public string DisplayCurrency { get; set; } = "ILS";
    }

    /// <summary>Top-customer-by-revenue row.</summary>
    [DataContract]
    public class CustomerRevenueRow
    {
        [DataMember] public int CustomerId { get; set; }
        [DataMember] public string BusinessName { get; set; }
        [DataMember] public decimal TotalInvoiced { get; set; }
        [DataMember] public decimal TotalPaid { get; set; }
        [DataMember] public string DisplayCurrency { get; set; } = "ILS";
    }

    /// <summary>Employee-driven revenue row (INNER JOIN Invoices+Projects+Users).</summary>
    [DataContract]
    public class EmployeeRevenueRow
    {
        [DataMember] public int EmployeeId { get; set; }
        [DataMember] public string EmployeeName { get; set; }
        [DataMember] public int ProjectsCount { get; set; }
        [DataMember] public decimal RevenueGenerated { get; set; }
        [DataMember] public string DisplayCurrency { get; set; } = "ILS";
    }

    /// <summary>
    /// Composite KPI scorecard for the Reports / Owner Home dashboards.
    /// All amounts are pre-converted to <see cref="DisplayCurrency"/>.
    /// </summary>
    [DataContract]
    public class AnalyticsKpis
    {
        // Receivables aging — outstanding (Sent / Overdue, not Paid) invoice
        // amounts bucketed by how long they have been due.
        [DataMember] public decimal AgingCurrent { get; set; }   // not yet due
        [DataMember] public decimal Aging1To30 { get; set; }     // 1–30 days late
        [DataMember] public decimal Aging31To60 { get; set; }    // 31–60 days late
        [DataMember] public decimal Aging61Plus { get; set; }    // 61+ days late
        [DataMember] public decimal TotalOutstanding { get; set; }

        // Payment behaviour over the trailing 6 months of paid invoices.
        [DataMember] public double  AvgDaysToPayment { get; set; }
        [DataMember] public double  OnTimeRatePct { get; set; }   // % paid on/before due date

        // Customer concentration — risk if revenue is too concentrated in one
        // customer. We report the top customer's share of YTD revenue.
        [DataMember] public string  TopCustomerName { get; set; }
        [DataMember] public double  TopCustomerSharePct { get; set; }
        [DataMember] public int     ActiveCustomerCount { get; set; }   // had revenue YTD

        // Trailing-3-month average flow + simple runway estimate. Runway = months
        // before current cumulative profit hits zero at recent burn pace; -1 if
        // the business is profitable (no runway concern).
        [DataMember] public decimal AvgMonthlyIncome { get; set; }
        [DataMember] public decimal AvgMonthlyExpenses { get; set; }
        [DataMember] public decimal AvgMonthlyProfit { get; set; }
        [DataMember] public double  RunwayMonths { get; set; } // -1 = profitable / no burn

        [DataMember] public string  DisplayCurrency { get; set; } = "ILS";
    }

    /// <summary>
    /// Single payload for the Owner dashboard. Folds ~10 separate SOAP ops
    /// into one round-trip so the dashboard renders fast even with a cold
    /// WCF channel.
    /// </summary>
    [DataContract]
    public class OwnerDashboardSnapshot
    {
        [DataMember] public int CustomersCount { get; set; }
        [DataMember] public int UnpaidCount { get; set; }
        [DataMember] public int OverdueCount { get; set; }
        [DataMember] public int ActiveProjectsCount { get; set; }
        [DataMember] public int UnreadNotificationsCount { get; set; }
        [DataMember] public List<ProfitLoss> CashFlowForecast { get; set; } = new List<ProfitLoss>();
        [DataMember] public AnalyticsKpis Kpis { get; set; }
        [DataMember] public LoanSummary LoanSummary { get; set; }

        // Rolled in May 2026 to absorb 5 more SOAP ops:
        [DataMember] public decimal UnpaidTotal { get; set; }
        [DataMember] public decimal VatDue { get; set; }
        [DataMember] public decimal MonthlyTaxSetAside { get; set; }
        [DataMember] public string  TopExpenseCategory { get; set; }
        [DataMember] public decimal TopExpenseAmount { get; set; }
        [DataMember] public decimal RevenueChangePct { get; set; }
        [DataMember] public List<RecentInvoice> RecentInvoices { get; set; } = new List<RecentInvoice>();

        [DataMember] public string DisplayCurrency { get; set; } = "ILS";
    }

    /// <summary>Compact recent-invoice row (already joined to customer name).</summary>
    [DataContract]
    public class RecentInvoice
    {
        [DataMember] public string InvoiceNumber { get; set; }
        [DataMember] public string CustomerName  { get; set; }
        [DataMember] public decimal Total        { get; set; }
        [DataMember] public string Currency      { get; set; }
        [DataMember] public string Status        { get; set; }
    }

    /// <summary>Bundles every read /Owner/Reports needs for one round-trip.</summary>
    [DataContract]
    public class ReportsSnapshot
    {
        [DataMember] public VatSummary Vat { get; set; }
        [DataMember] public List<CustomerRevenueRow> TopCustomers { get; set; } = new List<CustomerRevenueRow>();
        [DataMember] public List<ExpenseBreakdownRow> ExpenseBreakdown { get; set; } = new List<ExpenseBreakdownRow>();
        [DataMember] public ProfitLoss MonthPl { get; set; }
        [DataMember] public ProfitLoss YearPl  { get; set; }
        [DataMember] public AnalyticsKpis Kpis { get; set; }
        [DataMember] public LoanSummary LoanSummary { get; set; }
        [DataMember] public string  BusinessType { get; set; }
        [DataMember] public bool    IsZair { get; set; }
        [DataMember] public string  DisplayCurrency { get; set; } = "ILS";
    }
}
