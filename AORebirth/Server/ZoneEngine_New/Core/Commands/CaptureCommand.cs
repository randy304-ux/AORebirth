namespace ZoneEngine_New.Core.Commands
{
    using System;

    using ZoneEngine_New.Core.Network;

    public sealed class CaptureCommand : IGmCommand
    {
        public string Name => "capture";
        public int RequiredGmLevel => 1;
        public string Usage => ".capture [on|off]";
        public string Summary => "Logs every inbound client packet (full hex) to " + InboundCapture.DefaultPath + ".";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (context.Args.Length > 0)
                InboundCapture.Enabled = !string.Equals(context.Args[0], "off", StringComparison.OrdinalIgnoreCase);
            GmCommandFeedback.Send(context.Session, context.Player, "Inbound capture " + (InboundCapture.Enabled ? "ON" : "off") + " -> " + InboundCapture.Path);
        }
    }
}
