# ddogs/tools/Build-KarliAlienShip.ps1 - Karli Cappelleri (Arete Landing guide #20, AO-Universe) and her two alien missions.
# Real (AO-Universe / AOGalaxy): names, dialog, mob list and levels, rewards 2520 cr / 1386 xp / 3x 8-15 NCU Memory,
#   Nano Can: Friendly Buff = AOID 301250, quest name "Help From a Friend".
# Guessed: Karli's spot and look, the ship hatch (talk-to teleport, no client door), interior = playfield 8009
#   "Crashed Alien Ship" (fixed map, not random rooms), interior spawn spots, Alien General look/level, team-mission reward,
#   NCU chip QL 30 (ids 36778/36780), solo completion by talking to Karli instead of using the can on a teammate.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
Remove-TypeData System.Array -ErrorAction SilentlyContinue  # PS5: arrays serialize as {value,Count} otherwise
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData)
$karliPos = @(3790.0, 0.6, 270.0)          # guessed: centre of the Zix crash-site spawns
$shipIn  = @(44.0, 0.5, 44.0)              # guessed landing inside 8009
$shipOut = @(3790.0, 0.6, 262.0)           # beside Karli
# --- templates
$npath = Join-Path $gd 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
function Clone($o) { $o | ConvertTo-Json -Depth 8 | ConvertFrom-Json }
$gen = Clone ($nt.CRDY.Templates | Sort-Object Level | Select-Object -Last 1)
$gen.Name = 'Alien General'; $gen.Level = 14; $gen.Stats.'54' = 14; $gen.Stats.'360' = 140
$gen.Stats.'1' = [int]$gen.Stats.'1' * 3; $gen.Stats.'27' = $gen.Stats.'1'
$gen | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = 'CRDY'; Source = 'AO-Universe Arete #20 lists Alien General (level ?). Specialist Cha-Heru mesh 251772 scaled 140, level 14, 3x HP' })
$nt | Add-Member -Force NoteProperty ALGN ([pscustomobject]@{ Templates = @($gen) })
$zk = Clone ($nt.ZIXI.Templates | Sort-Object Level | Select-Object -Last 1)
$zk.Name = 'Alien Spider - Zix'; $zk.Level = 12; $zk.Stats.'54' = 12; $zk.Stats.'360' = 130
$zk | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = 'ZIXI'; Source = 'Team mission target: the Zix in the Alien General room (AO-Universe Arete #20). Level 12 and scale guessed' })
$nt | Add-Member -Force NoteProperty ZIXK ([pscustomobject]@{ Templates = @($zk) })
# insert as text so the rest of the 4 MB file keeps its formatting
$nraw = [IO.File]::ReadAllText($npath, $u)
if ($nraw -notmatch '"ALGN":') {
  $frag = ''
  foreach ($k in 'ALGN','ZIXK') { $frag += ",`n    `"$k`": " + ($nt.$k | ConvertTo-Json -Depth 8) }
  $end = $nraw.TrimEnd().LastIndexOf('}')
  $last = $nraw.LastIndexOf('}', $end - 1)
  [IO.File]::WriteAllText($npath, $nraw.Substring(0, $last + 1) + $frag + "`n}`n", $u)
}
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
$q += [pscustomobject]@{ Hash='FBUD'; Name='Help From a Friend'; QasInstance=920128; Scope='Solo'; Action='TargetNpc'
  Summary='Use the Nano Can: Friendly Buff on a teammate, then tell Karli Cappelleri'
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
$c.Npcs = @($c.Npcs | Where-Object { $_.Key -notlike 'ddogs:alienship:*' }) + (Npc 6553 '7A0EC001' 'Karli Cappelleri' $karliPos $sarah 'KRLI' 'ddogs:alienship:karli')
[IO.File]::WriteAllText($cpath, ($c | ConvertTo-Json -Depth 8) + "`n", $u)
$d8 = Join-Path $gd 'PlayfieldContent\8009'; New-Item -ItemType Directory -Force $d8 | Out-Null
$hatch = Npc 8009 '7A0EC002' 'Ship Hatch' @(40.0, 0.5, 44.0) $term 'SHHT' 'ddogs:alienship:hatch'
[IO.File]::WriteAllText((Join-Path $d8 'Npcs.json'), ([pscustomobject]@{ SchemaVersion = 1; PlayfieldId = 8009; Npcs = @($hatch); StandaloneShops = @() } | ConvertTo-Json -Depth 8) + "`n", $u)
# --- interior spawns (Playfields is gitignored; rerun this script after a data re-extract)
$spath = Join-Path $gd 'Playfields\8009\Spawns.json'
$s = [IO.File]::ReadAllText($spath, $u) | ConvertFrom-Json
$adds = @(
  @('ZIXI',10,50,0.5,40), @('ZIXI',11,56,0.5,44), @('ACFJ',11,54,0.5,52), @('ACFJ',13,58,0.5,50),
  @('CRDY',12,60,0.5,56), @('CRDY',13,52,0.5,58), @('ALGN',14,62,0.5,62), @('ZIXK',12,60,0.5,64)
)
$s.spawns = @($s.spawns | Where-Object { $_.hash -ne 0 -or $_.hashText -notin 'ZIXI','ACFJ','CRDY','ALGN','ZIXK' })
foreach ($a in $adds) {
  $s.spawns += [pscustomobject]@{ districtIndex=0; hash=0; hashText=$a[0]; manifestHash=0; minLevel=$a[1]; maxLevel=$a[1]; respawnChance=100; flags=1
    respawnTime= $(if ($a[0] -eq 'ZIXK') { 60 } else { 300 }); version7Extra=0; hasOptionalFlagBlock=$true; nativeFlags=0; moreFlags=0; assistanceRadius=0
    unknownOptionalU8=0; angle=0; angleW=1; position=@([double]$a[2],[double]$a[3],[double]$a[4]); radius=1; additionalPoints=@(); extensions=$null }
}
[IO.File]::WriteAllText($spath, ($s | ConvertTo-Json -Depth 8) + "`n", $u)
# --- dialog
$kd = Join-Path $gd 'Knubot'
$karli = [ordered]@{
  Notes = 'Karli Cappelleri, Arete Landing alien crash site (AO-Universe Arete #20). Dialog from the guide; hatch teleport and solo turn-in guessed. Generated by ddogs/tools/Build-KarliAlienShip.ps1.'
  Npcs = @('SimpleChar:7A0EC001')
  Openers = @( [ordered]@{ If = @('HasQuest FBUD'); Say = 'fb_back' }, [ordered]@{ Say = 'hello' } )
  Replies = [ordered]@{
    what = [ordered]@{ Text = 'What are you doing?'; Goto = 'spy' }
    fight = [ordered]@{ Text = 'I want to fight aliens too!'; If = @('!HasAnyQuest FBUD','!HasCompletedQuest FBUD'); Goto = 'friends' }
    nofriends = [ordered]@{ Text = "But, I don't have any friends..."; Goto = 'give_can' }
    ready = [ordered]@{ Text = 'We are ready to help you out.'; If = @('!HasQuest ZXK1'); Goto = 'team' }
    enter = [ordered]@{ Text = 'Get me into the ship.'; If = @('HasQuest ZXK1'); Goto = 'enter' }
    used = [ordered]@{ Text = 'I found a teammate and used the can on them.'; Goto = 'fb_done' }
    notyet = [ordered]@{ Text = 'Not yet.'; Goto = 'bye' }
    nothing = [ordered]@{ Text = 'Never mind.'; Goto = 'bye' }
  }
  Lines = [ordered]@{
    hello = [ordered]@{ Text = "I'm a little busy here..."; Replies = @('what','nothing') }
    spy = [ordered]@{ Text = "I'm an alien spy. I find and terminate aliens... Quietly!"; Replies = @('fight','ready','enter','nothing') }
    friends = [ordered]@{ Text = 'I think you should find some friends to help you out for this one.'; Replies = @('nofriends') }
    give_can = [ordered]@{ Text = @('You know we have a lookup system to find people with similar interests here on Rubi-Ka. Just open your team search interface. Or perhaps just look for someone around you that you can start a team with.', '{spawn FBCN 1}', '{givequest FBUD}', '{delay 1}Here, take this Friendly Buff Nano can and see if you can find a friend to use it on.'); Replies = @('nothing') }
    fb_back = [ordered]@{ Text = 'Did you find a friend for that Friendly Buff can?'; Replies = @('used','notyet') }
    fb_done = [ordered]@{ Text = @('{completequest FBUD}', 'See? Rubi-Ka is friendlier with a team. Take these memory chips.'); Replies = @('nothing') }
    team = [ordered]@{ Text = @('{givequest ZXK1}', 'Enter the crashed ship and do your killing there. Try not to blow my cover.'); Replies = @('enter','nothing') }
    enter = [ordered]@{ Text = ('{teleport 8009 ' + ($shipIn -join ' ') + '}Quietly, now.{close}') }
    bye = [ordered]@{ Text = 'Keep your head down.{close}' }
  }
}
[IO.File]::WriteAllText((Join-Path $kd 'KarliCappelleri.json'), ($karli | ConvertTo-Json -Depth 8) + "`n", $u)
$hs = [ordered]@{
  Notes = 'Crashed alien ship exit (8009). Guessed: no client door; talk-to teleport back beside Karli. Generated by ddogs/tools/Build-KarliAlienShip.ps1.'
  Npcs = @('SimpleChar:7A0EC002')
  Openers = @( [ordered]@{ Say = 'hello' } )
  Replies = [ordered]@{ out = [ordered]@{ Text = 'Leave the ship.'; Goto = 'out' }; stay = [ordered]@{ Text = 'Stay.'; Goto = 'stay' } }
  Lines = [ordered]@{
    hello = [ordered]@{ Text = 'The hatch hums. Daylight leaks through the seam.'; Replies = @('out','stay') }
    out = [ordered]@{ Text = ('{teleport 6553 ' + ($shipOut -join ' ') + '}{close}') }
    stay = [ordered]@{ Text = '{close}' }
  }
}
[IO.File]::WriteAllText((Join-Path $kd 'AlienShipHatch.json'), ($hs | ConvertTo-Json -Depth 8) + "`n", $u)
# repo files are LF
foreach ($p in @($npath, $ipath, $qpath, $cpath, (Join-Path $d8 'Npcs.json'), (Join-Path $kd 'KarliCappelleri.json'), (Join-Path $kd 'AlienShipHatch.json'))) { [IO.File]::WriteAllText($p, ([IO.File]::ReadAllText($p, $u) -replace "`r`n", "`n"), $u) }
'karli ship content written'

