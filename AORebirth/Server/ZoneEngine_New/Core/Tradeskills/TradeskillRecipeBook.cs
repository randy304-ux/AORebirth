namespace ZoneEngine_New.Core.Tradeskills
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    using ZoneEngine_New.Core.GameData;

    /// <summary>
    /// Recipes indexed by (source id, target id) for every listed id, plus per-id process counts for the
    /// TradeskillSource/Target replies. The first recipe added for a pair wins, so hand-made files load first.
    /// </summary>
    public sealed class TradeskillRecipeBook
    {
        public const string BaseFileName = "Tradeskills.json";
        public const string CellaoFileName = "Tradeskills.cellao.json";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        readonly Dictionary<long, TradeskillRecipe> _byPair = new();
        readonly Dictionary<int, int> _sourceProcesses = new();
        readonly Dictionary<int, int> _targetProcesses = new();

        public int Count { get; private set; }

        public int Skipped { get; private set; }

        static long Key(int source, int target) => ((long)source << 32) | (uint)target;

        /// <summary>Adds a recipe; pairs already taken by an earlier recipe are left alone. False when no pair was new.</summary>
        public bool Add(TradeskillRecipe recipe)
        {
            if (recipe.Source.Length == 0 || recipe.Target.Length == 0 || recipe.Result.Length == 0)
            {
                Skipped++;
                return false;
            }

            bool added = false;
            foreach (int source in recipe.Source)
            foreach (int target in recipe.Target)
            {
                if (_byPair.TryAdd(Key(source, target), recipe))
                    added = true;
            }

            if (!added)
            {
                Skipped++;
                return false;
            }

            foreach (int source in recipe.Source.Distinct())
                _sourceProcesses[source] = _sourceProcesses.GetValueOrDefault(source) + 1;
            foreach (int target in recipe.Target.Distinct())
                _targetProcesses[target] = _targetProcesses.GetValueOrDefault(target) + 1;
            Count++;
            return true;
        }

        public int AddRange(IEnumerable<TradeskillRecipe> recipes)
        {
            int added = 0;
            foreach (TradeskillRecipe recipe in recipes)
                if (Add(recipe))
                    added++;
            return added;
        }

        public bool TryFind(int sourceLow, int sourceHigh, int targetLow, int targetHigh, out TradeskillRecipe recipe)
        {
            return _byPair.TryGetValue(Key(sourceHigh, targetHigh), out recipe!)
                || _byPair.TryGetValue(Key(sourceLow, targetHigh), out recipe!)
                || _byPair.TryGetValue(Key(sourceHigh, targetLow), out recipe!)
                || _byPair.TryGetValue(Key(sourceLow, targetLow), out recipe!);
        }

        public int SourceProcesses(int low, int high)
            => _sourceProcesses.GetValueOrDefault(high) + (low != high ? _sourceProcesses.GetValueOrDefault(low) : 0);

        public int TargetProcesses(int low, int high)
            => _targetProcesses.GetValueOrDefault(high) + (low != high ? _targetProcesses.GetValueOrDefault(low) : 0);

        /// <summary>
        /// Tradeskills.json, then its other Tradeskills.*.json overlays in name order, then Tradeskills.cellao.json.
        /// Each file is read on its own (the generic overlay merge would build a DOM of the 17 MB CellAO file).
        /// </summary>
        public static TradeskillRecipeBook Load(string root, Action<string> info)
        {
            var book = new TradeskillRecipeBook();
            var clock = Stopwatch.StartNew();
            long memoryBefore = GC.GetTotalMemory(forceFullCollection: false);
            string basePath = Path.Combine(root, BaseFileName);
            var files = new List<string>();
            if (File.Exists(basePath))
                files.Add(basePath);
            string[] overlays = GameDataOverlay.FindOverlays(basePath);
            files.AddRange(overlays.Where(path => !IsCellao(path)));
            files.AddRange(overlays.Where(IsCellao));

            foreach (string file in files)
            {
                List<TradeskillRecipe> recipes;
                using (FileStream stream = File.OpenRead(file))
                    recipes = JsonSerializer.Deserialize<List<TradeskillRecipe>>(stream, JsonOptions) ?? new();
                int added = book.AddRange(recipes);
                info(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "GameData tradeskill recipes +{0} of {1} from {2}", added, recipes.Count, file));
            }

            long memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
            info(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "GameData tradeskill recipes={0} pairs={1} skipped={2} files={3} in {4} ms (+{5:0.0} MB managed)",
                book.Count, book._byPair.Count, book.Skipped, files.Count, clock.ElapsedMilliseconds,
                (memoryAfter - memoryBefore) / (1024.0 * 1024.0)));
            return book;
        }

        static bool IsCellao(string path)
            => string.Equals(Path.GetFileName(path), CellaoFileName, StringComparison.OrdinalIgnoreCase);
    }
}
