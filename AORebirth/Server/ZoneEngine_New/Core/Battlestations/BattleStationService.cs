namespace ZoneEngine_New.Core.Battlestations
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Raids;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>Playfield tick entry. The service is created after the playfield manager, so the tick only calls in.</summary>
    public static class BattleStationClock
    {
        public static BattleStationService? Service { get; set; }

        public static void Tick(Playfield playfield, double deltaTime) => Service?.Tick(playfield, deltaTime);

        public static bool IsChanneling(Player player) => Service?.IsChanneling(player) == true;

        public static void NotifyHit(Character target) => Service?.NotifyHit(target);

        public static bool TryRespawn(Player player, out Vector3 landing)
        {
            landing = new Vector3(0, 0, 0);
            return Service != null && Service.TryRespawn(player, out landing);
        }

        /// <summary>A console dynel was used. True when this click belongs to a live match, so the template's other OnUse spells stay unused.</summary>
        public static bool TryUseConsole(Player player, StaticDynel dynel)
            => Service != null && Service.TryUseConsole(player, dynel);
    }

    /// <summary>
    /// One battle-station match per level bracket. Rules and coordinates are <c>GameData/BattleStations.json</c>.
    /// Red is the arm-A spawn (the map's Omni homebase) and Blue is the arm-B spawn (the map's Clan homebase).
    /// The team is random and is not the character's faction.
    /// </summary>
    public sealed class BattleStationService
    {
        readonly IGameData _gameData;
        readonly IZoneLogger _logger;
        readonly Catalog _catalog;
        readonly object _gate = new();
        readonly Dictionary<int, Queue> _queues = new();
        readonly Dictionary<int, Match> _matches = new();
        readonly HashSet<int> _channeling = new();

        public BattleStationService(IGameData gameData, IZoneLogger logger)
        {
            _gameData = gameData;
            _logger = logger;
            _catalog = Catalog.Load(gameData, logger);
            BattleStationClock.Service = this;
        }

        public void Tick(Playfield playfield, double deltaTime)
        {
            if (playfield == null || deltaTime <= 0 || _catalog.Brackets.Length == 0)
                return;

            if (playfield.Identity.Instance == _catalog.HubPlayfield)
                TickQueues(playfield, deltaTime);

            TickMatch(playfield, deltaTime);
        }

        public void TryQueue(Player player, string terminalName)
        {
            if (player == null)
                return;

            Bracket? bracket = _catalog.FindBracket(terminalName);
            if (bracket == null)
            {
                Say(player, "This terminal is not signed up to a bracket.");
                return;
            }

            int level = player.Stats.GetOrOne(CharacterStat.Level);
            if (level < bracket.MinLevel || level > bracket.MaxLevel)
            {
                Say(player, "This terminal is for levels " + bracket.MinLevel + " to " + bracket.MaxLevel + ".");
                return;
            }

            int loyalty = Loyalty(player);
            if (loyalty <= 0)
            {
                Say(player, "Your battle station loyalty is 0. You cannot queue.");
                return;
            }

            lock (_gate)
            {
                if (FindHuman(player) != null)
                {
                    Say(player, "You are already in a battle station.");
                    return;
                }

                Queue queue = QueueFor(bracket);
                if (queue.Players.Contains(player))
                {
                    Say(player, "You are already queued for " + bracket.Name + ".");
                    return;
                }

                queue.Players.Add(player);
                if (queue.WaitLeft < 0 && !_matches.ContainsKey(bracket.Playfield))
                    queue.WaitLeft = _catalog.QueueWaitSeconds;

                Say(player, "Queued for " + bracket.Name + ". "
                    + queue.Players.Count.ToString(CultureInfo.InvariantCulture)
                    + " waiting. Loyalty " + loyalty.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        public bool IsChanneling(Player player)
        {
            lock (_gate)
                return _channeling.Contains(player.Identity.Instance);
        }

        public void NotifyHit(Character target)
        {
            if (target is not Player player)
                return;

            lock (_gate)
            {
                if (!_channeling.Remove(player.Identity.Instance))
                    return;

                Human? human = FindHuman(player);
                if (human == null)
                    return;

                human.HackConsole = -1;
                human.Confirm = false;
                Say(player, "The hack was interrupted.");
            }
        }

        public bool TryRespawn(Player player, out Vector3 landing)
        {
            landing = new Vector3(0, 0, 0);
            lock (_gate)
            {
                Human? human = FindHuman(player);
                if (human?.Match == null)
                    return false;

                Point spawn = human.Team == 1 ? _catalog.Spawns.Red : _catalog.Spawns.Blue;
                landing = new Vector3(spawn.X, spawn.Y, spawn.Z);
                human.IdleSeconds = 0;
                return true;
            }
        }

        public bool TryUseConsole(Player player, StaticDynel dynel)
        {
            lock (_gate)
            {
                Human? human = FindHuman(player);
                if (human?.Match == null || !ReferenceEquals(player.Playfield, human.Match.Field))
                    return false;

                int console = NearestConsole(dynel.Position.xf, dynel.Position.zf, 15f);
                if (console < 0)
                    return false;

                Touch(human);
                if (human.Confirm && human.HackConsole == console)
                {
                    LockConsole(human.Match, console, human.Team, player.Name ?? "You");
                    human.HackConsole = -1;
                    human.Confirm = false;
                    _channeling.Remove(player.Identity.Instance);
                    return true;
                }

                if (human.HackConsole == console && !human.Confirm)
                {
                    Say(player, "Keep hacking. Click again when the window opens.");
                    return true;
                }

                human.HackConsole = console;
                human.HackLeft = _catalog.HackSeconds;
                human.Confirm = false;
                _channeling.Add(player.Identity.Instance);
                player.StopInPlace();
                Say(player, "Hacking " + _catalog.Consoles[console].Id
                    + ". Stay for 60 seconds, then click it again within 5 seconds. A hit breaks it.");
                return true;
            }
        }

        void TickQueues(Playfield hub, double deltaTime)
        {
            List<Queue>? due = null;
            lock (_gate)
            {
                foreach (Queue queue in _queues.Values)
                {
                    queue.Players.RemoveAll(player => player.Session == null || player.Playfield == null);
                    if (queue.Players.Count == 0)
                    {
                        queue.WaitLeft = -1;
                        queue.Starting = false;
                        continue;
                    }

                    if (_matches.ContainsKey(queue.Bracket.Playfield) || queue.Starting)
                        continue;

                    if (queue.WaitLeft < 0)
                        queue.WaitLeft = _catalog.QueueWaitSeconds;

                    queue.WaitLeft -= deltaTime;
                    if (queue.WaitLeft > 0)
                        continue;

                    queue.Starting = true;
                    (due ??= new List<Queue>()).Add(queue);
                }
            }

            if (due == null)
                return;

            for (int i = 0; i < due.Count; i++)
                Begin(hub, due[i]);
        }

        void Begin(Playfield hub, Queue queue)
        {
            PlayfieldManager manager = hub.GetRequiredService<PlayfieldManager>();
            Player? anchor = null;
            lock (_gate)
            {
                for (int i = 0; i < queue.Players.Count; i++)
                {
                    if (queue.Players[i].Session != null)
                    {
                        anchor = queue.Players[i];
                        break;
                    }
                }
            }

            if (anchor == null)
            {
                lock (_gate)
                    queue.Starting = false;
                return;
            }

            bool queued = manager.WithPlayfield(queue.Bracket.Playfield, anchor, field => Open(queue, field));
            if (!queued)
            {
                lock (_gate)
                    queue.Starting = false;
            }
        }

        void Open(Queue queue, Playfield field)
        {
            List<Player> humans;
            lock (_gate)
            {
                if (_matches.ContainsKey(queue.Bracket.Playfield))
                {
                    queue.Starting = false;
                    return;
                }

                humans = new List<Player>();
                for (int i = 0; i < queue.Players.Count; i++)
                {
                    Player player = queue.Players[i];
                    int level = player.Stats.GetOrOne(CharacterStat.Level);
                    if (player.Session == null || level < queue.Bracket.MinLevel || level > queue.Bracket.MaxLevel)
                        continue;
                    if (Loyalty(player) < _catalog.LoyaltyAcceptCost)
                    {
                        Say(player, "Your loyalty is too low to accept this match.");
                        continue;
                    }

                    humans.Add(player);
                }

                queue.Players.Clear();
                queue.WaitLeft = -1;
                queue.Starting = false;
                if (humans.Count == 0)
                    return;
            }

            var order = new List<int>(humans.Count);
            for (int i = 0; i < humans.Count; i++)
                order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int swap = Random.Shared.Next(i + 1);
                (order[i], order[swap]) = (order[swap], order[i]);
            }

            bool fill = humans.Count <= _catalog.BotFillMaxHumans;
            int seats = fill ? _catalog.TeamSize * 2 : humans.Count;
            if (seats > humans.Count && !fill)
                seats = humans.Count;
            if (!fill && humans.Count > _catalog.TeamSize * 2)
            {
                lock (_gate)
                {
                    for (int i = _catalog.TeamSize * 2; i < order.Count; i++)
                        queue.Players.Add(humans[order[i]]);
                    queue.WaitLeft = _catalog.QueueWaitSeconds;
                }

                seats = _catalog.TeamSize * 2;
            }

            var match = new Match(queue.Bracket, field, _catalog.Consoles.Length);
            var red = new List<Player>();
            var blue = new List<Player>();
            for (int i = 0; i < seats && i < order.Count; i++)
            {
                Player player = humans[order[i]];
                int team = i % 2 == 0 ? 1 : 2;
                SetLoyalty(player, Loyalty(player) - _catalog.LoyaltyAcceptCost);
                player.Stats.Set(CharacterStat.BattlestationSide, team, StatDetail.Base, dirty: true);
                match.Humans.Add(new Human(player, team, match));
                (team == 1 ? red : blue).Add(player);
            }

            if (fill)
            {
                SpawnBots(match, 1, _catalog.TeamSize - red.Count);
                SpawnBots(match, 2, _catalog.TeamSize - blue.Count);
            }

            lock (_gate)
                _matches[queue.Bracket.Playfield] = match;

            Point redSpawn = _catalog.Spawns.Red;
            Point blueSpawn = _catalog.Spawns.Blue;
            for (int i = 0; i < match.Humans.Count; i++)
            {
                Human human = match.Humans[i];
                Point at = human.Team == 1 ? redSpawn : blueSpawn;
                Say(human.Player, (human.Team == 1 ? "Red" : "Blue")
                    + " team. Core is worth 2 points a tick, each arm 1. First to "
                    + _catalog.ScoreCap.ToString(CultureInfo.InvariantCulture) + " wins.");
                Transfer(human.Player, field, at);
            }

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Battle station {0} started humans={1} bots={2}",
                queue.Bracket.Name, match.Humans.Count, match.Bots.Count));
        }

        void SpawnBots(Match match, int team, int count)
        {
            if (count <= 0 || _catalog.BotHashes.Length == 0)
                return;

            SpawnService spawn = match.Field.GetRequiredService<SpawnService>();
            Point at = team == 1 ? _catalog.Spawns.Red : _catalog.Spawns.Blue;
            var home = new Vector3(at.X, at.Y, at.Z);
            for (int i = 0; i < count; i++)
            {
                string hash = _catalog.BotHashes[i % _catalog.BotHashes.Length];
                if (!_gameData.TryResolveMobTemplate(hash, match.Bracket.MaxLevel, out MobTemplate? template) || template == null)
                {
                    _logger.Warn("Battle station bot template missing: " + hash);
                    continue;
                }

                int slot = i;
                int teamId = team;
                NpcCharacter npc;
                try
                {
                    npc = spawn.SpawnMob(
                        template,
                        home,
                        level: match.Bracket.MaxLevel,
                        spawnSource: SpawnSource.None,
                        spawnHash: hash,
                        configure: body => Dress(body, teamId, slot));
                }
                catch (Exception exception)
                {
                    _logger.Warn("Battle station bot failed to spawn: " + exception.Message);
                    continue;
                }

                var bot = new Bot(npc, teamId, teamId == 1 ? 0 : 1);
                npc.Died += _ => bot.Dead = true;
                match.Bots.Add(bot);
            }
        }

        void Dress(NpcCharacter body, int team, int slot)
        {
            body.Name = (team == 1 ? "Red" : "Blue") + " Guard";
            body.Stats.Set(CharacterStat.BattlestationSide, team);
            body.Stats.Set(CharacterStat.Side, (int)Side.Neutral);
            body.Stats.Set(CharacterStat.BreedHostility, Math.Max(1, body.Stats.GetOrZero(CharacterStat.BreedHostility)));
            body.Stats.Set(CharacterStat.ProximityRangeOutdoors, 30);
            body.Stats.Set(CharacterStat.ProximityRangeIndoors, 30);
            if (body.Brain != null)
            {
                body.Brain.ReleaseHome();
                body.Brain.PatrolEnabled = true;
                body.Brain.PatrolWaypoints = new[] { body.Position };
            }

            _ = slot;
        }

        void TickMatch(Playfield field, double deltaTime)
        {
            Match? match;
            lock (_gate)
            {
                if (!_matches.TryGetValue(field.Identity.Instance, out match) || !ReferenceEquals(match.Field, field))
                    return;
            }

            if (match.Ending)
                return;

            ExpireHacks(match, deltaTime);
            Score(match, deltaTime);
            TickHumans(match, deltaTime);
            TickBots(match, deltaTime);

            if (match.RedScore >= _catalog.ScoreCap || match.BlueScore >= _catalog.ScoreCap)
                Finish(match, match.RedScore >= _catalog.ScoreCap ? 1 : 2);
            else if (!AnyHumanPresent(match))
                Finish(match, 0);
        }

        void ExpireHacks(Match match, double deltaTime)
        {
            lock (_gate)
            {
                for (int i = 0; i < match.Humans.Count; i++)
                {
                    Human human = match.Humans[i];
                    if (human.HackConsole < 0)
                        continue;

                    human.HackLeft -= deltaTime;
                    if (human.HackLeft > 0)
                        continue;

                    if (!human.Confirm)
                    {
                        human.Confirm = true;
                        human.HackLeft = _catalog.ConfirmSeconds;
                        _channeling.Remove(human.Player.Identity.Instance);
                        Say(human.Player, "Click the console again to lock it.");
                    }
                    else
                    {
                        human.HackConsole = -1;
                        human.Confirm = false;
                        Say(human.Player, "The lock window closed.");
                    }
                }
            }

            for (int i = 0; i < match.Bots.Count; i++)
            {
                Bot bot = match.Bots[i];
                if (bot.Dead || bot.HackConsole < 0)
                    continue;

                bot.HackLeft -= deltaTime;
                if (bot.HackLeft > 0)
                    continue;

                LockConsole(match, bot.HackConsole, bot.Team, bot.Npc.Name ?? "A guard");
                bot.HackConsole = -1;
                if (bot.Npc.Brain != null)
                    bot.Npc.Brain.PatrolEnabled = true;
            }
        }

        void Score(Match match, double deltaTime)
        {
            int redHeld = 0;
            int blueHeld = 0;
            int redPoints = 0;
            int bluePoints = 0;
            for (int i = 0; i < match.Owner.Length; i++)
            {
                if (match.Owner[i] == 1)
                {
                    redHeld++;
                    redPoints += _catalog.Consoles[i].Points;
                }
                else if (match.Owner[i] == 2)
                {
                    blueHeld++;
                    bluePoints += _catalog.Consoles[i].Points;
                }
            }

            bool dominated = redHeld == match.Owner.Length || blueHeld == match.Owner.Length;
            double interval = dominated ? _catalog.DominatedTickSeconds : _catalog.TickSeconds;
            match.ScoreDebt += deltaTime;
            if (match.ScoreDebt < interval)
                return;

            match.ScoreDebt = 0;
            match.RedScore += redPoints;
            match.BlueScore += bluePoints;
            match.AnnounceDebt += interval;
            if (match.AnnounceDebt < 30)
                return;

            match.AnnounceDebt = 0;
            Announce(match, "Score  Red " + match.RedScore + "  Blue " + match.BlueScore);
        }

        void TickHumans(Match match, double deltaTime)
        {
            for (int i = match.Humans.Count - 1; i >= 0; i--)
            {
                Human human = match.Humans[i];
                Player player = human.Player;
                if (player.Session == null || player.Playfield == null)
                {
                    DropHuman(match, human, "You left the battle station. No payout.");
                    continue;
                }

                if (!ReferenceEquals(player.Playfield, match.Field))
                {
                    DropHuman(match, human, "You left the battle station. No payout.");
                    continue;
                }

                float x = player.Position.xf;
                float z = player.Position.zf;
                float dx = x - human.LastX;
                float dz = z - human.LastZ;
                if ((dx * dx) + (dz * dz) > 4f || human.HackConsole >= 0 || player.FightingTarget != Identity.None)
                {
                    human.LastX = x;
                    human.LastZ = z;
                    human.IdleSeconds = 0;
                }
                else
                {
                    human.IdleSeconds += deltaTime;
                    if (human.IdleSeconds >= _catalog.IdleSeconds)
                    {
                        DropHuman(match, human, "You were idle for 3 minutes and were sent back.");
                        SendHome(player);
                    }
                }
            }
        }

        void TickBots(Match match, double deltaTime)
        {
            for (int i = 0; i < match.Bots.Count; i++)
            {
                Bot bot = match.Bots[i];
                if (bot.Dead)
                {
                    bot.RespawnLeft -= deltaTime;
                    if (bot.RespawnLeft > 0)
                        continue;

                    bot.RespawnLeft = 10;
                    try
                    {
                        match.Field.GetRequiredService<SpawnService>().DespawnNpc(bot.Npc);
                    }
                    catch (Exception exception)
                    {
                        _logger.Warn("Battle station bot despawn failed: " + exception.Message);
                    }

                    int count = 1;
                    int before = match.Bots.Count;
                    SpawnBots(match, bot.Team, count);
                    if (match.Bots.Count > before)
                    {
                        Bot born = match.Bots[match.Bots.Count - 1];
                        match.Bots.RemoveAt(match.Bots.Count - 1);
                        bot.Npc = born.Npc;
                        bot.Dead = false;
                        bot.HackConsole = -1;
                        bot.Npc.Died += _ => bot.Dead = true;
                    }

                    continue;
                }

                if (bot.Npc.IsDead)
                {
                    bot.Dead = true;
                    bot.RespawnLeft = 10;
                    continue;
                }

                if (bot.Npc.FightingTarget != Identity.None)
                {
                    if (bot.HackConsole >= 0)
                    {
                        bot.HackConsole = -1;
                        if (bot.Npc.Brain != null)
                            bot.Npc.Brain.PatrolEnabled = true;
                    }

                    continue;
                }

                StationConsole goal = _catalog.Consoles[bot.Goal];
                if (match.Owner[bot.Goal] == bot.Team)
                    bot.Goal = NextGoal(match, bot.Team);

                goal = _catalog.Consoles[bot.Goal];
                float x = bot.Npc.Position.xf;
                float z = bot.Npc.Position.zf;
                float goalDist = Distance(x, z, goal.X, goal.Z);
                if (goalDist < 6f && bot.HackConsole < 0 && match.Owner[bot.Goal] != bot.Team)
                {
                    bot.HackConsole = bot.Goal;
                    bot.HackLeft = _catalog.HackSeconds;
                    if (bot.Npc.Brain != null)
                        bot.Npc.Brain.PatrolEnabled = false;
                    bot.Npc.StopInPlace();
                    continue;
                }

                if (bot.HackConsole >= 0)
                    continue;

                Steer(bot, goal);
            }
        }

        int NextGoal(Match match, int team)
        {
            if (match.Owner[0] != team)
                return 0;

            for (int i = 1; i < match.Owner.Length; i++)
            {
                if (match.Owner[i] != team)
                    return i;
            }

            return 0;
        }

        void Steer(Bot bot, StationConsole goal)
        {
            float x = bot.Npc.Position.xf;
            float z = bot.Npc.Position.zf;
            float goalDist = Distance(x, z, goal.X, goal.Z);
            WarpEdge? hop = null;
            float hopPad = float.MaxValue;
            for (int i = 0; i < _catalog.Warps.Length; i++)
            {
                WarpEdge edge = _catalog.Warps[i];
                float toGoal = Distance(edge.ToX, edge.ToZ, goal.X, goal.Z);
                if (toGoal >= goalDist - 8f)
                    continue;

                float pad = Distance(x, z, edge.X, edge.Z);
                if (pad < hopPad)
                {
                    hopPad = pad;
                    hop = edge;
                }
            }

            if (hop != null && hopPad < 6f)
            {
                bot.Npc.Motor.Warp(new Vector3(hop.ToX, hop.ToY, hop.ToZ));
                return;
            }

            float destX = hop != null ? hop.X : goal.X;
            float destY = hop != null ? hop.Y : goal.Y;
            float destZ = hop != null ? hop.Z : goal.Z;
            if (bot.Npc.Brain != null)
            {
                bot.Npc.Brain.PatrolEnabled = true;
                bot.Npc.Brain.PatrolWaypoints = new[] { new Vector3(destX, destY, destZ) };
            }
        }

        void LockConsole(Match match, int console, int team, string who)
        {
            if (console < 0 || console >= match.Owner.Length)
                return;

            match.Owner[console] = team;
            Announce(match, who + " locked " + _catalog.Consoles[console].Id
                + " for " + (team == 1 ? "Red" : "Blue") + ".");
        }

        void Finish(Match match, int winner)
        {
            if (match.Ending)
                return;

            match.Ending = true;
            string text = winner == 0
                ? "The match ended with no one left."
                : (winner == 1 ? "Red" : "Blue") + " wins.  Red " + match.RedScore + "  Blue " + match.BlueScore + ".";
            Announce(match, text);

            var stat = (CharacterStat)InvasionService.VictoryPointsStatId;
            for (int i = match.Humans.Count - 1; i >= 0; i--)
            {
                Human human = match.Humans[i];
                if (winner != 0 && human.Player.Session != null && ReferenceEquals(human.Player.Playfield, match.Field))
                {
                    int per = human.Team == winner ? _catalog.WinVpPerLevel : _catalog.LossVpPerLevel;
                    int amount = Math.Max(1, human.Player.Stats.GetOrOne(CharacterStat.Level) * per);
                    int total = human.Player.Stats.GetOrZero(stat, StatDetail.Base) + amount;
                    human.Player.Stats.Set(stat, total, StatDetail.Base);
                    human.Player.SaveState.MarkDirty();
                    int loyalty = Math.Min(_catalog.LoyaltyMax, Loyalty(human.Player) + _catalog.LoyaltyComplete);
                    SetLoyalty(human.Player, loyalty);
                    Say(human.Player, "Victory Points +" + amount + " (total " + total + "). Loyalty " + loyalty + ".");
                }

                ClearSide(human.Player);
                lock (_gate)
                    _channeling.Remove(human.Player.Identity.Instance);
                if (human.Player.Session != null && ReferenceEquals(human.Player.Playfield, match.Field))
                    SendHome(human.Player);
            }

            SpawnService? spawn = match.Field.GetService<SpawnService>();
            for (int i = 0; i < match.Bots.Count; i++)
            {
                if (!match.Bots[i].Dead)
                    spawn?.DespawnNpc(match.Bots[i].Npc);
            }

            lock (_gate)
            {
                match.Humans.Clear();
                _matches.Remove(match.Bracket.Playfield);
            }
        }

        void DropHuman(Match match, Human human, string text)
        {
            ClearSide(human.Player);
            lock (_gate)
            {
                _channeling.Remove(human.Player.Identity.Instance);
                match.Humans.Remove(human);
            }

            if (human.Player.Session != null)
                Say(human.Player, text);
        }

        bool AnyHumanPresent(Match match)
        {
            for (int i = 0; i < match.Humans.Count; i++)
            {
                if (match.Humans[i].Player.Session != null
                    && ReferenceEquals(match.Humans[i].Player.Playfield, match.Field))
                    return true;
            }

            return false;
        }

        void SendHome(Player player)
        {
            if (player.Playfield == null)
                return;

            Point at = HubPoint();
            Playfield? hub = HubField(player.Playfield);
            if (hub != null)
            {
                Transfer(player, hub, at);
                return;
            }

            player.Playfield.GetRequiredService<PlayfieldManager>().WithPlayfield(
                _catalog.HubPlayfield,
                player,
                field => Transfer(player, field, at));
        }

        void Transfer(Player player, Playfield destination, Point at)
        {
            if (player.Session == null || player.Playfield == null)
                return;

            if (ReferenceEquals(player.Playfield, destination))
            {
                player.Motor.Warp(new Vector3(at.X, at.Y, at.Z));
                return;
            }

            try
            {
                player.Session.TransferToPlayfield(destination, new Vector3(at.X, at.Y, at.Z));
            }
            catch (Exception exception)
            {
                _logger.Warn("Battle station transfer failed: " + exception.Message);
            }
        }

        Playfield? HubField(Playfield from)
        {
            PlayfieldManager manager = from.GetRequiredService<PlayfieldManager>();
            return manager.TryGet(_catalog.HubPlayfield, out Playfield? hub) ? hub : null;
        }

        Point HubPoint() => new(_catalog.HubReturn[0], _catalog.HubReturn[1], _catalog.HubReturn[2]);

        Human? FindHuman(Player player)
        {
            foreach (Match match in _matches.Values)
            {
                for (int i = 0; i < match.Humans.Count; i++)
                {
                    if (ReferenceEquals(match.Humans[i].Player, player))
                        return match.Humans[i];
                }
            }

            return null;
        }

        Queue QueueFor(Bracket bracket)
        {
            if (!_queues.TryGetValue(bracket.Playfield, out Queue? queue))
            {
                queue = new Queue(bracket);
                _queues[bracket.Playfield] = queue;
            }

            return queue;
        }

        int NearestConsole(float x, float z, float max)
        {
            int best = -1;
            float bestDist = max;
            for (int i = 0; i < _catalog.Consoles.Length; i++)
            {
                float dist = Distance(x, z, _catalog.Consoles[i].X, _catalog.Consoles[i].Z);
                if (dist <= bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            return best;
        }

        int Loyalty(Player player)
        {
            int raw = player.Stats.Get(CharacterStat.BattlestationRep);
            return StatCollection.IsUnset(raw) ? _catalog.LoyaltyStart : raw;
        }

        void SetLoyalty(Player player, int value)
        {
            player.Stats.Set(CharacterStat.BattlestationRep, Math.Max(0, value), StatDetail.Base, dirty: true);
            player.SaveState.MarkDirty();
        }

        static void ClearSide(Player player)
        {
            player.Stats.Set(CharacterStat.BattlestationSide, 0, StatDetail.Base, dirty: true);
            player.SaveState.MarkDirty();
        }

        static void Touch(Human human)
        {
            human.IdleSeconds = 0;
            human.LastX = human.Player.Position.xf;
            human.LastZ = human.Player.Position.zf;
        }

        static void Announce(Match match, string text)
        {
            for (int i = 0; i < match.Humans.Count; i++)
                Say(match.Humans[i].Player, text);
        }

        static void Say(Player player, string text)
        {
            player.Session?.Send(new ChatTextMessage
            {
                Identity = player.Identity,
                Text = text
            });
        }

        static float Distance(float x, float z, float x2, float z2)
        {
            float dx = x - x2;
            float dz = z - z2;
            return MathF.Sqrt((dx * dx) + (dz * dz));
        }

        sealed class Match
        {
            public Match(Bracket bracket, Playfield field, int consoles)
            {
                Bracket = bracket;
                Field = field;
                Owner = new int[consoles];
            }

            public Bracket Bracket { get; }

            public Playfield Field { get; }

            public int[] Owner { get; }

            public int RedScore;

            public int BlueScore;

            public double ScoreDebt;

            public double AnnounceDebt;

            public bool Ending;

            public List<Human> Humans { get; } = new();

            public List<Bot> Bots { get; } = new();
        }

        sealed class Human
        {
            public Human(Player player, int team, Match match)
            {
                Player = player;
                Team = team;
                Match = match;
                LastX = player.Position.xf;
                LastZ = player.Position.zf;
            }

            public Player Player { get; }

            public int Team { get; }

            public Match Match { get; }

            public double IdleSeconds;

            public float LastX;

            public float LastZ;

            public int HackConsole = -1;

            public double HackLeft;

            public bool Confirm;
        }

        sealed class Bot
        {
            public Bot(NpcCharacter npc, int team, int goal)
            {
                Npc = npc;
                Team = team;
                Goal = goal;
            }

            public NpcCharacter Npc;

            public int Team { get; }

            public int Goal;

            public int HackConsole = -1;

            public double HackLeft;

            public double RespawnLeft = 10;

            public bool Dead;
        }

        sealed class Queue
        {
            public Queue(Bracket bracket) => Bracket = bracket;

            public Bracket Bracket { get; }

            public List<Player> Players { get; } = new();

            public double WaitLeft = -1;

            public bool Starting;
        }
    }

    sealed class Catalog
    {
        public int HubPlayfield { get; set; }

        public float[] HubReturn { get; set; } = [219f, 6.02f, 190f];

        public int ScoreCap { get; set; } = 500;

        public double TickSeconds { get; set; } = 10;

        public double DominatedTickSeconds { get; set; } = 2.5;

        public double HackSeconds { get; set; } = 60;

        public double ConfirmSeconds { get; set; } = 5;

        public double IdleSeconds { get; set; } = 180;

        public double QueueWaitSeconds { get; set; } = 45;

        public int TeamSize { get; set; } = 6;

        public int BotFillMaxHumans { get; set; } = 3;

        public int LoyaltyStart { get; set; } = 10;

        public int LoyaltyAcceptCost { get; set; } = 1;

        public int LoyaltyComplete { get; set; } = 2;

        public int LoyaltyMax { get; set; } = 10;

        public int WinVpPerLevel { get; set; } = 4;

        public int LossVpPerLevel { get; set; } = 1;

        public string[] BotHashes { get; set; } = [];

        public StationConsole[] Consoles { get; set; } = [];

        public Spawns Spawns { get; set; } = new();

        public WarpEdge[] Warps { get; set; } = [];

        public Bracket[] Brackets { get; set; } = [];

        public Bracket? FindBracket(string name)
        {
            for (int i = 0; i < Brackets.Length; i++)
            {
                if (string.Equals(Brackets[i].Name, name, StringComparison.Ordinal))
                    return Brackets[i];
            }

            return null;
        }

        public static Catalog Load(IGameData gameData, IZoneLogger logger)
        {
            string path = Path.Combine(gameData.RootPath, "BattleStations.json");
            try
            {
                Catalog? catalog = JsonSerializer.Deserialize<Catalog>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (catalog == null || catalog.Brackets.Length == 0 || catalog.Consoles.Length != 4)
                    throw new InvalidDataException("BattleStations.json needs 7 brackets and 4 consoles.");

                return catalog;
            }
            catch (Exception exception)
            {
                logger.Warn("BattleStations.json unreadable: " + exception.Message);
                return new Catalog();
            }
        }
    }

    sealed class StationConsole
    {
        public string Id { get; set; } = "";

        public int Points { get; set; }

        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }
    }

    sealed class Spawns
    {
        public Point Red { get; set; } = new();

        public Point Blue { get; set; } = new();
    }

    sealed class Point
    {
        public Point()
        {
        }

        public Point(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }
    }

    sealed class WarpEdge
    {
        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }

        public float ToX { get; set; }

        public float ToY { get; set; }

        public float ToZ { get; set; }
    }

    sealed class Bracket
    {
        public string Name { get; set; } = "";

        public int MinLevel { get; set; }

        public int MaxLevel { get; set; }

        public int Playfield { get; set; }
    }
}
