# ddogs/tools/Build-NewbieQuests.ps1 - writes newbie-area quest givers (Quests.json entries + Knubot scripts).
# Sources: AO-Universe "Nostalgic Guide - ICC Shuttleport Island Quests", AO-Universe/AOWiki Nascence start-up quests; item ids from the client RDB (auno/aogalaxy names).
# Re-runnable: replaces quests whose QasInstance is in 920000-929999 and rewrites the scripts it owns.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$script:qas = 920000; $script:bit = 610; $script:quests = @()
function Item($id, $name) { [ordered]@{ Id = $id; HighId = $id; Name = $name; Quality = 1 } }
function AddQuest($hash, $name, $action, $npc, $count, $next, $bit, $xp, $cr, $items, $spec) {
  $script:qas++
  if ($name.Length -gt 31) { throw "quest name too long: $name" }
  if ($script:quests | Where-Object { $_.Hash -eq $hash }) { throw "duplicate generated hash $hash" }
  $rec = $null; if ($items) { $rec = @(, @($items)) }
  $script:quests += [ordered]@{ Hash = $hash; Name = $name; QasInstance = $script:qas; Scope = 'Solo'; Action = $action; Summary = $name
    Objective = [ordered]@{ Count = $count; Npc = $npc; SpecItem = $spec; WorldItem = $null }
    Flags = $(if ($cr -eq 0) { 'NoCash' } else { 'None' }); MissionBit = $bit; NextStep = $next
    Reward = [ordered]@{ Hash = ('R' + $hash.Substring(1)); Credits = $cr; Xp = $xp; Records = $rec } }
}
# Job: steps = @( @(action, npcHash, count, name), ... ), then a return step to the giver that carries the reward.
# $opt: Ret = 'TargetNpc' (default; completes when the player targets a hash-spawned giver, or by {completequest} in
# the giver's script) or 'FindItem' (journal-only objective naming SpecItem; completed by a Knubot trade turn-in).
# NoBit = no mission bit (repeatable side job). Bits are handed out in file order, so append new jobs at the end.
function Job($giver, $code, $steps, $returnName, $xp, $cr, $items, $opt) {
  if (-not $opt) { $opt = @{} }
  $hashes = @(); for ($i = 0; $i -lt $steps.Count; $i++) { $hashes += ('{0}{1}' -f $code, $i) }
  $ret = $code + 'R'; $b = $null; if (-not $opt.NoBit) { $b = $script:bit++; if ($b -gt 767) { throw 'out of mission bits' } }
  for ($i = 0; $i -lt $steps.Count; $i++) {
    $s = $steps[$i]; $next = $(if ($i + 1 -lt $steps.Count) { $hashes[$i + 1] } else { $ret })
    AddQuest $hashes[$i] $s[3] $s[0] $s[1] $s[2] $next $null 0 0 $null $null
  }
  $ra = $(if ($opt.Ret) { $opt.Ret } else { 'TargetNpc' })
  AddQuest $ret $returnName $ra $giver $null $null $b $xp $cr $items $opt.Spec
  $first = $(if ($hashes.Count) { $hashes[0] } else { $ret })
  @{ First = $first; Steps = @($hashes + $ret); Return = $ret; Bit = $b }
}
# Handoff: a journal pointer "Talk to X" (no bit, small reward), completed by X's script on arrival.
function Handoff($hash, $name, $npc) { AddQuest $hash $name 'TargetNpc' $npc $null $null $null 50 0 $null $null; $hash }
function Script($file, $npcs, $notes, $intro, $jobs, $done, $extra) {
  # jobs: @{ Key; Job; Ask; Offer; Accept; Busy; Prereq; Mode ('target'|'talk'|'trade'|'elsewhere'); Items; Thanks; After }
  $openers = @(); $replies = [ordered]@{}; $lines = [ordered]@{}; $trades = [ordered]@{}
  if ($extra -and $extra.Openers) { $openers += $extra.Openers }
  foreach ($j in $jobs) { if ($j.Mode -eq 'talk') { $openers += [ordered]@{ If = @(('HasQuest ' + $j.Job.Return)); Say = ('done_' + $j.Key) } } }
  foreach ($j in $jobs) { $openers += [ordered]@{ If = @(('HasAnyQuest ' + ($j.Job.Steps -join ','))); Say = ('busy_' + $j.Key) } }
  $openers += [ordered]@{ Say = 'hello' }
  $offerReplies = @()
  foreach ($j in $jobs) {
    $cond = @(('!HasAnyQuest ' + ($j.Job.Steps -join ',')))
    if ($null -ne $j.Job.Bit) { $cond = @(('!HasCompletedQuest ' + $j.Job.Return)) + $cond }
    if ($j.Prereq) { $cond += 'HasCompletedQuest ' + $j.Prereq }
    $replies['ask_' + $j.Key] = [ordered]@{ Text = $j.Ask; If = $cond; Goto = 'offer_' + $j.Key }
    $replies['take_' + $j.Key] = [ordered]@{ Text = $j.Accept; If = $cond; Goto = 'give_' + $j.Key }
    $lines['offer_' + $j.Key] = [ordered]@{ Text = $j.Offer; Replies = @(('take_' + $j.Key), 'later') }
    $lines['give_' + $j.Key] = [ordered]@{ Text = @(('{givequest ' + $j.Job.First + '}'), '{delay 1}Good luck. Come back to me when it is done.') }
    $busyReplies = @('bye')
    $thanks = $(if ($j.Thanks) { @($j.Thanks) } else { @('Excellent work. Here is what I promised.') })
    $doneLine = [ordered]@{ Text = @(('{completequest ' + $j.Job.Return + '}')) + $thanks + @($j.After | Where-Object { $_ }); Replies = @('more', 'nothing') }
    if ($j.Mode -eq 'talk' -or $j.Mode -eq 'trade') { $lines['done_' + $j.Key] = $doneLine }
    if ($j.Mode -eq 'trade') {
      $trades['turnin_' + $j.Key] = [ordered]@{ If = @(('HasQuest ' + $j.Job.Return)); Items = @($j.Items); Goto = 'done_' + $j.Key }
      $replies['handin_' + $j.Key] = [ordered]@{ Text = 'I have it right here.'; If = @(('HasQuest ' + $j.Job.Return)); Goto = 'handin_' + $j.Key }
      $lines['handin_' + $j.Key] = [ordered]@{ Text = 'Let''s see it, then. Put it in the trade window.{opentrade}' }
      $busyReplies = @(('handin_' + $j.Key), 'bye')
    }
    $lines['busy_' + $j.Key] = [ordered]@{ Text = $j.Busy; Replies = $busyReplies }
    $offerReplies += 'ask_' + $j.Key
  }
  $replies['later'] = [ordered]@{ Text = 'Maybe later.'; Goto = 'later' }
  $replies['bye'] = [ordered]@{ Text = 'I''m on it.'; Goto = 'bye' }
  $replies['more'] = [ordered]@{ Text = 'Anything else?'; Goto = 'hello' }
  $lines['later'] = [ordered]@{ Text = 'Suit yourself. I''ll be here.{close}' }
  $lines['bye'] = [ordered]@{ Text = 'Good.{close}' }
  $helloReplies = @($offerReplies)
  if ($extra) {
    if ($extra.Replies) { foreach ($k in $extra.Replies.Keys) { $replies[$k] = $extra.Replies[$k] } }
    if ($extra.Lines) { foreach ($k in $extra.Lines.Keys) { $lines[$k] = $extra.Lines[$k] } }
    if ($extra.Trades) { foreach ($k in $extra.Trades.Keys) { $trades[$k] = $extra.Trades[$k] } }
    if ($extra.Hello) { $helloReplies += $extra.Hello }
  }
  $replies['nothing'] = [ordered]@{ Text = 'Nothing right now, thanks.'; Goto = 'nothing' }
  $lines['nothing'] = [ordered]@{ Text = $done + '{close}' }
  $lines['hello'] = [ordered]@{ Text = $intro; Replies = @($helloReplies + 'nothing') }
  $doc = [ordered]@{ Notes = $notes + ' Generated by ddogs/tools/Build-NewbieQuests.ps1.'; Npcs = $npcs; Openers = $openers; Replies = $replies; Lines = $lines }
  if ($trades.Count) { $doc['Trades'] = $trades }
  [IO.File]::WriteAllText((Join-Path $GameData "Knubot\$file"), ($doc | ConvertTo-Json -Depth 10), $u)
}
function J($key, $job, $ask, $offer, $accept, $busy, $prereq, $more) {
  $r = @{ Key = $key; Job = $job; Ask = $ask; Offer = $offer; Accept = $accept; Busy = $busy; Prereq = $prereq; Mode = 'target' }
  if ($more) { foreach ($k in $more.Keys) { $r[$k] = $more[$k] } }
  $r
}
function TI($hash, $count) { [ordered]@{ Hash = $hash; Count = $(if ($count) { $count } else { 1 }) } }
# ---------------- ICC Shuttleport (4582) ----------------
$bioCull = Job 'ICBI' 'DBC' @(@('KillMultiple','RFSL',3,'Kill 3 Reef Salamanders'),@('KillMultiple','TPSA',3,'Kill 3 Tropical Stalkers'),@('KillMultiple','CIMA',3,'Kill 3 Cliff Malles')) 'Report the cull' 350 350 @(Item 265797 'Shiny Band')
$bioTag = Job 'ICBI' 'DBT' @(@('TargetNpc','BLMM',$null,'Tag the Salamander leader'),@('TargetNpc','BEML',$null,'Tag the Leet leader'),@('TargetNpc','BDML',$null,'Tag the Malle leader'),@('TargetNpc','BSMG',$null,'Tag the Rollerrat leader')) 'Report the tagging' 350 350 @(Item 248374 'Range Meter')
Script 'BioInspector.json' @('ICBI') 'ICC Bio-Inspector, ICC Shuttleport beach (835x755).' @('{delay 1}Careful where you step, newcomer. The wildlife on this island has been breeding out of control since the evacuation.', '{delay 1}I could use a hand keeping the numbers down.') @(
  (J 'cull' $bioCull 'I''ll help. Tell me about the cull.' @('The Reef Salamanders along the shore to the west are the worst. Then the Tropical Stalkers, and finally the Cliff Malles.', '{delay 1}Kill three of each, in that order, and I''ll make it worth your while.') 'I''ll do it.' 'Three Reef Salamanders, three Tropical Stalkers, three Cliff Malles. In that order.' $null),
  (J 'tag' $bioTag 'Do you have any other work?' @('Every species here has a leader, a bigger and nastier specimen. I need them tagged for my survey.', '{delay 1}Get close and target each of them: the Salamander leader, the Leet leader, the Malle leader and the Rollerrat leader.') 'Consider them tagged.' 'Target each leader up close: Salamander, Leet, Malle, then Rollerrat.' $bioCull.Return)
) 'Stay safe out there. When you''re done here, Adri Afeli by the cliffs could use some help too.' $null

