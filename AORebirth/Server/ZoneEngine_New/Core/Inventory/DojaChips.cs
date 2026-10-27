namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Events;
    using ZoneEngine_New.Core.Quests;

    /// <summary>
    /// Shadowlands DOJA chips: right-click assigns the matching return daily (Quests.json DJ**)
    /// if the player is in the chip's level band and not lock-out blocked. Chip stays until Scarlett takes it.
    /// </summary>
    public static class DojaChips
    {
        static readonly Dictionary<int, ChipSpec> ById = new()
        {
            [284954] = new("DJNS", DailyLockoutService.TrackDoja, 1, 60, "Nascense"),
            [284955] = new("DJEL", DailyLockoutService.TrackDoja, 61, 100, "Elysium"),
            [284956] = new("DJSC", DailyLockoutService.TrackDoja, 101, 130, "Scheol"),
            [284957] = new("DJAD", DailyLockoutService.TrackDoja, 131, 160, "Adonis"),
            [284958] = new("DJPN", DailyLockoutService.TrackDoja, 161, 204, "Penumbra"),
            [284959] = new("DJIN", DailyLockoutService.TrackDoja, 205, 220, "Inferno"),
            [284961] = new("DJAA", DailyLockoutService.TrackDoja, 201, 210, "Alappaa"),
            [284962] = new("DJAB", DailyLockoutService.TrackDoja, 211, 219, "Albtraum"),
            [284960] = new("DJPD", DailyLockoutService.TrackDoja, 220, 220, "Pandemonium"),
            [282144] = new("DJDR", DailyLockoutService.TrackDojaDarkRuins, 1, 200, "Dark Ruins"),
        };

        public static bool TryUse(Item item, Player player)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(player);

            if (!ById.TryGetValue(item.LowId, out ChipSpec? spec) && !ById.TryGetValue(item.HighId, out spec))
                return false;

            if (player.Session == null || player.Playfield == null || !player.Inventory.IsHydrated)
                return true;

            int level = player.Stats.GetOrOne(CharacterStat.Level);
            if (level < spec.MinLevel || level > spec.MaxLevel)
            {
                Say(player, "You must be level " + spec.MinLevel + "-" + spec.MaxLevel + " to use the "
                    + spec.Zone + " DOJA chip.");
                return true;
            }

            DailyLockoutService? locks = DailyLockoutService.Instance;
            if (locks != null && !locks.TryBegin(player, spec.Track, out string lockMsg))
            {
                Say(player, string.IsNullOrEmpty(lockMsg) ? "DOJA lock-out is active." : lockMsg);
                return true;
            }

            QuestService? quests = player.Playfield.GetService<QuestService>();
            if (quests == null)
            {
                Say(player, "DOJA missions are unavailable.");
                return true;
            }

            if (quests.GetLog(player).Quests.TryGetValue(spec.QuestHash, out PlayerQuest? held) && held.IsActive)
            {
                Say(player, "You already have a DOJA return mission. Take the chip to Scarlett Dalquist in Jobe Research Lab R1.");
                return true;
            }

            if (spec.Track == DailyLockoutService.TrackDoja)
            {
                foreach (PlayerQuest q in quests.GetLog(player).Quests.Values)
                {
                    if (q.IsActive && q.QuestId.StartsWith("DJ", StringComparison.Ordinal) && q.QuestId != "DJDR")
                    {
                        Say(player, "You already loaded a DOJA return mission. Finish or delete it first.");
                        return true;
                    }
                }
            }

            if (!quests.TryAssignTemplate(player, spec.QuestHash, out string result))
            {
                Say(player, result);
                return true;
            }

            Say(player, "DOJA chip accepted. Return this " + spec.Zone
                + " chip to Scarlett Dalquist in Jobe Research Lab R1.");
            return true;
        }

        public static bool IsDojaTemplate(int id) => ById.ContainsKey(id);

        public static string? TrackForQuest(string questHash)
        {
            foreach (ChipSpec spec in ById.Values)
            {
                if (string.Equals(spec.QuestHash, questHash, StringComparison.OrdinalIgnoreCase))
                    return spec.Track;
            }

            return null;
        }

        static void Say(Player player, string text)
            => player.Session?.Send(new ChatTextMessage { Identity = player.Identity, Text = text });

        sealed record ChipSpec(string QuestHash, string Track, int MinLevel, int MaxLevel, string Zone);
    }
}
