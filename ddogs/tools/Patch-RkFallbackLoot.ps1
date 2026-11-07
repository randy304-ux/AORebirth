# ddogs/tools/Patch-RkFallbackLoot.ps1
# Fills empty scrape-rk LootTables with the server's existing level tables only.
# Named dyna-style bosses -> DYB1..DYB8 (same bands/row shape as Build-DynaLoot.ps1).
# Ordinary combat trash -> CMTR/CMHK/OFAB (same common rows used by L25-40 Galway mobs).
# Nonempty plan.loot / Reck rows are left alone. Does not invent item template IDs.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData)
$path = Join-Path $gd 'NpcTemplates.RK.json'
$nt = [IO.File]::ReadAllText($path, $u) | ConvertFrom-Json

function Band-Hash([int]$level) {
  if ($level -le 15) { return 'DYB1' }
  if ($level -le 35) { return 'DYB2' }
  if ($level -le 55) { return 'DYB3' }
  if ($level -le 85) { return 'DYB4' }
  if ($level -le 120) { return 'DYB5' }
  if ($level -le 160) { return 'DYB6' }
  if ($level -le 200) { return 'DYB7' }
  return 'DYB8'
}

function Test-NamedDynaBoss([string]$name, [int]$level) {
  if ([string]::IsNullOrWhiteSpace($name)) { return $false }
  if ($level -lt 1 -or $level -gt 275) { return $false }
  if ($name -match '^(The Ring|Ring Ticket)') { return $false }
  if ($name -match 'Ticket Salesman') { return $false }
  # Live Rubi-Ka dyna bosses use "X the Y" / "X of the Y" names (AO-Universe camps).
  return ($name -match '\bthe\b')
}

$dybRow = {
  param([string]$h)
  [pscustomobject]@{ Hash = $h; Repeats = 1; Chance = 100; LevelMod = 25; CountMin = 5; CountMax = 7 }
}
$commonRows = @(
  [pscustomobject]@{ Hash = 'CMTR'; Repeats = 1; Chance = 50; LevelMod = 20 }
  [pscustomobject]@{ Hash = 'CMHK'; Repeats = 1; Chance = 12; LevelMod = 20 }
  [pscustomobject]@{ Hash = 'OFAB'; Repeats = 1; Chance = 1; LevelMod = 0 }
)

$filledDyb = 0; $filledCommon = 0; $skippedNonempty = 0; $skippedNonCombat = 0
foreach ($p in @($nt.PSObject.Properties)) {
  foreach ($b in @($p.Value.Templates)) {
    if ($null -eq $b) { continue }
    $have = @($b.LootTable | Where-Object { $_ })
    if ($have.Count -gt 0) { $skippedNonempty++; continue }
    $side = 0
    if ($b.Stats -and $b.Stats.PSObject.Properties['33']) { $side = [int]$b.Stats.'33' }
    # Side 3 = monster. Friendlies/guards keep empty tables.
    if ($side -ne 3) { $skippedNonCombat++; continue }
    $L = [int]$b.Level
    $name = [string]$b.Name
    if (Test-NamedDynaBoss $name $L) {
      $b | Add-Member -Force NoteProperty LootTable @(& $dybRow (Band-Hash $L))
      $filledDyb++
    } else {
      $b | Add-Member -Force NoteProperty LootTable @($commonRows | ForEach-Object { $_ | ConvertTo-Json | ConvertFrom-Json })
      $filledCommon++
    }
  }
}

[IO.File]::WriteAllText($path, (($nt | ConvertTo-Json -Depth 12) + "`n"), $u)
"rk-fallback-loot dyb=$filledDyb common=$filledCommon skippedNonempty=$skippedNonempty skippedNonCombat=$skippedNonCombat"
