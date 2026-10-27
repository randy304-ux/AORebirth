namespace ZoneEngine_New.Core.Commands
{
    using System;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Events;

    /// <summary>
    /// <c>.event</c> (GM): seasonal content toggles. First switch: <c>.event halloween on|off|status</c>.
    /// </summary>
    public sealed class EventCommand : IGmCommand
    {
        readonly HalloweenService _halloween;

        public EventCommand(HalloweenService halloween)
        {
            _halloween = halloween ?? throw new ArgumentNullException(nameof(halloween));
        }

        public string Name => "event";

        public int RequiredGmLevel => 1;

        public string Usage => ".event halloween on|off|status";

        public string Summary => "Turns seasonal outdoor events on or off. Halloween spawns low outdoor Uncle Pumpkin-Heads and city leets.";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Player player = context.Player;
            void Say(string text) => GmCommandFeedback.Send(context.Session, player, text);

            if (context.Args.Length < 1)
            {
                Say("Usage: " + Usage);
                return;
            }

            string which = context.Args[0];
            if (!string.Equals(which, "halloween", StringComparison.OrdinalIgnoreCase))
            {
                Say("Unknown event '" + which + "'. Known: halloween.");
                return;
            }

            string sub = context.Args.Length > 1 ? context.Args[1] : "status";
            switch (sub.ToLowerInvariant())
            {
                case "on":
                case "start":
                case "enable":
                    Say(_halloween.Enable());
                    return;
                case "off":
                case "stop":
                case "disable":
                    Say(_halloween.Disable());
                    return;
                case "status":
                    Say(_halloween.Status());
                    return;
                default:
                    Say("Usage: " + Usage);
                    return;
            }
        }
    }
}
