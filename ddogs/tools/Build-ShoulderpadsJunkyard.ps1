# ddogs/tools/Build-ShoulderpadsJunkyard.ps1 - Arete junkyard bosses/dockers + Shoulderpads quest part 1 (Alvin Odeleder / Dodga Demercel).
# Called at the end of Build-AreteWildlife.ps1 (appends its keys after the wildlife keys; edits machine-mob loot in place).
# Real: AO-Universe Shoulderpads Quests guide (givers, 8 parts, rewards); item ids from itemnames (client RDB names);
#   ALOD (Lush Fields 695, 1544,2500) and DODE (Aegean 585, 606,1122) are the client spawn hashes at the guide's coordinates.
#   Mob names from Randy's live screenshots (IIV-X Advanced Docker inside resource 2150461, Supreme Collector of Waste, Cleanmeister).
# Guessed: hash mapping PO51(+3 nearby 32DO)=IIV-X Advanced Docker, SOCW=Supreme Collector of Waste, PRC2=Cleanmeister boss; levels/scales/HP;
#   no tradeskill engine in ZoneEngine_New, so the givers take the 8 raw parts by trade (retail: combine into the polyhedral casing first);
#   drop rates (Randy: "fairly often"); giver looks (Adri Afeli's mesh); pad QL 30. Parts 2 and 3 (Trash King & lackeys) not built.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
Remove-TypeData System.Array -ErrorAction SilentlyContinue
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData)
$npath = Join-Path $gd 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
function Clone($o) { $o | ConvertTo-Json -Depth 8 | ConvertFrom-Json }
function Prov($t, $donor, $src) { $t | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = $donor; Source = $src }) }
function Mk($donor, $name, $lvl, $mesh, $scale, $hpx, $src) {
  $t = Clone ($nt.$donor.Templates | Sort-Object Level | Select-Object -Last 1)
  $t.Name = $name; $t.Level = $lvl; $t.Stats.'54' = $lvl; $t.Stats.'359' = $mesh; $t.Stats.'360' = $scale
  $t.Stats.'1' = [int]([int]$t.Stats.'1' * $hpx * [Math]::Max(1, $lvl / [Math]::Max(1, [int]$nt.$donor.Templates[-1].Level))); $t.Stats.'27' = $t.Stats.'1'
  Prov $t $donor $src; $t
}
# --- shoulderpad parts (AO-Universe; ids from itemnames)
$parts = [ordered]@{ SPP1 = 155665; SPP2 = 155684; SPP3 = 155664; SPP4 = 155685; SPP5 = 155666; SPP6 = 155686; SPP7 = 155683; SPP8 = 155687 }
$pnames = @('Suspicious Looking Parts of Biotechnology','Liquid Based Conductors','Scrap of Notum','Large and partly fused lump of strange tetrahedral CPU bundles','Microscopic Pulsating Nano Crystals','Ancient Looking Lamp Relay','Bundle of Nano-Tubes','Highly plastic mother-circuit array')
function PartLoot($pct) { foreach ($k in $parts.Keys) { [pscustomobject]@{ Hash = $k; Repeats = 1; Chance = $pct; LevelMod = 0 } } }
$new = [ordered]@{}
$ix = Mk '32DO' 'IIV-X Advanced Docker' 4 17649 115 1 'IIV-X Advanced Docker (Randy live screenshot, inside the building at 3515.8,901.7, resource 2150461). Mesh 17649 android (green battle droid). Level 4 from PO51: guessed'
$new.IIVX = [pscustomobject]@{ Templates = @($ix) }
$sc = Mk '01V1' 'Supreme Collector of Waste' 13 17714 130 3 'Boss (Randy live screenshot; wiki.aodb.us: northeast part of the map). Hash SOCW (L13, east side 4067,667) by name/level, mesh 17714 organic waste collector (orange skin not settable), scale 130, 3x HP: guessed'
$sc | Add-Member -Force NoteProperty LootTable @(PartLoot 40); $sc.Stats | Add-Member -Force NoteProperty '455' 0; $sc.Stats.'359' = 45873
$new.SCWS = [pscustomobject]@{ Templates = @($sc) }
$cm = Mk 'JUCO' 'Cleanmeister Intelligence Robot' 4 297023 170 4 'Cleanmeister boss (Randy live screenshot: green box robot with antenna, among Cleaning Robots). Mesh 297023, scale 170, L4, 4x HP, hash PRC2 (L2, 600 s, inside the JUCO cluster): guessed'
$cm | Add-Member -Force NoteProperty LootTable @(PartLoot 40)
$new.CMBS = [pscustomobject]@{ Templates = @($cm) }
foreach ($g in @(@('ALOD','Alvin Odeleder','Omni-Tek outpost, Lush Fields 1540x2500 (AO-Universe)'), @('DODE','Dodga Demercel','clan outpost on the Aegean mountain 600x1100 (AO-Universe)'))) {
  $t = Clone ($nt.ADAF.Templates | Select-Object -First 1); $t.Name = $g[1]; $t.Level = 50; $t.Stats.'54' = 50
  $t | Add-Member -Force NoteProperty Features @('NoCombat','NoMove')
  Prov $t 'ADAF' ($g[1] + ', ' + $g[2] + '. Look borrowed from Adri Afeli: guessed')
  $new.($g[0]) = [pscustomobject]@{ Templates = @($t) }
}
# --- machine mobs in the starting areas drop the parts (Randy: every machine-type mob, fairly often)
$machine = 'robot|droid|docker|collector|Cargo Bot|Junkbot|Disobedient Bot|android|cleanmeister'
$startPfs = 6553, 4582, 127
$hashes = @{}
foreach ($pf in $startPfs) { foreach ($sp in ([IO.File]::ReadAllText((Join-Path $gd "Playfields\$pf\Spawns.json"), $u) | ConvertFrom-Json).spawns) { $hashes[$sp.hashText] = 1 } }
$nraw = [IO.File]::ReadAllText($npath, $u) -replace "`r`n", "`n"
$cut = $nraw.IndexOf(",`n    `"IIVX`":")
if ($cut -ge 0) { $nraw = $nraw.Substring(0, $cut) + "`n}`n" }
$mk = @()
foreach ($p in $nt.PSObject.Properties) {
  if (-not $hashes.ContainsKey($p.Name) -or $p.Name -in $new.Keys -or $p.Name -eq 'SRDO') { continue }
  $ts = @($p.Value.Templates); if (-not ($ts | Where-Object { $_.Name -match $machine })) { continue }
  foreach ($t in $ts) {
    $keep = @($t.LootTable | Where-Object { $_ -and $_.Hash -notin $parts.Keys })
    $t | Add-Member -Force NoteProperty LootTable @($keep + @(PartLoot 12))
  }
  $start = $nraw.IndexOf("`n    `"" + $p.Name + "`":")
  if ($start -lt 0) { throw "key $($p.Name) not found" }
  $m = [regex]::new("`n {0,4}`"[^`"]{1,6}`":").Match($nraw, $start + 5)
  $endIdx = if ($m.Success) { $m.Index } else { $nraw.TrimEnd().LastIndexOf('}') }
  $seg = $nraw.Substring($start, $endIdx - $start)
  $comma = if ($seg.TrimEnd().EndsWith(',')) { ',' } else { '' }
  $nraw = $nraw.Substring(0, $start) + "`n    `"" + $p.Name + "`": " + ($p.Value | ConvertTo-Json -Depth 8) + $comma + $nraw.Substring($endIdx)
  $mk += $p.Name
}
# --- flea sizes (Randy live screenshot: a normal Garbage Flea is knee-to-waist on a player). Regular 300, named 400 (100/160 looked microscopic on RC-LT 2026-10-16; dynas get x1.5+ from DynaModifiers). Exact scales guessed.
$fleas = @()
foreach ($p in $nt.PSObject.Properties) {
  if (-not $hashes.ContainsKey($p.Name)) { continue }
  $ts = @($p.Value.Templates); if (-not ($ts | Where-Object { $_.Name -match 'Flea' })) { continue }
  $km = [regex]::Match($nraw, "`n[ ]*`"" + $p.Name + "`":"); if (-not $km.Success) { Write-Warning ("flea key not found: " + $p.Name); continue }
  foreach ($t in $ts) { $t.Stats.'359' = 40069;   # gunbeetle: 17657/275710 flea models render see-through on 18.8.50 (Randy .set test 2026-10-16)
  $t.Stats.'360' = $(if ($t.Name -match 'Mutated|Dyna|King|Queen|Boss') { 160 } else { 100 }); $t.Stats | Add-Member -Force NoteProperty '455' 0 }   # fleas were see-through, not tiny: NPCFamily 0 (guess), original mesh/size
  $start = $km.Index
  $m = [regex]::new("`n {0,4}`"[^`"]{1,6}`":").Match($nraw, $start + 5)
  $endIdx = if ($m.Success) { $m.Index } else { $nraw.TrimEnd().LastIndexOf('}') }
  $seg = $nraw.Substring($start, $endIdx - $start)
  $comma = if ($seg.TrimEnd().EndsWith(',')) { ',' } else { '' }
  $nraw = $nraw.Substring(0, $start) + "`n" + $km.Value.TrimStart("`n").Split(":")[0] + ": " + ($p.Value | ConvertTo-Json -Depth 8) + $comma + $nraw.Substring($endIdx)
  $fleas += $p.Name
}
$ix | Add-Member -Force NoteProperty LootTable @(@($ix.LootTable | Where-Object { $_ -and $_.Hash -notin $parts.Keys }) + @(PartLoot 12))
$frag = ''
foreach ($k in $new.Keys) { $frag += ",`n    `"$k`": " + ($new.$k | ConvertTo-Json -Depth 8) }
$end = $nraw.TrimEnd().LastIndexOf('}')
$last = $nraw.LastIndexOf('}', $end - 1)
[IO.File]::WriteAllText($npath, $nraw.Substring(0, $last + 1) + $frag + "`n}`n", $u)
# --- item hashes
$ipath = Join-Path $gd 'ItemTemplates.json'
$il = [IO.File]::ReadAllLines($ipath, $u) | Where-Object { $_ -notmatch '^\s*"SPP\d":' }
$add = foreach ($k in $parts.Keys) { '    "{0}": {{"Templates": [{1},{1}]}},' -f $k, $parts[$k] }
[IO.File]::WriteAllLines($ipath, [string[]](@($il[0]) + $add + $il[1..($il.Count-1)]), $u)
# --- spawns on 6553 (Playfields is gitignored; rerun after re-extract)
$spath = Join-Path $gd 'Playfields\6553\Spawns.json'
$s = [IO.File]::ReadAllText($spath, $u) | ConvertFrom-Json
foreach ($sp in $s.spawns) {
  $x = $sp.position[0]; $z = $sp.position[2]
  if ($sp.hashText -eq 'PO51' -or ($sp.hashText -eq '32DO' -and [Math]::Sqrt(($x-3515.8)*($x-3515.8)+($z-901.7)*($z-901.7)) -lt 14)) { $sp.hashText = 'IIVX'; $sp.hash = 0; $sp.minLevel = 4; $sp.maxLevel = 4; $sp.respawnTime = 60 }
  if ($sp.hashText -in 'SOCW','SCWS') { $sp.hashText = 'SCWS'; $sp.hash = 0; $sp.minLevel = 13; $sp.maxLevel = 13; $sp.respawnTime = 900 }
  if ($sp.hashText -eq 'PRC2') { $sp.hashText = 'CMBS'; $sp.hash = 0; $sp.minLevel = 4; $sp.maxLevel = 4; $sp.respawnTime = 900 }
}
[IO.File]::WriteAllText($spath, ($s | ConvertTo-Json -Depth 8) + "`n", $u)
# --- quests (bits 700/701)
$qpath = Join-Path $gd 'Quests.json'
$q = @(); foreach ($x in (ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($qpath, $u)))) { if ($x.Hash -notin 'SPA1','SPC1') { $q += $x } }
$used = $q | ForEach-Object { $_.MissionBit } | Where-Object { $_ -ne $null }
if ($used -contains 700 -or $used -contains 701) { throw 'bits 700/701 taken' }
function Rec($id, $name) { [pscustomobject]@{ Id = $id; HighId = $id; Name = $name; Quality = 30 } }
$q += [pscustomobject]@{ Hash='SPA1'; Name='Acquiring the robot parts (Alvin)'; QasInstance=920170; Scope='Solo'; Action='FindItem'
  Summary='Bring Alvin Odeleder the eight robot parts'; Objective=[pscustomobject]@{ Count=$null; Npc='ALOD'; SpecItem='SPP1'; WorldItem=$null }; Flags='None'; MissionBit=700; NextStep=$null
  Reward=[pscustomobject]@{ Hash='RSA1'; Credits=0; Xp=5000; Records=@(,@((Rec 158000 'Pads of Interest'), (Rec 155722 "Alvin's Card I"))) } }
