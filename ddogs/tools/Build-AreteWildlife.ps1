# ddogs/tools/Build-AreteWildlife.ps1 - Arete Landing (6553) wildlife fill-ins + rollerrat loot. Run after Build-KarliAlienShip.ps1 (it calls this).
# Real (AO-Universe Arete guide; AO forum "Arete Landing - The NEW Starter Isle"; wiki.aodb.us):
#   Angry Minibull L8-13 aggro/flocks; Harvey the Bully L10 aggro dyna, wanders near 3700,517 among minibulls;
#   Saltworm L13 aggro dyna, wanders around the southern desert lake; rollerrat drops: Rollerrat Intestines (248333),
#   Undamaged Piece of Rubbery Rollerrat Flesh (206886), Undamaged Roller-Rat tail (165274), Monster Parts (42640/42641);
#   Stowaway Rollerrats drop only credits (wiki) so they are left alone.
# Guessed: hash mapping WTSO=Angry Minibull, REGD=Harvey, XUDP=Saltworm, HX1V=crash-site corpses (levels/respawn match);
#   drop chances; corpse look (live alien meshes posed with movement mode 11 "Sleep" = lying), Saltworm mesh (sandworm 17712).
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
Remove-TypeData System.Array -ErrorAction SilentlyContinue
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData)
$npath = Join-Path $gd 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
function Clone($o) { $o | ConvertTo-Json -Depth 8 | ConvertFrom-Json }
function Prov($t, $donor, $src) { $t | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = $donor; Source = $src }) }
function Band($donorKey, $name, $level, $mesh, $scale, $features, $src) {
  $t = Clone ($nt.$donorKey.Templates | Sort-Object Level | Select-Object -Last 1)
  $t.Name = $name; $t.Level = $level; $t.Stats.'54' = $level
  if ($mesh) { $t.Stats.'359' = $mesh }; if ($scale) { $t.Stats.'360' = $scale }
  $t.Stats | Add-Member -Force NoteProperty '455' 0   # NPCFamily 0: non-zero families rendered see-through on RC-LT (2026-10-16, guess)
  if ($features) { $t | Add-Member -Force NoteProperty Features @($features) }
  Prov $t $donorKey $src; $t
}
$new = [ordered]@{}
# crash-site dead aliens (Randy: should be the dead alien bodies). No corpse mesh exists in the client RDB (1040023 has no dead/corpse alien records), so live meshes lying down.
$z = Band 'ZIXI' 'Dead Alien Spider - Zix' 10 247728 100 @('NoCombat','NoMove') 'Arete alien crash site corpses (Randy screenshot). Mesh 247728 alien_spider, CurrentMovementMode 11 (Sleep) to lie down: guessed'
$h = Band 'CRDY' "Dead Specialist - Cha'Heru" 10 247468 100 @('NoCombat','NoMove') 'Arete alien crash site corpses. Mesh 247468 alien_battlesuit_hover_melee (rooted), lying pose via movement mode 11: guessed'
foreach ($t in @($z, $h)) { $t.Stats.'173' = 11; $t.Stats.'174' = 11; $t.Stats.'1' = 1; $t.Stats.'27' = 1 }
$new.HX1V = [pscustomobject]@{ Templates = @($z) }
$new.HXHR = [pscustomobject]@{ Templates = @($h) }
# minibulls: 38387 (solid on RC-LT); 30360 'minibull' renders see-through (its record has flag 0x4000 the others lack)
$m8 = Band 'E03C' 'Angry Minibull' 8 38387 $null $null 'AO-Universe Arete: Angry Minibull (Aggro, Flocks, Level 8-13). Mesh 30360 minibull. Stats scaled from E03C donor'
$m13 = Band 'E03C' 'Angry Minibull' 13 38387 $null $null 'AO-Universe Arete: Angry Minibull (Aggro, Flocks, Level 8-13)'
$new.WTSO = [pscustomobject]@{ Templates = @($m8, $m13) }
$hv = Band 'E03C' 'Harvey the Bully' 10 38387 125 @('Wander') 'Harvey the Bully L10 aggro dyna (AO forum Arete guide; wiki.aodb.us). Hash REGD (L10, 180 s respawn, near 3700,517) mapping, scale 125 and 2x HP guessed'
$hv.Stats.'1' = 876; $hv.Stats.'27' = 876; $m8.Stats.'1' = 216; $m8.Stats.'27' = 216; $m13.Stats.'1' = 416; $m13.Stats.'27' = 416
foreach ($t in @($m8, $m13, $hv)) { $t.Stats | Add-Member -Force NoteProperty '33' 3; $t.Stats | Add-Member -Force NoteProperty '204' 1 }   # sensible HP + aggro (2026-10-16)
$new.REGD = [pscustomobject]@{ Templates = @($hv) }
$sw = Band 'E03C' 'Saltworm' 13 17712 110 @('Wander') 'Saltworm L13 aggro dyna, wanders around the southern desert lake (AO forum Arete guide; wiki.aodb.us). Hash XUDP (L13, 180 s respawn, 3466,629). Mesh 17712 sandworm, scale and 3x HP guessed'
$sw.Stats.'1' = [int]$sw.Stats.'1' * 3; $sw.Stats.'27' = $sw.Stats.'1'
$sw.LootTable = @([pscustomobject]@{ Hash='USLT'; Repeats=1; Chance=60; LevelMod=0 }, [pscustomobject]@{ Hash='MPRT'; Repeats=1; Chance=50; LevelMod=0 })
$new.XUDP = [pscustomobject]@{ Templates = @($sw) }
# --- append our keys at the end (cut at our first key on rerun)
$nraw = [IO.File]::ReadAllText($npath, $u) -replace "`r`n", "`n"
$cut = $nraw.IndexOf(",`n    `"HX1V`":")
if ($cut -ge 0) { $nraw = $nraw.Substring(0, $cut) + "`n}`n" }
# --- rollerrat loot, edited in place per key
$loot = @(
  [pscustomobject]@{ Hash='QRRI'; Repeats=1; Chance=35; LevelMod=0 },  # Rollerrat Intestines
  [pscustomobject]@{ Hash='RRFL'; Repeats=1; Chance=30; LevelMod=0 },  # Undamaged Piece of Rubbery Rollerrat Flesh
  [pscustomobject]@{ Hash='RRTL'; Repeats=1; Chance=15; LevelMod=0 },  # Undamaged Roller-Rat tail
  [pscustomobject]@{ Hash='MPRT'; Repeats=1; Chance=50; LevelMod=0 })  # Monster Parts (QL by level)
$ratKeys = @()
foreach ($p in $nt.PSObject.Properties) {
  if ($p.Name -in $new.Keys) { continue }
  $ts = @($p.Value.Templates); if ($ts.Count -eq 0) { continue }
  if (-not ($ts | Where-Object { $_.Name -match 'Rollerrat' -and $_.Name -notmatch 'Stowaway|Queen|Matriarch' })) { continue }
  foreach ($t in $ts) {
    $keep = @($t.LootTable | Where-Object { $_ -and $_.Hash -notin 'QRRI','RRFL','RRTL','MPRT' })
    $t | Add-Member -Force NoteProperty LootTable @($keep + $loot)
  }
  $start = $nraw.IndexOf("`n    `"" + $p.Name + "`":")
  if ($start -lt 0) { throw "key $($p.Name) not found" }
  $m = [regex]::new("`n {0,4}`"[^`"]{1,6}`":").Match($nraw, $start + 5)
  $endIdx = if ($m.Success) { $m.Index } else { $nraw.TrimEnd().LastIndexOf('}') }
  $seg = $nraw.Substring($start, $endIdx - $start)
  $comma = if ($seg.TrimEnd().EndsWith(',')) { ',' } else { '' }
  $nraw = $nraw.Substring(0, $start) + "`n    `"" + $p.Name + "`": " + ($p.Value | ConvertTo-Json -Depth 8) + $comma + $nraw.Substring($endIdx)
  $ratKeys += $p.Name
}
$frag = ''
foreach ($k in $new.Keys) { $frag += ",`n    `"$k`": " + ($new.$k | ConvertTo-Json -Depth 8) }
$end = $nraw.TrimEnd().LastIndexOf('}')
$last = $nraw.LastIndexOf('}', $end - 1)
[IO.File]::WriteAllText($npath, $nraw.Substring(0, $last + 1) + $frag + "`n}`n", $u)
# --- item hashes
$ipath = Join-Path $gd 'ItemTemplates.json'
$il = [IO.File]::ReadAllLines($ipath, $u) | Where-Object { $_ -notmatch '^\s*"(RRFL|RRTL|USLT)":' }
$add = @('    "RRFL": {"Templates": [206886,206886]},', '    "RRTL": {"Templates": [165274,165274]},', '    "USLT": {"Templates": [247786,247786]},')
[IO.File]::WriteAllLines($ipath, [string[]](@($il[0]) + $add + $il[1..($il.Count-1)]), $u)
# --- spawns: split crash corpses Zix/Heru, give the bosses a respawn timer (Playfields is gitignored; rerun after re-extract)
$spath = Join-Path $gd 'Playfields\6553\Spawns.json'
$s = [IO.File]::ReadAllText($spath, $u) | ConvertFrom-Json
$i = 0
foreach ($sp in $s.spawns) {
  if ($sp.hashText -in 'HX1V','HXHR') { $sp.hashText = $(if ($i % 2) { 'HXHR' } else { 'HX1V' }); $sp.hash = 0; $sp.respawnTime = 30; $i++ }
  if ($sp.hashText -in 'REGD','XUDP','IEGA') { $sp.respawnTime = 180 }   # 3 min dyna boss respawn
}
[IO.File]::WriteAllText($spath, ($s | ConvertTo-Json -Depth 8) + "`n", $u)
foreach ($p in @($npath, $ipath)) { [IO.File]::WriteAllText($p, ([IO.File]::ReadAllText($p, $u) -replace "`r`n", "`n"), $u) }
'arete wildlife written; rollerrat loot on: ' + ($ratKeys -join ',')
& (Join-Path $PSScriptRoot 'Build-ShoulderpadsJunkyard.ps1') -GameData $gd
