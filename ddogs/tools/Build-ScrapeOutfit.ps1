# Build-ScrapeOutfit.ps1 - apply scraped SCFU outfit (texture places 0-4, meshes, gloves) into NpcTemplates.Appearance.json.
# Consumes AO_NPC_Scraper CSV columns (added in outfit bump): Textures, Meshes, EquipmentGloves.
# Old columns stay compatible; rows without the new columns are skipped.
# Textures format: place:id;place:id;...  (place 0 = hands/gloves — required to avoid green hands)
# Meshes format:   position:id:overrideTextureId:layer;...
# EquipmentGloves: itemId;itemId;... (worn glove/hand items when visible; written as Equipment pairs)
# Rerunnable. Does not invent outfits — only applies scraped values.
param(
  [string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'),
  [string]$ScrapeDir = $(if ($env:SCRAPE_OUTFIT_DIR) { $env:SCRAPE_OUTFIT_DIR } else { 'I:\AO\imports\scrape-20261105\extracted\output' }),
  [switch]$WhatIf
)
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData)
$appPath = Join-Path $gd 'NpcTemplates.Appearance.json'
if (-not (Test-Path $appPath)) { throw "missing $appPath" }
if (-not (Test-Path $ScrapeDir)) { throw "missing scrape dir $ScrapeDir" }

function Parse-CsvFile([string]$path) {
  $rows = New-Object System.Collections.Generic.List[object]
  $sr = [IO.StreamReader]::new($path, $u)
  try {
    $header = $sr.ReadLine()
    if (-not $header) { return @() }
    $cols = $header.Split(',')
    while ($null -ne ($line = $sr.ReadLine())) {
      if ([string]::IsNullOrWhiteSpace($line)) { continue }
      # Simple CSV (scraper does not quote commas inside outfit fields; fields use ; and :)
      $parts = $line.Split(',')
      if ($parts.Length -lt $cols.Length) { continue }
      $o = [ordered]@{}
      for ($i = 0; $i -lt $cols.Length; $i++) { $o[$cols[$i]] = $parts[$i] }
      [void]$rows.Add([pscustomobject]$o)
    }
  } finally { $sr.Close() }
  return ,$rows.ToArray()
}

function Parse-Textures([string]$s) {
  if ([string]::IsNullOrWhiteSpace($s)) { return @() }
  $out = New-Object System.Collections.Generic.List[object]
  foreach ($tok in ($s -split ';')) {
    if ([string]::IsNullOrWhiteSpace($tok)) { continue }
    $p = $tok.Split(':')
    if ($p.Length -lt 2) { continue }
    $place = 0; $id = 0
    if (-not [int]::TryParse($p[0], [ref]$place)) { continue }
    if (-not [int]::TryParse($p[1], [ref]$id)) { continue }
    if ($id -le 0) { continue }
    [void]$out.Add([pscustomobject]@{ Id = $id; Place = $place })
  }
  return ,$out.ToArray()
}

function Parse-Meshes([string]$s) {
  if ([string]::IsNullOrWhiteSpace($s)) { return @() }
  $out = New-Object System.Collections.Generic.List[object]
  foreach ($tok in ($s -split ';')) {
    if ([string]::IsNullOrWhiteSpace($tok)) { continue }
    $p = $tok.Split(':')
    if ($p.Length -lt 4) { continue }
    $pos = 0; $id = 0; $ovr = 0; $layer = 0
    if (-not [int]::TryParse($p[0], [ref]$pos)) { continue }
    if (-not [int]::TryParse($p[1], [ref]$id)) { continue }
    if (-not [int]::TryParse($p[2], [ref]$ovr)) { continue }
    if (-not [int]::TryParse($p[3], [ref]$layer)) { continue }
    if ($id -le 0) { continue }
    [void]$out.Add([pscustomobject]@{ Id = $id; Position = $pos; OverrideTextureId = $ovr; Layer = $layer })
  }
  return ,$out.ToArray()
}

function Parse-Gloves([string]$s) {
  if ([string]::IsNullOrWhiteSpace($s)) { return @() }
  $ids = New-Object System.Collections.Generic.List[int]
  foreach ($tok in ($s -split ';')) {
    $id = 0
    if ([int]::TryParse($tok, [ref]$id) -and $id -gt 0) { [void]$ids.Add($id) }
  }
  return ,($ids | ForEach-Object { ,[int[]]@($_, $_) })
}

function Has-Place0($textures) {
  foreach ($t in @($textures)) { if ([int]$t.Place -eq 0 -and [int]$t.Id -gt 0) { return $true } }
  return $false
}