$q += [pscustomobject]@{ Hash='SPC1'; Name='Acquiring the robot parts (Dodga)'; QasInstance=920171; Scope='Solo'; Action='FindItem'
  Summary='Bring Dodga Demercel the eight robot parts'; Objective=[pscustomobject]@{ Count=$null; Npc='DODE'; SpecItem='SPP1'; WorldItem=$null }; Flags='None'; MissionBit=701; NextStep=$null
  Reward=[pscustomobject]@{ Hash='RSC1'; Credits=0; Xp=5000; Records=@(,@((Rec 158087 'Clan Shoulderpads for the Suspicious'), (Rec 156950 "Dodga's Card I"))) } }
[IO.File]::WriteAllText($qpath, (ConvertTo-Json -InputObject @($q) -Depth 8) + "`n", $u)
# --- dialog
$list = ($pnames -join ', ')
function Giver($hash, $qh, $who, $intro, $rew) {
  [ordered]@{
    Notes = "$who, Shoulderpads quest part 1 (AO-Universe). Turn-in of the 8 raw parts by trade is a stand-in for the retail combine chain (no tradeskill engine): guessed. Generated by ddogs/tools/Build-ShoulderpadsJunkyard.ps1."
    Npcs = @($hash)
    Openers = @( [ordered]@{ If = @("HasQuest $qh"); Say = 'busy' }, [ordered]@{ Say = 'hello' } )
    Replies = [ordered]@{
      help = [ordered]@{ Text = 'I would love to help! What do you need?'; If = @("!HasAnyQuest $qh", "!HasCompletedQuest $qh"); Goto = 'offer' }
      yes = [ordered]@{ Text = "Count me in. I'll find them all."; Goto = 'give' }
      handin = [ordered]@{ Text = 'I have something for you.'; If = @("HasQuest $qh"); Goto = 'handin' }
      bye = [ordered]@{ Text = 'Goodbye.'; Goto = 'bye' }
    }
    Lines = [ordered]@{
      hello = [ordered]@{ Text = $intro; Replies = @('help','bye') }
      offer = [ordered]@{ Text = @('Robots all over Rubi-Ka carry strange parts. I need eight of them, one of each:', "{delay 1}$list.", '{delay 1}Machines drop them, even the junk robots and dockers around the newcomer areas. They are unique, so you can only hold one of each.'); Replies = @('yes','bye') }
      give = [ordered]@{ Text = @("{givequest $qh}", '{delay 1}Bring all eight together and I will make it worth your while.') }
      busy = [ordered]@{ Text = "Still need those eight robot parts: $list."; Replies = @('handin','bye') }
      handin = [ordered]@{ Text = 'All eight? Put them in the trade window.{opentrade}' }
      done = [ordered]@{ Text = @("{completequest $qh}", $rew); Replies = @('bye') }
      bye = [ordered]@{ Text = 'Stay sharp.{close}' }
    }
    Trades = [ordered]@{ turnin = [ordered]@{ If = @("HasQuest $qh"); Items = @(foreach ($k in $parts.Keys) { [ordered]@{ Hash = $k; Count = 1 } }); Goto = 'done' } }
  }
}
$kd = Join-Path $gd 'Knubot'
$a = Giver 'ALOD' 'SPA1' 'Alvin Odeleder' "I've been assigned to gather some very important information, but I'm short of people. Omni-Tek needs dedicated employees." 'Excellent work. Wear these Pads of Interest with pride, and keep this card. Bring it back to me when you want more work.'
$d = Giver 'DODE' 'SPC1' 'Dodga Demercel' "The Clans asked me to gather some important information up here, but I'm short of people." 'Well done, friend. These Clan Shoulderpads for the Suspicious are yours, and keep this card. Bring it back later and I will have more for you.'
[IO.File]::WriteAllText((Join-Path $kd 'AlvinOdeleder.json'), ($a | ConvertTo-Json -Depth 8) + "`n", $u)
[IO.File]::WriteAllText((Join-Path $kd 'DodgaDemercel.json'), ($d | ConvertTo-Json -Depth 8) + "`n", $u)
foreach ($p in @($npath, $ipath, $qpath, (Join-Path $kd 'AlvinOdeleder.json'), (Join-Path $kd 'DodgaDemercel.json'))) { [IO.File]::WriteAllText($p, ([IO.File]::ReadAllText($p, $u) -replace "`r`n", "`n"), $u) }
'shoulderpads/junkyard written; part drops on: ' + ($mk -join ',') + '; fleas resized: ' + ($fleas -join ',')
& (Join-Path $PSScriptRoot 'Build-AreteDocks.ps1') -GameData $gd
