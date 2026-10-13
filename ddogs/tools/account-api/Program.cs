using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using MySqlConnector;

// Dungeon Dogs AO account service. Config via env / secrets files; never logs passwords.
var builtDir = Environment.GetEnvironmentVariable("AO_BUILT_DIR") ?? @"I:\AO\src\AORebirth\Built\Debug";
var secrets = @"I:\AO\secrets";
var logDir = @"I:\AO\logs";
var port = int.Parse(Environment.GetEnvironmentVariable("AO_ACCOUNT_API_PORT") ?? "8098");
var connStr = File.ReadAllText(Path.Combine(secrets, "account-api-db.txt")).Trim();
string Invite() => File.ReadAllText(Path.Combine(secrets, "signup-invite.txt")).Trim();
const int NewAllowedChars = 50, NewExpansions = 2047;

AssemblyLoadContext.Default.Resolving += (ctx, n) => { var p = Path.Combine(builtDir, n.Name + ".dll"); return File.Exists(p) ? ctx.LoadFromAssemblyPath(p) : null; };
var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(builtDir, "AORebirth.Core.dll"));
var ph = core.GetType("AORebirth.Core.Encryption.PasswordHash", true);
var mCreate = ph.GetMethod("CreateHash", BindingFlags.Public | BindingFlags.Static);
var mValidate = ph.GetMethod("ValidatePassword", BindingFlags.Public | BindingFlags.Static);
string Hash(string pw) => (string)mCreate.Invoke(null, new object[] { pw });
bool Check(string pw, string h) { try { return (bool)mValidate.Invoke(null, new object[] { pw, h }); } catch { return false; } }

var logLock = new object();
void Log(string msg) { lock (logLock) File.AppendAllText(Path.Combine(logDir, "account-api.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}"); }

var hits = new ConcurrentDictionary<string, List<DateTime>>();
bool Limited(string key, int max, TimeSpan win) {
  var l = hits.GetOrAdd(key, _ => new List<DateTime>());
  lock (l) { var now = DateTime.UtcNow; l.RemoveAll(t => now - t > win); if (l.Count >= max) return true; l.Add(now); return false; }
}
string ClientIp(HttpContext c) {
  var ip = c.Connection.RemoteIpAddress;
  // Site server (via Docker loopback relay) may pass the real browser IP.
  if (ip != null && System.Net.IPAddress.IsLoopback(ip) && c.Request.Headers.TryGetValue("X-Client-IP", out var f) && f.ToString().Length is > 0 and < 64) return f.ToString();
  return ip?.ToString() ?? "?";
}
var userRx = new Regex("^[A-Za-z][A-Za-z0-9]{2,15}$");
var mailRx = new Regex(@"^[^@\s]{1,40}@[^@\s]{1,40}\.[^@\s]{2,10}$");
string PwErr(string p) => p == null || p.Length < 8 || p.Length > 64 ? "password must be 8-64 characters" : null;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.Logging.ClearProviders();
var app = builder.Build();

app.MapGet("/health", () => Results.Json(new { ok = true }));

app.MapPost("/signup", async (HttpContext c) => {
  var ip = ClientIp(c);
  if (Limited("su:" + ip, 5, TimeSpan.FromHours(1)) || Limited("su:all", 30, TimeSpan.FromHours(1))) { Log($"signup ratelimited ip={ip}"); return Results.Json(new { ok = false, error = "too many requests" }, statusCode: 429); }
  SignupReq r; try { r = await c.Request.ReadFromJsonAsync<SignupReq>(); } catch { return Results.Json(new { ok = false, error = "bad json" }, statusCode: 400); }
  if (r == null || r.invite == null || !CryptoEq(r.invite.Trim(), Invite())) { Log($"signup badinvite ip={ip}"); return Results.Json(new { ok = false, error = "invalid invite code" }, statusCode: 403); }
  if (r.username == null || !userRx.IsMatch(r.username)) return Results.Json(new { ok = false, error = "username must be 3-16 letters/digits, starting with a letter" }, statusCode: 400);
  var pe = PwErr(r.password); if (pe != null) return Results.Json(new { ok = false, error = pe }, statusCode: 400);
  var email = (r.email ?? "").Trim(); if (email.Length > 0 && !mailRx.IsMatch(email)) return Results.Json(new { ok = false, error = "invalid email" }, statusCode: 400);
  var user = r.username.ToLowerInvariant();
  try {
    await using var db = new MySqlConnection(connStr); await db.OpenAsync();
    await using var cmd = new MySqlCommand("INSERT INTO login (CreationDate,Email,FirstName,LastName,Username,Password,AllowedCharacters,Flags,AccountFlags,Expansions,GM) VALUES (NOW(),@e,'','',@u,@p,@a,0,0,@x,0)", db);
    cmd.Parameters.AddWithValue("@e", email); cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@p", Hash(r.password));
    cmd.Parameters.AddWithValue("@a", NewAllowedChars); cmd.Parameters.AddWithValue("@x", NewExpansions);
    await cmd.ExecuteNonQueryAsync();
  } catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry) { Log($"signup duplicate user={user} ip={ip}"); return Results.Json(new { ok = false, error = "username taken" }, statusCode: 409); }
  catch (Exception ex) { Log($"signup error user={user} {ex.GetType().Name}: {ex.Message}"); return Results.Json(new { ok = false, error = "server error" }, statusCode: 500); }
  Log($"signup ok user={user} ip={ip}");
  return Results.Json(new { ok = true, username = user });
});

