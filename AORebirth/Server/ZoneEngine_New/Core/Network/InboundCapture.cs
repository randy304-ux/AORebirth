namespace ZoneEngine_New.Core.Network
{
    using System;
    using System.Globalization;
    using System.IO;

    /// <summary>
    /// Opt-in capture of every inbound zone packet (type, N3 id, full hex) to I:\AO\logs\zone-inbound-capture.log.
    /// On at startup; toggled at runtime with <c>.capture on|off</c>; unknown messages are always captured.
    /// </summary>
    public static class InboundCapture
    {
        public const string DefaultPath = @"I:\AO\logs\zone-inbound-capture.log";
        static readonly object Sync = new();
        public static volatile bool Enabled = true;
        public static string Path = DefaultPath;

        public static void Write(byte[] packet, object? n3Id, object? body)
        {
            if (!Enabled && body != null)
                return;
            try
            {
                string type = body?.GetType().Name ?? "UNKNOWN";
                string line = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss.fff} type={1} n3id={2} bytes={3} hex={4}{5}",
                    DateTime.Now, type, n3Id, packet.Length, Convert.ToHexString(packet), Environment.NewLine);
                lock (Sync)
                    File.AppendAllText(Path, line);
            }
            catch (Exception)
            {
                // Capture must never break the session.
            }
        }
    }
}

