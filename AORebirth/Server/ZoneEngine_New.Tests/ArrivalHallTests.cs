namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Knubot;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Quests;
    using ZoneEngine_New.Core.WorldSimulation;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>Dungeon Dogs Arrival Hall (4604) new-character start.</summary>
    [TestClass]
    public sealed class ArrivalHallTests
    {
        sealed class CapturingLogger : IZoneLogger
        {
            public readonly List<string> Lines = new();
            public void Debug(string message) { }
            public void Info(string message) { }
            public void Warn(string message) => Lines.Add("WARN " + message);
            public void Error(string message) => Lines.Add("ERROR " + message);
            public void Error(Exception exception, string message) => Lines.Add("ERROR " + message);
            public IZoneLogger CreateForPlayfield(int playfieldId) => this;
        }

        static readonly Lazy<GameDataStore> Data = new(() => new GameDataStore(new StubLogger()));

        [TestMethod]
        public void HallDoorsAreTheDoorsInDynelsDat()
        {
            var dynels = Data.Value.GetPlayfieldGeometry(ArrivalHall.PlayfieldId).Dynels!.Dynels;
            var north = dynels.Single(d => d.IdentityInstance == ArrivalHall.NorthDoor);
            var arete = dynels.Single(d => d.IdentityInstance == ArrivalHall.AreteDoor);
            var sl = dynels.Single(d => d.IdentityInstance == ArrivalHall.ShadowlandsDoor);
            var term = dynels.Single(d => d.IdentityInstance == ArrivalHall.ShadowlandsTerminal);

            Assert.AreEqual(100406, north.TemplateId);
            Assert.AreEqual(100406, arete.TemplateId);
            Assert.AreEqual(100406, sl.TemplateId);
            Assert.AreEqual(95351, term.TemplateId);

            // North door is the farthest door in the hall.
            Assert.AreEqual(dynels.Where(d => d.TemplateId == 100406).Max(d => d.Position.Z), north.Position.Z, 0.01f);
            // Arete door: the middle south-wall door, between the ICC officer side and the Jobe/Shadowlands side.
            Assert.AreEqual(191.95f, arete.Position.X, 0.1f);
            Assert.AreEqual(153.55f, arete.Position.Z, 0.1f);

            // The new-character landing is inside the hall, south of the north door and clear of its 3 m walk-in pad.
            Vector3 land = ArrivalHall.NorthDoorLanding;
            double dx = land.xf - north.Position.X, dz = land.zf - north.Position.Z;
            Assert.IsTrue(Math.Sqrt(dx * dx + dz * dz) > 4.0);
            Assert.IsTrue(land.zf < north.Position.Z);
        }

        [TestMethod]
        public void JobeResearchLandingIsInFrontOfDrake()
        {
            // SCDK (Scientist Drake Rodriguez) spawn point in Jobe Research 4001.
            Assert.AreEqual(4001, ArrivalHall.JobeResearch);
            double dx = ArrivalHall.JobeResearchLanding.xf - 854.5, dz = ArrivalHall.JobeResearchLanding.zf - 958.6;
            double d = Math.Sqrt(dx * dx + dz * dz);
            Assert.IsTrue(d > 2.0 && d < 8.0);
        }

        [TestMethod]
        public void OnlyPendingCharactersOnTheNewCharacterPadArePlaced()
        {
            // No mission store: never placed (fails closed).
            Assert.IsFalse(ArrivalHall.ShouldPlaceNewCharacter(6553, 3609, 786.2, 1, null));
            // Elsewhere: never placed, whatever the state.
            Assert.IsFalse(ArrivalHall.ShouldPlaceNewCharacter(4582, 3609, 786.2, 1, null));
            Assert.IsFalse(ArrivalHall.ShouldPlaceNewCharacter(6553, 3455, 865, 1, null));
        }

        [TestMethod]
        public void ImmigrationOfficerScriptLoads()
        {
            var logger = new CapturingLogger();
            var items = new ItemTemplateCatalog(Data.Value, new StubLogger());
            var quests = new QuestCatalog(Data.Value, new StubLogger());
            var catalog = new KnubotCatalog(Data.Value, quests, items, logger);
            var mine = logger.Lines.Where(l => l.Contains("ImmigrationOfficer", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(0, mine.Length, string.Join(" | ", mine));
            Assert.AreEqual(0, logger.Lines.Count(l => l.StartsWith("ERROR", StringComparison.Ordinal)), string.Join(" | ", logger.Lines));
        }
    }
}
