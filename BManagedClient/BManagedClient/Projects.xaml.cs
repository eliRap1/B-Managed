using BManagedClient.BMsrv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    public partial class Projects : Page
    {
        private List<Customer> _customers = new();

        public Projects()
        {
            InitializeComponent();
            if (!ClientSession.IsOwner) { NavigationService?.Navigate(new LogIn()); return; }
            LoadCustomers();
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

        private void Refresh()
        {
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

        private void Filter_Changed(object s, SelectionChangedEventArgs e) => Refresh();

        private void Add_Click(object s, RoutedEventArgs e)
        {
            if (customerCombo.SelectedValue == null || string.IsNullOrWhiteSpace(titleBox.Text)) return;
            decimal.TryParse(budgetBox.Text, out var budget);
            try
            {
                ServiceGateway.Use(c => c.AddProject(new Project
                {
                    CustomerId = (int)customerCombo.SelectedValue,
                    Title = titleBox.Text,
                    Status = "Active",
                    StartDate = DateTime.Today,
                    DueDate = DateTime.Today.AddDays(30),
                    TotalBudget = budget,
                    Currency = LogIn.sign.PreferredCurrency
                }));
                titleBox.Text = ""; budgetBox.Text = "0";
                Refresh();
            }
            catch (Exception ex) { MessageBox.Show("Add failed: " + ex.Message); }
        }

        private void Back_Click(object s, RoutedEventArgs e) => NavigationService?.Navigate(new OwnerHome());
    }
}
