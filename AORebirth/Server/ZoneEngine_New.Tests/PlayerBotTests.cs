namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Bots;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Mobs;

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
            Assert.IsTrue(arete.HuntAreas.Count >= 4, "Arete must have hunt areas around mob clusters.");
            for (int i = 0; i < arete.HuntAreas.Count; i++)
                Assert.IsTrue(arete.HuntAreas[i].Center.y < 25.0, "Arete hunt area must not be a rooftop: " + arete.HuntAreas[i].Name);

            Assert.IsTrue(catalog.TryGetZone(4582, out PlayerBotCatalog.ZoneConfig icc));
            Assert.IsTrue(icc.HuntAreas.Count >= 4);

            Assert.IsTrue(catalog.TryGetZone(4313, out PlayerBotCatalog.ZoneConfig nascence));
            Assert.IsTrue(nascence.HuntAreas.Count >= 4);
            for (int i = 0; i < nascence.HuntAreas.Count; i++)
                Assert.IsTrue(nascence.HuntAreas[i].Center.y < 60.0, "Nascence hunt area must not be the Y=76 hub: " + nascence.HuntAreas[i].Name);

            Assert.IsFalse(catalog.TryGetZone(4604, out _));
        }

        [TestMethod]
        public void PlayerBotDoesNotEngagePlayersPetsOrMercs()
        {
            var bot = new NpcCharacter(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 900010 },
                new StubItemBuilder())
            {
                Attackable = true
            };
            NpcBrain.Create(bot, home: null, NpcAiProfiles.PlayerBot);
            Assert.IsNotNull(bot.Brain?.PlayerBot);

            Player player = TestWorld.CreatePlayer(26);
            var wildMob = new NpcCharacter(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 900011 },
                new StubItemBuilder())
            {
                Attackable = true
            };
            var owned = new NpcCharacter(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 900012 },
                new StubItemBuilder())
            {
                Attackable = true
            };
            owned.Stats.Set(CharacterStat.PetMaster, player.Identity.Instance);

            Assert.IsTrue(CombatRules.CanAttack(bot, wildMob));
            Assert.IsFalse(CombatRules.CanAttack(bot, player));
            Assert.IsFalse(CombatRules.CanAttack(bot, owned));
            Assert.IsTrue(NpcSupportRules.IsPlayerPetOrMerc(owned));
        }

        [TestMethod]
        public void PlayerBotTargetingRejectsCiviliansAndOutOfLevelPrey()
        {
            var bot = new NpcCharacter(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 900020 },
                new StubItemBuilder())
            {
                Attackable = true
            };
            NpcBrain.Create(bot, home: null, NpcAiProfiles.PlayerBot);
            PlayerBotState state = bot.Brain!.PlayerBot!;
            state.MinPreyLevel = 1;
            state.MaxPreyLevel = 15;

            var wild = PreyNpc(900021, attackable: true, level: 10);
            Assert.IsTrue(PlayerBotTargeting.IsValidPrey(bot, wild, applyLevelRange: true));

            var unattackable = PreyNpc(900022, attackable: false, level: 10);
            Assert.IsFalse(PlayerBotTargeting.IsValidPrey(bot, unattackable, applyLevelRange: true));

            var noCombat = PreyNpc(900023, attackable: true, level: 10);
            noCombat.MobTemplate = new MobTemplate
            {
                Features = new HashSet<NpcFeature> { NpcFeature.NoCombat }
            };
            Assert.IsFalse(PlayerBotTargeting.IsValidPrey(bot, noCombat, applyLevelRange: true));

            var blue = PreyNpc(900024, attackable: true, level: 10);
            blue.Stats.Set(CharacterStat.Flags, (int)CharacterFlags.HasBlueName);
            Assert.IsFalse(PlayerBotTargeting.IsValidPrey(bot, blue, applyLevelRange: true));

            var highLevel = PreyNpc(900025, attackable: true, level: 24);
            Assert.IsFalse(PlayerBotTargeting.IsValidPrey(bot, highLevel, applyLevelRange: true));

            Player player = TestWorld.CreatePlayer(28);
            Assert.IsFalse(PlayerBotTargeting.IsValidPrey(bot, player, applyLevelRange: true));
        }

        [TestMethod]
        public void PlayerBotGateRejectsServiceNpcsAndSameSide()
        {
            var bot = new NpcCharacter(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 900030 },
                new StubItemBuilder())
            {
                Attackable = true
            };
            NpcBrain.Create(bot, home: null, NpcAiProfiles.PlayerBot);
            PlayerBotState state = bot.Brain!.PlayerBot!;
            state.MinPreyLevel = 1;
            state.MaxPreyLevel = 15;

            // Mission giver / dialog NPC that is not proximity-hostile (WoW: non-hostile with NPC flags).
            var giver = PreyNpc(900031, attackable: true, level: 10);
            giver.MobTemplate = new MobTemplate { KnuBotId = 1234 };
            Assert.IsFalse(PlayerBotTargeting.IsPossibleTarget(bot, giver, true, out string why));
            Assert.AreEqual("service npc", why);

            // Same mob, but hostile (BreedHostility > 0): a quest-linked wild mob is still prey.
            var hostileGiver = PreyNpc(900032, attackable: true, level: 10);
            hostileGiver.MobTemplate = new MobTemplate { KnuBotId = 1234 };
            hostileGiver.Stats.Set(NpcAiRules.BreedHostilityStat, 1);
            Assert.IsTrue(PlayerBotTargeting.IsValidPrey(bot, hostileGiver, applyLevelRange: true));

            // Terminals.
            var terminal = PreyNpc(900033, attackable: true, level: 10);
            terminal.Name = "Contract Terminal";
            Assert.IsFalse(PlayerBotTargeting.IsValidPrey(bot, terminal, applyLevelRange: true));

            // Same side as a Clan/Omni bot (WoW: IsFriendlyTo).
            bot.Stats.Set(CharacterStat.Side, (int)Side.Omni);
            var omniGuard = PreyNpc(900034, attackable: true, level: 10);
            omniGuard.Stats.Set(CharacterStat.Side, (int)Side.Omni);
            Assert.IsFalse(PlayerBotTargeting.IsPossibleTarget(bot, omniGuard, true, out why));
            Assert.AreEqual("same side", why);
            bot.Stats.Set(CharacterStat.Side, (int)Side.Neutral);

        }
        [TestMethod]
        public void PlayerBotBlacklistExpires()
        {
            var state = new PlayerBotState();
            var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 44 };
            DateTime now = DateTime.UtcNow;

            Assert.IsFalse(PlayerBotTargeting.IsBlacklisted(state, identity, now));
            PlayerBotTargeting.Blacklist(state, identity, now);
            Assert.IsTrue(PlayerBotTargeting.IsBlacklisted(state, identity, now.AddSeconds(1)));
            Assert.IsFalse(PlayerBotTargeting.IsBlacklisted(
                state,
                identity,
                now + PlayerBotTargeting.BlacklistDuration + TimeSpan.FromSeconds(1)));
        }

        [TestMethod]
        public void PlayerBotHuntPicksPointsInsideAreaAndShortensLongLegs()
        {
            var center = new Vector3(100, 5, 200);
            const float radius = 40f;
            for (int i = 0; i < 80; i++)
            {
                Vector3 point = PlayerBotHunt.RandomPointInArea(center, radius);
                Assert.IsTrue(
                    PlayerBotHunt.IsInsideArea(point, center, radius),
                    "Random hunt point must stay in the area.");
                Assert.AreEqual(5.0, point.y, 0.01, "Random hunt point keeps the area height until the floor snap.");
            }

            var from = new Vector3(0, 5, 0);
            var far = new Vector3(200, 9, 0);
            Vector3 step = PlayerBotHunt.StepToward(from, far, 35f);
            Assert.AreEqual(35.0, PlayerBotHunt.HorizontalDistance(from, step), 0.01);
            Assert.IsTrue(step.x > 0 && step.x < 200);

            Vector3 arrived = PlayerBotHunt.StepToward(from, new Vector3(10, 5, 0), 35f);
            Assert.AreEqual(10.0, arrived.x, 0.01);

            Vector3 side = PlayerBotHunt.StepToward(from, far, 35f, Math.PI / 2.0);
            Assert.AreEqual(35.0, PlayerBotHunt.HorizontalDistance(from, side), 0.01);
            Assert.AreEqual(0.0, side.x, 0.01);
            Assert.AreEqual(35.0, side.z, 0.01);

            var state = new PlayerBotState();
            var first = new PlayerBotHuntArea("A", new Vector3(1, 2, 3), 20f);
            var second = new PlayerBotHuntArea("B", new Vector3(50, 2, 50), 25f);
            state.HuntAreas = new[] { first, second };
            PlayerBotHunt.AssignArea(state, first, 0);
            Assert.AreEqual("A", state.HuntAreaName);
            Assert.IsTrue(PlayerBotHunt.SwitchArea(state));
            Assert.AreEqual("B", state.HuntAreaName);
            Assert.AreEqual(25f, state.HuntRadius);
            Assert.IsFalse(state.HasHuntDestination);
        }

        static NpcCharacter PreyNpc(int instance, bool attackable, int level)
        {
            var npc = new NpcCharacter(
                new Identity { Type = IdentityType.CanbeAffected, Instance = instance },
                new StubItemBuilder())
            {
                Attackable = attackable
            };
            npc.Stats.Set(CharacterStat.Level, level);
            return npc;
        }
    }
}
