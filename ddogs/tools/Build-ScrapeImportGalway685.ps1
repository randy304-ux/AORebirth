# ddogs/tools/Build-ScrapeImportGalway685.ps1 - Galway County pf 685 outdoor scrape pilot.
# Plan = ddogs/tools/scrape_import_galway685_20261106.json (from plan-galway685.js; reviewed missing.csv only).
#  templates: NpcTemplates.Galway685.json overlay (new Pxxx hashes; donor+scraped-stat method;
#             no outfit/glove/texture-slot from pre-v6.0 scrapes; friendly => NoCombat; roam => Wander else NoMove for non-monsters).
#  spawns:    PlayfieldContent/685/SpawnsAdd.galway685.json (SpawnsAdd.<tag>.json; ZoneEngine loads these).
# Rerunnable: Source 'scrape-galway685-20261106'. Does not modify Surfaces / client Spawns.json / existing templates.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false); $gd = [IO.Path]::GetFullPath($GameData); $tag = 'scrape-galway685-20261106'
function RJ($p) { [IO.File]::ReadAllText($p, $u) | ConvertFrom-Json }
$plan = RJ (Join-Path $PSScriptRoot 'scrape_import_galway685_20261106.json')
if ($plan.source -ne $tag) { throw "plan source $($plan.source)" }

# --- donor pool from base + overlays (except our own output) ---
$nt = RJ (Join-Path $gd 'NpcTemplates.json')
foreach ($f in [IO.Directory]::GetFiles($gd, 'NpcTemplates.*.json')) {
  if ([IO.Path]::GetFileName($f) -eq 'NpcTemplates.Galway685.json') { continue }
  $ov0 = RJ $f
  foreach ($p in $ov0.PSObject.Properties) {
    if (-not $nt.($p.Name)) { $nt | Add-Member -Force NoteProperty $p.Name $p.Value }
  }
}
$pool = New-Object System.Collections.ArrayList
foreach ($p in $nt.PSObject.Properties) {
  if ($p.Value.ContentProvenance.Guessed) { continue }
  foreach ($b in @($p.Value.Templates)) {
    if ($b -and $b.Stats -and $b.Stats.'359') { [void]$pool.Add($b) }
  }
}
if ($pool.Count -lt 1) { throw 'no donor pool' }

$ov = [ordered]@{}; $made = 0
foreach ($t in @($plan.templates)) {
  if ($nt.($t.hash)) { throw "hash $($t.hash) already in NpcTemplates" }
  $bands = foreach ($b in @($t.bands)) {
    $L = [int]$b.Level
    $d = $pool | Sort-Object { [math]::Abs([int]$_.Level - $L) } | Select-Object -First 1
    $c = $d | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $c.Name = $b.Name; $c.Level = $L
    $set = [ordered]@{
      '54' = $L; '33' = [int]$b.side; '359' = [int]$b.md; '360' = [int]$b.sc; '455' = [int]$b.fam
    }
    if ([int]$b.hp -gt 0) { $set['1'] = [int]$b.hp; $set['27'] = [int]$b.hp }
    if ([int]$b.breed -gt 0) { $set['4'] = [int]$b.breed }
    if ([int]$b.head -gt 0) { $set['64'] = [int]$b.head }
    if ([int]$b.mesh -gt 0) { $set['12'] = [int]$b.mesh }
    if ([int]$b.tex -gt 0) { $set['344'] = [int]$b.tex }
    $set['0'] = 268964353
    foreach ($k in $set.Keys) { $c.Stats | Add-Member -Force NoteProperty $k $set[$k] }
    $c | Add-Member -Force NoteProperty LootTable @()
    foreach ($k in @('Equipment', 'ContentProvenance', 'Textures', 'Meshes')) {
      if ($c.PSObject.Properties[$k]) { $c.PSObject.Properties.Remove($k) }
    }
    $c | Add-Member -Force NoteProperty HasHeadMesh ([int]$b.head -gt 0)
    $feat = @($c.Features | Where-Object { $_ -and $_ -ne 'NoCombat' -and $_ -ne 'NoMove' -and $_ -ne 'Wander' })
    $isMonster = ([int]$b.side -eq 3)
    if (-not $isMonster) { $feat = @($feat) + 'NoCombat' }
    if ($t.wander) { $feat = @($feat) + 'Wander' }
    elseif (-not $isMonster) { $feat = @($feat) + 'NoMove' }
    $c | Add-Member -Force NoteProperty Features @($feat | Sort-Object -Unique)
    $c
  }
  $note = "live scrape Galway 685 ($($t.src))"
  if ($t.wander) { $note += '; Wander (multi-pos / roam)' }
  $note += '; outfit/gloves omitted (pre-v6.0 untrusted)'
  $ov[$t.hash] = [pscustomobject]@{
    Templates = @($bands)
    ContentProvenance = [pscustomobject]@{ Guessed = $false; Source = $tag; Note = $note }
  }
  $made++
}
[IO.File]::WriteAllText((Join-Path $gd 'NpcTemplates.Galway685.json'), (([pscustomobject]$ov) | ConvertTo-Json -Depth 10) + "`n", $u)

# --- SpawnsAdd.<tag>.json ---
$nsp = 0; $reuse = 0
foreach ($pp in $plan.spawnsByPf.PSObject.Properties) {
  $pf = [int]$pp.Name
  $dir = Join-Path $gd "PlayfieldContent\$pf"
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory $dir | Out-Null }
  $pts = foreach ($s in @($pp.Value)) {
    if ($s.reuse) { $reuse++ }
    [ordered]@{
      districtIndex = 0; hash = 0; hashText = $s.hash; manifestHash = 0
      minLevel = [int]$s.lo; maxLevel = [int]$s.hi; respawnChance = 100; flags = 1; respawnTime = 600
      version7Extra = 0; hasOptionalFlagBlock = $true; nativeFlags = 0; moreFlags = 0
      assistanceRadius = 0; unknownOptionalU8 = 0
      angle = [int]$s.angle; angleW = 1
      position = @([double]$s.x, [double]$s.y, [double]$s.z); radius = 1
      additionalPoints = @(); extensions = $null
    }
  }
  $doc = [ordered]@{ schemaVersion = 1; recordType = 1000014; playfieldId = $pf; source = $tag; spawns = @($pts) }
  [IO.File]::WriteAllText((Join-Path $dir 'SpawnsAdd.galway685.json'), ($doc | ConvertTo-Json -Depth 6) + "`n", $u)
  $nsp += @($pts).Count
}
"templates=$made spawns=$nsp reuseExistingHash=$reuse collisions=$(@($plan.collisions).Count) skipped=$(@($plan.skipped).Count) wrongFixed=$($plan.wrongFixed)"
