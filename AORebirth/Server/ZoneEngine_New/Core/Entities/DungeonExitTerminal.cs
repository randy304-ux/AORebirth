namespace ZoneEngine_New.Core.Entities;

using System;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory;

/// <summary>
/// Dungeon Dogs: the exit terminal a generated mission dungeon spawns once its quest is completed. Using it puts the
/// player back outside the entrance door they zoned in from (the same landing as walking out of the exit door).
/// </summary>
public sealed class DungeonExitTerminal : StaticDynel
{
    readonly Action<Player> _leave;

    public DungeonExitTerminal(Identity identity, ItemTemplate template, Action<Player> leave)
        : base(identity, template)
    {
        _leave = leave ?? throw new ArgumentNullException(nameof(leave));
        Stats.Set(CharacterStat.ACGItemLevel, template.Quality);
        Stats.Set(CharacterStat.MultipleCount, Math.Max(1, template.MultipleCount));
    }

    protected override bool DelaysUse => false;

    protected override bool OnUse(Player player)
    {
        if (player.IsDead || player.IsPersistenceQuarantined || player.Session == null
            || Playfield == null || !ReferenceEquals(player.Playfield, Playfield)
            || !CanBeginUse(player)) return false;
        _leave(player);
        return true;
    }
}