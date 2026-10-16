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

        public sealed record Modifier(string Name, int R, int G, int B, CharacterStat? Ac, CharacterStat? Reflect, int ScalePercent);

        public static readonly Modifier[] All =
        [
            new("Chemical", 0, 255, 0, CharacterStat.ChemicalAC, CharacterStat.ReflectChemicalAC, 100),
            new("Cold", 255, 255, 255, CharacterStat.ColdAC, CharacterStat.ReflectColdAC, 100),
            new("Energy", 0, 64, 255, CharacterStat.EnergyAC, CharacterStat.ReflectEnergyAC, 100),
            new("Fire", 255, 0, 0, CharacterStat.FireAC, CharacterStat.ReflectFireAC, 100),
            new("Poison", 0, 200, 200, CharacterStat.PoisonAC, CharacterStat.ReflectPoisonAC, 100),
            new("Radiation", 160, 0, 255, CharacterStat.RadiationAC, CharacterStat.ReflectRadiationAC, 100),
            new("Crit", 210, 180, 140, null, null, 160),
            new("Shield", 255, 140, 0, null, null, 140),
            new("Big", 255, 255, 0, null, null, 130),
        ];

        public static bool IsDyna(NpcLevelBand band)
            => !string.IsNullOrEmpty(band.DynaModifier)
                || (band.ContentProvenance?.CampKey?.StartsWith("boss|", StringComparison.OrdinalIgnoreCase) ?? false);

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

        static int Get(Dictionary<int, int> s, CharacterStat k) => s.TryGetValue((int)k, out int v) ? v : 0;
        static void Set(Dictionary<int, int> s, CharacterStat k, int v) => s[(int)k] = v;
    }

    public sealed class NpcContentProvenance
    {
        public string? CampKey { get; set; }
    }
}
