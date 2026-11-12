namespace ZoneEngine_New.Core.Knubot;

using System;

using AORebirth.Interfaces.Persistence.Missions;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Playfield;
using PlayfieldType = ZoneEngine_New.Core.Playfield.Playfield;
using ZoneEngine_New.Core.WorldSimulation;

using Quaternion = AORebirth.Core.Vector.Quaternion;
using Vector3 = AORebirth.Core.Vector.Vector3;

/// <summary>
/// Dungeon Dogs: the Arrival Hall (playfield 4604) new-character start. A character whose start-area choice is still
/// Pending and who would load in on the NewCharacter.json pad (Arete Landing 6553) is placed in the hall at the mid-hall
/// landing; LoginEngine is untouched. The three south-wall doors are the walk-in exits: the red left door ("Exit to
/// Clans", sign 0xC00B11FC) goes to Arete Landing and records the arete choice; the middle door ("Exit to Neutral",
/// sign 0xC00C11FC) goes to the ICC Shuttleport and records {startarea icc}; the blue right door ("Exit to Omni-Tek",
/// sign 0xC00D11FC) goes to Jobe Research (4001), as do the Shadowlands door and the exit terminal next to it. Sign
/// TemplateIds are left unchanged. The north door is not a walk-in exit.
/// </summary>
public static class ArrivalHall
{
    public const int PlayfieldId = 4604;
    public const int JobeResearch = 4001;
    public const int IccShuttleport = 4582;

    /// <summary>Far north door (template 100406) at (213.27, 1.02, 350.10); new characters land in the mid-hall, not through this door.</summary>
    public const int NorthDoor = unchecked((int)0xC00811FC);

    /// <summary>Red left south-wall door ("Exit to Clans") at (201.49, 1.02, 154.31), sign 0xC00B11FC above it.</summary>
    public const int AreteDoor = unchecked((int)0xC00111FC);

    /// <summary>Middle south-wall door ("Exit to Neutral") at (191.95, 1.02, 153.55), sign 0xC00C11FC above it.</summary>
    public const int IccDoor = unchecked((int)0xC00711FC);

    /// <summary>Blue right south-wall door ("Exit to Omni-Tek") at (181.88, 1.02, 154.33), sign 0xC00D11FC above it.</summary>
    public const int JobeDoor = unchecked((int)0xC00A11FC);

    /// <summary>Shadowlands door at (167.10, 1.02, 169.51) ("You need the Shadowlands Expansion Pack to go to Jobe.").</summary>
    public const int ShadowlandsDoor = unchecked((int)0xC00211FC);

    /// <summary>Exit terminal (template 95351) at (163.40, 1.02, 169.50) beside the Shadowlands door.</summary>
    public const int ShadowlandsTerminal = unchecked((int)0xC00311FC);

    /// <summary>Live PRK new-character spot, mid-hall south of the crashed shuttle (205.2, 1.0, 251.1), facing south into the hall.</summary>
    public static readonly Vector3 NorthDoorLanding = new Vector3(205.2, 1.0, 251.1);

    /// <summary>Yaw 180 degrees (the north door's own placement heading): looking down the hall toward the desks.</summary>
    public static Quaternion NorthDoorHeading => new Quaternion(0, 1, 0, 0);

    /// <summary>
    /// Jobe Research 4001: about 4 m south of Scientist Drake Rodriguez's spawn point (SCDK at 854.5, 34.8, 958.6),
    /// where his first talk hands out the Shadowlands start kit. (Live goes via the Arrival Hall Gateway 4561 and a
    /// paid nano; that scrape came from PRK, so the door goes straight here.)
    /// </summary>
    public static readonly Vector3 JobeResearchLanding = new Vector3(854.5, 34.8, 954.6);

    /// <summary>ICC Shuttleport 4582 landing used by the Immigration Officer's {teleport 4582 934.9 24.8 762.0}.</summary>
    public static readonly Vector3 IccShuttleportLanding = new Vector3(934.9, 24.8, 762.0);

    /// <summary>NewCharacter.json StartLocation (Arete Landing pad).</summary>
    public const int NewCharacterPlayfield = KnubotStartArea.AretePlayfield;
    public const double NewCharacterPadX = 3609.0;
    public const double NewCharacterPadZ = 786.2;
    public const double NewCharacterPadRadius = 50.0;

    static bool IsPending(IMissionDao? missions, int characterId)
        => missions != null
            && string.Equals(
                missions.GetStartAreaSelectionState(characterId),
                MissionStartAreaSelectionStates.Pending,
                StringComparison.Ordinal);

    /// <summary>
    /// True when a login should be moved into the Arrival Hall: saved on the NewCharacter pad and still Pending.
    /// Anyone who has chosen (or was never marked) keeps their saved spot.
    /// </summary>
    public static bool ShouldPlaceNewCharacter(int storedPlayfield, double x, double z, int characterId, IMissionDao? missions)
    {
        if (storedPlayfield != NewCharacterPlayfield)
            return false;
        double dx = x - NewCharacterPadX;
        double dz = z - NewCharacterPadZ;
        if (dx * dx + dz * dz > NewCharacterPadRadius * NewCharacterPadRadius)
            return false;
        try { return IsPending(missions, characterId); }
        catch { return false; }
    }

