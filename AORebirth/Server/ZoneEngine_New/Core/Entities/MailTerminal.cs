namespace ZoneEngine_New.Core.Entities
{
    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Claims;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// A placed mail terminal. Using it opens the mailbox. Claim Center rows are the mail that can be taken.
    /// </summary>
    public sealed class MailTerminal : StaticDynel
    {
        public MailTerminal(Identity identity, ItemTemplate template)
            : base(identity, template)
        {
        }

        /// <summary>Opens a window. There is no cast time.</summary>
        protected override bool DelaysUse => false;

        protected override bool OnUse(Player player)
        {
            if (player.Session == null || Playfield == null)
                return false;

            if (Playfield.GetService<ClaimService>() is ClaimService claims && player.Session != null)
                player.Session.Send(claims.BuildList(player));
            return true;
        }
    }
}
