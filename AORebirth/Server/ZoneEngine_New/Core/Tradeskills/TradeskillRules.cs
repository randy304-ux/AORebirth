namespace ZoneEngine_New.Core.Tradeskills
{
    using System;
    using System.Linq;

    /// <summary>
    /// Tradeskill numbers, kept free of game state so they can be tested. Sources: CellAO TradeSkillReceiver /
    /// TradeSkillEntry and the AO-Universe implant-creation guide (bump table, QL caps, Jobe rule).
    /// </summary>
    public static class TradeskillRules
    {
        /// <summary>Skill needed: percent of the target QL, rounded up (QL 100 Shiny 1h Blunt at 360% = 360).</summary>
        public static int Required(int percent, int targetQuality)
            => (int)Math.Ceiling(targetQuality * percent / 100.0 - 1e-9);

        /// <summary>Lowest source QL the recipe takes for this target QL (0 = any).</summary>
        public static int MinSourceQuality(TradeskillRecipe recipe, int targetQuality)
        {
            int min = 0;
            if (recipe.MinSourcePct > 0)
                min = (int)Math.Ceiling(targetQuality * recipe.MinSourcePct / 100.0 - 1e-9);
            if (recipe.QlRangePercent == 1)
                min = Math.Max(min, targetQuality);
            else if (recipe.QlRangePercent > 1)
                min = Math.Max(min, (int)Math.Ceiling(targetQuality * (100 - recipe.QlRangePercent) / 100.0 - 1e-9));
            return min;
        }

        public static bool SourceInRange(TradeskillRecipe recipe, int sourceQuality, int targetQuality)
            => sourceQuality >= MinSourceQuality(recipe, targetQuality);

        public static bool TargetAllowed(TradeskillRecipe recipe, int targetQuality)
            => recipe.MinTargetQl <= 0 || targetQuality >= recipe.MinTargetQl;

        /// <summary>
        /// Implant bump ceiling by implant QL (AO-Universe): 1-49 none, 50-99 one, 100-149 two, 150-199 three,
        /// 201-249 four, 250-299 five. QL 200 and 300 are already at their cap.
        /// </summary>
        public static int ImplantMaxBump(int quality)
        {
            if (quality >= 300 || quality == 200) return 0;
            if (quality >= 250) return 5;
            if (quality >= 201) return 4;
            if (quality >= 150) return 3;
            if (quality >= 100) return 2;
            if (quality >= 50) return 1;
            return 0;
        }

        /// <summary>Highest QL a combine may reach before the result item's own range: implants stop at 200 (basic) or 300 (refined).</summary>
        public static int QualityCap(TradeskillRecipe recipe, int targetQuality)
            => recipe.Implant ? (targetQuality <= 200 ? 200 : 300) : int.MaxValue;

        /// <summary>
        /// QL bumps from skill above the requirement: for each skill with a per-bump value, one QL per full step
        /// above the requirement; the weakest skill decides. Capped by MaxBump and, for implants, the QL table.
        /// </summary>
        public static int Bumps(TradeskillRecipe recipe, int targetQuality, Func<int, int> skill)
        {
            int ceiling = recipe.MaxBump > 0 ? recipe.MaxBump : 0;
            if (recipe.Implant)
                ceiling = recipe.MaxBump > 0 ? Math.Min(recipe.MaxBump, ImplantMaxBump(targetQuality)) : ImplantMaxBump(targetQuality);
            if (ceiling <= 0)
                return 0;

            int bumps = int.MaxValue;
            foreach (int[] entry in recipe.Skills.Where(entry => entry.Length >= 3 && entry[2] > 0))
            {
                int over = skill(entry[0]) - Required(entry[1], targetQuality);
                bumps = Math.Min(bumps, over <= 0 ? 0 : over / entry[2]);
            }

            if (bumps == int.MaxValue)
                return 0;
            return Math.Clamp(bumps, 0, ceiling);
        }

        /// <summary>First skill short of its requirement, if any.</summary>
        public static bool TryFindMissingSkill(TradeskillRecipe recipe, int targetQuality, Func<int, int> skill,
            out int stat, out int need, out int have)
        {
            foreach (int[] entry in recipe.Skills.Where(entry => entry.Length >= 2))
            {
                need = Required(entry[1], targetQuality);
                have = skill(entry[0]);
                if (have < need)
                {
                    stat = entry[0];
                    return true;
                }
            }

            stat = need = have = 0;
            return false;
        }

        /// <summary>
        /// Result QL range shown by the window's slider: from the target QL up to target + bumps, inside the result
        /// item's QL range and the implant cap.
        /// </summary>
        public static (int Min, int Max) ResultRange(TradeskillRecipe recipe, int targetQuality, int bumps,
            int resultLowQuality, int resultHighQuality)
        {
            int floor = Math.Max(1, resultLowQuality);
            int top = Math.Min(resultHighQuality > 0 ? resultHighQuality : int.MaxValue, QualityCap(recipe, targetQuality));
            int min = Math.Clamp(targetQuality, floor, Math.Max(floor, top));
            int max = Math.Clamp(targetQuality + Math.Max(0, bumps), min, Math.Max(min, top));
            return (min, max);
        }

        /// <summary>The slider pick: 0 or anything outside the range takes the top of the range (drag-and-drop gives the full bump).</summary>
        public static int PickQuality(int desired, (int Min, int Max) range)
            => desired >= range.Min && desired <= range.Max ? desired : range.Max;

        /// <summary>
        /// Jobe clusters (and Nano Delta) go only into QL 99+ implants, and QL 99 only when the combine bumps it to
        /// 100 (AO-Universe). Refined ones are on refined (201+) implants anyway.
        /// </summary>
        public static bool JobeAllowed(TradeskillRecipe recipe, int targetQuality, int resultQuality)
            => !recipe.IsJobeCluster || (targetQuality >= 99 && resultQuality >= 100);

        /// <summary>CellAO: XP runs linearly from MinXp at the result's lowest QL to MaxXp at its highest.</summary>
        public static int Xp(TradeskillRecipe recipe, int quality, int resultLowQuality, int resultHighQuality)
        {
            if (recipe.MaxXp <= 0 && recipe.MinXp <= 0)
                return 0;
            if (resultHighQuality <= resultLowQuality)
                return recipe.MaxXp;
            double t = Math.Clamp((quality - resultLowQuality) / (double)(resultHighQuality - resultLowQuality), 0, 1);
            return (int)Math.Floor(recipe.MinXp + (recipe.MaxXp - recipe.MinXp) * t);
        }
    }
}
