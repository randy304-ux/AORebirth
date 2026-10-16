# ddogs/tools/Build-AreteContent.ps1 - Arete Landing (6553) world content and quest-item loot. Re-runnable.
#  1. Spawns the Arete quest givers in GameData/PlayfieldContent/6553/Npcs.json (Keys "ddogs:arete:*") from the retired
#     capture-backed AreteLandingSpawn.cs (git ecefa673^), plus Lolly the Reet (look from the retired LoreleiOasisMobRuntime).
#  2. Adds mob templates for the Arete client spawn hashes the quests use (NpcTemplates.json), with names, models and levels
#     from the retired Arete mob runtimes (JunkyardCleaningRobot, AlexAreaMob, LoreleiOasisMob). Stats follow CRJU's skeleton.
#  3. Adds quest-item hashes (ItemTemplates.json, ids from itemnames) and their drops on the mobs that carry them.
# Run before Build-NewbieQuests.ps1 (its Knubot trades and {spawn} tags need these item hashes).
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$GameData = [IO.Path]::GetFullPath($GameData)
$u = [Text.UTF8Encoding]::new($false)
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))

# ---------- 1. quest-item hashes ----------
$items = [ordered]@{
  QPBS = @(259957,259957); QHPT = @(259955,259955); QBAC = @(156020,156021); QDLA = @(295618,295618); QUIC = @(296572,296572)
  QAAF = @(248306,248306); QPCR = @(297367,297367); QPWS = @(248307,248307); QSTL = @(248314,248314); QRRI = @(248333,248333)
}
$ipath = Join-Path $GameData 'ItemTemplates.json'
$lines = [IO.File]::ReadAllLines($ipath, $u) | Where-Object { $_ -notmatch ('^\s*"(' + ($items.Keys -join '|') + ')":') }
$add = foreach ($k in $items.Keys) { '    "{0}": {{"Templates": [{1},{2}]}},' -f $k, $items[$k][0], $items[$k][1] }
if ($lines[0].Trim() -ne '{') { throw 'ItemTemplates.json: unexpected first line' }
[IO.File]::WriteAllLines($ipath, [string[]](@($lines[0]) + $add + $lines[1..($lines.Count - 1)]), $u)

# ---------- 2. mob templates + quest drops ----------
$npath = Join-Path $GameData 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
$skel = $nt.CRJU.Templates | Where-Object { $_.Level -eq 5 } | Select-Object -First 1
$curve = @{ 1 = 15; 2 = 24; 3 = 35; 4 = 60; 5 = 58; 6 = 69; 7 = 80 }
# hash, name, min, max, MonsterData, scale, NPCFamily (source: retired Arete runtimes; hash<->name by spawn position/level)
$mobs = @(
  @('MLCE','Malfunctioning Cleaning Robot',1,1,297023,100,1019),
  @('JUCO','Cleaning Robot',1,1,297023,120,1019),
  @('JNME','Garbage Flea',2,2,17657,100,0),   # NPCFamily 0: 25 rendered see-through on RC-LT (2026-10-16, guess)
  @('32DO','32-V Docker',3,3,17649,110,1019),
  @('01V1','Waste Collector',5,5,17714,75,0),   # NPCFamily 0: 1019 rendered see-through (guess)
  @('WSSC','Rollerrat',5,6,17687,125,0),
  @('DERT','Desert Reet',5,6,30365,93,0),
  @('GRDS','Greedy Desert Reet',7,7,30365,130,0)
)
foreach ($m in $mobs) {
  $bands = @()
  for ($l = $m[2]; $l -le $m[3]; $l++) {
    $st = [ordered]@{}; foreach ($p in $skel.Stats.PSObject.Properties) { $st[$p.Name] = $p.Value }
    $st['1'] = $curve[$l]; $st['27'] = $curve[$l]; $st['54'] = $l; $st['359'] = $m[4]; $st['360'] = $m[5]; $st['455'] = $m[6]
    $st['61'] = 10 * $l; $st['285'] = [Math]::Max(2, $l); $st['286'] = [Math]::Max(1, [int][Math]::Floor($l / 2))
    $bands += [ordered]@{ Name = $m[1]; Level = $l; Stats = $st; LootTable = @($skel.LootTable) }
  }
  $nt | Add-Member -NotePropertyName $m[0] -NotePropertyValue ([ordered]@{ Templates = $bands }) -Force
}
# quest drops: mob hash -> @(item hash, chance %)
$drops = @{ PDST = @('QPBS',50); ATHL = @('QHPT',100); CADR = @('QPWS',35); TPSA = @('QSTL',35); SORL = @('QRRI',35); LLER = @('QRRI',35); JUCO = @('QBAC',15) }
foreach ($h in $drops.Keys) {
  $t = $nt.$h; if (-not $t) { throw "no template $h" }
  foreach ($b in $t.Templates) {
    $keep = @($b.LootTable | Where-Object { $_ -and -not $items.Contains($_.Hash) })
    $b.LootTable = @($keep) + @([pscustomobject]@{ Hash = $drops[$h][0]; Repeats = 1; Chance = $drops[$h][1]; LevelMod = 0 })
  }
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 8) + "`n", $u)

