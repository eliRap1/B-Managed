using Model;
using Model.Helpers;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;

namespace ViewDB
{
    /// <summary>
    /// Users table data-access. PBKDF2-hashed passwords; parameterized
    /// queries everywhere; mutating ops throw on OleDb errors.
    /// </summary>
    public class UserDB : BaseDB
    {
        protected override Base NewEntity() => new User();

        public UserDB()
        {
            EnsureSchema();
        }

        /// <summary>
        /// Adds the businessType column if missing. Lets older .accdb files
        /// upgrade automatically without a manual migration.
        /// </summary>
        private void EnsureSchema()
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("ALTER TABLE [Users] ADD COLUMN [businessType] TEXT(30)", conn))
            {
                try { conn.Open(); cmd.ExecuteNonQuery(); }
                catch (OleDbException ex)
                {
                    if (!(ex.Message.IndexOf("already exists",       StringComparison.OrdinalIgnoreCase) >= 0 ||
                          ex.Message.IndexOf("duplicate column",     StringComparison.OrdinalIgnoreCase) >= 0 ||
                          ex.Message.IndexOf("already has a field",  StringComparison.OrdinalIgnoreCase) >= 0))
                        System.Diagnostics.Debug.WriteLine("EnsureSchema(Users.businessType): " + ex.Message);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("EnsureSchema(Users.businessType): " + ex.Message);
                }
            }
        }

        protected override void CreateModel(Base entity)
        {
            base.CreateModel(entity);
            if (!(entity is User u)) return;
            try { u.Username       = reader["username"].ToString(); }       catch { }
            try { u.PasswordHash   = reader["passwordHash"].ToString(); }   catch { }
            try { u.Email          = reader["email"].ToString(); }          catch { }
            try { u.Phone          = reader["phone"].ToString(); }          catch { }
            try { u.Role           = reader["role"].ToString(); }           catch { }
            try { u.IsActive       = bool.Parse(reader["isActive"].ToString()); } catch { }
            try { u.CreatedAt      = DateTime.Parse(reader["createdAt"].ToString()); } catch { }
            try { u.PreferredCurrency = reader["preferredCurrency"].ToString(); } catch { u.PreferredCurrency = "ILS"; }
            try
            {
                var v = reader["businessType"];
                u.BusinessType = v == DBNull.Value ? "Individual" : v.ToString();
            }
            catch { u.BusinessType = "Individual"; }
        }

        public void SetBusinessType(int userId, string businessType)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "UPDATE [Users] SET [businessType]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@b",  OleDbType.VarWChar, 30) { Value = businessType ?? "Individual" });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)      { Value = userId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public bool UserExists(string username)
        {
            if (!SecurityHelper.IsSafeString(username, 50)) return false;
            object r = SelectScalar("SELECT COUNT(*) FROM [Users] WHERE [username] = ?",
                new OleDbParameter("@u", username));
            return r != null && Convert.ToInt32(r) > 0;
        }

        public bool VerifyPassword(string username, string password)
        {
            if (!SecurityHelper.IsSafeString(username, 50) || string.IsNullOrEmpty(password)) return false;
            object r = SelectScalar("SELECT [passwordHash] FROM [Users] WHERE [username] = ? AND [isActive] = ?",
                new OleDbParameter("@u", username),
                new OleDbParameter("@a", true));
            if (r == null || r == DBNull.Value) return false;
            return SecurityHelper.VerifyPassword(password, r.ToString());
        }

        public User GetById(int id)
        {
            return Select("SELECT * FROM [Users] WHERE [id] = ?", new OleDbParameter("@id", id))
                .OfType<User>().FirstOrDefault();
        }

        public int GetIdByUsername(string username)
        {
            if (!SecurityHelper.IsSafeString(username, 50)) return -1;
            object r = SelectScalar("SELECT [id] FROM [Users] WHERE [username] = ?",
                new OleDbParameter("@u", username));
            return (r != null && r != DBNull.Value) ? Convert.ToInt32(r) : -1;
        }

        public int Insert(User u)
        {
            string sql = @"INSERT INTO [Users] ([username],[passwordHash],[email],[phone],[role],[isActive],[createdAt],[preferredCurrency])
                           VALUES (?,?,?,?,?,?,?,?)";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@u",   OleDbType.VarWChar, 50)  { Value = u.Username ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@p",   OleDbType.VarWChar, 255) { Value = u.PasswordHash ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@e",   OleDbType.VarWChar, 100) { Value = (object)u.Email ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@ph",  OleDbType.VarWChar, 20)  { Value = (object)u.Phone ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@r",   OleDbType.VarWChar, 20)  { Value = u.Role ?? "Client" });
                cmd.Parameters.Add(new OleDbParameter("@a",   OleDbType.Boolean) { Value = u.IsActive });
                cmd.Parameters.Add(new OleDbParameter("@c",   OleDbType.Date)    { Value = u.CreatedAt });
                cmd.Parameters.Add(new OleDbParameter("@cur", OleDbType.VarWChar, 3)   { Value = u.PreferredCurrency ?? "ILS" });
                conn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        public void SetPassword(int userId, string hash)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("UPDATE [Users] SET [passwordHash] = ? WHERE [id] = ?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@h", OleDbType.VarWChar, 255) { Value = hash });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)      { Value = userId });
                conn.Open();
                if (cmd.ExecuteNonQuery() <= 0)
                    throw new InvalidOperationException("SetPassword: user not found id=" + userId);
            }
        }

        public void UpdateRole(int userId, string newRole)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("UPDATE [Users] SET [role] = ? WHERE [id] = ?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@r", OleDbType.VarWChar, 20) { Value = newRole });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)     { Value = userId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public bool IsRole(string username, string role)
        {
            object r = SelectScalar("SELECT [role] FROM [Users] WHERE [username] = ?",
                new OleDbParameter("@u", username));
            return r != null && r != DBNull.Value &&
                   string.Equals(r.ToString(), role, StringComparison.OrdinalIgnoreCase);
        }

        public AllUsers GetAll()
        {
            var list = Select("SELECT * FROM [Users] ORDER BY [username]");
            return new AllUsers(list);
        }

        public List<User> GetByRole(string role)
            => Select("SELECT * FROM [Users] WHERE [role] = ? ORDER BY [username]",
                new OleDbParameter("@r", role)).OfType<User>().ToList();

        public List<User> GetInactive()
            => Select("SELECT * FROM [Users] WHERE [isActive] = ? ORDER BY [createdAt] DESC",
                new OleDbParameter("@a", false)).OfType<User>().ToList();

        public void SetActive(int userId, bool isActive)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("UPDATE [Users] SET [isActive] = ? WHERE [id] = ?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@a",  OleDbType.Boolean) { Value = isActive });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = userId });
                conn.Open();
                if (cmd.ExecuteNonQuery() <= 0)
                    throw new InvalidOperationException("SetActive: user not found id=" + userId);
            }
        }

        public void Delete(int userId)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("DELETE FROM [Users] WHERE [id] = ?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = userId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateProfile(int userId, string email, string phone, string preferredCurrency)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "UPDATE [Users] SET [email]=?, [phone]=?, [preferredCurrency]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@e",  OleDbType.VarWChar, 100) { Value = (object)email ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@p",  OleDbType.VarWChar, 20)  { Value = (object)phone ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@c",  OleDbType.VarWChar, 3)   { Value = preferredCurrency ?? "ILS" });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)       { Value = userId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
