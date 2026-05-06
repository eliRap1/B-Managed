using BManagedClient.BMsrv;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    public partial class Customers : Page
    {
        // Same lenient regexes as the SignUp page so customer details and user
        // details validate consistently.
        private static readonly Regex EmailRx = new Regex(@"^[\w\.\-]+@[\w\-]+\.[\w\-\.]+$");
        private static readonly Regex PhoneRx = new Regex(@"^\+?\d{7,15}$");

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

        private void ToggleAdd_Click(object s, RoutedEventArgs e)
        {
            bool show = addPanel.Visibility != Visibility.Visible;
            addPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                ResetForm();
                bizBox.Focus();
            }
        }

        private void CancelAdd_Click(object s, RoutedEventArgs e)
        {
            addPanel.Visibility = Visibility.Collapsed;
            ResetForm();
        }

        private void ResetForm()
        {
            bizBox.Text = ""; contactBox.Text = ""; taxBox.Text = "";
            emailBox.Text = ""; phoneBox.Text = ""; addressBox.Text = "";
            curBox.SelectedIndex = 0;
        }

        private void SaveAdd_Click(object s, RoutedEventArgs e)
        {
            string biz     = bizBox.Text?.Trim()     ?? "";
            string contact = contactBox.Text?.Trim() ?? "";
            string tax     = taxBox.Text?.Trim()     ?? "";
            string email   = emailBox.Text?.Trim()   ?? "";
            string phone   = phoneBox.Text?.Trim()   ?? "";
            string addr    = addressBox.Text?.Trim() ?? "";
            string cur     = (curBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ILS";

            if (string.IsNullOrEmpty(biz))
            { status.Text = "Business name is required."; return; }
            if (!string.IsNullOrEmpty(email) && !EmailRx.IsMatch(email))
            { status.Text = "Email looks invalid."; return; }
            if (!string.IsNullOrEmpty(phone) && !PhoneRx.IsMatch(phone))
            { status.Text = "Phone looks invalid."; return; }

            var c = new Customer
            {
                BusinessName      = biz,
                ContactName       = contact,
                TaxId             = tax,
                Email             = email,
                Phone             = phone,
                Address           = addr,
                OwnerId           = LogIn.sign.Id,
                PreferredCurrency = cur,
            };
            try
            {
                ServiceGateway.Use(s2 => s2.AddCustomer(c));
                addPanel.Visibility = Visibility.Collapsed;
                ResetForm();
                Refresh();
            }
            catch (Exception ex) { status.Text = "Add failed: " + ex.Message; }
        }

        private void Back_Click(object s, RoutedEventArgs e) => NavigationService?.Navigate(new OwnerHome());
    }
}
