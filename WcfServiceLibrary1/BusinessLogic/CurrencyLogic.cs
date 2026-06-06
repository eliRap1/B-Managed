using Model;
using System;
using ViewDB;

namespace BusinessLogic
{
    /// <summary>
    /// Business logic for currency: exchange-rate lookup/update and the supported
    /// currency list.
    /// </summary>
    public class CurrencyLogic
    {
        private readonly ExchangeRateDB fxDB = new ExchangeRateDB();

        public double GetExchangeRate(string from, string to, DateTime asOfDate)
            => fxDB.GetLatestRate(from, to, asOfDate);

        public void SetExchangeRate(string from, string to, double rate)
            => fxDB.Insert(new ExchangeRate { FromCurrency = from, ToCurrency = to, Rate = rate, EffectiveDate = DateTime.Now });

        public string[] GetSupportedCurrencies() => new[] { "ILS", "USD" };
    }
}
