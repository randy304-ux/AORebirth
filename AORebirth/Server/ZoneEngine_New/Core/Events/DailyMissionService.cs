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
    using ZoneEngine_New.Core.Quests;

    using CharacterStat = SmokeLounge.AOtomation.Messaging.GameData.CharacterStat;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Reusable outdoor daily missions. Catalog: GameData/DailyMissions.json. Knubot <c>{givedaily HASH}</c>
    /// assigns Quests.json and spawns the field (miners + optional special). First entry: Killing the Competition.
    /// </summary>
    public sealed class DailyMissionService
    {
        public static DailyMissionService? Instance { get; private set; }

        readonly IGameData _gameData;
        readonly Lazy<PlayfieldManager> _playfields;
        readonly QuestService _quests;
        readonly IZoneLogger _logger;
        readonly object _gate = new();
        DailyMissionCatalog _catalog;
        DateTime _catalogStamp;

        public DailyMissionService(IGameData gameData, Lazy<PlayfieldManager> playfields, QuestService quests, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(playfields);
            ArgumentNullException.ThrowIfNull(quests);
            ArgumentNullException.ThrowIfNull(logger);
            _gameData = gameData;
            _playfields = playfields;
            _quests = quests;
            _logger = logger;
            _catalog = Load();
            Instance = this;
        }

        public bool TryGive(Player player, string hash, out string message)
        {
            ArgumentNullException.ThrowIfNull(player);
            ReloadIfStale();
            DailyMissionSpec? spec = Find(hash);
            if (spec == null)
            {
                message = "Unknown daily '" + hash + "'.";
                return false;
            }

            string questHash = string.IsNullOrWhiteSpace(spec.QuestHash) ? spec.Hash : spec.QuestHash;
            if (_quests.GetLog(player).Quests.TryGetValue(questHash, out PlayerQuest? held) && held.IsActive)
            {
                message = "You already have " + spec.Name + ".";
                SpawnField(player, spec);
                return true;
            }

            if (!_quests.TryAssignTemplate(player, questHash, out string assignResult))
            {
                message = assignResult;
                return false;
            }

            int spawned = SpawnField(player, spec);
            message = spec.Name + " accepted. " + (spec.WaypointText ?? string.Empty)
                + " Field spawns=" + spawned.ToString(CultureInfo.InvariantCulture) + ".";
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Daily given char={0} hash={1} spawned={2}", player.Identity.Instance, spec.Hash, spawned));
            return true;
        }

        int SpawnField(Player player, DailyMissionSpec spec)
        {
            if (spec.Pads == null || spec.Pads.Count == 0)
                return 0;
            if (!_playfields.Value.TryGet(spec.Playfield, out Playfield? playfield) || playfield == null)
            {
                // Soft-load the field so pads can spawn.
                playfield = _playfields.Value.GetOrCreate(spec.Playfield);
            }

            if (playfield == null)
                return 0;

            SpawnService spawns = playfield.GetRequiredService<SpawnService>();
            int spawned = 0;
            int pad = 0;
            if (spec.Miners != null && !string.IsNullOrWhiteSpace(spec.Miners.Hash))
            {
                int count = Math.Max(1, spec.Miners.Count);
                int level = Math.Max(1, spec.Miners.Level);
                for (int i = 0; i < count; i++)
                {
                    if (TrySpawn(spawns, spec.Miners.Hash, level, spec.Pads[pad % spec.Pads.Count], out _))
                        spawned++;
                    pad++;
                }
            }

            if (spec.Special != null && !string.IsNullOrWhiteSpace(spec.Special.Hash))
            {
                int roll = Random.Shared.Next(100);
                int chance = Math.Clamp(spec.Special.ChancePercent, 0, 100);
                if (roll < chance)
                {
                    int level = SpecialLevel(player, spec.Special);
                    if (TrySpawn(spawns, spec.Special.Hash, level, spec.Pads[pad % spec.Pads.Count], out NpcCharacter? special)
                        && special != null)
                    {
                        spawned++;
                        player.Session?.Send(new SmokeLounge.AOtomation.Messaging.Messages.N3Messages.ChatTextMessage
                        {
                            Identity = player.Identity,
                            Text = special.Name + " (level " + level.ToString(CultureInfo.InvariantCulture)
                                + ") has appeared in the field."
                        });
                    }
                }
            }

            return spawned;
        }

        static int SpecialLevel(Player player, DailySpecialSpec special)
        {
            if (!special.LevelFromPlayer)
                return Math.Max(1, special.FixedLevel);
            // AO-Universe: Ganking UPH level = max of the player's Battle Station bracket.
            int level = Math.Clamp(player.Stats.GetOrOne(CharacterStat.Level), 1, 220);
            if (level <= 30) return 30;
            if (level <= 60) return 60;
            if (level <= 100) return 100;
            if (level <= 150) return 150;
            if (level <= 200) return 200;
            if (level <= 214) return 214;
            return 220;
        }

        bool TrySpawn(SpawnService spawns, string hash, int level, float[] pad, out NpcCharacter? npc)
        {
            npc = null;
            if (pad == null || pad.Length < 3)
                return false;
            if (!_gameData.TryResolveMobTemplate(hash, level, out MobTemplate template)
                || !NpcTemplateValidation.CanSpawn(template))
            {
                _logger.Warn("Daily skip unresolved hash " + hash);
                return false;
            }

            try
            {
                npc = spawns.SpawnMob(
                    template,
                    new Vector3(pad[0], pad[1], pad[2]),
                    level: level,
                    spawnHash: hash,
                    spawnSource: SpawnSource.Command);
                return true;
            }
            catch (Exception exception)
            {
                _logger.Warn("Daily spawn failed hash=" + hash + ": " + exception.Message);
                return false;
            }
        }

        DailyMissionSpec? Find(string hash)
        {
            foreach (DailyMissionSpec spec in _catalog.Missions)
            {
                if (string.Equals(spec.Hash, hash, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(spec.QuestHash, hash, StringComparison.OrdinalIgnoreCase))
                    return spec;
            }

            return null;
        }

        void ReloadIfStale()
        {
            string path = Path.Combine(_gameData.RootPath, "DailyMissions.json");
            if (!File.Exists(path))
                return;
            DateTime write = File.GetLastWriteTimeUtc(path);
            if (write == _catalogStamp)
                return;
            _catalog = Load();
        }

        DailyMissionCatalog Load()
        {
            string path = Path.Combine(_gameData.RootPath, "DailyMissions.json");
            _catalogStamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (!File.Exists(path))
            {
                _logger.Warn("DailyMissions.json missing; daily catalog empty");
                return new DailyMissionCatalog();
            }

            try
            {
                return JsonSerializer.Deserialize<DailyMissionCatalog>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip })
                    ?? new DailyMissionCatalog();
            }
            catch (Exception exception)
            {
                _logger.Warn("DailyMissions.json unreadable: " + exception.Message);
                return new DailyMissionCatalog();
            }
        }
    }

    public sealed class DailyMissionCatalog
    {
        public List<DailyMissionSpec> Missions { get; set; } = new();
    }

    public sealed class DailyMissionSpec
    {
        public string Hash { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string QuestHash { get; set; } = string.Empty;
        public int Playfield { get; set; }
        public List<float[]> Pads { get; set; } = new();
        public DailyMinerSpec? Miners { get; set; }
        public DailySpecialSpec? Special { get; set; }
        public string? WaypointText { get; set; }
        public string? Note { get; set; }
    }

    public sealed class DailyMinerSpec
    {
        public string Hash { get; set; } = string.Empty;
        public int Count { get; set; } = 3;
        public int Level { get; set; } = 1;
    }

    public sealed class DailySpecialSpec
    {
        public string Hash { get; set; } = string.Empty;
        public int ChancePercent { get; set; } = 100;
        public bool LevelFromPlayer { get; set; }
        public int FixedLevel { get; set; } = 1;
        public string? Note { get; set; }
    }
}
