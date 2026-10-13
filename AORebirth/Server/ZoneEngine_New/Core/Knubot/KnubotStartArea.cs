namespace ZoneEngine_New.Core.Knubot;

using System;

using AORebirth.Interfaces.Persistence.Missions;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;

using Vector3 = AORebirth.Core.Vector.Vector3;

/// <summary>
/// Dungeon Dogs: the new-character start-area choice. LoginEngine marks every new Rubi-Ka character Pending;
/// a KnuBot line resolves it once with <c>{startarea arete}</c> (complete the choice and move to Arete Landing)
/// or <c>{startarea icc}</c> (complete the choice and stay at the ICC Shuttleport).
/// </summary>
public static class KnubotStartArea
{
    public const int AretePlayfield = 6553;

    /// <summary>Open ground by Stan Goodman and Sarah Greene at the Arete Landing camp.</summary>
    public static readonly Vector3 AreteLanding = new Vector3(3455f, 9.5f, 865f);

    /// <summary>Set once by <see cref="KnubotEffectServices"/> so load-time conditions can read the choice.</summary>
    public static IMissionDao? Missions { get; private set; }

    public static IMissionDao? Register(IMissionDao? missions)
    {
        if (missions != null)
            Missions = missions;
        return missions;
    }

    public static bool IsPending(Player player)
        => Missions != null
            && string.Equals(
                Missions.GetStartAreaSelectionState(player.Identity.Instance),
                MissionStartAreaSelectionStates.Pending,
                StringComparison.Ordinal);
}

/// <summary><c>StartAreaPending</c>: the character has not chosen Arete Landing or the ICC Shuttleport yet.</summary>
public sealed record KnubotStartAreaPendingCondition(bool Negated) : KnubotCondition(Negated)
{
    protected override bool Test(KnubotContext context) => KnubotStartArea.IsPending(context.Player);
}

/// <summary><c>{startarea arete|icc}</c>: records the choice; <c>arete</c> also transfers the player to Arete Landing.</summary>
public sealed record KnubotChooseStartArea(bool Arete) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
        => KnubotStartArea.IsPending(context.Player) && (!Arete || services.Playfields != null);

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        Player player = context.Player;
        string state = Arete ? MissionStartAreaSelectionStates.Arete : MissionStartAreaSelectionStates.IccShuttleport;
        if (KnubotStartArea.Missions == null || !KnubotStartArea.Missions.TryCompleteStartAreaSelection(player.Identity.Instance, state))
            return;

        if (!Arete || services.Playfields == null || player.Session == null)
            return;

        services.Playfields.Value.WithPlayfield(KnubotStartArea.AretePlayfield, player, destination =>
        {
            player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
            player.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
            player.Session?.TransferToPlayfield(destination, KnubotStartArea.AreteLanding);
        });
    }
}
