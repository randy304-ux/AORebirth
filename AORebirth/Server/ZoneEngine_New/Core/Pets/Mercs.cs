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
    /// A tier above basic does not raise the merc's level. The body stays at the owner's level (capped at 220)
    /// so a level 20 still walks with a level 20. The tier twinks that body: higher-QL gear and a higher-QL
    /// monster weapon, a fixed stat and skill package, merc pets and nano bands taken from a higher level,
    /// and (where the nano is a two-id QL pair) casts resolved at the gear QL.
    /// </summary>
    public static class Mercs
    {
        /// <summary>Server-only character stat: bit <c>Kind.Bit</c> set while that contract is active.</summary>
        public const CharacterStat ContractsStat = (CharacterStat)10280;

        /// <summary>Two bits per profession: 0 basic, 1 elite, 2 champion, 3 twink. 10281-10283 are aura unlocks.</summary>
        public const CharacterStat TierStat = (CharacterStat)10284;

        public enum Tier
        {
            Basic = 0,
            Elite = 1,
            Champion = 2,
            /// <summary>Highest tier. Stored value 3; display name Twink (uber/super still parse as aliases).</summary>
            Twink = 3
        }

        /// <summary>
        /// Fixed twink package. <see cref="GearQlBonus"/> raises worn-item and monster-weapon QL (cap 300).
        /// <see cref="PowerLevelBonus"/> raises the level used for armor tier, nano band, and this merc's own pets (cap 220).
        /// Health, damage, and skill are flat adds on the body, not a percent of the owner.
        /// </summary>
        public readonly record struct Package(int GearQlBonus, int PowerLevelBonus, int HealthAdd, int DamageAdd, int SkillAdd)
        {
            public bool IsBasic => GearQlBonus == 0 && PowerLevelBonus == 0 && HealthAdd == 0 && DamageAdd == 0 && SkillAdd == 0;
        }

        public static Package PackageFor(Tier tier) => tier switch
        {
            // A level 60 elite's pet lands at 140, inside the QL 100-200 cast a buffed player would get.
            Tier.Elite => new Package(80, 80, 800, 40, 200),
            // Champion wears Alien (Combined) gear, weaker than Ofab: compensate with a higher gear-QL bonus
            // (not a separate weapon-damage hack). Same DressMerc QL path as every other tier.
            Tier.Champion => new Package(180, 120, 2000, 90, 400),
            // Twink: largest flat package so the tier is clearly best (gear + nanos + these stats).
            Tier.Twink => new Package(220, 160, 5500, 220, 1000),
            _ => new Package(0, 0, 0, 0, 0)
        };

        /// <summary>One worn template pair (low/high item id for QL interpolation).</summary>
        public readonly record struct GearPiece(int Low, int High);

        /// <summary>Perennium Blaster QL 50 through the QL 200 superior (itemnames 246422-246428). Kept for older soldiers.</summary>
        public const int PerenniumBlasterLow = 246422;

        public const int PerenniumBlasterHigh = 246428;

        // Pandemonium Beast pieces (Awakened). Verified in items.dat.
        public const int BeastShoulderLow = 302725;
        public const int BeastShoulderHigh = 302725;
        public const int BeastHelmetLow = 302728;
        public const int BeastHelmetHigh = 302728;
        public const int BeastBackLow = 302730;
        public const int BeastBackHigh = 302730;

        /// <summary>The merc's displayed level: the owner's level, never above 220. A level 1000 GM still tops out here.</summary>
        public static int BodyLevel(int ownerLevel) => Math.Clamp(ownerLevel, 1, 220);

        /// <summary>QL of the twinked gear and the monster weapon. Basic stays at the body level.</summary>
        public static int GearQuality(int ownerLevel, Tier tier)
        {
            int body = BodyLevel(ownerLevel);
            int bonus = PackageFor(tier).GearQlBonus;
            return bonus <= 0 ? body : Math.Clamp(body + bonus, 1, 300);
        }

        /// <summary>Level used for armor bands, nano bands, and the merc's pets. Capped at 220. Basic equals the body.</summary>
        public static int PowerLevel(int ownerLevel, Tier tier)
        {
            int body = BodyLevel(ownerLevel);
            int bonus = PackageFor(tier).PowerLevelBonus;
            return bonus <= 0 ? body : Math.Clamp(body + bonus, 1, 220);
        }

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
                case "twink":
                case "t":
                case "uber":
                case "u":
                case "super":
                case "s":
                    tier = Tier.Twink;
                    return true;
                default:
                    return false;
            }
        }

        public static string TierName(Tier tier) => tier switch
        {
            Tier.Elite => "Elite",
            Tier.Champion => "Champion",
            Tier.Twink => "Twink",
            _ => "Basic"
        };

        /// <summary>
        /// Tier-specific worn armor (replaces Mercs.json bands above basic). Null means use Mercs.json.
        /// Elite = Penultimate Ofab. Champion = Combined Alien. Twink = Beast helm/shoulders/back + Combined body.
        /// </summary>
        public static IReadOnlyList<GearPiece>? TierArmorFor(Kind kind, Tier tier)
        {
            if (tier == Tier.Basic)
                return null;
            if (tier == Tier.Elite)
                return EliteOfabArmor(kind.Key);
            if (tier == Tier.Champion)
                return CombinedAlienArmor(kind.Key, includeHead: true);
            // Twink: Beast shoulders, helmet, back + Combined Alien for the rest (no Combined head).
            var body = CombinedAlienArmor(kind.Key, includeHead: false);
            var list = new List<GearPiece>(body.Count + 3)
            {
                new(BeastHelmetLow, BeastHelmetHigh),
                new(BeastShoulderLow, BeastShoulderHigh),
                new(BeastBackLow, BeastBackHigh)
            };
            list.AddRange(body);
            return list;
        }

        /// <summary>Tier hand weapon (look only; swing is still the monster weapon). Null = Mercs.json / basic mesh.</summary>
        public static GearPiece? TierWeaponFor(Kind kind, Tier tier)
        {
            if (tier == Tier.Basic)
                return null;
            if (tier is Tier.Elite or Tier.Twink)
                return OfabMk6Weapon(kind.Key);
            return KyrAlienWeapon(kind.Key);
        }

        static List<GearPiece> EliteOfabArmor(string key) => key switch
        {
            // Penultimate Ofab pairs verified in items.dat (ofab-all-names / penult dump).
            "adventurer" =>
            [
                P(264184, 264185), P(264178, 264179), P(264180, 264181), P(264182, 264183), P(264174, 264175), P(264176, 264177)
            ],
            "agent" =>
            [
                P(264283, 264284), P(264301, 264302), P(264295, 264296), P(264289, 264290), P(264313, 264314), P(264307, 264308)
            ],
            "bureaucrat" =>
            [
                P(264499, 264500), P(264517, 264518), P(264511, 264512), P(264505, 264506), P(264529, 264530), P(264523, 264524)
            ],
            "doctor" =>
            [
                P(264646, 264647), P(264664, 264665), P(264658, 264659), P(264652, 264653), P(264676, 264677), P(264670, 264671)
            ],
            "enforcer" =>
            [
                P(264245, 264246), P(264227, 264228), P(264233, 264234), P(264239, 264240), P(264215, 264216), P(264221, 264222)
            ],
            "engineer" =>
            [
                P(264571, 264572), P(264592, 264593), P(264583, 264584), P(264577, 264578), P(264604, 264605), P(264598, 264599)
            ],
            "fixer" =>
            [
                P(264463, 264464), P(264481, 264482), P(264475, 264476), P(264469, 264470), P(264493, 264494), P(264487, 264488)
            ],
            "keeper" =>
            [
                P(264610, 264611), P(264628, 264629), P(264622, 264623), P(264616, 264617), P(264640, 264641), P(264634, 264635)
            ],
            "martialartist" =>
            [
                P(264319, 264320), P(264337, 264338), P(264331, 264332), P(264325, 264326), P(264349, 264350), P(264343, 264344)
            ],
            "metaphysicist" =>
            [
                P(264355, 264356), P(264373, 264374), P(264367, 264368), P(264361, 264362), P(264385, 264386), P(264379, 264380)
            ],
            "nanotechnician" =>
            [
                P(264391, 264392), P(264409, 264410), P(264403, 264404), P(264397, 264398), P(264421, 264422), P(264415, 264416)
            ],
            "shade" =>
            [
                P(264535, 264536), P(264553, 264554), P(264547, 264548), P(264541, 264542), P(264565, 264566), P(264559, 264560)
            ],
            "soldier" =>
            [
                P(264427, 264428), P(264445, 264446), P(264439, 264440), P(264433, 264434), P(264457, 264458), P(264451, 264452)
            ],
            "trader" =>
            [
                P(264247, 264248), P(264265, 264266), P(264259, 264260), P(264253, 264254), P(264271, 264272), P(264277, 264278)
            ],
            _ => []
        };

        /// <summary>
        /// Combined Alien armor families (only six exist). Mapped to closest profession line.
        /// Missing Soft/Hard Cap of the Beast style alien shoulders: not in this items.dat (listed in design doc).
        /// </summary>
        static List<GearPiece> CombinedAlienArmor(string key, bool includeHead)
        {
            // Commando / Mercenary / Paramedic / Scout / Sharpshooter / Officer.
            List<GearPiece> pieces = key switch
            {
                "soldier" => CombinedCommando(includeHead),
                "enforcer" or "martialartist" or "shade" => CombinedMercenary(includeHead),
                "doctor" => CombinedParamedic(includeHead),
                "adventurer" or "fixer" => CombinedScout(includeHead),
                "agent" => CombinedSharpshooter(includeHead),
                _ => CombinedOfficer(includeHead) // crat, engi, keeper, mp, nt, trader
            };
            return pieces;
        }

        static List<GearPiece> CombinedCommando(bool head) => head
            ? [P(246657, 246658), P(246659, 246660), P(246661, 246662), P(246663, 246664), P(246665, 246666), P(246667, 246668)]
            : [P(246659, 246660), P(246661, 246662), P(246663, 246664), P(246665, 246666), P(246667, 246668)];

        static List<GearPiece> CombinedMercenary(bool head) => head
            ? [P(246635, 246636), P(246637, 246638), P(246639, 246640), P(246641, 246642), P(246643, 246644), P(246645, 246646)]
            : [P(246637, 246638), P(246639, 246640), P(246641, 246642), P(246643, 246644), P(246645, 246646)];

        static List<GearPiece> CombinedParamedic(bool head) => head
            ? [P(246631, 246632), P(246647, 246648), P(246649, 246650), P(246651, 246652), P(246653, 246654), P(246655, 246656)]
            : [P(246647, 246648), P(246649, 246650), P(246651, 246652), P(246653, 246654), P(246655, 246656)];

        static List<GearPiece> CombinedScout(bool head) => head
            ? [P(246681, 246682), P(246683, 246684), P(246685, 246686), P(246687, 246688), P(246689, 246690), P(246691, 246692)]
            : [P(246683, 246684), P(246685, 246686), P(246687, 246688), P(246689, 246690), P(246691, 246692)];

        static List<GearPiece> CombinedSharpshooter(bool head) => head
            ? [P(246693, 246694), P(246695, 246696), P(246697, 246698), P(246699, 246700), P(246701, 246702), P(246703, 246704)]
            : [P(246695, 246696), P(246697, 246698), P(246699, 246700), P(246701, 246702), P(246703, 246704)];

        static List<GearPiece> CombinedOfficer(bool head) => head
            ? [P(246669, 246670), P(246671, 246672), P(246673, 246674), P(246675, 246676), P(246677, 246678), P(246679, 246680)]
            : [P(246671, 246672), P(246673, 246674), P(246675, 246676), P(246677, 246678), P(246679, 246680)];

        static GearPiece OfabMk6Weapon(string key) => key switch
        {
            "soldier" => P(265097, 265103),       // Shark
            "agent" => P(265055, 265061),         // Peregrine
            "enforcer" => P(265272, 265278),      // Bear
            "martialartist" => P(264929, 264935), // Mongoose
            "adventurer" => P(265139, 265145),    // Tiger
            "fixer" => P(264892, 264898),         // Silverback
            "keeper" => P(265230, 265236),        // Wolf
            "shade" => P(264971, 264977),         // Panther
            "engineer" => P(265314, 265320),      // Cobra
            _ => P(265314, 265320)                // Cobra for casters / trader / doc / nt / mp / crat
        };

        static GearPiece KyrAlienWeapon(string key) => key switch
        {
            "soldier" => P(254507, 254513),       // Carbine Type 13
            "agent" => P(254479, 254485),         // Rifle Type 3
            "enforcer" => P(254731, 254737),      // Sledgehammer Type 76
            "martialartist" => P(288665, 288671), // Nunchacko Type 48
            "adventurer" => P(254689, 254695),    // Axe Type 240
            "fixer" => P(254535, 254541),         // SMG Type 12
            "engineer" => P(254570, 254576),      // Grenade Gun Type 1
            "shade" => P(254780, 254786),         // Spear Type 112
            "keeper" => P(254745, 254751),        // Energy Sword Type 76
            "doctor" => P(254640, 254646),        // Energy Pistol Type 5
            _ => P(254626, 254632)                // Pistol Type 4
        };

        static GearPiece P(int low, int high) => new(low, high);

        /// <summary>
        /// Profession nanos uploaded for this tier. Cumulative: basic &lt; elite &lt; champion &lt; twink.
        /// IDs are castable nano programs (Ofab crystal OnUse targets + classic specials/heals), verified in items.dat.
        /// </summary>
        public static IReadOnlyList<int> TierNanosFor(Kind kind, Tier tier)
        {
            int[] basic = ProfessionNanoBasic(kind.Key);
            int[] elite = ProfessionNanoElite(kind.Key);
            int[] champion = ProfessionNanoChampion(kind.Key);
            int[] twink = ProfessionNanoTwink(kind.Key);
            return tier switch
            {
                Tier.Twink => MergeNanos(basic, elite, champion, twink),
                Tier.Champion => MergeNanos(basic, elite, champion),
                Tier.Elite => MergeNanos(basic, elite),
                _ => basic
            };
        }

        static int[] MergeNanos(params int[][] bands)
        {
            var list = new List<int>();
            var seen = new HashSet<int>();
            foreach (int[] band in bands)
            {
                foreach (int id in band)
                {
                    if (id > 0 && seen.Add(id))
                        list.Add(id);
                }
            }

            return list.ToArray();
        }

        // Classic profession signatures + Ofab programs (cast IDs verified in items.dat).
        // Beneficial nanos target owner/team via NpcSupportRules; offensive only fight targets.
        // Tier lists are cumulative; NanoCastQuality (gear QL) interpolates two-id QL pairs.
        static int[] ProfessionNanoBasic(string key) => key switch
        {
            // Team Reflective Field + Damage Shield + Total Focus + specials/taunt.
            "soldier" => [82208, 82209, 82211, 82215, 227152, 70312, 70254, 29255],
            // Taunt / Mongo / self absorb + low Essence HP. (No classic team buff line.)
            "enforcer" => [82210, 211753, 261239, 227152, 100195, 75412, 95707],
            // Big/team heal + HoT + team HP (Enlarge / Iron Circle).
            "doctor" => [26465, 26466, 42405, 28651, 42399, 28658, 42400],
            // Heals / HoT / damage shield + specials.
            "adventurer" => [82208, 82215, 26465, 26466, 28651, 70254],
            // Aimed shot / crit starter + False Profession (Soldier).
            "agent" => [82215, 129082, 82208, 81856, 32038],
            // Calm + XP buff (Wit of the Atrox) + robot-policy / pet starters.
            "bureaucrat" => [267538, 279340, 267603, 100442, 26707, 252506],
            // Team armor (Sympathetic Armor Boost) + blind + low robot pet.
            "engineer" => [266309, 267606, 83954, 154970, 154567],
            // NCU + HoT + team Grid + evade/snare starters + specials.
            "fixer" => [162994, 162714, 162582, 56226, 82208, 82211],
            // Heal/nano/damage auras (Ambient / Tone / Vengeance) + Courage. Live aura lockout → one heal/nano slot.
            "keeper" => [279378, 266301, 210530, 210591, 210611],
            // Self heal + special attacks.
            "martialartist" => [82210, 82213, 82214, 26465],
            // Team nano-skill buffs (Mochams / Composite Nano) + scourges.
            "metaphysicist" => [267275, 267272, 266299, 29299, 29301, 223380],
            // Nuke + Nullity + team nano HoT + NCU shelter. (Classic nano-cost line not in items.dat.)
            "nanotechnician" => [28619, 150501, 266302, 150630, 43833, 263265],
            // Drain heal-self (Caress elite+) + sneak / specials. (Spirit procs not in items.dat.)
            "shade" => [129082, 82214, 82213, 230467],
            // Weak skill/AC drains + team wrangle starters (owner/team).
            "trader" => [301533, 301525, 280050, 121218, 121505, 91139],
            _ => []
        };

        static int[] ProfessionNanoElite(string key) => key switch
        {
            // Team Reactive Reflect + Aggression Multiplier (team damage).
            "soldier" => [266310, 267624, 301897, 70314, 83919],
            "enforcer" => [266304, 279347, 287753, 75407, 95702, 29652],
            // Heal-delta (Instinctive Control) + heals/DoTs.
            "doctor" => [28665, 28676, 266303, 267649, 279372, 28669, 95707],
            "adventurer" => [28665, 266306, 301835, 302223, 222824, 85054],
            "agent" => [266307, 82211, 275007],
            "bureaucrat" => [267535, 267604, 267611, 266305, 259367],
            "engineer" => [267607, 302254, 83960, 154976],
            "fixer" => [163005, 162732, 85234, 162586, 56221, 266308, 279374, 279376],
            "keeper" => [301153, 216603, 210532, 210593, 210613],
            "martialartist" => [266311, 287559, 279345, 28665],
            "metaphysicist" => [267269, 267271, 267598, 29300, 29302, 154980],
            "nanotechnician" => [266298, 266293, 266292, 150502, 150628, 43905, 301610],
            "shade" => [266300],
            "trader" => [301529, 301521, 266312, 301899, 121228, 121497, 91133],
            _ => []
        };

        static int[] ProfessionNanoChampion(string key) => key switch
        {
            "soldier" => [302395, 70324, 70251, 270806],
            "enforcer" => [301853, 75403, 95708],
            "doctor" => [28650, 267620, 301845, 222824, 43868, 43873, 95708, 270349],
            "adventurer" => [28676, 302235, 302229, 302217, 70251, 85052],
            "agent" => [301838],
            "bureaucrat" => [267605, 267311, 222687, 267612],
            "engineer" => [267608, 274369, 154974],
            "fixer" => [163012, 162734, 85227, 162587, 82502, 274373, 301861, 301859],
            "keeper" => [301602, 280496, 210534, 210595, 210617],
            "martialartist" => [301936, 301493],
            "metaphysicist" => [267600, 267601, 267599, 301888, 29303, 29304, 154983],
            "nanotechnician" => [266297, 266296, 266294, 302274, 150621, 43811],
            "shade" => [301593],
            "trader" => [267121, 267264, 267114, 121219, 121500, 91125],
            _ => []
        };

        static int[] ProfessionNanoTwink(string key) => key switch
        {
            "soldier" => [302399, 233020],
            "enforcer" => [301291, 75401, 273629],
            "doctor" => [267623, 267621, 222856, 273629, 273315],
            "adventurer" => [28650, 302243, 217672],
            "agent" => [82209],
            "bureaucrat" => [267616, 267916, 302247, 302154, 267917],
            "engineer" => [301855, 154972],
            "fixer" => [163057, 270352, 273352, 162588, 273355, 56219, 302412, 301867],
            "keeper" => [301117, 210528, 210589, 210621],
            "martialartist" => [100195, 225615],
            "metaphysicist" => [267609, 267617, 302257, 300506, 267281, 267283, 154981],
            "nanotechnician" => [218168, 266287, 302074, 266289],
            "shade" => [301895],
            "trader" => [267117, 267266, 267110, 302288, 302401, 302290, 121215, 273410, 91123],
            _ => []
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