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
    /// One server buffbot. The menu is the same text:// info window as .info.
    /// A click sends .buff cast, which lands the items.dat nano through the real NCU rules.
    /// There is no credit cost.
    /// </summary>
    public sealed class BuffbotService
    {
        const int PageMax = 3400;

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
                || verb.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                OpenMenu(player);
                return;
            }

            if (verb.Equals("page", StringComparison.OrdinalIgnoreCase))
            {
                // .buff page <Section words> [part]
                int part = 1;
                int last = args.Length;
                if (args.Length > 2 && int.TryParse(args[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    part = parsed;
                    last = args.Length - 1;
                }

                string section = args.Length > 1 ? string.Join(" ", args, 1, last - 1) : string.Empty;
                OpenSection(player, section, part);
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

            if (verb.Equals("cast", StringComparison.OrdinalIgnoreCase) && args.Length > 1
                && int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int nanoId))
            {
                Cast(player, nanoId, announceRefusal: true);
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

        public void OpenMenu(Player player) => OpenMenu(player, 0);

        public void OpenMenu(Player player, int onlyPage)
        {
            ArgumentNullException.ThrowIfNull(player);
            var body = new StringBuilder();
            body.Append(DiagAoml.Title("Buffbot"));
            body.Append("<br>").Append(DiagAoml.Muted(
                "One bot for every profession. Click a buff to cast it. Greyed buffs need more free NCU. No charge."));
            body.Append("<br>").Append(DiagAoml.Muted(string.Format(CultureInfo.InvariantCulture, "NCU {0}/{1}", player.UsedNcu, player.MaxNcu)));
            body.Append("<br><br><a href='chatcmd:///say .buff macro'>").Append(DiagAoml.Label("Buffmacro")).Append("</a> ")
                .Append(DiagAoml.Muted("- creates a macro of current NCU buffs"));
            body.Append("<br><a href='chatcmd:///say .buff rebuff'>").Append(DiagAoml.Label("Rebuff")).Append("</a> ")
                .Append(DiagAoml.Muted("- rebuffs current active NCU buffs"));
            body.Append("<br><a href='chatcmd:///say .buff help'>").Append(DiagAoml.Label("Help")).Append("</a> ")
                .Append(DiagAoml.Muted("- displays this window"));
            body.Append("<br><a href='chatcmd:///say .buff status'>").Append(DiagAoml.Label("Status")).Append("</a> ")
                .Append(DiagAoml.Muted("- shows your running buffbot nanos"));
            body.Append("<br><br>");
            if (_catalog.Sections.Count == 0)
                body.Append(DiagAoml.Muted("No buff names from Buffbot.json matched the item catalog."));
            foreach (BuffSection section in _catalog.Sections)
            {
                body.Append("<br><a href='chatcmd:///say .buff page ").Append(Esc(section.Name)).Append("'>")
                    .Append(DiagAoml.Label(Esc(section.Name))).Append("</a> ")
                    .Append(DiagAoml.Muted("(" + section.Lines.Count.ToString(CultureInfo.InvariantCulture) + ")"));
            }

            GmCommandFeedback.Send(player.Session!, player, Link(body.ToString(), "Buffbot"));
        }

        public void OpenSection(Player player, string sectionName, int part)
        {
            ArgumentNullException.ThrowIfNull(player);
            BuffSection? section = null;
            foreach (BuffSection candidate in _catalog.Sections)
            {
                if (string.Equals(candidate.Name, (sectionName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    section = candidate;
                    break;
                }
            }

            if (section == null)
            {
                OpenMenu(player);
                return;
            }

            List<string> parts = BuildSectionParts(player, section);
            int index = Math.Clamp(part, 1, parts.Count) - 1;
            string label = parts.Count == 1
                ? "Buffbot: " + section.Name
                : string.Format(CultureInfo.InvariantCulture, "Buffbot: {0} {1}/{2}", section.Name, index + 1, parts.Count);
            GmCommandFeedback.Send(player.Session!, player, Link(parts[index], label));
        }

        List<string> BuildSectionParts(Player player, BuffSection section)
        {
            int level = Math.Max(1, player.Stats.GetOrZero(CharacterStat.Level));
            var lines = new List<string>();
            foreach (BuffLine line in section.Lines)
            {
                _catalog.TryOffer(line, level, FreeNcuFor(player, line), out BuffOffer offer);
                var text = new StringBuilder();
                string bonus = offer.Bonus.Length == 0 ? string.Empty : " [" + offer.Bonus + "]";
                string ncu = " [NCU: " + offer.Ncu.ToString(CultureInfo.InvariantCulture) + "]";
                if (offer.Available)
                {
                    text.Append("<a href='chatcmd:///say .buff cast ")
                        .Append(offer.NanoId.ToString(CultureInfo.InvariantCulture))
                        .Append("'>").Append(DiagAoml.Label(Esc(offer.Name))).Append("</a> ")
                        .Append(DiagAoml.Muted(Esc(bonus.TrimStart() + ncu)));
                }
                else
                {
                    text.Append(DiagAoml.Muted(Esc(offer.Name + bonus + ncu + " - not enough free NCU")));
                }

                lines.Add(text.ToString());
            }

            var chunks = new List<List<string>>();
            var current = new List<string>();
            int size = 0;
            foreach (string line in lines)
            {
                if (current.Count > 0 && size + line.Length > PageMax)
                {
                    chunks.Add(current);
                    current = new List<string>();
                    size = 0;
                }

                current.Add(line);
                size += line.Length + 4;
            }

            if (current.Count > 0 || chunks.Count == 0)
                chunks.Add(current);

            var parts = new List<string>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                var body = new StringBuilder();
                body.Append(DiagAoml.Title(Esc(section.Name)));
                if (chunks.Count > 1)
                    body.Append(' ').Append(DiagAoml.Muted(string.Format(CultureInfo.InvariantCulture, "{0}/{1}", i + 1, chunks.Count)));
                body.Append("<br>").Append(DiagAoml.Muted(string.Format(CultureInfo.InvariantCulture, "NCU {0}/{1}", player.UsedNcu, player.MaxNcu)));
                foreach (string line in chunks[i])
                    body.Append("<br>").Append(line);
                body.Append("<br><br>");
                if (i > 0)
                    body.Append(PartLink(section.Name, i, "Back")).Append(" &#160; ");
                if (i + 1 < chunks.Count)
                    body.Append(PartLink(section.Name, i + 2, "Next")).Append(" &#160; ");
                body.Append("<a href='chatcmd:///say .buff menu'>").Append(DiagAoml.Label("Index")).Append("</a>");
                parts.Add(body.ToString());
            }

            return parts;
        }

        static string PartLink(string section, int part, string label)
            => "<a href='chatcmd:///say .buff page " + Esc(section) + " " + part.ToString(CultureInfo.InvariantCulture) + "'>" + DiagAoml.Label(label) + "</a>";

        /// <summary>Free NCU for this line, counting NCU the same line already holds (a recast replaces it).</summary>
        int FreeNcuFor(Player player, BuffLine line)
        {
            int free = Math.Max(0, player.MaxNcu - player.UsedNcu);
            foreach (Buff buff in player.Buffs)
            {
                if (_catalog.TryGetLine(buff.Id, out BuffLine running) && ReferenceEquals(running, line))
                    free += Math.Max(0, buff.NcuCost);
            }

            return free;
        }

        bool TryCurrentOffer(Player player, BuffLine line, out BuffOffer offer)
        {
            int level = Math.Max(1, player.Stats.GetOrZero(CharacterStat.Level));
            return _catalog.TryOffer(line, level, FreeNcuFor(player, line), out offer) && offer.Available;
        }

        void Cast(Player player, int nanoId, bool announceRefusal)
        {
            if (!_catalog.TryGetLine(nanoId, out BuffLine line))
            {
                GmCommandFeedback.Send(player.Session!, player, "That buff is not on the buffbot list.");
                return;
            }

            if (!TryCurrentOffer(player, line, out BuffOffer offer))
            {
                GmCommandFeedback.Send(player.Session!, player, "You do not have enough free NCU for " + line.Name + ".");
                return;
            }

            // Cast the rank the player can take now (a stale macro id still lands the best fitting rank).
            NanoRuntime.TryApplyServiceBuff(player, offer.NanoId, announceRefusal, out string detail);
            GmCommandFeedback.Send(player.Session!, player, detail);
        }

        /// <summary>
        /// Builds a client rebuff macro for the player's current buffbot buffs. Never casts or applies a nano.
        /// </summary>
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
            // AO macro create: /macro Name commands. Never land nanos from this path.
            string href = "chatcmd:///macro Buffbot " + macroBody;
            var body = new StringBuilder();
            body.Append(DiagAoml.Title("Buffmacro"));
            body.Append("<br>").Append(DiagAoml.Muted(string.Format(
                CultureInfo.InvariantCulture,
                "{0} buff(s) from your current buffbot set. This does not cast anything.",
                chosen.Count)));
            body.Append("<br><br><a href='").Append(Esc(href)).Append("'>")
                .Append(DiagAoml.Label("Create Buffbot macro")).Append("</a>");
            body.Append("<br><br>").Append(DiagAoml.Muted("Macro commands:"));
            body.Append("<br>").Append(DiagAoml.Muted(Esc(macroBody)));
            body.Append("<br><br><a href='chatcmd:///say .buff menu'>").Append(DiagAoml.Label("Back")).Append("</a>");
            GmCommandFeedback.Send(player.Session!, player, Link(body.ToString(), "Buffmacro"));
        }

        /// <summary>Re-lands the player's current buffbot buffs. Never casts buffs that are not running.</summary>
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
            var parts = new List<string>(offers.Count * 2);
            for (int i = 0; i < offers.Count; i++)
            {
                if (i > 0)
                    parts.Add("/delay 1500");
                parts.Add("/say .buff cast " + offers[i].NanoId.ToString(CultureInfo.InvariantCulture));
            }

            return string.Join(" | ", parts);
        }

        List<string> Status(Player player)
        {
            var body = new StringBuilder();
            body.Append(DiagAoml.Title("Buff status"));
            body.Append("<br>").Append(DiagAoml.Muted(string.Format(
                CultureInfo.InvariantCulture,
                "NCU {0}/{1}. No charge.",
                player.UsedNcu,
                player.MaxNcu)));
            int shown = 0;
            foreach (Buff buff in player.Buffs)
            {
                if (!_catalog.Contains(buff.Id))
                    continue;
                shown++;
                int left = 0;
                if (buff.ExpiresAtUtc > DateTime.UtcNow)
                    left = (int)(buff.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds;
                body.Append("<br>").Append(DiagAoml.Label(Esc(buff.Name)));
                body.Append(' ').Append(DiagAoml.Muted(string.Format(
                    CultureInfo.InvariantCulture,
                    "QL {0}, NCU {1}, {2}s",
                    buff.Quality,
                    buff.NcuCost,
                    left)));
            }

            if (shown == 0)
                body.Append("<br>").Append(DiagAoml.Muted("No buffbot nanos are running."));
            body.Append("<br><br><a href='chatcmd:///say .buff menu'>").Append(DiagAoml.Label("Back")).Append("</a>");
            return new List<string> { Link(body.ToString(), "Buff status") };
        }

        static string PageLabel(int index, int count)
            => count == 1
                ? "Buffbot"
                : string.Format(CultureInfo.InvariantCulture, "Buffbot ({0}/{1})", index + 1, count);

        static void Send(Player player, IReadOnlyList<string> lines)
            => GmCommandFeedback.SendLines(player.Session!, player, lines);

        static string Link(string body, string label)
            => GetStatsAomlBuilder.BuildLink(body, DiagAoml.Title(label));

        static string Esc(string text)
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
                    case '<':
                        escaped.Append("&lt;");
                        break;
                    case '>':
                        escaped.Append("&gt;");
                        break;
                    case '"':
                    case '\'':
                        escaped.Append("&#39;");
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
