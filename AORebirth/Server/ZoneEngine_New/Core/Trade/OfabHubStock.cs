namespace ZoneEngine_New.Core.Trade
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Fills the Ofab vending machines that already stand in the Unicorn Defence Hub (playfield 6007).
    /// Stock is <c>GameData/OfabHubStock.json</c>, keyed by the machine's item template.
    /// The General Vendor (266579) carries general/HUD items plus every Ofab nano in one scrollable list.
    /// </summary>
    public static class OfabHubStock
    {
        static readonly object Gate = new();
        static Dictionary<int, ShopStockSlot[]>? _byTemplate;
        static bool _loaded;

        /// <summary>The hub's original Ofab General Vendor machine.</summary>
        public const int GeneralTemplateId = 266579;

        /// <summary>
        /// Configured Ofab panes can exceed the legacy 64-slot random-vendor band; the client scrolls.
        /// </summary>
        const int ConfiguredSlotCap = 512;

        public static void Apply(IGameData gameData, IZoneLogger logger, VendingMachine machine)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(machine);

            Dictionary<int, ShopStockSlot[]>? stock = Load(gameData, logger);
            if (stock == null || !stock.TryGetValue(machine.Template.Id, out ShopStockSlot[]? slots) || slots.Length == 0)
                return;

            machine.Stock.SetConfiguredSnapshot(slots);
            machine.SpendsVictoryPoints = true;
            machine.Stats.Set(SmokeLounge.AOtomation.Messaging.GameData.CharacterStat.SellModifier, 0);
            machine.Stats.Set(SmokeLounge.AOtomation.Messaging.GameData.CharacterStat.BuyModifier, 0);
        }

        static Dictionary<int, ShopStockSlot[]>? Load(IGameData gameData, IZoneLogger logger)
        {
            lock (Gate)
            {
                if (_loaded)
                    return _byTemplate;

                _loaded = true;
                string path = Path.Combine(gameData.RootPath, "OfabHubStock.json");
                if (!File.Exists(path))
                    return null;

                try
                {
                    FileBody? body = JsonSerializer.Deserialize<FileBody>(
                        File.ReadAllText(path),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    var map = new Dictionary<int, ShopStockSlot[]>();
                    if (body?.Vendors != null)
                    {
                        foreach (KeyValuePair<string, StockRow[]> pair in body.Vendors)
                        {
                            if (!int.TryParse(pair.Key, out int template) || pair.Value == null)
                                continue;

                            var slots = BuildSlots(pair.Value);
                            if (template == GeneralTemplateId && body.GeneralNanos is { Length: > 0 })
                            {
                                // Legacy files kept nanos in a side list for per-profession filtering.
                                // Bake them onto the General machine so one open shows everything.
                                var merged = new List<ShopStockSlot>(slots);
                                foreach (ShopStockSlot nano in BuildSlots(body.GeneralNanos))
                                {
                                    if (merged.Count >= ConfiguredSlotCap)
                                        break;
                                    merged.Add(nano);
                                }

                                slots = merged.ToArray();
                            }

                            if (slots.Length > 0)
                                map[template] = slots;
                        }
                    }

                    _byTemplate = map;
                    logger.Info("Ofab hub vendors stocked: " + map.Count
                        + (map.TryGetValue(GeneralTemplateId, out ShopStockSlot[]? general)
                            ? (" generalSlots=" + general.Length)
                            : string.Empty));
                }
                catch (Exception exception)
                {
                    logger.Warn("OfabHubStock.json unreadable: " + exception.Message);
                    _byTemplate = null;
                }

                return _byTemplate;
            }
        }

        static ShopStockSlot[] BuildSlots(StockRow[] rows)
        {
            var slots = new List<ShopStockSlot>(rows.Length);
            for (int i = 0; i < rows.Length && slots.Count < ConfiguredSlotCap; i++)
            {
                StockRow row = rows[i];
                if (row.Low <= 0 || row.High <= 0 || row.Ql <= 0)
                    continue;

                slots.Add(new ShopStockSlot(row.Low, row.High, row.Ql, explicitPrice: Math.Max(1, row.Price)));
            }

            return slots.ToArray();
        }

        sealed class FileBody
        {
            public Dictionary<string, StockRow[]>? Vendors { get; set; }

            public StockRow[]? GeneralNanos { get; set; }
        }

        sealed class StockRow
        {
            public int Low { get; set; }

            public int High { get; set; }

            public int Ql { get; set; }

            public int Price { get; set; }
        }
    }
}
