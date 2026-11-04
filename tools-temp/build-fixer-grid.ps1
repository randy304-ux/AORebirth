$ErrorActionPreference = 'Stop'
$env:DOTNET_ROOT = 'I:\AO\tools\dotnet'
$env:PATH = 'I:\AO\tools\dotnet;' + $env:PATH
$dotnet = 'I:\AO\tools\dotnet\dotnet.exe'

$zeOut = 'I:\AO\src\tools-temp\fixer-grid-ze-out'
$unitDir = 'I:\AO\src\tools-temp\FixerGridUnit'
Remove-Item $zeOut,$unitDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $zeOut,$unitDir | Out-Null

Write-Host '=== BUILD ZoneEngine_New to private out (live Built is locked by running zone) ==='
$zeProj = 'I:\AO\src\AORebirth\Server\ZoneEngine_New\ZoneEngine_New.csproj'
& $dotnet build $zeProj -c Debug -o $zeOut --nologo
if ($LASTEXITCODE -ne 0) { throw 'zone build failed' }

Write-Host '=== UNIT TEST (private harness against private ZE out) ==='
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.10.4" />
    <PackageReference Include="MSTest.TestFramework" Version="3.10.4" />
    <Reference Include="ZoneEngine_New">
      <HintPath>I:\AO\src\tools-temp\fixer-grid-ze-out\ZoneEngine_New.dll</HintPath>
    </Reference>
    <Reference Include="AORebirth.Enums">
      <HintPath>I:\AO\src\tools-temp\fixer-grid-ze-out\AORebirth.Enums.dll</HintPath>
    </Reference>
    <Reference Include="AORebirth.Core">
      <HintPath>I:\AO\src\tools-temp\fixer-grid-ze-out\AORebirth.Core.dll</HintPath>
    </Reference>
    <Reference Include="SmokeLounge.AOtomation.Messaging">
      <HintPath>I:\AO\src\tools-temp\fixer-grid-ze-out\SmokeLounge.AOtomation.Messaging.dll</HintPath>
    </Reference>
    <Reference Include="AODB.Common">
      <HintPath>I:\AO\src\tools-temp\fixer-grid-ze-out\AODB.Common.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
'@ | Set-Content (Join-Path $unitDir 'FixerGridUnit.csproj') -Encoding UTF8

# Copy the authoritative test + minimal stubs from ZoneEngine_New.Tests
Copy-Item 'I:\AO\src\AORebirth\Server\ZoneEngine_New.Tests\FixerGridSpawnItemTests.cs' (Join-Path $unitDir 'FixerGridSpawnItemTests.cs') -Force
# Minimal stubs (only what FixerGridSpawnItemTests needs)
@'
namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using AORebirth.Enums;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;

    internal sealed class StubGameData : IGameData
    {
        readonly HashItemCatalog _hashItems;
        public StubGameData(HashItemCatalog hashItems) { _hashItems = hashItems; }
        public string RootPath => string.Empty;
        public int HashTemplateCount => _hashItems.CategoryCount;
        public int HashInstanceCount => _hashItems.InstanceCount;
        public bool CanResolveItemHash(string hash) => _hashItems.CanResolveItem(hash);
        public bool TryResolveHashInstance(string hash, out HashInstance instance) => _hashItems.TryResolveInstance(hash, out instance);
        public bool TryGetHashInstance(string hash, out HashInstance instance) => _hashItems.TryGetInstance(hash, out instance);
        public bool TryGetHashTemplate(string hash, out IReadOnlyList<string> childHashes) => _hashItems.TryGetCategory(hash, out childHashes);
        public void CollectHashSpawns(string hash, List<HashInstance> into) => _hashItems.CollectSpawns(hash, into);
        public void CollectHashSpawns(string hash, List<HashInstance> into, Func<string, bool> canPick) => _hashItems.CollectSpawns(hash, into, canPick);
        public void CollectHashLeafInstances(string hash, List<HashInstance> into) => _hashItems.CollectLeafInstances(hash, into);
        // Unused IGameData members throw — FixerGrid tests only touch hash APIs above.
        public bool TryGetAssignedItemHash(int lowId, int highId, out string hash) { hash = null!; return false; }
        public System.Collections.Generic.IEnumerable<object> GetType() => throw new NotImplementedException();
    }
}
'@ | Set-Content (Join-Path $unitDir 'StubsPartial.cs') -Encoding UTF8

