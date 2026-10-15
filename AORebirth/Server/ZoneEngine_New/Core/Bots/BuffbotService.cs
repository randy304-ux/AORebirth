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
                int page = 1;
                if (args.Length > 1)
                    int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out page);
                OpenMenu(player, page);
                return;
            }

            if (verb.Equals("status", StringComparison.OrdinalIgnoreCase))
            {
                Send(player, Status(player));
                return;
            }

            if (verb.Equals("macro", StringComparison.OrdinalIgnoreCase)
                || verb.Equals("buffmacro", StringComparison.OrdinalIgnoreCase)
                || verb.Equals("rebuff", StringComparison.OrdinalIgnoreCase))
            {
                Macro(player);
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
            int level = Math.Max(1, player.Stats.GetOrZero(CharacterStat.Level));
            List<string> pages = BuildPages(level);
            if (pages.Count == 0)
            {
                GmCommandFeedback.Send(player.Session!, player, "The buff list is empty.");
                return;
            }

            if (onlyPage > 0)
            {
                int index = Math.Clamp(onlyPage, 1, pages.Count) - 1;
                GmCommandFeedback.Send(player.Session!, player, Link(pages[index], PageLabel(index, pages.Count)));
                return;
            }

            var links = new List<string>(pages.Count);
            for (int i = 0; i < pages.Count; i++)
                links.Add(Link(pages[i], PageLabel(i, pages.Count)));
            GmCommandFeedback.SendLines(player.Session!, player, links);
        }

        void Cast(Player player, int nanoId, bool announceRefusal)
        {
            if (!_catalog.Contains(nanoId))
            {
                GmCommandFeedback.Send(player.Session!, player, "That buff is not on the buffbot list.");
                return;
            }

            int level = Math.Max(1, player.Stats.GetOrZero(CharacterStat.Level));
            foreach (BuffOffer offer in _catalog.Castable(level))
            {
                if (offer.NanoId != nanoId)
                    continue;
                NanoRuntime.TryApplyServiceBuff(player, nanoId, announceRefusal, out string detail);
                GmCommandFeedback.Send(player.Session!, player, detail);
                return;
            }

            GmCommandFeedback.Send(player.Session!, player, "Your level is too low for that buff.");
        }

        void Macro(Player player)
        {
            int level = Math.Max(1, player.Stats.GetOrZero(CharacterStat.Level));
            int landed = 0;
            int skipped = 0;
            foreach (BuffOffer offer in _catalog.Castable(level))
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
                    "Buffmacro finished. Landed {0}. Skipped {1}. NCU {2}/{3}.",
                    landed,
                    skipped,
                    player.UsedNcu,
                    player.MaxNcu));
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

        List<string> BuildPages(int level)
        {
            var blocks = new List<string>();
            var head = new StringBuilder();
            head.Append(DiagAoml.Title("Buffbot"));
            head.Append("<br>").Append(DiagAoml.Muted(
                "One bot for every profession. A click casts the nano your level allows. No charge."));
            head.Append("<br><a href='chatcmd:///say .buff macro'>").Append(DiagAoml.Label("Buffmacro")).Append("</a>");
            head.Append(" &#160; <a href='chatcmd:///say .buff rebuff'>").Append(DiagAoml.Label("Rebuff")).Append("</a>");
            head.Append(" &#160; <a href='chatcmd:///say .buff help'>").Append(DiagAoml.Label("Help")).Append("</a>");
            head.Append(" &#160; <a href='chatcmd:///say .buff status'>").Append(DiagAoml.Label("Status")).Append("</a>");
            blocks.Add(head.ToString());

            if (_catalog.Sections.Count == 0)
            {
                blocks.Add(DiagAoml.Muted("No buff names from Buffbot.json matched the item catalog."));
            }

            foreach (BuffSection section in _catalog.Sections)
            {
                var sectionBody = new StringBuilder();
                sectionBody.Append(DiagAoml.Section(Esc(section.Name)));
                foreach (BuffLine line in section.Lines)
                {
                    _catalog.TryOffer(line, level, out BuffOffer offer);
                    sectionBody.Append("<br>");
                    if (offer.Available)
                    {
                        sectionBody.Append("<a href='chatcmd:///say .buff cast ")
                            .Append(offer.NanoId.ToString(CultureInfo.InvariantCulture))
                            .Append("'>")
                            .Append(DiagAoml.Label(Esc(offer.Name)))
                            .Append("</a> ");
                        sectionBody.Append(DiagAoml.Muted(Esc(Describe(offer))));
                    }
                    else
                    {
                        sectionBody.Append(DiagAoml.Muted(Esc(offer.Name + " needs level " + offer.RequiredLevel.ToString(CultureInfo.InvariantCulture))));
                    }
                }

                blocks.Add(sectionBody.ToString());
            }

            var pages = new List<string>();
            var page = new StringBuilder();
            foreach (string block in blocks)
            {
                if (page.Length > 0 && page.Length + block.Length > PageMax)
                {
                    pages.Add(Finish(page.ToString(), pages.Count + 1));
                    page.Clear();
                }

                page.Append(block);
            }

            if (page.Length > 0)
                pages.Add(Finish(page.ToString(), pages.Count + 1));

            var finished = new List<string>(pages.Count);
            for (int i = 0; i < pages.Count; i++)
                finished.Add(pages[i].Replace("%%PAGES%%", pages.Count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
            return finished;
        }

        static string Finish(string body, int page)
        {
            var text = new StringBuilder(body);
            text.Append("<br><br>");
            if (page > 1)
            {
                text.Append("<a href='chatcmd:///say .buff page ")
                    .Append((page - 1).ToString(CultureInfo.InvariantCulture))
                    .Append("'>").Append(DiagAoml.Label("Back")).Append("</a> &#160; ");
            }

            text.Append("<a href='chatcmd:///say .buff page ")
                .Append((page + 1).ToString(CultureInfo.InvariantCulture))
                .Append("'>").Append(DiagAoml.Label("Forward")).Append("</a>");
            text.Append("<br>").Append(DiagAoml.Muted("Page " + page.ToString(CultureInfo.InvariantCulture) + "/%%PAGES%%"));
            return text.ToString();
        }

        static string Describe(BuffOffer offer)
        {
            string bonus = offer.Bonus.Length == 0 ? "buff" : offer.Bonus;
            return string.Format(
                CultureInfo.InvariantCulture,
                "QL {0}, NCU {1}, {2}",
                offer.Quality,
                offer.Ncu,
                bonus);
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
