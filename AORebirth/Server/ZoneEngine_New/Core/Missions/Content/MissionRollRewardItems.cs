namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Missions;

/// <summary>Queryable reward rows from packaged item content, with roll eligibility supplied by policy.</summary>
internal static class MissionRollRewardItems
{
    /// <summary>Catalog is the RollPolicy file name (Clusters / Implants / Nanos / …) used for equal-weight category picks.</summary>
    sealed record Item(int LowId, int HighId, int LowQl, int HighQl, string Name, bool Fixed, string Catalog);
    static readonly Lazy<Item[]> Catalog = new(Load);
    internal static QuestItemShort Select(int quality, Random random)
    {
        if (quality <= 0) throw new ArgumentOutOfRangeException(nameof(quality));
        ArgumentNullException.ThrowIfNull(random);
        var eligible = Eligible(quality);
        if (eligible.Length == 0) throw new InvalidOperationException("No configured mission reward covers the requested quality.");
        // Equal weight per catalog file so implants (~8k rows) cannot drown out nanos/armor/weapons.
        var groups = eligible.GroupBy(r => r.Catalog, StringComparer.OrdinalIgnoreCase).Where(g => g.Any()).ToArray();
        var group = groups[random.Next(groups.Length)].ToArray();
        var selected = group[random.Next(group.Length)];
        return ToReward(selected, quality);
    }

    /// <summary>True when <paramref name="itemId"/> is a LowId or HighId in the mission reward catalogs (any QL).</summary>
    internal static bool TryFindCatalogItem(int itemId, out string name, out string reason)
    {
        name = string.Empty;
        reason = string.Empty;
        if (itemId <= 0)
        {
            reason = "Item id must be a positive integer.";
            return false;
        }

        if (!TryGetRows(out Item[] rows, out reason))
            return false;

        Item? match = FindById(rows, itemId);
        if (match == null)
        {
            reason = "That item is not in the mission reward pool.";
            return false;
        }

        name = match.Name;
        return true;
    }

    /// <summary>
    /// Catalog QL band for a pin warning: fixed nanos use their single QL; variable rows use LowQl-HighQl.
    /// </summary>
    internal static bool TryGetCatalogQuality(int itemId, out string name, out int lowQl, out int highQl, out bool fixedQuality)
    {
        name = string.Empty;
        lowQl = 0;
        highQl = 0;
        fixedQuality = false;
        if (itemId <= 0 || !TryGetRows(out Item[] rows, out _))
            return false;
        Item? match = FindById(rows, itemId);
        if (match == null)
            return false;
        name = match.Name;
        lowQl = match.LowQl;
        highQl = match.HighQl;
        fixedQuality = match.Fixed;
        return true;
    }

    /// <summary>
    /// Finds a catalog row by exact reward name (e.g. <c>Nano Crystal (Mongo Slam!)</c>).
    /// Used to remap nano-program ids to their crystal reward ids.
    /// </summary>
    internal static bool TryFindCatalogItemByName(string itemName, out int itemId, out string name, out string reason)
    {
        itemId = 0;
        name = string.Empty;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(itemName))
        {
            reason = "Item name is required.";
            return false;
        }

        if (!TryGetRows(out Item[] rows, out reason))
            return false;

        Item? match = rows.FirstOrDefault(r => string.Equals(r.Name, itemName, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            reason = "That item is not in the mission reward pool.";
            return false;
        }

        itemId = match.LowId;
        name = match.Name;
        return true;
    }

    /// <summary>
    /// Resolves a pinned item to the reward that would be granted at <paramref name="quality"/>,
    /// or fails when the catalog row is not eligible at that QL (including nano tolerance).
    /// Accepts either LowId or HighId.
    /// </summary>
    internal static bool TryResolve(int itemId, int quality, out QuestItemShort reward, out string reason)
    {
        reward = new QuestItemShort();
        reason = string.Empty;
        if (itemId <= 0)
        {
            reason = "Item id must be a positive integer.";
            return false;
        }

        if (quality <= 0)
        {
            reason = "Mission quality must be positive.";
            return false;
        }

        if (!TryGetRows(out Item[] rows, out reason))
            return false;

        Item? catalogMatch = FindById(rows, itemId);
        if (catalogMatch == null)
        {
            reason = "That item is not in the mission reward pool.";
            return false;
        }

        Item[] eligible = Eligible(quality);
        Item? match = eligible.FirstOrDefault(r => r.LowId == itemId || r.HighId == itemId);
        if (match == null)
        {
            reason = FormatIneligibleQualityReason(catalogMatch, quality);
            return false;
        }

        reward = ToReward(match, quality);
        return true;
    }

