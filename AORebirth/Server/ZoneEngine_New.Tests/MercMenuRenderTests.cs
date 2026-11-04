namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Pets;

    /// <summary>
    /// Live-style merc menu invariants: one text:// blob, raw markup (no &lt; tags),
    /// owner detail vs non-owner guest lists differ (no stance/summon on guest).
    /// </summary>
    [TestClass]
    public sealed class MercMenuRenderTests
    {
        [TestMethod]
        public void LiveStyleOuter_HasSingleTextHref_AndRawMarkup()
        {
            string inner =
                "<font color=#D3BFFF>\n"
                + "<font color=#DEDE42>Mercs</font>\n"
                + "Filter: <font color=#DEDE42>[all]</font>\n"
                + "\n    <img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n\n"
                + "    <img src=tdb://id:GFX_GUI_FRIENDLIST_STATUS_GREEN> "
                + "<img src=tdb://id:GFX_GUI_ICON_PROFESSION_9> "
                + "<font color=#DEDE42>Enforcer</font>\n"
                + "    <a href='chatcmd:///tell Merc view enforcer_subway'>Anvil / Grit</a>"
                + " <font color=#D3BFFF>Twink · L24</font>\n";

            string message = MercMenuService.BuildOuter(inner);

            Assert.AreEqual(1, Regex.Matches(message, "text://").Count);
            Assert.IsFalse(message.Contains("&lt;", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains("&#39;chatcmd", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("<font color=#DEDE42>Enforcer</font>", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("chatcmd:///tell Merc view enforcer_subway", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains(">Merc Menu</a>", StringComparison.Ordinal));

            int first = message.IndexOf("text://", StringComparison.Ordinal);
            string after = message.Substring(first + "text://".Length);
            Assert.IsFalse(after.Contains("text://", StringComparison.Ordinal));
        }

        [TestMethod]
        public void EscText_EscapesAmpAndApos_NotUsedOnTags()
        {
            Assert.AreEqual("Assassin&#39;s Grin", MercMenuService.EscText("Assassin's Grin"));
            Assert.AreEqual("B&amp;E", MercMenuService.EscText("B&E"));
            Assert.IsTrue(
                MercMenuService.BuildOuter("<font color=#DEDE42>X</font>\n")
                    .Contains("<font", StringComparison.Ordinal));
        }

        [TestMethod]
        public void MessageMax_IsUnderInt16()
        {
            Assert.IsTrue(MercMenuService.MessageMax <= 32000);
            Assert.IsTrue(MercMenuService.MessageMax > 0);
        }

        [TestMethod]
        public void OwnerDetail_HasSummonStanceAndBuffs_GuestOmitsControls()
        {
            string owner = MercMenuService.Render.DetailSample(
                shown: "Brock \"Anvil\" Malone",
                tier: "Twink",
                description: "Unlock from Condemned Subway (Abmouth Supremus).",
                tierBlurb: "Twink-tier dungeon merc.",
                summoned: true,
                canSummon: false,
                stances: new List<(string, string)> { ("keeper aura heal", "Aura: heal") },
                buffs: new List<(int, string, int, bool)>
                {
                    (1001, "Composite Attribute Boost", 4, true),
                    (1002, "Locked Buff", 8, false),
                });

            string guest = MercMenuService.Render.GuestBuffsSample(
                mercName: "Brock \"Anvil\" Malone",
                buffs: new List<(int, string, int)> { (1001, "Composite Attribute Boost", 4) });

            Assert.AreEqual(1, Regex.Matches(owner, "text://").Count);
            Assert.AreEqual(1, Regex.Matches(guest, "text://").Count);
            Assert.IsFalse(owner.Contains("&lt;", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("&lt;", StringComparison.Ordinal));

            Assert.IsTrue(owner.Contains("chatcmd:///tell Merc dismiss", StringComparison.Ordinal));
            Assert.IsTrue(owner.Contains("chatcmd:///tell Merc preset keeper aura heal", StringComparison.Ordinal));
            Assert.IsTrue(owner.Contains("chatcmd:///tell Merc cast 1001", StringComparison.Ordinal));
            Assert.IsTrue(owner.Contains(">Stance</font>", StringComparison.Ordinal)
                || owner.Contains("Stance</font>", StringComparison.Ordinal));

            Assert.IsTrue(guest.Contains("can buff you", StringComparison.Ordinal));
            Assert.IsTrue(guest.Contains("chatcmd:///tell Merc cast 1001", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("chatcmd:///tell Merc summon", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("chatcmd:///tell Merc dismiss", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("chatcmd:///tell Merc preset", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("Stance", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("AOE", StringComparison.Ordinal));

            int ownerFirst = owner.IndexOf("text://", StringComparison.Ordinal);
            Assert.IsFalse(owner.Substring(ownerFirst + "text://".Length).Contains("text://", StringComparison.Ordinal));
            int guestFirst = guest.IndexOf("text://", StringComparison.Ordinal);
            Assert.IsFalse(guest.Substring(guestFirst + "text://".Length).Contains("text://", StringComparison.Ordinal));
        }

        [TestMethod]
        public void MainHeader_UsesNewlinesAndFilterLinks()
        {
            string header = MercMenuService.Render.MainHeader("twink");
            Assert.IsTrue(header.Contains("\n", StringComparison.Ordinal));
            Assert.IsFalse(header.Contains("<br>", StringComparison.Ordinal));
            Assert.IsTrue(header.Contains("<font color=#DEDE42>[twink]</font>", StringComparison.Ordinal));
            Assert.IsTrue(header.Contains("chatcmd:///tell Merc filter all", StringComparison.Ordinal));
            Assert.IsTrue(header.Contains("chatcmd:///tell Merc filter summoned", StringComparison.Ordinal));
        }
    }
}
