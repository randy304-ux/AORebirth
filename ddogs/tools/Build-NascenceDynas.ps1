# ddogs/tools/Build-NascenceDynas.ps1
# Nascence unique camps only. Same corpse rule as the Rubi-Ka dynas: 5 to 7 items, respawn 180 s.
# The pool is that camp's pattern pieces (and a pinned unique, when one has an AOID) plus the
# instruction discs already in its level band. Adds keep the common family table. CampKey is
# nascence| so Build-DynaLoot's boss| pass does not overwrite these tables.
# Called at the end of Build-DynaLoot.ps1, after the DYB leaves exist. Idempotent.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$GameData = [IO.Path]::GetFullPath($GameData)
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$ntPath = Join-Path $GameData 'NpcTemplates.json'
$itemPath = Join-Path $GameData 'ItemTemplates.json'
$namesPath = Join-Path $repo 'AORebirth\Libraries\Source\AORebirth.Database\SqlTables\itemnames.sql'

# Piece AOIDs from itemnames.sql. Each piece is one fixed QL (Abhan QL checked against Bhotaar/Chi/Dom).
$patternIds = @{
  'Sabretooth Slicer' = @(242802, 239545, 239546, 239547) # QL 35
  'Maledicta'         = @(242805, 239572, 239573, 239574) # QL 25
  'Wala'              = @(242806, 239581, 239582, 239583) # QL 50
  'Canceroid Cupid'   = @(242801, 239536, 239537, 239538) # QL 40
  'Careening Blight'  = @(242803, 239554, 239555, 239556) # QL 45
  'Careening Death'   = @(242804, 239563, 239564, 239565) # QL 45
  'Tcheser'           = @(242819, 239699, 239700, 239701) # QL 48
  'Upenpet'           = @(242820, 239708, 239709, 239710) # QL 48
}
$patternQl = @{
  'Sabretooth Slicer' = 35; 'Maledicta' = 25; 'Wala' = 50; 'Canceroid Cupid' = 40
  'Careening Blight' = 45; 'Careening Death' = 45; 'Tcheser' = 48; 'Upenpet' = 48
}
# Donor leaf in NpcTemplates. A stand-in is a different creature's mesh.
$donorOf = @{
  'silvertail' = 'SWSI'; 'predator' = 'PDST'; 'malah' = 'ALAH'; 'weaver' = 'VIWE'
  'crippler' = 'CIOG'; 'hiathlin' = 'ATHL'; 'croaker' = 'CEON'; 'spinetooth' = 'SIHA'
  'spirit' = 'NASI'; 'imp' = 'IMDR'; 'leet' = 'LE04'
  'dryad' = 'PODR'; 'mortiig' = 'PIMR'; 'feather' = 'SWSI'; 'claw' = 'PDST'; 'spider' = 'WROM'
  'heckler' = 'MOOM'
}
$standIn = @('dryad', 'mortiig', 'feather', 'claw', 'spider')
$bands = @(
  @{ Hash = 'DYB1'; Lo = 1;   Hi = 15 }
  @{ Hash = 'DYB2'; Lo = 16;  Hi = 35 }
  @{ Hash = 'DYB3'; Lo = 36;  Hi = 55 }
  @{ Hash = 'DYB4'; Lo = 56;  Hi = 85 }
  @{ Hash = 'DYB5'; Lo = 86;  Hi = 120 }
  @{ Hash = 'DYB6'; Lo = 121; Hi = 160 }
  @{ Hash = 'DYB7'; Lo = 161; Hi = 200 }
  @{ Hash = 'DYB8'; Lo = 201; Hi = 275 }
)
# QL 1 receptacle already used by the Rubi-Ka bands. A fixed-QL piece only covers rolls at or above its QL,
# so this anchor is what lets the 5-7 count resolve when the pattern sits at the top of the window.
$anchor = 'DL43270'

