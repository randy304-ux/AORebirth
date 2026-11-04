namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Pets;

    /// <summary>
    /// Merc menu invariants: href="text://" (double quotes) for tell and nested pages;
    /// inner pages use only single-quoted attrs and contain no raw double quotes; no entities.
    /// </summary>
    [TestClass]
    public sealed class MercMenuRenderTests
    {
        [TestMethod]
        public void LiveStyleOuter_HasDoubleQuotedTextHref_AndRawMarkup()
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
            Assert.IsFalse(message.Contains("&#39;", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("<font color=#DEDE42>Enforcer</font>", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains(">Merc Menu</a>", StringComparison.Ordinal));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(message));
        }

        [TestMethod]
        public void NestedLink_UsesDoubleQuotedTextHref_InnerHasNoDoubleQuotes()
        {
            string detail =
                "<font color=#D3BFFF>\n"
                + "<font color=#DEDE42>Soldier - Basic</font>\n"
                + "<a href='chatcmd:///tell Merc dismiss soldier'>Dismiss</a>\n";

            string link = MercMenuService.NestedLink(detail, "Soldier");

            Assert.IsTrue(link.StartsWith("<a href=\"text://", StringComparison.Ordinal), link);
            Assert.IsFalse(link.StartsWith("<a href='text://", StringComparison.Ordinal), link);
            Assert.IsTrue(link.Contains("<font color=#DEDE42>Soldier - Basic</font>", StringComparison.Ordinal));
            Assert.IsTrue(link.Contains("<a href='chatcmd:///tell Merc dismiss soldier'>Dismiss</a>", StringComparison.Ordinal));
            Assert.IsFalse(link.Contains("&lt;", StringComparison.Ordinal));
            Assert.IsFalse(link.Contains("&#39;", StringComparison.Ordinal));
            Assert.IsFalse(link.Contains("&quot;", StringComparison.Ordinal));

            const string hrefStart = "href=\"text://";
            int start = link.IndexOf(hrefStart, StringComparison.Ordinal) + hrefStart.Length;
            int end = link.LastIndexOf("\">", StringComparison.Ordinal);
            Assert.IsTrue(end > start, link);
            string nestedBody = link.Substring(start, end - start);
            Assert.IsFalse(nestedBody.Contains("\"", StringComparison.Ordinal), nestedBody);
            Assert.IsTrue(nestedBody.Contains("href='chatcmd://", StringComparison.Ordinal), nestedBody);
            Assert.IsTrue(link.EndsWith(">Soldier</a>", StringComparison.Ordinal));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(link));
        }

        [TestMethod]
        public void MainWithNestedDetail_DoubleQuotedNestedPages_NoInnerDoubleQuotes()
        {
            string message = MercMenuService.Render.MainWithNestedDetailSample();

            // Tell wrapper + nested Soldier + nested dungeon name + nested Macros.
            Assert.IsTrue(Regex.Matches(message, "href=\"text://").Count >= 3, message);
            Assert.AreEqual(0, Regex.Matches(message, "href='text://").Count, message);
            Assert.IsFalse(message.Contains("&lt;", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains("&#39;", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("<a href='chatcmd:///tell Merc dismiss soldier'>Dismiss</a>", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains(">dismiss</a>", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains("type .merc dismiss", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("Basic - hire", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("Twink - L24", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains(">Macros</a>", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains("chatcmd:///tell Merc macros", StringComparison.Ordinal));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(message));
            Assert.IsTrue(message.Length <= MercMenuService.MessageMax);

            AssertNestedPageBodyHasNoDoubleQuotes(message, "Soldier");
            AssertNestedPageBodyHasNoDoubleQuotes(message, "Macros");
        }

        static void AssertNestedPageBodyHasNoDoubleQuotes(string message, string label)
        {
            string needle = "\">" + label + "</a>";
            int labelAt = message.IndexOf(needle, StringComparison.Ordinal);
            Assert.IsTrue(labelAt > 0, "missing nested link label " + label);
            int hrefAt = message.LastIndexOf("href=\"text://", labelAt, StringComparison.Ordinal);
            Assert.IsTrue(hrefAt >= 0, "missing href=\"text:// before " + label);
            int bodyStart = hrefAt + "href=\"text://".Length;
            string body = message.Substring(bodyStart, labelAt - bodyStart);
            Assert.IsFalse(body.Contains("\"", StringComparison.Ordinal), label + " body has double quote: " + body);
            Assert.IsFalse(body.Contains("&lt;", StringComparison.Ordinal), label);
            Assert.IsFalse(body.Contains("&#39;", StringComparison.Ordinal), label);
        }

        [TestMethod]
        public void EscText_StripsQuotes_NoEntities()
        {
            Assert.AreEqual("Assassins Grin", MercMenuService.EscText("Assassin's Grin"));
            Assert.AreEqual("BE", MercMenuService.EscText("B\"E"));
            Assert.IsFalse(MercMenuService.EscText("Assassin's").Contains("&#39;", StringComparison.Ordinal));
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
            Assert.IsFalse(owner.Contains("&#39;", StringComparison.Ordinal));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(owner));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(guest));

            Assert.IsTrue(owner.Contains("chatcmd:///tell Merc dismiss", StringComparison.Ordinal));
            Assert.IsTrue(owner.Contains("chatcmd:///tell Merc preset keeper aura heal", StringComparison.Ordinal));
            Assert.IsTrue(owner.Contains("chatcmd:///tell Merc cast 1001", StringComparison.Ordinal));
            Assert.IsTrue(owner.Contains(" - Twink", StringComparison.Ordinal));

            Assert.IsTrue(guest.Contains("can buff you", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("chatcmd:///tell Merc summon", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("chatcmd:///tell Merc dismiss", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("Stance", StringComparison.Ordinal));
        }

        [TestMethod]
        public void MainHeader_NestedMacros_DoubleQuoted_NoInnerDoubleQuotes()
        {
            string header = MercMenuService.Render.MainHeader("twink");
            Assert.IsTrue(header.Contains("\n", StringComparison.Ordinal));
            Assert.IsTrue(header.Contains("<font color=#DEDE42>[twink]</font>", StringComparison.Ordinal));
            Assert.IsTrue(header.Contains("chatcmd:///tell Merc filter all", StringComparison.Ordinal));
            Assert.IsTrue(header.Contains("href=\"text://", StringComparison.Ordinal));
            Assert.IsFalse(header.Contains("href='text://", StringComparison.Ordinal));
            Assert.IsTrue(header.Contains(">Macros</a>", StringComparison.Ordinal));
            Assert.IsFalse(header.Contains("chatcmd:///tell Merc macros", StringComparison.Ordinal));
            Assert.IsFalse(header.Contains("&lt;", StringComparison.Ordinal));
            Assert.IsFalse(header.Contains("&#39;", StringComparison.Ordinal));
            AssertNestedPageBodyHasNoDoubleQuotes(header, "Macros");
            Assert.IsTrue(MercMenuService.IsAsciiOnly(header));
        }
    }
}
