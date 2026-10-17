# ddogs/tools/Build-AreteLiveFixes.ps1 - live fixes that no other builder makes (synced from live 2026-10-16). Called last by Build-NpcTemplates.ps1.
# Fleas/dynas, Waste Collectors and minibulls keep their original models (launcher TextureFix makes them opaque). Crash-site aliens HX1V/HXHR keep NPCFamily 220. JUCO keeps the guide name.
#   YBSH carries the Shoulderpads drop SHSW at 8%.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
Remove-TypeData System.Array -ErrorAction SilentlyContinue
$u = [Text.UTF8Encoding]::new($false)
$npath = Join-Path ([IO.Path]::GetFullPath($GameData)) 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
function SetStat($t, $k, $v) { $t.Stats | Add-Member -Force NoteProperty $k $v }
# Original models restored 2026-10-16 15:15: launcher 2026.10.16-1515 rewrites their textures opaque (TextureFix), Randy confirmed solid on RC-PC.
# Earlier builders still set the gunbeetle/junkbot/38387 stand-ins; this runs last and puts the real meshes back.
$orig = @{ VUFL = @(17657, 100); FCTO = @(17657, 100); JNME = @(17657, 100); IEGA = @(17657, 160); D011 = @(17657, 100); D017 = @(17657, 162); FLFE = @(17657, 100)
           WTCO = @(17714, 92); '01V1' = @(17714, 75); SCWS = @(17714, 130); WTSO = @(30360, $null); REGD = @(30360, $null) }
foreach ($k in $orig.Keys) { foreach ($t in @($nt.$k.Templates)) { if ($t) { SetStat $t '359' $orig[$k][0]; if ($orig[$k][1]) { SetStat $t '360' $orig[$k][1] }; SetStat $t '455' 0 } } }

foreach ($k in 'HX1V','HXHR') { foreach ($t in @($nt.$k.Templates)) { if ($t) { SetStat $t '455' 220 } } }
foreach ($t in @($nt.JUCO.Templates)) { if ($t) { $t.Name = 'Cleanmeister Intelligence Robot' } }
foreach ($t in @($nt.YBSH.Templates)) { if ($t -and -not ($t.LootTable | Where-Object Hash -eq 'SHSW')) { $t.LootTable = @($t.LootTable) + [pscustomobject][ordered]@{ Hash = 'SHSW'; Repeats = 1; Chance = 8; LevelMod = 0 } } }
# Saltworm (XUDP) never fought back: it sits in the lake, off the navmesh, so every target was "unreachable" and it evaded (healed, dropped hate).
# GUESS: rooted (NoMove, like a turret) so it fights from where it is, with WeaponRange (380) +400% so its bite reaches the shore.
foreach ($t in @($nt.XUDP.Templates)) { if ($t) { $t | Add-Member -Force NoteProperty Features @('NoMove'); SetStat $t '380' 400 } }

# Emiel Dijkman (AO-Universe #18) on ALPG; the bronto pens EJIW become Bronto Mama (mesh 17662 minibronto scaled up: guessed).
function NewNpc($key, $name, $lvl, $mesh, $scale, $feat, $src) {
  $t = $nt.ADAF.Templates[0] | ConvertTo-Json -Depth 10 | ConvertFrom-Json; $t.Name = $name; $t.Level = $lvl; SetStat $t '54' $lvl; SetStat $t '359' $mesh; SetStat $t '360' $scale
  $t | Add-Member -Force NoteProperty Features @($feat); $t | Add-Member -Force NoteProperty LootTable @()
  $t | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = 'ADAF'; Source = $src })
  $nt | Add-Member -Force NoteProperty $key ([pscustomobject]@{ Templates = @($t) }) }
