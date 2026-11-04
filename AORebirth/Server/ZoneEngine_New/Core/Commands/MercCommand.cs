namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Linq;
    using System.Text;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Pets;

    using CharacterStat = SmokeLounge.AOtomation.Messaging.GameData.CharacterStat;

    /// <summary>
    /// <c>.merc</c>: dungeon-unlock roster (list/summon/dismiss/menu) plus the existing profession hire/call path.
    /// </summary>
    public sealed class MercCommand : IGmCommand
    {
        readonly MercUnlockService _unlocks;
        readonly MercMenuService _menu;

        public MercCommand(MercUnlockService unlocks, MercMenuService menu)
        {
            _unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
            _menu = menu ?? throw new ArgumentNullException(nameof(menu));
        }

        public string Name => "merc";

        public int RequiredGmLevel => 0;

        public string Usage =>
            ".merc list | .merc summon <name> | .merc dismiss [name|all] | .merc menu | .merc filter <all|basic|twink|elite|summoned> | .merc view <name> | .merc macros | .merc buffs [name] | .merc hire <profession> [tier] | .merc call | .merc setstate <name> <locked|dungeon_only|unlocked>";

        public string Topic => "mercs";

        public string Summary => "Live-style merc menu (one text:// blob), dungeon summon/dismiss, and profession hire/call.";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Player player = context.Player;
            void Say(string s) => GmCommandFeedback.Send(context.Session, player, s);
            PetService? pets = player.Playfield?.GetService<PetService>();

            string sub = context.Args.Length > 0 ? context.Args[0].ToLowerInvariant() : "menu";
            string rest = string.Join(' ', context.Args.Skip(1));
            switch (sub)
            {
                case "menu":
                case "help":
                    _menu.OpenMenu(player);
                    return;
                case "filter":
                case "view":
                case "open":
                case "detail":
                case "macros":
                case "macro":
                case "buffs":
                case "cast":
                case "preset":
                    _menu.Execute(player, context.Args);
                    return;
                case "summon":
                {
                    if (string.IsNullOrWhiteSpace(rest))
                    {
                        Say("Usage: .merc summon <name>");
                        return;
                    }

                    MercDefinition? def = _unlocks.Roster.Find(rest);
                    if (def == null)
                    {
                        Say("Unknown merc. Try .merc list or .merc menu.");
                        return;
                    }

                    string? err = _unlocks.Summon(player, def);
                    Say(err ?? ("Summoned " + _unlocks.DisplayNameFor(player, def) + " (L" + def.Level + ")."));
                    return;
                }
                case "dismiss":
                {
                    if (string.IsNullOrWhiteSpace(rest) || rest.Equals("all", StringComparison.OrdinalIgnoreCase))
                    {
                        // Prefer roster dismiss; also clear profession contracts when "all".
                        string? err = _unlocks.Dismiss(player, null);
                        if (rest.Equals("all", StringComparison.OrdinalIgnoreCase) && pets != null)
                            pets.DismissMercs(player);
                        Say(err ?? "Dismissed.");
                        return;
                    }

                    MercDefinition? def = _unlocks.Roster.Find(rest);
                    if (def != null)
                    {
                        string shown = _unlocks.DisplayNameFor(player, def);
                        string? err = _unlocks.Dismiss(player, def);
                        Say(err ?? ("Dismissed " + shown + "."));
                        return;
                    }

                    // Legacy profession dismiss.
                    if (pets == null)
                    {
                        Say("Mercenaries are not available here.");
                        return;
                    }

                    if (Mercs.Find(rest) is not { } kind)
                    {
                        Say("Usage: .merc dismiss <name|profession|all>");
                        return;
                    }

                    Say(pets.DismissMerc(player, kind) ? "Your " + kind.Title + " contract has ended." : "You have no " + kind.Title + " under contract.");
                    return;
                }
                case "list":
                case "ls":
                {
                    var sb = new StringBuilder("Dungeon mercs (.merc menu for the clickable window):");
                    foreach (MercDefinition def in _unlocks.Roster.All)
                    {
                        MercUnlockState state = _unlocks.GetState(player, def);
                        string shown = _unlocks.DisplayNameFor(player, def);
                        string mark = _unlocks.IsSummoned(player, def) ? ", summoned" : "";
                        sb.Append("\n").Append(shown)
                            .Append(" (").Append(def.Id).Append(") L").Append(def.Level)
                            .Append(" ").Append(def.Role)
                            .Append(": ").Append(MercUnlockService.StateLabel(state)).Append(mark);
                        if (state == MercUnlockState.Locked)
                            sb.Append(" — finish ").Append(def.BossName).Append(" in ").Append(def.DungeonName);
                    }

                    sb.Append("\n\nProfession contracts (legacy hire):");
                    if (pets == null)
                    {
                        sb.Append(" (unavailable here)");
                        Say(sb.ToString());
                        return;
                    }

                    DateTime now = DateTime.UtcNow;
                    foreach (Mercs.Kind k in Mercs.Kinds)
                    {
                        string state = "-";
                        if (PetService.HasContract(player, k))
                        {
                            bool here = player.OwnedPets.All.Any(p => p.Pet != null && !p.IsDead
                                && string.Equals(p.Pet.Hash, k.Hash, StringComparison.OrdinalIgnoreCase));
                            state = here ? "hired, with you"
                                : player.OwnedPets.MercReviveUtc.TryGetValue(k.Key, out DateTime due) && due > now
                                    ? "hired, back in " + Math.Ceiling((due - now).TotalSeconds) + "s"
                                    : "hired, on the way";
                        }

                        string tierLabel = PetService.HasContract(player, k) && Mercs.GetTier(player, k) != Mercs.Tier.Basic
                            ? " " + Mercs.TierName(Mercs.GetTier(player, k))
                            : "";
                        sb.Append("\n").Append(k.Title).Append(tierLabel).Append(" (").Append(k.Key).Append("): ").Append(state);
                    }

                    Say(sb.ToString());
                    return;
                }
                case "setstate":
                case "unlock":
                {
                    if (player.Stats.GetOrZero(CharacterStat.GmLevel) < 1)
                    {
                        Say("GM only: .merc setstate <name> <locked|dungeon_only|unlocked>");
                        return;
                    }

                    string[] parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                    {
                        Say("Usage: .merc setstate <name> <locked|dungeon_only|unlocked>");
                        return;
                    }

                    string stateText = parts[^1];
                    string name = string.Join(' ', parts.Take(parts.Length - 1));
                    MercDefinition? def = _unlocks.Roster.Find(name);
                    if (def == null)
                    {
                        Say("Unknown merc.");
                        return;
                    }

                    if (!MercUnlockService.TryParseState(stateText, out MercUnlockState state))
                    {
                        Say("State must be locked, dungeon_only, or unlocked.");
                        return;
                    }

                    _unlocks.SetState(player, def, state, out string detail);
                    Say(detail);
                    return;
                }
                case "hire":
                {
                    if (pets == null)
                    {
                        Say("Mercenaries are not available here.");
                        return;
                    }

                    string[] hireParts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    Mercs.Tier tier = Mercs.Tier.Basic;
                    if (hireParts.Length > 1 && !Mercs.TryParseTier(hireParts[1], out tier))
                    {
                        Say("Tiers: basic, elite, champion, twink (uber/super are aliases for twink).");
                        return;
                    }

                    if (tier != Mercs.Tier.Basic && player.Stats.GetOrZero(CharacterStat.GmLevel) < 1)
                    {
                        Say("Elite, champion and twink contracts are GM-only until a drop is chosen. .merc hire " + (hireParts.Length > 0 ? hireParts[0] : "<profession>") + " still hires a basic.");
                        return;
                    }

                    if (Mercs.Find(hireParts.Length > 0 ? hireParts[0] : "") is not { } hire)
                    {
                        Say("Unknown profession. Professions: " + string.Join(", ", Mercs.Kinds.Select(k => k.Key)));
                        return;
                    }

                    Say(pets.HireMerc(player, hire, player.Stats.GetOrOne(CharacterStat.Level), tier) != null
                        ? "Contract signed: your " + Mercs.TierName(tier) + " " + hire.Title + " is on the way."
                        : "Could not hire right now (mercenaries off, or you are dead or zoning).");
                    return;
                }
                case "call":
                    if (pets == null)
                    {
                        Say("Mercenaries are not available here.");
                        return;
                    }

                    player.OwnedPets.MercReviveUtc.Clear();
                    player.OwnedPets.MercCheckUtc = DateTime.MinValue;
                    pets.TickMercs(player);
                    Say("Calling your mercenaries back.");
                    return;
                default:
                    // Unknown verb → menu (buffbot-style).
                    _menu.OpenMenu(player);
                    return;
            }
        }
    }
}
