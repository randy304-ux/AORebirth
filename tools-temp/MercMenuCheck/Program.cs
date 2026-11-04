using System.Text.RegularExpressions;
using ZoneEngine_New.Core.Pets;

static void Fail(string msg)
{
    Console.WriteLine("FAIL: " + msg);
    Environment.Exit(1);
}

// --- Outer: double-quoted text://, raw tags (Nadybot/Tyrbot makeBlob) ---
string outer = MercMenuService.BuildOuter("<font color=#D3BFFF>\n<font color=#DEDE42>Mercs</font>\n");
if (Regex.Matches(outer, "href=\"text://").Count != 1)
    Fail("outer text:// count");
if (outer.Contains("&lt;font", StringComparison.Ordinal))
    Fail("outer must not HTML-escape tags");

// --- Nested: single-quoted text://, RAW body, no quotes in body (TSW FAQ + InfoGuide) ---
string nestedBody =
    "<font color=#D3BFFF>\n<a href=chatcmd:///say .merc cast 1>X</a>\n";
string nested = MercMenuService.NestedLink(nestedBody, "Soldier");
if (!nested.StartsWith("<a href='text://", StringComparison.Ordinal))
    Fail("nested must be single-quoted text://");
if (nested.StartsWith("<a href=\"text://", StringComparison.Ordinal))
    Fail("nested must NOT be double-quoted text://");
if (nested.Contains("&lt;", StringComparison.Ordinal) || nested.Contains("&gt;", StringComparison.Ordinal))
    Fail("nested must NOT HTML-escape tags (live client shows raw code)");
if (!nested.Contains("<font color=#D3BFFF>", StringComparison.Ordinal))
    Fail("nested body must keep raw font tags");
if (!nested.Contains("href=chatcmd:///say .merc cast 1>", StringComparison.Ordinal))
    Fail("nested chatcmd must be unquoted");
if (nested.Contains("href='chatcmd", StringComparison.Ordinal)
    || nested.Contains("href=\"chatcmd", StringComparison.Ordinal))
    Fail("nested chatcmd must not use quotes");

try
{
    MercMenuService.NestedLink("<a href='chatcmd:///say .merc cast 1'>X</a>", "Bad");
    Fail("NestedLink must reject raw single quotes in body");
}
catch (InvalidOperationException)
{
    // expected
}

// --- Main sample: one outer ", nested ' for detail/macros/filters ---
string main = MercMenuService.Render.MainWithNestedDetailSample();
if (Regex.Matches(main, "href=\"text://").Count != 1)
    Fail("main must have exactly one outer double-quoted text:// count="
        + Regex.Matches(main, "href=\"text://").Count);
if (Regex.Matches(main, "href='text://").Count < 3)
    Fail("main missing nested single-quoted text:// (detail/macros/filters)");

const string marker = "href=\"text://";
int start = main.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
int end = main.LastIndexOf("\">", StringComparison.Ordinal);
string outerBody = main.Substring(start, end - start);
if (outerBody.Contains("\"", StringComparison.Ordinal))
    Fail("outer text:// body contains raw double quote");
if (outerBody.Contains("href=\"text://", StringComparison.Ordinal))
    Fail("nested double-quoted text:// inside outer");
if (outerBody.Contains("&lt;font", StringComparison.Ordinal))
    Fail("outer body must not contain HTML-escaped nested tags");

if (main.Contains("chatcmd:///say .merc view", StringComparison.Ordinal)
    || main.Contains("chatcmd:///say .merc contract", StringComparison.Ordinal)
    || main.Contains("chatcmd:///say .merc macros", StringComparison.Ordinal)
    || main.Contains("chatcmd:///say .merc filter", StringComparison.Ordinal))
    Fail("main still uses say-nav for windows/filters");
if (!main.Contains("chatcmd:///say .merc dismiss soldier", StringComparison.Ordinal))
    Fail("main missing dismiss say");
if (main.Contains("chatcmd:///tell", StringComparison.Ordinal))
    Fail("main has tell");
if (main.Length > MercMenuService.MessageMax)
    Fail("main over MessageMax");

// Filter sample NestedLinks present
string header = MercMenuService.Render.MainHeader("twink");
if (!header.Contains("href='text://", StringComparison.Ordinal))
    Fail("header missing nested text://");
if (header.Contains("chatcmd:///say .merc filter", StringComparison.Ordinal))
    Fail("header still uses filter say-commands");
if (!header.Contains(">basic</a>", StringComparison.Ordinal)
    && !header.Contains(">all</a>", StringComparison.Ordinal))
    Fail("header missing filter nested link labels");

Console.WriteLine("PASS MercMenuCheck raw-nested + filter NestedLink quote rule");
