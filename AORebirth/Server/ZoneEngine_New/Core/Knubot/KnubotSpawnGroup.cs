namespace ZoneEngine_New.Core.Knubot;

using ZoneEngine_New.Core.Playfield;

/// <summary><c>{spawngroup ID}</c>: starts the named GameData spawn group on the NPC's playfield.</summary>
public sealed record KnubotSpawnGroup(string GroupId) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
    {
        // Always choosable: a run already in progress is a no-op in Apply, so the Engineer
        // can still finish "We're ready, Sir." if someone talks to him mid-fight.
        return !string.IsNullOrWhiteSpace(GroupId) && context.Npc.Playfield != null;
    }

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        context.Npc.Playfield?.GetService<SpawnGroupService>()?.TryStart(GroupId);
    }
}
