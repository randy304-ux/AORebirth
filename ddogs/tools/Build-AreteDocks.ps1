# ddogs/tools/Build-AreteDocks.ps1 - Arete docks (Marcus Stone's gas fires + wounded dockworkers), Mario Carles' car, Saltworm look.
# Called at the end of Build-ShoulderpadsJunkyard.ps1. Re-patches Knubot/MarcusStone.json, so rerun it after Build-NewbieQuests.ps1.
# Real (AO-Universe Arete guide #2, #17; Randy live screenshots): Gas Fires on the containers, Wounded Dockworkers lying on the deck,
#   a Dockworker by the door, Marcus' wounded-worker errand (1040 cr, up to 1281 xp, 50 rechargers + 25 stims), Mario Carles L10 in a white car
#   wandering the desert, Saltworm a big teal worm at the lake surface.
# Guessed: hash mapping GAFR=Gas Fire, WODO=Wounded Dockworker, IESY=Dockworker; fire mesh 234586 sl_campfire; Dockworker look;
#   talk-to on the fire / worker instead of using the item on them (no item-on-NPC hook); 1 recharger + 1 stim (ids 291082/291043) instead of stacks;
#   Mario as the car itself (mesh 203157 yalmaha_executive, no crowbar step), his desert spot; Saltworm mesh 205942 abyss serpent, scale 220.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
Remove-TypeData System.Array -ErrorAction SilentlyContinue
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData)
$npath = Join-Path $gd 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
function Clone($o) { $o | ConvertTo-Json -Depth 8 | ConvertFrom-Json }
function Prov($t, $donor, $src) { $t | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = $donor; Source = $src }) }
function Obj($name, $lvl, $mesh, $scale, $src) {
  $t = Clone ($nt.ADAF.Templates | Select-Object -First 1); $t.Name = $name; $t.Level = $lvl; $t.Stats.'54' = $lvl; $t.Stats.'359' = $mesh; $t.Stats.'360' = $scale
  $t | Add-Member -Force NoteProperty Features @('NoCombat','NoMove'); $t | Add-Member -Force NoteProperty LootTable @(); Prov $t 'ADAF' $src; $t
}
$new = [ordered]@{}
$new.GAFR = [pscustomobject]@{ Templates = @(Obj 'Gas Fire' 1 234586 100 'Gas Fire on the dock containers (AO-Universe #2, Randy screenshot). Hash GAFR, mesh sl_campfire 234586: guessed') }
$w = Obj 'Wounded Dockworker' 1 296008 100 'Wounded Dockworker lying on the deck (AO-Universe #2, Randy screenshot). Mesh 296008 "wounded dock worker" from the client; hash WODO: guessed'
$w.Stats.'173' = 11; $w.Stats.'174' = 11
$new.WODO = [pscustomobject]@{ Templates = @($w) }
$new.IESY = [pscustomobject]@{ Templates = @(Obj 'Dockworker' 5 26092 100 'Dockworker by the dock door (Randy screenshot). Hash IESY, borrowed look: guessed') }
# in-place edits: Mario's car, Saltworm look
foreach ($t in @($nt.MARC.Templates)) { $t.Stats.'359' = 203157; $t.Stats.'360' = 100; $t | Add-Member -Force NoteProperty Features @('Wander'); Prov $t 'MARC' 'Mario Carles L10 in a white car wandering the desert (AO-Universe #17, Randy). Mesh 203157 yalmaha_executive; spot near 3700,640: guessed' }
foreach ($t in @($nt.XUDP.Templates)) { $t.Stats.'359' = 17712; $t.Stats.'360' = 100; $t.Stats.'1' = 1248; $t.Stats.'27' = 1248; $t.Stats | Add-Member -Force NoteProperty '33' 3; $t.Stats | Add-Member -Force NoteProperty '204' 1; $t.Stats | Add-Member -Force NoteProperty '455' 0 }   # sandworm 17712 (226557 giant sandworm has the burrow feature bit and sat underground), sensible HP, aggro (2026-10-16)
$nraw = [IO.File]::ReadAllText($npath, $u) -replace "`r`n", "`n"
$cut = $nraw.IndexOf(",`n    `"GAFR`":")
if ($cut -ge 0) { $nraw = $nraw.Substring(0, $cut) + "`n}`n" }
foreach ($k in 'MARC','XUDP') {
  $km = [regex]::Match($nraw, "`n[ ]*`"" + $k + "`":"); if (-not $km.Success) { throw "key $k not found" }
  $start = $km.Index
  $m = [regex]::new("`n {0,4}`"[^`"]{1,6}`":").Match($nraw, $start + 5)
  $endIdx = if ($m.Success) { $m.Index } else { $nraw.TrimEnd().LastIndexOf('}') }
  $seg = $nraw.Substring($start, $endIdx - $start)
  $comma = if ($seg.TrimEnd().EndsWith(',')) { ',' } else { '' }
  $nraw = $nraw.Substring(0, $start) + "`n    `"$k`": " + ($nt.$k | ConvertTo-Json -Depth 8) + $comma + $nraw.Substring($endIdx)
}
$frag = ''; foreach ($k in $new.Keys) { $frag += ",`n    `"$k`": " + ($new.$k | ConvertTo-Json -Depth 8) }
$end = $nraw.TrimEnd().LastIndexOf('}'); $last = $nraw.LastIndexOf('}', $end - 1)
[IO.File]::WriteAllText($npath, $nraw.Substring(0, $last + 1) + $frag + "`n}`n", $u)
# spawns: Mario out into the desert
$spath = Join-Path $gd 'Playfields\6553\Spawns.json'
$s = [IO.File]::ReadAllText($spath, $u) | ConvertFrom-Json
foreach ($sp in $s.spawns) { if ($sp.hashText -eq 'MARC') { $sp.position = @(3700.0, 32.5, 640.0); $sp.radius = 40; $sp.respawnTime = 600 } }
[IO.File]::WriteAllText($spath, ($s | ConvertTo-Json -Depth 8) + "`n", $u)
# quests 702-704
$qpath = Join-Path $gd 'Quests.json'
$q = @(); foreach ($x in (ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($qpath, $u)))) { if ($x.Hash -notin 'GFOT','WDK1','WDK2') { $q += $x } }
$used = $q | ForEach-Object { $_.MissionBit } | Where-Object { $_ -ne $null }
foreach ($b in 702,703,704) { if ($used -contains $b) { throw "bit $b taken" } }
function Qs($h, $n, $inst, $npc, $bit, $rw) { [pscustomobject]@{ Hash=$h; Name=$n; QasInstance=$inst; Scope='Solo'; Action='TargetNpc'; Summary=$n; Objective=[pscustomobject]@{ Count=$null; Npc=$npc; SpecItem=$null; WorldItem=$null }; Flags='NoCash'; MissionBit=$bit; NextStep=$null; Reward=$rw } }
$q += Qs 'GFOT' 'Extinguish the Gas Fire' 920172 'GAFR' 702 ([pscustomobject]@{ Hash='RGFO'; Credits=0; Xp=0; Records=$null })
$q += Qs 'WDK1' 'Use the stim on a Wounded Dockworker' 920173 'WODO' 703 ([pscustomobject]@{ Hash='RWD1'; Credits=0; Xp=0; Records=$null })
$wd2 = Qs 'WDK2' 'Return to Marcus Stone' 920174 'AMST' 704 ([pscustomobject]@{ Hash='RWD2'; Credits=1040; Xp=1281; Records=@(,@(
  [pscustomobject]@{ Id=291082; HighId=291082; Name='Health and Nano Recharger'; Quality=1 }, [pscustomobject]@{ Id=291043; HighId=291043; Name='Health and Nano Stim'; Quality=1 })) })
