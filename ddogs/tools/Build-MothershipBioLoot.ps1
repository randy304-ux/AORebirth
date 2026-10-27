# Build-MothershipBioLoot.ps1 - Unicorn Recruiter TEAM mothership drop rules (Randy 2026-10-26).
# 1) Trash (KZBM Chance 10 crew): VP clumps (KVCL), not numbered typed bios.
# 2) ALGT General: 5 VP clumps + 2-4 mothership numbered bios (KBMG). Remove trash KBTY from 793a0efe.
# 3) Right-click VP is ZoneEngine VictoryPointClumps (Hit 669); this script only wires loot groups.
# Item IDs verified against items.dat (QL1/QL300 pairs). Type 328 included (AO-Universe mothership loot list).
$ErrorActionPreference = 'Stop'
$gd = Join-Path $PSScriptRoot '..\..\AORebirth\GameData' | Resolve-Path
$u = New-Object Text.UTF8Encoding $false

# --- ItemTemplates: VP clumps + mothership General typed bios ---
$ipath = Join-Path $gd 'ItemTemplates.json'
$items = [IO.File]::ReadAllText($ipath, $u) -replace "`r`n", "`n"
$kept = @($items -split "`n" | Where-Object { $_ -notmatch '^\s*"(KB1[4-9]|KB2[0-2]|KBMG|KVPC|KVPP|KVPM|KVPR|KVCL)":' })
$at = [Array]::FindIndex($kept, [Predicate[string]]{ param($l) $l -match '^\s*"KBTY":' })
if ($at -lt 0) { throw 'KBTY group not found (run Build-AlienRaidLoot.ps1 first)' }

$extra = @(
  '    "KB14": {"Templates": [265321,265322]},',  # Type 18 Ofab melee edged/pierce
  '    "KB15": {"Templates": [265323,265324]},',  # Type 34 Ofab blunt
  '    "KB16": {"Templates": [265333,265334]},',  # Type 64 Ofab armor Doc/Eng/Kee/MP
  '    "KB17": {"Templates": [265329,265330]},',  # Type 295 Ofab armor Adv/Enf/MA/Sol
  '    "KB18": {"Templates": [265335,265336]},',  # Type 468 Ofab armor Crat/NT/Trader
  '    "KB19": {"Templates": [265327,265328]},',  # Type 687 Ofab heavy ranged
  '    "KB20": {"Templates": [265325,265326]},',  # Type 812 Ofab light ranged
  '    "KB21": {"Templates": [265331,265332]},',  # Type 935 Ofab armor Agent/Fix/Shade
  '    "KB22": {"Templates": [266352,266353]},',  # Type 328 (AO-U mothership list)
  '    "KBMG": {"Children": ["KB01","KB02","KB03","KB04","KB05","KB06","KB07","KB14","KB15","KB16","KB17","KB18","KB19","KB20","KB21","KB22"]},',
  '    "KVPC": {"Templates": [266363,266361]},',  # Cell Templates QL1=4 VP / QL300=8 VP
  '    "KVPP": {"Templates": [266354,266355]},',  # Plasmid Cultures 4 / 20
  '    "KVPM": {"Templates": [266356,266357]},',  # Mitochondria Samples 4 / 20
  '    "KVPR": {"Templates": [266358,266359]},',  # Protein Mapping Data 4 / 40
  '    "KVCL": {"Children": ["KVPC","KVPP","KVPM","KVPR"]},'
)
# Insert after KBTY line
$out = @($kept[0..$at]) + $extra + @($kept[($at + 1)..($kept.Count - 1)])
[IO.File]::WriteAllText($ipath, ($out -join "`n"), $u)

# --- NpcTemplates: crew trash KBTY -> KVCL; ALGT General loot ---
$npath = Join-Path $gd 'NpcTemplates.json'
$raw = [IO.File]::ReadAllText($npath, $u) -replace "`r`n", "`n"

# Replace crew KBTY Chance 3 (added by Add-ShipCrewBioMats) with VP clumps.
$kbtyCrew = @'
                                            {
                                                "Hash":  "KBTY",
                                                "Repeats":  1,
                                                "Chance":  3,
                                                "LevelMod":  20
                                            }
'@
$kvclCrew = @'
                                            {
                                                "Hash":  "KVCL",
                                                "Repeats":  1,
                                                "Chance":  15,
                                                "LevelMod":  20
                                            }
'@
$crewReplaced = 0
while ($raw.Contains($kbtyCrew)) {
  $raw = $raw.Replace($kbtyCrew, $kvclCrew)
  $crewReplaced++
}

# If Add-ShipCrew never ran / already swapped, ensure KZBM Chance 10 rows have KVCL (not KBTY).
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
                                                "Chance":  15,
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
    # Already has a follow-on row (KVCL preferred; KBTY would have been replaced above).
    [void]$sb.Append($kzbm10)
  } else {
    [void]$sb.Append($kzbmWithVp)
    $crewAdded++
  }
  $idx = $atK + $kzbm10.Length
}
$raw = $sb.ToString()

# ALGT: swap guaranteed KBTY for KBMG 2-4, add 5 VP clumps. Keep other raid rows.
$algtKbty = @'
                                            {
                                                "Hash":  "KBTY",
                                                "Repeats":  2,
                                                "Chance":  100,
                                                "LevelMod":  20
                                            }
'@
$algtGeneral = @'
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
if ($raw -notmatch [regex]::Escape($algtKbty)) {
  # Idempotent re-run: strip previous KBMG/KVCL ALGT inject if present, then require a marker.
  if ($raw -notmatch '"Hash":\s*"KBMG"') { throw 'ALGT KBTY Chance 100 x2 not found and KBMG not present' }
} else {
  $raw = $raw.Replace($algtKbty, $algtGeneral)
}

[IO.File]::WriteAllText($npath, $raw, $u)
Write-Output ("mothership bio loot: crew KBTY->KVCL=$crewReplaced; crew KVCL added on bare KZBM10=$crewAdded; item groups KVCL+KBMG written")
