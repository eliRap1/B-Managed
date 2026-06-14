using Model;
using Model.Helpers;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;

namespace ViewDB
{
    // =========================================================================
    // UserDB — Users table data-access.
    // -------------------------------------------------------------------------
    // Architecture:
    //   Service1 → UserDB → OleDb → [Users] table in BManaged.accdb
    // Schema (auto-migrated on first run via EnsureSchema):
    //   id (COUNTER PK), username, passwordHash, email, phone, role,
    //   isActive, createdAt, preferredCurrency,
    //   businessType ('Patur'|'Murshe'|'Individual'),
    //   isZair (BIT — Israeli small-business income-tax flag),
    //   ownerId (LONG — multi-tenant link to the parent Owner row),
    //   businessName (TEXT 120 — Owner display name),
    //   inviteCode (TEXT 16 — short invite secret for Employee signup).
    // Security:
    //   * Passwords stored as PBKDF2 hashes (SecurityHelper.HashPassword).
    //     VerifyPassword uses SlowEquals to avoid timing attacks.
    //   * Every parameter goes through OleDbParameter — no SQL concat.
    //   * SecurityHelper.IsSafeString gates inputs that touch LIKE / username.
    //   * Tenant-scoped reads (*ForOwner) JOIN/filter on [ownerId] so an
    //     Owner of company A can't see users from company B.
    // =========================================================================
    /// <summary>
    /// Users table data-access. PBKDF2-hashed passwords; parameterised
    /// queries everywhere; mutating ops throw on OleDb errors.
    /// </summary>
    public class UserDB : BaseDB
    {
        protected override Base NewEntity() => new User();

        // Run EnsureSchema once per process, not on every WCF request — otherwise
        // every Service1 instantiation pays for an ALTER TABLE round-trip and
        // a fresh OleDb connection-pool entry, which exhausts under load.
        private static readonly object _schemaLock = new object();
        // volatile prevents the JIT from reordering the write to _schemaEnsured
        // ahead of the EnsureSchema() body in the double-checked locking pattern.
        private static volatile bool _schemaEnsured;

        public UserDB()
        {
            if (_schemaEnsured) return;
            lock (_schemaLock)
            {
                if (_schemaEnsured) return;
                EnsureSchema();
                _schemaEnsured = true;
            }
        }

        /// <summary>
        /// Adds optional Osek-related columns if missing. Lets older .accdb
        /// files upgrade automatically without a manual migration.
        /// </summary>
        private void EnsureSchema()
        {
            AddColumnIfMissing("[businessType] TEXT(30)");
            AddColumnIfMissing("[isZair] BIT");
            AddColumnIfMissing("[ownerId] LONG");
            AddColumnIfMissing("[businessName] TEXT(120)");
            AddColumnIfMissing("[inviteCode] TEXT(16)");
            MigrateLegacyZair();
        }

