namespace ZoneEngine_New.Core.Helpers
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Pets;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Teams;

    /// <summary>
    /// Who an NPC may heal or buff. Mercs and other player pets use an allow-list only: owner, owner's team,
    /// the owner's other pets/mercs, and friendly players. Never any outdoor/wild NPC mob (faction, side, or
    /// Attackable:false must not grant support). Battle-station bots may only support the same side's players/bots.
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

            Character masterProbe = ResolveMaster(caster) ?? caster;
            if (IsEngagedWith(caster, target) || IsEngagedWith(masterProbe, target))
                return false;

            // Player pet / merc: allow-list only. Do not use CanAttack / Attackable / faction as permission —
            // outdoor neutrals (e.g. Eumenides) are often not attackable to the pet and were wrongly allowed
            // by the old "non-attackable NPC" fallthrough when PetMaster/PetOwner was missing.
            if (IsPlayerPetOrMerc(caster))
            {
                Character? petMaster = ResolveMaster(caster);
                return petMaster != null && IsOwnerParty(petMaster, target);
            }

            // Battle-station bot: same side, players or other BS bots only — never outdoor mobs.
            int side = caster.Stats.GetOrZero(CharacterStat.BattlestationSide);
            if (side != 0)
            {
                if (target.Stats.GetOrZero(CharacterStat.BattlestationSide) != side)
                    return false;
                if (target is Player)
                    return true;
                return target is NpcCharacter bot
                    && bot.PetOwner == null
                    && bot.Stats.GetOrZero(CharacterStat.PetMaster) == 0
                    && !IsPlayerPetOrMerc(bot)
                    && bot.Stats.GetOrZero(CharacterStat.BattlestationSide) == side;
            }

            // Uncontrolled wild NPC: no support through this shared gate.
            return false;
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
        /// Never a wild/outdoor NPC mob.
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

        /// <summary>
        /// True for hired mercs and other player pets (bound controller, PetMaster, or merc spawn hash).
        /// </summary>
        public static bool IsPlayerPetOrMerc(Character caster)
        {
            if (caster is not NpcCharacter npc)
                return false;

            if (npc.PetOwner != null || npc.Pet != null)
                return true;

            if (npc.Stats.GetOrZero(CharacterStat.PetMaster) != 0)
                return true;

            if (Mercs.IsMercHash(npc.Pet?.Hash) || Mercs.IsMercHash(npc.SpawnHash))
                return true;

            string? templateHash = npc.MobTemplate?.Hash;
            return Mercs.IsMercHash(templateHash);
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
