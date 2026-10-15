namespace ZoneEngine_New.Core.Teams
{
    using System;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.MessageHandlers;
    using ZoneEngine_New.Core.Network;

    /// <summary>The N3 ChatCmd route: /team, /invite, /stuck and /terminate; never consumes ordinary vicinity text.</summary>
    public sealed class TeamChatMessageHandler(TeamService teams, Characters.StuckService stuck) : IMessageHandler<ChatCmdMessage>
    {
        /// <summary>Chat slash commands this handler accepts. Keep in step with <see cref="TeamService.TryHandleChatCommand"/>.</summary>
        public static readonly (string Syntax, string Summary)[] Commands =
        [
            ("/team invite <name>", "Invites that player onto your team."),
            ("/invite <name>", "Same as /team invite."),
            ("/team accept", "Accepts a team invite."),
            ("/team decline", "Declines a team invite."),
            ("/team reject", "Same as /team decline."),
            ("/team leave", "Leaves your team.")
        ];

        public Type MessageBodyType => typeof(ChatCmdMessage);
        public void Handle(MessageBody body, IZoneSession session) => Handle((ChatCmdMessage)body, session);

        public void Handle(ChatCmdMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);
            Player? player = session.Player;
            if (session.State != SessionState.InPlay || player == null || !ReferenceEquals(player.Session, session)
                || message.Identity != player.Identity || player.IsPersistenceQuarantined) return;
            string[] args = CommandInput.Tokenize(message.Command);
            if (args.Length == 0) return;
            if (args[0].Equals("team", StringComparison.OrdinalIgnoreCase))
                teams.TryHandleChatCommand(player, args);
            else if (args[0].Equals("invite", StringComparison.OrdinalIgnoreCase) && args.Length == 2)
                teams.TryHandleChatCommand(player, ["team", "invite", args[1]]);
            else if (args[0].Equals("terminate", StringComparison.OrdinalIgnoreCase))
                Terminate(player);
            else if (args[0].Equals("stuck", StringComparison.OrdinalIgnoreCase))
                stuck.TryUnstick(player);
            else
                player.Logger.Warn("Unsupported ChatCmd command=" + args[0]);
        }

        /// <summary>/terminate after the client confirm: the player kills itself through the normal damage and death path, so reclaim and respawn run as usual.</summary>
        static void Terminate(Player player)
        {
            if (player.IsDead)
                return;
            int health = Math.Max(1, player.Stats.GetOrZero(SmokeLounge.AOtomation.Messaging.GameData.CharacterStat.Health));
            if (!player.ApplyDamage(player, health, SmokeLounge.AOtomation.Messaging.GameData.HitType.Normal) && !player.IsDead)
                player.OnDeath(player);
        }
    }
}
