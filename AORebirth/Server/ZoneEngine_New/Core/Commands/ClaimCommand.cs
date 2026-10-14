namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Concurrent;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    using ZoneEngine_New.Core.Claims;

    /// <summary>
    /// <c>.claim</c> (everyone): shows your Info-window claim code and delivers anything already claimed.
    /// <c>.claim list</c> shows the Claim Center items; <c>.claim &lt;number|key&gt;</c> claims one directly (once per character).
    /// </summary>
    public sealed class ClaimCommand : IGmCommand
    {
        static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(3);
        readonly ClaimService _claims;
        readonly ConcurrentDictionary<int, DateTime> _lastUseUtc = new();

        public ClaimCommand(ClaimService claims) => _claims = claims ?? throw new ArgumentNullException(nameof(claims));

        public string Name => "claim";

        public int RequiredGmLevel => 0;

        public string Usage => ".claim (your claim code + deliver) | .claim list | .claim <number>";

        public string Topic => "claims";

        public string Summary => "Shows your claim code and delivers claimed items, or lists and claims from chat.";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            int id = context.Player.Identity.Instance;
            DateTime now = DateTime.UtcNow;
            if (_lastUseUtc.TryGetValue(id, out DateTime last) && now - last < Cooldown)
                return;
            _lastUseUtc[id] = now;

            void Say(string s) => GmCommandFeedback.Send(context.Session, context.Player, s);
            try
            {
                if (context.Args.Length == 0)
                {
                    Say("Your claim code is " + _claims.GetOrCreateCode(id) + ". Open the Info window (Shop menu > Store) > Claim Center and enter it. Type .claim list to claim from chat instead.");
                    int n = _claims.DeliverPending(context.Player);
                    if (n == 0)
                        Say("No claimed items are waiting for you.");
                    return;
                }

                var catalog = _claims.LoadCatalog();
                if (string.Equals(context.Args[0], "list", StringComparison.OrdinalIgnoreCase))
                {
                    var done = _claims.ClaimedKeys(id);
                    var sb = new StringBuilder("Claim Center (type .claim <number>):");
                    for (int i = 0; i < catalog.Count; i++)
                        sb.Append(CultureInfo.InvariantCulture, $"\n{i + 1}. {catalog[i].Name}{(done.Contains(catalog[i].Key) ? " (claimed)" : string.Empty)}");
                    Say(sb.ToString());
                    return;
                }

                string arg = string.Join(' ', context.Args);
                ClaimService.CatalogItem? item = int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                    && index >= 1 && index <= catalog.Count
                    ? catalog[index - 1]
                    : catalog.FirstOrDefault(c => string.Equals(c.Key, arg, StringComparison.OrdinalIgnoreCase));
                if (item == null)
                {
                    Say("Unknown item. Type .claim list to see the numbers.");
                    return;
                }

                if (!_claims.Queue(id, item, "chat"))
                {
                    Say("You already claimed " + item.Name + ".");
                    return;
                }

                _claims.DeliverPending(context.Player);
            }
            catch (Exception ex)
            {
                Say("Claim Center is unavailable right now: " + ex.Message);
            }
        }
    }
}
