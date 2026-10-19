namespace ZoneEngine_New.Core.Missions
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Knubot;

    /// <summary>
    /// GameData/StatelLocks.json: locked statels the client spawns itself (a Terminal identity the server never
    /// registers, e.g. the Merchant's Strongbox on Arete Landing, Terminal:-1073014375). The client lets a Break and
    /// Enter item (Lock Pick 95577) be used on them and sends GenericCmd UseItemOnItem; this answers it: with the row's
    /// quest active, a pick rating at or above Difficulty and the player within Range of the row's X/Z, the quest is
    /// completed and the row's items are given. The pick is not used up.
    /// </summary>
    public static class StatelLocks
    {
        public const string FileName = "StatelLocks.json";

        public sealed class Row
        {
            public int Playfield { get; set; }
            public int Instance { get; set; }
            public string Name { get; set; } = "";
            public float X { get; set; }
            public float Z { get; set; }
            public float Range { get; set; } = 10f;
            public int Difficulty { get; set; }
            public string Quest { get; set; } = "";
            public string[] Picks { get; set; } = [];
            public SpawnRow[] Spawn { get; set; } = [];
            public string Text { get; set; } = "";
            public string NoQuestText { get; set; } = "";
        }

        public sealed class SpawnRow
        {
            public string Hash { get; set; } = "";
            public int Quality { get; set; } = 1;
            public int Count { get; set; } = 1;
        }

        static readonly ConcurrentDictionary<string, Dictionary<(int, int), Row>> Loaded = new();

        static Dictionary<(int, int), Row> For(string root) => Loaded.GetOrAdd(root, r =>
        {
            var map = new Dictionary<(int, int), Row>();
            string path = Path.Combine(r, FileName);
            if (!File.Exists(path))
                return map;
            Row[]? rows = JsonSerializer.Deserialize<Row[]>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip });
            foreach (Row row in rows ?? [])
                map[(row.Playfield, row.Instance)] = row;
            return map;
        });

        /// <summary>
        /// True when <paramref name="target"/> is a listed statel; <paramref name="opened"/> then says whether the pick worked
        /// (feedback already sent).
        /// </summary>
        public static bool TryPick(Player player, ZoneEngine_New.Core.Playfield.Playfield playfield, Identity slot, Identity target,
            Func<Player, Item, int> rating, out bool opened)
        {
            opened = false;
            if (target.Type != IdentityType.Terminal || playfield.GetService<IGameData>() is not IGameData gameData
                || !For(gameData.RootPath).TryGetValue((playfield.Identity.Instance, target.Instance), out Row? row))
                return false;

            if (slot.Type != IdentityType.Inventory || !player.Inventory.IsHydrated
                || !player.Inventory.TryGetItem(slot.Type, slot.Instance, out Item item) || !IsPick(gameData, row, item))
            {
                Say(player, "That won't open the " + row.Name + ". Try a Lock Pick.");
                return true;
            }

            float dx = player.Position.xf - row.X, dz = player.Position.zf - row.Z;
            if (dx * dx + dz * dz > row.Range * row.Range)
            {
                Say(player, "You are too far away from the " + row.Name + ".");
                return true;
            }

            Quests.QuestService? quests = playfield.GetService<Quests.QuestService>();
            HashItemMinter? minter = playfield.GetService<HashItemMinter>();
            InventoryFlushService? flush = playfield.GetService<InventoryFlushService>();
            if (quests == null || minter == null || flush == null)
                return true;

            if (row.Quest.Length != 0
                && !(quests.GetLog(player).Quests.TryGetValue(row.Quest, out Quests.PlayerQuest? quest) && quest.IsActive))
            {
                Say(player, row.NoQuestText.Length != 0 ? row.NoQuestText : "The " + row.Name + " is empty.");
                return true;
            }

            if (rating(player, item) < row.Difficulty)
            {
                Say(player, "Lockpicking failed.");
                return true;
            }

            int need = 0;
            foreach (SpawnRow spawn in row.Spawn)
                need += Math.Max(1, spawn.Count);
            if (KnubotItems.FreeSlots(player) < need)
            {
                Say(player, "Your inventory is full. Make some room first.");
                return true;
            }

            if (row.Quest.Length != 0 && !quests.TryComplete(player, row.Quest, out string result))
            {
                player.Logger.Warn("StatelLocks " + row.Name + ": " + result);
                return true;
            }

            foreach (SpawnRow spawn in row.Spawn)
                for (int i = 0; i < Math.Max(1, spawn.Count); i++)
                    if (minter.TryMint(spawn.Hash, Math.Max(1, spawn.Quality), ItemSource.Quest, out Item given))
                        KnubotItems.Give(player, given, flush);

            ClientFeedback.Send(player, ClientFeedback.LockpickingSuccessful);
            if (row.Text.Length != 0)
                Say(player, row.Text);
            player.Logger.Info("StatelLocks opened " + row.Name + " char=" + player.Identity.Instance + " quest=" + row.Quest);
            opened = true;
            return true;
        }

        static bool IsPick(IGameData gameData, Row row, Item item)
        {
            if (item.Can(CanFlags.BreakAndEnter))
                return true;
            foreach (string hash in row.Picks)
            {
                var leaves = new List<HashInstance>();
                if (!gameData.CanResolveItemHash(hash))
                    continue;
                gameData.CollectHashLeafInstances(hash, leaves);
                foreach (HashInstance leaf in leaves)
                    if (leaf.TemplateIds.Contains(item.LowId) || leaf.TemplateIds.Contains(item.HighId))
                        return true;
            }

            return false;
        }

        static void Say(Player player, string text) => ClientFeedback.SendFormatted(player, ClientFeedback.PlainText, text);
    }
}