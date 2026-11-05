namespace ZoneEngine_New.Core.Configuration
{
    using System;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Config/Gameplay.json: tunable gameplay rules, loaded once at startup. Resolved beside the GameData tree first
    /// (a deployment's own Config folder), else beside the process, where the repo copy is placed by the build.
    /// </summary>
    public sealed class GameplaySettings
    {
        public const int CurrentVersion = 1;

        public const string ConfigFileName = "Gameplay.json";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public int SchemaVersion { get; init; } = CurrentVersion;

        /// <summary>Seconds a successful /stuck locks the Stability skill, and so how long until the next one.</summary>
        public int StuckCooldownSeconds { get; init; } = 300;

        /// <summary>Where /stuck sends a player.</summary>
        public GameplayLocation? StuckLocation { get; init; }

        /// <summary>Kill XP rules.</summary>
        public GameplayXp Xp { get; init; } = new();

        /// <summary>How district-spawned NPCs wander around their spawn point.</summary>
        public GameplayWander Wander { get; init; } = new();

        /// <summary>Optional mishroll (preferred solo RK terminal reward) rules. Default off.</summary>
        public GameplayMishroll Mishroll { get; init; } = new();

        public static string ResolvePath(string gameDataRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            string? parent = Directory.GetParent(Path.GetFullPath(gameDataRoot))?.FullName;
            if (!string.IsNullOrEmpty(parent))
            {
                string besideGameData = Path.Combine(parent, "Config", ConfigFileName);
                if (File.Exists(besideGameData))
                    return besideGameData;
            }

            return Path.Combine(AppContext.BaseDirectory, "Config", ConfigFileName);
        }

        public static GameplaySettings Load(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (!File.Exists(path))
                throw new FileNotFoundException("Gameplay config was not found.", path);

            GameplaySettings? settings;
            try
            {
                settings = JsonSerializer.Deserialize<GameplaySettings>(File.ReadAllText(path), JsonOptions);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("Gameplay config is invalid: " + path, exception);
            }

            if (settings == null)
                throw new InvalidDataException("Gameplay config is empty: " + path);

            settings.Validate(path);
            return settings;
        }

        void Validate(string path)
        {
            if (SchemaVersion != CurrentVersion)
                throw new InvalidDataException(path + ": schemaVersion must be " + CurrentVersion + " but was " + SchemaVersion + ".");
            if (StuckCooldownSeconds < 0)
                throw new InvalidDataException(path + ": stuckCooldownSeconds must not be negative.");
            if (StuckLocation is not GameplayLocation stuck || !stuck.IsValid)
                throw new InvalidDataException(path + ": stuckLocation needs a positive playfield and finite x, y, z.");
            if (Xp == null || !double.IsFinite(Xp.KillPercentPerLevelDifference) || Xp.KillPercentPerLevelDifference < 0)
                throw new InvalidDataException(path + ": xp.killPercentPerLevelDifference must be a finite, non-negative number.");
            // A wanderer more than the leash range from its spawn point would be sent home mid-stroll.
            if (Wander == null || !float.IsFinite(Wander.Radius) || Wander.Radius < 0
                || Wander.Radius >= Ai.NpcAiRules.MaxLeashRange)
                throw new InvalidDataException(path + ": wander.radius must be 0 (off) up to below the "
                    + Ai.NpcAiRules.MaxLeashRange + " m leash range.");
            if (!double.IsFinite(Wander.MinPauseSeconds) || !double.IsFinite(Wander.MaxPauseSeconds)
                || Wander.MinPauseSeconds < 0 || Wander.MaxPauseSeconds < Wander.MinPauseSeconds)
                throw new InvalidDataException(path + ": wander pause seconds must be non-negative with min <= max.");
            if (Mishroll == null)
                throw new InvalidDataException(path + ": mishroll section is required (use defaults / enabled false).");
            Mishroll.Validate(path);
        }
    }

    /// <summary>The mishroll section of Gameplay.json (preferred solo RK terminal reward pin + future accept fee).</summary>
    public sealed class GameplayMishroll
    {
        /// <summary>Master switch. Default false — command and later gameplay hooks stay inert.</summary>
        public bool Enabled { get; init; }

        /// <summary>Accept-time targeting fee constants (not charged until a later mishroll stage).</summary>
        public GameplayMishrollTargetingFee TargetingFee { get; init; } = new();

        internal void Validate(string path)
        {
            if (TargetingFee == null)
                throw new InvalidDataException(path + ": mishroll.targetingFee is required.");
            TargetingFee.Validate(path);
        }
    }

    /// <summary>
    /// QL-scaled mishroll targeting fee: 0 through <see cref="FreeThroughQuality"/>, linear to
    /// <see cref="FullFeeCredits"/> at <see cref="FullFeeQuality"/>, then capped; B&amp;E discount
    /// <see cref="BreakAndEntryDiscountPercentPerTen"/>% per 10 effective B&amp;E, capped at
    /// <see cref="BreakAndEntryDiscountCapPercent"/>%; round half up once. Curve fraction scales the
    /// handoff curve (0.5 = 50%).
    /// </summary>
    public sealed class GameplayMishrollTargetingFee
    {
        public int FreeThroughQuality { get; init; } = 25;

        public int FullFeeQuality { get; init; } = 200;

        public int FullFeeCredits { get; init; } = 10000;

        /// <summary>Multiplier on the handoff curve (0.5 = half). Must be finite and &gt; 0.</summary>
        public double FeeCurveFraction { get; init; } = 0.5;

        public int BreakAndEntryDiscountPercentPerTen { get; init; } = 1;

        public int BreakAndEntryDiscountCapPercent { get; init; } = 50;

        internal void Validate(string path)
        {
            if (FreeThroughQuality < 0)
                throw new InvalidDataException(path + ": mishroll.targetingFee.freeThroughQuality must not be negative.");
            if (FullFeeQuality <= FreeThroughQuality)
                throw new InvalidDataException(path + ": mishroll.targetingFee.fullFeeQuality must be greater than freeThroughQuality.");
            if (FullFeeCredits < 0)
                throw new InvalidDataException(path + ": mishroll.targetingFee.fullFeeCredits must not be negative.");
            if (!double.IsFinite(FeeCurveFraction) || FeeCurveFraction <= 0 || FeeCurveFraction > 1)
                throw new InvalidDataException(path + ": mishroll.targetingFee.feeCurveFraction must be finite in (0, 1].");
            if (BreakAndEntryDiscountPercentPerTen < 0)
                throw new InvalidDataException(path + ": mishroll.targetingFee.breakAndEntryDiscountPercentPerTen must not be negative.");
            if (BreakAndEntryDiscountCapPercent < 0 || BreakAndEntryDiscountCapPercent > 100)
                throw new InvalidDataException(path + ": mishroll.targetingFee.breakAndEntryDiscountCapPercent must be 0..100.");
        }
    }

    /// <summary>The wander section of Gameplay.json.</summary>
    public sealed class GameplayWander
    {
        /// <summary>Meters around the district spawn point an NPC is scattered and wanders within. 0 turns wandering off.</summary>
        public float Radius { get; init; } = 50f;

        /// <summary>Shortest time an NPC stands still between strolls.</summary>
        public double MinPauseSeconds { get; init; } = 5;

        /// <summary>Longest time an NPC stands still between strolls.</summary>
        public double MaxPauseSeconds { get; init; } = 20;
    }

    /// <summary>The xp section of Gameplay.json.</summary>
    public sealed class GameplayXp
    {
        /// <summary>
        /// Percent of the victim's KillAward added per level the victim is above the recipient (removed per level below).
        /// </summary>
        public double KillPercentPerLevelDifference { get; init; } = 2.5;
    }

    /// <summary>A playfield and position in Gameplay.json.</summary>
    public sealed class GameplayLocation
    {
        public int Playfield { get; init; }

        public float X { get; init; }

        public float Y { get; init; }

        public float Z { get; init; }

        public bool IsValid => Playfield > 0 && float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
    }
}