$adriParts = Job 'ADAF' 'DAP' @() 'Bring Adri the three parts' 400 400 $null @{ Ret = 'FindItem'; Spec = 'QPWS' }
$adriDroids = Job 'ADAF' 'DAD' @(@('KillMultiple','CADR',3,'Kill 3 Cargo Droids'),@('KillMultiple','WTCO',3,'Kill 3 Waste Collectors'),@('KillOne','BDMO',$null,'Disable the Android Leader'),@('KillOne','BJMR',$null,'Disable the Junk Bot Leader')) 'Report to Adri Afeli' 450 450 @(Item 99228 'Small Backpack')
Script 'AdriAfeli.json' @('ADAF') 'Adri Afeli, weapon tinkerer (900x840). Parts drop from Cargo Droids, Tropical Stalkers and Rollerrats (35%) and are handed in by trade; the retail weapon upgrade itself is not built.' @('{delay 1}A fresh face! You look like someone who appreciates a good weapon.', '{delay 1}I tinker. Starter guns, mostly. With the right parts I can make yours sing.') @(
  (J 'parts' $adriParts 'What parts do you need?' @('A Power Supply from a Cargo Droid, a Stalker Limb from a Tropical Stalker and Rollerrat Intestines from a Stowaway Rollerrat.', '{delay 1}They don''t all carry what I need, so you may have to take down a few. Bring me all three together.') 'I''ll get them.' 'A Power Supply (Cargo Droids), a Stalker Limb (Tropical Stalkers) and Rollerrat Intestines (Rollerrats). All three at once.' $null @{ Mode = 'trade'; Items = @((TI 'QPWS'), (TI 'QSTL'), (TI 'QRRI')); Thanks = @('Perfect, these will do nicely.') }),
  (J 'droids' $adriDroids 'Anything else?' @('The machines around the cargo area have gone haywire. Three Cargo Droids, three Waste Collectors, and their two leaders: the Android Leader and the Junk Bot Leader.', '{delay 1}Shut them down and I have a backpack with your name on it.') 'Consider them scrap.' 'Three Cargo Droids, three Waste Collectors, then the Android and Junk Bot leaders.' $adriParts.Return)
) 'That''s all I have. Go see Manager Travis Molen up on the platform, he always needs people.' $null

