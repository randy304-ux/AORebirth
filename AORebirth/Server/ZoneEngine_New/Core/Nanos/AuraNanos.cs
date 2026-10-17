namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Dyna glow nanos (Boss Modifier Item OnWear CastNano, e.g. 160084 Red glow) last TimeExist 10000
    /// (100 s). The wear cast runs before the NPC has a cell, so the first SetNanoDuration has nobody to
    /// reach, and the buff then expires. This refreshes those buffs through the existing duration packet
    /// and sends that packet again when a player walks into range. Unlocked colours are a server-only
    /// bitfield; a container drop replaces one of the dyna's 5–7 items so the corpse count stays put.
    /// </summary>
    public static class AuraNanos
    {
        /// <summary>Bit i set when <see cref="DynaModifiers.All"/>[i] is unlocked.</summary>
        public const CharacterStat UnlocksStat = (CharacterStat)10281;

        /// <summary>1-based index into <see cref="DynaModifiers.All"/> of the aura currently worn. 0 is off.</summary>
        public const CharacterStat ActiveStat = (CharacterStat)10282;

        /// <summary>1 while the active aura is also kept on this player's pets and mercs.</summary>
        public const CharacterStat PetsStat = (CharacterStat)10283;

        /// <summary>Refresh while more than this many centiseconds remain, so the client never sees it drop.</summary>
        const int RefreshAtCentiseconds = 2500;

        public static void Maintain(Character character, DateTime nowUtc)
        {
            if (character.IsDead)
                return;

            if (character is Player player)
                MaintainPlayer(player, nowUtc);
            else if (character is NpcCharacter npc)
                MaintainDyna(npc, nowUtc);
        }

        /// <summary>A player just came into range of <paramref name="source"/>. Send the glow's duration packet to that player.</summary>
        public static void AnnounceToViewer(Player viewer, Character source)
        {
            ArgumentNullException.ThrowIfNull(viewer);
            ArgumentNullException.ThrowIfNull(source);
            if (ReferenceEquals(viewer, source) || source.IsDead)
                return;

            IReadOnlyList<Buff> buffs = source.Buffs;
            for (int i = 0; i < buffs.Count; i++)
            {
                if (DynaModifiers.IsGlowNano(buffs[i].Id))
                    NanoRuntime.AnnounceBuffTo(viewer, source, buffs[i]);
            }
        }

        public static bool IsUnlocked(Player player, int index)
            => index >= 0
                && index < DynaModifiers.All.Length
                && (player.Stats.GetOrZero(UnlocksStat) & (1 << index)) != 0;

        public static void Unlock(Player player, int index)
        {
            int bits = player.Stats.GetOrZero(UnlocksStat);
            player.Stats.Set(UnlocksStat, bits | (1 << index), StatDetail.Base);
        }

        /// <summary>Using one colour's container unlocks that aura and consumes the item.</summary>
        public static bool TryConsumeUnlock(Player player, Item item, Identity slotIdentity)
        {
            DynaModifiers.Modifier? mod = DynaModifiers.ByContainer(item.LowId);
            if (mod == null)
                return false;

            int index = DynaModifiers.IndexOf(mod.Name);
            if (index < 0)
                return false;

            if (!item.DestroyOne(player, slotIdentity))
                return false;

            bool already = IsUnlocked(player, index);
            Unlock(player, index);
            string text = already
                ? "You already have the " + mod.Name + " aura unlocked."
                : "Unlocked the " + mod.Name + " aura (" + mod.ContainerName + "). Apply it with .aura " + mod.Name.ToLowerInvariant();
            if (player.Session != null)
                GmCommandFeedback.Send(player.Session, player, text);
            return true;
        }

        /// <summary>
        /// Applies <paramref name="mod"/> to the player, and to current pets and mercs when
        /// <paramref name="includePets"/> is set. The choice is saved so the glow is put back before it expires.
        /// </summary>
        public static bool Apply(Player player, DynaModifiers.Modifier mod, bool includePets, DateTime nowUtc)
        {
            int index = DynaModifiers.IndexOf(mod.Name);
            if (index < 0)
                return false;

            Playfield? playfield = player.Playfield;
            if (playfield == null)
                return false;

            IItemBuilder items = playfield.GetRequiredService<IItemBuilder>();
            IInventoryRepository inventory = playfield.GetRequiredService<IInventoryRepository>();
            StripOtherGlows(player, mod.GlowNano);
            if (!NanoRuntime.TryApplyImmediate(player, player, mod.GlowNano, items, inventory, nowUtc, gmApply: true))
                return false;

            player.Stats.Set(ActiveStat, index + 1, StatDetail.Base);
            player.Stats.Set(PetsStat, includePets ? 1 : 0, StatDetail.Base);
            if (includePets)
                ApplyToPets(player, mod.GlowNano, items, inventory, nowUtc);
            return true;
        }

        public static void Clear(Player player)
        {
            StripOtherGlows(player, keep: 0);
            foreach (NpcCharacter pet in player.OwnedPets.All)
            {
                if (!pet.IsDead)
                    StripOtherGlows(pet, keep: 0);
            }

            player.Stats.Set(ActiveStat, 0, StatDetail.Base);
            player.Stats.Set(PetsStat, 0, StatDetail.Base);
        }

        /// <summary>Replaces one rolled drop with this dyna's colour container. The 5–7 count does not grow.</summary>
        public static void TrySwapLoot(Corpse corpse, string modifierName, IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(corpse);
            ArgumentNullException.ThrowIfNull(items);
            DynaModifiers.Modifier? mod = DynaModifiers.Find(modifierName);
            if (mod == null || Random.Shared.Next(100) >= DynaModifiers.ContainerDropPercent)
                return;

            int slot = -1;
            foreach (KeyValuePair<int, Item> pair in corpse.Loot.EnumerateSlots())
                slot = pair.Key;
            if (slot < 0 || corpse.Loot.Remove(slot) == null)
                return;

            Item container = items.CreateWithNewInstance(mod.ContainerId, mod.ContainerId, 1, ItemSource.Loot);
            corpse.Loot.Add(slot, container);
        }

        static void MaintainPlayer(Player player, DateTime nowUtc)
        {
            int active = player.Stats.GetOrZero(ActiveStat);
            if (active <= 0 || active > DynaModifiers.All.Length)
                return;

            int nanoId = DynaModifiers.All[active - 1].GlowNano;
            bool pets = player.Stats.GetOrZero(PetsStat) != 0;
            if (!NeedsRefresh(player, nanoId, nowUtc) && !PetsNeedRefresh(player, nanoId, nowUtc, pets))
                return;

            Playfield? playfield = player.Playfield;
            if (playfield == null)
                return;

            IItemBuilder items = playfield.GetRequiredService<IItemBuilder>();
            IInventoryRepository inventory = playfield.GetRequiredService<IInventoryRepository>();
            Ensure(player, player, nanoId, items, inventory, nowUtc);
            if (pets)
                ApplyToPets(player, nanoId, items, inventory, nowUtc);
        }

        static void MaintainDyna(NpcCharacter npc, DateTime nowUtc)
        {
            string? modifier = npc.MobTemplate?.DynaModifier;
            if (string.IsNullOrEmpty(modifier) || npc.Playfield == null)
                return;

            var ids = new List<int>(4);
            CollectWornCasts(npc, ids);
            if (ids.Count == 0 && DynaModifiers.Find(modifier) is { GlowNano: > 0 } mod)
                ids.Add(mod.GlowNano);
            if (ids.Count == 0)
                return;

            bool first = !npc.AuraAnnounced && npc.Cell != null;
            bool due = false;
            for (int i = 0; i < ids.Count; i++)
            {
                if (NeedsRefresh(npc, ids[i], nowUtc))
                {
                    due = true;
                    break;
                }
            }

            if (!first && !due)
                return;

            IItemBuilder items = npc.Playfield.GetRequiredService<IItemBuilder>();
            IInventoryRepository inventory = npc.Playfield.GetRequiredService<IInventoryRepository>();
            if (due)
            {
                for (int i = 0; i < ids.Count; i++)
                    Ensure(npc, npc, ids[i], items, inventory, nowUtc);
            }

            if (!first)
                return;

            IReadOnlyList<Buff> buffs = npc.Buffs;
            for (int i = 0; i < buffs.Count; i++)
            {
                if (ids.Contains(buffs[i].Id))
                    NanoRuntime.AnnounceBuff(npc, buffs[i]);
            }

            npc.AuraAnnounced = true;
        }

        static void ApplyToPets(Player player, int nanoId, IItemBuilder items, IInventoryRepository inventory, DateTime nowUtc)
        {
            foreach (NpcCharacter pet in player.OwnedPets.All)
            {
                if (pet.IsDead)
                    continue;
                StripOtherGlows(pet, nanoId);
                Ensure(player, pet, nanoId, items, inventory, nowUtc);
            }
        }

        static void Ensure(Character source, Character target, int nanoId, IItemBuilder items, IInventoryRepository inventory, DateTime nowUtc)
        {
            if (target.TryGetBuff(nanoId, out Buff? buff) && buff != null)
            {
                if (buff.RemainingCentiseconds(nowUtc) < RefreshAtCentiseconds)
                    NanoRuntime.RefreshBuffDuration(target, buff, nowUtc);
                return;
            }

            NanoRuntime.TryApplyImmediate(source, target, nanoId, items, inventory, nowUtc, gmApply: true);
        }

        static bool PetsNeedRefresh(Player player, int nanoId, DateTime nowUtc, bool pets)
        {
            if (!pets)
                return false;
            foreach (NpcCharacter pet in player.OwnedPets.All)
            {
                if (!pet.IsDead && NeedsRefresh(pet, nanoId, nowUtc))
                    return true;
            }

            return false;
        }

        static bool NeedsRefresh(Character target, int nanoId, DateTime nowUtc)
            => !target.TryGetBuff(nanoId, out Buff? buff) || buff == null || buff.RemainingCentiseconds(nowUtc) < RefreshAtCentiseconds;

        static void CollectWornCasts(NpcCharacter npc, List<int> into)
        {
            foreach (KeyValuePair<int, Item> pair in npc.Equipment.EnumerateSlots())
            {
                Item item = pair.Value;
                if (!item.Name.StartsWith(DynaModifiers.BossItemPrefix, StringComparison.Ordinal))
                    continue;
                if (!item.SpellList.TryGetValue(EventType.OnWear, out List<ItemSpell>? spells))
                    continue;
                for (int i = 0; i < spells.Count; i++)
                {
                    ItemSpell spell = spells[i];
                    if (spell.FunctionType != (int)FunctionType.CastNano)
                        continue;
                    if (spell.TryReadInt(0, out int nanoId) && nanoId > 0 && !into.Contains(nanoId))
                        into.Add(nanoId);
                }
            }
        }

        static void StripOtherGlows(Character target, int keep)
        {
            for (int i = 0; i < DynaModifiers.All.Length; i++)
            {
                int nanoId = DynaModifiers.All[i].GlowNano;
                if (nanoId != keep)
                    NanoRuntime.TryStripNano(target, nanoId);
            }
        }
    }
}