NewNpc 'ALPG' 'Emiel Dijkman' 15 26092 100 @('NoCombat','NoMove') 'Arete 3488,456 by the bronto pens (AO-Universe #18 at 3488,454). Pink mapping and look guessed'
NewNpc 'EJIW' 'Bronto Mama' 1 17662 200 @('NoMove') 'Bronto pens Arete 3504/3511,447 (Randy). Mesh 17662 minibronto at 200% guessed'
# Looks from the guide screenshots (/workspace/ao-ref): Fausto white shirt + black trousers, Emiel dark clothes. Items guessed.
foreach ($t in @($nt.FACS.Templates)) { if ($t) { $t | Add-Member -Force NoteProperty Equipment @(@(287552,287552), @(27378,27378)) } }
foreach ($t in @($nt.ALPG.Templates)) { if ($t) { $t | Add-Member -Force NoteProperty Equipment ([object[]]@(,[int[]]@(41028,41028))) } }
$gd = [IO.Path]::GetFullPath($GameData); $qpath = Join-Path $gd 'Quests.json'
$qall = [IO.File]::ReadAllText($qpath, $u) | ConvertFrom-Json
$q = @($qall | ForEach-Object { $_ } | Where-Object { $_.Hash -notin 'EMB1','EMB2','EMBH','EMBL' })
function Rq($h, $n, $qi, $sum, $npc, $bit, $rh, $cr, $xp, $recs) { [pscustomobject][ordered]@{ Hash=$h; Name=$n; QasInstance=$qi; Scope='Solo'; Action='TargetNpc'; Summary=$sum
  Objective=[pscustomobject]@{ Count=$null; Npc=$npc; SpecItem=$null; WorldItem=$null }; Flags='NoCash'; MissionBit=$bit; NextStep=$null
  Reward=[pscustomobject]@{ Hash=$rh; Credits=$cr; Xp=$xp; Records=$recs } } }
function Rec($id, $n) { [pscustomobject]@{ Id=$id; HighId=$id; Name=$n; Quality=10 } }
# Repeatable per the guide. The guide's reward is the Heavy or Light Bio Mesh choice, not credits or XP.
# Emiel's Box of Foodstuff 297048 (item hash EBOX) is given by Emiel ({spawn}) and taken by Fausto ({take}).
$q = @($q | Where-Object { $_.Hash -ne 'EMB2' })
$q += Rq 'EMB1' "Deliver Emiel's box" 920181 "Bring Emiel's Box of Foodstuff to Logistics Manager Fausto in the Bazaar" 'FACS' 711 'REB1' 0 0 @()
$q += Rq 'EMB2' "Deliver Emiel's box" 920184 "Bring Emiel's Box of Foodstuff to Logistics Manager Fausto in the Bazaar" 'FACS' 714 'REB2' 0 0 @()
$q += Rq 'EMBH' 'Heavy Bio Mesh tip' 920182 'Ask Fausto for a tip' 'FACS' 712 'REBH' 0 0 @(,@((Rec 246926 'Heavy Bio Mesh')))
$q += Rq 'EMBL' 'Light Bio Mesh tip' 920183 'Ask Fausto for a tip' 'FACS' 713 'REBL' 0 0 @(,@((Rec 246925 'Light Bio Mesh')))
[IO.File]::WriteAllText($qpath, (ConvertTo-Json -InputObject $q -Depth 8) + "`n", $u)
$ipath = Join-Path $gd 'ItemTemplates.json'; $iraw = [IO.File]::ReadAllText($ipath, $u)
if ($iraw -notmatch '"EBOX"') { $e = $iraw.TrimEnd().LastIndexOf('}'); $nl = $(if ($iraw.Contains("`r`n")) { "`r`n" } else { "`n" })
  $iraw = $iraw.Substring(0, $e).TrimEnd() + ",$nl    `"EBOX`": { `"Templates`": [ 297048, 297048 ] }$nl}$nl"; [IO.File]::WriteAllText($ipath, $iraw, $u) }