$tB = Job 'SHMG' 'DTB' @(,@('KillMultiple','CRJU',3,'Kill 3 Cross-Wired Junkbots')) 'Return to Travis (boots)' 300 100 @(Item 248269 "Newcomer's Boots")
$tG = Job 'SHMG' 'DTG' @(,@('KillMultiple','SORL',3,'Kill 3 Stowaway Rollerrats')) 'Return to Travis (gloves)' 300 100 @(Item 248277 "Newcomer's Gloves")
$tS = Job 'SHMG' 'DTS' @(,@('KillOne','LPAK',$null,'Kill a Shuttle Saboteur')) 'Return to Travis (sleeves)' 300 100 @(Item 248275 "Newcomer's Sleeves")
$tA = Job 'SHMG' 'DTA' @(,@('KillMultiple','CBSN',3,'Kill 3 Climbing Salamanders')) 'Return to Travis (body)' 300 100 @(Item 248273 "Newcomer's Body Armor")
$tP = Job 'SHMG' 'DTP' @(,@('KillMultiple','GISK',3,'Kill 3 Giant Snakes')) 'Return to Travis (pants)' 300 100 @(Item 248271 "Newcomer's Pants")
$tC = Job 'SHMG' 'DTC' @(,@('TargetNpc','CLFI',$null,'Get the medical status report')) 'Bring Travis the report' 400 400 $null
$tO = Job 'SHMG' 'DTO' @(,@('TargetNpc','OMUI',$null,'Get the combat status report')) 'Bring Travis the report' 400 400 $null
Script 'TravisMolen.json' @('SHMG') 'Manager Travis Molen, ICC Shuttleport platform (930x890).' @('{delay 1}So you made it up here. Good. I''m Travis Molen, I run what''s left of this shuttleport.', '{delay 1}I have spare Newcomer''s Armor for anyone who earns it, and I''m short on status reports.') @(
  (J 'boots' $tB 'I''d like the boots.' 'Cross-Wired Junkbots by the bridge. Kill three.' 'On my way.' 'Three Cross-Wired Junkbots.' $null),
  (J 'gloves' $tG 'I''d like the gloves.' 'Stowaway Rollerrats past the bridge. Kill three, and watch out, they bite.' 'On my way.' 'Three Stowaway Rollerrats.' $null),
  (J 'sleeves' $tS 'I''d like the sleeves.' 'There are Shuttle Saboteurs hiding near the bridge. Take one out.' 'On my way.' 'One Shuttle Saboteur.' $null),
  (J 'body' $tA 'I''d like the body armor.' 'Climbing Salamanders around the alien area. Kill three.' 'On my way.' 'Three Climbing Salamanders.' $null),
  (J 'pants' $tP 'I''d like the pants.' 'Giant Snakes, same area as the salamanders. Three of them.' 'On my way.' 'Three Giant Snakes.' $null),
  (J 'medics' $tC 'Can I help the medics?' 'Clan Field Surgeon Elsa Oosta is patching up the wounded down the ramp. Get me her status report.' 'I''ll go see her.' 'Talk to Elsa Oosta down the ramp.' $null),
  (J 'unicorns' $tO 'Can I help the Unicorns?' 'Omni Unicorn Squadleader Fixx is holding the line below. Get me his combat status report.' 'I''ll go see him.' 'Talk to Squadleader Fixx below the platform.' $null)
) 'When you''re ready to leave the island, the shuttle guards can point you to the transport.' $null

$elsa = Job 'CLFI' 'DES' @(@('TargetNpc','ZIXI',$null,'Scan an Alien Spider - Zix'),@('TargetNpc','RFSL',$null,'Scan a Reef Salamander')) 'Return the scanner to Elsa' 500 200 @(Item 248359 'Joint Clans Shoulderpad')
Script 'ElsaOosta.json' @('CLFI') 'Clan Field Surgeon Elsa Oosta (910x830). Retail: Clan reward; open to everyone here.' @('{delay 1}Mind the stretchers. I''m Elsa Oosta, field surgeon.', '{delay 1}I need lifeform scans to work out what we''re treating. You could help.') @(
  (J 'scan' $elsa 'What do you need scanned?' @('Target an Alien Spider - Zix down below the platform, then a Reef Salamander back on the coast.', '{delay 1}Just get close and target them, the scanner does the rest.') 'I''ll scan them.' 'An Alien Spider - Zix, then a Reef Salamander. Just target them.' $null)
) 'Tell Travis we''re holding up, but barely.' $null

$fixx = Job 'OMUI' 'DFX' @(,@('KillMultiple','ZIXI',3,'Kill 3 Alien Spiders - Zix')) 'Report to Squadleader Fixx' 500 200 @(Item 248360 'Omni-Tek Shoulderpad')
Script 'SquadleaderFixx.json' @('OMUI') 'Omni Unicorn Squadleader Fixx (810x830). Retail: Omni reward; open to everyone here.' @('{delay 1}Unicorn Squadleader Fixx. We''re holding these aliens back, but I''m short on hands.') @(
  (J 'aliens' $fixx 'How can I help?' 'Exterminate three Alien Spiders - Zix around 900 by 900. Each one counts.' 'Consider it done.' 'Three Alien Spiders - Zix.' $null)
) 'Good work, soldier. Report back to Travis when you can.' $null

Script 'AntonioStacklund.json' @('EQVE') 'Vendor Antonio Stacklund (933x873). Brandon Thorn''s belt quest MWTD completes when you target him.' @('{delay 1}Welcome, welcome! Antonio Stacklund, finest equipment on the island. Also the only equipment on the island.', '{delay 1}Weapons, Newcomer''s Armor, and my own Adaptation Factory for upgrading starter weapons.') @() 'Come back any time, my friend.' @{ Hello = @('factory'); Replies = [ordered]@{ factory = [ordered]@{ Text = 'Tell me about the Adaptation Factory.'; Goto = 'factory' } }; Lines = [ordered]@{ factory = [ordered]@{ Text = 'Combine the factory with your starter weapon and it comes out better, in some cases good enough to dual-wield. It takes a little tradeskill know-how.'; Replies = @('nothing') } } }

Script 'NeutralObserver.json' @('NERE') 'Neutral Observer, ICC Shuttleport.' @('{delay 1}I''m here to observe for the neutral settlements. Not everyone on Rubi-Ka picks a side, you know.', '{delay 1}Omni-Tek, the Clans, or neither: the recruiters around here will be happy to tell you about their side. I''m here to tell you there''s a third way.') @() 'Choose wisely. And remember, neutral doesn''t mean helpless.' $null

# ---------------- Nascense Frontier (4310) ----------------
$jos = Job 'JOFA' 'DJF' @(@('KillMultiple','SWSI',10,'Kill 10 Swift Silvertails'),@('KillMultiple','LLBE',10,'Kill 10 Barking Chimeras')) 'Return to Joshua Falker' 1000 500 @(Item 218315 'Coil of Health')
Script 'JoshuaFalker.json' @('JOFA') 'Joshua Falker, Frontier Bridge (847x1100).' @('{delay 1}Fresh out of Jobe Research? Welcome to Nascence.', '{delay 1}The bridge is crawling with wildlife. I could use help thinning them out.') @(
  (J 'cull' $jos 'What needs doing?' @('Kill ten Swift Silvertails first, then ten Barking Chimeras. The whole bridge area is full of them.') 'I''ll take care of it.' 'Ten Swift Silvertails, then ten Barking Chimeras.' $null)
) 'Head north to Old Frontier when you''re ready. Dr. Rosenblatt and his crew could use you.' $null

