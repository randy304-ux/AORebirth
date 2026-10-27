# Build-MothershipBioLoot.ps1 - Unicorn Recruiter TEAM mothership VP/bio loot (Randy).
# Tunables: AORebirth/GameData/MothershipVpLoot.json (edit then re-run this script).
# Trash: independent KVCL rolls (default 90/30/10, max 3); tier mix from TierWeights.
# ALMS = mothership General (KBMG 2-4 + GeneralVpBios). ALGT = city/invasion AI KBTY pool.
# ZXTE/ZXT1/ZXTH -> ALMS. Right-click VP: VictoryPointClumps (Hit 669).
$ErrorActionPreference = 'Stop'
$gd = Join-Path $PSScriptRoot '..\..\AORebirth\GameData' | Resolve-Path
$u = New-Object Text.UTF8Encoding $false
$cfgPath = Join-Path $gd 'MothershipVpLoot.json'
$cfg = Get-Content -LiteralPath $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
$trash = $cfg.TrashVpBios
$gen = $cfg.GeneralVpBios
if (-not $trash -or -not $trash.DropChancesPercent -or -not $trash.TierWeights) {
  throw 'MothershipVpLoot.json missing TrashVpBios.DropChancesPercent / TierWeights'
}
$maxDrops = [int]$trash.MaxDrops
if ($maxDrops -lt 1) { throw 'TrashVpBios.MaxDrops must be >= 1' }
$chances = @($trash.DropChancesPercent | ForEach-Object { [int]$_ })
if ($chances.Count -lt $maxDrops) { throw 'DropChancesPercent shorter than MaxDrops' }
$levelMod = if ($null -ne $trash.LevelMod) { [int]$trash.LevelMod } else { 20 }
$genRepeats = if ($gen -and $null -ne $gen.Repeats) { [int]$gen.Repeats } else { 5 }
$genChance = if ($gen -and $null -ne $gen.ChancePercent) { [int]$gen.ChancePercent } else { 100 }
$genLevelMod = if ($gen -and $null -ne $gen.LevelMod) { [int]$gen.LevelMod } else { 20 }

function Format-LootRow([string]$hash, [int]$repeats, [int]$chance, [int]$mod) {
  return @"
                                            {
                                                "Hash":  "$hash",
                                                "Repeats":  $repeats,
                                                "Chance":  $chance,
                                                "LevelMod":  $mod
                                            }
"@
}

$kvclChildren = New-Object System.Collections.Generic.List[string]
foreach ($name in @('KVPC', 'KVPP', 'KVPM', 'KVPR')) {
  $w = [int]$trash.TierWeights.$name
  if ($w -lt 0) { throw "TierWeights.$name must be >= 0" }
  for ($i = 0; $i -lt $w; $i++) { [void]$kvclChildren.Add('"' + $name + '"') }
}
if ($kvclChildren.Count -lt 1) { throw 'TierWeights sum to zero' }

$trashRows = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $maxDrops; $i++) {
  [void]$trashRows.Add((Format-LootRow 'KVCL' 1 $chances[$i] $levelMod))
}
$trashVpBlock = ($trashRows -join ",`n")

# --- ItemTemplates ---
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
  ('    "KVCL": {"Children": [' + ($kvclChildren -join ',') + ']},')
)
$out = @($kept[0..$at]) + $extra + @($kept[($at + 1)..($kept.Count - 1)])
[IO.File]::WriteAllText($ipath, ($out -join "`n"), $u)

# --- NpcTemplates: trash multi-drop after every KZBM Chance 10 ---
$npath = Join-Path $gd 'NpcTemplates.json'
$raw = [IO.File]::ReadAllText($npath, $u) -replace "`r`n", "`n"

$kzbm10 = @'
                                            {
                                                "Hash":  "KZBM",
                                                "Repeats":  1,
                                                "Chance":  10,
                                                "LevelMod":  20
                                            }
'@
# One Repeats:1 KVCL (or legacy KBTY Chance 3) loot object after the KZBM block.
$followTrash = '(?:,\s*\{\s*"Hash":\s*"(?:KVCL|KBTY)",\s*"Repeats":\s*1,\s*"Chance":\s*\d+,\s*"LevelMod":\s*\d+\s*\})*'
$crewPatched = 0
$sb = New-Object System.Text.StringBuilder
$idx = 0
while ($true) {
  $atK = $raw.IndexOf($kzbm10, $idx)
  if ($atK -lt 0) {
    [void]$sb.Append($raw.Substring($idx))
    break
  }
  [void]$sb.Append($raw.Substring($idx, $atK - $idx))
  [void]$sb.Append($kzbm10)
  [void]$sb.Append(",`n")
  [void]$sb.Append($trashVpBlock)
  $rest = $raw.Substring($atK + $kzbm10.Length)
  $m = [regex]::Match($rest, '^' + $followTrash)
  if (-not $m.Success) { throw 'expected optional KVCL/KBTY follow-on after KZBM Chance 10' }
  $idx = $atK + $kzbm10.Length + $m.Length
  $crewPatched++
}
$raw = $sb.ToString()