$camps = New-Object System.Collections.Generic.List[object]
function Add-Camp($name, [int]$level, [int]$pf, $x, $z, $model, $patterns, $extra, $existing, [bool]$modelGuess) {
  $script:camps.Add([pscustomobject]@{
    Name = [string]$name; Level = $level; Pf = $pf; X = [double]$x; Z = [double]$z
    Model = [string]$model
    Patterns = @($patterns | Where-Object { $_ })
    Extra = @($extra | Where-Object { $_ })
    Existing = [string]$existing
    ModelGuess = $modelGuess
  })
}

# Training ground 4313 is the retired starter. Predator Rager and The Devastator already exist (PRAG, TDVA).
Add-Camp 'Shadowleet of Powah' 3 4313 316 422 'leet' $null $null '' $false
Add-Camp 'Leader of the Pack' 5 4313 364 246 'silvertail' $null $null '' $true
Add-Camp 'Hardened Mortiig' 8 4313 526 212 'mortiig' $null $null '' $false
Add-Camp 'Dryad Watcher' 8 4313 380 459 'dryad' $null $null '' $false
# Frontier 4310. Malah-Aya is the captured QEGO at 976.4, 31.6, 1599.9 (guide 977, 1598).
Add-Camp 'Swift Wind' 10 4310 793 1271 'silvertail' $null $null '' $false
Add-Camp 'Starry Feathers' 15 4310 672 1379 'feather' $null $null '' $true
Add-Camp 'Starry Feathers' 15 4310 637 1293 'feather' $null $null '' $true
Add-Camp 'Rainbow Feathers' 15 4310 889 1250 'feather' $null $null '' $true
Add-Camp 'Papagena' 15 4310 956 1267 'feather' $null $null '' $true
Add-Camp 'The Lord' 20 4310 845 1402 'spirit' $null $null '' $true
Add-Camp 'The Lady' 15 4310 819 1391 'spirit' $null $null '' $true
Add-Camp 'Deadly Predator' 20 4310 738 1587 'predator' @('Sabretooth Slicer') $null '' $false
Add-Camp 'Demonic Subjugator' 20 4310 732 1563 'imp' $null $null '' $false
Add-Camp 'Malah-Aya' 20 4310 976.384 1599.944 'malah' @('Maledicta') $null 'QEGO' $false
Add-Camp 'Hesosas' 18 4310 710 1963 'hiathlin' $null @(226474) '' $true
Add-Camp 'Omathon' 15 4310 803 1748 'hiathlin' $null @(226474) '' $true
Add-Camp 'Deadly Predator' 20 4310 755 1876 'predator' @('Sabretooth Slicer') $null '' $false
Add-Camp 'Deadly Predator' 20 4310 820 1926 'predator' @('Sabretooth Slicer') $null '' $false
Add-Camp 'Hwall' 110 4310 833 1832 'spider' $null $null '' $true
# Swamp / wetlands / core, playfield 4312. Adsunt shares 1702,1576 with Annos; the spawner shifts the second X.
Add-Camp 'The Mountain Claw' 35 4312 1231 1685 'claw' $null $null '' $true
Add-Camp 'Malah-Animos' 26 4312 1557 1755 'malah' $null $null '' $false
Add-Camp 'Malah-Aerarium' 27 4312 1621 1676 'malah' $null $null '' $false
Add-Camp 'Malah-Audisse' 33 4312 1721 1621 'malah' $null $null '' $false
Add-Camp 'Malah-Annos' 40 4312 1702 1576 'malah' $null $null '' $false
Add-Camp 'Malah-Animi' 32 4312 1590 1523 'malah' $null $null '' $false
Add-Camp 'Malah-Alter' 29 4312 1507 1610 'malah' $null $null '' $false
Add-Camp 'Malah-Adsunt' 35 4312 1702 1576 'malah' $null $null '' $false
Add-Camp 'Sashuqa' 30 4312 1446 1466 'spinetooth' @('Wala') $null '' $false
Add-Camp 'Saliwata' 25 4312 1492 1466 'spinetooth' @('Wala') $null '' $false
Add-Camp 'Narunkt' 32 4312 1527 1579 'spinetooth' @('Wala') $null '' $false
Add-Camp 'Kumpari' 28 4312 1550 1539 'spinetooth' @('Wala') $null '' $false
Add-Camp 'Hawilli' 30 4312 1663 1587 'spinetooth' @('Wala') $null '' $false
Add-Camp 'Aquqa' 34 4312 1618 1603 'spinetooth' @('Wala') $null '' $false
Add-Camp 'Waychaw' 29 4312 1595 1634 'spinetooth' @('Wala') $null '' $false
Add-Camp 'Hatskiri' 40 4312 1736 1661 'spinetooth' @('Wala') $null '' $false
Add-Camp 'Qallyawi' 32 4312 1689 1512 'spinetooth' @('Wala') $null '' $false
Add-Camp 'Malah-Adsunt' 35 4312 1462 1223 'malah' $null $null '' $false
Add-Camp 'Malah-Adsunt' 35 4312 1538 1393 'malah' $null $null '' $false
Add-Camp 'Malah-Alinos' 38 4312 1606 1245 'malah' $null $null '' $false
Add-Camp 'Malah-Appi' 37 4312 1723 960 'malah' $null $null '' $false
Add-Camp 'Malah-Annos' 40 4312 1723 829 'malah' $null $null '' $false
Add-Camp 'Malah-Annos' 40 4312 1577 1024 'malah' $null $null '' $false
Add-Camp 'Maynatsk' 37 4312 1767 1135 'spinetooth' $null $null '' $false
Add-Camp 'Direful Weaver' 40 4312 1509 953 'weaver' @('Careening Death', 'Careening Blight') $null '' $false
Add-Camp 'Direful Weaver' 40 4312 1650 987 'weaver' @('Careening Death', 'Careening Blight') $null '' $false
Add-Camp 'Direful Weaver' 40 4312 1030 1028 'weaver' @('Careening Death', 'Careening Blight') $null '' $false
Add-Camp 'Direful Weaver' 40 4312 1795 1056 'weaver' @('Careening Death', 'Careening Blight') $null '' $false
Add-Camp 'Morbid Weaver' 40 4312 1804 865 'weaver' @('Careening Death', 'Careening Blight') $null '' $false
# Wilds / steppe / two mountains, playfield 4311. Ran-Roth-Or, Lord of the Void, and Mesut-Ra already placed.
Add-Camp 'Stunter of Growth' 35 4311 700 1404 'crippler' @('Canceroid Cupid') $null '' $false
Add-Camp 'Ahwere' 40 4311 673 1193 'croaker' $null $null '' $false
Add-Camp 'Heru-Maat' 40 4311 520 1458 'croaker' $null $null '' $false
Add-Camp 'Ehmat' 40 4311 530 1492 'croaker' $null $null '' $false
Add-Camp 'Tormentor of Growth' 28 4311 161 1783 'crippler' @('Canceroid Cupid') $null '' $false
Add-Camp 'Blighter of Growth' 28 4311 154 1589 'crippler' @('Canceroid Cupid') $null '' $false
Add-Camp 'Hateful Weaver' 35 4311 211 1263 'weaver' @('Careening Death', 'Careening Blight') $null '' $false
Add-Camp 'Vile Weaver' 35 4311 221 1166 'weaver' @('Careening Death', 'Careening Blight') $null '' $false
Add-Camp 'Remur-Nefer' 38 4311 251 561 'croaker' @('Tcheser', 'Upenpet') $null '' $false
Add-Camp 'Nesbaneb' 40 4311 143 663 'croaker' @('Tcheser', 'Upenpet') $null '' $false
Add-Camp 'Takheperu' 35 4311 176 748 'croaker' @('Tcheser', 'Upenpet') $null '' $false
# AOWiki Hunting Grounds Steppe run start. The spawn 12 m away is a level-80 MOOM. Not a named unique.
Add-Camp 'Heckler of Metals' 80 4311 170 1950 'heckler' $null $null '' $false