$ros = Job 'DRRS' 'DRL' @(,@('KillMultiple','HIAT',5,'Kill 5 Hiathlins')) 'Bring a Hiathlin Prime thigh' 2000 1000 @(Item 252157 'Light Bar') @{ Ret = 'FindItem'; Spec = 'QHPT' }
$rosE = Job 'DRRS' 'DRE' @(,@('KillOne','CCSP',$null,'Collect Essence of the Haunted')) 'Bring the essence to Rosenblatt' 1000 1000 $null
$rosP = Job 'DRRS' 'DRP' @(,@('KillOne','PAPA',$null,'Remove Papagena')) 'Report to Dr. Rosenblatt' 1000 1000 $null
$rosD = Job 'DRRS' 'DRD' @(,@('KillOne','TEDM',$null,'Destroy the Demonic Subjugator')) 'Report to Dr. Rosenblatt' 1500 1500 $null
Script 'DrRosenblatt.json' @('DRRS') 'Dr. Rosenblatt, Old Frontier (880x1570). Retail Light Bar quest was locked at level 90; open to all here. Datadisc quests offered directly.' @('{delay 1}Ah, a new arrival. Dr. Rosenblatt. We study the creatures of Nascence here.', '{delay 1}I have a few jobs, if you''re capable.') @(
  (J 'hiathlin' $ros 'Do you have a job for me?' @('Kill five Hiathlins, then hunt down a Hiathlin Prime. The Primes roam just before the zone to Milky Way.', '{delay 1}Bring me the Thigh of a Hiathlin Prime as proof, and I''ll give you something that lights up the darkest cave.') 'I''ll hunt them.' 'Five Hiathlins, then bring me the Thigh of a Hiathlin Prime.' $null @{ Mode = 'trade'; Items = @(,(TI 'QHPT')); Thanks = @('A fine specimen! Here, take this Light Bar.') }),
  (J 'essence' $rosE 'I found a datadisc about the haunted.' 'Cascading Spirits drift in the valley between the temples. Kill one and bring me its essence.' 'I''ll help you!' 'Kill a Cascading Spirit.' $null),
  (J 'papagena' $rosP 'I found a datadisc about Papagena.' 'Papagena wanders where the Tempterus gather. Remove her.' 'I''ll help you!' 'Kill Papagena.' $null),
  (J 'demon' $rosD 'I found a datadisc about a demon.' 'The Demonic Subjugator. Destroy it.' 'I''ll help you!' 'Kill the Demonic Subjugator.' $null)
) 'Science waits for no one. Off you go.' $null

$sab = Job 'SBFN' 'DSB' @() 'Bring 6 Predator Blood Samples' 2000 2000 $null @{ Ret = 'FindItem'; Spec = 'QPBS' }
Script 'SabinaFlorenta.json' @('SBFN') 'Sabina Florenta, Old Frontier (885x1575). Retail: blood samples spawned on kill; samples drop from Predator Strikers (50%) and are handed in by trade.' @('{delay 1}Hello! I''m researching the Predator Strikers. Their blood is fascinating.') @(
  (J 'blood' $sab 'Can I help?' 'I need six Predator Blood Samples. Predator Strikers carry them; you''ll find them a bit toward Nascence Frontier and to the left.' 'Blood donation, coming up.' 'Six Predator Blood Samples, from Predator Strikers.' $null @{ Mode = 'trade'; Items = @(,(TI 'QPBS' 6)); Thanks = @('Wonderful, six perfect samples!') })
) 'Thank you so much for your help!' $null

$lib = Job 'LIHY' 'DLH' @(,@('KillMultiple','WROM',7,'Kill 7 Weavers of Malice')) 'Return to Libbie Hyman' 2000 1500 $null
Script 'LibbieHyman.json' @('LIHY') 'Libbie Hyman, Old Frontier (880x1575).' @('{delay 1}The Weavers of Malice are spreading. Something has to be done.') @(
  (J 'weavers' $lib 'I can do something.' 'Kill seven Weavers of Malice. You''ll find them the same way as Sabina''s Predator Strikers.' 'Seven it is.' 'Seven Weavers of Malice.' $null)
) 'The valley is a little safer thanks to you.' $null

$emD = Job 'ESOJ' 'DED' @(,@('TargetNpc','SNDA',$null,'Talk to Scientist Donna Red')) 'Return to the Emissary' 500 500 $null
$emV = Job 'ESOJ' 'DEV' @(,@('TargetNpc','SCVR',$null,'Talk to Veronica Escobar')) 'Return to the Emissary' 500 500 $null
Script 'EmissaryOfJobe.json' @('ESOJ') 'Emissary of Jobe, Jobe bridge (854x1059). Starts the garden key introductions; the key quests are built further down (Nascence garden keys).' @('{delay 1}Welcome to the Shadowlands. The gardens of the Shadowlands hold great power, if you can earn a key.', '{delay 1}Two scientists study the gardens. They can tell you more.') @(
  (J 'aban' $emD 'Tell me about the Aban garden.' 'Scientist Donna Red studies the Aban garden. She is south-east of here, near 985 by 1785. She favors Clan and neutral travelers.' 'I''ll find her.' 'Find Scientist Donna Red near 985 by 1785.' $null),
  (J 'thrak' $emV 'Tell me about the Thrak garden.' 'Scientist Veronica Escobar studies the Thrak garden. She is south-west, near 745 by 1876. She favors Omni and neutral travelers.' 'I''ll find her.' 'Find Scientist Veronica Escobar near 745 by 1876.' $null)
) 'May the light of Jobe guide you.' $null

Script 'GuardianOfTheWeak.json' @('GROW') 'Guardian of the Weak, Jobe bridge.' @('{delay 1}I watch over those who are new to these lands.', '{delay 1}The creatures beyond the bridge are dangerous. Stay near the path until you have found your strength, and speak to Joshua Falker for work.') @() 'Be careful out there.' $null

# ---------------- Arete Landing (6553) ----------------
# Identity Crisis chain (retail titles from Content/Arete quest packs + Content/Runtime/interactions.json), NPCs spawned by
# Build-AreteContent.ps1. Content NPCs carry no spawn hash, so returns and "Talk to X" steps complete in dialogue.
# Retail item steps (lock pick + strongbox, hacker tool, implant assembly, pet cage capture) are simplified to NPCs that
# hand the real quest item over; turn-ins are real trades. Bits 632-639 are the eight chain milestones.
function Arrive($quest, $text) { @{ Openers = @([ordered]@{ If = @(('HasQuest ' + $quest)); Say = 'arrive' }); Lines = [ordered]@{ arrive = [ordered]@{ Text = @(('{completequest ' + $quest + '}')) + $text; Replies = @('more', 'nothing') } } } }
function Giveaway($quest, $item, $text, $idle) {
  @{ Openers = @([ordered]@{ If = @(('HasQuest ' + $quest), ('!has ' + $item)); Say = 'handover' })
     Lines = [ordered]@{ handover = [ordered]@{ Text = @($text) + @(('{spawn ' + $item + ' 1}')); Fail = 'full'; Replies = @('nothing') }
       full = [ordered]@{ Text = 'Your hands are full. Make some room in your inventory first.{close}' } } }
}
function Merge($a, $b) { $r = @{ Openers = @(); Lines = [ordered]@{}; Replies = [ordered]@{}; Trades = [ordered]@{}; Hello = @() }
  foreach ($x in @($a, $b)) { if (-not $x) { continue }; if ($x.Openers) { $r.Openers += $x.Openers }; if ($x.Hello) { $r.Hello += $x.Hello }
    foreach ($k in 'Lines', 'Replies', 'Trades') { if ($x[$k]) { foreach ($kk in $x[$k].Keys) { $r[$k][$kk] = $x[$k][$kk] } } } }; $r }