$wd2.Flags = 'None'; $q += $wd2
[IO.File]::WriteAllText($qpath, (ConvertTo-Json -InputObject @($q) -Depth 8) + "`n", $u)
# dialog
$kd = Join-Path $gd 'Knubot'
$fire = [ordered]@{ Notes = 'Gas Fire on the Arete dock containers (Marcus Stone, AO-Universe #2). Talk-to stands in for dragging the suppressant onto it: guessed. Generated by ddogs/tools/Build-AreteDocks.ps1.'
  Npcs = @('GAFR'); Openers = @( [ordered]@{ If = @('HasQuest GFOT'); Say = 'burn' }, [ordered]@{ Say = 'hot' } )
  Replies = [ordered]@{ spray = [ordered]@{ Text = 'Empty the Compact Fire Suppressant Container onto the flames.'; Goto = 'out' }; back = [ordered]@{ Text = 'Step back.'; Goto = 'shut' } }
  Lines = [ordered]@{ burn = [ordered]@{ Text = 'Gas hisses out of the container and burns blue.'; Replies = @('spray','back') }
    out = [ordered]@{ Text = @('{completequest GFOT}', 'The suppressant foams over the valve and the fire chokes out. Take the container back to Marcus Stone.{close}') }
    hot = [ordered]@{ Text = 'Gas hisses out of the container and burns blue. Too hot to touch.{close}' }; shut = [ordered]@{ Text = '{close}' } } }
