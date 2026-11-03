# ddogs/tools/Build-ScrapeImportCB.ps1 - Cyborg Barracks pf 1833 from Whozewhatzit live scrape 2026-11-03.
# Same pipeline as Build-ScrapeImportCities/Pinks/Nasc:
#  templates: fill pink client Spawns.json hashpoints (THVI/TEYN/GEXL/PRIN/...) from scrape
#             stats cloned from nearest-level non-guessed donor, then overwritten with scraped
#             name/level/hp/side/breed/MonsterData/scale/mesh/head/texture/NPCFamily.
#  spawns:    none — Playfields/1833/Spawns.json already has all 36 points (1:1 matched).
#  loot:      corpse drops with real item ids -> ItemTemplates Lxxx + LootTable on our templates.
#  roamers:   plan.wander hashes get Features+=Wander (from npc_moves MoveType=moving).
#  friendly:  Janella Gheron (EIQU) gets NoCombat+NoMove like city guards.
# Rerunnable: Source 'scrape-cb-20261103'.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false); $gd = [IO.Path]::GetFullPath($GameData); $tag = 'scrape-cb-20261103'
function RJ($p) { [IO.File]::ReadAllText($p, $u) | ConvertFrom-Json }
$plan = RJ (Join-Path $PSScriptRoot 'scrape_import_cb_20261103.json')
$npath = Join-Path $gd 'NpcTemplates.json'; $nt = RJ $npath
$ipath = Join-Path $gd 'ItemTemplates.json'; $it = RJ $ipath
foreach ($p in @($nt.PSObject.Properties)) { if ("$($p.Value.ContentProvenance.Source)" -eq $tag) { $nt.PSObject.Properties.Remove($p.Name) } }
foreach ($p in @($it.PSObject.Properties)) { if ("$($p.Value.Source)" -eq $tag) { $it.PSObject.Properties.Remove($p.Name) } }
$pool = New-Object System.Collections.ArrayList
foreach ($p in $nt.PSObject.Properties) {
  if ($p.Value.ContentProvenance.Guessed) { continue }
  foreach ($b in @($p.Value.Templates)) {
    if ($b -and $b.Stats -and $b.Stats.'359') { [void]$pool.Add($b) }
  }
}
if ($pool.Count -lt 1) { throw 'no donor pool' }
$b36 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'; $seq = 0
function NewItemHash {
  while ($true) {
    $script:seq++
    $h = 'L' + $b36[[int][math]::Floor($script:seq / 1296) % 36] + $b36[[int][math]::Floor($script:seq / 36) % 36] + $b36[$script:seq % 36]
    if (-not $it.$h -and -not $nt.$h) { return $h }
  }
}
$itemHash = @{}
function ItemRow($e) {
  $k = "$($e.low)/$($e.high)"
  if (-not $itemHash[$k]) {
    $h = NewItemHash
    $itemHash[$k] = $h
    $it | Add-Member -Force NoteProperty $h ([pscustomobject]@{ Templates = @([int]$e.low, [int]$e.high); Source = $tag })
  }
  [pscustomobject]@{ Hash = $itemHash[$k]; Repeats = 1; Chance = [int]$e.chance; LevelMod = 0 }
}
$lootByName = @{}
foreach ($m in $plan.loot.PSObject.Properties) {
  $lootByName[$m.Name] = @(foreach ($e in $m.Value) { ItemRow $e })
}
$made = 0; $skipReal = 0; $lootRows = 0; $friendlyT = 0; $wanderT = 0
foreach ($t in $plan.templates) {
  if ($nt.($t.hash)) { $skipReal++; continue }
  $isFriendly = $false
  $bands = foreach ($b in $t.bands) {
    $L = [int]$b.Level
    $d = $pool | Sort-Object { [math]::Abs([int]$_.Level - $L) } | Select-Object -First 1
    $c = $d | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $c.Name = $b.Name; $c.Level = $L
    $set = [ordered]@{ '54' = $L; '33' = [int]$b.side; '359' = [int]$b.md; '360' = [int]$b.sc; '455' = [int]$b.fam }
    if ([int]$b.hp -gt 0) { $set['1'] = [int]$b.hp; $set['27'] = [int]$b.hp }
    if ([int]$b.breed -gt 0) { $set['4'] = [int]$b.breed }
    if ([int]$b.head -gt 0) { $set['64'] = [int]$b.head }
    if ([int]$b.mesh -gt 0) { $set['12'] = [int]$b.mesh }
    if ([int]$b.tex -gt 0) { $set['344'] = [int]$b.tex }
    # scraped Flags 268964353 for hostiles; friendly uses 277352961
    if ($b.guard) { $set['0'] = 277352961 } else { $set['0'] = 268964353 }
    foreach ($k in $set.Keys) { $c.Stats | Add-Member -Force NoteProperty $k $set[$k] }
    $loot = @($lootByName[$b.Name] | Where-Object { $_ }); $lootRows += $loot.Count
    $c | Add-Member -Force NoteProperty LootTable $loot
    foreach ($k in @('Equipment', 'ContentProvenance')) {
      if ($c.PSObject.Properties[$k]) { $c.PSObject.Properties.Remove($k) }
    }
    $c | Add-Member -Force NoteProperty HasHeadMesh ([int]$b.head -gt 0)
    $feat = @($c.Features | Where-Object { $_ -and $_ -ne 'NoCombat' -and $_ -ne 'NoMove' -and $_ -ne 'Wander' })
    if ($b.guard) {
      $isFriendly = $true
      $feat = @($feat) + 'NoCombat' + 'NoMove'
    } elseif ($t.wander) {
      $feat = @($feat) + 'Wander'
    }
    $c | Add-Member -Force NoteProperty Features @($feat | Sort-Object -Unique)
    $c
  }
  if ($isFriendly) { $friendlyT++ }
  if ($t.wander) { $wanderT++ }
  $note = "live Whozewhatzit scrape 2026-11-03 Cyborg Barracks pf 1833 ($($t.src) hash $($t.hash))"
  if ($isFriendly) { $note += '; friendly quest NPC NoCombat/NoMove' }
  if ($t.wander) { $note += '; Wander from npc_moves' }
  $nt | Add-Member -Force NoteProperty $t.hash ([pscustomobject]@{
      Templates = @($bands)
      ContentProvenance = [pscustomobject]@{ Guessed = $false; Source = $tag; Note = $note }
    })
  $made++
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 10) + "`n", $u)
[IO.File]::WriteAllText($ipath, ($it | ConvertTo-Json -Depth 10) + "`n", $u)
# refuse accidental SpawnsAdd if plan ever grows one belonging to another source
$nsp = 0
foreach ($pp in @($plan.spawns.PSObject.Properties)) {
  $pf = [int]$pp.Name; $dir = Join-Path $gd "PlayfieldContent\$pf"
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory $dir | Out-Null }
  $f = Join-Path $dir 'SpawnsAdd.json'
  if (Test-Path $f) {
    $old = RJ $f
    if ("$($old.source)" -ne $tag) { throw "SpawnsAdd.json in $pf belongs to $($old.source)" }
  }
  $pts = foreach ($s in $pp.Value) {
    [ordered]@{
      districtIndex = 0; hash = 0; hashText = $s.hash; manifestHash = 0
      minLevel = [int]$s.lo; maxLevel = [int]$s.hi; respawnChance = 100; flags = 1; respawnTime = 600
      version7Extra = 0; hasOptionalFlagBlock = $true; nativeFlags = 0; moreFlags = 0
      assistanceRadius = 0; unknownOptionalU8 = 0
      angle = [int][math]::Round([double]$s.angle); angleW = 1
      position = @([double]$s.x, [double]$s.y, [double]$s.z); radius = 1
      additionalPoints = @(); extensions = $null
    }
  }
  if (@($pts).Count -gt 0) {
    $doc = [ordered]@{ schemaVersion = 1; recordType = 1000014; playfieldId = $pf; source = $tag; spawns = @($pts) }
    [IO.File]::WriteAllText($f, ($doc | ConvertTo-Json -Depth 6) + "`n", $u)
    $nsp += @($pts).Count
  }
}
"templates made=$made skipped(existing)=$skipReal friendly=$friendlyT wander=$wanderT spawnsAdd=$nsp lootItems=$($itemHash.Count) lootRows=$lootRows donorPool=$($pool.Count)"
