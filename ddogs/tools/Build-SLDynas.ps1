# ddogs/tools/Build-SLDynas.ps1
# Elysium, Scheol, Penumbra, and Inferno camps that already have a coordinate, a playfield id,
# and a mesh donor. Same corpse rule as Nascence: 5 to 7 items, respawn 180 s.
# CampKey is elysium| scheol| penumbra| inferno| so Build-DynaLoot's boss| pass does not overwrite them.
# Called at the end of Build-DynaLoot.ps1, after Build-NascenceDynas.ps1. Idempotent.
# Piece leaves are SP + AOID hex and pools are SL + hex, so the Nascence NP/NC rewrite leaves them.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$GameData = [IO.Path]::GetFullPath($GameData)
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$ntPath = Join-Path $GameData 'NpcTemplates.json'
$itemPath = Join-Path $GameData 'ItemTemplates.json'
$namesPath = Join-Path $repo 'AORebirth\Libraries\Source\AORebirth.Database\SqlTables\itemnames.sql'

# Abhan, Bhotaar, Chi, Dom. Completes are crafted and are not in the drop.
# Piece QL is the figure in ddogs/docs/sl-dyna-research.md.
$patternIds = @{
  'Ungulera' = @(242682, 231478, 231479, 231480)
  'The Archbile Queen' = @(242715, 231775, 231776, 231777)
  'The Achbile Guardian' = @(242818, 239690, 239691, 239692)
  'Bellowing Chimera' = @(242709, 231721, 231722, 231723)
  'Thunderous Chimera' = @(242708, 231712, 231713, 231714)
  'Hemut' = @(242699, 231631, 231632, 231633)
  'Khemhet' = @(242700, 231640, 231641, 231642)
  'Borer' = @(242691, 231559, 231560, 231561)
  'Iunmin' = @(242688, 231532, 231533, 231534)
  'Marem' = @(242694, 231586, 231587, 231588)
  'The Dryad Shuffle' = @(242732, 231937, 231938, 231939)
  'The Stupendous Breeder' = @(242811, 239626, 239627, 239628)
  'The Brobdingnagian Mother' = @(242810, 239617, 239618, 239619)
  'The Penumbral Spirit Hunter' = @(242728, 231892, 231893, 231894)
  'The Indomitable Chimera' = @(242783, 234273, 234274, 234275)
  'Pazuzu' = @(242780, 234244, 234245, 234246)
  'Arch Bigot Aliel' = @(242788, 234320, 234321, 234322)
}
$patternQl = @{
  'Ungulera' = 90; 'The Archbile Queen' = 120; 'The Achbile Guardian' = 120
  'Bellowing Chimera' = 125; 'Thunderous Chimera' = 125; 'Hemut' = 125; 'Khemhet' = 125
  'Borer' = 125; 'Iunmin' = 125; 'Marem' = 125; 'The Dryad Shuffle' = 185
  'The Stupendous Breeder' = 185; 'The Brobdingnagian Mother' = 185
  'The Penumbral Spirit Hunter' = 165; 'The Indomitable Chimera' = 220
  'Pazuzu' = 220; 'Arch Bigot Aliel' = 255
}
# Donors already used by Nascence, plus the captured chimera (LLBE) and heckler (MOOM).
# Spider uses WROM, the same stand-in Hwall already uses.
$donorOf = @{
  'hiathlin' = 'ATHL'; 'croaker' = 'CEON'; 'dryad' = 'PODR'; 'spirit' = 'NASI'
  'chimera' = 'LLBE'; 'spider' = 'WROM'; 'leet' = 'LE04'; 'heckler' = 'MOOM'
}
$standIn = @('dryad', 'spider')
$bossPrefix = @{ elysium = 'Y'; scheol = 'S'; penumbra = 'P'; inferno = 'I' }
$addPrefix = @{ elysium = 'W'; scheol = 'T'; penumbra = 'Q'; inferno = 'J' }
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
$anchor = 'DL43270'

