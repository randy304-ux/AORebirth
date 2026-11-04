using System;
using System.Text.RegularExpressions;
using ZoneEngine_New.Core.Bots;

// Standalone render check (ZoneEngine_New.Tests has unrelated baseline compile failures).
string inner =
    "<font color=#D3BFFF>\n"
    + "<font color=#DEDE42>Buffbot - Fleet buffs</font>\n"
    + "<a href='chatcmd:///tell Buffbot Buffmacro'>Buffmacro</a> - creates a macro\n"
    + "\n    <img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n\n"
    + "    <img src=tdb://id:GFX_GUI_FRIENDLIST_STATUS_GREEN> <font color=#DEDE42>Generic</font>\n"
    + "    <a href='chatcmd:///tell Buffbot cast 288774'>Composite Attribute Boost</a>"
    + " <font color=#F07171>[+12 Attributes]</font> <font color=#00BDBD>[NCU: 4]</font>\n";

string message = BuffbotService.BuildOuter(inner);
int textCount = Regex.Matches(message, "text://").Count;
bool hasLt = message.Contains("&lt;", StringComparison.Ordinal);
bool nested = false;
int first = message.IndexOf("text://", StringComparison.Ordinal);
if (first >= 0)
    nested = message.Substring(first + "text://".Length).Contains("text://", StringComparison.Ordinal);

Console.WriteLine("len={0} text://={1} has&lt;={2} nested={3}", message.Length, textCount, hasLt, nested);
Console.WriteLine("esc_apos={0}", BuffbotService.EscText("Assassin's Grin"));
Console.WriteLine("MessageMax={0}", BuffbotService.MessageMax);

if (textCount != 1 || hasLt || nested || BuffbotService.MessageMax > 32000)
{
    Console.WriteLine("FAIL");
    return 1;
}

Console.WriteLine("PASS");
return 0;
