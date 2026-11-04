namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    using ZoneEngine_New.Core.Bots;
    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;

    using CharacterStat = SmokeLounge.AOtomation.Messaging.GameData.CharacterStat;

    /// <summary>
    /// Live-style merc windows via <see cref="SendBlob"/> / <see cref="BuildOuter"/>.
    /// <para>
    /// AOML quote rule — outer <c>href="text://"</c> (Nadybot/Tyrbot makeBlob); nested
    /// <c>href='text://'</c> with raw tags (TSW FAQ; InfoGuide). Do not escape tags
    /// (<c>&amp;lt;</c> shows as code — d35eaec6). Nested bodies: no raw quotes.
    /// Action links inside nested pages use <see cref="SayLink"/> =
    /// <c>href=&amp;#39;chatcmd://...&amp;#39;</c> (InfoGuide embedding of Nadybot/Tyrbot
    /// makeChatcmd single-quoted form). Nested-in-nested merc pages from filters use
    /// <see cref="NestedLinkDeep"/>. See <c>.merc linktest</c> for A–E live variants.
    /// </para>
    /// </summary>
    public sealed class MercMenuService
    {
        public const int MessageMax = 32000;
        public const string BotName = "Merc";
        public const double GuestTellRangeMeters = 40.0;
        public static readonly TimeSpan GuestTellCooldown = TimeSpan.FromSeconds(3);

        const string ColorHeader = "#DEDE42";
        const string ColorBonus = "#F07171";
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
        readonly BuffbotCatalog _buffbot;
        readonly IZoneLogger _logger;
        readonly ConcurrentDictionary<string, DateTime> _guestCooldownUtc = new(StringComparer.Ordinal);

        public MercMenuService(MercUnlockService unlocks, BuffbotCatalog buffbot, IZoneLogger logger)
        {
            _unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
            _buffbot = buffbot ?? throw new ArgumentNullException(nameof(buffbot));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            if (verb.Equals("hire", StringComparison.OrdinalIgnoreCase)
                || verb.Equals("contract", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || Mercs.Find(string.Join(" ", args, 1, args.Length - 1)) is not { } hireKind)
                {
                    Say(player, "Unknown hire. Use .merc menu and click a hired contract.");
                    return;
                }

                if (!PetService.HasContract(player, hireKind)
                    && !player.OwnedPets.All.Any(p => p.Pet != null && !p.IsDead
                        && string.Equals(p.Pet.Hash, hireKind.Hash, StringComparison.OrdinalIgnoreCase)))
                {
                    Say(player, "You have no " + hireKind.Title + " contract.");
                    return;
                }

                OpenHireDetail(player, hireKind);
                return;
            }

            if (verb.Equals("macros", StringComparison.OrdinalIgnoreCase)
                || verb.Equals("macro", StringComparison.OrdinalIgnoreCase))
            {
                OpenMacros(player);
                return;
            }

            if (verb.Equals("linktest", StringComparison.OrdinalIgnoreCase))
            {
                OpenLinkTest(player);
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
                CastMercBuff(player, nanoId);
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
                string rest = args.Length > 1
                    ? string.Join(" ", args, 1, args.Length - 1)
                    : string.Empty;
                if (rest.Length == 0 || rest.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    string? err = _unlocks.Dismiss(player, null);
                    PetService? petsAll = player.Playfield?.GetService<PetService>();
                    if (rest.Equals("all", StringComparison.OrdinalIgnoreCase) && petsAll != null)
                        petsAll.ParkMercs(player);
                    Say(player, err ?? "Dismissed.");
                    OpenMenu(player);
                    return;
                }

                MercDefinition? def = _unlocks.Roster.Find(rest);
                if (def != null)
                {
                    string shown = _unlocks.DisplayNameFor(player, def);
                    string? err = _unlocks.Dismiss(player, def);
                    Say(player, err ?? ("Dismissed " + shown + "."));
                    OpenMenu(player);
                    return;
                }

                if (Mercs.Find(rest) is { } kind)
                {
                    PetService? pets = player.Playfield?.GetService<PetService>();
                    if (pets == null)
                    {
                        Say(player, "Mercenaries are not available here.");
                        return;
                    }

                    Say(player, pets.ParkMerc(player, kind)
                        ? "Dismissed " + kind.Title + ". Contract kept - use Call or open the merc to resummon."
                        : "You have no " + kind.Title + " under contract.");
                    OpenMenu(player);
                    return;
                }

                Say(player, "Unknown merc. Use .merc menu and click dismiss, or .merc dismiss <name|all>.");
                return;
            }

            if (verb.Equals("call", StringComparison.OrdinalIgnoreCase))
            {
                PetService? pets = player.Playfield?.GetService<PetService>();
                if (pets == null)
                {
                    Say(player, "Mercenaries are not available here.");
                    return;
                }

                player.OwnedPets.MercReviveUtc.Clear();
                player.OwnedPets.MercCheckUtc = DateTime.MinValue;
                pets.TickMercs(player);
                Say(player, "Calling your mercenaries back.");
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

        public void OpenHireDetail(Player player, Mercs.Kind kind)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(kind);
            Mercs.Tier tier = Mercs.GetTier(player, kind);
            bool here = player.OwnedPets.All.Any(p => p.Pet != null && !p.IsDead
                && string.Equals(p.Pet.Hash, kind.Hash, StringComparison.OrdinalIgnoreCase));
            SendBlob(player, BuildOuter(BuildHireDetailInner(player, kind, tier, here), kind.Title));
        }

        public void OpenMacros(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            SendBlob(player, BuildOuter(BuildMacrosInner(player), "Merc macros"));
        }

        /// <summary>
        /// Opens one outer window with a nested page of labelled chatcmd / nested-text variants
        /// for the same harmless action (<c>.merc menu</c>). Randy compares which render as links.
        /// </summary>
        public void OpenLinkTest(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            SendBlob(player, BuildOuter(BuildLinkTestOuterInner(), "Merc linktest"));
        }

        /// <summary>Test/helper: main menu tell payloads for this player and filter.</summary>
        public IReadOnlyList<string> BuildMainMessages(Player player, string filterKey)
        {
            ArgumentNullException.ThrowIfNull(player);
            string filter = string.IsNullOrWhiteSpace(filterKey) ? FilterAll : filterKey.Trim();
            var sections = new List<(string Name, string Body)>();

            // Profession hire contracts (ContractsStat 10280 / live MR** pets) - not the dungeon unlock table.
            var hired = new StringBuilder();
            if (AppendHiredContractsSection(hired, player, filter))
                sections.Add(("Hired", hired.ToString()));

            foreach (Mercs.Kind kind in Mercs.Kinds)
            {
                var body = new StringBuilder();
                if (!AppendProfessionSection(body, player, kind, filter))
                    continue;
                sections.Add((kind.Title, body.ToString()));
            }

            string header = BuildMainHeader(player, filter);
            if (sections.Count == 0)
            {
                return
                [
                    BuildOuter(
                        header
                        + "\n\n<font color=" + ColorMuted + ">No hired or unlocked mercs match this filter.</font>\n")
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
            public static string MainHeader(string filter)
                => BuildMainHeaderStatic(
                    filter,
                    NestedLink(
                        "<font color=" + ColorMuted + ">\n"
                        + "<font color=" + ColorHeader + ">Merc macros</font>\n"
                        + "Preset links.\n",
                        "Macros"),
                    BuildFilterLinksSample(filter));

            /// <summary>Sample filter NestedLinks with NestedLinkDeep merc names.</summary>
            public static string BuildFilterLinksSample(string activeFilter)
            {
                string sampleDetail =
                    "<font color=" + ColorMuted + ">\n"
                    + "<font color=" + ColorHeader + ">Sample Merc - Basic</font>\n"
                    + SayLink("menu", "Back to Mercs") + "\n";
                var links = new StringBuilder();
                foreach (string key in FilterKeys)
                {
                    links.Append(' ');
                    if (string.Equals(activeFilter, key, StringComparison.OrdinalIgnoreCase))
                    {
                        links.Append("<font color=").Append(ColorHeader).Append(">[")
                            .Append(EscText(key)).Append("]</font>");
                    }
                    else
                    {
                        links.Append(NestedLink(
                            "<font color=" + ColorMuted + ">\n"
                            + "<font color=" + ColorHeader + ">Filter: " + EscText(key) + "</font>\n"
                            + NestedLinkDeep(sampleDetail, "Sample Merc") + " (Basic)\n",
                            key));
                    }
                }

                return links.ToString();
            }

            public static string LinkTestSample() => BuildLinkTestOuterInner();

            public static string NestedLinkDeep(string body, string label)
                => MercMenuService.NestedLinkDeep(body, label);

            public static string DetailInnerSample(
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
                    .Append(EscText(shown)).Append(" - ").Append(EscText(tier)).Append("</font>\n");
                body.Append(EscText(description)).Append('\n');
                body.Append("<font color=").Append(ColorMuted).Append('>')
                    .Append(EscText(tierBlurb)).Append("</font>\n");
                if (summoned)
                    body.Append(SayLink("dismiss sample_id", "Dismiss")).Append('\n');
                else if (canSummon)
                    body.Append(SayLink("summon sample_id", "Summon")).Append('\n');
                if (stances.Count > 0)
                {
                    body.Append("\n<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n");
                    body.Append("<font color=").Append(ColorHeader).Append(">Stance</font>\n");
                    foreach ((string type, string label) in stances)
                        body.Append(SayLink("preset " + type, label)).Append('\n');
                }

                body.Append("\n<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n");
                body.Append("<font color=").Append(ColorHeader).Append(">Buffs</font>\n");
                foreach ((int nanoId, string name, int ncu, bool available) in buffs)
                {
                    if (available)
                        body.Append(SayLink("cast " + nanoId.ToString(CultureInfo.InvariantCulture), name));
                    else
                    {
                        body.Append("<font color=").Append(ColorMuted).Append('>')
                            .Append(SayLink("cast " + nanoId.ToString(CultureInfo.InvariantCulture), name))
                            .Append("</font>");
                    }

                    body.Append(" <font color=").Append(ColorNcu).Append(">[NCU: ")
                        .Append(ncu.ToString(CultureInfo.InvariantCulture)).Append("]</font>\n");
                }

                body.Append('\n').Append(SayLink("menu", "Back to Mercs")).Append('\n');
                return body.ToString();
            }

            public static string DetailSample(
                string shown,
                string tier,
                string description,
                string tierBlurb,
                bool summoned,
                bool canSummon,
                IReadOnlyList<(string Type, string Label)> stances,
                IReadOnlyList<(int NanoId, string Name, int Ncu, bool Available)> buffs)
                => BuildOuter(
                    DetailInnerSample(shown, tier, description, tierBlurb, summoned, canSummon, stances, buffs),
                    "Merc");

            public static string HireDetailInnerSample(
                string title,
                string tier,
                string description,
                string tierBlurb,
                bool present,
                IReadOnlyList<(int NanoId, string Name, int Ncu)> buffs)
            {
                var body = new StringBuilder();
                body.Append("<font color=").Append(ColorMuted).Append(">\n");
                body.Append("<font color=").Append(ColorHeader).Append('>')
                    .Append(EscText(title)).Append(" - ").Append(EscText(tier)).Append("</font>\n");
                body.Append(EscText(description)).Append('\n');
                body.Append("<font color=").Append(ColorMuted).Append('>')
                    .Append(EscText(tierBlurb)).Append("</font>\n");
                if (present)
                    body.Append(SayLink("dismiss soldier", "Dismiss")).Append('\n');
                else
                    body.Append(SayLink("call", "Summon")).Append('\n');
                body.Append("\n<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n");
                body.Append("<font color=").Append(ColorHeader).Append(">Buffs</font>\n");
                foreach ((int nanoId, string name, int ncu) in buffs)
                {
                    body.Append(SayLink("cast " + nanoId.ToString(CultureInfo.InvariantCulture), name))
                        .Append(" <font color=").Append(ColorNcu).Append(">[NCU: ")
                        .Append(ncu.ToString(CultureInfo.InvariantCulture)).Append("]</font>\n");
                }

                body.Append('\n').Append(SayLink("menu", "Back to Mercs")).Append('\n');
                return body.ToString();
            }

            public static string NestedLink(string body, string label) => MercMenuService.NestedLink(body, label);

            /// <summary>Main menu: nested text:// for hire/detail/Macros/filters; say for dismiss.</summary>
            public static string MainWithNestedDetailSample()
            {
                string hire = HireDetailInnerSample(
                    title: "Soldier",
                    tier: "Basic",
                    description: "Profession hire contract.",
                    tierBlurb: "Basic hire.",
                    present: true,
                    buffs: new List<(int, string, int)> { (1001, "Full Automatic Targeting", 4) });
                string detail = DetailInnerSample(
                    shown: "Brock Anvil Malone",
                    tier: "Twink",
                    description: "Unlock from Subway.",
                    tierBlurb: "Twink-tier.",
                    summoned: true,
                    canSummon: false,
                    stances: Array.Empty<(string, string)>(),
                    buffs: new List<(int, string, int, bool)> { (1002, "Team Reflective Field", 4, true) });
                var body = new StringBuilder();
                body.Append(BuildMainHeaderStatic(
                    FilterAll,
                    NestedLink(
                        "<font color=" + ColorMuted + ">\n"
                        + "<font color=" + ColorHeader + ">Merc macros</font>\n"
                        + "Preset links.\n",
                        "Macros"),
                    BuildFilterLinksSample(FilterAll)));
                body.Append("\n    <img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n\n");
                body.Append("    <img src=tdb://id:GFX_GUI_FRIENDLIST_STATUS_GREEN> ");
                body.Append("<font color=").Append(ColorHeader).Append(">Hired contracts</font>\n");
                body.Append("    ").Append(NestedLink(hire, "Soldier"));
                body.Append(" <font color=").Append(ColorMuted).Append(">Basic - hire</font>");
                body.Append(" <font color=").Append(ColorTeam).Append(">[Summoned]</font>");
                body.Append(' ').Append(SayLink("dismiss soldier", "dismiss")).Append('\n');
                body.Append("\n    <img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n\n");
                body.Append("    <font color=").Append(ColorHeader).Append(">Enforcer</font>\n");
                body.Append("    ").Append(NestedLink(detail, "Brock Anvil Malone"));
                body.Append(" <font color=").Append(ColorMuted).Append(">Twink - L24</font>\n");
                return BuildOuter(body.ToString());
            }

            /// <summary>Hired row still listed after park-dismiss (contract kept, Summon link).</summary>
            public static string HiredContractRowSample(bool present)
            {
                var row = new StringBuilder();
                row.Append(NestedLink(
                    HireDetailInnerSample(
                        "Soldier",
                        "Basic",
                        "Profession hire.",
                        "Basic.",
                        present,
                        new List<(int, string, int)> { (1001, "Full Automatic Targeting", 4) }),
                    "Soldier"));
                row.Append(" <font color=").Append(ColorMuted).Append(">Basic - hire</font>");
                if (present)
                {
                    row.Append(" <font color=").Append(ColorTeam).Append(">[Summoned]</font>");
                    row.Append(' ').Append(SayLink("dismiss soldier", "dismiss"));
                }
                else
                    row.Append(' ').Append(SayLink("call", "summon"));
                row.Append('\n');
                return row.ToString();
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
                    body.Append(SayLink("cast " + nanoId.ToString(CultureInfo.InvariantCulture), name))
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
            sb.Append(NestedLink(BuildDetailInner(player, def), shown));
            sb.Append(" <font color=").Append(ColorMuted).Append('>')
                .Append(EscText(tier))
                .Append(" - L").Append(def.Level.ToString(CultureInfo.InvariantCulture))
                .Append("</font>");
            if (summoned)
            {
                sb.Append(" <font color=").Append(ColorTeam).Append(">[Summoned]</font>");
                sb.Append(' ').Append(SayLink("dismiss " + def.Id, "dismiss"));
            }

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
                .Append(EscText(shown)).Append(" - ").Append(EscText(tier)).Append("</font>\n");
            body.Append(EscText(BuildDescription(def))).Append('\n');
            body.Append("<font color=").Append(ColorMuted).Append('>')
                .Append(EscText(TierBlurb(tier))).Append("</font>\n");

            if (summoned)
                body.Append(SayLink("dismiss " + def.Id, "Dismiss")).Append('\n');
            else if (_unlocks.CanSummonHere(player, def, state, out string reason))
                body.Append(SayLink("summon " + def.Id, "Summon")).Append('\n');
            else
                body.Append("<font color=").Append(ColorMuted).Append('>')
                    .Append(EscText(reason)).Append("</font>\n");

            AppendStanceBlock(body, player, def.Kind);
            AppendBuffBlock(body, player, def.Kind, def.Level, npc, guestOnly: false);

            body.Append('\n').Append(SayLink("menu", "Back to Mercs")).Append('\n');
            return body.ToString();
        }

        string BuildHireDetailInner(Player player, Mercs.Kind kind, Mercs.Tier tier, bool present)
        {
            int ownerLevel = Math.Max(1, player.Stats.GetOrOne(CharacterStat.Level));
            int powerLevel = Mercs.PowerLevel(ownerLevel, tier);
            NpcCharacter? npc = null;
            foreach (NpcCharacter pet in player.OwnedPets.All)
            {
                if (pet.Pet != null && !pet.IsDead
                    && string.Equals(pet.Pet.Hash, kind.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    npc = pet;
                    break;
                }
            }

            string tierName = Mercs.TierName(tier);
            Mercs.Package package = Mercs.PackageFor(tier);
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append('>')
                .Append(EscText(kind.Title)).Append(" - ").Append(EscText(tierName)).Append("</font>\n");
            body.Append("Profession hire contract. Body tracks your level (capped 220). Tier twinks gear and nanos.\n");
            body.Append("<font color=").Append(ColorMuted).Append('>')
                .Append(EscText(HireTierBlurb(tier, package))).Append("</font>\n");
            if (present)
                body.Append(SayLink("dismiss " + kind.Key, "Dismiss")).Append('\n');
            else
                body.Append(SayLink("call", "Summon")).Append('\n');

            AppendStanceBlock(body, player, kind);
            AppendBuffBlock(body, player, kind, powerLevel, npc, guestOnly: false);

            body.Append('\n').Append(SayLink("menu", "Back to Mercs")).Append('\n');
            return body.ToString();
        }

        static string HireTierBlurb(Mercs.Tier tier, Mercs.Package package)
        {
            if (package.IsBasic)
                return "Basic hire: gear and nanos at your level.";
            return Mercs.TierName(tier) + " hire: gear QL +" + package.GearQlBonus.ToString(CultureInfo.InvariantCulture)
                + ", power +" + package.PowerLevelBonus.ToString(CultureInfo.InvariantCulture)
                + ", health +" + package.HealthAdd.ToString(CultureInfo.InvariantCulture)
                + ", damage +" + package.DamageAdd.ToString(CultureInfo.InvariantCulture)
                + ", skill +" + package.SkillAdd.ToString(CultureInfo.InvariantCulture) + ".";
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
            AppendBuffBlock(body, player, def.Kind, def.Level, npc, guestOnly: true);
            return body.ToString();
        }

        void AppendStanceBlock(StringBuilder body, Player player, Mercs.Kind kind)
        {
            string key = kind.Key;
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
                body.Append("    ").Append(SayLink("preset keeper aura", "Aura list")).Append('\n');
                foreach (string type in _unlocks.Nanos.KeeperAuraTypes())
                    body.Append("    ").Append(SayLink("preset keeper aura " + type, "Aura: " + type)).Append('\n');
            }

            if (string.Equals(key, "nanotechnician", StringComparison.OrdinalIgnoreCase))
            {
                EnsureHeader();
                string mode = _unlocks.GetNtMode(player).ToUpperInvariant();
                body.Append("    <font color=").Append(ColorMuted).Append(">NT nuke mode: ")
                    .Append(EscText(mode)).Append("</font>\n");
                body.Append("    ").Append(SayLink("preset nt aoe", "AOE")).Append(' ')
                    .Append(SayLink("preset nt st", "Single target")).Append('\n');
            }

            if (string.Equals(key, "trader", StringComparison.OrdinalIgnoreCase))
            {
                EnsureHeader();
                string mode = _unlocks.GetTraderMode(player).ToUpperInvariant();
                body.Append("    <font color=").Append(ColorMuted).Append(">Trader mode: ")
                    .Append(EscText(mode)).Append("</font>\n");
                body.Append("    ").Append(SayLink("preset trader wrangle", "Wrangle")).Append(' ')
                    .Append(SayLink("preset trader umbral", "Umbral")).Append('\n');
            }

            if (string.Equals(key, "metaphysicist", StringComparison.OrdinalIgnoreCase))
            {
                EnsureHeader();
                string mode = _unlocks.GetMpMode(player).ToUpperInvariant();
                body.Append("    <font color=").Append(ColorMuted).Append(">MP mode: ")
                    .Append(EscText(mode)).Append("</font>\n");
                body.Append("    ").Append(SayLink("preset mp composite", "Composite")).Append(' ')
                    .Append(SayLink("preset mp singles", "Singles")).Append('\n');
            }
        }

        void AppendBuffBlock(
            StringBuilder body,
            Player player,
            Mercs.Kind kind,
            int level,
            NpcCharacter? npc,
            bool guestOnly)
        {
            _ = level;
            _ = npc;
            body.Append("\n    <img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n\n");
            body.Append("    <font color=").Append(ColorHeader).Append('>')
                .Append(guestOnly ? "Buffs for you" : "Buffs").Append("</font>\n");

            // Copy that profession's buffbot section (same layout/colors/NCU/Team tags).
            if (!_buffbot.TryFindSection(kind.Title, out BuffSection? section) || section == null)
            {
                body.Append("    <font color=").Append(ColorMuted).Append('>')
                    .Append("No buffbot section for ").Append(EscText(kind.Title))
                    .Append(".</font>\n");
                return;
            }

            int freeNcu = Math.Max(0, player.MaxNcu - player.UsedNcu);
            int listed = 0;
            foreach (BuffLine line in section.Lines)
            {
                _buffbot.TryOffer(line, freeNcu, out BuffOffer offer);
                if (guestOnly && !offer.Available)
                    continue;

                body.Append("    ");
                if (offer.Available)
                {
                    body.Append(SayLink(
                        "cast " + offer.NanoId.ToString(CultureInfo.InvariantCulture),
                        offer.Name));
                }
                else
                {
                    body.Append("<font color=").Append(ColorMuted).Append('>')
                        .Append(EscText(offer.Name))
                        .Append(" (need ")
                        .Append(offer.Ncu.ToString(CultureInfo.InvariantCulture))
                        .Append(" NCU)</font>");
                }

                if (IsTeamBuffName(offer.Name))
                    body.Append("<font color=").Append(ColorTeam).Append(">[Team]</font>");
                if (!string.IsNullOrEmpty(offer.Bonus))
                {
                    body.Append("<font color=").Append(ColorBonus).Append(">[")
                        .Append(EscText(offer.Bonus)).Append("]</font>");
                }

                body.Append("<font color=").Append(ColorNcu).Append(">[NCU:")
                    .Append(offer.Ncu.ToString(CultureInfo.InvariantCulture))
                    .Append("]</font>\n");
                listed++;
            }

            if (listed == 0)
            {
                body.Append("    <font color=").Append(ColorMuted).Append('>')
                    .Append(guestOnly
                        ? "No available buffs for your NCU right now."
                        : "No buffbot lines resolved for this profession.")
                    .Append("</font>\n");
            }
        }

        static bool IsTeamBuffName(string name)
            => name.IndexOf("(Team)", StringComparison.OrdinalIgnoreCase) >= 0
               || name.IndexOf("[Team]", StringComparison.OrdinalIgnoreCase) >= 0;

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
            body.Append('\n').Append(SayLink("menu", "Merc menu")).Append('\n');
            body.Append(SayLink("preset keeper aura", "Keeper aura list")).Append('\n');
            foreach (string type in _unlocks.Nanos.KeeperAuraTypes())
                body.Append(SayLink("preset keeper aura " + type, "Keeper " + type)).Append('\n');
            body.Append(SayLink("preset trader wrangle", "Trader wrangle")).Append('\n');
            body.Append(SayLink("preset trader umbral", "Trader Umbral")).Append('\n');
            body.Append(SayLink("preset mp composite", "MP composite")).Append('\n');
            body.Append(SayLink("preset mp singles", "MP singles")).Append('\n');
            body.Append(SayLink("preset nt aoe", "NT AOE")).Append('\n');
            body.Append(SayLink("preset nt st", "NT ST")).Append('\n');
            return body.ToString();
        }

        string BuildMainHeader(Player player, string filter)
            => BuildMainHeaderStatic(
                filter,
                NestedLink(BuildMacrosInner(player), "Macros"),
                BuildFilterLinks(player, filter));

        /// <summary>
        /// Filter keys as nested trimmed name-list windows (not chatcmd). Bodies stay small so the
        /// repeating main header remains under <see cref="MessageMax"/>; open mercs from the main list.
        /// </summary>
        string BuildFilterLinks(Player player, string activeFilter)
        {
            var links = new StringBuilder();
            foreach (string key in FilterKeys)
            {
                links.Append(' ');
                if (string.Equals(activeFilter, key, StringComparison.OrdinalIgnoreCase))
                {
                    links.Append("<font color=").Append(ColorHeader).Append(">[")
                        .Append(EscText(key)).Append("]</font>");
                }
                else
                    links.Append(NestedLink(BuildFilterPageInner(player, key), key));
            }

            return links.ToString();
        }

        string BuildFilterPageInner(Player player, string filterKey)
        {
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append(">Filter: ")
                .Append(EscText(filterKey)).Append("</font>\n");
            body.Append("Click a name (nested-in-nested detail). Window Back returns here.\n");

            int listed = 0;
            foreach (Mercs.Kind kind in Mercs.Kinds)
            {
                bool contract = PetService.HasContract(player, kind);
                bool here = player.OwnedPets.All.Any(p => p.Pet != null && !p.IsDead
                    && string.Equals(p.Pet.Hash, kind.Hash, StringComparison.OrdinalIgnoreCase));
                if (!contract && !here)
                    continue;
                Mercs.Tier tier = Mercs.GetTier(player, kind);
                if (!PassesHiredFilter(tier, here, filterKey))
                    continue;
                body.Append(NestedLinkDeep(BuildHireDetailInner(player, kind, tier, here), kind.Title));
                body.Append(" <font color=").Append(ColorMuted).Append(">(hire, ")
                    .Append(EscText(Mercs.TierName(tier))).Append(")</font>\n");
                listed++;
            }

            foreach (MercDefinition def in _unlocks.Roster.All)
            {
                if (!PassesAccess(player, def) || !PassesFilter(player, def, filterKey))
                    continue;
                string shown = _unlocks.DisplayNameFor(player, def);
                body.Append(NestedLinkDeep(BuildDetailInner(player, def), shown));
                body.Append(" <font color=").Append(ColorMuted).Append(">(")
                    .Append(EscText(ContentTierLabel(def)))
                    .Append(" L").Append(def.Level.ToString(CultureInfo.InvariantCulture))
                    .Append(")</font>\n");
                listed++;
            }

            if (listed == 0)
            {
                body.Append("<font color=").Append(ColorMuted).Append('>')
                    .Append("No hired or unlocked mercs match this filter.</font>\n");
            }

            return body.ToString();
        }

        /// <summary>
        /// Outer linktest page: one NestedLink into the labelled A–E + nested-in-nested panel.
        /// </summary>
        static string BuildLinkTestOuterInner()
        {
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append(">Merc linktest</font>\n");
            body.Append("Opens one nested page of chatcmd / nested-text variants. All harmless (.merc menu).\n");
            body.Append(NestedLink(BuildLinkTestNestedInner(), "Open variants")).Append('\n');
            return body.ToString();
        }

        /// <summary>
        /// Nested page body for <c>.merc linktest</c>. No raw quotes. Production uses variant C.
        /// </summary>
        static string BuildLinkTestNestedInner()
        {
            const string cmd = "chatcmd:///say .merc menu";
            string deepBody =
                "<font color=" + ColorMuted + ">\n"
                + "<font color=" + ColorHeader + ">Nested-in-nested sample</font>\n"
                + "If this opens formatted, filter→merc NestedLinkDeep works.\n"
                + "<a href=&#39;" + cmd + "&#39;>C inside deep: menu</a>\n";

            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append(">Link variants</font>\n");
            body.Append("Click each. Report which ones are real links (not raw code).\n\n");

            // A) entity-escaped double quotes, tags raw (Nadybot/Tyrbot blob: " → &quot; in content)
            body.Append("A) href=&amp;quot;chatcmd...&amp;quot; (entity double): ");
            body.Append("<a href=&quot;").Append(cmd).Append("&quot;>menu</a>\n");

            // B) backslash-escaped quotes need a raw " which terminates outer href="text://".
            // Cannot ship as a live link inside this nested body; label only.
            body.Append("B) href=\\&quot;...\\&quot; (backslash): NOT LIVE — raw quote would ");
            body.Append("break outer href=&quot;text://&quot;. Skipped.\n");

            // C) InfoGuide embedding of Nadybot/Tyrbot makeChatcmd — PRODUCTION
            body.Append("C) href=&amp;#39;chatcmd...&amp;#39; (entity single, PRODUCTION): ");
            body.Append("<a href=&#39;").Append(cmd).Append("&#39;>menu</a>\n");

            // D) unquoted with %20 for spaces
            body.Append("D) href=chatcmd...%20... (percent-encoded spaces): ");
            body.Append("<a href=chatcmd:///say%20.merc%20menu>menu</a>\n");

            // E) Cite bot source: makeChatcmd emits href='chatcmd://...' (Nadybot Text.php:229,
            // Tyrbot text.py make_chatcmd). Content apostrophes → &#39;. That raw form is for
            // blobs inside href="text://". Inside our href='text://' nest, E adapts to C.
            body.Append("E) Nadybot Text.php makeChatcmd / Tyrbot text.py make_chatcmd: ");
            body.Append("href=&#39;chatcmd://...&#39; with cmd apostrophes as &amp;#39;. ");
            body.Append("Raw form needs nest adaptation → same bytes as C. ");
            body.Append("<a href=&#39;").Append(cmd).Append("&#39;>menu (E=C)</a>\n");

            body.Append("\n<font color=").Append(ColorHeader).Append(">Nested-in-nested</font>\n");
            body.Append(NestedLinkDeep(deepBody, "Open deep page")).Append('\n');
            return body.ToString();
        }

        static string BuildMainHeaderStatic(string filter, string macrosLink, string filterLinks)
        {
            _ = filter;
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append(">Mercs</font>\n");
            body.Append("Hired profession contracts and unlocked dungeon mercs. Click a name for detail.\n");
            body.Append("Filter:").Append(filterLinks);
            body.Append('\n').Append(macrosLink).Append('\n');
            return body.ToString();
        }

        /// <summary>
        /// Dungeon roster row is accessible when unlocked/dungeon_only, or currently summoned.
        /// Profession hire access is listed separately via <see cref="AppendHiredContractsSection"/>.
        /// </summary>
        bool PassesAccess(Player player, MercDefinition def)
            => _unlocks.GetState(player, def) != MercUnlockState.Locked
               || _unlocks.IsSummoned(player, def);

        bool AppendHiredContractsSection(StringBuilder body, Player player, string filter)
        {
            var rows = new List<string>();
            foreach (Mercs.Kind kind in Mercs.Kinds)
            {
                bool contract = PetService.HasContract(player, kind);
                bool here = player.OwnedPets.All.Any(p => p.Pet != null && !p.IsDead
                    && string.Equals(p.Pet.Hash, kind.Hash, StringComparison.OrdinalIgnoreCase));
                if (!contract && !here)
                    continue;

                Mercs.Tier tier = Mercs.GetTier(player, kind);
                if (!PassesHiredFilter(tier, here, filter))
                    continue;

                string tierName = Mercs.TierName(tier);
                var row = new StringBuilder();
                row.Append(NestedLink(BuildHireDetailInner(player, kind, tier, here), kind.Title));
                row.Append(" <font color=").Append(ColorMuted).Append('>')
                    .Append(EscText(tierName))
                    .Append(" - hire</font>");
                if (here)
                {
                    row.Append(" <font color=").Append(ColorTeam).Append(">[Summoned]</font>");
                    row.Append(' ').Append(SayLink("dismiss " + kind.Key, "dismiss"));
                }
                else
                    row.Append(' ').Append(SayLink("call", "summon"));
                row.Append('\n');
                rows.Add(row.ToString());
            }

            if (rows.Count == 0)
                return false;

            body.Append("\n    <img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n\n");
            body.Append("    <img src=tdb://id:GFX_GUI_FRIENDLIST_STATUS_GREEN> ");
            body.Append("<font color=").Append(ColorHeader).Append(">Hired contracts</font>\n");
            foreach (string row in rows)
                body.Append("    ").Append(row);
            return true;
        }

        static bool PassesHiredFilter(Mercs.Tier tier, bool summoned, string filter)
        {
            if (string.Equals(filter, FilterAll, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(filter, FilterSummoned, StringComparison.OrdinalIgnoreCase))
                return summoned;
            if (string.Equals(filter, FilterBasic, StringComparison.OrdinalIgnoreCase))
                return tier == Mercs.Tier.Basic;
            if (string.Equals(filter, FilterElite, StringComparison.OrdinalIgnoreCase))
                return tier == Mercs.Tier.Elite;
            if (string.Equals(filter, FilterChampion, StringComparison.OrdinalIgnoreCase))
                return tier == Mercs.Tier.Champion;
            if (string.Equals(filter, FilterTwink, StringComparison.OrdinalIgnoreCase))
                return tier == Mercs.Tier.Twink;
            return true;
        }

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

            if (key.StartsWith("trader wrangle", StringComparison.Ordinal) || key == "trader wrangle"
                || key.StartsWith("trader umbral", StringComparison.Ordinal) || key == "trader umbral"
                || key == "trader")
            {
                // Pick-one list: never auto-cast a "best" or every wrangle.
                OpenTraderWranglePicker(player);
                return;
            }

            if (key.StartsWith("mp composite", StringComparison.Ordinal) || key == "mp composite")
            {
                OpenMpOfferPicker(player, MercUnlockService.MpModeComposite);
                return;
            }

            if (key.StartsWith("mp singles", StringComparison.Ordinal) || key == "mp singles")
            {
                OpenMpOfferPicker(player, MercUnlockService.MpModeSingles);
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
            Mercs.Kind? kind = Mercs.Find("keeper");
            NpcCharacter? npc = kind != null ? _unlocks.FindProfessionNpc(player, kind) : null;
            int level = kind != null
                ? _unlocks.ProfessionNanoLevel(player, kind)
                : 149;
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append(">Keeper auras</font>\n");
            body.Append("From items.dat. One aura per NanoStrain. Highest rank the Keeper level allows.\n");
            foreach (MercProfessionNanos.KeeperAuraOffer offer in _unlocks.Nanos.KeeperAurasFor(level, npc))
            {
                if (offer.Available)
                {
                    body.Append(SayLink("preset keeper aura " + offer.Type, offer.Type))
                        .Append(" <font color=").Append(ColorMuted).Append('>')
                        .Append(EscText(offer.Name + " L" + offer.RequiredLevel.ToString(CultureInfo.InvariantCulture)))
                        .Append("</font>\n");
                }
                else if (offer.RequiredLevel > level)
                {
                    body.Append("<font color=").Append(ColorMuted).Append('>')
                        .Append(EscText(offer.Type + ": " + offer.Name
                            + " (needs L" + offer.RequiredLevel.ToString(CultureInfo.InvariantCulture) + ")"))
                        .Append("</font>\n");
                }
                else
                {
                    body.Append("<font color=").Append(ColorMuted).Append('>')
                        .Append(EscText(offer.Type + ": " + offer.Name))
                        .Append("</font>\n");
                }
            }

            body.Append('\n').Append(SayLink("menu", "Back")).Append('\n');
            SendBlob(player, BuildOuter(body.ToString(), "Keeper auras"));
        }

        /// <summary>
        /// Pick-one cast list for MP composite or singles families. Never casts every line at once.
        /// Fresh outer tell (detail is already one nested level deep).
        /// </summary>
        void OpenMpOfferPicker(Player player, string modeText)
        {
            bool singles = modeText.Trim().Equals(MercUnlockService.MpModeSingles, StringComparison.OrdinalIgnoreCase);
            string mode = singles ? MercUnlockService.MpModeSingles : MercUnlockService.MpModeComposite;
            Mercs.Kind? kind = Mercs.Find("metaphysicist");
            NpcCharacter? npc = kind != null ? _unlocks.FindProfessionNpc(player, kind) : null;
            int level = kind != null ? _unlocks.ProfessionNanoLevel(player, kind) : Math.Max(1, player.Stats.GetOrOne(CharacterStat.Level));
            IReadOnlyList<MercProfessionNanos.MercBuffOffer> offers = singles
                ? _unlocks.Nanos.MpSinglesFor(level, npc)
                : _unlocks.Nanos.MpCompositesFor(level, npc);

            // Remember preferred mode for merc upload / name tag; casting is always one nano.
            _unlocks.SetMpModeOnly(player, mode);

            string title = singles ? "MP singles" : "MP composites";
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append('>').Append(title).Append("</font>\n");
            body.Append("Click one nano to cast it. Each line is a separate family - pick what you want.\n");
            AppendCastOfferList(body, offers, level);
            body.Append('\n').Append(SayLink("menu", "Back")).Append('\n');
            SendBlob(player, BuildOuter(body.ToString(), title));
        }

        /// <summary>Pick-one cast list for Skill / Team / Umbral wrangles.</summary>
        void OpenTraderWranglePicker(Player player)
        {
            Mercs.Kind? kind = Mercs.Find("trader");
            NpcCharacter? npc = kind != null ? _unlocks.FindProfessionNpc(player, kind) : null;
            int level = kind != null ? _unlocks.ProfessionNanoLevel(player, kind) : Math.Max(1, player.Stats.GetOrOne(CharacterStat.Level));
            IReadOnlyList<MercProfessionNanos.MercBuffOffer> offers = _unlocks.Nanos.TraderWrangleChoices(level, npc);

            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append(">Trader wrangles</font>\n");
            body.Append("Click one wrangle to cast it. Skill, Team, and Umbral are separate picks.\n");
            AppendCastOfferList(body, offers, level);
            body.Append('\n').Append(SayLink("menu", "Back")).Append('\n');
            SendBlob(player, BuildOuter(body.ToString(), "Trader wrangles"));
        }

        void AppendCastOfferList(
            StringBuilder body,
            IReadOnlyList<MercProfessionNanos.MercBuffOffer> offers,
            int level)
        {
            if (offers.Count == 0)
            {
                body.Append("<font color=").Append(ColorMuted).Append('>')
                    .Append("No matching nanos in items.dat for this merc level.</font>\n");
                return;
            }

            foreach (MercProfessionNanos.MercBuffOffer offer in offers)
            {
                if (offer.Available)
                {
                    body.Append(SayLink(
                        "cast " + offer.NanoId.ToString(CultureInfo.InvariantCulture),
                        offer.Name));
                }
                else
                {
                    body.Append("<font color=").Append(ColorMuted).Append('>')
                        .Append(EscText(offer.Name));
                    if (offer.RequiredLevel > level)
                    {
                        body.Append(" - merc needs level ")
                            .Append(offer.RequiredLevel.ToString(CultureInfo.InvariantCulture));
                    }

                    body.Append("</font>");
                }

                body.Append(" <font color=").Append(ColorNcu).Append(">[NCU: ")
                    .Append(offer.Ncu.ToString(CultureInfo.InvariantCulture)).Append("]</font>\n");
            }
        }

        public static string BuildOuter(string inner, string label = "Merc Menu")
        {
            if (!inner.EndsWith("</font>\n", StringComparison.Ordinal)
                && !inner.EndsWith("</font>", StringComparison.Ordinal))
                inner += "</font>";
            // Outer tell: double-quoted text://, raw tags (Nadybot/Tyrbot makeBlob; GetStatsAomlBuilder).
            return "<font color=" + ColorMuted + "><a href=\"text://" + inner + "\">"
                + EscText(label) + "</a></font>";
        }

        /// <summary>
        /// Nested <c>text://</c> inside the outer tell. Outer is <c>href="text://"</c>; nested is
        /// <c>href='text://'</c> with <strong>raw</strong> AOML (no tag-entity escape). Body must
        /// not contain raw quotes; use <see cref="SayLink"/> / <see cref="NestedLinkDeep"/>.
        /// </summary>
        public static string NestedLink(string body, string label)
        {
            ArgumentNullException.ThrowIfNull(body);
            ArgumentNullException.ThrowIfNull(label);
            EnsureNoRawQuotes(body, "NestedLink");
            return "<a href='text://" + body + "'>" + EscText(label) + "</a>";
        }

        /// <summary>
        /// Second-level nested <c>text://</c> for use inside a <see cref="NestedLink"/> body
        /// (filter → merc detail). Uses <c>href=&amp;#39;text://...&amp;#39;</c> so it does not
        /// terminate the parent single-quoted nested href (InfoGuide quote-entity pattern).
        /// </summary>
        public static string NestedLinkDeep(string body, string label)
        {
            ArgumentNullException.ThrowIfNull(body);
            ArgumentNullException.ThrowIfNull(label);
            EnsureNoRawQuotes(body, "NestedLinkDeep");
            return "<a href=&#39;text://" + body + "&#39;>" + EscText(label) + "</a>";
        }

        static void EnsureNoRawQuotes(string body, string where)
        {
            if (body.IndexOf('\'') >= 0 || body.IndexOf('"') >= 0)
            {
                throw new InvalidOperationException(
                    where + " body contained a raw quote. Use &#39;/&quot; entities or SayLink.");
            }
        }

        static int EstimateWrappedLength(int innerLength) => innerLength + 48;

        /// <summary>
        /// Production chatcmd action link (variant C). Nadybot/Tyrbot <c>makeChatcmd</c> emits
        /// <c>href='chatcmd://...'</c>; InfoGuide embeds that inside nested pages as
        /// <c>href=&amp;#39;chatcmd://...&amp;#39;</c> so raw quotes never appear in the nested
        /// <c>text://</c> attribute. Do not use for opening windows — use <see cref="NestedLink"/>.
        /// </summary>
        static string SayLink(string command, string label)
            => "<a href=&#39;chatcmd:///say .merc " + command + "&#39;>" + EscText(label) + "</a>";

        /// <summary>
        /// Cast a buffbot-listed nano from the player's summoned merc onto the player (or guest).
        /// Merc is the caster so TeamCastNano / other-target rules land on the recipient.
        /// </summary>
        void CastMercBuff(Player player, int nanoId)
        {
            if (!_buffbot.Contains(nanoId) && _unlocks.Nanos.TryGetTemplate(nanoId) == null)
            {
                Say(player, "That nano is not on the merc buff list.");
                _logger.Warn(string.Format(
                    CultureInfo.InvariantCulture,
                    "Merc cast refused: unknown nanoId={0} player={1}",
                    nanoId,
                    player.Identity.Instance));
                return;
            }

            if (!TryResolveMercCaster(player, nanoId, out NpcCharacter? caster, out Player? recipient, out string reason)
                || caster == null
                || recipient == null)
            {
                Say(player, reason);
                _logger.Warn(string.Format(
                    CultureInfo.InvariantCulture,
                    "Merc cast failed resolve nanoId={0} player={1}: {2}",
                    nanoId,
                    player.Identity.Instance,
                    reason));
                return;
            }

            Playfield? playfield = caster.Playfield;
            if (playfield == null
                || playfield.GetService<IItemBuilder>() is not IItemBuilder items
                || playfield.GetService<IInventoryRepository>() is not IInventoryRepository inventory)
            {
                Say(player, "Mercenaries are not available here.");
                _logger.Warn(string.Format(
                    CultureInfo.InvariantCulture,
                    "Merc cast missing playfield services nanoId={0} player={1}",
                    nanoId,
                    player.Identity.Instance));
                return;
            }

            DateTime now = DateTime.UtcNow;
            bool ok = NanoRuntime.TryApplyImmediate(caster, recipient, nanoId, items, inventory, now);
            if (!ok)
            {
                string detail = string.Format(
                    CultureInfo.InvariantCulture,
                    "Merc {0} failed to cast nano {1} on {2}.",
                    caster.Name ?? "merc",
                    nanoId,
                    recipient.Name ?? "target");
                Say(player, detail);
                _logger.Warn(detail + " casterId=" + caster.Identity.Instance.ToString(CultureInfo.InvariantCulture)
                    + " recipientId=" + recipient.Identity.Instance.ToString(CultureInfo.InvariantCulture));
                return;
            }

            ItemTemplate? template = _unlocks.Nanos.TryGetTemplate(nanoId);
            string name = template?.Name ?? ("nano " + nanoId.ToString(CultureInfo.InvariantCulture));
            int ncu = template != null ? NanoSpell.From(template).NcuCost : 0;
            Say(player, string.Format(
                CultureInfo.InvariantCulture,
                "{0} cast {1}. NCU {2}.",
                caster.Name ?? "Merc",
                name,
                ncu));
            _logger.Info(string.Format(
                CultureInfo.InvariantCulture,
                "Merc cast ok nanoId={0} name={1} caster={2} recipient={3}",
                nanoId,
                name,
                caster.Identity.Instance,
                recipient.Identity.Instance));
        }

        bool TryResolveMercCaster(
            Player clicker,
            int nanoId,
            out NpcCharacter? caster,
            out Player? recipient,
            out string reason)
        {
            caster = null;
            recipient = clicker;
            reason = "Summon your merc first.";

            Mercs.Kind? kind = KindForBuffNano(nanoId);
            if (kind != null)
            {
                NpcCharacter? own = _unlocks.FindProfessionNpc(clicker, kind);
                if (own != null)
                {
                    caster = own;
                    recipient = clicker;
                    reason = string.Empty;
                    return true;
                }
            }

            // Guest: ask a nearby summoned merc of any matching profession.
            foreach (MercDefinition def in _unlocks.Roster.All)
            {
                if (kind != null && !string.Equals(def.Kind.Key, kind.Key, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!TryFindSummonedNearby(clicker, def, out Player? owner, out NpcCharacter? npc)
                    || owner == null
                    || npc == null)
                    continue;
                if (ReferenceEquals(owner, clicker))
                {
                    caster = npc;
                    recipient = clicker;
                    reason = string.Empty;
                    return true;
                }

                if (!ReferenceEquals(clicker.Playfield, npc.Playfield)
                    || clicker.Distance3D(npc) > GuestTellRangeMeters)
                {
                    reason = "Move closer to " + (npc.Name ?? def.DisplayName) + " to request buffs.";
                    continue;
                }

                caster = npc;
                recipient = clicker;
                reason = string.Empty;
                return true;
            }

            if (kind != null)
                reason = "Summon your " + kind.Title + " merc first.";
            return false;
        }

        Mercs.Kind? KindForBuffNano(int nanoId)
        {
            if (_buffbot.TryGetLine(nanoId, out BuffLine line))
            {
                foreach (BuffSection section in _buffbot.Sections)
                {
                    foreach (BuffLine candidate in section.Lines)
                    {
                        if (!ReferenceEquals(candidate, line))
                            continue;
                        Mercs.Kind? match = Mercs.Find(section.Name);
                        if (match != null)
                            return match;
                    }
                }
            }

            // Fallback: first profession ToUse requirement on the template.
            ItemTemplate? template = _unlocks.Nanos.TryGetTemplate(nanoId);
            if (template == null)
                return null;
            foreach (ItemAction action in template.Actions)
            {
                if (action.ActionType != (int)AORebirth.Enums.ActionType.ToUse)
                    continue;
                foreach (ItemRequirement requirement in action.Requirements)
                {
                    if (requirement.StatNumber is not (
                        (int)CharacterStat.Profession or (int)CharacterStat.VisualProfession))
                        continue;
                    foreach (Mercs.Kind kind in Mercs.Kinds)
                    {
                        if (kind.Profession == requirement.Value)
                            return kind;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Plain text for AOML labels. No HTML entities (nested text:// shows them literally);
        /// strip quotes so attribute delimiters stay intact.
        /// </summary>
        public static string EscText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace("\"", string.Empty, StringComparison.Ordinal)
                .Replace("'", string.Empty, StringComparison.Ordinal);
        }

        /// <summary>True when every character is plain ASCII (0x00-0x7F).</summary>
        public static bool IsAsciiOnly(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;
            foreach (char c in text)
            {
                if (c > 0x7F)
                    return false;
            }

            return true;
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