$camps = New-Object System.Collections.Generic.List[object]
function Add-Camp($zone, $name, [int]$level, [int]$pf, $x, $z, $model, $patterns, $extra, [bool]$modelGuess, [bool]$bandTop) {
  $script:camps.Add([pscustomobject]@{
    Zone = [string]$zone; Name = [string]$name; Level = $level; Pf = $pf
    X = [double]$x; Z = [double]$z; Model = [string]$model
    Patterns = @($patterns | Where-Object { $_ })
    Extra = @($extra | Where-Object { $_ })
    ModelGuess = $modelGuess; BandTop = $bandTop
  })
}

# Elysium. A place is wired only when the wiki printed a playfield id. Compass labels are not a playfield.
# 1164,1567 is also the minx camp, so that croaker point stays out.
Add-Camp 'elysium' 'Hithalin' 85 4541 966 777 'hiathlin' @('Ungulera') @(246310) $false $false
Add-Camp 'elysium' 'Hithalin' 85 4543 490 1510 'hiathlin' @('Ungulera') @(246310) $false $false
Add-Camp 'elysium' 'Croaker' 115 4541 634 1383 'croaker' @('The Archbile Queen', 'The Achbile Guardian') @(246309) $false $false

# Scheol upper, playfield 4880. Personal names are a set, not pinned to one coordinate, so the mob name is the family.
Add-Camp 'scheol' 'Chimera' 130 4880 1306 1580 'chimera' @('Bellowing Chimera', 'Thunderous Chimera') $null $false $false
Add-Camp 'scheol' 'Chimera' 130 4880 1457 1663 'chimera' @('Bellowing Chimera', 'Thunderous Chimera') $null $false $false
Add-Camp 'scheol' 'Chimera' 130 4880 1439 1813 'chimera' @('Bellowing Chimera', 'Thunderous Chimera') $null $false $false
Add-Camp 'scheol' 'Chimera' 130 4880 1443 1933 'chimera' @('Bellowing Chimera', 'Thunderous Chimera') $null $false $false
Add-Camp 'scheol' 'Croaker' 120 4880 1744 935 'croaker' @('Hemut', 'Khemhet') $null $false $false
Add-Camp 'scheol' 'Croaker' 120 4880 1813 1037 'croaker' @('Hemut', 'Khemhet') $null $false $false
Add-Camp 'scheol' 'Croaker' 120 4880 1866 933 'croaker' @('Hemut', 'Khemhet') $null $false $false
Add-Camp 'scheol' 'Croaker' 120 4880 1930 997 'croaker' @('Hemut', 'Khemhet') $null $false $false
Add-Camp 'scheol' 'Croaker' 120 4880 2007 1080 'croaker' @('Hemut', 'Khemhet') $null $false $false
Add-Camp 'scheol' 'Croaker' 120 4880 1768 1038 'croaker' @('Hemut', 'Khemhet') $null $false $false
Add-Camp 'scheol' 'Spider' 130 4880 1339 1018 'spider' @('Borer') $null $false $false
Add-Camp 'scheol' 'Spider' 130 4880 1377 1077 'spider' @('Borer') $null $false $false
Add-Camp 'scheol' 'Spider' 130 4880 1386 1030 'spider' @('Borer') $null $false $false
Add-Camp 'scheol' 'Spider' 130 4880 1397 1055 'spider' @('Borer') $null $false $false

# Scheol lower, playfield 4881. Greek brink bosses are not here: the wiki and Aquarius coordinates disagree.
Add-Camp 'scheol' 'Leet' 130 4881 790 1173 'leet' $null $null $false $false
Add-Camp 'scheol' 'Leet' 130 4881 792 1174 'leet' $null $null $false $false
Add-Camp 'scheol' 'Croaker' 130 4881 1658 1216 'croaker' @('Iunmin', 'Marem') $null $false $false
Add-Camp 'scheol' 'Croaker' 130 4881 1668 1251 'croaker' @('Iunmin', 'Marem') $null $false $false
Add-Camp 'scheol' 'Croaker' 130 4881 1700 1246 'croaker' @('Iunmin', 'Marem') $null $false $false
Add-Camp 'scheol' 'Croaker' 130 4881 1722 1198 'croaker' @('Iunmin', 'Marem') $null $false $false
Add-Camp 'scheol' 'Croaker' 130 4881 1804 1306 'croaker' @('Iunmin', 'Marem') $null $false $false
Add-Camp 'scheol' 'Croaker' 130 4881 1819 1383 'croaker' @('Iunmin', 'Marem') $null $false $false

