namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Configuration;
    using ZoneEngine_New.Core.Missions;

    [TestClass]
    public sealed class MishrollTargetingFeeCalculatorTests
    {
        static GameplayMishrollTargetingFee DefaultFee() => new()
        {
            FreeThroughQuality = 25,
            FullFeeQuality = 200,
            FullFeeCredits = 10000,
            FeeCurveFraction = 0.5,
            BreakAndEntryDiscountPercentPerTen = 1,
            BreakAndEntryDiscountCapPercent = 50
        };

        [TestMethod]
        public void Fee_AtZeroBreakAndEntry_MatchesDeployTable()
        {
            var fee = DefaultFee();
            Assert.AreEqual(0, MishrollTargetingFeeCalculator.Compute(1, 0, fee));
            Assert.AreEqual(0, MishrollTargetingFeeCalculator.Compute(25, 0, fee));
            Assert.AreEqual(57, MishrollTargetingFeeCalculator.Compute(26, 0, fee));
            Assert.AreEqual(1429, MishrollTargetingFeeCalculator.Compute(50, 0, fee));
            Assert.AreEqual(4286, MishrollTargetingFeeCalculator.Compute(100, 0, fee));
            Assert.AreEqual(7143, MishrollTargetingFeeCalculator.Compute(150, 0, fee));
            Assert.AreEqual(10000, MishrollTargetingFeeCalculator.Compute(200, 0, fee));
            Assert.AreEqual(10000, MishrollTargetingFeeCalculator.Compute(220, 0, fee));
        }

        [TestMethod]
        public void Fee_BreakAndEntryDiscount_AppliesOnceAndCaps()
        {
            var fee = DefaultFee();
            // QL200 base 10000; 100 B&E => 10% off => 9000
            Assert.AreEqual(9000, MishrollTargetingFeeCalculator.Compute(200, 100, fee));
            // 500 B&E would be 50% at 1%/10; cap 50% => 5000
            Assert.AreEqual(5000, MishrollTargetingFeeCalculator.Compute(200, 500, fee));
            // 600 B&E still capped at 50%
            Assert.AreEqual(5000, MishrollTargetingFeeCalculator.Compute(200, 600, fee));
        }
    }
}
