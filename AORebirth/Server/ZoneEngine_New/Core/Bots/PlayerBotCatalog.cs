namespace ZoneEngine_New.Core.Bots
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>GameData/PlayerBots.json: starter-area playerbot counts, levels, zones, and on/off.</summary>
    public sealed class PlayerBotCatalog
    {
        public const string FileName = "PlayerBots.json";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public PlayerBotCatalog(IGameData gameData, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);

            string path = Path.Combine(gameData.RootPath, FileName);
            if (!File.Exists(path))
            {
                logger.Warn("PlayerBots.json not found at " + path);
                Enabled = false;
                return;
            }

            try
            {
                FileModel? file = JsonSerializer.Deserialize<FileModel>(File.ReadAllText(path), JsonOptions);
                if (file == null)
                {
                    logger.Warn("PlayerBots.json empty");
                    Enabled = false;
                    return;
                }

                Enabled = file.Enabled;
                CountPerPlayfield = Clamp(file.CountPerPlayfield, 1, 20, 8);
                MinLevel = Clamp(file.MinLevel, 1, 220, 1);
                MaxLevel = Math.Max(MinLevel, Clamp(file.MaxLevel, 1, 220, 15));
                RestBelowPercent = Clamp(file.RestBelowPercent, 5, 95, 40) / 100f;
                RestUntilPercent = Math.Max(RestBelowPercent + 0.05f, Clamp(file.RestUntilPercent, 10, 100, 80) / 100f);
                EngageRange = Math.Clamp(file.EngageRange <= 0 ? 28f : file.EngageRange, 5f, 80f);
                RoamPauseMinSeconds = Math.Max(0, file.RoamPauseMinSeconds <= 0 ? 3 : file.RoamPauseMinSeconds);
                RoamPauseMaxSeconds = Math.Max(RoamPauseMinSeconds, file.RoamPauseMaxSeconds <= 0 ? 10 : file.RoamPauseMaxSeconds);
                RespawnSeconds = Math.Max(5, file.RespawnSeconds <= 0 ? 45 : file.RespawnSeconds);
                IdleWhenNoPlayers = file.IdleWhenNoPlayers;
                EmptyZoneActivityChance = Math.Clamp(
                    file.EmptyZoneActivityChance <= 0 ? 0.04 : file.EmptyZoneActivityChance,
                    0,
                    1);
                TickSeconds = Math.Clamp(file.TickSeconds <= 0 ? 1.0 : file.TickSeconds, 0.25, 10);

                BotHashes = NormalizeHashes(file.BotHashes);
                FirstNames = NormalizeNames(file.FirstNames, DefaultFirstNames);
                LastNames = NormalizeNames(file.LastNames, DefaultLastNames);

                var zones = new List<ZoneConfig>();
                if (file.Playfields != null)
                {
                    foreach (ZoneModel? zone in file.Playfields)
                    {
                        if (zone == null || zone.Id <= 0)
                            continue;
                        var points = new List<Vector3>();
                        if (zone.Points != null)
                        {
                            foreach (PointModel? point in zone.Points)
                            {
                                if (point == null)
                                    continue;
                                points.Add(new Vector3(point.X, point.Y, point.Z));
                            }
                        }

                        var huntAreas = new List<PlayerBotHuntArea>();
                        if (zone.HuntAreas != null)
                        {
                            foreach (HuntAreaModel? area in zone.HuntAreas)
                            {
                                if (area == null)
                                    continue;
                                float radius = area.Radius > 0f ? area.Radius : PlayerBotHunt.DefaultRadius;
                                huntAreas.Add(new PlayerBotHuntArea(
                                    area.Name ?? "",
                                    new Vector3(area.X, area.Y, area.Z),
                                    radius));
                            }
                        }

                        if (points.Count == 0 && huntAreas.Count > 0)
                        {
                            for (int i = 0; i < huntAreas.Count; i++)
                                points.Add(huntAreas[i].Center);
                        }

                        if (points.Count == 0)
                            continue;

                        int count = zone.Count > 0
                            ? Clamp(zone.Count, 1, 20, CountPerPlayfield)
                            : CountPerPlayfield;
                        zones.Add(new ZoneConfig(
                            zone.Id,
                            zone.Name ?? zone.Id.ToString(CultureInfo.InvariantCulture),
                            count,
                            points,
                            huntAreas));
                    }
                }

                Playfields = zones;
                logger.Info(string.Format(
                    CultureInfo.InvariantCulture,
                    "PlayerBots.json loaded enabled={0} zones={1} countDefault={2} levels={3}-{4}",
                    Enabled,
                    Playfields.Count,
                    CountPerPlayfield,
                    MinLevel,
                    MaxLevel));
            }
            catch (JsonException exception)
            {
                logger.Error("PlayerBots.json not loaded: " + exception.Message);
                Enabled = false;
            }
        }

        public bool Enabled { get; }

        public int CountPerPlayfield { get; }

        public int MinLevel { get; }

        public int MaxLevel { get; }

        public float RestBelowPercent { get; }

        public float RestUntilPercent { get; }

        public float EngageRange { get; }

        public double RoamPauseMinSeconds { get; }

        public double RoamPauseMaxSeconds { get; }

        public double RespawnSeconds { get; }

        public bool IdleWhenNoPlayers { get; }

        public double EmptyZoneActivityChance { get; }

        public double TickSeconds { get; }

        public IReadOnlyList<string> BotHashes { get; } = Array.Empty<string>();

        public IReadOnlyList<string> FirstNames { get; } = DefaultFirstNames;

        public IReadOnlyList<string> LastNames { get; } = DefaultLastNames;

        public IReadOnlyList<ZoneConfig> Playfields { get; } = Array.Empty<ZoneConfig>();

        public bool TryGetZone(int playfieldId, out ZoneConfig zone)
        {
            for (int i = 0; i < Playfields.Count; i++)
            {
                if (Playfields[i].Id == playfieldId)
                {
                    zone = Playfields[i];
                    return true;
                }
            }

            zone = default!;
            return false;
        }

        static int Clamp(int value, int min, int max, int fallback)
        {
            if (value == 0)
                return fallback;
            return Math.Clamp(value, min, max);
        }

        static string[] NormalizeHashes(string[]? hashes)
        {
            if (hashes == null || hashes.Length == 0)
                return DefaultHashes;
            var list = new List<string>(hashes.Length);
            foreach (string? hash in hashes)
            {
                if (!string.IsNullOrWhiteSpace(hash))
                    list.Add(hash.Trim().ToUpperInvariant());
            }

            return list.Count > 0 ? list.ToArray() : DefaultHashes;
        }

        static string[] NormalizeNames(string[]? names, string[] fallback)
        {
            if (names == null || names.Length == 0)
                return fallback;
            var list = new List<string>(names.Length);
            foreach (string? name in names)
            {
                if (!string.IsNullOrWhiteSpace(name))
                    list.Add(name.Trim());
            }

            return list.Count > 0 ? list.ToArray() : fallback;
        }

        static readonly string[] DefaultHashes =
        [
            "MREN", "MRSO", "MRKE", "MRDR", "MRAD", "MRAG",
            "MRBU", "MREG", "MRFI", "MRMA", "MRMP", "MRNT", "MRSH", "MRTR"
        ];

        static readonly string[] DefaultFirstNames =
        [
            "Aric", "Bren", "Cora", "Dax", "Elara", "Finn", "Gwen", "Hale"
        ];

        static readonly string[] DefaultLastNames =
        [
            "Voss", "Kane", "Reed", "Sloan", "Pike", "Cross", "Vale", "Storm"
        ];

        public sealed class ZoneConfig
        {
            public ZoneConfig(
                int id,
                string name,
                int count,
                IReadOnlyList<Vector3> points,
                IReadOnlyList<PlayerBotHuntArea>? huntAreas = null)
            {
                Id = id;
                Name = name;
                Count = count;
                Points = points;
                HuntAreas = huntAreas ?? Array.Empty<PlayerBotHuntArea>();
            }

            public int Id { get; }

            public string Name { get; }

            public int Count { get; }

            public IReadOnlyList<Vector3> Points { get; }

            public IReadOnlyList<PlayerBotHuntArea> HuntAreas { get; }
        }

        sealed class FileModel
        {
            public int SchemaVersion { get; set; }

            public bool Enabled { get; set; } = true;

            public int CountPerPlayfield { get; set; } = 8;

            public int MinLevel { get; set; } = 1;

            public int MaxLevel { get; set; } = 15;

            public int RestBelowPercent { get; set; } = 40;

            public int RestUntilPercent { get; set; } = 80;

            public float EngageRange { get; set; } = 28f;

            public double RoamPauseMinSeconds { get; set; } = 3;

            public double RoamPauseMaxSeconds { get; set; } = 10;

            public double RespawnSeconds { get; set; } = 45;

            public bool IdleWhenNoPlayers { get; set; } = true;

            public double EmptyZoneActivityChance { get; set; } = 0.04;

            public double TickSeconds { get; set; } = 1.0;

            public string[]? BotHashes { get; set; }

            public string[]? FirstNames { get; set; }

            public string[]? LastNames { get; set; }

            public ZoneModel[]? Playfields { get; set; }
        }

        sealed class ZoneModel
        {
            public int Id { get; set; }

            public string? Name { get; set; }

            public int Count { get; set; }

            public PointModel[]? Points { get; set; }

            public HuntAreaModel[]? HuntAreas { get; set; }
        }

        sealed class HuntAreaModel
        {
            public string? Name { get; set; }

            public float X { get; set; }

            public float Y { get; set; }

            public float Z { get; set; }

            public float Radius { get; set; }
        }

        sealed class PointModel
        {
            public float X { get; set; }

            public float Y { get; set; }

            public float Z { get; set; }
        }
    }
}
