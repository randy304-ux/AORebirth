namespace ZoneEngine_New.Core.Battlestations
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// Right-click upgrade for the Ofab research attunement devices. The families, the tier order,
    /// and the level required to upgrade are <c>GameData/OfabAttunement.json</c>. Level three has
    /// no next tier, so a click falls through to the item's own (empty) use.
    /// </summary>
    public static class AttunementDevices
    {
        static readonly object Gate = new();
        static Dictionary<int, Step>? _steps;
        static bool _failed;

        public static bool TryUpgrade(Item item, Player player, Identity slotIdentity, IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(items);

            Step? step = Find(player, item.LowId);
            if (step == null)
                return false;

            int level = player.Stats.GetOrOne(CharacterStat.Level);
            if (level < step.Level)
            {
                Say(player, step.Name + " level " + (step.Tier + 1).ToString(CultureInfo.InvariantCulture)
                    + " needs level " + step.Level.ToString(CultureInfo.InvariantCulture) + ".");
                return true;
            }

            if (player.Playfield == null || player.Session == null)
                return true;

            Item next = items.CreateWithNewInstance(step.NextId, step.NextId, item.Quality, ItemSource.Other);
            if (!item.DestroyOne(player, slotIdentity))
            {
                Say(player, "The device could not be replaced.");
                return true;
            }

            if (!player.Inventory.TryPlace(next, out Container page, out int slot))
            {
                Say(player, "No room for the upgraded device. The old one is gone; tell a GM.");
                return true;
            }

            player.Inventory.MarkDirty(next, page, slot);
            player.Playfield.GetService<InventoryFlushService>()?.NotifyDirty(player);
            player.Session.Send(new AddTemplateMessage
            {
                Identity = player.Identity,
                HighId = next.HighId,
                LowId = next.LowId,
                Quality = next.Quality,
                Count = next.StackCount
            });
            Say(player, step.Name + " upgraded.");
            return true;
        }

        static Step? Find(Player player, int itemId)
        {
            Dictionary<int, Step>? steps = Steps(player);
            if (steps == null)
                return null;

            return steps.TryGetValue(itemId, out Step? step) ? step : null;
        }

        static Dictionary<int, Step>? Steps(Player player)
        {
            lock (Gate)
            {
                if (_steps != null || _failed)
                    return _steps;

                IGameData? data = player.Playfield?.GetService<IGameData>();
                if (data == null)
                    return null;

                try
                {
                    string path = Path.Combine(data.RootPath, "OfabAttunement.json");
                    Table? table = JsonSerializer.Deserialize<Table>(
                        File.ReadAllText(path),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    var map = new Dictionary<int, Step>();
                    if (table?.Families != null)
                    {
                        for (int f = 0; f < table.Families.Length; f++)
                        {
                            Family family = table.Families[f];
                            if (family.Ids == null)
                                continue;

                            for (int i = 0; i + 1 < family.Ids.Length; i++)
                            {
                                int level = table.Levels != null && i + 1 < table.Levels.Length
                                    ? table.Levels[i + 1]
                                    : 1;
                                map[family.Ids[i]] = new Step(family.Name ?? "Attunement", i + 1, family.Ids[i + 1], level);
                            }
                        }
                    }

                    _steps = map;
                }
                catch (Exception)
                {
                    _failed = true;
                    _steps = null;
                }

                return _steps;
            }
        }

        static void Say(Player player, string text)
        {
            player.Session?.Send(new ChatTextMessage { Identity = player.Identity, Text = text });
        }

        sealed class Step
        {
            public Step(string name, int tier, int nextId, int level)
            {
                Name = name;
                Tier = tier;
                NextId = nextId;
                Level = level;
            }

            public string Name { get; }

            public int Tier { get; }

            public int NextId { get; }

            public int Level { get; }
        }

        sealed class Table
        {
            public int[]? Levels { get; set; }

            public Family[]? Families { get; set; }
        }

        sealed class Family
        {
            public string? Name { get; set; }

            public int[]? Ids { get; set; }
        }
    }
}
