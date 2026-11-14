#nullable enable

namespace ZoneEngine_New.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Optional per-weapon Fling Shot area blast, data-driven from
    /// <c>GameData/WeaponFlingEffects.json</c>. Only weapons listed in that file get an effect;
    /// every other Fling weapon is unchanged. Used by the Neutrino Eruptor (and later Soldier /
    /// boss variants) so the blast can exclude the primary target, require line of sight, and
    /// cap the number of secondary targets.
    /// </summary>
    public static class WeaponFlingEffects
    {
        public const string FileName = "WeaponFlingEffects.json";

        /// <summary>Default / documented secondary-target cap when a row omits MaxTargets.</summary>
        public const int DefaultMaxTargets = 8;

        sealed class Document
        {
            public List<EffectRow> Effects { get; set; } = [];
        }

        sealed class EffectRow
        {
            public string? Name { get; set; }
            public int LowId { get; set; }
            public int HighId { get; set; }
            public int RadiusMeters { get; set; } = 15;
            public int MaxTargets { get; set; } = DefaultMaxTargets;
            public bool RequireLineOfSight { get; set; } = true;
            public bool ExcludePrimaryTarget { get; set; } = true;
            public int Stat { get; set; } = (int)CharacterStat.Health;
            public int AcStat { get; set; } = (int)CharacterStat.FireAC;
            public List<DamageAnchor> DamageByQl { get; set; } = [];
        }

        sealed class DamageAnchor
        {
            public int Ql { get; set; }
            public int Min { get; set; }
            public int Max { get; set; }
        }

        sealed class LoadedEffect
        {
            public required EffectRow Row { get; init; }
            public required (int Ql, int Min, int Max)[] Anchors { get; init; }
        }

        static readonly object Gate = new();
        static string? _loadedFrom;
        static LoadedEffect[] _effects = [];

        /// <summary>
        /// After a Fling Shot hit lands, apply any configured area blast for the weapon template.
        /// No-op when the weapon is not listed or when no secondary targets qualify.
        /// </summary>
        public static void TryApply(Character attacker, Character primary, Item? weapon)
        {
            if (attacker == null || primary == null || weapon == null || primary.IsDead)
                return;

            string? root = attacker.Playfield?.GetService<IGameData>()?.RootPath;
            if (string.IsNullOrEmpty(root))
                return;

            LoadedEffect? effect = Find(root, weapon.LowId, weapon.HighId);
            if (effect == null)
                return;

            EffectRow row = effect.Row;
            if (row.Stat != (int)CharacterStat.Health || row.RadiusMeters < 0)
                return;

            (int minHit, int maxHit) = DamageAt(effect.Anchors, weapon.Quality);
            if (maxHit >= 0)
                return;
            if (minHit > maxHit)
                (minHit, maxHit) = (maxHit, minHit);

            Playfield? playfield = primary.Playfield;
            DynelRegistry? registry = playfield?.GetService<DynelRegistry>();
            if (registry == null)
                return;

            int cap = row.MaxTargets > 0 ? row.MaxTargets : DefaultMaxTargets;
            int hit = 0;
            foreach (Dynel dynel in registry.Dynels())
            {
                if (hit >= cap)
                    break;
                if (dynel is not Character candidate || candidate.IsDead)
                    continue;
                if (row.ExcludePrimaryTarget && ReferenceEquals(candidate, primary))
                    continue;
                if (ReferenceEquals(candidate, attacker))
                    continue;
                if (!ReferenceEquals(candidate.Playfield, playfield))
                    continue;
                if (primary.Distance3D(candidate) > row.RadiusMeters)
                    continue;
                if (!CombatRules.CanAttack(attacker, candidate))
                    continue;
                if (row.RequireLineOfSight && !HasLos(primary, candidate) && !HasLos(attacker, candidate))
                    continue;

                int delta = minHit == maxHit
                    ? minHit
                    : Random.Shared.Next(minHit, maxHit + 1);
                // Reuse AreaHit's hostile Health path (Add Damage for the AC type).
                ItemUseFunctions.ApplyFlingAreaDelta(candidate, attacker, delta, row.AcStat);
                hit++;
            }
        }

        static bool HasLos(Character from, Character to)
            => from.HasLineOfSightTo(to);

        static LoadedEffect? Find(string root, int lowId, int highId)
        {
            foreach (LoadedEffect effect in Load(root))
            {
                EffectRow row = effect.Row;
                int lo = Math.Min(row.LowId, row.HighId);
                int hi = Math.Max(row.LowId, row.HighId);
                if (lowId >= lo && lowId <= hi && highId >= lo && highId <= hi)
                    return effect;
            }

            return null;
        }

        static (int Min, int Max) DamageAt((int Ql, int Min, int Max)[] anchors, int quality)
        {
            if (anchors.Length == 0)
                return (0, 0);
            if (quality <= anchors[0].Ql)
                return (anchors[0].Min, anchors[0].Max);
            if (quality >= anchors[^1].Ql)
                return (anchors[^1].Min, anchors[^1].Max);

            int upper = 1;
            while (anchors[upper].Ql <= quality)
                upper++;
            var low = anchors[upper - 1];
            var high = anchors[upper];
            float t = (quality - low.Ql) / (float)(high.Ql - low.Ql);
            return (Lerp(low.Min, high.Min, t), Lerp(low.Max, high.Max, t));
        }

        static int Lerp(int a, int b, float t)
            => a + (int)Math.Round((b - a) * t);

        static LoadedEffect[] Load(string root)
        {
            string path = Path.Combine(root, FileName);
            lock (Gate)
            {
                if (_loadedFrom != null && string.Equals(_loadedFrom, path, StringComparison.OrdinalIgnoreCase))
                    return _effects;

                _loadedFrom = path;
                _effects = [];
                if (!File.Exists(path))
                    return _effects;

                try
                {
                    Document? doc = JsonSerializer.Deserialize<Document>(File.ReadAllText(path));
                    if (doc?.Effects == null || doc.Effects.Count == 0)
                        return _effects;

                    var loaded = new List<LoadedEffect>(doc.Effects.Count);
                    foreach (EffectRow row in doc.Effects)
                    {
                        if (row.LowId <= 0 || row.HighId <= 0 || row.DamageByQl == null || row.DamageByQl.Count == 0)
                            continue;
                        var anchors = new List<(int, int, int)>(row.DamageByQl.Count);
                        foreach (DamageAnchor a in row.DamageByQl)
                            anchors.Add((a.Ql, a.Min, a.Max));
                        anchors.Sort((x, y) => x.Item1.CompareTo(y.Item1));
                        loaded.Add(new LoadedEffect { Row = row, Anchors = anchors.ToArray() });
                    }

                    _effects = loaded.ToArray();
                }
                catch
                {
                    _effects = [];
                }

                return _effects;
            }
        }

        /// <summary>Test helper: clear the cached catalog so a new GameData root is re-read.</summary>
        internal static void ResetCacheForTests()
        {
            lock (Gate)
            {
                _loadedFrom = null;
                _effects = [];
            }
        }
    }
}
