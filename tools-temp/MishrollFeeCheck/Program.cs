using System;
using ZoneEngine_New.Core.Configuration;
using ZoneEngine_New.Core.Missions;

static GameplayMishrollTargetingFee Fee() => new()
{
    FreeThroughQuality = 25,
    FullFeeQuality = 200,
    FullFeeCredits = 10000,
    FeeCurveFraction = 0.5,
    BreakAndEntryDiscountPercentPerTen = 1,
    BreakAndEntryDiscountCapPercent = 50
};

static void Expect(int ql, int be, int want)
{
    int got = MishrollTargetingFeeCalculator.Compute(ql, be, Fee());
    if (got != want)
        throw new InvalidOperationException($"QL{ql} BE{be}: expected {want}, got {got}");
    Console.WriteLine($"PASS QL{ql} BE{be} => {got}");
}

Expect(1, 0, 0);
Expect(25, 0, 0);
Expect(26, 0, 57);
Expect(50, 0, 1429);
Expect(100, 0, 4286);
Expect(150, 0, 7143);
Expect(200, 0, 10000);
Expect(220, 0, 10000);
Expect(200, 100, 9000);
Expect(200, 500, 5000);
Expect(200, 600, 5000);
Console.WriteLine("ALL FEE CHECKS PASS");
