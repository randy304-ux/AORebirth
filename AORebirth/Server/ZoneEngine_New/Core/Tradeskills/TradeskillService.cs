namespace ZoneEngine_New.Core.Tradeskills
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Tradeskill window and item-on-item combines. The window names the source and target slots
    /// (TradeskillSourceChanged/TargetChanged, Parameter1 = container type, Parameter2 = slot) and the server answers
    /// with the process counts, one TradeskillRequirement per skill and TradeskillResult (min QL, max QL with bumps).
    /// Build (Target.Instance = the slider QL, as CellAO reads it) makes the item. Dragging an item onto another runs
    /// the same combine at the highest QL. Reply layouts follow CellAO; not yet checked against a live capture. Only
    /// items the player owns in a resolvable slot take part.
    /// </summary>
    public sealed class TradeskillService
    {
        readonly IItemBuilder _items;
        readonly IItemTemplateCatalog? _catalog;
        readonly InventoryActionService _actions;
        readonly InventoryFlushService _flush;
        readonly IZoneLogger _logger;
        readonly TradeskillRecipeBook _book;
        readonly ConcurrentDictionary<int, (Identity Source, Identity Target)> _windows = new();

        public TradeskillService(IGameData gameData, IItemBuilder items, InventoryActionService actions,
            InventoryFlushService flush, IZoneLogger logger, IItemTemplateCatalog? catalog = null)
        {
            _items = items;
            _catalog = catalog;
            _actions = actions;
            _flush = flush;
            _logger = logger;
            try
            {
                _book = TradeskillRecipeBook.Load(gameData.RootPath, _logger.Info);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Failed to load tradeskill recipes from " + gameData.RootPath);
                _book = new TradeskillRecipeBook();
            }
        }

        public int Count => _book.Count;

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
                {
                    var window = _windows.GetValueOrDefault(player.Identity.Instance);
                    _logger.Info(string.Format(CultureInfo.InvariantCulture,
                        "Tradeskill build char={0} target={1} p1={2} p2={3} source={4} tgt={5}",
                        player.Identity.Instance, message.Target, message.Parameter1, message.Parameter2, window.Source, window.Target));
                    if (Build(player, window, message.Target.Instance))
                        _windows[player.Identity.Instance] = (Identity.None, Identity.None);
                    return true;
                }
                case CharacterActionType.UseItemOnItem:
                {
                    // CellAO's layout for an item dragged onto another: Target = source slot, Parameter1/2 = target slot.
                    var target = new Identity { Type = (IdentityType)message.Parameter1, Instance = message.Parameter2 };
                    _logger.Info(string.Format(CultureInfo.InvariantCulture,
                        "CharacterAction UseItemOnItem char={0} source={1} target={2}", player.Identity.Instance, message.Target, target));
                    if (!TryCombine(player, message.Target, target, out _))
                        Feedback(player, NoRecipeText(player, message.Target, target));
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>
        /// Item dragged onto another item in the inventory (GenericCmd UseItemOnItem or CharacterAction UseItemOnItem):
        /// the same combine as the Build button, at the highest QL the skills allow. False when no recipe takes this
        /// source and target, so the caller can try its other item-on-item uses; true when a recipe matched (built
        /// says whether the combine went through).
        /// </summary>
        public bool TryCombine(Player player, Identity sourceSlot, Identity targetSlot, out bool built)
        {
            built = false;
            if (!TryResolve(player, (sourceSlot, targetSlot), out Item source, out Item target, out TradeskillRecipe? recipe))
                return false;
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Tradeskill combine (item on item) char={0} source={1} {2} target={3} {4}",
                player.Identity.Instance, sourceSlot, source.Name, targetSlot, target.Name));
            built = Build(player, (sourceSlot, targetSlot), desiredQuality: 0);
            return true;
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
            int processes = source ? _book.SourceProcesses(item.LowId, item.HighId) : _book.TargetProcesses(item.LowId, item.HighId);
            Send(player, source ? CharacterActionType.TradeskillSource : CharacterActionType.TradeskillTarget, Identity.None, 0, processes);
            Evaluate(player, window);
        }

        bool TryResolve(Player player, (Identity Source, Identity Target) window, out Item source, out Item target,
            out TradeskillRecipe? recipe)
        {
            source = null!; target = null!; recipe = null;
            if (window.Source.Instance <= 0 || window.Target.Instance <= 0
                || !InventoryActionService.TryResolveOwnedSlot(player, window.Source, out _, out source)
                || !InventoryActionService.TryResolveOwnedSlot(player, window.Target, out _, out target)
                || ReferenceEquals(source, target))
                return false;
            if (!_book.TryFind(source.LowId, source.HighId, target.LowId, target.HighId, out TradeskillRecipe found))
                return false;
            recipe = found;
            return true;
        }

        int Skill(Player player, int stat) => player.Stats.GetOrZero((CharacterStat)stat);

        (int Low, int High) ResultQualities(TradeskillRecipe recipe)
        {
            int low = 1, high = 0;
            if (_catalog != null && _catalog.TryGet(recipe.ResultLow, out ItemTemplate lowTemplate))
                low = Math.Max(1, lowTemplate.Quality);
            if (_catalog != null && _catalog.TryGet(recipe.ResultHigh, out ItemTemplate highTemplate))
                high = highTemplate.Quality;
            return (low, Math.Max(low, high));
        }

        (int Min, int Max) Range(Player player, TradeskillRecipe recipe, Item target)
        {
            int bumps = TradeskillRules.Bumps(recipe, target.Quality, stat => Skill(player, stat));
            var qualities = ResultQualities(recipe);
            return TradeskillRules.ResultRange(recipe, target.Quality, bumps, qualities.Low, qualities.High);
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
            if (!TradeskillRules.SourceInRange(recipe, source.Quality, target.Quality))
            {
                Send(player, CharacterActionType.TradeskillOutOfRange, Identity.None, 0,
                    TradeskillRules.MinSourceQuality(recipe, target.Quality));
                return;
            }
            foreach (int[] skill in recipe.Skills)
                if (skill.Length >= 2)
                    Send(player, CharacterActionType.TradeskillRequirement, Identity.None, skill[0],
                        TradeskillRules.Required(skill[1], target.Quality));
            var range = Range(player, recipe, target);
            Send(player, CharacterActionType.TradeskillResult,
                new Identity { Type = (IdentityType)range.Max, Instance = recipe.ResultHigh }, range.Min, recipe.ResultLow);
        }

        bool Build(Player player, (Identity Source, Identity Target) window, int desiredQuality)
        {
            if (player.IsDead || player.IsPersistenceQuarantined || player.Session == null)
                return false;
            if (!TryResolve(player, window, out Item source, out Item target, out TradeskillRecipe? recipe) || recipe == null)
            {
                Feedback(player, NoRecipeText(player, window.Source, window.Target));
                return false;
            }
            if (source.Locked || target.Locked)
            {
                Feedback(player, "The combine failed: one of the items is locked.");
                return false;
            }
            if (!TradeskillRules.TargetAllowed(recipe, target.Quality))
            {
                Feedback(player, string.Format(CultureInfo.InvariantCulture,
                    "The {0} must be at least QL {1}.", target.Name, recipe.MinTargetQl));
                return false;
            }
            if (!TradeskillRules.SourceInRange(recipe, source.Quality, target.Quality))
            {
                Feedback(player, string.Format(CultureInfo.InvariantCulture,
                    "The {0} must be at least QL {1} for a QL {2} {3}.", source.Name,
                    TradeskillRules.MinSourceQuality(recipe, target.Quality), target.Quality, target.Name));
                return false;
            }
            if (TradeskillRules.TryFindMissingSkill(recipe, target.Quality, stat => Skill(player, stat),
                out int stat, out int need, out int have))
            {
                Feedback(player, string.Format(CultureInfo.InvariantCulture,
                    "You need {0} {1} for this ({2} now).", need, SkillName(stat), have));
                return false;
            }

            var range = Range(player, recipe, target);
            int quality = TradeskillRules.PickQuality(desiredQuality, range);
            if (!TradeskillRules.JobeAllowed(recipe, target.Quality, quality))
            {
                Feedback(player, target.Quality < 99
                    ? "Jobe clusters only go into QL 99 or higher implants."
                    : "A QL 99 implant takes a Jobe cluster only when the combine bumps it to QL 100; you need more Nano Programming.");
                return false;
            }
            bool sourceStacked = recipe.DeleteSource && source.StackCount > 1;
            if (sourceStacked && window.Source.Type != IdentityType.Inventory)
            {
                Feedback(player, "Put the " + source.Name + " stack in your main inventory to combine it.");
                return false;
            }
            if (player.Inventory.Inventory.FindFreeSlot() < 0 && !recipe.DeleteTarget && (!recipe.DeleteSource || sourceStacked))
            {
                Feedback(player, "Your inventory is full.");
                return false;
            }

            int low = recipe.ResultLow, high = recipe.ResultHigh;
            Identity sourceSlot = window.Source, targetSlot = window.Target;
            int targetInstance = target.InstanceId;
            string sourceName = source.Name, targetName = target.Name;
            if (recipe.DeleteTarget && !Remove(player, targetSlot, targetInstance))
                return false;
            if (recipe.DeleteSource && !UseSource(player, sourceSlot, source))
                return false;

            Item result = _items.CreateWithNewInstance(low, high, quality, ItemSource.Other);
            int slot = player.Inventory.Inventory.FindFreeSlot();
            if (slot < 0 || !player.Inventory.Inventory.Add(slot, result))
            {
                Feedback(player, "Your inventory is full; the combine failed.");
                _logger.Warn("Tradeskill result lost: no inventory slot char=" + player.Identity.Instance + " item=" + low);
                return false;
            }
            player.Inventory.MarkDirty(result, player.Inventory.Inventory, slot);
            _flush.NotifyDirty(player);
            player.Session.Send(new AddTemplateMessage
            {
                Identity = player.Identity, LowId = result.LowId, HighId = result.HighId,
                Quality = result.Quality, Count = result.StackCount
            });
            Feedback(player, string.Format(CultureInfo.InvariantCulture,
                "You combined \"{0}\" with \"{1}\" and the result is a quality level {2} \"{3}\".",
                sourceName, targetName, result.Quality, result.Name));

            var qualities = ResultQualities(recipe);
            int xp = TradeskillRules.Xp(recipe, result.Quality, qualities.Low, qualities.High);
            if (xp > 0)
                player.AwardXp(xp, XpSource.Quest);
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Tradeskill made char={0} source={1} target={2} result={3}/{4} ql={5} range={6}-{7} xp={8}",
                player.Identity.Instance, sourceName, targetName, low, high, result.Quality, range.Min, range.Max, xp));
            return true;
        }

        /// <summary>Why nothing matched, with a hint when a cluster meets an implant (taken grade slot or wrong body part).</summary>
        static string NoRecipeText(Player player, Identity sourceSlot, Identity targetSlot)
        {
            if (InventoryActionService.TryResolveOwnedSlot(player, sourceSlot, out _, out Item source)
                && InventoryActionService.TryResolveOwnedSlot(player, targetSlot, out _, out Item target)
                && source.Name.Contains("Cluster", StringComparison.OrdinalIgnoreCase)
                && target.Name.Contains("Implant", StringComparison.OrdinalIgnoreCase))
                return "That cluster does not fit this implant: its Shiny/Bright/Faded slot is taken or it belongs in another body location.";
            return "It is not possible to assemble those two items. Maybe the order was wrong?";
        }

        static string SkillName(int stat)
            => Enum.IsDefined(typeof(CharacterStat), stat) ? ((CharacterStat)stat).ToString() : "skill " + stat;

        /// <summary>A combine uses one source item: the last of a stack is deleted, a bigger stack loses one (live).</summary>
        bool UseSource(Player player, Identity slot, Item source)
        {
            if (source.StackCount <= 1)
                return Remove(player, slot, source.InstanceId);
            int remaining = source.StackCount - 1;
            if (!_actions.TryTakeOneFromStack(player, slot, source.InstanceId, out int placement))
            {
                Feedback(player, "The combine failed: an item changed.");
                return false;
            }
            player.Session?.Send(new CharacterActionMessage
            {
                Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot,
                Parameter1 = 0, Parameter2 = 0
            });
            player.Session?.Send(new AddTemplateMessage
            {
                Identity = player.Identity, LowId = source.LowId, HighId = source.HighId,
                Quality = source.Quality, Count = remaining
            });
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Tradeskill source stack char={0} {1} slot {2} -> {3} count={4}",
                player.Identity.Instance, source.Name, slot.Instance, placement, remaining));
            return true;
        }

        bool Remove(Player player, Identity slot, int instance)
        {
            if (!_actions.TryDelete(player, slot, instance))
            {
                Feedback(player, "The combine failed: an item changed.");
                return false;
            }
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
