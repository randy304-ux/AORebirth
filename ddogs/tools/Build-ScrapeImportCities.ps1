# ddogs/tools/Build-ScrapeImportCities.ps1 - 2026-10-30 batch from Randy's live PRK scrape (2026-10-29 CSVs):
#  cities/zones: Newland City 566, Newland 567, ICC/Andromeda 655, Omni-1 Entertainment 705, Omni-1 Trade 710, Borealis 800,
#  Omni/Neutral Training 950/954, Rome 735/740 leftovers, Jobe 4001/4530/4531, Neuters R Us 1902, backyards 3052/3089, Phasefront 9041, Palmiero 7001.
#  templates: pink hash points filled (hashText) or new Cxxx hashes, same donor+scraped-stat method as Build-ScrapeImportSL.ps1.
#             city guards (Omni/neutral side) get NoCombat+NoMove like the Rome Park guards.
#  spawns:    PlayfieldContent/{pf}/SpawnsAdd.json (refuses to overwrite a SpawnsAdd.json written by another source).
#  vendors:   scraped shops (exact stock) as content NPC shops in playfields with no DB vendors (566/567/705/800/4001/7001); body cloned from 4311 YUPH.
#  loot:      corpse drops -> ItemTemplates (Lxxx) on new templates, and APPENDED (no stat changes) to existing templates whose band name matches.
#  cleanup:   hide UNPU (Uncle Pumpkin-Head placeholders duplicate the Halloween UPH spawns), remove old daily stand-ins (800 John, 735/540 agency desks, 4001 Scarlett).
# Rerunnable: Source 'scrape-city-20261030' / key prefix ddogs:scrapecity:.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false); $gd = [IO.Path]::GetFullPath($GameData); $tag = 'scrape-city-20261030'
function RJ($p) { [IO.File]::ReadAllText($p, $u) | ConvertFrom-Json }
$plan = RJ (Join-Path $PSScriptRoot 'scrape_import_city_20261030.json')
$npath = Join-Path $gd 'NpcTemplates.json'; $nt = RJ $npath
$ipath = Join-Path $gd 'ItemTemplates.json'; $it = RJ $ipath
foreach ($p in @($nt.PSObject.Properties)) { if ("$($p.Value.ContentProvenance.Source)" -eq $tag) { $nt.PSObject.Properties.Remove($p.Name) } }
foreach ($p in @($it.PSObject.Properties)) { if ("$($p.Value.Source)" -eq $tag) { $it.PSObject.Properties.Remove($p.Name) } }
$pool = New-Object System.Collections.ArrayList
foreach ($p in $nt.PSObject.Properties) { if ($p.Value.ContentProvenance.Guessed) { continue }; foreach ($b in @($p.Value.Templates)) { if ($b -and $b.Stats -and $b.Stats.'359') { [void]$pool.Add($b) } } }
$b36 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'; $seq = 0
function NewItemHash { while ($true) { $script:seq++; $h = 'L' + $b36[[int][math]::Floor($script:seq / 1296) % 36] + $b36[[int][math]::Floor($script:seq / 36) % 36] + $b36[$script:seq % 36]; if (-not $it.$h -and -not $nt.$h) { return $h } } }
$itemHash = @{}
function ItemRow($e) {
  $k = "$($e.low)/$($e.high)"
  if (-not $itemHash[$k]) { $h = NewItemHash; $itemHash[$k] = $h; $it | Add-Member -Force NoteProperty $h ([pscustomobject]@{ Templates = @([int]$e.low, [int]$e.high); Source = $tag }) }
  [pscustomobject]@{ Hash = $itemHash[$k]; Repeats = 1; Chance = [int]$e.chance; LevelMod = 0 }
}
$lootByName = @{}; foreach ($m in $plan.loot.PSObject.Properties) { $lootByName[$m.Name] = @(foreach ($e in $m.Value) { ItemRow $e }) }
$made = 0; $skipReal = 0; $lootRows = 0; $guardT = 0
foreach ($t in $plan.templates) {
  if ($nt.($t.hash)) { $skipReal++; continue }
  $isGuard = $false
  $bands = foreach ($b in $t.bands) {
    $L = [int]$b.Level; $d = $pool | Sort-Object { [math]::Abs([int]$_.Level - $L) } | Select-Object -First 1
    $c = $d | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $c.Name = $b.Name; $c.Level = $L
    $set = [ordered]@{ '54' = $L; '33' = [int]$b.side; '359' = [int]$b.md; '360' = [int]$b.sc; '455' = [int]$b.fam }
    if ([int]$b.hp -gt 0) { $set['1'] = [int]$b.hp; $set['27'] = [int]$b.hp }
    if ([int]$b.breed -gt 0) { $set['4'] = [int]$b.breed }
    if ([int]$b.head -gt 0) { $set['64'] = [int]$b.head }
    if ([int]$b.mesh -gt 0) { $set['12'] = [int]$b.mesh }
    if ([int]$b.tex -gt 0) { $set['344'] = [int]$b.tex }
    foreach ($k in $set.Keys) { $c.Stats | Add-Member -Force NoteProperty $k $set[$k] }
    $loot = @($lootByName[$b.Name] | Where-Object { $_ }); $lootRows += $loot.Count
    $c | Add-Member -Force NoteProperty LootTable $loot
    if ($b.guard) { $isGuard = $true; $c | Add-Member -Force NoteProperty Features @(@($c.Features | Where-Object { $_ -and $_ -ne 'Wander' }) + 'NoCombat' + 'NoMove' | Sort-Object -Unique) }
    $c
  }
  if ($isGuard) { $guardT++ }
  $nt | Add-Member -Force NoteProperty $t.hash ([pscustomobject]@{ Templates = @($bands); ContentProvenance = [pscustomobject]@{ Guessed = $false; Source = $tag; Note = "live PRK scrape ($($t.src), pf $($t.pf))$(if($isGuard){' friendly city guard NoCombat/NoMove'})" } })
  $made++
}
# loot appended to existing templates (stats untouched)
$exRows = 0; $exBands = 0
foreach ($m in $plan.existingLoot.PSObject.Properties) {
  $rows = @(foreach ($e in $m.Value.rows) { ItemRow $e })
  foreach ($h in $m.Value.hashes) {
    $tp = $nt.$h; if (-not $tp) { continue }
    foreach ($b in @($tp.Templates)) {
      if ($b.Name -ne $m.Name) { continue }
      $have = @($b.LootTable | Where-Object { $_ } | ForEach-Object { $_.Hash })
      $add = @($rows | Where-Object { $have -notcontains $_.Hash })
      if ($add.Count -gt 0) { $b | Add-Member -Force NoteProperty LootTable @(@($b.LootTable | Where-Object { $_ }) + $add); $exRows += $add.Count; $exBands++ }
    }
  }
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 10) + "`n", $u)
[IO.File]::WriteAllText($ipath, ($it | ConvertTo-Json -Depth 10) + "`n", $u)
# add-on spawns
$nsp = 0
foreach ($pp in $plan.spawns.PSObject.Properties) {
  $pf = [int]$pp.Name; $dir = Join-Path $gd "PlayfieldContent\$pf"; if (-not (Test-Path $dir)) { New-Item -ItemType Directory $dir | Out-Null }
  $f = Join-Path $dir 'SpawnsAdd.json'
  if (Test-Path $f) { $old = RJ $f; if ("$($old.source)" -ne $tag) { throw "SpawnsAdd.json in $pf belongs to $($old.source)" } }
  $pts = foreach ($s in $pp.Value) {
    [ordered]@{ districtIndex = 0; hash = 0; hashText = $s.hash; manifestHash = 0; minLevel = [int]$s.lo; maxLevel = [int]$s.hi; respawnChance = 100; flags = 1; respawnTime = 600
      version7Extra = 0; hasOptionalFlagBlock = $true; nativeFlags = 0; moreFlags = 0; assistanceRadius = 0; unknownOptionalU8 = 0; angle = [int][math]::Round([double]$s.angle); angleW = 1
      position = @([double]$s.x, [double]$s.y, [double]$s.z); radius = 1; additionalPoints = @(); extensions = $null }
  }
  $doc = [ordered]@{ schemaVersion = 1; recordType = 1000014; playfieldId = $pf; source = $tag; spawns = @($pts) }
  [IO.File]::WriteAllText($f, ($doc | ConvertTo-Json -Depth 6) + "`n", $u); $nsp += @($pts).Count
}
# vendors + stand-in cleanup
$src = (RJ (Join-Path $gd 'PlayfieldContent\4311\Npcs.json')).Npcs | Where-Object { $_.Key -eq 'ddogs:yuttos:YUPH' } | Select-Object -First 1
if (-not $src) { throw 'no 4311 YUPH look' }
$drop = @('ddogs:daily:800:john-smith','ddogs:agency:735:receptionist','ddogs:agency:735:handler','ddogs:agency:735:senior','ddogs:agency:735:alien',
          'ddogs:agency:540:receptionist','ddogs:agency:540:handler','ddogs:agency:540:senior','ddogs:agency:540:alien','ddogs:doja:4001:scarlett')
