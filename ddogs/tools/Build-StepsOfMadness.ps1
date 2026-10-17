# ddogs/tools/Build-StepsOfMadness.ps1 - Steps of Madness (PF 1933). Re-runnable; called by Build-NpcTemplates.ps1.
#  Spawn points stay the client Playfields/1933/Spawns.json rows (114). This file only adds templates for those hashTexts.
#  Respawn seconds already on those points are kept (Neleb, Betrayer, and Thief 600s; named camps 300s; most trash 180s;
#  Foundation of Sanity 1200s; Gleeful Greeter 1700s). AO-Universe says Neleb is about 20 minutes; the client point is 10,
#  which matches the wiki, so the point is not rewritten.
#  Names: AO-Universe Steps of Madness guide, plus client attack-item names in itemnames.sql for hashes the guide does not
#  spell (Mind Fragment, Nebulous Feeling, Roaving Eye). No Childhood Memory key. No level lock.
#  Loot ids: itemnames.sql, checked by name below. 100% rows are the guide's 100% drops. Neleb drops two Fractured Sanity
#  (Repeats 2). Dark Dreams is 55% for one copy; the wiki's 15% second copy is not a separate exclusive roll.
#  Other named uniques have no published percent, so they are 25% (Emotional Sponge 40%, Childhood Horror's arachnid ether
#  40%, Gleeful Greeter ethers 5%). Those percents are flagged in ContentProvenance.
#  Meshes are captured donors, not the retail SoM bodies. See $mesh.
#  Zyvania Bagh is the existing Andromeda hash ZYBA at the guide pin. Her script teleports to the dungeon entrance.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$GameData = [IO.Path]::GetFullPath($GameData)
$u = [Text.UTF8Encoding]::new($false)

# ---------- 1. item hashes, ids from itemnames ----------
$items = [ordered]@{
  MROB=@(151895,151895); MBAT=@(152026,152026); MFRS=@(151896,151896); MFBL=@(151903,151903)
  MNER=@(152025,152025); MFLA=@(152027,152027); MESP=@(152031,152031); MDDR=@(274971,274971); MDMC=@(274972,274972)
  MLHV=@(152022,152022); MBRH=@(152021,152021); MGNH=@(152023,152023); MJEA=@(152030,152030); MCTX=@(152032,152032)
  MBRC=@(152024,152024); META=@(274958,274958); METD=@(274961,274961); METS=@(274959,274959); METB=@(274960,274960)
  METT=@(274879,274879); MMED=@(275003,275003); MPIL=@(275004,275004)
}
$fam = [ordered]@{ METH = @('META','METD','METS','METB','METT') }
$expect = [ordered]@{
  151895='Nano-circuit Robe'; 152026='Notum Battlerod'; 151896='Fractured Sanity'; 151903='Fear-forged Blade'
  152025='Nervejolter'; 152027='Neutrino Flash'; 152031='Emotional Sponge'; 274971='Dark Dreams'; 274972='Dream Mesh Circuit'
  152022='Loving Hands'; 152021='Brutal Hands'; 152023='Gentle Hands'; 152030='Essence of Pure Jealousy'
  152032='Cortex of the Executioner'; 152024='Brainchopper'; 274958='Nightmare Arachnid Ether'; 274961='Nightmare Darkness Ether'
  274959='Nightmare Spacial Ether'; 274960='Nightmare Burning Ether'; 274879='Nightmare Tidal Ether'
  275003='Sanity Recovery Medication'; 275004='Nightmare Therapy Pills'
}
$names = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\..\AORebirth\Libraries\Source\AORebirth.Database\SqlTables\itemnames.sql'))
foreach ($pair in $expect.GetEnumerator()) {
  $id = $pair.Key
  $fragment = [string]$pair.Value
  $at = $names.IndexOf("( $id ,")
  if ($at -lt 0) { throw "itemnames.sql has no $id" }
  $slice = $names.Substring($at, [Math]::Min(160, $names.Length - $at))
  if ($slice.IndexOf($fragment, [StringComparison]::Ordinal) -lt 0) { throw "item $id is not $fragment" }
}
$mine = @($items.Keys) + @($fam.Keys)
$ipath = Join-Path $GameData 'ItemTemplates.json'
$lines = [IO.File]::ReadAllLines($ipath, $u) | Where-Object { $_ -notmatch ('^\s*"(' + ($mine -join '|') + ')":') }
$add = @(foreach ($k in $items.Keys) { '    "{0}": {{"Templates": [{1},{2}]}},' -f $k, $items[$k][0], $items[$k][1] })
$add += @(foreach ($k in $fam.Keys) { '    "{0}": {{"Children": [{1}]}},' -f $k, (($fam[$k] | ForEach-Object { '"' + $_ + '"' }) -join ',') })
if ($lines[0].Trim() -ne '{') { throw 'ItemTemplates.json: unexpected first line' }
[IO.File]::WriteAllLines($ipath, [string[]](@($lines[0]) + $add + $lines[1..($lines.Count - 1)]), $u)

