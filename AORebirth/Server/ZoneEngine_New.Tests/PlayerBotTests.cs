namespace ZoneEngine_New.Tests
{
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Bots;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class PlayerBotTests
    {
        [TestMethod]
        public void PlayerBotProfileCreatesActionLoopState()
        {
            var npc = new NpcCharacter(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 900001 },
                new StubItemBuilder())
            {
                Attackable = true,
                Position = new Vector3(0, 0, 0)
            };

            NpcBrain brain = NpcBrain.Create(npc, home: null, NpcAiProfiles.PlayerBot);

            Assert.IsNotNull(brain.PlayerBot);
            Assert.IsTrue(NpcAiProfiles.PlayerBot.PlayerBot);
            Assert.IsFalse(NpcAiProfiles.Default.PlayerBot);
        }

        [TestMethod]
        public void CatalogLoadsStarterZonesFromGameData()
        {
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GameData"));
            if (!File.Exists(Path.Combine(root, PlayerBotCatalog.FileName)))
                root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "GameData"));
            if (!File.Exists(Path.Combine(root, PlayerBotCatalog.FileName)))
                Assert.Inconclusive("PlayerBots.json not found beside the test host.");

            var gameData = new StubGameData(HashItemCatalog.Parse("{}")) { RootPath = root };
            var catalog = new PlayerBotCatalog(gameData, new StubLogger());

            Assert.IsTrue(catalog.Enabled);
            Assert.IsTrue(catalog.CountPerPlayfield is >= 5 and <= 10);
            Assert.IsTrue(catalog.TryGetZone(6553, out PlayerBotCatalog.ZoneConfig arete));
            Assert.AreEqual(6553, arete.Id);
            Assert.IsTrue(arete.Points.Count >= 3);
            Assert.IsTrue(catalog.TryGetZone(4582, out _));
            Assert.IsTrue(catalog.TryGetZone(4313, out _));
            Assert.IsFalse(catalog.TryGetZone(4604, out _));
        }
    }
}