function Band-Of([int]$level) {
  foreach ($b in $script:bands) { if ($level -ge $b.Lo -and $level -le $b.Hi) { return $b.Hash } }
  throw "no dyna band for level $level"
}
function LevelMod-Of($camp) {
  $mod = 25
  $target = 0
  foreach ($p in @($camp.Patterns)) {
    $ql = [int]$script:patternQl[$p]
    if ($ql -gt $target) { $target = $ql }
  }
  $band = Band-Of $camp.Level
  $nanoQl = [int]$script:nanoMinQl[$band]
  if ($nanoQl -gt $target) { $target = $nanoQl }
  if ($target -gt 0) {
    $need = [int][math]::Ceiling(($target * 100.0 / $camp.Level) - 100)
    if ($need -gt $mod) { $mod = $need }
  }
  $mod
}

$sql = [IO.File]::ReadAllText($namesPath)
$discRx = [regex]"\(\s*(\d+)\s*,\s*'((?:[^']|'')*)'\s*,"
$nanoIds = @{}
foreach ($m in $discRx.Matches($sql)) {
  $name = $m.Groups[2].Value.Replace("''", "'")
  if ($name.StartsWith('Instruction Disc (')) { $nanoIds[[int]$m.Groups[1].Value] = $true }
}
foreach ($id in @(150504, 150503, 155190, 155192, 155191, 155193)) { $nanoIds[$id] = $true }

