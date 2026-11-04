namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Live-style merc windows: one <c>text://</c> blob per tell (no nested windows, no HTML-escaped tags),
    /// matching Buffbot. Main lists unlocked/accessible mercs by profession; detail and guest-buff blobs are
    /// separate tells. ChatText sizes its string with Int16 — stay under <see cref="MessageMax"/>.
    /// </summary>
    public sealed class MercMenuService
    {
        public const int MessageMax = 32000;
        public const string BotName = "Merc";
        public const double GuestTellRangeMeters = 40.0;
        public static readonly TimeSpan GuestTellCooldown = TimeSpan.FromSeconds(3);

        const string ColorHeader = "#DEDE42";
        const string ColorNcu = "#00BDBD";
        const string ColorMuted = "#D3BFFF";
        const string ColorTeam = "#FBFF96";

        const string FilterAll = "all";
        const string FilterBasic = "basic";
        const string FilterTwink = "twink";
        const string FilterElite = "elite";
        const string FilterChampion = "champion";
        const string FilterSummoned = "summoned";

        static readonly Dictionary<string, int> ProfessionIcons = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Adventurer"] = 6,
            ["Agent"] = 5,
            ["Bureaucrat"] = 8,
            ["Doctor"] = 10,
            ["Enforcer"] = 9,
            ["Engineer"] = 3,
            ["Fixer"] = 4,
            ["Keeper"] = 14,
            ["Martial Artist"] = 2,
            ["MartialArtist"] = 2,
            ["Metaphysicist"] = 12,
            ["Nano-Technician"] = 11,
            ["NanoTechnician"] = 11,
            ["Shade"] = 15,
            ["Soldier"] = 1,
            ["Trader"] = 7,
        };

        static readonly string[] FilterKeys =
        [
            FilterAll, FilterBasic, FilterTwink, FilterElite, FilterChampion, FilterSummoned
        ];

        readonly MercUnlockService _unlocks;
        readonly ConcurrentDictionary<string, DateTime> _guestCooldownUtc = new(StringComparer.Ordinal);

        public MercMenuService(MercUnlockService unlocks)
        {
            _unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
        }

        public void HandleTell(Player player, string botName, string message)
        {
            ArgumentNullException.ThrowIfNull(player);
            string trimmed = (message ?? string.Empty).Trim();
            MercDefinition? named = _unlocks.Roster.Find(botName);

            // Tell addressed to a specific roster merc (by id / nick / nameplate).
            if (named != null
                && !botName.Equals("Merc", StringComparison.OrdinalIgnoreCase)
                && !botName.Equals("Mercbot", StringComparison.OrdinalIgnoreCase)
                && !botName.Equals("MercMenu", StringComparison.OrdinalIgnoreCase))
            {
                if (_unlocks.IsSummoned(player, named))
                {
                    if (trimmed.Length == 0)
                    {
                        OpenMercDetail(player, named);
                        return;
                    }

                    Execute(player, trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                    return;
                }

                // Non-owner: only target-castable buffs when in range.
                if (TryOpenGuestBuffs(player, named, out string? guestError))
                    return;
                if (!string.IsNullOrEmpty(guestError))
                    Say(player, guestError);
                return;
            }

            if (trimmed.Length == 0)
            {
                IReadOnlyList<MercDefinition> summoned = _unlocks.SummonedFor(player);
                if (summoned.Count == 1)
                {
                    OpenMercDetail(player, summoned[0]);
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

            if (verb.Equals("view", StringComparison.OrdinalIgnoreCase)
                || verb.Equals("open", StringComparison.OrdinalIgnoreCase)
                || verb.Equals("detail", StringComparison.OrdinalIgnoreCase))
            {
                MercDefinition? target = args.Length > 1
                    ? _unlocks.Roster.Find(string.Join(" ", args, 1, args.Length - 1))
                    : null;
                if (target == null)
                {
                    Say(player, "Unknown merc. Use .merc menu and click a name.");
                    return;
                }

                if (_unlocks.GetState(player, target) == MercUnlockState.Locked)
                {
                    Say(player, "You have not unlocked " + _unlocks.DisplayNameFor(player, target) + " yet.");
                    return;
                }

                OpenMercDetail(player, target);
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
                    Say(player, "Summon a dungeon merc first, or use .merc view <name>.");
                    return;
                }

                OpenMercDetail(player, target);
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

            if (verb.Equals("summon", StringComparison.OrdinalIgnoreCase) && args.Length > 1)
            {
                MercDefinition? def = _unlocks.Roster.Find(string.Join(" ", args, 1, args.Length - 1));
                if (def == null)
                {
                    Say(player, "Unknown merc. Try .merc menu.");
                    return;
                }

                string? err = _unlocks.Summon(player, def);
                Say(player, err ?? ("Summoned " + _unlocks.DisplayNameFor(player, def)
                    + " (L" + def.Level.ToString(CultureInfo.InvariantCulture) + ")."));
                if (err == null)
                    OpenMercDetail(player, def);
                return;
            }

            if (verb.Equals("dismiss", StringComparison.OrdinalIgnoreCase))
            {
                MercDefinition? def = args.Length > 1
                    ? _unlocks.Roster.Find(string.Join(" ", args, 1, args.Length - 1))
                    : null;
                string? err = _unlocks.Dismiss(player, def);
                Say(player, err ?? (def == null
                    ? "Dismissed."
                    : "Dismissed " + _unlocks.DisplayNameFor(player, def) + "."));
                return;
            }

            if (verb.Equals("preset", StringComparison.OrdinalIgnoreCase) && args.Length > 1)
            {
                RunPreset(player, string.Join(" ", args, 1, args.Length - 1));
                return;
            }

            OpenMenu(player, FilterAll);
        }

        public void OpenMenu(Player player) => OpenMenu(player, FilterAll);

        public void OpenMenu(Player player, string filter)
        {
            ArgumentNullException.ThrowIfNull(player);
            string filterKey = string.IsNullOrWhiteSpace(filter) ? FilterAll : filter.Trim();
            foreach (string message in BuildMainMessages(player, filterKey))
                SendBlob(player, message);
        }

        public void OpenMercDetail(Player player, MercDefinition def)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(def);
            SendBlob(player, BuildOuter(BuildDetailInner(player, def), "Merc"));
        }

        public void OpenMacros(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            SendBlob(player, BuildOuter(BuildMacrosInner(player), "Merc macros"));
        }

        /// <summary>Test/helper: main menu tell payloads for this player and filter.</summary>
        public IReadOnlyList<string> BuildMainMessages(Player player, string filterKey)
        {
            ArgumentNullException.ThrowIfNull(player);
            string filter = string.IsNullOrWhiteSpace(filterKey) ? FilterAll : filterKey.Trim();
            var sections = new List<(string Name, string Body)>();
            foreach (Mercs.Kind kind in Mercs.Kinds)
            {
                var body = new StringBuilder();
                if (!AppendProfessionSection(body, player, kind, filter))
                    continue;
                sections.Add((kind.Title, body.ToString()));
            }

            string header = BuildMainHeader(filter);
            if (sections.Count == 0)
            {
                return
                [
                    BuildOuter(
                        header
                        + "\n\n<font color=" + ColorMuted + ">No unlocked mercs match this filter.</font>\n")
                ];
            }

            var messages = new List<string>();
            var current = new StringBuilder(header);
            foreach ((string _, string body) in sections)
            {
                if (current.Length > header.Length
                    && EstimateWrappedLength(current.Length + body.Length) > MessageMax)
                {
                    messages.Add(BuildOuter(current.ToString()));
                    current.Clear();
                    current.Append(header);
                    current.Append("\n\n<font color=").Append(ColorMuted).Append(">(continued)</font>");
                }

                current.Append(body);
            }

            if (current.Length > 0)
                messages.Add(BuildOuter(current.ToString()));
            return messages;
        }

        /// <summary>Pure render helpers for unit tests (no live player / GameData).</summary>
        public static class Render
        {
            public static string MainHeader(string filter) => BuildMainHeaderStatic(filter);

            public static string DetailSample(
                string shown,
                string tier,
                string description,
                string tierBlurb,
                bool summoned,
                bool canSummon,
                IReadOnlyList<(string Type, string Label)> stances,
                IReadOnlyList<(int NanoId, string Name, int Ncu, bool Available)> buffs)
            {
                var body = new StringBuilder();
                body.Append("<font color=").Append(ColorMuted).Append(">\n");
                body.Append("<font color=").Append(ColorHeader).Append('>')
                    .Append(EscText(shown)).Append(" — ").Append(EscText(tier)).Append("</font>\n");
                body.Append(EscText(description)).Append('\n');
                body.Append("<font color=").Append(ColorMuted).Append('>')
                    .Append(EscText(tierBlurb)).Append("</font>\n");
                body.Append(TellLink("summon " + "sample_id", "Summon")).Append(' ')
                    .Append(TellLink("dismiss sample_id", "Dismiss")).Append('\n');
                if (stances.Count > 0)
                {
                    body.Append("\n<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n");
                    body.Append("<font color=").Append(ColorHeader).Append(">Stance</font>\n");
                    foreach ((string type, string label) in stances)
                        body.Append(TellLink("preset " + type, label)).Append('\n');
                }

                body.Append("\n<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n");
                body.Append("<font color=").Append(ColorHeader).Append(">Buffs</font>\n");
                foreach ((int nanoId, string name, int ncu, bool available) in buffs)
                {
                    if (available)
                    {
                        body.Append("<a href='chatcmd:///tell ").Append(BotName).Append(" cast ")
                            .Append(nanoId.ToString(CultureInfo.InvariantCulture)).Append("'>")
                            .Append(EscText(name)).Append("</a>");
                    }
                    else
                    {
                        body.Append("<font color=").Append(ColorMuted).Append('>')
                            .Append(EscText(name)).Append("</font>");
                    }

                    body.Append(" <font color=").Append(ColorNcu).Append(">[NCU: ")
                        .Append(ncu.ToString(CultureInfo.InvariantCulture)).Append("]</font>\n");
                }

                body.Append('\n').Append(TellLink("menu", "Back to Mercs")).Append('\n');
                _ = summoned;
                _ = canSummon;
                return BuildOuter(body.ToString(), "Merc");
            }

            public static string GuestBuffsSample(
                string mercName,
                IReadOnlyList<(int NanoId, string Name, int Ncu)> buffs)
            {
                var body = new StringBuilder();
                body.Append("<font color=").Append(ColorMuted).Append(">\n");
                body.Append("<font color=").Append(ColorHeader).Append('>')
                    .Append(EscText(mercName)).Append(" can buff you</font>\n");
                body.Append("<font color=").Append(ColorMuted).Append('>')
                    .Append("Owner controls (summon, dismiss, stances) are hidden. Same playfield, close range.</font>\n");
                body.Append("\n<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n");
                foreach ((int nanoId, string name, int ncu) in buffs)
                {
                    body.Append("<a href='chatcmd:///tell ").Append(BotName).Append(" cast ")
                        .Append(nanoId.ToString(CultureInfo.InvariantCulture)).Append("'>")
                        .Append(EscText(name)).Append("</a>")
                        .Append(" <font color=").Append(ColorNcu).Append(">[NCU: ")
                        .Append(ncu.ToString(CultureInfo.InvariantCulture)).Append("]</font>\n");
                }

                return BuildOuter(body.ToString(), "Merc Buffs");
            }
        }

        bool TryOpenGuestBuffs(Player player, MercDefinition def, out string? error)
        {
            error = null;
            if (!TryFindSummonedNearby(player, def, out Player? owner, out NpcCharacter? npc)
                || owner == null
                || npc == null)
            {
                error = "That merc is not summoned nearby.";
                return false;
            }

            if (ReferenceEquals(owner, player))
            {
                OpenMercDetail(player, def);
                return true;
            }

            if (!ReferenceEquals(player.Playfield, npc.Playfield))
            {
                error = "You must be on the same playfield as " + (npc.Name ?? def.DisplayName) + ".";
                return false;
            }

            if (player.Distance3D(npc) > GuestTellRangeMeters)
            {
                error = "Move closer to " + (npc.Name ?? def.DisplayName) + " to request buffs.";
                return false;
            }

            string key = player.Identity.Instance.ToString(CultureInfo.InvariantCulture) + ":" + def.Id;
            DateTime now = DateTime.UtcNow;
            if (_guestCooldownUtc.TryGetValue(key, out DateTime last)
                && now - last < GuestTellCooldown)
            {
                error = "Wait a moment before asking that merc again.";
                return false;
            }

            _guestCooldownUtc[key] = now;
            SendBlob(player, BuildOuter(BuildGuestBuffInner(player, def, npc), "Merc Buffs"));
            return true;
        }

        bool TryFindSummonedNearby(
            Player requester,
            MercDefinition def,
            out Player? owner,
            out NpcCharacter? npc)
        {
            owner = null;
            npc = null;
            Playfield? playfield = requester.Playfield as Playfield;
            if (playfield == null)
                return false;

            DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
            foreach (Dynel dynel in registry.Players())
            {
                if (dynel is not Player candidate)
                    continue;
                if (!_unlocks.IsSummoned(candidate, def))
                    continue;
                NpcCharacter? pet = _unlocks.FindSummonedNpc(candidate, def);
                if (pet == null || pet.IsDead)
                    continue;
                if (!ReferenceEquals(pet.Playfield, playfield))
                    continue;
                owner = candidate;
                npc = pet;
                return true;
            }

            return false;
        }

        bool AppendProfessionSection(StringBuilder body, Player player, Mercs.Kind kind, string filter)
        {
            var rows = new List<string>();
            foreach (MercDefinition def in _unlocks.Roster.All)
            {
                if (!string.Equals(def.Kind.Key, kind.Key, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!PassesAccess(player, def))
                    continue;
                if (!PassesFilter(player, def, filter))
                    continue;
                rows.Add(BuildMercListRow(player, def));
            }

            if (rows.Count == 0)
                return false;

            body.Append("\n    <img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n\n");
            body.Append("    <img src=tdb://id:GFX_GUI_FRIENDLIST_STATUS_GREEN> ");
            if (ProfessionIcons.TryGetValue(kind.Title, out int icon)
                || ProfessionIcons.TryGetValue(kind.Key, out icon))
            {
                body.Append("<img src=tdb://id:GFX_GUI_ICON_PROFESSION_")
                    .Append(icon.ToString(CultureInfo.InvariantCulture))
                    .Append("> ");
            }

            body.Append("<font color=").Append(ColorHeader).Append('>')
                .Append(EscText(kind.Title)).Append("</font>\n");
            foreach (string row in rows)
                body.Append("    ").Append(row);
            return true;
        }

        string BuildMercListRow(Player player, MercDefinition def)
        {
            string shown = _unlocks.DisplayNameFor(player, def);
            string tier = ContentTierLabel(def);
            bool summoned = _unlocks.IsSummoned(player, def);
            var sb = new StringBuilder();
            sb.Append(TellLink("view " + def.Id, shown));
            sb.Append(" <font color=").Append(ColorMuted).Append('>')
                .Append(EscText(tier))
                .Append(" · L").Append(def.Level.ToString(CultureInfo.InvariantCulture))
                .Append("</font>");
            if (summoned)
                sb.Append(" <font color=").Append(ColorTeam).Append(">[Summoned]</font>");
            sb.Append('\n');
            return sb.ToString();
        }

        string BuildDetailInner(Player player, MercDefinition def)
        {
            string shown = _unlocks.DisplayNameFor(player, def);
            string tier = ContentTierLabel(def);
            bool summoned = _unlocks.IsSummoned(player, def);
            MercUnlockState state = _unlocks.GetState(player, def);
            NpcCharacter? npc = summoned ? _unlocks.FindSummonedNpc(player, def) : null;

            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append('>')
                .Append(EscText(shown)).Append(" — ").Append(EscText(tier)).Append("</font>\n");
            body.Append(EscText(BuildDescription(def))).Append('\n');
            body.Append("<font color=").Append(ColorMuted).Append('>')
                .Append(EscText(TierBlurb(tier))).Append("</font>\n");

            if (summoned)
                body.Append(TellLink("dismiss " + def.Id, "Dismiss")).Append('\n');
            else if (_unlocks.CanSummonHere(player, def, state, out string reason))
                body.Append(TellLink("summon " + def.Id, "Summon")).Append('\n');
            else
                body.Append("<font color=").Append(ColorMuted).Append('>')
                    .Append(EscText(reason)).Append("</font>\n");

            AppendStanceBlock(body, player, def);
            AppendBuffBlock(body, player, def, npc, guestOnly: false);

            body.Append('\n').Append(TellLink("menu", "Back to Mercs")).Append('\n');
            return body.ToString();
        }

        string BuildGuestBuffInner(Player player, MercDefinition def, NpcCharacter npc)
        {
            string shown = string.IsNullOrWhiteSpace(npc.Name) ? def.DisplayName : npc.Name!;
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append('>')
                .Append(EscText(shown)).Append(" can buff you</font>\n");
            body.Append("<font color=").Append(ColorMuted).Append('>')
                .Append("Owner-only controls (summon, dismiss, stances, AOE toggles) are hidden.</font>\n");
            AppendBuffBlock(body, player, def, npc, guestOnly: true);
            return body.ToString();
        }

        void AppendStanceBlock(StringBuilder body, Player player, MercDefinition def)
        {
            string key = def.Kind.Key;
            bool any = false;
            void EnsureHeader()
            {
                if (any)
                    return;
                body.Append("\n    <img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n\n");
                body.Append("    <font color=").Append(ColorHeader).Append(">Stance</font>\n");
                any = true;
            }

            if (string.Equals(key, "keeper", StringComparison.OrdinalIgnoreCase))
            {
                EnsureHeader();
                body.Append("    ").Append(TellLink("preset keeper aura", "Aura list")).Append('\n');
                foreach (string type in _unlocks.Nanos.KeeperAuraTypes())
                    body.Append("    ").Append(TellLink("preset keeper aura " + type, "Aura: " + type)).Append('\n');
            }

            if (string.Equals(key, "nanotechnician", StringComparison.OrdinalIgnoreCase))
            {
                EnsureHeader();
                string mode = _unlocks.GetNtMode(player).ToUpperInvariant();
                body.Append("    <font color=").Append(ColorMuted).Append(">NT nuke mode: ")
                    .Append(EscText(mode)).Append("</font>\n");
                body.Append("    ").Append(TellLink("preset nt aoe", "AOE")).Append(' ')
                    .Append(TellLink("preset nt st", "Single target")).Append('\n');
            }

            if (string.Equals(key, "trader", StringComparison.OrdinalIgnoreCase))
            {
                EnsureHeader();
                string mode = _unlocks.GetTraderMode(player).ToUpperInvariant();
                body.Append("    <font color=").Append(ColorMuted).Append(">Trader mode: ")
                    .Append(EscText(mode)).Append("</font>\n");
                body.Append("    ").Append(TellLink("preset trader wrangle", "Wrangle")).Append(' ')
                    .Append(TellLink("preset trader umbral", "Umbral")).Append('\n');
            }

            if (string.Equals(key, "metaphysicist", StringComparison.OrdinalIgnoreCase))
            {
                EnsureHeader();
                string mode = _unlocks.GetMpMode(player).ToUpperInvariant();
                body.Append("    <font color=").Append(ColorMuted).Append(">MP mode: ")
                    .Append(EscText(mode)).Append("</font>\n");
                body.Append("    ").Append(TellLink("preset mp composite", "Composite")).Append(' ')
                    .Append(TellLink("preset mp singles", "Singles")).Append('\n');
            }
        }

        void AppendBuffBlock(
            StringBuilder body,
            Player player,
            MercDefinition def,
            NpcCharacter? npc,
            bool guestOnly)
        {
            IReadOnlyList<MercProfessionNanos.MercBuffOffer> offers =
                _unlocks.Nanos.BuffsFor(def.Kind, def.Level, npc);
            body.Append("\n    <img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n\n");
            body.Append("    <font color=").Append(ColorHeader).Append('>')
                .Append(guestOnly ? "Buffs for you" : "Buffs").Append("</font>\n");
            if (offers.Count == 0)
            {
                body.Append("    <font color=").Append(ColorMuted).Append('>')
                    .Append("No team/other castable buffs in items.dat for this profession/level.</font>\n");
                return;
            }

            int listed = 0;
            foreach (MercProfessionNanos.MercBuffOffer offer in offers)
            {
                if (guestOnly && !offer.Available)
                    continue;
                body.Append("    ");
                if (offer.Available)
                {
                    body.Append("<a href='chatcmd:///tell ").Append(BotName).Append(" cast ")
                        .Append(offer.NanoId.ToString(CultureInfo.InvariantCulture)).Append("'>")
                        .Append(EscText(offer.Name)).Append("</a>");
                }
                else
                {
                    body.Append("<font color=").Append(ColorMuted).Append('>')
                        .Append(EscText(offer.Name))
                        .Append(" - merc needs level ")
                        .Append(offer.RequiredLevel.ToString(CultureInfo.InvariantCulture))
                        .Append("</font>");
                }

                body.Append(" <font color=").Append(ColorNcu).Append(">[NCU: ")
                    .Append(offer.Ncu.ToString(CultureInfo.InvariantCulture)).Append("]</font>\n");
                listed++;
            }

            if (guestOnly && listed == 0)
            {
                body.Append("    <font color=").Append(ColorMuted).Append('>')
                    .Append("No available buffs for your level right now.</font>\n");
            }

            _ = player;
        }

        string BuildMacrosInner(Player player)
        {
            string traderMode = _unlocks.GetTraderMode(player).ToUpperInvariant();
            string mpMode = _unlocks.GetMpMode(player).ToUpperInvariant();
            string ntMode = _unlocks.GetNtMode(player).ToUpperInvariant();
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append(">Merc macros</font>\n");
            body.Append("Modes: NT=").Append(EscText(ntMode))
                .Append(" Trader=").Append(EscText(traderMode))
                .Append(" MP=").Append(EscText(mpMode))
                .Append(". Presets pick highest items.dat ranks at merc level.\n");
            body.Append('\n').Append(TellLink("menu", "Merc menu")).Append('\n');
            body.Append(TellLink("preset keeper aura", "Keeper aura list")).Append('\n');
            foreach (string type in _unlocks.Nanos.KeeperAuraTypes())
                body.Append(TellLink("preset keeper aura " + type, "Keeper " + type)).Append('\n');
            body.Append(TellLink("preset trader wrangle", "Trader wrangle")).Append('\n');
            body.Append(TellLink("preset trader umbral", "Trader Umbral")).Append('\n');
            body.Append(TellLink("preset mp composite", "MP composite")).Append('\n');
            body.Append(TellLink("preset mp singles", "MP singles")).Append('\n');
            body.Append(TellLink("preset nt aoe", "NT AOE")).Append('\n');
            body.Append(TellLink("preset nt st", "NT ST")).Append('\n');
            return body.ToString();
        }

        static string BuildMainHeader(string filter) => BuildMainHeaderStatic(filter);

        static string BuildMainHeaderStatic(string filter)
        {
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append(">Mercs</font>\n");
            body.Append("Unlocked and accessible dungeon mercs. Click a name for summon, stance, and buffs.\n");
            body.Append("Filter:");
            foreach (string key in FilterKeys)
            {
                body.Append(' ');
                if (string.Equals(filter, key, StringComparison.OrdinalIgnoreCase))
                    body.Append("<font color=").Append(ColorHeader).Append(">[")
                        .Append(EscText(key)).Append("]</font>");
                else
                    body.Append(TellLink("filter " + key, key));
            }

            body.Append('\n').Append(TellLink("macros", "Macros")).Append('\n');
            return body.ToString();
        }

        bool PassesAccess(Player player, MercDefinition def)
            => _unlocks.GetState(player, def) != MercUnlockState.Locked;

        bool PassesFilter(Player player, MercDefinition def, string filter)
        {
            if (string.Equals(filter, FilterAll, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(filter, FilterSummoned, StringComparison.OrdinalIgnoreCase))
                return _unlocks.IsSummoned(player, def);
            if (string.Equals(filter, FilterBasic, StringComparison.OrdinalIgnoreCase))
                return IsBasicContent(def);
            if (string.Equals(filter, FilterTwink, StringComparison.OrdinalIgnoreCase))
                return def.HasTag(FilterTwink);
            if (string.Equals(filter, FilterElite, StringComparison.OrdinalIgnoreCase))
                return def.HasTag(FilterElite);
            if (string.Equals(filter, FilterChampion, StringComparison.OrdinalIgnoreCase))
                return def.HasTag(FilterChampion);
            return def.HasTag(filter);
        }

        static bool IsBasicContent(MercDefinition def)
            => !def.HasTag(FilterTwink) && !def.HasTag(FilterElite) && !def.HasTag(FilterChampion);

        static string ContentTierLabel(MercDefinition def)
        {
            if (def.HasTag(FilterTwink))
                return "Twink";
            if (def.HasTag(FilterElite))
                return "Elite";
            if (def.HasTag(FilterChampion))
                return "Champion";
            if (def.HasTag(FilterBasic))
                return "Basic";
            return "Basic";
        }

        static string BuildDescription(MercDefinition def)
            => "Unlock from " + def.DungeonName + " (" + def.BossName + "). Role: " + def.Role
               + ". Merc body level " + def.Level.ToString(CultureInfo.InvariantCulture) + ".";

        static string TierBlurb(string tier) => tier switch
        {
            "Twink" => "Twink-tier dungeon merc: high-end buff/combat package for its unlock level.",
            "Elite" => "Elite-tier dungeon merc: strong mid/high package for its unlock level.",
            "Champion" => "Champion-tier dungeon merc: heavy combat package for its unlock level.",
            _ => "Basic-access dungeon merc: standard package for its unlock level."
        };

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
                Say(player, _unlocks.RunKeeperAura(player, type));
                return;
            }

            if (key.StartsWith("keeper ", StringComparison.Ordinal))
            {
                string type = key.Substring("keeper ".Length).Trim();
                if (type.StartsWith("aura ", StringComparison.Ordinal))
                    type = type.Substring(5).Trim();
                Say(player, _unlocks.RunKeeperAura(player, type));
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
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append(">Keeper auras</font>\n");
            body.Append("From items.dat. One aura per NanoStrain. Highest rank the Keeper level allows.\n");
            foreach (MercProfessionNanos.KeeperAuraOffer offer in _unlocks.Nanos.KeeperAurasFor(level, npc))
            {
                if (offer.Available)
                {
                    body.Append(TellLink("preset keeper aura " + offer.Type, offer.Type))
                        .Append(" <font color=").Append(ColorMuted).Append('>')
                        .Append(EscText(offer.Name + " L" + offer.RequiredLevel.ToString(CultureInfo.InvariantCulture)))
                        .Append("</font>\n");
                }
                else
                {
                    body.Append("<font color=").Append(ColorMuted).Append('>')
                        .Append(EscText(offer.Type + ": " + offer.Name
                            + " (needs L" + offer.RequiredLevel.ToString(CultureInfo.InvariantCulture) + ")"))
                        .Append("</font>\n");
                }
            }

            body.Append('\n').Append(TellLink("menu", "Back")).Append('\n');
            SendBlob(player, BuildOuter(body.ToString(), "Keeper auras"));
        }

        public static string BuildOuter(string inner, string label = "Merc Menu")
        {
            if (!inner.EndsWith("</font>\n", StringComparison.Ordinal)
                && !inner.EndsWith("</font>", StringComparison.Ordinal))
                inner += "</font>";
            return "<font color='" + ColorMuted + "'><a href=\"text://" + inner + "\">"
                + EscText(label) + "</a></font>";
        }

        static int EstimateWrappedLength(int innerLength) => innerLength + 48;

        static string TellLink(string command, string label)
            => "<a href='chatcmd:///tell " + BotName + " " + command + "'>" + EscText(label) + "</a>";

        public static string EscText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("'", "&#39;", StringComparison.Ordinal);
        }

        static void SendBlob(Player player, string message)
        {
            if (player.Session == null)
                return;
            GmCommandFeedback.Send(player.Session, player, message);
        }

        static void Say(Player player, string text)
        {
            if (player.Session == null)
                return;
            GmCommandFeedback.Send(player.Session, player, text);
        }
    }
}
