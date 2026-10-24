# ddogs/tools/Build-KarliAlienShip.ps1 - Karli Cappelleri (Arete Landing guide #20, AO-Universe) and her two alien missions.
# Real (AO-Universe / AOGalaxy): names, dialog, mob list and levels, rewards 2520 cr / 1386 xp / 3x 8-15 NCU Memory,
#   Nano Can: Friendly Buff = AOID 301250, quest name "Help From a Friend".
# Guessed: Karli's spot and look, the ship hatch (talk-to teleport, no client door), interior = playfield 8009
#   "Crashed Alien Ship" (fixed map, not random rooms), interior spawn spots, Alien General scale/level (mesh = Khaz spider per screenshot), extra crew names, team-mission reward,
#   NCU chip QL 30 (ids 36778/36780), solo completion by talking to Karli instead of using the can on a teammate.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
Remove-TypeData System.Array -ErrorAction SilentlyContinue  # PS5: arrays serialize as {value,Count} otherwise
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData)
$karliPos = @(45.4, 0.4, 41.5)            # Randy (in game): inside the crashed ship 8009, near Kalari's spot
$entryPos = @(3790.0, 0.6, 270.0)          # guessed: crash-site entry hatch on 6553 (Karli's old spot)
$shipIn  = @(38.0, 0.5, 28.0)              # landing just inside the south door of 8009 (2.4 m clear, ColProbe); 2026-10-18 moved from 40,44 so the arrival is clear
$shipOut = @(3790.0, 0.6, 262.0)           # beside the entry hatch
# --- templates
$npath = Join-Path $gd 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
function Clone($o) { $o | ConvertTo-Json -Depth 8 | ConvertFrom-Json }
$gen = Clone ($nt.CRDY.Templates | Sort-Object Level | Select-Object -Last 1)
$gen.Name = 'Alien General'; $gen.Level = 14; $gen.Stats.'54' = 14; $gen.Stats.'359' = 247742; $gen.Stats.'360' = 75
$gen.Stats.'1' = [int]$gen.Stats.'1' * 3; $gen.Stats.'27' = $gen.Stats.'1'
# 2026-10-18: the General lives in the generated ship interior (GameData/AuthoredDungeons.json ZXK1); no death trigger, the ship hatch is a fixed spawn by the south door
$gen | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = 'CRDY'; Source = 'Name real (AO-Universe Arete #20 "Alien General", level ?). Mesh 247742 "alien boss detached" = Khaz battle-spider without rider, matches Randy PRK screenshot (big mech spider, cyan lights). Scale 75, level 14, 3x HP guessed' })
$nt | Add-Member -Force NoteProperty ALGN ([pscustomobject]@{ Templates = @($gen) })
$zk = Clone ($nt.ZIXI.Templates | Sort-Object Level | Select-Object -Last 1)
$zk.Name = 'Alien Spider - Zix'; $zk.Level = 12; $zk.Stats.'54' = 12; $zk.Stats.'360' = 130
$zk | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = 'ZIXI'; Source = 'Team mission target: the Zix in the Alien General room (AO-Universe Arete #20). Level 12 and scale guessed' })
$nt | Add-Member -Force NoteProperty ZIXK ([pscustomobject]@{ Templates = @($zk) })
# extra ship crew, Rank - Class'Race per AO-Universe Alien Hierarchy. Names are valid hierarchy combos but NOT listed for Arete (guessed).
# race meshes: Sinuh = alien_mini_bug (frog), Uri = alien_battlesuit_centaur, Heru = alien_battlesuit_hover (snake)
$crew = @(
  @('KSAU', "Scout - Ankari'Sinuh", 11, 251784, 96, 'ACFJ', 'alien_mini_bug_caster_unrooted'),
  @('KGRU', "Guard - Rimah'Uri", 12, 251785, 110, 'CRDY', 'alien_battlesuit_centaur_melee_unrooted'),
  @('KCIU', "Commando - Ilari'Uri", 13, 251786, 115, 'CRDY', 'alien_battlesuit_centaur_melee_ranged_unrooted'),
  @('KGXH', "Guard - Xoch'Heru", 12, 251773, 140, 'CRDY', 'alien_battlesuit_hover_caster_unrooted')
)
$crewKeys = @()
foreach ($r in $crew) {
  $t = Clone ($nt.($r[5]).Templates | Sort-Object Level | Select-Object -Last 1)
  $t.Name = $r[1]; $t.Level = $r[2]; $t.Stats.'54' = $r[2]; $t.Stats.'359' = $r[3]; $t.Stats.'360' = $r[4]
  $t | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = $r[5]; Source = ('Name follows AO-Universe Alien Hierarchy (Rank - Class''Race); not in the Arete #20 mob list. Mesh ' + $r[3] + ' ' + $r[6] + ' from client RDB hints; level/scale guessed') })
  $nt | Add-Member -Force NoteProperty $r[0] ([pscustomobject]@{ Templates = @($t) })
  $crewKeys += $r[0]
}
# exit hatch template (talks via Knubot AlienShipHatch.json, bound by spawn hash SHXT)
$hx = Clone ($nt.CRDY.Templates | Sort-Object Level | Select-Object -Last 1)
$hx.Name = 'Ship Hatch'; $hx.Level = 1; $hx.Stats.'54' = 1; $hx.Stats.'359' = 253599; $hx.Stats.'360' = 60
$hx | Add-Member -Force NoteProperty Features @('NoCombat','NoMove')
$hx | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = 'CRDY'; Source = 'Exit hatch spawned by the Alien General death trigger (Randy request). Mesh 253599 alien defense turret as a stand-in hatch: guessed' })
$nt | Add-Member -Force NoteProperty SHXT ([pscustomobject]@{ Templates = @($hx) })
# healing towers next to the Alien General in the team ship (AuthoredDungeons TowerHash ALHT)
$ht = Clone $hx
$ht.Name = 'Healing Tower'; $ht.Stats.'360' = 90
$ht | Add-Member -Force NoteProperty Features @('NoMove')
$ht | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = 'SHXT'; Source = 'Master of Biological Metamorphoses healing tower (AO-Universe LE Alien Motherships): two towers next to the Alien General, heal it until killed, no respawn. Mesh 253599 alien defense turret; level set at spawn to the General''s; stats guessed' })
$nt | Add-Member -Force NoteProperty ALHT ([pscustomobject]@{ Templates = @($ht) })
$ourKeys = @('ALGN','ZIXK') + $crewKeys + @('SHXT','ALHT')
# insert as text so the rest of the 4 MB file keeps its formatting; our keys are appended at the end, so cut and re-append
$nraw = [IO.File]::ReadAllText($npath, $u) -replace "`r`n", "`n"
$cut = $nraw.IndexOf(",`n    `"ALGN`":")
if ($cut -ge 0) { $nraw = $nraw.Substring(0, $cut) + "`n}`n" }
$frag = ''
foreach ($k in $ourKeys) { $frag += ",`n    `"$k`": " + ($nt.$k | ConvertTo-Json -Depth 8) }
$end = $nraw.TrimEnd().LastIndexOf('}')
$last = $nraw.LastIndexOf('}', $end - 1)
[IO.File]::WriteAllText($npath, $nraw.Substring(0, $last + 1) + $frag + "`n}`n", $u)
# --- item hash for the can
$ipath = Join-Path $gd 'ItemTemplates.json'
$il = [IO.File]::ReadAllLines($ipath, $u) | Where-Object { $_ -notmatch '^\s*"FBCN":' }
[IO.File]::WriteAllLines($ipath, [string[]](@($il[0]) + '    "FBCN": {"Templates": [301250,301250]},' + $il[1..($il.Count-1)]), $u)
# --- quests
$qpath = Join-Path $gd 'Quests.json'
$q = @(); foreach ($x in (ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($qpath, $u)))) { if ($x.Hash -notin 'FBUD','ZXK1') { $q += $x } }
$used = $q | ForEach-Object { $_.MissionBit } | Where-Object { $_ -ne $null }
if ($used -contains 663 -or $used -contains 664) { throw 'bits 663/664 taken' }
$ncu = [pscustomobject]@{ Id = 36778; HighId = 36780; Name = '8 - 15 NCU Memory'; Quality = 30 }
# SolveHash W4XY: the can's SolveQuest (items.dat 301250) names mission W4XY; using it on a teammate completes FBUD.
$q += [pscustomobject]@{ Hash='FBUD'; SolveHash='W4XY'; Name='Help From a Friend'; QasInstance=920128; Scope='Solo'; Action='TargetNpc'
  Summary='Team up and use the Nano Can: Friendly Buff on your teammate'
  Objective=[pscustomobject]@{ Count=$null; Npc='FBTM'; SpecItem=$null; WorldItem=$null }; Flags='None'; MissionBit=663; NextStep=$null
  Reward=[pscustomobject]@{ Hash='RFBD'; Credits=2520; Xp=1386; Records=@(,@($ncu,$ncu,$ncu)) } }
