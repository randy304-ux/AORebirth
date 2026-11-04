namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Pets;

    /// <summary>
    /// Merc menu: outer <c>href="text://"</c>; nested raw AOML; production chatcmd uses
    /// <c>href=&amp;#39;...&amp;#39;</c> (variant C); filters NestedLinkDeep to merc pages.
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
        public void NestedLink_UsesSingleQuotedTextHref_RawBody_AndEntityChatcmd()
        {
            string detail =
                "<font color=#D3BFFF>\n"
                + "<font color=#DEDE42>Soldier - Basic</font>\n"
                + "<a href=&#39;chatcmd:///say .merc dismiss soldier&#39;>Dismiss</a>\n";

            string link = MercMenuService.NestedLink(detail, "Soldier");

            Assert.IsTrue(link.StartsWith("<a href='text://", StringComparison.Ordinal), link);
            Assert.IsFalse(link.Contains("&lt;font", StringComparison.Ordinal), link);
            Assert.IsTrue(link.Contains("<font color=#DEDE42>", StringComparison.Ordinal), link);
            Assert.IsTrue(link.Contains("href=&#39;chatcmd:///say .merc dismiss soldier&#39;>", StringComparison.Ordinal), link);
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
        public void NestedLinkDeep_UsesEntityQuotedTextHref()
        {
            string deep = MercMenuService.NestedLinkDeep(
                "<font color=#D3BFFF>\n<a href=&#39;chatcmd:///say .merc menu&#39;>Back</a>\n",
                "Merc");
            Assert.IsTrue(deep.StartsWith("<a href=&#39;text://", StringComparison.Ordinal), deep);
            Assert.IsTrue(deep.Contains("<font color=#D3BFFF>", StringComparison.Ordinal), deep);
        }

        [TestMethod]
        public void MainMenu_ProductionEntityChatcmd_AndNestedFilters()
        {
            string message = MercMenuService.Render.MainWithNestedDetailSample();

            Assert.AreEqual(1, Regex.Matches(message, "href=\"text://").Count, message);
            Assert.IsTrue(Regex.Matches(message, "href='text://").Count >= 3, message);
            Assert.IsTrue(message.Contains("href=&#39;chatcmd:///say .merc dismiss soldier&#39;", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains("href=&#39;chatcmd:///say .merc cast ", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("chatcmd:///tell", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("chatcmd:///say .merc filter", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("chatcmd:///say .merc view", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("&lt;font", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Length <= MercMenuService.MessageMax);

            const string marker = "href=\"text://";
            int start = message.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            int end = message.LastIndexOf("\">", StringComparison.Ordinal);
            string outerBody = message.Substring(start, end - start);
            Assert.IsFalse(outerBody.Contains("\"", StringComparison.Ordinal), outerBody);
        }

        [TestMethod]
        public void MainHeader_FilterNestedLinkDeep()
        {
            string header = MercMenuService.Render.MainHeader("twink");
            Assert.IsTrue(header.Contains("href=&#39;text://", StringComparison.Ordinal), header);
            Assert.IsTrue(header.Contains("[twink]", StringComparison.Ordinal), header);
            Assert.IsFalse(header.Contains("chatcmd:///say .merc filter", StringComparison.Ordinal), header);
        }

        [TestMethod]
        public void LinkTest_HasVariantsAndNestedInNested()
        {
            string page = MercMenuService.Render.LinkTestSample();
            Assert.IsTrue(page.Contains(">Open variants</a>", StringComparison.Ordinal), page);
            Assert.IsTrue(page.Contains("href=&quot;chatcmd:///say .merc menu&quot;", StringComparison.Ordinal), page);
            Assert.IsTrue(page.Contains("NOT LIVE", StringComparison.Ordinal), page);
            Assert.IsTrue(page.Contains("href=&#39;chatcmd:///say .merc menu&#39;", StringComparison.Ordinal), page);
            Assert.IsTrue(page.Contains("href=chatcmd:///say%20.merc%20menu>", StringComparison.Ordinal), page);
            Assert.IsTrue(page.Contains("Nadybot", StringComparison.Ordinal), page);
            Assert.IsTrue(page.Contains("href=&#39;text://", StringComparison.Ordinal), page);
            Assert.IsFalse(page.Contains("\"", StringComparison.Ordinal), page);

            string msg = MercMenuService.BuildOuter(page, "Merc linktest");
            Assert.AreEqual(1, Regex.Matches(msg, "href=\"text://").Count);
        }

        [TestMethod]
        public void Detail_BuffsAndBack_UseEntitySayLinks()
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
            Assert.IsTrue(owner.Contains("href=&#39;chatcmd:///say .merc cast 1001&#39;", StringComparison.Ordinal), owner);
            Assert.IsTrue(owner.Contains("href=&#39;chatcmd:///say .merc menu&#39;", StringComparison.Ordinal), owner);
            Assert.IsFalse(owner.Contains("chatcmd:///tell", StringComparison.Ordinal), owner);
        }

        [TestMethod]
        public void HiredContract_AfterParkDismiss_StillListedWithSummon()
        {
            string parked = MercMenuService.Render.HiredContractRowSample(present: false);
            Assert.IsTrue(parked.Contains("href='text://", StringComparison.Ordinal), parked);
            Assert.IsTrue(parked.Contains("href=&#39;chatcmd:///say .merc call&#39;", StringComparison.Ordinal), parked);
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
    }
}
