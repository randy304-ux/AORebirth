# ddogs/tools/Build-DynaLoot.ps1
# Wires ddogs/tools/DynaLoot.proposed.json into source GameData.
# Each Rubi-Ka dyna boss (CampKey boss|...) rolls 5 to 7 items from the level-band pool.
# The pool is that band's signature weapons and armor, plus instruction discs whose QL sits in the band window.
# A nano with no instruction disc keeps its NanoCrystal row (Nullity Sphere, Grid Armor).
# Arete's seven named bosses use the low band plus the two Ring of Luck pieces.
# Adds are not touched. Does not copy into Built\...\GameData.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$GameData = [IO.Path]::GetFullPath($GameData)
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$proposalPath = Join-Path $PSScriptRoot 'DynaLoot.proposed.json'
$itemPath = Join-Path $GameData 'ItemTemplates.json'
$npcPath = Join-Path $GameData 'NpcTemplates.json'
$namesPath = Join-Path $repo 'AORebirth\Libraries\Source\AORebirth.Database\SqlTables\itemnames.sql'
$datPath = Join-Path $GameData 'items.dat'
$areteHashes = @('IEGA','REGD','XUDP','IIVX','SCWS','CMBS','YBSH')

function Read-ItemQls([string]$path, $want) {
  $found = @{}
  if ($want.Count -eq 0) { return $found }
  [void][Reflection.Assembly]::LoadFrom((Join-Path $repo 'tools-temp\zbuild\MsgPack.dll'))
  $fs = [IO.File]::Open($path, 'Open', 'Read', 'ReadWrite')
  $br = New-Object IO.BinaryReader($fs)
  try {
    $verLen = $br.ReadByte()
    [void]$br.ReadBytes($verLen)
    [void]$br.ReadInt32()
    [void]$br.ReadInt32()
    $slices = $br.ReadInt32()
    for ($s = 0; $s -lt $slices -and $found.Count -lt $want.Count; $s++) {
      $size = $br.ReadInt32()
      $blob = $br.ReadBytes($size)
      $ms = New-Object IO.MemoryStream(,$blob)
      [void]$ms.ReadByte(); [void]$ms.ReadByte()
      $ds = New-Object IO.Compression.DeflateStream($ms, [IO.Compression.CompressionMode]::Decompress)
      $out = New-Object IO.MemoryStream
      $ds.CopyTo($out)
      $ds.Dispose(); $ms.Dispose()
      $out.Position = 0
      $unpacker = [MsgPack.Unpacker]::Create($out)
      $list = $unpacker.ReadItemData().AsList()
      foreach ($item in $list) {
        $arr = $item.AsList()
        if ($arr[6].IsNil) { continue }
        $id = [int]$arr[6].AsInt64()
        if ($want.ContainsKey($id) -and -not $found.ContainsKey($id)) {
          $found[$id] = [int]$arr[10].AsInt64()
        }
      }
      $unpacker.Dispose(); $out.Dispose()
    }
  } finally {
    $br.Dispose(); $fs.Dispose()
  }
  $found
}

$sql = [IO.File]::ReadAllText($namesPath)
$discRx = [regex]"\(\s*(\d+)\s*,\s*'((?:[^']|'')*)'\s*,"
$discs = @{}
foreach ($m in $discRx.Matches($sql)) {
  $name = $m.Groups[2].Value.Replace("''", "'")
  if (-not $name.StartsWith('Instruction Disc (')) { continue }
  $id = [int]$m.Groups[1].Value
  if (-not $discs.ContainsKey($name)) { $discs[$name] = New-Object System.Collections.Generic.List[int] }
  if (-not $discs[$name].Contains($id)) { $discs[$name].Add($id) }
}

$proposal = [IO.File]::ReadAllText($proposalPath, $u) | ConvertFrom-Json
$bandDefs = @()
$n = 1
foreach ($b in @($proposal.levelBands)) {
  $weapons = @{}; $armors = @{}
  if ($b.weapons) { foreach ($name in @($b.weapons)) { if ($name) { $weapons[$name] = $true } } }
  if ($b.armors) { foreach ($name in @($b.armors)) { if ($name) { $armors[$name] = $true } } }
  $bandDefs += [pscustomobject]@{
    Id = [string]$b.id
    Hash = ('DYB{0}' -f $n)
    Lo = [int]$b.bossLevelMin
    Hi = [int]$b.bossLevelMax
    QlLo = [int]$b.qlWindow[0]
    QlHi = [int]$b.qlWindow[1]
    Weapons = $weapons
    Armors = $armors
  }
  $n++
}
if ($bandDefs.Count -ne 8) { throw "expected 8 level bands, got $($bandDefs.Count)" }

