namespace ZoneEngine_New.Core.Helpers
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Nanos;

    /// <summary>
    /// Bits from <see cref="FunctionType.ChangeActionRestriction"/>.
    /// 214879 sets <see cref="PvPEnabled"/>; 202732 sets <see cref="PvPEnabled_Tower"/>.
    /// </summary>
    [Flags]
    public enum ActionRestrictionFlags
    {
        None = 0,

        PvPEnabled = 1,

        PvPEnabled_Tower = 67108864,
    }

    /// <summary>Whether one character may engage another.</summary>
    public static class CombatRules
    {
        const ActionRestrictionFlags PlayerAttackable =
            ActionRestrictionFlags.PvPEnabled | ActionRestrictionFlags.PvPEnabled_Tower;

        public static bool CanAttack(Character attacker, Character target)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);

            if (attacker.IsDead || target.IsDead || ReferenceEquals(attacker, target))
                return false;

            if (IsInRestrictedGas(target))
                return false;

            // Sector 42 laser fences (ContactDamage + NoCombat): the prop may still
            // hurt a player who walks into it. Moderate damage is applied by ContactDamageClock.
            if (attacker is NpcCharacter contactSrc
                && contactSrc.MobTemplate != null
                && contactSrc.MobTemplate.Has(NpcFeature.ContactDamage)
                && target is Player)
                return true;

            // A NoCombat NPC does not attack, and neither does anyone whose fighting a buff restricts.
            if (attacker is NpcCharacter { Attackable: false } || attacker.CombatRestricted)
                return false;

            // A pet and its owner (and the owner's other pets) are one side. Followed up the chain, so a merc's
            // own pets (MP attack/heal/mezz pets, Crat worker-droid, Engineer robot) side with the merc's player.
            Character attackerSide = SideOf(attacker);
            Character targetSide = SideOf(target);
            if (ReferenceEquals(attackerSide, targetSide))
                return false;

            // Starter playerbots fight wild mobs only; never players, pets, mercs, or other player-owned NPCs.
            if (attacker is NpcCharacter { Brain.PlayerBot: not null }
                && (targetSide is Player
                    || NpcSupportRules.IsPlayerPetOrMerc(target)
                    || NpcSupportRules.IsPlayerPetOrMerc(targetSide)))
                return false;

            // Allied structures (Sector 35 towers / accumulator): aliens may damage them,
            // players and their pets may not, including AoE (NanoCastFunctions uses CanAttack).
            if (target is NpcCharacter untargetable
                && untargetable.MobTemplate != null
                && untargetable.MobTemplate.Has(NpcFeature.PlayerUntargetable)
                && attackerSide is Player)
                return false;

            // A player's pet only fights players (or their pets) its owner could fight.
            if (attackerSide is Player && !ReferenceEquals(attackerSide, attacker))
                return CanAttack(attackerSide, targetSide) || (targetSide is not Player && CanAttackNonPlayer(target));

            // A player's pet is attacked under the same rules as its owner (PvP flags for players, mobs always).
            if (targetSide is Player && !ReferenceEquals(targetSide, target))
                return CanAttack(attacker, targetSide);

            int battleAttacker = attackerSide.Stats.GetOrZero(SmokeLounge.AOtomation.Messaging.GameData.CharacterStat.BattlestationSide);
            int battleTarget = targetSide.Stats.GetOrZero(SmokeLounge.AOtomation.Messaging.GameData.CharacterStat.BattlestationSide);
            if (battleAttacker != 0 && battleTarget != 0)
            {
                if (battleAttacker == battleTarget)
                    return false;

                // Shared Decontamination Room is a no-combat safe zone for both teams.
                if (ZoneEngine_New.Core.Battlestations.BattleStationClock.IsInDecontamination(attacker)
                    || ZoneEngine_New.Core.Battlestations.BattleStationClock.IsInDecontamination(target)
                    || ZoneEngine_New.Core.Battlestations.BattleStationClock.IsInDecontamination(attackerSide)
                    || ZoneEngine_New.Core.Battlestations.BattleStationClock.IsInDecontamination(targetSide))
                    return false;

                return targetSide is not NpcCharacter teamNpc || teamNpc.Attackable;
            }

            if (target is Player player)
            {
                // Mobs can still hit players. Only another player needs the PvP flags.
                if (!attacker.IsPlayer)
                    return true;

                return (player.ActionRestrictionFlags & PlayerAttackable) != 0;
            }

            return target is not NpcCharacter npc || npc.Attackable;
        }

        static bool CanAttackNonPlayer(Character target) => target is not NpcCharacter npc || npc.Attackable;

        /// <summary>Most owner levels followed by <see cref="SideOf"/> (player, merc, merc pet, ...).</summary>
        const int MaxOwnerDepth = 4;

        /// <summary>
        /// The character whose side <paramref name="character"/> fights on: its pet owner, followed up the chain
        /// (a merc's pet sides with the merc, which sides with its player). A character with no owner is its own side.
        /// </summary>
        public static Character SideOf(Character character)
        {
            ArgumentNullException.ThrowIfNull(character);
            Character side = character;
            for (int depth = 0; depth < MaxOwnerDepth; depth++)
            {
                if (side is not NpcCharacter { PetOwner: Character owner } || ReferenceEquals(owner, side))
                    break;
                side = owner;
            }

            return side;
        }

        /// <summary>
        /// A blue-named NPC (Flags stat bit <see cref="SmokeLounge.AOtomation.Messaging.GameData.CharacterFlags.HasBlueName"/>):
        /// the friendly, non-combat NPCs (receptionists, recruiters, vendors, quest givers). They are not attackable.
        /// </summary>
        public static bool IsBlueNameNpc(int flags)
            => !Entities.StatCollection.IsUnset(flags)
               && (flags & (int)SmokeLounge.AOtomation.Messaging.GameData.CharacterFlags.HasBlueName) != 0;

        /// <summary>
        /// True when the only reason <see cref="CanAttack"/> fails is that the target player is not PvP-flagged.
        /// </summary>
        public static bool IsPvpAttackBlocked(Character attacker, Character target)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);

            if (!attacker.IsPlayer || attacker.IsDead || target.IsDead || ReferenceEquals(attacker, target))
                return false;
            if (IsInRestrictedGas(target))
                return false;

            // A player's pet is blocked exactly when its owner is.
            if (target is NpcCharacter { PetOwner: Player petOwner })
            {
                if (ReferenceEquals(petOwner, attacker))
                    return false;
                target = petOwner;
            }

            return target is Player player && (player.ActionRestrictionFlags & PlayerAttackable) == 0;
        }

        /// <summary>
        /// Restricted gas refuses fighting. Volumes are not wired yet, so no target is in gas.
        /// </summary>
        public static bool IsInRestrictedGas(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            return false;
        }

        /// <summary>
        /// Flags granted by active buffs. Arg1 of 0 means the bits last as long as the buff.
        /// </summary>
        public static ActionRestrictionFlags CollectActionRestrictions(IReadOnlyList<Buff> buffs, StatCollection stats)
        {
            ArgumentNullException.ThrowIfNull(buffs);
            ArgumentNullException.ThrowIfNull(stats);

            ActionRestrictionFlags flags = ActionRestrictionFlags.None;
            for (int i = 0; i < buffs.Count; i++)
            {
                IReadOnlyList<ItemSpell> spells = buffs[i].ModifierSpells;
                for (int spellIndex = 0; spellIndex < spells.Count; spellIndex++)
                {
                    ItemSpell spell = spells[spellIndex];
                    if (!spell.Is(FunctionType.ChangeActionRestriction))
                        continue;
                    if (!spell.MeetsRequirements(stats))
                        continue;
                    if (!spell.TryReadInt(0, out int bits))
                        continue;
                    if (spell.TryReadInt(1, out int mode) && mode != 0)
                        continue;

                    flags |= (ActionRestrictionFlags)bits;
                }
            }

            return flags;
        }
    }
}