$hFlint = Handoff 'HFLN' 'Talk to Flint Novak' 'FLNV'
$hStan = Handoff 'HSTG' 'Talk to Stan Goodman' 'SAGO'
$hSarah = Handoff 'HSAG' 'Talk to Sarah Greene' 'SAGE'
$hVern = Handoff 'HVGO' 'Speak to Vernon Godfray' 'AVGO'
$hMason = Handoff 'HDMA' 'Talk to Doctor Mason' 'ADMA'
$hLor = Handoff 'HLOR' 'Talk to Lorelei' 'LRTH'

$rex = Job 'RELS' 'ARX' @(,@('KillMultiple','MLCE',5,'Kill 5 Malfunctioning Robots')) 'Return to Rex Larsson' 300 150 $null
$flint = Job 'FLNV' 'AFL' @() 'Find a Bio Analyzing Computer' 400 200 $null @{ Ret = 'FindItem'; Spec = 'QBAC' }
$stan = Job 'SAGO' 'AST' @() 'Get the Adaptation Factory' 400 250 $null @{ Ret = 'FindItem'; Spec = 'QAAF' }
$sarah = Job 'SAGE' 'ASG' @() 'Recover the DNA-Locked Armor' 500 300 $null @{ Ret = 'FindItem'; Spec = 'QDLA' }
$vern = Job 'AVGO' 'AVG' @() 'Get the Identification Chip' 500 300 $null @{ Ret = 'FindItem'; Spec = 'QUIC' }
$mason = Job 'ADMA' 'AMA' @(,@('KillMultiple','WSSC',5,'Kill 5 Rollerrats')) 'Return to Dr. Mason' 600 300 @(Item 295706 'Leg Implant: Agility, Shiny')
$lor = Job 'LRTH' 'ALO' @() 'Find Lorelei''s Lost Pet' 600 300 $null @{ Ret = 'FindItem'; Spec = 'QPCR' }
$vaughnBit = $script:bit++; if ($vaughnBit -gt 639) { throw 'out of mission bits' }
AddQuest 'AVHR' 'Talk to Vaughn Hammond' 'TargetNpc' 'AVHA' $null $null $vaughnBit 1000 1000 $null $null
# repeatable side jobs (no mission bit)
$marcus = Job 'AMST' 'AMS' @(,@('KillMultiple','JNME',6,'Kill 6 Garbage Fleas')) 'Return to Marcus Stone' 150 50 $null @{ NoBit = $true }
$desmond = Job 'ADCA' 'ADC' @(,@('KillMultiple','32DO',4,'Kill 4 32-V Dockers')) 'Return to Desmond Calitri' 200 80 $null @{ NoBit = $true }
$alexW = Job 'ALGI' 'AAG' @(,@('KillMultiple','01V1',5,'Kill 5 Waste Collectors')) 'Return to Alex Gibbs' 250 100 $null @{ NoBit = $true }
$patR = Job 'APSU' 'APS' @(,@('KillMultiple','DERT',6,'Kill 6 Desert Reets')) 'Return to Patrick Sun' 300 120 $null @{ NoBit = $true }
$patG = Job 'APSU' 'APG' @(,@('KillOne','GRDS',$null,'Kill the Greedy Desert Reet')) 'Return to Patrick Sun' 250 150 $null @{ NoBit = $true }

Script 'RexLarsson.json' @('SimpleChar:78E0FC63') 'Rex Larsson, cleaning business on the upper deck (3624x788). Identity Crisis start.' @('{delay 1}You look new. Everyone who washes up here wants the same thing: an ID, so they can get off Arete and onto Rubi-Ka proper.', '{delay 1}I can point you to the people who make that happen. But first, a favor. My cleaning robots have gone haywire.') @(
  (J 'robots' $rex 'What''s wrong with the robots?' @('Five of my Malfunctioning Cleaning Robots are rolling around out here, scrubbing anything that moves. Terminate them.', '{delay 1}Come back when they''re scrap and I''ll introduce you to someone useful.') 'I''ll take care of them.' 'Five Malfunctioning Cleaning Robots, right around here.' $null @{ Mode = 'talk'; Thanks = @('Ha! Much quieter. Thanks.'); After = @('{givequest HFLN}', '{delay 1}Now go see Flint Novak, down by the junkyard. He knows people who can build you that ID.') })
) 'Keep your head down out there.' $null

Script 'FlintNovak.json' @('SimpleChar:78E0FC64') 'Flint Novak, junkyard (3598x863).' @('{delay 1}Flint Novak. I find things, and I fix things.', '{delay 1}An ID card needs parts, and parts need work.') @(
  (J 'computer' $flint 'Rex said you could help with an ID.' @('Alex Gibbs needs a Bio Analyzing Computer to start on your ID card. The Cleaning Robots in the junkyard pick up all sorts of junk, and sometimes one of them has swallowed one.', '{delay 1}Bust a few open until you find a Bio Analyzing Computer, then take it to Alex Gibbs by the shuttle pad.') 'I''ll find one.' 'Destroy junkyard Cleaning Robots until one drops a Bio Analyzing Computer, then give it to Alex Gibbs.' $rex.Return @{ Mode = 'elsewhere' })
) 'Alex Gibbs is by the shuttle pad, if you''re looking for him.' (Arrive $hFlint @('Rex sent you? Then you''ve earned a hearing.'))

$alexExtra = Merge (Arrive 'HFLN' @()) $null
$alexExtra = @{ Openers = @([ordered]@{ If = @(('HasQuest ' + $flint.Return)); Say = 'bac' })
  Lines = [ordered]@{ bac = [ordered]@{ Text = 'Flint sent you? If you found a Bio Analyzing Computer, hand it over.{opentrade}' }
    bac_done = [ordered]@{ Text = @(('{completequest ' + $flint.Return + '}'), 'This is exactly what I needed. Your ID card is under way.', ('{givequest ' + $hStan + '}'), '{delay 1}You''ll need more parts. Stan Goodman in the camp is a purveyor of recently used merchandise. Go talk to him.'); Replies = @('more', 'nothing') } }
  Trades = [ordered]@{ bac = [ordered]@{ If = @(('HasQuest ' + $flint.Return)); Items = @(,(TI 'QBAC')); Goto = 'bac_done' } } }
Script 'AlexGibbs.json' @('SimpleChar:78E0FC61') 'Alex Gibbs, shuttle pad (3521x857). Takes Flint''s Bio Analyzing Computer.' @('{delay 1}Alex Gibbs. I put together identities for people who need them.', '{delay 1}And I always need the Waste Collectors kept off my pad.') @(
  (J 'waste' $alexW 'Can I help with the Waste Collectors?' 'Five of them. They''re all over the landing.' 'Sure.' 'Five Waste Collectors.' $null @{ Mode = 'talk'; Thanks = @('Thanks. That keeps the pad clean for a while.') })
) 'Come back if you need anything.' $alexExtra

