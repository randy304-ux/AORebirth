# Build-AlienRaidLoot.ps1 - low-level Alien Invasion raid loot for the Arete alien ship (Randy 2026-10-25).
# Item ids from the client RDB (QL1/QL300 pairs; the loot roller interpolates the QL from the killer NPC level, LevelMod 20).
# Raid loot per AO-Universe/Auno Alien Invasion: Kyr'Ozch typed weapons, Kyr'Ozch Bio-Material types (weapon specials),
# Pristine/Mutated Bio-Material and the single-layer bio armor (Arithmetic/Enduring/Observant/Spiritual/Strong/Supple).
# Victory Points are not dropped (VP packages need a VP stat/vendor the server does not have).
# Team ship objective: ALGT = ALGN copy (same look/levels) with 2 guaranteed typed weapons + raid loot.
$ErrorActionPreference = 'Stop'
$gd = Join-Path $PSScriptRoot '..\..\AORebirth\GameData' | Resolve-Path
$u = New-Object Text.UTF8Encoding $false

# 1) item groups
$bioTypes = [ordered]@{ T001=247683; T002=247685; T003=247693; T004=247687; T005=247689; T012=247691; T013=247695; T048=288672; T076=247673; T112=247675; T240=247677; T880=247679; T992=247681 }
$lines = New-Object System.Collections.Generic.List[string]
$i = 0; $kbty = @()
foreach ($k in $bioTypes.Keys) { $i++; $h = ('KB{0:D2}' -f $i); $kbty += $h; $lines.Add(('    "{0}": {{"Templates": [{1},{2}]}},' -f $h, $bioTypes[$k], ($bioTypes[$k] + 1))) }
$lines.Add('    "KBTY": {"Children": [' + (($kbty | % { '"' + $_ + '"' }) -join ',') + ']},')
$lines.Add('    "KBPR": {"Templates": [247106,247107]},')
$lines.Add('    "KBMU": {"Templates": [247108,247109]},')
$lines.Add('    "KZPM": {"Children": ["KBPR","KBMU"]},')
$ar = @()
for ($n = 0; $n -lt 36; $n++) { $h = ('KA{0:D2}' -f ($n + 1)); $ar += $h; $lo = 246559 + 2 * $n; $lines.Add(('    "{0}": {{"Templates": [{1},{2}]}},' -f $h, $lo, ($lo + 1))) }
$lines.Add('    "KAAR": {"Children": [' + (($ar | % { '"' + $_ + '"' }) -join ',') + ']},')
$ipath = Join-Path $gd 'ItemTemplates.json'
$items = [IO.File]::ReadAllText($ipath, $u) -replace "`r`n", "`n"
$kept = @($items -split "`n" | Where-Object { $_ -notmatch '^\s*"(KB\d\d|KBTY|KBPR|KBMU|KZPM|KA\d\d|KAAR)":' })
$at = [Array]::FindIndex($kept, [Predicate[string]]{ param($l) $l -match '^\s*"KZTW":' })
if ($at -lt 0) { throw 'KZTW group not found (run Build-AreteAliens.ps1 first)' }
$out = @($kept[0..$at]) + $lines + @($kept[($at + 1)..($kept.Count - 1)])
[IO.File]::WriteAllText($ipath, ($out -join "`n"), $u)

# 2) ALGT = ALGN + raid loot
$npath = Join-Path $gd 'NpcTemplates.json'
$raw = [IO.File]::ReadAllText($npath, $u) -replace "`r`n", "`n"
$raw = [regex]::Replace($raw, '\n    "ALGT":  \{[\s\S]*?\n(?=    "\w{4}":)', "`n")
$s = $raw.IndexOf("`n    `"ALGN`":"); $e = $raw.IndexOf("`n    `"ZIXK`":", $s)
if ($s -lt 0 -or $e -lt 0) { throw 'ALGN/ZIXK block not found' }
$block = $raw.Substring($s, $e - $s).Trim()
$json = $block.Substring($block.IndexOf('{')).TrimEnd(',')
$algn = $json | ConvertFrom-Json
foreach ($t in $algn.Templates) {
  $rows = @($t.LootTable | Where-Object { $_.Hash -notin 'KBTY','KZPM','KAAR' })
  $rows += [pscustomobject]@{ Hash = 'KZTW'; Repeats = 2; Chance = 100; LevelMod = 20 }   # 2 guaranteed typed weapons
  $rows += [pscustomobject]@{ Hash = 'KBTY'; Repeats = 2; Chance = 100; LevelMod = 20 }   # 2 weapon-special bio types
  $rows += [pscustomobject]@{ Hash = 'KZPM'; Repeats = 1; Chance = 60; LevelMod = 20 }    # Pristine or Mutated bio
  $rows += [pscustomobject]@{ Hash = 'KAAR'; Repeats = 1; Chance = 40; LevelMod = 20 }    # one bio armor piece
  $t.LootTable = $rows
}
$frag = '    "ALGT":  ' + ($algn | ConvertTo-Json -Depth 20) + ','
$raw = $raw.Substring(0, $e + 1) + $frag + $raw.Substring($e)
[IO.File]::WriteAllText($npath, $raw, $u)

# 3) team ship specs kill ALGT
$apath = Join-Path $gd 'AuthoredDungeons.json'
$a = [IO.File]::ReadAllText($apath, $u)
$a = [regex]::Replace($a, '("Hash":\s*"ZXT[E1H]"[\s\S]*?"ObjectiveNpcHash":\s*)"ALGN"', '${1}"ALGT"')
[IO.File]::WriteAllText($apath, $a, $u)
Write-Output ("raid loot: KBTY={0} KAAR={1} ALGT rows={2}" -f $kbty.Count, $ar.Count, @($algn.Templates[0].LootTable).Count)