$itemText = [IO.File]::ReadAllText($itemPath, $u)
$nanoByBand = @{}
foreach ($b in $bands) {
  $at = $itemText.IndexOf(('"' + $b.Hash + '":'))
  if ($at -lt 0) { throw "missing item pool $($b.Hash)" }
  $br = $itemText.IndexOf('[', $at)
  $cl = $itemText.IndexOf(']', $br)
  if ($br -lt 0 -or $cl -lt 0) { throw "unclosed pool $($b.Hash)" }
  $body = $itemText.Substring($br + 1, $cl - $br - 1)
  $kids = New-Object System.Collections.Generic.List[string]
  foreach ($hm in [regex]::Matches($body, '"(DL[0-9A-F]+)"')) {
    $hex = $hm.Groups[1].Value.Substring(2)
    $id = [Convert]::ToInt32($hex, 16)
    if ($nanoIds.ContainsKey($id)) { $kids.Add($hm.Groups[1].Value) }
  }
  $nanoByBand[$b.Hash] = @($kids | Select-Object -Unique)
}
if ($itemText.IndexOf(('"' + $anchor + '":')) -lt 0) { throw "missing anchor leaf $anchor" }
$wantQl = @{}
foreach ($b in $bands) { foreach ($leaf in @($nanoByBand[$b.Hash])) { $wantQl[[Convert]::ToInt32($leaf.Substring(2), 16)] = $true } }
$qls = @{}
if ($wantQl.Count -gt 0) {
  [void][Reflection.Assembly]::LoadFrom((Join-Path $repo 'tools-temp\zbuild\MsgPack.dll'))
  $fs = [IO.File]::Open((Join-Path $GameData 'items.dat'), 'Open', 'Read', 'ReadWrite')
  $br = New-Object IO.BinaryReader($fs)
  try {
    $verLen = $br.ReadByte(); [void]$br.ReadBytes($verLen); [void]$br.ReadInt32(); [void]$br.ReadInt32()
    $slices = $br.ReadInt32()
    for ($s = 0; $s -lt $slices -and $qls.Count -lt $wantQl.Count; $s++) {
      $size = $br.ReadInt32(); $blob = $br.ReadBytes($size)
      $ms = New-Object IO.MemoryStream(,$blob); [void]$ms.ReadByte(); [void]$ms.ReadByte()
      $ds = New-Object IO.Compression.DeflateStream($ms, [IO.Compression.CompressionMode]::Decompress)
      $out = New-Object IO.MemoryStream; $ds.CopyTo($out); $ds.Dispose(); $ms.Dispose(); $out.Position = 0
      $unpacker = [MsgPack.Unpacker]::Create($out)
      foreach ($item in $unpacker.ReadItemData().AsList()) {
        $arr = $item.AsList(); if ($arr[6].IsNil) { continue }
        $id = [int]$arr[6].AsInt64()
        if ($wantQl.ContainsKey($id) -and -not $qls.ContainsKey($id)) { $qls[$id] = [int]$arr[10].AsInt64() }
      }
      $unpacker.Dispose(); $out.Dispose()
    }
  } finally { $br.Dispose(); $fs.Dispose() }
}
$nanoMinQl = @{}
foreach ($b in $bands) {
  $min = 0
  foreach ($leaf in @($nanoByBand[$b.Hash])) {
    $id = [Convert]::ToInt32($leaf.Substring(2), 16)
    if (-not $qls.ContainsKey($id)) { throw "items.dat missing nano $id" }
    $q = [int]$qls[$id]
    if ($min -eq 0 -or $q -lt $min) { $min = $q }
  }
  $nanoMinQl[$b.Hash] = $min
}

