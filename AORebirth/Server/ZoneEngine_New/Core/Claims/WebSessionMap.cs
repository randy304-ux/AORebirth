namespace ZoneEngine_New.Core.Claims
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    using MySqlConnector;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Publishes which online character plays from which IP (ddogs_web_sessions). The account service reads it so the
    /// in-game Info window pages (Claim Center, Daily Reward, Battlesuits) know the character without a claim code:
    /// the in-game browser calls from the same address as the game client. Rows of characters that left are deleted.
    /// </summary>
    public sealed class WebSessionMap : IDisposable
    {
        const int PublishMilliseconds = 10000;

        readonly Lazy<PlayfieldManager> _playfields;
        readonly IZoneLogger _logger;
        readonly string _connection;
        Timer? _timer;
        int _running;
        bool _warned;

        public WebSessionMap(Lazy<PlayfieldManager> playfields, IZoneLogger logger)
        {
            _playfields = playfields ?? throw new ArgumentNullException(nameof(playfields));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connection = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public void Start()
        {
            _timer ??= new Timer(_ => Publish(), null, 2000, PublishMilliseconds);
        }

        void Publish()
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
                return;
            try
            {
                var rows = new List<(int CharId, string Ip)>();
                foreach (Player player in _playfields.Value.SnapshotPlayers())
                {
                    System.Net.IPAddress? address = player.Session?.RemoteAddress;
                    if (address == null)
                        continue;
                    if (address.IsIPv4MappedToIPv6)
                        address = address.MapToIPv4();
                    rows.Add((player.Identity.Instance, address.ToString()));
                }

                using var db = new MySqlConnection(_connection);
                db.Open();
                foreach ((int charId, string ip) in rows)
                {
                    using var up = new MySqlCommand(
                        "INSERT INTO ddogs_web_sessions (CharId,Ip,UpdatedAt) VALUES (@c,@i,NOW()) ON DUPLICATE KEY UPDATE Ip=VALUES(Ip), UpdatedAt=NOW()", db);
                    up.Parameters.AddWithValue("@c", charId);
                    up.Parameters.AddWithValue("@i", ip);
                    up.ExecuteNonQuery();
                }

                using var old = new MySqlCommand("DELETE FROM ddogs_web_sessions WHERE UpdatedAt < NOW() - INTERVAL 30 SECOND", db);
                old.ExecuteNonQuery();
                _warned = false;
            }
            catch (Exception ex)
            {
                if (!_warned)
                    _logger.Warn("Web session map publish failed: " + ex.Message);
                _warned = true;
            }
            finally
            {
                Volatile.Write(ref _running, 0);
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}