# ddogs/tools/Build-ArrivalHall.ps1 - 2026-10-30 Arrival Hall (4604) new-character start, from Randy's live scrape 2026-10-30 14:32 ET.
#  templates: pink hashes filled from the scrape (same donor + scraped-stat method as Build-ScrapeImportCities.ps1):
#             NEOB = ICC Immigration Officer, JORE = Jobe Research Scientist, CETE/CLTC/CLTE = Cleaning Technicians.
#             All NoCombat+NoMove (hall staff; the L100-177 cleaners must not fight new characters).
#  content:   Pioneer Historian (no hash point) as a content NPC in PlayfieldContent/4604/Npcs.json.
#  knubot:    Knubot/ImmigrationOfficer.json (live text from dialogs_raw 2026-10-30); the Rubi-Ka line does {startarea icc} + ICC Shuttleport.
#  hidden:    CLRE (Clan desk) and OMIM (Omni desk) - unresolved desk pinks with no live NPC.
#  code side: ZoneEngine_New Core/Knubot/ArrivalHall.cs (login placement + Arete / Shadowlands doors).
# Rerunnable: Source 'arrival-hall-20261030' / key prefix ddogs:arrival:.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false); $gd = [IO.Path]::GetFullPath($GameData); $tag = 'arrival-hall-20261030'
function RJ($p) { [IO.File]::ReadAllText($p, $u) | ConvertFrom-Json }
function WJ($p, $o) { [IO.File]::WriteAllText($p, ($o | ConvertTo-Json -Depth 30), $u) }
$src = 'live PRK scrape 2026-10-30 14:32 ET, Arrival Hall 4604'

# --- templates ---
$npath = Join-Path $gd 'NpcTemplates.json'; $nt = RJ $npath
foreach ($p in @($nt.PSObject.Properties)) { if ("$($p.Value.ContentProvenance.Source)" -eq $tag) { $nt.PSObject.Properties.Remove($p.Name) } }
$pool = New-Object System.Collections.ArrayList
foreach ($p in $nt.PSObject.Properties) { if ($p.Value.ContentProvenance.Guessed) { continue }; foreach ($b in @($p.Value.Templates)) { if ($b -and $b.Stats -and $b.Stats.'359') { [void]$pool.Add($b) } } }
# breed: Solitus 1, Opifex 2, Nanomage 3, Atrox 4
$fills = @(
  @{ hash='NEOB'; inst=1053615; Name='ICC Immigration Officer'; Level=50;  hp=2266;  breed=1; md=165187; sc=105; mesh=17532; head=40687; fam=137 },
  @{ hash='JORE'; inst=1053616; Name='Jobe Research Scientist'; Level=50;  hp=2266;  breed=1; md=220406; sc=105; mesh=17530; head=40639; fam=103 },
  @{ hash='CETE'; inst=1053612; Name='Cleaning Technician';     Level=177; hp=17394; breed=2; md=165191; sc=119; mesh=23377; head=40247; fam=100 },
  @{ hash='CLTC'; inst=1053613; Name='Cleaning Technician';     Level=123; hp=9557;  breed=4; md=165195; sc=114; mesh=5900;  head=40118; fam=100 },
  @{ hash='CLTE'; inst=1053614; Name='Cleaning Technician';     Level=159; hp=14710; breed=4; md=165194; sc=117; mesh=5900;  head=40130; fam=100 }
)
$made = 0
foreach ($f in $fills) {
  if ($nt.($f.hash)) { throw "$($f.hash) already has a real template" }
  $L = $f.Level; $d = $pool | Sort-Object { [math]::Abs([int]$_.Level - $L) } | Select-Object -First 1
  $c = $d | ConvertTo-Json -Depth 10 | ConvertFrom-Json
  $c.Name = $f.Name; $c.Level = $L
  $set = [ordered]@{ '54' = $L; '33' = 0; '359' = $f.md; '360' = $f.sc; '455' = $f.fam; '1' = $f.hp; '27' = $f.hp; '4' = $f.breed; '64' = $f.head; '12' = $f.mesh }
  foreach ($k in $set.Keys) { $c.Stats | Add-Member -Force NoteProperty $k $set[$k] }
  $c | Add-Member -Force NoteProperty LootTable @()
  $c | Add-Member -Force NoteProperty Features @(@($c.Features | Where-Object { $_ -and $_ -ne 'Wander' }) + 'NoCombat' + 'NoMove' | Sort-Object -Unique)
  $nt | Add-Member -Force NoteProperty $f.hash ([pscustomobject]@{ Templates = @($c); ContentProvenance = [pscustomobject]@{ Guessed = $false; Source = $tag; Note = "$src (inst $($f.inst)) hall staff NoCombat/NoMove" } })
  $made++
}
WJ $npath $nt

# --- content NPC: Pioneer Historian ---
$cdir = Join-Path $gd 'PlayfieldContent\4604'; [void][IO.Directory]::CreateDirectory($cdir)
$cpath = Join-Path $cdir 'Npcs.json'
if (Test-Path $cpath) { $cj = RJ $cpath; $cj.Npcs = @($cj.Npcs | Where-Object { $_ -and "$($_.Key)" -notlike 'ddogs:arrival:*' }) }
else { $cj = [pscustomobject][ordered]@{ SchemaVersion = 1; PlayfieldId = 4604; MailTerminals = @(); Npcs = @() } }
$stats = [ordered]@{ '0'=277352961; '1'=1135; '4'=3; '12'=5927; '27'=1135; '33'=0; '47'=1; '54'=50; '59'=2; '60'=0; '64'=40629; '89'=1; '156'=136; '173'=3; '174'=3; '359'=26090; '360'=103; '368'=0; '389'=0; '455'=136; '466'=0; '660'=0; '673'=31 }
$hist = [pscustomobject][ordered]@{
  Key = 'ddogs:arrival:4604:historian'; Provenance = "$src (inst 1053617, position/heading/body)"; ContentNpcIdentity = 'ddogs-arrival-historian'
  PlayfieldId = 4604; InstanceId = 2110904617; Name = 'Pioneer Historian'
  Position = @(183.4, 1.024, 175.4); Rotation = @(0, 0.725, 0, 0.688); Stats = [pscustomobject]$stats
  Meshes = @([pscustomobject][ordered]@{ Position = 0; OverrideTextureId = 0; Id = 40629; Layer = 4 })
  Passive = $true; Attackable = $false; TickLiving = $true; Presentation = [pscustomobject]@{}
}
$cj.Npcs = @(@($cj.Npcs) + $hist)
WJ $cpath $cj

# --- hidden desk pinks ---
$spath = Join-Path $gd 'SpawnSkips.json'; $sk = RJ $spath
$hide = @{ CLRE = 'Arrival Hall 4604 Clan desk pink (205,161.6): live scrape 2026-10-30 has no NPC there. 2026-10-30'; OMIM = 'Arrival Hall 4604 Omni desk pink (184.7,162.1): live scrape 2026-10-30 has no NPC there. 2026-10-30' }
$sk.hiddenHashes = @(@($sk.hiddenHashes | Where-Object { $_ -and $_.hash -and -not $hide.ContainsKey("$($_.hash)") }) + @(foreach ($h in 'CLRE','OMIM') { [pscustomobject][ordered]@{ hash = $h; reason = $hide[$h] } }))
WJ $spath $sk
"templates=$made historian=1 hidden=CLRE,OMIM"