$mothershipLoot = @"
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
$(Format-LootRow 'KVCL' $genRepeats $genChance $genLevelMod)
"@
$cityRaidBios = @'
                                            {
                                                "Hash":  "KBTY",
                                                "Repeats":  2,
                                                "Chance":  100,
                                                "LevelMod":  20
                                            }
'@

$s = $raw.IndexOf("`n    `"ALGT`":")
if ($s -lt 0) { throw 'ALGT block not found' }
$e = $s + 1 + [regex]::Match($raw.Substring($s + 1), '\n    "[A-Z0-9]{4}":').Index
$algtBlock = $raw.Substring($s, $e - $s)

if ($raw -notmatch '"ALMS":') {
  $almsBlock = $algtBlock.Replace('"ALGT":', '"ALMS":')
  if ($almsBlock.Contains($cityRaidBios) -and -not $almsBlock.Contains('"Hash":  "KBMG"')) {
    $almsBlock = $almsBlock.Replace($cityRaidBios, $mothershipLoot)
  }
  $raw = $raw.Substring(0, $e) + $almsBlock + $raw.Substring($e)
}

$s = $raw.IndexOf("`n    `"ALGT`":")
$eAlms = $raw.IndexOf("`n    `"ALMS`":")
if ($eAlms -lt 0) { throw 'ALMS block missing after split' }
$algtFixed = $raw.Substring($s, $eAlms - $s)
# Strip any mothership General VP/KBMG block variants from ALGT; ensure KBTY x2.
if ($algtFixed -match '"Hash":\s*"KBMG"') {
  $algtFixed = [regex]::Replace($algtFixed,
    '(?:,\s*)?\{\s*"Hash":\s*"KBMG",\s*"Repeats":\s*\d+,\s*"Chance":\s*\d+,\s*"LevelMod":\s*\d+\s*\}',
    '')
  $algtFixed = [regex]::Replace($algtFixed,
    '(?:,\s*)?\{\s*"Hash":\s*"KVCL",\s*"Repeats":\s*' + $genRepeats + ',\s*"Chance":\s*' + $genChance + ',\s*"LevelMod":\s*\d+\s*\}',
    '')
  if ($algtFixed -notmatch '"Hash":\s*"KBTY"') {
    $kztw2 = @'
                                            {
                                                "Hash":  "KZTW",
                                                "Repeats":  2,
                                                "Chance":  100,
                                                "LevelMod":  20
                                            }
'@
    if ($algtFixed.Contains($kztw2)) {
      $algtFixed = $algtFixed.Replace($kztw2, $kztw2 + ',' + "`n" + $cityRaidBios)
    }
  }
  # Fix double commas from stripping
  $algtFixed = [regex]::Replace($algtFixed, ',\s*,', ',')
}
$raw = $raw.Substring(0, $s) + $algtFixed + $raw.Substring($eAlms)

$sMs = $raw.IndexOf("`n    `"ALMS`":")
$eMs = $sMs + 1 + [regex]::Match($raw.Substring($sMs + 1), '\n    "[A-Z0-9]{4}":').Index
$almsFixed = $raw.Substring($sMs, $eMs - $sMs)
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
  } else { throw 'ALMS missing KZTW and KBMG' }
} else {
  # Refresh General VP row from config (Repeats/Chance may have changed)
  $almsFixed = [regex]::Replace($almsFixed,
    '\{\s*"Hash":\s*"KVCL",\s*"Repeats":\s*\d+,\s*"Chance":\s*\d+,\s*"LevelMod":\s*\d+\s*\}',
    { param($m)
      if ($m.Value -match '"Repeats":\s*1,') { return $m.Value } # leave trash-style alone (should not be on ALMS)
      return (Format-LootRow 'KVCL' $genRepeats $genChance $genLevelMod).Trim()
    })
}
$raw = $raw.Substring(0, $sMs) + $almsFixed + $raw.Substring($eMs)

[IO.File]::WriteAllText($npath, $raw, $u)

$apath = Join-Path $gd 'AuthoredDungeons.json'
$a = [IO.File]::ReadAllText($apath, $u)
$a2 = [regex]::Replace($a, '("Hash":\s*"ZXT[E1H]"[\s\S]*?"ObjectiveNpcHash":\s*)"ALGT"', '${1}"ALMS"')
$a2 = [regex]::Replace($a2, '("Hash":\s*"ZXT[E1H]"[\s\S]*?"ObjectiveNpcHash":\s*)"ALGN"', '${1}"ALMS"')
[IO.File]::WriteAllText($apath, $a2, $u)

$tierBits = @()
foreach ($name in @('KVPC', 'KVPP', 'KVPM', 'KVPR')) {
  $tierBits += ('{0}={1}' -f $name, [int]$trash.TierWeights.$name)
}
Write-Output ("mothership VP: trash chances=[{0}] max={1}; tiers {2}; general KVCL x{3}@{4}; crew KZBM10 sites={5}" -f ($chances[0..($maxDrops-1)] -join ','), $maxDrops, ($tierBits -join ' '), $genRepeats, $genChance, $crewPatched)
