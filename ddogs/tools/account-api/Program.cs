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
// One invite code per non-empty line of signup-invite.txt (re-read on every request).
string[] Invites() => File.ReadAllLines(Path.Combine(secrets, "signup-invite.txt")).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
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
// The scheduled task starts us through cmd.exe. Stop-ScheduledTask ends only that cmd, and a task-started (S4U, session 0)
// process cannot be killed from another logon session, so follow the parent: when the task's cmd goes away, exit.
// That keeps "stop the task, start the task" a real restart for the watchdog. Runs on its own thread so a hung web host can't block it.
ParentWatch.Start(Log);

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
// The in-game Store/Info window dials the login server on port 80 (uwg.store.icc-rk / aoshop.funcom.com).
// Nothing was listening there, so LAN and remote both got "connection refused". 8098 stays the account port.
var listen80 = false;
if (port != 80)
{
  var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, 80);
  try { probe.Start(); listen80 = true; }
  catch (Exception ex) { Log($"store port80 unavailable {ex.GetType().Name}: {ex.Message}"); }
  finally { try { probe.Stop(); } catch { } }
}
builder.WebHost.ConfigureKestrel(options =>
{
  options.ListenAnyIP(port);
  if (listen80) options.ListenAnyIP(80);
});
builder.Logging.ClearProviders();
var app = builder.Build();

app.MapGet("/health", () => Results.Json(new { ok = true }));

