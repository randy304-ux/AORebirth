namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using SmokeLounge.AOtomation.Messaging.GameData;

public sealed class WorldNpcDefinition
{
    public string Key { get; set; } = string.Empty;
    /// <summary>Informational provenance; never used to grant runtime permission.</summary>
    public string Provenance { get; set; } = string.Empty;
    public string ContentNpcIdentity { get; set; } = string.Empty;
    /// <summary>Zero denotes a template instantiated by a mechanic rather than world activation.</summary>
    public int PlayfieldId { get; set; }
    /// <summary>AO identity instance bits; JSON may use unsigned 32-bit values above Int32.MaxValue.</summary>
    [JsonConverter(typeof(IdentityInstanceJsonConverter))]
    public int InstanceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public float[] Position { get; set; } = [];
    public float[] Rotation { get; set; } = [];
    public Dictionary<int, int> Stats { get; set; } = new();
    public WorldTexture[] Textures { get; set; } = [];
    public Mesh[] Meshes { get; set; } = [];
    public bool Passive { get; set; }
    public bool PassiveRegen { get; set; } = true;
    public bool Attackable { get; set; }
    public bool TickLiving { get; set; } = true;
    public WorldNpcPresentation Presentation { get; set; } = new();
    public WorldVendorDefinition? Vendor { get; set; }
    /// <summary>Official hash point this NPC stands on; quests and Knubot scripts bind by it.</summary>
    public string? SpawnHash { get; set; }
}

public sealed class WorldMailTerminalDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Provenance { get; set; } = string.Empty;
    public int PlayfieldId { get; set; }
    [JsonConverter(typeof(IdentityInstanceJsonConverter))]
    public int InstanceId { get; set; }
    /// <summary>Item template the client draws. 37180 is the Mail Terminal.</summary>
    public int TemplateId { get; set; }
    public float[] Position { get; set; } = [];
    public float[] Rotation { get; set; } = [];
}

/// <summary>Dungeon Dogs world pickup: an ItemTemplates.json hash placed in the world (see <c>WorldPickup</c>).</summary>
public sealed class WorldPickupDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Provenance { get; set; } = string.Empty;
    public int PlayfieldId { get; set; }
    public string ItemHash { get; set; } = string.Empty;
    public int Quality { get; set; } = 1;
    public float[] Position { get; set; } = [];
    public float[] Rotation { get; set; } = [];
    /// <summary>Quests (with a MissionBit) that, once completed, close this pickup for that character.</summary>
    public string[]? UnlessCompleted { get; set; }
}

public sealed class WorldStandaloneShopDefinition
{
    public string Key { get; set; } = string.Empty;
    /// <summary>Informational provenance; never used to grant runtime permission.</summary>
    public string Provenance { get; set; } = string.Empty;
    public int PlayfieldId { get; set; }
    public float[] Position { get; set; } = [];
    public float[] Rotation { get; set; } = [];
    public WorldVendorDefinition Vendor { get; set; } = new();
}

public sealed class WorldTexture
{
    public int Place { get; set; }
    public int Id { get; set; }
}

public sealed class WorldNpcPresentation
{
    public uint? AppearanceValue { get; set; }
    public short? Family { get; set; }
    public short? LosHeight { get; set; }
    public uint? Flags { get; set; }
    public int? Flags2 { get; set; }
    public byte[]? Unknown1 { get; set; }
    public byte? Unknown2 { get; set; }
    public byte? VisibleTitle { get; set; }
    public Texture[]? Textures { get; set; }
    public Mesh[]? Meshes { get; set; }
    public Vector3[]? Waypoints { get; set; }
}

public sealed class WorldVendorDefinition
{
    /// <summary>AO vending-machine identity bits; Ofab hubs use unsigned values above Int32.MaxValue.</summary>
    [JsonConverter(typeof(IdentityInstanceJsonConverter))]
    public int InstanceId { get; set; }
    public int TemplateId { get; set; }
    public WorldVendorStock[] Stock { get; set; } = [];
    public JsonElement? CompanionPacket { get; set; }

    /// <summary>The pane still opens as a shop. The number charged is <see cref="WorldVendorStock.Price"/> in Victory Points, and the machine's sell modifier is 0 so the window shows 0 credits.</summary>
    public bool SpendsVictoryPoints { get; set; }
}

/// <summary>
/// Reads AO identity instance fields that are authored as unsigned 32-bit numbers in JSON
/// (e.g. 3180030000) into the signed <see cref="Identity.Instance"/> wire form.
/// </summary>
public sealed class IdentityInstanceJsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out int signed))
                return signed;
            if (reader.TryGetUInt32(out uint unsigned))
                return unchecked((int)unsigned);
            if (reader.TryGetInt64(out long wide) && wide is >= uint.MinValue and <= uint.MaxValue)
                return unchecked((int)(uint)wide);
        }

        throw new JsonException("Identity InstanceId must be a 32-bit AO identity value.");
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(unchecked((uint)value));
}

public sealed class WorldVendorStock
{
    public int Slot { get; set; }
    public int LowId { get; set; }
    public int HighId { get; set; }
    public int Quality { get; set; }

    /// <summary>Victory Point price when the vendor spends VP. 0 means the credit formula.</summary>
    public int Price { get; set; }
}
