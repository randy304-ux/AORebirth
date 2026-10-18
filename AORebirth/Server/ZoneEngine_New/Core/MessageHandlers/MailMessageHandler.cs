namespace ZoneEngine_New.Core.MessageHandlers;

using System;

using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

using ZoneEngine_New.Core.Claims;
using ZoneEngine_New.Core.Network;

/// <summary>Mail terminal window. Claim Center rows are the messages that can be taken.</summary>
public sealed class MailMessageHandler(ClaimService claims) : IMessageHandler<MailMessage>
{
    public Type MessageBodyType => typeof(MailMessage);

    public void Handle(MessageBody body, IZoneSession session) => Handle((MailMessage)body, session);

    public void Handle(MailMessage message, IZoneSession session)
    {
        var player = session.Player;
        if (session.State != SessionState.InPlay || player == null || player.IsPersistenceQuarantined || player.IsDead)
            return;

        player.Logger.Info("Mail recv char=" + player.Identity.Instance + " action=" + message.Action + "(" + (int)message.Action
            + ") mail=" + message.RequestedMailId);
        switch (message.Action)
        {
            case MailAction.OpenOrRequest:
                if (message.RequestedMailId == 0)
                    session.Send(claims.BuildList(player));
                else if (claims.BuildDetail(player, message.RequestedMailId) is MailMessage detail)
                    session.Send(detail);
                break;

            case MailAction.TakeAll:
                claims.TryTake(player, message.RequestedMailId);
                break;

            case MailAction.Delete:
            case MailAction.ReturnToSender:
            case MailAction.SendMail:
                Commands.GmCommandFeedback.Send(session, player, "Take All collects a Claim Center item. Sending mail is not available.");
                break;
        }
    }
}
