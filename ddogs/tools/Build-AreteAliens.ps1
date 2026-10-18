# ddogs/tools/Build-AreteAliens.ps1 - Randy's 2026-10-18 Arete alien list. Run after Build-DynaLoot.ps1 (Build-NpcTemplates.ps1 calls it).
# 1. Aggro: every Arete Kyr'ozch alien gets BreedHostility 204 = 1, so it proximity-aggros (the engine already skips players it cons grey to).
# 2. Loot: Kyr'Ozch weapons. AO-Universe ICC guide: Scout Jaax'Sinuh and Specialist Cha'Heru drop Kyr'Ozch weapons, Alien Spider - Zix drops nothing.
#    Ids are the Kyr'Ozch weapon blocks in items.dat (QL1/99/100/199/200/299/300 = base..base+6, AO-Universe/Auno).
#    KZWP = one untyped Kyr'Ozch weapon, KZTW = one typed one. Rates are guessed for a small server:
#    crew 12% untyped + 2% typed, Alien General 50% + 15%. QL = mob level +-20%.
# 3. YZWI, the pink "To Be Determined" inside the crashed ship 8009 (48.97, 49.25), becomes Specialist - Cha'Heru L13
#    (AO-Universe Arete #20: ship aliens L11-13; the client spawn says L15, the spawn level is set to 13 in Build-KarliAlienShip.ps1). Guessed.
# Text edits only, so the rest of NpcTemplates.json keeps its formatting. Idempotent.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData)
$npath = Join-Path $gd 'NpcTemplates.json'
$ipath = Join-Path $gd 'ItemTemplates.json'

# --- item hashes
$untyped = [ordered]@{ KZ01 = 254465; KZ02 = 254486; KZ03 = 254514; KZ04 = 254542; KZ05 = 254563; KZ06 = 254577; KZ07 = 254591; KZ08 = 254612
  KZ09 = 254633; KZ10 = 254647; KZ11 = 254668; KZ12 = 254682; KZ13 = 254696; KZ14 = 254710; KZ15 = 254724; KZ16 = 254738; KZ17 = 254752; KZ18 = 254773; KZ19 = 288658 }
$typed = [ordered]@{ KT01 = 254472; KT02 = 254479; KT03 = 254493; KT04 = 254500; KT05 = 254507; KT06 = 254521; KT07 = 254528; KT08 = 254535
  KT09 = 254549; KT10 = 254556; KT11 = 254570; KT12 = 254584; KT13 = 254598; KT14 = 254605; KT15 = 254619; KT16 = 254626; KT17 = 254640
  KT18 = 254654; KT19 = 254661; KT20 = 254675; KT21 = 254689; KT22 = 254703; KT23 = 254717; KT24 = 254731; KT25 = 254745; KT26 = 254759
  KT27 = 254766; KT28 = 254780; KT29 = 288665 }
# Alien tradeskill drops (wiki.aodb.us Alien Loot; AO forum: Generals drop Kyr'Ozch Viralbots and, rarely, Lead Viralbots). QL1/QL300 pairs from items.dat.
$pairs = [ordered]@{ KZBM = @(247102, 247103); KZVB = @(247113, 247114); KL01 = @(247136, 247137); KL02 = @(247138, 247139); KL03 = @(247140, 247141); KL04 = @(247142, 247143); KL05 = @(247144, 247145); KL06 = @(247146, 247147) }
$mine = '^\s*"(KZWP|KZTW|KZ\d\d|KT\d\d|KZBM|KZVB|KZLV|KL\d\d)":'
$il = [IO.File]::ReadAllLines($ipath, $u) | Where-Object { $_ -notmatch $mine }
if ($il[0].Trim() -ne '{') { throw 'ItemTemplates.json: unexpected first line' }
$new = New-Object System.Collections.Generic.List[string]
foreach ($set in @($untyped, $typed)) {
  foreach ($k in $set.Keys) { $b = $set[$k]; $new.Add(('    "{0}": {{"Templates": [{1}]}},' -f $k, ((0..6 | ForEach-Object { $b + $_ }) -join ','))) }
}
foreach ($k in $pairs.Keys) { $new.Add(('    "{0}": {{"Templates": [{1},{2}]}},' -f $k, $pairs[$k][0], $pairs[$k][1])) }
$new.Add('    "KZLV": {"Children": ["KL01","KL02","KL03","KL04","KL05","KL06"]},')
$new.Add(('    "KZWP": {{"Children": [{0}]}},' -f (($untyped.Keys | ForEach-Object { '"' + $_ + '"' }) -join ',')))
$new.Add(('    "KZTW": {{"Children": [{0}]}},' -f (($typed.Keys | ForEach-Object { '"' + $_ + '"' }) -join ',')))
[IO.File]::WriteAllText($ipath, ([string[]](@($il[0]) + $new + $il[1..($il.Count - 1)]) -join "`n") + "`n", $u)

