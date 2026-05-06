using BManagedClient.BMsrv;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    public partial class Customers : Page
    {
        public Customers()
        {
            InitializeComponent();
            if (!ClientSession.IsOwner)
            {
                MessageBox.Show("Owner role required.");
                NavigationService?.Navigate(new LogIn());
                return;
            }
            Refresh();
        }

        private void Refresh()
        {
            try
            {
                var list = ServiceGateway.Use(c => c.GetCustomersForOwner(LogIn.sign.Id));
                customersList.ItemsSource = list;
                status.Text = (list?.Length ?? 0) + " customers.";
            }
            catch (Exception ex) { status.Text = "Load failed: " + ex.Message; }
        }

        private void Search_Click(object s, RoutedEventArgs e)
        {
            string kw = searchBox.Text?.Trim() ?? "";
            try
            {
                var list = ServiceGateway.Use(c => c.SearchCustomers(kw, LogIn.sign.Id));
                customersList.ItemsSource = list;
                status.Text = (list?.Length ?? 0) + " match(es).";
            }
            catch (Exception ex) { status.Text = "Search failed: " + ex.Message; }
        }

        private void Add_Click(object s, RoutedEventArgs e)
        {
            string name = Microsoft.VisualBasic.Interaction.InputBox(
                "Business name?", "New Customer", "");
            if (string.IsNullOrWhiteSpace(name)) return;
            var c = new Customer
            {
                BusinessName     = name,
                OwnerId          = LogIn.sign.Id,
                PreferredCurrency = LogIn.sign.PreferredCurrency
            };
            try
            {
                ServiceGateway.Use(s2 => s2.AddCustomer(c));
                Refresh();
            }
            catch (Exception ex) { MessageBox.Show("Add failed: " + ex.Message); }
        }

        private void Back_Click(object s, RoutedEventArgs e) => NavigationService?.Navigate(new OwnerHome());
    }
}
