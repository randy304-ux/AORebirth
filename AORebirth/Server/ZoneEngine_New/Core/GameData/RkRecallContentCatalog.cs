namespace ZoneEngine_New.Core.GameData;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using AORebirth.Core.GameData;
using AORebirth.Core.Vector;

/// <summary>
/// GameData/RkRecall.json: server landing for the Rubi-Ka Recall Beacon (and any other listed item ids).
/// Official destinations live in items.dat, which is not checked in.
/// </summary>
public sealed class RkRecallContentCatalog
{
    public static RkRecallContentCatalog Empty { get; } = new();

    public int SchemaVersion { get; set; } = 1;

    public string? Notes { get; set; }

    public int[] ItemIds { get; set; } = [];

    public int PlayfieldId { get; set; }

    public float[] Position { get; set; } = [];

    public float[]? Heading { get; set; }

    public bool IsConfigured =>
        PlayfieldId > 0
        && Position is { Length: 3 }
        && Position.All(float.IsFinite)
        && ItemIds is { Length: > 0 };

    public bool AppliesTo(int itemId)
        => IsConfigured && Array.IndexOf(ItemIds, itemId) >= 0;

    public bool TryGetLanding(out int playfieldId, out Vector3 landing, out Quaternion? heading)
    {
        playfieldId = 0;
        landing = null!;
        heading = null;
        if (!IsConfigured)
            return false;

        playfieldId = PlayfieldId;
        landing = new Vector3(Position[0], Position[1], Position[2]);
        if (Heading is { Length: 4 } && Heading.All(float.IsFinite))
            heading = new Quaternion(Heading[0], Heading[1], Heading[2], Heading[3]);
        return true;
    }

    public static RkRecallContentCatalog Load(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return Empty;

        string path = Path.Combine(root, GameDataPaths.RkRecallFileName);
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : Empty;
    }

    public static RkRecallContentCatalog Parse(string json)
    {
        RkRecallContentCatalog content = JsonSerializer.Deserialize<RkRecallContentCatalog>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("RK recall content is null.");
        content.Validate();
        return content;
    }

    public void Validate()
    {
        if (SchemaVersion != 1
            || PlayfieldId <= 0
            || Position is not { Length: 3 }
            || Position.Any(value => !float.IsFinite(value))
            || ItemIds is not { Length: > 0 }
            || ItemIds.Any(id => id <= 0)
            || (Heading != null && (Heading.Length != 4 || Heading.Any(value => !float.IsFinite(value)))))
        {
            throw new InvalidDataException("Invalid RK recall content.");
        }
    }
}
