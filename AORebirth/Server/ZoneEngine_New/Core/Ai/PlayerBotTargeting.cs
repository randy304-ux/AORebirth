namespace ZoneEngine_New.Core.Ai
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// One shared prey gate for starter playerbots, modelled on mod-playerbots (AzerothCore):
    /// AttackersValue::IsPossibleTarget + RemoveNonThreating + GrindTargetValue. The same gate runs at every stage:
    /// scan (PlayerBotEngageNode), engage check (NpcBrain.ComputeEngageable), hate purge (PlayerBotHate.Purge)
    /// and the per-tick attack watch (PlayerBotWatchAttackNode). A target that fails is dropped and blacklisted.
    /// </summary>
    public static class PlayerBotTargeting
    {
        public static readonly TimeSpan BlacklistDuration = TimeSpan.FromSeconds(45);

        public static readonly TimeSpan IneffectiveAttackTimeout = TimeSpan.FromSeconds(5);

        public static readonly TimeSpan RoamStuckTimeout = TimeSpan.FromSeconds(8);

        public const float RoamStuckMeters = 0.75f;

        public static bool IsValidPrey(NpcCharacter bot, Character? target, bool applyLevelRange)
            => IsPossibleTarget(bot, target, applyLevelRange, out _);

        /// <summary>
        /// Returns false with a short reason when <paramref name="target"/> must not be fought by the bot.
        /// </summary>
        public static bool IsPossibleTarget(NpcCharacter bot, Character? target, bool applyLevelRange, out string reason)
        {
            // WoW: !IsInWorld / different map / dead.
            if (bot == null || target == null || ReferenceEquals(bot, target))
                return Fail(out reason, "none");
            if (target.IsDead)
                return Fail(out reason, "dead");
            if (bot.Playfield != null && !ReferenceEquals(target.Playfield, bot.Playfield))
                return Fail(out reason, "other playfield");

            // WoW: unflagged players / no-PvP zones. Starter bots never fight players or player-owned NPCs.
            if (target is Player)
                return Fail(out reason, "player");
            if (target is not NpcCharacter npc)
                return Fail(out reason, "not npc");
            if (npc.Brain?.PlayerBot != null)
                return Fail(out reason, "other playerbot");
            if (NpcSupportRules.IsPlayerPetOrMerc(npc) || npc.PetOwner is Player)
                return Fail(out reason, "player pet/merc");

            // WoW: UNIT_FLAG_NON_ATTACKABLE / IMMUNE_TO_PC / NOT_SELECTABLE.
            if (!npc.Attackable)
                return Fail(out reason, "unattackable");
            MobTemplate? template = npc.MobTemplate;
            if (template != null && template.Has(NpcFeature.NoCombat))
                return Fail(out reason, "no-combat");
            if (template != null && template.Has(NpcFeature.PlayerUntargetable))
                return Fail(out reason, "player-untargetable");

            // WoW: immune to all damage / gives no XP.
            if (template != null && template.Has(NpcFeature.Immortal))
                return Fail(out reason, "immortal");

            // WoW: IsFriendlyTo + GrindTarget "non-hostile unit with NPC flags" (vendor, quest giver, trainer).
            if (CombatRules.IsBlueNameNpc(npc.Stats.GetOrZero(CharacterStat.Flags)))
                return Fail(out reason, "friendly (blue name)");
            if (npc.Shop != null)
                return Fail(out reason, "vendor");
            var definition = WorldNpcFactory.DefinitionFor(npc);
            if (definition != null)
            {
                if (definition.Vendor != null)
                    return Fail(out reason, "vendor");
                if (definition.Passive)
                    return Fail(out reason, "passive town npc");
            }

            bool hostile = NpcAiRules.IsProximityHostile(npc.Stats.GetOrZero(NpcAiRules.BreedHostilityStat));
            if (!hostile && IsServiceNpc(npc, template))
                return Fail(out reason, "service npc");

            // WoW: same faction (IsFriendlyTo). A Clan/Omni NPC is friendly to a bot of its own side.
            int botSide = bot.Stats.GetOrZero(CharacterStat.Side);
            int npcSide = npc.Stats.GetOrZero(CharacterStat.Side);
            if ((botSide == (int)Side.Clan || botSide == (int)Side.Omni) && npcSide == botSide)
                return Fail(out reason, "same side");

            // WoW: evade mode (reset/leash) and polymorph (hasRealThreat).
            if (npc.Brain != null && npc.Brain.IsEvading)
                return Fail(out reason, "evading");
            if (npc.IsPacified)
                return Fail(out reason, "pacified");

            // WoW: tapped by someone else. Leave a mob alone while a real player is fighting it,
            // unless it is already fighting this bot.
            if (IsTaggedByPlayer(bot, npc))
                return Fail(out reason, "tagged by player");

            // The same rule the damage step and the player attack path use.
            if (!CombatRules.CanAttack(bot, npc))
                return Fail(out reason, "CanAttack refused");

            // WoW GrindTargetValue: wrong level.
            if (applyLevelRange)
            {
                PlayerBotState? state = bot.Brain?.PlayerBot;
                if (state != null)
                {
                    int level = npc.Stats.GetOrZero(CharacterStat.Level);
                    if (level < state.MinPreyLevel || level > state.MaxPreyLevel + 3)
                        return Fail(out reason, "level");
                }
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>Mission givers, dialog (Knubot) NPCs and terminals.</summary>
        internal static bool IsServiceNpc(NpcCharacter npc, MobTemplate? template)
        {
            if (template != null && (template.KnuBotId != 0 || template.ShopStock.Count > 0))
                return true;
            if (npc.IsMailTerminal)
                return true;
            string name = npc.Name ?? string.Empty;
            return name.EndsWith("Terminal", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsTaggedByPlayer(NpcCharacter bot, NpcCharacter npc)
        {
            if (npc.FightingTarget == bot.Identity || npc.Playfield == null)
                return false;
            NpcBrain? brain = npc.Brain;
            if (brain != null && brain.Hate.Contains(bot.Identity))
                return false;

            DynelRegistry registry = npc.Playfield.GetRequiredService<DynelRegistry>();
            if (IsPlayer(registry, npc.FightingTarget))
                return true;
            if (brain == null)
                return false;
            foreach (HateEntry entry in brain.Hate.Entries)
            {
                if (IsPlayer(registry, entry.Identity))
                    return true;
            }

            return false;
        }

        static bool IsPlayer(DynelRegistry registry, Identity identity)
            => identity.Instance != 0 && registry.TryGet(identity, out Dynel? dynel) && dynel is Player;

        static bool Fail(out string reason, string why)
        {
            reason = why;
            return false;
        }

        public static bool IsBlacklisted(PlayerBotState? state, Identity identity, DateTime nowUtc)
        {
            if (state == null || identity.Instance == 0)
                return false;
            if (!state.TryGetBlacklist(identity, out DateTime untilUtc))
                return false;
            if (nowUtc < untilUtc)
                return true;
            state.ClearBlacklist(identity);
            return false;
        }

        public static void Blacklist(PlayerBotState? state, Identity identity, DateTime nowUtc)
        {
            if (state == null || identity.Instance == 0)
                return;
            state.Blacklist(identity, nowUtc + BlacklistDuration);
        }
    }
}
