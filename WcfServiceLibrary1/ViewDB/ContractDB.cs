using Model;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;

namespace ViewDB
{
    // =========================================================================
    // ContractDB — Contracts table data-access.
    // -------------------------------------------------------------------------
    // Lifecycle (status field):
    //   Draft → Sent → Signed → Fulfilled (paid)
    //                    └──────→ Cancelled
    //   * Draft     — Owner is still editing the body.
    //   * Sent      — PDF given to customer for signature.
    //   * Signed    — customer signed; eligible for invoicing.
    //   * Fulfilled — auto-set by Service1.MarkInvoicePaid when an invoice
    //                 with this ContractId is paid (May 2026 reform).
    //   * Cancelled — manual close.
    // Auto-numbering:
    //   NextContractNumber returns 'CTR-YYYY-NNN' counting per-year.
    // Connection back to Invoices:
    //   Invoice.ContractId (nullable FK) — when an Owner clicks 'Invoice
    //   this contract' (web Contracts page or WPF Contracts page),
    //   CreateInvoice persists this id; MarkInvoicePaid then auto-flips
    //   the contract to 'Fulfilled' via SetStatus(c.Id, "Fulfilled", ...).
    // PDF:
    //   GenerateContractPdf (in Service1, via PdfSharp) reads body + total
    //   + customer + signed-date and writes a one-pager. PdfPath is stored
    //   on the row so a regenerate uses the same filename.
    // =========================================================================
    /// <summary>
    /// Service-engagement contract between Owner and Customer for a Project.
    /// Auto-creates the Contracts table on first use so existing .accdb files
    /// don't need a manual migration.
    /// </summary>
    public class ContractDB : BaseDB
    {
        protected override Base NewEntity() => new Contract();

        private static readonly object _schemaLock = new object();
        // volatile: prevents JIT reordering in double-checked locking pattern.
        private static volatile bool _schemaEnsured;

        public ContractDB()
        {
            if (_schemaEnsured) return;
            lock (_schemaLock)
            {
                if (_schemaEnsured) return;
                EnsureSchema();
                _schemaEnsured = true;
            }
        }