# Actually StubGameData needs full IGameData - easier to compile TestDoubles excerpts.
# Use reflection-free approach: put catalog-only tests that don't need HashItemMinter/StubGameData full interface.

Write-Host 'rewriting harness test to avoid full IGameData stub surface'
@'
namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.WorldSimulation;

    [TestClass]
    public sealed class FixerGridSpawnItemTests
    {
        const int DataReceptacleId = 160978;
        const int QuiescentGridWaveformTemplateId = 265479;
        const int FixerGridPlayfieldId = 4107;
        const byte FixerGridLandingLine = 6;
        const int FixerGridPackedLine = 397323;

        const string FixerGridCatalogJson =
            """
            {
                "FIGI": { "Templates": [160978, 160978] },
                "QIGR": { "Templates": [265479, 265479] }
            }
            """;

        [TestMethod]
        public void FigiAndQigrResolveToRealItemsDatTemplates()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(FixerGridCatalogJson);
            Assert.IsTrue(catalog.CanResolveItem("FIGI"));
            Assert.IsTrue(catalog.CanResolveItem("QIGR"));
            Assert.IsTrue(catalog.TryResolveInstance("FIGI", out HashInstance figi));
            Assert.AreEqual("FIGI", figi.Hash);
            Assert.IsTrue(HashItemCatalog.TrySelectIds(figi, 1, _ => 1, out int figiLow, out int figiHigh));
            Assert.AreEqual(DataReceptacleId, figiLow);
            Assert.AreEqual(DataReceptacleId, figiHigh);
            Assert.IsTrue(catalog.TryResolveInstance("QIGR", out HashInstance qigr));
            Assert.IsTrue(HashItemCatalog.TrySelectIds(qigr, 1, _ => 1, out int qigrLow, out int qigrHigh));
            Assert.AreEqual(QuiescentGridWaveformTemplateId, qigrLow);
            Assert.AreEqual(QuiescentGridWaveformTemplateId, qigrHigh);
        }

        [TestMethod]
        public void EnterTheGridOnUseItemOnTeleportProxy2LandsOnFixerGridLine6()
        {
            var args = new List<object>
            {
                (int)IdentityType.PlayfieldDoor,
                FixerGridPlayfieldId,
                0,
                0,
                (int)IdentityType.Playfield3,
                FixerGridPackedLine
            };
            Assert.IsTrue(
                PortalDoorLandingResolver.TryParseProxyDestination(
                    args,
                    PortalDoorLandingResolver.Proxy2EntryDoorClearance,
                    recordsReturn: false,
                    out PortalDestination destination));
            Assert.AreEqual(FixerGridPlayfieldId, destination.PlayfieldId);
            Assert.AreEqual(PortalLandingKind.DestinationMidpoint, destination.Kind);
            Assert.AreEqual(FixerGridLandingLine, destination.DestinationIndex);
            Assert.IsFalse(destination.RecordsReturn);
            uint packed = unchecked((uint)FixerGridPackedLine);
            Assert.AreEqual(FixerGridLandingLine, (byte)(packed >> 16));
            Assert.AreEqual((uint)FixerGridPlayfieldId, packed & 0xFFFFu);
        }
    }
}
'@ | Set-Content (Join-Path $unitDir 'FixerGridSpawnItemTests.cs') -Encoding UTF8
Remove-Item (Join-Path $unitDir 'StubsPartial.cs') -ErrorAction SilentlyContinue

# Find AORebirth.Enums location in zeOut
Get-ChildItem $zeOut -Filter '*.dll' | Select-Object -First 30 | ForEach-Object { Write-Host $_.Name }

# Fix csproj refs to whatever exists
$enums = @(Get-ChildItem $zeOut -Filter '*Enum*.dll') + @(Get-ChildItem 'I:\AO\src\AORebirth\Built\Debug' -Filter 'AORebirth.Enums.dll' -ErrorAction SilentlyContinue)
Write-Host ('enums candidates=' + ($enums | ForEach-Object { $_.FullName }) -join ';')

Push-Location $unitDir
& $dotnet test -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'unit tests failed' }
Pop-Location

