namespace ZoneEngine_New.Core.Playfield;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Named, timed spawn sequences started from knubot <c>{spawngroup ID}</c> (e.g. Sector 35 towers and waves).
/// Loaded from <c>SpawnGroups.json</c> under the GameData root.
/// </summary>
public sealed class SpawnGroupCatalog
{
    public const string FileName = "SpawnGroups.json";

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    readonly Dictionary<string, SpawnGroupDefinition> _groups = new(StringComparer.OrdinalIgnoreCase);

    public static SpawnGroupCatalog Load(string gameDataRoot)
    {
        var catalog = new SpawnGroupCatalog();
        if (string.IsNullOrWhiteSpace(gameDataRoot))
            return catalog;

        string path = Path.Combine(gameDataRoot, FileName);
        if (!File.Exists(path))
            return catalog;

        string json = File.ReadAllText(path);
        Dictionary<string, SpawnGroupDefinition>? parsed =
            JsonSerializer.Deserialize<Dictionary<string, SpawnGroupDefinition>>(json, JsonOptions);
        if (parsed == null)
            return catalog;

        foreach (var pair in parsed)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                continue;
            pair.Value.Id = pair.Key.Trim();
            catalog._groups[pair.Value.Id] = pair.Value;
        }

        return catalog;
    }

    public int Count => _groups.Count;

    public bool TryGet(string id, out SpawnGroupDefinition group)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            group = null!;
            return false;
        }

        return _groups.TryGetValue(id.Trim(), out group!);
    }
}

public sealed class SpawnGroupDefinition
{
    public string Id { get; set; } = "";

    public string? Name { get; set; }

    public int Playfield { get; set; }

    /// <summary>Seconds from start until the encounter is despawned and can be run again.</summary>
    public int ResetTimeoutSeconds { get; set; } = 3600;

    /// <summary>After every success-hash NPC is dead, wait this long before despawning leftovers and resetting.</summary>
    public int SuccessDespawnSeconds { get; set; } = 180;

    /// <summary>If any still-spawned NPC with one of these hashes dies, the encounter fails and resets.</summary>
    public string[] FailIfDead { get; set; } = Array.Empty<string>();

    /// <summary>If every hash in a row has no living NPC, the encounter fails (e.g. all three towers down).</summary>
    public string[][] FailIfAllDead { get; set; } = Array.Empty<string[]>();

    /// <summary>The encounter succeeds once every one of these hashes has had a spawn that is now dead.</summary>
    public string[] SuccessIfDead { get; set; } = Array.Empty<string>();

    public SpawnGroupStage[] Stages { get; set; } = Array.Empty<SpawnGroupStage>();
}

public sealed class SpawnGroupStage
{
    public double DelaySeconds { get; set; }

    public SpawnGroupEntry[] Spawns { get; set; } = Array.Empty<SpawnGroupEntry>();
}

public sealed class SpawnGroupEntry
{
    [JsonPropertyName("hashText")]
    public string HashText { get; set; } = "";

    public float[] Position { get; set; } = Array.Empty<float>();

    public int Angle { get; set; }

    public int? MinLevel { get; set; }
}
