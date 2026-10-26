namespace ZoneEngine_New.Core.Raids
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Playfield;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// One alien wave plan running on a playfield. City raids, the ship-room pads, and the GM capital
    /// invasion all start through <see cref="InvasionService"/>; the playfield tick advances it.
    /// </summary>
    public static class WaveClock
    {
        public static InvasionService? Service { get; set; }

        public static void Tick(Playfield playfield, double deltaTime) => Service?.Tick(playfield, deltaTime);
    }

    public sealed class InvasionService
    {
        /// <summary>Client stat Victory Points (AORebirth.Stats id 669). Ofab terminals on Andromeda spend it.</summary>
        public const int VictoryPointsStatId = 669;

        /// <summary>MonsterData whose RDB neighbour string is "alien boss mothership". Overhead prop only.</summary>
        public const int MothershipMonsterData = 263884;

        readonly IGameData _gameData;
        readonly Lazy<PlayfieldManager> _playfields;
        readonly IZoneLogger _logger;
        readonly object _gate = new();
        readonly Dictionary<int, Encounter> _byPlayfield = new();
        InvasionCatalog _catalog;
        DateTime _catalogStamp;

        public InvasionService(IGameData gameData, Lazy<PlayfieldManager> playfields, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(playfields);
            ArgumentNullException.ThrowIfNull(logger);
            _gameData = gameData;
            _playfields = playfields;
            _logger = logger;
            _catalog = LoadCatalog();
            WaveClock.Service = this;
        }

        public InvasionCatalog Catalog
        {
            get
            {
                ReloadIfStale();
                return _catalog;
            }
        }

        public bool IsRunning(int playfieldId)
        {
            lock (_gate)
                return _byPlayfield.ContainsKey(playfieldId);
        }

        public IReadOnlyList<string> RunningTitles()
        {
            lock (_gate)
            {
                var titles = new List<string>(_byPlayfield.Count);
                foreach (Encounter encounter in _byPlayfield.Values)
                    titles.Add(encounter.Title + " (playfield " + encounter.PlayfieldId.ToString(CultureInfo.InvariantCulture)
                        + ", wave " + Math.Max(0, encounter.WaveIndex + 1).ToString(CultureInfo.InvariantCulture) + ")");
                return titles;
            }
        }

        /// <summary>Queues a wave encounter. The next tick of <paramref name="playfield"/> spawns wave 1.</summary>
        public bool TryStart(Playfield playfield, string title, IReadOnlyList<Vector3> pads, bool overhead, out string message, int? forcedLevel = null)
        {
            ArgumentNullException.ThrowIfNull(playfield);
            ArgumentNullException.ThrowIfNull(pads);
            if (pads.Count == 0)
            {
                message = "No drop pads.";
                return false;
            }

            ReloadIfStale();
            var encounter = new Encounter
            {
                PlayfieldId = playfield.Identity.Instance,
                Title = string.IsNullOrWhiteSpace(title) ? "Alien invasion" : title.Trim(),
                Pads = new List<Vector3>(pads),
                Overhead = overhead,
                Waves = CloneWaves(_catalog),
                Radius = _catalog.Radius,
                MinGapSeconds = _catalog.MinGapSeconds,
                MaxGapSeconds = _catalog.MaxGapSeconds,
                CleanupSeconds = _catalog.CleanupSeconds,
                VictoryPointsPerTenLevels = _catalog.VictoryPointsPerTenLevels,
                WaveIndex = -1,
                ForcedLevel = forcedLevel is > 0 and <= 220 ? forcedLevel.Value : 0
            };
            lock (_gate)
            {
                if (_byPlayfield.ContainsKey(encounter.PlayfieldId))
                {
                    message = encounter.Title + " already has an invasion on this playfield. .invasion stop";
                    return false;
                }

                _byPlayfield[encounter.PlayfieldId] = encounter;
            }

            Announce(null, "Alien invasion: dropships are landing in " + encounter.Title + ". Rally there.");
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Invasion queued title={0} playfield={1} pads={2} overhead={3}",
                encounter.Title, encounter.PlayfieldId, encounter.Pads.Count, overhead));
            message = "Invasion queued in " + encounter.Title + " (" + encounter.Pads.Count.ToString(CultureInfo.InvariantCulture)
                + " pads, " + encounter.Waves.Count.ToString(CultureInfo.InvariantCulture) + " waves).";
            return true;
        }

        public bool TryStop(int playfieldId, out string message)
        {
            Encounter? encounter;
            lock (_gate)
            {
                if (!_byPlayfield.Remove(playfieldId, out encounter))
                {
                    message = "No invasion on this playfield.";
                    return false;
                }

                encounter.Cancelled = true;
            }

            if (_playfields.Value.SnapshotPlayfields() is { } snapshot)
            {
                foreach (Playfield playfield in snapshot)
                {
                    if (playfield.Identity.Instance == playfieldId && !playfield.IsDisposed)
                        DespawnLiving(playfield, encounter);
                }
            }

            Announce(playfieldId, "Invasion stopped in " + encounter.Title + ".");
            message = "Stopped the invasion in " + encounter.Title + ". Living aliens were removed; corpses stay so they can be looted.";
            return true;
        }

        public int StopAll()
        {
            List<Encounter> stopped;
            lock (_gate)
            {
                stopped = new List<Encounter>(_byPlayfield.Values);
                foreach (Encounter encounter in stopped)
                    encounter.Cancelled = true;
                _byPlayfield.Clear();
            }

            foreach (Playfield playfield in _playfields.Value.SnapshotPlayfields())
            {
                foreach (Encounter encounter in stopped)
                {
                    if (encounter.PlayfieldId == playfield.Identity.Instance && !playfield.IsDisposed)
                        DespawnLiving(playfield, encounter);
                }
            }

            if (stopped.Count > 0)
                Announce(null, "Alien invasions have been stopped.");
            return stopped.Count;
        }

        public void Tick(Playfield playfield, double deltaTime)
        {
            if (playfield.IsDisposed)
                return;
            Encounter? encounter;
            lock (_gate)
            {
                if (!_byPlayfield.TryGetValue(playfield.Identity.Instance, out encounter))
                    return;
            }

            if (encounter.Cancelled)
            {
                DespawnLiving(playfield, encounter);
                lock (_gate)
                    _byPlayfield.Remove(playfield.Identity.Instance);
                return;
            }

            Prune(encounter);
            GrantVictoryPointsIfBossDead(playfield, encounter);
            DateTime now = DateTime.UtcNow;
            if (encounter.WaveIndex >= 0 && !WaveReady(encounter, now))
                return;

            int next = encounter.WaveIndex + 1;
            if (next >= encounter.Waves.Count)
            {
                if (!encounter.CleanupArmed)
                {
                    encounter.CleanupArmed = true;
                    encounter.CleanupUtc = now.AddSeconds(Math.Max(5, encounter.CleanupSeconds));
                    Announce(playfield.Identity.Instance, encounter.Title + ": the last wave is down. Leftover aliens leave in "
                        + encounter.CleanupSeconds.ToString(CultureInfo.InvariantCulture) + "s.");
                }

                if (now >= encounter.CleanupUtc)
                    Finish(playfield, encounter);
                return;
            }

            SpawnWave(playfield, encounter, encounter.Waves[next], next);
            encounter.WaveIndex = next;
            encounter.WaveSpawnedUtc = now;
        }

        static bool WaveReady(Encounter encounter, DateTime now)
        {
            if (now < encounter.WaveSpawnedUtc.AddSeconds(encounter.MinGapSeconds))
                return false;
            if (encounter.Living.Count == 0)
                return true;
            return now >= encounter.WaveSpawnedUtc.AddSeconds(encounter.MaxGapSeconds);
        }

        void Finish(Playfield playfield, Encounter encounter)
        {
            DespawnLiving(playfield, encounter);
            lock (_gate)
                _byPlayfield.Remove(playfield.Identity.Instance);
            Announce(null, "The invasion of " + encounter.Title + " is over.");
            _logger.Info("Invasion finished " + encounter.Title);
        }

        void SpawnWave(Playfield playfield, Encounter encounter, IReadOnlyList<SpawnRow> rows, int index)
        {
            if (encounter.Overhead && encounter.Ship == null)
                encounter.Ship = TrySpawnMothership(playfield, encounter);

            Measure(playfield, encounter, out int players, out int level);
            if (encounter.ForcedLevel > 0)
                level = encounter.ForcedLevel;
            SpawnService spawns = playfield.GetRequiredService<SpawnService>();
            int spawned = 0;
            NpcCharacter? boss = null;
            int padCursor = 0;
            foreach (SpawnRow row in rows)
            {
                if (row.HealBoss)
                    continue;
                boss = SpawnRowMobs(encounter, spawns, row, level, players, ref padCursor, boss, ref spawned) ?? boss;
            }

            foreach (SpawnRow row in rows)
            {
                if (!row.HealBoss)
                    continue;
                SpawnRowMobs(encounter, spawns, row, level, players, ref padCursor, boss, ref spawned);
            }

            string which = index + 1 == encounter.Waves.Count ? "the Alien General has landed" : "wave " + (index + 1).ToString(CultureInfo.InvariantCulture)
                + " of " + encounter.Waves.Count.ToString(CultureInfo.InvariantCulture);
            Announce(playfield.Identity.Instance, encounter.Title + ": " + which + " (level " + level.ToString(CultureInfo.InvariantCulture)
                + ", " + players.ToString(CultureInfo.InvariantCulture) + " defenders in range, " + spawned.ToString(CultureInfo.InvariantCulture) + " aliens).");
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Invasion wave title={0} index={1} level={2} players={3} spawned={4}",
                encounter.Title, index, level, players, spawned));
        }

        NpcCharacter? SpawnRowMobs(Encounter encounter, SpawnService spawns, SpawnRow row, int level,
            int players, ref int padCursor, NpcCharacter? boss, ref int spawned)
        {
            if (string.IsNullOrWhiteSpace(row.Hash) || row.Count < 1)
                return boss;
            if (!_gameData.TryResolveMobTemplate(row.Hash, level, out MobTemplate template)
                || !NpcTemplateValidation.CanSpawn(template))
            {
                _logger.Warn("Invasion skip unresolved hash " + row.Hash);
                return boss;
            }

            int count = row.Fixed ? row.Count : ScaleCount(row.Count, players);
            for (int n = 0; n < count; n++)
            {
                if (encounter.Cancelled)
                    return boss;
                Vector3 pad = encounter.Pads[padCursor % encounter.Pads.Count];
                padCursor++;
                float jitter = (n - (count - 1) / 2f) * 4f;
                var at = new Vector3(pad.x + jitter, pad.y, pad.z);
                try
                {
                    NpcCharacter npc = spawns.SpawnMob(template, at, level: level, spawnHash: row.Hash, spawnSource: SpawnSource.Command);
                    encounter.Living.Add(npc);
                    spawned++;
                    if (row.Boss)
                    {
                        boss ??= npc;
                        encounter.Bosses.Add(npc);
                    }

                    if (row.HealBoss && boss != null)
                    {
                        npc.HealTarget = boss;
                        npc.HealPercent = Math.Clamp(row.HealPercent, 1, 25);
                        npc.HealIntervalSeconds = 4;
                    }
                }
                catch (Exception exception)
                {
                    _logger.Warn("Invasion spawn failed hash=" + row.Hash + ": " + exception.Message);
                }
            }

            return boss;
        }

        NpcCharacter? TrySpawnMothership(Playfield playfield, Encounter encounter)
        {
            if (!_gameData.TryResolveMobTemplate("KSAU", 1, out MobTemplate template) || !NpcTemplateValidation.CanSpawn(template))
            {
                _logger.Warn("Invasion mothership skipped: KSAU template missing");
                return null;
            }

            Vector3 pad = encounter.Pads[0];
            try
            {
                SpawnService spawns = playfield.GetRequiredService<SpawnService>();
                NpcCharacter ship = spawns.SpawnMob(template, pad, level: 1, spawnHash: "KSAU", spawnSource: SpawnSource.Command,
                    attachDefaultBrain: false, configure: npc =>
                    {
                        npc.Name = "Alien Mothership";
                        npc.Attackable = false;
                        npc.Stats.Set(CharacterStat.MonsterData, MothershipMonsterData);
                        npc.Stats.Set(CharacterStat.Scale, 180);
                        npc.Stats.Set(CharacterStat.Features, 32771);
                        Vector3 at = npc.Position;
                        npc.Position = new Vector3(at.x, at.y + 80, at.z);
                    });
                _logger.Info(string.Format(CultureInfo.InvariantCulture,
                    "Invasion mothership id={0} at ({1:0.#},{2:0.#},{3:0.#}) monsterData={4}",
                    ship.Identity.Instance, ship.Position.xf, ship.Position.yf, ship.Position.zf, MothershipMonsterData));
                return ship;
            }
            catch (Exception exception)
            {
                _logger.Warn("Invasion mothership failed: " + exception.Message);
                return null;
            }
        }

        static void Prune(Encounter encounter)
        {
            encounter.Living.RemoveAll(npc => npc.IsDead || npc.Playfield == null);
        }

        void GrantVictoryPointsIfBossDead(Playfield playfield, Encounter encounter)
        {
            if (encounter.VpGranted || encounter.Bosses.Count == 0)
                return;
            NpcCharacter? dead = null;
            foreach (NpcCharacter boss in encounter.Bosses)
            {
                if (boss.IsDead)
                {
                    dead = boss;
                    break;
                }
            }

            if (dead == null)
                return;
            encounter.VpGranted = true;
            int level = Math.Max(1, dead.Stats.GetOrOne(CharacterStat.Level));
            int perTen = Math.Max(1, encounter.VictoryPointsPerTenLevels);
            int amount = Math.Max(1, level / 10) * perTen;
            DynelRegistry? registry = playfield.GetService<DynelRegistry>();
            if (registry == null)
                return;
            var stat = (CharacterStat)VictoryPointsStatId;
            int paid = 0;
            foreach (Player player in registry.PlayerEntities())
            {
                if (!ReferenceEquals(player.Playfield, playfield) || !Near(player, encounter.Pads, encounter.Radius * 2f))
                    continue;
                int total = player.Stats.GetOrZero(stat, StatDetail.Base) + amount;
                player.Stats.Set(stat, total, StatDetail.Base);
                player.SaveState.MarkDirty();
                player.Session?.Send(new ChatTextMessage
                {
                    Identity = player.Identity,
                    Text = "Victory Points +" + amount.ToString(CultureInfo.InvariantCulture) + " (total "
                        + total.ToString(CultureInfo.InvariantCulture) + ")."
                });
                paid++;
            }

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Invasion VP title={0} amount={1} players={2}", encounter.Title, amount, paid));
        }

        static void DespawnLiving(Playfield playfield, Encounter encounter)
        {
            SpawnService? spawns = playfield.GetService<SpawnService>();
            if (spawns == null)
                return;
            void One(NpcCharacter? npc)
            {
                if (npc == null || npc.IsDead || npc.Playfield == null)
                    return;
                try { spawns.DespawnNpc(npc); }
                catch (Exception) { /* already gone */ }
            }

            foreach (NpcCharacter npc in encounter.Living.ToArray())
                One(npc);
            One(encounter.Ship);
            encounter.Living.Clear();
            encounter.Ship = null;
        }

        void Measure(Playfield playfield, Encounter encounter, out int players, out int level)
        {
            players = 0;
            int sum = 0;
            DynelRegistry? registry = playfield.GetService<DynelRegistry>();
            if (registry != null)
            {
                foreach (Player player in registry.PlayerEntities())
                {
                    if (!ReferenceEquals(player.Playfield, playfield) || player.IsDead)
                        continue;
                    if (!Near(player, encounter.Pads, encounter.Radius))
                        continue;
                    // Level 1000 GMs stay on the 220 alien curve. One GM must not pull a level 1000 wave.
                    sum += Math.Clamp(player.Stats.GetOrOne(CharacterStat.Level), 1, 220);
                    players++;
                }
            }

            level = players == 0 ? 25 : Math.Clamp(sum / players, 1, 220);
        }

        static int ScaleCount(int baseCount, int players)
        {
            int extra = Math.Max(0, players - 1);
            int cap = Math.Max(baseCount, baseCount * 2);
            return Math.Clamp(baseCount + extra, 1, cap);
        }

        static bool Near(Player player, IReadOnlyList<Vector3> pads, float radius)
        {
            float limit = radius * radius;
            foreach (Vector3 pad in pads)
            {
                float dx = player.Position.xf - pad.xf;
                float dz = player.Position.zf - pad.zf;
                if (dx * dx + dz * dz <= limit)
                    return true;
            }

            return false;
        }

        void Announce(int? playfieldId, string text)
        {
            foreach (Playfield playfield in _playfields.Value.SnapshotPlayfields())
            {
                if (playfield.IsDisposed)
                    continue;
                if (playfieldId is int only && playfield.Identity.Instance != only)
                    continue;
                DynelRegistry? registry = playfield.GetService<DynelRegistry>();
                if (registry == null)
                    continue;
                foreach (Player player in registry.PlayerEntities())
                {
                    player.Session?.Send(new ChatTextMessage
                    {
                        Identity = player.Identity,
                        Text = text
                    });
                }
            }
        }

        void ReloadIfStale()
        {
            string path = Path.Combine(_gameData.RootPath, "Invasions.json");
            DateTime stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (stamp == _catalogStamp && _catalog.Waves.Count > 0)
                return;
            _catalog = LoadCatalog();
        }

        InvasionCatalog LoadCatalog()
        {
            string path = Path.Combine(_gameData.RootPath, "Invasions.json");
            _catalogStamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            try
            {
                if (!File.Exists(path))
                    return InvasionCatalog.Fallback();
                InvasionCatalog? loaded = JsonSerializer.Deserialize<InvasionCatalog>(File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (loaded == null || loaded.Cities.Count == 0)
                    return InvasionCatalog.Fallback();
                if (loaded.Waves.Count == 0)
                    loaded.Waves = InvasionCatalog.Fallback().Waves;
                loaded.Radius = loaded.Radius <= 0 ? 90 : loaded.Radius;
                loaded.MinGapSeconds = loaded.MinGapSeconds <= 0 ? 20 : loaded.MinGapSeconds;
                loaded.MaxGapSeconds = Math.Max(loaded.MinGapSeconds, loaded.MaxGapSeconds <= 0 ? 75 : loaded.MaxGapSeconds);
                return loaded;
            }
            catch (Exception exception)
            {
                _logger.Warn("Invasions.json unreadable: " + exception.Message);
                return InvasionCatalog.Fallback();
            }
        }

        static List<List<SpawnRow>> CloneWaves(InvasionCatalog catalog)
        {
            var waves = new List<List<SpawnRow>>();
            foreach (InvasionWave wave in catalog.Waves)
            {
                var rows = new List<SpawnRow>();
                foreach (InvasionSpawn spawn in wave.Spawns ?? new List<InvasionSpawn>())
                {
                    rows.Add(new SpawnRow
                    {
                        Hash = spawn.Hash ?? "",
                        Count = Math.Max(1, spawn.Count),
                        Fixed = spawn.Fixed,
                        Boss = spawn.Boss,
                        HealBoss = spawn.HealBoss,
                        HealPercent = spawn.HealPercent <= 0 ? 3 : spawn.HealPercent
                    });
                }

                if (rows.Count > 0)
                    waves.Add(rows);
            }

            return waves.Count > 0 ? waves : CloneWaves(InvasionCatalog.Fallback());
        }

        sealed class Encounter
        {
            public int PlayfieldId;
            public string Title = "";
            public List<Vector3> Pads = new();
            public bool Overhead;
            public List<List<SpawnRow>> Waves = new();
            public int Radius = 90;
            public int MinGapSeconds = 20;
            public int MaxGapSeconds = 75;
            public int CleanupSeconds = 45;
            public int VictoryPointsPerTenLevels = 5;
            public int ForcedLevel;
            public int WaveIndex = -1;
            public DateTime WaveSpawnedUtc;
            public bool CleanupArmed;
            public DateTime CleanupUtc;
            public bool Cancelled;
            public bool VpGranted;
            public NpcCharacter? Ship;
            public List<NpcCharacter> Living = new();
            public List<NpcCharacter> Bosses = new();
        }

        sealed class SpawnRow
        {
            public string Hash = "";
            public int Count = 1;
            public bool Fixed;
            public bool Boss;
            public bool HealBoss;
            public int HealPercent = 3;
        }
    }

    public sealed class InvasionCatalog
    {
        public int Radius { get; set; } = 90;
        public int MinGapSeconds { get; set; } = 20;
        public int MaxGapSeconds { get; set; } = 75;
        public int CleanupSeconds { get; set; } = 45;
        public int VictoryPointsPerTenLevels { get; set; } = 5;
        public List<InvasionWave> Waves { get; set; } = new();
        public List<InvasionCity> Cities { get; set; } = new();

        public InvasionCity? Find(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;
            string token = key.Trim();
            foreach (InvasionCity city in Cities)
            {
                if (string.Equals(city.Key, token, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(city.Name, token, StringComparison.OrdinalIgnoreCase))
                    return city;
                if (city.Aliases == null)
                    continue;
                foreach (string alias in city.Aliases)
                {
                    if (string.Equals(alias, token, StringComparison.OrdinalIgnoreCase))
                        return city;
                }
            }

            return null;
        }

        public static InvasionCatalog Fallback()
        {
            var catalog = new InvasionCatalog();
            catalog.Waves.Add(new InvasionWave { Spawns = new List<InvasionSpawn> { new() { Hash = "KSAU", Count = 3 } } });
            catalog.Waves.Add(new InvasionWave
            {
                Spawns = new List<InvasionSpawn>
                {
                    new() { Hash = "KGRU", Count = 2 },
                    new() { Hash = "KCIU", Count = 2 }
                }
            });
            catalog.Waves.Add(new InvasionWave
            {
                Spawns = new List<InvasionSpawn>
                {
                    new() { Hash = "KGXH", Count = 2 },
                    new() { Hash = "ZIXK", Count = 1 }
                }
            });
            catalog.Waves.Add(new InvasionWave
            {
                Spawns = new List<InvasionSpawn>
                {
                    new() { Hash = "ALGT", Count = 1, Fixed = true, Boss = true },
                    new() { Hash = "ALHT", Count = 2, Fixed = true, HealBoss = true, HealPercent = 3 }
                }
            });
            return catalog;
        }
    }

    public sealed class InvasionWave
    {
        public List<InvasionSpawn> Spawns { get; set; } = new();
    }

    public sealed class InvasionSpawn
    {
        public string Hash { get; set; } = "";
        public int Count { get; set; } = 1;
        public bool Fixed { get; set; }
        public bool Boss { get; set; }
        public bool HealBoss { get; set; }
        public int HealPercent { get; set; } = 3;
    }

    public sealed class InvasionCity
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public int Playfield { get; set; }
        public List<string> Aliases { get; set; } = new();
        public List<float[]> Pads { get; set; } = new();
        public string Note { get; set; } = "";
    }
}
