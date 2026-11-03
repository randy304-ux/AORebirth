namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Bots;
    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Nanos;

    /// <summary>
    /// Buffbot-style paged nested merc menu (MessageMax 7400 / PageMax 3400).
    /// ChatTextMessage sizes its string with Int16 — keep the outer budget Int16-safe.
    /// </summary>
    public sealed class MercMenuService
    {
        const int MessageMax = 7400;
        const int PageMax = 3400;

        readonly MercUnlockService _unlocks;
        readonly BuffbotCatalog _buffs;

        public MercMenuService(MercUnlockService unlocks, BuffbotCatalog buffs)
        {
            _unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
            _buffs = buffs ?? throw new ArgumentNullException(nameof(buffs));
        }

        public void HandleTell(Player player, string botName, string message)
        {
            ArgumentNullException.ThrowIfNull(player);
            string trimmed = (message ?? string.Empty).Trim();
            MercDefinition? named = _unlocks.Roster.Find(botName);
            if (named != null && _unlocks.IsSummoned(player, named))
            {
                if (trimmed.Length == 0)
                {
                    OpenMercBuffs(player, named);
                    return;
                }

                Execute(player, trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                return;
            }

            if (trimmed.Length == 0)
            {
                IReadOnlyList<MercDefinition> summoned = _unlocks.SummonedFor(player);
                if (summoned.Count == 1)
                {
                    OpenMercBuffs(player, summoned[0]);
                    return;
                }

                OpenMenu(player);
                return;
            }

            Execute(player, trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        public void Execute(Player player, string[] args)
        {
            ArgumentNullException.ThrowIfNull(player);
            args ??= Array.Empty<string>();
            string verb = args.Length == 0 ? "menu" : args[0];

            if (verb.Equals("menu", StringComparison.OrdinalIgnoreCase)
                || verb.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                OpenMenu(player, FilterAll);
                return;
            }

            if (verb.Equals("filter", StringComparison.OrdinalIgnoreCase) && args.Length > 1)
            {
                OpenMenu(player, string.Join(" ", args, 1, args.Length - 1));
                return;
            }

            if (verb.Equals("macros", StringComparison.OrdinalIgnoreCase)
                || verb.Equals("macro", StringComparison.OrdinalIgnoreCase))
            {
                OpenMacros(player);
                return;
            }

            if (verb.Equals("buffs", StringComparison.OrdinalIgnoreCase))
            {
                MercDefinition? target = args.Length > 1
                    ? _unlocks.Roster.Find(string.Join(" ", args, 1, args.Length - 1))
                    : null;
                if (target == null)
                {
                    IReadOnlyList<MercDefinition> summoned = _unlocks.SummonedFor(player);
                    if (summoned.Count == 1)
                        target = summoned[0];
                }

                if (target == null)
                {
                    Say(player, "Summon a dungeon merc first, or use .merc buffs <name>.");
                    return;
                }

                OpenMercBuffs(player, target);
                return;
            }

            if (verb.Equals("cast", StringComparison.OrdinalIgnoreCase) && args.Length > 1
                && int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int nanoId)
                && nanoId > 0)
            {
                NanoRuntime.TryApplyServiceBuff(player, nanoId, announceRefusal: true, out string detail);
                Say(player, detail);
                return;
            }

            if (verb.Equals("preset", StringComparison.OrdinalIgnoreCase) && args.Length > 1)
            {
                RunPreset(player, string.Join(" ", args, 1, args.Length - 1));
                return;
            }

            OpenMenu(player, FilterAll);
        }

        const string FilterAll = "all";

        public void OpenMenu(Player player) => OpenMenu(player, FilterAll);

        public void OpenMenu(Player player, string filter)
        {
            ArgumentNullException.ThrowIfNull(player);
            string filterKey = string.IsNullOrWhiteSpace(filter) ? FilterAll : filter.Trim();
            var body = new StringBuilder();
            body.Append(DiagAoml.Title("Mercs"));
            body.Append("<br>").Append(DiagAoml.Muted("Dungeon unlock mercs. Batch 1: menu + summon. Mission NPCs are batch 2."));
            body.Append("<br>").Append(FilterLinks(filterKey));
            body.Append("<br>").Append(CmdLink(".merc macros", "Macros"));
            body.Append("<br><br>");

            string head = body.ToString();
            var windows = new List<StringBuilder>();
            var current = new StringBuilder(head);
            int listed = 0;
            foreach (MercDefinition def in _unlocks.Roster.All)
            {
                if (!PassesFilter(player, def, filterKey))
                    continue;
                listed++;
                string entry = "<br>" + MercRow(player, def);
                if (current.Length > head.Length && current.Length + entry.Length > MessageMax)
                {
                    windows.Add(current);
                    current = new StringBuilder(DiagAoml.Title("Mercs") + "<br>" + FilterLinks(filterKey) + "<br><br>");
                }

                current.Append(entry);
            }

            if (listed == 0)
                current.Append(DiagAoml.Muted("No mercs match this filter."));

            windows.Add(current);
            for (int i = 0; i < windows.Count; i++)
                SendWindow(player, windows[i].ToString(), PageLabel("Mercs", i, windows.Count));
        }

        string MercRow(Player player, MercDefinition def)
        {
            MercUnlockState state = _unlocks.GetState(player, def);
            bool summoned = _unlocks.IsSummoned(player, def);
            var sb = new StringBuilder();
            sb.Append(DiagAoml.Label(Esc(def.DisplayName)));
            sb.Append(' ').Append(DiagAoml.Muted(
                "L" + def.Level.ToString(CultureInfo.InvariantCulture)
                + " " + def.Kind.Title
                + " · " + def.Role
                + " · " + def.DungeonName));

            if (state == MercUnlockState.Locked)
            {
                sb.Append("<br>").Append(DiagAoml.Muted(
                    "finish " + Esc(def.BossName) + " in " + Esc(def.DungeonName)));
                return sb.ToString();
            }

            sb.Append("<br>");
            if (summoned)
                sb.Append(CmdLink(".merc dismiss " + def.Id, "Dismiss"));
            else if (_unlocks.CanSummonHere(player, def, state, out _))
                sb.Append(CmdLink(".merc summon " + def.Id, "Summon"));
            else
                sb.Append(DiagAoml.Muted("dungeon_only — enter " + Esc(def.DungeonName) + " to summon"));

            if (state == MercUnlockState.DungeonOnly)
                sb.Append(' ').Append(DiagAoml.Muted("[dungeon_only]"));
            return sb.ToString();
        }

        static string FilterLinks(string active)
        {
            var sb = new StringBuilder();
            sb.Append(DiagAoml.Muted("Filter:"));
            foreach (string key in new[]
                     {
                         "all", "unlocked", "twink", "elite",
                         "tank", "healer", "crowd control", "carry", "DPS", "support",
                         "enforcer", "doctor", "bureaucrat", "fixer", "soldier", "adventurer",
                         "keeper", "nanotechnician", "shade", "martialartist", "trader",
                         "engineer", "metaphysicist", "agent",
                         "subway", "totw", "madness", "biomare", "cyborg", "crypt",
                         "smuggler", "sanctum", "camelot", "elysium", "scheol", "adonis", "penumbra"
                     })
            {
                sb.Append(' ');
                if (string.Equals(active, key, StringComparison.OrdinalIgnoreCase))
                    sb.Append(DiagAoml.Label("[" + key + "]"));
                else
                    sb.Append(CmdLink(".merc filter " + key, key));
            }

            return sb.ToString();
        }

        bool PassesFilter(Player player, MercDefinition def, string filter)
        {
            if (string.Equals(filter, FilterAll, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(filter, "unlocked", StringComparison.OrdinalIgnoreCase))
                return _unlocks.GetState(player, def) != MercUnlockState.Locked;
            return def.HasTag(filter);
        }

        public void OpenMacros(Player player)
        {
            var body = new StringBuilder();
            body.Append(DiagAoml.Title("Merc macros"));
            body.Append("<br>").Append(DiagAoml.Muted("Copyable commands. Preset handlers that the merc AI cannot do yet are marked TODO."));
            body.Append("<br><br>").Append(DiagAoml.Muted("General"));
            body.Append("<br>").Append(DiagAoml.Muted(Esc(".merc menu")));
            body.Append("<br>").Append(DiagAoml.Muted(Esc(".merc list")));
            body.Append("<br>").Append(DiagAoml.Muted(Esc(".merc summon <name>")));
            body.Append("<br>").Append(DiagAoml.Muted(Esc(".merc dismiss")));
            body.Append("<br>").Append(DiagAoml.Muted(Esc(".merc buffs")));
            body.Append("<br><br>").Append(DiagAoml.Muted("Presets"));
            body.Append("<br>").Append(CmdLink(".merc preset keeper aura", "Keeper aura picker"));
            body.Append(' ').Append(DiagAoml.Muted(Esc(".merc preset keeper aura <name>")));
            body.Append("<br>").Append(CmdLink(".merc preset trader wrangle", "Trader wrangle (largest ST)"));
            body.Append("<br>").Append(CmdLink(".merc preset trader umbral", "Trader Umbral wrangle"));
            body.Append("<br>").Append(CmdLink(".merc preset mp composite", "MP composite nano buffs"));
            body.Append("<br>").Append(CmdLink(".merc preset mp singles", "MP single nano buffs"));
            body.Append("<br>").Append(CmdLink(".merc preset nt aoe", "NT mode AOE"));
            body.Append("<br>").Append(CmdLink(".merc preset nt st", "NT mode ST"));
            body.Append("<br><br>").Append(CmdLink(".merc menu", "Back"));
            SendWindow(player, body.ToString(), "Merc macros");
        }

        public void OpenMercBuffs(Player player, MercDefinition def)
        {
            BuffSection? section = FindProfessionSection(def.Kind.Title);
            var body = new StringBuilder();
            body.Append(DiagAoml.Title(Esc(def.DisplayName) + " buffs"));
            body.Append("<br>").Append(DiagAoml.Muted(
                def.Kind.Title + " L" + def.Level.ToString(CultureInfo.InvariantCulture)
                + " · NCU " + player.UsedNcu.ToString(CultureInfo.InvariantCulture)
                + "/" + player.MaxNcu.ToString(CultureInfo.InvariantCulture)));
            if (!_unlocks.IsSummoned(player, def))
            {
                body.Append("<br>").Append(DiagAoml.Muted("This merc is not summoned — showing the profession buff list anyway."));
            }

            if (section == null)
            {
                body.Append("<br><br>").Append(DiagAoml.Muted(
                    "TODO: no Buffbot.json section for " + def.Kind.Title + " (Keeper/Adventurer/Shade may be missing)."));
                body.Append("<br><br>").Append(CmdLink(".merc menu", "Back"));
                SendWindow(player, body.ToString(), def.DisplayName + " buffs");
                return;
            }

            List<string> parts = BuildBuffParts(player, def, section);
            for (int i = 0; i < parts.Count; i++)
            {
                string label = parts.Count == 1
                    ? def.DisplayName + " buffs"
                    : string.Format(CultureInfo.InvariantCulture, "{0} buffs {1}/{2}", def.DisplayName, i + 1, parts.Count);
                SendWindow(player, parts[i], label);
            }
        }

        List<string> BuildBuffParts(Player player, MercDefinition def, BuffSection section)
        {
            var lines = new List<string>();
            foreach (BuffLine line in section.Lines)
            {
                if (!_buffs.TryOffer(line, def.Level, out BuffOffer offer))
                    continue;
                var text = new StringBuilder();
                string bonus = offer.Bonus.Length == 0 ? string.Empty : " [" + offer.Bonus + "]";
                string ncu = " [NCU: " + offer.Ncu.ToString(CultureInfo.InvariantCulture) + "]";
                if (offer.Available)
                {
                    text.Append("<a href='chatcmd:///say .merc cast ")
                        .Append(offer.NanoId.ToString(CultureInfo.InvariantCulture))
                        .Append("'>").Append(Esc(offer.Name)).Append("</a> ")
                        .Append(Esc(bonus.TrimStart() + ncu));
                }
                else
                {
                    text.Append(DiagAoml.Muted(Esc(offer.Name + bonus + ncu
                        + " - merc needs level " + offer.RequiredLevel.ToString(CultureInfo.InvariantCulture))));
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
                body.Append(DiagAoml.Title(Esc(def.DisplayName) + " buffs"));
                if (chunks.Count > 1)
                    body.Append(' ').Append(DiagAoml.Muted(string.Format(CultureInfo.InvariantCulture, "{0}/{1}", i + 1, chunks.Count)));
                body.Append("<br>").Append(DiagAoml.Muted(
                    "Limited to this merc's profession and level "
                    + def.Level.ToString(CultureInfo.InvariantCulture) + "."));
                if (lines.Count == 0)
                    body.Append("<br>").Append(DiagAoml.Muted("No matching buffs in Buffbot.json for this level."));
                foreach (string line in chunks[i])
                    body.Append("<br>").Append(line);
                body.Append("<br><br>").Append(CmdLink(".merc menu", "Back"));
                parts.Add(body.ToString());
            }

            return parts;
        }

        BuffSection? FindProfessionSection(string title)
        {
            foreach (BuffSection section in _buffs.Sections)
            {
                if (string.Equals(section.Name, title, StringComparison.OrdinalIgnoreCase))
                    return section;
                if (string.Equals(section.Name, "Nano-Technician", StringComparison.OrdinalIgnoreCase)
                    && title.Contains("Nano", StringComparison.OrdinalIgnoreCase))
                    return section;
                if (string.Equals(section.Name, "Metaphysicist", StringComparison.OrdinalIgnoreCase)
                    && title.Contains("Meta", StringComparison.OrdinalIgnoreCase))
                    return section;
            }

            return null;
        }

        void RunPreset(Player player, string preset)
        {
            string key = (preset ?? string.Empty).Trim().ToLowerInvariant();
            if (key.StartsWith("keeper", StringComparison.Ordinal))
            {
                // Keeper auras are player .aura today; merc-driven aura AI is not wired.
                Say(player, "TODO: Keeper merc aura preset — tell the summoned Keeper which aura to run once merc AI supports aura switching. Player .aura still works on you/pets.");
                return;
            }

            if (key.StartsWith("trader wrangle", StringComparison.Ordinal) || key == "trader wrangle")
            {
                Say(player, "TODO: Trader largest single-target wrangle — need verified wrangle nano id at merc level, then cast via merc nano AI.");
                return;
            }

            if (key.StartsWith("trader umbral", StringComparison.Ordinal) || key == "trader umbral")
            {
                Say(player, "TODO: Trader Umbral wrangle — need verified Umbral wrangle nano id before wiring.");
                return;
            }

            if (key.StartsWith("mp composite", StringComparison.Ordinal) || key == "mp composite")
            {
                Say(player, "TODO: MP composite nano buffs — need composite nano ids and merc cast-on-owner support.");
                return;
            }

            if (key.StartsWith("mp singles", StringComparison.Ordinal) || key == "mp singles")
            {
                Say(player, "TODO: MP single nano buffs — need the single-line nano ids and merc cast-on-owner support.");
                return;
            }

            if (key == "nt aoe" || key.StartsWith("nt aoe", StringComparison.Ordinal))
            {
                MercDefinition? aoe = _unlocks.Roster.Find("nt_sanctum_aoe");
                if (aoe == null)
                {
                    Say(player, "NT AOE roster entry missing.");
                    return;
                }

                string? err = _unlocks.Summon(player, aoe);
                Say(player, err ?? ("Switched to " + aoe.DisplayName + ". TODO: AOE-only nuke filter on UploadedNanoIds."));
                return;
            }

            if (key == "nt st" || key.StartsWith("nt st", StringComparison.Ordinal))
            {
                MercDefinition? st = _unlocks.Roster.Find("nt_sanctum_st");
                if (st == null)
                {
                    Say(player, "NT ST roster entry missing.");
                    return;
                }

                string? err = _unlocks.Summon(player, st);
                Say(player, err ?? ("Switched to " + st.DisplayName + ". TODO: ST-only nuke filter on UploadedNanoIds."));
                return;
            }

            Say(player, "Unknown preset. See .merc macros.");
        }

        static string CmdLink(string command, string label)
            => "<a href='chatcmd:///say " + command + "'>" + DiagAoml.Label(Esc(label)) + "</a>";

        static string PageLabel(string title, int index, int count)
            => count <= 1
                ? title
                : string.Format(CultureInfo.InvariantCulture, "{0} ({1}/{2})", title, index + 1, count);

        static void SendWindow(Player player, string body, string label)
        {
            if (player.Session == null)
                return;
            GmCommandFeedback.Send(player.Session, player, GetStatsAomlBuilder.BuildLink(body, DiagAoml.Title(label)));
        }

        static void Say(Player player, string text)
        {
            if (player.Session == null)
                return;
            GmCommandFeedback.Send(player.Session, player, text);
        }

        static string Esc(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal)
                .Replace("\"", string.Empty, StringComparison.Ordinal)
                .Replace("'", string.Empty, StringComparison.Ordinal);
        }
    }
}
