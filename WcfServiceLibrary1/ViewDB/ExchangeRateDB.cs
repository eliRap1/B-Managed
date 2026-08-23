using Model;
using System;
using System.Data.OleDb;
using System.Diagnostics;
using System.Linq;

namespace ViewDB
{
    /// <summary>Exchange rates lookup. Latest-by-date wins.</summary>
    public class ExchangeRateDB : BaseDB
    {
        protected override Base NewEntity() => new ExchangeRate();

        protected override void CreateModel(Base entity)
        {
            base.CreateModel(entity);
            if (!(entity is ExchangeRate r)) return;
            try { r.FromCurrency  = reader["fromCurrency"].ToString(); }  catch { }
            try { r.ToCurrency    = reader["toCurrency"].ToString(); }    catch { }
            try { r.Rate          = Convert.ToDouble(reader["rate"]); }   catch { }
            try { r.EffectiveDate = DateTime.Parse(reader["effectiveDate"].ToString()); } catch { }
        }

        /// <summary>
        /// Returns the most recent rate where EffectiveDate &lt;= asOfDate.
        /// If from == to, returns 1.0.
        /// </summary>
        public double GetLatestRate(string from, string to, DateTime asOfDate)
        {
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return 1.0;
            object r = SelectScalar(
                @"SELECT TOP 1 [rate] FROM [ExchangeRates]
                  WHERE [fromCurrency] = ? AND [toCurrency] = ? AND [effectiveDate] <= ?
                  ORDER BY [effectiveDate] DESC",
                new OleDbParameter("@f", from),
                new OleDbParameter("@t", to),
                new OleDbParameter("@d", asOfDate));
            if (r != null && r != DBNull.Value) return Convert.ToDouble(r);
            Trace.TraceWarning($"[ExchangeRateDB] No rate found for {from}->{to} as of {asOfDate:yyyy-MM-dd}; falling back to 1.0");
            return 1.0;
        }

        public int Insert(ExchangeRate r)
        {
            string sql = @"INSERT INTO [ExchangeRates]
                ([fromCurrency],[toCurrency],[rate],[effectiveDate])
                VALUES (?,?,?,?)";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@f", OleDbType.VarWChar, 3) { Value = r.FromCurrency });
                cmd.Parameters.Add(new OleDbParameter("@t", OleDbType.VarWChar, 3) { Value = r.ToCurrency });
                cmd.Parameters.Add(new OleDbParameter("@r", OleDbType.Double)      { Value = r.Rate });
                cmd.Parameters.Add(new OleDbParameter("@d", OleDbType.Date)        { Value = r.EffectiveDate });
                conn.Open();
                return cmd.ExecuteNonQuery();
            }
        }
    }
}
