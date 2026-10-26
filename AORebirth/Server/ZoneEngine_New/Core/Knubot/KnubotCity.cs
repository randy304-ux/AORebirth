namespace ZoneEngine_New.Core.Knubot;

/// <summary><c>{city claim|buy|enter|raid|status KEY}</c> runs the org-city shop from a Tower Shop line.</summary>
public sealed record KnubotCity(string[] Args) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services) => services.Cities != null;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
        => services.Cities?.Execute(context.Player, Args);
}
