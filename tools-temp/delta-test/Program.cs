using System.Security.Cryptography;
using System.Text.Json;
namespace DDogsLauncher;

static class Program
{
    static string Fx = @"I:\AO\cache\temp\delta-fx";
    static int fails;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static string V(int n) => "2099.01.01-000" + n;
    static string Sha(string f) { using var s = File.OpenRead(f); return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant(); }

    static MiniHttp Srv = new();
    static LauncherConfig Cfg => new() { SelfUpdateUrls = new() { $"http://127.0.0.1:{Srv.Port}/version.json" } };

    static string NewBase(string name)
    {
        var b = Path.Combine(Fx, "base-" + name);
        if (Directory.Exists(b)) Directory.Delete(b, true);
        Directory.CreateDirectory(b);
        Json.BaseDirOverride = b + "\\";
        return b;
    }
    static void Serve(string srv) { Srv.Root = Path.Combine(Fx, "srv-" + srv); lock (Srv.Log) Srv.Log.Clear(); }
    static List<(string Path, int Status, long Bytes)> Reqs() { lock (Srv.Log) return Srv.Log.ToList(); }
    static List<string> Blobs() => Reqs().Where(r => r.Path.StartsWith("files/")).Select(r => r.Path).ToList();
    static long Bytes() => Reqs().Sum(r => r.Bytes);

