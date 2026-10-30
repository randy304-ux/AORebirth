namespace ZoneEngine_New.Core.Inventory
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Quests;

    /// <summary>
    /// Dungeon Dogs (Arete live): the found Bank of Rubi-Ka Credit Card. Right-click in inventory completes
    /// "Steal the credits" (CCS1, 15000 credits via the quest reward) and consumes the card. A character that already
    /// returned it to Leonora (CCR1) or already cashed it (CCS1) gets nothing.
    /// </summary>
    public static class CreditCards
    {
        public const string StealQuest = "CCS1";
        public const string ReturnQuest = "CCR1";

        public static bool IsCard(int id) => id is 297302 or 297315;

        public static bool TryUse(Item item, Player player, Identity slotIdentity)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(player);
            if (!IsCard(item.LowId) && !IsCard(item.HighId))
                return false;

            QuestService? quests = player.Playfield?.GetService<QuestService>();
            if (quests == null)
                return true;

            if (Completed(quests, player, StealQuest) || Completed(quests, player, ReturnQuest))
            {
                Say(player, "The card has already been dealt with.");
                return true;
            }

            if (!quests.TryAssignTemplate(player, StealQuest, out string result))
            {
                Say(player, result);
                return true;
            }

            if (!quests.TryComplete(player, StealQuest, out result))
            {
                Say(player, result);
                return true;
            }

            item.DestroyOne(player, slotIdentity);
            return true;
        }

        static bool Completed(QuestService quests, Player player, string hash)
            => quests.Catalog.TryGet(hash, out QuestTemplate template) && template.MissionBit is int bit
               && MissionBits.Has(player.Stats, bit);

        static void Say(Player player, string text)
            => player.Session?.Send(new ChatTextMessage { Identity = player.Identity, Text = text });
    }
}