$wk = [ordered]@{ Notes = 'Wounded Dockworker (Marcus Stone''s errand, AO-Universe #2). Talk-to stands in for using the stim on him: guessed. Generated by ddogs/tools/Build-AreteDocks.ps1.'
  Npcs = @('WODO'); Openers = @( [ordered]@{ If = @('HasQuest WDK1'); Say = 'hurt' }, [ordered]@{ Say = 'groan' } )
  Replies = [ordered]@{ stim = [ordered]@{ Text = 'Use the Health Regeneration Stim on him.'; Goto = 'up' }; leave = [ordered]@{ Text = 'Leave him.'; Goto = 'shut' } }
  Lines = [ordered]@{ hurt = [ordered]@{ Text = 'The dockworker groans. His leg is bent the wrong way.'; Replies = @('stim','leave') }
    up = [ordered]@{ Text = @('{completequest WDK1}', '{givequest WDK2}', 'The stim kicks in. He staggers up, mutters a thank-you and limps off. Marcus will want to hear.{close}') }
    groan = [ordered]@{ Text = 'The dockworker groans and clutches his side.{close}' }; shut = [ordered]@{ Text = '{close}' } } }
$dw = [ordered]@{ Notes = 'Dockworker by the dock door (Randy screenshot). Flavor text guessed. Generated by ddogs/tools/Build-AreteDocks.ps1.'
  Npcs = @('IESY'); Openers = @( [ordered]@{ Say = 'hi' } ); Replies = [ordered]@{}
  Lines = [ordered]@{ hi = [ordered]@{ Text = 'Mind the fires up top. Marcus is trying to keep this deck from going up.{close}' } } }
foreach ($x in @(@('GasFire.json',$fire),@('WoundedDockworker.json',$wk),@('Dockworker.json',$dw))) { [IO.File]::WriteAllText((Join-Path $kd $x[0]), ($x[1] | ConvertTo-Json -Depth 8) + "`n", $u) }
# Marcus: fire must be out before the hand-in; wounded-worker errand
$mp = Join-Path $kd 'MarcusStone.json'
$mj = [IO.File]::ReadAllText($mp, $u) | ConvertFrom-Json
$mj.Lines.give_fire.Text = @('{spawn QCFS 1}', '{givequest AMRR}', '{givequest GFOT}', '{delay 1}The fires are on top of the containers. Put one out, then bring the container back to me.')
$mj.Lines.offer_fire.Text = @('Take this Compact Fire Suppressant Container and put out one of the gas fires on the containers.', '{delay 1}Then bring the container back to me and see Flint Novak.')
$mj.Lines.busy_fire.Text = 'Put out a gas fire on the containers with that suppressant, then hand the container back.'
$mj.Trades.turnin_fire.If = @('HasQuest AMRR', 'HasCompletedQuest GFOT')
$mj.Lines.nothing.Text = 'Then get moving.{close}'
$mj.Replies | Add-Member -Force NoteProperty ask_wounded ([pscustomobject]@{ Text = 'Are those wounded workers your guys?'; If = @('!HasCompletedQuest WDK2', '!HasAnyQuest WDK1,WDK2'); Goto = 'offer_wounded' })
$mj.Replies | Add-Member -Force NoteProperty done_wounded ([pscustomobject]@{ Text = 'I patched one of them up.'; If = @('HasQuest WDK2'); Goto = 'done_wounded' })
$mj.Lines | Add-Member -Force NoteProperty offer_wounded ([pscustomobject]@{ Text = @('They are. Accidents happen fast on this deck.', '{delay 1}Take this Health Regeneration Stim and use it on one of them. Over to the west, lying on the deck.', '{givequest WDK1}'); Replies = @('bye') })
$mj.Lines | Add-Member -Force NoteProperty done_wounded ([pscustomobject]@{ Text = @('{completequest WDK2}', 'Good. Take these rechargers and stims, you will need them out here.'); Replies = @('more','nothing') })
$mj.Lines.hello.Replies = @(@($mj.Lines.hello.Replies | Where-Object { $_ -notin 'ask_wounded','done_wounded','nothing' }) + 'ask_wounded','done_wounded','nothing')
$mj.Lines.busy_fire.Replies = @(@($mj.Lines.busy_fire.Replies | Where-Object { $_ -ne 'done_wounded' }) + 'done_wounded')
$mj.Notes = $mj.Notes + ' Patched by ddogs/tools/Build-AreteDocks.ps1: gas fire objects (GFOT) and the wounded-dockworker errand (WDK1/WDK2).'
[IO.File]::WriteAllText($mp, ($mj | ConvertTo-Json -Depth 8) + "`n", $u)
foreach ($p in @($npath, $qpath, $mp, (Join-Path $kd 'GasFire.json'), (Join-Path $kd 'WoundedDockworker.json'), (Join-Path $kd 'Dockworker.json'))) { [IO.File]::WriteAllText($p, ([IO.File]::ReadAllText($p, $u) -replace "`r`n", "`n"), $u) }
'docks/mario/saltworm written'
& (Join-Path $PSScriptRoot 'Build-AreteReetsFactions.ps1') -GameData $gd