app.MapPost("/signup", async (HttpContext c) => {
  var ip = ClientIp(c);
  if (Limited("su:" + ip, 5, TimeSpan.FromHours(1)) || Limited("su:all", 30, TimeSpan.FromHours(1))) { Log($"signup ratelimited ip={ip}"); return Results.Json(new { ok = false, error = "too many requests" }, statusCode: 429); }
  SignupReq r; try { r = await c.Request.ReadFromJsonAsync<SignupReq>(); } catch { return Results.Json(new { ok = false, error = "bad json" }, statusCode: 400); }
  if (r == null || r.invite == null || !Invites().Any(code => CryptoEq(r.invite.Trim(), code))) { Log($"signup badinvite ip={ip}"); return Results.Json(new { ok = false, error = "invalid invite code" }, statusCode: 403); }
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

app.MapGet("/reset-password", () => Results.Content(ResetPage(null), "text/html; charset=utf-8"));
app.MapPost("/reset-password", async (HttpContext c) => {
  var ip = ClientIp(c);
  if (Limited("rp:" + ip, 10, TimeSpan.FromHours(1))) { Log($"reset ratelimited ip={ip}"); return Results.Content(ResetPage("Too many tries. Wait and try again."), "text/html; charset=utf-8", statusCode: 429); }
  string user, email, password, confirm;
  var isForm = c.Request.HasFormContentType;
  if (isForm)
  {
    var fields = await c.Request.ReadFormAsync();
    user = fields["username"].ToString();
    email = fields["email"].ToString();
    password = fields["password"].ToString();
    confirm = fields["confirm"].ToString();
  }
  else
  {
    ResetReq body; try { body = await c.Request.ReadFromJsonAsync<ResetReq>(); } catch { return Results.Json(new { ok = false, error = "bad json" }, statusCode: 400); }
    if (body == null) return Results.Json(new { ok = false, error = "bad json" }, statusCode: 400);
    user = body.username; email = body.email; password = body.password; confirm = body.confirm;
  }
  IResult Fail(string error, int status) => isForm
    ? Results.Content(ResetPage(error), "text/html; charset=utf-8", statusCode: status)
    : Results.Json(new { ok = false, error }, statusCode: status);
  if (user == null || !userRx.IsMatch(user)) return Fail("Username must be 3-16 letters and digits, starting with a letter.", 400);
  email = (email ?? "").Trim();
  if (!mailRx.IsMatch(email)) return Fail("Enter the email on the account.", 400);
  if (!string.Equals(password, confirm, StringComparison.Ordinal)) return Fail("The two passwords do not match.", 400);
  var pe = PwErr(password); if (pe != null) return Fail("Password must be 8-64 characters.", 400);
  user = user.ToLowerInvariant();
  if (Limited("rpu:" + user, 5, TimeSpan.FromHours(1))) { Log($"reset ratelimited user={user} ip={ip}"); return Fail("Too many tries. Wait and try again.", 429); }
  try
  {
    await using var db = new MySqlConnection(connStr); await db.OpenAsync();
    string storedEmail = null, hash = null;
    await using (var s = new MySqlCommand("SELECT Email, Password FROM login WHERE Username=@u", db))
    {
      s.Parameters.AddWithValue("@u", user);
      await using var row = await s.ExecuteReaderAsync();
      if (await row.ReadAsync())
      {
        storedEmail = row.IsDBNull(0) ? "" : row.GetString(0);
        hash = row.IsDBNull(1) ? "" : row.GetString(1);
      }
    }
    if (hash == null || !string.Equals(storedEmail?.Trim(), email, StringComparison.OrdinalIgnoreCase))
    {
      Log($"reset denied user={user} ip={ip}");
      return Fail("That username and email do not match.", 403);
    }
    await using (var u = new MySqlCommand("UPDATE login SET Password=@p WHERE Username=@u AND Password=@old", db))
    {
      u.Parameters.AddWithValue("@p", Hash(password));
      u.Parameters.AddWithValue("@u", user);
      u.Parameters.AddWithValue("@old", hash);
      if (await u.ExecuteNonQueryAsync() != 1) return Fail("Could not save the new password. Try again.", 409);
    }
  }
  catch (Exception ex) { Log($"reset error user={user} {ex.GetType().Name}: {ex.Message}"); return Fail("Server error.", 500); }
  Log($"reset ok user={user} ip={ip}");
  return isForm
    ? Results.Content(ResetPage(null, done: true), "text/html; charset=utf-8")
    : Results.Json(new { ok = true });
});

app.MapGet("/players", async () => {
  try {
    await using var db = new MySqlConnection(connStr); await db.OpenAsync();
    await using var c = new MySqlCommand("SELECT COUNT(*) FROM characters WHERE Online=1", db);
    return Results.Json(new { online = Convert.ToInt32(await c.ExecuteScalarAsync()) });
  } catch (Exception ex) { Log($"players error {ex.GetType().Name}: {ex.Message}"); return Results.Json(new { online = (int?)null, error = "unavailable" }, statusCode: 503); }
});
DDogsStore.Map(app, connStr, Log, Limited);
Log($"start port={port} also80={listen80} pid={Environment.ProcessId}");
app.Run();

static bool CryptoEq(string a, string b) => System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(a), System.Text.Encoding.UTF8.GetBytes(b));
static string ResetPage(string error, bool done = false)
{
  string note = done
    ? "<p>Password saved. Log in with the new password.</p>"
    : "<form method=\"post\" action=\"/reset-password\">"
      + "<label>Username<br><input name=\"username\" required></label><br><br>"
      + "<label>Email on the account<br><input name=\"email\" type=\"email\" required></label><br><br>"
      + "<label>New password<br><input name=\"password\" type=\"password\" required></label><br><br>"
      + "<label>New password again<br><input name=\"confirm\" type=\"password\" required></label><br><br>"
      + "<button type=\"submit\">Reset password</button></form>";
  string alert = string.IsNullOrEmpty(error) ? "" : "<p>" + System.Net.WebUtility.HtmlEncode(error) + "</p>";
  return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Reset password</title></head><body>"
    + "<h1>Reset password</h1>"
    + "<p>Enter the username and the email on the account. This does not change the game client.</p>"
    + alert + note + "</body></html>";
}
record SignupReq(string username, string password, string email, string invite);
record ChangeReq(string username, string oldPassword, string newPassword);
record ResetReq(string username, string email, string password, string confirm);

static class ParentWatch
{
  [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
  struct BasicInfo { public IntPtr ExitStatus, Peb, Affinity, BasePriority, UniqueProcessId, InheritedFromUniqueProcessId; }

  [System.Runtime.InteropServices.DllImport("ntdll.dll")]
  static extern int NtQueryInformationProcess(IntPtr process, int infoClass, ref BasicInfo info, int length, out int returned);

  public static void Start(Action<string> log)
  {
    System.Diagnostics.Process parent;
    try
    {
      var self = System.Diagnostics.Process.GetCurrentProcess();
      var info = new BasicInfo();
      if (NtQueryInformationProcess(self.Handle, 0, ref info, System.Runtime.InteropServices.Marshal.SizeOf<BasicInfo>(), out _) != 0) return;
      parent = System.Diagnostics.Process.GetProcessById(info.InheritedFromUniqueProcessId.ToInt32());
      // A reused pid would be younger than us; only follow the cmd.exe wrapper (a manual run from a shell is left alone).
      if (parent.StartTime > self.StartTime || !parent.ProcessName.Equals("cmd", StringComparison.OrdinalIgnoreCase)) return;
    }
    catch { return; }
    log($"parent watch: following cmd pid={parent.Id}");
    new Thread(() =>
    {
      try { parent.WaitForExit(); } catch { }
      log("parent cmd exited (task stopped); exiting");
      Environment.Exit(0);
    }) { IsBackground = true, Name = "parent-watch" }.Start();
  }
}
