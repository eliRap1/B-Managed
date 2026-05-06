using BManagedClient.BMsrv;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    public partial class EmployeeProjects : Page
    {
        private Project[] _all = new Project[0];
        private bool _ready;

        public EmployeeProjects()
        {
            InitializeComponent();
            if (!ClientSession.IsEmployee) { NavigationService?.Navigate(new LogIn()); return; }
            _ready = true;
            Reload();
        }

        private void Reload()
        {
            if (!_ready) return;
            try
            {
                _all = ServiceGateway.Use(c => c.GetProjectsForEmployee(LogIn.sign.Id)) ?? new Project[0];
                Filter();
            }
            catch (Exception ex) { MessageBox.Show("Load failed: " + ex.Message); }
        }

        private void Filter()
        {
            string status = (statusFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
            var view = status == "All" ? _all : _all.Where(p => p.Status == status).ToArray();
            projectsList.ItemsSource = view;
            subtitle.Text = view.Length + " of " + _all.Length + " project" + (_all.Length == 1 ? "" : "s") + ".";
        }

        private void Filter_Changed(object s, SelectionChangedEventArgs e) { if (_ready) Filter(); }

        private void Project_Selected(object s, SelectionChangedEventArgs e)
        {
            if (projectsList.SelectedItem is not Project p)
            {
                detTitle.Text = "Select a project above to see its details.";
                detDescription.Text = "";
                detCustomer.Text = detStatus.Text = detDue.Text = "";
                return;
            }
            detTitle.Text = p.Title ?? "";
            detDescription.Text = string.IsNullOrEmpty(p.Description) ? "(no description)" : p.Description;
            detStatus.Text = p.Status ?? "";
            detDue.Text = p.DueDate?.ToString("dd MMM yyyy") ?? "—";

            // Customer name lookup — server only stores customerId on the project.
            try
            {
                var c = ServiceGateway.Use(s2 => s2.GetCustomerById(p.CustomerId));
                detCustomer.Text = c?.BusinessName ?? ("#" + p.CustomerId);
            }
            catch { detCustomer.Text = "#" + p.CustomerId; }
        }

        private void Back_Click(object s, RoutedEventArgs e)
            => NavigationService?.Navigate(new EmployeeHome());
    }
}
