# ddogs/tools/Build-ScrapeImportRK.ps1 - Phase 2 remaining Rubi-Ka scrape import
# Plan = scrape_import_rk_20261106.json. Overlays NpcTemplates.RK.json + ItemTemplates.RK.json;
# SpawnsAdd.rk.json per PF. Rerunnable Source 'scrape-rk-20261106'.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false); $gd = [IO.Path]::GetFullPath($GameData); $tag = 'scrape-rk-20261106'
function RJ($p) { [IO.File]::ReadAllText($p, $u) | ConvertFrom-Json }
$plan = RJ (Join-Path $PSScriptRoot 'scrape_import_rk_20261106.json')
if ($plan.source -ne $tag) { throw "plan source $($plan.source)" }

$nt = RJ (Join-Path $gd 'NpcTemplates.json')
foreach ($f in [IO.Directory]::GetFiles($gd, 'NpcTemplates.*.json')) {
  if ([IO.Path]::GetFileName($f) -eq 'NpcTemplates.RK.json') { continue }
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

$it = [ordered]@{}
$b36 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'; $seq = 0
$used = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($p in $nt.PSObject.Properties) { [void]$used.Add($p.Name) }
function NewItemHash {
  while ($true) {
    $script:seq++
    $h = 'L' + $b36[[int][math]::Floor($script:seq / 1296) % 36] + $b36[[int][math]::Floor($script:seq / 36) % 36] + $b36[$script:seq % 36]
    if (-not $used.Contains($h) -and -not $it.Contains($h)) { [void]$used.Add($h); return $h }
  }
}
$itemHash = @{}
function ItemRow($e) {
  $k = "$($e.low)/$($e.high)"
  if (-not $itemHash[$k]) {
    $h = NewItemHash
    $itemHash[$k] = $h
    $it[$h] = [pscustomobject]@{ Templates = @([int]$e.low, [int]$e.high); Source = $tag }
  }
  [pscustomobject]@{ Hash = $itemHash[$k]; Repeats = 1; Chance = [int]$e.chance; LevelMod = 0 }
}
$lootByName = @{}
foreach ($m in @($plan.loot.PSObject.Properties)) {
  $lootByName[$m.Name] = @(foreach ($e in @($m.Value)) { ItemRow $e })
}

function Parse-Textures([string]$s) {
  if ([string]::IsNullOrWhiteSpace($s)) { return @() }
  $out = New-Object System.Collections.Generic.List[object]
  foreach ($tok in ($s -split ';')) {
    if ([string]::IsNullOrWhiteSpace($tok)) { continue }
    $p = $tok.Split(':')
    if ($p.Length -lt 2) { continue }
    $place = 0; $id = 0
    if (-not [int]::TryParse($p[0], [ref]$place)) { continue }
    if (-not [int]::TryParse($p[1], [ref]$id)) { continue }
    if ($id -le 0) { continue }
    [void]$out.Add([pscustomobject]@{ Id = $id; Place = $place })
  }
  ,$out.ToArray()
}
function Parse-Meshes([string]$s) {
  if ([string]::IsNullOrWhiteSpace($s)) { return @() }
  $out = New-Object System.Collections.Generic.List[object]
  foreach ($tok in ($s -split ';')) {
    if ([string]::IsNullOrWhiteSpace($tok)) { continue }
    $p = $tok.Split(':')
    if ($p.Length -lt 4) { continue }
    $pos = 0; $id = 0; $ovr = 0; $layer = 0
    if (-not [int]::TryParse($p[0], [ref]$pos)) { continue }
    if (-not [int]::TryParse($p[1], [ref]$id)) { continue }
    if (-not [int]::TryParse($p[2], [ref]$ovr)) { continue }
    if (-not [int]::TryParse($p[3], [ref]$layer)) { continue }
    if ($id -le 0) { continue }
    [void]$out.Add([pscustomobject]@{ Id = $id; Position = $pos; OverrideTextureId = $ovr; Layer = $layer })
  }
  ,$out.ToArray()
}
function Test-PinkLikeProv($tp) {
  if (-not $tp -or -not $tp.Templates) { return $true }
  $prov = $tp.ContentProvenance
  if ($prov -and ($prov.Guessed -or $prov.StandIn)) { return $true }
  $b = @($tp.Templates)[0]
  if ($b -and $b.ContentProvenance -and ($b.ContentProvenance.Guessed -or $b.ContentProvenance.StandIn)) { return $true }
  return $false
}

$ov = [ordered]@{}; $made = 0; $lootRows = 0; $wanderT = 0; $pinkOverwrites = 0
foreach ($t in @($plan.templates)) {
  $existing = $nt.($t.hash)
  if ($existing -and $existing.Templates -and -not $t.fillPink) { continue }
  # fillPink may overwrite Guessed/StandIn/missing pink hashes; never clobber a real template.
  if ($t.fillPink -and $existing -and $existing.Templates -and -not (Test-PinkLikeProv $existing)) { continue }
  if ($t.fillPink -and $existing -and $existing.Templates -and (Test-PinkLikeProv $existing)) { $pinkOverwrites++ }
  $bands = foreach ($b in @($t.bands)) {
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
    $isMonster = ([int]$b.side -eq 3)
    if ($b.guard -and -not $isMonster) { $set['0'] = 277352961 } else { $set['0'] = 268964353 }
    foreach ($k in $set.Keys) { $c.Stats | Add-Member -Force NoteProperty $k $set[$k] }
    $loot = @($lootByName[$b.Name] | Where-Object { $_ })
    # No v6.1 scraped loot: use existing server tables only (no invented IDs).
    # Named dyna bosses -> DYB1..8 (Build-DynaLoot bands). Ordinary monsters -> CMTR/CMHK/OFAB.
    if ($loot.Count -eq 0 -and $isMonster) {
      $nm = [string]$b.Name
      $isNamedDyna = ($nm -match '\bthe\b') -and ($nm -notmatch '^(The Ring|Ring Ticket)|Ticket Salesman') -and ($L -ge 1) -and ($L -le 275)
      if ($isNamedDyna) {
        $dyb = $(if ($L -le 15) { 'DYB1' } elseif ($L -le 35) { 'DYB2' } elseif ($L -le 55) { 'DYB3' } elseif ($L -le 85) { 'DYB4' } elseif ($L -le 120) { 'DYB5' } elseif ($L -le 160) { 'DYB6' } elseif ($L -le 200) { 'DYB7' } else { 'DYB8' })
        $loot = @([pscustomobject]@{ Hash = $dyb; Repeats = 1; Chance = 100; LevelMod = 25; CountMin = 5; CountMax = 7 })
      } else {
        $loot = @(
          [pscustomobject]@{ Hash = 'CMTR'; Repeats = 1; Chance = 50; LevelMod = 20 }
          [pscustomobject]@{ Hash = 'CMHK'; Repeats = 1; Chance = 12; LevelMod = 20 }
          [pscustomobject]@{ Hash = 'OFAB'; Repeats = 1; Chance = 1; LevelMod = 0 }
        )
      }
    }
    $lootRows += $loot.Count
    $c | Add-Member -Force NoteProperty LootTable $loot
    foreach ($k in @('Equipment', 'ContentProvenance', 'Textures', 'Meshes')) {
      if ($c.PSObject.Properties[$k]) { $c.PSObject.Properties.Remove($k) }
    }
    $tex = Parse-Textures ([string]$b.textures)
    $msh = Parse-Meshes ([string]$b.meshes)
    if (@($tex).Count -gt 0) { $c | Add-Member -Force NoteProperty Textures @($tex) }
    if (@($msh).Count -gt 0) { $c | Add-Member -Force NoteProperty Meshes @($msh) }
    $c | Add-Member -Force NoteProperty HasHeadMesh ([int]$b.head -gt 0)
    $feat = @($c.Features | Where-Object { $_ -and $_ -ne 'NoCombat' -and $_ -ne 'NoMove' -and $_ -ne 'Wander' })
    if (-not $isMonster) { $feat = @($feat) + 'NoCombat' }
    if ($t.wander) { $feat = @($feat) + 'Wander' }
    elseif (-not $isMonster) { $feat = @($feat) + 'NoMove' }
    $c | Add-Member -Force NoteProperty Features @($feat | Sort-Object -Unique)
    $c
  }
  if ($t.wander) { $wanderT++ }
  $note = "live RK scrape ($($t.src))"
  if ($t.fillPink) { $note += '; pink hash fill' }
  if ($t.wander) { $note += '; Wander' }
  $note += '; gloves omitted unless city v6.0 capture supplies them (none in this pack)'
  $ov[$t.hash] = [pscustomobject]@{
    Templates = @($bands)
    ContentProvenance = [pscustomobject]@{ Guessed = $false; Source = $tag; Note = $note }
  }
  $made++
}

$exRows = 0
$existingPatch = [ordered]@{}
foreach ($m in @($plan.existingLoot.PSObject.Properties)) {
  $rows = @(foreach ($e in @($m.Value.rows)) { ItemRow $e })
  foreach ($h in @($m.Value.hashes)) {
    $tp = $nt.$h; if (-not $tp) { continue }
    $changed = $false
    $newBands = foreach ($b in @($tp.Templates)) {
      if ($b.Name -ne $m.Name) { $b; continue }
      $have = @($b.LootTable | Where-Object { $_ } | ForEach-Object { $_.Hash })
      $add = @($rows | Where-Object { $have -notcontains $_.Hash })
      if ($add.Count -gt 0) {
        $b | Add-Member -Force NoteProperty LootTable @(@($b.LootTable | Where-Object { $_ }) + $add)
        $exRows += $add.Count; $changed = $true
      }
      $b
    }
    if ($changed) {
      $existingPatch[$h] = [pscustomobject]@{
        Templates = @($newBands)
        ContentProvenance = $(if ($tp.ContentProvenance) { $tp.ContentProvenance } else { [pscustomobject]@{ Guessed = $false; Source = $tag; Note = "loot append $tag" } })
      }
    }
  }
}
foreach ($p in $existingPatch.GetEnumerator()) { $ov[$p.Key] = $p.Value }

[IO.File]::WriteAllText((Join-Path $gd 'NpcTemplates.RK.json'), (([pscustomobject]$ov) | ConvertTo-Json -Depth 12) + "`n", $u)
[IO.File]::WriteAllText((Join-Path $gd 'ItemTemplates.RK.json'), (([pscustomobject]$it) | ConvertTo-Json -Depth 6) + "`n", $u)

$nsp = 0; $reuse = 0
foreach ($pp in @($plan.spawnsByPf.PSObject.Properties)) {
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
  [IO.File]::WriteAllText((Join-Path $dir 'SpawnsAdd.rk.json'), ($doc | ConvertTo-Json -Depth 6) + "`n", $u)
  $nsp += @($pts).Count
}

$summary = [ordered]@{
  source = $tag; templates = $made; spawns = $nsp; reuse = $reuse
  pinkFills = @($plan.pinkFills).Count; lootItems = $itemHash.Count
  lootRowsNew = $lootRows; lootRowsExisting = $exRows; wander = $wanderT
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'scrape_import_rk_20261106.build.json'), ($summary | ConvertTo-Json) + "`n", $u)
$summary.pinkOverwrites = $pinkOverwrites
"templates=$made spawns=$nsp reuse=$reuse pinkFills=$($summary.pinkFills) pinkOverwrites=$pinkOverwrites lootItems=$($itemHash.Count) lootRowsNew=$lootRows lootRowsExisting=$exRows wander=$wanderT"