# ---------- 2. templates ----------
# Captured monsterData donors. None of these is the retail Steps of Madness body.
$mesh = @{
  eye    = 17653   # Eyemutant Bully EM01. Stand-in for the floating eyes.
  spider = 209354  # Weaver of Malice WROM. Stand-in for the arachnid horror.
  spirit = 217008  # Cascading Spirit. Stand-in for the two meatballs beside Neleb.
  figment= 100419  # Captured Figment of Imagination. One look for every figment hash.
  person = 26092   # Jobe scientist. Stand-in for Zyvania.
}
$curve = @(@(1,20),@(3,98),@(5,115),@(9,287),@(12,365),@(15,504),@(24,500),@(104,6236),@(139,10032),@(190,16798),@(214,20077))
function HealthAt([int]$lvl) {
  for ($i = 1; $i -lt $curve.Count; $i++) { $a = $curve[$i-1]; $b = $curve[$i]
    if ($lvl -le $b[0]) { $t = ([math]::Log($lvl) - [math]::Log($a[0])) / ([math]::Log($b[0]) - [math]::Log($a[0])); return [int][math]::Round([math]::Exp([math]::Log($a[1]) + $t * ([math]::Log($b[1]) - [math]::Log($a[1])))) } }
  $curve[-1][1]
}
function Row([string]$hash, [int]$chance, [int]$repeats = 1) {
  [ordered]@{ Hash = $hash; Repeats = $repeats; Chance = $chance; LevelMod = 0 }
}
function Pack([int]$etherChance, [string]$spec) {
  $loot = @()
  if ($spec) {
    foreach ($part in $spec.Split(',')) {
      $bits = $part.Split(':')
      $loot += Row $bits[0] ([int]$bits[1]) ([int]$bits[2])
    }
  }
  if ($etherChance -ge 0) {
    $loot += Row 'METH' $etherChance
    $loot += Row 'MMED' 8
    $loot += Row 'MPIL' 8
  }
  $loot += [ordered]@{ Hash = 'CMTR'; Repeats = 1; Chance = 50; LevelMod = 20 }
  $loot += [ordered]@{ Hash = 'CMHK'; Repeats = 1; Chance = 12; LevelMod = 20 }
  $loot += [ordered]@{ Hash = 'OFAB'; Repeats = 1; Chance = 1; LevelMod = 0 }
  $loot
}
# hash = name, mesh (0 keeps the human skeleton), scale, ether chance (-1 = no general ether/pills), "HASH:chance:repeats,...", note
$u100 = 10
$def = [ordered]@{
  GLGR=@('Gleeful Greeter',0,100,5,'','Entrance camp. Rarer ethers, 5 percent. Mesh is the human skeleton.')
  CHHO=@('Childhood Horror',$mesh.spider,55,$u100,'META:40:1','Arachnid ether 40 percent (guide: reliable, not 100). Spider mesh is Weaver of Malice.')
  FOOF=@('Foundation of Sanity',0,110,$u100,'METD:25:1,METB:25:1','Slow client respawn kept. Darkness and burning ethers 25 percent. Mesh is the human skeleton.')
  BIST=@('Brain Splitter',0,100,$u100,'','Name from Equip_BrainSplitterAtk. Mesh is the human skeleton.')
  FROF=@('Fragment of Sanity',0,100,$u100,'MGNH:25:1','Gentle Hands 25 percent (AO-Universe; rate not published). Mesh is the human skeleton.')
  SUEM=@('Suppressed Emotion',0,100,$u100,'MESP:40:1','Emotional Sponge 40 percent (guide: high chance, no number). Mesh is the human skeleton.')
  PUHA=@('Pulsing Hatred',0,100,$u100,'MNER:100:1,METT:25:1','Nervejolter 100 percent. Tidal ether 25 percent. Lava damage is not a scripted hazard.')
  UNFE=@('Unrelenting Fear',0,100,$u100,'MFBL:25:1,META:25:1','Fear-forged Blade 25 percent (rate not published) and arachnid ether 25 percent.')
  DEPS=@('Detached Psyche',0,100,$u100,'','General ethers only. Mesh is the human skeleton.')
  MIFR=@('Mind Fragment',0,90,$u100,'','Name from Equip_MindFragmentAtk. No pinned unique. Mesh is the human skeleton.')
  FMOI=@('Figment of Imagination',$mesh.figment,100,8,'','Figment look. One captured Figment mesh for every figment hash.')
  BLEY=@('Baleful Eye',$mesh.eye,80,$u100,'','Eyemutant mesh, stand-in for the floating eye.')
  MISH=@('Mind Shard',0,100,$u100,'MBRC:25:1,MDDR:20:1','Brainchopper 25 percent. Dark Dreams 20 percent (guide: sometimes).')
  FRNE=@('Frazzled Nerves',0,100,$u100,'','Name from Equip_FrazzledNervesAtk. General ethers only.')
  NEFE=@('Nebulous Feeling',0,100,$u100,'','Name from Equip_NebulousFeelingAtk. Not on the unique table. General ethers only.')
  ROEY=@('Roving Eye',$mesh.eye,75,$u100,'','Client attack item spells it Roaving Eye. Eyemutant mesh.')
  SAED=@("Sanity's Edge",0,100,$u100,'MLHV:25:1','Loving Hands 25 percent (AO-Universe; rate not published).')
  JEAL=@('Jealousy',0,105,$u100,'MJEA:25:1','Essence of Pure Jealousy 25 percent (rate not published).')
  GUOF=@('Guardian of Thought',0,100,$u100,'','Neleb add, already standing. General ethers only.')
  NOHA=@('Notum Habit',0,105,$u100,'MFLA:100:1,MDDR:20:1','Neutrino Flash 100 percent. Dark Dreams 20 percent (guide: sometimes).')
  BYOM=@('Betrayer of Memory',$mesh.spirit,70,$u100,'MCTX:25:1','Cortex of the Executioner 25 percent. The 1 percent on the guide is the crit on the item, not the drop rate. Spirit mesh stands in for the meatball.')
  THOF=@('Thief of Reason',$mesh.spirit,70,$u100,'MBRH:25:1','Brutal Hands 25 percent (AO-Universe; rate not published). Spirit mesh stands in for the meatball.')
  NETH=@('Neleb the Deranged',0,110,-1,'MROB:100:1,MBAT:100:1,MFRS:100:2,MDMC:100:1,MDDR:55:1','HP 15818 from the wiki. Two Fractured Sanity. Robe, battlerod, and Dream Mesh Circuit 100 percent. Dark Dreams 55 percent for one copy. Hide in Memory is not scripted.')
}
foreach ($h in @('FIOI','FIOF','FMOI','FMOF','FGOI','FGOF','FEOF')) {
  if ($h -eq 'FMOI') { continue }
  $def[$h] = $def['FMOI']
}
$npath = Join-Path $GameData 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
$skelPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\docs\accepted\npc\delmus\NpcTemplate.json'))
$skel = (([IO.File]::ReadAllText($skelPath, $u) | ConvertFrom-Json) | Where-Object { $_.Hash -eq 'DCQV' } | Select-Object -First 1).Stats
if (-not $skel) { throw 'DCQV skeleton missing' }
$sp = ([IO.File]::ReadAllText((Join-Path $GameData 'Playfields\1933\Spawns.json'), $u) | ConvertFrom-Json).spawns
$blue = 277352961
$report = @()
foreach ($h in @($def.Keys)) {
  $d = $def[$h]
  $pts = @($sp | Where-Object { $_.hashText -eq $h })
  if (-not $pts.Count) { throw "$h has no 1933 spawn point" }
  $lo = [int]($pts | Measure-Object minLevel -Minimum).Minimum
  $hi = [int]($pts | Measure-Object maxLevel -Maximum).Maximum
  $levels = @($lo)
  if ($hi -ne $lo) { $levels += $hi }
  $bands = @()
  foreach ($l in $levels) {
    $st = [ordered]@{}; foreach ($p in $skel.PSObject.Properties) { $st[$p.Name] = [int]$p.Value }
    $hp = $(if ($h -eq 'NETH') { 15818 } else { HealthAt $l })
    $mx = [math]::Max(3, [int][math]::Round($l * 1.5))
    $st['1'] = $hp; $st['27'] = $hp; $st['54'] = $l; $st['360'] = [int]$d[2]; $st['455'] = 136
    $st['285'] = $mx; $st['286'] = [math]::Max(1, [int][math]::Round($mx / 2)); $st['61'] = 10 * $l
    if ([int]$d[1] -gt 0) { $st['359'] = [int]$d[1] }
    $b = [ordered]@{ Name = [string]$d[0]; Level = $l; Stats = $st; LootTable = @(Pack ([int]$d[3]) ([string]$d[4])) }
    $b['ContentProvenance'] = [pscustomobject]@{
      Guessed = $true; Confirmed = $false; Donor = $(if ([int]$d[1] -gt 0) { [string]$d[1] } else { 'DCQV' })
      Source = [string]$d[5]; Levels = "$lo-$hi"
    }
    $bands += $b
  }
  $rts = @($pts | ForEach-Object { [int]$_.respawnTime } | Sort-Object -Unique) -join '/'
  $nt | Add-Member -NotePropertyName $h -NotePropertyValue ([ordered]@{ Templates = $bands }) -Force
  $report += "som $h $($d[0]) L$lo-$hi points=$($pts.Count) rt=$rts mesh=$($d[1])"
}
$missing = @($sp | ForEach-Object { $_.hashText } | Sort-Object -Unique | Where-Object { -not $def.Contains($_) })
if ($missing.Count) { throw ('1933 spawn hashes without template: ' + ($missing -join ' ')) }

