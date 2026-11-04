namespace ZoneEngine_New.Core.Bots
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Nanos;

    /// <summary>
    /// One server buffbot. Menu matches live AsyncBuffs: one tell with a single
    /// <c>text://</c> blob listing every profession (no nested windows, no HTML-escaping of tags).
    /// Clicks are <c>/say .buff cast &lt;nanoId&gt;</c> (zone command; no Chat tell target).
    /// </summary>
    public sealed class BuffbotService
    {
        /// <summary>ChatText / tell soft budget. Live blob was ~27 650; stay under Int16.</summary>
        public const int MessageMax = 32000;

        public const string BotName = "Buffbot";

        const string ColorHeader = "#DEDE42";
        const string ColorBonus = "#F07171";
        const string ColorNcu = "#00BDBD";
        const string ColorTeam = "#FBFF96";
        const string ColorMuted = "#D3BFFF";

        /// <summary>AO profession icon ids used by live AsyncBuffs section headers.</summary>
        static readonly Dictionary<string, int> ProfessionIcons = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Adventurer"] = 6,
            ["Agent"] = 5,
            ["Bureaucrat"] = 8,
            ["Doctor"] = 10,
            ["Enforcer"] = 9,
            ["Engineer"] = 3,
            ["Fixer"] = 4,
            ["MartialArtist"] = 2,
            ["Martial Artist"] = 2,
            ["Metaphysicist"] = 12,
            ["NanoTechnician"] = 11,
            ["Nano Technician"] = 11,
            ["Soldier"] = 1,
            ["Trader"] = 7,
        };

        readonly BuffbotCatalog _catalog;

        public BuffbotService(BuffbotCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            _catalog = catalog;
        }

        public void Execute(Player player, string[] args)
        {
            ArgumentNullException.ThrowIfNull(player);
            args ??= Array.Empty<string>();
            string verb = args.Length == 0 ? "menu" : args[0];
            if (verb.Equals("menu", StringComparison.OrdinalIgnoreCase)
                || verb.Equals("help", StringComparison.OrdinalIgnoreCase)
                || verb.Equals("mybuffs", StringComparison.OrdinalIgnoreCase))
            {
                OpenMenu(player);
                return;
            }

            if (verb.Equals("status", StringComparison.OrdinalIgnoreCase))
            {
                Send(player, Status(player));
                return;
            }

            if (verb.Equals("macro", StringComparison.OrdinalIgnoreCase)
                || verb.Equals("buffmacro", StringComparison.OrdinalIgnoreCase))
            {
                Macro(player);
                return;
            }

            if (verb.Equals("rebuff", StringComparison.OrdinalIgnoreCase))
            {
                Rebuff(player);
                return;
            }

            if (verb.Equals("cast", StringComparison.OrdinalIgnoreCase) && args.Length > 1)
            {
                // Macro line: ".buff cast A B C" or tell "cast A B C". Also tolerate old
                // "| /delay /say .buff cast" tokens.
                var ids = new List<int>();
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i - 1].Equals("/delay", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                        && id > 0
                        && !ids.Contains(id))
                        ids.Add(id);
                }

                foreach (int nanoId in ids)
                    Cast(player, nanoId, announceRefusal: true);
                if (ids.Count > 0)
                    return;
            }

            OpenMenu(player);
        }

        public void HandleTell(Player player, string text)
        {
            string trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                OpenMenu(player);
                return;
            }

            Execute(player, trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        public void OpenMenu(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            GmCommandFeedback.Send(
                player.Session!,
                player,
                "<font color=" + ColorMuted + ">Retrieving all available buffs. Just a moment.</font>");

            int freeNcu = Math.Max(0, player.MaxNcu - player.UsedNcu);
            IReadOnlyList<string> messages = BuildMenuMessages(freeNcu, BotName);
            foreach (string message in messages)
                GmCommandFeedback.Send(player.Session!, player, message);
        }

        /// <summary>
        /// Builds one or more live-style tell payloads (<c>&lt;a href="text://…"&gt;Buff Menu&lt;/a&gt;</c>).
        /// Prefer a single message; if over <see cref="MessageMax"/>, split by whole professions into
        /// additional tells — never nested <c>text://</c> windows.
        /// </summary>
        public IReadOnlyList<string> BuildMenuMessages(int freeNcu, string botName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(botName);
            freeNcu = Math.Max(0, freeNcu);

            var sections = new List<(string Name, string Body)>();
            foreach (BuffSection section in _catalog.Sections)
            {
                var body = new StringBuilder();
                AppendSection(body, section, freeNcu, botName);
                sections.Add((section.Name, body.ToString()));
            }

            if (sections.Count == 0)
            {
                string empty = BuildOuter(BuildHeader(botName) + "\n\nNo buff names from Buffbot.json matched the item catalog.\n");
                return new[] { empty };
            }

            var messages = new List<string>();
            var current = new StringBuilder();
            current.Append(BuildHeader(botName));
            foreach ((string name, string body) in sections)
            {
                if (current.Length > 0
                    && EstimateWrappedLength(current.Length + body.Length) > MessageMax
                    && current.Length > BuildHeader(botName).Length)
                {
                    messages.Add(BuildOuter(current.ToString()));
                    current.Clear();
                    current.Append(BuildHeader(botName));
                    current.Append("\n\n<font color=").Append(ColorMuted).Append(">(continued)</font>");
                }

                current.Append(body);
            }

            if (current.Length > 0)
                messages.Add(BuildOuter(current.ToString()));
            LastBuiltMessageCount = messages.Count;
            LastBuiltMaxMessageLength = 0;
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].Length > LastBuiltMaxMessageLength)
                    LastBuiltMaxMessageLength = messages[i].Length;
            }

            return messages;
        }

        /// <summary>Set by the last <see cref="BuildMenuMessages"/> call (for deploy/size reports).</summary>
        public static int LastBuiltMessageCount { get; private set; }

        /// <summary>Longest outer tell from the last <see cref="BuildMenuMessages"/> call.</summary>
        public static int LastBuiltMaxMessageLength { get; private set; }

        /// <summary>Inner blob only (no outer text:// wrapper). Used by tests.</summary>
        internal string BuildMenuBlobForTest(int freeNcu, string botName)
        {
            IReadOnlyList<string> messages = BuildMenuMessages(freeNcu, botName);
            if (messages.Count == 0)
                return string.Empty;
            // Unwrap first message's text:// body for assertion helpers.
            return UnwrapTextHref(messages[0]);
        }

        static int EstimateWrappedLength(int innerLength)
            // <a href="text://…">Buff Menu</a> plus color wrapper ~40 chars.
            => innerLength + 48;

        /// <summary>Live wrapper: one <c>text://</c> href; inner markup is NOT HTML-escaped.</summary>
        internal static string BuildOuter(string inner)
        {
            // Concatenate — do not string.Format the body (inner may contain '{' from nowhere,
            // and Format would also interpret braces).
            if (!inner.EndsWith("</font>\n", StringComparison.Ordinal)
                && !inner.EndsWith("</font>", StringComparison.Ordinal))
                inner += "</font>";
            return "<font color='" + ColorMuted + "'><a href=\"text://" + inner + "\">Buff Menu</a></font>";
        }

        static string UnwrapTextHref(string message)
        {
            const string marker = "href=\"text://";
            int start = message.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return message;
            start += marker.Length;
            int end = message.LastIndexOf("\">", StringComparison.Ordinal);
            if (end <= start)
                return message;
            return message.Substring(start, end - start);
        }

        string BuildHeader(string botName)
        {
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append('>');
            body.Append("<font color=").Append(ColorHeader).Append('>')
                .Append(EscText(botName)).Append("</font>\n");
            // Zone .buff commands — no ChatEngine /tell Buffbot target required.
            body.Append(BuffCmdLink("Buffmacro", "Buffmacro")).Append('\n');
            body.Append(BuffCmdLink("Rebuff", "Rebuff")).Append('\n');
            body.Append(BuffCmdLink("Status", "Status")).Append('\n');
            return body.ToString();
        }

        void AppendSection(StringBuilder body, BuffSection section, int freeNcu, string botName)
        {
            _ = botName;
            body.Append("\n<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n<img src=tdb://id:GFX_GUI_FRIENDLIST_STATUS_GREEN>");
            if (ProfessionIcons.TryGetValue(section.Name, out int icon))
                body.Append("<img src=tdb://id:GFX_GUI_ICON_PROFESSION_").Append(icon.ToString(CultureInfo.InvariantCulture)).Append('>');
            body.Append("<font color=").Append(ColorHeader).Append('>')
                .Append(EscText(section.Name)).Append("</font>\n");

            foreach (BuffLine line in section.Lines)
            {
                _catalog.TryOffer(line, freeNcu, out BuffOffer offer);
                bool team = IsTeamBuff(offer.Name);
                // Link label uses EscText so apostrophes (Dr Hack 'n Quack) cannot break AOML quoting.
                // Href uses numeric nano id only — never the name.
                if (offer.Available)
                {
                    body.Append("<a href='chatcmd:///say .buff cast ")
                        .Append(offer.NanoId.ToString(CultureInfo.InvariantCulture))
                        .Append("'>")
                        .Append(EscText(offer.Name))
                        .Append("</a>");
                }
                else
                {
                    body.Append("<font color=").Append(ColorMuted).Append('>')
                        .Append(EscText(offer.Name))
                        .Append(" (need ")
                        .Append(offer.Ncu.ToString(CultureInfo.InvariantCulture))
                        .Append(" NCU)</font>");
                }

                if (team)
                    body.Append("<font color=").Append(ColorTeam).Append(">[Team]</font>");
                if (!string.IsNullOrEmpty(offer.Bonus))
                {
                    body.Append("<font color=").Append(ColorBonus).Append(">[")
                        .Append(EscText(offer.Bonus)).Append("]</font>");
                }

                body.Append("<font color=").Append(ColorNcu).Append(">[NCU:")
                    .Append(offer.Ncu.ToString(CultureInfo.InvariantCulture))
                    .Append("]</font>\n");
            }
        }

        static bool IsTeamBuff(string name)
            => name.IndexOf("(Team)", StringComparison.OrdinalIgnoreCase) >= 0
               || name.IndexOf("[Team]", StringComparison.OrdinalIgnoreCase) >= 0;

        static string BuffCmdLink(string command, string label)
            => "<a href='chatcmd:///say .buff " + command + "'>" + EscText(label) + "</a>";

        void Cast(Player player, int nanoId, bool announceRefusal)
        {
            if (!_catalog.TryGetLine(nanoId, out BuffLine _))
            {
                GmCommandFeedback.Send(player.Session!, player, "That buff is not on the buffbot list.");
                return;
            }

            // Apply the clicked nano id as-is. No recipient level/QL gate — NCU / strain / self-only
            // rules live in NanoRuntime.TryApplyServiceBuff.
            NanoRuntime.TryApplyServiceBuff(player, nanoId, announceRefusal, out string detail);
            GmCommandFeedback.Send(player.Session!, player, detail);
        }

        void Macro(Player player)
        {
            List<BuffOffer> chosen = ChosenBuffs(player);
            if (chosen.Count == 0)
            {
                GmCommandFeedback.Send(
                    player.Session!,
                    player,
                    "No buffbot buffs are running. Click buffs from the menu first, then Buffmacro again. Nothing was cast.");
                return;
            }

            string macroBody = BuildMacroBody(chosen);
            string href = "chatcmd:///macro Buffbot " + macroBody;
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorHeader).Append(">Buffmacro</font>\n");
            body.Append(chosen.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" buff(s) from your current buffbot set. This does not cast anything.\n\n");
            body.Append("<a href='").Append(href.Replace("'", "&#39;")).Append("'>Create Buffbot macro</a>\n\n");
            body.Append("Macro commands:\n").Append(EscText(macroBody)).Append('\n');
            GmCommandFeedback.Send(player.Session!, player, BuildOuter(body.ToString()));
        }

        void Rebuff(Player player)
        {
            List<BuffOffer> chosen = ChosenBuffs(player);
            if (chosen.Count == 0)
            {
                GmCommandFeedback.Send(player.Session!, player, "No buffbot buffs are running. Click buffs from the menu first.");
                return;
            }

            int landed = 0;
            int skipped = 0;
            foreach (BuffOffer offer in chosen)
            {
                if (NanoRuntime.TryApplyServiceBuff(player, offer.NanoId, announceRefusal: false, out _))
                    landed++;
                else
                    skipped++;
            }

            GmCommandFeedback.Send(
                player.Session!,
                player,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Rebuff finished. Landed {0}. Skipped {1}. NCU {2}/{3}.",
                    landed,
                    skipped,
                    player.UsedNcu,
                    player.MaxNcu));
        }

        List<BuffOffer> ChosenBuffs(Player player)
        {
            var chosen = new List<BuffOffer>();
            var seenLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Buff buff in player.Buffs)
            {
                if (!_catalog.TryGetLine(buff.Id, out BuffLine line) || !seenLines.Add(line.Name))
                    continue;
                if (TryCurrentOffer(player, line, out BuffOffer offer))
                    chosen.Add(offer);
            }

            return chosen;
        }

        static string BuildMacroBody(IReadOnlyList<BuffOffer> offers)
        {
            var parts = new List<string>(offers.Count);
            for (int i = 0; i < offers.Count; i++)
                parts.Add(offers[i].NanoId.ToString(CultureInfo.InvariantCulture));
            // Same path as menu clicks: zone .buff cast (no Chat tell target needed).
            return "/say .buff cast " + string.Join(" ", parts);
        }

        List<string> Status(Player player)
        {
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorHeader).Append(">Buff status</font>\n");
            body.Append(string.Format(
                CultureInfo.InvariantCulture,
                "NCU {0}/{1}. No charge.\n",
                player.UsedNcu,
                player.MaxNcu));
            int shown = 0;
            foreach (Buff buff in player.Buffs)
            {
                if (!_catalog.Contains(buff.Id))
                    continue;
                shown++;
                int left = 0;
                if (buff.ExpiresAtUtc > DateTime.UtcNow)
                    left = (int)(buff.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds;
                body.Append(EscText(buff.Name));
                body.Append(string.Format(
                    CultureInfo.InvariantCulture,
                    " QL {0}, NCU {1}, {2}s\n",
                    buff.Quality,
                    buff.NcuCost,
                    left));
            }

            if (shown == 0)
                body.Append("No buffbot nanos are running.\n");
            return new List<string> { BuildOuter(body.ToString()) };
        }

        bool TryCurrentOffer(Player player, BuffLine line, out BuffOffer offer)
        {
            int freeNcu = Math.Max(0, player.MaxNcu - player.UsedNcu);
            return _catalog.TryOffer(line, freeNcu, out offer) && offer.Available;
        }

        static void Send(Player player, IReadOnlyList<string> lines)
            => GmCommandFeedback.SendLines(player.Session!, player, lines);

        /// <summary>Escape text that sits between tags. Do not use on markup.</summary>
        internal static string EscText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var escaped = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '&':
                        escaped.Append("&amp;");
                        break;
                    case '\'':
                        escaped.Append("&#39;");
                        break;
                    case '"':
                        escaped.Append("&quot;");
                        break;
                    case '<':
                        escaped.Append("&lt;");
                        break;
                    case '>':
                        escaped.Append("&gt;");
                        break;
                    default:
                        escaped.Append(c);
                        break;
                }
            }

            return escaped.ToString();
        }

    }
}
