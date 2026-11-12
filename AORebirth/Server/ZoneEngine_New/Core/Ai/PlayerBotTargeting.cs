namespace ZoneEngine_New.Core.Ai
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Mobs;

    /// <summary>
    /// Shared prey rules for starter playerbots. Used at acquisition and again while a target is already on the
    /// hate list, so a friendly or unattackable NPC cannot be locked forever after a refused or empty swing.
    /// </summary>
    public static class PlayerBotTargeting
    {
        public static readonly TimeSpan BlacklistDuration = TimeSpan.FromSeconds(45);

        public static readonly TimeSpan IneffectiveAttackTimeout = TimeSpan.FromSeconds(5);

        public static readonly TimeSpan RoamStuckTimeout = TimeSpan.FromSeconds(8);

        public const float RoamStuckMeters = 0.75f;

        public static bool IsValidPrey(NpcCharacter bot, Character? target, bool applyLevelRange)
        {
            if (bot == null || target == null || ReferenceEquals(bot, target) || target.IsDead)
                return false;
            if (target is Player)
                return false;
            if (target is not NpcCharacter npc)
                return false;
            if (!npc.Attackable)
                return false;
            if (npc.Brain?.PlayerBot != null)
                return false;
            if (NpcSupportRules.IsPlayerPetOrMerc(npc) || npc.PetOwner is Player)
                return false;
            if (npc.Shop != null)
                return false;
            if (npc.MobTemplate != null
                && (npc.MobTemplate.Has(NpcFeature.NoCombat)
                    || npc.MobTemplate.Has(NpcFeature.PlayerUntargetable)
                    || npc.MobTemplate.Has(NpcFeature.Immortal)))
                return false;
            if (CombatRules.IsBlueNameNpc(npc.Stats.GetOrZero(CharacterStat.Flags)))
                return false;
            if (!CombatRules.CanAttack(bot, npc))
                return false;

            if (applyLevelRange)
            {
                PlayerBotState? state = bot.Brain?.PlayerBot;
                if (state != null)
                {
                    int level = npc.Stats.GetOrZero(CharacterStat.Level);
                    if (level < state.MinPreyLevel || level > state.MaxPreyLevel + 3)
                        return false;
                }
            }

            return true;
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