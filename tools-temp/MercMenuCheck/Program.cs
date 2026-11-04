using System.Text.RegularExpressions;
using ZoneEngine_New.Core.Pets;

static void Fail(string msg)
{
    Console.WriteLine("FAIL: " + msg);
    Environment.Exit(1);
}

// Outer: double-quoted text://, raw tags
string outer = MercMenuService.BuildOuter("<font color=#D3BFFF>\n<font color=#DEDE42>Mercs</font>\n");
if (Regex.Matches(outer, "href=\"text://").Count != 1)
    Fail("outer text:// count");
if (outer.Contains("&lt;font", StringComparison.Ordinal))
    Fail("outer must not HTML-escape tags");

// Nested: raw tags + production SayLink variant C (&#39;)
string nestedBody =
    "<font color=#D3BFFF>\n<a href=&#39;chatcmd:///say .merc cast 1&#39;>X</a>\n";
string nested = MercMenuService.NestedLink(nestedBody, "Soldier");
if (!nested.StartsWith("<a href='text://", StringComparison.Ordinal))
    Fail("nested must be single-quoted text://");
if (nested.Contains("&lt;", StringComparison.Ordinal))
    Fail("nested must NOT HTML-escape tags");
if (!nested.Contains("href=&#39;chatcmd:///say .merc cast 1&#39;>", StringComparison.Ordinal))
    Fail("nested must use production variant C");

try
{
    MercMenuService.NestedLink("<a href='chatcmd:///say .merc cast 1'>X</a>", "Bad");
    Fail("NestedLink must reject raw single quotes");
}
catch (InvalidOperationException)
{
    // expected
}

// NestedLinkDeep (filter → merc)
string deep = MercMenuService.NestedLinkDeep(nestedBody, "Deep");
if (!deep.StartsWith("<a href=&#39;text://", StringComparison.Ordinal))
    Fail("NestedLinkDeep must use &#39;text://");

// Main sample: production &#39; dismiss, nested filters, no say-nav windows
string main = MercMenuService.Render.MainWithNestedDetailSample();
if (Regex.Matches(main, "href=\"text://").Count != 1)
    Fail("main outer text:// count");
if (Regex.Matches(main, "href='text://").Count < 3)
    Fail("main missing nested single-quoted text://");
if (!main.Contains("href=&#39;chatcmd:///say .merc dismiss soldier&#39;", StringComparison.Ordinal))
    Fail("main missing production &#39; dismiss");
if (main.Contains("chatcmd:///say .merc filter", StringComparison.Ordinal)
    || main.Contains("chatcmd:///say .merc view", StringComparison.Ordinal)
    || main.Contains("chatcmd:///say .merc macros", StringComparison.Ordinal))
    Fail("main still uses say-nav for windows/filters");
if (main.Contains("&lt;font", StringComparison.Ordinal))
    Fail("main must not HTML-escape tags");
if (main.Length > MercMenuService.MessageMax)
    Fail("main over MessageMax");

const string marker = "href=\"text://";
int start = main.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
int end = main.LastIndexOf("\">", StringComparison.Ordinal);
string outerBody = main.Substring(start, end - start);
if (outerBody.Contains("\"", StringComparison.Ordinal))
    Fail("outer text:// body contains raw double quote");

// Filter header NestedLinkDeep
string header = MercMenuService.Render.MainHeader("twink");
if (!header.Contains("href=&#39;text://", StringComparison.Ordinal))
    Fail("header filter pages must NestedLinkDeep merc names");
if (header.Contains("chatcmd:///say .merc filter", StringComparison.Ordinal))
    Fail("header still uses filter say");

// Linktest: A, B-skip, C, D, E citation, nested-in-nested
string linktest = MercMenuService.Render.LinkTestSample();
string ltMsg = MercMenuService.BuildOuter(linktest, "Merc linktest");
if (Regex.Matches(ltMsg, "href=\"text://").Count != 1)
    Fail("linktest outer count");
if (!linktest.Contains(">Open variants</a>", StringComparison.Ordinal))
    Fail("linktest missing NestedLink Open variants");
if (!linktest.Contains("href=&quot;chatcmd:///say .merc menu&quot;", StringComparison.Ordinal))
    Fail("linktest missing variant A");
if (!linktest.Contains("NOT LIVE", StringComparison.Ordinal))
    Fail("linktest missing variant B skip note");
if (!linktest.Contains("href=&#39;chatcmd:///say .merc menu&#39;", StringComparison.Ordinal))
    Fail("linktest missing variant C");
if (!linktest.Contains("href=chatcmd:///say%20.merc%20menu>", StringComparison.Ordinal))
    Fail("linktest missing variant D");
if (!linktest.Contains("Nadybot", StringComparison.Ordinal)
    || !linktest.Contains("makeChatcmd", StringComparison.Ordinal))
    Fail("linktest missing variant E citation");
if (!linktest.Contains("href=&#39;text://", StringComparison.Ordinal))
    Fail("linktest missing nested-in-nested NestedLinkDeep");
if (linktest.Contains("\"", StringComparison.Ordinal))
    Fail("linktest contains raw double quote (would break outer)");

Console.WriteLine("PASS MercMenuCheck linktest A/C/D/E + NestedLinkDeep + production &#39;");
