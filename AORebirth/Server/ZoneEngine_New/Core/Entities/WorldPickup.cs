namespace ZoneEngine_New.Core.Entities;

using System;
using System.Collections.Generic;

using AORebirth.Enums;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Knubot;
using ZoneEngine_New.Core.Quests;

/// <summary>
/// Dungeon Dogs: a placed world item (PlayfieldContent/&lt;pf&gt;/Npcs.json "Pickups") that hands a fresh copy of its
/// item to whoever clicks it (pickup or use). The world copy stays, so it is always "respawned"; a player can carry
/// one at a time, and <see cref="WorldPickupDefinition.UnlessCompleted"/> quests close it for good (one-time finds).
/// </summary>
public sealed class WorldPickup : WorldItem
{
    public WorldPickup(Identity identity, ItemTemplate template, int lowId, int highId, int quality, WorldPickupDefinition definition)
        : base(identity, template, lowId, highId, quality)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public WorldPickupDefinition Definition { get; }

    protected override bool DelaysUse => false;

    protected override bool OnUse(Player player) => TryGrant(player);

    public bool TryGrant(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.IsDead || player.IsPersistenceQuarantined || player.Session == null || Playfield == null
            || !ReferenceEquals(player.Playfield, Playfield) || !player.Inventory.IsHydrated
            || GetEdgeDistanceTo(player) > LootableDynel.OpenRange)
            return false;

        QuestService? quests = Playfield.GetService<QuestService>();
        foreach (string hash in Definition.UnlessCompleted ?? [])
        {
            if (quests != null && quests.Catalog.TryGet(hash, out QuestTemplate done) && done.MissionBit is int bit
                && MissionBits.Has(player.Stats, bit))
            {
                Say(player, "There is nothing more for you here.");
                return false;
            }
        }

        var ids = new HashSet<int> { LowId, HighId };
        if (KnubotItems.CountCarried(player, ids) > 0)
        {
            Say(player, "You already have one.");
            return false;
        }

        if (KnubotItems.FreeSlots(player) < 1)
        {
            Say(player, "Your inventory is full.");
            return false;
        }

        HashItemMinter? minter = Playfield.GetService<HashItemMinter>();
        InventoryFlushService? flush = Playfield.GetService<InventoryFlushService>();
        if (minter == null || flush == null || !minter.TryMint(Definition.ItemHash, Quality, ItemSource.Quest, out Item item))
            return false;

        KnubotItems.Give(player, item, flush);
        Say(player, "You picked up " + Template.Name + ".");
        return true;
    }

    static void Say(Player player, string text)
        => player.Session?.Send(new ChatTextMessage { Identity = player.Identity, Text = text });
}