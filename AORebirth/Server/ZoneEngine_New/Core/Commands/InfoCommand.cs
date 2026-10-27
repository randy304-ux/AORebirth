namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Chat;
    using ZoneEngine_New.Core.MessageHandlers;
    using ZoneEngine_New.Core.Pets;
    using ZoneEngine_New.Core.Teams;

    /// <summary>
    /// <c>.info</c> (everyone): the command appendix as <c>text://</c> info windows, the same popup as an item description.
    /// Topic pages are embedded in the appendix link so the client Back button returns to the index.
    /// </summary>
    public sealed class InfoCommand : IGmCommand
    {
        readonly Lazy<GmCommandDispatcher> _commands;

        public InfoCommand(Lazy<GmCommandDispatcher> commands)
        {
            ArgumentNullException.ThrowIfNull(commands);
            _commands = commands;
        }

        public string Name => "info";

        public int RequiredGmLevel => 0;

        public string Usage => ".info";

        public string Topic => "player";

        public string Summary => "Opens this command appendix.";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            int gmLevel = context.Player.Stats.GetOrZero(CharacterStat.GmLevel);
            string? topic = context.Args.Length > 0 ? context.Args[0] : null;
            IReadOnlyList<string> lines = InfoGuide.Build(_commands.Value.All, gmLevel, topic);
            if (lines.Count == 0)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Unknown topic. Type .info for the appendix.");
                return;
            }

            GmCommandFeedback.SendLines(context.Session, context.Player, lines);
        }
    }

    /// <summary>
    /// Builds the appendix from <see cref="IGmCommand"/> plus the help lists that sit next to the chat, pet, and stuck handlers.
    /// A topic page contains no raw quotes, so it can sit inside the appendix <c>text://</c> link. Back and Appendix use
    /// <c>&amp;#39;</c> so those quotes survive until the topic page itself is shown.
    /// </summary>
    internal static class InfoGuide
    {
        const int PageMax = 3400;
        const int AppendixMax = 3900;

        readonly record struct Line(string Syntax, string Summary);

        sealed class Page
        {
            public Page(string Label, string Body)
            {
                this.Label = Label;
                this.Body = Body;
            }

            public string Label { get; }

            public string Body { get; }
        }

        sealed class Topic
        {
            public Topic(string Id, string Title, string Intro, List<Line> Lines)
            {
                this.Id = Id;
                this.Title = Title;
                this.Intro = Intro;
                this.Lines = Lines;
            }

            public string Id { get; }

            public string Title { get; }

            public string Intro { get; }

            public List<Line> Lines { get; }

            public List<Page> Pages { get; } = new();
        }

        static readonly (string Id, string Title, string Intro)[] Known =
        [
            ("player", "Player commands", "Typed in chat. A line that starts with a dot is a command."),
            ("chat", "Chat / Social", "Nearby speech, and emotes from the social window."),
            ("team", "Team / Org", "Team slash commands. Organization chat is the client org channel."),
            ("mercs", "Mercs / Pets", "Pet orders, then hiring a merc from a recruiter."),
            ("claims", "Claims / Rewards", "Claim Center and Daily Reward are cards on the Info window."),
            ("dailies", "Daily missions", "Where the daily NPCs stand. Talk to them for the mission."),
            ("travel", "Travel", "Getting unstuck, and any teleport command you are allowed to use."),
            ("gm", "GM commands", "Shown because this character can run them.")
        ];

        public static IReadOnlyList<string> Build(IEnumerable<IGmCommand> commands, int gmLevel, string? topicId)
        {
            ArgumentNullException.ThrowIfNull(commands);
            List<Topic> topics = Collect(commands, gmLevel);
            if (!string.IsNullOrEmpty(topicId))
            {
                Topic? match = topics.FirstOrDefault(topic => string.Equals(topic.Id, topicId, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    return Array.Empty<string>();
                return match.Pages.Select(page => Link(page.Body, page.Label)).ToList();
            }

            return PackAppendix(topics);
        }

        static List<Topic> Collect(IEnumerable<IGmCommand> commands, int gmLevel)
        {
            var byTopic = new Dictionary<string, List<IGmCommand>>(StringComparer.OrdinalIgnoreCase);
            foreach (IGmCommand command in commands)
            {
                if (gmLevel < command.RequiredGmLevel)
                    continue;
                string topic = string.IsNullOrEmpty(command.Topic) ? "player" : command.Topic;
                if (!byTopic.TryGetValue(topic, out List<IGmCommand>? list))
                {
                    list = new List<IGmCommand>();
                    byTopic.Add(topic, list);
                }

                list.Add(command);
            }

            foreach (List<IGmCommand> list in byTopic.Values)
                list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            var topics = new List<Topic>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string id, string title, string intro) in Known)
            {
                seen.Add(id);
                Topic? topic = Make(id, title, intro, byTopic);
                if (topic != null)
                    topics.Add(topic);
            }

            foreach (KeyValuePair<string, List<IGmCommand>> extra in byTopic.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (seen.Contains(extra.Key))
                    continue;
                Topic? topic = Make(extra.Key, extra.Key, string.Empty, byTopic);
                if (topic != null)
                    topics.Add(topic);
            }

            return topics;
        }

        static Topic? Make(string id, string title, string intro, Dictionary<string, List<IGmCommand>> byTopic)
        {
            var lines = new List<Line>();
            if (byTopic.TryGetValue(id, out List<IGmCommand>? commands))
            {
                foreach (IGmCommand command in commands)
                    lines.Add(new Line(command.Usage, command.Summary));
            }

            switch (id)
            {
                case "chat":
                    foreach (VicinityChatRelay.ChatMode mode in VicinityChatRelay.Modes)
                    {
                        string range = mode.Range.ToString("0.##", CultureInfo.InvariantCulture);
                        lines.Add(new Line(mode.Syntax, mode.Summary + " Heard within " + range + "m."));
                    }

                    lines.Add(new Line(SocialActionCmdMessageHandler.HelpSyntax, SocialActionCmdMessageHandler.HelpSummary));
                    break;
                case "team":
                    foreach ((string syntax, string summary) in TeamChatMessageHandler.Commands)
                        lines.Add(new Line(syntax, summary));
                    lines.Add(new Line("Team window", "Kick, make leader, and make raid use the team window."));
                    lines.Add(new Line("Organization chat", "The client org channel. .org and a tell to Orgbot use the org bot."));
                    break;
                case "mercs":
                    foreach ((PetCommandCode _, string syntax, string summary) in PetService.Orders)
                        lines.Add(new Line(syntax, summary));
                    foreach (Mercs.Kind kind in Mercs.Kinds)
                        lines.Add(new Line("Hire " + kind.Title, "Talk to a merc recruiter. This merc then obeys /pet. Hiring again replaces that merc."));
                    lines.Add(new Line("Dismiss mercs", "A merc recruiter can send away every merc you hired."));
                    break;
                case "dailies":
                    lines.Add(new Line("John Smith", "Borealis (800) at 690, 590 — Killing the Competition, Battlestation tip, Tarasque daily."));
                    lines.Add(new Line("Agency Handler", "Rome Blue (540) at 515, 445 and Old Athen (735) at 660, 320 — Rubi-Ka Agency daily."));
                    lines.Add(new Line("Senior Agency Handler", "Rome Blue (540) at 520, 440 and Old Athen (735) at 665, 315 — Elite Agency daily."));
                    lines.Add(new Line("Alien Agency Handler", "Rome Blue (540) at 525, 445 and Old Athen (735) at 670, 320 — Alien Agency daily."));
                    lines.Add(new Line("Agency Receptionist", "Rome Blue (540) at 510, 440 and Old Athen (735) at 655, 315 — Agency desk."));
                    lines.Add(new Line("Scarlett Dalquist", "Jobe Research (4001) at 934, 910 — DOJA chip turn-in."));
                    lines.Add(new Line("Tarasque", "Avalon (505) near Camelot at 2092, 3822 — PvP Tarasque kill target."));
                    break;
                case "travel":
                    lines.Insert(0, new Line(StuckService.HelpSyntax, StuckService.HelpSummary));
                    break;
            }

            if (lines.Count == 0)
                return null;

            var topic = new Topic(id, title, intro, lines);
            topic.Pages.AddRange(Paginate(title, intro, lines));
            return topic;
        }

        static List<Page> Paginate(string title, string intro, List<Line> lines)
        {
            var batches = new List<List<Line>>();
            var batch = new List<Line>();
            foreach (Line line in lines)
            {
                if (batch.Count > 0)
                {
                    var trial = new List<Line>(batch) { line };
                    if (Render(title, intro, trial, 1, 2).Length > PageMax)
                    {
                        batches.Add(batch);
                        batch = new List<Line>();
                    }
                }

                batch.Add(line);
            }

            if (batch.Count > 0)
                batches.Add(batch);

            var pages = new List<Page>(batches.Count);
            for (int i = 0; i < batches.Count; i++)
            {
                string label = batches.Count == 1
                    ? title
                    : string.Format(CultureInfo.InvariantCulture, "{0} ({1}/{2})", title, i + 1, batches.Count);
                pages.Add(new Page(label, Render(title, intro, batches[i], i + 1, batches.Count)));
            }

            return pages;
        }

        static string Render(string title, string intro, IReadOnlyList<Line> lines, int part, int parts)
        {
            var body = new StringBuilder();
            body.Append(DiagAoml.Title(Esc(title)));
            if (parts > 1)
                body.Append(' ').Append(DiagAoml.Muted(part.ToString(CultureInfo.InvariantCulture) + "/" + parts.ToString(CultureInfo.InvariantCulture)));
            if (intro.Length > 0)
                body.Append("<br>").Append(DiagAoml.Muted(Esc(intro)));
            foreach (Line line in lines)
            {
                body.Append("<br>").Append(DiagAoml.Label(Esc(line.Syntax)));
                if (line.Summary.Length > 0)
                    body.Append(' ').Append(DiagAoml.Muted(Esc(line.Summary)));
            }

            body.Append("<br><br><a href='chatcmd:///say .info'>").Append(DiagAoml.Label("Back")).Append("</a>");
            body.Append(" &#160; <a href='chatcmd:///say .info'>").Append(DiagAoml.Label("Appendix")).Append("</a>");
            body.Append("<br>").Append(DiagAoml.Muted("The window Back button steps back through these pages."));
            string html = body.ToString();
            if (html.IndexOf('\'') >= 0 || html.IndexOf('"') >= 0)
                throw new InvalidOperationException("Info page contained a raw quote.");
            return html;
        }

        static List<string> PackAppendix(List<Topic> topics)
        {
            var pages = new List<Page>();
            foreach (Topic topic in topics)
                pages.AddRange(topic.Pages);

            var groups = new List<List<Page>>();
            var current = new List<Page>();
            foreach (Page page in pages)
            {
                if (current.Count > 0)
                {
                    var trial = new List<Page>(current) { page };
                    if (AppendixBody(trial, 1, 2).Length > AppendixMax)
                    {
                        groups.Add(current);
                        current = new List<Page>();
                    }
                }

                current.Add(page);
            }

            if (current.Count > 0)
                groups.Add(current);

            var links = new List<string>(groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                string body = AppendixBody(groups[i], i + 1, groups.Count);
                if (body.IndexOf('"') >= 0)
                    throw new InvalidOperationException("Appendix contained a double quote.");
                string label = groups.Count == 1
                    ? "Appendix"
                    : string.Format(CultureInfo.InvariantCulture, "Appendix ({0}/{1})", i + 1, groups.Count);
                links.Add(Link(body, label));
            }

            return links;
        }

        static string AppendixBody(List<Page> pages, int part, int parts)
        {
            var body = new StringBuilder();
            body.Append(DiagAoml.Title("Appendix"));
            if (parts > 1)
                body.Append(' ').Append(DiagAoml.Muted(part.ToString(CultureInfo.InvariantCulture) + "/" + parts.ToString(CultureInfo.InvariantCulture)));
            body.Append("<br>").Append(DiagAoml.Muted("Command index. Click a topic. The window Back button returns to this page."));
            foreach (Page page in pages)
            {
                body.Append("<br><a href='text://").Append(page.Body).Append("'>").Append(DiagAoml.Name(Esc(page.Label))).Append("</a>");
            }

            return body.ToString();
        }

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
