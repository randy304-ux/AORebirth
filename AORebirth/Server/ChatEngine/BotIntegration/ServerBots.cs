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
            "adventurer_crypt", "keeper_smugglers", "nt_sanctum", "shade_camelot",
            "ma_camelot", "trader_elysium", "engineer_scheol", "mp_adonis", "agent_penumbra"
        };

        /// <summary>
        /// Unique Randy nicknames (male then female) parallel to <see cref="MercIds"/>.
        /// Tells resolve on nick, nick+last, full First "Nick" Last, and mode-tagged variants.
        /// </summary>
        static readonly (string MaleNick, string FemaleNick, string MaleFull, string FemaleFull)[] MercNames =
        {
            ("Anvil", "Grit", "Brock \"Anvil\" Malone", "Roxy \"Grit\" Stone"),
            ("Scalpel", "Suture", "Julian \"Scalpel\" Vance", "Clara \"Suture\" Thorne"),
            ("Form", "Stamp", "Alistair \"Form\" Sterling", "Evelyn \"Stamp\" Vance"),
            ("Cipher", "Grid", "Jax \"Cipher\" Malone", "Nikki \"Grid\" Cross"),
            ("Trigger", "Striker", "Vance \"Trigger\" Kincaid", "Jane \"Striker\" Sterling"),
            ("Wild", "Roam", "Logan \"Wild\" Kincaid", "Skye \"Roam\" Finch"),
            ("Shield", "Virtue", "Gideon \"Shield\" Sterling", "Valera \"Virtue\" Thorne"),
            ("Flux", "Pulse", "Victor \"Flux\" Thorne", "Lyra \"Pulse\" Sterling"),
            ("Blade", "Stab", "Kael \"Blade\" Malone", "Vesper \"Stab\" Cross"),
            ("Fist", "Kick", "Kaelen \"Fist\" Wu", "Rin \"Kick\" Chen"),
            ("Broker", "Margin", "Sterling \"Broker\" Hayes", "Vivienne \"Margin\" Vance"),
            ("Wrench", "Sprocket", "Silas \"Wrench\" Finch", "Tessa \"Sprocket\" Hayes"),
            ("Ether", "Wisp", "Xavier \"Ether\" Vance", "Seraphina \"Wisp\" Cross"),
            ("Ghost", "Shadow", "Silas \"Ghost\" Thorne", "Maya \"Shadow\" Cross"),
        };

        static readonly string[] ModeTags =
        {
            "AOE", "ST", "Wrangle", "Umbral", "Composite", "Singles"
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
                if (string.Equals(displayName, MercIds[i], StringComparison.OrdinalIgnoreCase))
                {
                    wireId = MercIdBase + (uint)i;
                    return true;
                }
            }

            string core = StripModeTag(displayName);
            for (int i = 0; i < MercNames.Length && i < MercIds.Length; i++)
            {
                if (MatchesMercName(core, MercNames[i]))
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

        /// <summary>True when <paramref name="bot"/> is the merc menu or any Randy merc name/id.</summary>
        public static bool IsMercTellName(string bot)
        {
            if (string.IsNullOrWhiteSpace(bot))
                return false;
            if (bot.Equals(MercName, StringComparison.OrdinalIgnoreCase)
                || bot.Equals("Mercbot", StringComparison.OrdinalIgnoreCase)
                || bot.Equals("MercMenu", StringComparison.OrdinalIgnoreCase))
                return true;
            return TryResolve(bot, out uint wire) && wire >= MercIdBase && wire < MercIdBase + (uint)MercIds.Length;
        }

        static bool MatchesMercName(
            string core,
            (string MaleNick, string FemaleNick, string MaleFull, string FemaleFull) names)
        {
            if (EqualsAny(core, names.MaleNick, names.FemaleNick, names.MaleFull, names.FemaleFull))
                return true;

            // Nick + last forms used when the full name + mode tag exceeds 32 chars.
            if (NickLastMatch(core, names.MaleFull, names.MaleNick)
                || NickLastMatch(core, names.FemaleFull, names.FemaleNick))
                return true;

            return false;
        }

        static bool NickLastMatch(string core, string full, string nick)
        {
            int q2 = full.LastIndexOf('"');
            if (q2 < 0 || q2 + 1 >= full.Length)
                return false;
            string last = full.Substring(q2 + 1).Trim();
            if (string.IsNullOrEmpty(last))
                return false;
            return string.Equals(core, nick + " " + last, StringComparison.OrdinalIgnoreCase);
        }

        static bool EqualsAny(string core, params string[] options)
        {
            foreach (string option in options)
            {
                if (!string.IsNullOrEmpty(option)
                    && string.Equals(core, option, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static string StripModeTag(string name)
        {
            string trimmed = name.Trim();
            int open = trimmed.LastIndexOf(" (", StringComparison.Ordinal);
            if (open > 0 && trimmed.EndsWith(")", StringComparison.Ordinal))
            {
                string tag = trimmed.Substring(open + 2, trimmed.Length - open - 3);
                foreach (string known in ModeTags)
                {
                    if (string.Equals(tag, known, StringComparison.OrdinalIgnoreCase))
                        return trimmed.Substring(0, open).Trim();
                }
            }

            return trimmed;
        }
    }
}
