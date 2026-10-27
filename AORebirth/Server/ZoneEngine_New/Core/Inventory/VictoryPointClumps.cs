namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Lost Eden VP-reward clumps (Cell Templates, Plasmid Cultures, Mitochondria Samples,
    /// Protein Mapping Data). items.dat OnUse is Hit 669 plus SystemText, but Can has no Use bit,
    /// so the normal use gate would refuse them. Convert here, then destroy the clump.
    /// </summary>
    public static class VictoryPointClumps
    {
        static readonly HashSet<int> TemplateIds = new()
        {
            266354, 266355, // Plasmid Cultures QL1 / QL300 (4 / 20 VP)
            266356, 266357, // Mitochondria Samples QL1 / QL300 (4 / 20 VP)
            266358, 266359, // Protein Mapping Data QL1 / QL300 (4 / 40 VP)
            266360, 266361, 266363, 266364 // Cell Templates (4 / 8 VP across QL band)
        };

        public static bool TryConvert(
            Item item,
            Player player,
            Identity slotIdentity,
            IInventoryRepository inventoryRepository,
            IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(items);

            if (!TemplateIds.Contains(item.LowId) && !TemplateIds.Contains(item.HighId))
                return false;

            if (player.Session == null || player.Playfield == null || !player.Inventory.IsHydrated)
                return true;

            if (!item.Definition.ExecuteOnUseSpells(
                    player,
                    inventoryRepository,
                    items,
                    source: player,
                    criteria: new SpellCriteria { Subject = item, SubjectSlot = slotIdentity }))
                return true;

            item.DestroyOne(player, slotIdentity);
            return true;
        }
    }
}
