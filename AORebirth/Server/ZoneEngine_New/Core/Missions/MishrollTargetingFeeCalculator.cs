namespace ZoneEngine_New.Core.Missions
{
    using System;

    using ZoneEngine_New.Core.Configuration;

    /// <summary>
    /// QL-scaled mishroll accept fee: 0 through free QL, linear to <see cref="GameplayMishrollTargetingFee.FullFeeCredits"/>
    /// at full QL (already the 50%-of-handoff peak), B&amp;E discount, round half up once on the base then once after discount.
    /// </summary>
    public static class MishrollTargetingFeeCalculator
    {
        public static int Compute(int missionQuality, int breakAndEntryEffective, GameplayMishrollTargetingFee fee)
        {
            ArgumentNullException.ThrowIfNull(fee);
            if (missionQuality <= fee.FreeThroughQuality)
                return 0;
            if (fee.FullFeeCredits <= 0)
                return 0;

            int span = fee.FullFeeQuality - fee.FreeThroughQuality;
            if (span <= 0)
                return 0;

            int steps = Math.Min(missionQuality, fee.FullFeeQuality) - fee.FreeThroughQuality;
            // FullFeeCredits is the charged peak (50% of the handoff curve already applied in config).
            double raw = (double)fee.FullFeeCredits * steps / span;
            int baseFee = RoundHalfUp(raw);
            if (baseFee > fee.FullFeeCredits)
                baseFee = fee.FullFeeCredits;
            if (baseFee <= 0)
                return 0;

            int be = Math.Max(0, breakAndEntryEffective);
            int discountPercent = 0;
            if (fee.BreakAndEntryDiscountPercentPerTen > 0 && be >= 10)
            {
                discountPercent = (be / 10) * fee.BreakAndEntryDiscountPercentPerTen;
                if (discountPercent > fee.BreakAndEntryDiscountCapPercent)
                    discountPercent = fee.BreakAndEntryDiscountCapPercent;
            }

            if (discountPercent <= 0)
                return baseFee;

            double discounted = baseFee * (100.0 - discountPercent) / 100.0;
            int result = RoundHalfUp(discounted);
            return result < 0 ? 0 : result;
        }

        /// <summary>Round half away from zero (half-up for non-negative fees).</summary>
        public static int RoundHalfUp(double value)
            => (int)Math.Floor(value + 0.5d);
    }
}