        // 2026 reform: Osek Zair is no longer a BusinessType — it is a flag on top
        // of Patur or Murshe. Old rows with businessType='Zair' are migrated to
        // ('Patur', isZair=true) since legacy Zair behaved like Patur for VAT.
        private void MigrateLegacyZair()
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "UPDATE [Users] SET [businessType]='Patur', [isZair]=? WHERE [businessType]='Zair'", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@z", OleDbType.Boolean) { Value = true });
                try { conn.Open(); cmd.ExecuteNonQuery(); }
                catch (Exception ex)
                { System.Diagnostics.Debug.WriteLine("MigrateLegacyZair: " + ex.Message); }
            }
        }

        private void AddColumnIfMissing(string columnDef)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("ALTER TABLE [Users] ADD COLUMN " + columnDef, conn))
            {
                try { conn.Open(); cmd.ExecuteNonQuery(); }
                catch (OleDbException ex)
                {
                    if (!(ex.Message.IndexOf("already exists",       StringComparison.OrdinalIgnoreCase) >= 0 ||
                          ex.Message.IndexOf("duplicate column",     StringComparison.OrdinalIgnoreCase) >= 0 ||
                          ex.Message.IndexOf("already has a field",  StringComparison.OrdinalIgnoreCase) >= 0))
                        System.Diagnostics.Debug.WriteLine("EnsureSchema(Users): " + ex.Message);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("EnsureSchema(Users): " + ex.Message);
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
            try
            {
                var v = reader["isZair"];
                u.IsZair = v != DBNull.Value && Convert.ToBoolean(v);
            }
            catch { u.IsZair = false; }
            try
            {
                var v = reader["ownerId"];
                u.OwnerId = v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
            }
            catch { u.OwnerId = null; }
            try
            {
                var v = reader["businessName"];
                u.BusinessName = v == DBNull.Value ? null : v.ToString();
            }
            catch { u.BusinessName = null; }
            try
            {
                var v = reader["inviteCode"];
                u.InviteCode = v == DBNull.Value ? null : v.ToString();
            }
            catch { u.InviteCode = null; }
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

        public void SetIsZair(int userId, bool isZair)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "UPDATE [Users] SET [isZair]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@z",  OleDbType.Boolean) { Value = isZair });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = userId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void SetOwnerId(int userId, int? ownerId)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "UPDATE [Users] SET [ownerId]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@o",  OleDbType.Integer) { Value = (object)ownerId ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = userId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // Owners that an employee/client can pick at signup. Filters to
        // active Owner accounts only. Used internally and as a fallback —
        // primary employee onboarding path is by invite code.
        public List<User> GetActiveOwners()
            => Select("SELECT * FROM [Users] WHERE [role]='Owner' AND [isActive]=? ORDER BY [username]",
                new OleDbParameter("@a", true)).OfType<User>().ToList();

        public void SetBusinessName(int userId, string businessName)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "UPDATE [Users] SET [businessName]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@b",  OleDbType.VarWChar, 120) { Value = (object)businessName ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)       { Value = userId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public string SetInviteCode(int userId, string inviteCode)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "UPDATE [Users] SET [inviteCode]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@c",  OleDbType.VarWChar, 16) { Value = (object)inviteCode ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)      { Value = userId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
            return inviteCode;
        }

        // Resolve invite code -> Owner. Used by Employee signup so the user
        // doesn't see (or pick from) a list of every company on the platform.
        public User GetOwnerByInviteCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length > 16) return null;
            return Select("SELECT * FROM [Users] WHERE [role]='Owner' AND [inviteCode]=? AND [isActive]=?",
                new OleDbParameter("@c", code.Trim()),
                new OleDbParameter("@a", true)).OfType<User>().FirstOrDefault();
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

        // ---- Tenant-scoped variants (May 2026) ------------------------
        // These restrict results to the caller's company (ownerId chain) so an
        // Owner of company A can never see / approve / assign users from
        // company B. The legacy un-scoped versions are kept for the seed
        // admin / migration paths but should not be called from per-tenant UI.

        public List<User> GetUsersForOwner(int ownerId)
            => Select(@"SELECT * FROM [Users]
                        WHERE [id] = ? OR [ownerId] = ?
                        ORDER BY [role], [username]",
                new OleDbParameter("@id", ownerId),
                new OleDbParameter("@o",  ownerId)).OfType<User>().ToList();

        public List<User> GetPendingForOwner(int ownerId)
            => Select(@"SELECT * FROM [Users]
                        WHERE [isActive] = ? AND [ownerId] = ?
                        ORDER BY [createdAt] DESC",
                new OleDbParameter("@a", false),
                new OleDbParameter("@o", ownerId)).OfType<User>().ToList();

        public List<User> GetEmployeesForOwner(int ownerId)
            => Select(@"SELECT * FROM [Users]
                        WHERE [role] = ? AND [ownerId] = ?
                        ORDER BY [username]",
                new OleDbParameter("@r", "Employee"),
                new OleDbParameter("@o", ownerId)).OfType<User>().ToList();

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