# Latest outfit per Name|MonsterData from scrape CSVs that carry the new columns.
$byKey = @{}
$csvFiles = @(Get-ChildItem -LiteralPath $ScrapeDir -Filter 'scraped_npcs_*.csv' -File -ErrorAction SilentlyContinue)
foreach ($f in $csvFiles) {
  $rows = Parse-CsvFile $f.FullName
  if ($rows.Count -lt 1) { continue }
  $sample = $rows[0]
  if (-not ($sample.PSObject.Properties.Name -contains 'Textures')) { continue }
  foreach ($r in $rows) {
    if ($r.Kind -and $r.Kind -ne 'npc') { continue }
    if ($r.IsNpc -and $r.IsNpc -ne '1') { continue }
    if ($r.IsPet -eq '1') { continue }
    if ([string]::IsNullOrWhiteSpace($r.Name)) { continue }
    $tex = Parse-Textures $r.Textures
    $mesh = Parse-Meshes $r.Meshes
    $gloves = Parse-Gloves $r.EquipmentGloves
    if (@($tex).Count -lt 1 -and @($mesh).Count -lt 1 -and @($gloves).Count -lt 1) { continue }
    $md = 0; [void][int]::TryParse([string]$r.MonsterData, [ref]$md)
    $key = ($r.Name + '|' + $md)
    $prev = $byKey[$key]
    if (-not $prev -or [string]$r.Timestamp -gt [string]$prev.ts) {
      $byKey[$key] = [pscustomobject]@{
        ts = $r.Timestamp; name = $r.Name; md = $md
        tex = $tex; mesh = $mesh; gloves = $gloves; file = $f.Name
      }
    }
  }
}

$app = [IO.File]::ReadAllText($appPath, $u) | ConvertFrom-Json
$changed = 0; $skippedNoCsv = 0; $skippedNoPlace0 = 0; $touched = New-Object System.Collections.Generic.List[string]
$tag = 'scrape-outfit-' + (Get-Date -Format 'yyyyMMdd')

foreach ($prop in $app.PSObject.Properties) {
  $hash = $prop.Name
  $entry = $prop.Value
  if (-not $entry.Templates) { continue }
  foreach ($t in @($entry.Templates)) {
    if (-not $t -or -not $t.Name) { continue }
    $md = 0
    if ($t.Stats -and $t.Stats.'359') { [void][int]::TryParse([string]$t.Stats.'359', [ref]$md) }
    $hit = $byKey[$t.Name + '|' + $md]
    if (-not $hit) { $hit = $byKey[$t.Name + '|0'] }
    if (-not $hit) {
      # Name-only fallback when scrape MonsterData differs slightly
      $hit = $byKey.Values | Where-Object { $_.name -eq $t.Name } | Select-Object -First 1
    }
    if (-not $hit) { $skippedNoCsv++; continue }
    if (@($hit.tex).Count -gt 0 -and -not (Has-Place0 $hit.tex)) { $skippedNoPlace0++; continue }

    $did = $false
    if (@($hit.tex).Count -gt 0) {
      $t | Add-Member -Force NoteProperty Textures @($hit.tex)
      $did = $true
    }
    if (@($hit.mesh).Count -gt 0) {
      $t | Add-Member -Force NoteProperty Meshes @($hit.mesh)
      $did = $true
    }
    if (@($hit.gloves).Count -gt 0 -and -not $t.Equipment) {
      $t | Add-Member -Force NoteProperty Equipment @($hit.gloves)
      $did = $true
    }
    if ($did) {
      $cp = $t.ContentProvenance
      if (-not $cp) { $cp = [pscustomobject]@{}; $t | Add-Member -Force NoteProperty ContentProvenance $cp }
      $note = "${tag}: Textures/Meshes/gloves from $($hit.file) (places 0-4 incl. hands)"
      $cp | Add-Member -Force NoteProperty AppearanceFix $note
      $changed++
      [void]$touched.Add("$hash $($t.Name)")
    }
  }
}

if ($WhatIf) {
  "WHATIF changed=$changed skippedNoCsv=$skippedNoCsv skippedNoPlace0=$skippedNoPlace0 scrapeKeys=$($byKey.Count) files=$($csvFiles.Count)"
  $touched | Select-Object -First 20
  return
}

if ($byKey.Count -lt 1) {
  "Build-ScrapeOutfit: no CSV rows with Textures/Meshes/EquipmentGloves under $ScrapeDir (scraper outfit bump not rescanned yet). Appearance unchanged."
  return
}

[IO.File]::WriteAllText($appPath, (($app | ConvertTo-Json -Depth 12) + "`n"), $u)
"Build-ScrapeOutfit: changed=$changed skippedNoCsv=$skippedNoCsv skippedNoPlace0=$skippedNoPlace0 scrapeKeys=$($byKey.Count) -> $appPath"
