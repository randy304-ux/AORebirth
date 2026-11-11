namespace ZoneEngine_New.Core.Battlestations
{
    using System;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// Visible VP Reward drop. Reuses the Dust Brigade Exit Beacon template (269820, mesh 258404) —
    /// the same red-beam pillar used as the alien-ship / generated-dungeon exit beacon.
    /// </summary>
    public sealed class BattleStationVpRewardDynel : StaticDynel
    {
        public const int DefaultTemplateId = BattleStationScoring.VpRewardTemplateId;

        const int WorldFlags = (int)(ItemFlags.Visible | ItemFlags.TellCollision | ItemFlags.DisableStatelCollision);

        public BattleStationVpRewardDynel(Identity identity, ItemTemplate template)
            : base(identity, template)
        {
            Stats.Set(CharacterStat.ACGItemLevel, Math.Max(1, template.Quality));
            Stats.Set(CharacterStat.MultipleCount, 1);
        }

        protected override bool DelaysUse => false;

        protected override bool OnUse(Player player)
        {
            if (player.IsDead || player.Session == null || Playfield == null
                || !ReferenceEquals(player.Playfield, Playfield)
                || !CanBeginUse(player))
                return false;

            return BattleStationClock.TryCollectVpReward(player, this);
        }

        public override MessageBody BuildSpawnMessage()
        {
            var message = (SimpleItemFullUpdateMessage)base.BuildSpawnMessage();
            // Owner 0:0 so Coordinate/Heading are written (runtime instances have no Dynels.dat slot).
            message.Identitytype = 0;
            message.Instance = 0;
            message.Name = "VP Reward\0";
            return message;
        }

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

        static GameTuple<CharacterStat, uint> Stat(CharacterStat stat, int value)
            => new() { Value1 = stat, Value2 = (uint)value };
    }
}
