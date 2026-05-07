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
    // =========================================================================
    // Contracts page (WPF) — Owner-only.
    // -------------------------------------------------------------------------
    // Lifecycle (server-side ContractDB tracks .status):
    //   Draft → Sent → Signed → Fulfilled (paid)
    //                    └──→ Cancelled
    // UI:
    //   Top: Customer + Project + title + amount + currency form, with a
    //        SCOPE OF WORK MEMO box, Create button.
    //   Middle: GridView of every contract for this Owner (via
    //           GetContractsForOwner — tenant-scoped INNER JOIN).
    //   Bottom-right action bar:
    //     * Update status (Draft/Sent/Signed/Fulfilled/Cancelled).
    //     * 🧾 Invoice this contract — only valid in Signed state. Creates
    //       a Draft invoice with the customer + currency from the contract,
    //       seeds an initial line for the contract total, and persists
    //       Invoice.ContractId so MarkInvoicePaid can auto-flip the contract
    //       to Fulfilled later.
    //     * 📄 PDF — generate + save to disk via PdfSharp.
    //     * 🗑 Delete — confirm prompt.
    // Israeli VAT:
    //   InvoiceContract_Click sets VatRate = 0 for Patur Owners, else 0.18.
    // =========================================================================
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

        // Generate a Draft invoice from the selected contract: same customer +
        // currency, with one initial line for the contract total. The contract
        // auto-flips to 'Fulfilled' once that invoice is marked Paid (server-side).
        private void InvoiceContract_Click(object s, RoutedEventArgs e)
        {
            if (Selected == null) { MessageBox.Show("Pick a signed contract first."); return; }
            if (Selected.Status != "Signed")
            { MessageBox.Show("Only Signed contracts can be invoiced."); return; }

            try
            {
                double vatRate = (LogIn.sign != null && LogIn.sign.IsPatur) ? 0.0 : 0.18;
                int newId = ServiceGateway.Use(c => c.CreateInvoice(new Invoice
                {
                    CustomerId = Selected.CustomerId,
                    IssueDate  = DateTime.Today,
                    DueDate    = DateTime.Today.AddDays(30),
                    Currency   = Selected.Currency,
                    Status     = "Draft",
                    VatRate    = vatRate,
                    ContractId = Selected.Id,
                }));
                if (Selected.TotalAmount > 0)
                {
                    ServiceGateway.Use(c => c.AddInvoiceLine(new InvoiceLine
                    {
                        InvoiceId   = newId,
                        Description = Selected.Title ?? ("Contract " + Selected.ContractNumber),
                        Quantity    = 1,
                        UnitPrice   = Selected.TotalAmount,
                        LineTotal   = Selected.TotalAmount,
                        Currency    = Selected.Currency,
                    }));
                }
                MessageBox.Show("Draft invoice created. Open the Invoices page to send / record payment.",
                    "Invoice created", MessageBoxButton.OK, MessageBoxImage.Information);
                NavigationService?.Navigate(new Invoices());
            }
            catch (Exception ex) { MessageBox.Show("Failed: " + ex.Message); }
        }
    }
}
