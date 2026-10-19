namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Tradeskills;

    [TestClass]
    public sealed class TradeskillTests
    {
        const int NanoProgramming = 160;
        const int QuantumFt = 157;

        static TradeskillRecipe FadedSharpObj() => new()
        {
            Source = new[] { 101370 }, Target = new[] { 101102 }, Result = new[] { 101103, 101104 },
            DeleteSource = true, QlRangePercent = 18, Skills = new[] { new[] { NanoProgramming, 125, 100 } },
            MaxBump = 5, MinXp = 5, MaxXp = 1000, Implant = true
        };

        static TradeskillRecipe ShinyJobe() => new()
        {
            Source = new[] { 166222 }, Target = new[] { 101214 }, Result = new[] { 1, 2 }, DeleteSource = true,
            QlRangePercent = 14, Skills = new[] { new[] { NanoProgramming, 500, 400 }, new[] { QuantumFt, 625, 400 } },
            MaxBump = 5, Implant = true
        };

        [TestMethod]
        public void RequirementRoundsUp()
        {
            Assert.AreEqual(360, TradeskillRules.Required(360, 100));
            Assert.AreEqual(42, TradeskillRules.Required(125, 33));
            Assert.AreEqual(950, TradeskillRules.Required(475, 200));
        }

        [TestMethod]
        public void ClusterQualityFollowsTheGradePercent()
        {
            TradeskillRecipe faded = FadedSharpObj();
            Assert.AreEqual(82, TradeskillRules.MinSourceQuality(faded, 100));
            Assert.IsTrue(TradeskillRules.SourceInRange(faded, 82, 100));
            Assert.IsFalse(TradeskillRules.SourceInRange(faded, 81, 100));
            Assert.IsTrue(TradeskillRules.SourceInRange(faded, 200, 100));
            Assert.AreEqual(100, TradeskillRules.MinSourceQuality(new TradeskillRecipe { QlRangePercent = 1 }, 100));
            Assert.AreEqual(80, TradeskillRules.MinSourceQuality(new TradeskillRecipe { MinSourcePct = 80 }, 100));
            Assert.AreEqual(0, TradeskillRules.MinSourceQuality(new TradeskillRecipe(), 100));
        }

        [TestMethod]
        public void ImplantBumpTableMatchesTheGuide()
        {
            Assert.AreEqual(0, TradeskillRules.ImplantMaxBump(49));
            Assert.AreEqual(1, TradeskillRules.ImplantMaxBump(50));
            Assert.AreEqual(2, TradeskillRules.ImplantMaxBump(100));
            Assert.AreEqual(3, TradeskillRules.ImplantMaxBump(199));
            Assert.AreEqual(0, TradeskillRules.ImplantMaxBump(200));
            Assert.AreEqual(4, TradeskillRules.ImplantMaxBump(201));
            Assert.AreEqual(5, TradeskillRules.ImplantMaxBump(299));
            Assert.AreEqual(0, TradeskillRules.ImplantMaxBump(300));
        }

        [TestMethod]
        public void BumpsComeFromSkillAboveTheRequirement()
        {
            TradeskillRecipe faded = FadedSharpObj();
            // QL 100 needs 125 NP; every 100 above bumps one QL, at most 2 for a QL 100 implant.
            Assert.AreEqual(0, TradeskillRules.Bumps(faded, 100, _ => 224));
            Assert.AreEqual(1, TradeskillRules.Bumps(faded, 100, _ => 225));
            Assert.AreEqual(2, TradeskillRules.Bumps(faded, 100, _ => 1000));
            Assert.AreEqual(0, TradeskillRules.Bumps(faded, 40, _ => 1000));
            Assert.AreEqual(3, TradeskillRules.Bumps(faded, 197, _ => 2000));
            var range = TradeskillRules.ResultRange(faded, 199, 3, 1, 200);
            Assert.AreEqual((199, 200), range);
        }

        [TestMethod]
        public void TheWeakestSkillDecidesTheBump()
        {
            TradeskillRecipe jobe = ShinyJobe();
            int Skill(int stat) => stat == NanoProgramming ? 1300 : 625 + 50;
            Assert.AreEqual(0, TradeskillRules.Bumps(jobe, 100, Skill));
            Assert.AreEqual(2, TradeskillRules.Bumps(jobe, 100, _ => 5000));
        }

        [TestMethod]
        public void JobeClustersNeedQl99AndABumpTo100()
        {
            TradeskillRecipe jobe = ShinyJobe();
            Assert.IsFalse(TradeskillRules.JobeAllowed(jobe, 98, 99));
            Assert.IsFalse(TradeskillRules.JobeAllowed(jobe, 99, 99));
            Assert.IsTrue(TradeskillRules.JobeAllowed(jobe, 99, 100));
            Assert.IsTrue(TradeskillRules.JobeAllowed(FadedSharpObj(), 20, 20));
        }

        [TestMethod]
        public void SliderPicksInsideTheRangeElseTheTop()
        {
            Assert.AreEqual(101, TradeskillRules.PickQuality(101, (100, 102)));
            Assert.AreEqual(102, TradeskillRules.PickQuality(0, (100, 102)));
            Assert.AreEqual(102, TradeskillRules.PickQuality(300, (100, 102)));
        }

        [TestMethod]
        public void XpRunsAcrossTheResultQualityRange()
        {
            TradeskillRecipe faded = FadedSharpObj();
            Assert.AreEqual(5, TradeskillRules.Xp(faded, 1, 1, 200));
            Assert.AreEqual(1000, TradeskillRules.Xp(faded, 200, 1, 200));
            Assert.AreEqual(780, TradeskillRules.Xp(new TradeskillRecipe { MinXp = 780, MaxXp = 780 }, 50, 50, 50));
            Assert.AreEqual(0, TradeskillRules.Xp(new TradeskillRecipe(), 50, 1, 200));
        }

        [TestMethod]
        public void HandMadeRecipesWinOverTheCellaoFile()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ts-book-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "Tradeskills.json"),
                    "[{\"Name\":\"alien\",\"Source\":[10],\"Target\":[20,21],\"Result\":[30,31]}]");
                File.WriteAllText(Path.Combine(dir, "Tradeskills.cellao.json"),
                    "[{\"Source\":[10],\"Target\":[20],\"Result\":[99,99]},{\"Source\":[11],\"Target\":[20],\"Result\":[40,41],\"Skills\":[[160,125,100]],\"Implant\":true}]");
                File.WriteAllText(Path.Combine(dir, "Tradeskills.zzz.json"),
                    "[{\"Name\":\"overlay\",\"Source\":[11],\"Target\":[20],\"Result\":[50,51]}]");
                var lines = new List<string>();
                TradeskillRecipeBook book = TradeskillRecipeBook.Load(dir, lines.Add);
                Assert.AreEqual(2, book.Count);
                Assert.IsTrue(book.TryFind(1, 10, 1, 20, out TradeskillRecipe alien));
                Assert.AreEqual("alien", alien.Name);
                Assert.IsTrue(book.TryFind(11, 11, 21, 20, out TradeskillRecipe overlay));
                Assert.AreEqual("overlay", overlay.Name);
                Assert.IsFalse(book.TryFind(12, 12, 20, 20, out _));
                Assert.AreEqual(2, book.TargetProcesses(20, 20));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [TestMethod]
        public void TheConvertedCellaoFileLoadsWithTheGuideNumbers()
        {
            string? root = FindGameData();
            if (root == null)
                Assert.Inconclusive("GameData/Tradeskills.cellao.json not found from " + AppContext.BaseDirectory);
            var clock = Stopwatch.StartNew();
            TradeskillRecipeBook book = TradeskillRecipeBook.Load(root!, line => Console.WriteLine(line));
            Console.WriteLine("load ms=" + clock.ElapsedMilliseconds);
            Assert.IsTrue(book.Count > 100000, "recipes=" + book.Count);

            // Sharp Obj Cluster - Faded (Eye) + Basic Eye Implant: 1.25x NP, every 100 bumps, cluster >= 82%.
            Assert.IsTrue(book.TryFind(101369, 101370, 101101, 101102, out TradeskillRecipe faded));
            Assert.AreEqual(101104, faded.ResultHigh);
            CollectionAssert.AreEqual(new[] { NanoProgramming, 125, 100 }, faded.Skills[0]);
            Assert.AreEqual(18, faded.QlRangePercent);
            Assert.IsTrue(faded.Implant && faded.DeleteSource && faded.DeleteTarget);

            // Implant Disassembly Clinic: 4.75x B&E (corrected), 1x NP, clinic >= 90%, clinic kept.
            Assert.IsTrue(book.TryFind(161867, 161867, 101103, 101104, out TradeskillRecipe clinic));
            CollectionAssert.AreEqual(new[] { 165, 475, 0 }, clinic.Skills[0]);
            Assert.AreEqual(10, clinic.QlRangePercent);
            Assert.IsFalse(clinic.DeleteSource);

            // Symbol Library + Instruction Disc: 4x NP and CL, library down to 25% of the disc (corrected).
            Assert.IsTrue(book.TryFind(149838, 149838, 144818, 144818, out TradeskillRecipe library));
            Assert.AreEqual(75, library.QlRangePercent);
            Assert.AreEqual(400, library.Skills[0][1]);
        }

        static readonly Dictionary<int, string> Names = new()
        {
            [101261] = "Basic Leg Implant", [101262] = "Basic Leg Implant",
            [101101] = "Basic Eye Implant", [101102] = "Basic Eye Implant",
            [101117] = "Basic Head Implant", [101118] = "Basic Head Implant",
            [101213] = "Basic Waist Implant", [101214] = "Basic Waist Implant",
            [101103] = "Eye Implant: Sharp Obj, Faded", [101104] = "Eye Implant: Sharp Obj, Faded",
            [101369] = "Sharp Obj Cluster - Faded (Eye)", [101370] = "Sharp Obj Cluster - Faded (Eye)",
        };

        static int[] Fam(int low, int high) => TradeskillRecipeBook.Family(low, high, id => Names.GetValueOrDefault(id));

        [TestMethod]
        public void SingleIdItemsFindTheirPair()
        {
            CollectionAssert.AreEquivalent(new[] { 101261, 101262 }, Fam(101261, 101261));
            CollectionAssert.AreEquivalent(new[] { 101262, 101261 }, Fam(101262, 101262));
            CollectionAssert.AreEquivalent(new[] { 101782, 101781 }, Fam(101781, 101782));
            CollectionAssert.AreEquivalent(new[] { 555 }, Fam(555, 555));
        }

        [TestMethod]
        public void ClustersGoIntoEmptyBasicImplantsWhateverIdTheItemWasMintedWith()
        {
            string? root = FindGameData();
            if (root == null)
                Assert.Inconclusive("GameData not found");
            TradeskillRecipeBook book = TradeskillRecipeBook.Load(root!, _ => { });

            // Randy 2026-10-19: Agility Cluster - Shiny (Leg) QL 5 (101781/101782) + Basic Leg Implant QL 1 (101261/101261).
            Assert.IsTrue(book.TryFind(Fam(101781, 101782), Fam(101261, 101261), out TradeskillRecipe shiny));
            Assert.AreEqual(113128, shiny.ResultHigh);
            Assert.AreEqual(450, shiny.Skills[0][1]); // Agility 2.25 x2
            Assert.AreEqual(14, shiny.QlRangePercent);
            // Faded Sharp Obj (single id) into Basic Eye (single id).
            Assert.IsTrue(book.TryFind(Fam(101369, 101369), Fam(101101, 101101), out TradeskillRecipe faded));
            Assert.AreEqual(101104, faded.ResultHigh);
            // Bright Grenade (Eye) into the Faded Sharp Obj eye implant: second slot.
            Assert.IsTrue(book.TryFind(Fam(101431, 101432), Fam(101103, 101103), out TradeskillRecipe bright));
            Assert.AreEqual(101862, bright.ResultHigh);
            Assert.AreEqual(285, bright.Skills[0][1]);
            // Shiny Treatment (Head) and Faded Evade-ClsC (Waist).
            Assert.IsTrue(book.TryFind(Fam(101523, 101524), Fam(101117, 101117), out TradeskillRecipe head));
            Assert.AreEqual(430, head.Skills[0][1]);
            Assert.IsTrue(book.TryFind(Fam(101681, 101682), Fam(101213, 101213), out TradeskillRecipe waist));
            Assert.AreEqual(200, waist.Skills[0][1]);
            // Wrong body location: a Leg cluster does not go into a Head implant.
            Assert.IsFalse(book.TryFind(Fam(101781, 101782), Fam(101117, 101118), out _));
            Assert.IsTrue(book.TargetProcesses(Fam(101261, 101261)) > 0);
        }
        [TestMethod]
        public void LegImplantClustersGiveTheSameImplantInEitherOrder()
        {
            string? root = FindGameData();
            if (root == null)
                Assert.Inconclusive("GameData not found");
            TradeskillRecipeBook book = TradeskillRecipeBook.Load(root!, _ => { });

            // Randy 2026-10-19 16:33: Shiny Agility then Bright Stamina.
            Assert.IsTrue(book.TryFind(Fam(101781, 101782), Fam(101261, 101261), out TradeskillRecipe agi));
            Assert.AreEqual(113127, agi.ResultLow);
            Assert.IsTrue(book.TryFind(Fam(101785, 101786), Fam(agi.ResultLow, agi.ResultHigh), out TradeskillRecipe agiStam));
            Assert.AreEqual(113186, agiStam.ResultLow);
            Assert.AreEqual(113187, agiStam.ResultHigh);
            // Bright Stamina first, then Shiny Agility: the same Agility+Stamina implant.
            Assert.IsTrue(book.TryFind(Fam(101785, 101786), Fam(101261, 101261), out TradeskillRecipe stam));
            Assert.AreEqual(113258, stam.ResultHigh);
            Assert.AreNotEqual(stam.ResultHigh, agi.ResultHigh);
            Assert.IsTrue(book.TryFind(Fam(101781, 101782), Fam(stam.ResultLow, stam.ResultHigh), out TradeskillRecipe stamAgi));
            Assert.AreEqual(113186, stamAgi.ResultLow);
            // Faded Max Health completes the guide implant.
            Assert.IsTrue(book.TryFind(Fam(101807, 101808), Fam(113186, 113187), out TradeskillRecipe full));
            Assert.AreEqual(113440, full.ResultLow);
            // Faded first (Randy's later tries): Max Health + Agility, Max Health + Stamina.
            Assert.IsTrue(book.TryFind(Fam(101807, 101808), Fam(101261, 101261), out TradeskillRecipe mh));
            Assert.AreEqual(113397, mh.ResultHigh);
            Assert.IsTrue(book.TryFind(Fam(101781, 101782), Fam(mh.ResultLow, mh.ResultHigh), out TradeskillRecipe mhAgi));
            Assert.AreEqual(113420, mhAgi.ResultLow);
            Assert.IsTrue(book.TryFind(Fam(101785, 101786), Fam(mh.ResultLow, mh.ResultHigh), out TradeskillRecipe mhStam));
            Assert.AreEqual(113521, mhStam.ResultLow);
        }

        static string? FindGameData()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "AORebirth", "GameData");
                if (File.Exists(Path.Combine(candidate, TradeskillRecipeBook.CellaoFileName)))
                    return candidate;
                candidate = Path.Combine(dir.FullName, "GameData");
                if (File.Exists(Path.Combine(candidate, TradeskillRecipeBook.CellaoFileName)))
                    return candidate;
            }
            return null;
        }
    }
}
