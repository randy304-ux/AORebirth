# ddogs/tools/Build-AreteReetsFactions.ps1 - Greedy Desert Reet / Leet King quests, Gnarl the Roller, ICC HQ faction-area NPC names.
# Called at the end of Build-AreteDocks.ps1.
# Real (AO-Universe Arete #16, #21; wiki.aodb.us; Randy live screenshots): Shiny Sword (297289) dropped by local mobs, given to the Greedy Desert Reet
#   for Nano Crystal (Composite Nano Expertise) 223381; Leet King: kill the Greedy Desert Reet / smash his treasure with the Sledgehammer (297045),
#   return the hammer, bring gifts to Princess Leet, biomesh reward (Light Bio Mesh 246925); Gnarl the Roller wanders the beach edge;
#   faction-area NPC names (ICC Peacekeeper, Omni-AF Private, Omni-Pol Guard, Clan Protester, Clan Guard Elina Jones, Omni-AF Officer Milne,
#   Omni-Trans Equipment Vendor, Field Commander Ericson, Vanguard Henri Zure, Clan Unit Sentinel).
# Guessed: every pink-code -> name mapping (by code letters, cluster and count), all looks (human mesh 26092; Omni-Trans vendor 250380 from 4582),
#   Gnarl = YBSH (L7, 180 s, 3426,748) at scale 170; Leet King and Princess Leet spots; sword drop 8%; sword hand-in by trade instead of right-click
#   mission; treasure smashing skipped (kill the Greedy Desert Reet is the step); gifts not itemised; fixed Light Bio Mesh instead of random.
#   EJVC (To Be Determined, inside the Greedy Desert Reet) and HEZU (same spot as VAHE) removed as overlaps. Faction recruitment quests NOT built.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
Remove-TypeData System.Array -ErrorAction SilentlyContinue
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData)
$npath = Join-Path $gd 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
function Clone($o) { $o | ConvertTo-Json -Depth 8 | ConvertFrom-Json }
function Prov($t, $donor, $src) { $t | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = $donor; Source = $src }) }
function Npc($donor, $name, $lvl, $mesh, $scale, $src) {
  $t = Clone ($nt.$donor.Templates | Select-Object -First 1); $t.Name = $name; $t.Level = $lvl; $t.Stats.'54' = $lvl
  if ($mesh) { $t.Stats.'359' = $mesh }; if ($scale) { $t.Stats.'360' = $scale }
  $t | Add-Member -Force NoteProperty Features @('NoCombat','NoMove'); $t | Add-Member -Force NoteProperty LootTable @(); Prov $t $donor $src; [pscustomobject]@{ Templates = @($t) }
}
$new = [ordered]@{}
$fa = @(
  @('OIUR','ICC Peacekeeper',10),@('CPPS','Clan Protester',20),@('CSPE','Clan Protester',20),@('ONGA','Omni-Pol Guard',20),
  @('LUSN','Omni-AF Private',25),@('KLHG','Omni-AF Private',20),@('DART','Omni-AF Private',20),@('OMCN','Omni-AF Officer Milne',35),
  @('CSPT','Clan Unit Sentinel',20),@('CAGD','Clan Guard Elina Jones',25),@('FDCS','Field Commander Ericson',35),@('VAHE','Vanguard Henri Zure',10))