$kd = Join-Path $gd 'Knubot'
$emiel = [ordered]@{ Notes="Emiel Dijkman (AO-Universe #18); dialog from the guide. Generated by ddogs/tools/Build-AreteLiveFixes.ps1."; Npcs=@('ALPG'); Openers=@(@{ Say='hello' })
  Replies=[ordered]@{ work=@{ Text="I'm looking for some honest work."; If=@('!HasAnyQuest EMB1,EMB2'); Goto='offer' }; yes1=@{ Text="I'll do it."; If=@('!HasCompletedQuest EMB1'); Goto='give1' }; yes2=@{ Text="I'll do it."; If=@('HasCompletedQuest EMB1'); Goto='give2' }; bye=@{ Text='Goodbye.'; Goto='shut' } }
  Lines=[ordered]@{ hello=@{ Text='Hello there, traveller.'; Replies=@('work','bye') }
    offer=@{ Text='You have come to the right place then. I have some goodies that need delivery to the Logistics manager in the bazaar. He enjoys food as much as I do. *Emiel smiles*'; Replies=@('yes1','yes2','bye') }
    give1=@{ Text=@('{givequest EMB1}','{spawn EBOX 1}','Perfect, here take this box and locate Fausto Cesario in the Bazaar area and hand it to him.{close}') }
    give2=@{ Text=@('{givequest EMB2}','{spawn EBOX 1}','Perfect, here take this box and locate Fausto Cesario in the Bazaar area and hand it to him.{close}') } } }
$fausto = [ordered]@{ Notes='Logistics Manager Fausto (AO-Universe #18 delivery); dialog from the guide. Generated by ddogs/tools/Build-AreteLiveFixes.ps1.'; Npcs=@('FACS'); Openers=@(@{ Say='hello' })
  Replies=[ordered]@{ box1=@{ Text='Emiel told me to give you this box of foodstuffs.'; If=@('HasQuest EMB1','has EBOX'); Goto='hand1' }
    box2=@{ Text='Emiel told me to give you this box of foodstuffs.'; If=@('HasQuest EMB2','has EBOX'); Goto='hand2' }
    over=@{ Text="(Hand over Emiel's Box of Foodstuff)"; Goto='taken' }; tip=@{ Text='What about a little tip for my efforts?'; Goto='tipoffer' }
    heavy=@{ Text='The Heavy Bio Mesh.'; Goto='heavy' }; light=@{ Text='The Light Bio Mesh.'; Goto='light' }; bye=@{ Text='Goodbye.'; Goto='shut' } }
  Lines=[ordered]@{ hello=@{ Text='Well well, a newcomer?'; Replies=@('box1','box2','bye') }
    hand1=@{ Text='Hand it over.'; Replies=@('over1') }; hand2=@{ Text='Hand it over.'; Replies=@('over2') }
    taken=@{ Text='Perfect, this was just what I needed. Emiel''s super tasty food. My day is saved.'; Replies=@('tip','bye') }
    tipoffer=@{ Text='I found some old stuff lying around that you might be interested in. It is called a biomesh and is used for tailoring fancy clothes. The heavy bio mesh is used for footwear, legwear and chestwear. The light biomesh is used for backwear, handwear, headwear and armwear. Which one would you like?'; Replies=@('heavy','light') }
    heavy=@{ Text=@('{givequest EMBH}','{completequest EMBH}','Here you go.{close}') }
    light=@{ Text=@('{givequest EMBL}','{completequest EMBL}','Here you go.{close}') } } }
$fausto.Replies.over1 = @{ Text="(Hand over Emiel's Box of Foodstuff)"; Goto='done1' }; $fausto.Replies.over2 = @{ Text="(Hand over Emiel's Box of Foodstuff)"; Goto='done2' }; $fausto.Replies.Remove('over')
$fausto.Lines.done1 = @{ Text=@('{take EBOX}','{completequest EMB1}','{goto taken}') }; $fausto.Lines.done2 = @{ Text=@('{take EBOX}','{completequest EMB2}','{goto taken}') }
[IO.File]::WriteAllText((Join-Path $kd 'EmielDijkman.json'), ($emiel | ConvertTo-Json -Depth 8) + "`n", $u)
[IO.File]::WriteAllText((Join-Path $kd 'LogisticsFausto.json'), ($fausto | ConvertTo-Json -Depth 8) + "`n", $u)
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 12) + "`n", $u)
