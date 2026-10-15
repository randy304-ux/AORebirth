namespace ZoneEngine_New.Core.Bots
{
    using System;

    using AORebirth.Communication.Messages;

    using ZoneEngine_New.Core.Chat;
    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// ChatEngine forwards tells to Buffbot and Orgbot, and dot commands typed in a channel.
    /// The zone is the only process with the item catalog and the player's NCU.
    /// </summary>
    public sealed class BotChatDispatch
    {
        public const string TellPrefix = "#aorebirth-bot-tell ";

        readonly Lazy<GmCommandDispatcher> _commands;
        readonly BuffbotService _buffs;
        readonly OrgBotService _orgs;
        readonly Lazy<IChatEngineLink> _chat;

        public BotChatDispatch(
            Lazy<GmCommandDispatcher> commands,
            BuffbotService buffs,
            OrgBotService orgs,
            Lazy<IChatEngineLink> chat)
        {
            ArgumentNullException.ThrowIfNull(commands);
            ArgumentNullException.ThrowIfNull(buffs);
            ArgumentNullException.ThrowIfNull(orgs);
            ArgumentNullException.ThrowIfNull(chat);
            _commands = commands;
            _buffs = buffs;
            _orgs = orgs;
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
                Text = "Enter the world, then tell Buffbot or Orgbot again.",
                Unk1 = 3
            });
        }
    }
}