$curve = @(@(1,20),@(5,115),@(15,504),@(24,500),@(50,2500),@(104,6236),@(139,10032),@(190,16798),@(214,20077),@(275,28000))
function HealthAt([int]$lvl) {
  $lvl = [math]::Max(1, $lvl)
  for ($i = 1; $i -lt $script:curve.Count; $i++) {
    $a0 = [double]$script:curve[$i-1][0]; $a1 = [double]$script:curve[$i-1][1]
    $b0 = [double]$script:curve[$i][0]; $b1 = [double]$script:curve[$i][1]
    if ($lvl -le $b0) {
      $t = ([math]::Log($lvl) - [math]::Log($a0)) / ([math]::Log($b0) - [math]::Log($a0))
      $log = [math]::Log($a1) + $t * ([math]::Log($b1) - [math]::Log($a1))
      return [int][math]::Round([math]::Exp($log))
    }
  }
  [int]$script:curve[-1][1]
}

$nt = [IO.File]::ReadAllText($ntPath, $u) | ConvertFrom-Json
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

$common = @(
  [pscustomobject]@{ Hash='CMTR'; Repeats=1; Chance=40; LevelMod=20 }
  [pscustomobject]@{ Hash='CMHK'; Repeats=1; Chance=8; LevelMod=20 }
  [pscustomobject]@{ Hash='OFAB'; Repeats=1; Chance=1; LevelMod=0 }
)
function Loot-Text($rows) {
  $parts = foreach ($r in $rows) {
    $s = '{{"Hash":"{0}","Repeats":{1},"Chance":{2},"LevelMod":{3}' -f $r.Hash, $r.Repeats, $r.Chance, $r.LevelMod
    if ($r.CountMax -gt 0) { $s += (',"CountMin":{0},"CountMax":{1}' -f $r.CountMin, $r.CountMax) }
    $s + '}'
  }
  '"LootTable": [' + ($parts -join ',') + ']'
}

# One pool per name+level+pattern set. Piece leaves are NP + AOID hex so Build-DynaLoot's DL strip leaves them.
$pools = @{}
$pieceLeaves = @{}
$poolLines = New-Object System.Collections.Generic.List[string]
$poolN = 0
function Pool-For($camp) {
  $pkey = (@($camp.Patterns) | Sort-Object) -join '+'
  $ekey = (@($camp.Extra) | Sort-Object) -join '+'
  $key = '{0}|{1}|{2}|{3}' -f $camp.Name, $camp.Level, $pkey, $ekey
  if ($script:pools.ContainsKey($key)) { return $script:pools[$key] }
  $kids = New-Object System.Collections.Generic.List[string]
  foreach ($p in @($camp.Patterns)) {
    if (-not $script:patternIds.ContainsKey($p)) { throw "no piece ids for $p" }
    foreach ($id in $script:patternIds[$p]) {
      $leaf = 'NP' + ([int]$id).ToString('X')
      if (-not $script:pieceLeaves.ContainsKey($leaf)) {
        $script:pieceLeaves[$leaf] = [int]$id
        $script:poolLines.Add(('    "{0}": {{"Templates": [{1},{1}]}},' -f $leaf, [int]$id))
      }
      $kids.Add($leaf)
    }
  }
  foreach ($id in @($camp.Extra)) {
    $leaf = 'NP' + ([int]$id).ToString('X')
    if (-not $script:pieceLeaves.ContainsKey($leaf)) {
      $script:pieceLeaves[$leaf] = [int]$id
      $script:poolLines.Add(('    "{0}": {{"Templates": [{1},{1}]}},' -f $leaf, [int]$id))
    }
    $kids.Add($leaf)
  }
  $band = Band-Of $camp.Level
  foreach ($n in @($script:nanoByBand[$band])) { $kids.Add($n) }
  $kids.Add($script:anchor)
  $uniq = @($kids | Select-Object -Unique)
  $script:poolN++
  $hash = 'NC' + $script:poolN.ToString('X2')
  $mod = LevelMod-Of $camp
  $script:poolLines.Add(('    "{0}": {{"Children": [{1}]}},' -f $hash, (($uniq | ForEach-Object { '"' + $_ + '"' }) -join ',')))
  $row = [pscustomobject]@{ Hash = $hash; Repeats = 1; Chance = 100; LevelMod = $mod; CountMin = 5; CountMax = 7 }
  $script:pools[$key] = [pscustomobject]@{ Hash = $hash; LevelMod = $mod; Loot = (Loot-Text @($row)); Uniques = (@($camp.Patterns) + @($camp.Extra)).Count }
  $script:pools[$key]
}

