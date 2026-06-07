using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using ViewDB;

namespace BusinessLogic
{
    /// <summary>
    /// Business logic for loans and loan payments, plus the aggregated loan summary
    /// (currency-converted totals, next payment, and debt-service ratios joined
    /// against trailing income KPIs).
    /// </summary>
    public class LoanLogic
    {
        private readonly LoanDB    loanDB    = new LoanDB();
        private readonly ReportsDB reportsDB = new ReportsDB();

        public int  AddLoan(Loan l)
        {
            try { return loanDB.Insert(l); }
            catch (Exception ex) { throw new FaultException("AddLoan failed: " + ex.Message); }
        }
        public void UpdateLoan(Loan l)
        {
            try { loanDB.Update(l); }
            catch (Exception ex) { throw new FaultException("UpdateLoan failed: " + ex.Message); }
        }
        public void DeleteLoan(int id)
        {
            try { loanDB.Delete(id); }
            catch (Exception ex) { throw new FaultException("DeleteLoan failed: " + ex.Message); }
        }

        /// <summary>
        /// Ownership check: returns true only if the loan row exists and belongs to
        /// the given owner. Used by callers that receive a raw loan id from client input.
        /// </summary>
        public bool LoanBelongsToOwner(int loanId, int ownerId)
        {
            var loan = loanDB.GetById(loanId);
            return loan != null && loan.OwnerId == ownerId;
        }
        public Loan GetLoanById(int id) => loanDB.GetById(id);
        public List<Loan> GetLoansForOwner(int ownerId) => loanDB.GetForOwner(ownerId);
        public int RecordLoanPayment(LoanPayment p)
        {
            try { return loanDB.InsertPayment(p); }
            catch (Exception ex) { throw new FaultException("RecordLoanPayment failed: " + ex.Message); }
        }
        public List<LoanPayment> GetLoanPayments(int loanId) => loanDB.GetPaymentsByLoan(loanId);

        public LoanSummary GetLoanSummary(int ownerId, string displayCurrency)
        {
            string cur = string.IsNullOrEmpty(displayCurrency) ? "ILS" : displayCurrency;
            var loans = loanDB.GetForOwner(ownerId) ?? new List<Loan>();
            var s = new LoanSummary { DisplayCurrency = cur };
            var fx = new ViewDB.CurrencyConverter();
            DateTime today = DateTime.Today;

            foreach (var l in loans.Where(x => x.IsActive))
            {
                s.LoanCount++;
                if (l.IsKerenBacked) s.KerenBackedCount++;
                s.TotalPrincipal       += fx.Convert(l.Principal,        l.Currency, cur, today);
                s.TotalRemaining       += fx.Convert(l.RemainingBalance, l.Currency, cur, today);
                s.MonthlyPaymentTotal  += fx.Convert(l.MonthlyPayment,   l.Currency, cur, today);

                if (l.NextPaymentDate.HasValue)
                {
                    if (!s.NextPaymentDate.HasValue || l.NextPaymentDate.Value < s.NextPaymentDate.Value)
                    {
                        s.NextPaymentDate   = l.NextPaymentDate;
                        s.NextPaymentAmount = fx.Convert(l.MonthlyPayment, l.Currency, cur, today);
                    }
                }
            }

            // Debt-service ratios joined against trailing-3-month income
            try
            {
                var kpis = reportsDB.AdvancedKpis(ownerId, cur);
                if (kpis.AvgMonthlyIncome > 0)
                {
                    decimal annual = kpis.AvgMonthlyIncome * 12m;
                    s.DebtToAnnualIncomePct = annual <= 0 ? 0
                        : Math.Round((double)(s.TotalRemaining / annual) * 100.0, 1);
                    s.MonthlyDebtServiceRatioPct = Math.Round(
                        (double)(s.MonthlyPaymentTotal / kpis.AvgMonthlyIncome) * 100.0, 1);
                }
            }
            catch { }

            return s;
        }
    }
}
