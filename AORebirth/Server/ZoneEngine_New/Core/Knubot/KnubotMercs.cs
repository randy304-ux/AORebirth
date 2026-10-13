namespace ZoneEngine_New.Core.Knubot;

using AORebirth.Enums;

using SmokeLounge.AOtomation.Messaging.GameData;

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