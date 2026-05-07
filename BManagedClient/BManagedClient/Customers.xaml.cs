using BManagedClient.BMsrv;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    // =========================================================================
    // Customers page (WPF) — Owner-only CRM.
    // -------------------------------------------------------------------------
    // Layout (XAML):
    //   Header → Search bar (with '+ New customer' toggle) → optional
    //   inline add-form panel (collapsed by default) → ListView of
    //   customers → status TextBlock at the bottom.
    // SOAP ops:
    //   GetCustomersForOwner / SearchCustomers — both pass LogIn.sign.Id
    //   so company A can never see company B's customers.
    //   AddCustomer.
    // Validation (per-field):
    //   Same regexes as the Web page — EmailRx and PhoneRx are matched in
    //   SaveAdd_Click. Bad fields get a red BorderBrush (1.5px); the first
    //   error message lands in the existing 'status' TextBlock.
    // History:
    //   Pre-May-2026 the '+ New customer' button used Microsoft.VisualBasic
    //   InputBox to grab only the business name — every other column ended
    //   up blank. Replaced with a real inline form to collect contact
    //   details + tax id + email + phone + address + currency.
    // =========================================================================
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

            // Per-field red-border feedback. Reset on every save attempt.
            var rose = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["Rose"];
            var soft = System.Windows.Media.Brushes.Transparent;
            bizBox.BorderBrush = soft; emailBox.BorderBrush = soft; phoneBox.BorderBrush = soft;
            bool bad = false; string firstErr = null;
            void Bad(System.Windows.Controls.TextBox b, string msg)
            { b.BorderBrush = rose; b.BorderThickness = new Thickness(1.5); bad = true; if (firstErr == null) firstErr = msg; }

            if (string.IsNullOrEmpty(biz))                              Bad(bizBox,   "Business name is required.");
            if (!string.IsNullOrEmpty(email) && !EmailRx.IsMatch(email)) Bad(emailBox, "Email looks invalid (e.g. name@example.com).");
            if (!string.IsNullOrEmpty(phone) && !PhoneRx.IsMatch(phone)) Bad(phoneBox, "Phone must be 7–15 digits, optional + country code.");
            if (bad) { status.Text = firstErr; return; }

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
