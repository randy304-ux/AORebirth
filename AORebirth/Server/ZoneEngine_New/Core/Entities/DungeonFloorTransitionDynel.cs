namespace ZoneEngine_New.Core.Entities;

using System;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Inventory;
using Vector3 = AORebirth.Core.Vector.Vector3;

/// <summary>A configured dungeon platform/button using the existing world-use and intrazone teleport mechanics.</summary>
public sealed class DungeonFloorTransitionDynel : StaticDynel
{
    public DungeonFloorTransitionDynel(Identity identity, ItemTemplate template, Vector3 landing)
        : base(identity, template)
    {
        Landing = landing ?? throw new ArgumentNullException(nameof(landing));
        Stats.Set(CharacterStat.ACGItemLevel, template.Quality);
        Stats.Set(CharacterStat.MultipleCount, Math.Max(1, template.MultipleCount));
    }

    public Vector3 Landing { get; }

    protected override bool DelaysUse => false;

    protected override bool OnUse(Player player)
    {
        if (player.IsDead || player.IsPersistenceQuarantined || player.Session == null
            || Playfield == null || !ReferenceEquals(player.Playfield, Playfield)
            || !CanBeginUse(player)) return false;
        player.TeleportWithinPlayfield(Landing);
        return true;
    }

    /// <summary>
    /// Runtime statels with no Dynels.dat entry are only drawn when sent as retail world statels: owner 0:0 so the
    /// position is serialized, and the short world stat set (see <see cref="DungeonExitTerminal"/>, fixed 2026-10-18).
    /// The lift platform/buttons went out with the base message and were never drawn (Randy 2026-10-24).
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