$antonio = Giveaway $stan.Return 'QAAF' @('Stan sent you for my Adaptation Factory? That crook.', '{delay 1}Fine, fine. I owe him. Take it, and tell him we''re even.')
Script 'StanGoodman.json' @('SimpleChar:78E0FC65') 'Stan Goodman, Arete Landing camp (3463x880). Retail: lock pick + Merchant''s Strongbox; here Antonio Stacklund hands the factory over.' @('{delay 1}Stan Goodman, purveyor of recently used merchandise.', '{delay 1}New in town? Talk to Rex Larsson on the upper deck if you want an ID. Everybody wants an ID.') @(
  (J 'factory' $stan 'Alex said you could get me ID parts.' @('Parts cost. But I''ll trade you. Antonio Stacklund over at the camp has an Adaptation Factory he owes me.', '{delay 1}Get it from him and bring it back. Then we''ll talk.') 'Deal.' 'Get Antonio''s Adaptation Factory from Antonio Stacklund and bring it to me.' $flint.Return @{ Mode = 'trade'; Items = @(,(TI 'QAAF')); Thanks = @('Pleasure doing business.'); After = @(('{givequest ' + $hSarah + '}'), '{delay 1}For the next part, see Sarah Greene. She''s an armorsmith, and she owes ME a favor.') })
) 'Stay sharp out there.' (Arrive $hStan @('Alex sent you, did he?'))
Script 'AntonioStacklundArete.json' @('SimpleChar:78E0FC7C') 'Antonio Stacklund, Arete camp. Gives Stan''s Adaptation Factory during the Identity Crisis chain.' @('{delay 1}Antonio Stacklund. If it''s for sale, I''ve got it. Mostly.') @() 'Come back any time.' $antonio

$shady = Giveaway $sarah.Return 'QDLA' @('Whoa, whoa! I didn''t steal anything. I found it. Lying around.', '{delay 1}Take the stupid armor, it doesn''t even fit me. DNA-locked, who knew?')
Script 'SarahGreene.json' @('SimpleChar:78E0FC69') 'Sarah Greene, armorsmith, Arete camp (3471x841). Retail: thief in the underground; here the Shady Guy (3541x748) has it.' @('{delay 1}Hi there! I''m Sarah. I make armor, good armor.', '{delay 1}Everyone here is trying to get off this rock and onto Rubi-Ka proper.') @(
  (J 'armor' $sarah 'Stan said you could help with my ID.' @('I can, but someone stole one of my custom suits: a DNA-Locked Prototype Raven Combat Armor. It''s useless to the thief, it''s locked to its owner.', '{delay 1}Word is a Shady Guy has been hanging around south of the landing. Get my armor back.') 'I''ll find the thief.' 'Find the Shady Guy south of the landing and get the DNA-Locked Armor back.' $stan.Return @{ Mode = 'trade'; Items = @(,(TI 'QDLA')); Thanks = @('My armor! Thank you.'); After = @(('{givequest ' + $hVern + '}'), '{delay 1}For your ID you''ll need a chip. Vernon Godfray is a hacker in the camp. Tell him I sent you.') })
) 'See you around!' (Arrive $hSarah @('Stan sent you? Of course he did.'))
Script 'ShadyGuy.json' @('SimpleChar:78E0FC79') 'Shady Guy, south of the landing. Hands over Sarah''s DNA-Locked Armor.' @('{delay 1}What are you looking at? Move along.') @() 'Nothing to see here.' $shady

$term = Giveaway $vern.Return 'QUIC' @('SHIPPING MANIFEST TERMINAL. Access denied.', '{delay 1}...override accepted. Manifest entry 4471: 1x Unprogrammed Identification Chip. Released to bearer.')
Script 'VernonGodfray.json' @('SimpleChar:78E0FC68') 'Vernon Godfray, hacker, Arete camp (3434x826). Retail: hacker tool + Technical Library; here the Shipping Manifest Terminal releases the chip.' @('{delay 1}Vernon Godfray. I don''t do small talk.') @(
  (J 'chip' $vern 'Sarah sent me about an ID chip.' @('An Unprogrammed Identification Chip came in on the last cargo run. It''s sitting in the Shipping Manifest Terminal down by the pad.', '{delay 1}I''ve already put a backdoor in it. Just talk to the terminal and it''ll release the chip. Bring it to me.') 'On my way.' 'Talk to the Shipping Manifest Terminal by the pad and bring me the Unprogrammed Identification Chip.' $sarah.Return @{ Mode = 'trade'; Items = @(,(TI 'QUIC')); Thanks = @('Nice. I''ll program it with your new identity.'); After = @(('{givequest ' + $hMason + '}'), '{delay 1}The chip needs to be implanted. Doctor Mason can do that.') })
) 'Don''t mention my name to the peacekeepers.' (Arrive $hVern @('Sarah sent you. Fine.'))
Script 'ShippingManifestTerminal.json' @('SimpleChar:78E0FC6A') 'Shipping Manifest Terminal by the shuttle pad. Releases the Unprogrammed Identification Chip.' @('SHIPPING MANIFEST TERMINAL. Access denied.') @() 'SESSION CLOSED.' $term

Script 'DrMason.json' @('SimpleChar:78E0FC6C') 'Dr. Mason, Arete camp (3431x795). Retail: implant assembly tradeskill quests; here a Rollerrat harvest and the leg implant as reward.' @('{delay 1}Doctor Mason. Hold still, this won''t hurt. Much.') @(
  (J 'implant' $mason 'Vernon said you could implant my ID chip.' @('I can, but I''ll need fresh tissue cultures for the bonding. Rollerrats are ideal.', '{delay 1}Kill five Rollerrats around the landing and come back. I''ll even throw in a leg implant.') 'I''ll get them.' 'Five Rollerrats.' $vern.Return @{ Mode = 'talk'; Thanks = @('Perfect cultures. There, your chip is in. And this implant is yours.'); After = @(('{givequest ' + $hLor + '}'), '{delay 1}One more thing. Lorelei at the bar up the hill knows the man who can get you out of here.') })
) 'Come back if anything itches.' (Arrive $hMason @('Vernon''s patient? Sit down.'))

