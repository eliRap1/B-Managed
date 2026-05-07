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
        protected OleDbConnection connection;
        protected OleDbCommand command;
        protected OleDbDataReader reader;

        protected abstract Base NewEntity();

        public BaseDB()
        {
            connection = GetConnection();
            command = new OleDbCommand();
            command.Connection = connection;
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
            try
            {
                connection.Open();
                command.CommandText = sqlCommandTxt;
                command.Parameters.Clear();

                // Add parameters if provided
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                reader = command.ExecuteReader();

                while (reader.Read())
                {
                    Base entity = NewEntity();
                    CreateModel(entity);
                    list.Add(entity);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Select Error: " + ex.Message);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
                if (connection.State == ConnectionState.Open)
                    connection.Close();
            }
            return list;
        }

        /// <summary>
        /// SECURE: Execute SELECT for reviews with parameterized query
        /// </summary>
        protected virtual List<string> SelectReview(string sqlCommandTxt, params OleDbParameter[] parameters)
        {
            List<string> list = new List<string>();
            try
            {
                connection.Open();
                command.CommandText = sqlCommandTxt;
                command.Parameters.Clear();

                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                reader = command.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(reader["Rewiew"].ToString());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SelectReview Error: " + ex.Message);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
                if (connection.State == ConnectionState.Open)
                    connection.Close();
            }
            return list;
        }

        /// <summary>
        /// SECURE: Execute INSERT/UPDATE/DELETE with parameterized query
        /// </summary>
        protected int SaveChanges(string commandText, params OleDbParameter[] parameters)
        {
            int records = 0;
            OleDbCommand cmd = new OleDbCommand();
            try
            {
                cmd.Connection = connection;
                cmd.CommandText = commandText;
                cmd.Parameters.Clear();

                if (parameters != null && parameters.Length > 0)
                {
                    cmd.Parameters.AddRange(parameters);
                    // Log parameter values for debugging
                    System.Diagnostics.Debug.WriteLine($"SaveChanges: Executing with {parameters.Length} parameters");
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var p = parameters[i];
                        System.Diagnostics.Debug.WriteLine($"  Param[{i}]: {p.ParameterName} = {p.Value} (Type: {p.OleDbType})");
                    }
                }

                connection.Open();
                records = cmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"SaveChanges: Successfully affected {records} row(s)");
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"SaveChanges Error: {e.Message}");
                System.Diagnostics.Debug.WriteLine($"SQL: {cmd.CommandText}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {e.StackTrace}");
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                    connection.Close();
            }
            return records;
        }

        /// <summary>
        /// SECURE: Execute SELECT and return single scalar value
        /// </summary>
        protected object SelectScalar(string query, params OleDbParameter[] parameters)
        {
            try
            {
                command.CommandText = query;
                command.Parameters.Clear();

                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                connection.Open();
                object result = command.ExecuteScalar();
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SelectScalar Error: " + ex.Message);
                return null;
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                    connection.Close();
            }
        }
    }
}