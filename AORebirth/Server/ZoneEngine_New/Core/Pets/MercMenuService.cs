namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

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

        public MercMenuService(MercUnlockService unlocks)
        {
            _unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
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
            body.Append("<br>").Append(DiagAoml.Muted("Dungeon unlock mercs. Buff lists come from items.dat (team/other castable)."));
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
            string shown = _unlocks.DisplayNameFor(player, def);
            var sb = new StringBuilder();
            sb.Append(DiagAoml.Label(Esc(shown)));
            sb.Append(' ').Append(DiagAoml.Muted(
                "L" + def.Level.ToString(CultureInfo.InvariantCulture)
                + " " + def.Kind.Title
                + " · " + def.Role
                + " · " + def.DungeonName));

            if (string.Equals(def.Kind.Key, "nanotechnician", StringComparison.OrdinalIgnoreCase))
            {
                string mode = _unlocks.GetNtMode(player).ToUpperInvariant();
                sb.Append(' ').Append(DiagAoml.Muted("[NT " + mode + "]"));
                sb.Append(' ').Append(CmdLink(".merc preset nt aoe", "AOE"));
                sb.Append(' ').Append(CmdLink(".merc preset nt st", "ST"));
            }

            if (string.Equals(def.Kind.Key, "trader", StringComparison.OrdinalIgnoreCase))
            {
                string mode = _unlocks.GetTraderMode(player).ToUpperInvariant();
                sb.Append(' ').Append(DiagAoml.Muted("[Trader " + mode + "]"));
                sb.Append(' ').Append(CmdLink(".merc preset trader wrangle", "Wrangle"));
                sb.Append(' ').Append(CmdLink(".merc preset trader umbral", "Umbral"));
            }

            if (string.Equals(def.Kind.Key, "metaphysicist", StringComparison.OrdinalIgnoreCase))
            {
                string mode = _unlocks.GetMpMode(player).ToUpperInvariant();
                sb.Append(' ').Append(DiagAoml.Muted("[MP " + mode + "]"));
                sb.Append(' ').Append(CmdLink(".merc preset mp composite", "Composite"));
                sb.Append(' ').Append(CmdLink(".merc preset mp singles", "Singles"));
            }

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
            string traderMode = _unlocks.GetTraderMode(player).ToUpperInvariant();
            string mpMode = _unlocks.GetMpMode(player).ToUpperInvariant();
            string ntMode = _unlocks.GetNtMode(player).ToUpperInvariant();
            body.Append("<br>").Append(DiagAoml.Muted(
                "Modes: NT=" + ntMode + " Trader=" + traderMode + " MP=" + mpMode
                + ". Presets pick highest items.dat ranks at merc level."));
            body.Append("<br><br>").Append(DiagAoml.Muted("General"));
            body.Append("<br>").Append(DiagAoml.Muted(Esc(".merc menu")));
            body.Append("<br>").Append(DiagAoml.Muted(Esc(".merc list")));
            body.Append("<br>").Append(DiagAoml.Muted(Esc(".merc summon <name>")));
            body.Append("<br>").Append(DiagAoml.Muted(Esc(".merc dismiss")));
            body.Append("<br>").Append(DiagAoml.Muted(Esc(".merc buffs")));
            body.Append("<br><br>").Append(DiagAoml.Muted("Presets"));
            body.Append("<br>").Append(CmdLink(".merc preset keeper aura", "Keeper aura list"));
            foreach (string type in _unlocks.Nanos.KeeperAuraTypes())
                body.Append("<br>").Append(CmdLink(".merc preset keeper aura " + type, "Keeper " + type));
            body.Append("<br>").Append(CmdLink(".merc preset trader wrangle", "Trader wrangle [" + traderMode + "]"));
            body.Append("<br>").Append(CmdLink(".merc preset trader umbral", "Trader Umbral"));
            body.Append("<br>").Append(CmdLink(".merc preset mp composite", "MP composite [" + mpMode + "]"));
            body.Append("<br>").Append(CmdLink(".merc preset mp singles", "MP singles"));
            body.Append("<br>").Append(CmdLink(".merc preset nt aoe", "NT mode AOE"));
            body.Append("<br>").Append(CmdLink(".merc preset nt st", "NT mode ST"));
            body.Append("<br><br>").Append(CmdLink(".merc menu", "Back"));
            SendWindow(player, body.ToString(), "Merc macros");
        }

        public void OpenMercBuffs(Player player, MercDefinition def)
        {
            NpcCharacter? npc = _unlocks.FindSummonedNpc(player, def);
            IReadOnlyList<MercProfessionNanos.MercBuffOffer> offers = _unlocks.Nanos.BuffsFor(def.Kind, def.Level, npc);
            string shown = _unlocks.DisplayNameFor(player, def);

            var lines = new List<string>();
            if (string.Equals(def.Kind.Key, "nanotechnician", StringComparison.OrdinalIgnoreCase))
            {
                string mode = _unlocks.GetNtMode(player).ToUpperInvariant();
                lines.Add(DiagAoml.Muted("NT nuke mode: " + mode + " — ")
                    + CmdLink(".merc preset nt aoe", "AOE") + " "
                    + CmdLink(".merc preset nt st", "ST"));
            }

            foreach (MercProfessionNanos.MercBuffOffer offer in offers)
            {
                var text = new StringBuilder();
                string ncu = " [NCU: " + offer.Ncu.ToString(CultureInfo.InvariantCulture) + "]";
                if (offer.Available)
                {
                    text.Append("<a href='chatcmd:///say .merc cast ")
                        .Append(offer.NanoId.ToString(CultureInfo.InvariantCulture))
                        .Append("'>").Append(Esc(offer.Name)).Append("</a> ")
                        .Append(Esc(ncu));
                }
                else
                {
                    text.Append(DiagAoml.Muted(Esc(offer.Name + ncu
                        + " - merc needs level " + offer.RequiredLevel.ToString(CultureInfo.InvariantCulture))));
                }

                lines.Add(text.ToString());
            }

            List<string> parts = PackPages(shown + " buffs",
                def.Kind.Title + " L" + def.Level.ToString(CultureInfo.InvariantCulture)
                + " · team/other buffs from items.dat · NCU "
                + player.UsedNcu.ToString(CultureInfo.InvariantCulture) + "/"
                + player.MaxNcu.ToString(CultureInfo.InvariantCulture),
                lines);
            for (int i = 0; i < parts.Count; i++)
            {
                string label = parts.Count == 1
                    ? shown + " buffs"
                    : string.Format(CultureInfo.InvariantCulture, "{0} buffs {1}/{2}", shown, i + 1, parts.Count);
                SendWindow(player, parts[i], label);
            }
        }

        List<string> PackPages(string title, string subtitle, List<string> lines)
        {
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
                body.Append(DiagAoml.Title(Esc(title)));
                if (chunks.Count > 1)
                    body.Append(' ').Append(DiagAoml.Muted(string.Format(CultureInfo.InvariantCulture, "{0}/{1}", i + 1, chunks.Count)));
                body.Append("<br>").Append(DiagAoml.Muted(Esc(subtitle)));
                if (lines.Count == 0)
                    body.Append("<br>").Append(DiagAoml.Muted("No team/other castable buffs found in items.dat for this profession/level."));
                foreach (string line in chunks[i])
                    body.Append("<br>").Append(line);
                body.Append("<br><br>").Append(CmdLink(".merc menu", "Back"));
                parts.Add(body.ToString());
            }

            return parts;
        }

        void RunPreset(Player player, string preset)
        {
            string key = (preset ?? string.Empty).Trim().ToLowerInvariant();
            if (key == "keeper aura" || key == "keeper" || key.Equals("keeper aura list", StringComparison.Ordinal))
            {
                OpenKeeperAuraPicker(player);
                return;
            }

            if (key.StartsWith("keeper aura ", StringComparison.Ordinal))
            {
                string type = key.Substring("keeper aura ".Length).Trim();
                string result = _unlocks.RunKeeperAura(player, type);
                Say(player, result);
                return;
            }

            if (key.StartsWith("keeper ", StringComparison.Ordinal))
            {
                string type = key.Substring("keeper ".Length).Trim();
                if (type.StartsWith("aura ", StringComparison.Ordinal))
                    type = type.Substring(5).Trim();
                string result = _unlocks.RunKeeperAura(player, type);
                Say(player, result);
                return;
            }

            if (key.StartsWith("trader wrangle", StringComparison.Ordinal) || key == "trader wrangle")
            {
                Say(player, _unlocks.RunTraderPreset(player, MercUnlockService.TraderModeWrangle));
                return;
            }

            if (key.StartsWith("trader umbral", StringComparison.Ordinal) || key == "trader umbral")
            {
                Say(player, _unlocks.RunTraderPreset(player, MercUnlockService.TraderModeUmbral));
                return;
            }

            if (key.StartsWith("mp composite", StringComparison.Ordinal) || key == "mp composite")
            {
                Say(player, _unlocks.RunMpPreset(player, MercUnlockService.MpModeComposite));
                return;
            }

            if (key.StartsWith("mp singles", StringComparison.Ordinal) || key == "mp singles")
            {
                Say(player, _unlocks.RunMpPreset(player, MercUnlockService.MpModeSingles));
                return;
            }

            if (key == "nt aoe" || key.StartsWith("nt aoe", StringComparison.Ordinal))
            {
                string mode = _unlocks.SetNtMode(player, MercUnlockService.NtModeAoe);
                Say(player, "NT nuke mode set to " + mode.ToUpperInvariant()
                    + ". Name shows (AOE); highest usable AOE nukes from items.dat.");
                return;
            }

            if (key == "nt st" || key.StartsWith("nt st", StringComparison.Ordinal))
            {
                string mode = _unlocks.SetNtMode(player, MercUnlockService.NtModeSt);
                Say(player, "NT nuke mode set to " + mode.ToUpperInvariant()
                    + ". Name shows (ST); highest usable single-target nukes from items.dat.");
                return;
            }

            Say(player, "Unknown preset. See .merc macros.");
        }

        void OpenKeeperAuraPicker(Player player)
        {
            MercDefinition? keeper = null;
            foreach (MercDefinition def in _unlocks.Roster.All)
            {
                if (def.Kind.Key == "keeper")
                {
                    keeper = def;
                    break;
                }
            }

            int level = keeper?.Level ?? 149;
            NpcCharacter? npc = keeper != null ? _unlocks.FindSummonedNpc(player, keeper) : null;
            var body = new StringBuilder();
            body.Append(DiagAoml.Title("Keeper auras"));
            body.Append("<br>").Append(DiagAoml.Muted(
                "From items.dat. One aura per NanoStrain slot. Highest rank the Keeper level allows."));
            body.Append("<br>").Append(DiagAoml.Muted(
                "Types: heal(Ambient/527), nano(Tone/527), damage(Vengeance/529), enervate(529), barrier(528), aao(Imminence/528), courage(900), ward(224)."));
            foreach (MercProfessionNanos.KeeperAuraOffer offer in _unlocks.Nanos.KeeperAurasFor(level, npc))
            {
                body.Append("<br>");
                if (offer.Available)
                {
                    body.Append(CmdLink(".merc preset keeper aura " + offer.Type, offer.Type));
                    body.Append(' ').Append(DiagAoml.Muted(Esc(offer.Name + " L"
                        + offer.RequiredLevel.ToString(CultureInfo.InvariantCulture)
                        + " strain " + offer.Strain.ToString(CultureInfo.InvariantCulture))));
                }
                else
                {
                    body.Append(DiagAoml.Muted(Esc(offer.Type + ": " + offer.Name
                        + " (needs L" + offer.RequiredLevel.ToString(CultureInfo.InvariantCulture) + ")")));
                }
            }

            body.Append("<br><br>").Append(CmdLink(".merc menu", "Back"));
            SendWindow(player, body.ToString(), "Keeper auras");
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
