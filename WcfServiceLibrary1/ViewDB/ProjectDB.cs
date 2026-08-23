using Model;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;

namespace ViewDB
{
    /// <summary>Projects table access. Status flow Active → AwaitingPayment → Done.</summary>
    public class ProjectDB : BaseDB
    {
        protected override Base NewEntity() => new Project();

        protected override void CreateModel(Base entity)
        {
            base.CreateModel(entity);
            if (!(entity is Project p)) return;
            try { p.CustomerId          = Convert.ToInt32(reader["customerId"]); } catch { }
            try { p.Title               = reader["title"].ToString(); }              catch { }
            try { p.Description         = reader["description"].ToString(); }        catch { }
            try { p.Status              = reader["status"].ToString(); }             catch { p.Status = "Active"; }
            try { p.StartDate           = DateTime.Parse(reader["startDate"].ToString()); } catch { p.StartDate = null; }
            try { p.DueDate             = DateTime.Parse(reader["dueDate"].ToString()); }   catch { p.DueDate = null; }
            try
            {
                var v = reader["assignedEmployeeId"];
                p.AssignedEmployeeId = v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
            } catch { }
            try { p.TotalBudget         = Convert.ToDecimal(reader["totalBudget"]); } catch { }
            try { p.Currency            = reader["currency"].ToString(); }            catch { p.Currency = "ILS"; }
        }

        public Project GetById(int id)
            => Select("SELECT * FROM [Projects] WHERE [id] = ?",
                new OleDbParameter("@id", id)).OfType<Project>().FirstOrDefault();

        public Project GetByIdForOwner(int id, int ownerId)
        {
            string sql = @"SELECT P.*
                           FROM [Projects] AS P
                           INNER JOIN [Customers] AS C ON P.[customerId] = C.[id]
                           WHERE P.[id] = ? AND C.[ownerId] = ?";
            return Select(sql,
                new OleDbParameter("@id", id),
                new OleDbParameter("@o", ownerId)).OfType<Project>().FirstOrDefault();
        }

        public bool BelongsToOwner(int id, int ownerId)
        {
            object r = SelectScalar(
                @"SELECT COUNT(*)
                  FROM [Projects] AS P
                  INNER JOIN [Customers] AS C ON P.[customerId] = C.[id]
                  WHERE P.[id] = ? AND C.[ownerId] = ?",
                new OleDbParameter("@id", id),
                new OleDbParameter("@o", ownerId));
            return r != null && r != DBNull.Value && Convert.ToInt32(r) > 0;
        }

        public List<Project> GetByCustomer(int customerId)
            => Select("SELECT * FROM [Projects] WHERE [customerId] = ? ORDER BY [startDate] DESC",
                new OleDbParameter("@c", customerId)).OfType<Project>().ToList();

        public List<Project> GetByEmployee(int employeeId)
            => Select("SELECT * FROM [Projects] WHERE [assignedEmployeeId] = ? ORDER BY [dueDate]",
                new OleDbParameter("@e", employeeId)).OfType<Project>().ToList();

        /// <summary>
        /// Filter projects by status, scoped to a single owner.
        /// Uses INNER JOIN [Customers] to keep only projects whose customer
        /// belongs to <paramref name="ownerId"/>.
        /// </summary>
        public List<Project> GetByStatus(string status, int ownerId)
        {
            string sql = @"SELECT P.*
                           FROM [Projects] AS P
                           INNER JOIN [Customers] AS C ON P.[customerId] = C.[id]
                           WHERE P.[status] = ? AND C.[ownerId] = ?
                           ORDER BY P.[dueDate]";
            return Select(sql,
                new OleDbParameter("@s", status),
                new OleDbParameter("@o", ownerId)).OfType<Project>().ToList();
        }

        public int Insert(Project p)
        {
            string sql = @"INSERT INTO [Projects]
                ([customerId],[title],[description],[status],[startDate],[dueDate],
                 [assignedEmployeeId],[totalBudget],[currency])
                VALUES (?,?,?,?,?,?,?,?,?)";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@c",   OleDbType.Integer)       { Value = p.CustomerId });
                cmd.Parameters.Add(new OleDbParameter("@t",   OleDbType.VarWChar, 120) { Value = p.Title ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@d",   OleDbType.LongVarWChar)  { Value = (object)p.Description ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@s",   OleDbType.VarWChar, 20)  { Value = p.Status ?? "Active" });
                cmd.Parameters.Add(new OleDbParameter("@sd",  OleDbType.Date)          { Value = (object)p.StartDate ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@dd",  OleDbType.Date)          { Value = (object)p.DueDate   ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@ae",  OleDbType.Integer)       { Value = (object)p.AssignedEmployeeId ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@b",   OleDbType.Currency)      { Value = p.TotalBudget });
                cmd.Parameters.Add(new OleDbParameter("@cur", OleDbType.VarWChar, 3)   { Value = p.Currency ?? "ILS" });
                conn.Open();
                cmd.ExecuteNonQuery();
                using (var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn))
                    return Convert.ToInt32(idCmd.ExecuteScalar());
            }
        }

        public void Update(Project p)
        {
            string sql = @"UPDATE [Projects] SET
                [title]=?, [description]=?, [status]=?, [startDate]=?, [dueDate]=?,
                [assignedEmployeeId]=?, [totalBudget]=?, [currency]=?
                WHERE [id]=?";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@t",   OleDbType.VarWChar, 120) { Value = p.Title ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@d",   OleDbType.LongVarWChar)  { Value = (object)p.Description ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@s",   OleDbType.VarWChar, 20)  { Value = p.Status ?? "Active" });
                cmd.Parameters.Add(new OleDbParameter("@sd",  OleDbType.Date)          { Value = (object)p.StartDate ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@dd",  OleDbType.Date)          { Value = (object)p.DueDate   ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@ae",  OleDbType.Integer)       { Value = (object)p.AssignedEmployeeId ?? DBNull.Value });
                cmd.Parameters.Add(new OleDbParameter("@b",   OleDbType.Currency)      { Value = p.TotalBudget });
                cmd.Parameters.Add(new OleDbParameter("@cur", OleDbType.VarWChar, 3)   { Value = p.Currency ?? "ILS" });
                cmd.Parameters.Add(new OleDbParameter("@id",  OleDbType.Integer)       { Value = p.Id });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void SetStatus(int projectId, string status)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("UPDATE [Projects] SET [status]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@s",  OleDbType.VarWChar, 20) { Value = status });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)      { Value = projectId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void AssignEmployee(int projectId, int employeeId)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("UPDATE [Projects] SET [assignedEmployeeId]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@e",  OleDbType.Integer) { Value = employeeId });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = projectId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
