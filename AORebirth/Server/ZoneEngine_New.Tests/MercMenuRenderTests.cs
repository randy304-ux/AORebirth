namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Pets;

    /// <summary>
    /// Merc menu: outer <c>href="text://"</c>; nested <c>href='text://'</c> with raw AOML
    /// (no HTML tag escape); cast/dismiss use unquoted <c>chatcmd:///say .merc</c>;
    /// filters are nested trimmed name lists.
    /// </summary>
    [TestClass]
    public sealed class MercMenuRenderTests
    {
        [TestMethod]
        public void LiveStyleOuter_HasDoubleQuotedTextHref_AndRawMarkup()
        {
            string message = MercMenuService.BuildOuter(
                "<font color=#D3BFFF>\n<font color=#DEDE42>Mercs</font>\n");

            Assert.AreEqual(1, Regex.Matches(message, "href=\"text://").Count);
            Assert.IsTrue(message.Contains(">Merc Menu</a>", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains("&lt;font", StringComparison.Ordinal));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(message));
        }

        [TestMethod]
        public void NestedLink_UsesSingleQuotedTextHref_AndRawBody()
        {
            string detail =
                "<font color=#D3BFFF>\n"
                + "<font color=#DEDE42>Soldier - Basic</font>\n"
                + "<a href=chatcmd:///say .merc dismiss soldier>Dismiss</a>\n";

            string link = MercMenuService.NestedLink(detail, "Soldier");

            Assert.IsTrue(link.StartsWith("<a href='text://", StringComparison.Ordinal), link);
            Assert.IsFalse(link.StartsWith("<a href=\"text://", StringComparison.Ordinal), link);
            Assert.IsFalse(link.Contains("&lt;font", StringComparison.Ordinal), link);
            Assert.IsTrue(link.Contains("<font color=#DEDE42>", StringComparison.Ordinal), link);
            Assert.IsTrue(link.Contains("href=chatcmd:///say .merc dismiss soldier>", StringComparison.Ordinal), link);
            Assert.IsFalse(link.Contains("href='chatcmd", StringComparison.Ordinal), link);
            Assert.IsTrue(MercMenuService.IsAsciiOnly(link));
        }

        [TestMethod]
        public void NestedLink_RejectsRawQuotesInBody()
        {
            Assert.ThrowsException<InvalidOperationException>(
                () => MercMenuService.NestedLink("<a href='chatcmd:///say .merc cast 1'>X</a>", "X"));
            Assert.ThrowsException<InvalidOperationException>(
                () => MercMenuService.NestedLink("<a href=\"chatcmd:///say .merc cast 1\">X</a>", "X"));
        }

        [TestMethod]
        public void MainMenu_NestedSingleQuote_NoDoubleQuotedNestedInsideOuter()
        {
            string message = MercMenuService.Render.MainWithNestedDetailSample();

            Assert.AreEqual(1, Regex.Matches(message, "href=\"text://").Count, message);
            Assert.IsTrue(Regex.Matches(message, "href='text://").Count >= 3, message);
            Assert.IsTrue(message.Contains("chatcmd:///say .merc dismiss soldier", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains("chatcmd:///say .merc cast ", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("chatcmd:///tell", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("chatcmd:///say .merc contract", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("chatcmd:///say .merc view", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("chatcmd:///say .merc macros", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("chatcmd:///say .merc filter", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains(">Macros</a>", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("&lt;font", StringComparison.Ordinal), message);
            Assert.IsTrue(MercMenuService.IsAsciiOnly(message));
            Assert.IsTrue(message.Length <= MercMenuService.MessageMax);

            const string marker = "href=\"text://";
            int start = message.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            int end = message.LastIndexOf("\">", StringComparison.Ordinal);
            Assert.IsTrue(end > start, message);
            string outerBody = message.Substring(start, end - start);
            Assert.IsFalse(outerBody.Contains("href=\"text://", StringComparison.Ordinal), outerBody);
            Assert.IsFalse(outerBody.Contains("\"", StringComparison.Ordinal), "outer body has raw double quote: " + outerBody);
        }

        [TestMethod]
        public void Detail_BuffsAndBack_UseSayLinks()
        {
            string owner = MercMenuService.Render.DetailSample(
                shown: "Soldier",
                tier: "Basic",
                description: "Profession hire.",
                tierBlurb: "Basic.",
                summoned: true,
                canSummon: false,
                stances: new List<(string, string)> { ("keeper aura heal", "Aura: heal") },
                buffs: new List<(int, string, int, bool)>
                {
                    (1001, "Team Reflective Field", 4, true),
                    (1002, "Locked Buff", 8, false),
                });

            Assert.AreEqual(1, Regex.Matches(owner, "href=\"text://").Count);
            Assert.IsTrue(owner.Contains("chatcmd:///say .merc cast 1001", StringComparison.Ordinal), owner);
            Assert.IsTrue(owner.Contains("chatcmd:///say .merc preset keeper aura heal", StringComparison.Ordinal), owner);
            Assert.IsTrue(owner.Contains("chatcmd:///say .merc menu", StringComparison.Ordinal), owner);
            Assert.IsTrue(owner.Contains("chatcmd:///say .merc dismiss", StringComparison.Ordinal), owner);
            Assert.IsFalse(owner.Contains("chatcmd:///tell", StringComparison.Ordinal), owner);
            Assert.IsTrue(owner.Contains("href=chatcmd:///say .merc cast 1001>", StringComparison.Ordinal), owner);
        }

        [TestMethod]
        public void HiredContract_AfterParkDismiss_StillListedWithSummon()
        {
            string parked = MercMenuService.Render.HiredContractRowSample(present: false);
            Assert.IsTrue(parked.Contains("href='text://", StringComparison.Ordinal), parked);
            Assert.IsTrue(parked.Contains("chatcmd:///say .merc call", StringComparison.Ordinal), parked);
            Assert.IsFalse(parked.Contains("chatcmd:///tell", StringComparison.Ordinal), parked);
            Assert.IsFalse(parked.Contains("&lt;font", StringComparison.Ordinal), parked);
        }

        [TestMethod]
        public void ParkDismiss_KeepsContractBit_EndDismissClearsIt()
        {
            const int soldierBit = 1;
            int bits = 260 | (1 << soldierBit);
            Assert.AreEqual(262, PetService.ContractBitsAfterDismiss(bits, soldierBit, endContract: false));
            Assert.AreEqual(260, PetService.ContractBitsAfterDismiss(bits, soldierBit, endContract: true));
        }

        [TestMethod]
        public void MessageMax_IsUnderInt16()
        {
            Assert.IsTrue(MercMenuService.MessageMax <= 32000);
        }

        [TestMethod]
        public void MainHeader_MacrosAndFiltersAreNestedSingleQuoted()
        {
            string header = MercMenuService.Render.MainHeader("twink");
            Assert.IsTrue(header.Contains("href='text://", StringComparison.Ordinal), header);
            Assert.IsTrue(header.Contains(">Macros</a>", StringComparison.Ordinal), header);
            Assert.IsTrue(header.Contains("[twink]", StringComparison.Ordinal), header);
            Assert.IsTrue(header.Contains(">basic</a>", StringComparison.Ordinal)
                || header.Contains(">all</a>", StringComparison.Ordinal), header);
            Assert.IsFalse(header.Contains("href=\"text://", StringComparison.Ordinal), header);
            Assert.IsFalse(header.Contains("chatcmd:///say .merc macros", StringComparison.Ordinal), header);
            Assert.IsFalse(header.Contains("chatcmd:///say .merc filter", StringComparison.Ordinal), header);
            Assert.IsFalse(header.Contains("chatcmd:///tell", StringComparison.Ordinal));
            Assert.IsFalse(header.Contains("&lt;font", StringComparison.Ordinal), header);
        }
    }
}
