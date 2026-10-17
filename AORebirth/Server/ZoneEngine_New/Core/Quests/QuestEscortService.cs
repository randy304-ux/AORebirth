namespace ZoneEngine_New.Core.Quests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Playfield;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// A quest item used on a recruit makes that NPC follow the player until it reaches the destination NPC.
    /// The rules live in GameData/EscortQuests.json.
    /// </summary>
    public sealed class QuestEscortService
    {
        const double FollowMeters = 3.5;
        const double HomeMeters = 2.0;
        const float ReplanMeters = 3f;

        readonly Playfield _playfield;
        readonly DynelRegistry _registry;
        readonly List<EscortRule> _rules;
        readonly List<Active> _active = new();

        public QuestEscortService(Playfield playfield, DynelRegistry registry, IGameData gameData)
        {
            _playfield = playfield ?? throw new ArgumentNullException(nameof(playfield));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _rules = Load(gameData?.RootPath);
        }

        /// <summary>Use-item-on-NPC. True when this service accepted the use.</summary>
        public bool TryRecruit(Player player, Identity sourceSlot, Identity target)
        {
            if (player.IsDead || !player.Inventory.IsHydrated || sourceSlot.Type != IdentityType.Inventory)
                return false;
            if (!_registry.TryGet(target, out Dynel? dynel) || dynel is not NpcCharacter recruit)
                return false;
            if (!ReferenceEquals(recruit.Playfield, _playfield) || recruit.IsDead)
                return false;
            if (!player.Inventory.TryGetItem(sourceSlot.Type, sourceSlot.Instance, out Item source) || source.Locked)
                return false;

            EscortRule? rule = null;
            for (int i = 0; i < _rules.Count; i++)
            {
                if (_rules[i].Matches(source, recruit.SpawnHash))
                {
                    rule = _rules[i];
                    break;
                }
            }

            if (rule == null)
                return false;

            QuestService? quests = _playfield.GetService<QuestService>();
            if (quests == null || !quests.GetLog(player).Quests.TryGetValue(rule.Quest, out PlayerQuest? quest) || !quest.IsActive)
            {
                Say(player, "That does nothing right now.");
                return true;
            }

            for (int i = 0; i < _active.Count; i++)
            {
                if (ReferenceEquals(_active[i].Npc, recruit))
                {
                    Say(player, "He is already coming with you.");
                    return true;
                }
            }

            var job = new Active(recruit, player, rule, recruit.Position);
            job.SavedRun = recruit.Stats.GetOrZero(CharacterStat.RunSpeed);
            if (recruit.IsImmobile && recruit.Stats.TryGetValue(CharacterStat.Features, out int features))
            {
                job.SavedFeatures = features;
                recruit.Stats.Set(CharacterStat.Features, features | MonsterBodyFeatures.CanMove);
            }

            int run = player.Stats.GetOrZero(CharacterStat.RunSpeed);
            if (run > job.SavedRun)
                recruit.Stats.Set(CharacterStat.RunSpeed, run);
            _active.Add(job);
            Say(player, "He follows you. Stay close and take him to the one you were sent for. He will not wait if you wander off.");
            return true;
        }

        public void Tick()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Active job = _active[i];
                if (!StillHere(job.Npc))
                {
                    _active.RemoveAt(i);
                    continue;
                }

                if (job.Returning)
                {
                    if (Vector3.Abs(job.Npc.Position - job.Home) <= HomeMeters)
                    {
                        job.Npc.Motor.ClearPath();
                        Restore(job);
                        _active.RemoveAt(i);
                    }
                    else if (!job.Npc.Motor.HasPath)
                    {
                        job.Npc.Motor.NavigateTo(job.Home);
                    }

                    continue;
                }

                Player player = job.Player;
                if (player.IsDead || !ReferenceEquals(player.Playfield, _playfield))
                {
                    BeginReturn(job);
                    continue;
                }

                if (Vector3.Abs(job.Npc.Position - player.Position) > job.Rule.LeashMeters)
                {
                    Say(player, "He got tired of waiting and went back.");
                    BeginReturn(job);
                    continue;
                }

                NpcCharacter? destination = Find(job.Rule.Destination);
                if (destination != null && job.Npc.GetEdgeDistanceTo(destination) <= job.Rule.ArriveMeters)
                {
                    Arrive(job, player);
                    BeginReturn(job);
                    continue;
                }

                double gap = Vector3.Abs(job.Npc.Position - player.Position);
                if (gap <= FollowMeters)
                {
                    if (job.Npc.Motor.HasPath)
                        job.Npc.Motor.ClearPath();
                    continue;
                }

                Vector3 at = player.Position;
                if (!job.Npc.Motor.HasPath || Vector3.Abs(at - job.Aim) >= ReplanMeters)
                {
                    job.Aim = new Vector3(at.x, at.y, at.z);
                    job.Npc.Motor.NavigateTo(at);
                }
            }
        }

        void Arrive(Active job, Player player)
        {
            if (job.Arrived)
                return;
            job.Arrived = true;
            TakeOne(player, job.Rule.ItemLowId);
            QuestService? quests = _playfield.GetService<QuestService>();
            if (quests != null && quests.TryComplete(player, job.Rule.Quest, out _))
                Say(player, "He makes the arrest. Go back and report.");
            else
                Say(player, "He is there, but the job is already finished.");
        }

        static void BeginReturn(Active job)
        {
            job.Returning = true;
            job.Npc.Motor.NavigateTo(job.Home);
        }

        static void Restore(Active job)
        {
            job.Npc.Stats.Set(CharacterStat.RunSpeed, job.SavedRun);
            if (job.SavedFeatures != null)
                job.Npc.Stats.Set(CharacterStat.Features, job.SavedFeatures.Value);
        }

        bool StillHere(NpcCharacter npc)
            => !npc.IsDead && ReferenceEquals(npc.Playfield, _playfield)
               && _registry.TryGet(npc.Identity, out Dynel? current) && ReferenceEquals(current, npc);

        NpcCharacter? Find(string hash)
        {
            foreach (Dynel dynel in _registry.Dynels())
            {
                if (dynel is NpcCharacter npc && !npc.IsDead && string.Equals(npc.SpawnHash, hash, StringComparison.Ordinal))
                    return npc;
            }

            return null;
        }

        static void TakeOne(Player player, int itemId)
        {
            if (!player.Inventory.IsHydrated)
                return;
            foreach (KeyValuePair<int, Item> entry in player.Inventory.Inventory.Content)
            {
                Item item = entry.Value;
                if (item == null || (item.LowId != itemId && item.HighId != itemId))
                    continue;
                item.DestroyOne(player, new Identity { Type = IdentityType.Inventory, Instance = entry.Key });
                return;
            }
        }

        static void Say(Player player, string text)
        {
            if (player.Session != null)
                Commands.GmCommandFeedback.Send(player.Session, player, text);
        }

        static List<EscortRule> Load(string? root)
        {
            var list = new List<EscortRule>();
            if (string.IsNullOrWhiteSpace(root))
                return list;
            string path = Path.Combine(root, "EscortQuests.json");
            if (!File.Exists(path))
                return list;
            EscortRule[]? rows = JsonSerializer.Deserialize<EscortRule[]>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (rows == null)
                return list;
            foreach (EscortRule row in rows)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.Quest) || row.ItemLowId <= 0
                    || string.IsNullOrWhiteSpace(row.Destination) || row.Recruits == null || row.Recruits.Length == 0)
                    continue;
                if (row.ArriveMeters <= 0)
                    row.ArriveMeters = 8;
                if (row.LeashMeters <= 0)
                    row.LeashMeters = 30;
                list.Add(row);
            }

            return list;
        }

        sealed class EscortRule
        {
            public string Quest { get; set; } = string.Empty;
            public int ItemLowId { get; set; }
            public string[] Recruits { get; set; } = [];
            public string Destination { get; set; } = string.Empty;
            public double ArriveMeters { get; set; }
            public double LeashMeters { get; set; }

            public bool Matches(Item item, string? spawnHash)
            {
                if (item.LowId != ItemLowId && item.HighId != ItemLowId)
                    return false;
                if (string.IsNullOrEmpty(spawnHash))
                    return false;
                for (int i = 0; i < Recruits.Length; i++)
                {
                    if (string.Equals(Recruits[i], spawnHash, StringComparison.Ordinal))
                        return true;
                }

                return false;
            }
        }

        sealed class Active
        {
            public Active(NpcCharacter npc, Player player, EscortRule rule, Vector3 home)
            {
                Npc = npc;
                Player = player;
                Rule = rule;
                Home = new Vector3(home.x, home.y, home.z);
            }

            public NpcCharacter Npc { get; }
            public Player Player { get; }
            public EscortRule Rule { get; }
            public Vector3 Home { get; }
            public Vector3 Aim;
            public int SavedRun;
            public int? SavedFeatures;
            public bool Returning;
            public bool Arrived;
        }
    }
}
