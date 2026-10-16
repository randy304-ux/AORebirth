# ddogs/tools/Build-DynaContent.ps1 - Rubi-Ka dyna camps from the AO-Universe table (ddogs/tools/dyna-camps.csv).
# Boss and adds are hash spawns so the boss LootTable is copied onto the corpse. PlayfieldContent NPCs do not get that table.
# Positions: guide x,z. Y is the nearest client spawn in that playfield. ContentProvenance.Guessed=true.
# The dyna color buff is not implemented. Boss HP is 4x the rescaled donor, respawn 20 min; adds respawn in 3 min.
# Boss loot is applied by Build-DynaLoot.ps1 (5 to 7 items from that level band). Adds keep the common family table below.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$GameData = [IO.Path]::GetFullPath($GameData)
$ntPath = Join-Path $GameData 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($ntPath, $u) | ConvertFrom-Json
$camps = Import-Csv (Join-Path $PSScriptRoot 'dyna-camps.csv')
$pfId = @{
  'Aegean'=585; 'Andromeda'=655; 'Belial Forest'=605; 'Central Artery Valley'=590; 'Newland Desert'=565
  'Omni Forest'=716; 'Perpetual Wastelands'=570; 'Varmint Woods'=600; 'Wailing Wastes'=551
}
# Donor leaf. A stand-in is a captured mesh of a different creature; the real family was not in NpcTemplates.
$donorOf = @{
  'Reets'='DERT'; 'Pareets'='DERT'; 'Malle'='CIMA'; 'Leets'='LE04'; 'Shadowmutants'='SH05'
  'Tentacle Mutants'='TM05'; 'Tentacle Mutant'='TM07'; 'Fleas'='FLFE'; 'Eyemutants'='EM01'
  'Salamanders'='CBSN'; 'Snakes'='GISK'; 'Androids'='AD05'; 'Rollerrats'='RR06'; 'Spiders'='WROM'
  'Unfinished Breed'='UB05'; 'Clawfingers'='CL07'; 'Biofreak'='BFC1'; 'Biofreaks'='BFC1'; 'Nanofreaks'='BFC1'
  'Lizards'='SRLZ'; 'Igruana'='SRLZ'
  'Blubbags'='RR03'; 'Aquaans'='TM02'; 'Nighthowler'='PDST'; 'Hounds'='PDST'
  'Rhinomen'='CL07'; 'Minibulls'='CL10'; 'Buzzsaws'='AD03'; 'Mechdogs'='AD03'; 'Ninjadroids'='AD05'; 'Cyborgs'='AD05'
  'Scorpiods'='WROM'; 'Mantezes'='CL09'; 'Manteze'='CL09'; 'Mantis'='CL09'; 'Anuns'='HIAT'
  'Enigmas'='SH12'; 'Ottous'='CEON'; 'Bileswarms'='BFC2'; 'Biledrinkers'='BFC2'; 'Sandworms'='GISK'; 'Sand Demon'='GISK'
  'Brontos'='SRLZ'; 'Pit Lizards'='SRLZ'; 'Quake Lizards'='SRLZ'; 'Hammerbeasts'='SRLZ'
}
$real = @('Reets','Pareets','Malle','Leets','Shadowmutants','Tentacle Mutants','Tentacle Mutant','Fleas','Eyemutants','Salamanders','Snakes','Androids','Rollerrats','Spiders','Unfinished Breed','Clawfingers','Biofreak','Biofreaks','Nanofreaks','Lizards')
$big = @('Brontos','Pit Lizards','Quake Lizards','Hammerbeasts','Sandworms','Sand Demon')
$curve = @(@(1,20),@(5,115),@(15,504),@(24,500),@(50,2500),@(104,6236),@(139,10032),@(190,16798),@(214,20077),@(275,28000))
function HealthAt([int]$lvl) {
  $lvl = [math]::Max(1, $lvl)
  for ($i = 1; $i -lt $curve.Count; $i++) {
    $a0 = [double]$curve[$i-1][0]; $a1 = [double]$curve[$i-1][1]
    $b0 = [double]$curve[$i][0]; $b1 = [double]$curve[$i][1]
    if ($lvl -le $b0) {
      $t = ([math]::Log($lvl) - [math]::Log($a0)) / ([math]::Log($b0) - [math]::Log($a0))
      $log = [math]::Log($a1) + $t * ([math]::Log($b1) - [math]::Log($a1))
      return [int][math]::Round([math]::Exp($log))
    }
  }
  [int]$curve[-1][1]
}
$used = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($p in $nt.PSObject.Properties) { [void]$used.Add($p.Name) }
function Take-Hash([string]$prefix) {
  for ($i = 0; $i -lt 4096; $i++) {
    $h = $prefix + $i.ToString('x3').ToUpper()
    if (-not $script:used.Contains($h)) { [void]$script:used.Add($h); return $h }
  }
  throw "no free hash for $prefix"
}
$byKey = @{}
foreach ($p in $nt.PSObject.Properties) {
  if (-not $p.Value.Templates) { continue }
  $prov = $p.Value.Templates[0].ContentProvenance
  if ($prov -and $prov.CampKey) { $byKey[$prov.CampKey] = $p.Name }
}
$items = [ordered]@{ NS01 = @(150504,150504); NS02 = @(150503,150503); EDRC = @(275056,275056) }
$ipath = Join-Path $GameData 'ItemTemplates.json'
$ilines = [IO.File]::ReadAllLines($ipath, $u) | Where-Object { $_ -notmatch '^\s*"(NS01|NS02|EDRC)":' }
$iadd = foreach ($k in $items.Keys) { '    "{0}": {{"Templates": [{1},{2}]}},' -f $k, $items[$k][0], $items[$k][1] }
if ($ilines[0].Trim() -ne '{') { throw 'ItemTemplates.json: unexpected first line' }
[IO.File]::WriteAllLines($ipath, [string[]](@($ilines[0]) + $iadd + $ilines[1..($ilines.Count - 1)]), $u)
$rare = @(
  [pscustomobject]@{ Hash='CMTR'; Repeats=1; Chance=50; LevelMod=20 }
  [pscustomobject]@{ Hash='CMHK'; Repeats=1; Chance=20; LevelMod=20 }
  [pscustomobject]@{ Hash='OFAB'; Repeats=1; Chance=2; LevelMod=0 }
  [pscustomobject]@{ Hash='NS01'; Repeats=1; Chance=3; LevelMod=0 }
  [pscustomobject]@{ Hash='NS02'; Repeats=1; Chance=1; LevelMod=0 }
  [pscustomobject]@{ Hash='EDRC'; Repeats=1; Chance=3; LevelMod=0 }
)
$common = @(
  [pscustomobject]@{ Hash='CMTR'; Repeats=1; Chance=40; LevelMod=20 }
  [pscustomobject]@{ Hash='CMHK'; Repeats=1; Chance=8; LevelMod=20 }
  [pscustomobject]@{ Hash='OFAB'; Repeats=1; Chance=1; LevelMod=0 }
)
function New-Leaf($key, $name, $lvl, $type, $boss) {
  if (-not $script:donorOf.ContainsKey($type)) { throw "no donor for $type" }
  $don = $script:donorOf[$type]
  if (-not $script:nt.$don) { throw "donor template $don missing for $type" }
  $hash = $script:byKey[$key]
  if (-not $hash) { $hash = Take-Hash $(if ($boss) { 'D' } else { 'E' }); $script:byKey[$key] = $hash }
  if ($script:nt.$hash) { return $hash }
  $d = $script:nt.$don.Templates[0]
  $b = $d | ConvertTo-Json -Depth 8 | ConvertFrom-Json
  $dl = [math]::Max(1, [int]$d.Level)
  $b.Name = $name; $b.Level = $lvl; $b.Stats.'54' = $lvl
  $hp = [int][math]::Max((HealthAt $lvl), [double]$d.Stats.'1' * $lvl / $dl)
  if ($boss) { $hp = $hp * 4 }
  $b.Stats.'1' = $hp; if ($b.Stats.'27') { $b.Stats.'27' = $hp }
  $b.Stats.'0' = 268964353
  $scale = 100; if ($b.Stats.'360') { $scale = [int]$b.Stats.'360' }
  if ($type -eq 'Blubbags') { $b.Stats.'359' = 40075 }  # client monster data 'blubbag'
  if ($type -eq 'Rhinomen') { $b.Stats.'359' = 17664 }  # client monster data 'rhinoman'
  if ($type -in $script:big) { $b.Stats.'359' = 226557; $scale = 140 }
  if ($boss) { $scale = [int][math]::Max($scale, [int]($scale * 1.25)) }
  $b.Stats.'360' = $scale
  $b.Stats | Add-Member -Force NoteProperty '52' ([int]$(if ($boss) { $lvl * 40 } else { $lvl * 8 }))
  $loot = $(if ($boss) { @($script:rare) } else { @($script:common) })
  $b | Add-Member -Force NoteProperty LootTable $loot
  $stand = $type -notin $script:real
  $note = $(if ($stand) { "Stand-in mesh from $don. " } else { "Donor $don. " })
  $note += 'AO-Universe dyna table. Y from the nearest client spawn. No color buff. Boss rare loot is the guide''s general list only.'
  $b | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{
    Guessed=$true; Confirmed=$false; Donor=$don; Source=$note; Levels="$lvl"; CampKey=$key; StandIn=$stand
  })
  $script:nt | Add-Member -Force NoteProperty $hash ([pscustomobject]@{ Templates = @($b) })
  $script:made += $hash
  $hash
}
$made = @()
$bossHash = @{}
$addHash = @{}
foreach ($g in ($camps | Group-Object { '{0}|{1}' -f $_.type, $_.boss } | Sort-Object { [int]$_.Group[0].boss }, Name)) {
  $c0 = $g.Group[0]
  $bossHash[$g.Name] = New-Leaf ('boss|' + $g.Name) ($c0.type + ' Dyna') ([int]$c0.boss) $c0.type $true
}
foreach ($g in ($camps | Where-Object { $_.mobs -ne '(no mobs)' } | Group-Object { '{0}|{1}' -f $_.type, $_.mobs } | Sort-Object Name)) {
  $c0 = $g.Group[0]
  $lo, $hi = $c0.mobs -split '-'
  $lvl = [int][math]::Ceiling(([int]$lo + [int]$hi) / 2)
  $addHash[$g.Name] = New-Leaf ('add|' + $g.Name) $c0.type $lvl $c0.type $false
}
if ($made.Count) {
  $raw = [IO.File]::ReadAllText($ntPath, $u).TrimEnd()
  if (-not $raw.EndsWith('}')) { throw 'NpcTemplates.json: unexpected ending' }
  $raw = $raw.Substring(0, $raw.Length - 1).TrimEnd()
  if (-not $raw.EndsWith(',')) { $raw += ',' }
  $chunks = foreach ($h in $made) {
    $body = ($nt.$h | ConvertTo-Json -Depth 8)
    '    "{0}": {1}' -f $h, ($body -replace '(?m)^', '    ').TrimStart()
  }
  $raw += "`n" + ($chunks -join ",`n") + "`n}`n"
  [IO.File]::WriteAllText($ntPath, $raw, $u)
}
# Spawns. Idempotent on the exact position text.
$byPf = @{}
foreach ($c in $camps) {
  if (-not $pfId.ContainsKey($c.playfield)) { throw "unknown playfield $($c.playfield)" }
  $id = $pfId[$c.playfield]
  if (-not $byPf.ContainsKey($id)) { $byPf[$id] = New-Object System.Collections.Generic.List[object] }
  $byPf[$id].Add($c)
}
$offs = @(
  ,@(14, 0)
  ,@(-14, 0)
  ,@(0, 14)
  ,@(0, -14)
)
$added = 0; $far = 0
foreach ($id in $byPf.Keys) {
  $spath = Join-Path $GameData "Playfields\$id\Spawns.json"
  if (-not (Test-Path $spath)) { throw "missing spawns $spath" }
  $sraw = [IO.File]::ReadAllText($spath, $u)
  $sp = ($sraw | ConvertFrom-Json).spawns
  $metaPath = Join-Path $GameData "Playfields\$id\metadata.json"
  $world = 99999
  if (Test-Path $metaPath) {
    $meta = [IO.File]::ReadAllText($metaPath, $u) | ConvertFrom-Json
    $world = [int]$meta.width * [int]$meta.tileSize
  }
  $pieces = @(); $seen = @{}
  foreach ($c in $byPf[$id]) {
    $x = [double]$c.x; $z = [double]$c.z
    $spot = '{0}|{1}|{2}' -f $id, $x, $z
    if ($seen.ContainsKey($spot)) { $x += 8 }
    $seen[$spot] = $true
    $best = $null; $bd = [double]::MaxValue
    foreach ($s in $sp) {
      $dx = [double]$s.position[0] - $x; $dz = [double]$s.position[2] - $z
      $d = $dx*$dx + $dz*$dz
      if ($d -lt $bd) { $bd = $d; $best = $s }
    }
    $y = $(if ($best) { [double]$best.position[1] } else { 10 })
    if ($x -lt 0 -or $z -lt 0 -or $x -gt $world -or $z -gt $world) { $script:far++ }
    $ys = '{0:0.###}' -f $y
    $points = New-Object System.Collections.Generic.List[object]
    $points.Add(@($bossHash[($c.type + '|' + $c.boss)], [int]$c.boss, 1200, $x, $z))
    if ($c.mobs -ne '(no mobs)') {
      $ah = $addHash[($c.type + '|' + $c.mobs)]
      $lo, $hi = $c.mobs -split '-'
      $lvl = [int][math]::Ceiling(([int]$lo + [int]$hi) / 2)
      foreach ($o in $offs) { $points.Add(@($ah, $lvl, 180, ($x + $o[0]), ($z + $o[1]))) }
    }
    foreach ($pt in $points) {
      $px = '{0:0.###}' -f $pt[3]; $pz = '{0:0.###}' -f $pt[4]
      $pos = "[ $px, $ys, $pz ]"
      if ($sraw.Contains($pos) -or ($pieces -join "`n").Contains($pos)) { continue }
      $pieces += @"
    {
      "districtIndex": 0,
      "hash": 0,
      "hashText": "$($pt[0])",
      "manifestHash": 0,
      "minLevel": $($pt[1]),
      "maxLevel": $($pt[1]),
      "respawnChance": 100,
      "flags": 1,
      "respawnTime": $($pt[2]),
      "version7Extra": 0,
      "hasOptionalFlagBlock": true,
      "nativeFlags": 0,
      "moreFlags": 0,
      "assistanceRadius": 0,
      "unknownOptionalU8": 0,
      "angle": 0,
      "angleW": 1,
      "position": $pos,
      "radius": 1,
      "additionalPoints": [],
      "extensions": null
    }
"@
    }
  }
  if ($pieces.Count) {
    $cut = $sraw.LastIndexOf("  ]")
    if ($cut -lt 0) { throw "Spawns.json $id has no spawn list" }
    $brace = $sraw.LastIndexOf('}', $cut)
    if ($brace -lt 0) { throw "Spawns.json $id has no last spawn" }
    if ($sraw.Substring($brace + 1, [Math]::Min(8, $sraw.Length - $brace - 1)) -notmatch '^\s*,') {
      $sraw = $sraw.Insert($brace + 1, ',')
      $cut = $sraw.LastIndexOf("  ]")
    }
    $sraw = $sraw.Insert($cut, (($pieces -join ",`n") + "`n"))
    [IO.File]::WriteAllText($spath, $sraw, $u)
    $added += $pieces.Count
  }
}
"dyna templatesNew=$($made.Count) bossGroups=$($bossHash.Count) addGroups=$($addHash.Count) spawnsAdded=$added outsideMap=$far"
& "$PSScriptRoot\Build-DynaLoot.ps1" -GameData $GameData
