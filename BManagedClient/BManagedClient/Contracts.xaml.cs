using BManagedClient.BMsrv;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    public partial class Contracts : Page
    {
        private List<Customer> _customers = new();
        private List<Project>  _projects  = new();

        public Contracts()
        {
            InitializeComponent();
            if (!ClientSession.IsOwner) { NavigationService?.Navigate(new LogIn()); return; }
            LoadDropdowns();
            Refresh();
        }

        private void LoadDropdowns()
        {
            try
            {
                var arr = ServiceGateway.Use(c => c.GetCustomersForOwner(LogIn.sign.Id));
                _customers = (arr ?? new Customer[0]).ToList();
                customerCombo.ItemsSource = _customers;
                if (_customers.Count > 0) customerCombo.SelectedIndex = 0;

                var allProj = new List<Project>();
                foreach (var c in _customers)
                {
                    var p = ServiceGateway.Use(s => s.GetProjectsByCustomer(c.Id));
                    if (p != null) allProj.AddRange(p);
                }
                _projects = allProj;
                projectCombo.ItemsSource = _projects;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        private void Refresh()
        {
            try
            {
                var arr = ServiceGateway.Use(c => c.GetContractsForOwner(LogIn.sign.Id));
                contractsList.ItemsSource = arr;
            }
            catch (Exception ex) { MessageBox.Show("Load failed: " + ex.Message); }
        }

        private Contract Selected => contractsList.SelectedItem as Contract;

        private void Create_Click(object s, RoutedEventArgs e)
        {
            if (customerCombo.SelectedValue == null || string.IsNullOrWhiteSpace(titleBox.Text))
            { MessageBox.Show("Customer + title required."); return; }

            decimal.TryParse(amountBox.Text, out decimal amount);
            string cur = (currencyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ILS";
            int? projId = projectCombo.SelectedValue is int p ? p : (int?)null;

            try
            {
                ServiceGateway.Use(c => c.CreateContract(new Contract
                {
                    CustomerId  = (int)customerCombo.SelectedValue,
                    ProjectId   = projId ?? 0,
                    Title       = titleBox.Text,
                    Body        = bodyBox.Text ?? "",
                    TotalAmount = amount,
                    Currency    = cur,
                    Status      = "Draft",
                    CreatedAt   = DateTime.Now,
                }));
                titleBox.Text = ""; bodyBox.Text = ""; amountBox.Text = "0";
                Refresh();
            }
            catch (Exception ex) { MessageBox.Show("Create failed: " + ex.Message); }
        }

        private void Status_Click(object s, RoutedEventArgs e)
        {
            if (Selected == null) { MessageBox.Show("Pick a contract."); return; }
            string newStatus = (statusCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrEmpty(newStatus)) { MessageBox.Show("Pick a status."); return; }
            try
            {
                if (newStatus == "Signed")
                    ServiceGateway.Use(c => c.MarkContractSigned(Selected.Id, DateTime.Today));
                else
                {
                    var copy = Selected;
                    copy.Status = newStatus;
                    ServiceGateway.Use(c => c.UpdateContract(copy));
                }
                Refresh();
            }
            catch (Exception ex) { MessageBox.Show("Status failed: " + ex.Message); }
        }

        private void Delete_Click(object s, RoutedEventArgs e)
        {
            if (Selected == null) return;
            if (MessageBox.Show("Delete this contract?", "Confirm",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try { ServiceGateway.Use(c => c.DeleteContract(Selected.Id)); Refresh(); }
            catch (Exception ex) { MessageBox.Show("Failed: " + ex.Message); }
        }

        private void Pdf_Click(object s, RoutedEventArgs e)
        {
            if (Selected == null) { MessageBox.Show("Pick a contract."); return; }
            try
            {
                byte[] pdf = ServiceGateway.Use(c => c.GenerateContractPdf(Selected.Id));
                var dlg = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = "Contract-" + Selected.ContractNumber + ".pdf",
                };
                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllBytes(dlg.FileName, pdf);
                    MessageBox.Show("Saved.\n" + dlg.FileName);
                }
            }
            catch (Exception ex) { MessageBox.Show("PDF failed: " + ex.Message); }
        }

        private void Back_Click(object s, RoutedEventArgs e)
            => NavigationService?.Navigate(new OwnerHome());
    }
}