# Zyvania Bagh: client hash ZYBA on Andromeda, on the AO-Universe pin. Talk teleports in. No second spawn.
$and = ([IO.File]::ReadAllText((Join-Path $GameData 'Playfields\655\Spawns.json'), $u) | ConvertFrom-Json).spawns
$zy = @($and | Where-Object { $_.hashText -eq 'ZYBA' })
if ($zy.Count -ne 1) { throw "expected one ZYBA spawn, found $($zy.Count)" }
$z = $zy[0]
if ([math]::Abs($z.position[0] - 3158.1) -gt 1 -or [math]::Abs($z.position[2] - 899.3) -gt 1) { throw 'ZYBA is not on the AO-Universe pin' }
if ($nt.PSObject.Properties['ZYBA'] -and $nt.ZYBA.Templates[0].Name -ne 'Zyvania Bagh') { throw 'ZYBA is already another NPC' }
$zl = [int]$z.minLevel; if ($zl -lt 1) { $zl = 1 }
$st = [ordered]@{}; foreach ($p in $skel.PSObject.Properties) { $st[$p.Name] = [int]$p.Value }
$st['0'] = $blue; $st['1'] = 200; $st['27'] = 200; $st['54'] = $zl; $st['359'] = $mesh.person; $st['360'] = 100; $st['61'] = 0
$zb = [ordered]@{ Name = 'Zyvania Bagh'; Level = $zl; Stats = $st; LootTable = @() }
$zb['ContentProvenance'] = [pscustomobject]@{
  Guessed = $true; Confirmed = $false; Donor = '26092'
  Source = 'Existing Andromeda spawn ZYBA at the AO-Universe pin 3158.1, 899.3. Scientist mesh stands in for her. Knubot ZyvaniaBagh teleports to 1933 40, 33.8, 170. No key.'
  Levels = "$($z.minLevel)-$($z.maxLevel)"
}
$nt | Add-Member -NotePropertyName 'ZYBA' -NotePropertyValue ([ordered]@{ Templates = @($zb) }) -Force
$report += ("som ZYBA Zyvania Bagh L{0} pos={1:N1},{2:N1},{3:N1} pf=655" -f $zl, $z.position[0], $z.position[1], $z.position[2])

