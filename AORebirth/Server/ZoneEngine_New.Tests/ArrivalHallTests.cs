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
            var icc = dynels.Single(d => d.IdentityInstance == ArrivalHall.IccDoor);
            var jobe = dynels.Single(d => d.IdentityInstance == ArrivalHall.JobeDoor);
            var sl = dynels.Single(d => d.IdentityInstance == ArrivalHall.ShadowlandsDoor);
            var term = dynels.Single(d => d.IdentityInstance == ArrivalHall.ShadowlandsTerminal);

            Assert.AreEqual(100406, north.TemplateId);
            Assert.AreEqual(100406, arete.TemplateId);
            Assert.AreEqual(100406, icc.TemplateId);
            Assert.AreEqual(100406, jobe.TemplateId);
            Assert.AreEqual(100406, sl.TemplateId);
            Assert.AreEqual(95351, term.TemplateId);

            // North door is the farthest door in the hall and is not a walk-in exit.
            Assert.AreEqual(dynels.Where(d => d.TemplateId == 100406).Max(d => d.Position.Z), north.Position.Z, 0.01f);
            // Red left: Exit to Clans -> Arete.
            Assert.AreEqual(201.49f, arete.Position.X, 0.1f);
            Assert.AreEqual(154.31f, arete.Position.Z, 0.1f);
            // Middle: Exit to Neutral -> ICC Shuttleport.
            Assert.AreEqual(191.95f, icc.Position.X, 0.1f);
            Assert.AreEqual(153.55f, icc.Position.Z, 0.1f);
            // Blue right: Exit to Omni-Tek -> Jobe Research.
            Assert.AreEqual(181.88f, jobe.Position.X, 0.1f);
            Assert.AreEqual(154.33f, jobe.Position.Z, 0.1f);

            // The new-character landing is inside the hall, south of the north door and clear of its 3 m walk-in pad.
            Vector3 land = ArrivalHall.NorthDoorLanding;
            double dx = land.xf - north.Position.X, dz = land.zf - north.Position.Z;
            Assert.IsTrue(Math.Sqrt(dx * dx + dz * dz) > 4.0);
            Assert.IsTrue(land.zf < north.Position.Z);
        }

        [TestMethod]
        public void SouthDoorWalkInPadsSitInTheDoorway()
        {
            Assert.AreEqual(201.6f, ArrivalHall.AreteWalkIn.xf, 0.01f);
            Assert.AreEqual(1.0f, ArrivalHall.AreteWalkIn.yf, 0.01f);
            Assert.AreEqual(157.7f, ArrivalHall.AreteWalkIn.zf, 0.01f);
            Assert.AreEqual(192.0f, ArrivalHall.IccWalkIn.xf, 0.01f);
            Assert.AreEqual(157.9f, ArrivalHall.IccWalkIn.zf, 0.01f);
            Assert.AreEqual(181.8f, ArrivalHall.JobeWalkIn.xf, 0.01f);
            Assert.AreEqual(157.9f, ArrivalHall.JobeWalkIn.zf, 0.01f);
            Assert.AreEqual(3f, ArrivalHall.WalkInRadius);

            // Live doorway /loc is more than 3 m from the door dynel (Z ~154, inside the wall).
            var dynels = Data.Value.GetPlayfieldGeometry(ArrivalHall.PlayfieldId).Dynels!.Dynels;
            void TooFarFromDoor(int door, Vector3 walk)
            {
                var d = dynels.Single(x => x.IdentityInstance == door);
                double dx = walk.xf - d.Position.X, dz = walk.zf - d.Position.Z;
                Assert.IsTrue(Math.Sqrt(dx * dx + dz * dz) > ArrivalHall.WalkInRadius);
            }
            TooFarFromDoor(ArrivalHall.AreteDoor, ArrivalHall.AreteWalkIn);
            TooFarFromDoor(ArrivalHall.IccDoor, ArrivalHall.IccWalkIn);
            TooFarFromDoor(ArrivalHall.JobeDoor, ArrivalHall.JobeWalkIn);
        }

        [TestMethod]
        public void JobeResearchLandingIsInFrontOfDrake()
        {
            // SCDK (Scientist Drake Rodriguez) spawn point in Jobe Research 4001.
            Assert.AreEqual(4001, ArrivalHall.JobeResearch);
            Assert.AreEqual(4582, ArrivalHall.IccShuttleport);
            Assert.AreEqual(934.9f, ArrivalHall.IccShuttleportLanding.xf, 0.01f);
            Assert.AreEqual(24.8f, ArrivalHall.IccShuttleportLanding.yf, 0.01f);
            Assert.AreEqual(762.0f, ArrivalHall.IccShuttleportLanding.zf, 0.01f);
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