foreach ($f in $fa) { $new.($f[0]) = Npc 'ADAF' $f[1] $f[2] 26092 100 ("ICC HQ faction area (Randy live screenshot). Pink $($f[0]) mapping and look guessed") }
$new.ODDD = Npc 'OITA' 'Omni-Trans Equipment Vendor' 40 250380 100 'Omni-Trans counter (Randy screenshot); look from the 4582 vendor OITA; pink ODDD mapping guessed; shop not wired'
$new.LKNG = Npc 'LE04' 'Leet King' 10 17655 140 'Leet King (AO-Universe #16). Spot and scale guessed'
$new.PRLT = Npc 'LE04' 'Princess Leet' 8 17655 115 'Princess Leet (AO-Universe #16). Spot and scale guessed'
$g = Clone ($nt.WSSC.Templates | Select-Object -First 1); $g.Name = 'Gnarl the Roller'; $g.Level = 7; $g.Stats.'54' = 7; $g.Stats.'360' = 170
$g.Stats.'1' = [int]$g.Stats.'1' * 4; $g.Stats.'27' = $g.Stats.'1'; $g | Add-Member -Force NoteProperty Features @('Wander')
Prov $g 'WSSC' 'Gnarl the Roller, rollerrat boss wandering the desert edge / beach near town (wiki.aodb.us, AO forum; Randy). Hash YBSH (L7, 180 s), level, scale 170, 4x HP guessed; rollerrat-part loot from WSSC'
$new.YBSH = [pscustomobject]@{ Templates = @($g) }
# in-place: Shiny Sword on Desert Reets and Rollerrats
$nraw = [IO.File]::ReadAllText($npath, $u) -replace "`r`n", "`n"
$cut = $nraw.IndexOf(",`n    `"OIUR`":")
if ($cut -ge 0) { $nraw = $nraw.Substring(0, $cut) + "`n}`n" }
foreach ($k in 'DERT','WSSC') {
  foreach ($t in @($nt.$k.Templates)) { $t | Add-Member -Force NoteProperty LootTable @(@($t.LootTable | Where-Object { $_ -and $_.Hash -ne 'SHSW' }) + [pscustomobject]@{ Hash='SHSW'; Repeats=1; Chance=8; LevelMod=0 }) }
  $km = [regex]::Match($nraw, "`n {0,4}`"" + $k + "`":"); if (-not $km.Success) { throw "key $k not found" }
  $start = $km.Index; $m = [regex]::new("`n {0,4}`"[^`"]{1,6}`":").Match($nraw, $start + 5)
  $endIdx = if ($m.Success) { $m.Index } else { $nraw.TrimEnd().LastIndexOf('}') }
  $comma = if ($nraw.Substring($start, $endIdx - $start).TrimEnd().EndsWith(',')) { ',' } else { '' }
  $nraw = $nraw.Substring(0, $start) + "`n    `"$k`": " + ($nt.$k | ConvertTo-Json -Depth 8) + $comma + $nraw.Substring($endIdx)
}
$frag = ''; foreach ($k in $new.Keys) { $frag += ",`n    `"$k`": " + ($new.$k | ConvertTo-Json -Depth 8) }
$end = $nraw.TrimEnd().LastIndexOf('}'); $last = $nraw.LastIndexOf('}', $end - 1)
[IO.File]::WriteAllText($npath, $nraw.Substring(0, $last + 1) + $frag + "`n}`n", $u)
$ipath = Join-Path $gd 'ItemTemplates.json'
$il = [IO.File]::ReadAllLines($ipath, $u) | Where-Object { $_ -notmatch '^\s*"(SHSW|SLDG)":' }
[IO.File]::WriteAllLines($ipath, [string[]](@($il[0]) + '    "SHSW": {"Templates": [297289,297289]},' + '    "SLDG": {"Templates": [297045,297045]},' + $il[1..($il.Count-1)]), $u)
# spawns
$spath = Join-Path $gd 'Playfields\6553\Spawns.json'
$s = [IO.File]::ReadAllText($spath, $u) | ConvertFrom-Json
$s.spawns = @($s.spawns | Where-Object { $_.hashText -notin 'EJVC','HEZU','LKNG','PRLT' })
foreach ($sp in $s.spawns) { if ($sp.hashText -eq 'YBSH') { $sp.respawnTime = 180; $sp.radius = 40 } }
foreach ($a in @(@('LKNG',10,3345.0,4.3,640.0), @('PRLT',8,3335.4,0.8,600.0))) {
  $s.spawns += [pscustomobject]@{ districtIndex=1; hash=0; hashText=$a[0]; manifestHash=0; minLevel=$a[1]; maxLevel=$a[1]; respawnChance=100; flags=1; respawnTime=60; version7Extra=0; hasOptionalFlagBlock=$false; nativeFlags=0; moreFlags=0; assistanceRadius=0; unknownOptionalU8=0; angle=0; angleW=1; position=@($a[2],$a[3],$a[4]); radius=1; additionalPoints=@(); extensions=$null }
}
[IO.File]::WriteAllText($spath, ($s | ConvertTo-Json -Depth 8) + "`n", $u)
# quests 705-707
$qpath = Join-Path $gd 'Quests.json'
$q = @(); foreach ($x in (ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($qpath, $u)))) { if ($x.Hash -notin 'SSW1','LKQ1','LKQ2') { $q += $x } }
$used = $q | ForEach-Object { $_.MissionBit } | Where-Object { $_ -ne $null }
foreach ($b in 705,706,707) { if ($used -contains $b) { throw "bit $b taken" } }
function Rec($id, $name) { [pscustomobject]@{ Id = $id; HighId = $id; Name = $name; Quality = 10 } }
$q += [pscustomobject]@{ Hash='SSW1'; Name='The Shiny Sword'; QasInstance=920175; Scope='Solo'; Action='FindItem'; Summary='Give the Shiny Sword to the Greedy Desert Reet'
  Objective=[pscustomobject]@{ Count=$null; Npc='GRDS'; SpecItem='SHSW'; WorldItem=$null }; Flags='None'; MissionBit=705; NextStep=$null
  Reward=[pscustomobject]@{ Hash='RSSW'; Credits=1400; Xp=2170; Records=@(,@((Rec 223381 'Nano Crystal (Composite Nano Expertise)'))) } }
