namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Bots;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// Client org UI (<c>/org create …</c> and related). Capture/live: OrgClientMessage was
    /// deserialized then ignored — create never persisted. Names with spaces arrive in CommandArgs.
    /// </summary>
    public sealed class OrgClientMessageHandler : IMessageHandler<OrgClientMessage>
    {
        readonly OrgBotService _orgs;

        public OrgClientMessageHandler(OrgBotService orgs)
        {
            _orgs = orgs ?? throw new ArgumentNullException(nameof(orgs));
        }

        public Type MessageBodyType => typeof(OrgClientMessage);

        public void Handle(MessageBody body, IZoneSession session)
            => Handle((OrgClientMessage)body, session);

        public void Handle(OrgClientMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);
            if (session.State != SessionState.InPlay || session.Player is not { } player
                || !ReferenceEquals(player.Session, session) || player.IsPersistenceQuarantined)
                return;

            switch (message.Command)
            {
                case OrgClientCommand.Create:
                    _orgs.CreateOrganization(player, message.CommandArgs ?? string.Empty);
                    break;
                default:
                    // Other org client verbs (invite, kick, bank, …) are not wired yet.
                    break;
            }
        }
    }
}
