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
    /// Flat merc menu matching Buffbot: one top-level <c>href="text://…"</c> tell with
    /// <c>href='chatcmd:///say .merc …'</c> action links. No nested <c>text://</c> (client shows
    /// links inside nested pages as raw code — Randy a6975ebd). Overflow splits into extra tells
    /// by whole merc sections, same rule as BuffbotService.
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

        /// <summary>Set by the last <see cref="BuildMainMessages"/> call (blob size reports).</summary>
        public static int LastBuiltMessageCount { get; private set; }

        /// <summary>Longest outer tell from the last <see cref="BuildMainMessages"/> call.</summary>
        public static int LastBuiltMaxMessageLength { get; private set; }

        public void HandleTell(Player player, string botName, string message)
        {
            ArgumentNullException.ThrowIfNull(player);
            string trimmed = (message ?? string.Empty).Trim();
            MercDefinition? named = _unlocks.Roster.Find(botName);

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
                // Typed/chat-link only — no filter links in the flat window (would be chat links).
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
                    Say(player, "Unknown merc. Use .merc menu.");
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
                    Say(player, "Unknown hire. Use .merc menu.");
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
                OpenMenu(player);
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
                    Say(player, "Summon a dungeon merc first, or use .merc menu.");
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
            var body = new StringBuilder(BuildMainHeader());
            AppendDungeonMercSection(body, player, def);
            SendBlob(player, BuildOuter(body.ToString()));
        }

        public void OpenHireDetail(Player player, Mercs.Kind kind)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(kind);
            Mercs.Tier tier = Mercs.GetTier(player, kind);
            bool here = player.OwnedPets.All.Any(p => p.Pet != null && !p.IsDead
                && string.Equals(p.Pet.Hash, kind.Hash, StringComparison.OrdinalIgnoreCase));
            var body = new StringBuilder(BuildMainHeader());
            AppendHiredMercSection(body, player, kind, tier, here);
            SendBlob(player, BuildOuter(body.ToString()));
        }

        /// <summary>Flat tell payloads: hired contracts first, then dungeon unlocks; split like buffbot.</summary>
        public IReadOnlyList<string> BuildMainMessages(Player player, string filterKey)
        {
            ArgumentNullException.ThrowIfNull(player);
            string filter = string.IsNullOrWhiteSpace(filterKey) ? FilterAll : filterKey.Trim();
            var sections = new List<string>();

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
                var hired = new StringBuilder();
                AppendHiredMercSection(hired, player, kind, tier, here);
                sections.Add(hired.ToString());
            }

            foreach (MercDefinition def in _unlocks.Roster.All)
            {
                if (!PassesAccess(player, def) || !PassesFilter(player, def, filter))
                    continue;
                var dungeon = new StringBuilder();
                AppendDungeonMercSection(dungeon, player, def);
                sections.Add(dungeon.ToString());
            }

            string header = BuildMainHeader();
            if (sections.Count == 0)
            {
                string empty = BuildOuter(
                    header
                    + "\n\n<font color=" + ColorMuted + ">No hired or unlocked mercs.</font>\n");
                LastBuiltMessageCount = 1;
                LastBuiltMaxMessageLength = empty.Length;
                return [empty];
            }

            var messages = new List<string>();
            var current = new StringBuilder(header);
            foreach (string body in sections)
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

            LastBuiltMessageCount = messages.Count;
            LastBuiltMaxMessageLength = 0;
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].Length > LastBuiltMaxMessageLength)
                    LastBuiltMaxMessageLength = messages[i].Length;
            }

            return messages;
        }

        /// <summary>Pure render helpers for unit tests / harness (no live player).</summary>
        public static class Render
        {
            public static string MainHeader() => BuildMainHeader();

            public static string FlatMenuSample()
            {
                var body = new StringBuilder(BuildMainHeader());
                body.Append(HiredSectionSample(present: true));
                body.Append(DungeonSectionSample(summoned: true));
                return BuildOuter(body.ToString());
            }

            public static string HiredSectionSample(bool present)
            {
                var body = new StringBuilder();
                body.Append("\n<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n");
                body.Append("<img src=tdb://id:GFX_GUI_FRIENDLIST_STATUS_GREEN>");
                body.Append("<img src=tdb://id:GFX_GUI_ICON_PROFESSION_1>");
                body.Append("<font color=").Append(ColorHeader).Append(">Soldier - Basic (hire)</font>\n");
                if (present)
                {
                    body.Append("<font color=").Append(ColorTeam).Append(">[Summoned]</font> ");
                    body.Append(SayLink("dismiss soldier", "Dismiss")).Append('\n');
                }
                else
                    body.Append(SayLink("call", "Summon")).Append('\n');
                body.Append("<font color=").Append(ColorHeader).Append(">Buffs</font>\n");
                body.Append(SayLink("cast 1001", "Full Automatic Targeting"));
                body.Append("<font color=").Append(ColorNcu).Append(">[NCU:4]</font>\n");
                return body.ToString();
            }

            public static string DungeonSectionSample(bool summoned)
            {
                var body = new StringBuilder();
                body.Append("\n<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n");
                body.Append("<img src=tdb://id:GFX_GUI_FRIENDLIST_STATUS_GREEN>");
                body.Append("<font color=").Append(ColorHeader)
                    .Append(">Brock Anvil Malone - Twink L24</font>\n");
                if (summoned)
                {
                    body.Append("<font color=").Append(ColorTeam).Append(">[Summoned]</font> ");
                    body.Append(SayLink("dismiss sample_id", "Dismiss")).Append('\n');
                }
                else
                    body.Append(SayLink("summon sample_id", "Summon")).Append('\n');
                body.Append("<font color=").Append(ColorHeader).Append(">Stance</font>\n");
                body.Append(SayLink("preset nt aoe", "AOE")).Append(' ')
                    .Append(SayLink("preset nt st", "Single target")).Append('\n');
                body.Append("<font color=").Append(ColorHeader).Append(">Buffs</font>\n");
                body.Append(SayLink("cast 1002", "Team Reflective Field"));
                body.Append("<font color=").Append(ColorTeam).Append(">[Team]</font>");
                body.Append("<font color=").Append(ColorNcu).Append(">[NCU:4]</font>\n");
                body.Append("<font color=").Append(ColorHeader).Append(">MP composites / singles</font>\n");
                body.Append(SayLink("cast 2001", "Composite Attribute")).Append('\n');
                body.Append("<font color=").Append(ColorHeader).Append(">Trader wrangles</font>\n");
                body.Append(SayLink("cast 3001", "Team Skill Wrangler")).Append('\n');
                return body.ToString();
            }

            public static string GuestBuffsSample(string mercName)
            {
                var body = new StringBuilder();
                body.Append("<font color=").Append(ColorMuted).Append(">\n");
                body.Append("<font color=").Append(ColorHeader).Append('>')
                    .Append(EscText(mercName)).Append(" can buff you</font>\n");
                body.Append(SayLink("cast 1001", "Full Automatic Targeting"));
                body.Append("<font color=").Append(ColorNcu).Append(">[NCU:4]</font>\n");
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
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append('>')
                .Append(EscText(string.IsNullOrWhiteSpace(npc.Name) ? def.DisplayName : npc.Name!))
                .Append(" can buff you</font>\n");
            body.Append("<font color=").Append(ColorMuted).Append('>')
                .Append("Owner-only controls are hidden.</font>\n");
            AppendBuffBlock(body, player, def.Kind, def.Level, npc, guestOnly: true);
            SendBlob(player, BuildOuter(body.ToString(), "Merc Buffs"));
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

        static string BuildMainHeader()
        {
            var body = new StringBuilder();
            body.Append("<font color=").Append(ColorMuted).Append(">\n");
            body.Append("<font color=").Append(ColorHeader).Append(">Mercs</font>\n");
            body.Append("Hired contracts and unlocked dungeon mercs. All links are in this window.\n");
            body.Append(SayLink("call", "Call hired")).Append(' ')
                .Append(SayLink("dismiss all", "Dismiss all")).Append('\n');
            return body.ToString();
        }

        void AppendHiredMercSection(
            StringBuilder body,
            Player player,
            Mercs.Kind kind,
            Mercs.Tier tier,
            bool present)
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
            body.Append("\n<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n");
            body.Append("<img src=tdb://id:GFX_GUI_FRIENDLIST_STATUS_GREEN>");
            AppendProfessionIcon(body, kind);
            body.Append("<font color=").Append(ColorHeader).Append('>')
                .Append(EscText(kind.Title)).Append(" - ").Append(EscText(tierName))
                .Append(" (hire)</font>\n");
            body.Append("<font color=").Append(ColorMuted).Append('>')
                .Append(EscText(HireTierBlurb(tier, package))).Append("</font>\n");
            if (present)
            {
                body.Append("<font color=").Append(ColorTeam).Append(">[Summoned]</font> ");
                body.Append(SayLink("dismiss " + kind.Key, "Dismiss")).Append('\n');
            }
            else
                body.Append(SayLink("call", "Summon")).Append('\n');

            AppendStanceAndPickOne(body, player, kind, powerLevel, npc);
            AppendBuffBlock(body, player, kind, powerLevel, npc, guestOnly: false);
        }

        void AppendDungeonMercSection(StringBuilder body, Player player, MercDefinition def)
        {
            string shown = _unlocks.DisplayNameFor(player, def);
            string tier = ContentTierLabel(def);
            bool summoned = _unlocks.IsSummoned(player, def);
            MercUnlockState state = _unlocks.GetState(player, def);
            NpcCharacter? npc = summoned ? _unlocks.FindSummonedNpc(player, def) : null;

            body.Append("\n<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>\n");
            body.Append("<img src=tdb://id:GFX_GUI_FRIENDLIST_STATUS_GREEN>");
            AppendProfessionIcon(body, def.Kind);
            body.Append("<font color=").Append(ColorHeader).Append('>')
                .Append(EscText(shown)).Append(" - ").Append(EscText(tier))
                .Append(" L").Append(def.Level.ToString(CultureInfo.InvariantCulture))
                .Append("</font>\n");
            body.Append("<font color=").Append(ColorMuted).Append('>')
                .Append(EscText(BuildDescription(def))).Append("</font>\n");

            if (summoned)
            {
                body.Append("<font color=").Append(ColorTeam).Append(">[Summoned]</font> ");
                body.Append(SayLink("dismiss " + def.Id, "Dismiss")).Append('\n');
            }
            else if (_unlocks.CanSummonHere(player, def, state, out string reason))
                body.Append(SayLink("summon " + def.Id, "Summon")).Append('\n');
            else
                body.Append("<font color=").Append(ColorMuted).Append('>')
                    .Append(EscText(reason)).Append("</font>\n");

            AppendStanceAndPickOne(body, player, def.Kind, def.Level, npc);
            AppendBuffBlock(body, player, def.Kind, def.Level, npc, guestOnly: false);
        }

        static void AppendProfessionIcon(StringBuilder body, Mercs.Kind kind)
        {
            if (ProfessionIcons.TryGetValue(kind.Title, out int icon)
                || ProfessionIcons.TryGetValue(kind.Key, out icon))
            {
                body.Append("<img src=tdb://id:GFX_GUI_ICON_PROFESSION_")
                    .Append(icon.ToString(CultureInfo.InvariantCulture))
                    .Append('>');
            }
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

        void AppendStanceAndPickOne(
            StringBuilder body,
            Player player,
            Mercs.Kind kind,
            int level,
            NpcCharacter? npc)
        {
            string key = kind.Key;

            if (string.Equals(key, "keeper", StringComparison.OrdinalIgnoreCase))
            {
                body.Append("<font color=").Append(ColorHeader).Append(">Keeper auras</font>\n");
                foreach (MercProfessionNanos.KeeperAuraOffer offer in _unlocks.Nanos.KeeperAurasFor(level, npc))
                {
                    if (offer.Available)
                    {
                        body.Append(SayLink("preset keeper aura " + offer.Type, offer.Type))
                            .Append(" <font color=").Append(ColorMuted).Append('>')
                            .Append(EscText(offer.Name)).Append("</font>\n");
                    }
                    else
                    {
                        body.Append("<font color=").Append(ColorMuted).Append('>')
                            .Append(EscText(offer.Type + ": " + offer.Name)).Append("</font>\n");
                    }
                }
            }

            if (string.Equals(key, "nanotechnician", StringComparison.OrdinalIgnoreCase))
            {
                string mode = _unlocks.GetNtMode(player).ToUpperInvariant();
                body.Append("<font color=").Append(ColorHeader).Append(">NT nuke mode: ")
                    .Append(EscText(mode)).Append("</font>\n");
                body.Append(SayLink("preset nt aoe", "AOE")).Append(' ')
                    .Append(SayLink("preset nt st", "Single target")).Append('\n');
            }

            if (string.Equals(key, "metaphysicist", StringComparison.OrdinalIgnoreCase))
            {
                body.Append("<font color=").Append(ColorHeader).Append(">MP composites</font>\n");
                AppendCastOfferList(body, _unlocks.Nanos.MpCompositesFor(level, npc), level);
                body.Append("<font color=").Append(ColorHeader).Append(">MP singles</font>\n");
                AppendCastOfferList(body, _unlocks.Nanos.MpSinglesFor(level, npc), level);
            }

            if (string.Equals(key, "trader", StringComparison.OrdinalIgnoreCase))
            {
                body.Append("<font color=").Append(ColorHeader).Append(">Trader wrangles</font>\n");
                AppendCastOfferList(body, _unlocks.Nanos.TraderWrangleChoices(level, npc), level);
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
            body.Append("<font color=").Append(ColorHeader).Append('>')
                .Append(guestOnly ? "Buffs for you" : "Buffs").Append("</font>\n");

            if (!_buffbot.TryFindSection(kind.Title, out BuffSection? section) || section == null)
            {
                body.Append("<font color=").Append(ColorMuted).Append('>')
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
                body.Append("<font color=").Append(ColorMuted).Append('>')
                    .Append(guestOnly
                        ? "No available buffs for your NCU right now."
                        : "No buffbot lines resolved for this profession.")
                    .Append("</font>\n");
            }
        }

        static bool IsTeamBuffName(string name)
            => name.IndexOf("(Team)", StringComparison.OrdinalIgnoreCase) >= 0
               || name.IndexOf("[Team]", StringComparison.OrdinalIgnoreCase) >= 0;

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

        bool PassesAccess(Player player, MercDefinition def)
            => _unlocks.GetState(player, def) != MercUnlockState.Locked
               || _unlocks.IsSummoned(player, def);

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
            return "Basic";
        }

        static string BuildDescription(MercDefinition def)
            => "Unlock from " + def.DungeonName + " (" + def.BossName + "). Role: " + def.Role
               + ". Merc body level " + def.Level.ToString(CultureInfo.InvariantCulture) + ".";

        void RunPreset(Player player, string preset)
        {
            string key = (preset ?? string.Empty).Trim().ToLowerInvariant();
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

            if (key == "keeper aura" || key == "keeper" || key == "keeper aura list"
                || key.StartsWith("trader", StringComparison.Ordinal)
                || key.StartsWith("mp ", StringComparison.Ordinal))
            {
                // Pick-one lists live in the flat merc window — no nested/picker windows.
                Say(player, "Use .merc menu — aura/wrangle/MP lines are listed under that merc.");
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

            Say(player, "Unknown preset. See .merc menu.");
        }

        public static string BuildOuter(string inner, string label = "Merc Menu")
        {
            if (!inner.EndsWith("</font>\n", StringComparison.Ordinal)
                && !inner.EndsWith("</font>", StringComparison.Ordinal))
                inner += "</font>";
            // Buffbot-identical wrapper: double-quoted text://, single-quoted chatcmd inside.
            return "<font color='" + ColorMuted + "'><a href=\"text://" + inner + "\">"
                + EscText(label) + "</a></font>";
        }

        static int EstimateWrappedLength(int innerLength) => innerLength + 48;

        /// <summary>Buffbot-style action link inside the outer <c>href="text://"</c>.</summary>
        static string SayLink(string command, string label)
            => "<a href='chatcmd:///say .merc " + command + "'>" + EscText(label) + "</a>";

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

        public static string EscText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace("\"", string.Empty, StringComparison.Ordinal)
                .Replace("'", string.Empty, StringComparison.Ordinal);
        }

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
