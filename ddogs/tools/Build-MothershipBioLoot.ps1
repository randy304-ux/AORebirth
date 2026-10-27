# Build-MothershipBioLoot.ps1 - Unicorn Recruiter TEAM mothership drop rules (Randy 2026-10-26 + addendum).
# Trash: 90% one VP bio; of those, 50% Cell Templates, ~16.7% each Plasmid/Mito/Protein (weighted KVCL children).
# Mothership General hash ALMS: KBMG 2-4 + KVCL x5. City-raid / invasion hash ALGT keeps AI KBTY pool (no KBMG/KVCL).
# Team authored dungeons ZXTE/ZXT1/ZXTH objective -> ALMS. Right-click VP is VictoryPointClumps (Hit 669).
$ErrorActionPreference = 'Stop'
$gd = Join-Path $PSScriptRoot '..\..\AORebirth\GameData' | Resolve-Path
$u = New-Object Text.UTF8Encoding $false

# --- ItemTemplates: VP clumps (weighted) + mothership General typed bios ---
$ipath = Join-Path $gd 'ItemTemplates.json'
$items = [IO.File]::ReadAllText($ipath, $u) -replace "`r`n", "`n"
$kept = @($items -split "`n" | Where-Object { $_ -notmatch '^\s*"(KB1[4-9]|KB2[0-2]|KBMG|KVPC|KVPP|KVPM|KVPR|KVCL)":' })
$at = [Array]::FindIndex($kept, [Predicate[string]]{ param($l) $l -match '^\s*"KBTY":' })
if ($at -lt 0) { throw 'KBTY group not found (run Build-AlienRaidLoot.ps1 first)' }

$extra = @(
  '    "KB14": {"Templates": [265321,265322]},',
  '    "KB15": {"Templates": [265323,265324]},',
  '    "KB16": {"Templates": [265333,265334]},',
  '    "KB17": {"Templates": [265329,265330]},',
  '    "KB18": {"Templates": [265335,265336]},',
  '    "KB19": {"Templates": [265327,265328]},',
  '    "KB20": {"Templates": [265325,265326]},',
  '    "KB21": {"Templates": [265331,265332]},',
  '    "KB22": {"Templates": [266352,266353]},',
  '    "KBMG": {"Children": ["KB01","KB02","KB03","KB04","KB05","KB06","KB07","KB14","KB15","KB16","KB17","KB18","KB19","KB20","KB21","KB22"]},',
  '    "KVPC": {"Templates": [266363,266361]},',
  '    "KVPP": {"Templates": [266354,266355]},',
  '    "KVPM": {"Templates": [266356,266357]},',
  '    "KVPR": {"Templates": [266358,266359]},',
  # 3x Cell + one each higher tier => 50% / ~16.7% / ~16.7% / ~16.7% when Children pick uniformly
  '    "KVCL": {"Children": ["KVPC","KVPC","KVPC","KVPP","KVPM","KVPR"]},'
)
$out = @($kept[0..$at]) + $extra + @($kept[($at + 1)..($kept.Count - 1)])
[IO.File]::WriteAllText($ipath, ($out -join "`n"), $u)

# --- NpcTemplates ---
$npath = Join-Path $gd 'NpcTemplates.json'
$raw = [IO.File]::ReadAllText($npath, $u) -replace "`r`n", "`n"

