namespace ZoneEngine_New.Core.Knubot;

using ZoneEngine_New.Core.Battlestations;

/// <summary><c>{bsqueue}</c>: join the battle-station bracket named by this terminal.</summary>
public sealed record KnubotBattleStationQueue : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
        => BattleStationClock.Service != null;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
        => BattleStationClock.Service?.TryQueue(context.Player, context.Npc.Name);
}