    static bool TreeMatches(string root, string src)
    {
        foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var t = Path.Combine(root, Path.GetRelativePath(src, f));
            if (!File.Exists(t) || Sha(t) != Sha(f)) { Console.WriteLine("   differs: " + t); return false; }
        }
        return true;
    }
    static string Src(int v, string part) => Path.Combine(Fx, "src-" + V(v), part);
    static string BlobOf(int v, string rel) => "files/" + Sha(Path.Combine(Src(v, rel.StartsWith("client-") ? "patch" : "launcher"), rel.Replace('/', '\\'))) + ".bin";

    // The real launcher would exit and robocopy root over BaseDir; the hook does the same copy in-process.
    static string? swappedRoot;
    static void Swap(string root, string version)
    {
        swappedRoot = root;
        foreach (var f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, f);
            if (rel.StartsWith("update\\")) continue;
            var t = Path.Combine(Json.BaseDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(t)!);
            File.Copy(f, t, true);
        }
    }

    static async Task<int> Main(string[] args)
    {
        SelfUpdate.SwapHook = Swap;
        if (args.Length == 3 && args[0] == "live")
        {
            // live <versionUrl> <baseDir>: the real update logic against a public URL (scratch install only)
            Json.BaseDirOverride = args[2].TrimEnd('\\') + "\\"; swappedRoot = null;
            var cfg = new LauncherConfig { SelfUpdateUrls = new() { args[1] } };
            var t0 = DateTime.Now;
            bool rs = await SelfUpdate.RunAsync(cfg, null, CancellationToken.None);
            Console.WriteLine($"launcher: restart={rs} version={SelfUpdate.LocalVersion()} ({(DateTime.Now - t0).TotalSeconds:0}s)");
            if (swappedRoot != null) Console.WriteLine("staged: " + string.Join(",", Directory.GetFiles(swappedRoot, "*", SearchOption.AllDirectories).Select(x => Path.GetRelativePath(swappedRoot, x))));
            t0 = DateTime.Now;
            await SelfUpdate.EnsurePatchAsync(cfg, null, CancellationToken.None);
            Console.WriteLine($"patch done ({(DateTime.Now - t0).TotalSeconds:0}s)");
            return 0;
        }
        if (args.Length == 2)
        {
            // real <stageDir> <baseDir>: one launcher + patch check against a real staged release
            Srv.Root = args[0]; Json.BaseDirOverride = args[1].TrimEnd('\\') + "\\"; swappedRoot = null;
            var t0 = DateTime.Now;
            bool rs = await SelfUpdate.RunAsync(Cfg, null, CancellationToken.None);
            var l = Reqs(); lock (Srv.Log) Srv.Log.Clear();
            await SelfUpdate.EnsurePatchAsync(Cfg, null, CancellationToken.None);
            var p = Reqs();
            Console.WriteLine($"launcher: restart={rs} requests={string.Join(",", l.Select(r => r.Path.Length > 40 ? r.Path[..18] + ".." : r.Path))} bytes={l.Sum(r => r.Bytes)}");
            Console.WriteLine($"patch: blobs={p.Count(r => r.Path.StartsWith("files/"))} zip={p.Any(r => r.Path.EndsWith(".zip"))} bytes={p.Sum(r => r.Bytes)}  ({(DateTime.Now - t0).TotalSeconds:0}s)");
            if (swappedRoot != null) Console.WriteLine("staged: " + string.Join(",", Directory.GetFiles(swappedRoot, "*", SearchOption.AllDirectories).Select(x => Path.GetRelativePath(swappedRoot, x))));
            return 0;
        }
        var none = CancellationToken.None;
        const string mapA = "client-files/cd_image/textures/PlanetMap/TestMap/mapA.bin";
        const string mapB = "client-files/cd_image/textures/PlanetMap/TestMap/mapB.bin";

        Console.WriteLine("== 1 clean install (v1): everything per-file");
        var b = NewBase("clean"); Serve(V(1)); swappedRoot = null;
        bool restart = await SelfUpdate.RunAsync(Cfg, null, none);
        Check(restart && swappedRoot != null && swappedRoot.EndsWith("delta-stage"), "launcher staged per-file and swap requested");
        Check(TreeMatches(b, Src(1, "launcher")), "all launcher files installed with matching hashes");
        Check(!Reqs().Any(r => r.Path.EndsWith(".zip")), "no zip downloaded");
        long launcherBytes = Bytes();
        Serve(V(1));
        await SelfUpdate.EnsurePatchAsync(Cfg, null, none);
        Check(TreeMatches(b, Src(1, "patch")), "all patch files installed with matching hashes");
        Check(Blobs().Count == 5 && !Reqs().Any(r => r.Path.EndsWith(".zip")), "patch: 5 blobs, no zip (" + Blobs().Count + ")");
        Console.WriteLine($"   bytes: launcher {launcherBytes}, patch {Bytes()}");

        Console.WriteLine("== 2 nothing changed: no downloads");
        Serve(V(1));
        restart = await SelfUpdate.RunAsync(Cfg, null, none);
        await SelfUpdate.EnsurePatchAsync(Cfg, null, none);
        Check(!restart && Blobs().Count == 0, "no restart, no blobs (requests: " + string.Join(",", Reqs().Select(r => r.Path)) + ")");

        Console.WriteLine("== 3 update v1->v2: one patch file changed, launcher metadata only");
        Serve(V(2)); swappedRoot = null;
        restart = await SelfUpdate.RunAsync(Cfg, null, none);
        Check(!restart && swappedRoot == null, "launcher NOT restarted (program unchanged)");
        Check(SelfUpdate.LocalVersion() == V(2), "release.json refreshed in place to " + SelfUpdate.LocalVersion());
        var lb = Blobs();
        Check(lb.Count == 2 && lb.Contains(BlobOf(2, "release.json")) && lb.Contains(BlobOf(2, "manifest.json")), "launcher fetched only release.json + manifest.json (" + lb.Count + " blobs, " + Bytes() + " bytes)");
        Serve(V(2));
        await SelfUpdate.EnsurePatchAsync(Cfg, null, none);
        var pb = Blobs();
        Check(pb.Count == 1 && pb[0] == BlobOf(2, mapB), "patch fetched only mapB.bin (" + string.Join(",", pb) + ")");
        Check(TreeMatches(b, Src(2, "patch")), "patch tree matches v2");
        Console.WriteLine($"   bytes for the whole v1->v2 patch check: {Bytes()} (manifest + 1 file)");

        Console.WriteLine("== 4 corrupted local file is re-fetched");
        var aPath = Path.Combine(b, mapA.Replace('/', '\\'));
        var bytes = File.ReadAllBytes(aPath); bytes[12345] ^= 0x5A; File.WriteAllBytes(aPath, bytes);
        Serve(V(2));
        await SelfUpdate.EnsurePatchAsync(Cfg, null, none);
        Check(Blobs().SequenceEqual(new[] { BlobOf(2, mapA) }), "only mapA.bin re-fetched (" + string.Join(",", Blobs()) + ")");
        Check(Sha(aPath) == Sha(Path.Combine(Src(2, "patch"), mapA.Replace('/', '\\'))), "mapA.bin hash restored");
        File.Delete(Path.Combine(b, "client-patch", "aovk", "randy31.dll"));
        Serve(V(2));
        await SelfUpdate.EnsurePatchAsync(Cfg, null, none);
        Check(Blobs().Count == 1 && File.Exists(Path.Combine(b, "client-patch", "aovk", "randy31.dll")), "deleted file re-fetched");

        Console.WriteLine("== 5 update v2->v3: launcher exe changed");
        Serve(V(3)); swappedRoot = null;
        restart = await SelfUpdate.RunAsync(Cfg, null, none);
        var staged = swappedRoot == null ? new string[0] : Directory.GetFiles(swappedRoot, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(swappedRoot, f).Replace('\\', '/')).OrderBy(x => x).ToArray();
        Check(restart && staged.SequenceEqual(new[] { "DungeonDogsLauncher.exe", "manifest.json", "release.json" }), "restart with only the changed files staged: " + string.Join(",", staged));
        Check(TreeMatches(b, Src(3, "launcher")) && SelfUpdate.LocalVersion() == V(3), "launcher tree matches v3");
        Serve(V(3));
        await SelfUpdate.EnsurePatchAsync(Cfg, null, none);
        Check(Blobs().Count == 0, "patch unchanged v2->v3: nothing fetched");

        Console.WriteLine("== 6 fallback: version.json without manifest (as 0210 publishes) -> full zips");
        b = NewBase("nomani"); Serve("nomani"); swappedRoot = null;
        restart = await SelfUpdate.RunAsync(Cfg, null, none);
        Check(restart && Reqs().Any(r => r.Path == "DungeonDogs-AO-Launcher-" + V(2) + ".zip" && r.Status == 200), "launcher zip downloaded");
        Check(TreeMatches(b, Src(2, "launcher")), "launcher installed from zip");
        Serve("nomani");
        await SelfUpdate.EnsurePatchAsync(Cfg, null, none);
        Check(Reqs().Any(r => r.Path == "DungeonDogs-AO-Patch.zip") && TreeMatches(b, Src(2, "patch")), "patch installed from zip");

        Console.WriteLine("== 7 fallback: manifest 404 -> full zips");
        b = NewBase("mani404"); Serve("mani404");
        restart = await SelfUpdate.RunAsync(Cfg, null, none);
        Serve("mani404");
        await SelfUpdate.EnsurePatchAsync(Cfg, null, none);
        Check(restart && TreeMatches(b, Src(2, "launcher")) && TreeMatches(b, Src(2, "patch")) && Reqs().Any(r => r.Path == "DungeonDogs-AO-Patch.zip"), "installed from zips");

        Console.WriteLine("== 8 fallback: a served blob is corrupt -> retries, then full patch zip; nothing bad installed");
        b = NewBase("badblob"); Serve("badblob");
        await SelfUpdate.RunAsync(Cfg, null, none);
        Serve("badblob");
        await SelfUpdate.EnsurePatchAsync(Cfg, null, none);
        Check(Reqs().Count(r => r.Path == BlobOf(2, mapA)) == 4, "corrupt blob tried 4 times");
        Check(Reqs().Any(r => r.Path == "DungeonDogs-AO-Patch.zip") && TreeMatches(b, Src(2, "patch")), "fell back to patch zip; tree matches v2");
        Check(!Directory.GetFiles(b, "*.ddpart", SearchOption.AllDirectories).Any(), "no partial files left");

        Console.WriteLine("== 9 tampered manifest (hash mismatch) is rejected -> zip");
        b = NewBase("tamper"); var t = Path.Combine(Fx, "srv-tamper");
        if (Directory.Exists(t)) Directory.Delete(t, true);
        CopyDir(Path.Combine(Fx, "srv-" + V(2)), t);
        var mf = Path.Combine(t, "files-manifest-" + V(2) + ".json");
        File.WriteAllText(mf, File.ReadAllText(mf).Replace("client-files/cd_image", "client-files/../../evil"));
        Serve("tamper");
        await SelfUpdate.EnsurePatchAsync(Cfg, null, none);
        Check(Reqs().Any(r => r.Path == "DungeonDogs-AO-Patch.zip") && !Directory.Exists(Path.Combine(Fx, "evil")), "rejected; patch from zip");

        var log = File.ReadAllText(Path.Combine(Fx, "base-badblob", "update.log"));
        Console.WriteLine("   badblob update.log: " + string.Join(" | ", log.Split('\n').Where(l => l.Contains("per-file") || l.Contains("falling back")).Select(l => l.Trim()).Take(3)));
        Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
        return fails;
    }
    static void CopyDir(string s, string d)
    {
        foreach (var f in Directory.GetFiles(s, "*", SearchOption.AllDirectories))
        { var t = Path.Combine(d, Path.GetRelativePath(s, f)); Directory.CreateDirectory(Path.GetDirectoryName(t)!); File.Copy(f, t); }
    }
}