$want = @{}
foreach ($item in @($proposal.items)) {
  foreach ($id in @($item.templates)) { if ([int]$id -gt 0) { $want[[int]$id] = $true } }
}
foreach ($ids in $discs.Values) { foreach ($id in $ids) { $want[$id] = $true } }
foreach ($id in @(150504,150503,155190,155192,155191,155193,275056,84144,84143,84142,84141)) { $want[$id] = $true }
$qls = Read-ItemQls $datPath $want
$missing = @($want.Keys | Where-Object { -not $qls.ContainsKey($_) })
if ($missing.Count) { throw ("items.dat missing {0} template ids, first {1}" -f $missing.Count, $missing[0]) }

function Span-Of($ids) {
  $vals = @($ids | ForEach-Object { $script:qls[[int]$_] } | Sort-Object -Unique)
  $open = ($vals.Count -le 1)
  [pscustomobject]@{ Min = [int]$vals[0]; Max = [int]$vals[-1]; Open = [bool]$open }
}

function In-Window($span, $band) {
  if ($span.Open) { return ($span.Min -ge $band.QlLo -and $span.Min -le $band.QlHi) }
  return ($span.Max -ge $band.QlLo -and $span.Min -le $band.QlHi)
}

function Covers-Q($span, [int]$q) {
  if ($q -lt $span.Min) { return $false }
  if ($span.Open) { return $true }
  return $q -le $span.Max
}

$drops = @{}
function Add-Drop($key, $name, $kind, $ids, $bands, [bool]$areteOnly) {
  $ids = @($ids | ForEach-Object { [int]$_ } | Where-Object { $_ -gt 0 } | Select-Object -Unique)
  if ($ids.Count -eq 0) { throw "no templates for $name" }
  $hash = 'DL' + $ids[0].ToString('X')
  $span = Span-Of $ids
  if (-not $script:drops.ContainsKey($hash)) {
    $script:drops[$hash] = [pscustomobject]@{
      Hash = $hash; Name = $name; Kind = $kind; Ids = $ids; Span = $span
      Bands = New-Object System.Collections.Generic.HashSet[string]
      AreteOnly = $areteOnly
    }
  }
  $row = $script:drops[$hash]
  if ($areteOnly) { $row.AreteOnly = $true }
  foreach ($b in @($bands)) { if ($b) { [void]$row.Bands.Add([string]$b) } }
}

$discUsed = 0; $crystalFallback = 0; $nanoSkipped = 0
foreach ($item in @($proposal.items)) {
  $kind = [string]$item.kind
  $ids = @($item.templates)
  $span = Span-Of $ids
  if ($kind -eq 'nano') {
    $program = [string]$item.programName
    if (-not $program) { $program = [string]$item.name }
    $label = 'Instruction Disc (' + $program + ')'
    $picked = $null
    if ($discs.ContainsKey($label)) {
      $best = $null; $bestDelta = [int]::MaxValue
      foreach ($id in $discs[$label]) {
        $delta = [math]::Abs($qls[$id] - $span.Min)
        if ($delta -lt $bestDelta) { $bestDelta = $delta; $best = $id }
      }
      $picked = $best
    }
    if ($picked) {
      $discSpan = Span-Of @($picked)
      $hit = @($bandDefs | Where-Object { In-Window $discSpan $_ } | ForEach-Object { $_.Id })
      if ($hit.Count) { Add-Drop $picked $label 'nano' @($picked) $hit $false; $script:discUsed += 1 }
      else { $script:nanoSkipped += 1 }
    } elseif ($item.crystalFound) {
      $hit = @($bandDefs | Where-Object { In-Window $span $_ } | ForEach-Object { $_.Id })
      if ($hit.Count) { Add-Drop $ids[0] ([string]$item.name) 'nano' $ids $hit $false; $script:crystalFallback += 1 }
      else { $script:nanoSkipped += 1 }
    } else { $script:nanoSkipped += 1 }
    continue
  }
  $table = $(if ($kind -eq 'armor') { 'Armors' } else { 'Weapons' })
  $hit = @()
  foreach ($b in $bandDefs) {
    $listed = $b.$table
    if ($listed.ContainsKey([string]$item.name) -and (In-Window $span $b)) { $hit += $b.Id }
  }
  if ($hit.Count) { Add-Drop $ids[0] ([string]$item.name) $kind $ids $hit $false }
}

