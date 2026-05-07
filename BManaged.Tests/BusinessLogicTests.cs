using BusinessLogic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    }
}
