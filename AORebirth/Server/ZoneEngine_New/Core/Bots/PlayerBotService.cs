namespace ZoneEngine_New.Core.Bots
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    using AORebirth.Core.GameData;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Pets;
    using ZoneEngine_New.Core.Playfield;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>Playfield tick entry for starter-area playerbots.</summary>
    public static class PlayerBotClock
    {
        public static PlayerBotService? Service { get; set; }

        public static void Tick(Playfield playfield, double deltaTime) => Service?.Tick(playfield, deltaTime);
    }

    /// <summary>
    /// Spawns a small set of player-looking bots in starter hubs (Arete / ICC / Nascence).
    /// Behaviour is the playerbot action loop on <see cref="NpcBrain"/>; counts/levels/zones live in PlayerBots.json.
    /// </summary>
    public sealed class PlayerBotService
    {
        readonly IGameData _gameData;
        readonly IZoneLogger _logger;
        readonly PlayerBotCatalog _catalog;
        readonly object _gate = new();
        readonly Dictionary<int, ZoneRuntime> _zones = new();

        public PlayerBotService(IGameData gameData, IZoneLogger logger, PlayerBotCatalog catalog)
        {
            _gameData = gameData;
            _logger = logger;
            _catalog = catalog;
            PlayerBotClock.Service = this;
            LogNavmeshPresence();
        }

        public void Tick(Playfield playfield, double deltaTime)
        {
            if (!_catalog.Enabled || playfield == null || deltaTime <= 0)
                return;
            if (!_catalog.TryGetZone(playfield.Identity.Instance, out PlayerBotCatalog.ZoneConfig zone))
                return;

            ZoneRuntime runtime;
            lock (_gate)
            {
                if (!_zones.TryGetValue(zone.Id, out runtime!) || !ReferenceEquals(runtime.Field, playfield))
                {
                    runtime = new ZoneRuntime(playfield, zone);
                    _zones[zone.Id] = runtime;
                }
            }

            runtime.Elapsed += deltaTime;
            if (runtime.Elapsed < _catalog.TickSeconds)
                return;
            runtime.Elapsed = 0;

            bool hasPlayers = playfield.GetRequiredService<DynelRegistry>().PlayerEntities().Any();
            CullDead(runtime);
            FillToCount(runtime, hasPlayers);
            ThrottleActivity(runtime, hasPlayers);
        }

        void CullDead(ZoneRuntime runtime)
        {
            DateTime now = DateTime.UtcNow;
            for (int i = runtime.Bots.Count - 1; i >= 0; i--)
            {
                Bot bot = runtime.Bots[i];
                if (bot.Npc.IsDead || bot.Npc.Playfield == null)
                {
                    runtime.Bots.RemoveAt(i);
                    runtime.PendingRespawns.Add(now.AddSeconds(_catalog.RespawnSeconds));
                }
            }
        }

        void FillToCount(ZoneRuntime runtime, bool hasPlayers)
        {
            int want = runtime.Zone.Count;
            int live = runtime.Bots.Count;
            DateTime now = DateTime.UtcNow;
            while (runtime.PendingRespawns.Count > 0 && live < want)
            {
                if (runtime.PendingRespawns[0] > now)
                    break;
                runtime.PendingRespawns.RemoveAt(0);
                if (TrySpawnOne(runtime, hasPlayers))
                    live++;
            }

            while (live < want)
            {
                if (!TrySpawnOne(runtime, hasPlayers))
                    break;
                live++;
            }
        }

        void ThrottleActivity(ZoneRuntime runtime, bool hasPlayers)
        {
            for (int i = 0; i < runtime.Bots.Count; i++)
            {
                Bot bot = runtime.Bots[i];
                PlayerBotState? state = bot.Npc.Brain?.PlayerBot;
                if (state == null)
                    continue;

                if (!_catalog.IdleWhenNoPlayers || hasPlayers)
                {
                    state.Active = true;
                    continue;
                }

                // Empty starter zones: mostly idle, occasional roam/fight so the area is not frozen.
                if (!state.Active && Random.Shared.NextDouble() < _catalog.EmptyZoneActivityChance)
                    state.Active = true;
                else if (state.Active && Random.Shared.NextDouble() < 0.15)
                    state.Active = false;
            }
        }

        bool TrySpawnOne(ZoneRuntime runtime, bool hasPlayers)
        {
            if (_catalog.BotHashes.Count == 0 || runtime.Zone.Points.Count == 0)
                return false;

            string hash = _catalog.BotHashes[Random.Shared.Next(_catalog.BotHashes.Count)];
            int level = Random.Shared.Next(_catalog.MinLevel, _catalog.MaxLevel + 1);
            if (!_gameData.TryResolveMobTemplate(hash, level, out MobTemplate? template) || template == null)
            {
                _logger.Warn("Playerbot template missing: " + hash);
                return false;
            }

            // Fresh materialize — safe to mark untargetable and strip loot without touching the catalog.
            template.Features.Add(NpcFeature.PlayerUntargetable);
            template.ItemTable = new List<MobItemTableEntry>();

            Vector3 at = runtime.Zone.Points[Random.Shared.Next(runtime.Zone.Points.Count)];
            SpawnService spawn = runtime.Field.GetRequiredService<SpawnService>();
            NpcCharacter npc;
            try
            {
                npc = spawn.SpawnMob(
                    template,
                    at,
                    level: level,
                    spawnSource: SpawnSource.None,
                    spawnHash: "PBOT",
                    attachDefaultBrain: false,
                    configure: body => Dress(body, hash, level, runtime.Zone, hasPlayers));
            }
            catch (Exception exception)
            {
                _logger.Warn("Playerbot spawn failed: " + exception.Message);
                return false;
            }

            var bot = new Bot(npc);
            npc.Died += _ => { };
            runtime.Bots.Add(bot);
            _logger.Info(string.Format(
                CultureInfo.InvariantCulture,
                "Playerbot spawned pf={0} name={1} level={2} hash={3} at={4:0},{5:0}",
                runtime.Zone.Id,
                npc.Name,
                level,
                hash,
                at.xf,
                at.zf));
            return true;
        }

        void Dress(NpcCharacter body, string hash, int level, PlayerBotCatalog.ZoneConfig zone, bool hasPlayers)
        {
            body.Name = PickName();
            body.SuppressDeathCorpse = true;
            body.Stats.Set(CharacterStat.Side, (int)Side.Neutral);
            body.Stats.Set(CharacterStat.BreedHostility, 0);
            body.Stats.Set(CharacterStat.Level, level);
            body.Stats.Set(CharacterStat.VisualProfession, body.Stats.GetOrZero(CharacterStat.Profession));

            Mercs.Kind? kind = Mercs.ByHash(hash);
            if (kind != null)
            {
                body.Stats.Set(CharacterStat.Profession, kind.Profession);
                body.Stats.Set(CharacterStat.VisualProfession, kind.Profession);
                bool female = Random.Shared.Next(2) == 0;
                // Atrox has no female look — force male for that roll path via ApplyAppearance.
                MercNameFormat.ApplyAppearance(body, kind, female, randomAppearance: true);

                Mercs.Catalog catalog = Mercs.Load(_gameData.RootPath);
                body.WearsArmorStats = catalog.ArmorStats;
                int ql = Math.Clamp(level, 1, 50);
                foreach (List<int> pair in Mercs.ArmorFor(_gameData.RootPath, kind, level, ownerSide: 0))
                {
                    if (pair == null || pair.Count == 0)
                        continue;
                    body.WearExtra(pair[0], pair.Count > 1 ? pair[1] : pair[0], ql, _logger);
                }

                if (Mercs.WeaponFor(_gameData.RootPath, kind) is { Count: > 0 } weapon)
                {
                    int weaponLow = weapon[0];
                    int weaponHigh = weapon.Count > 1 ? weapon[1] : weapon[0];
                    body.WearExtra(weaponLow, weaponHigh, ql, _logger);
                    body.ReplaceCombatWeapon(weaponLow, weaponHigh, ql, _logger);
                }
            }

            body.Rebase();
            int max = body.Stats.GetOrZero(CharacterStat.MaxHealth);
            if (max > 0)
                body.Stats.Set(CharacterStat.Health, max);

            NpcBrain brain = NpcBrain.Create(body, home: null, NpcAiProfiles.PlayerBot);
            brain.ReleaseHome();
            PlayerBotState state = brain.PlayerBot
                ?? throw new InvalidOperationException("Playerbot brain missing PlayerBotState.");
            state.Waypoints = ShuffleCopy(zone.Points);
            state.WaypointIndex = Random.Shared.Next(state.Waypoints.Count);
            state.MinPreyLevel = Math.Max(1, level - 2);
            state.MaxPreyLevel = level + 2;
            state.EngageRange = _catalog.EngageRange;
            state.RestBelow = _catalog.RestBelowPercent;
            state.RestUntil = _catalog.RestUntilPercent;
            state.RoamPauseMinSeconds = _catalog.RoamPauseMinSeconds;
            state.RoamPauseMaxSeconds = _catalog.RoamPauseMaxSeconds;
            state.Active = hasPlayers || !_catalog.IdleWhenNoPlayers;
        }

        string PickName()
        {
            string first = _catalog.FirstNames[Random.Shared.Next(_catalog.FirstNames.Count)];
            string last = _catalog.LastNames[Random.Shared.Next(_catalog.LastNames.Count)];
            return first + " " + last;
        }

        static IReadOnlyList<Vector3> ShuffleCopy(IReadOnlyList<Vector3> points)
        {
            var copy = new List<Vector3>(points.Count);
            for (int i = 0; i < points.Count; i++)
                copy.Add(points[i]);
            for (int i = copy.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }

            return copy;
        }

        void LogNavmeshPresence()
        {
            string root = _gameData.RootPath;
            for (int i = 0; i < _catalog.Playfields.Count; i++)
            {
                PlayerBotCatalog.ZoneConfig zone = _catalog.Playfields[i];
                string relative = GameDataPaths.PlayfieldNavMeshRelativePath(zone.Id);
                string path = Path.Combine(root, relative);
                bool present = File.Exists(path);
                _logger.Info(string.Format(
                    CultureInfo.InvariantCulture,
                    "Playerbot zone pf={0} name={1} count={2} navmesh={3}",
                    zone.Id,
                    zone.Name,
                    zone.Count,
                    present ? "yes" : "no"));
            }
        }

        sealed class ZoneRuntime
        {
            public ZoneRuntime(Playfield field, PlayerBotCatalog.ZoneConfig zone)
            {
                Field = field;
                Zone = zone;
            }

            public Playfield Field { get; }

            public PlayerBotCatalog.ZoneConfig Zone { get; }

            public List<Bot> Bots { get; } = new();

            public List<DateTime> PendingRespawns { get; } = new();

            public double Elapsed { get; set; }
        }

        sealed class Bot
        {
            public Bot(NpcCharacter npc) => Npc = npc;

            public NpcCharacter Npc { get; }
        }
    }
}