foreach ($extra in @(
  @{ Name = 'Nano Crystal (Nullity Sphere)'; Ids = @(150504); Kind = 'nano' }
  @{ Name = 'Nano Crystal (Nullity Sphere MK II)'; Ids = @(150503); Kind = 'nano' }
  @{ Name = 'Nano Crystal (Summon Grid Armor Mk I)'; Ids = @(155190); Kind = 'nano' }
  @{ Name = 'Nano Crystal (Summon Grid Armor Mk II)'; Ids = @(155192); Kind = 'nano' }
  @{ Name = 'Nano Crystal (Summon Grid Armor Mk III)'; Ids = @(155191); Kind = 'nano' }
  @{ Name = 'Nano Crystal (Summon Grid Armor Mk IV)'; Ids = @(155193); Kind = 'nano' }
)) {
  $span = Span-Of $extra.Ids
  $hit = @($bandDefs | Where-Object { In-Window $span $_ } | ForEach-Object { $_.Id })
  if ($hit.Count) { Add-Drop $extra.Ids[0] $extra.Name $extra.Kind $extra.Ids $hit $false }
}
$edrcHit = @($bandDefs | ForEach-Object { $_.Id })
Add-Drop 275056 'Empty Data Receptacle Container' 'receptacle' @(275056) $edrcHit $false
Add-Drop 84144 'Ring of Luck - Offensive' 'ring' @(84144,84143) @() $true
Add-Drop 84142 'Ring of Luck - Defensive' 'ring' @(84142,84141) @() $true

foreach ($b in $bandDefs) {
  $members = @($drops.Values | Where-Object { $_.Bands.Contains($b.Id) -and -not $_.AreteOnly })
  if ($members.Count -eq 0) { throw "band $($b.Id) has no drops" }
  for ($q = $b.QlLo; $q -le $b.QlHi; $q++) {
    $ok = $false
    foreach ($m in $members) { if (Covers-Q $m.Span $q) { $ok = $true; break } }
    if (-not $ok) { throw "band $($b.Id) QL $q is not covered by any drop" }
  }
}

$itemLines = New-Object System.Collections.Generic.List[string]
$used = @{}
foreach ($d in ($drops.Values | Sort-Object Hash)) {
  if ($used.ContainsKey($d.Hash)) { throw "duplicate leaf $($d.Hash)" }
  $used[$d.Hash] = $true
  $pair = $d.Ids
  if ($pair.Count -eq 1) { $pair = @($pair[0], $pair[0]) }
  $itemLines.Add(('    "{0}": {{"Templates": [{1},{2}]}},' -f $d.Hash, $pair[0], $pair[1]))
}
foreach ($b in $bandDefs) {
  $kids = @($drops.Values | Where-Object { $_.Bands.Contains($b.Id) -and -not $_.AreteOnly } | ForEach-Object { $_.Hash } | Sort-Object -Unique)
  $itemLines.Add(('    "{0}": {{"Children": [{1}]}},' -f $b.Hash, (($kids | ForEach-Object { '"' + $_ + '"' }) -join ',')))
}
$areteKids = @($drops.Values | Where-Object { ($_.Bands.Contains('L1-15') -and -not $_.AreteOnly) -or $_.AreteOnly } | ForEach-Object { $_.Hash } | Sort-Object -Unique)
$itemLines.Add(('    "DYAR": {{"Children": [{0}]}},' -f (($areteKids | ForEach-Object { '"' + $_ + '"' }) -join ',')))

$ilines = [IO.File]::ReadAllLines($itemPath, $u) | Where-Object { $_ -notmatch '^\s*"(DL[0-9A-F]+|DYB[1-8]|DYAR)":' }
if ($ilines[0].Trim() -ne '{') { throw 'ItemTemplates.json: unexpected first line' }
$have = @{}
foreach ($line in $ilines) {
  $km = [regex]::Match($line, '^\s*"([^"]+)":')
  if ($km.Success) { $have[$km.Groups[1].Value] = $true }
}
foreach ($d in $drops.Values) { if ($have.ContainsKey($d.Hash)) { throw "item hash $($d.Hash) already used" } }
foreach ($h in (@($bandDefs | ForEach-Object { $_.Hash }) + 'DYAR')) { if ($have.ContainsKey($h)) { throw "item hash $h already used" } }
[IO.File]::WriteAllLines($itemPath, [string[]](@($ilines[0]) + $itemLines + $ilines[1..($ilines.Count - 1)]), $u)

