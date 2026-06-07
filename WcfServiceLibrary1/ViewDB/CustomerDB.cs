using Model;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;

namespace ViewDB
{
    /// <summary>Customers / CRM table access.</summary>
    public class CustomerDB : BaseDB
    {
        protected override Base NewEntity() => new Customer();

        protected override void CreateModel(Base entity)
        {
            base.CreateModel(entity);
            if (!(entity is Customer c)) return;
            try { c.BusinessName       = reader["businessName"].ToString(); } catch { }
            try { c.ContactName        = reader["contactName"].ToString(); }  catch { }
            try { c.Email              = reader["email"].ToString(); }        catch { }
            try { c.Phone              = reader["phone"].ToString(); }        catch { }
            try { c.TaxId              = reader["taxId"].ToString(); }        catch { }
            try { c.Address            = reader["address"].ToString(); }      catch { }
            try { c.OwnerId            = Convert.ToInt32(reader["ownerId"]); } catch { }
            try { c.PreferredCurrency  = reader["preferredCurrency"].ToString(); } catch { c.PreferredCurrency = "ILS"; }
            try { c.Notes              = reader["notes"].ToString(); }        catch { }
        }

        public Customer GetById(int id)
            => Select("SELECT * FROM [Customers] WHERE [id] = ?", new OleDbParameter("@id", id))
               .OfType<Customer>().FirstOrDefault();

        public Customer GetByIdForOwner(int id, int ownerId)
            => Select("SELECT * FROM [Customers] WHERE [id] = ? AND [ownerId] = ?",
                new OleDbParameter("@id", id),
                new OleDbParameter("@o", ownerId)).OfType<Customer>().FirstOrDefault();

        public bool BelongsToOwner(int id, int ownerId)
        {
            object r = SelectScalar("SELECT COUNT(*) FROM [Customers] WHERE [id] = ? AND [ownerId] = ?",
                new OleDbParameter("@id", id),
                new OleDbParameter("@o", ownerId));
            return r != null && r != DBNull.Value && Convert.ToInt32(r) > 0;
        }

        public List<Customer> GetByOwner(int ownerId)
            => Select("SELECT * FROM [Customers] WHERE [ownerId] = ? ORDER BY [businessName]",
                new OleDbParameter("@o", ownerId)).OfType<Customer>().ToList();

        public List<Customer> Search(string keyword, int ownerId)
        {
            string like = "%" + (keyword ?? "") + "%";
            return Select(
                @"SELECT * FROM [Customers]
                  WHERE [ownerId] = ?
                    AND ([businessName] LIKE ? OR [contactName] LIKE ? OR [email] LIKE ?)
                  ORDER BY [businessName]",
                new OleDbParameter("@o",  ownerId),
                new OleDbParameter("@k1", like),
                new OleDbParameter("@k2", like),
                new OleDbParameter("@k3", like)).OfType<Customer>().ToList();
        }

        public int Insert(Customer c)
        {
            string sql = @"INSERT INTO [Customers]
                ([businessName],[contactName],[email],[phone],[taxId],[address],[ownerId],[preferredCurrency],[notes])
                VALUES (?,?,?,?,?,?,?,?,?)";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@bn",  OleDbType.VarWChar, 100) { Value = c.BusinessName ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@cn",  OleDbType.VarWChar, 80)  { Value = (object)c.ContactName ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@e",   OleDbType.VarWChar, 100) { Value = (object)c.Email ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@p",   OleDbType.VarWChar, 20)  { Value = (object)c.Phone ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@tid", OleDbType.VarWChar, 20)  { Value = (object)c.TaxId ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@a",   OleDbType.VarWChar, 200) { Value = (object)c.Address ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@o",   OleDbType.Integer)       { Value = c.OwnerId });
                cmd.Parameters.Add(new OleDbParameter("@cur", OleDbType.VarWChar, 3)   { Value = c.PreferredCurrency ?? "ILS" });
                cmd.Parameters.Add(new OleDbParameter("@n",   OleDbType.LongVarWChar)  { Value = (object)c.Notes ?? DBNull.Value });
                conn.Open();
                cmd.ExecuteNonQuery();
                using (var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn))
                    return Convert.ToInt32(idCmd.ExecuteScalar());
            }
        }

        public void Update(Customer c)
        {
            string sql = @"UPDATE [Customers] SET
                [businessName]=?, [contactName]=?, [email]=?, [phone]=?, [taxId]=?,
                [address]=?, [preferredCurrency]=?, [notes]=?
                WHERE [id]=?";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@bn",  OleDbType.VarWChar, 100) { Value = c.BusinessName ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@cn",  OleDbType.VarWChar, 80)  { Value = (object)c.ContactName ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@e",   OleDbType.VarWChar, 100) { Value = (object)c.Email ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@p",   OleDbType.VarWChar, 20)  { Value = (object)c.Phone ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@tid", OleDbType.VarWChar, 20)  { Value = (object)c.TaxId ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@a",   OleDbType.VarWChar, 200) { Value = (object)c.Address ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@cur", OleDbType.VarWChar, 3)   { Value = c.PreferredCurrency ?? "ILS" });
                cmd.Parameters.Add(new OleDbParameter("@n",   OleDbType.LongVarWChar)  { Value = (object)c.Notes ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@id",  OleDbType.Integer)       { Value = c.Id });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int id)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("DELETE FROM [Customers] WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = id });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
