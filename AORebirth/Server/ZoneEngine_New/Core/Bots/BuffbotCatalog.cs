namespace ZoneEngine_New.Core.Bots
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Nanos;

    /// <summary>
    /// Buff menu names from GameData/Buffbot.json. Each name is resolved to items.dat templates.
    /// The id is never written in the file. A name with no friendly buff template is omitted.
    /// </summary>
    public sealed class BuffbotCatalog
    {
        public const string FileName = "Buffbot.json";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        readonly Dictionary<int, BuffLine> _byId = new();
        readonly List<BuffSection> _sections = new();
        readonly List<string> _missing = new();

        public BuffbotCatalog(IGameData gameData, IItemTemplateCatalog items, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(logger);

            string path = Path.Combine(gameData.RootPath, FileName);
            BuffbotFile? file = null;
            if (!File.Exists(path))
            {
                logger.Warn("Buffbot.json not found at " + path);
            }
            else
            {
                try
                {
                    file = JsonSerializer.Deserialize<BuffbotFile>(File.ReadAllText(path), JsonOptions);
                }
                catch (JsonException exception)
                {
                    logger.Error("Buffbot.json not loaded: " + exception.Message);
                }
            }

            var byName = new Dictionary<string, List<ItemTemplate>>(StringComparer.OrdinalIgnoreCase);
            foreach (ItemTemplate template in items.All)
            {
                if (string.IsNullOrWhiteSpace(template.Name))
                    continue;
                if (!byName.TryGetValue(template.Name, out List<ItemTemplate>? list))
                {
                    list = new List<ItemTemplate>();
                    byName.Add(template.Name, list);
                }

                list.Add(template);
            }

            if (file?.Sections != null)
            {
                foreach (BuffbotSectionFile section in file.Sections)
                {
                    if (section == null || string.IsNullOrWhiteSpace(section.Name) || section.Nanos == null)
                        continue;
                    var lines = new List<BuffLine>();
                    foreach (string? raw in section.Nanos)
                    {
                        string name = (raw ?? string.Empty).Trim();
                        if (name.Length == 0)
                            continue;
                        // Morph / appearance nanos must never be on the buffbot (L33t Transformation, etc.).
                        if (IsExcludedBuffName(name))
                        {
                            _missing.Add(name + " (excluded morph/appearance)");
                            continue;
                        }

                        if (!byName.TryGetValue(name, out List<ItemTemplate>? templates))
                        {
                            _missing.Add(name);
                            continue;
                        }

                        var ranks = new List<ItemTemplate>();
                        bool sawSelfOnly = false;
                        foreach (ItemTemplate template in templates)
                        {
                            NanoSpell spell = NanoSpell.From(template);
                            if (!spell.IsBuff || spell.IsHostile)
                                continue;
                            // Timed composite variants (2/4/8 hours) stay out; base name ranks only.
                            if (!string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase))
                                continue;
                            // Buffbot casts onto other players: need ApplyOnFriendly. Self/pulse/aura lines stay out.
                            if (!CanTargetOtherPlayer(template))
                            {
                                sawSelfOnly = true;
                                continue;
                            }

                            ranks.Add(template);
                        }

                        if (ranks.Count == 0)
                        {
                            _missing.Add(sawSelfOnly ? name + " (self-only/pulse)" : name);
                            continue;
                        }

                        ranks.Sort((a, b) => a.Quality.CompareTo(b.Quality));
                        var line = new BuffLine(name, ranks);
                        lines.Add(line);
                        foreach (ItemTemplate template in ranks)
                            _byId[template.Id] = line;
                    }

                    if (lines.Count > 0)
                        _sections.Add(new BuffSection(section.Name.Trim(), lines));
                }
            }

            logger.Info(string.Format(
                CultureInfo.InvariantCulture,
                "Buffbot catalog sections={0} lines={1} missing={2}",
                _sections.Count,
                LineCount,
                _missing.Count));
            if (_missing.Count > 0)
                logger.Warn("Buffbot names not in items.dat: " + string.Join(", ", _missing));
        }

        public IReadOnlyList<BuffSection> Sections => _sections;

        public IReadOnlyList<string> MissingNames => _missing;

        public int LineCount
        {
            get
            {
                int count = 0;
                foreach (BuffSection section in _sections)
                    count += section.Lines.Count;
                return count;
            }
        }

        public bool Contains(int nanoId) => _byId.ContainsKey(nanoId);

        public bool TryGetLine(int nanoId, out BuffLine line)
            => _byId.TryGetValue(nanoId, out line!);

        /// <summary>Highest rank whose required level is at or below <paramref name="level"/>.</summary>
        public bool TryOffer(BuffLine line, int level, out BuffOffer offer)
        {
            ArgumentNullException.ThrowIfNull(line);
            ItemTemplate? best = null;
            ItemTemplate? lowest = null;
            foreach (ItemTemplate template in line.Ranks)
            {
                lowest ??= template;
                if (RequiredLevel(template) <= level)
                    best = template;
            }

            ItemTemplate shown = best ?? lowest!;
            NanoSpell spell = NanoSpell.From(shown);
            offer = new BuffOffer(
                line.Name,
                shown.Id,
                shown.Quality,
                spell.NcuCost,
                Bonus(shown),
                best != null,
                RequiredLevel(lowest!));
            return best != null;
        }

        public IEnumerable<BuffOffer> Castable(int level)
        {
            foreach (BuffSection section in _sections)
            {
                foreach (BuffLine line in section.Lines)
                {
                    if (TryOffer(line, level, out BuffOffer offer))
                        yield return offer;
                }
            }
        }

        /// <summary>
        /// QL is the level gate. A real ToUse level compare on the template can raise it.
        /// Stat 54 on the nano's own stat map is NCU, not this check.
        /// </summary>
        public static int RequiredLevel(ItemTemplate template)
        {
            int need = template.Quality < 1 ? 1 : template.Quality;
            foreach (ItemAction action in template.Actions)
            {
                if (action.ActionType != (int)ActionType.ToUse)
                    continue;
                foreach (ItemRequirement requirement in action.Requirements)
                {
                    if (requirement.StatNumber != (int)CharacterStat.Level)
                        continue;
                    var op = (Operator)requirement.Operator;
                    if (op == Operator.GreaterThan)
                        need = Math.Max(need, requirement.Value + 1);
                    else if (op == Operator.EqualTo)
                        need = Math.Max(need, requirement.Value);
                }
            }

            return need;
        }

        static bool IsExcludedBuffName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return true;
            if (name.Contains("L33t", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Leet Transformation", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.Contains("Transformation", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Composite", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.Contains("(Team)", StringComparison.OrdinalIgnoreCase))
                return true;
            // Keeper auras: self-cast, pulse to team — not castable onto another player as a buffbot hit.
            if (name.Contains("Tone of", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Courage Of The Just", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Clarion Call", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Blessing of The Light", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Blessing of Life", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Barrier of the", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Benevolent Ward", StringComparison.OrdinalIgnoreCase))
                return true;
            // Soldier shields other than single-target reflect / damage-shield projector.
            if (name.Contains("Absorption Shield", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Deflection Shield", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Mirror Shield", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Combat Barrier", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Adaptive Resonance Field", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        /// <summary>
        /// True when the nano can land on another player. Many buff templates leave Can=0; those are
        /// allowed unless <see cref="NanoSpell.IsSelfOnly"/> (ApplyOnSelf-only or all functions User/Wearer).
        /// When Can is set, ApplyOnFriendly is required.
        /// </summary>
        static bool CanTargetOtherPlayer(ItemTemplate template)
        {
            NanoSpell spell = NanoSpell.From(template);
            if (spell.IsSelfOnly)
                return false;
            if (!template.Stats.TryGetValue(CharacterStat.Can, out int canRaw) || canRaw == 0)
                return true;
            return ((CanFlags)(uint)canRaw & CanFlags.ApplyOnFriendly) != 0;
        }

        static string Bonus(ItemTemplate template)
        {
            if (!template.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells) || spells == null)
                return string.Empty;

            var parts = new List<string>();
            foreach (ItemSpell spell in spells)
            {
                if (spell.FunctionType != (int)FunctionType.Modify
                    && spell.FunctionType != (int)FunctionType.ScalingModify)
                    continue;
                if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int delta))
                    continue;
                if (parts.Count == 4)
                {
                    parts.Add("...");
                    break;
                }

                string sign = delta >= 0 ? "+" : string.Empty;
                parts.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}{1} {2}",
                    sign,
                    delta,
                    ((CharacterStat)statId).ToString()));
            }

            return string.Join(", ", parts);
        }

        sealed class BuffbotFile
        {
            public List<BuffbotSectionFile>? Sections { get; set; }
        }

        sealed class BuffbotSectionFile
        {
            public string? Name { get; set; }

            public List<string>? Nanos { get; set; }
        }
    }

    public sealed class BuffSection
    {
        public BuffSection(string name, List<BuffLine> lines)
        {
            Name = name;
            Lines = lines;
        }

        public string Name { get; }

        public List<BuffLine> Lines { get; }
    }

    public sealed class BuffLine
    {
        public BuffLine(string name, List<ItemTemplate> ranks)
        {
            Name = name;
            Ranks = ranks;
        }

        public string Name { get; }

        public List<ItemTemplate> Ranks { get; }
    }

    public readonly struct BuffOffer
    {
        public BuffOffer(string name, int nanoId, int quality, int ncu, string bonus, bool available, int requiredLevel)
        {
            Name = name;
            NanoId = nanoId;
            Quality = quality;
            Ncu = ncu;
            Bonus = bonus;
            Available = available;
            RequiredLevel = requiredLevel;
        }

        public string Name { get; }

        public int NanoId { get; }

        public int Quality { get; }

        public int Ncu { get; }

        public string Bonus { get; }

        public bool Available { get; }

        public int RequiredLevel { get; }
    }
}
