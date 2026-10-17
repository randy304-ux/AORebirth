namespace ZoneEngine_New.Core.Mobs
{
    using System.Collections.Generic;
    using System.Text.Json;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// One boss trigger. On = "FirstAggro" (first damage taken), "HealthBelow" (health under Pct percent) or "Death" (on death; those adds are not despawned with the boss).
    /// Each trigger fires once per fight; the fight resets on death or leash.
    /// </summary>
    public sealed class NpcBossTrigger
    {
        public string On { get; set; } = string.Empty;

        public int Pct { get; set; }

        public List<NpcBossAction> Actions { get; set; } = new();
    }

    /// <summary>
    /// Type "SpawnAdds": Count NPCs of Hash within Radius metres of the boss, at the boss level unless Level is set.
    /// They aggro the attacker at once and are despawned when the boss dies or resets.
    /// </summary>
    public sealed class NpcBossAction
    {
        public string Type { get; set; } = "SpawnAdds";

        public string Hash { get; set; } = string.Empty;

        public int Count { get; set; } = 1;

        public float Radius { get; set; } = 4f;

        public int? Level { get; set; }

        /// <summary>Death triggers: despawn the add after this many seconds (e.g. an exit hatch that resets).</summary>
        public int? DespawnSeconds { get; set; }
    }

    public sealed class MobItemTableEntry
    {
        public string Hash { get; set; } = string.Empty;

        public int Repeats { get; set; }

        public int Chance { get; set; }

        public int LevelMod { get; set; }

        /// <summary>
        /// When <see cref="CountMax"/> is greater than zero, this row ignores chance and instead adds a
        /// uniform random count of items in [<see cref="CountMin"/>, <see cref="CountMax"/>]. Each item
        /// is its own quality roll of <see cref="Hash"/>.
        /// </summary>
        public int CountMin { get; set; }

        public int CountMax { get; set; }
    }

    /// <summary>One fixed line in a hash vendor's shop. HighId may equal LowId.</summary>
    public sealed class NpcShopStockLine
    {
        public int LowId { get; set; }

        public int HighId { get; set; }

        public int Quality { get; set; }
    }

    /// <summary>
    /// Resolved NPC template used at spawn. Materialized from NpcTemplates.json.
    /// </summary>
    public sealed class MobTemplate
    {
        public bool HasHeadMesh { get; set; }

        public string Hash { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int TemplateId { get; set; }

        /// <summary>
        /// Placeholder leaf used when a spawn hash is not in <c>NpcTemplates.json</c>.
        /// </summary>
        public const string FallbackHash = "AAAA";

        /// <summary>Per-band stats copied from NpcTemplates.json.</summary>
        public Dictionary<int, int> Stats { get; set; } = new();

        /// <summary>
        /// Behaviour switches from the level entry's "Features" (plus NoCombat for the unresolved placeholder and for
        /// blue-named NPCs).
        /// </summary>
        public HashSet<NpcFeature> Features { get; set; } = new();

        /// <summary>World XYZ points the NPC walks in a loop when Features contains Wander (used as patrol instead of random roam).</summary>
        public System.Collections.Generic.List<float[]>? PatrolWaypoints { get; set; }

        public bool Has(NpcFeature feature) => Features.Contains(feature);

        public int MinLevel { get; set; }

        public int MaxLevel { get; set; }

        /// <summary>Historical evidence metadata is preserved but does not authorize runtime activation.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("ContentAcceptance")]
        public JsonElement? ContentProvenance { get; set; }

        public bool UnresolvedPlaceholder { get; set; }

        public List<NpcWeaponVariant> WeaponVariants { get; set; } = new();

        /// <summary>Per-slot AOID lists from the template Equipment jagged array.</summary>
        public List<List<int>> Equipment { get; set; } = new();

        /// <summary>Fixed shop lines for a hash vendor. Empty leaves the shared vending-machine table in place.</summary>
        public List<NpcShopStockLine> ShopStock { get; set; } = new();

        /// <summary>SCFU texture overrides keyed by place. Empty omits the texture block.</summary>
        public Dictionary<int, int> Textures { get; set; } = new();

        /// <summary>Opaque SCFU extended texture override payload copied from template data.</summary>
        public List<TextureOverride> TextureOverrides { get; set; } = new();

        /// <summary>Optional raw CorpseFullUpdate template for NPCs whose corpse visuals are not codec-representable.</summary>
        public MobCorpseFullUpdateTemplate? CorpseFullUpdateTemplate { get; set; }

        public int KnuBotId { get; set; }

        public List<MobItemTableEntry> ItemTable { get; set; } = new();

        /// <summary>Dyna modifier that was rolled for this spawn (DynaModifiers.All name), or null.</summary>
        public string? DynaModifier { get; set; }

        /// <summary>Data-driven boss triggers (NpcTemplates.json band "BossTriggers"), see <see cref="NpcBossTrigger"/>.</summary>
        public List<NpcBossTrigger> BossTriggers { get; set; } = new();

        public string BinaryListData { get; set; } = string.Empty;

        public JsonElement? BinaryList { get; set; }
    }

    public sealed class MobCorpseFullUpdateTemplate
    {
        public byte[] PacketTemplate { get; set; } = [];

        public int MessageId { get; set; }

        public int MessageIdOffset { get; set; } = -1;

        public int PacketLengthOffset { get; set; } = -1;

        public int SenderInstanceOffset { get; set; } = -1;

        public int ReceiverInstanceOffset { get; set; } = -1;

        public int CorpseInstanceOffset { get; set; } = -1;

        public int PositionXOffset { get; set; } = -1;

        public int PositionYOffset { get; set; } = -1;

        public int PositionZOffset { get; set; } = -1;

        public int PlayfieldIdOffset { get; set; } = -1;

        public int DeadNpcInstanceOffset { get; set; } = -1;

        public int CatMeshOffset { get; set; } = -1;

        public int CashOffset { get; set; } = -1;

        public int MonsterDataOffset { get; set; } = -1;

        public int TailDeadNpcInstanceOffset { get; set; } = -1;
    }
}
