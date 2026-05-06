using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BManagedClient
{
    public partial class ClientHome : Page
    {
        private DispatcherTimer pollTimer;

        public ClientHome()
        {
            InitializeComponent();
            if (!ClientSession.IsClient)
            {
                MessageBox.Show("Client login required.");
                NavigationService?.Navigate(new LogIn());
                return;
            }

            welcome.Text = LogIn.sign.Username;
            outstandingCurrency.Text = LogIn.sign.PreferredCurrency ?? "ILS";
            Refresh();

            pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            pollTimer.Tick += (s, e) => Refresh();
            pollTimer.Start();
            Unloaded += (s, e) => pollTimer.Stop();
        }

        private void Refresh()
        {
            try
            {
                // Demo data lines up Client.user.Id ↔ Customer.Id (same seed pattern as web Portal).
                var invoices = ServiceGateway.Use(c => c.GetInvoicesByCustomer(LogIn.sign.Id));
                if (invoices == null || invoices.Length == 0)
                {
                    invoicesList.ItemsSource = null;
                    outstandingValue.Text = "0";
                    unpaidCount.Text = "0";
                    paidCount.Text = "0";
                    emptyHint.Visibility = Visibility.Visible;
                    return;
                }
                emptyHint.Visibility = Visibility.Collapsed;

                var ordered = invoices.OrderByDescending(i => i.IssueDate).ToArray();
                invoicesList.ItemsSource = ordered;

                decimal outstanding = ordered.Where(i => i.Status != "Paid").Sum(i => i.Total);
                outstandingValue.Text = outstanding.ToString("N0");
                unpaidCount.Text = ordered.Count(i => i.Status != "Paid").ToString();
                paidCount.Text   = ordered.Count(i => i.Status == "Paid").ToString();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        private void Settings_Click(object s, RoutedEventArgs e)
            => NavigationService?.Navigate(new Settings());

        private void Logout_Click(object s, RoutedEventArgs e)
        {
            pollTimer?.Stop();
            LogIn.sign = new Sign();
            NavigationService?.Navigate(new LogIn());
        }
    }
}
