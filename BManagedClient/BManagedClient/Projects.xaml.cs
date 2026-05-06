using BManagedClient.BMsrv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BManagedClient
{
    public partial class Projects : Page
    {
        private List<Customer> _customers = new();
        private List<User> _employees = new();
        private Project _selected;
        private bool _ready;

        public Projects()
        {
            InitializeComponent();
            if (!ClientSession.IsOwner) { NavigationService?.Navigate(new LogIn()); return; }
            LoadCustomers();
            LoadEmployees();
            _ready = true;
            Refresh();
        }

        private void LoadCustomers()
        {
            try
            {
                var arr = ServiceGateway.Use(c => c.GetCustomersForOwner(LogIn.sign.Id));
                _customers = (arr ?? new Customer[0]).ToList();
                customerCombo.ItemsSource = _customers;
                if (_customers.Count > 0) customerCombo.SelectedIndex = 0;
            }
            catch { }
        }

        private void LoadEmployees()
        {
            try
            {
                var users = ServiceGateway.Use(c => c.GetAllUsers());
                _employees = users == null
                    ? new List<User>()
                    : users.Where(u => u.Role == "Employee" && u.IsActive).ToList();
                employeeCombo.ItemsSource = _employees;
            }
            catch { }
        }

        private void Refresh()
        {
            if (!_ready || projectsList == null) return;
            try
            {
                var status = (statusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
                Project[] arr;
                if (status == "All")
                {
                    var list = new List<Project>();
                    foreach (var c in _customers)
                    {
                        var x = ServiceGateway.Use(s => s.GetProjectsByCustomer(c.Id));
                        if (x != null) list.AddRange(x);
                    }
                    arr = list.ToArray();
                }
                else
                {
                    arr = ServiceGateway.Use(s => s.GetProjectsByStatus(status, LogIn.sign.Id));
                }
                projectsList.ItemsSource = arr;
            }
            catch (Exception ex) { MessageBox.Show("Load failed: " + ex.Message); }
        }

        private void Filter_Changed(object s, SelectionChangedEventArgs e) { if (_ready) Refresh(); }

        private void Project_Selected(object s, SelectionChangedEventArgs e)
        {
            _selected = projectsList.SelectedItem as Project;
            if (_selected == null)
            {
                selTitle.Text = "Select a project on the left.";
                assignBtn.IsEnabled = false;
                statusBtn.IsEnabled = false;
                assigneesList.ItemsSource = null;
                return;
            }
            selTitle.Text = _selected.Title + "  ·  " + _selected.Status;
            assignBtn.IsEnabled = true;
            statusBtn.IsEnabled = true;

            statusCombo.SelectedIndex = (_selected.Status ?? "Active") switch
            {
                "AwaitingPayment" => 1,
                "Done"            => 2,
                "Cancelled"       => 3,
                _                 => 0,
            };

            ReloadAssignees();
        }

        private void ReloadAssignees()
        {
            if (_selected == null) return;
            try
            {
                var assigned = ServiceGateway.Use(c => c.GetProjectAssignees(_selected.Id)) ?? new User[0];
                assigneesList.ItemsSource = assigned;
                // Hide already-assigned from the dropdown.
                var assignedIds = new HashSet<int>(assigned.Select(u => u.Id));
                employeeCombo.ItemsSource = _employees.Where(u => !assignedIds.Contains(u.Id)).ToList();
            }
            catch { }
        }

        private void Add_Click(object s, RoutedEventArgs e)
        {
            if (customerCombo.SelectedValue == null || string.IsNullOrWhiteSpace(titleBox.Text)) return;
            decimal.TryParse(budgetBox.Text, out var budget);
            try
            {
                ServiceGateway.Use(c => c.AddProject(new Project
                {
                    CustomerId  = (int)customerCombo.SelectedValue,
                    Title       = titleBox.Text,
                    Status      = "Active",
                    StartDate   = DateTime.Today,
                    DueDate     = DateTime.Today.AddDays(30),
                    TotalBudget = budget,
                    Currency    = LogIn.sign.PreferredCurrency
                }));
                titleBox.Text = ""; budgetBox.Text = "0";
                Refresh();
            }
            catch (Exception ex) { MessageBox.Show("Add failed: " + ex.Message); }
        }

        private void Assign_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null || employeeCombo.SelectedValue == null) return;
            int empId = (int)employeeCombo.SelectedValue;
            try
            {
                ServiceGateway.Use(c => c.AddProjectAssignment(_selected.Id, empId));
                ShowOk("Employee added.");
                ReloadAssignees();
            }
            catch (Exception ex) { ShowErr("Assign failed: " + ex.Message); }
        }

        private void Unassign_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            if (s is FrameworkElement fe && fe.Tag is int empId)
            {
                try
                {
                    ServiceGateway.Use(c => c.RemoveProjectAssignment(_selected.Id, empId));
                    ShowOk("Employee removed.");
                    ReloadAssignees();
                }
                catch (Exception ex) { ShowErr("Remove failed: " + ex.Message); }
            }
        }

        private void UpdateStatus_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            var newStatus = (statusCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrEmpty(newStatus)) return;
            try
            {
                ServiceGateway.Use(c => c.SetProjectStatus(_selected.Id, newStatus));
                ShowOk("Status set to " + newStatus + ".");
                Refresh();
            }
            catch (Exception ex) { ShowErr("Update failed: " + ex.Message); }
        }

        private void ShowOk(string msg)
        {
            manageStatus.Text = msg;
            manageStatus.Foreground = (Brush)Application.Current.Resources["Mint"];
        }

        private void ShowErr(string msg)
        {
            manageStatus.Text = msg;
            manageStatus.Foreground = (Brush)Application.Current.Resources["Rose"];
        }

        private void Back_Click(object s, RoutedEventArgs e) => NavigationService?.Navigate(new OwnerHome());
    }
}
