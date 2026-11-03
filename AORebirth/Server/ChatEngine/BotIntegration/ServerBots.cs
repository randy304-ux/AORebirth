namespace ChatEngine.BotIntegration
{
    using System;

    /// <summary>
    /// Built-in chat names for the zone buffbot, org bot and merc menu. They are not player characters
    /// and they are outside the external bot wire-id range.
    /// </summary>
    internal static class ServerBots
    {
        public const uint BuffbotWireId = 0xE1000001;

        public const uint OrgbotWireId = 0xE1000002;

        public const uint MercWireId = 0xE1000003;

        public const uint MercIdBase = 0xE1000100;

        public const string BuffbotName = "Buffbot";

        public const string OrgbotName = "Orgbot";

        public const string MercName = "Merc";

        /// <summary>Keep in sync with GameData/MercRoster.json ids (and ZoneEngine BotChatDispatch.MercTellIds).</summary>
        static readonly string[] MercIds =
        {
            "enforcer_subway", "doctor_totw", "bureaucrat_som", "fixer_biomare", "soldier_cyborg",
            "adventurer_crypt", "keeper_smugglers", "nt_sanctum_aoe", "nt_sanctum_st", "shade_camelot",
            "ma_camelot", "trader_elysium", "engineer_scheol", "mp_adonis", "agent_penumbra"
        };

        static readonly string[] MercDisplayAliases =
        {
            "TODO-Randy-Name (Enforcer)",
            "TODO-Randy-Name (Doctor)",
            "TODO-Randy-Name (Bureaucrat)",
            "TODO-Randy-Name (Fixer)",
            "TODO-Randy-Name (Soldier)",
            "TODO-Randy-Name (Adventurer)",
            "TODO-Randy-Name (Keeper)",
            "TODO-Randy-Name (AOE)",
            "TODO-Randy-Name (ST)",
            "TODO-Randy-Name (Shade)",
            "TODO-Randy-Name (Martial Artist)",
            "TODO-Randy-Name (Trader)",
            "TODO-Randy-Name (Engineer)",
            "TODO-Randy-Name (Meta-Physicist)",
            "TODO-Randy-Name (Agent)"
        };

        public static bool TryResolve(string displayName, out uint wireId)
        {
            wireId = 0;
            if (string.IsNullOrWhiteSpace(displayName))
                return false;

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

            if (string.Equals(displayName, MercName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(displayName, "Mercbot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(displayName, "MercMenu", StringComparison.OrdinalIgnoreCase))
            {
                wireId = MercWireId;
                return true;
            }

            for (int i = 0; i < MercIds.Length; i++)
            {
                if (string.Equals(displayName, MercIds[i], StringComparison.OrdinalIgnoreCase)
                    || (i < MercDisplayAliases.Length
                        && string.Equals(displayName, MercDisplayAliases[i], StringComparison.OrdinalIgnoreCase)))
                {
                    wireId = MercIdBase + (uint)i;
                    return true;
                }
            }

            return false;
        }

        public static bool IsServerBot(uint wireId)
        {
            if (wireId == BuffbotWireId || wireId == OrgbotWireId || wireId == MercWireId)
                return true;
            return wireId >= MercIdBase && wireId < MercIdBase + (uint)MercIds.Length;
        }

        public static string NameOf(uint wireId)
        {
            if (wireId == OrgbotWireId)
                return OrgbotName;
            if (wireId == MercWireId)
                return MercName;
            if (wireId >= MercIdBase && wireId < MercIdBase + (uint)MercIds.Length)
                return MercIds[(int)(wireId - MercIdBase)];
            return BuffbotName;
        }
    }
}
