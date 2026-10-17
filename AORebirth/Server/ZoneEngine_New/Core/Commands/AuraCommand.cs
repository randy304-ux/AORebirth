namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Nanos;

    /// <summary>
    /// <c>.aura</c> (everyone): lists unlocked dyna auras and applies one to yourself, or to yourself plus your
    /// pets and mercs. The colour has to be unlocked first by using that colour's container. <c>.aura unlock</c>
    /// is a GM shortcut so a colour can be tested without a drop.
    /// </summary>
    public sealed class AuraCommand : IGmCommand
    {
        static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["green"] = "Chemical",
            ["white"] = "Cold",
            ["blue"] = "Energy",
            ["red"] = "Fire",
            ["aqua"] = "Poison",
            ["purple"] = "Radiation",
            ["tan"] = "Crit",
            ["orange"] = "Shield",
            ["yellow"] = "Big",
            ["health"] = "Big",
            ["massive"] = "Big",
            ["dmgshield"] = "Shield",
            ["damageshield"] = "Shield",
        };

        public string Name => "aura";

        public int RequiredGmLevel => 0;

        public string Usage => ".aura | .aura <color> [pets] | .aura off | .aura unlock <color>";

        public string Summary => "Lists unlocked dyna auras and applies one to you or your pets and mercs.";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Player player = context.Player;
            void Say(string s) => GmCommandFeedback.Send(context.Session, player, s);

            if (context.Args.Length == 0)
            {
                Say(List(player));
                return;
            }

            string sub = context.Args[0];
            if (string.Equals(sub, "off", StringComparison.OrdinalIgnoreCase))
            {
                AuraNanos.Clear(player);
                Say("Aura removed.");
                return;
            }

            if (string.Equals(sub, "unlock", StringComparison.OrdinalIgnoreCase))
            {
                if (player.Stats.GetOrZero(CharacterStat.GmLevel) < 1)
                {
                    Say("Use the container from a dyna to unlock an aura.");
                    return;
                }

                if (context.Args.Length < 2 || Find(context.Args[1]) is not { } grant)
                {
                    Say("Usage: .aura unlock <color>");
                    return;
                }

                int index = DynaModifiers.IndexOf(grant.Name);
                AuraNanos.Unlock(player, index);
                Say("Unlocked the " + grant.Name + " aura.");
                return;
            }

            if (Find(sub) is not { } mod)
            {
                Say("Unknown aura. Colours: " + Names() + ".");
                return;
            }

            int modIndex = DynaModifiers.IndexOf(mod.Name);
            if (!AuraNanos.IsUnlocked(player, modIndex))
            {
                Say("The " + mod.Name + " aura is locked. Use a " + mod.ContainerName + " from a dyna of that colour.");
                return;
            }

            bool pets = context.Args.Length > 1
                && string.Equals(context.Args[1], "pets", StringComparison.OrdinalIgnoreCase);
            if (context.Args.Length > 1 && !pets)
            {
                Say("Usage: .aura " + mod.Name.ToLowerInvariant() + " [pets]");
                return;
            }

            if (!AuraNanos.Apply(player, mod, pets, DateTime.UtcNow))
            {
                Say("Could not apply the " + mod.Name + " aura right now.");
                return;
            }

            Say(pets
                ? mod.Name + " aura applied to you, your pets and your mercs."
                : mod.Name + " aura applied.");
        }

        static string List(Player player)
        {
            var sb = new StringBuilder("Auras (use the container, then .aura <color> or .aura <color> pets):");
            int active = player.Stats.GetOrZero(AuraNanos.ActiveStat);
            for (int i = 0; i < DynaModifiers.All.Length; i++)
            {
                DynaModifiers.Modifier mod = DynaModifiers.All[i];
                string state = !AuraNanos.IsUnlocked(player, i)
                    ? "locked (" + mod.ContainerName + ")"
                    : active == i + 1
                        ? "unlocked, worn"
                        : "unlocked";
                sb.Append('\n').Append(mod.Name).Append(": ").Append(state);
            }

            return sb.ToString();
        }

        static string Names()
        {
            var names = new string[DynaModifiers.All.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = DynaModifiers.All[i].Name;
            return string.Join(", ", names);
        }

        static DynaModifiers.Modifier? Find(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            string key = text.Trim();
            if (Aliases.TryGetValue(key, out string? canonical))
                key = canonical;
            return DynaModifiers.Find(key);
        }
    }
}
