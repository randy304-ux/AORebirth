# Adds a small Kyr'Ozch typed bio-material (KBTY) chance wherever crew already drop
# Solid Clumps (KZBM Chance 10). Generals keep KZBM 60 and ALGT keeps guaranteed KBTY.
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
                                                "Hash":  "KBTY",
                                                "Repeats":  1,
                                                "Chance":  3,
                                                "LevelMod":  20
                                            }
'@
if ($raw -notmatch [regex]::Escape($needle)) { throw 'crew KZBM Chance 10 pattern not found' }
# Skip rows that already have KBTY immediately after this KZBM block.
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
  $after = $raw.Substring($at + $needle.Length, [Math]::Min(120, $raw.Length - ($at + $needle.Length)))
  if ($after -match 'KBTY') {
    [void]$sb.Append($needle)
  } else {
    [void]$sb.Append($insert)
    $count++
  }
  $idx = $at + $needle.Length
}
[IO.File]::WriteAllText($path, $sb.ToString(), $u)
Write-Output ("crew KBTY Chance=3 added on $count KZBM Chance=10 loot rows")
