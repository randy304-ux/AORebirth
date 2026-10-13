namespace ZoneEngine_New.Core.Claims
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text.Json;

    using AORebirth.Enums;

    using MySqlConnector;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Dungeon Dogs Claim Center. The Store window's Claim/Daily pages (account service) write pending rows
    /// into ddogs_claims for the character behind a claim code (ddogs_claim_codes). This service hands pending
    /// rows out when the player enters a playfield or types .claim. ddogs/store/claims.json is the catalog for
    /// the in-game .claim fallback.
    /// </summary>
    public sealed class ClaimService
    {
        const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        readonly IItemBuilder _items;
        readonly IItemTemplateCatalog _catalog;
        readonly InventoryFlushService _flush;
        readonly IZoneLogger _logger;
        readonly string _connection;

        public ClaimService(IItemBuilder items, IItemTemplateCatalog catalog, InventoryFlushService flush, IZoneLogger logger)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _flush = flush ?? throw new ArgumentNullException(nameof(flush));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connection = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public static string StoreDirectory =>
            Environment.GetEnvironmentVariable("AO_DDOGS_STORE_DIR") ?? @"I:\AO\src\ddogs\store";

        public sealed record CatalogItem(string Key, string Name, string Group, int LowId, int HighId, int Ql);

        public IReadOnlyList<CatalogItem> LoadCatalog()
        {
            var list = new List<CatalogItem>();
            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(StoreDirectory, "claims.json")));
                if (doc.RootElement.TryGetProperty("items", out JsonElement items))
                    foreach (JsonElement it in items.EnumerateArray())
                        list.Add(new CatalogItem(
                            Str(it, "key"), Str(it, "name"), Str(it, "group"),
                            Int(it, "lowId"), Int(it, "highId"), Int(it, "ql")));
            }
            catch (Exception ex)
            {
                _logger.Warn("Claim catalog load failed: " + ex.Message);
            }

            return list;

            static string Str(JsonElement e, string k) => e.TryGetProperty(k, out JsonElement v) ? v.GetString() ?? string.Empty : string.Empty;
            static int Int(JsonElement e, string k) => e.TryGetProperty(k, out JsonElement v) && v.TryGetInt32(out int i) ? i : 0;
        }

        /// <summary>The character's claim code for the Store pages; created on first use.</summary>
        public string GetOrCreateCode(int characterId)
        {
            using var db = new MySqlConnection(_connection);
            db.Open();
            for (int attempt = 0; attempt < 5; attempt++)
            {
                using (var get = new MySqlCommand("SELECT Code FROM ddogs_claim_codes WHERE CharId=@c", db))
                {
                    get.Parameters.AddWithValue("@c", characterId);
                    if (get.ExecuteScalar() is string existing)
                        return existing;
                }

                char[] code = new char[8];
                for (int i = 0; i < code.Length; i++)
                    code[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
                using var add = new MySqlCommand("INSERT IGNORE INTO ddogs_claim_codes (CharId, Code) VALUES (@c, @k)", db);
                add.Parameters.AddWithValue("@c", characterId);
                add.Parameters.AddWithValue("@k", new string(code));
                add.ExecuteNonQuery();
            }

            throw new InvalidOperationException("Could not create a claim code.");
        }

        public HashSet<string> ClaimedKeys(int characterId)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var db = new MySqlConnection(_connection);
            db.Open();
            using var cmd = new MySqlCommand("SELECT ItemKey FROM ddogs_claims WHERE CharId=@c", db);
            cmd.Parameters.AddWithValue("@c", characterId);
            using MySqlDataReader r = cmd.ExecuteReader();
            while (r.Read())
                set.Add(r.GetString(0));
            return set;
        }

        /// <summary>Queues a catalog item (once per character). False when already claimed.</summary>
        public bool Queue(int characterId, CatalogItem item, string source)
        {
            using var db = new MySqlConnection(_connection);
            db.Open();
            using var cmd = new MySqlCommand(
                "INSERT IGNORE INTO ddogs_claims (CharId,ItemKey,LowId,HighId,Quality,Source,Status) VALUES (@c,@k,@l,@h,@q,@s,0)", db);
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.Parameters.AddWithValue("@k", item.Key);
            cmd.Parameters.AddWithValue("@l", item.LowId);
            cmd.Parameters.AddWithValue("@h", item.HighId > 0 ? item.HighId : item.LowId);
            cmd.Parameters.AddWithValue("@q", item.Ql);
            cmd.Parameters.AddWithValue("@s", source);
            return cmd.ExecuteNonQuery() == 1;
        }

        /// <summary>Gives every pending claim that fits in the inventory. Never throws.</summary>
        public int DeliverPending(Player player)
        {
            try
            {
                return DeliverPendingCore(player);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, string.Format(CultureInfo.InvariantCulture, "Claim delivery failed character={0}", player.Identity.Instance));
                return 0;
            }
        }

        int DeliverPendingCore(Player player)
        {
            if (player.IsDead || !player.Inventory.IsHydrated)
                return 0;

            int characterId = player.Identity.Instance;
            var pending = new List<(int Id, string Key, int Low, int High, int Ql)>();
            using var db = new MySqlConnection(_connection);
            db.Open();
            using (var cmd = new MySqlCommand("SELECT Id, ItemKey, LowId, HighId, Quality FROM ddogs_claims WHERE CharId=@c AND Status=0 ORDER BY Id", db))
            {
                cmd.Parameters.AddWithValue("@c", characterId);
                using MySqlDataReader r = cmd.ExecuteReader();
                while (r.Read())
                    pending.Add((r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4)));
            }

            int delivered = 0;
            foreach (var row in pending)
            {
                if (!_catalog.TryGet(row.Low, out ItemTemplate template))
                {
                    _logger.Warn(string.Format(CultureInfo.InvariantCulture, "Claim {0} unknown item {1}; left pending", row.Id, row.Low));
                    continue;
                }

                int high = row.High > 0 ? row.High : row.Low;
                int ql = row.Ql > 0 ? row.Ql : Math.Max(1, template.Quality);

                // Mark first so a crash can never duplicate a claim; a failed add flips it back.
                using (var mark = new MySqlCommand("UPDATE ddogs_claims SET Status=1, DeliveredAt=NOW() WHERE Id=@i AND Status=0", db))
                {
                    mark.Parameters.AddWithValue("@i", row.Id);
                    if (mark.ExecuteNonQuery() != 1)
                        continue;
                }

                Item item = _items.CreateWithNewInstance(row.Low, high, ql, ItemSource.Other);
                if (!TryGive(player, item))
                {
                    using var undo = new MySqlCommand("UPDATE ddogs_claims SET Status=0, DeliveredAt=NULL WHERE Id=@i", db);
                    undo.Parameters.AddWithValue("@i", row.Id);
                    undo.ExecuteNonQuery();
                    Feedback(player, "Claim Center: your inventory is full. Make space and type .claim to receive the rest.");
                    break;
                }

                delivered++;
                _logger.Info(string.Format(CultureInfo.InvariantCulture, "Claim delivered character={0} key={1} item={2} ql={3}", characterId, row.Key, row.Low, ql));
                Feedback(player, string.Format(CultureInfo.InvariantCulture, "Claim Center: received {0}.", item.Name));
            }

            return delivered;
        }

        bool TryGive(Player player, Item item)
        {
            if (InventoryStacking.TryGrantOntoStack(player, item, _flush))
                return true;

            int slot = player.Inventory.Inventory.FindFreeSlot();
            if (slot < 0 || !player.Inventory.Inventory.Add(slot, item))
                return false;

            player.Inventory.MarkDirty(item, player.Inventory.Inventory, slot);
            _flush.NotifyDirty(player);
            player.Session?.Send(new AddTemplateMessage
            {
                Identity = player.Identity,
                HighId = item.HighId,
                LowId = item.LowId,
                Quality = item.Quality,
                Count = item.StackCount
            });
            return true;
        }

        static void Feedback(Player player, string text)
        {
            if (player.Session != null)
                Commands.GmCommandFeedback.Send(player.Session, player, text);
        }
    }
}
