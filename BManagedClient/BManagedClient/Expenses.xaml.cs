using BManagedClient.BMsrv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    public partial class Expenses : Page
    {
        public Expenses()
        {
            InitializeComponent();
            if (!ClientSession.IsOwner && !ClientSession.IsEmployee)
            { NavigationService?.Navigate(new LogIn()); return; }
            LoadCategories();
            Refresh();
        }

        private void LoadCategories()
        {
            var arr = ServiceGateway.Use(c => c.GetExpenseCategories());
            catCombo.ItemsSource = arr;
            if (arr != null && arr.Length > 0) catCombo.SelectedIndex = 0;
        }

        private void Refresh()
        {
            var arr = ServiceGateway.Use(c => c.GetExpensesByOwner(LogIn.sign.Id));
            expList.ItemsSource = arr;
        }

        private void Add_Click(object s, RoutedEventArgs e)
        {
            decimal.TryParse(amountBox.Text, out decimal amt);
            decimal.TryParse(vatBox.Text, out decimal vat);
            try
            {
                var kind = (kindCombo?.SelectedValue as string) ?? "";
                ServiceGateway.Use(c => c.AddExpense(new Expense
                {
                    OwnerId = LogIn.sign.Id,
                    CategoryId = catCombo.SelectedValue is int cid ? cid : (int?)null,
                    Date = DateTime.Today,
                    Amount = amt,
                    VatPaid = vat,
                    Vendor = vendorBox.Text ?? "",
                    Currency = LogIn.sign.PreferredCurrency,
                    RecurringKind = string.IsNullOrWhiteSpace(kind) ? null : kind
                }));
                vendorBox.Text = ""; amountBox.Text = "0"; vatBox.Text = "0";
                if (kindCombo != null) kindCombo.SelectedIndex = 0;
                Refresh();
            }
            catch (Exception ex) { MessageBox.Show("Add failed: " + ex.Message); }
        }

        private void Back_Click(object s, RoutedEventArgs e)
            => NavigationService?.Navigate(ClientSession.IsOwner ? (Page)new OwnerHome() : new EmployeeHome());

        // Auto VAT: from a gross amount, derive the VAT portion at 18 % (Israeli
        // standard since Jan 2025). gross = net + vat = net*1.18 → vat = gross*18/118.
        // Osek Patur cannot deduct VAT on expenses, so VAT is forced to 0.
        private decimal ComputeVatFromGross(decimal gross)
        {
            if (LogIn.sign != null && LogIn.sign.IsPatur) return 0m;
            return Math.Round(gross * 18m / 118m, 2);
        }

        private void AutoVat_Click(object s, RoutedEventArgs e)
        {
            if (decimal.TryParse(amountBox.Text, out decimal gross) && gross > 0)
                vatBox.Text = ComputeVatFromGross(gross).ToString("0.##");
        }

        // Live recalc: when the user types in Amount, refresh VAT only if they
        // haven't manually overridden it yet (VAT still 0 / blank).
        private void Amount_Changed(object s, TextChangedEventArgs e)
        {
            if (vatBox == null) return;
            if (string.IsNullOrWhiteSpace(vatBox.Text) || vatBox.Text.Trim() == "0")
            {
                if (decimal.TryParse(amountBox.Text, out decimal gross) && gross > 0)
                    vatBox.Text = ComputeVatFromGross(gross).ToString("0.##");
            }
        }
    }
}
