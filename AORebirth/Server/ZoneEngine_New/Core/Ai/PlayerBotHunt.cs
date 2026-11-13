namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// One grind / hunt area for starter playerbots. Centres come from real mob spawn clusters
    /// in GameData/PlayerBots.json (Arete, ICC, Nascence). Modelled on mod-playerbots grind areas:
    /// a bot stays in one area, wanders, and switches when the area is empty or the path is stuck.
    /// </summary>
    public sealed class PlayerBotHuntArea
    {
        public PlayerBotHuntArea(string name, Vector3 center, float radius)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "hunt" : name;
            Center = center;
            Radius = radius > 0f ? radius : PlayerBotHunt.DefaultRadius;
        }

        public string Name { get; }

        public Vector3 Center { get; }

        public float Radius { get; }
    }

    /// <summary>
    /// Destination picking for playerbot roam when a playfield has no navmesh.
    /// Short ground-snapped steps toward a hunt point or a living prey, with a sidestep after a stuck skip.
    /// </summary>
    public static class PlayerBotHunt
    {
        public const float DefaultRadius = 40f;

        public const float ScanRange = 120f;

        public const float StepMeters = 35f;

        public const float PreyApproachMeters = 8f;

        public const float ArriveMeters = 3.5f;

        public const int StuckStrikesBeforeSwitch = 2;

        public static Vector3 RandomPointInArea(Vector3 center, float radius)
        {
            double range = radius > 0f ? radius : DefaultRadius;
            double theta = Random.Shared.NextDouble() * 2.0 * Math.PI;
            double r = Math.Sqrt(Random.Shared.NextDouble()) * range;
            return new Vector3(center.x + (r * Math.Cos(theta)), center.y, center.z + (r * Math.Sin(theta)));
        }

        public static double HorizontalDistance(Vector3 a, Vector3 b)
        {
            double dx = a.x - b.x;
            double dz = a.z - b.z;
            return Math.Sqrt((dx * dx) + (dz * dz));
        }

        public static bool IsInsideArea(Vector3 point, Vector3 center, float radius)
            => HorizontalDistance(point, center) <= (radius > 0f ? radius : DefaultRadius) + 1f;

        /// <summary>
        /// Walk at most <paramref name="stepMeters"/> toward <paramref name="to"/>, optionally rotated
        /// by <paramref name="sidestepRadians"/> so a wall-stuck bot can try a new heading.
        /// </summary>
        public static Vector3 StepToward(Vector3 from, Vector3 to, float stepMeters, double sidestepRadians = 0)
        {
            double dx = to.x - from.x;
            double dz = to.z - from.z;
            double dist = Math.Sqrt((dx * dx) + (dz * dz));
            float step = stepMeters > 0f ? stepMeters : StepMeters;
            if (dist <= step || dist < 0.05)
                return new Vector3(to.x, to.y, to.z);

            double heading = Math.Atan2(dz, dx) + sidestepRadians;
            return new Vector3(
                from.x + (Math.Cos(heading) * step),
                from.y,
                from.z + (Math.Sin(heading) * step));
        }

        public static Vector3 ApproachPoint(Vector3 from, Vector3 prey, float approachMeters)
        {
            double dx = prey.x - from.x;
            double dz = prey.z - from.z;
            double dist = Math.Sqrt((dx * dx) + (dz * dz));
            float standOff = approachMeters > 0f ? approachMeters : PreyApproachMeters;
            if (dist <= standOff + ArriveMeters)
                return new Vector3(prey.x, prey.y, prey.z);

            double scale = (dist - standOff) / dist;
            return new Vector3(from.x + (dx * scale), prey.y, from.z + (dz * scale));
        }

        public static void AssignArea(PlayerBotState state, PlayerBotHuntArea area, int index)
        {
            if (state == null || area == null)
                return;

            state.HuntAreaName = area.Name;
            state.HuntCenter = area.Center;
            state.HuntRadius = area.Radius;
            state.HuntAreaIndex = index;
            state.StuckStrikes = 0;
            state.HasHuntDestination = false;
        }

        public static void AssignAreasFromPoints(PlayerBotState state, IReadOnlyList<Vector3> points)
        {
            if (state == null)
                return;

            var areas = new List<PlayerBotHuntArea>();
            if (points != null)
            {
                for (int i = 0; i < points.Count; i++)
                    areas.Add(new PlayerBotHuntArea("point-" + (i + 1), points[i], DefaultRadius));
            }

            state.HuntAreas = areas;
            if (areas.Count > 0)
            {
                int index = Random.Shared.Next(areas.Count);
                AssignArea(state, areas[index], index);
            }
        }

        public static bool SwitchArea(PlayerBotState state)
        {
            if (state == null || state.HuntAreas == null || state.HuntAreas.Count == 0)
                return false;

            if (state.HuntAreas.Count == 1)
            {
                AssignArea(state, state.HuntAreas[0], 0);
                return true;
            }

            int next = Random.Shared.Next(state.HuntAreas.Count - 1);
            if (next >= state.HuntAreaIndex)
                next++;
            AssignArea(state, state.HuntAreas[next], next);
            return true;
        }

        public static void EnsureArea(PlayerBotState state)
        {
            if (state == null)
                return;
            if (state.HuntRadius > 0f && state.HuntCenter != null)
                return;
            if (state.HuntAreas != null && state.HuntAreas.Count > 0)
            {
                int index = Random.Shared.Next(state.HuntAreas.Count);
                AssignArea(state, state.HuntAreas[index], index);
                return;
            }

            if (state.Waypoints != null && state.Waypoints.Count > 0)
                AssignAreasFromPoints(state, state.Waypoints);
        }
    }
}
