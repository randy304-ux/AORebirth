namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Linq;

    using ZoneEngine_New.Core.Appearance;

    /// <summary>
    /// <c>.battlesuit</c> (everyone): shows or sets the battle suit look toggles, the same settings as the Info window's
    /// Battlesuits page. <c>.battlesuit look on|off</c> draws the suit body; <c>.battlesuit only on|off</c> hides the
    /// other worn armor while the suit shows.
    /// </summary>
    public sealed class BattlesuitCommand : IGmCommand
    {
        readonly AppearancePrefsService _prefs;

        public BattlesuitCommand(AppearancePrefsService prefs) => _prefs = prefs ?? throw new ArgumentNullException(nameof(prefs));

        public string Name => "battlesuit";

        public int RequiredGmLevel => 0;

        public string Usage => ".battlesuit | .battlesuit look on|off | .battlesuit only on|off";

        public string Topic => "appearance";

        public string Summary => "Battle suit look: draw the suit over your armor, or show only the suit.";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            int id = context.Player.Identity.Instance;
            AppearanceGroup group = AppearanceGroups.BattleSuit;
            void Say(string s) => GmCommandFeedback.Send(context.Session, context.Player, s);
            string State() => "Battle suit appearance: " + (_prefs.Get(id, group.LookKey, 1) != 0 ? "ON" : "OFF")
                + ". Only show battle suit: " + (_prefs.Get(id, group.OnlyKey, 0) != 0 ? "ON" : "OFF") + ".";

            if (context.Args.Length == 0)
            {
                Say(State() + " Change with .battlesuit look on|off or .battlesuit only on|off.");
                return;
            }

            string which = context.Args[0].ToLowerInvariant();
            string? key = which == "look" ? group.LookKey : which == "only" ? group.OnlyKey : null;
            string? arg = context.Args.Length > 1 ? context.Args[1].ToLowerInvariant() : null;
            if (key == null || arg is not ("on" or "off"))
            {
                Say("Usage: " + Usage);
                return;
            }

            try
            {
                _prefs.Set(id, key, arg == "on" ? 1 : 0);
            }
            catch (Exception ex)
            {
                Say("Could not save the setting: " + ex.Message);
                return;
            }

            context.Player.RebaseStats();
            Say(State());
        }
    }
}