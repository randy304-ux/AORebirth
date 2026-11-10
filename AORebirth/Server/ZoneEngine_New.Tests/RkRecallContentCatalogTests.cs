namespace ZoneEngine_New.Tests
{
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.GameData;

    [TestClass]
    public sealed class RkRecallContentCatalogTests
    {
        const string Sample = """
            {
              "SchemaVersion": 1,
              "ItemIds": [268748],
              "PlayfieldId": 655,
              "Position": [3211.15, 35.1, 865.7],
              "Heading": [0, 0.70710677, 0, 0.70710677]
            }
            """;

        [TestMethod]
        public void ParseAcceptsTheIccMidpointLanding()
        {
            RkRecallContentCatalog content = RkRecallContentCatalog.Parse(Sample);
            Assert.IsTrue(content.AppliesTo(268748));
            Assert.IsFalse(content.AppliesTo(259103));
            Assert.IsTrue(content.TryGetLanding(out int playfieldId, out var landing, out var heading));
            Assert.AreEqual(655, playfieldId);
            Assert.AreEqual(3211.15f, landing.xf, 0.0001f);
            Assert.AreEqual(35.1f, landing.yf, 0.0001f);
            Assert.AreEqual(865.7f, landing.zf, 0.0001f);
            Assert.IsNotNull(heading);
        }

        [TestMethod]
        public void LoadReturnsEmptyWhenTheFileIsMissing()
        {
            RkRecallContentCatalog content = RkRecallContentCatalog.Load(
                Path.Combine(Path.GetTempPath(), "rk-recall-missing-" + Path.GetRandomFileName()));
            Assert.IsFalse(content.IsConfigured);
            Assert.IsFalse(content.AppliesTo(268748));
        }

        [TestMethod]
        public void ParseRejectsABadSchema()
        {
            Assert.ThrowsException<InvalidDataException>(
                () => RkRecallContentCatalog.Parse("""{"SchemaVersion": 2, "ItemIds": [268748], "PlayfieldId": 655, "Position": [1, 2, 3]}"""));
        }
    }
}
