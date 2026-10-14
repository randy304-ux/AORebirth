using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MySqlConnector;

// Dungeon Dogs in-game Info window (vgtp://uwg.store.icc-rk), Claim Center and Daily Reward pages.
// The client patch routes uwg.store.icc-rk / uwg.daily.icc-rk to this service. Content comes from
// editable JSON in the store folder (AO_DDOGS_STORE_DIR, default I:\AO\src\ddogs\store).
static class DDogsStore
{
  static string Dir => Environment.GetEnvironmentVariable("AO_DDOGS_STORE_DIR") ?? @"I:\AO\src\ddogs\store";
  static JsonNode Load(string name) { try { return JsonNode.Parse(File.ReadAllText(Path.Combine(Dir, name))); } catch { return null; } }
  static string H(string s) => WebUtility.HtmlEncode(s ?? "");
  static string S(JsonNode n, string k) => n?[k]?.GetValue<string>() ?? "";
  static int I(JsonNode n, string k) { try { return n?[k]?.GetValue<int>() ?? 0; } catch { return 0; } }

  static string Page(string title, string body)
  {
    var cfg = Load("store.json");
    var bg = S(cfg, "background").Trim();
    if (bg.Length > 0 && !bg.Contains("://") && !bg.StartsWith("/")) bg = "/store/img/" + Uri.EscapeDataString(bg);
    var bgCss = bg.Length > 0
      ? $"background:#071518 url('{H(bg)}') center center / cover no-repeat fixed;"
      : "background:#071518;";
    return $@"<!DOCTYPE html><html><head><meta charset=""utf-8""><meta http-equiv=""X-UA-Compatible"" content=""IE=edge""><title>{H(title)}</title>
<style>
html,body{{margin:0;padding:0;min-height:100%}}
body{{{bgCss}color:#cfe9e6;font-family:Verdana,Tahoma,Arial,sans-serif;font-size:13px}}
.shade{{min-height:100%;background:rgba(4,16,18,0.80);padding:14px 18px}}
h1{{margin:0 0 2px 0;color:#5fe0d6;font-size:22px;letter-spacing:1px}}
h2{{color:#5fe0d6;font-size:15px;margin:16px 0 6px 0;border-bottom:1px solid #1f5a57;padding-bottom:3px}}
.sub{{color:#7fa9a5;margin-bottom:12px}}
a{{color:#7ff2e8}} a:hover{{color:#ffffff}}
.card{{display:block;background:rgba(14,46,48,0.85);border:1px solid #2a7a74;padding:8px 10px;margin:6px 0;text-decoration:none}}
.card:hover{{background:rgba(24,74,76,0.95);border-color:#5fe0d6}}
.card b{{color:#bff8f2;font-size:14px}} .card span{{display:block;color:#8fbab5;margin-top:2px}}
.row{{background:rgba(14,46,48,0.85);border:1px solid #23605c;padding:5px 8px;margin:4px 0;overflow:hidden}}
.row form{{float:right;margin:0}}
button,input[type=submit]{{background:#16514d;color:#e6fffc;border:1px solid #5fe0d6;padding:3px 12px;cursor:pointer;font-family:inherit}}
input[type=text]{{background:#0a2224;color:#e6fffc;border:1px solid #2a7a74;padding:3px 6px;font-family:inherit;text-transform:uppercase}}
.ok{{color:#9cff9c}} .err{{color:#ff9c9c}} .done{{float:right;color:#6f8f8b}}
.nav{{margin-bottom:10px}}
</style></head><body><div class=""shade"">{body}</div></body></html>";
  }

  static IResult Html(string s) => Results.Content(s, "text/html; charset=utf-8");

  static string Hub()
  {
    var cfg = Load("store.json");
    var sb = new StringBuilder();
    sb.Append($"<h1>{H(S(cfg, "title") is var t && t.Length > 0 ? t : "Info")}</h1><div class=\"sub\">{H(S(cfg, "subtitle"))}</div>");
    sb.Append("<a class=\"card\" href=\"chatcmd:///say .info\"><b>Commands</b><span>Opens the command appendix in an info window. Click the Appendix link in chat, or type .info</span></a>");
    if (cfg?["links"] is JsonArray links)
      foreach (var l in links)
      {
        var url = S(l, "url");
        var external = url.Contains("://");
        sb.Append($"<a class=\"card\" href=\"{H(url)}\"{(external ? " target=\"_blank\"" : "")}><b>{H(S(l, "title"))}</b><span>{H(S(l, "description"))}</span></a>");
      }
    return Page("Info", sb.ToString());
  }

  record Who(int CharId, string Name);

  static async Task<Who> Lookup(string connStr, string code)
  {
    code = (code ?? "").Trim().ToUpperInvariant();
    if (code.Length < 6 || code.Length > 12) return null;
    await using var db = new MySqlConnection(connStr); await db.OpenAsync();
    await using var c = new MySqlCommand("SELECT k.CharId, ch.Name FROM ddogs_claim_codes k JOIN characters ch ON ch.Id=k.CharId WHERE k.Code=@c", db);
    c.Parameters.AddWithValue("@c", code);
    await using var r = await c.ExecuteReaderAsync();
    return await r.ReadAsync() ? new Who(r.GetInt32(0), r.GetString(1)) : null;
  }

  static async Task<HashSet<string>> Claimed(string connStr, int charId)
  {
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    await using var db = new MySqlConnection(connStr); await db.OpenAsync();
    await using var c = new MySqlCommand("SELECT ItemKey FROM ddogs_claims WHERE CharId=@id", db);
    c.Parameters.AddWithValue("@id", charId);
    await using var r = await c.ExecuteReaderAsync();
    while (await r.ReadAsync()) set.Add(r.GetString(0));
    return set;
  }

  static async Task<bool> Insert(string connStr, int charId, string key, int low, int high, int ql, string source)
  {
    await using var db = new MySqlConnection(connStr); await db.OpenAsync();
    await using var c = new MySqlCommand("INSERT IGNORE INTO ddogs_claims (CharId,ItemKey,LowId,HighId,Quality,Source,Status) VALUES (@c,@k,@l,@h,@q,@s,0)", db);
    c.Parameters.AddWithValue("@c", charId); c.Parameters.AddWithValue("@k", key); c.Parameters.AddWithValue("@l", low);
    c.Parameters.AddWithValue("@h", high > 0 ? high : low); c.Parameters.AddWithValue("@q", ql); c.Parameters.AddWithValue("@s", source);
    return await c.ExecuteNonQueryAsync() == 1;
  }

  const string CodeHelp = "Type <b>.claim</b> in the game chat to see your character's claim code, then enter it here. Items arrive in your inventory right away if you are online (or on your next login/zone).";

  static string CodeForm(string action, string code, string msg) =>
    $"<div class=\"nav\"><a href=\"/store\">&laquo; Back to Info</a></div>{msg}<p>{CodeHelp}</p><form method=\"get\" action=\"{action}\">Claim code: <input type=\"text\" name=\"code\" size=\"12\" value=\"{H(code)}\"> <input type=\"submit\" value=\"Continue\"></form>";

  static string Msg(string m, bool ok) => string.IsNullOrEmpty(m) ? "" : $"<p class=\"{(ok ? "ok" : "err")}\">{H(m)}</p>";

  static async Task<string> ClaimPage(string connStr, string code, string msg, bool ok)
  {
    var who = string.IsNullOrWhiteSpace(code) ? null : await Lookup(connStr, code);
    if (who == null)
      return Page("Claim Center", "<h1>Claim Center</h1>" + CodeForm("/claim", code, Msg(string.IsNullOrWhiteSpace(code) ? msg : "That claim code was not found.", false)));
    var claimed = await Claimed(connStr, who.CharId);
    var sb = new StringBuilder($"<h1>Claim Center</h1><div class=\"sub\">Claiming for <b>{H(who.Name)}</b>. Each item can be claimed once per character.</div><div class=\"nav\"><a href=\"/store\">&laquo; Back to Info</a> &nbsp; <a href=\"/daily?code={H(code)}\">Daily Reward</a></div>{Msg(msg, ok)}");
    var cat = Load("claims.json");
    string group = null;
    if (cat?["items"] is JsonArray items)
      foreach (var it in items)
      {
        var g = S(it, "group"); if (g != group) { group = g; sb.Append($"<h2>{H(g)}</h2>"); }
        var key = S(it, "key");
        sb.Append("<div class=\"row\">");
        if (claimed.Contains(key)) sb.Append("<span class=\"done\">Claimed</span>");
        else sb.Append($"<form method=\"post\" action=\"/claim\"><input type=\"hidden\" name=\"code\" value=\"{H(code)}\"><input type=\"hidden\" name=\"key\" value=\"{H(key)}\"><input type=\"submit\" value=\"Claim\"></form>");
        sb.Append($"{H(S(it, "name"))}</div>");
      }
    return Page("Claim Center", sb.ToString());
  }

  static JsonNode TodaysReward(out string dayKey)
  {
    var today = DateTime.Now.Date;
    dayKey = "daily-" + today.ToString("yyyy-MM-dd");
    if (Load("daily.json")?["rewards"] is JsonArray r && r.Count > 0) return r[today.DayOfYear % r.Count];
    return null;
  }

  static async Task<string> DailyPage(string connStr, string code, string msg, bool ok)
  {
    var who = string.IsNullOrWhiteSpace(code) ? null : await Lookup(connStr, code);
    if (who == null)
      return Page("Daily Reward", "<h1>Daily Reward</h1>" + CodeForm("/daily", code, Msg(string.IsNullOrWhiteSpace(code) ? msg : "That claim code was not found.", false)));
    var reward = TodaysReward(out var dayKey);
    var claimed = await Claimed(connStr, who.CharId);
    var sb = new StringBuilder($"<h1>Daily Reward</h1><div class=\"sub\">For <b>{H(who.Name)}</b> &middot; {DateTime.Now:dddd, MMM d}</div><div class=\"nav\"><a href=\"/store\">&laquo; Back to Info</a> &nbsp; <a href=\"/claim?code={H(code)}\">Claim Center</a></div>{Msg(msg, ok)}");
    if (reward == null) sb.Append("<p>No daily reward is set up today.</p>");
    else
    {
      sb.Append($"<div class=\"row\">");
      if (claimed.Contains(dayKey)) sb.Append("<span class=\"done\">Claimed today</span>");
      else sb.Append($"<form method=\"post\" action=\"/daily\"><input type=\"hidden\" name=\"code\" value=\"{H(code)}\"><input type=\"submit\" value=\"Claim\"></form>");
      sb.Append($"Today: {H(S(reward, "name"))}</div><p>Come back tomorrow for the next one.</p>");
    }
    return Page("Daily Reward", sb.ToString());
  }

  static bool IsDailyHost(HttpContext c) => c.Request.Host.Host.Contains("daily", StringComparison.OrdinalIgnoreCase);

  public static void Map(WebApplication app, string connStr, Action<string> log, Func<string, int, TimeSpan, bool> limited)
  {
    Func<HttpContext, Task<IResult>> Root = async c =>
      IsDailyHost(c) ? Html(await DailyPage(connStr, c.Request.Query["code"], null, false)) : Html(Hub());
    app.MapGet("/", Root);
    app.MapGet("/index.app", Root);
    app.MapGet("/store", () => Html(Hub()));
    app.MapGet("/store/img/{file}", (string file) => {
      var name = Path.GetFileName(file ?? "");
      var p = Path.Combine(Dir, "img", name);
      if (name.Length == 0 || !File.Exists(p)) return Results.NotFound();
      var ext = Path.GetExtension(p).ToLowerInvariant();
      var type = ext switch { ".png" => "image/png", ".gif" => "image/gif", ".webp" => "image/webp", _ => "image/jpeg" };
      return Results.File(p, type);
    });

    app.MapGet("/claim", async (HttpContext c) => {
      if (limited("cl:" + c.Connection.RemoteIpAddress, 120, TimeSpan.FromMinutes(10))) return Html(Page("Claim Center", "<p class=\"err\">Too many requests, try again in a few minutes.</p>"));
      return Html(await ClaimPage(connStr, c.Request.Query["code"], null, false));
    });
    app.MapGet("/daily", async (HttpContext c) => {
      if (limited("cl:" + c.Connection.RemoteIpAddress, 120, TimeSpan.FromMinutes(10))) return Html(Page("Daily Reward", "<p class=\"err\">Too many requests, try again in a few minutes.</p>"));
      return Html(await DailyPage(connStr, c.Request.Query["code"], null, false));
    });

    app.MapPost("/claim", async (HttpContext c) => {
      var f = await c.Request.ReadFormAsync();
      string code = f["code"], key = f["key"];
      if (limited("cl:" + c.Connection.RemoteIpAddress, 120, TimeSpan.FromMinutes(10))) return Html(Page("Claim Center", "<p class=\"err\">Too many requests, try again in a few minutes.</p>"));
      var who = await Lookup(connStr, code);
      if (who == null) return Html(await ClaimPage(connStr, code, null, false));
      JsonNode item = null;
      if (Load("claims.json")?["items"] is JsonArray items)
        item = items.FirstOrDefault(i => string.Equals(S(i, "key"), key, StringComparison.OrdinalIgnoreCase));
      if (item == null) return Html(await ClaimPage(connStr, code, "That item is not in the Claim Center list.", false));
      bool added = await Insert(connStr, who.CharId, S(item, "key"), I(item, "lowId"), I(item, "highId"), I(item, "ql"), "web");
      log($"claim char={who.CharId} key={S(item, "key")} added={added}");
      return Html(await ClaimPage(connStr, code, added ? $"{S(item, "name")} is on its way to {who.Name}." : "You already claimed that item.", added));
    });

    app.MapPost("/daily", async (HttpContext c) => {
      var f = await c.Request.ReadFormAsync();
      string code = f["code"];
      if (limited("cl:" + c.Connection.RemoteIpAddress, 120, TimeSpan.FromMinutes(10))) return Html(Page("Daily Reward", "<p class=\"err\">Too many requests, try again in a few minutes.</p>"));
      var who = await Lookup(connStr, code);
      if (who == null) return Html(await DailyPage(connStr, code, null, false));
      var reward = TodaysReward(out var dayKey);
      if (reward == null) return Html(await DailyPage(connStr, code, null, false));
      bool added = await Insert(connStr, who.CharId, dayKey, I(reward, "lowId"), I(reward, "highId"), I(reward, "ql"), "daily");
      log($"daily char={who.CharId} key={dayKey} added={added}");
      return Html(await DailyPage(connStr, code, added ? $"{S(reward, "name")} is on its way to {who.Name}." : "Already claimed today.", added));
    });
  }
}