# Trash VP chance: any prior KVCL Chance 15 (or leftover KBTY Chance 3) -> Chance 90
$raw = $raw.Replace(@'
                                            {
                                                "Hash":  "KBTY",
                                                "Repeats":  1,
                                                "Chance":  3,
                                                "LevelMod":  20
                                            }
'@, @'
                                            {
                                                "Hash":  "KVCL",
                                                "Repeats":  1,
                                                "Chance":  90,
                                                "LevelMod":  20
                                            }
'@)
$raw = $raw.Replace(@'
                                            {
                                                "Hash":  "KVCL",
                                                "Repeats":  1,
                                                "Chance":  15,
                                                "LevelMod":  20
                                            }
'@, @'
                                            {
                                                "Hash":  "KVCL",
                                                "Repeats":  1,
                                                "Chance":  90,
                                                "LevelMod":  20
                                            }
'@)

# Ensure KZBM Chance 10 crew rows have a follow-on KVCL Chance 90
$kzbm10 = @'
                                            {
                                                "Hash":  "KZBM",
                                                "Repeats":  1,
                                                "Chance":  10,
                                                "LevelMod":  20
                                            }
'@
$kzbmWithVp = @'
                                            {
                                                "Hash":  "KZBM",
                                                "Repeats":  1,
                                                "Chance":  10,
                                                "LevelMod":  20
                                            },
                                            {
                                                "Hash":  "KVCL",
                                                "Repeats":  1,
                                                "Chance":  90,
                                                "LevelMod":  20
                                            }
'@
$crewAdded = 0
$sb = New-Object System.Text.StringBuilder
$idx = 0
while ($true) {
  $atK = $raw.IndexOf($kzbm10, $idx)
  if ($atK -lt 0) {
    [void]$sb.Append($raw.Substring($idx))
    break
  }
  [void]$sb.Append($raw.Substring($idx, $atK - $idx))
  $after = $raw.Substring($atK + $kzbm10.Length, [Math]::Min(160, $raw.Length - ($atK + $kzbm10.Length)))
  if ($after -match '"Hash":\s*"KVCL"' -or $after -match '"Hash":\s*"KBTY"') {
    [void]$sb.Append($kzbm10)
  } else {
    [void]$sb.Append($kzbmWithVp)
    $crewAdded++
  }
  $idx = $atK + $kzbm10.Length
}
$raw = $sb.ToString()

$mothershipLoot = @'
                                            {
                                                "Hash":  "KBMG",
                                                "Repeats":  2,
                                                "Chance":  100,
                                                "LevelMod":  20
                                            },
                                            {
                                                "Hash":  "KBMG",
                                                "Repeats":  2,
                                                "Chance":  50,
                                                "LevelMod":  20
                                            },
                                            {
                                                "Hash":  "KVCL",
                                                "Repeats":  5,
                                                "Chance":  100,
                                                "LevelMod":  20
                                            }
'@
$cityRaidBios = @'
                                            {
                                                "Hash":  "KBTY",
                                                "Repeats":  2,
                                                "Chance":  100,
                                                "LevelMod":  20
                                            }
'@

# Split ALGT (city/invasion AI pool) vs ALMS (mothership Ofab/VP pool).
$s = $raw.IndexOf("`n    `"ALGT`":")
$e = $raw.IndexOf("`n    `"", $s + 5)
if ($s -lt 0) { throw 'ALGT block not found' }
# Find next top-level hash after ALGT
$e = [regex]::Match($raw.Substring($s + 1), '\n    "[A-Z0-9]{4}":').Index
if ($e -lt 0) { throw 'end of ALGT block not found' }
$e = $s + 1 + $e
$algtBlock = $raw.Substring($s, $e - $s)

if ($raw -notmatch '"ALMS":') {
  $almsBlock = $algtBlock.Replace('"ALGT":', '"ALMS":')
  # ALMS keeps mothership loot; if ALGT still has KBTY x2, swap to KBMG/KVCL on the ALMS copy only.
  if ($almsBlock.Contains($cityRaidBios) -and -not $almsBlock.Contains('"Hash":  "KBMG"')) {
    $almsBlock = $almsBlock.Replace($cityRaidBios, $mothershipLoot)
  }
  $raw = $raw.Substring(0, $e) + $almsBlock + $raw.Substring($e)
}

# Re-read ALGT / ALMS after possible insert
# ALGT must be city-raid: KBTY x2, no KBMG/KVCL x5
$algtMs = [regex]::Escape($mothershipLoot)
if ($raw -match ('("ALGT":\s*\{[\s\S]*?)' + $algtMs + '([\s\S]*?\n    "(?:ALMS|[A-Z0-9]{4})":)')) {
  # fragile; do string surgery on ALGT section only
}
$s = $raw.IndexOf("`n    `"ALGT`":")
$eAlms = $raw.IndexOf("`n    `"ALMS`":")
if ($eAlms -lt 0) { $eAlms = $raw.IndexOf("`n    `"", $s + 5); $eAlms = $s + 1 + [regex]::Match($raw.Substring($s + 1), '\n    "[A-Z0-9]{4}":').Index }
$algtOnly = $raw.Substring($s, $eAlms - $s)
$algtFixed = $algtOnly
if ($algtFixed.Contains($mothershipLoot)) {
  $algtFixed = $algtFixed.Replace($mothershipLoot, $cityRaidBios)
} elseif ($algtFixed -notmatch '"Hash":\s*"KBTY"') {
  # Insert city bios after the second KZTW guaranteed block if present
  $kztw2 = @'
                                            {
                                                "Hash":  "KZTW",
                                                "Repeats":  2,
                                                "Chance":  100,
                                                "LevelMod":  20
                                            }
'@
  if ($algtFixed.Contains($kztw2) -and -not $algtFixed.Contains($cityRaidBios)) {
    $algtFixed = $algtFixed.Replace($kztw2, $kztw2 + ',' + "`n" + $cityRaidBios)
  }
}
$raw = $raw.Substring(0, $s) + $algtFixed + $raw.Substring($eAlms)

# ALMS must have mothership loot
$sMs = $raw.IndexOf("`n    `"ALMS`":")
$eMs = $sMs + 1 + [regex]::Match($raw.Substring($sMs + 1), '\n    "[A-Z0-9]{4}":').Index
$almsOnly = $raw.Substring($sMs, $eMs - $sMs)
$almsFixed = $almsOnly
if ($almsFixed.Contains($cityRaidBios) -and -not $almsFixed.Contains('"Hash":  "KBMG"')) {
  $almsFixed = $almsFixed.Replace($cityRaidBios, $mothershipLoot)
} elseif (-not $almsFixed.Contains('"Hash":  "KBMG"')) {
  $kztw2 = @'
                                            {
                                                "Hash":  "KZTW",
                                                "Repeats":  2,
                                                "Chance":  100,
                                                "LevelMod":  20
                                            }
'@
  if ($almsFixed.Contains($kztw2)) {
    $almsFixed = $almsFixed.Replace($kztw2, $kztw2 + ',' + "`n" + $mothershipLoot)
  } else { throw 'ALMS missing KZTW and KBMG; cannot attach mothership loot' }
}
$raw = $raw.Substring(0, $sMs) + $almsFixed + $raw.Substring($eMs)

[IO.File]::WriteAllText($npath, $raw, $u)

# Team mothership objectives use ALMS (city invasions keep ALGT)
$apath = Join-Path $gd 'AuthoredDungeons.json'
$a = [IO.File]::ReadAllText($apath, $u)
$a2 = [regex]::Replace($a, '("Hash":\s*"ZXT[E1H]"[\s\S]*?"ObjectiveNpcHash":\s*)"ALGT"', '${1}"ALMS"')
$a2 = [regex]::Replace($a2, '("Hash":\s*"ZXT[E1H]"[\s\S]*?"ObjectiveNpcHash":\s*)"ALGN"', '${1}"ALMS"')
[IO.File]::WriteAllText($apath, $a2, $u)

Write-Output ("mothership bio loot: KVCL weighted 50/16.7; trash Chance=90; crew bare+KVCL=$crewAdded; ALMS split; ZXT* -> ALMS")