# --- YZWI template (appended as its own key; replaced on re-run)
$nraw = [IO.File]::ReadAllText($npath, $u) -replace "`r`n", "`n"
$heads = @([regex]::Matches($nraw, '(?m)^    "([A-Z0-9]+)":\s*\{'))
function Block([string]$key) {
  for ($h = 0; $h -lt $script:heads.Count; $h++) {
    if ($script:heads[$h].Groups[1].Value -eq $key) {
      $s = $script:heads[$h].Index
      $e = $(if ($h + 1 -lt $script:heads.Count) { $script:heads[$h + 1].Index } else { $script:nraw.LastIndexOf('}') })
      return @($s, $e)
    }
  }
  $null
}
$yz = Block 'YZWI'
if ($yz) {
  # drop ",\n    "YZWI": {...}" (the comma before the head belongs to the previous key's line end)
  $cut = $nraw.LastIndexOf(',', $yz[0]); $rest = $nraw.Substring($yz[1])
  if ($rest.TrimStart().StartsWith('"')) { $nraw = $nraw.Substring(0, $cut + 1) + "`n" + $rest } else { $nraw = $nraw.Substring(0, $cut) + "`n" + $rest }
}
$nt = $nraw | ConvertFrom-Json
$donor = @($nt.CRDY.Templates) | Sort-Object Level | Select-Object -Last 1
$t = $donor | ConvertTo-Json -Depth 10 | ConvertFrom-Json
$t.Name = "Specialist - Cha'Heru"; $t.Level = 13; $t.Stats.'54' = 13
$t | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = 'CRDY'
  Source = 'Pink "To Be Determined" YZWI inside the crashed ship 8009 (48.97, 49.25). AO-Universe Arete #20 lists ship aliens Zix, Scout Jaax''Sinuh, Specialist Cha''Heru (L11-13); mapped to Cha''Heru L13: guessed' })
$frag = "    `"YZWI`": " + ([pscustomobject]@{ Templates = @($t) } | ConvertTo-Json -Depth 8).Replace("`r`n", "`n")
$end = $nraw.TrimEnd().LastIndexOf('}')
$last = $nraw.LastIndexOf('}', $end - 1)
$nraw = $nraw.Substring(0, $last + 1) + ",`n" + $frag + "`n}`n"
$heads = @([regex]::Matches($nraw, '(?m)^    "([A-Z0-9]+)":\s*\{'))

