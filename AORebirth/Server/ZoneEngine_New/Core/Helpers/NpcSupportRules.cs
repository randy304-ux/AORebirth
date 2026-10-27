namespace ZoneEngine_New.Core.Helpers
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Teams;

    /// <summary>
    /// Who an NPC may heal or buff. Mercs and other player pets may only support their owner, that owner's
    /// team, the owner's other pets/mercs, and friendly players. Battle-station bots may only support the
    /// same side. Never support attackable mobs/aliens or anyone the caster (or its master) is fighting.
    /// </summary>
    public static class NpcSupportRules
    {
        /// <summary>
        /// True when <paramref name="caster"/> may cast a friendly heal/buff on <paramref name="target"/>.
        /// </summary>
        public static bool CanSupport(Character caster, Character target)
        {
            if (caster == null || target == null || caster.IsDead || target.IsDead)
                return false;
            if (ReferenceEquals(caster, target))
                return true;

            Character master = ResolveMaster(caster) ?? caster;

            // Never heal/buff something we (or our master) can fight, or are fighting.
            if (CombatRules.CanAttack(caster, target) || CombatRules.CanAttack(master, target))
                return false;
            if (IsEngagedWith(caster, target) || IsEngagedWith(master, target))
                return false;

            // Player pet / merc (Pet controller or PetMaster stat): owner party only — never wild NPCs.
            if (ResolveMaster(caster) is Character petMaster)
                return IsOwnerParty(petMaster, target);

            // Battle-station bot: same side only (players or bots).
            int side = caster.Stats.GetOrZero(CharacterStat.BattlestationSide);
            if (side != 0)
                return target.Stats.GetOrZero(CharacterStat.BattlestationSide) == side;

            // Other wild NPCs may support non-attackable (blue-name) NPCs only — never monsters/aliens.
            return target is NpcCharacter { Attackable: false };
        }

        /// <summary>
        /// Whether an NPC may land <paramref name="spell"/> on <paramref name="target"/>. Support nanos and any
        /// health-raising heal require <see cref="CanSupport"/>; true hostile damage/debuffs may hit enemies.
        /// Doctor heals carry ApplyOnFightingTarget and must never land on the fight target.
        /// </summary>
        public static bool MayNpcLand(Character caster, Character target, NanoSpell spell)
        {
            if (caster == null || target == null || spell == null)
                return false;
            if (CanSupport(caster, target))
                return true;
            if (caster is not NpcCharacter)
                return true;
            if (IsHealthHeal(spell) || !spell.IsHostile)
                return false;
            return true;
        }

        /// <summary>OnUse (or tick) Hit that raises Health — a heal or heal-over-time.</summary>
        public static bool IsHealthHeal(NanoSpell spell)
        {
            if (spell == null)
                return false;

            foreach (List<ItemSpell> spells in spell.SpellList.Values)
            {
                for (int i = 0; i < spells.Count; i++)
                {
                    ItemSpell hit = spells[i];
                    if (hit.Is(FunctionType.Hit)
                        && hit.TryReadInt(0, out int stat) && stat == (int)CharacterStat.Health
                        && hit.TryReadInt(1, out int amount) && amount > 0)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Owner, owner's other pets/mercs, teammates (and their pets), and other players the owner cannot attack.
        /// </summary>
        public static bool IsOwnerParty(Character master, Character target)
        {
            if (ReferenceEquals(master, target))
                return true;

            if (target is NpcCharacter npc)
            {
                Character? npcMaster = ResolveMaster(npc);
                if (npcMaster != null && ReferenceEquals(npcMaster, master))
                    return true;
                if (npcMaster is Player npcOwner && master is Player masterPlayer
                    && AreTeammates(masterPlayer, npcOwner))
                    return true;
                return false;
            }

            if (target is not Player player || master is not Player owner)
                return false;

            if (AreTeammates(owner, player))
                return true;

            // Friendly player: owner cannot attack them (no PvP flag / same restrictions).
            return !CombatRules.CanAttack(owner, player);
        }

        /// <summary>
        /// The character this NPC supports for: pet controller owner, else the PetMaster dynel when set.
        /// </summary>
        public static Character? ResolveMaster(Character caster)
        {
            if (caster is not NpcCharacter npc)
                return null;

            if (npc.PetOwner is Character owner)
                return owner;

            int masterId = npc.Stats.GetOrZero(CharacterStat.PetMaster);
            if (masterId == 0 || npc.Playfield == null)
                return null;

            return npc.Playfield.GetService<DynelRegistry>() is DynelRegistry registry
                && registry.TryGet(new Identity { Type = IdentityType.CanbeAffected, Instance = masterId }, out Dynel? dynel)
                && dynel is Character character && !character.IsDead
                ? character
                : null;
        }

        static bool AreTeammates(Player a, Player b)
        {
            if (ReferenceEquals(a, b))
                return true;
            TeamService? teams = a.Playfield?.GetService<TeamService>()
                ?? b.Playfield?.GetService<TeamService>();
            return teams != null && teams.AreTeammates(a, b);
        }

        static bool IsEngagedWith(Character a, Character b)
        {
            if (a.FightingTarget.Instance != 0 && a.FightingTarget.Instance == b.Identity.Instance)
                return true;
            if (b.FightingTarget.Instance != 0 && b.FightingTarget.Instance == a.Identity.Instance)
                return true;
            return false;
        }
    }
}
