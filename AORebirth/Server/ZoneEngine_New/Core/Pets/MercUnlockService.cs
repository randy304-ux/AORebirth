namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    using MySqlConnector;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Nanos;

    /// <summary>
    /// Per-character dungeon merc unlock state and summon gate. Reuses <see cref="PetService.HireMerc"/> /
    /// <see cref="PetService.DismissMerc"/>; does not replace profession contracts.
    /// Batch 2 hooks: <see cref="OnMissionAccepted"/> and <see cref="OnBossKilled"/>.
    /// </summary>
    public sealed class MercUnlockService
    {
        public const string StateLocked = "locked";
        public const string StateDungeonOnly = "dungeon_only";
        public const string StateUnlocked = "unlocked";
        public const string NtModeAoe = "aoe";
        public const string NtModeSt = "st";
        public const string NtModeSettingKey = "nt_mode";
        public const string TraderModeWrangle = "wrangle";
        public const string TraderModeUmbral = "umbral";
        public const string TraderModeSettingKey = "trader_mode";
        public const string MpModeComposite = "composite";
        public const string MpModeSingles = "singles";
        public const string MpModeSettingKey = "mp_mode";

        readonly MercRosterCatalog _roster;
        readonly MercProfessionNanos _nanos;
        readonly IZoneLogger _logger;
        readonly string _connection;
        readonly ConcurrentDictionary<int, Dictionary<string, MercUnlockState>> _cache = new();
        readonly ConcurrentDictionary<int, HashSet<string>> _summoned = new();
        readonly ConcurrentDictionary<int, string> _ntModeCache = new();
        readonly ConcurrentDictionary<int, string> _traderModeCache = new();
        readonly ConcurrentDictionary<int, string> _mpModeCache = new();

        public MercUnlockService(MercRosterCatalog roster, MercProfessionNanos nanos, IZoneLogger logger)
        {
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _nanos = nanos ?? throw new ArgumentNullException(nameof(nanos));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connection = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public MercRosterCatalog Roster => _roster;

        public MercProfessionNanos Nanos => _nanos;

        public MercUnlockState GetState(Player player, MercDefinition def)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(def);
            Dictionary<string, MercUnlockState> map = Load(player.Identity.Instance);
            // NT AOE/ST share one unlock group: any sibling row counts.
            MercUnlockState best = MercUnlockState.Locked;
            foreach (MercDefinition sibling in _roster.ByUnlockGroup(def.UnlockGroup))
            {
                if (map.TryGetValue(sibling.Id, out MercUnlockState state) && state > best)
                    best = state;
                if (map.TryGetValue(sibling.UnlockGroup, out MercUnlockState groupState) && groupState > best)
                    best = groupState;
            }

            return best;
        }

        public bool SetState(Player player, MercDefinition def, MercUnlockState state, out string detail)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(def);
            detail = string.Empty;
            if (state == MercUnlockState.Locked)
            {
                DeleteRows(player.Identity.Instance, def.UnlockGroup);
                Invalidate(player.Identity.Instance);
                detail = def.DisplayName + " set to locked.";
                return true;
            }

            string dbState = state == MercUnlockState.DungeonOnly ? StateDungeonOnly : StateUnlocked;
            // One row per unlock group member so NT AOE/ST both see the state.
            foreach (MercDefinition sibling in _roster.ByUnlockGroup(def.UnlockGroup))
                Upsert(player.Identity.Instance, sibling.Id, dbState);
            Invalidate(player.Identity.Instance);
            detail = def.DisplayName + " set to " + dbState + ".";
            return true;
        }

        /// <summary>Batch 2 hook: mission accepted → dungeon_only (summon only inside that dungeon).</summary>
        public void OnMissionAccepted(Player player, string unlockGroupOrMercId)
        {
            if (player == null || string.IsNullOrWhiteSpace(unlockGroupOrMercId))
                return;
            MercDefinition? def = _roster.Find(unlockGroupOrMercId)
                ?? FirstInGroup(unlockGroupOrMercId);
            if (def == null)
            {
                _logger.Warn("Merc mission-accepted hook: unknown id " + unlockGroupOrMercId);
                return;
            }

            MercUnlockState current = GetState(player, def);
            if (current == MercUnlockState.Unlocked)
                return;
            SetState(player, def, MercUnlockState.DungeonOnly, out _);
        }

        /// <summary>Batch 2 hook: end boss killed → unlocked (summon anywhere).</summary>
        public void OnBossKilled(Player player, string bossHashOrMercId)
        {
            if (player == null || string.IsNullOrWhiteSpace(bossHashOrMercId))
                return;
            MercDefinition? def = _roster.Find(bossHashOrMercId);
            if (def == null)
            {
                foreach (MercDefinition candidate in _roster.All)
                {
                    if (string.Equals(candidate.BossHash, bossHashOrMercId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(candidate.BossName, bossHashOrMercId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(candidate.UnlockGroup, bossHashOrMercId, StringComparison.OrdinalIgnoreCase))
                    {
                        def = candidate;
                        break;
                    }
                }
            }

            if (def == null)
            {
                _logger.Warn("Merc boss-killed hook: unknown id " + bossHashOrMercId);
                return;
            }

            SetState(player, def, MercUnlockState.Unlocked, out _);
        }

        /// <summary>
        /// Kill-credit unlock: match roster bosses by MonsterData template in a dungeon playfield,
        /// with BossName fallback when hash/template matching is unreliable (e.g. Commander Jocasta).
        /// </summary>
        public void OnNpcKilled(NpcCharacter npc, IReadOnlyList<Player> players)
        {
            if (npc == null || players == null || players.Count == 0)
                return;

            int playfieldId = npc.Playfield?.Identity.Instance ?? 0;
            int monsterData = npc.Stats.GetOrZero(SmokeLounge.AOtomation.Messaging.GameData.CharacterStat.MonsterData);
            string name = npc.Name ?? string.Empty;

            foreach (MercDefinition def in _roster.All)
            {
                if (!MatchesBossKill(def, playfieldId, monsterData, name))
                    continue;
                for (int i = 0; i < players.Count; i++)
                {
                    Player player = players[i];
                    if (player == null || player.IsDead)
                        continue;
                    if (GetState(player, def) == MercUnlockState.Unlocked)
                        continue;
                    SetState(player, def, MercUnlockState.Unlocked, out _);
                }
            }
        }

        static bool MatchesBossKill(MercDefinition def, int playfieldId, int monsterData, string npcName)
        {
            if (def.DungeonPlayfields.Count > 0)
            {
                bool inDungeon = false;
                foreach (int pf in def.DungeonPlayfields)
                {
                    if (pf == playfieldId)
                    {
                        inDungeon = true;
                        break;
                    }
                }

                if (!inDungeon)
                    return false;
            }

            if (def.BossMonsterData is int template && template > 0 && monsterData == template)
                return true;

            if (!string.IsNullOrWhiteSpace(def.BossHash)
                && !string.IsNullOrWhiteSpace(npcName)
                && string.Equals(def.BossHash, npcName, StringComparison.OrdinalIgnoreCase))
                return true;

            // Name fallback when NPCHash/template binding is missing or unreliable.
            if (!string.IsNullOrWhiteSpace(def.BossName)
                && !string.IsNullOrWhiteSpace(npcName)
                && (string.Equals(npcName, def.BossName, StringComparison.OrdinalIgnoreCase)
                    || npcName.Contains(def.BossName, StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        public bool CanSummonHere(Player player, MercDefinition def, MercUnlockState state, out string reason)
        {
            reason = string.Empty;
            if (state == MercUnlockState.Locked)
            {
                reason = "Locked — finish " + def.BossName + " in " + def.DungeonName + ".";
                return false;
            }

            if (state == MercUnlockState.Unlocked)
                return true;

            // dungeon_only
            int pf = player.Playfield?.Identity.Instance ?? 0;
            if (def.DungeonPlayfields.Count == 0)
            {
                reason = "Dungeon playfields TODO for " + def.DungeonName + " — cannot summon yet (dungeon_only).";
                return false;
            }

            foreach (int id in def.DungeonPlayfields)
            {
                if (id == pf)
                    return true;
            }

            reason = "Dungeon-only until the boss is dead. Summon inside " + def.DungeonName + ".";
            return false;
        }

        public string? Summon(Player player, MercDefinition def)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(def);
            MercUnlockState state = GetState(player, def);
            if (!CanSummonHere(player, def, state, out string reason))
                return reason;

            PetService? pets = player.Playfield?.GetService<PetService>();
            if (pets == null)
                return "Mercenaries are not available here.";

            pets.DismissMerc(player, def.Kind);
            ClearSummonedKind(player.Identity.Instance, def.Kind.Key);

            NpcCharacter? npc = pets.HireMerc(player, def.Kind, def.Level, Mercs.Tier.Basic);
            if (npc == null)
                return "Could not summon right now (mercenaries off, or you are dead or zoning).";

            HashSet<string> set = _summoned.GetOrAdd(player.Identity.Instance, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            set.RemoveWhere(id =>
            {
                MercDefinition? other = _roster.Find(id);
                return other != null && other.Kind.Key == def.Kind.Key;
            });
            set.Add(def.Id);
            ConfigureSummonedMerc(player, npc, def);
            return null;
        }

        /// <summary>Randy name + sex/appearance + NT/trader/MP filters after hire/revive/mode change.</summary>
        public void ConfigureSummonedMerc(Player owner, NpcCharacter npc, MercDefinition? def = null)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(npc);
            Mercs.Kind? kind = Mercs.ByHash(npc.Pet?.Hash);
            if (kind == null)
                return;

            def ??= FindSummonedDefinition(owner, kind) ?? _roster.All.FirstOrDefault(d => d.Kind.Key == kind.Key);
            if (def == null)
                return;

            ApplySummonIdentity(npc, def, owner);

            if (string.Equals(def.Kind.Key, "nanotechnician", StringComparison.OrdinalIgnoreCase))
            {
                string mode = GetNtMode(owner);
                _nanos.FilterNtNukes(
                    npc.UploadedNanoIds,
                    aoeMode: mode == NtModeAoe,
                    level: def.Level,
                    skillSource: npc);
                return;
            }

            if (string.Equals(def.Kind.Key, "trader", StringComparison.OrdinalIgnoreCase))
            {
                ApplyTraderModeNanos(npc, def, GetTraderMode(owner));
                return;
            }

            if (string.Equals(def.Kind.Key, "metaphysicist", StringComparison.OrdinalIgnoreCase))
            {
                ApplyMpModeNanos(npc, def, GetMpMode(owner));
            }
        }

        /// <summary>
        /// Pick sex (+ breed when RandomAppearance) once per summon body. Mode changes keep the
        /// existing sex/breed/head (inferred from the Randy name already on the body).
        /// </summary>
        void ApplySummonIdentity(NpcCharacter npc, MercDefinition def, Player owner)
        {
            // Keeper/Agent: no female Atrox in data → always male name + Atrox male look.
            bool forceMale = MercNameFormat.ForcesMaleAppearance(def);
            bool? inferred = MercNameFormat.TryInferFemale(npc.Name, def);
            bool female = forceMale ? false : inferred ?? (Random.Shared.Next(2) == 1);

            // First configure only (template name, not a Randy name yet). Mode changes keep breed/sex.
            if (inferred == null)
            {
                string root = owner.Playfield?.GetService<IGameData>()?.RootPath ?? string.Empty;
                // Default RandomAppearance=false → Randy fixed profession breeds.
                bool randomAppearance = root.Length > 0 && Mercs.RandomAppearance(root);
                MercNameFormat.ApplyAppearance(npc, def, female, randomAppearance);
            }

            string? modeTag = MercNameFormat.ModeTagFor(def, GetNtMode(owner), GetTraderMode(owner), GetMpMode(owner));
            npc.Name = MercNameFormat.FormatSummonName(def, female, modeTag);
        }

        public MercDefinition? FindSummonedDefinition(Player player, Mercs.Kind kind)
        {
            if (!_summoned.TryGetValue(player.Identity.Instance, out HashSet<string>? set))
                return null;
            foreach (string id in set)
            {
                MercDefinition? def = _roster.Find(id);
                if (def != null && def.Kind.Key == kind.Key)
                    return def;
            }

            return null;
        }

        public NpcCharacter? FindSummonedNpc(Player player, MercDefinition def)
        {
            foreach (NpcCharacter pet in player.OwnedPets.All)
            {
                if (pet.Pet == null || pet.IsDead)
                    continue;
                if (!string.Equals(pet.Pet.Hash, def.Kind.Hash, StringComparison.OrdinalIgnoreCase))
                    continue;
                return pet;
            }

            return null;
        }

        public string DisplayNameFor(Player player, MercDefinition def)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(def);
            NpcCharacter? npc = IsSummoned(player, def) ? FindSummonedNpc(player, def) : null;
            if (npc != null && !string.IsNullOrWhiteSpace(npc.Name))
                return npc.Name!;

            string? modeTag = MercNameFormat.ModeTagFor(def, GetNtMode(player), GetTraderMode(player), GetMpMode(player));
            // Unsummoned catalog label (nicks); mode tag only when the profession has one.
            if (string.IsNullOrWhiteSpace(modeTag))
                return def.DisplayName;
            string tagged = def.DisplayName + " (" + modeTag + ")";
            return tagged.Length <= MercNameFormat.MaxNameChars ? tagged : def.DisplayName;
        }

        public static string DisplayNameWithNtMode(MercDefinition def, string mode)
            => MercNameFormat.FormatSummonName(
                def,
                female: false,
                mode == NtModeSt ? "ST" : "AOE");

        public static string DisplayNameWithTraderMode(MercDefinition def, string mode)
            => MercNameFormat.FormatSummonName(
                def,
                female: false,
                mode == TraderModeUmbral ? "Umbral" : "Wrangle");

        public static string DisplayNameWithMpMode(MercDefinition def, string mode)
            => MercNameFormat.FormatSummonName(
                def,
                female: false,
                mode == MpModeSingles ? "Singles" : "Composite");

        public string GetNtMode(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            int id = player.Identity.Instance;
            if (_ntModeCache.TryGetValue(id, out string? cached))
                return cached;
            string mode = LoadSetting(id, NtModeSettingKey) ?? NtModeAoe;
            if (!mode.Equals(NtModeSt, StringComparison.OrdinalIgnoreCase))
                mode = NtModeAoe;
            else
                mode = NtModeSt;
            _ntModeCache[id] = mode;
            return mode;
        }

        public string SetNtMode(Player player, string modeText)
        {
            ArgumentNullException.ThrowIfNull(player);
            string mode = modeText.Trim().Equals(NtModeSt, StringComparison.OrdinalIgnoreCase) ? NtModeSt : NtModeAoe;
            SaveSetting(player.Identity.Instance, NtModeSettingKey, mode);
            _ntModeCache[player.Identity.Instance] = mode;

            MercDefinition? nt = _roster.Find("nt_sanctum");
            if (nt != null && IsSummoned(player, nt))
            {
                NpcCharacter? npc = FindSummonedNpc(player, nt);
                if (npc != null)
                {
                    // Keep non-nukes from the tier list; FilterNtNukes rebuilds AOE/ST from items.dat.
                    npc.UploadedNanoIds.Clear();
                    npc.UploadedNanoIds.AddRange(Mercs.TierNanosFor(nt.Kind, Mercs.Tier.Basic));
                    ConfigureSummonedMerc(player, npc, nt);
                }
            }

            return mode;
        }

        public string GetTraderMode(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            int id = player.Identity.Instance;
            if (_traderModeCache.TryGetValue(id, out string? cached))
                return cached;
            string mode = LoadSetting(id, TraderModeSettingKey) ?? TraderModeWrangle;
            mode = mode.Equals(TraderModeUmbral, StringComparison.OrdinalIgnoreCase)
                ? TraderModeUmbral
                : TraderModeWrangle;
            _traderModeCache[id] = mode;
            return mode;
        }

        public string GetMpMode(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            int id = player.Identity.Instance;
            if (_mpModeCache.TryGetValue(id, out string? cached))
                return cached;
            string mode = LoadSetting(id, MpModeSettingKey) ?? MpModeComposite;
            mode = mode.Equals(MpModeSingles, StringComparison.OrdinalIgnoreCase)
                ? MpModeSingles
                : MpModeComposite;
            _mpModeCache[id] = mode;
            return mode;
        }

        /// <summary>
        /// Persist trader wrangle mode, upload the chosen wrangle to a summoned Trader, and apply it to the owner.
        /// </summary>
        public string RunTraderPreset(Player player, string modeText)
        {
            ArgumentNullException.ThrowIfNull(player);
            bool umbral = modeText.Trim().Equals(TraderModeUmbral, StringComparison.OrdinalIgnoreCase);
            string mode = umbral ? TraderModeUmbral : TraderModeWrangle;
            MercDefinition? trader = _roster.All.FirstOrDefault(d => d.Kind.Key == "trader");
            if (trader == null)
                return "Trader roster entry missing.";

            MercProfessionNanos.MercBuffOffer? offer = umbral
                ? _nanos.BestUmbralWrangle(trader.Level)
                : _nanos.BestSkillWrangle(trader.Level);

            if (umbral && !_nanos.HasUmbralWrangleFamily())
                return "Umbral Wrangler family not found in items.dat — cannot set umbral mode.";
            if (offer == null)
                return umbral
                    ? "Umbral Wrangler family not found in items.dat."
                    : "Skill Wrangler (single-target) family not found in items.dat.";
            if (!offer.Value.Available)
            {
                return offer.Value.Name + " needs a higher level (requires "
                    + offer.Value.RequiredLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "; trader merc is L"
                    + trader.Level.ToString(System.Globalization.CultureInfo.InvariantCulture) + ").";
            }

            SaveSetting(player.Identity.Instance, TraderModeSettingKey, mode);
            _traderModeCache[player.Identity.Instance] = mode;

            NpcCharacter? npc = IsSummoned(player, trader) ? FindSummonedNpc(player, trader) : null;
            if (npc != null)
            {
                ApplyTraderModeNanos(npc, trader, mode);
                ApplySummonIdentity(npc, trader, player);
            }

            NanoRuntime.TryApplyServiceBuff(player, offer.Value.NanoId, announceRefusal: true, out string detail);
            return "Trader mode " + mode.ToUpperInvariant() + ": " + offer.Value.Name
                + " [id " + offer.Value.NanoId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]. "
                + detail;
        }

        /// <summary>
        /// Persist MP composite/singles mode, upload those nanos to a summoned MP, and apply them to the owner.
        /// </summary>
        public string RunMpPreset(Player player, string modeText)
        {
            ArgumentNullException.ThrowIfNull(player);
            bool singles = modeText.Trim().Equals(MpModeSingles, StringComparison.OrdinalIgnoreCase);
            string mode = singles ? MpModeSingles : MpModeComposite;
            MercDefinition? mp = _roster.All.FirstOrDefault(d => d.Kind.Key == "metaphysicist");
            if (mp == null)
                return "Meta-Physicist roster entry missing.";

            IReadOnlyList<MercProfessionNanos.MercBuffOffer> offers = singles
                ? _nanos.MpSinglesFor(mp.Level)
                : _nanos.MpCompositesFor(mp.Level);

            if (offers.Count == 0)
            {
                return singles
                    ? "No MP single nano families (Mocham's Gift / Infuse / Mastery / Teachings) found in items.dat."
                    : "No MP composite nano families (Composite Attribute / Nano Expertise / Mochams / …) found in items.dat.";
            }

            var available = new List<MercProfessionNanos.MercBuffOffer>();
            var blocked = new List<string>();
            foreach (MercProfessionNanos.MercBuffOffer offer in offers)
            {
                if (offer.Available)
                    available.Add(offer);
                else
                    blocked.Add(offer.Name + " needs L"
                        + offer.RequiredLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (available.Count == 0)
            {
                return "MP " + mode + " mode: none of the "
                    + offers.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " family ranks are usable at merc L"
                    + mp.Level.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ". Examples: " + string.Join("; ", blocked.Take(3));
            }

            SaveSetting(player.Identity.Instance, MpModeSettingKey, mode);
            _mpModeCache[player.Identity.Instance] = mode;

            NpcCharacter? npc = IsSummoned(player, mp) ? FindSummonedNpc(player, mp) : null;
            if (npc != null)
            {
                ApplyMpModeNanos(npc, mp, mode);
                ApplySummonIdentity(npc, mp, player);
            }

            int applied = 0;
            foreach (MercProfessionNanos.MercBuffOffer offer in available)
            {
                if (NanoRuntime.TryApplyServiceBuff(player, offer.NanoId, announceRefusal: false, out _))
                    applied++;
            }

            return "MP mode " + mode.ToUpperInvariant() + ": applied "
                + applied.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/"
                + available.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " nanos (" + string.Join(", ", available.Select(o => o.Name)) + ")"
                + (blocked.Count > 0
                    ? ". Skipped (level): " + string.Join("; ", blocked)
                    : ".");
        }

        void ApplyTraderModeNanos(NpcCharacter npc, MercDefinition def, string mode)
        {
            StripUploadedByName(npc, MercProfessionNanos.IsTraderWrangleName);
            bool umbral = mode == TraderModeUmbral;
            MercProfessionNanos.MercBuffOffer? offer = umbral
                ? _nanos.BestUmbralWrangle(def.Level, npc)
                : _nanos.BestSkillWrangle(def.Level, npc);
            if (offer == null || !offer.Value.Available)
                return;
            if (!npc.UploadedNanoIds.Contains(offer.Value.NanoId))
                npc.UploadedNanoIds.Add(offer.Value.NanoId);
        }

        void ApplyMpModeNanos(NpcCharacter npc, MercDefinition def, string mode)
        {
            StripUploadedByName(npc, MercProfessionNanos.IsMpPresetName);
            IReadOnlyList<MercProfessionNanos.MercBuffOffer> offers = mode == MpModeSingles
                ? _nanos.MpSinglesFor(def.Level, npc)
                : _nanos.MpCompositesFor(def.Level, npc);
            foreach (MercProfessionNanos.MercBuffOffer offer in offers)
            {
                if (!offer.Available)
                    continue;
                if (!npc.UploadedNanoIds.Contains(offer.NanoId))
                    npc.UploadedNanoIds.Add(offer.NanoId);
            }
        }

        void StripUploadedByName(NpcCharacter npc, Func<string?, bool> nameMatch)
        {
            for (int i = npc.UploadedNanoIds.Count - 1; i >= 0; i--)
            {
                ItemTemplate? template = _nanos.TryGetTemplate(npc.UploadedNanoIds[i]);
                if (template == null)
                    continue;
                if (nameMatch(template.Name))
                    npc.UploadedNanoIds.RemoveAt(i);
            }
        }

        /// <summary>Runs a Keeper aura on the summoned Keeper (one per NanoStrain slot).</summary>
        public string RunKeeperAura(Player player, string auraType)
        {
            ArgumentNullException.ThrowIfNull(player);
            MercDefinition? keeper = _roster.All.FirstOrDefault(d => d.Kind.Key == "keeper");
            if (keeper == null)
                return "Keeper roster entry missing.";
            if (!IsSummoned(player, keeper))
                return "Summon your Keeper first (.merc summon " + keeper.Id + ").";

            NpcCharacter? npc = FindSummonedNpc(player, keeper);
            if (npc == null)
                return "Keeper pet not found.";

            MercProfessionNanos.KeeperAuraOffer? offer = _nanos.BestKeeperAura(auraType, keeper.Level, npc);
            if (offer == null)
                return "Unknown Keeper aura type. Try: " + string.Join(", ", _nanos.KeeperAuraTypes());
            if (!offer.Value.Available)
                return offer.Value.Name + " needs a higher level (requires "
                    + offer.Value.RequiredLevel.ToString(System.Globalization.CultureInfo.InvariantCulture) + ").";

            ItemTemplate? template = _nanos.TryGetTemplate(offer.Value.NanoId);
            if (template == null)
                return "Aura nano " + offer.Value.NanoId + " is not in the item catalog.";

            NanoSpell spell = NanoSpell.From(template);
            // One aura per strain slot: drop any running buff on that strain first.
            if (spell.NanoStrain > 0)
                npc.RemoveBuffs(buff => buff.NanoStrain == spell.NanoStrain);

            DateTime now = DateTime.UtcNow;
            BuffApplyDecision decision = npc.TryApplyBuff(spell, npc.Identity, now, out Buff? applied, out _);
            if (applied == null)
                return "Could not apply " + offer.Value.Name + " (" + decision + ").";

            if (!npc.UploadedNanoIds.Contains(offer.Value.NanoId))
                npc.UploadedNanoIds.Add(offer.Value.NanoId);

            return "Keeper aura set to " + offer.Value.Name + " [" + offer.Value.Type + ", strain "
                + offer.Value.Strain.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].";
        }

        public string? Dismiss(Player player, MercDefinition? def)
        {
            ArgumentNullException.ThrowIfNull(player);
            PetService? pets = player.Playfield?.GetService<PetService>();
            if (pets == null)
                return "Mercenaries are not available here.";

            if (def != null)
            {
                bool had = pets.DismissMerc(player, def.Kind);
                if (_summoned.TryGetValue(player.Identity.Instance, out HashSet<string>? set))
                    set.Remove(def.Id);
                return had ? null : "You have no " + def.DisplayName + " summoned.";
            }

            if (!_summoned.TryGetValue(player.Identity.Instance, out HashSet<string>? active) || active.Count == 0)
            {
                // After a restart the in-memory summon set is empty: dismiss pets whose name matches a roster entry.
                bool any = false;
                foreach (MercDefinition row in _roster.All)
                {
                    foreach (NpcCharacter pet in player.OwnedPets.All.ToArray())
                    {
                        if (pet.Pet == null)
                            continue;
                        string petName = pet.Name ?? string.Empty;
                        if (!MercNameFormat.NameMatches(petName, row)
                            && !string.Equals(pet.Pet.Hash, row.Kind.Hash, StringComparison.OrdinalIgnoreCase))
                            continue;
                        pets.DismissMerc(player, row.Kind);
                        any = true;
                    }
                }

                return any ? null : "No dungeon merc summoned. Use .merc dismiss <name> or .merc dismiss all.";
            }

            foreach (string id in active.ToArray())
            {
                MercDefinition? row = _roster.Find(id);
                if (row != null)
                    pets.DismissMerc(player, row.Kind);
            }

            active.Clear();
            return null;
        }

        public IReadOnlyList<MercDefinition> SummonedFor(Player player)
        {
            if (!_summoned.TryGetValue(player.Identity.Instance, out HashSet<string>? set) || set.Count == 0)
                return [];
            var list = new List<MercDefinition>();
            foreach (string id in set)
            {
                MercDefinition? def = _roster.Find(id);
                if (def != null)
                    list.Add(def);
            }

            return list;
        }

        public bool IsSummoned(Player player, MercDefinition def)
        {
            return _summoned.TryGetValue(player.Identity.Instance, out HashSet<string>? set)
                && set.Contains(def.Id);
        }

        public static string StateLabel(MercUnlockState state) => state switch
        {
            MercUnlockState.DungeonOnly => StateDungeonOnly,
            MercUnlockState.Unlocked => StateUnlocked,
            _ => StateLocked
        };

        public static bool TryParseState(string text, out MercUnlockState state)
        {
            state = MercUnlockState.Locked;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            switch (text.Trim().ToLowerInvariant())
            {
                case "locked":
                case "lock":
                case "none":
                    state = MercUnlockState.Locked;
                    return true;
                case "dungeon_only":
                case "dungeon":
                case "dungeon-only":
                case "mission":
                    state = MercUnlockState.DungeonOnly;
                    return true;
                case "unlocked":
                case "unlock":
                case "open":
                    state = MercUnlockState.Unlocked;
                    return true;
                default:
                    return false;
            }
        }

        MercDefinition? FirstInGroup(string unlockGroup)
        {
            IReadOnlyList<MercDefinition> group = _roster.ByUnlockGroup(unlockGroup);
            return group.Count > 0 ? group[0] : null;
        }

        void ClearSummonedKind(int characterId, string professionKey)
        {
            if (!_summoned.TryGetValue(characterId, out HashSet<string>? set))
                return;
            set.RemoveWhere(id =>
            {
                MercDefinition? other = _roster.Find(id);
                return other != null && string.Equals(other.Kind.Key, professionKey, StringComparison.OrdinalIgnoreCase);
            });
        }

        void Invalidate(int characterId) => _cache.TryRemove(characterId, out _);

        Dictionary<string, MercUnlockState> Load(int characterId)
        {
            if (_cache.TryGetValue(characterId, out Dictionary<string, MercUnlockState>? hit))
                return hit;

            var map = new Dictionary<string, MercUnlockState>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var db = new MySqlConnection(_connection);
                db.Open();
                using var cmd = new MySqlCommand(
                    "SELECT MercId, State FROM character_merc_unlocks WHERE CharacterId=@id",
                    db);
                cmd.Parameters.AddWithValue("@id", characterId);
                using MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string mercId = reader.GetString(0);
                    string state = reader.GetString(1);
                    map[mercId] = state.Equals(StateUnlocked, StringComparison.OrdinalIgnoreCase)
                        ? MercUnlockState.Unlocked
                        : state.Equals(StateDungeonOnly, StringComparison.OrdinalIgnoreCase)
                            ? MercUnlockState.DungeonOnly
                            : MercUnlockState.Locked;
                }
            }
            catch (Exception exception)
            {
                // Table missing until deploy applies the migration — stay locked, do not crash commands.
                _logger.Warn("character_merc_unlocks read failed: " + exception.Message);
            }

            _cache[characterId] = map;
            return map;
        }

        void Upsert(int characterId, string mercId, string state)
        {
            try
            {
                using var db = new MySqlConnection(_connection);
                db.Open();
                using var cmd = new MySqlCommand(
                    "INSERT INTO character_merc_unlocks (CharacterId, MercId, State) VALUES (@c,@m,@s) "
                    + "ON DUPLICATE KEY UPDATE State=VALUES(State), UpdatedAtUtc=CURRENT_TIMESTAMP",
                    db);
                cmd.Parameters.AddWithValue("@c", characterId);
                cmd.Parameters.AddWithValue("@m", mercId);
                cmd.Parameters.AddWithValue("@s", state);
                cmd.ExecuteNonQuery();
            }
            catch (Exception exception)
            {
                _logger.Warn("character_merc_unlocks write failed: " + exception.Message);
            }
        }

        void DeleteRows(int characterId, string unlockGroup)
        {
            try
            {
                using var db = new MySqlConnection(_connection);
                db.Open();
                foreach (MercDefinition sibling in _roster.ByUnlockGroup(unlockGroup))
                {
                    using var cmd = new MySqlCommand(
                        "DELETE FROM character_merc_unlocks WHERE CharacterId=@c AND MercId=@m",
                        db);
                    cmd.Parameters.AddWithValue("@c", characterId);
                    cmd.Parameters.AddWithValue("@m", sibling.Id);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception exception)
            {
                _logger.Warn("character_merc_unlocks delete failed: " + exception.Message);
            }
        }

        string? LoadSetting(int characterId, string key)
        {
            try
            {
                using var db = new MySqlConnection(_connection);
                db.Open();
                using var cmd = new MySqlCommand(
                    "SELECT SettingValue FROM character_merc_settings WHERE CharacterId=@c AND SettingKey=@k LIMIT 1",
                    db);
                cmd.Parameters.AddWithValue("@c", characterId);
                cmd.Parameters.AddWithValue("@k", key);
                object? value = cmd.ExecuteScalar();
                return value?.ToString();
            }
            catch (Exception exception)
            {
                _logger.Warn("character_merc_settings read failed: " + exception.Message);
                return null;
            }
        }

        void SaveSetting(int characterId, string key, string value)
        {
            try
            {
                using var db = new MySqlConnection(_connection);
                db.Open();
                using var cmd = new MySqlCommand(
                    "INSERT INTO character_merc_settings (CharacterId, SettingKey, SettingValue) VALUES (@c,@k,@v) "
                    + "ON DUPLICATE KEY UPDATE SettingValue=VALUES(SettingValue), UpdatedAtUtc=CURRENT_TIMESTAMP",
                    db);
                cmd.Parameters.AddWithValue("@c", characterId);
                cmd.Parameters.AddWithValue("@k", key);
                cmd.Parameters.AddWithValue("@v", value);
                cmd.ExecuteNonQuery();
            }
            catch (Exception exception)
            {
                _logger.Warn("character_merc_settings write failed: " + exception.Message);
            }
        }
    }
}