function Band-Hash([int]$level) {
  foreach ($b in $script:bandDefs) { if ($level -ge $b.Lo -and $level -le $b.Hi) { return $b.Hash } }
  throw "boss level $level is outside the dyna bands"
}

function Format-Loot($rows) {
  $parts = foreach ($r in $rows) {
    $s = '{{"Hash":"{0}","Repeats":{1},"Chance":{2},"LevelMod":{3}' -f $r.Hash, $r.Repeats, $r.Chance, $r.LevelMod
    if ($r.CountMax -gt 0) { $s += (',"CountMin":{0},"CountMax":{1}' -f $r.CountMin, $r.CountMax) }
    $s + '}'
  }
  '"LootTable": [' + ($parts -join ',') + ']'
}

function New-Rows($pool, $oldText) {
  # The boss corpse is the 5-7 band roll. Previous family or quest rows on that boss are not kept.
  Format-Loot @([pscustomobject]@{ Hash = $pool; Repeats = 1; Chance = 100; LevelMod = 25; CountMin = 5; CountMax = 7 })
}

function Level-Before([string]$text, [int]$at) {
  $from = [Math]::Max(0, $at - 20000)
  $slice = $text.Substring($from, $at - $from)
  $ms = [regex]::Matches($slice, '"Level"\s*:\s*(\d+)')
  if ($ms.Count -eq 0) { throw 'no Level before a dyna loot table' }
  [int]$ms[$ms.Count - 1].Groups[1].Value
}

$npc = [IO.File]::ReadAllText($npcPath, $u)
$edits = New-Object System.Collections.Generic.List[object]
$seenLoot = @{}
$heads = @([regex]::Matches($npc, '(?m)^    "([A-Z0-9]+)":\s*\{'))
for ($h = 0; $h -lt $heads.Count; $h++) {
  $start = $heads[$h].Index
  $end = $(if ($h + 1 -lt $heads.Count) { $heads[$h + 1].Index } else { $npc.Length })
  $name = $heads[$h].Groups[1].Value
  $block = $npc.Substring($start, $end - $start)
  $isArete = $areteHashes -contains $name
  $isBoss = $block -match '"CampKey"\s*:\s*"boss\|'
  if (-not $isArete -and -not $isBoss) { continue }
  $from = $start
  $found = $false
  while ($from -lt $end) {
    $at = $npc.IndexOf('"LootTable"', $from)
    if ($at -lt 0 -or $at -ge $end) { break }
    $br = $npc.IndexOf('[', $at)
    if ($br -lt 0 -or $br -ge $end) { throw "unclosed loot on $name" }
    $depth = 0
    $stop = -1
    for ($i = $br; $i -lt $end; $i++) {
      $c = $npc[$i]
      if ($c -eq '[') { $depth++ }
      elseif ($c -eq ']') {
        $depth--
        if ($depth -eq 0) { $stop = $i; break }
      }
    }
    if ($stop -lt 0) { throw "unclosed loot on $name" }
    $spanStart = $at
    $spanLen = $stop - $at + 1
    $key = [string]$spanStart
    if (-not $seenLoot.ContainsKey($key)) {
      $seenLoot[$key] = $true
      $pool = $(if ($isArete) { 'DYAR' } else { Band-Hash (Level-Before $npc $spanStart) })
      $old = $npc.Substring($spanStart, $spanLen)
      $edits.Add([pscustomobject]@{ Start = $spanStart; Length = $spanLen; Text = (New-Rows $pool $old) })
    }
    $found = $true
    $from = $spanStart + $spanLen
  }
  if (-not $found) { throw "no LootTable on $name" }
}
foreach ($e in ($edits | Sort-Object Start -Descending)) {
  $npc = $npc.Remove($e.Start, $e.Length).Insert($e.Start, $e.Text)
}
[IO.File]::WriteAllText($npcPath, $npc, $u)
$rk = @($edits | Where-Object { $_.Text -notlike '*"DYAR"*' }).Count
$ar = $edits.Count - $rk
"dyna loot leaves=$($drops.Count) discs=$discUsed crystalFallback=$crystalFallback nanosSkipped=$nanoSkipped bosses=$rk arete=$ar"
