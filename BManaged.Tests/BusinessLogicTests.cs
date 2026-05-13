using BusinessLogic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Model;
using Model.Helpers;

namespace BManaged.Tests
{
    [TestClass]
    public class BusinessLogicTests
    {
        [TestMethod]
        public void VatCalculator_UsesCurrentIsraeliDefaultRate()
        {
            Assert.AreEqual(18m, VatCalculator.VatOn(100m));
            Assert.AreEqual(118m, VatCalculator.GrossOf(100m));
            Assert.AreEqual(100m, VatCalculator.SubtotalFromGross(118m));
        }

        [TestMethod]
        public void InvoiceNumberer_FormatsFiveDigitSequence()
        {
            StringAssert.Matches(InvoiceNumberer.Next(42),
                new System.Text.RegularExpressions.Regex(@"^INV-\d{4}-00042$"));
        }

        [TestMethod]
        public void SecurityHelper_VerifiesOnlyMatchingPassword()
        {
            string hash = SecurityHelper.HashPassword("correct horse");

            Assert.IsTrue(SecurityHelper.VerifyPassword("correct horse", hash));
            Assert.IsFalse(SecurityHelper.VerifyPassword("wrong horse", hash));
        }

        // -- New-field defaults & assignment -------------------------------
        // ExpenseDB / LoanDB do idempotent ALTER TABLE on first instantiation
        // for these columns, so a missing/old .accdb still loads. Model-side
        // we just want to lock in the defaults + the fact the values flow.

        [TestMethod]
        public void Expense_RecurringKind_DefaultsToNull_AndAcceptsKnownKinds()
        {
            var e = new Expense();
            Assert.IsNull(e.RecurringKind, "RecurringKind should default to null (one-time).");

            e.RecurringKind = "Fixed";    Assert.AreEqual("Fixed",    e.RecurringKind);
            e.RecurringKind = "Variable"; Assert.AreEqual("Variable", e.RecurringKind);
            e.RecurringKind = null;       Assert.IsNull(e.RecurringKind);
        }

        [TestMethod]
        public void Expense_Currency_DefaultsToILS()
        {
            Assert.AreEqual("ILS", new Expense().Currency,
                "Currency must default to ILS — the Israeli market default.");
        }

        [TestMethod]
        public void Loan_HasStandingOrder_DefaultsFalse_AndIsIndependentOfKerenBacked()
        {
            var l = new Loan();
            Assert.IsFalse(l.HasStandingOrder, "Standing-order flag must default to false.");
            Assert.IsFalse(l.IsKerenBacked,    "Keren-backed flag must default to false.");

            l.HasStandingOrder = true;
            Assert.IsTrue(l.HasStandingOrder);
            Assert.IsFalse(l.IsKerenBacked, "Toggling standing-order must not flip Keren flag.");

            l.IsKerenBacked = true;
            Assert.IsTrue(l.HasStandingOrder, "Toggling Keren flag must not flip standing-order.");
        }

        [TestMethod]
        public void Loan_DefaultsLookSane_ForFreshRow()
        {
            var l = new Loan();
            Assert.IsTrue(l.IsActive,                "A new loan should default to active.");
            Assert.AreEqual("ILS", l.Currency,       "Loan currency must default to ILS.");
            Assert.IsFalse(l.HasStandingOrder);
            Assert.IsFalse(l.IsKerenBacked);
        }
    }
}
