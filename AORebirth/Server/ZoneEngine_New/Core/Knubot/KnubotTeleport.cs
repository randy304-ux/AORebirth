namespace ZoneEngine_New.Core.Knubot;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;

using Vector3 = AORebirth.Core.Vector.Vector3;

/// <summary>
/// Dungeon Dogs: <c>{teleport PF X Y Z}</c> moves the talking player to a point, zoning when PF is another playfield.
/// Used for doors that have no client dynel, such as the Arete crashed alien ship (Karli Cappelleri).
/// </summary>
public sealed record KnubotTeleport(int Playfield, float X, float Y, float Z) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
        => services.Playfields != null && context.Player.Session != null;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        var player = context.Player;
        if (services.Playfields == null || player.Session == null)
            return;

        var landing = new Vector3(X, Y, Z);
        if (player.Playfield != null && player.Playfield.Identity.Instance == Playfield)
        {
            player.TeleportWithinPlayfield(landing);
            return;
        }

        services.Playfields.Value.WithPlayfield(Playfield, player, destination =>
        {
            player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
            player.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
            player.Session?.TransferToPlayfield(destination, landing);
        });
    }
}