# Penumbra. Rows whose playfield id is flagged as a guess stay out.
Add-Camp 'penumbra' 'Skedadding Spirit' 172 4321 2345 1937 'spirit' @('The Penumbral Spirit Hunter') $null $false $false
Add-Camp 'penumbra' 'Vamoosing Spirit' 170 4321 2282 1917 'spirit' @('The Penumbral Spirit Hunter') $null $false $false
Add-Camp 'penumbra' 'Cool' 175 4322 1850 1260 'dryad' @('The Dryad Shuffle') $null $false $true
Add-Camp 'penumbra' 'Crisp' 175 4322 1850 1290 'dryad' @('The Dryad Shuffle') $null $false $false
Add-Camp 'penumbra' 'A Dauntless Dryad' 175 4320 1555 916 'dryad' @('The Stupendous Breeder', 'The Brobdingnagian Mother') $null $false $true
Add-Camp 'penumbra' 'A Fearless Dryad' 175 4320 1490 808 'dryad' @('The Stupendous Breeder', 'The Brobdingnagian Mother') $null $false $true
Add-Camp 'penumbra' 'A Resolute Dryad' 175 4320 1626 325 'dryad' @('The Stupendous Breeder', 'The Brobdingnagian Mother') $null $false $true

# Inferno, playfield 4605. Chimera Drivers have no coordinate. Families with no donor stay out.
# Limnos, Syros, and Crete are marked heckler as a guess in the proposal.
Add-Camp 'inferno' 'Chimera Monitor' 215 4605 1375 560 'chimera' @('The Indomitable Chimera') $null $false $false
Add-Camp 'inferno' 'Chimera Trainer' 210 4605 1430 620 'chimera' @('The Indomitable Chimera') $null $false $false
Add-Camp 'inferno' 'Limnos' 250 4605 3110 2980 'heckler' @('Pazuzu') $null $true $false
Add-Camp 'inferno' 'Syros' 250 4605 3200 2970 'heckler' @('Pazuzu') $null $true $false
Add-Camp 'inferno' 'Crete' 250 4605 3130 3010 'heckler' @('Pazuzu') $null $true $false
Add-Camp 'inferno' 'Pursued Spirit' 215 4605 710 1100 'spirit' @('Arch Bigot Aliel') $null $false $false

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
$itemName = @{}
foreach ($m in $discRx.Matches($sql)) {
  $id = [int]$m.Groups[1].Value
  $name = $m.Groups[2].Value.Replace("''", "'")
  if ($name.StartsWith('Instruction Disc (')) { $nanoIds[$id] = $true }
  if (-not $itemName.ContainsKey($id)) { $itemName[$id] = $name }
}
foreach ($id in @(150504, 150503, 155190, 155192, 155191, 155193)) { $nanoIds[$id] = $true }
$expect = @{ 246309 = 'Cuirass'; 246310 = 'Ring' }
foreach ($id in @($expect.Keys)) {
  if (-not $itemName.ContainsKey($id)) { throw "itemnames missing $id" }
  if ($itemName[$id] -notlike ('*' + $expect[$id] + '*')) { throw "AOID $id is '$($itemName[$id])'" }
}

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

