namespace ZoneEngine_New.Core.Tradeskills
{
    using System;

    /// <summary>
    /// One source + target combine. GameData/Tradeskills.json (hand-made, e.g. the alien armor) and its
    /// Tradeskills.*.json overlays win over GameData/Tradeskills.cellao.json, converted from the CellAO dump by
    /// ddogs/tools/Convert-TradeskillDump.ps1.
    /// </summary>
    public sealed class TradeskillRecipe
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Source item ids (low or high id matches).</summary>
        public int[] Source { get; set; } = Array.Empty<int>();

        /// <summary>Target item ids (low or high id matches). The target's QL sets the skill needs and the result QL.</summary>
        public int[] Target { get; set; } = Array.Empty<int>();

        /// <summary>Result low/high ids; the result QL is the target's QL plus any bump, clamped to the result's QL range.</summary>
        public int[] Result { get; set; } = Array.Empty<int>();

        public bool DeleteSource { get; set; }

        public bool DeleteTarget { get; set; } = true;

        /// <summary>Source QL must be at least this percent of the target QL (alien armor 80% rule); 0 = any.</summary>
        public int MinSourcePct { get; set; }

        /// <summary>
        /// CellAO range rule: the source may be at most this percent below the target QL (18 = a Faded cluster down to
        /// 82% of the implant); 1 = the source must be at least the target QL; 0 = any.
        /// </summary>
        public int QlRangePercent { get; set; }

        /// <summary>The target must be at least this QL; 0 = any.</summary>
        public int MinTargetQl { get; set; }

        /// <summary>[stat id, percent of target QL, skill per QL bump (optional, 0 = no bump)], e.g. [160, 125, 100].</summary>
        public int[][] Skills { get; set; } = Array.Empty<int[]>();

        /// <summary>Most QL bumps one combine may give; 0 = none (implants also follow the QL table in TradeskillRules).</summary>
        public int MaxBump { get; set; }

        /// <summary>XP at the result's lowest and highest QL, interpolated by the result QL.</summary>
        public int MinXp { get; set; }

        public int MaxXp { get; set; }

        /// <summary>Cluster into implant: implant bump table, QL 200/300 caps, Jobe QL 99/100 rule.</summary>
        public bool Implant { get; set; }

        public int ResultLow => Result.Length > 0 ? Result[0] : 0;

        public int ResultHigh => Result.Length > 1 ? Result[1] : ResultLow;

        /// <summary>A Jobe-family cluster (Jobe, refined Jobe, Nano Delta): NP plus a second tradeskill.</summary>
        public bool IsJobeCluster => Implant && Skills.Length >= 2;
    }
}
