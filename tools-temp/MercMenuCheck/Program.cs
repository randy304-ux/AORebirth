using System.Text.RegularExpressions;
using ZoneEngine_New.Core.Pets;

static void Fail(string msg)
{
    Console.WriteLine("FAIL: " + msg);
    Environment.Exit(1);
}

string outer = MercMenuService.BuildOuter("<font color=#D3BFFF>\n<font color=#DEDE42>Mercs</font>\n");
if (Regex.Matches(outer, "href=\"text://").Count != 1)
    Fail("outer text:// count");
if (outer.Contains("href='text://", StringComparison.Ordinal)
    || outer.Contains("href=&#39;text://", StringComparison.Ordinal))
    Fail("must not nest text://");
if (outer.Contains("&lt;font", StringComparison.Ordinal))
    Fail("outer must not HTML-escape tags");

string main = MercMenuService.Render.FlatMenuSample();
if (Regex.Matches(main, "href=\"text://").Count != 1)
    Fail("flat menu must have exactly one outer text:// count="
        + Regex.Matches(main, "href=\"text://").Count);
if (main.Contains("href='text://", StringComparison.Ordinal)
    || main.Contains("href=&#39;text://", StringComparison.Ordinal)
    || main.Contains("NestedLink", StringComparison.Ordinal))
    Fail("flat menu must not nest text://");
if (!main.Contains("href='chatcmd:///say .merc dismiss soldier'", StringComparison.Ordinal))
    Fail("flat menu missing buffbot-style single-quoted chatcmd");
if (!main.Contains("href='chatcmd:///say .merc cast 1001'", StringComparison.Ordinal))
    Fail("flat menu missing cast chatcmd");
if (!main.Contains("href='chatcmd:///say .merc cast 2001'", StringComparison.Ordinal))
    Fail("flat menu missing MP pick-one cast");
if (!main.Contains("href='chatcmd:///say .merc cast 3001'", StringComparison.Ordinal))
    Fail("flat menu missing Trader wrangle cast");
if (main.Contains("href=&#39;chatcmd", StringComparison.Ordinal))
    Fail("must use raw single-quoted chatcmd like buffbot, not &#39;");
if (main.Contains("chatcmd:///tell", StringComparison.Ordinal))
    Fail("main has tell");
if (main.Contains("chatcmd:///say .merc filter", StringComparison.Ordinal))
    Fail("filter links dropped from flat window");
if (main.Length > MercMenuService.MessageMax)
    Fail("main over MessageMax");

string guest = MercMenuService.Render.GuestBuffsSample("Brock");
if (Regex.Matches(guest, "href=\"text://").Count != 1)
    Fail("guest must be one flat text://");
if (guest.Contains("href='text://", StringComparison.Ordinal))
    Fail("guest must not nest");
if (!guest.Contains("href='chatcmd:///say .merc cast 1001'", StringComparison.Ordinal))
    Fail("guest missing cast link");

Console.WriteLine(
    "PASS MercMenuCheck flat-buffbot-style bytes=" + main.Length
    + " max=" + MercMenuService.MessageMax);
