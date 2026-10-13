namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Dungeon Dogs hired mercenaries: pet-based companions a player hires from a recruiter NPC (Knubot
    /// <c>{hiremerc KIND}</c> / <c>{dismissmercs}</c>). A merc is an ordinary player pet (in memory only, never saved),
    /// so it follows, fights for its owner, goes with it through zone changes, and is gone on logout or owner death.
    /// One merc per profession; each profession has its own pet slot so mercs do not replace the player's own pets,
    /// except the Doctor, which uses the heal slot so the pet AI heals its owner. GameData/Mercs.json "Enabled" toggles hiring.
    /// </summary>
    public static class Mercs
    {
        public sealed record Kind(string Key, string Hash, string Title, int PetType);

        public static readonly Kind[] Kinds =
        [
            new("doctor", "MRDR", "Doctor", PetTypes.Heal),
            new("enforcer", "MREN", "Enforcer", 10001),
            new("soldier", "MRSO", "Soldier", 11001),
            new("keeper", "MRKE", "Keeper", 12001),
        ];

        public static Kind? Find(string key)
        {
            foreach (Kind kind in Kinds)
            {
                if (string.Equals(kind.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kind;
            }

            return null;
        }

        public static bool IsMercHash(string? hash)
        {
            foreach (Kind kind in Kinds)
            {
                if (string.Equals(kind.Hash, hash, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>Doctor heal nanos by merc level (Minor Healing, Healing, Improved Healing, Superior Healing).</summary>
        public static IReadOnlyList<int> NanosFor(string hash, int level)
        {
            if (!string.Equals(hash, "MRDR", StringComparison.OrdinalIgnoreCase))
                return [];

            return level switch
            {
                < 25 => [26465],
                < 75 => [26466],
                < 150 => [28665],
                _ => [28676]
            };
        }

        /// <summary>GameData/Mercs.json { "Enabled": false } turns hiring off. Missing file: enabled.</summary>
        public static bool Enabled(string gameDataRoot)
        {
            try
            {
                string path = Path.Combine(gameDataRoot, "Mercs.json");
                if (!File.Exists(path))
                    return true;

                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
                return !doc.RootElement.TryGetProperty("Enabled", out JsonElement value) || value.ValueKind != JsonValueKind.False;
            }
            catch
            {
                return true;
            }
        }
    }
}