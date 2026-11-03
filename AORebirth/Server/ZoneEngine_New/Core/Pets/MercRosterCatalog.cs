namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Data-driven dungeon-unlock merc roster from GameData/MercRoster.json.
    /// Profession templates, gear and follow AI stay on <see cref="Mercs"/> / <see cref="PetService"/>.
    /// </summary>
    public sealed class MercRosterCatalog
    {
        public const string FileName = "MercRoster.json";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        readonly List<MercDefinition> _all = new();
        readonly Dictionary<string, MercDefinition> _byId = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, List<MercDefinition>> _byUnlockGroup = new(StringComparer.OrdinalIgnoreCase);

        public MercRosterCatalog(IGameData gameData, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);

            string path = Path.Combine(gameData.RootPath, FileName);
            if (!File.Exists(path))
            {
                logger.Warn("MercRoster.json not found at " + path);
                return;
            }

            try
            {
                MercRosterFile? file = JsonSerializer.Deserialize<MercRosterFile>(File.ReadAllText(path), JsonOptions);
                if (file?.Mercs == null)
                    return;
                foreach (MercDefinitionFile row in file.Mercs)
                {
                    if (row == null || string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.ProfessionKey))
                        continue;
                    Mercs.Kind? kind = Mercs.Find(row.ProfessionKey);
                    if (kind == null)
                    {
                        logger.Warn("MercRoster entry " + row.Id + " has unknown professionKey " + row.ProfessionKey);
                        continue;
                    }

                    string maleName = string.IsNullOrWhiteSpace(row.MaleName) ? string.Empty : row.MaleName.Trim();
                    string femaleName = string.IsNullOrWhiteSpace(row.FemaleName) ? string.Empty : row.FemaleName.Trim();
                    MercNameFormat.ParseQuotedName(maleName, out string maleNick, out string maleLast);
                    MercNameFormat.ParseQuotedName(femaleName, out string femaleNick, out string femaleLast);

                    string display;
                    if (!string.IsNullOrWhiteSpace(row.DisplayName))
                        display = row.DisplayName.Trim();
                    else if (maleNick.Length > 0 && femaleNick.Length > 0)
                        display = maleNick + " / " + femaleNick;
                    else if (maleName.Length > 0)
                        display = maleName;
                    else
                        display = "TODO-Randy-Name (" + kind.Title + ")";

                    var def = new MercDefinition(
                        row.Id.Trim(),
                        display,
                        maleName,
                        femaleName,
                        maleNick,
                        maleLast,
                        femaleNick,
                        femaleLast,
                        kind,
                        row.Breed,
                        Math.Clamp(row.Level, 1, 220),
                        string.IsNullOrWhiteSpace(row.Role) ? "DPS" : row.Role.Trim(),
                        NormalizeTags(row.Tags),
                        string.IsNullOrWhiteSpace(row.DungeonName) ? "TODO dungeon" : row.DungeonName.Trim(),
                        row.DungeonPlayfields ?? [],
                        string.IsNullOrWhiteSpace(row.BossName) ? "TODO boss" : row.BossName.Trim(),
                        string.IsNullOrWhiteSpace(row.BossHash) ? null : row.BossHash.Trim(),
                        string.IsNullOrWhiteSpace(row.UnlockGroup) ? row.Id.Trim() : row.UnlockGroup.Trim(),
                        string.IsNullOrWhiteSpace(row.NanoStyle) ? null : row.NanoStyle.Trim().ToLowerInvariant(),
                        row.DungeonPlayfieldsTodo,
                        row.BossHashTodo);
                    _all.Add(def);
                    _byId[def.Id] = def;
                    if (!_byUnlockGroup.TryGetValue(def.UnlockGroup, out List<MercDefinition>? group))
                    {
                        group = new List<MercDefinition>();
                        _byUnlockGroup[def.UnlockGroup] = group;
                    }

                    group.Add(def);
                }
            }
            catch (JsonException exception)
            {
                logger.Error("MercRoster.json not loaded: " + exception.Message);
            }
        }

        public IReadOnlyList<MercDefinition> All => _all;

        public MercDefinition? Find(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;
            string raw = key.Trim();
            if (_byId.TryGetValue(raw, out MercDefinition? byId))
                return byId;

            string core = MercNameFormat.StripModeTag(raw);
            foreach (MercDefinition def in _all)
            {
                if (MercNameFormat.NameMatches(core, def) || MercNameFormat.NameMatches(raw, def))
                    return def;
                if (string.Equals(def.DisplayName, raw, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(def.DisplayName, core, StringComparison.OrdinalIgnoreCase))
                    return def;
                if (string.Equals(def.Kind.Key, raw, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(def.Kind.Title, raw, StringComparison.OrdinalIgnoreCase))
                    return def;
                if (raw.Length >= 3
                    && def.DisplayName.Contains(raw, StringComparison.OrdinalIgnoreCase))
                    return def;
            }

            // NT aliases (single body; mode is separate).
            if (raw.Equals("nt", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("nt_sanctum", StringComparison.OrdinalIgnoreCase)
                || (raw.Contains("nano", StringComparison.OrdinalIgnoreCase)
                    && raw.Contains("sanctum", StringComparison.OrdinalIgnoreCase)))
                return _byId.TryGetValue("nt_sanctum", out MercDefinition? nt) ? nt : null;

            return null;
        }

        public IReadOnlyList<MercDefinition> ByUnlockGroup(string unlockGroup)
            => _byUnlockGroup.TryGetValue(unlockGroup, out List<MercDefinition>? list) ? list : [];

        static IReadOnlyList<string> NormalizeTags(List<string>? tags)
        {
            if (tags == null || tags.Count == 0)
                return [];
            var list = new List<string>(tags.Count);
            foreach (string tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    continue;
                string t = tag.Trim();
                if (!list.Exists(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
                    list.Add(t);
            }

            return list;
        }

        sealed class MercRosterFile
        {
            public List<MercDefinitionFile>? Mercs { get; set; }
        }

        sealed class MercDefinitionFile
        {
            public string Id { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string? MaleName { get; set; }
            public string? FemaleName { get; set; }
            public string ProfessionKey { get; set; } = "";
            public string? Breed { get; set; }
            public int Level { get; set; }
            public string Role { get; set; } = "";
            public List<string>? Tags { get; set; }
            public string DungeonName { get; set; } = "";
            public int[]? DungeonPlayfields { get; set; }
            public string? DungeonPlayfieldsTodo { get; set; }
            public string BossName { get; set; } = "";
            public string? BossHash { get; set; }
            public string? BossHashTodo { get; set; }
            public string UnlockGroup { get; set; } = "";
            public string? NanoStyle { get; set; }
        }
    }

    public sealed record MercDefinition(
        string Id,
        string DisplayName,
        string MaleName,
        string FemaleName,
        string MaleNick,
        string MaleLast,
        string FemaleNick,
        string FemaleLast,
        Mercs.Kind Kind,
        string? Breed,
        int Level,
        string Role,
        IReadOnlyList<string> Tags,
        string DungeonName,
        IReadOnlyList<int> DungeonPlayfields,
        string BossName,
        string? BossHash,
        string UnlockGroup,
        string? NanoStyle,
        string? DungeonPlayfieldsTodo,
        string? BossHashTodo)
    {
        public bool HasTag(string tag)
        {
            foreach (string t in Tags)
            {
                if (string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return string.Equals(Role, tag, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Kind.Key, tag, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Kind.Title, tag, StringComparison.OrdinalIgnoreCase)
                || DungeonName.Contains(tag, StringComparison.OrdinalIgnoreCase);
        }
    }

    public enum MercUnlockState
    {
        Locked = 0,
        DungeonOnly = 1,
        Unlocked = 2
    }
}
