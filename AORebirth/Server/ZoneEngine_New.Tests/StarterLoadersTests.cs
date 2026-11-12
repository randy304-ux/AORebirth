namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using AORebirth.Core.GameData;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Knubot;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Quests;

    [TestClass]
    public sealed class StarterLoadersTests
    {
        sealed class CapturingLogger : IZoneLogger
        {
            public readonly List<string> Lines = new();
            public void Debug(string message) { }
            public void Info(string message) { }
            public void Warn(string message) => Lines.Add("WARN " + message);
            public void Error(string message) => Lines.Add("ERROR " + message);
            public void Error(Exception exception, string message) => Lines.Add("ERROR " + message + " " + exception.Message);
            public IZoneLogger CreateForPlayfield(int playfieldId) => this;
        }

        static string ResolveGameDataRoot()
        {
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GameData"));
            if (!File.Exists(Path.Combine(root, "NpcTemplates.Pets.json")))
                root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "GameData"));
            return root;
        }

        [TestMethod]
        public void QuestAndKnubotCatalogsLoadWithoutErrors()
        {
            string root = ResolveGameDataRoot();
            Assert.IsTrue(File.Exists(Path.Combine(root, "NpcTemplates.Pets.json")), "NpcTemplates.Pets.json missing at " + root);
            Assert.IsTrue(File.Exists(Path.Combine(root, "Quests.json")), "Quests.json missing at " + root);
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Knubot")), "Knubot folder missing at " + root);

            string? previous = Environment.GetEnvironmentVariable(GameDataPaths.EnvironmentVariableName);
            try
            {
                Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, root);
                var logger = new CapturingLogger();
                var data = new GameDataStore(logger);
                var items = new ItemTemplateCatalog(data, logger);
                var quests = new QuestCatalog(data, logger);
                var knubot = new KnubotCatalog(data, quests, items, logger);

                string[] errors = logger.Lines.Where(l => l.StartsWith("ERROR", StringComparison.Ordinal)).ToArray();
                Assert.AreEqual(0, errors.Length, string.Join(" | ", errors));
                Assert.IsTrue(data.TryResolveMobTemplate("PT50", 1, out MobTemplate anger));
                Assert.AreEqual("Anger Manifestation", anger.Name);
                PlayfieldNpcContentCatalog nasc = PlayfieldNpcContentCatalog.Load(root, 4313);
                Assert.AreEqual(4313, nasc.PlayfieldId);
                Assert.IsTrue(nasc.Npcs.Any(n => n.Key == "ddogs:merc:4313:terminal"));
            }
            finally
            {
                Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, previous);
            }
        }
    }
}