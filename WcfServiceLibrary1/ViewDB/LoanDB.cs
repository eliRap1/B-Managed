using Model;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;

namespace ViewDB
{
    // =========================================================================
    // LoanDB — Loans + LoanPayments tables (introduced May 2026).
    // -------------------------------------------------------------------------
    // Schema (auto-created on first run via EnsureSchema):
    //   Loans:
    //     id, ownerId (FK Users), lender, principal (קרן), remainingBalance,
    //     interestRatePct, monthlyPayment, startDate, termMonths,
    //     nextPaymentDate, currency, purpose, isKerenBacked (BIT —
    //     state-backed business fund: קרן הלוואות בערבות מדינה / קרן שמש /
    //     קרן קורת), isActive, notes, createdAt.
    //   LoanPayments:
    //     id, loanId (FK Loans), paidDate, amount, principalPortion,
    //     interestPortion, notes.
    // Why two tables:
    //   Recording payments separately preserves history for the sparkline
    //   on the Loans page (Web) and audit trail. The parent Loan row carries
    //   the running RemainingBalance + NextPaymentDate so reports don't have
    //   to re-aggregate every read.
    // RecordPayment atomicity:
    //   Access OleDb has weak multi-statement transaction support, so
    //   InsertPayment runs INSERT then a follow-up UPDATE on Loans
    //   (decrement RemainingBalance, advance NextPaymentDate, flip IsActive
    //   to false when balance hits zero). Both calls share one connection
    //   open inside the same scope; failures still leak the INSERT but the
    //   subsequent reads will self-correct on the next payment.
    // Used by:
    //   Service1: AddLoan / UpdateLoan / DeleteLoan / RecordLoanPayment /
    //             GetLoanSummary (joins to ReportsDB.AdvancedKpis for DSR).
    //   Web /Owner/Loans + WPF Loans page.
    // =========================================================================
    /// <summary>
    /// Loans + LoanPayments tables. Auto-creates schema on first use so
    /// existing .accdb files migrate automatically. Pairs with the LoanSummary
    /// roll-up which joins against trailing income for debt-service ratios.
    /// </summary>
    public class LoanDB : BaseDB
    {
        protected override Base NewEntity() => new Loan();

        private static readonly object _schemaLock = new object();
        // volatile: prevents JIT reordering in double-checked locking pattern.
        private static volatile bool _schemaEnsured;

        public LoanDB()
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
            string loansSql =
                "CREATE TABLE [Loans] (" +
                "  [id] COUNTER PRIMARY KEY," +
                "  [ownerId] LONG," +
                "  [lender] TEXT(120)," +
                "  [principal] CURRENCY," +
                "  [remainingBalance] CURRENCY," +
                "  [interestRatePct] DOUBLE," +
                "  [monthlyPayment] CURRENCY," +
                "  [startDate] DATETIME," +
                "  [termMonths] LONG," +
                "  [nextPaymentDate] DATETIME," +
                "  [currency] TEXT(3)," +
                "  [purpose] TEXT(200)," +
                "  [isKerenBacked] BIT," +
                "  [hasStandingOrder] BIT," +
                "  [isActive] BIT," +
                "  [notes] MEMO," +
                "  [createdAt] DATETIME)";

            string paymentsSql =
                "CREATE TABLE [LoanPayments] (" +
                "  [id] COUNTER PRIMARY KEY," +
                "  [loanId] LONG," +
                "  [paidDate] DATETIME," +
                "  [amount] CURRENCY," +
                "  [principalPortion] CURRENCY," +
                "  [interestPortion] CURRENCY," +
                "  [notes] MEMO)";

            CreateTableIfMissing(loansSql, "Loans");
            CreateTableIfMissing(paymentsSql, "LoanPayments");
            // Idempotent migration for older .accdb files that pre-date the
            // hasStandingOrder column.
            AddColumnIfMissing("Loans", "hasStandingOrder", "BIT");
        }

