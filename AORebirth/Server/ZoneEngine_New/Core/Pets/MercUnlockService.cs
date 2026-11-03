namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    using MySqlConnector;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;

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

        readonly MercRosterCatalog _roster;
        readonly IZoneLogger _logger;
        readonly string _connection;
        readonly ConcurrentDictionary<int, Dictionary<string, MercUnlockState>> _cache = new();
        readonly ConcurrentDictionary<int, HashSet<string>> _summoned = new();

        public MercUnlockService(MercRosterCatalog roster, IZoneLogger logger)
        {
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connection = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public MercRosterCatalog Roster => _roster;

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

            // One body per profession slot. NT AOE/ST replace each other.
            pets.DismissMerc(player, def.Kind);
            ClearSummonedKind(player.Identity.Instance, def.Kind.Key);

            NpcCharacter? npc = pets.HireMerc(player, def.Kind, def.Level, Mercs.Tier.Basic);
            if (npc == null)
                return "Could not summon right now (mercenaries off, or you are dead or zoning).";

            npc.Name = def.DisplayName;
            // TODO: filter UploadedNanoIds by nanoStyle (aoe/st) once AOE vs ST nano ids are classified.
            if (!string.IsNullOrEmpty(def.NanoStyle))
                _logger.Info("Merc " + def.Id + " summoned with nanoStyle=" + def.NanoStyle + " (AOE/ST filter TODO).");

            HashSet<string> set = _summoned.GetOrAdd(player.Identity.Instance, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            // Drop siblings of the same profession so NT mode is singular.
            set.RemoveWhere(id =>
            {
                MercDefinition? other = _roster.Find(id);
                return other != null && other.Kind.Key == def.Kind.Key;
            });
            set.Add(def.Id);
            return null;
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
                        if (pet.Pet == null || !string.Equals(pet.Name, row.DisplayName, StringComparison.OrdinalIgnoreCase))
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
    }
}
