# Adds a level-220 band to a one-band ddogs template so the spawn level can follow mission difficulty
# (NpcTemplateCatalog interpolates between bands; a single band clamps every requested level to it).
# Curve from the Mercenary families (MREN L15 -> L220): HP x41.5, stat 285 20->297, stat 286 7->99.
function Add-AlienHighBand($entry) {
  $low = $entry.Templates | Sort-Object Level | Select-Object -First 1
  if (@($entry.Templates).Count -gt 1) { return $entry }
  $high = $low | ConvertTo-Json -Depth 8 | ConvertFrom-Json
  $f = 220.0 / [Math]::Max(1, $low.Level)
  $high.Level = 220; $high.Stats.'54' = 220
  foreach ($k in '1','27') { if ($low.Stats.$k) { $high.Stats.$k = [int][Math]::Min(2000000000, $low.Stats.$k * 33483.0 / 806 * [Math]::Min(1, 15.0 / [Math]::Max(1,$low.Level)) * [Math]::Max(1, $low.Level / 15.0)) } }
  if ($low.Stats.'285' -ne $null) { $high.Stats.'285' = [int]([Math]::Max(1,$low.Stats.'285') * 297.0 / 20) }
  if ($low.Stats.'286' -ne $null) { $high.Stats.'286' = [int]([Math]::Max(1,$low.Stats.'286') * 99.0 / 7) }
  $entry.Templates = @($low, $high)
  return $entry
}
