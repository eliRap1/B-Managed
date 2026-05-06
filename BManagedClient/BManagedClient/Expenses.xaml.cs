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
                ServiceGateway.Use(c => c.AddExpense(new Expense
                {
                    OwnerId = LogIn.sign.Id,
                    CategoryId = catCombo.SelectedValue is int cid ? cid : (int?)null,
                    Date = DateTime.Today,
                    Amount = amt,
                    VatPaid = vat,
                    Vendor = vendorBox.Text ?? "",
                    Currency = LogIn.sign.PreferredCurrency
                }));
                vendorBox.Text = ""; amountBox.Text = "0"; vatBox.Text = "0";
                Refresh();
            }
            catch (Exception ex) { MessageBox.Show("Add failed: " + ex.Message); }
        }

        private void Back_Click(object s, RoutedEventArgs e)
            => NavigationService?.Navigate(ClientSession.IsOwner ? (Page)new OwnerHome() : new EmployeeHome());
    }
}
