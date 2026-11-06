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

        public string? TryPin(int characterId, int itemId)
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
            return string.IsNullOrEmpty(remapNote) ? null : ("Mishroll pin set to item "
                + itemId.ToString(CultureInfo.InvariantCulture)
                + (string.IsNullOrEmpty(name) ? string.Empty : (" (" + name + ")"))
                + "." + remapNote);
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
