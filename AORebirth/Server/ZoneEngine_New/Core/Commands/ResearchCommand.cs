namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;

    /// <summary>
    /// LE personal research fallback while the client's research window protocol is unconfirmed: pick a line, set the
    /// share of XP that goes to it, see progress. GM-only extras grant research XP or a tier outright.
    /// </summary>
    public sealed class ResearchCommand : IGmCommand
    {
        public string Name => "research";

        public int RequiredGmLevel => 0;

        public string Usage => ".research [list | <line name> [percent] | percent <0-100> | stop | xp <amount> (GM) | grant <line name> (GM)]";

        public string Summary => "LE personal research: choose a line and how much XP goes to it.";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Player p = context.Player;
            ResearchCatalog catalog = p.ResearchCatalog;
            string[] a = context.Args;
            void Say(string text) => GmCommandFeedback.Send(context.Session, p, text);
            bool gm = p.Stats.GetOrZero(CharacterStat.GmLevel) > 0;
            int profession = p.Stats.GetOrZero(CharacterStat.Profession);

            if (a.Length == 0)
            {
                int active = p.ActiveResearchPerk;
                if (active == 0 || !catalog.TryGetLine(active, out ResearchCatalog.Line line))
                {
                    Say(string.Format(CultureInfo.InvariantCulture, "No active research. XP share {0}%. Try .research list", p.ResearchPercent));
                    return;
                }
                int tier = Array.IndexOf(line.PerkIds, active) + 1;
                string? blocker = p.ResearchBlocker(active);
                Say(string.Format(CultureInfo.InvariantCulture, "Researching {0} {1}: {2:N0} / {3:N0} research XP, {4}% of XP.{5}",
                    line.Name, tier, p.ResearchXp, catalog.CostOf(active), p.ResearchPercent, blocker == null ? string.Empty : " Waiting: " + blocker));
                return;
            }

            string sub = a[0].ToLowerInvariant();
            if (sub == "list")
            {
                var rows = new List<string>();
                foreach (ResearchCatalog.Line line in catalog.Lines.Where(l => ResearchCatalog.AllowsProfession(l, profession)).OrderBy(l => l.ProfessionMask == 0).ThenBy(l => l.Name))
                {
                    int done = line.PerkIds.Count(id => p.TrainedPerks.Contains(id));
                    rows.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1}/{2}{3}", line.Name, done, line.PerkIds.Length, line.ProfessionMask == 0 ? " (general)" : string.Empty));
                }
                GmCommandFeedback.SendLines(context.Session, p, rows.Count == 0 ? new[] { "No research lines for your profession." } : rows.ToArray());
                return;
            }
            if (sub == "stop")
            {
                p.SetResearch(0, 0);
                Say("Research stopped; all XP goes to levels.");
                return;
            }
            if (sub == "percent" && a.Length > 1 && int.TryParse(a[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pct))
            {
                p.SetResearch(p.ActiveResearchPerk, pct);
                Say(string.Format(CultureInfo.InvariantCulture, "Research gets {0}% of earned XP.", p.ResearchPercent));
                return;
            }
            if (sub == "xp" && gm && a.Length > 1 && long.TryParse(a[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long xp))
            {
                List<int> done = p.AddResearchXp(xp);
                Say(string.Format(CultureInfo.InvariantCulture, "Added {0:N0} research XP; {1} tier(s) completed.", xp, done.Count));
                return;
            }
            if (sub == "grant" && gm && a.Length > 1)
            {
                ResearchCatalog.Line? gl = FindLine(catalog, string.Join(' ', a.Skip(1)), profession, any: true);
                int next = gl == null ? 0 : p.NextResearchTier(gl);
                Say(next != 0 && p.ForceResearch(next) ? "Granted " + gl!.Name + " " + (Array.IndexOf(gl.PerkIds, next) + 1) + "." : "No line or nothing left to grant.");
                return;
            }

            // .research <line name> [percent]
            int? percent = null;
            string[] nameParts = a;
            if (a.Length > 1 && int.TryParse(a[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int tail))
            {
                percent = tail;
                nameParts = a[..^1];
            }
            ResearchCatalog.Line? found = FindLine(catalog, string.Join(' ', nameParts), profession, any: false);
            if (found == null)
            {
                Say("No research line by that name for your profession. Try .research list");
                return;
            }
            int perk = p.NextResearchTier(found);
            if (perk == 0)
            {
                Say(found.Name + " is fully researched.");
                return;
            }
            string? why = p.ResearchBlocker(perk);
            if (percent == null && p.ResearchPercent == 0)
                percent = 50;
            p.SetResearch(perk, percent);
            Say(string.Format(CultureInfo.InvariantCulture, "Researching {0} {1} ({2:N0} research XP), {3}% of XP.{4}",
                found.Name, Array.IndexOf(found.PerkIds, perk) + 1, catalog.CostOf(perk), p.ResearchPercent, why == null ? string.Empty : " Note: " + why));
        }

        static ResearchCatalog.Line? FindLine(ResearchCatalog catalog, string name, int profession, bool any)
        {
            IEnumerable<ResearchCatalog.Line> pool = catalog.Lines.Where(l => any || ResearchCatalog.AllowsProfession(l, profession));
            return pool.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? pool.FirstOrDefault(l => l.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
