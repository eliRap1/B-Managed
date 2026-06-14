using Model;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using ViewDB;

namespace BusinessLogic
{
    /// <summary>
    /// Business logic for invoices and invoice lines: creation (with auto invoice
    /// number), line totals + recalculation, status/paid transitions (with contract
    /// fulfilment sync), queries, and PDF generation. Owner-scoped variants enforce
    /// tenant ownership via Guards.
    /// </summary>
    public class InvoiceLogic
    {
        private readonly InvoiceDB     invDB      = new InvoiceDB();
        private readonly InvoiceLineDB lineDB     = new InvoiceLineDB();
        private readonly CustomerDB    custDB     = new CustomerDB();
        private readonly ContractDB    contractDB = new ContractDB();

        public int  CreateInvoice(Invoice inv)
        {
            if (string.IsNullOrEmpty(inv.InvoiceNumber))
                inv.InvoiceNumber = invDB.NextInvoiceNumber();
            return invDB.Insert(inv);
        }

        public int CreateInvoiceForOwner(Invoice inv, int ownerId)
        {
            if (inv == null) throw new FaultException("Invoice is required.");
            Guards.RequireCustomerOwner(inv.CustomerId, ownerId);
            if (inv.ProjectId.HasValue && inv.ProjectId.Value > 0)
                Guards.RequireProjectOwner(inv.ProjectId.Value, ownerId);
            if (inv.ContractId.HasValue && inv.ContractId.Value > 0 &&
                !contractDB.BelongsToOwner(inv.ContractId.Value, ownerId))
                throw new FaultException("Contract does not belong to this owner.");
            return CreateInvoice(inv);
        }

        public int  AddInvoiceLine(InvoiceLine l)
        {
            l.LineTotal = (decimal)l.Quantity * l.UnitPrice;
            int id = lineDB.Insert(l);
            invDB.RecalcTotals(l.InvoiceId);
            return id;
        }

        public int AddInvoiceLineForOwner(InvoiceLine l, int ownerId)
        {
            if (l == null) throw new FaultException("Invoice line is required.");
            Guards.RequireInvoiceOwner(l.InvoiceId, ownerId);
            return AddInvoiceLine(l);
        }

        public void UpdateInvoiceStatus(int id, string s) => invDB.UpdateStatus(id, s);
        public void UpdateInvoiceStatusForOwner(int id, int ownerId, string s)
        {
            Guards.RequireInvoiceOwner(id, ownerId);
            invDB.UpdateStatus(id, s);
        }

        public void MarkInvoicePaid(int id, DateTime paidDate)
        {
            try
            {
                invDB.MarkPaid(id, paidDate);

                // If the invoice was raised from a contract, mark the contract
                // 'Fulfilled' so it stops appearing as outstanding work. We do
                // not surface contract changes if the contract is already
                // Fulfilled / Cancelled.
                try
                {
                    var inv = invDB.GetById(id);
                    if (inv != null && inv.ContractId.HasValue && inv.ContractId.Value > 0)
                    {
                        var c = contractDB.GetById(inv.ContractId.Value);
                        if (c != null && c.Status != "Fulfilled" && c.Status != "Cancelled")
                        {
                            contractDB.SetStatus(c.Id, "Fulfilled", c.SignedDate);
                        }
                    }
                }
                catch (Exception inner)
                { System.Diagnostics.Debug.WriteLine("MarkInvoicePaid (contract sync): " + inner.Message); }
            }
            catch (Exception ex) { throw new FaultException("MarkInvoicePaid failed: " + ex.Message); }
        }

        public void MarkInvoicePaidForOwner(int id, int ownerId, DateTime paidDate)
        {
            Guards.RequireInvoiceOwner(id, ownerId);
            MarkInvoicePaid(id, paidDate);
        }

        public void RecalcInvoiceTotals(int invoiceId) => invDB.RecalcTotals(invoiceId);
        public Invoice GetInvoiceById(int id)              => invDB.GetById(id);
        public Invoice GetInvoiceByIdForOwner(int id, int ownerId)
            => invDB.GetByIdForOwner(id, ownerId);
        public List<InvoiceLine> GetInvoiceLines(int id)   => lineDB.GetByInvoice(id);
        public List<InvoiceLine> GetInvoiceLinesForOwner(int id, int ownerId)
        {
            Guards.RequireInvoiceOwner(id, ownerId);
            return lineDB.GetByInvoice(id);
        }
        public List<Invoice> GetInvoicesByCustomer(int cid) => invDB.GetByCustomer(cid);
        public List<Invoice> GetUnpaidInvoices(int ownerId) => invDB.GetUnpaidForOwner(ownerId);
        public List<Invoice> GetOverdueInvoices(int ownerId)=> invDB.GetOverdueForOwner(ownerId);
        public List<Invoice> GetInvoicesForOwner(int ownerId) => invDB.GetForOwner(ownerId);

        public byte[] GenerateInvoicePdf(int invoiceId)
        {
            try
            {
                var inv = invDB.GetById(invoiceId);
                if (inv == null)
                    throw new FaultException("Invoice not found: " + invoiceId);
                var lines = lineDB.GetByInvoice(invoiceId);
                var customer = custDB.GetById(inv.CustomerId);
                return new InvoicePdfBuilder().Render(inv, lines, customer);
            }
            catch (FaultException) { throw; }
            catch (Exception ex)
            {
                throw new FaultException("GenerateInvoicePdf failed: " + ex.Message);
            }
        }

        public byte[] GenerateInvoicePdfForOwner(int invoiceId, int ownerId)
        {
            Guards.RequireInvoiceOwner(invoiceId, ownerId);
            return GenerateInvoicePdf(invoiceId);
        }
    }
}
