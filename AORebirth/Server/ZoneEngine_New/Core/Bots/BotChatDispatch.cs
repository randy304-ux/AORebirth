namespace ZoneEngine_New.Core.Bots
{
    using System;

    using AORebirth.Communication.Messages;

    using ZoneEngine_New.Core.Chat;
    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Pets;

    /// <summary>
    /// ChatEngine forwards tells to Buffbot, Orgbot and Merc, and dot commands typed in a channel.
    /// The zone is the only process with the item catalog and the player's NCU.
    /// </summary>
    public sealed class BotChatDispatch
    {
        public const string TellPrefix = "#aorebirth-bot-tell ";

        readonly Lazy<GmCommandDispatcher> _commands;
        readonly BuffbotService _buffs;
        readonly OrgBotService _orgs;
        readonly MercMenuService _mercs;
        readonly Lazy<IChatEngineLink> _chat;

        public BotChatDispatch(
            Lazy<GmCommandDispatcher> commands,
            BuffbotService buffs,
            OrgBotService orgs,
            MercMenuService mercs,
            Lazy<IChatEngineLink> chat)
        {
            ArgumentNullException.ThrowIfNull(commands);
            ArgumentNullException.ThrowIfNull(buffs);
            ArgumentNullException.ThrowIfNull(orgs);
            ArgumentNullException.ThrowIfNull(mercs);
            ArgumentNullException.ThrowIfNull(chat);
            _commands = commands;
            _buffs = buffs;
            _orgs = orgs;
            _mercs = mercs;
            _chat = chat;
        }

        public bool TryHandle(Player player, string text)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string trimmed = text.Trim();
            if (trimmed.StartsWith(TellPrefix, StringComparison.Ordinal))
            {
                string rest = trimmed.Substring(TellPrefix.Length).Trim();
                int space = rest.IndexOf(' ');
                string bot = space < 0 ? rest : rest.Substring(0, space);
                string message = space < 0 ? string.Empty : rest.Substring(space + 1);
                if (bot.Equals("Buffbot", StringComparison.OrdinalIgnoreCase))
                    _buffs.HandleTell(player, message);
                else if (bot.Equals("Orgbot", StringComparison.OrdinalIgnoreCase))
                    _orgs.HandleTell(player, message);
                else if (IsMercTellTarget(bot))
                    _mercs.HandleTell(player, bot, message);
                return true;
            }

            if (trimmed[0] != '.' && trimmed[0] != '/')
                return false;

            string command = trimmed.TrimStart('.', '/').Trim();
            if (command.Length == 0)
                return true;

            string[] parts = command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || player.Session == null)
                return true;

            _commands.Value.TryExecute(player.Session, player, parts[0], parts.Length > 1 ? parts[1..] : Array.Empty<string>());
            return true;
        }

        public void TellOffline(int characterId, string text)
        {
            if (!text.StartsWith(TellPrefix, StringComparison.Ordinal))
                return;
            _chat.Value.TrySend(new PrivateSystemMessage
            {
                CharacterId = characterId,
                Text = "Enter the world, then tell Buffbot, Orgbot or Merc again.",
                Unk1 = 3
            });
        }

        static bool IsMercTellTarget(string bot)
        {
            if (string.IsNullOrWhiteSpace(bot))
                return false;
            if (bot.Equals("Merc", StringComparison.OrdinalIgnoreCase)
                || bot.Equals("Mercbot", StringComparison.OrdinalIgnoreCase)
                || bot.Equals("MercMenu", StringComparison.OrdinalIgnoreCase))
                return true;
            foreach (string id in MercTellIds)
            {
                if (bot.Equals(id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Randy nicks / full names / mode-tagged nameplates (unique across professions).
            string core = StripMercModeTag(bot);
            foreach (string nick in MercNicks)
            {
                if (core.Equals(nick, StringComparison.OrdinalIgnoreCase)
                    || core.StartsWith(nick + " ", StringComparison.OrdinalIgnoreCase)
                    || core.Contains('"' + nick + '"', StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static string StripMercModeTag(string name)
        {
            int open = name.LastIndexOf(" (", StringComparison.Ordinal);
            if (open > 0 && name.EndsWith(")", StringComparison.Ordinal))
                return name.Substring(0, open).Trim();
            return name.Trim();
        }

        /// <summary>Keep in sync with ChatEngine ServerBots merc id list and GameData/MercRoster.json.</summary>
        static readonly string[] MercTellIds =
        [
            "enforcer_subway", "doctor_totw", "bureaucrat_som", "fixer_biomare", "soldier_cyborg",
            "adventurer_crypt", "keeper_smugglers", "nt_sanctum", "shade_camelot",
            "ma_camelot", "trader_elysium", "engineer_scheol", "mp_adonis", "agent_penumbra"
        ];

        /// <summary>Unique Randy nicknames — keep in sync with MercRoster.json / ChatEngine ServerBots.</summary>
        static readonly string[] MercNicks =
        [
            "Anvil", "Grit", "Scalpel", "Suture", "Form", "Stamp", "Cipher", "Grid",
            "Trigger", "Striker", "Wild", "Roam", "Shield", "Virtue", "Flux", "Pulse",
            "Blade", "Stab", "Fist", "Kick", "Broker", "Margin", "Wrench", "Sprocket",
            "Ether", "Wisp", "Ghost", "Shadow"
        ];
    }
}
