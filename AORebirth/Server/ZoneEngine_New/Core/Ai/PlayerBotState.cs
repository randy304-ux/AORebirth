namespace ZoneEngine_New.Core.Ai
{
    using System.Collections.Generic;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Per-NPC state for starter-area playerbots. Owned by <see cref="NpcBrain"/> when the AI profile is the
    /// playerbot loop; counts/levels/waypoints come from GameData/PlayerBots.json.
    /// </summary>
    public sealed class PlayerBotState
    {
        public IReadOnlyList<Vector3> Waypoints { get; set; } = System.Array.Empty<Vector3>();

        public int WaypointIndex { get; set; }

        public int MinPreyLevel { get; set; } = 1;

        public int MaxPreyLevel { get; set; } = 15;

        public float EngageRange { get; set; } = 28f;

        public float RestBelow { get; set; } = 0.40f;

        public float RestUntil { get; set; } = 0.80f;

        public bool Resting { get; set; }

        /// <summary>False when the zone has no real players and the idle throttle parked this bot.</summary>
        public bool Active { get; set; } = true;

        public double RoamPauseMinSeconds { get; set; } = 3;

        public double RoamPauseMaxSeconds { get; set; } = 10;

        public bool RoamPausing { get; set; }

        public System.DateTime RoamPauseUntilUtc { get; set; }
    }
}
