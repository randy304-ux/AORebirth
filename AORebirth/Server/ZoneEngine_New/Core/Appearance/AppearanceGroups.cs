namespace ZoneEngine_New.Core.Appearance
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// Item groups whose worn look players can switch from the Store window (Battlesuits page).
    /// A group's toggles are stored per character in ddogs_appearance_prefs as "&lt;Key&gt;.look" (draw the group's look,
    /// default on) and "&lt;Key&gt;.only" (hide every other worn armor look while it shows, default off).
    /// To give another item type the same treatment, add a group here and a block on the Store page.
    /// </summary>
    public sealed class AppearanceGroup
    {
        public AppearanceGroup(string key, string label, System.Func<Item, int> shapeOf)
        {
            Key = key;
            Label = label;
            ShapeOf = shapeOf;
        }

        public string Key { get; }

        public string Label { get; }

        /// <summary>The MonsterData body the item swaps the wearer into, or 0 when the item is not in the group.</summary>
        public System.Func<Item, int> ShapeOf { get; }

        public string LookKey => Key + ".look";

        public string OnlyKey => Key + ".only";
    }

    public static class AppearanceGroups
    {
        /// <summary>Armor-page back slot bit (ArmorSlots.Back = 3).</summary>
        const int BackSlotBit = 1 << 3;

        /// <summary>
        /// Battle suits and body costumes: armor worn in the back slot whose OnWear MonsterShape swaps the whole body
        /// (items.dat: Desert / Plasteel / Urban / Omni-Pol battle suits, Desert Scorpiod, Divaad's Armor, Charred
        /// Abaddon Chassis, Hellfyre Magma Suit, Cyborg and Catgirl costumes). Vehicles use MonsterShape too, but
        /// they are utility-slot weapons, so the class and slot checks leave them out.
        /// </summary>
        public static readonly AppearanceGroup BattleSuit = new("battlesuit", "Battle suits", BackSlotBodySwap);

        public static IReadOnlyList<AppearanceGroup> All { get; } = [BattleSuit];

        static int BackSlotBodySwap(Item item)
        {
            if (item?.Definition == null
                || (ItemClass)item.GetStat(CharacterStat.ItemClass) != ItemClass.Armor
                || (item.GetStat(CharacterStat.Slot) & BackSlotBit) == 0)
                return 0;
            return MonsterShapeOf(item);
        }

        public static int MonsterShapeOf(Item item)
        {
            foreach (ItemSpell spell in item.WearSpells)
            {
                if (spell.Is(FunctionType.MonsterShape) && spell.TryReadInt(0, out int shape) && shape > 0)
                    return shape;
            }

            return 0;
        }
    }
}