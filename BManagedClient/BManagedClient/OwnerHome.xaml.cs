using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BManagedClient
{
    /// <summary>
    /// Owner dashboard. Polls server every 15s to refresh stats and the
    /// notification badge — same DispatcherTimer pattern as Driver-moodle.
    /// </summary>
    public partial class OwnerHome : Page
    {
        private DispatcherTimer pollTimer;

        public OwnerHome()
        {
            InitializeComponent();
            welcome.Text = LogIn.sign.Username;

            // Guard wrong role.
            if (!ClientSession.IsOwner)
            {
                MessageBox.Show("Access denied.");
                NavigationService?.Navigate(new LogIn());
                return;
            }

            RefreshStats();

            pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            pollTimer.Tick += (s, e) => RefreshStats();
            pollTimer.Start();
            Unloaded += (s, e) => pollTimer.Stop();
        }

        private void RefreshStats()
        {
            try
            {
                var customers = ServiceGateway.Use(c => c.GetCustomersForOwner(LogIn.sign.Id));
                customersCount.Text = (customers?.Length ?? 0).ToString();

                var unpaid = ServiceGateway.Use(c => c.GetUnpaidInvoices(LogIn.sign.Id));
                unpaidCount.Text = (unpaid?.Length ?? 0).ToString();

                var overdue = ServiceGateway.Use(c => c.GetOverdueInvoices(LogIn.sign.Id));
                overdueCount.Text = (overdue?.Length ?? 0).ToString();

                var projs = ServiceGateway.Use(c => c.GetProjectsByStatus("Active", LogIn.sign.Id));
                activeProjects.Text = (projs?.Length ?? 0).ToString();

                // Auto-create overdue notifications on the server side
                try { ServiceGateway.Use(c => c.EnsureOverdueNotifications(LogIn.sign.Id)); } catch { }

                int unread = ServiceGateway.Use(c => c.GetUnreadNotificationCount(LogIn.sign.Id));
                if (unread > 0)
                {
                    notifBadgeText.Text = unread > 99 ? "99+" : unread.ToString();
                    notifBadge.Visibility = Visibility.Visible;
                }
                else
                {
                    notifBadge.Visibility = Visibility.Collapsed;
                }

                // 3-month cashflow forecast
                try
                {
                    string cur = LogIn.sign.PreferredCurrency ?? "ILS";
                    var forecast = ServiceGateway.Use(c => c.GetCashFlowForecast(LogIn.sign.Id, 3, cur));
                    if (forecast != null && forecast.Length > 0 && forecastPanel != null)
                    {
                        var anchor = DateTime.Today;
                        forecastPanel.Children.Clear();
                        for (int i = 0; i < forecast.Length; i++)
                        {
                            var f = forecast[i];
                            var monthLabel = anchor.AddMonths(i + 1).ToString("MMM yyyy");
                            var fg = f.Profit >= 0
                                ? (System.Windows.Media.Brush)Application.Current.Resources["Mint"]
                                : (System.Windows.Media.Brush)Application.Current.Resources["Rose"];
                            var card = new Border
                            {
                                Background  = (System.Windows.Media.Brush)Application.Current.Resources["Paper100"],
                                CornerRadius = new CornerRadius(14),
                                Padding     = new Thickness(16),
                                Margin      = new Thickness(0, 0, 8, 0),
                            };
                            var sp = new StackPanel();
                            sp.Children.Add(new TextBlock
                            {
                                Text = monthLabel,
                                FontSize = 11, FontWeight = FontWeights.SemiBold,
                                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["Ink40"],
                            });
                            sp.Children.Add(new TextBlock
                            {
                                Text = "Income: " + f.Income.ToString("N0") + " " + cur,
                                FontSize = 12, Margin = new Thickness(0, 4, 0, 0),
                            });
                            sp.Children.Add(new TextBlock
                            {
                                Text = "Expenses: " + f.Expenses.ToString("N0") + " " + cur,
                                FontSize = 12,
                            });
                            sp.Children.Add(new TextBlock
                            {
                                Text = f.Profit.ToString("N0") + " " + cur,
                                FontSize = 22, FontWeight = FontWeights.Bold,
                                Foreground = fg, Margin = new Thickness(0, 6, 0, 0),
                            });
                            card.Child = sp;
                            forecastPanel.Children.Add(card);
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("RefreshStats: " + ex.Message); }
        }

        private void Customers_Click(object s, RoutedEventArgs e)     => NavigationService?.Navigate(new Customers());
        private void Projects_Click(object s, RoutedEventArgs e)      => NavigationService?.Navigate(new Projects());
        private void Invoices_Click(object s, RoutedEventArgs e)      => NavigationService?.Navigate(new Invoices());
        private void Contracts_Click(object s, RoutedEventArgs e)     => NavigationService?.Navigate(new Contracts());
        private void Expenses_Click(object s, RoutedEventArgs e)      => NavigationService?.Navigate(new Expenses());
        private void Reports_Click(object s, RoutedEventArgs e)       => NavigationService?.Navigate(new Reports());
        private void Settings_Click(object s, RoutedEventArgs e)      => NavigationService?.Navigate(new ManageUsers());
        private void Notifications_Click(object s, RoutedEventArgs e) => NavigationService?.Navigate(new Notifications());

        private void Logout_Click(object s, RoutedEventArgs e)
        {
            if (MessageBox.Show("Logout?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                pollTimer?.Stop();
                LogIn.sign = new Sign();
                NavigationService?.Navigate(new LogIn());
            }
        }
    }
}
