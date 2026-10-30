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
/// Pending and who would load in on the NewCharacter.json pad (Arete Landing 6553) is placed in the hall at the far
/// (north) door instead; LoginEngine is untouched. Two walk-in doors are overridden here: the middle south-wall door
/// (between the ICC Immigration Officer and the Jobe Research side) goes to Arete Landing and records the arete choice;
/// the Shadowlands door and the exit terminal next to it go to Jobe Platform. The Immigration Officer's KnuBot handles
/// Rubi-Ka ({startarea icc} + ICC Shuttleport).
/// </summary>
public static class ArrivalHall
{
    public const int PlayfieldId = 4604;
    public const int JobePlatform = 4530;

    /// <summary>Far north door (template 100406) at (213.27, 1.02, 350.10); new characters land just inside it.</summary>
    public const int NorthDoor = unchecked((int)0xC00811FC);

    /// <summary>Middle south-wall door at (191.95, 1.02, 153.55), directly behind the Immigration Officer's desk.</summary>
    public const int AreteDoor = unchecked((int)0xC00711FC);

    /// <summary>Shadowlands door at (167.10, 1.02, 169.51) ("You need the Shadowlands Expansion Pack to go to Jobe.").</summary>
    public const int ShadowlandsDoor = unchecked((int)0xC00211FC);

    /// <summary>Exit terminal (template 95351) at (163.40, 1.02, 169.50) beside the Shadowlands door.</summary>
    public const int ShadowlandsTerminal = unchecked((int)0xC00311FC);

    /// <summary>About 6 m south of the north door, clear of its walk-in pad, facing south into the hall.</summary>
    public static readonly Vector3 NorthDoorLanding = new Vector3(212.4, 1.02, 344.0);

    /// <summary>Yaw 180 degrees (the north door's own placement heading): looking down the hall toward the desks.</summary>
    public static Quaternion NorthDoorHeading => new Quaternion(0, 1, 0, 0);

    /// <summary>Jobe Platform destination line 6, where the other doors into Jobe Platform land.</summary>
    public const byte JobeDestinationLine = 6;
    public static readonly Vector3 JobeFallbackLanding = new Vector3(297.1, 199.8, 615.85);

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
        IMissionDao? missions = KnubotStartArea.Missions ?? source.GetService<IMissionDao>();
        try
        {
            if (IsPending(missions, player.Identity.Instance))
                missions!.TryCompleteStartAreaSelection(player.Identity.Instance, MissionStartAreaSelectionStates.Arete);
        }
        catch
        {
            // The choice is bookkeeping; the door still works.
        }

        Transfer(source, player, KnubotStartArea.AretePlayfield, KnubotStartArea.AreteLanding, null);
    }

    static void GoToJobe(PlayfieldType source, Player player)
    {
        Vector3 landing = JobeFallbackLanding;
        Quaternion? heading = null;
        if (PortalDoorLandingResolver.TryResolveMidpointLanding(
                DestinationsCatalog.Instance, JobePlatform, JobeDestinationLine, out Vector3 line, out Quaternion lineHeading))
        {
            landing = line;
            heading = lineHeading;
        }

        Transfer(source, player, JobePlatform, landing, heading);
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

