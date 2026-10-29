namespace ZoneEngine_New.Core.Commands
{
    using System;

    using ZoneEngine_New.Core.Battlestations;

    /// <summary>
    /// <c>.bs leave</c> (everyone): stuck escape from a live Battle Station match back to the hub lobby.
    /// Works while under attack (unlike <c>/stuck</c>).
    /// </summary>
    public sealed class BattleStationCommand : IGmCommand
    {
        public string Name => "bs";

        public int RequiredGmLevel => 0;

        public string Usage => ".bs leave";

        public string Topic => "travel";

        public string Summary => "Leave a Battle Station match and return to the lobby (works while attacked).";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            void Say(string s) => GmCommandFeedback.Send(context.Session, context.Player, s);

            if (context.Args.Length == 0
                || !string.Equals(context.Args[0], "leave", StringComparison.OrdinalIgnoreCase))
            {
                Say("Usage: " + Usage);
                return;
            }

            if (!BattleStationClock.TryLeave(context.Player, out string message))
            {
                Say(string.IsNullOrEmpty(message) ? "You are not in a battle station match." : message);
                return;
            }

            // Leave feedback is already sent in-match via BattleStationService.
            if (!string.IsNullOrEmpty(message))
                Say(message);
        }
    }
}
