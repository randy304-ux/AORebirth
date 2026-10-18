using System.Security.Cryptography;
namespace DDogsLauncher;
static class Program
{
    static string Sha(string f) { using var s = File.OpenRead(f); return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant(); }
    static async Task<int> Main()
    {
        // Unmodified 0210 SelfUpdate/Updater/Models against a new-format server (version.json with filesManifest* fields).
        var fx = @"I:\AO\cache\temp\delta-fx";
        var srv = new MiniHttp { Root = Path.Combine(fx, "srv-2099.01.01-0002") };
        var b = AppContext.BaseDirectory;
        foreach (var x in new[] { "client-files", "client-patch", "update" }) if (Directory.Exists(Path.Combine(b, x))) Directory.Delete(Path.Combine(b, x), true);
        foreach (var x in new[] { "patch.json", "update.log", "DungeonDogsLauncher.exe", "assets" }) { var p = Path.Combine(b, x); if (File.Exists(p)) File.Delete(p); if (Directory.Exists(p)) Directory.Delete(p, true); }
        File.WriteAllText(Path.Combine(b, "release.json"), "{ \"version\": \"2026.10.18-0210\" }");
        var cfg = new LauncherConfig { SelfUpdateUrls = new() { $"http://127.0.0.1:{srv.Port}/version.json" } };
        await SelfUpdate.EnsurePatchAsync(cfg, null, CancellationToken.None);
        var src = Path.Combine(fx, "src-2099.01.01-0002", "patch");
        bool ok = Directory.GetFiles(src, "*", SearchOption.AllDirectories).All(f => { var t = Path.Combine(b, Path.GetRelativePath(src, f)); return File.Exists(t) && Sha(t) == Sha(f); });
        Console.WriteLine((ok ? "PASS" : "FAIL") + " 0210 code: patch zip downloaded, sha-verified, extracted (" + string.Join(",", srv.Log.Select(l => l.Path)) + ")");
        bool restart = await SelfUpdate.RunAsync(cfg, null, CancellationToken.None);
        var log = File.ReadAllText(Path.Combine(b, "update.log"));
        bool lok = restart && log.Contains("launcher zip sha256 ok") && File.Exists(Path.Combine(b, "update", "stage", "DungeonDogsLauncher.exe"));
        Console.WriteLine((lok ? "PASS" : "FAIL") + " 0210 code: launcher zip downloaded, sha ok, staged; swap script started");
        return ok && lok ? 0 : 1;
    }
}