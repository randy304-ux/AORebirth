# ddogs/tools/Build-ScrapeImport585.ps1 - Stret West City pf 585 pilot from Whozewhatzit live scrape
# (rows in scrape-20261105 extract, scraped_npcs_20261103_Whozewhatzit.csv pf 585).
# Same pipeline as Build-ScrapeImportCities/Pinks/CB/Nasc:
#  templates: new Pxxx hashes (and any resolved pink hashpoints) from scrape stats;
#             donor+scraped-stat method; never overwrites an existing template.
#  spawns:    PlayfieldContent/585/SpawnsAdd.json only (client Spawns.json / Surfaces untouched).
#  pinks:     unresolved pinks left pink (match-pinks 12m / level+-1 / >=60% agree); collisions flagged in plan.
#  vendors:   none in this scrape.
# Rerunnable: Source 'scrape-585-20261105'.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false); $gd = [IO.Path]::GetFullPath($GameData); $tag = 'scrape-585-20261105'
function RJ($p) { [IO.File]::ReadAllText($p, $u) | ConvertFrom-Json }
$plan = RJ (Join-Path $PSScriptRoot 'scrape_import_585_20261105.json')
$npath = Join-Path $gd 'NpcTemplates.json'; $nt = RJ $npath
foreach ($p in @($nt.PSObject.Properties)) {
  if ("$($p.Value.ContentProvenance.Source)" -eq $tag) { $nt.PSObject.Properties.Remove($p.Name) }
}
$pool = New-Object System.Collections.ArrayList
foreach ($p in $nt.PSObject.Properties) {
  if ($p.Value.ContentProvenance.Guessed) { continue }
  foreach ($b in @($p.Value.Templates)) {
    if ($b -and $b.Stats -and $b.Stats.'359') { [void]$pool.Add($b) }
  }
}
if ($pool.Count -lt 1) { throw 'no donor pool' }
$made = 0; $skipReal = 0; $wanderT = 0
foreach ($t in $plan.templates) {
  if ($nt.($t.hash)) { $skipReal++; continue }
  $bands = foreach ($b in $t.bands) {
    $L = [int]$b.Level
    $d = $pool | Sort-Object { [math]::Abs([int]$_.Level - $L) } | Select-Object -First 1
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
    foreach ($k in @('Equipment', 'ContentProvenance')) {
      if ($c.PSObject.Properties[$k]) { $c.PSObject.Properties.Remove($k) }
    }
    $c | Add-Member -Force NoteProperty HasHeadMesh ([int]$b.head -gt 0)
    $feat = @($c.Features | Where-Object { $_ -and $_ -ne 'NoCombat' -and $_ -ne 'NoMove' -and $_ -ne 'Wander' })
    if ($t.wander) { $feat = @($feat) + 'Wander' }
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
  if ($t.wander) { $wanderT++ }
  $note = "live Whozewhatzit scrape pf 585 ($($t.src) hash $($t.hash))"
  if ($t.wander) { $note += '; Wander from multi-position/npc_moves' }
  $nt | Add-Member -Force NoteProperty $t.hash ([pscustomobject]@{
      Templates = @($bands)
      ContentProvenance = [pscustomobject]@{ Guessed = $false; Source = $tag; Note = $note }
    })
  $made++
}
# SpawnsAdd first (fast); NpcTemplates write is large/slow
$nsp = 0
foreach ($pp in @($plan.spawns.PSObject.Properties)) {
  $pf = [int]$pp.Name
  $dir = Join-Path $gd "PlayfieldContent\$pf"
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory $dir | Out-Null }
  $f = Join-Path $dir 'SpawnsAdd.json'
  if (Test-Path $f) {
    $old = RJ $f
    if ("$($old.source)" -ne $tag) { throw "SpawnsAdd.json in $pf belongs to $($old.source)" }
  }
  $pts = foreach ($s in $pp.Value) {
    [ordered]@{
      districtIndex = 0; hash = 0; hashText = $s.hash; manifestHash = 0
      minLevel = [int]$s.lo; maxLevel = [int]$s.hi; respawnChance = 100; flags = 1; respawnTime = 600
      version7Extra = 0; hasOptionalFlagBlock = $true; nativeFlags = 0; moreFlags = 0
      assistanceRadius = 0; unknownOptionalU8 = 0
      angle = [int][math]::Round([double]$s.angle); angleW = 1
      position = @([double]$s.x, [double]$s.y, [double]$s.z); radius = 1
      additionalPoints = @(); extensions = $null
    }
  }
  if (@($pts).Count -gt 0) {
    $doc = [ordered]@{ schemaVersion = 1; recordType = 1000014; playfieldId = $pf; source = $tag; spawns = @($pts) }
    [IO.File]::WriteAllText($f, ($doc | ConvertTo-Json -Depth 6) + "`n", $u)
    $nsp += @($pts).Count
  }
}
"spawnsAdd=$nsp writing NpcTemplates..."
# Prefer Newtonsoft when present (much faster on large GameData); else ConvertTo-Json
$json = $null
try {
  Add-Type -AssemblyName 'Newtonsoft.Json' -ErrorAction Stop
  $json = [Newtonsoft.Json.JsonConvert]::SerializeObject($nt, [Newtonsoft.Json.Formatting]::Indented)
} catch {
  $json = ($nt | ConvertTo-Json -Depth 10)
}
[IO.File]::WriteAllText($npath, $json.TrimEnd() + "`n", $u)
"templates made=$made skipped(existing)=$skipReal wander=$wanderT spawnsAdd=$nsp unresolvedPinks=$(@($plan.unresolved).Count) collisions=$(@($plan.collisions).Count) donorPool=$($pool.Count)"
