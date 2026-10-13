namespace Utility.Config
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// Dungeon Dogs: picks the zone/chat address advertised to a client. Clients whose source
    /// address is loopback or inside LanSubnets get the LAN value (ZoneIP/ChatIP); everyone else
    /// gets ZoneIPPublic/ChatIPPublic when configured, otherwise the LAN value.
    /// </summary>
    public static class AdvertisedAddress
    {
        public const string DefaultLanSubnets = "192.168.1.0/24,127.0.0.0/8";

        public static string Zone(IPAddress remote)
        {
            Config c = ConfigReadWrite.Instance.CurrentConfig;
            return Select(c == null ? null : c.ZoneIP, c == null ? null : c.ZoneIPPublic, c == null ? null : c.LanSubnets, remote);
        }

        public static string Chat(IPAddress remote)
        {
            Config c = ConfigReadWrite.Instance.CurrentConfig;
            return Select(c == null ? null : c.ChatIP, c == null ? null : c.ChatIPPublic, c == null ? null : c.LanSubnets, remote);
        }

        /// <summary>Resolves a hostname to an IPv4 string at call time (IP literals pass through); falls back to the input.</summary>
        public static string ResolveIPv4(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return host;
            IPAddress parsed;
            if (IPAddress.TryParse(host, out parsed)) return host;
            try
            {
                foreach (IPAddress a in Dns.GetHostAddresses(host))
                    if (a.AddressFamily == AddressFamily.InterNetwork) return a.ToString();
            }
            catch (SocketException)
            {
            }

            return host;
        }

        public static string Select(string lanValue, string publicValue, string lanSubnets, IPAddress remote)
        {
            if (string.IsNullOrWhiteSpace(publicValue) || remote == null || IsLan(remote, lanSubnets))
            {
                return lanValue;
            }

            return publicValue.Trim();
        }

        public static bool IsLan(IPAddress remote, string lanSubnets)
        {
            if (remote == null) return true;
            if (remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
            if (IPAddress.IsLoopback(remote)) return true;
            if (remote.AddressFamily != AddressFamily.InterNetwork) return false;
            uint addr = ToUInt(remote);
            string list = string.IsNullOrWhiteSpace(lanSubnets) ? DefaultLanSubnets : lanSubnets;
            foreach (string raw in list.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = raw.Split('/');
                IPAddress net;
                int bits = 32;
                if (!IPAddress.TryParse(parts[0], out net) || net.AddressFamily != AddressFamily.InterNetwork) continue;
                if (parts.Length > 1 && (!int.TryParse(parts[1], out bits) || bits < 0 || bits > 32)) continue;
                uint mask = bits == 0 ? 0u : uint.MaxValue << (32 - bits);
                if ((addr & mask) == (ToUInt(net) & mask)) return true;
            }

            return false;
        }

        private static uint ToUInt(IPAddress ip)
        {
            byte[] b = ip.GetAddressBytes();
            return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        }
    }
}
