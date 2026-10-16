namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Side files next to a GameData catalog: "NpcTemplates.json" also takes "NpcTemplates.*.json" (for example
    /// NpcTemplates.Alien.json written by ddogs/tools/Build-AlienInvasion.ps1), merged in name order. Objects merge by
    /// top-level key (an overlay key replaces the base key); arrays are appended. Lets content scripts own a file
    /// instead of rewriting a shared one.
    /// </summary>
    public static class GameDataOverlay
    {
        public static string ReadMerged(string basePath)
        {
            string text = File.ReadAllText(basePath);
            string[] overlays = FindOverlays(basePath);
            if (overlays.Length == 0)
                return text;

            var options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
            JsonNode? root = JsonNode.Parse(text, documentOptions: options);
            foreach (string overlay in overlays)
            {
                JsonNode? extra = JsonNode.Parse(File.ReadAllText(overlay), documentOptions: options);
                if (root is JsonObject baseObject && extra is JsonObject extraObject)
                {
                    foreach (string key in extraObject.Select(pair => pair.Key).ToArray())
                    {
                        JsonNode? value = extraObject[key];
                        extraObject.Remove(key);
                        baseObject[key] = value;
                    }
                }
                else if (root is JsonArray baseArray && extra is JsonArray extraArray)
                {
                    foreach (JsonNode? item in extraArray.ToArray())
                    {
                        extraArray.Remove(item);
                        baseArray.Add(item);
                    }
                }
                else
                {
                    throw new InvalidDataException("GameData overlay " + overlay + " does not match the shape of " + basePath);
                }
            }

            return root?.ToJsonString() ?? text;
        }

        public static string[] FindOverlays(string basePath)
        {
            string? dir = Path.GetDirectoryName(basePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return Array.Empty<string>();
            string stem = Path.GetFileNameWithoutExtension(basePath);
            return Directory.GetFiles(dir, stem + ".*.json")
                .Where(path => !string.Equals(Path.GetFullPath(path), Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
    }
}