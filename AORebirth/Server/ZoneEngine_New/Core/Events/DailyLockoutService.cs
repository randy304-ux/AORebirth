namespace ZoneEngine_New.Core.Events
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Per-track daily lock-outs (18 hours). Persists under the engine log dir as DailyLockouts.json.
    /// Battle Station PvP daily allows <see cref="BattlestationMaxPerWindow"/> completes inside one window.
    /// </summary>
    public sealed class DailyLockoutService
    {
        public const string TrackAgency = "FreelancersInc";
        public const string TrackElite = "FreelancersIncElite";
        public const string TrackAlien = "FreelancersIncAlien";
        public const string TrackDoja = "Doja";
        public const string TrackDojaDarkRuins = "DojaDarkRuins";
        public const string TrackPvP = "PvPDaily";
        public const string TrackPvPBattlestation = "PvPBattlestation";

        public const int Hours = 18;
        public const int BattlestationMaxPerWindow = 3;

        public static DailyLockoutService? Instance { get; private set; }

        readonly IZoneLogger _logger;
        readonly object _gate = new();
        readonly string _path;
        Dictionary<string, TrackState> _byKey = new(StringComparer.Ordinal);

        public DailyLockoutService(IZoneLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            string root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "logs", "engines");
            try
            {
                root = Path.GetFullPath(root);
            }
            catch
            {
                root = Path.Combine(Path.GetTempPath(), "aorebirth-dailies");
            }

            Directory.CreateDirectory(root);
            _path = Path.Combine(root, "DailyLockouts.json");
            Load();
            Instance = this;
        }

        public bool TryBegin(Player player, string track, out string message, int maxPerWindow = 1)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (string.IsNullOrWhiteSpace(track))
            {
                message = "Unknown daily track.";
                return false;
            }

            DateTime now = DateTime.UtcNow;
            string key = Key(player.Identity.Instance, track);
            lock (_gate)
            {
                if (_byKey.TryGetValue(key, out TrackState? state) && state.UnlockUtc > now)
                {
                    if (maxPerWindow > 1 && state.CompletesInWindow < maxPerWindow)
                    {
                        message = string.Empty;
                        return true;
                    }

                    TimeSpan left = state.UnlockUtc - now;
                    message = track + " lock-out: "
                        + ((int)left.TotalHours).ToString(CultureInfo.InvariantCulture) + "h "
                        + left.Minutes.ToString(CultureInfo.InvariantCulture) + "m remaining.";
                    return false;
                }

                message = string.Empty;
                return true;
            }
        }

        public void RecordComplete(Player player, string track, int maxPerWindow = 1)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (string.IsNullOrWhiteSpace(track))
                return;

            DateTime now = DateTime.UtcNow;
            string key = Key(player.Identity.Instance, track);
            lock (_gate)
            {
                if (!_byKey.TryGetValue(key, out TrackState? state) || state.UnlockUtc <= now)
                {
                    state = new TrackState
                    {
                        UnlockUtc = now.AddHours(Hours),
                        CompletesInWindow = 1
                    };
                }
                else
                {
                    state.CompletesInWindow++;
                    if (state.CompletesInWindow >= maxPerWindow)
                        state.UnlockUtc = now.AddHours(Hours);
                }

                _byKey[key] = state;
                Save();
            }
        }

        static string Key(int characterId, string track)
            => characterId.ToString(CultureInfo.InvariantCulture) + ":" + track;

        void Load()
        {
            if (!File.Exists(_path))
                return;
            try
            {
                var file = JsonSerializer.Deserialize<LockoutFile>(File.ReadAllText(_path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (file?.Entries == null)
                    return;
                DateTime now = DateTime.UtcNow;
                foreach (LockoutEntry entry in file.Entries)
                {
                    if (entry.UnlockUtc <= now && entry.CompletesInWindow <= 0)
                        continue;
                    _byKey[Key(entry.CharacterId, entry.Track)] = new TrackState
                    {
                        UnlockUtc = entry.UnlockUtc,
                        CompletesInWindow = entry.CompletesInWindow
                    };
                }
            }
            catch (Exception exception)
            {
                _logger.Warn("DailyLockouts.json unreadable: " + exception.Message);
            }
        }

        void Save()
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                var entries = new List<LockoutEntry>();
                foreach (KeyValuePair<string, TrackState> pair in _byKey)
                {
                    int sep = pair.Key.IndexOf(':');
                    if (sep <= 0)
                        continue;
                    if (pair.Value.UnlockUtc <= now && pair.Value.CompletesInWindow <= 0)
                        continue;
                    entries.Add(new LockoutEntry
                    {
                        CharacterId = int.Parse(pair.Key.AsSpan(0, sep), CultureInfo.InvariantCulture),
                        Track = pair.Key[(sep + 1)..],
                        UnlockUtc = pair.Value.UnlockUtc,
                        CompletesInWindow = pair.Value.CompletesInWindow
                    });
                }

                File.WriteAllText(_path, JsonSerializer.Serialize(new LockoutFile { Entries = entries },
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception exception)
            {
                _logger.Warn("DailyLockouts.json write failed: " + exception.Message);
            }
        }

        sealed class TrackState
        {
            public DateTime UnlockUtc;
            public int CompletesInWindow;
        }

        sealed class LockoutFile
        {
            public List<LockoutEntry> Entries { get; set; } = new();
        }

        sealed class LockoutEntry
        {
            public int CharacterId { get; set; }
            public string Track { get; set; } = string.Empty;
            public DateTime UnlockUtc { get; set; }
            public int CompletesInWindow { get; set; }
        }
    }
}
