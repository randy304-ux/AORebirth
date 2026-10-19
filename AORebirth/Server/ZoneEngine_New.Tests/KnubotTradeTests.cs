namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Knubot;

    [TestClass]
    public sealed class KnubotTradeTests
    {
        // Mason's AMCH hand-in: Blank ICC ID Chip + Biological Survey Nanobots.
        static readonly KnubotTrade Chip = new("turnin_chip", [],
            [new KnubotTradeItem("QICC", new HashSet<int> { 296575 }, 1), new KnubotTradeItem("QBSN", new HashSet<int> { 296574 }, 1)], 0, "done");

        static Item It(int id) => new() { LowId = id, HighId = id, Quality = 1 };

        [TestMethod]
        public void PartialHandInDoesNotMatch()
        {
            Assert.IsFalse(Chip.Matches([It(296575)], 0));
            Assert.IsFalse(Chip.Matches([It(296574)], 0));
            Assert.IsFalse(Chip.Matches([It(296575), It(296574), It(1)], 0));
            Assert.IsTrue(Chip.Matches([It(296574), It(296575)], 0));
        }

        [TestMethod]
        public void WindowHasOneSlotPerRequiredItem()
        {
            Assert.AreEqual(2, KnubotTrade.SlotsFor([Chip], null));
            KnubotTrade one = new("show", [], [new KnubotTradeItem("QLA3", new HashSet<int> { 113440 }, 1)], 0, "x");
            Assert.AreEqual(2, KnubotTrade.SlotsFor([one, Chip], null));
            Assert.AreEqual(1, KnubotTrade.SlotsFor([one], null));
        }

        [TestMethod]
        public void PartialHandInSaysHowManyAreNeeded()
            => StringAssert.Contains(KnubotTrade.RejectText([Chip], null, 1), "all 2 items");
    }
}
