namespace ZoneEngine_New.Core.Inventory
{
    using System;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Quests;
    using ZoneEngine_New.Core.Teams;

    /// <summary>
    /// Requirement operators the Operator enum does not name, read from items.dat and the client's own requirement text
    /// (Nano Can: Friendly Buff 301250, ToUse [OnUser, 117 W4XY, 138 3, And, OnTarget, 127 301249, And]; the client's
    /// info window shows them as "Must have specific active mission", "Must be in team with target" and "Must have
    /// enough free NCU for: Help From a Friend").
    /// </summary>
    internal static class MissionRequirements
    {
        /// <summary>Value = the mission's 4-character hash packed big-endian (1463048281 = "W4XY").</summary>
        public const int HasActiveMission = 117;

        /// <summary>Value = nano id; the subject has room in NCU for it (strain, stacking and NCU as a real cast).</summary>
        public const int HasFreeNcuFor = 127;

        /// <summary>The user is in the same team as the action's target (the target is another player).</summary>
        public const int InTeamWithTarget = 138;

        /// <summary>"W4XY" from 1463048281: four ASCII bytes, high byte first. Null when it is not printable.</summary>
        public static string? HashText(int value)
        {
            var chars = new char[4];
            for (int i = 0; i < 4; i++)
            {
                int b = (value >> (24 - 8 * i)) & 0xFF;
                if (b < 0x20 || b > 0x7E)
                    return null;
                chars[i] = (char)b;
            }

            return new string(chars);
        }

        /// <summary>An active quest of <paramref name="player"/> whose Hash or SolveHash is <paramref name="hash"/>.</summary>
        public static PlayerQuest? FindActive(Player player, string? hash)
        {
            if (string.IsNullOrEmpty(hash) || player.Playfield?.GetService<QuestService>() is not QuestService quests)
                return null;

            foreach (PlayerQuest quest in quests.GetLog(player).Quests.Values)
            {
                if (quest.IsActive
                    && (string.Equals(quest.Template.Hash, hash, StringComparison.Ordinal)
                        || string.Equals(quest.Template.SolveHash, hash, StringComparison.Ordinal)))
                    return quest;
            }

            return null;
        }

        public static bool HasActive(object user, int value)
            => user is Player player && FindActive(player, HashText(value)) != null;

        public static bool AreTeammates(object user, object? target)
            => user is Player first && target is Player second && !ReferenceEquals(first, second)
                && first.Playfield?.GetService<TeamService>() is TeamService teams
                && teams.AreTeammates(first, second);
    }
}
