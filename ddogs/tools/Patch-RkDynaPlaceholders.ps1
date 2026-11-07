# ddogs/tools/Patch-RkDynaPlaceholders.ps1
# Desert / RK guide-placeholder dynas ("Lizards Dyna") sit on top of SpawnsAdd.rk named bosses.
# 1) Drop Spawns.json D* Dyna points within 12m of a SpawnsAdd.rk named boss (scraped name wins).
# 2) For remaining 1:1 placeholder camps (no nearby SpawnsAdd boss), rename + set scraped MaxHealth
#    and mark LiveScraped so DynaModifiers skips Big x3. Names come only from the scrape CSV.
# Does not invent names or item IDs. Does not deploy.
param(
  [string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'),
  [string]$ScrapeCsv = 'I:\AO\imports\scrape-20261106-ado\raw\scraped_npcs_20261106_Whozewhatzit.csv'
)
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData)
$rkPath = Join-Path $gd 'NpcTemplates.RK.json'
$ntPath = Join-Path $gd 'NpcTemplates.json'
$rk = [IO.File]::ReadAllText($rkPath, $u) | ConvertFrom-Json
$nt = [IO.File]::ReadAllText($ntPath, $u) | ConvertFrom-Json

function Test-NamedBoss([string]$name) {
  if ([string]::IsNullOrWhiteSpace($name)) { return $false }
  if ($name -match '^(The Ring|Ring Ticket)|Ticket Salesman') { return $false }
  return ($name -match '\bthe\b')
}

# --- 1) Suppress dual placeholder bosses covered by SpawnsAdd.rk ---
$removed = 0
$pfDirs = [IO.Directory]::GetDirectories((Join-Path $gd 'PlayfieldContent'))
foreach ($dir in $pfDirs) {
  $addPath = Join-Path $dir 'SpawnsAdd.rk.json'
  if (-not (Test-Path $addPath)) { continue }
  $pf = [int][IO.Path]::GetFileName($dir)
  $spPath = Join-Path $gd "Playfields\$pf\Spawns.json"
  if (-not (Test-Path $spPath)) { continue }
  $add = [IO.File]::ReadAllText($addPath, $u) | ConvertFrom-Json
  $sp = [IO.File]::ReadAllText($spPath, $u) | ConvertFrom-Json
  $bossAdds = @()
  foreach ($s in @($add.spawns)) {
    $h = [string]$s.hashText
    $tpl = $rk.$h
    if (-not $tpl) { continue }
    $nm = [string]$tpl.Templates[0].Name
    if (-not (Test-NamedBoss $nm)) { continue }
    $bossAdds += [pscustomobject]@{ X = [double]$s.position[0]; Z = [double]$s.position[2]; Hash = $h; Name = $nm }
  }
  if ($bossAdds.Count -eq 0) { continue }
  $kept = New-Object System.Collections.Generic.List[object]
  $changed = $false
  foreach ($s in @($sp.spawns)) {
    $h = [string]$s.hashText
    $tpl = $nt.$h
    $nm = if ($tpl -and $tpl.Templates) { [string]$tpl.Templates[0].Name } else { '' }
    $isPlaceholderDyna = ($h -match '^D[0-9A-F]{3}$') -and ($nm -match '\bdyna\b')
    if ($isPlaceholderDyna) {
      $x = [double]$s.position[0]; $z = [double]$s.position[2]
      $hit = $false
      foreach ($b in $bossAdds) {
        $dx = $b.X - $x; $dz = $b.Z - $z
        if ([math]::Sqrt($dx * $dx + $dz * $dz) -le 12.0) { $hit = $true; break }
      }
      if ($hit) { $removed++; $changed = $true; continue }
    }
    $kept.Add($s) | Out-Null
  }
  if ($changed) {
    $sp.spawns = @($kept)
    [IO.File]::WriteAllText($spPath, (($sp | ConvertTo-Json -Depth 8) + "`n"), $u)
  }
}

