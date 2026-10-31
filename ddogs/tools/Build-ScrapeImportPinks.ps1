# ddogs/tools/Build-ScrapeImportPinks.ps1 - 2026-10-31: fill pink placeholder hash points (no template -> AAAA "To Be Determined")
#  from Randy's live scrapes. Plan = ddogs/tools/scrape_import_pinks_20261031.json, made by ddogs/tools/match-pinks.js:
#  each client Spawns.json point whose hash has no template is matched to the nearest live-scraped NPC within 12 m and
#  inside the point's level range (+-1); a hash is filled only when >=60% of its matched points agree on one name.
#  Unresolved hashes are listed in the plan's "unresolved" (left pink, nothing invented).
#  Stats/model: same donor+scraped-stat method as Build-ScrapeImportCities.ps1 (name, level, side, MonsterData, scale, family,
#  hp, breed, head, mesh, texture from the scrape). Never overwrites an existing template. Templates only: no spawns/loot/skips.
# Rerunnable: Source 'scrape-pinks-20261031'. Zones in this plan: Inferno 4605, Nascence 4310-4313 + ICC 655 (Whozewhatzit live scrape 2026-10-31 02:40-02:52 ET).
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false); $gd = [IO.Path]::GetFullPath($GameData); $tag = 'scrape-pinks-20261031'
function RJ($p) { [IO.File]::ReadAllText($p, $u) | ConvertFrom-Json }
$plan = RJ (Join-Path $PSScriptRoot 'scrape_import_pinks_20261031.json')
$npath = Join-Path $gd 'NpcTemplates.json'; $nt = RJ $npath
foreach ($p in @($nt.PSObject.Properties)) { if ("$($p.Value.ContentProvenance.Source)" -eq $tag) { $nt.PSObject.Properties.Remove($p.Name) } }
$pool = New-Object System.Collections.ArrayList
foreach ($p in $nt.PSObject.Properties) { if ($p.Value.ContentProvenance.Guessed) { continue }; foreach ($b in @($p.Value.Templates)) { if ($b -and $b.Stats -and $b.Stats.'359') { [void]$pool.Add($b) } } }
$made = 0; $skipReal = 0
foreach ($t in $plan.templates) {
  if ($nt.($t.hash)) { $skipReal++; continue }
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
    foreach ($k in $set.Keys) { $c.Stats | Add-Member -Force NoteProperty $k $set[$k] }
    $c | Add-Member -Force NoteProperty LootTable @()
    # donor leftovers must not leak: no donor gear, band provenance or donor NoCombat/NoMove on hostile mobs.
    foreach ($k in @('Equipment','ContentProvenance')) { if ($c.PSObject.Properties[$k]) { $c.PSObject.Properties.Remove($k) } }
    $c | Add-Member -Force NoteProperty HasHeadMesh ([int]$b.head -gt 0)
    $feat = @($c.Features | Where-Object { $_ -and $_ -ne 'NoCombat' -and $_ -ne 'NoMove' })
    $c | Add-Member -Force NoteProperty Features @($feat | Sort-Object -Unique)
    $c
  }
  $nt | Add-Member -Force NoteProperty $t.hash ([pscustomobject]@{ Templates = @($bands); ContentProvenance = [pscustomobject]@{ Guessed = $false; Source = $tag; Note = "live scrape position match (pf $($t.pf), $($t.agree)/$($t.matched) matched points agree, $($t.points) points)" } })
  $made++
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 10) + "`n", $u)
"templates made=$made skipped(existing)=$skipReal unresolved=$(@($plan.unresolved).Count)"