        private void EnsureSchema()
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "CREATE TABLE [Contracts] (" +
                "  [id] COUNTER PRIMARY KEY," +
                "  [contractNumber] TEXT(30)," +
                "  [projectId] LONG," +
                "  [customerId] LONG," +
                "  [title] TEXT(200)," +
                "  [body] MEMO," +
                "  [totalAmount] CURRENCY," +
                "  [currency] TEXT(3)," +
                "  [status] TEXT(20)," +
                "  [createdAt] DATETIME," +
                "  [signedDate] DATETIME," +
                "  [pdfPath] TEXT(255))", conn))
            {
                try { conn.Open(); cmd.ExecuteNonQuery(); }
                catch (OleDbException ex)
                {
                    if (!(ex.Message.IndexOf("already exists",
                          StringComparison.OrdinalIgnoreCase) >= 0))
                        System.Diagnostics.Debug.WriteLine("EnsureSchema(Contracts): " + ex.Message);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("EnsureSchema(Contracts): " + ex.Message);
                }
            }
        }

        protected override void CreateModel(Base entity)
        {
            base.CreateModel(entity);
            if (!(entity is Contract c)) return;
            try { c.ContractNumber = reader["contractNumber"].ToString(); }            catch { }
            try { c.ProjectId      = Convert.ToInt32(reader["projectId"]); }            catch { }
            try { c.CustomerId     = Convert.ToInt32(reader["customerId"]); }           catch { }
            try { c.Title          = reader["title"].ToString(); }                      catch { }
            try { c.Body           = reader["body"].ToString(); }                       catch { }
            try { c.TotalAmount    = Convert.ToDecimal(reader["totalAmount"]); }        catch { }
            try { c.Currency       = reader["currency"].ToString(); }                   catch { c.Currency = "ILS"; }
            try { c.Status         = reader["status"].ToString(); }                     catch { c.Status = "Draft"; }
            try { c.CreatedAt      = DateTime.Parse(reader["createdAt"].ToString()); }  catch { }
            try
            {
                var v = reader["signedDate"];
                c.SignedDate = v == DBNull.Value ? (DateTime?)null : DateTime.Parse(v.ToString());
            } catch { }
            try { c.PdfPath        = reader["pdfPath"].ToString(); }                    catch { }
        }

        public int Insert(Contract c)
        {
            if (string.IsNullOrEmpty(c.ContractNumber))
                c.ContractNumber = NextContractNumber();
            string sql = @"INSERT INTO [Contracts]
                ([contractNumber],[projectId],[customerId],[title],[body],
                 [totalAmount],[currency],[status],[createdAt],[signedDate],[pdfPath])
                VALUES (?,?,?,?,?,?,?,?,?,?,?)";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@n", OleDbType.VarWChar, 30) { Value = c.ContractNumber ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@p", OleDbType.Integer)      { Value = c.ProjectId });
                cmd.Parameters.Add(new OleDbParameter("@c", OleDbType.Integer)      { Value = c.CustomerId });
                cmd.Parameters.Add(new OleDbParameter("@t", OleDbType.VarWChar,200) { Value = c.Title ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@b", OleDbType.LongVarWChar) { Value = (object)c.Body ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@a", OleDbType.Currency)     { Value = c.TotalAmount });
                cmd.Parameters.Add(new OleDbParameter("@cur", OleDbType.VarWChar, 3){ Value = c.Currency ?? "ILS" });
                cmd.Parameters.Add(new OleDbParameter("@s", OleDbType.VarWChar, 20) { Value = c.Status ?? "Draft" });
                cmd.Parameters.Add(new OleDbParameter("@ca",OleDbType.Date)         { Value = c.CreatedAt });
                cmd.Parameters.Add(new OleDbParameter("@sd",OleDbType.Date)         { Value = (object)c.SignedDate ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@pp",OleDbType.VarWChar,255) { Value = (object)c.PdfPath ?? DBNull.Value });
                conn.Open();
                cmd.ExecuteNonQuery();
                using (var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn))
                    return Convert.ToInt32(idCmd.ExecuteScalar());
            }
        }

        public string NextContractNumber()
        {
            int year = DateTime.Today.Year;
            int seq = 1;
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "SELECT COUNT(*) FROM [Contracts] WHERE [contractNumber] LIKE ?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@y", OleDbType.VarWChar) { Value = "CTR-" + year + "-%" });
                conn.Open();
                seq = Convert.ToInt32(cmd.ExecuteScalar()) + 1;
            }
            return "CTR-" + year + "-" + seq.ToString("D3");
        }

        public Contract GetById(int id)
            => Select("SELECT * FROM [Contracts] WHERE [id]=?",
                new OleDbParameter("@id", id)).OfType<Contract>().FirstOrDefault();

        public bool BelongsToOwner(int id, int ownerId)
        {
            object r = SelectScalar(
                @"SELECT COUNT(*)
                  FROM [Contracts] AS C
                  INNER JOIN [Customers] AS Cu ON C.[customerId] = Cu.[id]
                  WHERE C.[id] = ? AND Cu.[ownerId] = ?",
                new OleDbParameter("@id", id),
                new OleDbParameter("@o", ownerId));
            return r != null && r != DBNull.Value && Convert.ToInt32(r) > 0;
        }

        public List<Contract> GetByProject(int projectId)
            => Select("SELECT * FROM [Contracts] WHERE [projectId]=? ORDER BY [createdAt] DESC",
                new OleDbParameter("@p", projectId)).OfType<Contract>().ToList();

        public List<Contract> GetByCustomer(int customerId)
            => Select("SELECT * FROM [Contracts] WHERE [customerId]=? ORDER BY [createdAt] DESC",
                new OleDbParameter("@c", customerId)).OfType<Contract>().ToList();

        public List<Contract> GetForOwner(int ownerId)
        {
            string sql = @"SELECT C.*
                           FROM [Contracts] AS C
                           INNER JOIN [Customers] AS Cu ON C.[customerId] = Cu.[id]
                           WHERE Cu.[ownerId] = ?
                           ORDER BY C.[createdAt] DESC";
            return Select(sql, new OleDbParameter("@o", ownerId)).OfType<Contract>().ToList();
        }

        public void SetStatus(int id, string status, DateTime? signedDate = null)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "UPDATE [Contracts] SET [status]=?, [signedDate]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@s",  OleDbType.VarWChar, 20) { Value = status });
                cmd.Parameters.Add(new OleDbParameter("@sd", OleDbType.Date)         { Value = (object)signedDate ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)      { Value = id });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int id)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("DELETE FROM [Contracts] WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = id });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void SetPdfPath(int id, string path)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("UPDATE [Contracts] SET [pdfPath]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@p", OleDbType.VarWChar, 255) { Value = (object)path ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)      { Value = id });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