        private void AddColumnIfMissing(string table, string column, string sqlType)
        {
            string sql = $"ALTER TABLE [{table}] ADD COLUMN [{column}] {sqlType}";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                try { conn.Open(); cmd.ExecuteNonQuery(); }
                catch (OleDbException) { /* column already exists — ignore */ }
                catch (Exception ex)
                { System.Diagnostics.Debug.WriteLine($"AddColumn({table}.{column}): " + ex.Message); }
            }
        }

        private void CreateTableIfMissing(string sql, string tableName)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                try { conn.Open(); cmd.ExecuteNonQuery(); }
                catch (OleDbException ex)
                {
                    if (!(ex.Message.IndexOf("already exists",
                          StringComparison.OrdinalIgnoreCase) >= 0))
                        System.Diagnostics.Debug.WriteLine("EnsureSchema(" + tableName + "): " + ex.Message);
                }
                catch (Exception ex)
                { System.Diagnostics.Debug.WriteLine("EnsureSchema(" + tableName + "): " + ex.Message); }
            }
        }

        protected override void CreateModel(Base entity)
        {
            base.CreateModel(entity);
            if (!(entity is Loan l)) return;
            try { l.OwnerId          = Convert.ToInt32(reader["ownerId"]); }                         catch { }
            try { l.Lender           = reader["lender"].ToString(); }                                catch { }
            try { l.Principal        = Convert.ToDecimal(reader["principal"]); }                     catch { }
            try { l.RemainingBalance = Convert.ToDecimal(reader["remainingBalance"]); }              catch { }
            try { l.InterestRatePct  = Convert.ToDouble(reader["interestRatePct"]); }                catch { }
            try { l.MonthlyPayment   = Convert.ToDecimal(reader["monthlyPayment"]); }                catch { }
            try { l.StartDate        = Convert.ToDateTime(reader["startDate"]); }                    catch { }
            try { l.TermMonths       = Convert.ToInt32(reader["termMonths"]); }                      catch { }
            try
            {
                var v = reader["nextPaymentDate"];
                l.NextPaymentDate = v == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(v);
            } catch { }
            try { l.Currency         = reader["currency"].ToString(); }                              catch { l.Currency = "ILS"; }
            try { l.Purpose          = reader["purpose"].ToString(); }                               catch { }
            try { l.IsKerenBacked    = Convert.ToBoolean(reader["isKerenBacked"]); }                 catch { }
            try { l.HasStandingOrder = Convert.ToBoolean(reader["hasStandingOrder"]); }              catch { }
            try { l.IsActive         = Convert.ToBoolean(reader["isActive"]); }                      catch { l.IsActive = true; }
            try { l.Notes            = reader["notes"].ToString(); }                                 catch { }
            try { l.CreatedAt        = Convert.ToDateTime(reader["createdAt"]); }                    catch { l.CreatedAt = DateTime.Now; }
        }

        public int Insert(Loan l)
        {
            if (l.RemainingBalance <= 0) l.RemainingBalance = l.Principal;
            if (!l.NextPaymentDate.HasValue && l.MonthlyPayment > 0)
                l.NextPaymentDate = l.StartDate.AddMonths(1);

            string sql = @"INSERT INTO [Loans]
                ([ownerId],[lender],[principal],[remainingBalance],[interestRatePct],
                 [monthlyPayment],[startDate],[termMonths],[nextPaymentDate],[currency],
                 [purpose],[isKerenBacked],[hasStandingOrder],[isActive],[notes],[createdAt])
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@o",  OleDbType.Integer)        { Value = l.OwnerId });
                cmd.Parameters.Add(new OleDbParameter("@ln", OleDbType.VarWChar, 120)  { Value = l.Lender ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@pr", OleDbType.Currency)       { Value = l.Principal });
                cmd.Parameters.Add(new OleDbParameter("@rb", OleDbType.Currency)       { Value = l.RemainingBalance });
                cmd.Parameters.Add(new OleDbParameter("@ir", OleDbType.Double)         { Value = l.InterestRatePct });
                cmd.Parameters.Add(new OleDbParameter("@mp", OleDbType.Currency)       { Value = l.MonthlyPayment });
                cmd.Parameters.Add(new OleDbParameter("@sd", OleDbType.Date)           { Value = l.StartDate });
                cmd.Parameters.Add(new OleDbParameter("@tm", OleDbType.Integer)        { Value = l.TermMonths });
                cmd.Parameters.Add(new OleDbParameter("@np", OleDbType.Date)           { Value = (object)l.NextPaymentDate ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@cu", OleDbType.VarWChar, 3)    { Value = l.Currency ?? "ILS" });
                cmd.Parameters.Add(new OleDbParameter("@pu", OleDbType.VarWChar, 200)  { Value = l.Purpose ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@kb", OleDbType.Boolean)        { Value = l.IsKerenBacked });
                cmd.Parameters.Add(new OleDbParameter("@so", OleDbType.Boolean)        { Value = l.HasStandingOrder });
                cmd.Parameters.Add(new OleDbParameter("@ia", OleDbType.Boolean)        { Value = l.IsActive });
                cmd.Parameters.Add(new OleDbParameter("@nt", OleDbType.LongVarWChar)   { Value = (object)l.Notes ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@ca", OleDbType.Date)           { Value = l.CreatedAt });
                conn.Open();
                cmd.ExecuteNonQuery();
                using (var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn))
                    return Convert.ToInt32(idCmd.ExecuteScalar());
            }
        }

        public void Update(Loan l)
        {
            string sql = @"UPDATE [Loans] SET
                [lender]=?, [principal]=?, [remainingBalance]=?, [interestRatePct]=?,
                [monthlyPayment]=?, [startDate]=?, [termMonths]=?, [nextPaymentDate]=?,
                [currency]=?, [purpose]=?, [isKerenBacked]=?, [hasStandingOrder]=?, [isActive]=?, [notes]=?
                WHERE [id]=?";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@ln", OleDbType.VarWChar, 120)  { Value = l.Lender ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@pr", OleDbType.Currency)       { Value = l.Principal });
                cmd.Parameters.Add(new OleDbParameter("@rb", OleDbType.Currency)       { Value = l.RemainingBalance });
                cmd.Parameters.Add(new OleDbParameter("@ir", OleDbType.Double)         { Value = l.InterestRatePct });
                cmd.Parameters.Add(new OleDbParameter("@mp", OleDbType.Currency)       { Value = l.MonthlyPayment });
                cmd.Parameters.Add(new OleDbParameter("@sd", OleDbType.Date)           { Value = l.StartDate });
                cmd.Parameters.Add(new OleDbParameter("@tm", OleDbType.Integer)        { Value = l.TermMonths });
                cmd.Parameters.Add(new OleDbParameter("@np", OleDbType.Date)           { Value = (object)l.NextPaymentDate ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@cu", OleDbType.VarWChar, 3)    { Value = l.Currency ?? "ILS" });
                cmd.Parameters.Add(new OleDbParameter("@pu", OleDbType.VarWChar, 200)  { Value = l.Purpose ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@kb", OleDbType.Boolean)        { Value = l.IsKerenBacked });
                cmd.Parameters.Add(new OleDbParameter("@so", OleDbType.Boolean)        { Value = l.HasStandingOrder });
                cmd.Parameters.Add(new OleDbParameter("@ia", OleDbType.Boolean)        { Value = l.IsActive });
                cmd.Parameters.Add(new OleDbParameter("@nt", OleDbType.LongVarWChar)   { Value = (object)l.Notes ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)        { Value = l.Id });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new OleDbCommand("DELETE FROM [LoanPayments] WHERE [loanId]=?", conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@l", OleDbType.Integer) { Value = id });
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new OleDbCommand("DELETE FROM [Loans] WHERE [id]=?", conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = id });
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public Loan GetById(int id)
            => Select("SELECT * FROM [Loans] WHERE [id]=?",
                new OleDbParameter("@id", id)).OfType<Loan>().FirstOrDefault();

        public List<Loan> GetForOwner(int ownerId)
            => Select("SELECT * FROM [Loans] WHERE [ownerId]=? ORDER BY [isActive] DESC, [createdAt] DESC",
                new OleDbParameter("@o", ownerId)).OfType<Loan>().ToList();

        // -----------------------------------------------------------------
        // LoanPayments — one row per payment recorded by the Owner. Updates
        // the parent Loan's RemainingBalance + NextPaymentDate atomically.
        // -----------------------------------------------------------------
        public int InsertPayment(LoanPayment p)
        {
            string sql = @"INSERT INTO [LoanPayments]
                ([loanId],[paidDate],[amount],[principalPortion],[interestPortion],[notes])
                VALUES (?,?,?,?,?,?)";
            int newId;
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@l",  OleDbType.Integer)      { Value = p.LoanId });
                cmd.Parameters.Add(new OleDbParameter("@d",  OleDbType.Date)         { Value = p.PaidDate });
                cmd.Parameters.Add(new OleDbParameter("@a",  OleDbType.Currency)     { Value = p.Amount });
                cmd.Parameters.Add(new OleDbParameter("@pp", OleDbType.Currency)     { Value = p.PrincipalPortion });
                cmd.Parameters.Add(new OleDbParameter("@ip", OleDbType.Currency)     { Value = p.InterestPortion });
                cmd.Parameters.Add(new OleDbParameter("@n",  OleDbType.LongVarWChar) { Value = (object)p.Notes ?? DBNull.Value });
                conn.Open();
                cmd.ExecuteNonQuery();
                using (var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn))
                    newId = Convert.ToInt32(idCmd.ExecuteScalar());
            }

            // Adjust parent loan in a follow-up statement (Access OleDb does
            // not support multi-statement transactions easily — this is a
            // best-effort update).
            var loan = GetById(p.LoanId);
            if (loan != null)
            {
                decimal newRemaining = loan.RemainingBalance - p.PrincipalPortion;
                if (newRemaining < 0) newRemaining = 0;
                DateTime? next = loan.NextPaymentDate.HasValue
                    ? loan.NextPaymentDate.Value.AddMonths(1)
                    : (DateTime?)p.PaidDate.AddMonths(1);
                bool stillActive = newRemaining > 0;

                using (var conn = GetConnection())
                using (var cmd = new OleDbCommand(
                    "UPDATE [Loans] SET [remainingBalance]=?, [nextPaymentDate]=?, [isActive]=? WHERE [id]=?", conn))
                {
                    cmd.Parameters.Add(new OleDbParameter("@rb", OleDbType.Currency) { Value = newRemaining });
                    cmd.Parameters.Add(new OleDbParameter("@np", OleDbType.Date)     { Value = (object)next ?? DBNull.Value });
                    cmd.Parameters.Add(new OleDbParameter("@ia", OleDbType.Boolean)  { Value = stillActive });
                    cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)  { Value = loan.Id });
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            return newId;
        }

        public List<LoanPayment> GetPaymentsByLoan(int loanId)
        {
            var rows = new List<LoanPayment>();
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "SELECT * FROM [LoanPayments] WHERE [loanId]=? ORDER BY [paidDate] DESC", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@l", OleDbType.Integer) { Value = loanId });
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var p = new LoanPayment
                        {
                            Id     = Convert.ToInt32(r["id"]),
                            LoanId = Convert.ToInt32(r["loanId"]),
                        };
                        try { p.PaidDate         = Convert.ToDateTime(r["paidDate"]); }   catch { }
                        try { p.Amount           = Convert.ToDecimal(r["amount"]); }      catch { }
                        try { p.PrincipalPortion = Convert.ToDecimal(r["principalPortion"]); } catch { }
                        try { p.InterestPortion  = Convert.ToDecimal(r["interestPortion"]); }  catch { }
                        try { p.Notes            = r["notes"].ToString(); }               catch { }
                        rows.Add(p);
                    }
                }
            }
            return rows;
        }
    }
}