$lolly = Giveaway $lor.Return 'QPCR' @('*The reet chirps, sniffs your hand, and hops straight into the pet cage.*')
$lorExtra = Merge (Arrive $hLor @('Mason sent you? Have a seat.')) @{ Hello = @('shop'); Replies = [ordered]@{ shop = [ordered]@{ Text = 'What are you serving?'; Goto = 'shop' } }; Lines = [ordered]@{ shop = [ordered]@{ Text = 'Take a look.{shop}' } } }
Script 'LoreleiTheBartender.json' @('SimpleChar:78E0FC6B') 'Lorelei the Bartender (3369x...); keeps her shop via {shop}.' @('{delay 1}Welcome to my bar. What''ll it be?') @(
  (J 'pet' $lor 'Mason said you know a way out of here.' @('I might. But first: my reet, Lolly, ran off again. She likes the oasis to the south.', '{delay 1}Bring her back to me and I''ll introduce you to Vaughn Hammond.') 'I''ll find her.' 'Find Lolly the Reet at the oasis south of the bar and bring her back.' $mason.Return @{ Mode = 'trade'; Items = @(,(TI 'QPCR')); Thanks = @('Lolly! You bad girl. Thank you, really.'); After = @('{givequest AVHR}', '{delay 1}Vaughn Hammond, just outside. He gets people off Arete. Tell him I sent you.') })
) 'Come back any time.' $lorExtra
Script 'LollyTheReet.json' @('SimpleChar:7985CAEC') 'Lolly the Reet, Lorelei''s pet, at the oasis.' @('*The reet eyes you suspiciously.*') @() '*The reet goes back to sniffing the sand.*' $lolly
Script 'VaughnHammond.json' @('SimpleChar:78E0FC73') 'Vaughn Hammond, end of the Identity Crisis chain.' @('{delay 1}Vaughn Hammond. You need an ID first, friend.') @() 'Safe travels.' (Arrive 'AVHR' @('So Lorelei vouches for you. Your ID checks out, too.', '{delay 1}Welcome to Rubi-Ka, citizen. Whenever you''re ready, the ICC shuttle will take you anywhere you want to go.'))

Script 'MarcusStone.json' @('SimpleChar:78E0FC62') 'Marcus Stone, upper deck (3630x824). Repeatable side job.' @('{delay 1}Marcus Stone. Keep your distance from the fleas.') @(
  (J 'fleas' $marcus 'Fleas?' 'Garbage Fleas, all over the landing. Kill six of them for me.' 'Sure.' 'Six Garbage Fleas.' $null @{ Mode = 'talk'; Thanks = @('Better. Thanks.') })
) 'See you.' $null
Script 'DesmondCalitri.json' @('SimpleChar:78E0FC77') 'Desmond Calitri (3605x827). Repeatable side job.' @('{delay 1}Desmond Calitri. Those docker robots have stopped listening to anyone.') @(
  (J 'dockers' $desmond 'Want me to deal with them?' 'Four 32-V Dockers. Shut them down.' 'Will do.' 'Four 32-V Dockers.' $null @{ Mode = 'talk'; Thanks = @('Nice work.') })
) 'Later.' $null
Script 'PatrickSun.json' @('SimpleChar:78E0FC7B') 'Patrick Sun, camp (3463x812). Repeatable side jobs.' @('{delay 1}Patrick Sun. The reets are getting into the supplies again.') @(
  (J 'reets' $patR 'I can help with the reets.' 'Six Desert Reets, south of the camp.' 'On it.' 'Six Desert Reets.' $null @{ Mode = 'talk'; Thanks = @('Thanks! The supplies are safe for another day.') }),
  (J 'greedy' $patG 'Is there a ringleader?' 'The Greedy Desert Reet. Big one, south of the camp near the oasis. Take it down.' 'Consider it done.' 'The Greedy Desert Reet.' $null @{ Mode = 'talk'; Thanks = @('Ha! That''ll show it.') })
) 'Watch your supplies.' $null
Script 'ImmigrationOfficerBill.json' @('SimpleChar:78E0FC66') 'ICC Immigration Officer Bill.' @('{delay 1}ICC Immigration. No ID, no passage.', '{delay 1}If you want off Arete, start with Rex Larsson on the upper deck. Unofficially, of course.') @() 'Next!' $null
# ---------------- Nascence garden keys (bits 640+, server-only block, see ddogs/docs/MISSION_BITS.md) ----------------
# Clan: Aban garden (AO-Universe "Aban Garden Key Quest"). Omni/neutral: Thrak garden (AOWiki "Thrak Garden Mission").
# NPCs: Donna Red / Veronica Escobar are Frontier hash spawns; Fala (4312), Lux-Wei (4676), Yutt Thrak (4311) and Urga-Lum (4677) are
# content NPCs from Build-GardenContent.ps1. Simplified: the insignia + device combine happens in a trade with the garden NPC,
# and "save/curse three Dreaming Silvertails" is a kill objective (the retail chat-trade with the silvertail is not built).
$script:bit = 640
$hFala = Handoff 'HGAF' 'Show the device to Aban Fala' 'GAFA'
$hLux = Handoff 'HGAL' 'Talk to Sipius Aban Lux-Wei' 'GLUX'
$hYutt = Handoff 'HGTY' 'Find Prophet Yutt Thrak' 'GYUT'
$hUrga = Handoff 'HGTU' 'Talk to Urga-Lum Thrak' 'GURG'
$fala = Job 'GAFA' 'GAF' @() 'Show an Insignia of Aban' 3000 2000 $null @{ Ret = 'FindItem'; Spec = 'GINA' }
$lux = Job 'GLUX' 'GAL' @(,@('KillMultiple','DRSL',3,'Save 3 Dreaming Silvertails')) 'Return the Analyzer to Lux-Wei' 5000 5000 @(Item 226824 'The Key to the Garden of Aban') @{ Ret = 'FindItem'; Spec = 'GAPG' }
$yutt = Job 'GYUT' 'GTY' @() 'Bring Yutt Thrak an Insignia' 3000 2000 $null @{ Ret = 'FindItem'; Spec = 'GINT' }
$urga = Job 'GURG' 'GTU' @() 'Give Urga-Lum the device' 7000 10000 $null @{ Ret = 'FindItem'; Spec = 'GADV' }
$urgaK = Job 'GURG' 'GTK' @(,@('KillMultiple','DRSL',3,'Curse 3 Dreaming Silvertails')) 'Return the Analyzer to Urga-Lum' 462 800 @(Item 226994 'The Key to the Garden of Thrak') @{ Ret = 'FindItem'; Spec = 'GAPF' }

function Starter($handoff, $done, $text) {
  @{ Hello = @('device'); Replies = [ordered]@{ device = [ordered]@{ Text = 'Tell me about the garden key.'; If = @(('!HasAnyQuest ' + $handoff), ('!HasCompletedQuest ' + $done), '!has GADV'); Goto = 'device' } }
     Lines = [ordered]@{ device = [ordered]@{ Text = @($text) + @('{spawn GADV 1}', ('{givequest ' + $handoff + '}')); Fail = 'full'; Replies = @('nothing') }
       full = [ordered]@{ Text = 'Your hands are full. Make some room in your inventory first.{close}' } } }
}
Script 'DonnaRed.json' @('SNDA') 'Scientist Donna Red (985x1785). Aban garden key (Clan) start: hands out the Ancient Device.' @('{delay 1}The Aban garden is a place of redemption. Only those with a key may enter freely.', '{delay 1}I have found something the Redeemed might want to see.') @() 'Good luck, traveler.' (Starter $hFala $lux.Return @('This Ancient Device turned up near the Frontier. The Redeemed will know what it is.', '{delay 1}Take it to Ecclesiast Aban Fala in the Redeemed village in south-east Nascence. Bring Insignias of Aban, the Barking Chimeras carry them.'))
Script 'VeronicaEscobar.json' @('SCVR') 'Scientist Veronica Escobar (745x1876). Thrak garden key (Omni/neutral) start: hands out the Ancient Device.' @('{delay 1}The Thrak garden belongs to those who embrace the darkness. A key lets you enter it freely.', '{delay 1}I have an artifact the Unredeemed would kill for.') @() 'Until next time.' (Starter $hYutt $urgaK.Return @('This Ancient Device came out of the Frontier. Someone in the settlement to the west, in Brawl, will be very interested.', '{delay 1}Find Prophet Yutt Thrak there. Carry a few Insignias of Thrak, the beasts of the Frontier drop them.'))

