# Ofab hub batch 2: Training Dummy (PUBG) + hide pink Battle Station signup hash bodies.
# PUBG's three hash points on PF 6007 become immortal DPS punching bags.
# The seven BS queue hashes (plus MGOS) sit on content terminals and are removed so only the terminal name/body remains.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$utf8 = New-Object Text.UTF8Encoding $false
$gd = [IO.Path]::GetFullPath($GameData)

# --- 1. PUBG Training Dummy template (immortal, no loot, immobile body) ---
$npath = Join-Path $gd 'NpcTemplates.json'
$raw = [IO.File]::ReadAllText($npath, $utf8)
if ($raw -match '"PUBG"\s*:') {
  Write-Output 'PUBG template already present; leaving NpcTemplates.json bytes unchanged'
} else {
  $nt = $raw | ConvertFrom-Json
  $skel = $nt.AAAA.Templates[0].Stats
  $stats = [ordered]@{}
  foreach ($p in $skel.PSObject.Properties) { $stats[$p.Name] = $p.Value }
  # Attackable (no blue-name), huge HP pool for the bar, immobile ICC-guard body as a punching-bag target.
  $stats['0'] = 0
  $stats['1'] = 100000
  $stats['27'] = 100000
  $stats['33'] = 0
  $stats['54'] = 300
  $stats['359'] = 254118
  $stats['360'] = 110
  $stats['368'] = 0
  $stats['389'] = 0
  $stats['455'] = 0
  $entry = [ordered]@{
    Templates = @(
      [ordered]@{
        Name = 'Training Dummy'
        Level = 300
        Stats = $stats
        Features = @('Immortal', 'NoMove')
        LootTable = @()
        ContentProvenance = [ordered]@{
          Guessed = $true
          Confirmed = $false
          Source = 'Ofab hub PF 6007 PUBG x3 (east wall). Immortal DPS punching bag; mesh 254118 ICC Shuttle Guard body (immobile): guessed'
        }
      }
    )
  }
  $blob = '    "PUBG": ' + (($entry | ConvertTo-Json -Depth 8 -Compress))
  $end = $raw.LastIndexOf('}')
  if ($end -lt 0) { throw 'NpcTemplates.json: missing closing brace' }
  $head = $raw.Substring(0, $end).TrimEnd()
  if (-not $head.EndsWith(',')) { $head = $head + ',' }
  $nl = $(if ($raw.Contains("`r`n")) { "`r`n" } else { "`n" })
  [IO.File]::WriteAllText($npath, ($head + $nl + $blob + $nl + '}' + $nl), $utf8)
  Write-Output 'PUBG Training Dummy template inserted'
}

# --- 2. SpawnSkips: drop pink hash bodies that sit on BS queue content NPCs ---
$skipPath = Join-Path $gd 'SpawnSkips.json'
$skips = [IO.File]::ReadAllText($skipPath, $utf8) | ConvertFrom-Json
$existing = @{}
foreach ($r in @($skips.removedPoints)) {
  $existing["$($r.playfield)|$($r.hash)|$([math]::Round([double]$r.position[0],2))|$([math]::Round([double]$r.position[2],2))"] = 1
}
$removals = @(
  @{ hash = '1TTO'; x = 248.5; y = 6.01; z = 189; reason = 'on top of Battle Station 10-30 (content)' }
  @{ hash = '31TO'; x = 241; y = 6.01; z = 196.5; reason = 'on top of Battle Station 31-60 (content)' }
  @{ hash = '60T1'; x = 241; y = 6.01; z = 181.49962; reason = 'on top of Battle Station 61-100 (content)' }
  @{ hash = '10T1'; x = 225; y = 6.01; z = 196.5; reason = 'on top of Battle Station 101-150 (content)' }
  @{ hash = '15T2'; x = 217.5; y = 6.01; z = 189; reason = 'on top of Battle Station 151-200 (content)' }
  @{ hash = '17TO'; x = 225; y = 6.01; z = 181.5; reason = 'on top of Battle Station 201-214 (content)' }
  @{ hash = '20TO'; x = 231; y = 6.01; z = 189.03943; reason = 'on top of Battle Station 215-220 (content)' }
  @{ hash = 'MGOS'; x = 235; y = 6.01; z = 189; reason = 'orphan pink AAAA in BS queue ring (no content NPC)' }
)
$added = 0
$points = [System.Collections.Generic.List[object]]::new()
foreach ($r in @($skips.removedPoints)) { $points.Add($r) }
foreach ($r in $removals) {
  $key = "6007|$($r.hash)|$([math]::Round([double]$r.x,2))|$([math]::Round([double]$r.z,2))"
  if ($existing[$key]) { continue }
  $points.Add([pscustomobject][ordered]@{
    playfield = 6007
    hash = $r.hash
    position = @([double]$r.x, [double]$r.y, [double]$r.z)
    reason = $r.reason
  })
  $added++
}
$skips.removedPoints = @($points)
[IO.File]::WriteAllText($skipPath, (($skips | ConvertTo-Json -Depth 8) + "`n"), $utf8)

# --- 3. Bind SpawnHash on BS queue content NPCs; keep terminal MonsterData 279184 ---
$hubPath = Join-Path $gd 'PlayfieldContent\6007\Npcs.json'
$hub = [IO.File]::ReadAllText($hubPath, $utf8) | ConvertFrom-Json
$hashByName = @{
  'Battle Station 10-30' = '1TTO'
  'Battle Station 31-60' = '31TO'
  'Battle Station 61-100' = '60T1'
  'Battle Station 101-150' = '10T1'
  'Battle Station 151-200' = '15T2'
  'Battle Station 201-214' = '17TO'
  'Battle Station 215-220' = '20TO'
}
$bound = 0
foreach ($n in @($hub.Npcs)) {
  if ($n.ContentNpcIdentity -ne 'ddogs-bs-queue') { continue }
  $h = $hashByName[[string]$n.Name]
  if (-not $h) { continue }
  $n | Add-Member -Force NoteProperty SpawnHash $h
  if (-not $n.Stats.'359' -or [int]$n.Stats.'359' -ne 279184) {
    $n.Stats | Add-Member -Force NoteProperty '359' 279184
  }
  $bound++
}
[IO.File]::WriteAllText($hubPath, (($hub | ConvertTo-Json -Depth 12) + "`n"), $utf8)

Write-Output ("PUBG=Training Dummy Immortal; spawnSkipsAdded={0}; bsSpawnHashBound={1}" -f $added, $bound)
