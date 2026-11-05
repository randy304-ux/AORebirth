namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Globalization;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Missions;

    /// <summary>
    /// <c>.mishroll</c>: pin or clear a preferred solo Rubi-Ka terminal mission reward item.
    /// Solo rolls use the pin when eligible; accepting a matching offer charges the QL-scaled targeting fee.
    /// </summary>
    public sealed class MishrollCommand : IGmCommand
    {
        readonly MishrollService _mishroll;

        public MishrollCommand(MishrollService mishroll)
            => _mishroll = mishroll ?? throw new ArgumentNullException(nameof(mishroll));

        public string Name => "mishroll";

        public int RequiredGmLevel => 0;

        public string Usage => ".mishroll | .mishroll <itemId> | .mishroll clear";

        public string Topic => "player";

        public string Summary =>
            "Pin a preferred solo RK terminal mission reward (item id). Clear with .mishroll clear. Disabled unless Gameplay.json mishroll.enabled is true.";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Player player = context.Player;
            void Say(string text) => GmCommandFeedback.Send(context.Session, player, text);

            if (!_mishroll.IsEnabled)
            {
                Say("Mishroll is disabled on this server.");
                return;
            }

            int characterId = player.Identity.Instance;
            if (context.Args.Length == 0)
            {
                if (_mishroll.TryGetPinnedItemId(characterId, out int pinned))
                    Say("Mishroll pin: item " + pinned.ToString(CultureInfo.InvariantCulture)
                        + ". Solo RK terminal rolls use this reward; accepting charges a QL-scaled targeting fee. .mishroll clear removes it.");
                else
                    Say("No mishroll pin. Usage: .mishroll <itemId> | .mishroll clear");
                return;
            }

            string arg0 = context.Args[0];
            if (string.Equals(arg0, "clear", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg0, "off", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg0, "none", StringComparison.OrdinalIgnoreCase))
            {
                string? clearError = _mishroll.TryClear(characterId);
                Say(clearError ?? "Mishroll pin cleared.");
                return;
            }

            if (context.Args.Length != 1
                || !int.TryParse(arg0, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId)
                || itemId <= 0)
            {
                Say("Usage: .mishroll <itemId> | .mishroll clear");
                return;
            }

            string? pinError = _mishroll.TryPin(characterId, itemId);
            Say(pinError ?? ("Mishroll pin set to item " + itemId.ToString(CultureInfo.InvariantCulture) + "."));
        }
    }
}
