using Model;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;

namespace ViewDB
{
    /// <summary>Expenses table. Categories live in <see cref="ExpenseCategories"/>.</summary>
    public class ExpenseDB : BaseDB
    {
        protected override Base NewEntity() => new Expense();

        protected override void CreateModel(Base entity)
        {
            base.CreateModel(entity);
            if (!(entity is Expense e)) return;
            try { e.OwnerId = Convert.ToInt32(reader["ownerId"]); } catch { }
            try
            {
                var v = reader["categoryId"];
                e.CategoryId = v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
            } catch { }
            try { e.Date        = DateTime.Parse(reader["date"].ToString()); } catch { }
            try { e.Amount      = Convert.ToDecimal(reader["amount"]); }      catch { }
            try { e.VatPaid     = Convert.ToDecimal(reader["vatPaid"]); }     catch { }
            try { e.Vendor      = reader["vendor"].ToString(); }              catch { }
            try { e.Description = reader["description"].ToString(); }         catch { }
            try
            {
                var v = reader["projectId"];
                e.ProjectId = v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
            } catch { }
            try { e.ReceiptPath = reader["receiptPath"].ToString(); }         catch { }
            try { e.Currency    = reader["currency"].ToString(); }            catch { e.Currency = "ILS"; }
        }

        public List<Expense> GetByOwner(int ownerId)
            => Select("SELECT * FROM [Expenses] WHERE [ownerId] = ? ORDER BY [date] DESC",
                new OleDbParameter("@o", ownerId)).OfType<Expense>().ToList();

        public List<Expense> GetByCategory(int ownerId, int categoryId)
            => Select("SELECT * FROM [Expenses] WHERE [ownerId] = ? AND [categoryId] = ? ORDER BY [date] DESC",
                new OleDbParameter("@o", ownerId),
                new OleDbParameter("@c", categoryId)).OfType<Expense>().ToList();

        public List<Expense> GetByPeriod(int ownerId, DateTime from, DateTime to)
            => Select(
                "SELECT * FROM [Expenses] WHERE [ownerId] = ? AND [date] >= ? AND [date] <= ? ORDER BY [date] DESC",
                new OleDbParameter("@o", ownerId),
                new OleDbParameter("@f", from),
                new OleDbParameter("@t", to)).OfType<Expense>().ToList();

        public int Insert(Expense e)
        {
            string sql = @"INSERT INTO [Expenses]
                ([ownerId],[categoryId],[date],[amount],[vatPaid],[vendor],[description],[projectId],[receiptPath],[currency])
                VALUES (?,?,?,?,?,?,?,?,?,?)";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@o",   OleDbType.Integer)       { Value = e.OwnerId });
                cmd.Parameters.Add(new OleDbParameter("@c",   OleDbType.Integer)       { Value = (object)e.CategoryId ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@d",   OleDbType.Date)          { Value = e.Date });
                cmd.Parameters.Add(new OleDbParameter("@a",   OleDbType.Currency)      { Value = e.Amount });
                cmd.Parameters.Add(new OleDbParameter("@v",   OleDbType.Currency)      { Value = e.VatPaid });
                cmd.Parameters.Add(new OleDbParameter("@vd",  OleDbType.VarWChar, 100) { Value = (object)e.Vendor ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@desc",OleDbType.LongVarWChar)  { Value = (object)e.Description ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@p",   OleDbType.Integer)       { Value = (object)e.ProjectId ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@rp",  OleDbType.VarWChar, 255) { Value = (object)e.ReceiptPath ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@cur", OleDbType.VarWChar, 3)   { Value = e.Currency ?? "ILS" });
                conn.Open();
                cmd.ExecuteNonQuery();
                using (var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn))
                    return Convert.ToInt32(idCmd.ExecuteScalar());
            }
        }

        public void Update(Expense e)
        {
            string sql = @"UPDATE [Expenses] SET
                [categoryId]=?, [date]=?, [amount]=?, [vatPaid]=?, [vendor]=?,
                [description]=?, [projectId]=?, [receiptPath]=?, [currency]=?
                WHERE [id]=?";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@c",   OleDbType.Integer)       { Value = (object)e.CategoryId ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@d",   OleDbType.Date)          { Value = e.Date });
                cmd.Parameters.Add(new OleDbParameter("@a",   OleDbType.Currency)      { Value = e.Amount });
                cmd.Parameters.Add(new OleDbParameter("@v",   OleDbType.Currency)      { Value = e.VatPaid });
                cmd.Parameters.Add(new OleDbParameter("@vd",  OleDbType.VarWChar, 100) { Value = (object)e.Vendor ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@desc",OleDbType.LongVarWChar)  { Value = (object)e.Description ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@p",   OleDbType.Integer)       { Value = (object)e.ProjectId ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@rp",  OleDbType.VarWChar, 255) { Value = (object)e.ReceiptPath ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@cur", OleDbType.VarWChar, 3)   { Value = e.Currency ?? "ILS" });
                cmd.Parameters.Add(new OleDbParameter("@id",  OleDbType.Integer)       { Value = e.Id });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void SetReceiptPath(int expenseId, string relPath)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("UPDATE [Expenses] SET [receiptPath]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@rp", OleDbType.VarWChar, 255) { Value = (object)relPath ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)       { Value = expenseId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int id)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("DELETE FROM [Expenses] WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = id });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public List<ExpenseCategory> GetCategories()
        {
            var list = new List<ExpenseCategory>();
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("SELECT * FROM [ExpenseCategories] ORDER BY [name]", conn))
            {
                conn.Open();
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        list.Add(new ExpenseCategory
                        {
                            Id   = Convert.ToInt32(rdr["id"]),
                            Name = rdr["name"].ToString(),
                            IsVatDeductible = Convert.ToBoolean(rdr["isVatDeductible"])
                        });
                    }
                }
            }
            return list;
        }
    }
}
