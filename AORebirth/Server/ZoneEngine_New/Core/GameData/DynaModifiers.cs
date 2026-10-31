namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Dyna (dynamic boss) modifier buffs, after athenpaladins.org t=2166: each dyna gets one modifier that sets its aura
    /// colour. Elemental ones switch damage type and add matching AC + reflect; Crit, DamageShield and Big change other
    /// stats. Every dyna is drawn larger than normal mobs. Aura = EffectType + EffectRed/Green/Blue stats (GUESS: the
    /// client's EffectType value for a coloured glow is unconfirmed; see PROGRESS.md).
    /// </summary>
    public static class DynaModifiers
    {
        /// <summary>GUESS: EffectType value that makes the client draw the coloured aura.</summary>
        public static int AuraEffectType = 1;

        /// <summary>Every dyna: base size multiplier over its template scale.</summary>
        public const int DynaScalePercent = 150;

        /// <summary>
        /// One modifier. <see cref="GlowNano"/> is the OnWear CastNano from the live Boss Modifier Item
        /// (items.dat: Fire = 160084 "Red glow", whose only function is GfxEffect). <see cref="ContainerId"/>
        /// is an existing client item that uses icon 20407, the spongy/muddy container look (same icon as
        /// Spongy Novictum and Notum Container). The client shows that item's own name; using it tells the
        /// player which aura it unlocked.
        /// </summary>
        public sealed record Modifier(string Name, int R, int G, int B, CharacterStat? Ac, CharacterStat? Reflect, int ScalePercent, int BossItem, int GlowNano, int ContainerId, string ContainerName);

        public static readonly Modifier[] All =
        [
            new("Chemical", 0, 255, 0, CharacterStat.ChemicalAC, CharacterStat.ReflectChemicalAC, 100, 164346, 160083, 218474, "Spongy Novictum"),
            new("Cold", 255, 255, 255, CharacterStat.ColdAC, CharacterStat.ReflectColdAC, 100, 164301, 160082, 218472, "Novictum Mud"),
            new("Energy", 0, 64, 255, CharacterStat.EnergyAC, CharacterStat.ReflectEnergyAC, 100, 164337, 160086, 218468, "Transparent Novictum Gas"),
            new("Fire", 255, 0, 0, CharacterStat.FireAC, CharacterStat.ReflectFireAC, 100, 164328, 160084, 218476, "Caustic Novictum"),
            new("Poison", 0, 200, 200, CharacterStat.PoisonAC, CharacterStat.ReflectPoisonAC, 100, 164356, 160085, 218475, "Plastic Novictum"),
            new("Radiation", 160, 0, 255, CharacterStat.RadiationAC, CharacterStat.ReflectRadiationAC, 100, 164319, 160081, 218469, "Radiant Novictum Gas"),
            new("Crit", 210, 180, 140, null, null, 160, 164365, 160073, 218473, "Compact Novictum Stone"),
            new("Shield", 255, 140, 0, null, null, 140, 164292, 160077, 100354, "Radioactive Isotope Container"),
            new("Big", 255, 255, 0, null, null, 130, 164283, 160074, 225998, "Dangerous Container"),
        ];

        public const string BossItemPrefix = "Boss Modifier Item";

        /// <summary>Chance that one of a dyna's 5–7 drops is replaced by its colour's container.</summary>
        public const int ContainerDropPercent = 20;

        public static bool IsDyna(NpcLevelBand band)
        {
            // Dungeon Dogs: live-scraped NPCs carry live's own Scale, stats and look. The dyna CampKey they inherited from a
            // donor made them dynas here (size x1.5-2.4, aura, AC/crit hardening, Boss Modifier Item): the Rome Park Unicorn
            // Commander drew twice live's height (Randy 2026-10-30). Hand-made dynas are unchanged.
            if (band.LiveScraped)
                return false;
            if (!string.IsNullOrEmpty(band.DynaModifier))
                return true;
            string? key = band.ContentProvenance?.CampKey;
            if (string.IsNullOrEmpty(key))
                return false;
            // CampKey stays zone| so Build-DynaLoot's boss| pass does not replace the loot table.
            // Adds use zone-add| and must not match.
            return key.StartsWith("boss|", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("elysium|", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("scheol|", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("adonis|", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("penumbra|", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("inferno|", StringComparison.OrdinalIgnoreCase)
                // Nascence dynas (49 camps) were left out, so they had no aura or dyna size (Randy 2026-10-17).
                || key.StartsWith("nascence|", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Applies the band's named modifier, or a random one, to freshly materialized stats.</summary>
        public static string? Apply(NpcLevelBand band, Dictionary<int, int> stats, Random? random = null)
        {
            if (!IsDyna(band))
                return null;
            Modifier mod = Array.Find(All, m => string.Equals(m.Name, band.DynaModifier, StringComparison.OrdinalIgnoreCase))
                ?? All[(random ?? Random.Shared).Next(All.Length)];
            int level = Math.Max(1, Get(stats, CharacterStat.Level));

            int scale = Get(stats, CharacterStat.Scale);
            if (scale <= 0) scale = 100;
            Set(stats, CharacterStat.Scale, Math.Min(short.MaxValue, scale * DynaScalePercent / 100 * mod.ScalePercent / 100));

            if (mod.Ac is CharacterStat ac)
            {
                Set(stats, CharacterStat.DamageType, (int)ac);
                Set(stats, ac, Get(stats, ac) + level * 10);
                Set(stats, mod.Reflect!.Value, Math.Max(Get(stats, mod.Reflect.Value), 25));
            }
            else if (mod.Name == "Crit")
            {
                Set(stats, CharacterStat.CriticalIncrease, Get(stats, CharacterStat.CriticalIncrease) + 50);
                // Initiative debuff on the boss's targets is not modelled; the boss gets fast init instead.
                Set(stats, CharacterStat.MeleeInit, Get(stats, CharacterStat.MeleeInit) + level * 5);
            }
            else if (mod.Name == "Shield")
            {
                int shield = Math.Max(5, level / 2);
                foreach (CharacterStat s in new[] { CharacterStat.ShieldProjectileAC, CharacterStat.ShieldMeleeAC, CharacterStat.ShieldEnergyAC,
                    CharacterStat.ShieldChemicalAC, CharacterStat.ShieldRadiationAC, CharacterStat.ShieldColdAC, CharacterStat.ShieldFireAC, CharacterStat.ShieldPoisonAC })
                    Set(stats, s, Get(stats, s) + shield);
            }
            else if (mod.Name == "Big")
            {
                foreach (CharacterStat s in new[] { CharacterStat.MaxHealth, CharacterStat.Health, (CharacterStat)27 })
                    if (stats.ContainsKey((int)s)) Set(stats, s, (int)Math.Min(int.MaxValue, Get(stats, s) * 3L));
                Set(stats, CharacterStat.DamageBonus, Get(stats, CharacterStat.DamageBonus) + level * 3);
                Set(stats, CharacterStat.AttackSpeed, Math.Max(Get(stats, CharacterStat.AttackSpeed), 100) * 2);
                int run = Get(stats, CharacterStat.RunSpeed);
                if (run > 0) Set(stats, CharacterStat.RunSpeed, run / 2);
            }

            Set(stats, CharacterStat.EffectType, AuraEffectType);
            Set(stats, CharacterStat.EffectRed, mod.R);
            Set(stats, CharacterStat.EffectGreen, mod.G);
            Set(stats, CharacterStat.EffectBlue, mod.B);
            return mod.Name;
        }

        /// <summary>
        /// The live "Boss Modifier Item (...)" for a modifier at this level (9 QL tiers per kind, ids from itemnames).
        /// Live dynas wear one; it carries the modifier's own effects. GUESS: tier = level / 25, capped at 8.
        /// </summary>
        public static int BossItemFor(string modifierName, int level)
        {
            Modifier? mod = Find(modifierName);
            if (mod == null)
                return 0;

            int id = mod.BossItem + Math.Clamp(level / 25, 0, 8);
            // 164349 is Metallic Mantis Body Armor, sitting in the chemical tier list.
            return id == 164349 ? 164348 : id;
        }

        public static Modifier? Find(string? name)
            => string.IsNullOrEmpty(name)
                ? null
                : Array.Find(All, m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

        public static int IndexOf(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        public static Modifier? ByContainer(int aoid)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].ContainerId == aoid)
                    return All[i];
            }

            return null;
        }

        public static bool IsGlowNano(int nanoId)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].GlowNano == nanoId)
                    return true;
            }

            return false;
        }

        static int Get(Dictionary<int, int> s, CharacterStat k) => s.TryGetValue((int)k, out int v) ? v : 0;
        static void Set(Dictionary<int, int> s, CharacterStat k, int v) => s[(int)k] = v;
    }

    public sealed class NpcContentProvenance
    {
        public string? CampKey { get; set; }
    }
}
