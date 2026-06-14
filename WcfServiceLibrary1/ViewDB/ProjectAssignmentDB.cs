using Model;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;

namespace ViewDB
{
    /// <summary>
    /// Many-to-many link between Projects and Users (Employees).
    /// Auto-creates the ProjectAssignments table on first use so no manual
    /// schema migration is needed for existing BManaged.accdb files.
    /// </summary>
    public class ProjectAssignmentDB : BaseDB
    {
        protected override Base NewEntity() => null;

        private static readonly object _schemaLock = new object();
        // volatile: prevents JIT reordering in double-checked locking pattern.
        private static volatile bool _schemaEnsured;

        public ProjectAssignmentDB()
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
                "CREATE TABLE [ProjectAssignments] (" +
                "  [id] COUNTER PRIMARY KEY," +
                "  [projectId] LONG," +
                "  [employeeId] LONG," +
                "  [assignedAt] DATETIME)", conn))
            {
                try
                {
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                catch (OleDbException ex)
                {
                    // Access throws -2147217900 / "already exists" when the table is there. Ignore it.
                    if (!(ex.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0))
                        System.Diagnostics.Debug.WriteLine("EnsureSchema(ProjectAssignments): " + ex.Message);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("EnsureSchema(ProjectAssignments): " + ex.Message);
                }
            }
        }

        /// <summary>Idempotent insert: silently no-ops if the row already exists.</summary>
        public void Add(int projectId, int employeeId)
        {
            if (Exists(projectId, employeeId)) return;
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "INSERT INTO [ProjectAssignments] ([projectId],[employeeId],[assignedAt]) VALUES (?,?,?)", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@p", OleDbType.Integer) { Value = projectId });
                cmd.Parameters.Add(new OleDbParameter("@e", OleDbType.Integer) { Value = employeeId });
                cmd.Parameters.Add(new OleDbParameter("@a", OleDbType.Date)    { Value = DateTime.Now });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void Remove(int projectId, int employeeId)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "DELETE FROM [ProjectAssignments] WHERE [projectId]=? AND [employeeId]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@p", OleDbType.Integer) { Value = projectId });
                cmd.Parameters.Add(new OleDbParameter("@e", OleDbType.Integer) { Value = employeeId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public bool Exists(int projectId, int employeeId)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "SELECT COUNT(*) FROM [ProjectAssignments] WHERE [projectId]=? AND [employeeId]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@p", OleDbType.Integer) { Value = projectId });
                cmd.Parameters.Add(new OleDbParameter("@e", OleDbType.Integer) { Value = employeeId });
                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        public List<int> GetAssigneesByProject(int projectId)
        {
            var ids = new List<int>();
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "SELECT [employeeId] FROM [ProjectAssignments] WHERE [projectId]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@p", OleDbType.Integer) { Value = projectId });
                conn.Open();
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read()) ids.Add(Convert.ToInt32(rdr[0]));
            }
            return ids;
        }

        public List<int> GetProjectsByEmployee(int employeeId)
        {
            var ids = new List<int>();
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "SELECT [projectId] FROM [ProjectAssignments] WHERE [employeeId]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@e", OleDbType.Integer) { Value = employeeId });
                conn.Open();
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read()) ids.Add(Convert.ToInt32(rdr[0]));
            }
            return ids;
        }
    }
}
