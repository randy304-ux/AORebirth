namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;

    using GroveGames.BehaviourTree;
    using GroveGames.BehaviourTree.Nodes;
    using GroveGames.BehaviourTree.Nodes.Composites;
    using GroveGames.BehaviourTree.Nodes.Decorators;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Playfield.Locality;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Starter playerbot brain: one prioritized action loop (rest, fight, engage, roam, idle).
    /// Uses the same chase/fight leaves as ordinary NPCs; pathing falls back to straight line when no navmesh.
    /// Invalid or ineffective prey is dropped and blacklisted so the bot returns to roam/hunt.
    /// </summary>
    sealed class PlayerBotBehaviourTree : BehaviourTree
    {
        readonly NpcBrain _brain;

        public PlayerBotBehaviourTree(IRoot root, NpcBrain brain)
            : base(root)
        {
            _brain = brain;
        }

        public override void SetupTree()
        {
            IParent root = Root.Selector("playerbot-action-loop");

            root.Conditional(NeedsRest, "hurt")
                .Attach(new PlayerBotRestNode(_brain));

            IParent combat = root.Conditional(HasUsableHate, "has-nearby-hate")
                .Sequence("combat")
                .Attach(new SelectHighestThreatNode(_brain));
            combat.Sequence("default-fight")
                .Attach(new MoveTowardTargetNode(_brain))
                .Attach(new StartFightingNode(_brain))
                .Attach(new PlayerBotWatchAttackNode(_brain));

            root.Attach(new PlayerBotEngageNode(_brain));
            root.Attach(new PlayerBotRoamNode(_brain));
            root.Attach(new IdleNode(_brain));
        }

        bool NeedsRest()
        {
            PlayerBotState? state = _brain.PlayerBot;
            if (state == null)
                return false;

            int max = _brain.Npc.Stats.GetOrZero(CharacterStat.MaxHealth);
            if (max <= 0)
                return false;

            float pct = _brain.Npc.Stats.GetOrZero(CharacterStat.Health) / (float)max;
            if (state.Resting)
            {
                if (pct >= state.RestUntil)
                    state.Resting = false;
                return state.Resting;
            }

            if (pct <= state.RestBelow)
            {
                state.Resting = true;
                return true;
            }

            return false;
        }

        bool HasUsableHate()
        {
            PlayerBotHate.Purge(_brain);
            return _brain.HasNearbyHate();
        }
    }

    static class PlayerBotHate
    {
        public static void Purge(NpcBrain brain)
        {
            PlayerBotState? state = brain.PlayerBot;
            if (state == null || brain.Hate.IsEmpty)
                return;

            DateTime now = DateTime.UtcNow;
            List<Identity>? drop = null;
            foreach (HateEntry entry in brain.Hate.Entries)
            {
                Character? target = brain.Resolve(entry.Identity);
                if (target == null || PlayerBotTargeting.IsBlacklisted(state, entry.Identity, now))
                {
                    (drop ??= new List<Identity>()).Add(entry.Identity);
                }
                else if (!PlayerBotTargeting.IsPossibleTarget(brain.Npc, target, applyLevelRange: true, out string reason))
                {
                    LogDrop(brain, target, reason);
                    (drop ??= new List<Identity>()).Add(entry.Identity);
                }
            }

            if (drop == null)
                return;

            for (int i = 0; i < drop.Count; i++)
                Drop(brain, state, drop[i], now, blacklist: true);
        }

        public static void LogDrop(NpcBrain brain, Character target, string reason)
        {
            brain.Npc.Playfield?.GetService<IZoneLogger>()?.Info(
                "Playerbot dropped target bot=" + brain.Npc.Name
                + " target=" + target.Name
                + " reason=" + reason);
        }

        public static void Drop(NpcBrain brain, PlayerBotState state, Identity identity, DateTime nowUtc, bool blacklist)
        {
            if (identity.Instance == 0)
                return;

            if (blacklist)
                PlayerBotTargeting.Blacklist(state, identity, nowUtc);

            brain.Hate.Remove(identity);
            if (brain.Npc.FightingTarget == identity)
                brain.StopFighting();
            state.ClearWatch();
        }
    }

    sealed class PlayerBotRestNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public PlayerBotRestNode(NpcBrain brain)
            : base("playerbot-rest")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.playerbot-rest", _brain.Npc.Identity.Instance);
            _brain.ClearHate();
            _brain.StopFighting();
            _brain.StopPathing();
            _brain.PlayerBot?.ClearWatch();
            return _nodeState = NodeState.Running;
        }
    }

    /// <summary>Pick a nearby level-appropriate attackable mob and put it on the hate list.</summary>
    sealed class PlayerBotEngageNode : BehaviourNode
    {
        readonly NpcBrain _brain;
        readonly List<NpcCharacter> _scratch = new(16);

        public PlayerBotEngageNode(NpcBrain brain)
            : base("playerbot-engage")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.playerbot-engage", _brain.Npc.Identity.Instance);
            PlayerBotState? state = _brain.PlayerBot;
            if (state == null || !state.Active || _brain.Npc.Playfield == null)
                return _nodeState = NodeState.Failure;

            DateTime now = DateTime.UtcNow;
            var locality = _brain.Npc.Playfield.GetRequiredService<PlayfieldLocality>();
            locality.CollectNearby(_brain.Npc, state.EngageRange, _scratch);
            NpcCharacter? prey = null;
            double best = double.MaxValue;
            for (int i = 0; i < _scratch.Count; i++)
            {
                NpcCharacter other = _scratch[i];
                if (PlayerBotTargeting.IsBlacklisted(state, other.Identity, now))
                    continue;
                if (!PlayerBotTargeting.IsValidPrey(_brain.Npc, other, applyLevelRange: true))
                    continue;

                double dx = other.Position.x - _brain.Npc.Position.x;
                double dz = other.Position.z - _brain.Npc.Position.z;
                double d = dx * dx + dz * dz;
                if (d < best)
                {
                    best = d;
                    prey = other;
                }
            }

            _scratch.Clear();
            if (prey == null)
                return _nodeState = NodeState.Failure;

            _brain.AddThreat(prey.Identity, NpcAiRules.ProximityHate);
            if (!CombatRules.CanAttack(_brain.Npc, prey))
            {
                PlayerBotHate.Drop(_brain, state, prey.Identity, now, blacklist: true);
                return _nodeState = NodeState.Failure;
            }

            _brain.Npc.StartFighting(prey.Identity, 0);
            if (_brain.Npc.FightingTarget != prey.Identity)
            {
                PlayerBotHate.Drop(_brain, state, prey.Identity, now, blacklist: true);
                return _nodeState = NodeState.Failure;
            }

            return _nodeState = NodeState.Success;
        }
    }

    /// <summary>
    /// Drops a target that refused StartFighting or took no damage after several seconds in range.
    /// </summary>
    sealed class PlayerBotWatchAttackNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public PlayerBotWatchAttackNode(NpcBrain brain)
            : base("playerbot-watch-attack")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.playerbot-watch-attack", _brain.Npc.Identity.Instance);
            PlayerBotState? state = _brain.PlayerBot;
            Character? target = _brain.ResolveCurrentTarget();
            if (state == null || target == null)
                return _nodeState = NodeState.Failure;

            DateTime now = DateTime.UtcNow;
            if (!PlayerBotTargeting.IsPossibleTarget(_brain.Npc, target, applyLevelRange: true, out string reason)
                || _brain.Npc.FightingTarget != target.Identity)
            {
                LogDrop(target, reason.Length > 0 ? reason : "attack refused");
                PlayerBotHate.Drop(_brain, state, target.Identity, now, blacklist: true);
                return _nodeState = NodeState.Failure;
            }

            int health = target.Stats.GetOrZero(CharacterStat.Health);
            if (state.WatchTarget != target.Identity)
            {
                state.WatchTarget = target.Identity;
                state.WatchHealth = health;
                state.WatchSinceUtc = now;
                return _nodeState = NodeState.Success;
            }

            if (health < state.WatchHealth)
            {
                state.WatchHealth = health;
                state.WatchSinceUtc = now;
                return _nodeState = NodeState.Success;
            }

            if (_brain.CanAttackNow(target)
                && now - state.WatchSinceUtc >= PlayerBotTargeting.IneffectiveAttackTimeout)
            {
                LogDrop(target, "no damage");
                PlayerBotHate.Drop(_brain, state, target.Identity, now, blacklist: true);
                return _nodeState = NodeState.Failure;
            }

            return _nodeState = NodeState.Success;
        }

        void LogDrop(Character target, string reason)
        {
            PlayerBotHate.LogDrop(_brain, target, reason);
        }
    }

    /// <summary>
    /// Hunt-area roam (mod-playerbots grind style). With no navmesh these starter PFs fall back to
    /// short ground-snapped steps toward a living prey or a random point in the current hunt area.
    /// An idle bot always picks a new roam target; two stuck skips switch the hunt area.
    /// </summary>
    sealed class PlayerBotRoamNode : BehaviourNode
    {
        readonly NpcBrain _brain;
        readonly List<NpcCharacter> _scratch = new(32);

        public PlayerBotRoamNode(NpcBrain brain)
            : base("playerbot-roam")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.playerbot-roam", _brain.Npc.Identity.Instance);
            PlayerBotState? state = _brain.PlayerBot;
            if (state == null)
                return _nodeState = NodeState.Failure;

            PlayerBotHunt.EnsureArea(state);
            if ((state.HuntAreas == null || state.HuntAreas.Count == 0) && state.HuntRadius <= 0f)
                return _nodeState = NodeState.Failure;

            if (!state.Active)
            {
                _brain.StopPathing();
                return _nodeState = NodeState.Running;
            }

            DateTime now = DateTime.UtcNow;
            if (state.RoamPausing)
            {
                if (now < state.RoamPauseUntilUtc)
                    return _nodeState = NodeState.Running;
                state.RoamPausing = false;
                state.HasHuntDestination = false;
                MarkMoved(state, now);
            }

            if (!state.HasHuntDestination)
                ChooseDestination(state, sidestep: 0, reason: "new");

            Vector3 dest = state.HuntDestination;
            if (PlayerBotHunt.HorizontalDistance(_brain.Npc.Position, dest) <= PlayerBotHunt.ArriveMeters)
            {
                _brain.StopPathing();
                state.RoamPausing = true;
                state.HasHuntDestination = false;
                state.StuckStrikes = 0;
                state.LastSidestepRadians = 0;
                double pause = state.RoamPauseMinSeconds
                    + Random.Shared.NextDouble() * Math.Max(0, state.RoamPauseMaxSeconds - state.RoamPauseMinSeconds);
                state.RoamPauseUntilUtc = now.AddSeconds(pause);
                MarkMoved(state, now);
                return _nodeState = NodeState.Running;
            }

            if (IsStuck(state, now))
            {
                _brain.StopPathing();
                state.StuckStrikes++;
                if (state.StuckStrikes >= PlayerBotHunt.StuckStrikesBeforeSwitch)
                {
                    PlayerBotHunt.SwitchArea(state);
                    ChooseDestination(state, sidestep: 0, reason: "stuck-switch");
                }
                else
                {
                    double sidestep = (Random.Shared.Next(2) == 0 ? -1 : 1) * (Math.PI / 3.0);
                    state.LastSidestepRadians = sidestep;
                    ChooseDestination(state, sidestep, reason: "stuck-sidestep");
                }

                MarkMoved(state, now);
                dest = state.HuntDestination;
            }

            Vector3 step = PlayerBotHunt.StepToward(
                _brain.Npc.Position,
                dest,
                PlayerBotHunt.StepMeters,
                state.LastSidestepRadians);
            step = Snap(step);

            if (_brain.Npc.Motor.State != Movement.MovementState.Walk)
                _brain.Npc.Motor.ApplyAction(Movement.MovementAction.SwitchToWalk);
            _brain.PathTo(step);
            return _nodeState = NodeState.Running;
        }

        void ChooseDestination(PlayerBotState state, double sidestep, string reason)
        {
            PlayerBotHunt.EnsureArea(state);
            NpcCharacter? prey = FindHuntPrey(state);
            Vector3 goal;
            string how;
            if (prey != null)
            {
                goal = PlayerBotHunt.ApproachPoint(_brain.Npc.Position, prey.Position, PlayerBotHunt.PreyApproachMeters);
                how = "prey:" + prey.Name;
            }
            else
            {
                goal = PlayerBotHunt.RandomPointInArea(state.HuntCenter, state.HuntRadius);
                how = "area:" + (string.IsNullOrEmpty(state.HuntAreaName) ? "hunt" : state.HuntAreaName);
            }

            if (sidestep != 0)
                goal = PlayerBotHunt.StepToward(_brain.Npc.Position, goal, PlayerBotHunt.StepMeters, sidestep);

            state.HuntDestination = Snap(goal);
            state.HasHuntDestination = true;
            state.LastSidestepRadians = sidestep;

            if (reason == "stuck-switch" || reason == "stuck-sidestep")
            {
                _brain.Npc.Playfield?.GetService<IZoneLogger>()?.Info(
                    "Playerbot roam bot=" + _brain.Npc.Name
                    + " action=" + reason
                    + " area=" + state.HuntAreaName
                    + " via=" + how
                    + " dest=" + string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "{0:0},{1:0}",
                        state.HuntDestination.xf,
                        state.HuntDestination.zf));
            }
        }

        NpcCharacter? FindHuntPrey(PlayerBotState state)
        {
            if (_brain.Npc.Playfield == null)
                return null;

            DateTime now = DateTime.UtcNow;
            var locality = _brain.Npc.Playfield.GetRequiredService<PlayfieldLocality>();
            locality.CollectNearby(_brain.Npc, PlayerBotHunt.ScanRange, _scratch);
            NpcCharacter? prey = null;
            double best = double.MaxValue;
            for (int i = 0; i < _scratch.Count; i++)
            {
                NpcCharacter other = _scratch[i];
                if (PlayerBotTargeting.IsBlacklisted(state, other.Identity, now))
                    continue;
                if (!PlayerBotTargeting.IsValidPrey(_brain.Npc, other, applyLevelRange: true))
                    continue;

                double d = PlayerBotHunt.HorizontalDistance(_brain.Npc.Position, other.Position);
                if (d < best)
                {
                    best = d;
                    prey = other;
                }
            }

            _scratch.Clear();
            return prey;
        }

        Vector3 Snap(Vector3 point)
        {
            if (_brain.Npc.Playfield == null)
                return point;
            return _brain.Npc.Playfield.SnapFeetToFloor(point);
        }

        bool IsStuck(PlayerBotState state, DateTime now)
        {
            if (state.LastMoveUtc == default)
            {
                MarkMoved(state, now);
                return false;
            }

            if (NpcAiRules.IsNearby(_brain.Npc.Position, state.LastMovePosition, PlayerBotTargeting.RoamStuckMeters))
                return now - state.LastMoveUtc >= PlayerBotTargeting.RoamStuckTimeout;

            MarkMoved(state, now);
            return false;
        }

        void MarkMoved(PlayerBotState state, DateTime now)
        {
            state.LastMovePosition = _brain.Npc.Position;
            state.LastMoveUtc = now;
        }
    }
}
