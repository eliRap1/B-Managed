using BManagedClient.BMsrv;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    // =========================================================================
    // Invoices page (WPF) — Owner-only.
    // -------------------------------------------------------------------------
    // Two-pane layout:
    //   Left  — list of every invoice (across every customer of this Owner)
    //           sorted by IssueDate desc. Click selects → loads detail.
    //   Right — selected invoice: Customer + Currency picker + Create button
    //           (creates a Draft); Add-Line form (Description / Qty / Unit
    //           Price → Line Total auto-computed); Mark Sent / Mark Paid /
    //           Save PDF actions; live totals row.
    // Performance note:
    //   RefreshInvoices used to loop customers and call GetInvoicesByCustomer
    //   per row — N round-trips, one channel-open each. Replaced with one
    //   GetInvoicesForOwner SOAP call (server-side INNER JOIN). For 30
    //   customers that's 30 → 1 round-trips.
    // Israeli VAT default:
    //   Create_Click sets VatRate = 0.0 for Patur Owners (LogIn.sign.IsPatur),
    //   else 0.18. The header VatRate is what RecalcTotals applies on the
    //   server; line items don't carry their own rate.
    // =========================================================================
    public partial class Invoices : Page
    {
        private List<Customer> _customers = new();
        private Invoice _selected;

        public Invoices()
        {
            InitializeComponent();
            if (!ClientSession.IsOwner) { NavigationService?.Navigate(new LogIn()); return; }
            LoadCustomers();
            RefreshInvoices();
        }

        private void LoadCustomers()
        {
            var arr = ServiceGateway.Use(c => c.GetCustomersForOwner(LogIn.sign.Id));
            _customers = (arr ?? new Customer[0]).ToList();
            newCustomer.ItemsSource = _customers;
            if (_customers.Count > 0) newCustomer.SelectedIndex = 0;
        }

        private void RefreshInvoices()
        {
            // Single JOIN query — replaces the old O(N) per-customer loop
            // (RefreshInvoices used to do one SOAP call per customer plus one
            // channel-open each, killing performance for big lists).
            var list = ServiceGateway.Use(s => s.GetInvoicesForOwner(LogIn.sign.Id));
            invoiceList.ItemsSource = (list ?? new Invoice[0])
                .OrderByDescending(i => i.IssueDate).ToList();
        }

        private void Inv_Selected(object s, SelectionChangedEventArgs e)
        {
            if (invoiceList.SelectedItem is Invoice inv)
            {
                _selected = inv;
                LoadLines();
            }
        }

        private void LoadLines()
        {
            if (_selected == null) return;
            var lines = ServiceGateway.Use(s => s.GetInvoiceLinesForOwner(_selected.Id, LogIn.sign.Id)) ?? new InvoiceLine[0];
            lineList.ItemsSource = lines;
            // refresh totals
            _selected = ServiceGateway.Use(s => s.GetInvoiceByIdForOwner(_selected.Id, LogIn.sign.Id));
            totalsText.Text = $"Subtotal {_selected.Subtotal:N2}  ·  VAT {_selected.VatAmount:N2}  ·  Total {_selected.Total:N2} {_selected.Currency}";
        }

        private void Create_Click(object s, RoutedEventArgs e)
        {
            if (newCustomer.SelectedValue == null) return;
            string cur = (newCurrency.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ILS";
            // Israeli VAT default 18 % (since Jan 2025). Osek Patur invoices issue at 0 %.
            double vatRate = (LogIn.sign != null && LogIn.sign.IsPatur) ? 0.0 : 0.18;
            try
            {
                int newId = ServiceGateway.Use(c => c.CreateInvoiceForOwner(new Invoice
                {
                    CustomerId = (int)newCustomer.SelectedValue,
                    IssueDate = DateTime.Today,
                    DueDate = DateTime.Today.AddDays(30),
                    Currency = cur,
                    Status = "Draft",
                    VatRate = vatRate,
                }, LogIn.sign.Id));
                RefreshInvoices();
                _selected = ServiceGateway.Use(c => c.GetInvoiceByIdForOwner(newId, LogIn.sign.Id));
                LoadLines();
            }
            catch (Exception ex) { MessageBox.Show("Create failed: " + ex.Message); }
        }

        private void AddLine_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) { MessageBox.Show("Pick or create an invoice first."); return; }
            double.TryParse(lineQty.Text, out double q);
            decimal.TryParse(lineUnit.Text, out decimal up);
            try
            {
                ServiceGateway.Use(c => c.AddInvoiceLineForOwner(new InvoiceLine
                {
                    InvoiceId = _selected.Id,
                    Description = lineDesc.Text ?? "",
                    Quantity = q == 0 ? 1.0 : q,
                    UnitPrice = up,
                    LineTotal = (decimal)(q == 0 ? 1.0 : q) * up,
                    Currency = _selected.Currency
                }, LogIn.sign.Id));
                lineDesc.Text = ""; lineQty.Text = "1"; lineUnit.Text = "0";
                LoadLines();
            }
            catch (Exception ex) { MessageBox.Show("Add failed: " + ex.Message); }
        }

        private void Sent_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            ServiceGateway.Use(c => c.UpdateInvoiceStatusForOwner(_selected.Id, LogIn.sign.Id, "Sent"));
            LoadLines();
        }

        private void Paid_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            ServiceGateway.Use(c => c.MarkInvoicePaidForOwner(_selected.Id, LogIn.sign.Id, DateTime.Today));
            LoadLines();
            RefreshInvoices();
        }

        private void Pdf_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            try
            {
                var bytes = ServiceGateway.Use(c => c.GenerateInvoicePdfForOwner(_selected.Id, LogIn.sign.Id));
                var path = Path.Combine(Path.GetTempPath(), $"INV-{_selected.Id}.pdf");
                File.WriteAllBytes(path, bytes);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("PDF failed: " + ex.Message); }
        }

        private void Back_Click(object s, RoutedEventArgs e) => NavigationService?.Navigate(new OwnerHome());
    }
}
