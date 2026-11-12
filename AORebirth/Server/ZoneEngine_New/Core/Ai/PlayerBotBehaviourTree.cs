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
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Playfield.Locality;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Starter playerbot brain: one prioritized action loop (rest → fight → engage → roam → idle).
    /// Uses the same chase/fight leaves as ordinary NPCs; pathing falls back to straight line when no navmesh.
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

            IParent combat = root.Conditional(_brain.HasNearbyHate, "has-nearby-hate")
                .Sequence("combat")
                .Attach(new SelectHighestThreatNode(_brain));
            combat.Sequence("default-fight")
                .Attach(new MoveTowardTargetNode(_brain))
                .Attach(new StartFightingNode(_brain));

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

            var locality = _brain.Npc.Playfield.GetRequiredService<PlayfieldLocality>();
            locality.CollectNearby(_brain.Npc, state.EngageRange, _scratch);
            NpcCharacter? prey = null;
            double best = double.MaxValue;
            for (int i = 0; i < _scratch.Count; i++)
            {
                NpcCharacter other = _scratch[i];
                if (ReferenceEquals(other, _brain.Npc) || other.IsDead || !other.Attackable)
                    continue;
                if (other.Brain?.PlayerBot != null)
                    continue;
                if (NpcSupportRules.IsPlayerPetOrMerc(other) || other.PetOwner is Player)
                    continue;
                if (other.MobTemplate != null
                    && (other.MobTemplate.Has(NpcFeature.NoCombat)
                        || other.MobTemplate.Has(NpcFeature.PlayerUntargetable)
                        || other.MobTemplate.Has(NpcFeature.Immortal)))
                    continue;
                if (CombatRules.IsBlueNameNpc(other.Stats.GetOrZero(CharacterStat.Flags)))
                    continue;
                if (!CombatRules.CanAttack(_brain.Npc, other))
                    continue;

                int level = other.Stats.GetOrZero(CharacterStat.Level);
                if (level < state.MinPreyLevel || level > state.MaxPreyLevel + 3)
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
            _brain.Npc.StartFighting(prey.Identity, 0);
            return _nodeState = NodeState.Success;
        }
    }

    /// <summary>Walk the configured POI list (navmesh when present, otherwise straight-line PathTo).</summary>
    sealed class PlayerBotRoamNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public PlayerBotRoamNode(NpcBrain brain)
            : base("playerbot-roam")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.playerbot-roam", _brain.Npc.Identity.Instance);
            PlayerBotState? state = _brain.PlayerBot;
            if (state == null || state.Waypoints.Count == 0)
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
                state.WaypointIndex = (state.WaypointIndex + 1) % state.Waypoints.Count;
            }

            if (state.WaypointIndex < 0 || state.WaypointIndex >= state.Waypoints.Count)
                state.WaypointIndex = 0;

            Vector3 dest = state.Waypoints[state.WaypointIndex];
            if (NpcAiRules.IsNearby(_brain.Npc.Position, dest, NpcAiRules.ArriveHomeMeters))
            {
                _brain.StopPathing();
                state.RoamPausing = true;
                double pause = state.RoamPauseMinSeconds
                    + Random.Shared.NextDouble() * Math.Max(0, state.RoamPauseMaxSeconds - state.RoamPauseMinSeconds);
                state.RoamPauseUntilUtc = now.AddSeconds(pause);
                return _nodeState = NodeState.Running;
            }

            if (_brain.Npc.Motor.State != Movement.MovementState.Walk)
                _brain.Npc.Motor.ApplyAction(Movement.MovementAction.SwitchToWalk);
            _brain.PathTo(dest);
            return _nodeState = NodeState.Running;
        }
    }
}
