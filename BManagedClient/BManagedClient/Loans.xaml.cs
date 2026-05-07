using BManagedClient.BMsrv;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    // =========================================================================
    // Loans page (WPF) — Owner-only.
    // -------------------------------------------------------------------------
    // Mirrors /Owner/Loans on the Web. Two halves:
    //   * Top: KPI strip (active count, outstanding, monthly payment,
    //          DSR vs annual income) + threshold warning banner at >=25%
    //          and >=40% (industry yellow / red bands).
    //   * Bottom: ListView of every loan + add-loan form + 'record payment'
    //          panel that pre-fills 70% principal split (rough industry
    //          default; the user can override).
    // SOAP ops:
    //   GetLoansForOwner / GetLoanSummary — both per-Owner-scoped.
    //   AddLoan / DeleteLoan / RecordLoanPayment.
    // Israeli context:
    //   IsKerenBacked flag marks loans from state-backed business funds
    //   (קרן הלוואות בערבות מדינה / קרן שמש / קרן קורת). These usually
    //   carry lower rates — surfaced visually with a 'state-backed' label.
    // =========================================================================
    public partial class Loans : Page
    {
        public Loans()
        {
            InitializeComponent();
            if (!ClientSession.IsOwner) { NavigationService?.Navigate(new LogIn()); return; }
            startBox.SelectedDate = DateTime.Today;
            payDateBox.SelectedDate = DateTime.Today;
            Refresh();
        }

        private string CurrentCurrency =>
            (LogIn.sign != null && !string.IsNullOrEmpty(LogIn.sign.PreferredCurrency))
                ? LogIn.sign.PreferredCurrency : "ILS";

        private Loan Selected => loansList.SelectedItem as Loan;

        private void Refresh()
        {
            try
            {
                var arr = ServiceGateway.Use(c => c.GetLoansForOwner(LogIn.sign.Id)) ?? new Loan[0];
                loansList.ItemsSource = arr;

                var s = ServiceGateway.Use(c => c.GetLoanSummary(LogIn.sign.Id, CurrentCurrency)) ?? new LoanSummary();
                kpiCount.Text     = s.LoanCount.ToString();
                kpiKeren.Text     = s.KerenBackedCount > 0
                    ? s.KerenBackedCount + " state-backed (קרן)"
                    : "";
                kpiRemaining.Text = s.TotalRemaining.ToString("N0") + " " + (s.DisplayCurrency ?? CurrentCurrency);
                kpiPrincipal.Text = "of original " + s.TotalPrincipal.ToString("N0");
                kpiMonthly.Text   = s.MonthlyPaymentTotal.ToString("N0") + " " + (s.DisplayCurrency ?? CurrentCurrency);
                kpiNext.Text      = s.NextPaymentDate.HasValue
                    ? "Next: " + s.NextPaymentDate.Value.ToString("dd/MM/yyyy")
                    : "";
                kpiDsr.Text       = s.DebtToAnnualIncomePct <= 0 ? "—" : s.DebtToAnnualIncomePct.ToString("N1") + "%";
                kpiBurden.Text    = s.MonthlyDebtServiceRatioPct <= 0 ? ""
                    : "Monthly burden: " + s.MonthlyDebtServiceRatioPct.ToString("N1") + "% of income";

                // Threshold banner
                if (s.MonthlyDebtServiceRatioPct >= 40)
                {
                    warningText.Text = "⚠️ Your monthly loan payments exceed 40% of recent monthly income. This is a high debt-service ratio.";
                    warningBox.Visibility = Visibility.Visible;
                }
                else if (s.MonthlyDebtServiceRatioPct >= 25)
                {
                    warningText.Text = "Monthly loan payments are 25–40% of recent income. Watch this ratio.";
                    warningBox.Visibility = Visibility.Visible;
                }
                else
                {
                    warningBox.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex) { addStatus.Text = "Load failed: " + ex.Message; }
        }

        private void Loan_Selected(object s, SelectionChangedEventArgs e)
        {
            if (Selected != null && payAmountBox != null)
            {
                payAmountBox.Text    = Selected.MonthlyPayment.ToString("F2");
                // Default principal portion ≈ 70% of payment (rough) — Owner can adjust.
                payPrincipalBox.Text = Math.Round(Selected.MonthlyPayment * 0.7m, 2).ToString("F2");
            }
        }

        private void SaveLoan_Click(object s, RoutedEventArgs e)
        {
            string lender = lenderBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(lender)) { addStatus.Text = "Lender is required."; return; }
            if (!decimal.TryParse(principalBox.Text, out decimal principal) || principal <= 0)
            { addStatus.Text = "Principal must be > 0."; return; }
            double.TryParse(interestBox.Text, out double rate);
            if (!decimal.TryParse(monthlyBox.Text, out decimal monthly) || monthly <= 0)
            { addStatus.Text = "Monthly payment must be > 0."; return; }
            int.TryParse(termBox.Text, out int term);
            DateTime start = startBox.SelectedDate ?? DateTime.Today;
            string cur = (curBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ILS";

            try
            {
                ServiceGateway.Use(c => c.AddLoan(new Loan
                {
                    OwnerId          = LogIn.sign.Id,
                    Lender           = lender,
                    Principal        = principal,
                    RemainingBalance = principal,
                    InterestRatePct  = rate,
                    MonthlyPayment   = monthly,
                    StartDate        = start,
                    TermMonths       = term,
                    NextPaymentDate  = start.AddMonths(1),
                    Currency         = cur,
                    Purpose          = purposeBox.Text ?? "",
                    IsKerenBacked    = kerenBox.IsChecked == true,
                    IsActive         = true,
                    CreatedAt        = DateTime.Now,
                }));

                lenderBox.Text = ""; principalBox.Text = "0"; interestBox.Text = "0";
                monthlyBox.Text = "0"; termBox.Text = "60"; purposeBox.Text = "";
                kerenBox.IsChecked = false;
                addStatus.Text = "";
                Refresh();
            }
            catch (Exception ex) { addStatus.Text = "Failed: " + ex.Message; }
        }

        private void RecordPayment_Click(object s, RoutedEventArgs e)
        {
            if (Selected == null) { payStatus.Text = "Pick a loan first."; return; }
            if (!decimal.TryParse(payAmountBox.Text, out decimal amt) || amt <= 0)
            { payStatus.Text = "Amount must be > 0."; return; }
            decimal.TryParse(payPrincipalBox.Text, out decimal pp);
            if (pp <= 0) pp = Math.Round(amt * 0.7m, 2);
            if (pp > amt) pp = amt;

            try
            {
                ServiceGateway.Use(c => c.RecordLoanPayment(new LoanPayment
                {
                    LoanId           = Selected.Id,
                    PaidDate         = payDateBox.SelectedDate ?? DateTime.Today,
                    Amount           = amt,
                    PrincipalPortion = pp,
                    InterestPortion  = amt - pp,
                }));
                payStatus.Text = "Recorded " + amt.ToString("N2") + " (קרן " + pp.ToString("N2") + ").";
                Refresh();
            }
            catch (Exception ex) { payStatus.Text = "Failed: " + ex.Message; }
        }

        private void Delete_Click(object s, RoutedEventArgs e)
        {
            if (Selected == null) return;
            if (MessageBox.Show("Delete this loan and its payment history?", "Confirm",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try
            {
                ServiceGateway.Use(c => c.DeleteLoan(Selected.Id));
                Refresh();
            }
            catch (Exception ex) { MessageBox.Show("Failed: " + ex.Message); }
        }

        private void Back_Click(object s, RoutedEventArgs e)
            => NavigationService?.Navigate(new OwnerHome());
    }
}
