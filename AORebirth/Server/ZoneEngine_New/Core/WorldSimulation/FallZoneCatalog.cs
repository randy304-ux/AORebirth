namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Core.Vector;

    /// <summary>
    /// Playfields where falling into a heightmap abyss (terrain Y near 0) zones the character.
    /// Retail Shadowlands ledge drops — not walk-in wall/teleportal polygons.
    /// </summary>
    public sealed class FallZoneCatalog
    {
        public static FallZoneCatalog Instance { get; } = new FallZoneCatalog();

        const string FileName = "FallZones.json";

        readonly object _sync = new();
        readonly Dictionary<int, FallZoneExit> _exits = new();
        string? _rootPath;
        float _abyssTerrainMax = 0.05f;
        float _fallEnterMaxY = 6f;
        bool _loaded;

        public float AbyssTerrainMax
        {
            get
            {
                EnsureLoaded();
                return _abyssTerrainMax;
            }
        }

        public float FallEnterMaxY
        {
            get
            {
                EnsureLoaded();
                return _fallEnterMaxY;
            }
        }

        public int ExitCount
        {
            get
            {
                EnsureLoaded();
                lock (_sync)
                    return _exits.Count;
            }
        }

        public void ConfigureRoot(string gameDataRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            lock (_sync)
            {
                _rootPath = gameDataRoot;
                _exits.Clear();
                _loaded = false;
            }
        }

        public bool TryGetExit(int playfieldId, out FallZoneExit exit)
        {
            EnsureLoaded();
            lock (_sync)
                return _exits.TryGetValue(playfieldId, out exit!);
        }

        void EnsureLoaded()
        {
            lock (_sync)
            {
                if (_loaded)
                    return;

                _loaded = true;
                if (string.IsNullOrWhiteSpace(_rootPath))
                    return;

                string path = Path.Combine(_rootPath, FileName);
                if (!File.Exists(path))
                    return;

                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("abyssTerrainMax", out JsonElement abyss))
                        _abyssTerrainMax = abyss.GetSingle();
                    if (root.TryGetProperty("fallEnterMaxY", out JsonElement enter))
                        _fallEnterMaxY = enter.GetSingle();
                    if (!root.TryGetProperty("exits", out JsonElement exits))
                        return;

                    foreach (JsonProperty prop in exits.EnumerateObject())
                    {
                        if (!int.TryParse(prop.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pf)
                            || pf <= 0)
                            continue;

                        if (!TryParseExit(prop.Value, out FallZoneExit exit))
                            continue;

                        _exits[pf] = exit;
                    }
                }
                catch
                {
                    _exits.Clear();
                }
            }
        }

        static bool TryParseExit(JsonElement value, out FallZoneExit exit)
        {
            exit = default;
            int destPf = value.GetProperty("destPlayfield").GetInt32();
            if (destPf <= 0)
                return false;

            byte destIdx = 0;
            if (value.TryGetProperty("destIndex", out JsonElement destIdxEl))
                destIdx = destIdxEl.GetByte();

            bool hasFixed = false;
            float lx = 0f, ly = 0f, lz = 0f;
            bool keepHeading = true;
            if (value.TryGetProperty("landing", out JsonElement landing))
            {
                lx = landing.GetProperty("x").GetSingle();
                ly = landing.GetProperty("y").GetSingle();
                lz = landing.GetProperty("z").GetSingle();
                hasFixed = true;
                if (value.TryGetProperty("keepPlayerHeading", out JsonElement keep)
                    && keep.ValueKind is JsonValueKind.False)
                    keepHeading = false;
            }

            if (!hasFixed && destIdx == 0)
                return false;

            exit = new FallZoneExit(destPf, destIdx, hasFixed, lx, ly, lz, keepHeading);
            return true;
        }
    }

    public readonly record struct FallZoneExit(
        int DestPlayfieldId,
        byte DestIndex,
        bool HasFixedLanding,
        float LandingX,
        float LandingY,
        float LandingZ,
        bool KeepPlayerHeading)
    {
        public Vector3 FixedLanding => new(LandingX, LandingY, LandingZ);
    }
}
