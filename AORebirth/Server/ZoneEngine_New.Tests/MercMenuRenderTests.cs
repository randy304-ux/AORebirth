namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Pets;

    /// <summary>
    /// Merc menu: one server-opened text:// window per tell; sub-pages via chatcmd (no nested text://).
    /// Dismiss parks hired contracts (ContractsStat bit stays).
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
        public void MainMenu_UsesChatcmdNav_NoNestedTextHref()
        {
            string message = MercMenuService.Render.MainWithChatcmdNavSample();

            Assert.AreEqual(1, Regex.Matches(message, "href=\"text://").Count, message);
            Assert.AreEqual(0, Regex.Matches(message, "href='text://").Count, message);
            Assert.IsTrue(message.Contains("chatcmd:///tell Merc hire soldier", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains("chatcmd:///tell Merc view enforcer_subway", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains("chatcmd:///tell Merc macros", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains("chatcmd:///tell Merc dismiss soldier", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains("Basic - hire", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("Twink - L24", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains("&lt;", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains("&#39;", StringComparison.Ordinal));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(message));
            Assert.IsTrue(message.Length <= MercMenuService.MessageMax);
        }

        [TestMethod]
        public void HiredContract_AfterParkDismiss_StillListedWithSummon()
        {
            string present = MercMenuService.Render.HiredContractRowSample(present: true);
            string parked = MercMenuService.Render.HiredContractRowSample(present: false);

            Assert.IsTrue(present.Contains("chatcmd:///tell Merc hire soldier", StringComparison.Ordinal), present);
            Assert.IsTrue(present.Contains("dismiss soldier", StringComparison.Ordinal), present);
            Assert.IsTrue(present.Contains("[Summoned]", StringComparison.Ordinal), present);

            Assert.IsTrue(parked.Contains("chatcmd:///tell Merc hire soldier", StringComparison.Ordinal), parked);
            Assert.IsTrue(parked.Contains(">Soldier</a>", StringComparison.Ordinal), parked);
            Assert.IsTrue(parked.Contains("chatcmd:///tell Merc call", StringComparison.Ordinal), parked);
            Assert.IsTrue(parked.Contains(">summon</a>", StringComparison.Ordinal), parked);
            Assert.IsFalse(parked.Contains("dismiss soldier", StringComparison.Ordinal), parked);
            Assert.IsFalse(parked.Contains("[Summoned]", StringComparison.Ordinal), parked);
        }

        [TestMethod]
        public void ParkDismiss_KeepsContractBit_EndDismissClearsIt()
        {
            const int soldierBit = 1;
            int bits = 260 | (1 << soldierBit); // Blitzkreig-style + soldier
            Assert.AreEqual(262, bits);
            Assert.AreEqual(262, PetService.ContractBitsAfterDismiss(bits, soldierBit, endContract: false));
            Assert.AreEqual(260, PetService.ContractBitsAfterDismiss(bits, soldierBit, endContract: true));
        }

        [TestMethod]
        public void HireDetail_WhenParked_ShowsSummonNotDismiss()
        {
            string parked = MercMenuService.Render.HireDetailInnerSample(
                title: "Soldier",
                tier: "Basic",
                description: "Profession hire contract.",
                tierBlurb: "Basic hire.",
                present: false,
                buffs: new List<(int, string, int)> { (2001, "Grid Armor", 3) });
            Assert.IsTrue(parked.Contains("chatcmd:///tell Merc call", StringComparison.Ordinal), parked);
            Assert.IsTrue(parked.Contains(">Summon</a>", StringComparison.Ordinal), parked);
            Assert.IsFalse(parked.Contains("dismiss soldier", StringComparison.Ordinal), parked);
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
            Assert.IsTrue(MercMenuService.IsAsciiOnly(owner));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(guest));

            Assert.IsTrue(owner.Contains("chatcmd:///tell Merc dismiss", StringComparison.Ordinal));
            Assert.IsTrue(owner.Contains("chatcmd:///tell Merc preset keeper aura heal", StringComparison.Ordinal));
            Assert.IsTrue(owner.Contains("chatcmd:///tell Merc cast 1001", StringComparison.Ordinal));
            Assert.IsTrue(guest.Contains("can buff you", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("chatcmd:///tell Merc summon", StringComparison.Ordinal));
            Assert.IsFalse(guest.Contains("Stance", StringComparison.Ordinal));
        }

        [TestMethod]
        public void MainHeader_MacrosIsChatcmd()
        {
            string header = MercMenuService.Render.MainHeader("twink");
            Assert.IsTrue(header.Contains("<font color=#DEDE42>[twink]</font>", StringComparison.Ordinal));
            Assert.IsTrue(header.Contains("chatcmd:///tell Merc macros", StringComparison.Ordinal));
            Assert.IsFalse(header.Contains("href=\"text://", StringComparison.Ordinal));
            Assert.IsFalse(header.Contains("href='text://", StringComparison.Ordinal));
            Assert.IsTrue(MercMenuService.IsAsciiOnly(header));
        }
    }
}
