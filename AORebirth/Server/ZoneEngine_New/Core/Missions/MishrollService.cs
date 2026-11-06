namespace ZoneEngine_New.Core.Missions
{
    using System;
    using System.Collections.Concurrent;
    using System.Globalization;

    using MySqlConnector;

    using ZoneEngine_New.Core.Configuration;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Per-character preferred solo RK terminal reward pin for <c>.mishroll</c>.
    /// Validates pins against the mission reward pool; roll override and accept fee consume this pin.
    /// </summary>
    public sealed class MishrollService
    {
        readonly GameplaySettings _settings;
        readonly IZoneLogger _logger;
        readonly IItemTemplateCatalog _items;
        readonly string _connection;
        readonly ConcurrentDictionary<int, int?> _cache = new();

        public MishrollService(GameplaySettings settings, IZoneLogger logger, IItemTemplateCatalog items)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _connection = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public bool IsEnabled => _settings.Mishroll.Enabled;

        public GameplayMishroll Settings => _settings.Mishroll;

        public bool TryGetPinnedItemId(int characterId, out int itemId)
        {
            itemId = 0;
            if (characterId <= 0)
                return false;
            if (_cache.TryGetValue(characterId, out int? cached))
            {
                if (cached is not int pinned)
                    return false;
                itemId = pinned;
                return true;
            }

            int? loaded = Load(characterId);
            _cache[characterId] = loaded;
            if (loaded is not int value)
                return false;
            itemId = value;
            return true;
        }

        /// <summary>
        /// Pins <paramref name="itemId"/> for <paramref name="characterId"/>. Returns an error, or a success
        /// chat line (always non-null on success) that may include a QL-band warning for the player's level.
        /// </summary>
        public string? TryPin(int characterId, int itemId, int characterLevel = 0)
        {
            if (!IsEnabled)
                return "Mishroll is disabled on this server.";
            if (characterId <= 0)
                return "Mishroll pin is unavailable.";
            if (itemId <= 0)
                return "Usage: .mishroll <itemId> | .mishroll clear";

            string name;
            string? remapNote = null;
            if (!MissionRollRewardItems.TryFindCatalogItem(itemId, out name, out string reason))
            {
                if (!TryRemapToCatalogReward(itemId, out int remappedId, out name, out string remapReason))
                    return remapReason;
                itemId = remappedId;
                remapNote = " Remapped from your id to reward id "
                    + itemId.ToString(CultureInfo.InvariantCulture) + ".";
            }

            if (!Upsert(characterId, itemId))
                return "Could not save your mishroll pin (database unavailable).";
            _cache[characterId] = itemId;
            _logger.Info("Mishroll pin set char=" + characterId.ToString(CultureInfo.InvariantCulture)
                + " item=" + itemId.ToString(CultureInfo.InvariantCulture)
                + (string.IsNullOrEmpty(name) ? string.Empty : (" name=" + name)));

            string label = string.IsNullOrEmpty(name)
                ? ("item " + itemId.ToString(CultureInfo.InvariantCulture))
                : name;
            string message = "Mishroll pin set to " + label
                + " (" + itemId.ToString(CultureInfo.InvariantCulture) + ").";
            if (!string.IsNullOrEmpty(remapNote))
                message += remapNote;

            string? qlWarning = FormatQualityBandWarning(itemId, name, characterLevel);
            if (!string.IsNullOrEmpty(qlWarning))
                message += " " + qlWarning;
            return message;
        }

        /// <summary>
        /// When the pinned reward's catalog QL sits outside the mission QL band, warn them to change
        /// difficulty. Prefer <paramref name="missionQuality"/> (current terminal difficulty) when known;
        /// otherwise use the full Q0..Q10 band for <paramref name="characterLevel"/>. Catalog QL is compared
        /// as displayed (no nano-tolerance inflate): tolerance only decides roll eligibility at a concrete QL.
        /// </summary>
        public static string? FormatQualityBandWarning(int itemId, string? knownName, int characterLevel,
            int? missionQuality = null)
        {
            if (!MissionRollRewardItems.TryGetCatalogQuality(itemId, out string name, out int lowQl, out int highQl, out bool fixedQuality))
                return null;
            if (string.IsNullOrEmpty(knownName))
                knownName = name;

            int rangeMin;
            int rangeMax;
            if (missionQuality is int quality && quality > 0)
            {
                rangeMin = quality;
                rangeMax = quality;
            }
            else if (characterLevel > 0
                && ZoneEngine.Core.Missions.MissionLevelRuntime.TryGetQualityRange(characterLevel, out rangeMin, out rangeMax))
            {
                // keep rangeMin/rangeMax from TryGetQualityRange
            }
            else
                return null;

            // Pin warning compares the catalog QL band itself (what the player sees), not the widened
            // nano-tolerance window. Overlap against the widened window hid QL17 vs level-32 Easy..Hard.
            if (highQl >= rangeMin && lowQl <= rangeMax)
                return null;

            int displayQl = fixedQuality || lowQl == highQl ? lowQl : lowQl;
            string displayName = string.IsNullOrEmpty(knownName) ? ("item " + itemId.ToString(CultureInfo.InvariantCulture)) : knownName;
            string hint = highQl < rangeMin
                ? "so set difficulty lower to roll it."
                : "so set difficulty higher to roll it.";
            string band = rangeMin == rangeMax
                ? rangeMin.ToString(CultureInfo.InvariantCulture)
                : rangeMin.ToString(CultureInfo.InvariantCulture) + "-" + rangeMax.ToString(CultureInfo.InvariantCulture);
            return displayName + " is QL " + displayQl.ToString(CultureInfo.InvariantCulture)
                + "; your current mission QL range is " + band + ", " + hint;
        }

        /// <summary>
        /// Resolves a typed id or a case-insensitive partial reward/item name to a pin id.
        /// Ambiguous names return an error listing the top matches.
        /// </summary>
        public string? TryResolvePinArgument(string argument, out int itemId, out string? nameNote)
        {
            itemId = 0;
            nameNote = null;
            if (string.IsNullOrWhiteSpace(argument))
                return "Usage: .mishroll <itemId|name> | .mishroll clear";

            if (int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                && parsed > 0)
            {
                itemId = parsed;
                return null;
            }

            if (MissionRollRewardItems.TryFindCatalogItemsByPartialName(argument, out var matches, out _))
            {
                if (matches.Count == 1)
                {
                    itemId = matches[0].ItemId;
                    nameNote = matches[0].Name;
                    return null;
                }

                return FormatAmbiguousMatches(argument, matches);
            }

            // Catalog miss: treat the typed name as a nano program (or other item) and remap to its crystal.
            if (TryRemapPartialItemName(argument, out itemId, out string remappedName, out string? ambiguous))
            {
                if (ambiguous != null)
                    return ambiguous;
                nameNote = remappedName;
                return null;
            }

            return "No mission reward matches '" + argument.Trim() + "'.";
        }

        static string FormatAmbiguousMatches(string argument,
            System.Collections.Generic.IReadOnlyList<MissionRollRewardItems.CatalogNameMatch> matches)
        {
            const int listLimit = 8;
            var lines = new System.Collections.Generic.List<string>();
            int shown = Math.Min(listLimit, matches.Count);
            for (int i = 0; i < shown; i++)
                lines.Add(matches[i].Name + " (" + matches[i].ItemId.ToString(CultureInfo.InvariantCulture) + ")");
            string more = matches.Count > listLimit
                ? " … +" + (matches.Count - listLimit).ToString(CultureInfo.InvariantCulture) + " more"
                : string.Empty;
            return "Ambiguous mishroll name '" + argument.Trim() + "'. Matches: "
                + string.Join("; ", lines) + more + ". Use a fuller name or the item id.";
        }

        bool TryRemapPartialItemName(string argument, out int itemId, out string name, out string? ambiguous)
        {
            itemId = 0;
            name = string.Empty;
            ambiguous = null;
            // Prefer an exact crystal remap from a unique template name hit via catalog crystal pattern.
            string trimmed = argument.Trim();
            if (MissionRollRewardItems.TryFindCatalogItemByName("Nano Crystal (" + trimmed + ")", out itemId, out name, out _))
                return true;

            // Partial: collect catalog crystals whose inner nano name contains the query.
            if (!MissionRollRewardItems.TryFindCatalogItemsByPartialName("Nano Crystal (" + trimmed,
                    out var crystalPrefix, out _)
                && !MissionRollRewardItems.TryFindCatalogItemsByPartialName(trimmed, out crystalPrefix, out _))
                return false;

            // Filter to crystal rows when we used the raw partial (already filtered when prefix search hit).
            var crystals = new System.Collections.Generic.List<MissionRollRewardItems.CatalogNameMatch>();
            foreach (var match in crystalPrefix)
            {
                if (match.Name.StartsWith("Nano Crystal (", StringComparison.OrdinalIgnoreCase)
                    && match.Name.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                    crystals.Add(match);
            }

            if (crystals.Count == 0)
                return false;
            if (crystals.Count > 1)
            {
                ambiguous = FormatAmbiguousMatches(argument, crystals);
                return true;
            }

            itemId = crystals[0].ItemId;
            name = crystals[0].Name;
            return true;
        }

        public string? TryClear(int characterId)
        {
            if (!IsEnabled)
                return "Mishroll is disabled on this server.";
            if (characterId <= 0)
                return "Mishroll pin is unavailable.";
            if (!Delete(characterId))
                return "Could not clear your mishroll pin (database unavailable).";
            _cache[characterId] = null;
            return null;
        }

        public void Invalidate(int characterId) => _cache.TryRemove(characterId, out _);

        bool TryRemapToCatalogReward(int itemId, out int remappedId, out string name, out string reason)
        {
            remappedId = 0;
            name = string.Empty;
            reason = string.Empty;

            if (!_items.TryGet(itemId, out ItemTemplate template) || string.IsNullOrWhiteSpace(template.Name))
            {
                reason = "That item is not in the mission reward pool.";
                return false;
            }

            // Nano programs are not reward rows; their crystals are (Nano Crystal (Name)).
            string crystalName = "Nano Crystal (" + template.Name + ")";
            if (MissionRollRewardItems.TryFindCatalogItemByName(crystalName, out remappedId, out name, out _))
                return true;

            // Also accept pinning by either end of a LowId/HighId pair already covered by TryFindCatalogItem.
            reason = MissionRollRewardItems.FormatNotInPoolReason(itemId, template.Name);
            return false;
        }

        int? Load(int characterId)
        {
            try
            {
                using var db = new MySqlConnection(_connection);
                db.Open();
                using var cmd = new MySqlCommand(
                    "SELECT ItemId FROM character_mishroll WHERE CharacterId=@c LIMIT 1",
                    db);
                cmd.Parameters.AddWithValue("@c", characterId);
                object? value = cmd.ExecuteScalar();
                if (value == null || value is DBNull)
                    return null;
                int itemId = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                return itemId > 0 ? itemId : null;
            }
            catch (Exception exception)
            {
                // Table missing until deploy applies the migration — treat as no pin.
                _logger.Warn("character_mishroll read failed: " + exception.Message);
                return null;
            }
        }

        bool Upsert(int characterId, int itemId)
        {
            try
            {
                using var db = new MySqlConnection(_connection);
                db.Open();
                using var cmd = new MySqlCommand(
                    "INSERT INTO character_mishroll (CharacterId, ItemId) VALUES (@c,@i) "
                    + "ON DUPLICATE KEY UPDATE ItemId=VALUES(ItemId), UpdatedAtUtc=CURRENT_TIMESTAMP",
                    db);
                cmd.Parameters.AddWithValue("@c", characterId);
                cmd.Parameters.AddWithValue("@i", itemId);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception exception)
            {
                _logger.Warn("character_mishroll write failed: " + exception.Message);
                return false;
            }
        }

        bool Delete(int characterId)
        {
            try
            {
                using var db = new MySqlConnection(_connection);
                db.Open();
                using var cmd = new MySqlCommand(
                    "DELETE FROM character_mishroll WHERE CharacterId=@c",
                    db);
                cmd.Parameters.AddWithValue("@c", characterId);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception exception)
            {
                _logger.Warn("character_mishroll delete failed: " + exception.Message);
                return false;
            }
        }
    }
}
