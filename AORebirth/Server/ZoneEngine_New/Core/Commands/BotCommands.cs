namespace ZoneEngine_New.Core.Commands
{
    using System;

    using ZoneEngine_New.Core.Bots;

    /// <summary>.buff for everyone. The info window is the menu. Casting uses the real nano.</summary>
    public sealed class BuffbotCommand : IGmCommand
    {
        readonly BuffbotService _buffs;

        public BuffbotCommand(BuffbotService buffs)
        {
            ArgumentNullException.ThrowIfNull(buffs);
            _buffs = buffs;
        }

        public string Name => "buff";

        public int RequiredGmLevel => 0;

        public string Usage => ".buff";

        public string Topic => "player";

        public string Summary => "Buffbot menu. macro, rebuff, status, or cast. No charge. Tell Buffbot the same.";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _buffs.Execute(context.Player, context.Args);
        }
    }

    /// <summary>.org for everyone. Forming an org creates the org bot.</summary>
    public sealed class OrgBotCommand : IGmCommand
    {
        readonly OrgBotService _orgs;

        public OrgBotCommand(OrgBotService orgs)
        {
            ArgumentNullException.ThrowIfNull(orgs);
            _orgs = orgs;
        }

        public string Name => "org";

        public int RequiredGmLevel => 0;

        public string Usage => ".org";

        public string Topic => "team";

        public string Summary => "Org bot. create, online, members, lastseen, motd, news, alts, add, raid, loot. Tell Orgbot the same.";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _orgs.Execute(context.Player, context.Args);
        }
    }
}
