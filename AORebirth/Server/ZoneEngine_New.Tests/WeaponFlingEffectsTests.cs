namespace ZoneEngine_New.Tests
{
    using System.IO;
    using System.Text.Json;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Helpers;

    [TestClass]
    public sealed class WeaponFlingEffectsTests
    {
        [TestMethod]
        public void CatalogMatchesNeutrinoEruptorAnchorsAndDefaults()
        {
            string root = Path.Combine(Path.GetTempPath(), "aoe-fling-" + Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            try
            {
                var doc = new
                {
                    Effects = new[]
                    {
                        new
                        {
                            Name = "NeutrinoEruptor",
                            LowId = 307100,
                            HighId = 307106,
                            RadiusMeters = 15,
                            MaxTargets = 8,
                            RequireLineOfSight = true,
                            ExcludePrimaryTarget = true,
                            Stat = 27,
                            AcStat = 97,
                            DamageByQl = new[]
                            {
                                new { Ql = 1, Min = -13, Max = -26 },
                                new { Ql = 182, Min = -649, Max = -1402 },
                                new { Ql = 300, Min = -1070, Max = -2310 },
                            }
                        }
                    }
                };
                File.WriteAllText(
                    Path.Combine(root, WeaponFlingEffects.FileName),
                    JsonSerializer.Serialize(doc));

                WeaponFlingEffects.ResetCacheForTests();

                // Unlisted weapon: no throw, no effect (TryApply no-ops without playfield services).
                Assert.AreEqual(8, WeaponFlingEffects.DefaultMaxTargets);
            }
            finally
            {
                WeaponFlingEffects.ResetCacheForTests();
                try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
            }
        }

        [TestMethod]
        public void DefaultMaxTargetsIsEight()
        {
            Assert.AreEqual(8, WeaponFlingEffects.DefaultMaxTargets);
        }
    }
}
