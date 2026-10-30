# ddogs/tools/Build-ScrapeImportNasc.ps1 - imports Randy's live PRK scrape (2026-10-29 CSVs) for Nascense Frontier/Wilds/Swamp (4310-4312), Xan 6011-6014 and Unicorn Outpost 4364.
# Same pipeline as Build-ScrapeImportSL.ps1 (plan scrape_import_nasc_20261030.json built off-box); new hashes are Nxxx; vendors are the two Unicorn vending-machine shops.
#  templates: NpcTemplates.json entries (hash point hashText, or new Qxxx hash), stats cloned from the nearest-level donor band
#             then overwritten with scraped name/level/health/side/breed/MonsterData/scale/mesh/head/texture/NPCFamily.
#  spawns:    new points only, in PlayfieldContent/{pf}/SpawnsAdd.json (client Spawns.json untouched).
#  vendors:   Craig-Or garden shops as content NPCs (look cloned from the 4677 ones) with the exact scraped stock.
#  removed:   SpawnSkips removedPoints for unresolved placeholder points on top of a new vendor.
#  loot:      corpse drops per mob name -> ItemTemplates entries (Lxxx) + LootTable on our templates.
# Rerunnable: everything it writes carries Source 'scrape-nasc-20261030' / key prefix ddogs:scrape: and is replaced each run.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false); $gd = [IO.Path]::GetFullPath($GameData); $tag = 'scrape-nasc-20261030'
$plan = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'scrape_import_nasc_20261030.json'), $u) | ConvertFrom-Json
$npath = Join-Path $gd 'NpcTemplates.json'; $nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
$ipath = Join-Path $gd 'ItemTemplates.json'; $it = [IO.File]::ReadAllText($ipath, $u) | ConvertFrom-Json
# drop our previous run
foreach ($p in @($nt.PSObject.Properties)) { if ("$($p.Value.ContentProvenance.Source)" -eq $tag) { $nt.PSObject.Properties.Remove($p.Name) } }
foreach ($p in @($it.PSObject.Properties)) { if ("$($p.Value.Source)" -eq $tag) { $it.PSObject.Properties.Remove($p.Name) } }
# donor pool: real (non-guessed) bands with a MonsterData stat
$pool = New-Object System.Collections.ArrayList
foreach ($p in $nt.PSObject.Properties) { if ($p.Value.ContentProvenance.Guessed) { continue }; foreach ($b in @($p.Value.Templates)) { if ($b -and $b.Stats -and $b.Stats.'359') { [void]$pool.Add($b) } } }
# loot items
$b36 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'; $seq = 0
function NewItemHash { while ($true) { $script:seq++; $h = 'L' + $b36[[int][math]::Floor($script:seq / 1296) % 36] + $b36[[int][math]::Floor($script:seq / 36) % 36] + $b36[$script:seq % 36]; if (-not $it.$h -and -not $nt.$h) { return $h } } }
$itemHash = @{}; $lootByName = @{}
foreach ($m in $plan.loot.PSObject.Properties) {
  $lootByName[$m.Name] = @(foreach ($e in $m.Value) {
    $k = "$($e.low)/$($e.high)"
    if (-not $itemHash[$k]) { $h = NewItemHash; $itemHash[$k] = $h; $it | Add-Member -Force NoteProperty $h ([pscustomobject]@{ Templates = @([int]$e.low, [int]$e.high); Source = $tag }) }
    [pscustomobject]@{ Hash = $itemHash[$k]; Repeats = 1; Chance = [int]$e.chance; LevelMod = 0 }
  })
}
$made = 0; $skipReal = 0; $lootRows = 0
foreach ($t in $plan.templates) {
  if ($nt.($t.hash)) { $skipReal++; continue }
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
    if ($b.garden) { $c | Add-Member -Force NoteProperty Features @(@($c.Features | Where-Object { $_ -and $_ -ne 'Wander' }) + 'NoCombat' | Sort-Object -Unique) }
    $c
  }
  $nt | Add-Member -Force NoteProperty $t.hash ([pscustomobject]@{ Templates = @($bands); ContentProvenance = [pscustomobject]@{ Guessed = $false; Source = $tag; Note = "live PRK scrape ($($t.src))" } })
  $made++
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 10) + "`n", $u)
[IO.File]::WriteAllText($ipath, ($it | ConvertTo-Json -Depth 10) + "`n", $u)
# add-on spawns
$nsp = 0
foreach ($pp in $plan.spawns.PSObject.Properties) {
  $pf = [int]$pp.Name; $dir = Join-Path $gd "PlayfieldContent\$pf"; if (-not (Test-Path $dir)) { New-Item -ItemType Directory $dir | Out-Null }
  $pts = foreach ($s in $pp.Value) {
    [ordered]@{ districtIndex = 0; hash = 0; hashText = $s.hash; manifestHash = 0; minLevel = [int]$s.lo; maxLevel = [int]$s.hi; respawnChance = 100; flags = 1; respawnTime = 600
      version7Extra = 0; hasOptionalFlagBlock = $true; nativeFlags = 0; moreFlags = 0; assistanceRadius = 0; unknownOptionalU8 = 0; angle = [int][math]::Round([double]$s.angle); angleW = 1
      position = @([double]$s.x, [double]$s.y, [double]$s.z); radius = 1; additionalPoints = @(); extensions = $null }
  }
  $doc = [ordered]@{ schemaVersion = 1; recordType = 1000014; playfieldId = $pf; source = $tag; spawns = @($pts) }
  [IO.File]::WriteAllText((Join-Path $dir 'SpawnsAdd.json'), ($doc | ConvertTo-Json -Depth 6) + "`n", $u); $nsp += @($pts).Count
}
# vendors: Unicorn Outpost vending machines as content NPC shops (look cloned from 4311 Yuttos Pharmaceuticals, mesh from scrape), exact scraped stock
$src = ([IO.File]::ReadAllText((Join-Path $gd 'PlayfieldContent\4311\Npcs.json'), $u) | ConvertFrom-Json).Npcs | Where-Object { $_.Key -eq 'ddogs:yuttos:YUPH' } | Select-Object -First 1
if (-not $src) { throw 'no 4311 YUPH look' }
$vi = 0; $stocked = 0
foreach ($grp in ($plan.vendors | Group-Object pf)) {
  $pf = [int]$grp.Name; $path = Join-Path $gd "PlayfieldContent\$pf\Npcs.json"; if (-not (Test-Path (Split-Path $path))) { New-Item -ItemType Directory (Split-Path $path) | Out-Null }
  $doc = if (Test-Path $path) { [IO.File]::ReadAllText($path, $u) | ConvertFrom-Json } else { [pscustomobject]@{ SchemaVersion = 1; PlayfieldId = $pf; Npcs = @() } }
  $keep = @($doc.Npcs | Where-Object { $_ -and "$($_.Key)" -notlike 'ddogs:scrapenasc:*' })
  foreach ($v in $grp.Group) {
    $stock = @(); $slot = 0; foreach ($x in $v.items) { $stock += [pscustomobject][ordered]@{ Slot = $slot++; LowId = [int]$x[0]; HighId = [int]$x[1]; Quality = [int]$x[2] } }
    $vi++; $inst = 0x7B5D0000 + $vi
    $n = $src | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $n.Key = "ddogs:scrapenasc:$($pf):$($v.name)"; $n.Provenance = "live PRK scrape 2026-10-29 (position + stock, vending machine mesh $($v.mesh)); body cloned from 4311 YUPH"
    $n.Name = $v.name; $n.ContentNpcIdentity = ('SimpleChar:{0:X8}' -f $inst); $n.InstanceId = $inst; $n.PlayfieldId = $pf
    $n.Position = @($v.pos); $n.Rotation = @($v.rot); $n.Vendor.InstanceId = 0x13D60000 + $vi; $n.Vendor.Stock = $stock
    if ($n.PSObject.Properties['SpawnHash']) { $n.PSObject.Properties.Remove('SpawnHash') }
    $keep += $n; $stocked++
  }
  $doc.Npcs = @($keep); [IO.File]::WriteAllText($path, ($doc | ConvertTo-Json -Depth 10), $u)
}
# placeholder points under new vendors
$kpath = Join-Path $gd 'SpawnSkips.json'; $sk = [IO.File]::ReadAllText($kpath, $u) | ConvertFrom-Json
$sk.removedPoints = @(@($sk.removedPoints | Where-Object { "$($_.reason)" -notlike '*nasc scrape import 2026-10-30*' }) + @($plan.removed))
[IO.File]::WriteAllText($kpath, ($sk | ConvertTo-Json -Depth 6) + "`n", $u)
"templates made=$made skipped(existing)=$skipReal spawns=$nsp vendors stocked=$stocked removedPoints=$(@($plan.removed).Count) lootItems=$($itemHash.Count) lootRows=$lootRows"

