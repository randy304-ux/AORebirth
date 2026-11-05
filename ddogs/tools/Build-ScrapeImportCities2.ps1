# ddogs/tools/Build-ScrapeImportCities2.ps1 - missing named city NPCs from scrape-20261105b (pfs 800/545/540/566/700/705/710).
# Plan = ddogs/tools/scrape_import_cities_20261105b.json, made by ddogs/tools/plan-cities-20261105b.js (only scraped data).
#  templates: NpcTemplates.Cities2.json overlay (new Q7xx hashes; donor+scraped-stat method of Build-ScrapeImportICC655.ps1;
#             friendly NoCombat; NoMove unless Wander). Existing templates reused, never changed.
#  spawns:    PlayfieldContent/{pf}/SpawnsAdd.cities20261105b.json (own tag file per pf; SpawnsAdd.json stays owned by scrape-city-20261030).
#  removed:   Levon Karubian old LEKA point in pf 800 as SpawnSkips removedPoints (live spot added via SpawnsAdd).
# Rerunnable: Source 'scrape-cities-20261105b'.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false); $gd = [IO.Path]::GetFullPath($GameData); $tag = 'scrape-cities-20261105b'
function RJ($p) { [IO.File]::ReadAllText($p, $u) | ConvertFrom-Json }
$plan = RJ (Join-Path $PSScriptRoot 'scrape_import_cities_20261105b.json')
if ($plan.source -ne $tag) { throw "plan source $($plan.source)" }
# --- templates overlay
$nt = RJ (Join-Path $gd 'NpcTemplates.json')
foreach ($f in [IO.Directory]::GetFiles($gd, 'NpcTemplates.*.json')) {
  if ([IO.Path]::GetFileName($f) -eq 'NpcTemplates.Cities2.json') { continue }
  $ov0 = RJ $f; foreach ($p in $ov0.PSObject.Properties) { if (-not $nt.($p.Name)) { $nt | Add-Member -Force NoteProperty $p.Name $p.Value } }
}
$pool = New-Object System.Collections.ArrayList
foreach ($p in $nt.PSObject.Properties) { if ($p.Value.ContentProvenance.Guessed) { continue }; foreach ($b in @($p.Value.Templates)) { if ($b -and $b.Stats -and $b.Stats.'359') { [void]$pool.Add($b) } } }
if ($pool.Count -lt 1) { throw 'no donor pool' }
$ov = [ordered]@{}; $made = 0
foreach ($t in $plan.templates) {
  if ($nt.($t.hash)) { throw "hash $($t.hash) already in NpcTemplates" }
  $bands = foreach ($b in $t.bands) {
    $L = [int]$b.Level; $d = $pool | Sort-Object { [math]::Abs([int]$_.Level - $L) } | Select-Object -First 1
    $c = $d | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $c.Name = $b.Name; $c.Level = $L
    $set = [ordered]@{ '54' = $L; '33' = [int]$b.side; '359' = [int]$b.md; '360' = [int]$b.sc; '455' = [int]$b.fam }
    if ([int]$b.hp -gt 0) { $set['1'] = [int]$b.hp; $set['27'] = [int]$b.hp }
    if ([int]$b.breed -gt 0) { $set['4'] = [int]$b.breed }
    if ([int]$b.head -gt 0) { $set['64'] = [int]$b.head }
    if ([int]$b.mesh -gt 0) { $set['12'] = [int]$b.mesh }
    if ([int]$b.tex -gt 0) { $set['344'] = [int]$b.tex }
    $set['0'] = 268964353
    foreach ($k in $set.Keys) { $c.Stats | Add-Member -Force NoteProperty $k $set[$k] }
    $c | Add-Member -Force NoteProperty LootTable @()
    foreach ($k in @('Equipment', 'ContentProvenance')) { if ($c.PSObject.Properties[$k]) { $c.PSObject.Properties.Remove($k) } }
    $c | Add-Member -Force NoteProperty HasHeadMesh ([int]$b.head -gt 0)
    $feat = @($c.Features | Where-Object { $_ -and $_ -ne 'NoCombat' -and $_ -ne 'NoMove' -and $_ -ne 'Wander' })
    if ($t.friendly) { $feat = @($feat) + 'NoCombat' }
    if ($t.wander) { $feat = @($feat) + 'Wander' } else { $feat = @($feat) + 'NoMove' }
    $c | Add-Member -Force NoteProperty Features @($feat | Sort-Object -Unique)
    if ($b.textures) {
      $tex = @($b.textures | ForEach-Object { [pscustomobject]@{ Id = [int]$_.Id; Place = [int]$_.Place } })
      if ($tex.Count -gt 0) { $c | Add-Member -Force NoteProperty Textures $tex }
    }
    if ($b.meshes) {
      $msh = @($b.meshes | ForEach-Object { [pscustomobject]@{ Id = [int]$_.Id; Position = [int]$_.Position; OverrideTextureId = [int]$_.OverrideTextureId; Layer = [int]$_.Layer } })
      if ($msh.Count -gt 0) { $c | Add-Member -Force NoteProperty Meshes $msh }
    }
    if ($b.gloves -and -not $c.Equipment) {
      $eq = @($b.gloves | ForEach-Object { ,[int[]]@([int]$_, [int]$_) })
      if ($eq.Count -gt 0) { $c | Add-Member -Force NoteProperty Equipment $eq }
    }
    $c
  }
  $ov[$t.hash] = [pscustomobject]@{ Templates = @($bands); ContentProvenance = [pscustomobject]@{ Guessed = $false; Source = $tag; Note = "live scrape cities ($($t.src))$(if($t.wander){'; Wander (seen roaming)'})" } }
  $made++
}
[IO.File]::WriteAllText((Join-Path $gd 'NpcTemplates.Cities2.json'), (([pscustomobject]$ov) | ConvertTo-Json -Depth 10) + "`n", $u)
# --- spawns (own add-on file per pf)
$nsp = 0
foreach ($pp in $plan.spawnsByPf.PSObject.Properties) {
  $pf = [int]$pp.Name; $dir = Join-Path $gd "PlayfieldContent\$pf"; if (-not (Test-Path $dir)) { New-Item -ItemType Directory $dir | Out-Null }
  $pts = foreach ($s in @($pp.Value)) {
    [ordered]@{ districtIndex = 0; hash = 0; hashText = $s.hash; manifestHash = 0; minLevel = [int]$s.lo; maxLevel = [int]$s.hi; respawnChance = 100; flags = 1; respawnTime = 600
      version7Extra = 0; hasOptionalFlagBlock = $true; nativeFlags = 0; moreFlags = 0; assistanceRadius = 0; unknownOptionalU8 = 0; angle = [int]$s.angle; angleW = 1
      position = @([double]$s.x, [double]$s.y, [double]$s.z); radius = 1; additionalPoints = @(); extensions = $null }
  }
  $doc = [ordered]@{ schemaVersion = 1; recordType = 1000014; playfieldId = $pf; source = $tag; spawns = @($pts) }
  [IO.File]::WriteAllText((Join-Path $dir 'SpawnsAdd.cities20261105b.json'), ($doc | ConvertTo-Json -Depth 6) + "`n", $u)
  $nsp += @($pts).Count
}
# --- SpawnSkips removedPoints (replace this tag's entries)
$kpath = Join-Path $gd 'SpawnSkips.json'; $sk = RJ $kpath
$sk.removedPoints = @(@($sk.removedPoints | Where-Object { "$($_.reason)" -notlike "*$tag*" }) + @($plan.removePoints | ForEach-Object { [pscustomobject][ordered]@{ playfield = [int]$_.playfield; hash = $_.hash; position = @($_.position); reason = $_.reason } }))
[IO.File]::WriteAllText($kpath, ($sk | ConvertTo-Json -Depth 6) + "`n", $u)
"templates=$made spawns=$nsp removedPoints=$(@($plan.removePoints).Count) skipped=$(@($plan.skipped).Count)"