$bossHash = @{}
$bossLoot = @{}
$addHash = @{}
$made = @()
function New-Leaf($key, $name, [int]$lvl, $model, [bool]$boss, $lootRows, $note) {
  if (-not $script:donorOf.ContainsKey($model)) { throw "no donor for $model" }
  $don = $script:donorOf[$model]
  if (-not $script:nt.$don) { throw "donor template $don missing for $model" }
  $hash = $script:byKey[$key]
  if (-not $hash) {
    $hash = Take-Hash $(if ($boss) { 'N' } else { 'F' })
    $script:byKey[$key] = $hash
  }
  if (-not $script:nt.$hash) {
    $d = $script:nt.$don.Templates[0]
    $b = $d | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $dl = [math]::Max(1, [int]$d.Level)
    $b.Name = $name; $b.Level = $lvl; $b.Stats.'54' = $lvl
    $hp = [int][math]::Max((HealthAt $lvl), [double]$d.Stats.'1' * $lvl / $dl)
    if ($boss) { $hp = $hp * 4 }
    $b.Stats.'1' = $hp; if ($b.Stats.'27') { $b.Stats.'27' = $hp }
    $b.Stats.'0' = 268964353
    $scale = 100; if ($b.Stats.'360') { $scale = [int]$b.Stats.'360' }
    if ($boss) { $scale = [int][math]::Max($scale, [int]($scale * 1.25)) }
    $b.Stats.'360' = $scale
    $b.Stats | Add-Member -Force NoteProperty '52' ([int]$(if ($boss) { $lvl * 40 } else { $lvl * 8 }))
    if ($b.PSObject.Properties['Features']) { $b.PSObject.Properties.Remove('Features') }
    $b | Add-Member -Force NoteProperty LootTable $lootRows
    $stand = $model -in $script:standIn
    $b | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{
      Guessed = $true; Confirmed = $false; Donor = $don; Source = $note; Levels = "$lvl"; CampKey = $key; StandIn = [bool]$stand
    })
    $script:nt | Add-Member -Force NoteProperty $hash ([pscustomobject]@{ Templates = @($b) })
    $script:made += $hash
  }
  $hash
}