app.MapPost("/change-password", async (HttpContext c) => {
  var ip = ClientIp(c);
  ChangeReq r; try { r = await c.Request.ReadFromJsonAsync<ChangeReq>(); } catch { return Results.Json(new { ok = false, error = "bad json" }, statusCode: 400); }
  if (r == null || r.username == null || !userRx.IsMatch(r.username) || r.oldPassword == null) return Results.Json(new { ok = false, error = "bad request" }, statusCode: 400);
  var user = r.username.ToLowerInvariant();
  if (Limited("cp:" + ip, 10, TimeSpan.FromHours(1)) || Limited("cpu:" + user, 5, TimeSpan.FromHours(1))) { Log($"chpw ratelimited user={user} ip={ip}"); return Results.Json(new { ok = false, error = "too many requests" }, statusCode: 429); }
  var pe = PwErr(r.newPassword); if (pe != null) return Results.Json(new { ok = false, error = pe }, statusCode: 400);
  try {
    await using var db = new MySqlConnection(connStr); await db.OpenAsync();
    string hash;
    await using (var s = new MySqlCommand("SELECT Password FROM login WHERE Username=@u", db)) { s.Parameters.AddWithValue("@u", user); hash = (string)await s.ExecuteScalarAsync(); }
    if (hash == null || !Check(r.oldPassword, hash)) { Log($"chpw denied user={user} ip={ip}"); return Results.Json(new { ok = false, error = "wrong username or password" }, statusCode: 403); }
    await using (var u = new MySqlCommand("UPDATE login SET Password=@p WHERE Username=@u AND Password=@old", db)) {
      u.Parameters.AddWithValue("@p", Hash(r.newPassword)); u.Parameters.AddWithValue("@u", user); u.Parameters.AddWithValue("@old", hash);
      if (await u.ExecuteNonQueryAsync() != 1) return Results.Json(new { ok = false, error = "conflict, retry" }, statusCode: 409);
    }
  } catch (Exception ex) { Log($"chpw error user={user} {ex.GetType().Name}: {ex.Message}"); return Results.Json(new { ok = false, error = "server error" }, statusCode: 500); }
  Log($"chpw ok user={user} ip={ip}");
  return Results.Json(new { ok = true });
});

app.MapGet("/players", async () => {
  try {
    await using var db = new MySqlConnection(connStr); await db.OpenAsync();
    await using var c = new MySqlCommand("SELECT COUNT(*) FROM characters WHERE Online=1", db);
    return Results.Json(new { online = Convert.ToInt32(await c.ExecuteScalarAsync()) });
  } catch (Exception ex) { Log($"players error {ex.GetType().Name}: {ex.Message}"); return Results.Json(new { online = (int?)null, error = "unavailable" }, statusCode: 503); }
});
DDogsStore.Map(app, connStr, Log, Limited);
Log($"start port={port} pid={Environment.ProcessId}");
app.Run();

static bool CryptoEq(string a, string b) => System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(a), System.Text.Encoding.UTF8.GetBytes(b));
record SignupReq(string username, string password, string email, string invite);
record ChangeReq(string username, string oldPassword, string newPassword);
