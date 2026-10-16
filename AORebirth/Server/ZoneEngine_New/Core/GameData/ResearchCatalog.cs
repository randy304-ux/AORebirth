namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using AORebirth.Core.GameData;
    using Utility;

    /// <summary>
    /// Lost Eden personal research lines: Perks.json lines whose perks carry flag 0x40, with each tier's research XP
    /// cost and profession mask from ResearchCosts.json (client perk records type 1000036, Field18 / Field6).
    /// </summary>
    public sealed class ResearchCatalog
    {
        public sealed record Line(string Name, int[] PerkIds, int ProfessionMask);

        static readonly Lazy<ResearchCatalog> DefaultCatalog = new(() => LoadOrEmpty(GameDataPaths.ResolveRuntimeRoot()));
        public static ResearchCatalog Default => DefaultCatalog.Value;

        readonly Dictionary<int, long> _costs;
        readonly Dictionary<int, Line> _lineOfPerk = new();
        public IReadOnlyList<Line> Lines { get; }

        ResearchCatalog(List<Line> lines, Dictionary<int, long> costs)
        {
            Lines = lines;
            _costs = costs;
            foreach (Line line in lines)
                foreach (int id in line.PerkIds)
                    _lineOfPerk[id] = line;
        }

        public bool IsResearch(int perkId) => _lineOfPerk.ContainsKey(perkId);
        public long CostOf(int perkId) => _costs.TryGetValue(perkId, out long cost) ? cost : 0;
        public bool TryGetLine(int perkId, out Line line) => _lineOfPerk.TryGetValue(perkId, out line!);

        /// <summary>Profession id bit (1 &lt;&lt; profession); mask 0 is every profession.</summary>
        public static bool AllowsProfession(Line line, int profession)
            => line.ProfessionMask == 0 || (profession >= 0 && profession < 31 && (line.ProfessionMask & (1 << profession)) != 0);

        static ResearchCatalog LoadOrEmpty(string root)
        {
            try
            {
                return Load(root);
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or KeyNotFoundException)
            {
                Console.Error.WriteLine("ResearchCatalog: " + exception.Message);
                return new ResearchCatalog(new List<Line>(), new Dictionary<int, long>());
            }
        }

        public static ResearchCatalog Load(string root)
        {
            using JsonDocument costsDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "ResearchCosts.json")));
            var costs = new Dictionary<int, long>();
            var masks = new Dictionary<int, int>();
            foreach (JsonProperty p in costsDoc.RootElement.GetProperty("Perks").EnumerateObject())
            {
                int id = int.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture);
                costs[id] = p.Value.GetProperty("Cost").GetInt64();
                masks[id] = p.Value.GetProperty("ProfessionMask").GetInt32();
            }

            using JsonDocument perksDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, GameDataPaths.PerksFileName)));
            var lines = new List<Line>();
            foreach (JsonElement line in perksDoc.RootElement.GetProperty("PerkLines").EnumerateArray())
            {
                int flags = line.TryGetProperty("Flags", out JsonElement f) ? f.GetInt32() : 0;
                if ((flags & 0x40) == 0 || !line.TryGetProperty("Perks", out JsonElement perks))
                    continue;
                int[] ids = perks.EnumerateArray().OrderBy(p => p.GetProperty("Tier").GetInt32()).Select(p => p.GetProperty("Id").GetInt32()).ToArray();
                if (ids.Length == 0 || !costs.ContainsKey(ids[0]))
                    continue;
                lines.Add(new Line(line.GetProperty("Name").GetString() ?? "?", ids, masks[ids[0]]));
            }
            return new ResearchCatalog(lines, costs);
        }
    }
}