$pfs = @(@($plan.vendors | ForEach-Object { [int]$_.pf }) + 800,735,540,4001 | Sort-Object -Unique)
$vi = 0; $stocked = 0; $removed = 0
foreach ($pf in $pfs) {
  $path = Join-Path $gd "PlayfieldContent\$pf\Npcs.json"; if (-not (Test-Path (Split-Path $path))) { New-Item -ItemType Directory (Split-Path $path) | Out-Null }
  $doc = if (Test-Path $path) { RJ $path } else { [pscustomobject]@{ SchemaVersion = 1; PlayfieldId = $pf; Npcs = @() } }
  $before = @($doc.Npcs | Where-Object { $_ }).Count
  $keep = @($doc.Npcs | Where-Object { $_ -and "$($_.Key)" -notlike 'ddogs:scrapecity:*' -and $drop -notcontains "$($_.Key)" })
  $removed += $before - $keep.Count
  foreach ($v in @($plan.vendors | Where-Object { [int]$_.pf -eq $pf })) {
    $stock = @(); $slot = 0; foreach ($x in $v.items) { $stock += [pscustomobject][ordered]@{ Slot = $slot++; LowId = [int]$x[0]; HighId = [int]$x[1]; Quality = [int]$x[2] } }
    $vi++; $inst = 0x7B5E0000 + $vi
    $n = $src | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $n.Key = "ddogs:scrapecity:$($pf):$($v.inst)"; $n.Provenance = "live PRK scrape 2026-10-29 (position + stock, vending machine mesh $($v.mesh)); body cloned from 4311 YUPH"
    $n.Name = $v.name; $n.ContentNpcIdentity = ('SimpleChar:{0:X8}' -f $inst); $n.InstanceId = $inst; $n.PlayfieldId = $pf
    $n.Position = @($v.pos); $n.Rotation = @($v.rot); $n.Vendor.InstanceId = 0x13D70000 + $vi; $n.Vendor.Stock = $stock
    if ($n.PSObject.Properties['SpawnHash']) { $n.PSObject.Properties.Remove('SpawnHash') }
    $keep += $n; $stocked++
  }
  $doc.Npcs = @($keep); [IO.File]::WriteAllText($path, ($doc | ConvertTo-Json -Depth 10), $u)
}
# SpawnSkips: UNPU hidden (object format), placeholder points under new vendors
$kpath = Join-Path $gd 'SpawnSkips.json'; $sk = RJ $kpath
$sk.hiddenHashes = @(@($sk.hiddenHashes | Where-Object { $_ -and $_.hash -and $_.hash -ne 'UNPU' }) + [pscustomobject][ordered]@{ hash = 'UNPU'; reason = 'Uncle Pumpkin-Head placeholders: live scrape shows Uncle Pumpkin-Head on these points; Halloween.json UPH spawns already place them (Borealis/ICC). 2026-10-30' })
$sk.removedPoints = @(@($sk.removedPoints | Where-Object { "$($_.reason)" -notlike '*city scrape import 2026-10-30*' }) + @($plan.removed | Where-Object { $_ }))
[IO.File]::WriteAllText($kpath, ($sk | ConvertTo-Json -Depth 6) + "`n", $u)
"templates made=$made guards=$guardT skipped(existing)=$skipReal spawns=$nsp vendors=$stocked standinsRemoved=$removed lootItems=$($itemHash.Count) lootRowsNew=$lootRows lootRowsExisting=$exRows bands=$exBands"
