namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Per-NPC state for starter-area playerbots. Owned by <see cref="NpcBrain"/> when the AI profile is the
    /// playerbot loop; counts/levels/waypoints come from GameData/PlayerBots.json.
    /// </summary>
    public sealed class PlayerBotState
    {
        readonly Dictionary<ulong, DateTime> _blacklist = new();

        public IReadOnlyList<Vector3> Waypoints { get; set; } = Array.Empty<Vector3>();

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

        public DateTime RoamPauseUntilUtc { get; set; }

        public Vector3 LastMovePosition { get; set; }

        public DateTime LastMoveUtc { get; set; }

        public Identity WatchTarget { get; set; }

        public int WatchHealth { get; set; }

        public DateTime WatchSinceUtc { get; set; }

        public void Blacklist(Identity identity, DateTime untilUtc)
        {
            if (identity.Instance == 0)
                return;
            _blacklist[identity.Long()] = untilUtc;
        }

        public bool TryGetBlacklist(Identity identity, out DateTime untilUtc)
        {
            if (identity.Instance == 0)
            {
                untilUtc = default;
                return false;
            }

            return _blacklist.TryGetValue(identity.Long(), out untilUtc);
        }

        public void ClearBlacklist(Identity identity)
        {
            if (identity.Instance == 0)
                return;
            _blacklist.Remove(identity.Long());
        }

        public void ClearWatch()
        {
            WatchTarget = Identity.None;
            WatchHealth = 0;
            WatchSinceUtc = default;
        }
    }
}