# --- 2) Rename leftover 1:1 placeholders from scrape (unique name per hash) ---
if (-not (Test-Path $ScrapeCsv)) {
  "rk-dyna-placeholders removedDual=$removed renameSkipped=no-scrape-csv"
  return
}
function Read-CsvMap([string]$path) {
  $rows = New-Object System.Collections.Generic.List[object]
  $sr = [IO.StreamReader]::new($path, $u)
  try {
    $header = $sr.ReadLine()
    if (-not $header) { return @() }
    # naive CSV split sufficient for this scrape export (quoted fields rare in these cols)
    $cols = $header.Split(',')
    while ($null -ne ($line = $sr.ReadLine())) {
      if ([string]::IsNullOrWhiteSpace($line)) { continue }
      $parts = $line.Split(',')
      if ($parts.Count -lt $cols.Count) { continue }
      $o = [ordered]@{}
      for ($i = 0; $i -lt $cols.Count; $i++) { $o[$cols[$i]] = $parts[$i] }
      $rows.Add([pscustomobject]$o) | Out-Null
    }
  } finally { $sr.Dispose() }
  $rows
}
$scrape = @(Read-CsvMap $ScrapeCsv | Where-Object { $_.IsNpc -eq '1' -or $_.IsNpc -eq 'True' })

$renamed = 0
# Only Newland Desert (565) leftovers for this pass — other PFs follow once Mike accepts the pattern.
$sp565 = Join-Path $gd 'Playfields\565\Spawns.json'
if (Test-Path $sp565) {
  $sp = [IO.File]::ReadAllText($sp565, $u) | ConvertFrom-Json
  $byHash = @{}
  foreach ($s in @($sp.spawns)) {
    $h = [string]$s.hashText
    if ($h -notmatch '^D[0-9A-F]{3}$') { continue }
    $tpl = $nt.$h
    if (-not $tpl -or -not $tpl.Templates) { continue }
    $nm = [string]$tpl.Templates[0].Name
    if ($nm -notmatch '\bdyna\b') { continue }
    if (-not $byHash.ContainsKey($h)) { $byHash[$h] = New-Object System.Collections.Generic.List[object] }
    $byHash[$h].Add($s) | Out-Null
  }
  foreach ($h in @($byHash.Keys)) {
    $pts = @($byHash[$h])
    $band = $nt.$h.Templates[0]
    $md = 0
    if ($band.Stats -and $band.Stats.PSObject.Properties['359']) { $md = [int]$band.Stats.'359' }
    $names = New-Object 'System.Collections.Generic.HashSet[string]'
    $bestHp = 0; $bestL = 0; $bestName = $null
    foreach ($s in $pts) {
      $x = [double]$s.position[0]; $z = [double]$s.position[2]
      $best = $null; $bestD = 1e9
      foreach ($r in $scrape) {
        if ([int]$r.PlayfieldID -ne 565) { continue }
        if ($md -gt 0 -and [int]$r.MonsterData -ne $md) { continue }
        $d = [math]::Sqrt(([double]$r.X - $x) * ([double]$r.X - $x) + ([double]$r.Z - $z) * ([double]$r.Z - $z))
        if ($d -lt $bestD -and $d -le 40) { $bestD = $d; $best = $r }
      }
      if (-not $best) { $names.Add('__unmatched__') | Out-Null; continue }
      [void]$names.Add([string]$best.Name)
      $bestName = [string]$best.Name
      $bestHp = [int]$best.MaxHealth
      $bestL = [int]$best.Level
    }
    # Only rename when every remaining spawn for this hash maps to the same scraped name.
    if ($names.Count -ne 1 -or $names.Contains('__unmatched__') -or [string]::IsNullOrWhiteSpace($bestName)) { continue }
    if ($bestName -eq $band.Name) { continue }
    $band.Name = $bestName
    if ($bestL -gt 0) { $band.Level = $bestL }
    if ($bestHp -gt 0) {
      $band.Stats | Add-Member -Force NoteProperty '1' $bestHp
      $band.Stats | Add-Member -Force NoteProperty '27' $bestHp
    }
    $band | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{
        Guessed = $false
        Source = 'scrape-rk-20261106'
        Note = "placeholder rename from live scrape; was guide Dyna name; LiveScraped skips Big"
        CampKey = $(if ($band.ContentProvenance) { $band.ContentProvenance.CampKey } else { $null })
      })
    # Hash-level provenance so IsLiveScrape matches (Source scrape-rk-20261106).
    $nt.$h | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{
        Guessed = $false
        Source = 'scrape-rk-20261106'
        Note = 'placeholder renamed from live scrape'
      })
    $renamed++
  }
  [IO.File]::WriteAllText($ntPath, (($nt | ConvertTo-Json -Depth 12) + "`n"), $u)
}

"rk-dyna-placeholders removedDual=$removed renamed1to1=$renamed"
