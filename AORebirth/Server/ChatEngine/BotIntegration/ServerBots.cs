namespace ChatEngine.BotIntegration
{
    using System;

    /// <summary>
    /// Built-in chat names for the zone buffbot and org bot. They are not player characters
    /// and they are outside the external bot wire-id range.
    /// </summary>
    internal static class ServerBots
    {
        public const uint BuffbotWireId = 0xE1000001;

        public const uint OrgbotWireId = 0xE1000002;

        public const string BuffbotName = "Buffbot";

        public const string OrgbotName = "Orgbot";

        public static bool TryResolve(string displayName, out uint wireId)
        {
            wireId = 0;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return false;
            }

            if (string.Equals(displayName, BuffbotName, StringComparison.OrdinalIgnoreCase))
            {
                wireId = BuffbotWireId;
                return true;
            }

            if (string.Equals(displayName, OrgbotName, StringComparison.OrdinalIgnoreCase))
            {
                wireId = OrgbotWireId;
                return true;
            }

            return false;
        }

        public static bool IsServerBot(uint wireId)
        {
            return wireId == BuffbotWireId || wireId == OrgbotWireId;
        }

        public static string NameOf(uint wireId)
        {
            if (wireId == OrgbotWireId)
            {
                return OrgbotName;
            }

            return BuffbotName;
        }
    }
}
