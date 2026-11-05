# ddogs/tools/Build-ScrapeImportICC655.ps1 - ICC HQ (pf 655) fixes from Randy's live scrapes (Oct 30-Nov 4 2026, Whozewhatzit/Nazgore).
# Plan = ddogs/tools/scrape_import_icc655_20261105.json, made by ddogs/tools/plan-icc655.js (only scraped data).
#  templates: NpcTemplates.ICC655.json overlay (new Q6xx hashes only; donor+scraped-stat method of Build-ScrapeImport585.ps1;
#             friendly ICC NPCs NoCombat; NoMove unless seen roaming (Wander)). Existing templates reused, never changed.
#  spawns:    PlayfieldContent/655/SpawnsAdd.icc655.json (own file; SpawnsAdd.json stays owned by scrape-city-20261030).
#  removed:   wrong ICC points (3846 lvl 1-200, ICFM, ICPA, 01V1, ABGU, PPCD, ZYBA, FPBY) as SpawnSkips removedPoints.
#  halloween: Halloween.json uph-icc Pads = live Uncle Pumpkin-Head spots only.
#  2CIE stays hidden; the scraped named citizens are added as their own templates/points.
# Rerunnable: Source 'scrape-icc655-20261105'.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false); $gd = [IO.Path]::GetFullPath($GameData); $tag = 'scrape-icc655-20261105'
function RJ($p) { [IO.File]::ReadAllText($p, $u) | ConvertFrom-Json }
$plan = RJ (Join-Path $PSScriptRoot 'scrape_import_icc655_20261105.json')
if ($plan.source -ne $tag) { throw "plan source $($plan.source)" }
# --- templates overlay
$nt = RJ (Join-Path $gd 'NpcTemplates.json')
$pool = New-Object System.Collections.ArrayList
foreach ($p in $nt.PSObject.Properties) { if ($p.Value.ContentProvenance.Guessed) { continue }; foreach ($b in @($p.Value.Templates)) { if ($b -and $b.Stats -and $b.Stats.'359') { [void]$pool.Add($b) } } }
if ($pool.Count -lt 1) { throw 'no donor pool' }
$ov = [ordered]@{}; $made = 0
foreach ($t in $plan.templates) {
  if ($nt.($t.hash)) { throw "hash $($t.hash) already in NpcTemplates.json" }
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
    # Outfit from scraper (places 0-4 incl. hands). Plan fields optional for older plans.
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
  $ov[$t.hash] = [pscustomobject]@{ Templates = @($bands); ContentProvenance = [pscustomobject]@{ Guessed = $false; Source = $tag; Note = "live scrape pf 655 ICC ($($t.src))$(if($t.wander){'; Wander (seen roaming)'})" } }
  $made++
}
[IO.File]::WriteAllText((Join-Path $gd 'NpcTemplates.ICC655.json'), (([pscustomobject]$ov) | ConvertTo-Json -Depth 10) + "`n", $u)
# --- spawns (own add-on file)
$pts = foreach ($s in $plan.spawns) {
  [ordered]@{ districtIndex = 0; hash = 0; hashText = $s.hash; manifestHash = 0; minLevel = [int]$s.lo; maxLevel = [int]$s.hi; respawnChance = 100; flags = 1; respawnTime = 600
    version7Extra = 0; hasOptionalFlagBlock = $true; nativeFlags = 0; moreFlags = 0; assistanceRadius = 0; unknownOptionalU8 = 0; angle = [int]$s.angle; angleW = 1
    position = @([double]$s.x, [double]$s.y, [double]$s.z); radius = 1; additionalPoints = @(); extensions = $null }
}
$doc = [ordered]@{ schemaVersion = 1; recordType = 1000014; playfieldId = 655; source = $tag; spawns = @($pts) }
[IO.File]::WriteAllText((Join-Path $gd 'PlayfieldContent\655\SpawnsAdd.icc655.json'), ($doc | ConvertTo-Json -Depth 6) + "`n", $u)
# --- SpawnSkips removedPoints (replace this tag's entries)
$kpath = Join-Path $gd 'SpawnSkips.json'; $sk = RJ $kpath
$sk.removedPoints = @(@($sk.removedPoints | Where-Object { "$($_.reason)" -notlike "*$tag*" }) + @($plan.removePoints | ForEach-Object { [pscustomobject][ordered]@{ playfield = 655; hash = $_.hash; position = @($_.position); reason = $_.reason } }))
[IO.File]::WriteAllText($kpath, ($sk | ConvertTo-Json -Depth 6) + "`n", $u)
# --- Halloween uph-icc pads: text splice of the Pads array only
$hpath = Join-Path $gd 'Halloween.json'; $h = [IO.File]::ReadAllText($hpath, $u)
$k = $h.IndexOf('"uph-icc"'); if ($k -lt 0) { throw 'uph-icc not found' }
$pi = $h.IndexOf('"Pads"', $k); $a = $h.IndexOf('[', $pi); $depth = 0; $e = $a
for ($i = $a; $i -lt $h.Length; $i++) { if ($h[$i] -eq '[') { $depth++ } elseif ($h[$i] -eq ']') { $depth--; if ($depth -eq 0) { $e = $i; break } } }
$pads = '[' + ((@($plan.uphPads) | ForEach-Object { '[' + (($_ | ForEach-Object { ([double]$_).ToString([Globalization.CultureInfo]::InvariantCulture) }) -join ',') + ']' }) -join ',') + ']'
$h = $h.Substring(0, $a) + $pads + $h.Substring($e + 1)
$h = $h.Replace('ICC HQ / Andromeda densified to village/path density. Level 25-50. Floor-snapped at spawn.', 'ICC HQ: live Uncle Pumpkin-Head spots only (scrape-icc655-20261105). Floor-snapped at spawn.')
[IO.File]::WriteAllText($hpath, $h, $u)
$null = $h | ConvertFrom-Json
"templates=$made spawns=$(@($pts).Count) removedPoints=$(@($plan.removePoints).Count) uphPads=$(@($plan.uphPads).Count) (was $($plan.uphOldPadCount)) skipped=$(@($plan.skipped).Count)"
