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
        List<KeeperAuraRank>? _keeperAuras;

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

        /// <summary>Filter uploaded merc nanos: keep non-nukes; keep only AOE or only ST nukes.</summary>
        public void FilterNtNukes(IList<int> uploaded, bool aoeMode)
        {
            ArgumentNullException.ThrowIfNull(uploaded);
            for (int i = uploaded.Count - 1; i >= 0; i--)
            {
                if (!_items.TryGet(uploaded[i], out ItemTemplate? template) || template == null)
                    continue;
                if (!IsHostileNuke(template))
                    continue;
                bool aoe = IsAoeNuke(template);
                if (aoeMode ? !aoe : aoe)
                    uploaded.RemoveAt(i);
            }
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
            var keeperAuras = new List<KeeperAuraRank>();
            int scanned = 0;
            foreach (ItemTemplate template in _items.All)
            {
                scanned++;
                if (template == null || string.IsNullOrWhiteSpace(template.Name))
                    continue;
                string name = template.Name.Trim();
                if (name.StartsWith("Nano Crystal", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("NanoCrystal", StringComparison.OrdinalIgnoreCase))
                    continue;

                NanoSpell spell = NanoSpell.From(template);
                if (TryClassifyKeeperAura(name, spell, out string auraType))
                {
                    if (!RequiresProfession(template, (int)Profession.Keeper) && !LooksLikeKeeperAura(name))
                    {
                        // Still accept when name is a known Keeper aura family found in items.dat.
                    }

                    keeperAuras.Add(new KeeperAuraRank(
                        auraType,
                        template.Id,
                        name,
                        spell.NanoStrain,
                        spell.StackingOrder,
                        template));
                }

                if (spell.IsHostile || !spell.IsBuff)
                    continue;
                if (!IsTeamOrOtherCastable(template, spell))
                    continue;

                foreach (int profession in ProfessionRequirements(template))
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
            _keeperAuras = keeperAuras;
            _logger.Info(string.Format(
                CultureInfo.InvariantCulture,
                "MercProfessionNanos indexed templates={0} professions={1} keeperAuras={2}",
                scanned,
                byProfession.Count,
                keeperAuras.Count));
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
