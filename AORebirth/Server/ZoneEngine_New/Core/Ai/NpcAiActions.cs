namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;

    using GroveGames.BehaviourTree.Nodes;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Metrics;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    sealed class ReturnToSpawnNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public ReturnToSpawnNode(NpcBrain brain)
            : base("return-to-spawn")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.return-to-spawn", _brain.Npc.Identity.Instance);
            _brain.StopFighting();
            if (!_brain.HasHome || _brain.HasArrivedHome())
                return _nodeState = NodeState.Success;

            // Run home when there is a route. Snap only when there is none, or the walk home gets
            // stuck; the reset that follows heals either way.
            if (_brain.ReturnHome())
                return _nodeState = NodeState.Running;

            _brain.WarpHome();
            return _nodeState = NodeState.Success;
        }
    }

    sealed class ResetNpcNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public ResetNpcNode(NpcBrain brain)
            : base("reset")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.reset", _brain.Npc.Identity.Instance);
            _brain.ResetOutOfCombat();
            return _nodeState = NodeState.Success;
        }
    }

    sealed class SelectHighestThreatNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public SelectHighestThreatNode(NpcBrain brain)
            : base("select-threat")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.select-threat", _brain.Npc.Identity.Instance);
            return _nodeState = _brain.TrySelectHighestThreat() ? NodeState.Success : NodeState.Failure;
        }
    }

    sealed class MoveTowardTargetNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public MoveTowardTargetNode(NpcBrain brain)
            : base("move-toward-target")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.move-toward-target", _brain.Npc.Identity.Instance);
            // The combat sequence stays on this node while chasing, so re-select here.
            // A new or stolen target has to enter combat on this tick, before arrival.
            _brain.TrySelectHighestThreat();
            Character? target = _brain.ResolveCurrentTarget();
            if (target == null)
                return _nodeState = NodeState.Failure;

            if (_brain.Npc.FightingTarget != target.Identity)
                _brain.Npc.StartFighting(target.Identity, 0);

            if (_brain.CanAttackNow(target))
            {
                _brain.StopPathing();
                return _nodeState = NodeState.Success;
            }

            if (_brain.HasUnfinishedPath())
            {
                if (_brain.CanPathTo(target))
                    _brain.PathTo(target.Position);
                return _nodeState = NodeState.Running;
            }

            if (!_brain.CanPathTo(target))
            {
                if (_brain.HasChanceWithGrace(target))
                    return _nodeState = NodeState.Running;

                _brain.StopFighting();
                return _nodeState = NodeState.Failure;
            }

            _brain.PathTo(target.Position);
            return _nodeState = NodeState.Running;
        }
    }

    sealed class StartFightingNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public StartFightingNode(NpcBrain brain)
            : base("start-fighting")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.start-fighting", _brain.Npc.Identity.Instance);
            Character? target = _brain.ResolveCurrentTarget();
            if (target == null)
                return _nodeState = NodeState.Failure;

            if (_brain.Npc.FightingTarget != target.Identity)
                _brain.Npc.StartFighting(target.Identity, 0);

            return _nodeState = NodeState.Success;
        }
    }

    sealed class PatrolNode : BehaviourNode
    {
        const double MinPauseSeconds = 2;
        const double MaxPauseSeconds = 6;

        readonly NpcBrain _brain;
        int _index;
        bool _pausing;
        DateTime _pauseUntilUtc;

        public PatrolNode(NpcBrain brain)
            : base("patrol")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.patrol", _brain.Npc.Identity.Instance);
            IReadOnlyList<Vector3> points = _brain.PatrolWaypoints;
            if (points.Count == 0)
                return _nodeState = NodeState.Failure;

            if (_index < 0 || _index >= points.Count)
                _index = NearestIndex(points);

            if (_pausing)
            {
                if (DateTime.UtcNow < _pauseUntilUtc)
                    return _nodeState = NodeState.Running;
                _pausing = false;
                _index = (_index + 1) % points.Count;
            }

            Vector3 dest = points[_index];
            if (NpcAiRules.IsNearby(_brain.Npc.Position, dest, NpcAiRules.ArriveHomeMeters))
            {
                _brain.StopPathing();
                _pausing = true;
                _pauseUntilUtc = DateTime.UtcNow.AddSeconds(
                    MinPauseSeconds + Random.Shared.NextDouble() * (MaxPauseSeconds - MinPauseSeconds));
                return _nodeState = NodeState.Running;
            }

            if (_brain.Npc.Motor.State != Movement.MovementState.Walk)
                _brain.Npc.Motor.ApplyAction(Movement.MovementAction.SwitchToWalk);
            _brain.PathTo(dest);
            return _nodeState = NodeState.Running;
        }

        public override void Reset()
        {
            // After leash/combat, resume from the nearest recorded point rather than restarting the loop.
            _pausing = false;
            _pauseUntilUtc = default;
            _index = NearestIndex(_brain.PatrolWaypoints);
        }

        int NearestIndex(IReadOnlyList<Vector3> points)
        {
            if (points.Count == 0)
                return 0;
            Vector3 here = _brain.Npc.Position;
            int best = 0;
            double bestDist = double.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                double dx = here.x - points[i].x;
                double dy = here.y - points[i].y;
                double dz = here.z - points[i].z;
                double d = dx * dx + dy * dy + dz * dz;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            return best;
        }
    }

    /// <summary>A district NPC's idle activity: pause, walk to a random reachable spot near home, repeat.</summary>
    sealed class WanderNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public WanderNode(NpcBrain brain)
            : base("wander")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.wander", _brain.Npc.Identity.Instance);
            _brain.TickWander();
            return _nodeState = NodeState.Running;
        }
    }

    sealed class IdleNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public IdleNode(NpcBrain brain)
            : base("idle")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.idle", _brain.Npc.Identity.Instance);
            _brain.StopPathing();
            return _nodeState = NodeState.Success;
        }
    }

    static class NpcAiCombat
    {
        public static void AnnounceStopFight(Character npc)
        {
            if (npc.FightingTarget.Instance == 0)
                return;

            npc.Cell?.Announce(
                new StopFightMessage
                {
                    Identity = npc.Identity,
                    Unknown1 = 1
                });
            npc.SetFightingTarget(Identity.None);
        }
    }
}
