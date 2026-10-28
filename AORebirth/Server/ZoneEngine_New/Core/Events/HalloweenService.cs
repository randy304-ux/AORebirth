namespace ZoneEngine_New.Core.Events
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.Json;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Playfield;

    using CharacterStat = SmokeLounge.AOtomation.Messaging.GameData.CharacterStat;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Outdoor / low Halloween spawns and city quest NPCs. Toggled with <c>.event halloween on|off|status</c>.
    /// Catalog: GameData/Halloween.json.
    /// </summary>
    public sealed class HalloweenService
    {
        public static HalloweenService? Instance { get; private set; }

        readonly IGameData _gameData;
        readonly Lazy<PlayfieldManager> _playfields;
        readonly IZoneLogger _logger;
        readonly object _gate = new();
        readonly List<Tracked> _living = new();
        HalloweenCatalog _catalog;
        bool _enabled;
        DateTime _catalogStamp;

        static readonly Dictionary<int, string> ZoneNames = new()
        {
            [800] = "Borealis",
            [655] = "Andromeda (ICC)",
            [790] = "Stret West Bank",
            [791] = "Stret West Notum Ore",
        };

        public HalloweenService(IGameData gameData, Lazy<PlayfieldManager> playfields, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(playfields);
            ArgumentNullException.ThrowIfNull(logger);
            _gameData = gameData;
            _playfields = playfields;
            _logger = logger;
            _catalog = Load();
            Instance = this;
            // Headless boots have no GM console; Halloween.json EnabledOnBoot (or AO_REBIRTH_HALLOWEEN=1) is the server-side .event halloween on.
            if (_catalog.EnabledOnBoot
                || string.Equals(Environment.GetEnvironmentVariable("AO_REBIRTH_HALLOWEEN"), "1", StringComparison.Ordinal))
            {
                Enable();
            }
        }

        public bool Enabled
        {
            get { lock (_gate) return _enabled; }
        }

        public string Status()
        {
            lock (_gate)
            {
                ReloadIfStale();
                if (!_enabled)
                    return "Halloween event OFF. Use .event halloween on to spawn outdoor/low Uncle Pumpkin-Heads and city leets.";

                int living = 0, queued = 0;
                var perZone = new SortedDictionary<string, (int Living, int Queued)>(StringComparer.Ordinal);
                foreach (Tracked t in _living)
                {
                    string zone = ZoneLabel(t.PlayfieldId);
                    perZone.TryGetValue(zone, out (int Living, int Queued) counts);
                    if (t.Npc != null && !t.Npc.IsDead && t.Npc.Playfield != null
                        && !t.Npc.Playfield.IsDisposed)
                    {
                        living++;
                        counts.Living++;
                    }
                    else
                    {
                        queued++;
                        counts.Queued++;
                    }

                    perZone[zone] = counts;
                }

                var parts = new List<string>(perZone.Count);
                foreach (KeyValuePair<string, (int Living, int Queued)> pair in perZone)
                {
                    parts.Add(pair.Key + " alive=" + pair.Value.Living.ToString(CultureInfo.InvariantCulture)
                        + " queued=" + pair.Value.Queued.ToString(CultureInfo.InvariantCulture));
                }

                return "Halloween event ON. Living=" + living.ToString(CultureInfo.InvariantCulture)
                    + " queued=" + queued.ToString(CultureInfo.InvariantCulture)
                    + (parts.Count > 0 ? " [" + string.Join("; ", parts) + "]" : "") + ".";
            }
        }

        public string Enable()
        {
            lock (_gate)
            {
                ReloadIfStale();
                if (_enabled)
                    return Status();
                _enabled = true;
            }

            SpawnReport report = SpawnAll();
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Halloween event enabled; spawned={0} queued={1} skipped={2}",
                report.Spawned, report.Queued, report.Skipped));
            return FormatEnableReply(report);
        }

        public string Disable()
        {
            List<NpcCharacter> remove;
            lock (_gate)
            {
                if (!_enabled && _living.Count == 0)
                    return Status();
                _enabled = false;
                remove = new List<NpcCharacter>(_living.Count);
                foreach (Tracked t in _living)
                {
                    if (t.Npc != null && !t.Npc.IsDead && t.Npc.Playfield != null)
                        remove.Add(t.Npc);
                }

                _living.Clear();
            }

            foreach (NpcCharacter npc in remove)
            {
                try
                {
                    npc.Playfield?.GetService<SpawnService>()?.DespawnNpc(npc);
                }
                catch (Exception exception)
                {
                    _logger.Warn("Halloween despawn failed: " + exception.Message);
                }
            }

            _logger.Info("Halloween event disabled; despawned=" + remove.Count.ToString(CultureInfo.InvariantCulture));
            return "Halloween event OFF. Despawned " + remove.Count.ToString(CultureInfo.InvariantCulture) + " NPCs.";
        }

        /// <summary>
        /// Called every playfield tick. Ghost tracks (queued while the PF was unloaded) and death timers
        /// spawn here as soon as the PF is live — typically when a player enters the zone.
        /// </summary>
        public void Tick(Playfield playfield, double deltaTime)
        {
            ArgumentNullException.ThrowIfNull(playfield);
            if (deltaTime <= 0)
                return;

            List<Tracked> due = null!;
            lock (_gate)
            {
                if (!_enabled)
                    return;
                DateTime now = DateTime.UtcNow;
                for (int i = 0; i < _living.Count; i++)
                {
                    Tracked t = _living[i];
                    if (t.PlayfieldId != playfield.Identity.Instance)
                        continue;

                    // Empty unload leaves disposed NPCs whose Playfield ref can still look non-null; require this live PF.
                    bool alive = t.Npc != null && !t.Npc.IsDead && ReferenceEquals(t.Npc.Playfield, playfield);
                    if (alive)
                        continue;

                    // Death path sets RespawnAtUtc to now+RespawnSeconds. Ghosts / unloaded NPCs are due now
                    // (RespawnAtUtc is UtcNow or null). Do not invent a delay when the timer was never armed.
                    if (t.RespawnAtUtc is DateTime when && when > now)
                        continue;

                    (due ??= new List<Tracked>()).Add(t);
                }
            }

            if (due == null)
                return;
            foreach (Tracked t in due)
                Respawn(playfield, t);
        }

        /// <summary>
        /// Empty playfield unload drops living Halloween NPCs; tracks can go stale. While the event is on,
        /// re-seed this playfield's catalog pads every time it is built/loaded (including Borealis 800 and BS 665).
        /// </summary>
        public void OnPlayfieldReady(Playfield playfield)
        {
            ArgumentNullException.ThrowIfNull(playfield);
            lock (_gate)
            {
                if (!_enabled)
                    return;
                ReloadIfStale();
            }

            SpawnReport report = ApplyPlayfield(playfield);
            if (report.Spawned + report.Queued + report.Skipped == 0)
                return;

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Halloween re-applied playfield={0} spawned={1} queued={2} skipped={3}",
                playfield.Identity.Instance, report.Spawned, report.Queued, report.Skipped));
        }

        SpawnReport SpawnAll()
        {
            var report = new SpawnReport();
            foreach (HalloweenQuestNpc q in _catalog.QuestNpcs)
                Record(report, SpawnOne(q.Playfield, q.Hash, 1, q.Pad, q.Key, social: true), q.Playfield, q.Hash);
            foreach (HalloweenOutdoorGroup g in _catalog.Outdoor)
            {
                foreach (float[] pad in g.Pads)
                    Record(report, SpawnOne(g.Playfield, g.Hash, g.Level, pad, g.Key, social: false), g.Playfield, g.Hash);
            }

            return report;
        }

        SpawnReport ApplyPlayfield(Playfield playfield)
        {
            int playfieldId = playfield.Identity.Instance;
            lock (_gate)
            {
                for (int i = _living.Count - 1; i >= 0; i--)
                {
                    if (_living[i].PlayfieldId == playfieldId)
                        _living.RemoveAt(i);
                }
            }

            var report = new SpawnReport();
            foreach (HalloweenQuestNpc q in _catalog.QuestNpcs)
            {
                if (q.Playfield != playfieldId)
                    continue;
                Record(report, SpawnOne(q.Playfield, q.Hash, 1, q.Pad, q.Key, social: true), q.Playfield, q.Hash);
            }

            foreach (HalloweenOutdoorGroup g in _catalog.Outdoor)
            {
                if (g.Playfield != playfieldId)
                    continue;
                foreach (float[] pad in g.Pads)
                    Record(report, SpawnOne(g.Playfield, g.Hash, g.Level, pad, g.Key, social: false), g.Playfield, g.Hash);
            }

            return report;
        }

        static void Record(SpawnReport report, SpawnOutcome outcome, int playfieldId, string hash)
        {
            string zone = ZoneLabel(playfieldId);
            switch (outcome)
            {
                case SpawnOutcome.Spawned:
                    report.Spawned++;
                    report.SpawnedZones.Add(zone);
                    break;
                case SpawnOutcome.Queued:
                    report.Queued++;
                    report.QueuedZones.Add(zone);
                    break;
                default:
                    report.Skipped++;
                    report.SkippedHashes.Add(hash + "@" + zone);
                    break;
            }
        }

        SpawnOutcome SpawnOne(int playfieldId, string hash, int level, float[] pad, string key, bool social)
        {
            if (pad == null || pad.Length < 3)
                return SpawnOutcome.Skipped;
            if (!_gameData.TryResolveMobTemplate(hash, level, out MobTemplate template)
                || !NpcTemplateValidation.CanSpawn(template))
            {
                _logger.Warn("Halloween skip unresolved hash " + hash + " pf=" + playfieldId.ToString(CultureInfo.InvariantCulture));
                return SpawnOutcome.Skipped;
            }

            if (!_playfields.Value.TryGet(playfieldId, out Playfield? playfield) || playfield == null)
            {
                // Playfield not loaded yet; remember a ghost track that Tick() fulfills when the PF boots.
                lock (_gate)
                {
                    if (!HasTrack(key, playfieldId, pad))
                    {
                        _living.Add(new Tracked
                        {
                            Key = key,
                            Hash = hash,
                            Level = level,
                            PlayfieldId = playfieldId,
                            Pad = new Vector3(pad[0], pad[1], pad[2]),
                            Social = social,
                            RespawnAtUtc = DateTime.UtcNow
                        });
                    }
                }

                return SpawnOutcome.Queued;
            }

            bool ok = Respawn(playfield, new Tracked
            {
                Key = key,
                Hash = hash,
                Level = level,
                PlayfieldId = playfieldId,
                Pad = new Vector3(pad[0], pad[1], pad[2]),
                Social = social
            });
            return ok ? SpawnOutcome.Spawned : SpawnOutcome.Skipped;
        }

        bool HasTrack(string key, int playfieldId, float[] pad)
        {
            foreach (Tracked t in _living)
            {
                if (t.Key == key && t.PlayfieldId == playfieldId
                    && Math.Abs(t.Pad.x - pad[0]) < 0.01f
                    && Math.Abs(t.Pad.z - pad[2]) < 0.01f)
                    return true;
            }

            return false;
        }

        bool Respawn(Playfield playfield, Tracked track)
        {
            if (!_gameData.TryResolveMobTemplate(track.Hash, track.Level, out MobTemplate template)
                || !NpcTemplateValidation.CanSpawn(template))
            {
                _logger.Warn("Halloween skip unresolved hash " + track.Hash
                    + " pf=" + track.PlayfieldId.ToString(CultureInfo.InvariantCulture));
                return false;
            }

            try
            {
                // Pads must sit on terrain; bad Y (e.g. literal 5 under Borealis hills) leaves ghosts "living" but invisible.
                Vector3 at = track.Pad;
                if (playfield.TrySnapFeetToFloor(at, out Vector3 floor))
                    at = floor;

                SpawnService spawns = playfield.GetRequiredService<SpawnService>();
                NpcCharacter npc = spawns.SpawnMob(
                    template,
                    at,
                    level: track.Level,
                    spawnHash: track.Hash,
                    spawnSource: SpawnSource.Command,
                    configure: spawned =>
                    {
                        if (!track.Social)
                            return;
                        spawned.Attackable = false;
                        spawned.Stats.Set(CharacterStat.Flags, 277352961);
                    });
                track.Npc = npc;
                track.RespawnAtUtc = null;
                npc.Died += _ =>
                {
                    lock (_gate)
                    {
                        if (_enabled)
                            track.RespawnAtUtc = DateTime.UtcNow.AddSeconds(_catalog.RespawnSeconds);
                    }
                };
                lock (_gate)
                {
                    if (!_living.Contains(track))
                        _living.Add(track);
                }

                return true;
            }
            catch (Exception exception)
            {
                _logger.Warn("Halloween spawn failed hash=" + track.Hash + ": " + exception.Message);
                return false;
            }
        }

        static string FormatEnableReply(SpawnReport report)
        {
            var sb = new StringBuilder();
            sb.Append("Halloween event ON. Spawned ")
                .Append(report.Spawned.ToString(CultureInfo.InvariantCulture))
                .Append(" now");
            if (report.SpawnedZones.Count > 0)
                sb.Append(" (").Append(string.Join(", ", report.SpawnedZones)).Append(')');
            sb.Append("; queued ")
                .Append(report.Queued.ToString(CultureInfo.InvariantCulture))
                .Append(" for unloaded zones");
            if (report.QueuedZones.Count > 0)
                sb.Append(" (").Append(string.Join(", ", report.QueuedZones)).Append(')');
            sb.Append("; skipped ")
                .Append(report.Skipped.ToString(CultureInfo.InvariantCulture))
                .Append(" unresolved");
            if (report.SkippedHashes.Count > 0)
                sb.Append(" (").Append(string.Join(", ", report.SkippedHashes)).Append(')');
            sb.Append('.');
            return sb.ToString();
        }

        static string ZoneLabel(int playfieldId)
            => ZoneNames.TryGetValue(playfieldId, out string? name)
                ? name + " " + playfieldId.ToString(CultureInfo.InvariantCulture)
                : "PF " + playfieldId.ToString(CultureInfo.InvariantCulture);

        void ReloadIfStale()
        {
            string path = Path.Combine(_gameData.RootPath, "Halloween.json");
            if (!File.Exists(path))
                return;
            DateTime write = File.GetLastWriteTimeUtc(path);
            if (write == _catalogStamp)
                return;
            _catalog = Load();
        }

        HalloweenCatalog Load()
        {
            string path = Path.Combine(_gameData.RootPath, "Halloween.json");
            _catalogStamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (!File.Exists(path))
            {
                _logger.Warn("Halloween.json missing; event catalog empty");
                return new HalloweenCatalog();
            }

            try
            {
                HalloweenCatalog? catalog = JsonSerializer.Deserialize<HalloweenCatalog>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip });
                return catalog ?? new HalloweenCatalog();
            }
            catch (Exception exception)
            {
                _logger.Warn("Halloween.json unreadable: " + exception.Message);
                return new HalloweenCatalog();
            }
        }

        enum SpawnOutcome
        {
            Spawned,
            Queued,
            Skipped
        }

        sealed class SpawnReport
        {
            public int Spawned;
            public int Queued;
            public int Skipped;
            public SortedSet<string> SpawnedZones { get; } = new(StringComparer.Ordinal);
            public SortedSet<string> QueuedZones { get; } = new(StringComparer.Ordinal);
            public SortedSet<string> SkippedHashes { get; } = new(StringComparer.Ordinal);
        }

        sealed class Tracked
        {
            public string Key = string.Empty;
            public string Hash = string.Empty;
            public int Level;
            public int PlayfieldId;
            public Vector3 Pad;
            public bool Social;
            public NpcCharacter? Npc;
            public DateTime? RespawnAtUtc;
        }
    }

    public static class HalloweenClock
    {
        public static void Tick(Playfield playfield, double deltaTime)
            => HalloweenService.Instance?.Tick(playfield, deltaTime);

        public static void OnPlayfieldReady(Playfield playfield)
            => HalloweenService.Instance?.OnPlayfieldReady(playfield);
    }

    public sealed class HalloweenCatalog
    {
        public int RespawnSeconds { get; set; } = 180;

        /// <summary>When true, ZoneEngine enables the event during service construction (headless equivalent of .event halloween on).</summary>
        public bool EnabledOnBoot { get; set; }

        public List<HalloweenQuestNpc> QuestNpcs { get; set; } = new();

        public List<HalloweenOutdoorGroup> Outdoor { get; set; } = new();
    }

    public sealed class HalloweenQuestNpc
    {
        public string Key { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Playfield { get; set; }
        public float[] Pad { get; set; } = Array.Empty<float>();
        public string? Note { get; set; }
    }

    public sealed class HalloweenOutdoorGroup
    {
        public string Key { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public int Level { get; set; } = 1;
        public int Playfield { get; set; }
        public List<float[]> Pads { get; set; } = new();
        public string? Note { get; set; }
    }
}
