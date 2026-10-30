namespace ZoneEngine_New.Core.Knubot;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Playfield;
using PlayfieldType = ZoneEngine_New.Core.Playfield.Playfield;
using ZoneEngine_New.Core.WorldSimulation;

using Quaternion = AORebirth.Core.Vector.Quaternion;
using Vector3 = AORebirth.Core.Vector.Vector3;

/// <summary>
/// Dungeon Dogs: the Crashed Alien Ship walk-through doors (live scrape v5.4, 2026-10-30 18:32-18:33 ET). The client's
/// door statels carry no usable destination, so both are routed here like the Arrival Hall doors.
/// Arete Landing 6553 "Crashed Alien Ship" door 281110661 at (3892.8, 8.4, 248.6) -> 8009 (40.28, 0.44, 31.18) heading 359;
/// 8009 "Exit" door -1073733815 at (39.95, 0.0, 25.26) -> 6553 (3887.51, 6.11, 248.76) heading 270.5.
/// </summary>
public static class AlienShip
{
    public const int AretePlayfield = 6553;
    public const int ShipPlayfield = 8009;

    public const int EntryDoor = 281110661;
    public const int ExitDoor = -1073733815;

    public static readonly Vector3 ShipLanding = new Vector3(40.28, 0.44, 31.18);
    public static readonly Vector3 AreteLanding = new Vector3(3887.51, 6.11, 248.76);

    public static Quaternion ShipHeading => Yaw(359);
    public static Quaternion AreteHeading => Yaw(270.5);

    static Quaternion Yaw(double degrees)
    {
        double half = degrees * System.Math.PI / 360.0;
        return new Quaternion(0, System.Math.Sin(half), 0, System.Math.Cos(half));
    }

    /// <summary>Registers a walk-in trigger at each ship door (DungeonExit volume with the live landing).</summary>
    public static void RegisterDoorTriggers(PlayfieldWorldSimulation world, int playfieldId)
    {
        if (playfieldId == AretePlayfield)
            world.RegisterDungeonExit(3892.8f, 8.4f, 248.6f, EntryDoor, ShipPlayfield, ShipLanding, ShipHeading);
        else if (playfieldId == ShipPlayfield)
            world.RegisterDungeonExit(39.95f, 0.0f, 25.26f, ExitDoor, AretePlayfield, AreteLanding, AreteHeading);
    }

    /// <summary>True when the pad was one of the ship doors and was handled (its own spells must not run).</summary>
    public static bool TryHandleVicinity(PlayfieldType playfield, Player player, int dynelInstance, float x, float z)
    {
        if (player.Session == null)
            return false;
        // The 8009 exit number is shared by another dynel type far down the hall (z 55); only the door spot counts.
        if (dynelInstance == ExitDoor && (System.Math.Abs(x - 39.95f) > 4f || System.Math.Abs(z - 25.26f) > 4f))
            return false;
        if (dynelInstance == EntryDoor && (System.Math.Abs(x - 3892.8f) > 4f || System.Math.Abs(z - 248.6f) > 4f))
            return false;
        int pf = playfield.Identity.Instance;
        if (pf == AretePlayfield && dynelInstance == EntryDoor)
        {
            Transfer(playfield, player, ShipPlayfield, ShipLanding, ShipHeading);
            return true;
        }

        if (pf == ShipPlayfield && dynelInstance == ExitDoor)
        {
            Transfer(playfield, player, AretePlayfield, AreteLanding, AreteHeading);
            return true;
        }

        return false;
    }

    static void Transfer(PlayfieldType source, Player player, int destination, Vector3 landing, Quaternion heading)
    {
        player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
        player.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
        source.GetService<WorldSimulationAccess>()?.Instance?.ForgetCharacterTriggers(player.Identity.Instance);
        source.GetRequiredService<PlayfieldManager>().WithPlayfield(destination, player, arrival =>
        {
            if (!ReferenceEquals(player.Playfield, source) || player.Session is not { } session)
                return;
            player.Rotation = heading;
            session.TransferToPlayfield(arrival, landing, heading);
        });
    }
}