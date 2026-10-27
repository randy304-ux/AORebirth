namespace ZoneEngine_New.Core.Events
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
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
                return _enabled
                    ? "Halloween event ON. Living spawns=" + _living.Count.ToString(CultureInfo.InvariantCulture)
                      + " (outdoor pads + quest NPCs from Halloween.json)."
                    : "Halloween event OFF. Use .event halloween on to spawn outdoor/low Uncle Pumpkin-Heads and city leets.";
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

            int spawned = SpawnAll();
            _logger.Info("Halloween event enabled; spawned=" + spawned.ToString(CultureInfo.InvariantCulture));
            return "Halloween event ON. Spawned " + spawned.ToString(CultureInfo.InvariantCulture) + " NPCs.";
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
                    if (t.Npc != null && !t.Npc.IsDead && t.Npc.Playfield != null)
                        continue;
                    if (t.RespawnAtUtc is DateTime when && when > now)
                        continue;
                    if (t.RespawnAtUtc == null && (t.Npc == null || t.Npc.IsDead || t.Npc.Playfield == null))
                    {
                        t.RespawnAtUtc = now.AddSeconds(_catalog.RespawnSeconds);
                        continue;
                    }

                    (due ??= new List<Tracked>()).Add(t);
                }
            }

            if (due == null)
                return;
            foreach (Tracked t in due)
                Respawn(playfield, t);
        }

        int SpawnAll()
        {
            int count = 0;
            foreach (HalloweenQuestNpc q in _catalog.QuestNpcs)
                count += SpawnOne(q.Playfield, q.Hash, 1, q.Pad, q.Key, social: true) ? 1 : 0;
            foreach (HalloweenOutdoorGroup g in _catalog.Outdoor)
            {
                foreach (float[] pad in g.Pads)
                {
                    if (SpawnOne(g.Playfield, g.Hash, g.Level, pad, g.Key, social: false))
                        count++;
                }
            }

            return count;
        }

        bool SpawnOne(int playfieldId, string hash, int level, float[] pad, string key, bool social)
        {
            if (pad == null || pad.Length < 3)
                return false;
            if (!_gameData.TryResolveMobTemplate(hash, level, out MobTemplate template)
                || !NpcTemplateValidation.CanSpawn(template))
            {
                _logger.Warn("Halloween skip unresolved hash " + hash);
                return false;
            }

            if (!_playfields.Value.TryGet(playfieldId, out Playfield? playfield) || playfield == null)
            {
                // Playfield not loaded yet; remember a ghost track that ticks when the PF boots.
                lock (_gate)
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

                return false;
            }

            return Respawn(playfield, new Tracked
            {
                Key = key,
                Hash = hash,
                Level = level,
                PlayfieldId = playfieldId,
                Pad = new Vector3(pad[0], pad[1], pad[2]),
                Social = social
            });
        }

        bool Respawn(Playfield playfield, Tracked track)
        {
            if (!_gameData.TryResolveMobTemplate(track.Hash, track.Level, out MobTemplate template)
                || !NpcTemplateValidation.CanSpawn(template))
                return false;

            try
            {
                SpawnService spawns = playfield.GetRequiredService<SpawnService>();
                NpcCharacter npc = spawns.SpawnMob(
                    template,
                    track.Pad,
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
    }

    public sealed class HalloweenCatalog
    {
        public int RespawnSeconds { get; set; } = 180;

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
