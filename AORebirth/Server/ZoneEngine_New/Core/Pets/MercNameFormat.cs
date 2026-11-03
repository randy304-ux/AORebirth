namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Randy merc nameplate formatting and sex/appearance selection.
    /// SCFU names are a length-prefixed byte string (max 254 usable chars). Quotes are fine as ASCII.
    /// Practical nameplate budget is <see cref="MaxNameChars"/> so mode tags still fit.
    /// Appearance values are copied only from existing NPC/merc templates (Breed/Sex/HeadMesh).
    /// </summary>
    public static class MercNameFormat
    {
        /// <summary>Practical nameplate budget (SCFU allows up to 254; keep room for mode tags).</summary>
        public const int MaxNameChars = 32;

        // Real looks from NpcTemplates.json (Breed=4, Sex=59, HeadMesh=64). Never invent meshes.
        // Male Solitus: MRBU/MRFI 40687, MRMA/MRKE 40689, MRSO/MREG 40694, MRDR/MRTR 40700.
        // Female Solitus: MRAG/MRMP/MRSH 40634, MRNT/MRAD 40635.
        // Male Atrox: MREN sex=1 head=40111 (no Atrox female template exists).
        // Male Opifex: KLHG breed=2 sex=2 head=40249.
        // Female Opifex: BTMV/SAOI/SNOL 40209, CNTD 40243.
        // Male Nanomage: QWJW/C03C 40169, VEAE/C00Z/KELE 40171, OITA/NETH 40173.
        // Female Nanomage: OBE_ 40137, C036/IVOK/LYOD 40140, C00M 40151, C00L 40172.
        static readonly AppearanceLook[] MaleSolitusLooks =
        [
            new(1, 2, 40687),
            new(1, 2, 40689),
            new(1, 2, 40694),
            new(1, 2, 40700),
        ];

        static readonly AppearanceLook[] FemaleSolitusLooks =
        [
            new(1, 3, 40634),
            new(1, 3, 40635),
        ];

        static readonly AppearanceLook AtroxMaleLook = new(4, 1, 40111);

        static readonly AppearanceLook[] MaleOpifexLooks =
        [
            new(2, 2, 40249),
        ];

        static readonly AppearanceLook[] FemaleOpifexLooks =
        [
            new(2, 3, 40209),
            new(2, 3, 40243),
        ];

        static readonly AppearanceLook[] MaleNanomageLooks =
        [
            new(3, 2, 40169),
            new(3, 2, 40171),
            new(3, 2, 40173),
        ];

        static readonly AppearanceLook[] FemaleNanomageLooks =
        [
            new(3, 3, 40137),
            new(3, 3, 40140),
            new(3, 3, 40151),
            new(3, 3, 40172),
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

        /// <summary>
        /// Apply Breed/Sex/HeadMesh for a new summon. Mode changes must not call this when the Randy
        /// name is already set — existing Breed/Sex/HeadMesh stay as rolled.
        /// When <paramref name="randomAppearance"/> is true (default): roll sex first (caller), then a
        /// random breed with a real template for that sex (Atrox males only).
        /// When false: Randy fixed profession breeds (MA/Shade/Fixer Opifex; NT/MP Nanomage;
        /// male Enforcer/Keeper/Agent Atrox; rest Solitus; Atrox females fall back to Solitus).
        /// </summary>
        public static void ApplyAppearance(NpcCharacter npc, MercDefinition def, bool female, bool randomAppearance)
        {
            ArgumentNullException.ThrowIfNull(npc);
            ArgumentNullException.ThrowIfNull(def);
            AppearanceLook look = randomAppearance
                ? ResolveRandomLook(female)
                : ResolveFixedProfessionLook(def, female);
            npc.Stats.Set(CharacterStat.Breed, look.Breed, StatDetail.Base, dirty: true);
            npc.Stats.Set(CharacterStat.Sex, look.Sex, StatDetail.Base, dirty: true);
            npc.Stats.Set(CharacterStat.HeadMesh, look.HeadMesh, StatDetail.Base, dirty: true);
        }

        static AppearanceLook ResolveRandomLook(bool female)
        {
            var pool = new List<AppearanceLook>(8);
            if (female)
            {
                AddBreedIfPresent(pool, FemaleSolitusLooks);
                AddBreedIfPresent(pool, FemaleOpifexLooks);
                AddBreedIfPresent(pool, FemaleNanomageLooks);
                // No female Atrox template in our data — skipped.
            }
            else
            {
                AddBreedIfPresent(pool, MaleSolitusLooks);
                AddBreedIfPresent(pool, MaleOpifexLooks);
                AddBreedIfPresent(pool, MaleNanomageLooks);
                pool.Add(AtroxMaleLook);
            }

            if (pool.Count == 0)
                return female ? FemaleSolitusLooks[0] : MaleSolitusLooks[0];

            // One look per breed in the pool, then pick a head within that breed.
            AppearanceLook breedPick = pool[Random.Shared.Next(pool.Count)];
            AppearanceLook[] heads = LooksForBreed(breedPick.Breed, female);
            return heads.Length > 0 ? Pick(heads) : breedPick;
        }

        static AppearanceLook ResolveFixedProfessionLook(MercDefinition def, bool female)
        {
            string key = def.Kind.Key;
            if (UsesAtroxMale(key, def.Breed))
                return female ? Pick(FemaleSolitusLooks) : AtroxMaleLook;

            string breed = ResolveFixedBreed(def);
            if (string.Equals(breed, "Opifex", StringComparison.OrdinalIgnoreCase))
                return female ? Pick(FemaleOpifexLooks) : Pick(MaleOpifexLooks);
            if (string.Equals(breed, "Nanomage", StringComparison.OrdinalIgnoreCase))
                return female ? Pick(FemaleNanomageLooks) : Pick(MaleNanomageLooks);
            return female ? Pick(FemaleSolitusLooks) : Pick(MaleSolitusLooks);
        }

        static bool UsesAtroxMale(string professionKey, string? breed)
        {
            if (string.Equals(professionKey, "enforcer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(professionKey, "keeper", StringComparison.OrdinalIgnoreCase)
                || string.Equals(professionKey, "agent", StringComparison.OrdinalIgnoreCase))
                return true;
            return string.Equals(breed, "Atrox", StringComparison.OrdinalIgnoreCase);
        }

        static string ResolveFixedBreed(MercDefinition def)
        {
            if (!string.IsNullOrWhiteSpace(def.Breed))
                return def.Breed.Trim();

            string key = def.Kind.Key;
            if (string.Equals(key, "martialartist", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "shade", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "fixer", StringComparison.OrdinalIgnoreCase))
                return "Opifex";
            if (string.Equals(key, "nanotechnician", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "metaphysicist", StringComparison.OrdinalIgnoreCase))
                return "Nanomage";
            if (string.Equals(key, "enforcer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "keeper", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "agent", StringComparison.OrdinalIgnoreCase))
                return "Atrox";
            return "Solitus";
        }

        static void AddBreedIfPresent(List<AppearanceLook> pool, AppearanceLook[] looks)
        {
            if (looks.Length > 0)
                pool.Add(looks[0]);
        }

        static AppearanceLook[] LooksForBreed(int breed, bool female) => breed switch
        {
            2 => female ? FemaleOpifexLooks : MaleOpifexLooks,
            3 => female ? FemaleNanomageLooks : MaleNanomageLooks,
            4 => female ? [] : [AtroxMaleLook],
            _ => female ? FemaleSolitusLooks : MaleSolitusLooks,
        };

        static AppearanceLook Pick(AppearanceLook[] looks)
            => looks[Random.Shared.Next(looks.Length)];

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