$pools = @{}
$pieceLeaves = @{}
$poolLines = New-Object System.Collections.Generic.List[string]
$poolN = 0
function Pool-For($camp) {
  $pkey = (@($camp.Patterns) | Sort-Object) -join '+'
  $ekey = (@($camp.Extra) | Sort-Object) -join '+'
  $key = '{0}|{1}|{2}|{3}|{4}' -f $camp.Zone, $camp.Name, $camp.Level, $pkey, $ekey
  if ($script:pools.ContainsKey($key)) { return $script:pools[$key] }
  $kids = New-Object System.Collections.Generic.List[string]
  foreach ($p in @($camp.Patterns)) {
    if (-not $script:patternIds.ContainsKey($p)) { throw "no piece ids for $p" }
    foreach ($id in $script:patternIds[$p]) {
      $leaf = 'SP' + ([int]$id).ToString('X')
      if (-not $script:pieceLeaves.ContainsKey($leaf)) {
        $script:pieceLeaves[$leaf] = [int]$id
        $script:poolLines.Add(('    "{0}": {{"Templates": [{1},{1}]}},' -f $leaf, [int]$id))
      }
      $kids.Add($leaf)
    }
  }
  foreach ($id in @($camp.Extra)) {
    $leaf = 'SP' + ([int]$id).ToString('X')
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
  $hash = 'SL' + $script:poolN.ToString('X2')
  $mod = LevelMod-Of $camp
  $script:poolLines.Add(('    "{0}": {{"Children": [{1}]}},' -f $hash, (($uniq | ForEach-Object { '"' + $_ + '"' }) -join ',')))
  $script:pools[$key] = [pscustomobject]@{ Hash = $hash; LevelMod = $mod }
  $script:pools[$key]
}

$bossHash = @{}
$bossLoot = @{}
$addHash = @{}
$made = @()
function New-Leaf($key, $name, [int]$lvl, $model, [bool]$boss, $lootRows, $note, $prefix) {
  if (-not $script:donorOf.ContainsKey($model)) { throw "no donor for $model" }
  $don = $script:donorOf[$model]
  if (-not $script:nt.$don) { throw "donor template $don missing for $model" }
  $hash = $script:byKey[$key]
  if (-not $hash) {
    $hash = Take-Hash $prefix
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
    # Scale stays the donor's. IsDyna applies the 150% dyna scale when the camp key matches.
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
  $gk = '{0}|{1}|{2}|{3}' -f $c.Zone, $c.Name, $c.Level, $pkey
  $pool = Pool-For $c
  if (-not $groupKey.ContainsKey($gk)) {
    $note = "SL dyna. Donor $($donorOf[$c.Model]). "
    if ($c.Model -in $standIn) { $note += 'Stand-in mesh. ' }
    if ($c.ModelGuess) { $note += 'Family is a guess. ' }
    if ($c.BandTop) { $note += 'Level is the top of the guide band. ' }
    if ($pool.LevelMod -gt 25) { $note += "LevelMod $($pool.LevelMod) so the pattern QL is inside the roll. " }
    $note += 'Y from the nearest client spawn. Respawn 180. Loot 5-7.'
    $row = [pscustomobject]@{ Hash = $pool.Hash; Repeats = 1; Chance = 100; LevelMod = $pool.LevelMod; CountMin = 5; CountMax = 7 }
    $groupKey[$gk] = New-Leaf $gk $c.Name $c.Level $c.Model $true @($row) $note $bossPrefix[$c.Zone]
  }
  $bossHash[$gk] = $groupKey[$gk]
  $bossLoot[$bossHash[$gk]] = (Loot-Text @([pscustomobject]@{ Hash = $pool.Hash; Repeats = 1; Chance = 100; LevelMod = $pool.LevelMod; CountMin = 5; CountMax = 7 }))
  $ak = '{0}-add|{1}|{2}' -f $c.Zone, $c.Model, $c.Level
  if (-not $addHash.ContainsKey($ak)) {
    $addHash[$ak] = New-Leaf $ak ($c.Model + ' add') $c.Level $c.Model $false $common ("Family add for a $($c.Zone) camp. Donor $($donorOf[$c.Model]).") $addPrefix[$c.Zone]
  }
}

$ilines = [IO.File]::ReadAllLines($itemPath, $u) | Where-Object { $_ -notmatch '^\s*"(SP[0-9A-F]+|SL[0-9A-F]{2})":' }
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
    if ($body -notmatch '"Templates"\s*:\s*\[') { throw "template $h lost its Templates array" }
    if ($body -notmatch '"LootTable"\s*:\s*\[') { throw "template $h lost its LootTable array" }
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
$added = 0
$lines = New-Object System.Collections.Generic.List[string]
foreach ($id in ($byPf.Keys | Sort-Object)) {
  $spath = Join-Path $GameData "Playfields\$id\Spawns.json"
  if (-not (Test-Path $spath)) { throw "missing spawns $spath" }
  $sraw = [IO.File]::ReadAllText($spath, $u)
  $sp = ($sraw | ConvertFrom-Json).spawns
  $meta = [IO.File]::ReadAllText((Join-Path $GameData "Playfields\$id\metadata.json"), $u) | ConvertFrom-Json
  $worldX = [int]$meta.width * [int]$meta.tileSize
  $worldZ = [int]$meta.height * [int]$meta.tileSize
  $pieces = @(); $seen = @{}
  foreach ($c in $byPf[$id]) {
    $x = [double]$c.X; $z = [double]$c.Z
    $spot = '{0}|{1}|{2}' -f $id, $x, $z
    if ($seen.ContainsKey($spot)) { throw "duplicate spot $spot on $($c.Name)" }
    $seen[$spot] = $true
    if ($x -lt 0 -or $z -lt 0 -or $x -gt $worldX -or $z -gt $worldZ) { throw "$($c.Name) $x,$z is outside playfield $id ($worldX x $worldZ)" }
    $best = $null; $bd = [double]::MaxValue
    foreach ($s in $sp) {
      $dx = [double]$s.position[0] - $x; $dz = [double]$s.position[2] - $z
      $d = $dx*$dx + $dz*$dz
      if ($d -lt $bd) { $bd = $d; $best = $s }
    }
    $y = $(if ($best) { [double]$best.position[1] } else { 10 })
    $ys = '{0:0.###}' -f $y
    $pkey = (@($c.Patterns) | Sort-Object) -join '+'
    $gk = '{0}|{1}|{2}|{3}' -f $c.Zone, $c.Name, $c.Level, $pkey
    $ak = '{0}-add|{1}|{2}' -f $c.Zone, $c.Model, $c.Level
    $points = New-Object System.Collections.Generic.List[object]
    $points.Add(@($bossHash[$gk], [int]$c.Level, $x, $z, $c.Name))
    foreach ($o in $offs) { $points.Add(@($addHash[$ak], [int]$c.Level, ($x + $o[0]), ($z + $o[1]), ($c.Name + ' add'))) }
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
    $lines.Add(('TP pf={0} x={1:0.###} y={2} z={3:0.###} {4} hash={5} mod={6}' -f $id, $x, $ys, $z, $c.Name, $bossHash[$gk], (LevelMod-Of $c)))
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
    [IO.File]::WriteAllText($spath, $sraw, $u)
  }
}
$skips = @(
  'SKIP elysium no playfield id (compass label only): Predator, Mortiig, Dryad'
  'SKIP elysium 1164,1567 and 930,1480: printed on two camps, not resolved (croaker north, minx, Unredeemed Kuir)'
  'SKIP elysium no donor: Eremite, Lost Soul, Minx, Sadistic Soul Dredge, Unredeemed Kuir'
  'SKIP scheol no donor: Beit, Girder, Eremite, Rash, Shadow, Demon, Hydra, Minx, lower spider range, hiathlin range, soul dredges'
  'SKIP scheol Greek brink: wiki and Aquarius coordinates disagree (Naxos, Lesvos, Kalymnos, Sifnos, Poros). Both stay recorded, neither is placed'
  'SKIP adonis: outdoor areas have coordinates and no playfield id (City 4872 vs Abyss 4873). Sinful Souldredge extra coordinates stay unguessed'
  'SKIP penumbra no donor: rafter, stalker, hiisi, eremite, and unnamed valley bosses'
  'SKIP penumbra playfield id is flagged as a guess: Frozen Plateau, west, extreme north, The Pipe'
  'SKIP penumbra no coordinate: Daring Dryad, Skyros, Folegandros, Alonissos, Lefkodo, Maar, Wing, Malah-Eadem'
  'SKIP inferno no coordinate: Chimera Drivers'
  'SKIP inferno no donor: dragon, shadow, afreet, medusa, and the named bosses whose family is blank (including Nyame and the Fester Leila set)'
  'SKIP inferno spirit hunter has no donor (Devoted Spirit Hunter, Bigot Nzaemihiel)'
  'SKIP pandemonium: raids, not dynas'
)
$report = @("sl templatesNew=$($made.Count) pools=$poolN pieces=$($pieceLeaves.Count) spawnsAdded=$added places=$($camps.Count)") + $lines + $skips
$report | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'Build-SLDynas.last.txt')
$report | ForEach-Object { $_ }
