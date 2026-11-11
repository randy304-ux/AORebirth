namespace ZoneEngine_New.Core.Battlestations
{
    using System;

    /// <summary>
    /// Pure Battle Station score/reward helpers (live PF 4384 capture 2026-11-11).
    /// Tunables live in <c>GameData/BattleStations.json</c> via <see cref="Catalog"/>.
    /// </summary>
    public static class BattleStationScoring
    {
        /// <summary>Our match Owner[] uses 0=none, 1=Red, 2=Blue. Wire SendScore uses 0=Red, 1=Blue, 2=Neutral.</summary>
        public static int ToWireOwner(int matchOwner)
            => matchOwner switch
            {
                1 => 0,
                2 => 1,
                _ => 2
            };

        public static int RollVpReward(int minInclusive, int maxInclusive, Random? random = null)
        {
            int lo = Math.Min(minInclusive, maxInclusive);
            int hi = Math.Max(minInclusive, maxInclusive);
            Random rng = random ?? Random.Shared;
            return rng.Next(lo, hi + 1);
        }

        /// <summary>
        /// End-of-match PVP Solo/Team Score. Loss at ~100 contribution → ~83 with default LossScoreMult=0.83;
        /// wins use a higher multiplier so the same contribution clearly pays more.
        /// </summary>
        public static int ComputeEndPvpScore(
            int pointsEarned,
            int kills,
            bool won,
            double lossMult,
            double winMult,
            int killBonus)
        {
            int contribution = Math.Max(0, pointsEarned) + Math.Max(0, kills) * Math.Max(0, killBonus);
            if (contribution <= 0)
                contribution = 1;

            double mult = won ? winMult : lossMult;
            if (mult <= 0)
                mult = won ? 1.25 : 0.83;

            int score = (int)Math.Round(contribution * mult, MidpointRounding.AwayFromZero);
            return Math.Max(1, score);
        }

        /// <summary>
        /// Role pick from cumulative weights (Capper/Defender/Hunter/Flanker). Weights may be any non-negative ints.
        /// </summary>
        public static int PickRoleIndex(int capper, int defender, int hunter, int flanker, Random? random = null)
        {
            int c = Math.Max(0, capper);
            int d = Math.Max(0, defender);
            int h = Math.Max(0, hunter);
            int f = Math.Max(0, flanker);
            int total = c + d + h + f;
            if (total <= 0)
                return 2; // Hunter default

            int roll = (random ?? Random.Shared).Next(total);
            if (roll < c)
                return 0;
            roll -= c;
            if (roll < d)
                return 1;
            roll -= d;
            if (roll < h)
                return 2;
            return 3;
        }
    }
}
