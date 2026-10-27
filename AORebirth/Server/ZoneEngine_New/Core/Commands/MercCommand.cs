namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Linq;
    using System.Text;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Pets;

    using CharacterStat = SmokeLounge.AOtomation.Messaging.GameData.CharacterStat;

    /// <summary>
    /// <c>.merc</c> (everyone): lists your mercenary contracts; <c>.merc hire PROF</c>, <c>.merc dismiss PROF|all</c>,
    /// <c>.merc call</c> (bring every contracted merc back now). The Mercenary Contracts terminals do the same from a chat window.
    /// </summary>
    public sealed class MercCommand : IGmCommand
    {
        public string Name => "merc";

        public int RequiredGmLevel => 0;

        public string Usage => ".merc | .merc hire <profession> [basic|elite|champion|uber] | .merc dismiss <profession|all> | .merc call | .merc list";

        public string Topic => "mercs";

        public string Summary => "Lists, hires, dismisses or calls back your mercenaries.";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Player player = context.Player;
            void Say(string s) => GmCommandFeedback.Send(context.Session, player, s);
            PetService? pets = player.Playfield?.GetService<PetService>();
            if (pets == null)
            {
                Say("Mercenaries are not available here.");
                return;
            }

            string sub = context.Args.Length > 0 ? context.Args[0].ToLowerInvariant() : "list";
            string rest = string.Join(' ', context.Args.Skip(1));
            switch (sub)
            {
                case "hire":
                    string[] hireParts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    Mercs.Tier tier = Mercs.Tier.Basic;
                    if (hireParts.Length > 1 && !Mercs.TryParseTier(hireParts[1], out tier))
                    {
                        Say("Tiers: basic, elite, champion, uber (super is an alias for uber).");
                        return;
                    }

                    if (tier != Mercs.Tier.Basic && player.Stats.GetOrZero(CharacterStat.GmLevel) < 1)
                    {
                        Say("Elite, champion and uber contracts are GM-only until a drop is chosen. .merc hire " + (hireParts.Length > 0 ? hireParts[0] : "<profession>") + " still hires a basic.");
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
                case "dismiss":
                    if (string.Equals(rest, "all", StringComparison.OrdinalIgnoreCase))
                    {
                        pets.DismissMercs(player);
                        Say("All contracts ended.");
                        return;
                    }

                    if (Mercs.Find(rest) is not { } kind)
                    {
                        Say("Usage: .merc dismiss <profession|all>");
                        return;
                    }

                    Say(pets.DismissMerc(player, kind) ? "Your " + kind.Title + " contract has ended." : "You have no " + kind.Title + " under contract.");
                    return;
                case "call":
                    player.OwnedPets.MercReviveUtc.Clear();
                    player.OwnedPets.MercCheckUtc = DateTime.MinValue;
                    pets.TickMercs(player);
                    Say("Calling your mercenaries back.");
                    return;
                case "list":
                case "ls":
                default:
                    var sb = new StringBuilder("Mercenary contracts (hire at a Mercenary Contracts terminal or with .merc hire <profession>):");
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
        }
    }
}