$q += [pscustomobject]@{ Hash='LKQ1'; Name='The wrath of the Leet King'; QasInstance=920176; Scope='Solo'; Action='KillOne'; Summary='Kill the Greedy Desert Reet and smash his stolen treasure'
  Objective=[pscustomobject]@{ Count=$null; Npc='GRDS'; SpecItem=$null; WorldItem=$null }; Flags='NoCash'; MissionBit=706; NextStep=$null
  Reward=[pscustomobject]@{ Hash='RLK1'; Credits=0; Xp=500; Records=$null } }
$q += [pscustomobject]@{ Hash='LKQ2'; Name='A queen for the king'; QasInstance=920177; Scope='Solo'; Action='TargetNpc'; Summary='Bring the Leet King''s gifts to Princess Leet'
  Objective=[pscustomobject]@{ Count=$null; Npc='PRLT'; SpecItem=$null; WorldItem=$null }; Flags='None'; MissionBit=707; NextStep=$null
  Reward=[pscustomobject]@{ Hash='RLK2'; Credits=0; Xp=1000; Records=@(,@((Rec 246925 'Light Bio Mesh'))) } }
[IO.File]::WriteAllText($qpath, (ConvertTo-Json -InputObject @($q) -Depth 8) + "`n", $u)
# dialog (lines from the AO-Universe guide)
$kd = Join-Path $gd 'Knubot'
function New-Reply($t, $g, $if) { $r = [ordered]@{ Text = $t; Goto = $g }; if ($if) { $r.If = @($if) }; $r }
$reet = [ordered]@{ Notes = 'Greedy Desert Reet (AO-Universe #16). Sword hand-in by trade (retail: Shiny Sword item mission): guessed. Generated by ddogs/tools/Build-AreteReetsFactions.ps1.'
  Npcs = @('GRDS'); Openers = @( [ordered]@{ Say = 'hello' } )
  Replies = [ordered]@{ what = (New-Reply 'What?' 'sword' @('!HasCompletedQuest SSW1')); give = (New-Reply 'Here, take my sword.' 'take'); bye = (New-Reply 'Goodbye.' 'bye') }
  Lines = [ordered]@{ hello = [ordered]@{ Text = 'Can I have ur sword?'; Replies = @('what','bye') }
    sword = [ordered]@{ Text = 'UR SWORD!! GIVE IT!'; Replies = @('give','bye') }
    take = [ordered]@{ Text = @('{givequest SSW1}', 'WAT!??! Shiny stuff for me? Z0mg!{opentrade}') }
    done = [ordered]@{ Text = @('{completequest SSW1}', 'Oh so SHINY! I HAS UR SWORD!!!{close}') }
    bye = [ordered]@{ Text = '{close}' } }
  Trades = [ordered]@{ sword = [ordered]@{ If = @('HasQuest SSW1'); Items = @([ordered]@{ Hash = 'SHSW'; Count = 1 }); Goto = 'done' } } }
