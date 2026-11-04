namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Pets;

    /// <summary>
    /// Merc menu: ChatText window via BuildOuter; navigation via chatcmd:///say .merc (not /tell).
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
            Assert.IsTrue(MercMenuService.IsAsciiOnly(message));
        }

        [TestMethod]
        public void MainMenu_UsesSayCommandNav_NotTell()
        {
            string message = MercMenuService.Render.MainWithChatcmdNavSample();

            Assert.AreEqual(1, Regex.Matches(message, "href=\"text://").Count, message);
            Assert.IsTrue(message.Contains("chatcmd:///say .merc contract soldier", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains("chatcmd:///say .merc view enforcer_subway", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains("chatcmd:///say .merc macros", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains("chatcmd:///say .merc dismiss soldier", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("chatcmd:///tell", StringComparison.Ordinal), message);
            Assert.IsTrue(MercMenuService.IsAsciiOnly(message));
            Assert.IsTrue(message.Length <= MercMenuService.MessageMax);
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
        }

        [TestMethod]
        public void HiredContract_AfterParkDismiss_StillListedWithSummon()
        {
            string parked = MercMenuService.Render.HiredContractRowSample(present: false);
            Assert.IsTrue(parked.Contains("chatcmd:///say .merc contract soldier", StringComparison.Ordinal), parked);
            Assert.IsTrue(parked.Contains("chatcmd:///say .merc call", StringComparison.Ordinal), parked);
            Assert.IsFalse(parked.Contains("chatcmd:///tell", StringComparison.Ordinal), parked);
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
        public void MainHeader_MacrosIsSayCommand()
        {
            string header = MercMenuService.Render.MainHeader("twink");
            Assert.IsTrue(header.Contains("chatcmd:///say .merc macros", StringComparison.Ordinal));
            Assert.IsFalse(header.Contains("chatcmd:///tell", StringComparison.Ordinal));
            Assert.IsFalse(header.Contains("href=\"text://", StringComparison.Ordinal));
        }
    }
}
