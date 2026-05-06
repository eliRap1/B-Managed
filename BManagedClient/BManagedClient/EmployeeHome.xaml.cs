using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BManagedClient
{
    public partial class EmployeeHome : Page
    {
        private DispatcherTimer pollTimer;

        public EmployeeHome()
        {
            InitializeComponent();
            if (!ClientSession.IsEmployee)
            {
                MessageBox.Show("Access denied.");
                NavigationService?.Navigate(new LogIn());
                return;
            }

            welcome.Text = LogIn.sign.Username;
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
                var projects = ServiceGateway.Use(c => c.GetProjectsForEmployee(LogIn.sign.Id));
                projectsList.ItemsSource = projects;
                projectsCount.Text = (projects?.Length ?? 0).ToString();

                // My-only expenses: filter by ownerId match.
                var allExp = ServiceGateway.Use(c => c.GetExpensesByOwner(LogIn.sign.Id));
                expensesCount.Text = (allExp?.Length ?? 0).ToString();

                int unread = ServiceGateway.Use(c => c.GetUnreadNotificationCount(LogIn.sign.Id));
                notifCount.Text = unread.ToString();
                if (unread > 0)
                {
                    notifBadgeText.Text = unread > 99 ? "99+" : unread.ToString();
                    notifBadge.Visibility = Visibility.Visible;
                }
                else
                {
                    notifBadge.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        private void Projects_Click(object s, RoutedEventArgs e)
        {
            // Employees can browse the list of their assigned projects in detail.
            // Re-purpose the Projects page: Projects.xaml hosts the Owner CRUD,
            // so we just route Employee back to this dashboard which already shows them.
            NavigationService?.Navigate(new EmployeeHome());
        }

        private void Expenses_Click(object s, RoutedEventArgs e)
            => NavigationService?.Navigate(new Expenses());

        private void Notifications_Click(object s, RoutedEventArgs e)
            => NavigationService?.Navigate(new Notifications());

        private void Logout_Click(object s, RoutedEventArgs e)
        {
            pollTimer?.Stop();
            LogIn.sign = new Sign();
            NavigationService?.Navigate(new LogIn());
        }
    }
}
