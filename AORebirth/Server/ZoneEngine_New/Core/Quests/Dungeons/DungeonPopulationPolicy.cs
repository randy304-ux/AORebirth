namespace ZoneEngine_New.Core.Quests.Dungeons;

using System;
using System.Linq;
using ZoneEngine_New.Core.GameData;

/// <summary>Mission population references into the existing NPC catalog, not a second source of NPC definitions.</summary>
public sealed class DungeonPopulationPolicy
{
    public int SpawnPointChancePercent { get; set; }

    public string[] RegularNpcHashes { get; set; } = [];

    /// <summary>Team missions: regular family hash to its boss family hash (spawned once in the boss room). Optional.</summary>
    public System.Collections.Generic.Dictionary<string, string> BossNpcHashes { get; set; } = new();

    /// <summary>Boss level = mission QL + this, capped at <see cref="BossMaxLevel"/>.</summary>
    public int BossLevelBonus { get; set; } = 2;

    public int BossMaxLevel { get; set; } = 101;

    internal bool IsValid(IGameData gameData)
        => SpawnPointChancePercent is >= 0 and <= 100
            && RegularNpcHashes is { Length: > 0 }
            && RegularNpcHashes.Distinct(StringComparer.Ordinal).Count() == RegularNpcHashes.Length
            && RegularNpcHashes.All(hash => !string.IsNullOrWhiteSpace(hash) && gameData.HasMobTemplate(hash));
}
