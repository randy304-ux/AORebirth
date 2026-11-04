namespace ZoneEngine_New.Tests
{
    using System;
    using System.Text.RegularExpressions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Pets;

    /// <summary>
    /// Flat merc menu: one outer <c>href="text://"</c>, buffbot-style
    /// <c>href='chatcmd:///say .merc …'</c>, no nested text://.
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
        public void FlatMenu_OneOuterText_BuffbotChatcmd_NoNestedText()
        {
            string message = MercMenuService.Render.FlatMenuSample();

            Assert.AreEqual(1, Regex.Matches(message, "href=\"text://").Count, message);
            Assert.AreEqual(0, Regex.Matches(message, "href='text://").Count, message);
            Assert.IsFalse(message.Contains("href=&#39;text://", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains("href='chatcmd:///say .merc dismiss soldier'", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains("href='chatcmd:///say .merc cast 1001'", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains("href='chatcmd:///say .merc cast 2001'", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Contains("href='chatcmd:///say .merc cast 3001'", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("href=&#39;chatcmd", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("chatcmd:///tell", StringComparison.Ordinal), message);
            Assert.IsFalse(message.Contains("chatcmd:///say .merc filter", StringComparison.Ordinal), message);
            Assert.IsTrue(message.Length <= MercMenuService.MessageMax);
            Assert.IsTrue(MercMenuService.IsAsciiOnly(message));
        }

        [TestMethod]
        public void GuestBuffs_FlatSingleWindow()
        {
            string guest = MercMenuService.Render.GuestBuffsSample("Brock");
            Assert.AreEqual(1, Regex.Matches(guest, "href=\"text://").Count);
            Assert.AreEqual(0, Regex.Matches(guest, "href='text://").Count);
            Assert.IsTrue(guest.Contains("href='chatcmd:///say .merc cast 1001'", StringComparison.Ordinal), guest);
        }

        [TestMethod]
        public void HiredContract_ParkDismiss_SampleHasSummon()
        {
            string parked = MercMenuService.Render.HiredSectionSample(present: false);
            Assert.IsTrue(parked.Contains("href='chatcmd:///say .merc call'", StringComparison.Ordinal), parked);
            Assert.IsFalse(parked.Contains("href='text://", StringComparison.Ordinal), parked);
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