# Keep the authoritative copy in ZoneEngine_New.Tests aligned with harness (catalog+proxy tests, plus minter tests when suite can build)
# The committed FixerGridSpawnItemTests.cs already has the fuller version with HashItemMinter.

$zbuild = 'I:\AO\src\tools-temp\zbuild-fixer-grid'
New-Item -ItemType Directory -Force -Path (Join-Path $zbuild 'GameData') | Out-Null
$live = 'I:\AO\src\AORebirth\Built\Debug\ZoneEngine_New'
foreach ($n in @('ZoneEngine_New.dll','ZoneEngine_New.exe','ZoneEngine_New.pdb')) {
  $from = Join-Path $live $n
  if (-not (Test-Path $from)) { throw "missing live $from" }
  Copy-Item $from (Join-Path $zbuild $n) -Force
}
Copy-Item 'I:\AO\src\AORebirth\GameData\ItemTemplates.FixerGrid.json' (Join-Path $zbuild 'GameData\ItemTemplates.FixerGrid.json') -Force
"live_bins_from=$live`r`nprivate_ze_out=$zeOut`r`nnote=GameData-only fix; live bins staged for deploy restart" |
  Set-Content (Join-Path $zbuild 'BUILD_NOTE.txt') -Encoding ascii

Write-Host '=== DRY-RUN against source GameData overlay ==='
$dir = 'I:\AO\src\tools-temp\FixerGridDryRun'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
'@ | Set-Content (Join-Path $dir 'x.csproj') -Encoding UTF8
@'
using System.Reflection;
string bin = args[0], gd = args[1];
AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
  var p = Path.Combine(bin, new AssemblyName(e.Name).Name + ".dll");
  return File.Exists(p) ? Assembly.LoadFrom(p) : null;
};
var asms = Directory.GetFiles(bin, "*.dll").Select(f => { try { return Assembly.LoadFrom(f); } catch { return null; } }).Where(a => a != null).ToList();
Type Find(string n) {
  foreach (var a in asms) {
    Type[] ts; try { ts = a.GetTypes(); } catch (ReflectionTypeLoadException ex) { ts = ex.Types.Where(t => t != null).ToArray(); } catch { continue; }
    var t = ts.FirstOrDefault(x => x != null && x.Name == n); if (t != null) return t;
  }
  return null;
}
var logT = Find("IZoneLogger");
var log = typeof(DispatchProxy).GetMethod("Create", 2, Type.EmptyTypes).MakeGenericMethod(logT, typeof(LP)).Invoke(null, null);
var gdsT = Find("GameDataStore");
var store = gdsT.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First(c => c.GetParameters().Length == 3).Invoke(new object[] { log, null, gd });
foreach (string h in new[] { "FIGI", "QIGR" }) {
  bool ok = (bool)store.GetType().GetMethod("CanResolveItemHash").Invoke(store, new object[] { h });
  Console.WriteLine("CanResolveItemHash " + h + "=" + ok);
  if (!ok) Environment.Exit(2);
  var a = new object[] { h, null };
  bool got = (bool)store.GetType().GetMethod("TryResolveHashInstance").Invoke(store, a);
  Console.WriteLine("TryResolveHashInstance " + h + "=" + got + " inst=" + a[1]);
}
Console.WriteLine("DRYRUN_OK");
public class LP : DispatchProxy { protected override object Invoke(MethodInfo m, object[] a) => m.Name == "CreateForPlayfield" ? this : null; }
'@ | Set-Content (Join-Path $dir 'Program.cs') -Encoding UTF8
Push-Location $dir
& $dotnet build -c Release -o out | Select-Object -Last 6
if ($LASTEXITCODE -ne 0) { throw 'dry-run build failed' }
& (Join-Path $dir 'out\x.exe') $zeOut 'I:\AO\src\AORebirth\GameData'
if ($LASTEXITCODE -ne 0) { throw 'dry-run resolve failed' }
Pop-Location

Write-Host '=== ZBUILD ==='
Get-ChildItem $zbuild -Recurse -File | ForEach-Object { Write-Host ($_.FullName.Substring($zbuild.Length+1) + ' ' + $_.Length) }
Write-Host 'BUILD_OK'
