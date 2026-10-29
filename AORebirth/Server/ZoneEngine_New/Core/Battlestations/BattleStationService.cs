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
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Pets;
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

        /// <summary>True when the character is inside the shared Decontamination Room (no combat).</summary>
        public static bool IsInDecontamination(Character character)
            => Service != null && Service.IsInDecontamination(character);

        public static bool TryRespawn(Player player, out Vector3 landing)
        {
            landing = new Vector3(0, 0, 0);
            return Service != null && Service.TryRespawn(player, out landing);
        }

        /// <summary>A console dynel was used. True when this click belongs to a live match, so the template's other OnUse spells stay unused.</summary>
        public static bool TryUseConsole(Player player, StaticDynel dynel)
            => Service != null && Service.TryUseConsole(player, dynel);

        /// <summary>
        /// True when this dynel is a control point in the caller's live match (open station, not Decontamination).
        /// Used to skip the capture terminal's ToUse nano-line requirements before the hack starts.
        /// </summary>
        public static bool IsCaptureConsole(Player player, StaticDynel dynel)
            => Service != null && Service.IsCaptureConsole(player, dynel);
    }

    /// <summary>
    /// One battle-station match per level bracket. Rules and coordinates are <c>GameData/BattleStations.json</c>.
    /// Both teams land in the shared Decontamination Room (district Entry). After <c>StartGateSeconds</c> the
    /// Exit Decontamination pad opens into the contested floor; death returns to Decontamination. No combat there.
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

                Point spawn = SafePoint();
                landing = new Vector3(spawn.X, spawn.Y, spawn.Z);
                human.IdleSeconds = 0;
                human.InField = false;
                return true;
            }
        }

        public bool IsCaptureConsole(Player player, StaticDynel dynel)
        {
            if (player == null || dynel == null)
                return false;
            lock (_gate)
            {
                Human? human = FindHuman(player);
                if (human?.Match == null || !ReferenceEquals(player.Playfield, human.Match.Field))
                    return false;
                return NearestConsole(dynel.Position.xf, dynel.Position.zf, 15f) >= 0;
            }
        }

        public bool TryUseConsole(Player player, StaticDynel dynel)
        {
            lock (_gate)
            {
                Human? human = FindHuman(player);
                if (human?.Match == null || !ReferenceEquals(player.Playfield, human.Match.Field))
                    return false;

                if (!human.InField || !human.Match.FieldOpen || IsInSafe(player.Position.xf, player.Position.zf))
                {
                    Say(player, "Capture points are on the open station. Use the Decontamination pad after the timer.");
                    return true;
                }

                int console = NearestConsole(dynel.Position.xf, dynel.Position.zf, 15f);
                if (console < 0)
                    return false;

                Touch(human);
                Match match = human.Match;
                if (match.Owner[console] == human.Team)
                {
                    Say(player, "Your side already holds " + _catalog.Consoles[console].Id + ".");
                    return true;
                }

                if (match.CaptureLockLeft[console] > 0)
                {
                    int left = Math.Max(1, (int)Math.Ceiling(match.CaptureLockLeft[console]));
                    Say(player, _catalog.Consoles[console].Id + " is locked for "
                        + left.ToString(CultureInfo.InvariantCulture) + " more seconds.");
                    return true;
                }

                if (human.HackConsole == console)
                {
                    Say(player, "Still capturing " + _catalog.Consoles[console].Id
                        + ". Stay alive and in range.");
                    return true;
                }

                if (human.HackConsole >= 0)
                {
                    _channeling.Remove(player.Identity.Instance);
                    human.HackConsole = -1;
                    human.Confirm = false;
                }

                human.HackConsole = console;
                human.HackLeft = _catalog.HackSeconds;
                human.Confirm = false;
                _channeling.Add(player.Identity.Instance);
                player.StopInPlace();
                Say(player, "Capturing " + _catalog.Consoles[console].Id
                    + ". Stay alive and in range for "
                    + _catalog.HackSeconds.ToString(CultureInfo.InvariantCulture)
                    + " seconds. A hit breaks it.");
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

            match.UsesBots = fill;
            int pendingRed = fill ? _catalog.TeamSize - red.Count : 0;
            int pendingBlue = fill ? _catalog.TeamSize - blue.Count : 0;
            match.PendingBotRed = 0;
            match.PendingBotBlue = 0;
            match.StartGateLeft = Math.Max(1, _catalog.StartGateSeconds);

            lock (_gate)
                _matches[queue.Bracket.Playfield] = match;

            // Spawn defenders as soon as the match field exists so empty seats are filled before the
            // gate opens (ao-universe: bots pad each team to TeamSize). Pending was previously only
            // flushed after StartGateSeconds, so a stuck gate / early leave looked like "no bots".
            if (fill)
            {
                SpawnBots(match, 1, pendingRed);
                SpawnBots(match, 2, pendingBlue);
            }

            Point safe = SafePoint();
            for (int i = 0; i < match.Humans.Count; i++)
            {
                Human human = match.Humans[i];
                human.InField = false;
                Say(human.Player, SideLabel(human.Team)
                    + " team. Decontamination Room: buff up (safe zone). When the "
                    + match.StartGateLeft.ToString(CultureInfo.InvariantCulture)
                    + "s timer ends, step the teleport pad into the station. Core is worth 2 points a tick, each arm 1. First to "
                    + _catalog.ScoreCap.ToString(CultureInfo.InvariantCulture) + " wins.");
                Transfer(human.Player, field, safe);
                RestoreSafeVitals(human.Player);
            }

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Battle station {0} started humans={1} bots={2} gate={3}s",
                queue.Bracket.Name, match.Humans.Count, match.Bots.Count, match.StartGateLeft));
        }

        void SpawnBots(Match match, int team, int count)
        {
            if (count <= 0 || _catalog.BotHashes.Length == 0)
                return;

            SpawnService spawn = match.Field.GetRequiredService<SpawnService>();
            int level = match.Bracket.MaxLevel;
            for (int i = 0; i < count; i++)
            {
                // Same TELEPORT LOCATIONS destinations players get from the Decontamination exit pad.
                Point at = PickArm();
                var home = new Vector3(at.X, at.Y, at.Z);
                string hash = _catalog.BotHashes[Random.Shared.Next(_catalog.BotHashes.Length)];
                if (!_gameData.TryResolveMobTemplate(hash, level, out MobTemplate? template) || template == null)
                {
                    _logger.Warn("Battle station bot template missing: " + hash);
                    continue;
                }

                int slot = match.Bots.Count + i;
                int teamId = team;
                string botHash = hash;
                int botLevel = level;
                NpcCharacter npc;
                try
                {
                    npc = spawn.SpawnMob(
                        template,
                        home,
                        level: botLevel,
                        spawnSource: SpawnSource.None,
                        spawnHash: botHash,
                        configure: body => Dress(body, teamId, slot, botHash, botLevel));
                }
                catch (Exception exception)
                {
                    _logger.Warn("Battle station bot failed to spawn: " + exception.Message);
                    continue;
                }

                BotRole role = PickRole();
                var bot = new Bot(npc, teamId, teamId == 1 ? 0 : 1, role);
                AssignHunterPack(match, bot);
                npc.Name = (npc.Name ?? "Guard") + " [" + bot.Role + "]";
                npc.Died += _ => bot.Dead = true;
                match.Bots.Add(bot);
                _logger.Info(string.Format(CultureInfo.InvariantCulture,
                    "Battle station bot role={0} team={1} pack={2} name={3}",
                    bot.Role, teamId, bot.PackId, npc.Name));
            }
        }

        static BotRole PickRole()
        {
            // Capper 35 / Defender 15 / Hunter 25 / Flanker 25.
            int roll = Random.Shared.Next(100);
            if (roll < 35)
                return BotRole.Capper;
            if (roll < 50)
                return BotRole.Defender;
            if (roll < 75)
                return BotRole.Hunter;
            return BotRole.Flanker;
        }

        void AssignHunterPack(Match match, Bot bot)
        {
            if (bot.Role != BotRole.Hunter)
                return;

            const int packMax = 4;
            int openPack = -1;
            bool openNeedsLeader = false;
            for (int p = 0; p < match.NextPackId; p++)
            {
                int count = 0;
                bool hasLeader = false;
                for (int i = 0; i < match.Bots.Count; i++)
                {
                    Bot other = match.Bots[i];
                    if (other.Dead || other.Team != bot.Team || other.Role != BotRole.Hunter || other.PackId != p)
                        continue;
                    count++;
                    if (other.PackLeader)
                        hasLeader = true;
                }

                if (count > 0 && count < packMax)
                {
                    openPack = p;
                    openNeedsLeader = !hasLeader;
                    break;
                }
            }

            if (openPack >= 0)
            {
                bot.PackId = openPack;
                bot.PackLeader = openNeedsLeader;
                return;
            }

            // Start a new pack (fills toward 3-4 as more hunters spawn on this team).
            bot.PackId = match.NextPackId++;
            bot.PackLeader = true;
        }

        void Dress(NpcCharacter body, int team, int slot, string hash, int level)
        {
            string tag = GuardNames[Math.Abs(slot) % GuardNames.Length];
            body.Name = (team == 1 ? "Red" : "Blue") + " Guard " + tag;
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

            // Merc DressMerc path (basic): profession armor band + held/combat weapon for this level bracket.
            Mercs.Kind? kind = Mercs.ByHash(hash);
            if (kind == null)
                return;

            Mercs.Catalog catalog = Mercs.Load(_gameData.RootPath);
            body.WearsArmorStats = catalog.ArmorStats;
            int ql = Math.Clamp(level, 1, 300);
            int worn = 0;
            foreach (List<int> pair in Mercs.ArmorFor(_gameData.RootPath, kind, level, ownerSide: 0))
            {
                if (pair == null || pair.Count == 0)
                    continue;
                if (body.WearExtra(pair[0], pair.Count > 1 ? pair[1] : pair[0], ql, _logger))
                    worn++;
            }

            if (Mercs.WeaponFor(_gameData.RootPath, kind) is { Count: > 0 } weapon)
            {
                int weaponLow = weapon[0];
                int weaponHigh = weapon.Count > 1 ? weapon[1] : weapon[0];
                if (body.WearExtra(weaponLow, weaponHigh, ql, _logger))
                    worn++;
                body.ReplaceCombatWeapon(weaponLow, weaponHigh, ql, _logger);
            }

            body.Rebase();
            int max = body.Stats.GetOrZero(CharacterStat.MaxHealth);
            if (max > 0)
                body.Stats.Set(CharacterStat.Health, max);
            int maxNano = body.Stats.GetOrZero(CharacterStat.MaxNanoEnergy);
            if (maxNano > 0)
                body.Stats.Set(CharacterStat.CurrentNano, maxNano);
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Battle station bot dressed hash={0} level={1} ql={2} pieces={3}",
                hash, level, ql, worn));
        }

        static readonly string[] GuardNames =
        [
            "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot",
            "Golf", "Hotel", "India", "Juliet", "Kilo", "Lima"
        ];

        void TickMatch(Playfield field, double deltaTime)
        {
            Match? match;
            lock (_gate)
            {
                if (!_matches.TryGetValue(field.Identity.Instance, out match) || !ReferenceEquals(match.Field, field))
                    match = null;
            }

            if (match == null)
            {
                // Players can land on a station with no live match (relog after a restart, GM .tp, walk-in).
                // Open one for them so the Decontamination countdown runs, bots fill and the pad opens.
                TryOpenOrphanMatch(field, deltaTime);
                return;
            }

            if (match.Ending)
                return;

            match.BotSpawnedThisTick = false;
            TickStartGate(match, deltaTime);
            ExpireHacks(match, deltaTime);
            if (match.FieldOpen)
                Score(match, deltaTime);
            TickHumans(match, deltaTime);
            if (match.FieldOpen)
            {
                TryBackfill(match, deltaTime);
                TickBots(match, deltaTime);
            }

            if (match.RedScore >= _catalog.ScoreCap || match.BlueScore >= _catalog.ScoreCap)
                Finish(match, match.RedScore >= _catalog.ScoreCap ? 1 : 2);
            else if (!AnyHumanPresent(match))
                Finish(match, 0);
        }

        void ExpireHacks(Match match, double deltaTime)
        {
            for (int i = 0; i < match.CaptureLockLeft.Length; i++)
            {
                if (match.CaptureLockLeft[i] > 0)
                    match.CaptureLockLeft[i] = Math.Max(0, match.CaptureLockLeft[i] - deltaTime);
            }

            lock (_gate)
            {
                for (int i = 0; i < match.Humans.Count; i++)
                {
                    Human human = match.Humans[i];
                    if (human.HackConsole < 0)
                        continue;

                    int console = human.HackConsole;
                    Player player = human.Player;
                    StationConsole point = _catalog.Consoles[console];
                    float dist = Distance(player.Position.xf, player.Position.zf, point.X, point.Z);
                    if (player.IsDead || player.Session == null || dist > 15f)
                    {
                        _channeling.Remove(player.Identity.Instance);
                        human.HackConsole = -1;
                        human.Confirm = false;
                        if (!player.IsDead && player.Session != null)
                            Say(player, "Capture broken — stay next to the point.");
                        continue;
                    }

                    human.HackLeft -= deltaTime;
                    if (human.HackLeft > 0)
                        continue;

                    LockConsole(match, console, human.Team, player.Name ?? "You");
                    human.HackConsole = -1;
                    human.Confirm = false;
                    _channeling.Remove(player.Identity.Instance);
                }
            }

            for (int i = 0; i < match.Bots.Count; i++)
            {
                Bot bot = match.Bots[i];
                if (bot.Dead || bot.HackConsole < 0)
                    continue;

                if (match.CaptureLockLeft[bot.HackConsole] > 0
                    || match.Owner[bot.HackConsole] == bot.Team)
                {
                    bot.HackConsole = -1;
                    if (bot.Npc.Brain != null)
                        bot.Npc.Brain.PatrolEnabled = true;
                    continue;
                }

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
            // PvP daily (John Smith): update when either side first reaches 425 while humans are present.
            if (!match.Daily425Fired && (match.RedScore >= 425 || match.BlueScore >= 425))
            {
                match.Daily425Fired = true;
                for (int h = 0; h < match.Humans.Count; h++)
                    ZoneEngine_New.Core.Events.DailyMissionService.Instance?.NotifyBattlestationThreshold(match.Humans[h].Player);
            }

            match.AnnounceDebt += interval;
            if (match.AnnounceDebt < 30)
                return;

            match.AnnounceDebt = 0;
            Announce(match, "Score  " + SideLabel(1) + " " + match.RedScore
                + "  " + SideLabel(2) + " " + match.BlueScore);
        }

        static readonly int[] CountdownMarks = [45, 30, 15, 10, 5, 3, 2, 1];

        readonly Dictionary<int, double> _orphanCheck = new();

        void TryOpenOrphanMatch(Playfield field, double deltaTime)
        {
            int pf = field.Identity.Instance;
            Bracket? bracket = null;
            for (int i = 0; i < _catalog.Brackets.Length; i++)
            {
                if (_catalog.Brackets[i].Playfield == pf)
                {
                    bracket = _catalog.Brackets[i];
                    break;
                }
            }

            if (bracket == null)
                return;

            lock (_gate)
            {
                double left = _orphanCheck.TryGetValue(pf, out double l) ? l - deltaTime : 0;
                if (left > 0)
                {
                    _orphanCheck[pf] = left;
                    return;
                }

                _orphanCheck[pf] = 5;
                if (_matches.ContainsKey(pf))
                    return;
            }

            var queue = new Queue(bracket) { Starting = true };
            foreach (Player player in field.GetRequiredService<DynelRegistry>().PlayerEntities())
            {
                if (player.Session != null)
                    queue.Players.Add(player);
            }

            if (queue.Players.Count == 0)
                return;

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Battle station {0} opening for {1} player(s) already on the field (no live match)",
                bracket.Name, queue.Players.Count));
            Open(queue, field);
        }

        void TickStartGate(Match match, double deltaTime)
        {
            if (match.FieldOpen)
                return;

            double before = match.StartGateLeft;
            match.StartGateLeft -= deltaTime;
            if (match.StartGateLeft > 0)
            {
                foreach (int mark in CountdownMarks)
                {
                    if (before > mark && match.StartGateLeft <= mark)
                    {
                        Announce(match, "Battle starts in " + mark.ToString(CultureInfo.InvariantCulture) + " seconds.");
                        break;
                    }
                }

                return;
            }

            match.FieldOpen = true;
            match.StartGateLeft = 0;
            Announce(match, "Decontamination pad is open. Step it to enter the station.");
            for (int i = 0; i < match.Humans.Count; i++)
            {
                Human human = match.Humans[i];
                if (human.Player.Session == null || !ReferenceEquals(human.Player.Playfield, match.Field))
                    continue;
                if (human.InField)
                    continue;
                // Guide: when time expires you are warped into the main station.
                SendToArm(match, human, PickArm());
            }

            if (match.PendingBotRed > 0)
                SpawnBots(match, 1, match.PendingBotRed);
            if (match.PendingBotBlue > 0)
                SpawnBots(match, 2, match.PendingBotBlue);
            match.PendingBotRed = 0;
            match.PendingBotBlue = 0;
        }

        void TickHumans(Match match, double deltaTime)
        {
            for (int i = match.Humans.Count - 1; i >= 0; i--)
            {
                Human human = match.Humans[i];
                Player player = human.Player;
                if (player.Session == null || player.Playfield == null || !ReferenceEquals(player.Playfield, match.Field))
                {
                    // The hub -> station transfer is a reconnect: for a moment the old Player is still on the hub
                    // or replaced by a new object on the field. Rebind by character id; drop only after a grace.
                    Player? current = FindOnField(match.Field, player.Identity.Instance);
                    if (current != null)
                    {
                        human.Player = current;
                        player = current;
                    }
                    else if (human.Grace > 0)
                    {
                        human.Grace -= deltaTime;
                        continue;
                    }
                    else
                    {
                        DropHuman(match, human, "You left the battle station. No payout.");
                        continue;
                    }
                }

                human.Grace = TransferGraceSeconds;

                float x = player.Position.xf;
                float z = player.Position.zf;
                bool inSafe = IsInSafe(x, z);
                if (inSafe)
                {
                    human.InField = false;
                    RestoreSafeVitals(player);
                    if (TryExitPad(match, human, x, z))
                        continue;
                }

                float dx = x - human.LastX;
                float dz = z - human.LastZ;
                if ((dx * dx) + (dz * dz) > 4f || human.HackConsole >= 0 || player.FightingTarget != Identity.None)
                {
                    human.LastX = x;
                    human.LastZ = z;
                    human.IdleSeconds = 0;
                }
                else if (!inSafe)
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

        bool TryExitPad(Match match, Human human, float x, float z)
        {
            // Pad only opens after the start timer (ao-universe / Randy: leave Decontamination once the timer ends).
            if (!match.FieldOpen || human.InField)
                return false;

            ExitPad[] pads = _catalog.ExitPads;
            for (int i = 0; i < pads.Length; i++)
            {
                ExitPad pad = pads[i];
                if (Distance(x, z, pad.X, pad.Z) > pad.Radius)
                    continue;

                Point? arm = string.IsNullOrWhiteSpace(pad.Arm) ? null : FindArm(pad.Arm);
                if (arm == null)
                    arm = PickArm();
                SendToArm(match, human, arm);
                return true;
            }

            return false;
        }

        void SendToArm(Match match, Human human, Point? arm)
        {
            Point at = arm ?? (_catalog.Arms.Length > 0 ? ArmPoint(_catalog.Arms[0]) : _catalog.Spawns.Red);
            human.InField = true;
            human.IdleSeconds = 0;
            human.LastX = at.X;
            human.LastZ = at.Z;
            Transfer(human.Player, match.Field, at);
            Say(human.Player, "Entering the station.");
        }

        Point? FindArm(string id)
        {
            for (int i = 0; i < _catalog.Arms.Length; i++)
            {
                if (string.Equals(_catalog.Arms[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return ArmPoint(_catalog.Arms[i]);
            }

            return null;
        }

        Point PickArm()
        {
            if (_catalog.Arms.Length == 0)
                return Random.Shared.Next(2) == 0 ? _catalog.Spawns.Red : _catalog.Spawns.Blue;
            return ArmPoint(_catalog.Arms[Random.Shared.Next(_catalog.Arms.Length)]);
        }

        static Point ArmPoint(ArmLanding arm) => new(arm.X, arm.Y, arm.Z);

        Point SafePoint()
        {
            if (_catalog.SafeSpawn != null)
                return _catalog.SafeSpawn;
            return _catalog.Spawns.Red;
        }

        bool IsInSafe(float x, float z)
        {
            Point safe = SafePoint();
            return Distance(x, z, safe.X, safe.Z) <= _catalog.SafeRadius;
        }

        public bool IsInDecontamination(Character character)
        {
            if (character?.Playfield == null)
                return false;

            lock (_gate)
            {
                if (!_matches.TryGetValue(character.Playfield.Identity.Instance, out Match? match)
                    || !ReferenceEquals(match.Field, character.Playfield)
                    || match.Ending)
                    return false;
            }

            return IsInSafe(character.Position.xf, character.Position.zf);
        }

        static void RestoreSafeVitals(Player player)
        {
            int maxHealth = player.Stats.GetOrZero(CharacterStat.MaxHealth);
            if (maxHealth > 0)
                player.Stats.Set(CharacterStat.Health, maxHealth, StatDetail.Base, dirty: true);
            int maxNano = player.Stats.GetOrZero(CharacterStat.MaxNanoEnergy);
            if (maxNano > 0)
                player.Stats.Set(CharacterStat.CurrentNano, maxNano, StatDetail.Base, dirty: true);
        }

        void TryBackfill(Match match, double deltaTime)
        {
            if (!match.UsesBots || match.Ending || match.BotSpawnedThisTick)
                return;

            if (match.BackfillLeft > 0)
            {
                match.BackfillLeft -= deltaTime;
                if (match.BackfillLeft > 0)
                    return;
            }

            for (int team = 1; team <= 2; team++)
            {
                int humans = 0;
                for (int i = 0; i < match.Humans.Count; i++)
                {
                    if (match.Humans[i].Team == team)
                        humans++;
                }

                int live = 0;
                for (int i = 0; i < match.Bots.Count; i++)
                {
                    if (match.Bots[i].Team == team && !match.Bots[i].Dead)
                        live++;
                }

                if (humans + live >= _catalog.TeamSize)
                    continue;

                SpawnBots(match, team, 1);
                match.BotSpawnedThisTick = true;
                return;
            }
        }

        void TickBots(Match match, double deltaTime)
        {
            bool respawned = false;
            for (int i = 0; i < match.Bots.Count; i++)
            {
                Bot bot = match.Bots[i];
                if (bot.Dead)
                {
                    bot.RespawnLeft -= deltaTime;
                    if (bot.RespawnLeft > 0 || respawned || match.BotSpawnedThisTick)
                        continue;

                    try
                    {
                        match.Field.GetRequiredService<SpawnService>().DespawnNpc(bot.Npc);
                    }
                    catch (Exception exception)
                    {
                        _logger.Warn("Battle station bot despawn failed: " + exception.Message);
                    }

                    int team = bot.Team;
                    match.Bots.RemoveAt(i);
                    i--;
                    if (!respawned && !match.BotSpawnedThisTick)
                    {
                        SpawnBots(match, team, 1);
                        match.BotSpawnedThisTick = true;
                        respawned = true;
                    }

                    continue;
                }

                if (bot.Npc.IsDead)
                {
                    bot.Dead = true;
                    bot.RespawnLeft = 10;
                    bot.HackConsole = -1;
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

                    bot.StuckSeconds = 0;
                    bot.ThinkLeft = Math.Max(0, bot.ThinkLeft - deltaTime);
                    continue;
                }

                bot.ThinkLeft -= deltaTime;
                if (bot.ThinkLeft > 0)
                {
                    if (bot.HackConsole >= 0)
                        bot.StuckSeconds = 0;
                    continue;
                }

                bot.ThinkLeft = 1.0 + (Random.Shared.NextDouble() * 1.0);

                switch (bot.Role)
                {
                    case BotRole.Defender:
                        TickDefender(match, bot, deltaTime);
                        break;
                    case BotRole.Hunter:
                        TickHunter(match, bot, deltaTime);
                        break;
                    case BotRole.Flanker:
                        TickFlanker(match, bot, deltaTime);
                        break;
                    default:
                        TickCapper(match, bot, deltaTime);
                        break;
                }
            }
        }

        void TickCapper(Match match, Bot bot, double deltaTime)
        {
            // Fight anything in ~20 m before capturing.
            if (TryEngage(match, bot, 20f))
                return;

            if (match.Owner[bot.Goal] == bot.Team || bot.HackConsole < 0)
                bot.Goal = NextGoal(match, bot.Team, bot);

            TryCaptureOrSteer(match, bot, deltaTime, requireClear: true);
        }

        void TickDefender(Match match, Bot bot, double deltaTime)
        {
            if (match.Owner[bot.Goal] != bot.Team)
                bot.Goal = NextOwnedGoal(match, bot.Team);

            // Guard ~25 m around the owned point.
            if (TryEngage(match, bot, 25f))
                return;

            StationConsole goal = _catalog.Consoles[bot.Goal];
            float dist = Distance(bot.Npc.Position.xf, bot.Npc.Position.zf, goal.X, goal.Z);
            if (dist > 12f)
            {
                UnstickAndSteer(match, bot, goal, dist, deltaTime);
                return;
            }

            bot.StuckSeconds = 0;
            bot.Npc.StopInPlace();
            if (bot.Npc.Brain != null)
            {
                bot.Npc.Brain.PatrolEnabled = true;
                bot.Npc.Brain.PatrolWaypoints = new[] { new Vector3(goal.X, goal.Y, goal.Z) };
            }
        }

        void TickHunter(Match match, Bot bot, double deltaTime)
        {
            PromotePackLeader(match, bot);
            Bot? leader = bot.PackLeader ? bot : FindPackLeader(match, bot);
            if (!bot.PackLeader && leader != null)
            {
                float packDist = Distance(
                    bot.Npc.Position.xf, bot.Npc.Position.zf,
                    leader.Npc.Position.xf, leader.Npc.Position.zf);
                if (packDist > 15f)
                {
                    SteerTo(bot, leader.Npc.Position.xf, leader.Npc.Position.yf, leader.Npc.Position.zf);
                    return;
                }
            }

            Character? prey = FindNearestEnemy(match, bot, preferPlayers: true, maxRange: 800f);
            if (prey != null)
            {
                float dist = Distance(bot.Npc.Position.xf, bot.Npc.Position.zf, prey.Position.xf, prey.Position.zf);
                if (dist <= 25f && TryEngage(match, bot, 25f))
                    return;
                SteerTo(bot, prey.Position.xf, prey.Position.yf, prey.Position.zf);
                TrackStuck(match, bot, dist, prey.Position.xf, prey.Position.yf, prey.Position.zf, deltaTime);
                return;
            }

            // No prey: roam toward an enemy-held console as a group rally point.
            if (match.Owner[bot.Goal] == bot.Team || bot.HackConsole < 0)
                bot.Goal = NextGoal(match, bot.Team, bot);
            StationConsole goal = _catalog.Consoles[bot.Goal];
            float goalDist = Distance(bot.Npc.Position.xf, bot.Npc.Position.zf, goal.X, goal.Z);
            UnstickAndSteer(match, bot, goal, goalDist, deltaTime);
        }

        void TickFlanker(Match match, Bot bot, double deltaTime)
        {
            if (TryEngage(match, bot, 20f))
                return;

            if (bot.Goal == 0 || match.Owner[bot.Goal] == bot.Team || bot.HackConsole < 0)
                bot.Goal = NextEnemyArm(match, bot.Team, bot);

            // Indirect hop: visit another arm tip first, then the target arm.
            if (bot.FlankVia < 0)
                bot.FlankVia = PickFlankVia(bot.Goal);

            if (bot.FlankVia >= 0)
            {
                StationConsole via = _catalog.Consoles[bot.FlankVia];
                float viaDist = Distance(bot.Npc.Position.xf, bot.Npc.Position.zf, via.X, via.Z);
                if (viaDist > 14f)
                {
                    UnstickAndSteer(match, bot, via, viaDist, deltaTime);
                    return;
                }

                bot.FlankVia = -1;
            }

            TryCaptureOrSteer(match, bot, deltaTime, requireClear: false);
        }

        void TryCaptureOrSteer(Match match, Bot bot, double deltaTime, bool requireClear)
        {
            StationConsole goal = _catalog.Consoles[bot.Goal];
            float x = bot.Npc.Position.xf;
            float z = bot.Npc.Position.zf;
            float goalDist = Distance(x, z, goal.X, goal.Z);
            if (goalDist < 6f && bot.HackConsole < 0 && match.Owner[bot.Goal] != bot.Team
                && match.CaptureLockLeft[bot.Goal] <= 0)
            {
                if (requireClear && EnemiesNear(match, bot, _catalog.BotEnemyClearRange))
                {
                    bot.StuckSeconds = 0;
                    TryEngage(match, bot, _catalog.BotEnemyClearRange);
                    return;
                }

                bot.HackConsole = bot.Goal;
                bot.HackLeft = _catalog.HackSeconds;
                if (bot.Npc.Brain != null)
                    bot.Npc.Brain.PatrolEnabled = false;
                bot.Npc.StopInPlace();
                return;
            }

            if (bot.HackConsole >= 0)
            {
                bot.StuckSeconds = 0;
                return;
            }

            UnstickAndSteer(match, bot, goal, goalDist, deltaTime);
        }

        void UnstickAndSteer(Match match, Bot bot, StationConsole goal, float goalDist, double deltaTime)
        {
            TrackStuck(match, bot, goalDist, goal.X, goal.Y, goal.Z, deltaTime);
            if (bot.Npc.FightingTarget != Identity.None)
                return;
            Steer(bot, goal);
        }

        void TrackStuck(Match match, Bot bot, float goalDist, float destX, float destY, float destZ, double deltaTime)
        {
            float x = bot.Npc.Position.xf;
            float z = bot.Npc.Position.zf;
            float moved = Distance(x, z, bot.LastX, bot.LastZ);
            bot.LastX = x;
            bot.LastZ = z;
            if (goalDist > 12f && moved < 1f)
            {
                bot.StuckSeconds += deltaTime;
                if (bot.StuckSeconds >= _catalog.BotStuckSeconds && !HumanCanSee(match, bot))
                {
                    bot.Npc.Motor.Warp(new Vector3(destX, destY, destZ));
                    bot.StuckSeconds = 0;
                }
            }
            else
            {
                bot.StuckSeconds = 0;
            }
        }

        bool TryEngage(Match match, Bot bot, float range)
        {
            Character? enemy = FindNearestEnemy(match, bot, preferPlayers: true, maxRange: range);
            if (enemy == null || !CombatRules.CanAttack(bot.Npc, enemy))
                return false;

            if (bot.HackConsole >= 0)
            {
                bot.HackConsole = -1;
                if (bot.Npc.Brain != null)
                    bot.Npc.Brain.PatrolEnabled = true;
            }

            bot.Npc.Brain?.AddThreat(enemy.Identity, NpcAiRules.ProximityHate);
            bot.Npc.StartFighting(enemy.Identity, action: 0);
            if (!match.LoggedEngage)
            {
                match.LoggedEngage = true;
                _logger.Info(string.Format(CultureInfo.InvariantCulture,
                    "Battle station engage {0} ({1}) -> {2}",
                    bot.Npc.Name ?? "bot", bot.Role, enemy.Name ?? "enemy"));
            }

            return bot.Npc.FightingTarget != Identity.None;
        }

        Character? FindNearestEnemy(Match match, Bot bot, bool preferPlayers, float maxRange)
        {
            float x = bot.Npc.Position.xf;
            float z = bot.Npc.Position.zf;
            Character? bestPlayer = null;
            float bestPlayerDist = maxRange;
            Character? bestBot = null;
            float bestBotDist = maxRange;

            for (int i = 0; i < match.Humans.Count; i++)
            {
                Human human = match.Humans[i];
                if (human.Team == bot.Team || human.Player.IsDead || !human.InField)
                    continue;
                float d = Distance(x, z, human.Player.Position.xf, human.Player.Position.zf);
                if (d <= bestPlayerDist)
                {
                    bestPlayerDist = d;
                    bestPlayer = human.Player;
                }
            }

            for (int i = 0; i < match.Bots.Count; i++)
            {
                Bot other = match.Bots[i];
                if (other.Dead || other.Team == bot.Team || ReferenceEquals(other, bot) || other.Npc.IsDead)
                    continue;
                float d = Distance(x, z, other.Npc.Position.xf, other.Npc.Position.zf);
                if (d <= bestBotDist)
                {
                    bestBotDist = d;
                    bestBot = other.Npc;
                }
            }

            if (preferPlayers && bestPlayer != null)
                return bestPlayer;
            if (bestPlayer != null && (bestBot == null || bestPlayerDist <= bestBotDist))
                return bestPlayer;
            return bestBot;
        }

        int NextGoal(Match match, int team, Bot self)
        {
            int best = -1;
            int bestCost = int.MaxValue;
            for (int i = 0; i < match.Owner.Length; i++)
            {
                if (match.Owner[i] == team || match.CaptureLockLeft[i] > 0)
                    continue;

                int cost = i == 0 ? 0 : 10;
                for (int b = 0; b < match.Bots.Count; b++)
                {
                    Bot other = match.Bots[b];
                    if (ReferenceEquals(other, self) || other.Dead || other.Team != team)
                        continue;
                    if (other.Goal == i || other.HackConsole == i)
                        cost += 25;
                }

                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = i;
                }
            }

            return best >= 0 ? best : 0;
        }

        int NextOwnedGoal(Match match, int team)
        {
            // Prefer an owned arm tip; else Core if owned; else rally at Core.
            int ownedArm = -1;
            bool ownCore = match.Owner.Length > 0 && match.Owner[0] == team;
            for (int i = 1; i < match.Owner.Length; i++)
            {
                if (match.Owner[i] != team)
                    continue;
                if (ownedArm < 0 || Random.Shared.Next(2) == 0)
                    ownedArm = i;
            }

            if (ownedArm >= 0)
                return ownedArm;
            return ownCore ? 0 : 0;
        }

        int NextEnemyArm(Match match, int team, Bot self)
        {
            int best = -1;
            int bestCost = int.MaxValue;
            for (int i = 1; i < match.Owner.Length; i++)
            {
                if (match.Owner[i] == team || match.CaptureLockLeft[i] > 0)
                    continue;

                int cost = 10;
                for (int b = 0; b < match.Bots.Count; b++)
                {
                    Bot other = match.Bots[b];
                    if (ReferenceEquals(other, self) || other.Dead || other.Team != team)
                        continue;
                    if (other.Role != BotRole.Flanker)
                        continue;
                    if (other.Goal == i || other.HackConsole == i)
                        cost += 25;
                }

                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = i;
                }
            }

            return best >= 0 ? best : NextGoal(match, team, self);
        }

        static int PickFlankVia(int goalArm)
        {
            // Pick a different arm tip as the indirect waypoint (consoles 1..3).
            int a = 1 + Random.Shared.Next(3);
            if (a == goalArm)
                a = 1 + ((a) % 3);
            if (a == goalArm)
                a = goalArm == 1 ? 2 : 1;
            return a;
        }

        void PromotePackLeader(Match match, Bot bot)
        {
            if (bot.Role != BotRole.Hunter || bot.PackId < 0 || bot.PackLeader)
                return;
            for (int i = 0; i < match.Bots.Count; i++)
            {
                Bot other = match.Bots[i];
                if (!other.Dead && other.Team == bot.Team && other.PackId == bot.PackId && other.PackLeader)
                    return;
            }

            bot.PackLeader = true;
        }

        Bot? FindPackLeader(Match match, Bot bot)
        {
            if (bot.PackId < 0)
                return null;
            for (int i = 0; i < match.Bots.Count; i++)
            {
                Bot other = match.Bots[i];
                if (!other.Dead && other.Team == bot.Team && other.PackId == bot.PackId && other.PackLeader)
                    return other;
            }

            return null;
        }

        bool EnemiesNear(Match match, Bot bot, float range)
        {
            return FindNearestEnemy(match, bot, preferPlayers: true, maxRange: range) != null;
        }

        bool HumanCanSee(Match match, Bot bot)
        {
            float x = bot.Npc.Position.xf;
            float z = bot.Npc.Position.zf;
            for (int i = 0; i < match.Humans.Count; i++)
            {
                Human human = match.Humans[i];
                if (human.Player.Session == null || !ReferenceEquals(human.Player.Playfield, match.Field))
                    continue;
                if (Distance(x, z, human.Player.Position.xf, human.Player.Position.zf) < 80f)
                    return true;
            }

            return false;
        }

        void Steer(Bot bot, StationConsole goal)
            => SteerTo(bot, goal.X, goal.Y, goal.Z);

        void SteerTo(Bot bot, float destX, float destY, float destZ)
        {
            float x = bot.Npc.Position.xf;
            float z = bot.Npc.Position.zf;
            float goalDist = Distance(x, z, destX, destZ);
            WarpEdge? hop = null;
            float hopPad = float.MaxValue;
            for (int i = 0; i < _catalog.Warps.Length; i++)
            {
                WarpEdge edge = _catalog.Warps[i];
                float toGoal = Distance(edge.ToX, edge.ToZ, destX, destZ);
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

            float wx = hop != null ? hop.X : destX;
            float wy = hop != null ? hop.Y : destY;
            float wz = hop != null ? hop.Z : destZ;
            if (bot.Npc.Brain != null)
            {
                bot.Npc.Brain.PatrolEnabled = true;
                bot.Npc.Brain.PatrolWaypoints = new[] { new Vector3(wx, wy, wz) };
            }
        }

        void LockConsole(Match match, int console, int team, string who)
        {
            if (console < 0 || console >= match.Owner.Length)
                return;

            match.Owner[console] = team;
            match.CaptureLockLeft[console] = _catalog.CaptureLockSeconds;
            Announce(match, who + " captured " + _catalog.Consoles[console].Id
                + " for " + SideLabel(team)
                + ". " + FormatBoard(match));
        }

        string FormatBoard(Match match)
        {
            var parts = new string[match.Owner.Length];
            for (int i = 0; i < match.Owner.Length; i++)
            {
                string hold = match.Owner[i] switch
                {
                    1 => SideLabel(1),
                    2 => SideLabel(2),
                    _ => "-"
                };
                parts[i] = _catalog.Consoles[i].Id + "=" + hold;
            }

            return "Board " + string.Join(" ", parts);
        }

        static string SideLabel(int team)
            => team == 1
                ? "<font color=#FF5555>Red</font>"
                : "<font color=#5599FF>Blue</font>";

        void Finish(Match match, int winner)
        {
            if (match.Ending)
                return;

            match.Ending = true;
            string text = winner == 0
                ? "The match ended with no one left."
                : SideLabel(winner) + " wins.  "
                    + SideLabel(1) + " " + match.RedScore + "  "
                    + SideLabel(2) + " " + match.BlueScore + ".";
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

            if (match.UsesBots)
                match.BackfillLeft = _catalog.BotBackfillSeconds;

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

        const double TransferGraceSeconds = 30;

        static Player? FindOnField(Playfield field, int instance)
        {
            foreach (Player candidate in field.GetRequiredService<DynelRegistry>().PlayerEntities())
            {
                if (candidate.Identity.Instance == instance && candidate.Session != null)
                    return candidate;
            }

            return null;
        }

        Human? FindHuman(Player player)
        {
            foreach (Match match in _matches.Values)
            {
                for (int i = 0; i < match.Humans.Count; i++)
                {
                    Human candidate = match.Humans[i];
                    if (ReferenceEquals(candidate.Player, player)
                        || candidate.Player.Identity.Instance == player.Identity.Instance)
                    {
                        if (player.Session != null)
                            candidate.Player = player;
                        return candidate;
                    }
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
                CaptureLockLeft = new double[consoles];
            }

            public Bracket Bracket { get; }

            public Playfield Field { get; }

            public int[] Owner { get; }

            /// <summary>Seconds remaining before this console can be captured again.</summary>
            public double[] CaptureLockLeft { get; }

            public int RedScore;

            public int BlueScore;

            public double ScoreDebt;

            public double AnnounceDebt;

            public bool Ending;

            public bool Daily425Fired;

            public bool UsesBots;

            public double BackfillLeft;

            public bool BotSpawnedThisTick;

            public double StartGateLeft;

            public bool FieldOpen;

            public int PendingBotRed;

            public int PendingBotBlue;

            public int NextPackId;

            public bool LoggedEngage;

            public List<Human> Humans { get; } = new();

            public List<Bot> Bots { get; } = new();
        }

        enum BotRole
        {
            Capper = 0,
            Defender = 1,
            Hunter = 2,
            Flanker = 3
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

            public Player Player { get; set; }

            public double Grace = 30;

            public int Team { get; }

            public Match Match { get; }

            public double IdleSeconds;

            public float LastX;

            public float LastZ;

            public int HackConsole = -1;

            public double HackLeft;

            public bool Confirm;

            /// <summary>False while still in the shared Decontamination Room; true after an arm landing.</summary>
            public bool InField;
        }

        sealed class Bot
        {
            public Bot(NpcCharacter npc, int team, int goal, BotRole role)
            {
                Npc = npc;
                Team = team;
                Goal = goal;
                Role = role;
                LastX = npc.Position.xf;
                LastZ = npc.Position.zf;
                ThinkLeft = Random.Shared.NextDouble() * 1.5;
            }

            public NpcCharacter Npc;

            public int Team { get; }

            public BotRole Role { get; }

            public int PackId = -1;

            public bool PackLeader;

            public int FlankVia = -1;

            public double ThinkLeft;

            public int Goal;

            public int HackConsole = -1;

            public double HackLeft;

            public double RespawnLeft = 10;

            public double StuckSeconds;

            public float LastX;

            public float LastZ;

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

        public double DominatedTickSeconds { get; set; } = 1;

        public double HackSeconds { get; set; } = 10;

        public double ConfirmSeconds { get; set; } = 5;

        /// <summary>After a capture completes, nobody can start another on that point for this many seconds.</summary>
        public double CaptureLockSeconds { get; set; } = 15;

        public double IdleSeconds { get; set; } = 180;

        public double QueueWaitSeconds { get; set; } = 45;

        public double StartGateSeconds { get; set; } = 60;

        public float SafeRadius { get; set; } = 28f;

        public int TeamSize { get; set; } = 11;

        public int BotFillMaxHumans { get; set; } = 11;

        public double BotBackfillSeconds { get; set; } = 15;

        public float BotEnemyClearRange { get; set; } = 20f;

        public double BotStuckSeconds { get; set; } = 2;

        public int LoyaltyStart { get; set; } = 10;

        public int LoyaltyAcceptCost { get; set; } = 1;

        public int LoyaltyComplete { get; set; } = 2;

        public int LoyaltyMax { get; set; } = 10;

        public int WinVpPerLevel { get; set; } = 4;

        public int LossVpPerLevel { get; set; } = 1;

        public string[] BotHashes { get; set; } = [];

        public StationConsole[] Consoles { get; set; } = [];

        public Point? SafeSpawn { get; set; }

        public ExitPad[] ExitPads { get; set; } = [];

        public ArmLanding[] Arms { get; set; } = [];

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

    sealed class ArmLanding
    {
        public string Id { get; set; } = "";

        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }
    }

    sealed class ExitPad
    {
        public string Arm { get; set; } = "";

        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }

        public float Radius { get; set; } = 3.5f;
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