[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 8) + "`n", $u)
$written = [IO.File]::ReadAllText($npath, $u)
if ($written.IndexOf('Aztur the Immortal') -lt 0 -or $written.IndexOf('Neleb the Deranged') -lt 0 -or $written.IndexOf('"ZYBA"') -lt 0) {
  throw 'NpcTemplates.json lost TotW, Neleb, or Zyvania'
}
$kpath = Join-Path $GameData 'Knubot\ZyvaniaBagh.json'
$knu = @'
{
  "Notes": "Zyvania Bagh, Andromeda hash ZYBA, at the AO-Universe pin. The Childhood Memory teddy bear is not a key. She teleports to the Steps of Madness entrance, beside the Gleeful Greeter. Generated by ddogs/tools/Build-StepsOfMadness.ps1.",
  "Npcs": [ "ZYBA" ],
  "Openers": [ { "Say": "hello" } ],
  "Replies": {
    "about": { "Text": "What do I need to enter?", "Goto": "about" },
    "ride": { "Text": "Send me in.", "Goto": "ride" },
    "leave": { "Text": "Not now.", "Goto": "leave" }
  },
  "Lines": {
    "hello": { "Text": "The eye in Omni Forest still opens. The blood-soaked teddy bear is only a souvenir now.", "Replies": [ "about", "ride", "leave" ] },
    "about": { "Text": "Steps of Madness is under that forest. There is no level lock, and there is no key.", "Replies": [ "ride", "leave" ] },
    "ride": { "Text": "{teleport 1933 40 33.8 170}{close}" },
    "leave": { "Text": "{close}" }
  }
}
'@
[IO.File]::WriteAllText($kpath, ($knu -replace "`r`n", "`n").Trim() + "`n", $u)
$report | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'Build-StepsOfMadness.last.txt')
"som items=$($items.Count)+$($fam.Count) templates=$($def.Count) zyvania=1 spawnpoints=$($sp.Count)"
