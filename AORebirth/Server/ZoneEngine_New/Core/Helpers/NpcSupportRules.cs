namespace ZoneEngine_New.Core.Helpers
{
    using System;
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
    /// the owner's other pets/mercs, and friendly players. Never any outdoor/wild NPC mob.
    /// Offensive nanos (damage, drains that hurt the cast target, hostile debuffs) may only land on enemies.
    /// Beneficial nanos (heals, HoTs, team heals, friendly buffs, heal procs attached to the cast target) may only
    /// land on that allow-list — Shade's Caress-style AddDefProc(Heal) on the fight target is beneficial-to-target.
    /// </summary>
    public static class NpcSupportRules
    {
        const int MaxChildDepth = 4;

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

            // Player pet / merc: allow-list only.
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

            return false;
        }

        /// <summary>
        /// Whether an NPC may land <paramref name="spell"/> on <paramref name="target"/>.
        /// Beneficial-to-recipient effects require <see cref="CanSupport"/>; damaging/offensive-to-recipient
        /// effects require a non-support (enemy) target. Mixed drains that hurt the recipient and heal only
        /// the caster (User/Wearer) count as offensive.
        /// </summary>
        public static bool MayNpcLand(Character caster, Character target, NanoSpell spell)
        {
            if (caster == null || target == null || spell == null)
                return false;
            if (caster is not NpcCharacter)
                return true;

            bool beneficialToTarget = BenefitsCastRecipient(caster, spell);
            bool offensiveToTarget = HarmsCastRecipient(caster, spell);

            if (beneficialToTarget)
                return CanSupport(caster, target);

            if (offensiveToTarget)
                return !CanSupport(caster, target);

            // Ambiguous non-hostile utility: allow-list only.
            if (!spell.IsHostile)
                return CanSupport(caster, target);

            return !CanSupport(caster, target);
        }

        /// <summary>
        /// True when this nano should be aimed at allow-listed friendlies (heals, HoTs, team heals, buffs,
        /// or hostile-flagged programs that still heal/buff the cast recipient — e.g. Shade's Caress).
        /// </summary>
        public static bool IsSupportNano(Character? caster, NanoSpell spell)
            => spell != null && BenefitsCastRecipient(caster, spell);

        /// <summary>
        /// True when this nano should be aimed at enemies (damage, drains, hostile debuffs that do not
        /// heal/buff the cast recipient).
        /// </summary>
        public static bool IsOffensiveNano(Character? caster, NanoSpell spell)
            => spell != null && HarmsCastRecipient(caster, spell) && !BenefitsCastRecipient(caster, spell);

        /// <summary>OnUse (or tick) Hit that raises Health — a heal or heal-over-time.</summary>
        public static bool IsHealthHeal(NanoSpell spell)
        {
            if (spell == null)
                return false;

            foreach (List<ItemSpell> spells in spell.SpellList.Values)
            {
                for (int i = 0; i < spells.Count; i++)
                {
                    if (IsPositiveHealthHit(spells[i]))
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

        /// <summary>
        /// True when landing this nano on a recipient would heal or buff that recipient (not merely the caster).
        /// Covers direct heals, team heals, heal procs attached via AddDefProc/AddOffProc, and CastNano children
        /// that benefit the land target. Caster-only Wearer/User heals on a drain do not count.
        /// </summary>
        static bool BenefitsCastRecipient(Character? caster, NanoSpell spell, int depth = 0, HashSet<int>? seen = null)
        {
            if (spell == null || depth > MaxChildDepth)
                return false;

            seen ??= new HashSet<int>();
            if (spell.Id != 0 && !seen.Add(spell.Id))
                return false;

            foreach (List<ItemSpell> spells in spell.SpellList.Values)
            {
                for (int i = 0; i < spells.Count; i++)
                {
                    ItemSpell fn = spells[i];
                    if (AppliesToCastRecipient(fn) && IsPositiveHealthHit(fn))
                        return true;

                    if (fn.Is(FunctionType.TeamCastNano) || fn.Is(FunctionType.AreaCastNano))
                    {
                        // Team/area heal wrappers benefit allies around the cast, never the fight target as sole land.
                        if (TryReadNanoId(fn, out int teamChild)
                            && ResolveChild(caster, teamChild) is NanoSpell teamSpell
                            && (IsHealthHeal(teamSpell) || BenefitsCastRecipient(caster, teamSpell, depth + 1, seen)))
                            return true;
                    }

                    if ((fn.Is(FunctionType.AddDefProc) || fn.Is(FunctionType.AddOffProc))
                        && AppliesToCastRecipient(fn)
                        && TryReadProcNanoId(fn, out int procId)
                        && ResolveChild(caster, procId) is NanoSpell procSpell
                        && (IsHealthHeal(procSpell) || BenefitsCastRecipient(caster, procSpell, depth + 1, seen)))
                        return true;

                    if ((fn.Is(FunctionType.CastNano) || fn.Is(FunctionType.CastNano2)
                            || fn.Is(FunctionType.CastNanoIfPossible) || fn.Is(FunctionType.NpcCastNanoIfPossible)
                            || fn.Is(FunctionType.CastNanoIfPossibleOnFightTarget)
                            || fn.Is(FunctionType.NpcCastNanoIfPossibleOnFightTarget))
                        && AppliesToCastRecipient(fn)
                        && TryReadNanoId(fn, out int castId)
                        && ResolveChild(caster, castId) is NanoSpell castSpell
                        && BenefitsCastRecipient(caster, castSpell, depth + 1, seen))
                        return true;
                }
            }

            // Non-hostile buff with no damage-to-recipient: treat as support (buff the land target).
            if (!spell.IsHostile && spell.IsBuff && !HarmsCastRecipient(caster, spell, depth, seen: null))
                return true;

            return false;
        }

        /// <summary>
        /// True when landing this nano damages or otherwise harms the cast recipient (negative Hit/DrainHit/AreaHit
        /// on Target, or a hostile CastNano child that harms the recipient).
        /// </summary>
        static bool HarmsCastRecipient(Character? caster, NanoSpell spell, int depth = 0, HashSet<int>? seen = null)
        {
            if (spell == null || depth > MaxChildDepth)
                return false;

            seen ??= new HashSet<int>();
            if (spell.Id != 0 && !seen.Add(spell.Id))
                return false;

            foreach (List<ItemSpell> spells in spell.SpellList.Values)
            {
                for (int i = 0; i < spells.Count; i++)
                {
                    ItemSpell fn = spells[i];
                    if (!AppliesToCastRecipient(fn))
                        continue;

                    if (IsNegativeHealthHit(fn) || fn.Is(FunctionType.DrainHit) || fn.Is(FunctionType.AreaHit)
                        || fn.Is(FunctionType.SpecialHit))
                        return true;

                    if ((fn.Is(FunctionType.CastNano) || fn.Is(FunctionType.CastNano2)
                            || fn.Is(FunctionType.CastNanoIfPossible) || fn.Is(FunctionType.NpcCastNanoIfPossible)
                            || fn.Is(FunctionType.CastNanoIfPossibleOnFightTarget)
                            || fn.Is(FunctionType.NpcCastNanoIfPossibleOnFightTarget))
                        && TryReadNanoId(fn, out int castId)
                        && ResolveChild(caster, castId) is NanoSpell castSpell
                        && HarmsCastRecipient(caster, castSpell, depth + 1, seen))
                        return true;
                }
            }

            // Hostile programs with no classified heal-to-recipient are treated as offensive (debuffs, etc.).
            return spell.IsHostile && !IsHealthHeal(spell);
        }

        static bool AppliesToCastRecipient(ItemSpell fn)
        {
            int t = fn.Target;
            // 0/unset and explicit Target / Fighting / Selected aim at the land recipient.
            return t == 0
                || t == (int)ItemTarget.Target
                || t == (int)ItemTarget.Fightingtarget
                || t == (int)ItemTarget.Selectedtarget;
        }

        static bool IsPositiveHealthHit(ItemSpell fn)
            => fn.Is(FunctionType.Hit)
               && fn.TryReadInt(0, out int stat) && stat == (int)CharacterStat.Health
               && fn.TryReadInt(1, out int amount) && amount > 0;

        static bool IsNegativeHealthHit(ItemSpell fn)
            => fn.Is(FunctionType.Hit)
               && fn.TryReadInt(0, out int stat) && stat == (int)CharacterStat.Health
               && fn.TryReadInt(1, out int amount) && amount < 0;

        static bool TryReadNanoId(ItemSpell fn, out int nanoId)
            => fn.TryReadInt(0, out nanoId) && nanoId > 0;

        static bool TryReadProcNanoId(ItemSpell fn, out int nanoId)
        {
            nanoId = 0;
            if (!fn.TryReadInt(0, out int first) || !fn.TryReadInt(1, out int second))
                return false;
            // [chance %, nano] or [nano, chance]
            nanoId = first > 100 && second <= 100 ? first : second;
            if (nanoId <= 0)
                nanoId = first > 0 && first <= 100 ? second : first;
            return nanoId > 0;
        }

        static NanoSpell? ResolveChild(Character? caster, int nanoId)
        {
            if (nanoId <= 0 || caster?.Playfield == null)
                return null;
            IItemBuilder? items = caster.Playfield.GetService<IItemBuilder>();
            if (items == null)
                return null;
            ItemTemplate template = items.CreateTemplate(nanoId, nanoId, quality: 1);
            return template.Id == nanoId ? NanoSpell.From(template) : null;
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