$q += [pscustomobject]@{ Hash='ZXK1'; Name='The Alien Ship'; QasInstance=920129; Scope='Team'; Action='KillOne'
  Summary='Enter the crashed ship and kill the Alien Spider - Zix in the Alien General''s room'
  Objective=[pscustomobject]@{ Count=$null; Npc='ZIXK'; SpecItem=$null; WorldItem=$null }; Flags='None'; MissionBit=664; NextStep=$null
  Reward=[pscustomobject]@{ Hash='RZK1'; Credits=1500; Xp=1500; Records=$null } }
[IO.File]::WriteAllText($qpath, (ConvertTo-Json -InputObject @($q) -Depth 8) + "`n", $u)
# --- content NPCs
function Npc($pf, $hex, $name, $pos, $donor, $hash, $key) {
  $n = Clone $donor
  $n.Key = $key; $n.Provenance = 'ddogs/tools/Build-KarliAlienShip.ps1 (guessed look, cloned from ' + $donor.Name + ')'
  $n.ContentNpcIdentity = 'SimpleChar:' + $hex; $n.InstanceId = [Convert]::ToInt32($hex, 16); $n.PlayfieldId = $pf
  $n.Name = $name; $n.Position = $pos; $n.Rotation = @(0,0,0,1); $n.Attackable = $false; $n.Passive = $true
  $n | Add-Member -Force NoteProperty SpawnHash $hash
  $n
}
$cpath = Join-Path $gd 'PlayfieldContent\6553\Npcs.json'
$c = [IO.File]::ReadAllText($cpath, $u) | ConvertFrom-Json
$sarah = $c.Npcs | Where-Object Name -eq 'Sarah Greene'
$term = $c.Npcs | Where-Object Name -eq 'Shipping Manifest Terminal'
$entry = Npc 6553 '7A0EC003' 'Crashed Ship Hatch' $entryPos $term 'SHEN' 'ddogs:alienship:entry'
$c.Npcs = @($c.Npcs | Where-Object { $_.Key -notlike 'ddogs:alienship:*' }) + $entry
[IO.File]::WriteAllText($cpath, ($c | ConvertTo-Json -Depth 8) + "`n", $u)
$d8 = Join-Path $gd 'PlayfieldContent\8009'; New-Item -ItemType Directory -Force $d8 | Out-Null
$karliNpc = Npc 8009 '7A0EC001' 'Karli Cappelleri' $karliPos $sarah 'KRLI' 'ddogs:alienship:karli'
# look: Uri centaur battlesuit (rooted) as the alien-disguise costume. The veteran/loyalty-store costume item was not found in the client RDB: guessed
$karliNpc.Stats.'359' = 247464; $karliNpc.Stats.'360' = 100; $karliNpc.Meshes = @(); $karliNpc.Textures = @()
$karliNpc.Provenance = 'ddogs/tools/Build-KarliAlienShip.ps1 (spot from Randy; look = Uri centaur mesh 247464 standing in for the alien disguise costume, guessed)'
[IO.File]::WriteAllText((Join-Path $d8 'Npcs.json'), ([pscustomobject]@{ SchemaVersion = 1; PlayfieldId = 8009; Npcs = @($karliNpc); StandaloneShops = @() } | ConvertTo-Json -Depth 8) + "`n", $u)
# --- interior spawns (Playfields is gitignored; rerun this script after a data re-extract)
$spath = Join-Path $gd 'Playfields\8009\Spawns.json'
$s = [IO.File]::ReadAllText($spath, $u) | ConvertFrom-Json
# 2026-10-18: the ship's collision (Surfaces.dat) is a closed hull x 25.96-53.95, z 24.65-55.66 with the walkable floor about x 29-51, z 28-52.
# The old spots put ACFJ (54,52) and KSAU (46,54) inside the hull wall and eight more outside it; every spot below is open floor with
# at least 1.9 m clearance to any wall (ColProbe over 8009 collision). Landing (40,44) and Karli (45.4,41.5) are left clear.
# 2026-10-18 (Randy): 8009 is only the entrance room: Karli, the native YZWI and one Zix by the back (north) door, 20+ m from the
# landing; the crew, the General and the mission Zix are in the generated interior behind the 'Crashed Alien Ship' door
# (Karli gives it: {givedungeon ZXK1}, GameData/AuthoredDungeons.json). SHXT is the way back out to 6553, by the south door.
$adds = @( @('ZIXI',10,32,0.5,48), @('SHXT',1,36,0.5,28) )
# floor: 8009 heightmap is flat 0; native spawn YZWI sits at y=0.498, so 0.5 is the ship floor
$s.spawns = @($s.spawns | Where-Object { $_.hash -ne 0 -or $_.hashText -notin (@('ZIXI','ACFJ','CRDY') + $ourKeys) })
foreach ($a in $adds) {
  $s.spawns += [pscustomobject]@{ districtIndex=0; hash=0; hashText=$a[0]; manifestHash=0; minLevel=$a[1]; maxLevel=$a[1]; respawnChance=100; flags=1
    respawnTime= $(if ($a[0] -eq 'SHXT') { 30 } else { 600 }); version7Extra=0; hasOptionalFlagBlock=$true; nativeFlags=0; moreFlags=0; assistanceRadius=0
    unknownOptionalU8=0; angle=0; angleW=1; position=@([double]$a[2],[double]$a[3],[double]$a[4]); radius=1; additionalPoints=@(); extensions=$null }
}
# YZWI (native L15 'To Be Determined' at 48.97,49.25) is Specialist - Cha'Heru L13 from Build-AreteAliens.ps1 (AO-Universe: ship aliens L11-13).
# Its native respawn is 30 s; 600 s for a small server (Randy 2026-10-18 17:14: the ship 'keeps respawning').
foreach ($x in @($s.spawns | Where-Object { $_.hashText -eq 'YZWI' })) { $x.minLevel = 13; $x.maxLevel = 13; $x.respawnTime = 600 }
[IO.File]::WriteAllText($spath, ($s | ConvertTo-Json -Depth 8) + "`n", $u)
# --- dialog
$kd = Join-Path $gd 'Knubot'
$karli = [ordered]@{
  Notes = 'Karli Cappelleri, inside the crashed alien ship 8009 (AO-Universe Arete #20). Dialog from the guide; spot from Randy; solo turn-in guessed. Generated by ddogs/tools/Build-KarliAlienShip.ps1.'
  Npcs = @('SimpleChar:7A0EC001')
  Openers = @( [ordered]@{ If = @('HasQuest FBUD'); Say = 'fb_back' }, [ordered]@{ Say = 'hello' } )
  Replies = [ordered]@{
    what = [ordered]@{ Text = 'What are you doing?'; Goto = 'spy' }
    fight = [ordered]@{ Text = 'I want to fight aliens too!'; If = @('!HasAnyQuest FBUD','!HasCompletedQuest FBUD'); Goto = 'friends' }
    nofriends = [ordered]@{ Text = "But, I don't have any friends..."; Goto = 'give_can' }
    ready = [ordered]@{ Text = 'We are ready to help you out.'; Goto = 'solo_pick' }
    teamready = [ordered]@{ Text = 'My team is ready to storm the ship.'; Goto = 'team_pick' }
    s_easy = [ordered]@{ Text = 'Take it easy on us.'; Goto = 'solo_e' }
    s_med = [ordered]@{ Text = 'We can handle our business.'; Goto = 'team' }
    s_hard = [ordered]@{ Text = 'Give us the mission the others are too chicken to take.'; Goto = 'solo_h' }
    t_easy = [ordered]@{ Text = 'Take it easy on us.'; Goto = 'team_e' }
    t_med = [ordered]@{ Text = 'We can handle our business.'; Goto = 'team_big' }
    t_hard = [ordered]@{ Text = 'Give us the mission the others are too chicken to take.'; Goto = 'team_h' }
    used = [ordered]@{ Text = 'I found a teammate and used the can on them.'; Goto = 'fb_done' }
    notyet = [ordered]@{ Text = 'Not yet.'; Goto = 'bye' }
    nothing = [ordered]@{ Text = 'Never mind.'; Goto = 'bye' }
  }
  Lines = [ordered]@{
    hello = [ordered]@{ Text = "I'm a little busy here..."; Replies = @('what','nothing') }
    spy = [ordered]@{ Text = "I'm an alien spy. I find and terminate aliens... Quietly!"; Replies = @('fight','ready','teamready','nothing') }
    friends = [ordered]@{ Text = 'I think you should find some friends to help you out for this one.'; Replies = @('nofriends') }
    give_can = [ordered]@{ Text = @('You know we have a lookup system to find people with similar interests here on Rubi-Ka. Just open your team search interface. Or perhaps just look for someone around you that you can start a team with.', '{spawn FBCN 1}', '{givequest FBUD}', '{delay 1}Here, take this Friendly Buff Nano can and see if you can find a friend to use it on.'); Replies = @('nothing') }
    fb_back = [ordered]@{ Text = 'Did you find a friend for that Friendly Buff can?'; Replies = @('notyet') }
    fb_done = [ordered]@{ Text = @('{completequest FBUD}', 'See? Rubi-Ka is friendlier with a team. Take these memory chips.'); Replies = @('nothing') }
    team = [ordered]@{ Text = @('{givedungeon ZXK1}', 'The Zix hides in the Alien General''s room, deep inside the ship. Take this key and go through the Crashed Alien Ship door at the back of this room. Try not to blow my cover.'); Replies = @('nothing') }
    team_big = [ordered]@{ Text = @('{givedungeon ZXT1}', 'The General itself is holed up in the arena at the bottom of the ship, three decks down. Every one of your team gets the mission and a key; go through the Crashed Alien Ship door together.'); Replies = @('nothing') }
    solo_pick = [ordered]@{ Text = 'How tough do you want it?'; Replies = @('s_easy','s_med','s_hard','nothing') }
    team_pick = [ordered]@{ Text = 'How tough does your team want it?'; Replies = @('t_easy','t_med','t_hard','nothing') }
    solo_e = [ordered]@{ Text = @('{givedungeon ZXKE}', 'The crew is a bit below you. The Zix hides in the Alien General''s room, deep inside the ship. Take this key and go through the Crashed Alien Ship door at the back of this room.'); Replies = @('nothing') }
    solo_h = [ordered]@{ Text = @('{givedungeon ZXKH}', 'The crew is above you and the General is nasty. The Zix hides in its room, deep inside the ship. Take this key and go through the Crashed Alien Ship door at the back of this room.'); Replies = @('nothing') }
    team_e = [ordered]@{ Text = @('{givedungeon ZXTE}', 'Easy, then. The General waits in the arena three decks down, with two healing towers. Kill the towers first. Every one of your team gets the mission and a key.'); Replies = @('nothing') }
    team_h = [ordered]@{ Text = @('{givedungeon ZXTH}', 'Your funeral. The General waits in the arena three decks down, with two healing towers. Kill the towers first. Every one of your team gets the mission and a key.'); Replies = @('nothing') }
    bye = [ordered]@{ Text = 'Keep your head down.{close}' }
  }
}
[IO.File]::WriteAllText((Join-Path $kd 'KarliCappelleri.json'), ($karli | ConvertTo-Json -Depth 8) + "`n", $u)
$hs = [ordered]@{
  Notes = 'Crashed alien ship exit (8009), a fixed spawn by the south door (the client exit door there has no destination). Guessed: talk-to teleport back beside the entry hatch. Generated by ddogs/tools/Build-KarliAlienShip.ps1.'
  Npcs = @('SHXT')
  Openers = @( [ordered]@{ Say = 'hello' } )
  Replies = [ordered]@{ out = [ordered]@{ Text = 'Leave the ship.'; Goto = 'out' }; stay = [ordered]@{ Text = 'Stay.'; Goto = 'stay' } }
  Lines = [ordered]@{
    hello = [ordered]@{ Text = 'The hatch hums. Daylight leaks through the seam.'; Replies = @('out','stay') }
    out = [ordered]@{ Text = ('{teleport 6553 ' + ($shipOut -join ' ') + '}{close}') }
    stay = [ordered]@{ Text = '{close}' }
  }
}
[IO.File]::WriteAllText((Join-Path $kd 'AlienShipHatch.json'), ($hs | ConvertTo-Json -Depth 8) + "`n", $u)
$en = [ordered]@{
  Notes = 'Crashed alien ship entry on 6553 near the crash site. Guessed: talk-to teleport into 8009. Generated by ddogs/tools/Build-KarliAlienShip.ps1.'
  Npcs = @('SimpleChar:7A0EC003')
  Openers = @( [ordered]@{ Say = 'hello' } )
  Replies = [ordered]@{ in = [ordered]@{ Text = 'Climb inside.'; Goto = 'in' }; stay = [ordered]@{ Text = 'Stay out.'; Goto = 'stay' } }
  Lines = [ordered]@{
    hello = [ordered]@{ Text = 'A torn hatch in the hull. Something moves inside.'; Replies = @('in','stay') }
    in = [ordered]@{ Text = ('{teleport 8009 ' + ($shipIn -join ' ') + '}{close}') }
    stay = [ordered]@{ Text = '{close}' }
  }
}
[IO.File]::WriteAllText((Join-Path $kd 'AlienShipEntry.json'), ($en | ConvertTo-Json -Depth 8) + "`n", $u)
# repo files are LF
foreach ($p in @($npath, $ipath, $qpath, $cpath, (Join-Path $d8 'Npcs.json'), (Join-Path $kd 'KarliCappelleri.json'), (Join-Path $kd 'AlienShipHatch.json'), (Join-Path $kd 'AlienShipEntry.json'))) { [IO.File]::WriteAllText($p, ([IO.File]::ReadAllText($p, $u) -replace "`r`n", "`n"), $u) }
'karli ship content written'
& (Join-Path $PSScriptRoot 'Build-AreteWildlife.ps1') -GameData $gd

