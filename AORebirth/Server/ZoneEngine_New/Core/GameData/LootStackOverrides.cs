namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Core.GameData;

    /// <summary>
    /// GameData/LootStackOverrides.json: when loot mints one of these template ids, set
    /// <see cref="ZoneEngine_New.Core.Inventory.Item.StackCount"/> to the override (drop chance unchanged).
    /// </summary>
    public static class LootStackOverrides
    {
        static readonly object Gate = new();
        static Dictionary<int, int>? ByLowId;

        public static bool TryGetStack(int lowId, out int stackCount)
        {
            Dictionary<int, int> map = EnsureLoaded();
            return map.TryGetValue(lowId, out stackCount) && stackCount > 0;
        }

        static Dictionary<int, int> EnsureLoaded()
        {
            if (ByLowId != null)
                return ByLowId;

            lock (Gate)
            {
                if (ByLowId != null)
                    return ByLowId;

                ByLowId = LoadOrEmpty(GameDataPaths.ResolveRuntimeRoot());
                return ByLowId;
            }
        }

        static Dictionary<int, int> LoadOrEmpty(string root)
        {
            var map = new Dictionary<int, int>();
            string path = Path.Combine(root, GameDataPaths.LootStackOverridesFileName);
            if (!File.Exists(path))
                return map;

            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
                if (!doc.RootElement.TryGetProperty("overrides", out JsonElement overrides)
                    || overrides.ValueKind != JsonValueKind.Array)
                    return map;

                foreach (JsonElement row in overrides.EnumerateArray())
                {
                    if (!row.TryGetProperty("lowId", out JsonElement lowEl))
                        continue;
                    int lowId = lowEl.GetInt32();
                    if (lowId <= 0)
                        continue;
                    int stack = row.TryGetProperty("stackCount", out JsonElement stackEl) ? stackEl.GetInt32() : 0;
                    if (stack > 0)
                        map[lowId] = stack;
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine("LootStackOverrides: " + exception.Message);
            }

            return map;
        }
    }
}