$king = [ordered]@{ Notes = 'Leet King (AO-Universe #16). Treasure smashing folded into killing the Greedy Desert Reet; gifts not itemised: guessed. Generated by ddogs/tools/Build-AreteReetsFactions.ps1.'
  Npcs = @('LKNG'); Openers = @( [ordered]@{ If = @('HasQuest LKQ1'); Say = 'waiting' }, [ordered]@{ If = @('HasCompletedQuest LKQ1','!HasAnyQuest LKQ2','!HasCompletedQuest LKQ2'); Say = 'hammer' }, [ordered]@{ If = @('HasQuest LKQ2'); Say = 'lonely' }, [ordered]@{ Say = 'hello' } )
  Replies = [ordered]@{ what = (New-Reply 'What are you?' 'king' @('!HasCompletedQuest LKQ1','!HasAnyQuest LKQ1')); rat = (New-Reply 'But you look like... A rat..' 'angry'); sorry = (New-Reply 'Sorry..' 'sulk')
    howto = (New-Reply 'Just tell me what to do to get your forgiveness, my king.' 'job'); ok = (New-Reply "I'll do it.." 'go'); did = (New-Reply 'I did it..' 'did'); where = (New-Reply 'Where?' 'where'); bye = (New-Reply 'Goodbye.' 'bye') }
  Lines = [ordered]@{ hello = [ordered]@{ Text = 'Wat?'; Replies = @('what','bye') }
    king = [ordered]@{ Text = 'I r teh king!'; Replies = @('rat') }
    angry = [ordered]@{ Text = 'Silence j00! I r teh King, j00 no speek to me liek dat! U maekz mad nao! U needz to deserve forgivenesseses!'; Replies = @('sorry') }
    sulk = [ordered]@{ Text = 'Dewd u sux. Now I r so angreh I dun wanna speek wif u againz! Go awayz!'; Replies = @('howto','bye') }
    job = [ordered]@{ Text = 'U haz makez teh king furiousez! U has to remove teh wrath, only way to do dat is annoyz teh Greedy Desert Reet and smash his treazurez! He stolez them and maed urs king vereh angreh!'; Replies = @('ok') }
    go = [ordered]@{ Text = @('{givequest LKQ1}', '{spawn SLDG 1}', 'U slowz noob. Wai u still standeh thar? Go nao!{close}') }
    waiting = [ordered]@{ Text = 'Wai u still here? Smash teh Greedy Desert Reet!{close}' }
    hammer = [ordered]@{ Text = 'Wat?'; Replies = @('did') }
    did = [ordered]@{ Text = 'Gief mah hammar backz!{opentrade}' }
    queen = [ordered]@{ Text = @('{givequest LKQ2}', '!U did gewd. Nao u hazs to fiend me a womanthing! Yus! Ofc king needzz a queen. And giev her chocolateseses and fl0warz fr0m teh kiiiing!'); Replies = @('where') }
    where = [ordered]@{ Text = 'U hazs eyeseses don''tz u? Hurry I r vereh loneleh!{close}' }
    lonely = [ordered]@{ Text = 'Hurry I r vereh loneleh!{close}' }
    bye = [ordered]@{ Text = '{close}' } }
  Trades = [ordered]@{ hammer = [ordered]@{ If = @('HasCompletedQuest LKQ1','!HasAnyQuest LKQ2','!HasCompletedQuest LKQ2'); Items = @([ordered]@{ Hash = 'SLDG'; Count = 1 }); Goto = 'queen' } } }
$prin = [ordered]@{ Notes = 'Princess Leet (AO-Universe #16). Reward fixed Light Bio Mesh (retail: random biomesh item): guessed. Generated by ddogs/tools/Build-AreteReetsFactions.ps1.'
  Npcs = @('PRLT'); Openers = @( [ordered]@{ Say = 'hello' } )
  Replies = [ordered]@{ gifts = (New-Reply 'I bring gifts from the king..' 'pretend' @('HasQuest LKQ2')); nothing = (New-Reply 'So I went through all this trouble for nothing...?' 'maybe'); bye = (New-Reply 'Goodbye.' 'bye') }
  Lines = [ordered]@{ hello = [ordered]@{ Text = 'I r tha princess!'; Replies = @('gifts','bye') }
    pretend = [ordered]@{ Text = 'Silleh humin. He iz no king, he juzt pretandz! Besidez he alwayzs bring giftzezes..'; Replies = @('nothing') }
    maybe = [ordered]@{ Text = @('{completequest LKQ2}', 'Maybeh.. He gotz me all diz stuffz... Kewl he muszt liekz meh! I found diz junkz in teh desart, u canz has it forz urz troublez! Movez along nao u. I haz a daet!{close}') }
    bye = [ordered]@{ Text = '{close}' } } }
foreach ($x in @(@('GreedyDesertReet.json',$reet),@('LeetKing.json',$king),@('PrincessLeet.json',$prin))) { [IO.File]::WriteAllText((Join-Path $kd $x[0]), ($x[1] | ConvertTo-Json -Depth 8) + "`n", $u) }
foreach ($p in @($npath, $ipath, $qpath)) { [IO.File]::WriteAllText($p, ([IO.File]::ReadAllText($p, $u) -replace "`r`n", "`n"), $u) }
'reets/gnarl/factions written'
& (Join-Path $PSScriptRoot 'Build-AreteMarket.ps1') -GameData $gd
