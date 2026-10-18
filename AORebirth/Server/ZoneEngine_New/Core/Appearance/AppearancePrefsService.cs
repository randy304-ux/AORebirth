namespace ZoneEngine_New.Core.Appearance
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    using MySqlConnector;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Per-character look toggles (ddogs_appearance_prefs, written by the Store window's Battlesuits page on the
    /// account service). The table is tiny, so it is re-read every few seconds; a changed row rebuilds that
    /// player's stats and look on their playfield tick so the change shows without zoning.
    /// </summary>
    public sealed class AppearancePrefsService : IDisposable
    {
        const int PollMilliseconds = 3000;

        readonly ConcurrentDictionary<(int CharId, string Key), int> _values = new();
        readonly Lazy<PlayfieldManager> _playfields;
        readonly IZoneLogger _logger;
        readonly string _connection;
        Timer? _timer;
        int _polling;
        bool _warned;

        public AppearancePrefsService(Lazy<PlayfieldManager> playfields, IZoneLogger logger)
        {
            _playfields = playfields ?? throw new ArgumentNullException(nameof(playfields));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connection = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public void Start()
        {
            Poll(announce: false);
            _logger.Info("Appearance prefs loaded rows=" + _values.Count.ToString(CultureInfo.InvariantCulture));
            _timer ??= new Timer(_ => Poll(announce: true), null, PollMilliseconds, PollMilliseconds);
        }

        public int Get(int characterId, string key, int fallback)
            => _values.TryGetValue((characterId, key), out int value) ? value : fallback;

        /// <summary>Saves a toggle (the .battlesuit command). The caller rebuilds the player's look.</summary>
        public void Set(int characterId, string key, int value)
        {
            using (var db = new MySqlConnection(_connection))
            {
                db.Open();
                using var cmd = new MySqlCommand(
                    "INSERT INTO ddogs_appearance_prefs (CharId,PrefKey,Value) VALUES (@c,@k,@v) ON DUPLICATE KEY UPDATE Value=VALUES(Value)", db);
                cmd.Parameters.AddWithValue("@c", characterId);
                cmd.Parameters.AddWithValue("@k", key);
                cmd.Parameters.AddWithValue("@v", value);
                cmd.ExecuteNonQuery();
            }

            _values[(characterId, key)] = value;
        }

        void Poll(bool announce)
        {
            if (Interlocked.Exchange(ref _polling, 1) == 1)
                return;
            try
            {
                var seen = new Dictionary<(int, string), int>();
                using (var db = new MySqlConnection(_connection))
                {
                    db.Open();
                    using var cmd = new MySqlCommand("SELECT CharId, PrefKey, Value FROM ddogs_appearance_prefs", db);
                    using MySqlDataReader r = cmd.ExecuteReader();
                    while (r.Read())
                        seen[(r.GetInt32(0), r.GetString(1))] = r.GetInt32(2);
                }

                var changed = new HashSet<int>();
                foreach (KeyValuePair<(int, string), int> row in seen)
                {
                    if (!_values.TryGetValue(row.Key, out int old) || old != row.Value)
                    {
                        _values[row.Key] = row.Value;
                        changed.Add(row.Key.Item1);
                    }
                }

                foreach ((int CharId, string Key) key in _values.Keys)
                {
                    if (!seen.ContainsKey(key) && _values.TryRemove(key, out _))
                        changed.Add(key.CharId);
                }

                _warned = false;
                if (!announce)
                    return;
                foreach (int characterId in changed)
                {
                    _logger.Info("Appearance prefs changed char=" + characterId.ToString(CultureInfo.InvariantCulture));
                    if (_playfields.Value.FindPlayer(characterId, out Player player) && player.Playfield is { } playfield)
                        playfield.DispatchPlayerProjection(player, player.RebaseStats);
                }
            }
            catch (Exception ex)
            {
                if (!_warned)
                    _logger.Warn("Appearance prefs poll failed: " + ex.Message);
                _warned = true;
            }
            finally
            {
                Volatile.Write(ref _polling, 0);
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}