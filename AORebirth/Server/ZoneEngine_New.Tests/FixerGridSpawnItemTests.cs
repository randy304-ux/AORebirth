namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.WorldSimulation;

    /// <summary>
    /// Hack Grid Data Stream / Instant Fixer Grid Conversion need FIGI (Data Receptacle 160978)
    /// and QIGR (Quiescent Grid Waveform Template 265479) resolvable in ItemTemplates. Enter The
    /// Grid terminal 95350 OnUseItemOn TeleportProxy2 lands on Fixer Grid pf 4107 destinations line 6.
    /// </summary>
    [TestClass]
    public sealed class FixerGridSpawnItemTests
    {
        const int DataReceptacleId = 160978;
        const int QuiescentGridWaveformTemplateId = 265479;
        const int FixerGridPlayfieldId = 4107;
        const byte FixerGridLandingLine = 6;
        // Packed Destinations.dat key from items.dat template 95350 OnUseItemOn TeleportProxy2 args.
        const int FixerGridPackedLine = 397323;

        const string FixerGridCatalogJson =
            """
            {
                "FIGI": { "Templates": [160978, 160978] },
                "QIGR": { "Templates": [265479, 265479] }
            }
            """;

        [TestMethod]
        public void FigiAndQigrResolveToRealItemsDatTemplates()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(FixerGridCatalogJson);
            Assert.IsTrue(catalog.CanResolveItem("FIGI"));
            Assert.IsTrue(catalog.CanResolveItem("QIGR"));

            var minter = new HashItemMinter(new StubGameData(catalog), new StubCatalog(), new StubItemBuilder());

            Assert.IsTrue(minter.TryRollIds("FIGI", 1, out int figiLow, out int figiHigh, out int figiQl));
            Assert.AreEqual(DataReceptacleId, figiLow);
            Assert.AreEqual(DataReceptacleId, figiHigh);
            Assert.AreEqual(1, figiQl);

            Assert.IsTrue(minter.TryRollIds("QIGR", 1, out int qigrLow, out int qigrHigh, out int qigrQl));
            Assert.AreEqual(QuiescentGridWaveformTemplateId, qigrLow);
            Assert.AreEqual(QuiescentGridWaveformTemplateId, qigrHigh);
            Assert.AreEqual(1, qigrQl);
        }

        [TestMethod]
        public void SpawnItemHashesMintDataReceptacleAndWaveformTemplate()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(FixerGridCatalogJson);
            var minter = new HashItemMinter(new StubGameData(catalog), new StubCatalog(), new StubItemBuilder());

            Assert.IsTrue(minter.TryMint("FIGI", 1, ItemSource.Other, out Item receptacle));
            Assert.AreEqual(DataReceptacleId, receptacle.LowId);
            Assert.AreEqual(DataReceptacleId, receptacle.HighId);

            Assert.IsTrue(minter.TryMint("QIGR", 1, ItemSource.Other, out Item waveform));
            Assert.AreEqual(QuiescentGridWaveformTemplateId, waveform.LowId);
            Assert.AreEqual(QuiescentGridWaveformTemplateId, waveform.HighId);
        }

        [TestMethod]
        public void EnterTheGridOnUseItemOnTeleportProxy2LandsOnFixerGridLine6()
        {
            // items.dat 95350 OnUseItemOn TeleportProxy2 args: PlayfieldDoor, 4107, 0, 0, Playfield3, 397323
            // SecondaryItemTemplate requirement Value=160978 (Data Receptacle) is on the same event.
            var args = new List<object>
            {
                (int)IdentityType.PlayfieldDoor,
                FixerGridPlayfieldId,
                0,
                0,
                (int)IdentityType.Playfield3,
                FixerGridPackedLine
            };

            Assert.IsTrue(
                PortalDoorLandingResolver.TryParseProxyDestination(
                    args,
                    PortalDoorLandingResolver.Proxy2EntryDoorClearance,
                    recordsReturn: false,
                    out PortalDestination destination));

            Assert.AreEqual(FixerGridPlayfieldId, destination.PlayfieldId);
            Assert.AreEqual(PortalLandingKind.DestinationMidpoint, destination.Kind);
            Assert.AreEqual(FixerGridLandingLine, destination.DestinationIndex);
            Assert.IsFalse(destination.RecordsReturn);

            uint packed = unchecked((uint)FixerGridPackedLine);
            Assert.AreEqual(FixerGridLandingLine, (byte)(packed >> 16));
            Assert.AreEqual((uint)FixerGridPlayfieldId, packed & 0xFFFFu);
            Assert.AreEqual(DataReceptacleId, 160978);
        }
    }
}
