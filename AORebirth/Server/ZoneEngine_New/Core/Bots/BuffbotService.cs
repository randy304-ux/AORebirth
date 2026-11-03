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

            if (verb.Equals("cast", StringComparison.OrdinalIgnoreCase) && args.Length > 1)
            {
                // An old macro arrives as ONE line: ".buff cast A | /delay 1500 | /say .buff cast B ...".
                // Only args[1] used to be read, so just the first id was cast. Cast every nano id on the line,
                // skipping "|", "/delay <ms>" and "/say .buff cast" tokens.
                var ids = new List<int>();
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i - 1].Equals("/delay", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && id > 0 && !ids.Contains(id))
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

        public void OpenMenu(Player player) => OpenMenu(player, 0);

        /// <summary>The action links shared by the main window and the lightweight Back window.</summary>
        static string ActionLinks()
        {
            var body = new StringBuilder();
            body.Append("<br><a href='chatcmd:///say .buff macro'>").Append(DiagAoml.Label("Buffmacro")).Append("</a> ")
                .Append(DiagAoml.Muted("- creates a macro of current NCU buffs"));
            body.Append("<br><a href='chatcmd:///say .buff rebuff'>").Append(DiagAoml.Label("Rebuff")).Append("</a> ")
                .Append(DiagAoml.Muted("- rebuffs current active NCU buffs"));
            body.Append("<br><a href='chatcmd:///say .buff status'>").Append(DiagAoml.Label("Status")).Append("</a> ")
                .Append(DiagAoml.Muted("- shows your running buffbot nanos"));
            return body.ToString();
        }

        /// <summary>
        /// Lightweight main for Back links: actions only, no nested profession pages, so nesting stays
        /// main -> profession -> main and never grows. The full list is the Buffbot link already in chat.
        /// </summary>
        static string LiteMain(Player player)
        {
            var body = new StringBuilder();
            body.Append(DiagAoml.Title("Buffbot"));
            body.Append("<br>").Append(DiagAoml.Muted(string.Format(CultureInfo.InvariantCulture, "NCU {0}/{1}", player.UsedNcu, player.MaxNcu)));
            body.Append("<br>").Append(ActionLinks());
            body.Append("<br><br>").Append(DiagAoml.Muted("To pick another profession, click the Buffbot link in chat again."));
            return body.ToString();
        }

        static string BackLink(Player player) => NestedLink(LiteMain(player), DiagAoml.Label("Back to Buffbot"));

        public void OpenMenu(Player player, int onlyPage)
        {
            ArgumentNullException.ThrowIfNull(player);
            var body = new StringBuilder();
            body.Append(DiagAoml.Title("Buffbot"));
            body.Append("<br>").Append(DiagAoml.Muted(
                "Click a profession to open its buff list, then click a buff to cast it. No charge."));
            body.Append("<br>").Append(DiagAoml.Muted(string.Format(CultureInfo.InvariantCulture, "NCU {0}/{1}", player.UsedNcu, player.MaxNcu)));
            body.Append("<br>").Append(ActionLinks());
            body.Append("<br><br>");
            if (_catalog.Sections.Count == 0)
                body.Append(DiagAoml.Muted("No buff names from Buffbot.json matched the item catalog."));
            // Profession links are nested text:// windows (client opens them without posting to chat).
            // ChatTextMessage sizes its string with Int16, so one blob must stay under ~32 KB; keep the
            // outer budget at 7400 (same as 4b069b71) and PageMax-split large professions so nested
            // pages stay Int16-safe after HTML escaping. Commit 210464b7 raised this to 60000 and
            // packed every full profession into one message — the Int16 size wraps and the window never opens.
            string head = body.ToString();
            var entries = new List<string>();
            foreach (BuffSection section in _catalog.Sections)
            {
                List<string> parts = BuildSectionParts(player, section, nested: true);
                for (int i = 0; i < parts.Count; i++)
                {
                    string name = parts.Count == 1
                        ? section.Name
                        : string.Format(CultureInfo.InvariantCulture, "{0} {1}/{2}", section.Name, i + 1, parts.Count);
                    entries.Add("<br>" + NestedLink(parts[i], DiagAoml.Label(Esc(name))));
                }
            }

            var windows = new List<StringBuilder>();
            var current = new StringBuilder(head);
            foreach (string entry in entries)
            {
                if (current.Length > head.Length && current.Length + entry.Length > MessageMax)
                {
                    windows.Add(current);
                    current = new StringBuilder(DiagAoml.Title("Buffbot") + "<br>");
                }

                current.Append(entry);
            }

            windows.Add(current);
            for (int i = 0; i < windows.Count; i++)
                GmCommandFeedback.Send(player.Session!, player, Link(windows[i].ToString(), PageLabel(i, windows.Count)));
        }

        /// <summary>
        /// Budget for one index ChatText blob including nested profession pages. Must stay well under
        /// the Int16 string/packet size used by ChatTextMessage (~32767); 7400 matches the last
        /// known-good nested menu (4b069b71).
        /// </summary>
        const int MessageMax = 7400;

        /// <summary>
        /// A text:// link whose body is escaped one level so it can sit inside the outer text:// window
        /// (the client unescapes once per window, like Budabot nested blobs).
        /// </summary>
        internal static string NestedLink(string body, string label)
            => "<a href='text://" + body.Replace("&", "&amp;").Replace("'", "&#39;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;") + "'>" + label + "</a>";

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

            List<string> parts = BuildSectionParts(player, section, nested: false);
            int index = Math.Clamp(part, 1, parts.Count) - 1;
            string label = parts.Count == 1
                ? "Buffbot: " + section.Name
                : string.Format(CultureInfo.InvariantCulture, "Buffbot: {0} {1}/{2}", section.Name, index + 1, parts.Count);
            GmCommandFeedback.Send(player.Session!, player, Link(parts[index], label));
        }

        List<string> BuildSectionParts(Player player, BuffSection section, bool nested)
        {
            int level = Math.Max(1, player.Stats.GetOrZero(CharacterStat.Level));
            var lines = new List<string>();
            foreach (BuffLine line in section.Lines)
            {
                // No NCU gate (Randy): only the real level requirement greys a line.
                _catalog.TryOffer(line, level, out BuffOffer offer);
                var text = new StringBuilder();
                string bonus = offer.Bonus.Length == 0 ? string.Empty : " [" + offer.Bonus + "]";
                string ncu = " [NCU: " + offer.Ncu.ToString(CultureInfo.InvariantCulture) + "]";
                if (offer.Available)
                {
                    text.Append("<a href='chatcmd:///say .buff cast ")
                        .Append(offer.NanoId.ToString(CultureInfo.InvariantCulture))
                        .Append("'>").Append(Esc(offer.Name)).Append("</a> ")
                        .Append(Esc(bonus.TrimStart() + ncu)); // plain text: a font tag per line cost ~8 KB once nested
                }
                else
                {
                    text.Append(DiagAoml.Muted(Esc(offer.Name + bonus + ncu + " - requires level " + offer.RequiredLevel.ToString(CultureInfo.InvariantCulture))));
                }

                lines.Add(text.ToString());
            }

            var chunks = new List<List<string>>();
            var current = new List<string>();
            int size = 0;
            foreach (string line in lines)
            {
                // Nested pages are HTML-escaped again inside NestedLink; keep PageMax for both so a
                // single profession cannot blow the ChatText Int16 size by itself.
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
                if (nested)
                {
                    body.Append("<br><br>").Append(BackLink(player));
                    parts.Add(body.ToString());
                    continue;
                }

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
            return _catalog.TryOffer(line, level, out offer) && offer.Available;
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
                GmCommandFeedback.Send(player.Session!, player, "You are not high enough level for " + line.Name + " (needs level " + offer.RequiredLevel.ToString(CultureInfo.InvariantCulture) + ").");
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
            body.Append("<br><br>").Append(BackLink(player));
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
                parts.Add(offers[i].NanoId.ToString(CultureInfo.InvariantCulture));
            }

            // AO macros do not chain "|" commands; the client sends the whole text as one /say.
            // One ".buff cast id id id" line casts every buff server-side.
            return "/say .buff cast " + string.Join(" ", parts);
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
            body.Append("<br><br>").Append(BackLink(player));
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