Script 'EcclesiastAbanFala.json' @('SimpleChar:7A1B033F') 'Ecclesiast Aban Fala, Redeemed village (PF 4312, 1893x690). Aban key step 1: show an Insignia of Aban (returned).' @('{delay 1}Be welcome, child. Aban watches over the humble.') @(
  (J 'insignia' $fala 'Donna Red sent me with this device.' @('An ancient device... Before I speak of it, bring me something that shows the Divine presence of Aban in Nascence.', '{delay 1}Show me an Insignia of Aban.') 'I will.' 'Show me an Insignia of Aban.' $null @{ Mode = 'trade'; Items = @(,(TI 'GINA')); Thanks = @('Yes, His mark. Keep it, you will need it.'); After = @('{spawn GINA 1}', ('{givequest ' + $hLux + '}'), '{delay 1}Take the device to Sipius Aban Lux-Wei in the Garden of Aban, by the teleporter statue. He knows the old Xan technology.') })
) 'Go in light.' (Arrive $hFala @('Donna Red''s device. Let me see.'))

$luxExtra = Merge (Arrive $hLux @('Fala sent you. Show me the device.')) @{ Hello = @('combine')
  Replies = [ordered]@{ combine = [ordered]@{ Text = 'How do I awaken the device?'; If = @('!has GAPG'); Goto = 'combine' } }
  Lines = [ordered]@{ combine = [ordered]@{ Text = 'Give me the Ancient Device together with an Insignia of Aban.{opentrade}' }
    graced = [ordered]@{ Text = @('{spawn GAPG 1}', 'It is done: an Ancient Pattern Analyzer, graced by the Faithful.', '{delay 1}Now save three souls. The Dreaming Silvertails north of Frontier Bridge are lost; free them, then return the Analyzer to me.'); Fail = 'full'; Replies = @('more', 'nothing') }
    full = [ordered]@{ Text = 'Your hands are full. Make some room in your inventory first.{close}' } }
  Trades = [ordered]@{ combine = [ordered]@{ If = @('!has GAPG'); Items = @((TI 'GADV'), (TI 'GINA')); Goto = 'graced' } } }
Script 'SipiusAbanLuxWei.json' @('SimpleChar:7A2013BC') 'Sipius Aban Lux-Wei, Garden of Aban (PF 4676, 468x495). Aban key: device + insignia -> graced Analyzer, 3 silvertails, Analyzer -> Key to the Garden of Aban.' @('{delay 1}The garden is at peace. May you find it too.') @(
  (J 'souls' $lux 'I am ready to save the souls.' @('Three Dreaming Silvertails wander north of Frontier Bridge. Free them, carrying the graced Analyzer.', '{delay 1}Then give the Analyzer back to me.') 'I will save them.' 'Free three Dreaming Silvertails, then give me back the graced Analyzer.' $fala.Return @{ Mode = 'trade'; Items = @(,(TI 'GAPG')); Thanks = @('Three souls saved. Aban is pleased, and so am I. This key is yours: the Garden of Aban is open to you.') })
) 'Peace be with you.' $luxExtra

Script 'ProphetYuttThrak.json' @('SimpleChar:78D280F6') 'Prophet Yutt Thrak, Brawl (PF 4311, 288x1006). Thrak key step 1: show an Insignia of Thrak (returned).' @('{delay 1}You stand before a prophet of the Unredeemed. Speak.') @(
  (J 'insignia' $yutt 'Veronica sent me with this device.' @('*The demon turns the device over in its claws and hands it back.*', '{delay 1}Interesting. Prove you walk with Thrak. Show me an Insignia of Thrak.') 'Here it is.' 'Show me an Insignia of Thrak.' $null @{ Mode = 'trade'; Items = @(,(TI 'GINT')); Thanks = @('His mark. Keep it.'); After = @('{spawn GINT 1}', ('{givequest ' + $hUrga + '}'), '{delay 1}Use an insignia on the Thrak statue and enter His Garden. Hypnagogic Urga-Lum Thrak, below the ramp, will take the device.') })
) 'Go.' (Arrive $hYutt @('The scientist''s errand runner. Come closer.'))

Script 'HypnagogicUrgaLumThrak.json' @('SimpleChar:79758F3A') 'Hypnagogic Urga-Lum Thrak, Garden of Thrak (PF 4677, 464x359). Thrak key: device + insignia -> favored Analyzer, 3 silvertails, Analyzer -> Key to the Garden of Thrak.' @('{delay 1}*The demon''s eyes flicker open.* You dream loudly, mortal.') @(
  (J 'device' $urga 'Prophet Yutt sent me with the device.' @('Give me the device, and an Insignia of Thrak to wake it.') 'Take them.' 'Give me the Ancient Device and an Insignia of Thrak.' $null @{ Mode = 'trade'; Items = @((TI 'GADV'), (TI 'GINT')); Thanks = @('*The device hums with dark light.* An Ancient Pattern Analyzer, favored by the Chosen One.'); After = @('{spawn GAPF 1}', '{delay 1}Now curse three Dreaming Silvertails near Frontier Bridge and bring it back.') }),
  (J 'souls' $urgaK 'I will claim the souls.' 'Three Dreaming Silvertails near Frontier Bridge. Claim their souls, then give me the Analyzer.' 'It will be done.' 'Curse three Dreaming Silvertails, then give me back the favored Analyzer.' $urga.Return @{ Mode = 'trade'; Items = @(,(TI 'GAPF')); Thanks = @('Three souls for Thrak. Take this key; His Garden is yours to enter.') })
) 'Dream well.' (Arrive $hUrga @('Yutt sent you. I felt it.'))
# ---------------- write Quests.json ----------------
$qpath = Join-Path $GameData 'Quests.json'
$existing = ([IO.File]::ReadAllText($qpath) | ConvertFrom-Json) | ForEach-Object { $_ } | Where-Object { $_.QasInstance -lt 920000 -or $_.QasInstance -gt 929999 }
$all = @($existing) + $script:quests
$dup = $all | ForEach-Object { $_.Hash } | Group-Object | Where-Object Count -gt 1; if ($dup) { throw ('duplicate quest hashes: ' + ($dup.Name -join ',')) }
[IO.File]::WriteAllText($qpath, (ConvertTo-Json @($all) -Depth 10), $u)
"quests total=$($all.Count) generated=$($script:quests.Count) bits=610-639,640-$($script:bit - 1)"
