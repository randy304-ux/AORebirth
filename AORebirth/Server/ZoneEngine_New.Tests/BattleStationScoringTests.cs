namespace ZoneEngine_New.Tests
{
    using System;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Battlestations;

    [TestClass]
    public sealed class BattleStationScoringTests
    {
        [TestMethod]
        public void ToWireOwner_MapsMatchSides()
        {
            Assert.AreEqual(2, BattleStationScoring.ToWireOwner(0));
            Assert.AreEqual(0, BattleStationScoring.ToWireOwner(1));
            Assert.AreEqual(1, BattleStationScoring.ToWireOwner(2));
        }

        [TestMethod]
        public void EndPvpScore_LossAt100_IsAbout83()
        {
            int score = BattleStationScoring.ComputeEndPvpScore(
                pointsEarned: 100,
                kills: 0,
                won: false,
                lossMult: 0.83,
                winMult: 1.35,
                killBonus: 2);
            Assert.AreEqual(83, score);
        }

        [TestMethod]
        public void EndPvpScore_WinClearlyHigherThanLoss()
        {
            int loss = BattleStationScoring.ComputeEndPvpScore(100, 0, false, 0.83, 1.35, 2);
            int win = BattleStationScoring.ComputeEndPvpScore(100, 0, true, 0.83, 1.35, 2);
            Assert.IsTrue(win > loss);
            Assert.AreEqual(135, win);
        }

        [TestMethod]
        public void EndPvpScore_KillsAddBonusBeforeMult()
        {
            int score = BattleStationScoring.ComputeEndPvpScore(100, 5, false, 0.83, 1.35, 2);
            // (100 + 10) * 0.83 = 91.3 → 91
            Assert.AreEqual(91, score);
        }

        [TestMethod]
        public void RollVpReward_StaysInConfiguredRange()
        {
            var rng = new Random(42);
            for (int i = 0; i < 40; i++)
            {
                int v = BattleStationScoring.RollVpReward(30, 60, rng);
                Assert.IsTrue(v >= 30 && v <= 60, "rolled " + v);
            }
        }

        [TestMethod]
        public void PickRoleIndex_FewerCappersWhenWeightLower()
        {
            var rng = new Random(7);
            int cappers = 0;
            const int n = 1000;
            for (int i = 0; i < n; i++)
            {
                if (BattleStationScoring.PickRoleIndex(15, 20, 35, 30, rng) == 0)
                    cappers++;
            }

            // ~15% ± slack
            Assert.IsTrue(cappers > 80 && cappers < 220, "cappers=" + cappers);
        }
    }
}