# --- aggro + loot, per block
$aliens = @('ZIXI','ACFJ','CRDY','ALGN','ZIXK','KSAU','KGRU','KCIU','KGXH','HX1V','HXHR','JXSC','YZWI')
$noWeapons = @('ZIXI','ZIXK','HX1V')
function Loot-Text($rows) {
  $parts = foreach ($r in $rows) {
    $s = '{{"Hash":"{0}","Repeats":{1},"Chance":{2},"LevelMod":{3}' -f $r.Hash, $r.Repeats, $r.Chance, $r.LevelMod
    if ($r.PSObject.Properties['CountMax'] -and $r.CountMax -gt 0) { $s += (',"CountMin":{0},"CountMax":{1}' -f $r.CountMin, $r.CountMax) }
    $s + '}'
  }
  '"LootTable": [' + ($parts -join ',') + ']'
}
$edits = New-Object System.Collections.Generic.List[object]
foreach ($key in $aliens) {
  $b = Block $key
  if (-not $b) { throw "no template $key" }
  $text = $nraw.Substring($b[0], $b[1] - $b[0])
  # 204 in every Stats object of the block
  $text = [regex]::Replace($text, '("Stats"\s*:\s*\{)(\s*)([^{}]*?)(\})', {
    param($m)
    $body = $m.Groups[3].Value
    if ($body -match '"204"\s*:\s*-?\d+') { return $m.Groups[1].Value + $m.Groups[2].Value + ($body -replace '"204"(\s*):(\s*)-?\d+', '"204"$1:${2}1') + $m.Groups[4].Value }
    $m.Groups[1].Value + $m.Groups[2].Value + '"204":  1,' + $m.Groups[2].Value + $body + $m.Groups[4].Value
  })
  # General's corpse credits (Cash 61, paid to the first opener): 500 instead of the crew's 130. Guessed.
  if ($key -eq 'ALGN') { $text = [regex]::Replace($text, '("61"\s*:\s*)\d+', '${1}500') }
  # loot: drop our rows, add the weapon rows
  $out = ''; $from = 0
  while ($true) {
    $at = $text.IndexOf('"LootTable"', $from)
    if ($at -lt 0) { $out += $text.Substring($from); break }
    $br = $text.IndexOf('[', $at); $depth = 0; $stop = -1
    for ($i = $br; $i -lt $text.Length; $i++) { if ($text[$i] -eq '[') { $depth++ } elseif ($text[$i] -eq ']') { $depth--; if ($depth -eq 0) { $stop = $i; break } } }
    if ($stop -lt 0) { throw "unclosed loot on $key" }
    $parsed = ConvertFrom-Json -InputObject $text.Substring($br, $stop - $br + 1)
    $rows = @(@($parsed) | ForEach-Object { $_ } | Where-Object { $_.Hash -notin 'KZWP','KZTW','KZBM','KZVB','KZLV' })
    if ($noWeapons -notcontains $key) {
      $ut = $(if ($key -eq 'ALGN') { 50 } else { 12 }); $ty = $(if ($key -eq 'ALGN') { 15 } else { 2 })
      $rows += [pscustomobject]@{ Hash = 'KZWP'; Repeats = 1; Chance = $ut; LevelMod = 20 }
      $rows += [pscustomobject]@{ Hash = 'KZTW'; Repeats = 1; Chance = $ty; LevelMod = 20 }
      # Solid Clump of Kyr'Ozch Bio-Material: crew 10%, General 60%. General only: Kyr'Ozch Viralbots 25%, one Lead Viralbots 5% (rare). Guessed rates.
      $rows += [pscustomobject]@{ Hash = 'KZBM'; Repeats = 1; Chance = $(if ($key -eq 'ALGN') { 60 } else { 10 }); LevelMod = 20 }
      if ($key -eq 'ALGN') { $rows += [pscustomobject]@{ Hash = 'KZVB'; Repeats = 1; Chance = 25; LevelMod = 20 }; $rows += [pscustomobject]@{ Hash = 'KZLV'; Repeats = 1; Chance = 5; LevelMod = 20 } }
    }
    $lootText = $(if ($noWeapons -contains $key -and @($parsed).Count -eq $rows.Count) { $text.Substring($at, $stop - $at + 1) } else { Loot-Text $rows })
    $out += $text.Substring($from, $at - $from) + $lootText
    $from = $stop + 1
  }
  $edits.Add([pscustomobject]@{ Start = $b[0]; Length = $b[1] - $b[0]; Text = $out })
}
foreach ($e in ($edits | Sort-Object Start -Descending)) { $nraw = $nraw.Remove($e.Start, $e.Length).Insert($e.Start, $e.Text) }
try { $null = $nraw | ConvertFrom-Json } catch { [IO.File]::WriteAllText((Join-Path $env:TEMP 'arete-aliens-bad.json'), $nraw, $u); throw }
[IO.File]::WriteAllText($npath, $nraw, $u)
"arete aliens: aggro=$($aliens.Count) weaponLoot=$($aliens.Count - $noWeapons.Count) itemHashes=$($new.Count)"