# ---------- 3. Arete quest givers ----------
$src = (& git -C $repo show 'ecefa673^:AORebirth/Server/ZoneEngine/Core/Playfields/AreteLandingSpawn.cs') -join "`n"
if (-not $src) { throw 'cannot read retired AreteLandingSpawn.cs from git' }
$want = [ordered]@{ 'Rex Larsson' = 0x78E0FC63; 'Marcus Stone' = 0x78E0FC62; 'Flint Novak' = 0x78E0FC64; 'Alex Gibbs' = 0; 'ICC Immigration Officer Bill' = 0
  'Desmond Calitri' = 0; 'Antonio Stacklund' = 0; 'Dr. Mason' = 0; 'Patrick Sun' = 0; 'Shady Guy' = 0; 'Shipping Manifest Terminal' = 0
  'Vernon Godfray' = 0; 'Vaughn Hammond' = 0 }
function F($x, $k) { $m = [regex]::Match($x, '\b' + $k + ' = (-?[\d.]+)f?'); if (-not $m.Success) { throw "missing $k" }; [double]$m.Groups[1].Value }
$new = @()
foreach ($b in [regex]::Matches($src, 'new AreteNpc\s*\{(.*?)\n\s*\},', 'Singleline')) {
  $x = $b.Groups[1].Value; $name = [regex]::Match($x, 'Name = "([^"]+)"').Groups[1].Value
  if (-not $want.Contains($name)) { continue }
  $ci = [regex]::Match($x, 'CaptureInstance = unchecked\(\(int\)0x([0-9A-F]+)\)')
  $inst = $(if ($ci.Success) { [Convert]::ToInt32($ci.Groups[1].Value, 16) } else { $want[$name] })
  if (-not $inst) { throw "no instance for $name" }
  $hp = [int](F $x 'Health')
  $stats = [ordered]@{ '0' = [int](F $x 'CharacterFlags'); '1' = $hp; '4' = [int](F $x 'Breed'); '27' = $hp; '33' = [int](F $x 'Side'); '47' = [int](F $x 'Fatness')
    '54' = [int](F $x 'Level'); '59' = [int](F $x 'Gender'); '60' = 0; '64' = [int](F $x 'HeadMesh'); '89' = [int](F $x 'Race'); '156' = [int](F $x 'RunSpeed')
    '173' = [int](F $x 'MovementMode'); '174' = [int](F $x 'MovementMode'); '359' = [int](F $x 'MonsterData'); '360' = [int](F $x 'Scale'); '368' = 0; '389' = 0
    '455' = [int](F $x 'NpcFamily'); '466' = 0; '660' = 0; '673' = [int](F $x 'VisualFlags') }
  $tex = @(); foreach ($t in [regex]::Matches(([regex]::Match($x, 'Textures = (.*?)\},\s*\n', 'Singleline').Value), 'new\[\] \{ (\d+), (\d+) \}')) { $tex += [ordered]@{ Place = [int]$t.Groups[1].Value; Id = [int]$t.Groups[2].Value } }
  $mesh = @(); foreach ($t in [regex]::Matches(([regex]::Match($x, 'Meshes = ([^\n]*)').Value), 'new\[\] \{ (\d+), (\d+), (\d+), (\d+) \}')) { $mesh += [ordered]@{ Position = [int]$t.Groups[1].Value; OverrideTextureId = [int]$t.Groups[3].Value; Id = [int]$t.Groups[2].Value; Layer = [int]$t.Groups[4].Value } }
  $new += [ordered]@{ Key = ('ddogs:arete:{0:X8}' -f $inst); Provenance = 'AORebirth/Server/ZoneEngine/Core/Playfields/AreteLandingSpawn.cs (ecefa673^); ddogs/tools/Build-AreteContent.ps1'
    ContentNpcIdentity = ('SimpleChar:{0:X8}' -f $inst); PlayfieldId = 6553; InstanceId = $inst; Name = $name
    Position = @((F $x 'X'), (F $x 'Y'), (F $x 'Z')); Rotation = @((F $x 'Hx'), (F $x 'Hy'), (F $x 'Hz'), (F $x 'Hw'))
    Stats = $stats; Textures = $tex; Meshes = $mesh; Passive = $true; Attackable = $false; TickLiving = $true; Presentation = [ordered]@{} }
  $want.Remove($name)
}
if ($want.Count) { throw ('not found in AreteLandingSpawn.cs: ' + ($want.Keys -join ', ')) }
# Lolly the Reet: LoreleiOasisMobRuntime fixed instance 0x7985CAEC, Desert Reet model 30365, (3360.2, 3.5, 620.3).
$new += [ordered]@{ Key = 'ddogs:arete:7985CAEC'; Provenance = 'LoreleiOasisMobRuntime.cs (ecefa673^) Lolly; ddogs/tools/Build-AreteContent.ps1'
  ContentNpcIdentity = 'SimpleChar:7985CAEC'; PlayfieldId = 6553; InstanceId = 0x7985CAEC; Name = 'Lolly the Reet'
  Position = @(3360.186, 3.527, 620.348); Rotation = @(0, 0, 0, 1)
  Stats = [ordered]@{ '0' = 277352961; '1' = 69; '4' = 6; '27' = 69; '33' = 0; '47' = 1; '54' = 6; '59' = 1; '60' = 0; '64' = 0; '89' = 1; '156' = 20; '173' = 3; '174' = 3; '359' = 30365; '360' = 80; '368' = 0; '389' = 0; '455' = 0; '466' = 0; '660' = 0; '673' = 31 }
  Textures = @(); Meshes = @(); Passive = $true; Attackable = $false; TickLiving = $true; Presentation = [ordered]@{} }

$ppath = Join-Path $GameData 'PlayfieldContent\6553\Npcs.json'
$pf = [IO.File]::ReadAllText($ppath, $u) | ConvertFrom-Json
$keep = @($pf.Npcs | ForEach-Object { $_ } | Where-Object { $_.Key -notlike 'ddogs:arete:*' })
$ids = @{}; foreach ($n in $keep) { $ids[[int]$n.InstanceId] = $n.Name }
foreach ($n in $new) { if ($ids.ContainsKey([int]$n.InstanceId)) { throw "instance clash $($n.Name) vs $($ids[[int]$n.InstanceId])" } }
$pf.Npcs = @($keep) + $new
[IO.File]::WriteAllText($ppath, ($pf | ConvertTo-Json -Depth 10), $u)
"items=$($items.Count) mobs=$($mobs.Count) drops=$($drops.Count) arete npcs added=$($new.Count) total=$($pf.Npcs.Count)"