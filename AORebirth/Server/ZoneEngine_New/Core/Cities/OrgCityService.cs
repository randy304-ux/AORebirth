namespace ZoneEngine_New.Core.Cities
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.Json;

    using MySqlConnector;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Raids;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>Ticked from the playfield, same way the invasion clock is.</summary>
    public static class OrgCityClock
    {
        public static OrgCityService? Service { get; set; }

        public static void Tick(Playfield playfield) => Service?.Tick(playfield);
    }

    /// <summary>
    /// Org city plots and premade buildings. Cost and upkeep are 0. Buffs apply to org members
    /// standing in their city. State is a JSON file, not a schema change.
    /// </summary>
    public sealed class OrgCityService
    {
        public const int LowRaidLevelDefault = 30;

        static readonly JsonSerializerOptions Json = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        readonly IGameData _gameData;
        readonly Lazy<PlayfieldManager> _playfields;
        readonly InvasionService _invasions;
        readonly IZoneLogger _logger;
        readonly object _gate = new();
        readonly Dictionary<int, string> _bonusSig = new();
        OrgCityCatalog _catalog;
        DateTime _catalogStamp;
        Dictionary<int, OrgCityState> _byOrg = new();
        Dictionary<string, int> _plotOwner = new(StringComparer.OrdinalIgnoreCase);

        public OrgCityService(IGameData gameData, Lazy<PlayfieldManager> playfields, InvasionService invasions, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(playfields);
            ArgumentNullException.ThrowIfNull(invasions);
            ArgumentNullException.ThrowIfNull(logger);
            _gameData = gameData;
            _playfields = playfields;
            _invasions = invasions;
            _logger = logger;
            _catalog = LoadCatalog();
            LoadState();
            OrgCityClock.Service = this;
        }

        public static string StatePath =>
            Environment.GetEnvironmentVariable("AO_DDOGS_ORG_CITIES")
            ?? @"I:\AO\src\ddogs\data\org-city-state.json";

        public void Execute(Player player, string[] args)
        {
            ArgumentNullException.ThrowIfNull(player);
            args ??= Array.Empty<string>();
            ReloadCatalogIfStale();
            string verb = args.Length == 0 ? "status" : args[0].ToLowerInvariant();
            switch (verb)
            {
                case "help":
                    Say(player, " .city | .city claim <plot> | .city buy <building> | .city enter [plot] | .city release | .city raid [plot] | .city buildings");
                    Say(player, "Plots: " + PlotList() + ". Cost 0. Upkeep 0. The president buys buildings.");
                    return;
                case "buildings":
                    Buildings(player);
                    return;
                case "claim":
                    if (args.Length < 2)
                    {
                        Say(player, "Use .city claim <plot>. Plots: " + PlotList());
                        return;
                    }

                    Claim(player, args[1]);
                    return;
                case "release":
                    Release(player);
                    return;
                case "enter":
                    Enter(player, args.Length > 1 ? args[1] : null);
                    return;
                case "buy":
                    if (args.Length < 2)
                    {
                        Say(player, "Use .city buy <building>. .city buildings lists them.");
                        return;
                    }

                    Buy(player, args[1]);
                    return;
                case "raid":
                    Raid(player, args.Length > 1 ? args[1] : null);
                    return;
                default:
                    Status(player);
                    return;
            }
        }

        public void Tick(Playfield playfield)
        {
            if (playfield.IsDisposed)
                return;
            DynelRegistry? registry = playfield.GetService<DynelRegistry>();
            if (registry == null)
                return;
            ReloadCatalogIfStale();
            foreach (Player player in registry.PlayerEntities())
            {
                if (!ReferenceEquals(player.Playfield, playfield) || player.IsDead)
                    continue;
                (CharacterStat Stat, int Amount)[] bonuses = BonusesFor(player);
                string sig = Signature(bonuses);
                int id = player.Identity.Instance;
                if (_bonusSig.TryGetValue(id, out string? previous) && previous == sig)
                    continue;
                _bonusSig[id] = sig;
                player.CityContractBonuses = bonuses;
                player.MarkRebaseDirty();
            }
        }

        void Status(Player player)
        {
            int orgId = OrgOf(player);
            if (orgId <= 0)
            {
                Say(player, "You are not in an organization. Plots: " + PlotList() + ". Cost 0. Upkeep 0.");
                return;
            }

            if (!TryOrg(orgId, out OrgCityState? state) || state == null)
            {
                Say(player, "Your organization has no city. .city claim <plot>. Plots: " + PlotList() + ". Cost 0. Upkeep 0.");
                return;
            }

            OrgPlot? plot = FindPlot(state.Plot);
            Say(player, (plot?.Name ?? state.Plot) + ". Cost 0. Upkeep 0. Buildings: " + (state.Buildings.Count == 0 ? "none" : string.Join(", ", state.Buildings)));
            if (plot != null)
                Say(player, "Space " + Used(state).ToString(CultureInfo.InvariantCulture) + " / " + plot.Units.ToString(CultureInfo.InvariantCulture)
                    + " units. Largest single building " + plot.MaxFootprint.ToString(CultureInfo.InvariantCulture) + ".");
        }

        void Buildings(Player player)
        {
            if (_catalog.Buildings.Count == 0)
            {
                Say(player, "No buildings are listed in OrgCities.json.");
                return;
            }

            foreach (OrgBuilding building in _catalog.Buildings)
                Say(player, building.Key + " — " + building.Name + ", footprint " + building.Footprint.ToString(CultureInfo.InvariantCulture)
                    + ", QL " + building.Ql.ToString(CultureInfo.InvariantCulture) + ", price 0.");
        }

        void Claim(Player player, string key)
        {
            int orgId = OrgOf(player);
            if (orgId <= 0)
            {
                Say(player, "You are not in an organization.");
                return;
            }

            if (!IsLeader(player, orgId))
            {
                Say(player, "Only the president can claim a plot.");
                return;
            }

            OrgPlot? plot = FindPlot(key);
            if (plot == null)
            {
                Say(player, "Unknown plot '" + key + "'. Plots: " + PlotList());
                return;
            }

            lock (_gate)
            {
                if (_plotOwner.TryGetValue(plot.Key, out int owner) && owner != orgId)
                {
                    Say(player, plot.Name + " is already claimed.");
                    return;
                }

                if (_byOrg.TryGetValue(orgId, out OrgCityState? existing) && !existing.Plot.Equals(plot.Key, StringComparison.OrdinalIgnoreCase))
                {
                    Say(player, "Your organization already has " + existing.Plot + ". .city release first.");
                    return;
                }

                var state = existing ?? new OrgCityState { OrgId = orgId, Buildings = new List<string>() };
                state.Plot = plot.Key;
                _byOrg[orgId] = state;
                _plotOwner[plot.Key] = orgId;
                SaveState();
            }

            Say(player, plot.Name + " is yours. Cost 0. Upkeep 0. .city buy hq (or hq-small on a small plot), then .city enter.");
        }

        void Release(Player player)
        {
            int orgId = OrgOf(player);
            if (orgId <= 0 || !IsLeader(player, orgId))
            {
                Say(player, "Only the president can release the city.");
                return;
            }

            lock (_gate)
            {
                if (!_byOrg.Remove(orgId, out OrgCityState? state))
                {
                    Say(player, "Your organization has no city.");
                    return;
                }

                _plotOwner.Remove(state.Plot);
                SaveState();
            }

            Say(player, "The city plot is released. Buildings on it are gone.");
        }

        void Buy(Player player, string key)
        {
            int orgId = OrgOf(player);
            if (orgId <= 0)
            {
                Say(player, "You are not in an organization.");
                return;
            }

            if (!IsLeader(player, orgId))
            {
                Say(player, "Only the president can buy a building.");
                return;
            }

            OrgBuilding? building = FindBuilding(key);
            if (building == null)
            {
                Say(player, "Unknown building '" + key + "'. .city buildings");
                return;
            }

            lock (_gate)
            {
                if (!_byOrg.TryGetValue(orgId, out OrgCityState? state))
                {
                    Say(player, "Claim a plot first. .city claim <plot>");
                    return;
                }

                OrgPlot? plot = FindPlot(state.Plot);
                if (plot == null)
                {
                    Say(player, "The claimed plot is not in OrgCities.json anymore.");
                    return;
                }

                if (state.Buildings.Exists(b => b.Equals(building.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    Say(player, building.Name + " is already planted.");
                    return;
                }

                if (building.Hq && state.Buildings.Exists(b => FindBuilding(b)?.Hq == true))
                {
                    Say(player, "The city already has a headquarters.");
                    return;
                }

                if (!building.Hq && !state.Buildings.Exists(b => FindBuilding(b)?.Hq == true))
                {
                    Say(player, "Plant a headquarters first. .city buy hq or .city buy hq-small.");
                    return;
                }

                if (building.Footprint > plot.MaxFootprint)
                {
                    Say(player, building.Name + " does not fit " + plot.Name + ". Largest slot is " + plot.MaxFootprint.ToString(CultureInfo.InvariantCulture) + ".");
                    return;
                }

                int used = Used(state) + building.Footprint;
                if (used > plot.Units)
                {
                    Say(player, "Not enough plot space. " + used.ToString(CultureInfo.InvariantCulture) + " would exceed " + plot.Units.ToString(CultureInfo.InvariantCulture) + ".");
                    return;
                }

                state.Buildings.Add(building.Key);
                SaveState();
                Say(player, building.Name + " planted. Price 0. Upkeep 0. Space " + used.ToString(CultureInfo.InvariantCulture) + " / " + plot.Units.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        void Enter(Player player, string? key)
        {
            OrgPlot? plot = PlotForEnter(player, key, out string? error);
            if (plot == null)
            {
                Say(player, error ?? "No city to enter.");
                return;
            }

            if (plot.Landing == null || plot.Landing.Length < 3)
            {
                Say(player, plot.Name + " has no landing point.");
                return;
            }

            var landing = new Vector3(plot.Landing[0], plot.Landing[1], plot.Landing[2]);
            if (player.Playfield != null && player.Playfield.Identity.Instance == plot.Playfield)
            {
                player.TeleportWithinPlayfield(landing);
                Say(player, "You are at " + plot.Name + ".");
                return;
            }

            if (player.Session == null)
                return;
            if (!_playfields.Value.TryGet(plot.Playfield, out _))
                Say(player, "Initializing " + plot.Name + ". Please wait...");
            if (!_playfields.Value.WithPlayfield(plot.Playfield, player, destination =>
                {
                    player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
                    player.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
                    player.Session?.TransferToPlayfield(destination, landing);
                }))
                Say(player, "You are already changing playfield.");
        }

        void Raid(Player player, string? key)
        {
            bool gm = player.Stats.GetOrZero(CharacterStat.GmLevel) >= 1;
            OrgPlot? plot = null;
            OrgCityState? state = null;
            int orgId = OrgOf(player);
            if (!string.IsNullOrWhiteSpace(key))
            {
                plot = FindPlot(key);
                if (plot == null)
                {
                    Say(player, "Unknown plot '" + key + "'.");
                    return;
                }

                lock (_gate)
                    _plotOwner.TryGetValue(plot.Key, out orgId);
            }
            else if (orgId > 0)
            {
                lock (_gate)
                    _byOrg.TryGetValue(orgId, out state);
                plot = state == null ? null : FindPlot(state.Plot);
            }

            if (plot == null)
            {
                Say(player, "No city to raid. .city raid <plot>");
                return;
            }

            if (!gm)
            {
                if (OrgOf(player) <= 0 || !IsLeader(player, OrgOf(player)))
                {
                    Say(player, "Only the president can call the aliens.");
                    return;
                }

                lock (_gate)
                {
                    if (!_plotOwner.TryGetValue(plot.Key, out int owner) || owner != OrgOf(player))
                    {
                        Say(player, "That plot is not your organization's.");
                        return;
                    }
                }
            }

            if (plot.Landing == null || plot.Landing.Length < 3)
            {
                Say(player, plot.Name + " has no landing point.");
                return;
            }

            if (!_playfields.Value.TryGet(plot.Playfield, out Playfield? playfield) || playfield == null)
            {
                Say(player, plot.Name + " is not loaded. .city enter first.");
                return;
            }

            float x = plot.Landing[0];
            float y = plot.Landing[1];
            float z = plot.Landing[2];
            var pads = new List<Vector3>
            {
                new(x + 12f, y, z),
                new(x - 12f, y, z),
                new(x, y, z + 12f),
                new(x, y, z - 12f)
            };
            int level = _catalog.LowRaidLevel > 0 ? _catalog.LowRaidLevel : LowRaidLevelDefault;
            if (state == null && orgId > 0)
            {
                lock (_gate)
                    _byOrg.TryGetValue(orgId, out state);
            }

            bool uplink = state != null && state.Buildings.Exists(b => FindBuilding(b)?.Uplink == true);
            bool cloak = state != null && state.Buildings.Exists(b => FindBuilding(b)?.Cloak == true);
            if (uplink)
                Say(player, "Satellite Uplink: aliens are on the controller.");
            if (cloak)
                Say(player, "ECM cloak is down. This raid was called in.");
            Say(player, _invasions.TryStart(playfield, plot.Name + " controller", pads, overhead: false, out string message, level)
                ? message + " Alien level " + level.ToString(CultureInfo.InvariantCulture) + " (low-QL raid loot)."
                : message);
        }

        OrgPlot? PlotForEnter(Player player, string? key, out string? error)
        {
            error = null;
            bool gm = player.Stats.GetOrZero(CharacterStat.GmLevel) >= 1;
            if (!string.IsNullOrWhiteSpace(key))
            {
                OrgPlot? named = FindPlot(key);
                if (named == null)
                {
                    error = "Unknown plot '" + key + "'.";
                    return null;
                }

                if (gm)
                    return named;
                int orgId = OrgOf(player);
                lock (_gate)
                {
                    if (orgId > 0 && _plotOwner.TryGetValue(named.Key, out int owner) && owner == orgId)
                        return named;
                }

                error = named.Name + " is not your organization's city.";
                return null;
            }

            int mine = OrgOf(player);
            if (mine <= 0)
            {
                error = "You are not in an organization.";
                return null;
            }

            lock (_gate)
            {
                if (_byOrg.TryGetValue(mine, out OrgCityState? state))
                    return FindPlot(state.Plot);
            }

            error = "Your organization has no city.";
            return null;
        }

        (CharacterStat Stat, int Amount)[] BonusesFor(Player player)
        {
            int orgId = OrgOf(player);
            if (orgId <= 0)
                return Array.Empty<(CharacterStat, int)>();
            OrgCityState? state;
            lock (_gate)
            {
                if (!_byOrg.TryGetValue(orgId, out state) || state == null)
                    return Array.Empty<(CharacterStat, int)>();
            }

            OrgPlot? plot = FindPlot(state.Plot);
            if (plot == null || player.Playfield == null || player.Playfield.Identity.Instance != plot.Playfield)
                return Array.Empty<(CharacterStat, int)>();
            if (plot.Radius > 0 && plot.Landing != null && plot.Landing.Length >= 3)
            {
                float dx = player.Position.xf - plot.Landing[0];
                float dz = player.Position.zf - plot.Landing[2];
                if (dx * dx + dz * dz > plot.Radius * plot.Radius)
                    return Array.Empty<(CharacterStat, int)>();
            }

            int level = Math.Clamp(player.Stats.GetOrOne(CharacterStat.Level), 1, 220);
            var totals = new Dictionary<CharacterStat, int>();
            foreach (string key in state.Buildings)
            {
                OrgBuilding? building = FindBuilding(key);
                if (building?.Buffs == null)
                    continue;
                foreach (OrgBuff buff in building.Buffs)
                {
                    if (!Enum.TryParse(buff.Stat, ignoreCase: true, out CharacterStat stat) || buff.Amount == 0)
                        continue;
                    int scaled = Scale(buff.Amount, building.Ql, level);
                    if (scaled == 0)
                        continue;
                    totals[stat] = totals.TryGetValue(stat, out int have) ? have + scaled : scaled;
                }
            }

            if (totals.Count == 0)
                return Array.Empty<(CharacterStat, int)>();
            var result = new (CharacterStat Stat, int Amount)[totals.Count];
            int i = 0;
            foreach (KeyValuePair<CharacterStat, int> pair in totals)
                result[i++] = (pair.Key, pair.Value);
            return result;
        }

        static int Scale(int amount, int ql, int level)
        {
            if (amount == 0 || ql <= 0)
                return 0;
            double scaled = amount * (ql / 300d) * (level / 200d);
            return (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
        }

        static string Signature((CharacterStat Stat, int Amount)[] bonuses)
        {
            if (bonuses.Length == 0)
                return string.Empty;
            var text = new StringBuilder();
            for (int i = 0; i < bonuses.Length; i++)
            {
                if (i > 0)
                    text.Append(';');
                text.Append((int)bonuses[i].Stat);
                text.Append(':');
                text.Append(bonuses[i].Amount);
            }

            return text.ToString();
        }

        int Used(OrgCityState state)
        {
            int used = 0;
            foreach (string key in state.Buildings)
                used += FindBuilding(key)?.Footprint ?? 0;
            return used;
        }

        bool TryOrg(int orgId, out OrgCityState? state)
        {
            lock (_gate)
                return _byOrg.TryGetValue(orgId, out state);
        }

        static int OrgOf(Player player) => player.Stats.GetOrZero(CharacterStat.Clan);

        bool IsLeader(Player player, int orgId)
        {
            try
            {
                using var db = new MySqlConnection(MySqlConnectionSettings.GetRequiredConnectionString());
                db.Open();
                using var cmd = new MySqlCommand("SELECT LeaderId FROM organizations WHERE Id=@id", db);
                cmd.Parameters.AddWithValue("@id", orgId);
                object? value = cmd.ExecuteScalar();
                return value != null && value != DBNull.Value && Convert.ToInt32(value, CultureInfo.InvariantCulture) == player.Identity.Instance;
            }
            catch (Exception exception)
            {
                _logger.Warn("Org city leader lookup failed: " + exception.Message);
                return false;
            }
        }

        OrgPlot? FindPlot(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;
            foreach (OrgPlot plot in _catalog.Plots)
            {
                if (plot.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return plot;
            }

            return null;
        }

        OrgBuilding? FindBuilding(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;
            foreach (OrgBuilding building in _catalog.Buildings)
            {
                if (building.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return building;
            }

            return null;
        }

        string PlotList()
        {
            if (_catalog.Plots.Count == 0)
                return "none";
            var names = new string[_catalog.Plots.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = _catalog.Plots[i].Key;
            return string.Join(", ", names);
        }

        void ReloadCatalogIfStale()
        {
            string path = Path.Combine(_gameData.RootPath, "OrgCities.json");
            DateTime stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (stamp == _catalogStamp)
                return;
            _catalog = LoadCatalog();
        }

        OrgCityCatalog LoadCatalog()
        {
            string path = Path.Combine(_gameData.RootPath, "OrgCities.json");
            _catalogStamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            try
            {
                if (!File.Exists(path))
                    return new OrgCityCatalog();
                return JsonSerializer.Deserialize<OrgCityCatalog>(File.ReadAllText(path), Json) ?? new OrgCityCatalog();
            }
            catch (Exception exception)
            {
                _logger.Warn("OrgCities.json unreadable: " + exception.Message);
                return new OrgCityCatalog();
            }
        }

        void LoadState()
        {
            _byOrg = new Dictionary<int, OrgCityState>();
            _plotOwner = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(StatePath))
                    return;
                OrgCityStateFile? file = JsonSerializer.Deserialize<OrgCityStateFile>(File.ReadAllText(StatePath), Json);
                if (file?.Orgs == null)
                    return;
                foreach (OrgCityState row in file.Orgs)
                {
                    if (row.OrgId <= 0 || string.IsNullOrWhiteSpace(row.Plot))
                        continue;
                    row.Buildings ??= new List<string>();
                    _byOrg[row.OrgId] = row;
                    _plotOwner[row.Plot] = row.OrgId;
                }
            }
            catch (Exception exception)
            {
                _logger.Warn("Org city state unreadable: " + exception.Message);
            }
        }

        void SaveState()
        {
            var file = new OrgCityStateFile { Orgs = new List<OrgCityState>(_byOrg.Values) };
            string path = StatePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(file, Json));
            File.Move(temp, path, overwrite: true);
        }

        static void Say(Player player, string text)
        {
            if (player.Session == null)
                return;
            GmCommandFeedback.Send(player.Session, player, text);
        }

        sealed class OrgCityStateFile
        {
            public List<OrgCityState> Orgs { get; set; } = new();
        }
    }

    public sealed class OrgCityCatalog
    {
        public int LowRaidLevel { get; set; } = OrgCityService.LowRaidLevelDefault;

        public List<OrgPlot> Plots { get; set; } = new();

        public List<OrgBuilding> Buildings { get; set; } = new();
    }

    public sealed class OrgPlot
    {
        public string Key { get; set; } = "";

        public string Name { get; set; } = "";

        public string Kind { get; set; } = "";

        public int Playfield { get; set; }

        public int Units { get; set; }

        public int MaxFootprint { get; set; }

        public float Radius { get; set; }

        public float[] Landing { get; set; } = Array.Empty<float>();
    }

    public sealed class OrgBuilding
    {
        public string Key { get; set; } = "";

        public string Name { get; set; } = "";

        public int Footprint { get; set; }

        public int Ql { get; set; } = 300;

        public bool Hq { get; set; }

        public bool Uplink { get; set; }

        public bool Cloak { get; set; }

        public List<OrgBuff> Buffs { get; set; } = new();
    }

    public sealed class OrgBuff
    {
        public string Stat { get; set; } = "";

        public int Amount { get; set; }
    }

    public sealed class OrgCityState
    {
        public int OrgId { get; set; }

        public string Plot { get; set; } = "";

        public List<string> Buildings { get; set; } = new();
    }
}