    /// <summary>Where a player stands in the red left doorway. Live /loc 2026-11-11 21:39 ET: 201.6, 1.0, 157.7. The door dynel is at Z 154.31, inside the wall, more than 3 m away.</summary>
    public static readonly Vector3 AreteWalkIn = new Vector3(201.6, 1.0, 157.7);

    /// <summary>Middle doorway. Live /loc 192.0, 1.0, 157.9. The door dynel is at Z 153.55.</summary>
    public static readonly Vector3 IccWalkIn = new Vector3(192.0, 1.0, 157.9);

    /// <summary>Blue right doorway. Live /loc 181.8, 1.0, 157.9. The door dynel is at Z 154.33.</summary>
    public static readonly Vector3 JobeWalkIn = new Vector3(181.8, 1.0, 157.9);

    /// <summary>Walk-in pad radius, same 3 m reach as a door with no DynelCollision.json sphere.</summary>
    public const float WalkInRadius = 3f;

    /// <summary>
    /// Places the three south-wall door pads on the live doorway /loc, not on the door dynels. Those doors only
    /// carry the unused 53142 event; bake leaves a 1 m (then widened 3 m) disc on the dynel at Z ~154, which sits
    /// inside the wall. Players stop in the opening at Z ~157.7, just outside that disc, so FireTargetVicinity never
    /// ran. Sign TemplateIds are not changed.
    /// </summary>
    public static void RegisterDoorTriggers(PlayfieldWorldSimulation world, int playfieldId)
    {
        if (playfieldId != PlayfieldId)
            return;

        world.PlaceVicinityTrigger(AreteDoor, (float)AreteWalkIn.xf, (float)AreteWalkIn.yf, (float)AreteWalkIn.zf, WalkInRadius);
        world.PlaceVicinityTrigger(IccDoor, (float)IccWalkIn.xf, (float)IccWalkIn.yf, (float)IccWalkIn.zf, WalkInRadius);
        world.PlaceVicinityTrigger(JobeDoor, (float)JobeWalkIn.xf, (float)JobeWalkIn.yf, (float)JobeWalkIn.zf, WalkInRadius);
    }

    /// <summary>Runs the hall's door overrides. Returns true when the pad was handled here (its own spells must not run).</summary>
    public static bool TryHandleVicinity(PlayfieldType playfield, Player player, int dynelInstance)
    {
        if (playfield.Identity.Instance != PlayfieldId || player.Session == null)
            return false;

        switch (dynelInstance)
        {
            case AreteDoor:
                GoToArete(playfield, player);
                return true;
            case IccDoor:
                GoToIcc(playfield, player);
                return true;
            case JobeDoor:
            case ShadowlandsDoor:
            case ShadowlandsTerminal:
                GoToJobe(playfield, player);
                return true;
            default:
                return false;
        }
    }

    static void GoToArete(PlayfieldType source, Player player)
    {
        RecordStartArea(source, player, MissionStartAreaSelectionStates.Arete);
        Transfer(source, player, KnubotStartArea.AretePlayfield, KnubotStartArea.AreteLanding, null);
    }

    static void GoToIcc(PlayfieldType source, Player player)
    {
        RecordStartArea(source, player, MissionStartAreaSelectionStates.IccShuttleport);
        Transfer(source, player, IccShuttleport, IccShuttleportLanding, null);
    }

    static void GoToJobe(PlayfieldType source, Player player)
    {
        // Same as the existing Shadowlands path: transfer only. Jobe is not a start-area choice.
        Transfer(source, player, JobeResearch, JobeResearchLanding, null);
    }

    static void RecordStartArea(PlayfieldType source, Player player, string state)
    {
        IMissionDao? missions = KnubotStartArea.Missions ?? source.GetService<IMissionDao>();
        try
        {
            if (IsPending(missions, player.Identity.Instance))
                missions!.TryCompleteStartAreaSelection(player.Identity.Instance, state);
        }
        catch
        {
            // The choice is bookkeeping; the door still works.
        }
    }

    static void Transfer(PlayfieldType source, Player player, int destination, Vector3 landing, Quaternion? heading)
    {
        player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
        player.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
        source.GetService<WorldSimulationAccess>()?.Instance?.ForgetCharacterTriggers(player.Identity.Instance);
        source.GetRequiredService<PlayfieldManager>().WithPlayfield(destination, player, arrival =>
        {
            if (!ReferenceEquals(player.Playfield, source) || player.Session is not { } session)
                return;
            if (heading is { } h)
            {
                player.Rotation = h;
                session.TransferToPlayfield(arrival, landing, h);
            }
            else
                session.TransferToPlayfield(arrival, landing);
        });
    }
}
