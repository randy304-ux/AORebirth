using System.Net;
using System.Net.Sockets;
using System.Text;
namespace DDogsLauncher;

/// <summary>Tiny static HTTP/1.1 server on 127.0.0.1 for update tests: GET + Range, request log.</summary>
public sealed class MiniHttp : IDisposable
{
    readonly TcpListener _l = new(IPAddress.Loopback, 0);
    public string Root = "";
    public readonly List<(string Path, int Status, long Bytes)> Log = new();
    public int Port => ((IPEndPoint)_l.LocalEndpoint).Port;
    public MiniHttp() { _l.Start(); _ = Loop(); }
    async Task Loop()
    {
        while (true)
        {
            TcpClient c;
            try { c = await _l.AcceptTcpClientAsync(); } catch { return; }
            _ = Task.Run(() => Serve(c));
        }
    }
    async Task Serve(TcpClient c)
    {
        using (c)
        {
            var s = c.GetStream();
            var r = new StreamReader(s, Encoding.ASCII, false, 1024, true);
            var line = await r.ReadLineAsync() ?? "";
            long from = 0; string? h;
            while (!string.IsNullOrEmpty(h = await r.ReadLineAsync()))
                if (h.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)) from = long.Parse(h.Split('=')[1].TrimEnd('-'));
            var path = Uri.UnescapeDataString(line.Split(' ')[1].Split('?')[0]).TrimStart('/');
            var file = Path.Combine(Root, path.Replace('/', '\\'));
            if (!File.Exists(file))
            {
                var nf = Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                await s.WriteAsync(nf); lock (Log) Log.Add((path, 404, 0)); return;
            }
            var data = await File.ReadAllBytesAsync(file);
            if (from >= data.Length) from = 0;
            var status = from > 0 ? "206 Partial Content" : "200 OK";
            var head = $"HTTP/1.1 {status}\r\nContent-Length: {data.Length - from}\r\nAccept-Ranges: bytes\r\nConnection: close\r\n" +
                       (from > 0 ? $"Content-Range: bytes {from}-{data.Length - 1}/{data.Length}\r\n" : "") + "\r\n";
            await s.WriteAsync(Encoding.ASCII.GetBytes(head));
            await s.WriteAsync(data.AsMemory((int)from));
            lock (Log) Log.Add((path, from > 0 ? 206 : 200, data.Length - from));
        }
    }
    public void Dispose() => _l.Stop();
}