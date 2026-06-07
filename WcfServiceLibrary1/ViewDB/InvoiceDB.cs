using Model;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;

namespace ViewDB
{
    // =========================================================================
    // InvoiceDB — Invoices table data-access.
    // -------------------------------------------------------------------------
    // Schema:
    //   id (COUNTER PK), invoiceNumber (auto-generated INV-YYYY-NNNNN),
    //   projectId? (FK Projects), customerId (FK Customers),
    //   issueDate, dueDate, paidDate?, status ('Draft'/'Sent'/'Paid'/'Overdue'),
    //   subtotal CURRENCY, vatRate DOUBLE (default 0.18),
    //   vatAmount CURRENCY, total CURRENCY, currency TEXT(3),
    //   notes MEMO, contractId? (FK Contracts — drives Fulfilled flow).
    // Lifecycle:
    //   Draft → Sent → Paid (or Overdue). MarkPaid stamps paidDate + flips
    //   status; Service1.MarkInvoicePaid then auto-flips the linked Contract
    //   to 'Fulfilled' (May 2026 reform).
    // Money:
    //   Subtotal/VatAmount/Total are recomputed from line items via
    //   RecalcTotals; called on every AddInvoiceLine to keep totals fresh.
    //   VAT defaults to 18% (post-Jan-2025 rate); Patur signups force 0%.
    // Tenant scoping:
    //   GetByCustomer is per-customer (no JOIN to Customers).
    //   GetForOwner / GetUnpaidForOwner / GetOverdueForOwner all JOIN
    //   Customers ON ownerId so a logged-in Owner only sees their own data.
    // =========================================================================
    /// <summary>Invoices header table. Lines stored in <see cref="InvoiceLineDB"/>.</summary>
    public class InvoiceDB : BaseDB
    {
        protected override Base NewEntity() => new Invoice();

        protected override void CreateModel(Base entity)
        {
            base.CreateModel(entity);
            if (!(entity is Invoice i)) return;
            try { i.InvoiceNumber = reader["invoiceNumber"].ToString(); } catch { }
            try
            {
                var v = reader["projectId"];
                i.ProjectId = v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
            } catch { }
            try { i.CustomerId = Convert.ToInt32(reader["customerId"]); } catch { }
            try { i.IssueDate  = DateTime.Parse(reader["issueDate"].ToString()); } catch { }
            try { i.DueDate    = DateTime.Parse(reader["dueDate"].ToString()); }   catch { }
            try { i.Subtotal   = Convert.ToDecimal(reader["subtotal"]); }  catch { }
            try { i.VatRate    = Convert.ToDouble(reader["vatRate"]); }    catch { i.VatRate = 0.18; }
            try { i.VatAmount  = Convert.ToDecimal(reader["vatAmount"]); } catch { }
            try { i.Total      = Convert.ToDecimal(reader["total"]); }     catch { }
            try { i.Currency   = reader["currency"].ToString(); }          catch { i.Currency = "ILS"; }
            try { i.Status     = reader["status"].ToString(); }            catch { i.Status = "Draft"; }
            try
            {
                var v = reader["paidDate"];
                i.PaidDate = v == DBNull.Value ? (DateTime?)null : DateTime.Parse(v.ToString());
            } catch { }
            try { i.Notes      = reader["notes"].ToString(); }             catch { }
            try
            {
                var v = reader["contractId"];
                i.ContractId = v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
            } catch { }
        }

        public Invoice GetById(int id)
            => Select("SELECT * FROM [Invoices] WHERE [id] = ?",
                new OleDbParameter("@id", id)).OfType<Invoice>().FirstOrDefault();

        public Invoice GetByIdForOwner(int id, int ownerId)
        {
            string sql = @"SELECT I.*
                           FROM [Invoices] AS I
                           INNER JOIN [Customers] AS C ON I.[customerId] = C.[id]
                           WHERE I.[id] = ? AND C.[ownerId] = ?";
            return Select(sql,
                new OleDbParameter("@id", id),
                new OleDbParameter("@o", ownerId)).OfType<Invoice>().FirstOrDefault();
        }

        public bool BelongsToOwner(int id, int ownerId)
        {
            object r = SelectScalar(
                @"SELECT COUNT(*)
                  FROM [Invoices] AS I
                  INNER JOIN [Customers] AS C ON I.[customerId] = C.[id]
                  WHERE I.[id] = ? AND C.[ownerId] = ?",
                new OleDbParameter("@id", id),
                new OleDbParameter("@o", ownerId));
            return r != null && r != DBNull.Value && Convert.ToInt32(r) > 0;
        }

        public List<Invoice> GetByCustomer(int customerId)
            => Select("SELECT * FROM [Invoices] WHERE [customerId] = ? ORDER BY [issueDate] DESC",
                new OleDbParameter("@c", customerId)).OfType<Invoice>().ToList();

        /// <summary>
        /// All invoices for an Owner across every customer — a single JOINed
        /// query, replacing the old WPF pattern of looping customers and
        /// calling GetByCustomer N times. O(1) round-trips instead of O(N).
        /// </summary>
        public List<Invoice> GetForOwner(int ownerId)
        {
            string sql = @"SELECT I.*
                           FROM [Invoices] AS I
                           INNER JOIN [Customers] AS C ON I.[customerId] = C.[id]
                           WHERE C.[ownerId] = ?
                           ORDER BY I.[issueDate] DESC";
            return Select(sql, new OleDbParameter("@o", ownerId)).OfType<Invoice>().ToList();
        }

