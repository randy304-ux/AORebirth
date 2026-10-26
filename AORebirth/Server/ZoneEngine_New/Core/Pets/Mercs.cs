namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Dungeon Dogs hired mercenaries: pet-based companions a player hires from a Mercenary Contracts terminal (Knubot
    /// <c>{hiremerc KIND}</c> / <c>{dismissmerc KIND}</c> / <c>{dismissmercs}</c>) or with <c>.merc</c>. A merc is a player pet
    /// in its own pet slot (10 and up), so it never replaces the player's own pets. Contracts are saved on the character
    /// (server-only stat <see cref="ContractsStat"/>, one bit per profession): a merc that dies, or whose owner dies or logs
    /// off, comes back beside its owner with full health after <see cref="Catalog.ReviveSeconds"/>, and on the next login.
    /// Professions are fixed here (key, hash, slot); levels, looks, heals, nanos and armor come from GameData
    /// (NpcTemplates MR** and Mercs.json, both written by ddogs/tools/Build-Mercs.ps1).
    /// A tier above basic raises the merc's level (capped at 220) and then its health. It does not copy the owner's stats.
    /// </summary>
    public static class Mercs
    {
        /// <summary>Server-only character stat: bit <c>Kind.Bit</c> set while that contract is active.</summary>
        public const CharacterStat ContractsStat = (CharacterStat)10280;

        /// <summary>Two bits per profession: 0 basic, 1 elite, 2 champion, 3 super. 10281-10283 are aura unlocks.</summary>
        public const CharacterStat TierStat = (CharacterStat)10284;

        public enum Tier
        {
            Basic = 0,
            Elite = 1,
            Champion = 2,
            Super = 3
        }

        public readonly record struct Package(int LevelBonus, int HpPercent, int DamagePercent);

        public static Package PackageFor(Tier tier) => tier switch
        {
            Tier.Elite => new Package(40, 125, 125),
            Tier.Champion => new Package(80, 150, 150),
            Tier.Super => new Package(120, 175, 175),
            _ => new Package(0, 100, 100)
        };

        /// <summary>Owner level plus the tier bonus, never above 220. A level 1000 GM still tops out here.</summary>
        public static int GearLevel(int bodyLevel, Tier tier)
            => Math.Clamp(Math.Clamp(bodyLevel, 1, 220) + PackageFor(tier).LevelBonus, 1, 220);

        public static bool TryParseTier(string text, out Tier tier)
        {
            tier = Tier.Basic;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            switch (text.Trim().ToLowerInvariant())
            {
                case "basic":
                case "normal":
                    tier = Tier.Basic;
                    return true;
                case "elite":
                case "e":
                    tier = Tier.Elite;
                    return true;
                case "champion":
                case "champ":
                case "c":
                    tier = Tier.Champion;
                    return true;
                case "super":
                case "s":
                    tier = Tier.Super;
                    return true;
                default:
                    return false;
            }
        }

        public static string TierName(Tier tier) => tier switch
        {
            Tier.Elite => "Elite",
            Tier.Champion => "Champion",
            Tier.Super => "Super",
            _ => "Basic"
        };

        public static Tier GetTier(Player owner, Kind kind)
        {
            int packed = owner.Stats.GetOrZero(TierStat, StatDetail.Base);
            return (Tier)((packed >> (kind.Bit * 2)) & 3);
        }

        public static void SetTier(Player owner, Kind kind, Tier tier)
        {
            int shift = kind.Bit * 2;
            int packed = owner.Stats.GetOrZero(TierStat, StatDetail.Base);
            int next = (packed & ~(3 << shift)) | (((int)tier & 3) << shift);
            if (next == packed)
                return;
            owner.Stats.Set(TierStat, next, StatDetail.Base);
            owner.SaveState.MarkDirty();
        }

        public static void ClearTiers(Player owner)
        {
            if (owner.Stats.GetOrZero(TierStat, StatDetail.Base) == 0)
                return;
            owner.Stats.Set(TierStat, 0, StatDetail.Base);
            owner.SaveState.MarkDirty();
        }

        public sealed record Kind(string Key, string Hash, string Title, int Profession, int Slot, int Bit)
        {
            public int PetType => (Slot * 1000) + 1;
        }

        public static readonly Kind[] Kinds =
        [
            new("enforcer", "MREN", "Enforcer", 9, 10, 0),
            new("soldier", "MRSO", "Soldier", 1, 11, 1),
            new("keeper", "MRKE", "Keeper", 14, 12, 2),
            new("doctor", "MRDR", "Doctor", 10, 13, 3),
            new("adventurer", "MRAD", "Adventurer", 6, 14, 4),
            new("agent", "MRAG", "Agent", 5, 15, 5),
            new("bureaucrat", "MRBU", "Bureaucrat", 8, 16, 6),
            new("engineer", "MREG", "Engineer", 3, 17, 7),
            new("fixer", "MRFI", "Fixer", 4, 18, 8),
            new("martialartist", "MRMA", "Martial Artist", 2, 19, 9),
            new("metaphysicist", "MRMP", "Metaphysicist", 12, 20, 10),
            new("nanotechnician", "MRNT", "Nano-Technician", 11, 21, 11),
            new("shade", "MRSH", "Shade", 15, 22, 12),
            new("trader", "MRTR", "Trader", 7, 23, 13),
        ];

        static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["enf"] = "enforcer", ["sol"] = "soldier", ["doc"] = "doctor", ["adv"] = "adventurer", ["crat"] = "bureaucrat",
            ["engi"] = "engineer", ["eng"] = "engineer", ["ma"] = "martialartist", ["martial"] = "martialartist",
            ["mp"] = "metaphysicist", ["meta"] = "metaphysicist", ["nt"] = "nanotechnician", ["nano"] = "nanotechnician",
            ["keep"] = "keeper", ["fix"] = "fixer"
        };

        public static Kind? Find(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;
            string k = key.Trim().Replace("-", string.Empty).Replace(" ", string.Empty);
            if (Aliases.TryGetValue(k, out string? full))
                k = full;
            foreach (Kind kind in Kinds)
            {
                if (string.Equals(kind.Key, k, StringComparison.OrdinalIgnoreCase))
                    return kind;
            }

            return null;
        }

        public static Kind? ByHash(string? hash)
        {
            foreach (Kind kind in Kinds)
            {
                if (string.Equals(kind.Hash, hash, StringComparison.OrdinalIgnoreCase))
                    return kind;
            }

            return null;
        }

        public static bool IsMercHash(string? hash) => ByHash(hash) != null;

        public sealed class ArmorTier
        {
            public int MinLevel { get; set; }
            public List<List<int>> Items { get; set; } = new();
            /// <summary>Omni owners (Side 2) wear these instead when set (Chosen); others wear Items (Faithful).</summary>
            public List<List<int>>? OmniItems { get; set; }
        }

        public sealed class NanoBand
        {
            public int MinLevel { get; set; }
            public List<int> Nanos { get; set; } = new();
        }

        public sealed class Profession
        {
            public bool Heals { get; set; }
            public List<NanoBand> Nanos { get; set; } = new();
            public List<ArmorTier> Armor { get; set; } = new();
            /// <summary>Held weapon (low/high item id) shown in the right hand; looks only.</summary>
            public List<int>? Weapon { get; set; }
            /// <summary>NpcTemplates hash of the one pet this merc keeps out, when <see cref="Pets"/> is empty.</summary>
            public string? Pet { get; set; }
            /// <summary>Pets this merc keeps out. Absent or empty falls back to <see cref="Pet"/>.</summary>
            public List<MercPetSpec>? Pets { get; set; }
        }

        /// <summary>One pet a merc summons: hash, the merc level it starts appearing at, and its pet type (0 = PetTypes.json).</summary>
        public sealed class MercPetSpec
        {
            public string Hash { get; set; } = "";
            public int MinLevel { get; set; } = 1;
            public int Type { get; set; }
        }

        public sealed class Catalog
        {
            public bool Enabled { get; set; } = true;
            public int ReviveSeconds { get; set; } = 10;
            public bool ArmorStats { get; set; } = true;
            public Dictionary<string, Profession> Professions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

            public Profession? For(Kind kind) => Professions.TryGetValue(kind.Key, out Profession? p) ? p : null;
        }

        static readonly ConcurrentDictionary<string, (DateTime Stamp, Catalog Catalog)> Cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>GameData/Mercs.json, re-read when the file changes. Missing or broken file: enabled, no extras.</summary>
        public static Catalog Load(string gameDataRoot)
        {
            string path = Path.Combine(gameDataRoot, "Mercs.json");
            try
            {
                if (!File.Exists(path))
                    return new Catalog();
                DateTime stamp = File.GetLastWriteTimeUtc(path);
                if (Cache.TryGetValue(path, out var hit) && hit.Stamp == stamp)
                    return hit.Catalog;
                Catalog loaded = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Catalog();
                loaded.Professions = new Dictionary<string, Profession>(loaded.Professions ?? new(), StringComparer.OrdinalIgnoreCase);
                Cache[path] = (stamp, loaded);
                return loaded;
            }
            catch
            {
                return new Catalog();
            }
        }

        public static bool Enabled(string gameDataRoot) => Load(gameDataRoot).Enabled;

        /// <summary>Nanos the merc uploads at <paramref name="level"/> (the highest band at or below it).</summary>
        public static IReadOnlyList<int> NanosFor(string gameDataRoot, string hash, int level)
        {
            Kind? kind = ByHash(hash);
            Profession? prof = kind == null ? null : Load(gameDataRoot).For(kind);
            if (prof == null)
                return [];
            NanoBand? best = null;
            foreach (NanoBand band in prof.Nanos)
            {
                if (band.MinLevel <= level && (best == null || band.MinLevel > best.MinLevel))
                    best = band;
            }

            return best?.Nanos ?? [];
        }

        /// <summary>Armor pieces (low/high item id pairs) for this merc level and owner side.</summary>
        public static IReadOnlyList<List<int>> ArmorFor(string gameDataRoot, Kind kind, int level, int ownerSide)
        {
            Profession? prof = Load(gameDataRoot).For(kind);
            if (prof == null)
                return [];
            ArmorTier? best = null;
            foreach (ArmorTier tier in prof.Armor)
            {
                if (tier.MinLevel <= level && (best == null || tier.MinLevel > best.MinLevel))
                    best = tier;
            }

            if (best == null)
                return [];
            return ownerSide == 2 && best.OmniItems is { Count: > 0 } omni ? omni : best.Items;
        }

        public static List<int>? WeaponFor(string gameDataRoot, Kind kind) => Load(gameDataRoot).For(kind)?.Weapon;

        public static string? PetFor(string gameDataRoot, Kind kind) => Load(gameDataRoot).For(kind)?.Pet;

        /// <summary>Pets this merc should have out at <paramref name="level"/> (attack, and for an MP the mezz and heal once the level allows).</summary>
        public static IReadOnlyList<MercPetSpec> PetsFor(string gameDataRoot, Kind kind, int level)
        {
            Profession? prof = Load(gameDataRoot).For(kind);
            if (prof == null)
                return [];
            if (prof.Pets is { Count: > 0 })
            {
                var list = new List<MercPetSpec>();
                foreach (MercPetSpec spec in prof.Pets)
                {
                    if (spec.Hash.Length > 0 && spec.MinLevel <= level)
                        list.Add(spec);
                }

                return list;
            }

            return string.IsNullOrEmpty(prof.Pet) ? [] : [new MercPetSpec { Hash = prof.Pet, MinLevel = 1, Type = 0 }];
        }

        public static bool Heals(string gameDataRoot, string? hash)
        {
            Kind? kind = ByHash(hash);
            return kind != null && Load(gameDataRoot).For(kind)?.Heals == true;
        }
    }
}