$groupKey = @{}
foreach ($c in $camps) {
  $pkey = (@($c.Patterns) | Sort-Object) -join '+'
  $gk = 'nascence|{0}|{1}|{2}' -f $c.Name, $c.Level, $pkey
  $pool = Pool-For $c
  if ($c.Existing) {
    $bossHash[$gk] = $c.Existing
  } elseif (-not $groupKey.ContainsKey($gk)) {
    $note = "Aquarius Nascence v1.2. Donor $($donorOf[$c.Model]). "
    if ($c.Model -in $standIn) { $note += "Stand-in mesh. " }
    if ($c.ModelGuess) { $note += "Family is a guess. " }
    if ($c.Name -eq 'Heckler of Metals') { $note += 'AOWiki Hunting Grounds Steppe run start, not a named unique. ' }
    if ($pool.LevelMod -gt 25) { $note += "LevelMod $($pool.LevelMod) so the pattern QL is inside the roll. " }
    $note += 'Y from the nearest client spawn.'
    $row = [pscustomobject]@{ Hash = $pool.Hash; Repeats = 1; Chance = 100; LevelMod = $pool.LevelMod; CountMin = 5; CountMax = 7 }
    $groupKey[$gk] = New-Leaf $gk $c.Name $c.Level $c.Model $true @($row) $note
    $bossHash[$gk] = $groupKey[$gk]
  }
  $bossLoot[$bossHash[$gk]] = $pool.Loot
  $ak = 'nascence-add|{0}|{1}' -f $c.Model, $c.Level
  if (-not $addHash.ContainsKey($ak)) {
    $addHash[$ak] = New-Leaf $ak ($c.Model + ' add') $c.Level $c.Model $false $common ("Family add for a Nascence camp. Donor $($donorOf[$c.Model]).")
  }
}

$ilines = [IO.File]::ReadAllLines($itemPath, $u) | Where-Object { $_ -notmatch '^\s*"(NP[0-9A-F]+|NC[0-9A-F]{2})":' }
if ($ilines[0].Trim() -ne '{') { throw 'ItemTemplates.json: unexpected first line' }
$have = @{}
foreach ($line in $ilines) {
  $km = [regex]::Match($line, '^\s*"([^"]+)":')
  if ($km.Success) { $have[$km.Groups[1].Value] = $true }
}
foreach ($line in $poolLines) {
  $km = [regex]::Match($line, '^\s*"([^"]+)":')
  if ($have.ContainsKey($km.Groups[1].Value)) { throw "item hash $($km.Groups[1].Value) already used" }
}
[IO.File]::WriteAllLines($itemPath, [string[]](@($ilines[0]) + $poolLines + $ilines[1..($ilines.Count - 1)]), $u)

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

$npc = [IO.File]::ReadAllText($ntPath, $u)
$edits = New-Object System.Collections.Generic.List[object]
$heads = @([regex]::Matches($npc, '(?m)^    "([A-Z0-9]+)":\s*\{'))
for ($h = 0; $h -lt $heads.Count; $h++) {
  $name = $heads[$h].Groups[1].Value
  if (-not $bossLoot.ContainsKey($name)) { continue }
  $start = $heads[$h].Index
  $end = $(if ($h + 1 -lt $heads.Count) { $heads[$h + 1].Index } else { $npc.Length })
  $at = $npc.IndexOf('"LootTable"', $start)
  if ($at -lt 0 -or $at -ge $end) { throw "no LootTable on $name" }
  $br = $npc.IndexOf('[', $at)
  $depth = 0; $stop = -1
  for ($i = $br; $i -lt $end; $i++) {
    $c = $npc[$i]
    if ($c -eq '[') { $depth++ }
    elseif ($c -eq ']') { $depth--; if ($depth -eq 0) { $stop = $i; break } }
  }
  if ($stop -lt 0) { throw "unclosed loot on $name" }
  $edits.Add([pscustomobject]@{ Start = $at; Length = ($stop - $at + 1); Text = $bossLoot[$name] })
}
foreach ($e in ($edits | Sort-Object Start -Descending)) {
  $npc = $npc.Remove($e.Start, $e.Length).Insert($e.Start, $e.Text)
}
[IO.File]::WriteAllText($ntPath, $npc, $u)

