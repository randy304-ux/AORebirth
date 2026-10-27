# DEPRECATED: prefer Build-MothershipBioLoot.ps1.
# Kept so old docs still run something safe: adds VP clumps (KVCL) after crew KZBM Chance 10
# instead of the old numbered KBTY Chance 3 (removed — Generals only via KBMG on ALGT).
$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '..\..\AORebirth\GameData\NpcTemplates.json' | Resolve-Path
$u = New-Object Text.UTF8Encoding $false
$raw = [IO.File]::ReadAllText($path, $u)
$needle = @'
                                            {
                                                "Hash":  "KZBM",
                                                "Repeats":  1,
                                                "Chance":  10,
                                                "LevelMod":  20
                                            }
'@
$insert = @'
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
if ($raw -notmatch [regex]::Escape($needle)) { throw 'crew KZBM Chance 10 pattern not found' }
$count = 0
$sb = New-Object System.Text.StringBuilder
$idx = 0
while ($true) {
  $at = $raw.IndexOf($needle, $idx)
  if ($at -lt 0) {
    [void]$sb.Append($raw.Substring($idx))
    break
  }
  [void]$sb.Append($raw.Substring($idx, $at - $idx))
  $after = $raw.Substring($at + $needle.Length, [Math]::Min(160, $raw.Length - ($at + $needle.Length)))
  if ($after -match 'KVCL|KBTY') {
    [void]$sb.Append($needle)
  } else {
    [void]$sb.Append($insert)
    $count++
  }
  $idx = $at + $needle.Length
}
[IO.File]::WriteAllText($path, $sb.ToString(), $u)
Write-Output ("crew KVCL Chance=15 added on $count KZBM Chance=10 loot rows (use Build-MothershipBioLoot.ps1 for full setup)")
