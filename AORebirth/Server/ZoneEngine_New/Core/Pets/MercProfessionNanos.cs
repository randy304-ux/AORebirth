namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Bots;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Nanos;

    /// <summary>
    /// Builds merc castable buff / nuke lists from items.dat (never invents nano ids).
    /// Per profession: team or other-castable buffs, highest rank each line the merc's level/skills allow,
    /// falling back to the next lower rank.
    /// </summary>
    public sealed class MercProfessionNanos
    {
        readonly IItemTemplateCatalog _items;
        readonly IZoneLogger _logger;
        readonly object _gate = new();
        Dictionary<int, List<NanoRank>>? _byProfession;
        Dictionary<int, List<NanoRank>>? _nukesByProfession;
        List<KeeperAuraRank>? _keeperAuras;
        List<NanoRank>? _stWrangles;
        List<NanoRank>? _umbralWrangles;
        Dictionary<string, List<NanoRank>>? _mpCompositeLines;
        Dictionary<string, List<NanoRank>>? _mpSingleLines;

        public MercProfessionNanos(IItemTemplateCatalog items, IZoneLogger logger)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ItemTemplate? TryGetTemplate(int nanoId)
            => nanoId > 0 && _items.TryGet(nanoId, out ItemTemplate? template) ? template : null;

        public readonly record struct MercBuffOffer(int NanoId, string Name, int RequiredLevel, int Ncu, int Strain, bool Available);

        public readonly record struct KeeperAuraOffer(string Type, int NanoId, string Name, int Strain, int RequiredLevel, bool Available);

        public IReadOnlyList<MercBuffOffer> BuffsFor(Mercs.Kind kind, int level, Character? skillSource = null)
        {
            EnsureIndex();
            if (_byProfession == null || !_byProfession.TryGetValue(kind.Profession, out List<NanoRank>? ranks))
                return [];

            var byLine = new Dictionary<string, List<NanoRank>>(StringComparer.OrdinalIgnoreCase);
            foreach (NanoRank rank in ranks)
            {
                if (!byLine.TryGetValue(rank.LineKey, out List<NanoRank>? list))
                {
                    list = new List<NanoRank>();
                    byLine[rank.LineKey] = list;
                }

                list.Add(rank);
            }

            var offers = new List<MercBuffOffer>();
            foreach ((string _, List<NanoRank> line) in byLine)
            {
                line.Sort(CompareRanksDesc);
                NanoRank? chosen = null;
                NanoRank? fallback = null;
                foreach (NanoRank rank in line)
                {
                    fallback ??= rank;
                    if (CanUse(rank.Template, kind.Profession, level, skillSource))
                    {
                        chosen = rank;
                        break;
                    }
                }

                NanoRank pick = chosen ?? fallback!;
                bool available = chosen != null;
                offers.Add(new MercBuffOffer(
                    pick.Id,
                    pick.Name,
                    BuffbotCatalog.RequiredLevel(pick.Template),
                    NanoSpell.From(pick.Template).NcuCost,
                    pick.Strain,
                    available));
            }

            offers.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return offers;
        }

        public IReadOnlyList<KeeperAuraOffer> KeeperAurasFor(int level, Character? skillSource = null)
        {
            EnsureIndex();
            var best = new Dictionary<string, KeeperAuraRank>(StringComparer.OrdinalIgnoreCase);
            foreach (KeeperAuraRank rank in _keeperAuras ?? [])
            {
                if (!CanUse(rank.Template, (int)Profession.Keeper, level, skillSource))
                    continue;
                if (!best.TryGetValue(rank.Type, out KeeperAuraRank? current)
                    || CompareRanksDesc(rank.AsNanoRank(), current.AsNanoRank()) < 0)
                    best[rank.Type] = rank;
            }

            // Fallback: if nothing usable, expose the lowest rank per type greyed out.
            if (best.Count == 0)
            {
                foreach (KeeperAuraRank rank in _keeperAuras ?? [])
                {
                    if (!best.TryGetValue(rank.Type, out KeeperAuraRank? current)
                        || CompareRanksDesc(rank.AsNanoRank(), current.AsNanoRank()) > 0)
                        best[rank.Type] = rank;
                }
            }

            var list = new List<KeeperAuraOffer>();
            foreach ((string type, KeeperAuraRank rank) in best)
            {
                bool available = CanUse(rank.Template, (int)Profession.Keeper, level, skillSource);
                list.Add(new KeeperAuraOffer(
                    type,
                    rank.Id,
                    rank.Name,
                    rank.Strain,
                    BuffbotCatalog.RequiredLevel(rank.Template),
                    available));
            }

            list.Sort((a, b) => string.Compare(a.Type, b.Type, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        public KeeperAuraOffer? BestKeeperAura(string type, int level, Character? skillSource = null)
        {
            EnsureIndex();
            string key = NormalizeAuraType(type);
            if (key.Length == 0)
                return null;

            KeeperAuraRank? best = null;
            KeeperAuraRank? fallback = null;
            foreach (KeeperAuraRank rank in _keeperAuras ?? [])
            {
                if (!string.Equals(rank.Type, key, StringComparison.OrdinalIgnoreCase))
                    continue;
                fallback ??= rank;
                if (!CanUse(rank.Template, (int)Profession.Keeper, level, skillSource))
                    continue;
                if (best == null || CompareRanksDesc(rank.AsNanoRank(), best.AsNanoRank()) < 0)
                    best = rank;
            }

            KeeperAuraRank? pick = best ?? fallback;
            if (pick == null)
                return null;
            return new KeeperAuraOffer(
                pick.Type,
                pick.Id,
                pick.Name,
                pick.Strain,
                BuffbotCatalog.RequiredLevel(pick.Template),
                best != null);
        }

        public IReadOnlyList<string> KeeperAuraTypes()
        {
            EnsureIndex();
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeeperAuraRank rank in _keeperAuras ?? [])
                set.Add(rank.Type);
            return [.. set];
        }

        /// <summary>Highest single-target Skill Wrangler (not Team) the merc level allows.</summary>
        public MercBuffOffer? BestSkillWrangle(int level, Character? skillSource = null)
        {
            EnsureIndex();
            return BestFromLine(_stWrangles, (int)Profession.Trader, level, skillSource);
        }

        /// <summary>Highest Umbral Wrangler the merc level allows, or null if the family is missing.</summary>
        public MercBuffOffer? BestUmbralWrangle(int level, Character? skillSource = null)
        {
            EnsureIndex();
            if (_umbralWrangles == null || _umbralWrangles.Count == 0)
                return null;
            return BestFromLine(_umbralWrangles, (int)Profession.Trader, level, skillSource);
        }

        public bool HasUmbralWrangleFamily()
        {
            EnsureIndex();
            return _umbralWrangles != null && _umbralWrangles.Count > 0;
        }

        public IReadOnlyList<MercBuffOffer> MpCompositesFor(int level, Character? skillSource = null)
        {
            EnsureIndex();
            return BestPerLine(_mpCompositeLines, (int)Profession.Metaphysicist, level, skillSource);
        }

        public IReadOnlyList<MercBuffOffer> MpSinglesFor(int level, Character? skillSource = null)
        {
            EnsureIndex();
            return BestPerLine(_mpSingleLines, (int)Profession.Metaphysicist, level, skillSource);
        }

        public IReadOnlyList<string> MpCompositeFamilies()
        {
            EnsureIndex();
            if (_mpCompositeLines == null)
                return [];
            var list = new List<string>(_mpCompositeLines.Keys);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        public IReadOnlyList<string> MpSingleFamilies()
        {
            EnsureIndex();
            if (_mpSingleLines == null)
                return [];
            var list = new List<string>(_mpSingleLines.Keys);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        public static bool IsTraderWrangleName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            string n = name.Trim();
            return n.StartsWith("Skill Wrangler", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("Team Skill Wrangler", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("Umbral Wrangler", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMpPresetName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            return TryClassifyMpComposite(name.Trim(), out _)
                || TryClassifyMpSingle(name.Trim(), out _);
        }

        MercBuffOffer? BestFromLine(List<NanoRank>? ranks, int profession, int level, Character? skillSource)
        {
            if (ranks == null || ranks.Count == 0)
                return null;
            var sorted = new List<NanoRank>(ranks);
            sorted.Sort(CompareRanksDesc);
            NanoRank? chosen = null;
            foreach (NanoRank rank in sorted)
            {
                if (CanUse(rank.Template, profession, level, skillSource))
                {
                    chosen = rank;
                    break;
                }
            }

            NanoRank pick = chosen ?? sorted[sorted.Count - 1];
            return new MercBuffOffer(
                pick.Id,
                pick.Name,
                BuffbotCatalog.RequiredLevel(pick.Template),
                NanoSpell.From(pick.Template).NcuCost,
                pick.Strain,
                chosen != null);
        }

        IReadOnlyList<MercBuffOffer> BestPerLine(
            Dictionary<string, List<NanoRank>>? lines,
            int profession,
            int level,
            Character? skillSource)
        {
            if (lines == null || lines.Count == 0)
                return [];
            var offers = new List<MercBuffOffer>();
            foreach ((string _, List<NanoRank> ranks) in lines)
            {
                MercBuffOffer? offer = BestFromLine(ranks, profession, level, skillSource);
                if (offer != null)
                    offers.Add(offer.Value);
            }

            offers.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return offers;
        }

        public static bool IsAoeNuke(ItemTemplate template)
        {
            if (!template.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells) || spells == null)
                return false;
            foreach (ItemSpell spell in spells)
            {
                if (spell.FunctionType is (int)FunctionType.AreaCastNano or (int)FunctionType.AreaHit)
                    return true;
            }

            return false;
        }

        public static bool IsSingleTargetNuke(ItemTemplate template)
        {
            NanoSpell nano = NanoSpell.From(template);
            if (!nano.IsHostile || IsAoeNuke(template))
                return false;
            if (!template.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells) || spells == null)
                return false;
            foreach (ItemSpell spell in spells)
            {
                if (spell.FunctionType is (int)FunctionType.Hit or (int)FunctionType.DrainHit)
                    return true;
                // Direct cast of a child nuke still counts as ST delivery from this program.
                if (spell.FunctionType == (int)FunctionType.CastNano && !IsAoeNuke(template))
                    return true;
            }

            return false;
        }

        public static bool IsHostileNuke(ItemTemplate template)
        {
            NanoSpell nano = NanoSpell.From(template);
            return nano.IsHostile && (IsAoeNuke(template) || IsSingleTargetNuke(template));
        }

        /// <summary>
        /// Replace hostile nukes on the NT with the highest usable AOE or ST line ranks from items.dat.
        /// Non-nuke uploaded nanos are kept. Never invents nano ids.
        /// </summary>
        public void FilterNtNukes(IList<int> uploaded, bool aoeMode, int level = 0, Character? skillSource = null)
        {
            ArgumentNullException.ThrowIfNull(uploaded);
            EnsureIndex();

            // Drop existing hostile nukes; keep buffs / utility.
            for (int i = uploaded.Count - 1; i >= 0; i--)
            {
                if (!_items.TryGet(uploaded[i], out ItemTemplate? template) || template == null)
                    continue;
                if (IsHostileNuke(template))
                    uploaded.RemoveAt(i);
            }

            int useLevel = level > 0
                ? level
                : skillSource?.Stats.GetOrOne(CharacterStat.Level) ?? 1;
            foreach (int nanoId in SelectNukes((int)Profession.Nanotechnician, aoeMode, useLevel, skillSource))
            {
                if (!uploaded.Contains(nanoId))
                    uploaded.Add(nanoId);
            }
        }

        IReadOnlyList<int> SelectNukes(int profession, bool aoeMode, int level, Character? skillSource)
        {
            if (_nukesByProfession == null || !_nukesByProfession.TryGetValue(profession, out List<NanoRank>? ranks))
                return [];

            var byLine = new Dictionary<string, List<NanoRank>>(StringComparer.OrdinalIgnoreCase);
            foreach (NanoRank rank in ranks)
            {
                bool aoe = IsAoeNuke(rank.Template);
                if (aoeMode ? !aoe : aoe)
                    continue;
                if (!byLine.TryGetValue(rank.LineKey, out List<NanoRank>? list))
                {
                    list = new List<NanoRank>();
                    byLine[rank.LineKey] = list;
                }

                list.Add(rank);
            }

            var ids = new List<int>();
            foreach ((string _, List<NanoRank> line) in byLine)
            {
                line.Sort(CompareRanksDesc);
                NanoRank? chosen = null;
                foreach (NanoRank rank in line)
                {
                    if (CanUse(rank.Template, profession, level, skillSource))
                    {
                        chosen = rank;
                        break;
                    }
                }

                // Fallback to the lowest rank in the line so the merc still has something.
                chosen ??= line.Count > 0 ? line[line.Count - 1] : null;
                if (chosen != null)
                    ids.Add(chosen.Id);
            }

            return ids;
        }

        public static string NormalizeAuraType(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            string t = text.Trim().ToLowerInvariant();
            return t switch
            {
                "heal" or "ambient" or "hp" => "heal",
                "nano" or "tone" or "np" => "nano",
                "damage" or "vengeance" or "dmg" => "damage",
                "barrier" or "reflect" or "absorb" or "ams" => "barrier",
                "aao" or "imminence" or "aa" => "aao",
                "enervate" or "snare" => "enervate",
                "courage" or "fear" => "courage",
                "ward" or "benevolent" => "ward",
                _ => t
            };
        }

        void EnsureIndex()
        {
            if (_byProfession != null)
                return;
            lock (_gate)
            {
                if (_byProfession != null)
                    return;
                BuildIndex();
            }
        }

        void BuildIndex()
        {
            var byProfession = new Dictionary<int, List<NanoRank>>();
            var nukesByProfession = new Dictionary<int, List<NanoRank>>();
            var keeperAuras = new List<KeeperAuraRank>();
            var stWrangles = new List<NanoRank>();
            var umbralWrangles = new List<NanoRank>();
            var mpComposites = new Dictionary<string, List<NanoRank>>(StringComparer.OrdinalIgnoreCase);
            var mpSingles = new Dictionary<string, List<NanoRank>>(StringComparer.OrdinalIgnoreCase);
            int scanned = 0;
            foreach (ItemTemplate template in _items.All)
            {
                scanned++;
                if (template == null || string.IsNullOrWhiteSpace(template.Name))
                    continue;
                string name = template.Name.Trim();
                if (IsNonCastableNanoShell(name))
                    continue;

                NanoSpell spell = NanoSpell.From(template);
                if (TryClassifyKeeperAura(name, spell, out string auraType)
                    && (RequiresProfession(template, (int)Profession.Keeper) || LooksLikeKeeperAura(name)))
                {
                    keeperAuras.Add(new KeeperAuraRank(
                        auraType,
                        template.Id,
                        name,
                        spell.NanoStrain,
                        spell.StackingOrder,
                        template));
                }

                // Trader / MP preset families: index by exact items.dat name patterns (never invent ids).
                if (TryClassifyTraderWrangle(name, out bool umbral))
                {
                    var rank = new NanoRank(
                        template.Id,
                        name,
                        umbral ? "umbral" : "skill_wrangle",
                        spell.NanoStrain,
                        spell.StackingOrder,
                        template);
                    if (umbral)
                        umbralWrangles.Add(rank);
                    else
                        stWrangles.Add(rank);
                }

                if (TryClassifyMpComposite(name, out string compositeFamily))
                    AddPresetLine(mpComposites, compositeFamily, template, spell, name);
                if (TryClassifyMpSingle(name, out string singleFamily))
                    AddPresetLine(mpSingles, singleFamily, template, spell, name);

                List<int> professions = ProfessionRequirements(template);
                if (professions.Count == 0)
                    continue;

                if (IsHostileNuke(template))
                {
                    foreach (int profession in professions)
                    {
                        if (!nukesByProfession.TryGetValue(profession, out List<NanoRank>? nukeList))
                        {
                            nukeList = new List<NanoRank>();
                            nukesByProfession[profession] = nukeList;
                        }

                        nukeList.Add(new NanoRank(
                            template.Id,
                            name,
                            LineKey(spell, name),
                            spell.NanoStrain,
                            spell.StackingOrder,
                            template));
                    }

                    continue;
                }

                if (spell.IsHostile || !spell.IsBuff)
                    continue;
                if (!IsTeamOrOtherCastable(template, spell))
                    continue;

                foreach (int profession in professions)
                {
                    if (!byProfession.TryGetValue(profession, out List<NanoRank>? list))
                    {
                        list = new List<NanoRank>();
                        byProfession[profession] = list;
                    }

                    list.Add(new NanoRank(
                        template.Id,
                        name,
                        LineKey(spell, name),
                        spell.NanoStrain,
                        spell.StackingOrder,
                        template));
                }
            }

            _byProfession = byProfession;
            _nukesByProfession = nukesByProfession;
            _keeperAuras = keeperAuras;
            _stWrangles = stWrangles;
            _umbralWrangles = umbralWrangles;
            _mpCompositeLines = mpComposites;
            _mpSingleLines = mpSingles;
            _logger.Info(string.Format(
                CultureInfo.InvariantCulture,
                "MercProfessionNanos indexed templates={0} buffProfessions={1} nukeProfessions={2} keeperAuras={3} stWrangles={4} umbral={5} mpCompositeLines={6} mpSingleLines={7}",
                scanned,
                byProfession.Count,
                nukesByProfession.Count,
                keeperAuras.Count,
                stWrangles.Count,
                umbralWrangles.Count,
                mpComposites.Count,
                mpSingles.Count));
        }

        static void AddPresetLine(
            Dictionary<string, List<NanoRank>> map,
            string family,
            ItemTemplate template,
            NanoSpell spell,
            string name)
        {
            if (!map.TryGetValue(family, out List<NanoRank>? list))
            {
                list = new List<NanoRank>();
                map[family] = list;
            }

            list.Add(new NanoRank(
                template.Id,
                name,
                family,
                spell.NanoStrain,
                spell.StackingOrder,
                template));
        }

        static bool IsNonCastableNanoShell(string name)
            => name.StartsWith("Nano Crystal", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("NanoCrystal", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Compiled Algorithm", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Instruction Disc", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Programmed Photon Particle Emitter", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Nano Can", StringComparison.OrdinalIgnoreCase);

        /// <summary>Single-target Skill Wrangler only (excludes Team Skill Wrangler).</summary>
        static bool TryClassifyTraderWrangle(string name, out bool umbral)
        {
            umbral = false;
            if (name.StartsWith("Umbral Wrangler", StringComparison.OrdinalIgnoreCase))
            {
                umbral = true;
                return true;
            }

            if (name.StartsWith("Team Skill Wrangler", StringComparison.OrdinalIgnoreCase))
                return false;
            return name.StartsWith("Skill Wrangler", StringComparison.OrdinalIgnoreCase);
        }

        static bool TryClassifyMpComposite(string name, out string family)
        {
            family = string.Empty;
            if (name.StartsWith("Composite Mochams", StringComparison.OrdinalIgnoreCase))
            {
                family = "Composite Mochams";
                return true;
            }

            if (name.Equals("Composite Infuse With Knowledge", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Composite Infuse with Knowledge", StringComparison.OrdinalIgnoreCase))
            {
                family = "Composite Infuse With Knowledge";
                return true;
            }

            if (name.Equals("Composite Mastery", StringComparison.OrdinalIgnoreCase))
            {
                family = "Composite Mastery";
                return true;
            }

            if (name.Equals("Composite Teachings", StringComparison.OrdinalIgnoreCase))
            {
                family = "Composite Teachings";
                return true;
            }

            // Generic composite expertise packages (Buffbot Generic / "and so on").
            if (name.StartsWith("Composite Attribute Boost", StringComparison.OrdinalIgnoreCase))
            {
                family = "Composite Attribute Boost";
                return true;
            }

            if (name.StartsWith("Composite Nano Expertise", StringComparison.OrdinalIgnoreCase))
            {
                family = "Composite Nano Expertise";
                return true;
            }

            if (name.StartsWith("Composite Utility Expertise", StringComparison.OrdinalIgnoreCase))
            {
                family = "Composite Utility Expertise";
                return true;
            }

            if (name.StartsWith("Composite Tradeskill Expertise", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Special", StringComparison.OrdinalIgnoreCase))
            {
                family = "Composite Tradeskill Expertise";
                return true;
            }

            if (name.StartsWith("Composite Martial Prowess Expertise", StringComparison.OrdinalIgnoreCase))
            {
                family = "Composite Martial Prowess Expertise";
                return true;
            }

            if (name.StartsWith("Composite Ranged Special Expertise", StringComparison.OrdinalIgnoreCase))
            {
                family = "Composite Ranged Special Expertise";
                return true;
            }

            if (name.StartsWith("Composite Ranged Expertise", StringComparison.OrdinalIgnoreCase))
            {
                family = "Composite Ranged Expertise";
                return true;
            }

            if (name.StartsWith("Composite Melee Expertise", StringComparison.OrdinalIgnoreCase))
            {
                family = "Composite Melee Expertise";
                return true;
            }

            if (name.StartsWith("Composite Physical Special Expertise", StringComparison.OrdinalIgnoreCase))
            {
                family = "Composite Physical Special Expertise";
                return true;
            }

            return false;
        }

        static bool TryClassifyMpSingle(string name, out string family)
        {
            family = string.Empty;
            if (name.StartsWith("Mocham's Gift:", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Mocham\u0027s Gift:", StringComparison.OrdinalIgnoreCase))
            {
                family = name;
                return true;
            }

            if (name.StartsWith("Infuse With Knowledge:", StringComparison.OrdinalIgnoreCase))
            {
                family = name;
                return true;
            }

            if (name.Equals("BioMet Mastery", StringComparison.OrdinalIgnoreCase)
                || name.Equals("MatCrea Mastery", StringComparison.OrdinalIgnoreCase)
                || name.Equals("SpaceTime Mastery", StringComparison.OrdinalIgnoreCase)
                || name.Equals("MatMet Mastery", StringComparison.OrdinalIgnoreCase)
                || name.Equals("PsyMod Mastery", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Sense Imp Mastery", StringComparison.OrdinalIgnoreCase))
            {
                family = name;
                return true;
            }

            if (name.StartsWith("Teachings of ", StringComparison.OrdinalIgnoreCase)
                && !name.StartsWith("Teachings of the Immortal", StringComparison.OrdinalIgnoreCase))
            {
                family = name;
                return true;
            }

            return false;
        }

        static bool LooksLikeKeeperAura(string name)
            => name.StartsWith("Ambient ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Adaptive Ambient ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Tone of ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Adaptive Tone of ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Vengeance of the ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Improved Vengeance of the ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Barrier of the ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Imminence of ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Enervate ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Improved Enervate ", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Courage Of The Just", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Benevolent Ward", StringComparison.OrdinalIgnoreCase);

        static bool TryClassifyKeeperAura(string name, NanoSpell spell, out string type)
        {
            type = string.Empty;
            if (name.Equals("Courage Of The Just", StringComparison.OrdinalIgnoreCase)
                || (spell.NanoStrain == 900 && name.Contains("Courage", StringComparison.OrdinalIgnoreCase)))
            {
                type = "courage";
                return true;
            }

            if (name.Equals("Benevolent Ward", StringComparison.OrdinalIgnoreCase))
            {
                type = "ward";
                return true;
            }

            if (name.StartsWith("Imminence of ", StringComparison.OrdinalIgnoreCase))
            {
                type = "aao";
                return true;
            }

            if (name.StartsWith("Barrier of the ", StringComparison.OrdinalIgnoreCase))
            {
                type = "barrier";
                return true;
            }

            if (name.StartsWith("Enervate ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Improved Enervate ", StringComparison.OrdinalIgnoreCase))
            {
                type = "enervate";
                return true;
            }

            if (name.StartsWith("Vengeance of the ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Improved Vengeance of the ", StringComparison.OrdinalIgnoreCase))
            {
                type = "damage";
                return true;
            }

            if (name.StartsWith("Tone of ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Adaptive Tone of ", StringComparison.OrdinalIgnoreCase))
            {
                type = "nano";
                return true;
            }

            if (name.StartsWith("Ambient ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Adaptive Ambient ", StringComparison.OrdinalIgnoreCase))
            {
                type = "heal";
                return true;
            }

            return false;
        }

        static bool IsTeamOrOtherCastable(ItemTemplate template, NanoSpell spell)
        {
            if (HasFunction(template, FunctionType.TeamCastNano))
                return true;
            if (spell.IsSelfOnly)
                return false;
            if (!template.Stats.TryGetValue(CharacterStat.Can, out int canRaw) || canRaw == 0)
                return true;
            return ((CanFlags)(uint)canRaw & CanFlags.ApplyOnFriendly) != 0;
        }

        static bool HasFunction(ItemTemplate template, FunctionType type)
        {
            if (!template.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells) || spells == null)
                return false;
            foreach (ItemSpell spell in spells)
            {
                if (spell.FunctionType == (int)type)
                    return true;
            }

            return false;
        }

        static string LineKey(NanoSpell spell, string name)
        {
            if (spell.NanoStrain > 0)
                return "strain:" + spell.NanoStrain.ToString(CultureInfo.InvariantCulture);
            // Strainless: keep exact name so Improved X does not collide with unrelated lines.
            return "name:" + name;
        }

        static List<int> ProfessionRequirements(ItemTemplate template)
        {
            var list = new List<int>();
            foreach (ItemAction action in template.Actions)
            {
                if (action.ActionType != (int)ActionType.ToUse)
                    continue;
                foreach (ItemRequirement requirement in action.Requirements)
                {
                    if (requirement.StatNumber is not ((int)CharacterStat.Profession or (int)CharacterStat.VisualProfession))
                        continue;
                    if ((Operator)requirement.Operator is not (Operator.EqualTo or Operator.GreaterThan))
                        continue;
                    int profession = requirement.Value;
                    if (profession > 0 && !list.Contains(profession))
                        list.Add(profession);
                }
            }

            return list;
        }

        static bool RequiresProfession(ItemTemplate template, int profession)
        {
            foreach (int id in ProfessionRequirements(template))
            {
                if (id == profession)
                    return true;
            }

            return false;
        }

        static bool CanUse(ItemTemplate template, int profession, int level, Character? skillSource)
        {
            if (BuffbotCatalog.RequiredLevel(template) > level)
                return false;

            int GetStat(CharacterStat stat)
            {
                if (stat is CharacterStat.Level or CharacterStat.TitleLevel)
                    return level;
                if (stat is CharacterStat.Profession or CharacterStat.VisualProfession)
                    return profession;
                if (skillSource != null)
                    return skillSource.Stats.GetOrZero(stat);
                // No live body yet: approximate profession skills from level so higher ranks unlock by level.
                return Math.Clamp(level * 5, 1, 1200);
            }

            try
            {
                return template.MeetsActionRequirements(GetStat, ActionType.ToUse);
            }
            catch
            {
                return BuffbotCatalog.RequiredLevel(template) <= level;
            }
        }

        static int CompareRanksDesc(NanoRank a, NanoRank b)
        {
            int stack = b.StackingOrder.CompareTo(a.StackingOrder);
            if (stack != 0)
                return stack;
            int level = BuffbotCatalog.RequiredLevel(b.Template).CompareTo(BuffbotCatalog.RequiredLevel(a.Template));
            if (level != 0)
                return level;
            return b.Template.Quality.CompareTo(a.Template.Quality);
        }

        sealed class NanoRank
        {
            public NanoRank(int id, string name, string lineKey, int strain, int stackingOrder, ItemTemplate template)
            {
                Id = id;
                Name = name;
                LineKey = lineKey;
                Strain = strain;
                StackingOrder = stackingOrder;
                Template = template;
            }

            public int Id { get; }
            public string Name { get; }
            public string LineKey { get; }
            public int Strain { get; }
            public int StackingOrder { get; }
            public ItemTemplate Template { get; }
        }

        sealed class KeeperAuraRank
        {
            public KeeperAuraRank(string type, int id, string name, int strain, int stackingOrder, ItemTemplate template)
            {
                Type = type;
                Id = id;
                Name = name;
                Strain = strain;
                StackingOrder = stackingOrder;
                Template = template;
            }

            public string Type { get; }
            public int Id { get; }
            public string Name { get; }
            public int Strain { get; }
            public int StackingOrder { get; }
            public ItemTemplate Template { get; }

            public NanoRank AsNanoRank() => new(Id, Name, Type, Strain, StackingOrder, Template);
        }
    }
}
