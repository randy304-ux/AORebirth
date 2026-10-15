namespace ZoneEngine_New.Core.Bots
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    using AORebirth.Communication.Messages;

    using Dapper;

    using MySqlConnector;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Chat;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Server org bot. Forming an org with .org create makes the bot available. There is no player
    /// character. MOTD is organizations.Objective and news is organizations.History. Membership is
    /// stats StatId 5. Raid and loot notes are memory only and reset when the zone restarts.
    /// Last seen is online or offline. This database has no last-login clock.
    /// </summary>
    public sealed class OrgBotService
    {
        const int OwnerType = (int)IdentityType.CanbeAffected;
        const int PresidentRank = 0;
        const int MemberRank = 1;

        readonly Lazy<IChatEngineLink> _chat;
        readonly Lazy<PlayfieldManager> _playfields;
        readonly Dictionary<int, RaidNote> _raids = new();

        public OrgBotService(Lazy<IChatEngineLink> chat, Lazy<PlayfieldManager> playfields)
        {
            ArgumentNullException.ThrowIfNull(chat);
            ArgumentNullException.ThrowIfNull(playfields);
            _chat = chat;
            _playfields = playfields;
        }

        public void Execute(Player player, string[] args)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (player.Session == null)
                return;
            args ??= Array.Empty<string>();
            string verb = args.Length == 0 ? "help" : args[0];
            if (verb.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                Help(player);
                return;
            }

            if (verb.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                Create(player, args.Length > 1 ? string.Join(' ', args[1..]) : string.Empty);
                return;
            }

            int orgId = player.Stats.GetOrZero(CharacterStat.Clan);
            if (orgId <= 0)
            {
                Say(player, "You are not in an organization. .org create Name forms one.");
                return;
            }

            OrgRecord? org = LoadOrg(orgId);
            if (org == null)
            {
                Say(player, "That organization record is missing.");
                return;
            }

            bool leader = org.LeaderId == player.Identity.Instance;
            if (verb.Equals("online", StringComparison.OrdinalIgnoreCase))
                ShowMembers(player, org, onlineOnly: true);
            else if (verb.Equals("members", StringComparison.OrdinalIgnoreCase))
                ShowMembers(player, org, onlineOnly: false);
            else if (verb.Equals("lastseen", StringComparison.OrdinalIgnoreCase))
                ShowLastSeen(player, org);
            else if (verb.Equals("motd", StringComparison.OrdinalIgnoreCase))
                Motd(player, org, leader, args);
            else if (verb.Equals("news", StringComparison.OrdinalIgnoreCase))
                News(player, org, leader, args);
            else if (verb.Equals("alts", StringComparison.OrdinalIgnoreCase))
                Alts(player, org);
            else if (verb.Equals("add", StringComparison.OrdinalIgnoreCase))
                Add(player, org, leader, args.Length > 1 ? args[1] : string.Empty);
            else if (verb.Equals("raid", StringComparison.OrdinalIgnoreCase))
                Raid(player, org, leader, args);
            else if (verb.Equals("loot", StringComparison.OrdinalIgnoreCase))
                Loot(player, org, leader, args);
            else
                Help(player);
        }

        public void HandleTell(Player player, string text)
        {
            string trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0 || trimmed.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                Help(player);
                return;
            }

            Execute(player, trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        void Create(Player player, string name)
        {
            name = (name ?? string.Empty).Trim();
            if (player.Stats.GetOrZero(CharacterStat.Clan) > 0)
            {
                Say(player, "You are already in an organization.");
                return;
            }

            if (!ValidName(name))
            {
                Say(player, "Use .org create Name. 3 to 24 letters, numbers, spaces or hyphens.");
                return;
            }

            if (OrgNameTaken(name))
            {
                Say(player, "An organization already uses that name.");
                return;
            }

            int orgId = InsertOrg(name, player.Identity.Instance);
            if (orgId <= 0)
            {
                Say(player, "The organization was not created.");
                return;
            }

            WriteClan(player.Identity.Instance, orgId, PresidentRank);
            ApplyLive(player, orgId, PresidentRank);
            OpenChannel(player, orgId);
            Say(player, "Organization " + name + " is formed. Orgbot answers in org chat and by tell.");
            Post(orgId, name + " is formed. Tell Orgbot, or type .org help in org chat.");
        }

        void Add(Player player, OrgRecord org, bool leader, string name)
        {
            if (!leader)
            {
                Say(player, "Only the president can add a member.");
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                Say(player, "Use .org add CharacterName.");
                return;
            }

            MemberRecord? target = LoadCharacter(name.Trim());
            if (target == null || target.Id <= 0)
            {
                Say(player, "No character named " + name.Trim() + ".");
                return;
            }

            int current = ClanOf(target.Id);
            if (_playfields.Value.FindPlayer(target.Id, out Player online))
                current = online.Stats.GetOrZero(CharacterStat.Clan);
            if (current == org.Id)
            {
                Say(player, target.Name + " is already in " + org.Name + ".");
                return;
            }

            if (current > 0)
            {
                Say(player, target.Name + " is already in an organization.");
                return;
            }

            WriteClan(target.Id, org.Id, MemberRank);
            if (online != null)
                ApplyLive(online, org.Id, MemberRank);
            OpenChannel(online ?? player, org.Id);
            Say(player, target.Name + " is now in " + org.Name + ".");
            Post(org.Id, target.Name + " joined " + org.Name + ".");
        }

        void ShowMembers(Player player, OrgRecord org, bool onlineOnly)
        {
            List<MemberRecord> rows = Members(org.Id);
            var body = new StringBuilder();
            body.Append(DiagAoml.Title(Esc(onlineOnly ? org.Name + " online" : org.Name + " members")));
            int shown = 0;
            foreach (MemberRecord row in rows)
            {
                bool online = row.Online != 0;
                if (onlineOnly && !online)
                    continue;
                shown++;
                body.Append("<br>").Append(DiagAoml.Label(Esc(row.Name ?? ("#" + row.Id.ToString(CultureInfo.InvariantCulture)))));
                body.Append(' ').Append(DiagAoml.Muted(online ? "online" : "offline"));
            }

            if (shown == 0)
                body.Append("<br>").Append(DiagAoml.Muted(onlineOnly ? "Nobody in the org is online." : "No members are stored."));
            Window(player, body.ToString(), onlineOnly ? "Org online" : "Org members");
            Post(org.Id, (onlineOnly ? "Online" : "Members") + ": " + shown.ToString(CultureInfo.InvariantCulture) + ".");
        }

        void ShowLastSeen(Player player, OrgRecord org)
        {
            var body = new StringBuilder();
            body.Append(DiagAoml.Title(Esc(org.Name + " last seen")));
            body.Append("<br>").Append(DiagAoml.Muted("Online or offline. This server does not store a clock time."));
            foreach (MemberRecord row in Members(org.Id))
            {
                body.Append("<br>").Append(DiagAoml.Label(Esc(row.Name ?? "?")));
                body.Append(' ').Append(DiagAoml.Muted(row.Online != 0 ? "online" : "offline"));
            }

            Window(player, body.ToString(), "Org last seen");
        }

        void Motd(Player player, OrgRecord org, bool leader, string[] args)
        {
            if (args.Length > 1)
            {
                if (!leader)
                {
                    Say(player, "Only the president can set the message of the day.");
                    return;
                }

                org.Objective = Clip(string.Join(' ', args[1..]), 200);
                SaveText(org.Id, objective: org.Objective, history: null);
                Post(org.Id, "MOTD: " + org.Objective);
            }

            var body = new StringBuilder();
            body.Append(DiagAoml.Title(Esc(org.Name + " MOTD")));
            body.Append("<br>").Append(string.IsNullOrWhiteSpace(org.Objective)
                ? DiagAoml.Muted("No message of the day.")
                : DiagAoml.Name(Esc(org.Objective)));
            Window(player, body.ToString(), "Org MOTD");
        }

        void News(Player player, OrgRecord org, bool leader, string[] args)
        {
            if (args.Length > 1)
            {
                if (!leader)
                {
                    Say(player, "Only the president can add news.");
                    return;
                }

                string line = Clip(string.Join(' ', args[1..]), 120);
                string prior = org.History ?? string.Empty;
                org.History = Clip(prior.Length == 0 ? line : prior + " | " + line, 240);
                SaveText(org.Id, objective: null, history: org.History);
                Post(org.Id, "News: " + line);
            }

            var body = new StringBuilder();
            body.Append(DiagAoml.Title(Esc(org.Name + " news")));
            body.Append("<br>").Append(string.IsNullOrWhiteSpace(org.History)
                ? DiagAoml.Muted("No news.")
                : DiagAoml.Name(Esc(org.History)));
            Window(player, body.ToString(), "Org news");
        }

        void Alts(Player player, OrgRecord org)
        {
            var body = new StringBuilder();
            body.Append(DiagAoml.Title(Esc(org.Name + " alts")));
            body.Append("<br>").Append(DiagAoml.Muted("Other characters on the same account."));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MemberRecord row in Members(org.Id))
            {
                if (string.IsNullOrWhiteSpace(row.Username) || !seen.Add(row.Username))
                    continue;
                body.Append("<br>").Append(DiagAoml.Label(Esc(row.Name ?? row.Username))).Append(": ");
                bool any = false;
                foreach (MemberRecord alt in AltsOf(row.Username))
                {
                    if (alt.Id == row.Id)
                        continue;
                    if (any)
                        body.Append(", ");
                    any = true;
                    body.Append(DiagAoml.Name(Esc(alt.Name ?? "?")));
                }

                if (!any)
                    body.Append(DiagAoml.Muted("none"));
            }

            Window(player, body.ToString(), "Org alts");
        }

        void Raid(Player player, OrgRecord org, bool leader, string[] args)
        {
            RaidNote note = Note(org.Id);
            string verb = args.Length > 1 ? args[1] : "show";
            if (verb.Equals("join", StringComparison.OrdinalIgnoreCase))
            {
                string name = player.Name ?? ("#" + player.Identity.Instance.ToString(CultureInfo.InvariantCulture));
                if (!note.Raid.Contains(name))
                    note.Raid.Add(name);
                Post(org.Id, name + " joined the raid list.");
            }
            else if (verb.Equals("leave", StringComparison.OrdinalIgnoreCase))
            {
                note.Raid.Remove(player.Name ?? string.Empty);
            }
            else if (verb.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                if (!leader)
                {
                    Say(player, "Only the president can clear the raid list.");
                    return;
                }

                note.Raid.Clear();
                Post(org.Id, "Raid list cleared.");
            }

            var body = new StringBuilder();
            body.Append(DiagAoml.Title(Esc(org.Name + " raid")));
            body.Append("<br>").Append(DiagAoml.Muted("In memory only. A zone restart clears it. .org raid join, leave, or clear."));
            if (note.Raid.Count == 0)
                body.Append("<br>").Append(DiagAoml.Muted("Nobody is on the raid list."));
            foreach (string name in note.Raid)
                body.Append("<br>").Append(DiagAoml.Label(Esc(name)));
            Window(player, body.ToString(), "Org raid");
        }

        void Loot(Player player, OrgRecord org, bool leader, string[] args)
        {
            RaidNote note = Note(org.Id);
            if (args.Length > 1)
            {
                if (!leader)
                {
                    Say(player, "Only the president can set the loot note.");
                    return;
                }

                note.Loot = Clip(string.Join(' ', args[1..]), 200);
                Post(org.Id, "Loot: " + note.Loot);
            }

            var body = new StringBuilder();
            body.Append(DiagAoml.Title(Esc(org.Name + " loot")));
            body.Append("<br>").Append(DiagAoml.Muted("In memory only. A zone restart clears it."));
            body.Append("<br>").Append(string.IsNullOrWhiteSpace(note.Loot)
                ? DiagAoml.Muted("No loot note.")
                : DiagAoml.Name(Esc(note.Loot)));
            Window(player, body.ToString(), "Org loot");
        }

        void Help(Player player)
        {
            var body = new StringBuilder();
            body.Append(DiagAoml.Title("Orgbot"));
            body.Append("<br>").Append(DiagAoml.Muted(
                "Forming an org creates this bot. No player character. Commands work in org chat or by tell."));
            Line(body, ".org create Name", "Forms the org and joins you as president.");
            Line(body, ".org online", "Members who are online.");
            Line(body, ".org members", "Every member.");
            Line(body, ".org lastseen", "Online or offline. No clock time is stored.");
            Line(body, ".org motd text", "President sets the message of the day.");
            Line(body, ".org news text", "President adds a news line.");
            Line(body, ".org alts", "Other characters on members accounts.");
            Line(body, ".org add Name", "President adds a character.");
            Line(body, ".org raid join", "Raid list. leave and clear. Resets on restart.");
            Line(body, ".org loot text", "Loot note. Resets on restart.");
            Window(player, body.ToString(), "Orgbot");
        }

        static void Line(StringBuilder body, string syntax, string summary)
        {
            body.Append("<br>").Append(DiagAoml.Label(syntax)).Append(' ').Append(DiagAoml.Muted(summary));
        }

        RaidNote Note(int orgId)
        {
            if (!_raids.TryGetValue(orgId, out RaidNote? note))
            {
                note = new RaidNote();
                _raids.Add(orgId, note);
            }

            return note;
        }

        static List<MemberRecord> Members(int orgId)
        {
            using MySqlConnection connection = Open();
            return connection.Query<MemberRecord>(
                "SELECT c.Id, c.Name, c.Username, c.Online FROM stats s "
                + "INNER JOIN characters c ON c.Id = s.Instance "
                + "WHERE s.StatId = 5 AND s.StatValue = @orgId AND s.Type = @type ORDER BY c.Name",
                new { orgId, type = OwnerType }).ToList();
        }

        static List<MemberRecord> AltsOf(string username)
        {
            using MySqlConnection connection = Open();
            return connection.Query<MemberRecord>(
                "SELECT Id, Name, Username FROM characters WHERE Username = @username ORDER BY Name",
                new { username }).ToList();
        }

        static OrgRecord? LoadOrg(int orgId)
        {
            using MySqlConnection connection = Open();
            return connection.QuerySingleOrDefault<OrgRecord>(
                "SELECT Id, Name, LeaderId, Objective, History FROM organizations WHERE Id = @orgId",
                new { orgId });
        }

        static bool OrgNameTaken(string name)
        {
            using MySqlConnection connection = Open();
            return connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM organizations WHERE Name = @name",
                new { name }) > 0;
        }

        static int InsertOrg(string name, int leaderId)
        {
            using MySqlConnection connection = Open();
            connection.Execute(
                "INSERT INTO organizations (Creation, Name, LeaderId, GovernmentForm, Description, Objective, History, Comission) "
                + "VALUES (UTC_TIMESTAMP(), @name, @leaderId, 0, '', '', '', 1)",
                new { name, leaderId });
            return connection.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");
        }

        static void SaveText(int orgId, string? objective, string? history)
        {
            using MySqlConnection connection = Open();
            if (objective != null)
            {
                connection.Execute(
                    "UPDATE organizations SET Objective = @objective WHERE Id = @orgId",
                    new { orgId, objective });
            }

            if (history != null)
            {
                connection.Execute(
                    "UPDATE organizations SET History = @history WHERE Id = @orgId",
                    new { orgId, history });
            }
        }

        static MemberRecord? LoadCharacter(string name)
        {
            using MySqlConnection connection = Open();
            return connection.QuerySingleOrDefault<MemberRecord>(
                "SELECT Id, Name, Username, Online FROM characters WHERE Name = @name LIMIT 1",
                new { name });
        }

        static int ClanOf(int characterId)
        {
            using MySqlConnection connection = Open();
            return connection.ExecuteScalar<int>(
                "SELECT StatValue FROM stats WHERE Type = @type AND Instance = @characterId AND StatId = 5",
                new { type = OwnerType, characterId });
        }

        static void WriteClan(int characterId, int orgId, int rank)
        {
            using MySqlConnection connection = Open();
            connection.Execute(
                "INSERT INTO stats (Type, Instance, StatId, StatValue) VALUES (@type, @characterId, @statId, @statValue) "
                + "ON DUPLICATE KEY UPDATE StatValue = @statValue",
                new { type = OwnerType, characterId, statId = (int)CharacterStat.Clan, statValue = orgId });
            connection.Execute(
                "INSERT INTO stats (Type, Instance, StatId, StatValue) VALUES (@type, @characterId, @statId, @statValue) "
                + "ON DUPLICATE KEY UPDATE StatValue = @statValue",
                new { type = OwnerType, characterId, statId = (int)CharacterStat.ClanLevel, statValue = rank });
        }

        static MySqlConnection Open()
        {
            var connection = new MySqlConnection(MySqlConnectionSettings.GetRequiredConnectionString());
            connection.Open();
            return connection;
        }

        static void ApplyLive(Player player, int orgId, int rank)
        {
            player.Stats.Set(CharacterStat.Clan, orgId, StatDetail.Base, dirty: true);
            player.Stats.Set(CharacterStat.ClanLevel, rank, StatDetail.Base, dirty: true);
            player.FlushDirtyStats();
        }

        void OpenChannel(Player player, int orgId)
        {
            _chat.Value.TrySend(new ChatCommand
            {
                CharacterId = player.Identity.Instance,
                ChatCommandString = "#aorebirth-org-open " + orgId.ToString(CultureInfo.InvariantCulture)
            });
        }

        void Post(int orgId, string text)
        {
            if (orgId <= 0 || string.IsNullOrWhiteSpace(text))
                return;
            string clipped = text.Replace('\r', ' ').Replace('\n', ' ');
            if (clipped.Length > 350)
                clipped = clipped.Substring(0, 350);
            _chat.Value.TrySend(new ChatCommand
            {
                CharacterId = 0,
                ChatCommandString = "#aorebirth-org-say " + orgId.ToString(CultureInfo.InvariantCulture) + " " + clipped
            });
        }

        static void Say(Player player, string text)
            => GmCommandFeedback.Send(player.Session!, player, text);

        static void Window(Player player, string body, string label)
            => GmCommandFeedback.Send(player.Session!, player, GetStatsAomlBuilder.BuildLink(body, DiagAoml.Title(label)));

        static string Clip(string text, int max)
        {
            text = (text ?? string.Empty).Trim().Replace('\r', ' ').Replace('\n', ' ');
            return text.Length <= max ? text : text.Substring(0, max);
        }

        static bool ValidName(string name)
        {
            if (name.Length < 3 || name.Length > 24)
                return false;
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '-')
                    continue;
                return false;
            }

            return name.Trim().Length >= 3;
        }

        static string Esc(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var escaped = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '&':
                        escaped.Append("&amp;");
                        break;
                    case '<':
                        escaped.Append("&lt;");
                        break;
                    case '>':
                        escaped.Append("&gt;");
                        break;
                    case '"':
                    case '\'':
                        escaped.Append("&#39;");
                        break;
                    default:
                        escaped.Append(c);
                        break;
                }
            }

            return escaped.ToString();
        }

        sealed class RaidNote
        {
            public List<string> Raid { get; } = new();

            public string Loot { get; set; } = string.Empty;
        }

        sealed class OrgRecord
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;

            public int LeaderId { get; set; }

            public string Objective { get; set; } = string.Empty;

            public string History { get; set; } = string.Empty;
        }

        sealed class MemberRecord
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;

            public string Username { get; set; } = string.Empty;

            public int Online { get; set; }
        }
    }
}
