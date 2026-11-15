namespace ZoneEngine_New.Core.Knubot;

using System.Collections.Generic;

using AORebirth.Enums;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

using ZoneEngine_New.Core.Pets;

/// <summary><c>{hiremerc KIND}</c>: hires (or replaces) the player's merc of that profession at the player's level.</summary>
public sealed record KnubotHireMerc(string Kind) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
        => Mercs.Find(Kind) != null && context.Player.Playfield?.GetService<PetService>() is { MercsEnabled: true };

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        if (Mercs.Find(Kind) is not { } kind || context.Player.Playfield?.GetService<PetService>() is not { } pets)
            return;

        pets.HireMerc(context.Player, kind, context.Player.Stats.GetOrOne(CharacterStat.Level));
    }
}

/// <summary><c>{dismissmercs}</c>: sends away every merc the player has hired.</summary>
public sealed record KnubotDismissMercs : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services) => true;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
        => context.Player.Playfield?.GetService<PetService>()?.DismissMercs(context.Player);
}
/// <summary><c>{dismissmerc KIND}</c>: ends one merc contract and sends that merc home.</summary>
public sealed record KnubotDismissMerc(string Kind) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services) => Mercs.Find(Kind) != null;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        if (Mercs.Find(Kind) is { } kind)
            context.Player.Playfield?.GetService<PetService>()?.DismissMerc(context.Player, kind);
    }
}

/// <summary>
/// <c>{dungeonmerc ID}</c>: a dungeon's twink merc joins on its kill-the-boss mission (MercRoster.json id or unlock
/// group): a locked merc becomes dungeon-only (summon inside that dungeon until the boss dies), and it is summoned
/// into the player's party unless it already is.
/// </summary>
public sealed record KnubotDungeonMerc(string Id) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
        => context.Player.Playfield?.GetService<MercUnlockService>() is { } unlocks
           && (unlocks.Roster.Find(Id) ?? FirstInGroup(unlocks, Id)) != null;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        if (context.Player.Playfield?.GetService<MercUnlockService>() is not { } unlocks
            || (unlocks.Roster.Find(Id) ?? FirstInGroup(unlocks, Id)) is not { } def)
            return;

        unlocks.OnMissionAccepted(context.Player, def.Id);
        if (unlocks.IsSummoned(context.Player, def))
            return;

        string? refused = unlocks.Summon(context.Player, def);
        if (refused != null)
            context.Player.Session?.Send(new ChatTextMessage
            {
                Identity = context.Player.Identity,
                Text = refused
            });
    }

    static MercDefinition? FirstInGroup(MercUnlockService unlocks, string group)
    {
        IReadOnlyList<MercDefinition> list = unlocks.Roster.ByUnlockGroup(group);
        return list.Count > 0 ? list[0] : null;
    }
}