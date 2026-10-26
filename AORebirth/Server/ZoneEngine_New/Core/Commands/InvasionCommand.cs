namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Raids;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// <c>.invasion</c> / <c>/invasion</c> (GM): capital-city alien waves, a test wave at your feet, or the
    /// saved ship-room wave pads. Same spawner a later org-city raid will call.
    /// </summary>
    public sealed class InvasionCommand : IGmCommand
    {
        readonly InvasionService _invasions;
        readonly IGameData _gameData;
        readonly PlayfieldManager _playfields;

        public InvasionCommand(InvasionService invasions, IGameData gameData, PlayfieldManager playfields)
        {
            ArgumentNullException.ThrowIfNull(invasions);
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(playfields);
            _invasions = invasions;
            _gameData = gameData;
            _playfields = playfields;
        }

        public string Name => "invasion";

        public int RequiredGmLevel => 1;

        public string Usage => ".invasion start <city> [overhead] | .invasion here [overhead] | .invasion ship | .invasion stop [city|all] | .invasion status";

        public string Summary => "Starts or stops a timed alien wave invasion. Cities and pads are GameData/Invasions.json.";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Player player = context.Player;
            void Say(string text) => GmCommandFeedback.Send(context.Session, player, text);
            string sub = context.Args.Length > 0 ? context.Args[0] : "status";
            switch (sub.ToLowerInvariant())
            {
                case "status":
                    Say(Status());
                    return;
                case "cities":
                    Say(CityList());
                    return;
                case "stop":
                    Stop(context, Say);
                    return;
                case "here":
                    Here(player, Has(context.Args, "overhead"), Say);
                    return;
                case "ship":
                    Ship(player, Say);
                    return;
                case "start":
                    if (context.Args.Length < 2)
                    {
                        Say("Usage: .invasion start <city> [overhead]. Cities: " + CityKeys());
                        return;
                    }

                    StartCity(player, context.Args[1], Has(context.Args, "overhead"), Say);
                    return;
                default:
                    Say("Usage: " + Usage);
                    return;
            }
        }

        void StartCity(Player player, string key, bool overhead, Action<string> say)
        {
            InvasionCity? city = _invasions.Catalog.Find(key);
            if (city == null)
            {
                say("Unknown city '" + key + "'. Cities: " + CityKeys());
                return;
            }

            if (city.Playfield <= 0 || _gameData.GetPlayfieldMetaData(city.Playfield) == null)
            {
                say(city.Name + " playfield " + city.Playfield.ToString(CultureInfo.InvariantCulture) + " is not in GameData.");
                return;
            }

            List<Vector3> pads = PadsOf(city);
            if (pads.Count == 0)
            {
                say(city.Name + " has no drop pads in Invasions.json.");
                return;
            }

            Playfield playfield;
            try
            {
                playfield = _playfields.GetOrCreate(city.Playfield);
            }
            catch (Exception exception)
            {
                say("Could not open " + city.Name + ": " + exception.Message);
                return;
            }

            say(_invasions.TryStart(playfield, city.Name, pads, overhead, out string message)
                ? message + (overhead ? " Overhead mothership requested (experimental)." : "")
                    + (string.IsNullOrWhiteSpace(city.Note) ? "" : " " + city.Note)
                : message);
        }

        void Here(Player player, bool overhead, Action<string> say)
        {
            Playfield? playfield = player.Playfield;
            if (playfield == null)
            {
                say("Not on a playfield.");
                return;
            }

            Vector3 at = player.Position;
            var pads = new List<Vector3>
            {
                new Vector3(at.x + 8, at.y, at.z),
                new Vector3(at.x - 8, at.y, at.z),
                new Vector3(at.x, at.y, at.z + 8),
                new Vector3(at.x, at.y, at.z - 8)
            };
            say(_invasions.TryStart(playfield, "here", pads, overhead, out string message) ? message : message);
        }

        void Ship(Player player, Action<string> say)
        {
            if (player.Playfield is not QuestDungeonPlayfield dungeon)
            {
                say("Ship waves need you inside an alien ship dungeon. .invasion here works anywhere.");
                return;
            }

            var pads = new List<Vector3>();
            int copied = dungeon.CopyWavePads(pads);
            if (copied == 0)
            {
                say("This room has no BossArenas wave pads.");
                return;
            }

            say(_invasions.TryStart(dungeon, "ship room", pads, overhead: false, out string message) ? message : message);
        }

        void Stop(GmCommandContext context, Action<string> say)
        {
            string which = context.Args.Length > 1 ? context.Args[1] : "";
            if (string.Equals(which, "all", StringComparison.OrdinalIgnoreCase))
            {
                int count = _invasions.StopAll();
                say(count == 0 ? "No invasion was running." : "Stopped " + count.ToString(CultureInfo.InvariantCulture) + " invasion(s).");
                return;
            }

            int playfieldId;
            if (which.Length == 0)
            {
                if (context.Player.Playfield == null)
                {
                    say("Not on a playfield. .invasion stop <city>|all");
                    return;
                }

                playfieldId = context.Player.Playfield.Identity.Instance;
            }
            else if (_invasions.Catalog.Find(which) is { } city)
            {
                playfieldId = city.Playfield;
            }
            else
            {
                say("Unknown city '" + which + "'. .invasion stop <city>|all");
                return;
            }

            say(_invasions.TryStop(playfieldId, out string message) ? message : message);
        }

        string Status()
        {
            IReadOnlyList<string> running = _invasions.RunningTitles();
            if (running.Count == 0)
                return "No invasion running. " + Usage;
            return "Running: " + string.Join("; ", running);
        }

        string CityList()
        {
            var lines = new List<string> { "Invasion cities (pads are landmark offsets; walk them with /pos):" };
            foreach (InvasionCity city in _invasions.Catalog.Cities)
            {
                lines.Add(city.Key + " — " + city.Name + " playfield " + city.Playfield.ToString(CultureInfo.InvariantCulture)
                    + ", " + (city.Pads?.Count ?? 0).ToString(CultureInfo.InvariantCulture) + " pads"
                    + (string.IsNullOrWhiteSpace(city.Note) ? "" : ". " + city.Note));
            }

            return string.Join("\n", lines);
        }

        string CityKeys()
        {
            var keys = new List<string>();
            foreach (InvasionCity city in _invasions.Catalog.Cities)
                keys.Add(city.Key);
            return keys.Count == 0 ? "(none loaded)" : string.Join(", ", keys);
        }

        static List<Vector3> PadsOf(InvasionCity city)
        {
            var pads = new List<Vector3>();
            if (city.Pads == null)
                return pads;
            foreach (float[] pad in city.Pads)
            {
                if (pad == null || pad.Length < 3)
                    continue;
                pads.Add(new Vector3(pad[0], pad[1], pad[2]));
            }

            return pads;
        }

        static bool Has(string[] args, string word)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, word, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
