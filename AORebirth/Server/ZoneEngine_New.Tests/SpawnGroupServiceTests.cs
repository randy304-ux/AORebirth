namespace ZoneEngine_New.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Guards the playfield-container DI shape that blew up live on 2026-11-10:
    /// Playfield.BuildServices registers <see cref="IGameData"/> (not GameDataStore),
    /// so SpawnGroupService must depend on IGameData. Playfield construction also
    /// resolves SpawnGroupService from the real container so a mismatch fails at
    /// startup instead of on every heartbeat.
    /// </summary>
    [TestClass]
    public sealed class SpawnGroupServiceTests
    {
        [TestMethod]
        public void ConstructorDependsOnIGameDataNotGameDataStore()
        {
            ConstructorInfo ctor = typeof(SpawnGroupService).GetConstructors().Single();
            Assert.IsTrue(ctor.GetParameters().Any(p => p.ParameterType == typeof(IGameData)));
            Assert.IsFalse(ctor.GetParameters().Any(p => p.ParameterType == typeof(GameDataStore)));
        }

        [TestMethod]
        public void PlayfieldStyleIGameDataRegistrationDoesNotExposeGameDataStore()
        {
            // Same registration Playfield.BuildServices uses: services.AddSingleton(_gameData)
            // where _gameData is typed IGameData.
            var services = new ServiceCollection();
            IGameData data = new StubGameData(HashItemCatalog.Parse("{}"), rootPath: Path.GetTempPath());
            services.AddSingleton(data);
            services.AddSingleton<IZoneLogger>(new StubLogger());
            using ServiceProvider sp = services.BuildServiceProvider();

            Assert.IsNotNull(sp.GetService<IGameData>());
            Assert.IsNull(sp.GetService<GameDataStore>());
            Assert.IsNotNull(data.SpawnGroups);
        }
    }
}