    internal static string FormatNotInPoolReason(int itemId, string? knownName)
    {
        if (string.IsNullOrWhiteSpace(knownName))
            return "That item is not in the mission reward pool.";

        string crystalName = "Nano Crystal (" + knownName + ")";
        if (TryFindCatalogItemByName(crystalName, out int crystalId, out string matchedName, out _))
        {
            return "Id " + itemId.ToString(CultureInfo.InvariantCulture)
                + " is " + knownName
                + " (nano program), not a mission reward. Use the crystal id "
                + crystalId.ToString(CultureInfo.InvariantCulture)
                + " for " + matchedName + ".";
        }

        return "Id " + itemId.ToString(CultureInfo.InvariantCulture)
            + " is " + knownName
            + ", which is not in the mission reward pool (XML Data/MissionRewards catalogs).";
    }

    static string FormatIneligibleQualityReason(Item row, int quality)
    {
        string ids = row.LowId == row.HighId
            ? row.LowId.ToString(CultureInfo.InvariantCulture)
            : row.LowId.ToString(CultureInfo.InvariantCulture) + "/" + row.HighId.ToString(CultureInfo.InvariantCulture);
        string band = row.LowQl == row.HighQl
            ? "QL " + row.LowQl.ToString(CultureInfo.InvariantCulture)
            : "QL " + row.LowQl.ToString(CultureInfo.InvariantCulture) + "-" + row.HighQl.ToString(CultureInfo.InvariantCulture);

        if (row.Fixed)
        {
            int tolerance = MissionRollPolicy.Current.Rewards.NanoQualityTolerance;
            int minMission = Math.Max(1, row.LowQl - tolerance);
            int maxMission = row.HighQl + tolerance;
            return row.Name + " (" + ids + ") is in the pool at " + band
                + ", but only rolls when mission QL is "
                + minMission.ToString(CultureInfo.InvariantCulture) + "-"
                + maxMission.ToString(CultureInfo.InvariantCulture)
                + " (nano tolerance ±"
                + tolerance.ToString(CultureInfo.InvariantCulture)
                + "). Mission QL is "
                + quality.ToString(CultureInfo.InvariantCulture) + ".";
        }

        return row.Name + " (" + ids + ") is in the pool at " + band
            + ", which does not cover mission QL "
            + quality.ToString(CultureInfo.InvariantCulture) + ".";
    }

    static bool TryGetRows(out Item[] rows, out string reason)
    {
        rows = [];
        reason = string.Empty;
        try
        {
            rows = Catalog.Value;
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            reason = "Mission reward catalogs are unavailable.";
            return false;
        }
    }

    static Item? FindById(Item[] rows, int itemId)
        => rows.FirstOrDefault(r => r.LowId == itemId || r.HighId == itemId);

    static QuestItemShort ToReward(Item selected, int quality)
        => new() { LowId = selected.LowId, HighId = selected.HighId, Quality = selected.Fixed ? selected.LowQl : quality };

    /// <summary>
    /// Variable-QL rows in band, plus fixed-quality (nano) rows within NanoQualityTolerance.
    /// Nanos used to be fallback-only, so any implant match blocked them from ever rolling.
    /// </summary>
    static Item[] Eligible(int quality)
    {
        var rows = Catalog.Value;
        var variable = rows.Where(r => !r.Fixed && r.LowQl <= quality && quality <= r.HighQl);
        int tolerance = MissionRollPolicy.Current.Rewards.NanoQualityTolerance;
        var fixedOk = rows.Where(r => r.Fixed && r.LowQl <= quality + tolerance && r.HighQl >= quality - tolerance);
        return variable.Concat(fixedOk).ToArray();
    }

    static Item[] Load()
    {
        var policy = MissionRollPolicy.Current.Rewards;
        var rows = new List<Item>();
        var fixedFamilies = new HashSet<(int, int)>();
        string[] roots = [Path.Combine(AppContext.BaseDirectory, "XML Data", "MissionRewards"),
            Path.Combine(AppContext.BaseDirectory, "MissionRewards"), Path.Combine(Environment.CurrentDirectory, "XML Data", "MissionRewards"),
            Path.Combine(Environment.CurrentDirectory, "MissionRewards")];
        string root = roots.FirstOrDefault(Directory.Exists) ?? throw new InvalidDataException("Mission reward content directory is missing.");
        foreach (var file in policy.Catalogs)
        {
            string path = Path.Combine(root, file.File);
            if (!File.Exists(path)) continue;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("Key", out var key) || key.ValueKind != JsonValueKind.Object) continue;
                int Number(string name) => key.TryGetProperty(name, out var field) ? field.GetInt32() : 0;
                int low = Number("LowId"), high = Number("HighId"), min = Number("LowQl"), max = Number("HighQl");
                string name = key.TryGetProperty("Name", out var label) ? label.GetString() ?? string.Empty : string.Empty;
                if (low <= 0 || MissionRareLootCatalog.IsRareLootTemplate(low)
                    || policy.ExcludedNameFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    || file.FixedQuality && (min != max || !fixedFamilies.Add((low, high)))) continue;
                rows.Add(new(low, high > 0 ? high : low, min, max > 0 ? max : min, name, file.FixedQuality, file.File));
            }
        }
        if (rows.Count == 0) throw new InvalidDataException("Mission reward content has no eligible rows.");
        return rows.ToArray();
    }
}
