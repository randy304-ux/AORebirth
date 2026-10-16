namespace ZoneEngine_New.Core.Tradeskills
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;

    /// <summary>One source + target combine from GameData/Tradeskills.json (plus Tradeskills.*.json overlays).</summary>
    public sealed class TradeskillRecipe
    {
        public string Name { get; set; } = string.Empty;
        public int[] Source { get; set; } = Array.Empty<int>();
        public int[] Target { get; set; } = Array.Empty<int>();
        /// <summary>Result low/high ids; the result QL is the target's QL, clamped to the result's QL range by the item builder.</summary>
        public int[] Result { get; set; } = Array.Empty<int>();
        public bool DeleteSource { get; set; }
        public bool DeleteTarget { get; set; } = true;
        /// <summary>Source QL must be at least this percent of the target QL (alien armor 80% rule); 0 = any.</summary>
        public int MinSourcePct { get; set; }
        /// <summary>[stat id, percent of target QL] pairs, e.g. [126, 450] = EE at 4.5x QL.</summary>
        public int[][] Skills { get; set; } = Array.Empty<int[]>();
    }

    /// <summary>
    /// Tradeskill window: the client names the source and target slots (TradeskillSourceChanged/TargetChanged,
    /// Parameter1 = container type, Parameter2 = slot), then presses Build. Replies follow the CellAO layout
    /// (TradeskillSource/Target with the process count, TradeskillRequirement per skill, TradeskillResult with the
    /// QL range); not yet checked against a live capture. Only main-inventory items take part.
    /// </summary>
    public sealed class TradeskillService
    {
        readonly IItemBuilder _items;
        readonly InventoryActionService _actions;
        readonly InventoryFlushService _flush;
        readonly IZoneLogger _logger;
        readonly List<TradeskillRecipe> _recipes = new();
        readonly ConcurrentDictionary<int, (Identity Source, Identity Target)> _windows = new();

        public TradeskillService(IGameData gameData, IItemBuilder items, InventoryActionService actions,
            InventoryFlushService flush, IZoneLogger logger)
        {
            _items = items;
            _actions = actions;
            _flush = flush;
            _logger = logger;
            string path = Path.Combine(gameData.RootPath, "Tradeskills.json");
            try
            {
                if (File.Exists(path))
                    _recipes.AddRange(JsonSerializer.Deserialize<List<TradeskillRecipe>>(GameDataOverlay.ReadMerged(path),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new());
                _logger.Info("GameData tradeskill recipes=" + _recipes.Count + " from " + path);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Failed to load Tradeskills.json from " + path);
            }
        }

        public int Count => _recipes.Count;

        public bool TryHandle(Player player, CharacterActionMessage message)
        {
            switch (message.Action)
            {
                case CharacterActionType.TradeskillSourceChanged:
                    SlotChanged(player, message, source: true);
                    return true;
                case CharacterActionType.TradeskillTargetChanged:
                    SlotChanged(player, message, source: false);
                    return true;
                case CharacterActionType.TradeskillBuildPressed:
                    Build(player, message);
                    return true;
                default:
                    return false;
            }
        }

        static Identity SlotOf(CharacterActionMessage message)
            => message.Parameter1 > 0
                ? new Identity { Type = (IdentityType)message.Parameter1, Instance = message.Parameter2 }
                : message.Target;

        void SlotChanged(Player player, CharacterActionMessage message, bool source)
        {
            Identity slot = SlotOf(message);
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Tradeskill {0} char={1} target={2} p1={3} p2={4}", source ? "source" : "target",
                player.Identity.Instance, message.Target, message.Parameter1, message.Parameter2));
            var window = _windows.GetValueOrDefault(player.Identity.Instance);
            bool valid = slot.Instance > 0 && slot.Type != IdentityType.None;
            window = source ? (valid ? slot : Identity.None, window.Target) : (window.Source, valid ? slot : Identity.None);
            _windows[player.Identity.Instance] = window;
            if (!valid || !InventoryActionService.TryResolveOwnedSlot(player, slot, out _, out Item item))
                return;
            int processes = _recipes.Count(r => (source ? r.Source : r.Target).Any(id => Is(item, id)));
            Send(player, source ? CharacterActionType.TradeskillSource : CharacterActionType.TradeskillTarget, Identity.None, 0, processes);
            Evaluate(player, window);
        }

        static bool Is(Item item, int id) => id == item.LowId || id == item.HighId;

        bool TryResolve(Player player, (Identity Source, Identity Target) window, out Item source, out Item target, out TradeskillRecipe? recipe)
        {
            source = null!; target = null!; recipe = null;
            if (window.Source.Instance <= 0 || window.Target.Instance <= 0
                || !InventoryActionService.TryResolveOwnedSlot(player, window.Source, out _, out source)
                || !InventoryActionService.TryResolveOwnedSlot(player, window.Target, out _, out target)
                || ReferenceEquals(source, target))
                return false;
            Item s = source, t = target;
            recipe = _recipes.FirstOrDefault(r => r.Source.Any(id => Is(s, id)) && r.Target.Any(id => Is(t, id)));
            return recipe != null;
        }

        void Evaluate(Player player, (Identity Source, Identity Target) window)
        {
            if (window.Source.Instance <= 0 || window.Target.Instance <= 0)
                return;
            if (!TryResolve(player, window, out Item source, out Item target, out TradeskillRecipe? recipe) || recipe == null)
            {
                Send(player, CharacterActionType.TradeskillNotValid, Identity.None, 0, 0);
                return;
            }
            if (!SourceInRange(recipe, source, target))
            {
                Send(player, CharacterActionType.TradeskillOutOfRange, Identity.None, 0, MinSourceQl(recipe, target));
                return;
            }
            foreach (int[] skill in recipe.Skills.Where(s => s.Length >= 2))
                Send(player, CharacterActionType.TradeskillRequirement, Identity.None, skill[0], Required(skill[1], target.Quality));
            Send(player, CharacterActionType.TradeskillResult,
                new Identity { Type = (IdentityType)target.Quality, Instance = recipe.Result[^1] }, target.Quality, recipe.Result[0]);
        }

        static int Required(int percent, int quality) => (int)Math.Ceiling(quality * percent / 100.0);

        static int MinSourceQl(TradeskillRecipe recipe, Item target) => (int)Math.Ceiling(target.Quality * recipe.MinSourcePct / 100.0);

        static bool SourceInRange(TradeskillRecipe recipe, Item source, Item target)
            => recipe.MinSourcePct <= 0 || source.Quality >= MinSourceQl(recipe, target);

        void Build(Player player, CharacterActionMessage message)
        {
            var window = _windows.GetValueOrDefault(player.Identity.Instance);
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Tradeskill build char={0} target={1} p1={2} p2={3} source={4} tgt={5}",
                player.Identity.Instance, message.Target, message.Parameter1, message.Parameter2, window.Source, window.Target));
            if (player.IsDead || player.IsPersistenceQuarantined || player.Session == null)
                return;
            if (!TryResolve(player, window, out Item source, out Item target, out TradeskillRecipe? recipe) || recipe == null || recipe.Result.Length == 0)
            {
                Feedback(player, "These items cannot be combined.");
                return;
            }
            if (!SourceInRange(recipe, source, target))
            {
                Feedback(player, string.Format(CultureInfo.InvariantCulture,
                    "The {0} must be at least QL {1} for a QL {2} {3}.", source.Name, MinSourceQl(recipe, target), target.Quality, target.Name));
                return;
            }
            foreach (int[] skill in recipe.Skills.Where(s => s.Length >= 2))
            {
                int need = Required(skill[1], target.Quality);
                int have = player.Stats.GetOrZero((CharacterStat)skill[0]);
                if (have < need)
                {
                    Feedback(player, string.Format(CultureInfo.InvariantCulture,
                        "You need {0} {1} for this ({2} now).", need, (CharacterStat)skill[0], have));
                    return;
                }
            }

            int quality = target.Quality;
            int low = recipe.Result[0];
            int high = recipe.Result.Length > 1 ? recipe.Result[1] : low;
            Identity sourceSlot = window.Source, targetSlot = window.Target;
            int sourceInstance = source.InstanceId, targetInstance = target.InstanceId;
            string sourceName = source.Name, targetName = target.Name;
            if (recipe.DeleteTarget && !Remove(player, targetSlot, targetInstance))
                return;
            if (recipe.DeleteSource && !Remove(player, sourceSlot, sourceInstance))
                return;

            Item result = _items.CreateWithNewInstance(low, high, quality, ItemSource.Other);
            int slot = player.Inventory.Inventory.FindFreeSlot();
            if (slot < 0 || !player.Inventory.Inventory.Add(slot, result))
            {
                Feedback(player, "Your inventory is full; the combine failed.");
                _logger.Warn("Tradeskill result lost: no inventory slot char=" + player.Identity.Instance + " item=" + low);
                return;
            }
            player.Inventory.MarkDirty(result, player.Inventory.Inventory, slot);
            _flush.NotifyDirty(player);
            player.Session.Send(new AddTemplateMessage
            {
                Identity = player.Identity, LowId = result.LowId, HighId = result.HighId,
                Quality = result.Quality, Count = result.StackCount
            });
            _windows[player.Identity.Instance] = (recipe.DeleteSource ? Identity.None : sourceSlot, Identity.None);
            Feedback(player, string.Format(CultureInfo.InvariantCulture,
                "You combined {0} with {1} and made {2} (QL {3}).", sourceName, targetName, result.Name, result.Quality));
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Tradeskill {0} char={1} result={2}/{3} ql={4}", recipe.Name, player.Identity.Instance, low, high, quality));
        }

        bool Remove(Player player, Identity slot, int instance)
        {
            if (!_actions.TryDelete(player, slot, instance))
            {
                Feedback(player, "The combine failed: an item changed.");
                return false;
            }
            InventoryActionService.TryResolveOwnedSlot(player, slot, out _, out _);
            player.Session?.Send(new CharacterActionMessage
            {
                Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot,
                Parameter1 = 0, Parameter2 = 0
            });
            return true;
        }

        static void Send(Player player, CharacterActionType action, Identity target, int p1, int p2)
            => player.Session?.Send(new CharacterActionMessage
            {
                Identity = player.Identity, Action = action, Target = target, Parameter1 = p1, Parameter2 = p2
            });

        static void Feedback(Player player, string text)
        {
            if (player.Session != null)
                GmCommandFeedback.Send(player.Session, player, text);
        }
    }
}