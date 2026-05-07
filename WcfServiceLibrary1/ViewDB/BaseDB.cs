using Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;

namespace ViewDB
{
    // =========================================================================
    // BaseDB — abstract base for every per-table data-access class.
    // -------------------------------------------------------------------------
    // Why a base class:
    //   * Centralises the OleDb connection-string resolution (Microsoft ACE
    //     provider, reads BManaged.accdb from AppDomain.BaseDirectory).
    //   * Provides Select / SelectScalar templates so every subclass writes
    //     SQL once and gets row-→ object materialisation for free.
    //   * Forces parameterised queries — every subclass uses
    //     OleDbParameter, never string concatenation, so SQL injection is
    //     impossible at the data layer.
    // Inheritance pattern (OOP requirement #6 of the matriculation rubric):
    //   BaseDB
    //    ├─ UserDB         (Users + multi-tenant scoping)
    //    ├─ CustomerDB     (per-Owner CRM rows)
    //    ├─ ProjectDB      (with multi-employee assignment)
    //    ├─ ContractDB     (auto-creates Contracts table on first use)
    //    ├─ InvoiceDB / InvoiceLineDB
    //    ├─ ExpenseDB      (with category JOIN)
    //    ├─ NotificationDB
    //    ├─ ExchangeRateDB (currency conversion table)
    //    ├─ ReportsDB      (read-only aggregations, no mutation)
    //    ├─ LoanDB / LoanPayment helpers
    //    └─ ProjectAssignmentDB (Project↔Employee link table)
    // Each subclass overrides NewEntity() to tell BaseDB which Model class
    // to instantiate when reading rows; CreateModel() then maps DataReader
    // columns to the strongly-typed object.
    // =========================================================================
    public abstract class BaseDB
    {
        private static string connectionString = null;

        // Shared instance fields kept ONLY for backwards-compat with subclasses
        // that read `reader["col"]` inside CreateModel. Each Select() now binds
        // them locally per call so concurrent SOAP calls don't trip over each
        // other (pre-fix the WCF host would crash under load because two
        // threads shared the same OleDbCommand + reader).
        protected OleDbConnection connection;
        protected OleDbCommand command;
        protected OleDbDataReader reader;

        protected abstract Base NewEntity();

        public BaseDB()
        {
            // Don't pre-open anything. Each query method opens its own
            // connection + command + reader and closes them in finally.
        }

        public static OleDbConnection GetConnection()
        {
            if (connectionString == null)
            {
                string ApplicationBaseFolder = AppDomain.CurrentDomain.BaseDirectory;
                connectionString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" +
                    ApplicationBaseFolder + "\\..\\..\\..\\ViewDB\\Database\\BManaged.accdb;Persist Security Info=True";
            }
            return new OleDbConnection(connectionString);
        }

        protected virtual void CreateModel(Base entity)
        {
            if (entity != null)
            {
                try
                {
                    entity.Id = (int)reader["id"];
                }
                catch
                {
                    Console.WriteLine("No ID in DB.");
                }
            }
        }

        /// <summary>
        /// SECURE: Execute SELECT with parameterized query
        /// </summary>
        protected virtual List<Base> Select(string sqlCommandTxt, params OleDbParameter[] parameters)
        {
            List<Base> list = new List<Base>();
            // Per-call connection + command + reader. The instance fields
            // `connection`, `command`, `reader` are also bound here so that
            // CreateModel(entity) inside subclasses (which reads reader[...])
            // keeps working without per-subclass changes.
            using (var conn = GetConnection())
            using (var cmd  = new OleDbCommand(sqlCommandTxt, conn))
            {
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                try
                {
                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        // Wire the legacy fields so CreateModel(reader[...]) works.
                        connection = conn;
                        command    = cmd;
                        reader     = rd;
                        while (rd.Read())
                        {
                            Base entity = NewEntity();
                            CreateModel(entity);
                            list.Add(entity);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Select Error: " + ex.Message);
                    throw new InvalidOperationException(
                        "Select failed: " + sqlCommandTxt + "  ::  " + ex.Message, ex);
                }
                finally
                {
                    reader = null; command = null; connection = null;
                }
            }
            return list;
        }

        /// <summary>
        /// SECURE: Execute SELECT for reviews with parameterized query
        /// </summary>
        protected virtual List<string> SelectReview(string sqlCommandTxt, params OleDbParameter[] parameters)
        {
            List<string> list = new List<string>();
            using (var conn = GetConnection())
            using (var cmd  = new OleDbCommand(sqlCommandTxt, conn))
            {
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                try
                {
                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            list.Add(rd["Rewiew"].ToString());
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("SelectReview Error: " + ex.Message);
                    throw new InvalidOperationException(
                        "SelectReview failed: " + sqlCommandTxt + "  ::  " + ex.Message, ex);
                }
            }
            return list;
        }

        /// <summary>
        /// SECURE: Execute INSERT/UPDATE/DELETE with parameterized query
        /// </summary>
        protected int SaveChanges(string commandText, params OleDbParameter[] parameters)
        {
            int records = 0;
            using (var conn = GetConnection())
            using (var cmd  = new OleDbCommand(commandText, conn))
            {
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                try
                {
                    conn.Open();
                    records = cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"SaveChanges Error: {e.Message}");
                    throw new InvalidOperationException(
                        "SaveChanges failed: " + commandText + "  ::  " + e.Message, e);
                }
            }
            return records;
        }

        /// <summary>
        /// SECURE: Execute SELECT and return single scalar value
        /// </summary>
        protected object SelectScalar(string query, params OleDbParameter[] parameters)
        {
            using (var conn = GetConnection())
            using (var cmd  = new OleDbCommand(query, conn))
            {
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                try
                {
                    conn.Open();
                    return cmd.ExecuteScalar();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("SelectScalar Error: " + ex.Message);
                    throw new InvalidOperationException(
                        "SelectScalar failed: " + query + "  ::  " + ex.Message, ex);
                }
            }
        }
    }
}
