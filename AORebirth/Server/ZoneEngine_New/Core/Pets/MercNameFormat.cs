namespace ZoneEngine_New.Core.Pets
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Randy merc nameplate formatting and sex/appearance selection.
    /// SCFU names are a length-prefixed byte string (max 254 usable chars). Quotes are fine as ASCII.
    /// Practical nameplate budget is <see cref="MaxNameChars"/> so mode tags still fit.
    /// Appearance values are copied only from existing merc NPC templates (Breed/Sex/HeadMesh).
    /// </summary>
    public static class MercNameFormat
    {
        /// <summary>Practical nameplate budget (SCFU allows up to 254; keep room for mode tags).</summary>
        public const int MaxNameChars = 32;

        // Real merc-template looks (NpcTemplates.json): Breed=4, Sex=59, HeadMesh=64.
        // Male Solitus: MRBU/MRFI 40687, MRMA/MRKE 40689, MRSO/MREG 40694, MRDR/MRTR 40700.
        // Male Atrox: MREN sex=1 head=40111 (Atrox has no female mesh — never invent one).
        // Female Solitus: MRAG/MRMP/MRSH 40634, MRNT/MRAD 40635.
        static readonly AppearanceLook[] MaleSolitusLooks =
        [
            new(1, 2, 40687),
            new(1, 2, 40689),
            new(1, 2, 40694),
            new(1, 2, 40700),
        ];

        static readonly AppearanceLook AtroxMaleLook = new(4, 1, 40111);

        static readonly AppearanceLook[] FemaleSolitusLooks =
        [
            new(1, 3, 40634),
            new(1, 3, 40635),
        ];

        public static string FormatSummonName(MercDefinition def, bool female, string? modeTag)
        {
            ArgumentNullException.ThrowIfNull(def);
            string full = female ? def.FemaleName : def.MaleName;
            string nick = female ? def.FemaleNick : def.MaleNick;
            string last = female ? def.FemaleLast : def.MaleLast;
            if (string.IsNullOrWhiteSpace(full))
                full = def.DisplayName;
            if (string.IsNullOrWhiteSpace(nick))
                nick = full;
            if (string.IsNullOrWhiteSpace(last))
                last = string.Empty;

            string tag = string.IsNullOrWhiteSpace(modeTag) ? string.Empty : " (" + modeTag.Trim() + ")";
            string candidate = full + tag;
            if (candidate.Length <= MaxNameChars)
                return candidate;

            // Prefer "Nickname Last" when the full First "Nick" Last + tag is too long.
            string nickLast = string.IsNullOrEmpty(last) ? nick + tag : nick + " " + last + tag;
            if (nickLast.Length <= MaxNameChars)
                return nickLast;

            string nickOnly = nick + tag;
            if (nickOnly.Length <= MaxNameChars)
                return nickOnly;

            return nickOnly.Length <= MaxNameChars
                ? nickOnly
                : nickOnly.Substring(0, MaxNameChars);
        }

        public static string? ModeTagFor(MercDefinition def, string ntMode, string traderMode, string mpMode)
        {
            ArgumentNullException.ThrowIfNull(def);
            if (string.Equals(def.Kind.Key, "nanotechnician", StringComparison.OrdinalIgnoreCase))
                return string.Equals(ntMode, MercUnlockService.NtModeSt, StringComparison.OrdinalIgnoreCase) ? "ST" : "AOE";
            if (string.Equals(def.Kind.Key, "trader", StringComparison.OrdinalIgnoreCase))
                return string.Equals(traderMode, MercUnlockService.TraderModeUmbral, StringComparison.OrdinalIgnoreCase)
                    ? "Umbral"
                    : "Wrangle";
            if (string.Equals(def.Kind.Key, "metaphysicist", StringComparison.OrdinalIgnoreCase))
                return string.Equals(mpMode, MercUnlockService.MpModeSingles, StringComparison.OrdinalIgnoreCase)
                    ? "Singles"
                    : "Composite";
            return null;
        }

        /// <summary>
        /// Infer sex from an already-applied Randy name so mode switches do not re-roll.
        /// Null means the body still has a template/default name.
        /// </summary>
        public static bool? TryInferFemale(string? currentName, MercDefinition def)
        {
            ArgumentNullException.ThrowIfNull(def);
            if (string.IsNullOrWhiteSpace(currentName))
                return null;
            string core = StripModeTag(currentName);
            if (NameMatchesSide(core, def.FemaleName, def.FemaleNick, def.FemaleLast))
                return true;
            if (NameMatchesSide(core, def.MaleName, def.MaleNick, def.MaleLast))
                return false;
            return null;
        }

        public static bool NameMatches(string? raw, MercDefinition def)
        {
            ArgumentNullException.ThrowIfNull(def);
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            string core = StripModeTag(raw.Trim());
            return NameMatchesSide(core, def.MaleName, def.MaleNick, def.MaleLast)
                || NameMatchesSide(core, def.FemaleName, def.FemaleNick, def.FemaleLast)
                || string.Equals(core, def.DisplayName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(core, def.Id, StringComparison.OrdinalIgnoreCase);
        }

        public static void ApplyAppearance(NpcCharacter npc, MercDefinition def, bool female)
        {
            ArgumentNullException.ThrowIfNull(npc);
            ArgumentNullException.ThrowIfNull(def);
            AppearanceLook look;
            if (female)
            {
                // Never invent Atrox female — use Solitus female heads from MRAG/MRNT pool.
                look = FemaleSolitusLooks[Random.Shared.Next(FemaleSolitusLooks.Length)];
            }
            else if (string.Equals(def.Breed, "Atrox", StringComparison.OrdinalIgnoreCase))
            {
                look = AtroxMaleLook;
            }
            else
            {
                look = MaleSolitusLooks[Random.Shared.Next(MaleSolitusLooks.Length)];
            }

            npc.Stats.Set(CharacterStat.Breed, look.Breed, StatDetail.Base, dirty: true);
            npc.Stats.Set(CharacterStat.Sex, look.Sex, StatDetail.Base, dirty: true);
            npc.Stats.Set(CharacterStat.HeadMesh, look.HeadMesh, StatDetail.Base, dirty: true);
        }

        public static string StripModeTag(string name)
        {
            int open = name.LastIndexOf(" (", StringComparison.Ordinal);
            if (open > 0 && name.EndsWith(")", StringComparison.Ordinal))
                return name.Substring(0, open).Trim();
            return name.Trim();
        }

        public static void ParseQuotedName(string full, out string nick, out string last)
        {
            nick = full?.Trim() ?? string.Empty;
            last = string.Empty;
            if (string.IsNullOrEmpty(full))
                return;

            int q1 = full.IndexOf('"');
            int q2 = q1 >= 0 ? full.IndexOf('"', q1 + 1) : -1;
            if (q1 >= 0 && q2 > q1)
            {
                nick = full.Substring(q1 + 1, q2 - q1 - 1).Trim();
                last = full.Substring(q2 + 1).Trim();
                return;
            }

            string[] parts = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                nick = parts[0];
                last = parts[^1];
            }
        }

        static bool NameMatchesSide(string core, string full, string nick, string last)
        {
            if (string.IsNullOrWhiteSpace(core))
                return false;
            if (!string.IsNullOrWhiteSpace(full)
                && string.Equals(core, full, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrWhiteSpace(nick)
                && string.Equals(core, nick, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrWhiteSpace(nick) && !string.IsNullOrWhiteSpace(last))
            {
                string nickLast = nick + " " + last;
                if (string.Equals(core, nickLast, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // "/tell Anvil" / partial summons: unique nick as a whole word.
            if (!string.IsNullOrWhiteSpace(nick)
                && core.Contains(nick, StringComparison.OrdinalIgnoreCase)
                && (core.Equals(nick, StringComparison.OrdinalIgnoreCase)
                    || core.Contains('"' + nick + '"', StringComparison.OrdinalIgnoreCase)
                    || core.StartsWith(nick + " ", StringComparison.OrdinalIgnoreCase)
                    || core.EndsWith(" " + nick, StringComparison.OrdinalIgnoreCase)
                    || core.Contains(" " + nick + " ", StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        readonly record struct AppearanceLook(int Breed, int Sex, int HeadMesh);
    }
}