        /// <summary>Unpaid invoices for an owner — INNER JOIN [Customers] on [ownerId].</summary>
        public List<Invoice> GetUnpaidForOwner(int ownerId)
        {
            string sql = @"SELECT I.*
                           FROM [Invoices] AS I
                           INNER JOIN [Customers] AS C ON I.[customerId] = C.[id]
                           WHERE C.[ownerId] = ?
                             AND (I.[status] = 'Sent' OR I.[status] = 'Draft' OR I.[status] = 'Overdue')
                           ORDER BY I.[dueDate]";
            return Select(sql, new OleDbParameter("@o", ownerId)).OfType<Invoice>().ToList();
        }

        /// <summary>Past-due unpaid invoices for an owner.</summary>
        public List<Invoice> GetOverdueForOwner(int ownerId)
        {
            string sql = @"SELECT I.*
                           FROM [Invoices] AS I
                           INNER JOIN [Customers] AS C ON I.[customerId] = C.[id]
                           WHERE C.[ownerId] = ?
                             AND I.[status] <> 'Paid'
                             AND I.[dueDate] < ?
                           ORDER BY I.[dueDate]";
            return Select(sql,
                new OleDbParameter("@o", ownerId),
                new OleDbParameter("@d", DateTime.Today)).OfType<Invoice>().ToList();
        }

        public string NextInvoiceNumber()
        {
            object r = SelectScalar("SELECT MAX([id]) FROM [Invoices]");
            int next = (r != null && r != DBNull.Value) ? Convert.ToInt32(r) + 1 : 1;
            return $"INV-{DateTime.Today:yyyy}-{next:D5}";
        }

        public int Insert(Invoice i)
        {
            string sql = @"INSERT INTO [Invoices]
                ([invoiceNumber],[projectId],[customerId],[issueDate],[dueDate],
                 [subtotal],[vatRate],[vatAmount],[total],[currency],[status],[paidDate],[notes])
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@n",   OleDbType.VarWChar, 20)  { Value = i.InvoiceNumber ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@p",   OleDbType.Integer)       { Value = (object)i.ProjectId ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@c",   OleDbType.Integer)       { Value = i.CustomerId });
                cmd.Parameters.Add(new OleDbParameter("@id_", OleDbType.Date)          { Value = i.IssueDate });
                cmd.Parameters.Add(new OleDbParameter("@dd",  OleDbType.Date)          { Value = i.DueDate });
                cmd.Parameters.Add(new OleDbParameter("@s",   OleDbType.Currency)      { Value = i.Subtotal });
                cmd.Parameters.Add(new OleDbParameter("@vr",  OleDbType.Double)        { Value = i.VatRate });
                cmd.Parameters.Add(new OleDbParameter("@va",  OleDbType.Currency)      { Value = i.VatAmount });
                cmd.Parameters.Add(new OleDbParameter("@t",   OleDbType.Currency)      { Value = i.Total });
                cmd.Parameters.Add(new OleDbParameter("@cur", OleDbType.VarWChar, 3)   { Value = i.Currency ?? "ILS" });
                cmd.Parameters.Add(new OleDbParameter("@st",  OleDbType.VarWChar, 20)  { Value = i.Status ?? "Draft" });
                cmd.Parameters.Add(new OleDbParameter("@pd",  OleDbType.Date)          { Value = (object)i.PaidDate ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@no",  OleDbType.LongVarWChar)  { Value = (object)i.Notes ?? DBNull.Value });
                conn.Open();
                cmd.ExecuteNonQuery();
                using (var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn))
                    return Convert.ToInt32(idCmd.ExecuteScalar());
            }
        }

        public void UpdateStatus(int invoiceId, string status)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("UPDATE [Invoices] SET [status]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@s",  OleDbType.VarWChar, 20) { Value = status });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)      { Value = invoiceId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void MarkPaid(int invoiceId, DateTime paidDate)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "UPDATE [Invoices] SET [status]='Paid', [paidDate]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@d",  OleDbType.Date)    { Value = paidDate });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = invoiceId });
                conn.Open();
                if (cmd.ExecuteNonQuery() <= 0)
                    throw new InvalidOperationException("MarkPaid: invoice not found id=" + invoiceId);
            }
        }

        /// <summary>
        /// Recompute Subtotal/VatAmount/Total from the line items. Aggregation
        /// done with SUM on InvoiceLines using the invoice's vatRate.
        /// </summary>
        public void RecalcTotals(int invoiceId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                decimal subtotal = 0m;
                using (var cmd = new OleDbCommand(
                    "SELECT SUM([lineTotal]) FROM [InvoiceLines] WHERE [invoiceId]=?", conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = invoiceId });
                    object r = cmd.ExecuteScalar();
                    if (r != null && r != DBNull.Value) subtotal = Convert.ToDecimal(r);
                }

                double rate = 0.18;
                using (var cmd = new OleDbCommand(
                    "SELECT [vatRate] FROM [Invoices] WHERE [id]=?", conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = invoiceId });
                    object r = cmd.ExecuteScalar();
                    if (r != null && r != DBNull.Value) rate = Convert.ToDouble(r);
                }

                decimal vat = Math.Round(subtotal * (decimal)rate, 2);
                decimal total = subtotal + vat;

                using (var cmd = new OleDbCommand(
                    "UPDATE [Invoices] SET [subtotal]=?, [vatAmount]=?, [total]=? WHERE [id]=?", conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@s",  OleDbType.Currency) { Value = subtotal });
                    cmd.Parameters.Add(new OleDbParameter("@v",  OleDbType.Currency) { Value = vat });
                    cmd.Parameters.Add(new OleDbParameter("@t",  OleDbType.Currency) { Value = total });
                    cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)  { Value = invoiceId });
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
