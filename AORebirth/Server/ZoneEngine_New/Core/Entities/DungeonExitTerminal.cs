namespace ZoneEngine_New.Core.Entities;

using System;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
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

    /// <summary>
    /// Randy 20:43: neither the 19:16 boss-button terminal nor the arrival beacon was ever drawn. A runtime statel must go
    /// out as retail world statels do (capture Terminals_1419310 / Radar Display): owner fields 0:0 so the codec writes
    /// Coordinate/Heading (the base sent the dynel's own identity there, so the client looked for a Dynels.dat position
    /// this runtime instance does not have), and the short world stat set instead of the full template stats (as
    /// <see cref="WorldItem"/>).
    /// </summary>
    public override MessageBody BuildSpawnMessage()
    {
        var message = (SimpleItemFullUpdateMessage)base.BuildSpawnMessage();
        message.Identitytype = 0;
        message.Instance = 0;
        return message;
    }

    const int WorldFlags = (int)(ItemFlags.Visible | ItemFlags.TellCollision | ItemFlags.DisableStatelCollision);

    protected override GameTuple<CharacterStat, uint>[] BuildStats()
    {
        int flags = Template.Stats.TryGetValue(CharacterStat.Flags, out int templateFlags) ? templateFlags : 0;
        return
        [
            Stat(CharacterStat.Flags, flags | WorldFlags),
            Stat(CharacterStat.StaticInstance, Template.Id),
            Stat(CharacterStat.ACGItemLevel, Math.Max(1, Template.Quality)),
            Stat(CharacterStat.ACGItemTemplateID, Template.Id),
            Stat(CharacterStat.ACGItemTemplateID2, Template.Id),
            Stat(CharacterStat.MultipleCount, 1)
        ];
    }

    static GameTuple<CharacterStat, uint> Stat(CharacterStat stat, int value) => new() { Value1 = stat, Value2 = (uint)value };
}
