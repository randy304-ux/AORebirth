namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Pets;

    /// <summary>
    /// Merc menu invariants: outer text:// tell, nested detail text:// links (escaped),
    /// ASCII-only separators, owner detail vs guest lists, size under MessageMax.
    /// </summary>
    [TestClass]
    public sealed class MercMenuRenderTests
    {
        [TestMethod]
        public void LiveStyleOuter_HasSingleOuterTextHref_AndRawMarkup()
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
                + " <font color=#D3BFFF>Twink - L24</font>\n";

            string message = MercMenuService.BuildOuter(inner);

            Assert.AreEqual(1, Regex.Matches(message, "href=\"text://").Count);
            Assert.IsFalse(message.Contains("&lt;", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains("&#39;chatcmd", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("<font color=#DEDE42>Enforcer</font>", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains(">Merc Menu</a>", StringComparison.Ordinal));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(message));
        }

        [TestMethod]
        public void NestedLink_EscapesBody_AndParsesAsSingleQuotedHref()
        {
            string detail =
                "<font color=#D3BFFF>\n"
                + "<font color=#DEDE42>Soldier - Basic</font>\n"
                + "<a href='chatcmd:///tell Merc dismiss soldier'>Dismiss</a>\n";

            string link = MercMenuService.NestedLink(detail, "Soldier");

            Assert.IsTrue(link.StartsWith("<a href='text://", StringComparison.Ordinal));
            Assert.IsTrue(link.Contains("&lt;font", StringComparison.Ordinal));
            Assert.IsTrue(link.Contains("&#39;chatcmd:///tell Merc dismiss soldier&#39;", StringComparison.Ordinal));
            Assert.IsFalse(link.Contains("<font color=#DEDE42>Soldier", StringComparison.Ordinal));
            Assert.IsTrue(link.EndsWith(">Soldier</a>", StringComparison.Ordinal));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(link));

            // One-level unescape recovers the detail markup the client shows.
            string hrefStart = "href='text://";
            int start = link.IndexOf(hrefStart, StringComparison.Ordinal) + hrefStart.Length;
            int end = link.IndexOf("'>", start, StringComparison.Ordinal);
            string escaped = link.Substring(start, end - start);
            string recovered = escaped
                .Replace("&lt;", "<", StringComparison.Ordinal)
                .Replace("&gt;", ">", StringComparison.Ordinal)
                .Replace("&quot;", "\"", StringComparison.Ordinal)
                .Replace("&#39;", "'", StringComparison.Ordinal)
                .Replace("&amp;", "&", StringComparison.Ordinal);
            Assert.IsTrue(recovered.Contains("<a href='chatcmd:///tell Merc dismiss soldier'>Dismiss</a>", StringComparison.Ordinal));
            Assert.IsTrue(recovered.Contains("Soldier - Basic", StringComparison.Ordinal));
        }

        [TestMethod]
        public void MainWithNestedDetail_HasNestedTextLinks_AsciiOnly_WithinLimit()
        {
            string message = MercMenuService.Render.MainWithNestedDetailSample();

            Assert.AreEqual(1, Regex.Matches(message, "href=\"text://").Count);
            Assert.IsTrue(Regex.Matches(message, "href='text://").Count >= 2);
            Assert.IsTrue(message.Contains("&lt;font", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("chatcmd:///tell Merc dismiss soldier", StringComparison.Ordinal)
                || message.Contains("&#39;chatcmd:///tell Merc dismiss soldier", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains(">dismiss</a>", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains("type .merc dismiss", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains(" · ", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains(" — ", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("Basic - hire", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("Twink - L24", StringComparison.Ordinal));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(message));
            Assert.IsTrue(message.Length <= MercMenuService.MessageMax);
            Assert.IsTrue(message.Contains("chatcmd:///tell Merc filter basic", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("chatcmd:///tell Merc filter summoned", StringComparison.Ordinal));
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
                shown: "Brock Anvil Malone",
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
                mercName: "Brock Anvil Malone",
                buffs: new List<(int, string, int)> { (1001, "Composite Attribute Boost", 4) });

            Assert.AreEqual(1, Regex.Matches(owner, "href=\"text://").Count);
            Assert.AreEqual(1, Regex.Matches(guest, "href=\"text://").Count);
            Assert.IsFalse(owner.Contains("&lt;", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("&lt;", StringComparison.Ordinal));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(owner));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(guest));

            Assert.IsTrue(owner.Contains("chatcmd:///tell Merc dismiss", StringComparison.Ordinal));
            Assert.IsTrue(owner.Contains("chatcmd:///tell Merc preset keeper aura heal", StringComparison.Ordinal));
            Assert.IsTrue(owner.Contains("chatcmd:///tell Merc cast 1001", StringComparison.Ordinal));
            Assert.IsTrue(owner.Contains(">Stance</font>", StringComparison.Ordinal)
                || owner.Contains("Stance</font>", StringComparison.Ordinal));
            Assert.IsTrue(owner.Contains(" - Twink", StringComparison.Ordinal));

            Assert.IsTrue(guest.Contains("can buff you", StringComparison.Ordinal));
            Assert.IsTrue(guest.Contains("chatcmd:///tell Merc cast 1001", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("chatcmd:///tell Merc summon", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("chatcmd:///tell Merc dismiss", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("chatcmd:///tell Merc preset", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("Stance", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("AOE", StringComparison.Ordinal));
        }

        [TestMethod]
        public void MainHeader_UsesNewlinesAndFilterChatcmds()
        {
            string header = MercMenuService.Render.MainHeader("twink");
            Assert.IsTrue(header.Contains("\n", StringComparison.Ordinal));
            Assert.IsFalse(header.Contains("<br>", StringComparison.Ordinal));
            Assert.IsTrue(header.Contains("<font color=#DEDE42>[twink]</font>", StringComparison.Ordinal));
            Assert.IsTrue(header.Contains("chatcmd:///tell Merc filter all", StringComparison.Ordinal));
            Assert.IsTrue(header.Contains("chatcmd:///tell Merc filter summoned", StringComparison.Ordinal));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(header));
            // Filters stay chatcmd: embedding every filtered main page as nested text://
            // would multiply the blob past MessageMax once detail pages are nested too.
        }
    }
}