$offs = @(@(14, 0), @(-14, 0), @(0, 14), @(0, -14))
$byPf = @{}
foreach ($c in $camps) {
  if (-not $byPf.ContainsKey($c.Pf)) { $byPf[$c.Pf] = New-Object System.Collections.Generic.List[object] }
  $byPf[$c.Pf].Add($c)
}
$added = 0; $respawnFixed = 0; $shifted = 0
$lines = New-Object System.Collections.Generic.List[string]
foreach ($id in ($byPf.Keys | Sort-Object)) {
  $spath = Join-Path $GameData "Playfields\$id\Spawns.json"
  if (-not (Test-Path $spath)) { throw "missing spawns $spath" }
  $sraw = [IO.File]::ReadAllText($spath, $u)
  $dirty = $false
  $existHashes = @($byPf[$id] | Where-Object { $_.Existing } | ForEach-Object { $_.Existing } | Select-Object -Unique)
  if ($existHashes.Count) {
    $spawnRe = [regex]::new('"hashText":\s*"(?<h>' + (($existHashes | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')"[\s\S]{0,800}?"respawnTime":\s*(?<t>\d+)')
    $hits = @($spawnRe.Matches($sraw))
    for ($mi = $hits.Count - 1; $mi -ge 0; $mi--) {
      $hm = $hits[$mi]
      if ($hm.Groups['t'].Value -eq '180') { continue }
      $tg = $hm.Groups['t']
      $sraw = $sraw.Remove($tg.Index, $tg.Length).Insert($tg.Index, '180')
      $respawnFixed++; $dirty = $true
    }
  }
  $sp = ($sraw | ConvertFrom-Json).spawns
  $meta = [IO.File]::ReadAllText((Join-Path $GameData "Playfields\$id\metadata.json"), $u) | ConvertFrom-Json
  $world = [int]$meta.width * [int]$meta.tileSize
  $pieces = @(); $seen = @{}
  foreach ($c in $byPf[$id]) {
    $x = [double]$c.X; $z = [double]$c.Z
    $spot = '{0}|{1}|{2}' -f $id, $x, $z
    if ($seen.ContainsKey($spot)) { $x += 8; $script:shifted++ }
    $seen[$spot] = $true
    if ($x -lt 0 -or $z -lt 0 -or $x -gt $world -or $z -gt $world) { throw "$($c.Name) $x,$z is outside playfield $id ($world)" }
    $best = $null; $bd = [double]::MaxValue
    foreach ($s in $sp) {
      $dx = [double]$s.position[0] - $x; $dz = [double]$s.position[2] - $z
      $d = $dx*$dx + $dz*$dz
      if ($d -lt $bd) { $bd = $d; $best = $s }
    }
    $y = $(if ($best) { [double]$best.position[1] } else { 10 })
    $ys = '{0:0.###}' -f $y
    $pkey = (@($c.Patterns) | Sort-Object) -join '+'
    $gk = 'nascence|{0}|{1}|{2}' -f $c.Name, $c.Level, $pkey
    $ak = 'nascence-add|{0}|{1}' -f $c.Model, $c.Level
    $points = New-Object System.Collections.Generic.List[object]
    if (-not $c.Existing) { $points.Add(@($bossHash[$gk], [int]$c.Level, $x, $z, $c.Name)) }
    foreach ($o in $offs) { $points.Add(@($addHash[$ak], [int]$c.Level, ($x + $o[0]), ($z + $o[1]), ($c.Name + ' add'))) }
    $bossY = $ys
    foreach ($pt in $points) {
      $px = '{0:0.###}' -f $pt[2]; $pz = '{0:0.###}' -f $pt[3]
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
      "respawnTime": 180,
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
    $where = $(if ($c.Existing) { $c.Existing } else { $bossHash[$gk] })
    $lines.Add(('TP pf={0} x={1:0.###} y={2} z={3:0.###} {4} hash={5} mod={6}' -f $id, $x, $bossY, $z, $c.Name, $where, (LevelMod-Of $c)))
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
    $added += $pieces.Count
    $dirty = $true
  }
  if ($dirty) { [IO.File]::WriteAllText($spath, $sraw, $u) }
}
$report = @("nascence templatesNew=$($made.Count) pools=$poolN pieces=$($pieceLeaves.Count) spawnsAdded=$added respawn180=$respawnFixed coordShift=$shifted") + $lines
$report | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'Build-NascenceDynas.last.txt')
$report | ForEach-Object { $_ }
