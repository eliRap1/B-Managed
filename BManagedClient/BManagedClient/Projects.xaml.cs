using BManagedClient.BMsrv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BManagedClient
{
    /// <summary>Wrapper that adds a comma-joined assignee column to the row.</summary>
    public class ProjectRow
    {
        public Project Project { get; set; }
        public int      Id            => Project.Id;
        public string   Title         => Project.Title;
        public string   Status        => Project.Status;
        public DateTime? DueDate      => Project.DueDate;
        public decimal  TotalBudget   => Project.TotalBudget;
        public string   Currency      => Project.Currency;
        public string   AssigneesText { get; set; } = "—";
    }

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
                // Tenant-scoped: only employees that belong to this Owner.
                // Replaces GetAllUsers() + client-side filter, which leaked
                // employees from other companies.
                var emps = ServiceGateway.Use(c => c.GetEmployeesForOwner(LogIn.sign.Id));
                _employees = emps == null
                    ? new List<User>()
                    : emps.Where(u => u.IsActive).ToList();
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

                // Wrap each project with its assignees so the grid can show
                // multiple employees per row (joined by comma).
                var rows = new List<ProjectRow>();
                foreach (var p in arr)
                {
                    var row = new ProjectRow { Project = p };
                    try
                    {
                        var assignees = ServiceGateway.Use(c => c.GetProjectAssignees(p.Id));
                        if (assignees != null && assignees.Length > 0)
                        {
                            row.AssigneesText = string.Join(", ",
                                assignees.Select(u => u.Username));
                        }
                        else if (p.AssignedEmployeeId.HasValue)
                        {
                            // Legacy single-assign fallback
                            var u = _employees.FirstOrDefault(x => x.Id == p.AssignedEmployeeId.Value);
                            row.AssigneesText = u?.Username ?? ("#" + p.AssignedEmployeeId.Value);
                        }
                    }
                    catch { }
                    rows.Add(row);
                }
                projectsList.ItemsSource = rows;
            }
            catch (Exception ex) { MessageBox.Show("Load failed: " + ex.Message); }
        }

        private void Filter_Changed(object s, SelectionChangedEventArgs e) { if (_ready) Refresh(); }

        private void Project_Selected(object s, SelectionChangedEventArgs e)
        {
            // Items in the list are ProjectRow wrappers; unwrap to the inner Project.
            _selected = (projectsList.SelectedItem as ProjectRow)?.Project;
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
                ServiceGateway.Use(c => c.AddProjectForOwner(new Project
                {
                    CustomerId  = (int)customerCombo.SelectedValue,
                    Title       = titleBox.Text,
                    Status      = "Active",
                    StartDate   = DateTime.Today,
                    DueDate     = DateTime.Today.AddDays(30),
                    TotalBudget = budget,
                    Currency    = LogIn.sign.PreferredCurrency
                }, LogIn.sign.Id));
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
                ServiceGateway.Use(c => c.AddProjectAssignmentForOwner(_selected.Id, LogIn.sign.Id, empId));
                ShowOk("Employee added.");
                ReloadAssignees();
                Refresh();   // refresh main table so the Employees column updates
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
                    ServiceGateway.Use(c => c.RemoveProjectAssignmentForOwner(_selected.Id, LogIn.sign.Id, empId));
                    ShowOk("Employee removed.");
                    ReloadAssignees();
                    Refresh();
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
                ServiceGateway.Use(c => c.SetProjectStatusForOwner(_selected.Id, LogIn.sign.Id, newStatus));
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
