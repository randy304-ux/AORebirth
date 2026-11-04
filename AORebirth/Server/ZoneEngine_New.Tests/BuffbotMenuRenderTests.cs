namespace ZoneEngine_New.Tests
{
    using System;
    using System.Text.RegularExpressions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Bots;

    /// <summary>
    /// Live AsyncBuffs menu invariants: one text:// blob, raw markup (no &amp;lt; tags), tell cast links.
    /// </summary>
    [TestClass]
    public sealed class BuffbotMenuRenderTests
    {
        [TestMethod]
        public void LiveStyleOuter_HasSingleTextHref_AndRawMarkup()
        {
            string inner =
                "<font color=#D3BFFF>\n"
                + "<font color=#DEDE42>Buffbot - Fleet buffs</font>\n"
                + "<a href='chatcmd:///tell Buffbot Buffmacro'>Buffmacro</a> - creates a macro\n"
                + "\n    <img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n\n"
                + "    <img src=tdb://id:GFX_GUI_FRIENDLIST_STATUS_GREEN> <font color=#DEDE42>Generic</font>\n"
                + "    <a href='chatcmd:///tell Buffbot cast 288774'>Composite Attribute Boost</a>"
                + " <font color=#F07171>[+12 Attributes]</font> <font color=#00BDBD>[NCU: 4]</font>\n";

            string message = BuffbotService.BuildOuter(inner);

            Assert.AreEqual(1, Regex.Matches(message, "text://").Count);
            Assert.IsFalse(message.Contains("&lt;", StringComparison.Ordinal));
            Assert.IsFalse(message.Contains("&#39;chatcmd", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("<font color=#DEDE42>Generic</font>", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains("chatcmd:///tell Buffbot cast 288774", StringComparison.Ordinal));
            Assert.IsTrue(message.Contains(">Buff Menu</a>", StringComparison.Ordinal));

            int first = message.IndexOf("text://", StringComparison.Ordinal);
            string after = message.Substring(first + "text://".Length);
            Assert.IsFalse(after.Contains("text://", StringComparison.Ordinal));
        }

        [TestMethod]
        public void EscText_EscapesAmpAndApos_NotUsedOnTags()
        {
            Assert.AreEqual("Assassin&#39;s Grin", BuffbotService.EscText("Assassin's Grin"));
            Assert.AreEqual("B&amp;E", BuffbotService.EscText("B&E"));
            Assert.IsTrue(BuffbotService.BuildOuter("<font color=#DEDE42>X</font>\n").Contains("<font", StringComparison.Ordinal));
        }

        [TestMethod]
        public void MessageMax_IsUnderInt16()
        {
            Assert.IsTrue(BuffbotService.MessageMax <= 32000);
            Assert.IsTrue(BuffbotService.MessageMax > 0);
        }
    }
}
