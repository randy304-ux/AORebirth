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
  $ret = $code + 'R'; $b = $null
  if ($opt.Contains('Bit')) { $b = [int]$opt.Bit; if ($b -lt 640 -or $b -gt 767) { throw "bit out of ddogs range: $b" } }
  elseif (-not $opt.NoBit) { $b = $script:bit++; if ($b -gt 767) { throw 'out of mission bits' } }
  for ($i = 0; $i -lt $steps.Count; $i++) {
    $s = $steps[$i]
    if ($s -is [Array] -and $s.Count -eq 1 -and $s[0] -is [Array]) { $s = $s[0] }
    if ($s[0] -isnot [string] -or $s[3] -isnot [string]) { throw "quest step $code $i is not an action/name pair" }
    $next = $(if ($i + 1 -lt $steps.Count) { $hashes[$i + 1] } else { $ret })
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
    if ($j.MinLevel) { $cond += 'minlevel ' + $j.MinLevel }
    if ($j.Prof) { $cond += 'prof ' + $j.Prof }
    $replies['ask_' + $j.Key] = [ordered]@{ Text = $j.Ask; If = $cond; Goto = 'offer_' + $j.Key }
    $replies['take_' + $j.Key] = [ordered]@{ Text = $j.Accept; If = $cond; Goto = 'give_' + $j.Key }
    $lines['offer_' + $j.Key] = [ordered]@{ Text = $j.Offer; Replies = @(('take_' + $j.Key), 'later') }
    $give = @('{givequest ' + $j.Job.First + '}')
    if ($j.Give) { $give = @($j.Give) + $give }
    $luck = $(if ($j.Luck) { $j.Luck } else { '{delay 1}Good luck. Come back to me when it is done.' })
    $lines['give_' + $j.Key] = [ordered]@{ Text = $give + @($luck) }
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
# AO-Universe Arete Landing, sections 1-22. Journal steps use the guide's order.
# Use-item, tradeskill and shop steps that the quest actions cannot perform are a talk or a trade of the real item.
# Bits 632-639 stay the eight chain milestones. Added one-time jobs use explicit bits 656+ so the garden reset is untouched.
function Arrive($quest, $text) { @{ Openers = @([ordered]@{ If = @(('HasQuest ' + $quest)); Say = 'arrive' }); Lines = [ordered]@{ arrive = [ordered]@{ Text = @(('{completequest ' + $quest + '}')) + $text; Replies = @('more', 'nothing') } } } }
function Giveaway($quest, $item, $text, $idle) {
  @{ Openers = @([ordered]@{ If = @(('HasQuest ' + $quest), ('!has ' + $item)); Say = 'handover' })
     Lines = [ordered]@{ handover = [ordered]@{ Text = @($text) + @(('{spawn ' + $item + ' 1}')); Fail = 'full'; Replies = @('nothing') }
       full = [ordered]@{ Text = 'Your hands are full. Make some room in your inventory first.{close}' } } }
}
function Merge($a, $b) { $r = @{ Openers = @(); Lines = [ordered]@{}; Replies = [ordered]@{}; Trades = [ordered]@{}; Hello = @() }
  foreach ($x in @($a, $b)) { if (-not $x) { continue }; if ($x.Openers) { $r.Openers += $x.Openers }; if ($x.Hello) { $r.Hello += $x.Hello }
    foreach ($k in 'Lines', 'Replies', 'Trades') { if ($x[$k]) { foreach ($kk in $x[$k].Keys) { $r[$k][$kk] = $x[$k][$kk] } } } }; $r }
$hMarcus = Handoff 'HAMS' 'Talk to Marcus Stone' 'AMST'
$hFlint = Handoff 'HFLN' 'Talk to Flint Novak' 'FLNV'
$hStan = Handoff 'HSTG' 'Talk to Stan Goodman' 'SAGO'
$hMarco = Handoff 'HMPS' 'Talk to Marco Spida' 'ONRP'
$hSarah = Handoff 'HSAG' 'Talk to Sarah Greene' 'SAGE'
$hVern = Handoff 'HVGO' 'Speak to Vernon Godfray' 'AVGO'
$hMason = Handoff 'HDMA' 'Talk to Doctor Mason' 'ADMA'
$hLor = Handoff 'HLOR' 'Talk to Lorelei' 'LRTH'

$rex = Job 'RELS' 'ARX' @(@('KillMultiple','MLCE',5,'Terminate 5 Cleaning Robots'),@('TargetNpc',$null,$null,'Open the Cargo Box')) 'Return to Rex Larsson' 1281 1040 $null
$marcus = Job 'AMST' 'AMR' @() 'Extinguish the gas fire' 1281 1080 @(Item 296569 'Generic Nano Transmitter') @{ Bit = 656; Ret = 'FindItem'; Spec = 'QCFS' }
$flint = Job 'FLNV' 'AFL' @(,@('KillMultiple','JUCO',7,'Kill 7 Cleaning Robots')) 'Bring the computer to Alex' 0 0 @(Item 296570 'Blank Info Chip') @{ Ret = 'FindItem'; Spec = 'QBAC' }
$alex = Job 'ALGI' 'AAX' @(
  @('TargetNpc','SRDO',$null,'Hack the Surveillance Droid'),
  @('TargetNpc',$null,$null,'Plant the Recording Device'),
  @('TargetNpc',$null,$null,'Deliver the monitor to Bill'),
  @('KillOne','UNAF',$null,'Defeat Kneebreaker Alfonzo')
) 'Report to Alex Gibbs' 0 0 @((Item 296571 '2560-Bit Encryption Compiler'), (Item 287041 'Nano Crystal (Composite Tradeskill Expertise)')) @{ Bit = 657 }
$stan = Job 'SAGO' 'AST' @(,@('TargetNpc',$null,$null,'Lockpick the Merchant Strongbox')) 'Bring Stan the four parts' 2229 1240 @(Item 296572 'Unprogrammed Identification Chip') @{ Ret = 'FindItem' }
$marco = Job 'ONRP' 'AMP' @() 'Open your nano container' 2229 1120 @(Item 223373 'Nano Crystal (Composite Attribute Boost)') @{ Bit = 658 }
$sarah = Job 'SAGE' 'ASG' @() 'Recover the DNA-Locked Armor' 0 0 @(Item 296574 'Biological Survey Nanobots') @{ Ret = 'FindItem'; Spec = 'QDLA' }
$vern = Job 'AVGO' 'AVG' @(@('TargetNpc',$null,$null,'Hack the Technical Library'),@('TargetNpc',$null,$null,'Hack the Shipping Manifest')) 'Bring Vernon the ID chip' 0 0 @(Item 296575 'Blank ICC ID Chip') @{ Ret = 'FindItem'; Spec = 'QUIC' }
$mason = Job 'ADMA' 'AMA' @() 'Fit the agility implant' 0 0 @(Item 295706 'Leg Implant: Agility, Shiny')
$sheila = Job 'LDSH' 'ASH' @() 'Speak with Lady Sheila' 0 0 @(Item 296576 'Personalized ICC ID Chip') @{ Bit = 662 }
$lor = Job 'LRTH' 'ALO' @() 'Bring Lolly back to Lorelei' 0 0 @(Item 296692 'Identification Card') @{ Ret = 'FindItem'; Spec = 'QPCR' }
$vaughnBit = $script:bit++; if ($vaughnBit -gt 639) { throw 'out of mission bits' }
AddQuest 'AVHR' 'Hand Vaughn your ID card' 'FindItem' 'AVHA' $null $null $vaughnBit 0 0 $null 'QIDC'
$desmond = Job 'ADCA' 'ADP' @(@('TargetNpc',$null,$null,'Return the Bronto Burger'),@('KillMultiple','ROTE',2,'Kill 2 Protesters'),@('KillOne','CEHA',$null,'Kill Cedric Harding')) 'Collect Desmond''s payment' 2229 1120 @((Item 295698 'Dockworker Overalls'), (Item 295699 'The Brute Suit'), (Item 295703 'Experimental Nano Armor Exosuit')) @{ Bit = 659 }
$patrick = Job 'APSU' 'APN' @() 'Touch the Insurance Terminal' 0 0 @(Item 36777 'Belt Component Platform 300X') @{ Bit = 660 }
$shady = Job 'ASYG' 'ASY' @(,@('KillOne','MARC',$null,'Kill Mario Carles')) 'Return the Yalmaha' 1540 5000 @(Item 295705 'Kodaik Velocity IV') @{ Bit = 661; Ret = 'FindItem' }

Script 'RexLarsson.json' @('SimpleChar:78E0FC63') 'Rex Larsson, upper deck (3624x788). Guide: five cleaning robots, the cargo box, then Marcus Stone. Reward 1040 credits and up to 1281 xp.' @('{delay 1}You look new. Everyone who washes up here wants an ID, so they can get off Arete.', '{delay 1}I can point you on. First, my cleaning robots have gone haywire.') @(
  (J 'robots' $rex 'What''s wrong with the robots?' @('Terminate five Malfunctioning Cleaning Robots rolling around out here.', '{delay 1}Then open the Cargo Box beside me and come back. After that, Marcus Stone is the man with the ID papers.') 'I''ll do it.' 'Five Malfunctioning Cleaning Robots, then the Cargo Box.' $null @{ Mode = 'talk'; Thanks = @('Much quieter, and the box is open. Here.'); After = @('{givequest HAMS}', '{delay 1}Marcus Stone is on this deck, about 3630 by 824. He is next, not the junkyard.') })
) 'Keep your head down out there.' $null
Script 'CargoBox.json' @('ddogs-cargo-box') 'Cargo Box at the captured static (3621.6, 51.7, 780.5), beside Rex.' @('A cargo box. The lid is shut.') @() 'The lid stays shut.' @{
  Openers = @([ordered]@{ If = @('HasQuest ARX1'); Say = 'open' })
  Lines = [ordered]@{ open = [ordered]@{ Text = @('{completequest ARX1}', 'The lid gives. Rex will want to hear that you opened it.'); Replies = @('nothing') } }
}

Script 'MarcusStone.json' @('SimpleChar:78E0FC62') 'Marcus Stone, upper deck (3630x824). Guide: Compact Fire Suppressant on the gas fire, then return the container. 1080 credits, up to 1281 xp, Generic Nano Transmitter.' @('{delay 1}Marcus Stone. There is a gas fire on a container up here, and it will not put itself out.') @(
  (J 'fire' $marcus 'Rex sent me.' @('Take this Compact Fire Suppressant Container and put the gas fire out. Drag it onto the fire.', '{delay 1}The fire object is not wired as a quest target, so bring the container back to me once you have dealt with it. Then see Flint Novak.') 'I''ll put it out.' 'Use the Compact Fire Suppressant Container on the gas fire, then return the container.' $rex.Return @{ Mode = 'trade'; Items = @(,(TI 'QCFS')); Give = '{spawn QCFS 1}'; Thanks = @('The fire is out. This Generic Nano Transmitter is yours. Do not lose it. Stan Goodman will want it later.'); After = @('{givequest HFLN}', '{delay 1}Flint Novak is down by the junkyard, about 3598 by 863.') })
) 'The wounded dockworkers are a separate errand. I do not have them standing here yet.' $null

Script 'FlintNovak.json' @('SimpleChar:78E0FC64') 'Flint Novak, junkyard (3598x863). Guide: seven junkyard robots, Bio Analyzing Computer goes to Alex, not back to Flint.' @('{delay 1}Flint Novak. I find things, and I fix things.') @(
  (J 'computer' $flint 'Marcus sent me.' @('Alex Gibbs needs a Bio Analyzing Computer. On retail any mix of seven junkyard robots will do: Cleaning Robots, Waste Collectors or 32-V Dockers.', '{delay 1}This counter is the seven Cleaning Robots, because one objective counts one kind. Take the computer to Alex Gibbs. Do not bring it back to me.') 'I''ll find one.' 'Seven junkyard Cleaning Robots. Take the Bio Analyzing Computer to Alex Gibbs.' $marcus.Return @{ Mode = 'elsewhere'; Luck = 'Take whatever you find to Alex Gibbs, by the shuttle pad. Not back here.' })
) 'Alex Gibbs is by the shuttle pad.' (Arrive $hFlint @('Marcus sent you? Then you have earned a hearing.'))

$alexTurn = @{ Openers = @([ordered]@{ If = @('HasQuest AFLR'); Say = 'bac' })
  Lines = [ordered]@{ bac = [ordered]@{ Text = 'Flint sent you? If that is a Bio Analyzing Computer, hand it over.{opentrade}' }
    bac_done = [ordered]@{ Text = @('{completequest AFLR}', 'This is the one. Here is a Blank Info Chip. Hold onto it.', '{givequest AAX0}', '{delay 1}Next: the surveillance droid at 3563 by 823. Hack it, plant the recorder on the prized house plant in Desmond''s office, take the monitor to Immigration Officer Bill, then deal with Kneebreaker Alfonzo.'); Replies = @('more', 'nothing') } }
  Trades = [ordered]@{ bac = [ordered]@{ If = @('HasQuest AFLR'); Items = @(,(TI 'QBAC')); Goto = 'bac_done' } } }
Script 'AlexGibbs.json' @('SimpleChar:78E0FC61') 'Alex Gibbs, shuttle pad (3521x857). Guide: computer from Flint, then droid, house plant, Bill, Kneebreaker Alfonzo.' @('{delay 1}Alex Gibbs. I put identities together for people who need them.') @(
  (J 'watch' $alex 'The computer is with you. What next?' @('Hack the Surveillance Droid at 3563 by 823 with the Rebuilt HC-12 SecTec Monitor.', '{delay 1}Plant the RC-P Audio Recording Device on the prized house plant in Desmond''s office. Take the monitor to Immigration Officer Bill. Then defeat Kneebreaker Alfonzo Rizzolo, about 3580 by 833. A bruiser sometimes stands with him; he is not a separate objective here.') 'I''ll see it done.' 'Droid, house plant, Bill, then Kneebreaker Alfonzo.' $flint.Return @{ Mode = 'talk'; Give = @('{spawn QMON 1}', '{spawn QRCP 1}'); Thanks = @('Alfonzo is done. The 2560-Bit Encryption Compiler, and a tradeskill crystal. Stan Goodman is next.'); After = @('{givequest HSTG}', '{delay 1}Stan Goodman is in the camp, about 3463 by 880.') })
) 'Come back if you need anything.' $alexTurn
Script 'HousePlant.json' @('ddogs-house-plant') 'Prized House Plant in Desmond''s office, a few meters from Desmond Calitri. The guide does not pin the pot.' @('A prized house plant.') @() 'The leaves do not answer.' @{
  Openers = @([ordered]@{ If = @('HasQuest AAX1'); Say = 'plant' })
  Lines = [ordered]@{ plant = [ordered]@{ Text = 'The recorder has to be planted on the pot. Put the RC-P in the trade window.{opentrade}' }
    planted = [ordered]@{ Text = @('{completequest AAX1}', 'The RC-P is hidden in the leaves. Immigration Officer Bill is waiting for the monitor.'); Replies = @('nothing') } }
  Trades = [ordered]@{ plant = [ordered]@{ If = @('HasQuest AAX1'); Items = @(,(TI 'QRCP')); Goto = 'planted' } }
}
Script 'ImmigrationOfficerBill.json' @('SimpleChar:78E0FC66') 'ICC Immigration Officer Bill (3510x826). After he sends you after the Kneebreaker he will not discuss Arete.' @('{delay 1}ICC Immigration. No ID, no passage.') @() 'Next!' @{
  Hello = @('rex','monitor')
  Openers = @([ordered]@{ If = @('HasQuest AAX2'); Say = 'monitor' })
  Replies = [ordered]@{
    rex = [ordered]@{ Text = 'How do I get off Arete?'; If = @('!HasAnyQuest AAX3,AAXR','!HasCompletedQuest AAXR'); Goto = 'rex' }
    monitor = [ordered]@{ Text = 'Alex Gibbs sent the monitor.'; If = @('HasQuest AAX2'); Goto = 'monitor' }
  }
  Lines = [ordered]@{
    rex = [ordered]@{ Text = 'Unofficially, start with Rex Larsson on the upper deck.'; Replies = @('nothing') }
    monitor = [ordered]@{ Text = 'The Rebuilt HC-12. Put it in the trade window.{opentrade}' }
    briefed = [ordered]@{ Text = @('{completequest AAX2}', 'Kneebreaker Alfonzo Rizzolo. That is all I will say about Arete.'); Replies = @('nothing') }
  }
  Trades = [ordered]@{ monitor = [ordered]@{ If = @('HasQuest AAX2'); Items = @(,(TI 'QMON')); Goto = 'briefed' } }
}

Script 'Strongbox.json' @('ddogs-strongbox') 'Merchant''s Strongbox in the room the guide gives at 3407.9 by 877.6. Y is the camp deck.' @('A merchant''s strongbox. It is locked.') @() 'Still locked.' @{
  Openers = @([ordered]@{ If = @('HasQuest AST0'); Say = 'pick' })
  Lines = [ordered]@{ pick = [ordered]@{ Text = 'The Vacuum Packed Lock Pick goes in the trade window.{opentrade}' }
    opened = [ordered]@{ Text = @('{completequest AST0}', '{spawn QAAF 1}', 'The lock gives. Antonio''s Adaptation Factory was inside. Stan wants that, plus the transmitter, the blank info chip and the encryption compiler.'); Fail = 'full'; Replies = @('nothing') }
    full = [ordered]@{ Text = 'Your hands are full. Make some room in your inventory first.{close}' } }
  Trades = [ordered]@{ pick = [ordered]@{ If = @('HasQuest AST0'); Items = @(,(TI 'QLOK')); Goto = 'opened' } }
}
Script 'StanGoodman.json' @('SimpleChar:78E0FC65') 'Stan Goodman, camp (3463x880). Guide: lockpick from ICC Tech, the strongbox, then four parts. 1240 credits, up to 2229 xp, Unprogrammed Identification Chip.' @('{delay 1}Stan Goodman, purveyor of recently used merchandise.') @(
  (J 'factory' $stan 'Alex sent me.' @('Buy a Vacuum Packed Lock Pick from ICC Tech Supplies if you do not have one. I will hand you one, because that counter is not stocked here.', '{delay 1}Sneak past the security dog and lockpick the Merchant''s Strongbox in the room at 3408 by 878. Bring me Antonio''s Adaptation Factory, the Generic Nano Transmitter, the Blank Info Chip and the 2560-Bit Encryption Compiler.') 'Deal.' 'Lockpick the strongbox, then bring the factory, the transmitter, the info chip and the compiler.' $alex.Return @{ Mode = 'trade'; Items = @((TI 'QAAF'), (TI 'QGNT'), (TI 'QBIC'), (TI 'QENC')); Give = '{spawn QLOK 1}'; Thanks = @('That is the set. The Unprogrammed Identification Chip is yours.'); After = @('{givequest HMPS}', '{delay 1}Marco Spida is in the camp, about 3408 by 831. He sells profession nanos.') })
) 'Stay sharp out there.' (Arrive $hStan @('Alex sent you, did he?'))
Script 'AntonioStacklundArete.json' @('SimpleChar:78E0FC7C') 'Antonio Stacklund, Arete camp. The Adaptation Factory is in the strongbox, not a gift from him. He sells the upgrade parts.' @('{delay 1}Antonio Stacklund. If it is for sale, I have it. Mostly.', '{delay 1}The Adaptation Factory people keep asking me for was in a strongbox. I sell the parts the upgrades eat: plates, hides, chambers, beaks, intestines. The factory itself is consumed each time you combine.') @() 'Come back any time.' $null

Script 'MarcoSpida.json' @('ONRP') 'Marco Spida (3408x831). Guide: buy your profession nano container from him and open it. 1120 credits, up to 2229 xp, Composite Attribute Boost.' @('{delay 1}Marco Spida. Nanos, for people who can pay.') @(
  (J 'nano' $marco 'Stan said you sell nanos.' @('Buy the nano container for your profession and open it. My counter is not a full shop yet, so I will hand you the container.', '{delay 1}Open it, then tell me.') 'I will open it.' 'Open the profession nano container Marco gives you.' $stan.Return @{ Mode = 'talk'; Give = '{spawn QNPC 1}'; Thanks = @('Opened. Here is a Composite Attribute Boost crystal. Sarah Greene is next.'); After = @('{givequest HSAG}', '{delay 1}Sarah Greene, the armorsmith, about 3471 by 841.') })
) 'The can I handed you is the Soldier nanoprogram container. The other professions have their own can on retail. The crystal is the same.' (Arrive $hMarco @('Stan sent you. I can see it.'))

$thief = Giveaway $sarah.Return 'QDLA' @('The thief is dead. The DNA-Locked Prototype Raven Combat Armor is still on the body.', '{delay 1}It is locked to its owner. Sarah Greene is the armorsmith who can use it.')
Script 'SarahGreene.json' @('SimpleChar:78E0FC69') 'Sarah Greene, armorsmith (3471x841). Guide: the armor is on a dead thief in the Underground, under a Mutated Garbage Flea. Not the Shady Guy.' @('{delay 1}Hi there! I''m Sarah. I make armor, good armor.') @(
  (J 'armor' $sarah 'Marco said you could help with my ID.' @('Someone stole a DNA-Locked Prototype Raven Combat Armor. The thief is dead in the Underground under the market, under a Mutated Garbage Flea.', '{delay 1}Garbage Fleas line that path. Bring the armor back. The Shady Guy south of the landing is a different crook.') 'I''ll go under the market.' 'The dead thief in the Underground, under the Mutated Garbage Flea.' $marco.Return @{ Mode = 'trade'; Items = @(,(TI 'QDLA')); Thanks = @('My armor! These Biological Survey Nanobots are yours.'); After = @('{givequest HVGO}', '{delay 1}Vernon Godfray is the hacker in the camp, about 3434 by 826.') })
) 'See you around!' (Merge (Arrive $hSarah @('Marco sent you? Of course he did.')) $null)
Script 'DeadThief.json' @('ddogs-dead-thief') 'Dead thief beside the Mutated Garbage Flea spawn IEGA in the Underground.' @('A dead thief. The armor is still buckled on.') @() 'The body does not move.' $thief

$lib = @{ Openers = @([ordered]@{ If = @('HasQuest AVG0'); Say = 'lib' })
  Lines = [ordered]@{ lib = [ordered]@{ Text = 'Hacker Tool and the Omni-Tek Technical Library, together.{opentrade}' }
    lib_done = [ordered]@{ Text = @('{completequest AVG0}', '{spawn QHKL 1}', '{spawn QHKT 1}', 'Hacked. Keep the library and the tool. Now the Shipping Manifest Terminal, same building as the Kneebreaker. Use the Hacker Tool on it and bring me the chip it releases.'); Fail = 'full'; Replies = @('nothing') }
    full = [ordered]@{ Text = 'Your hands are full. Make some room in your inventory first.{close}' } }
  Trades = [ordered]@{ lib = [ordered]@{ If = @('HasQuest AVG0'); Items = @((TI 'QHKT'), (TI 'QLIB')); Goto = 'lib_done' } } }
Script 'VernonGodfray.json' @('SimpleChar:78E0FC68') 'Vernon Godfray (3434x826). Guide: hack the library, hack the manifest terminal, he programs the chip. Shades go to Lady Sheila. Everyone else to Doctor Mason.' @('{delay 1}Vernon Godfray. I do not do small talk.') @(
  (J 'chip' $vern 'Sarah sent me about an ID chip.' @('Combine a Hacker Tool with an Omni-Tek Technical Library. I will hand you both, the shop step is not stocked here.', '{delay 1}Bring me the hacked library, then hack the Shipping Manifest Terminal. The chip comes back here so I can program it.') 'On my way.' 'Hack the library, hack the manifest terminal, bring the chip.' $sarah.Return @{ Mode = 'trade'; Items = @(,(TI 'QUIC')); Give = @('{spawn QHKT 1}', '{spawn QLIB 1}'); Thanks = @('Programmed. This Blank ICC ID Chip is yours.'); After = @('{delay 1}Shades: Lady Sheila Black, about 3412 by 904. Everyone else: Doctor Mason, about 3431 by 796.') })
) 'Do not mention my name to the peacekeepers.' (Merge (Arrive $hVern @('Sarah sent you. Fine.')) $lib)
Script 'ShippingManifestTerminal.json' @('SimpleChar:78E0FC6A') 'Shipping Manifest Terminal by the pad. Guide: hack it with the Hacker Tool after the library.' @('SHIPPING MANIFEST TERMINAL. Access denied.') @() 'SESSION CLOSED.' @{
  Openers = @([ordered]@{ If = @('HasQuest AVG1'); Say = 'hack' })
  Lines = [ordered]@{ hack = [ordered]@{ Text = 'The Hacker Tool interfaces. Put it in the trade window.{opentrade}' }
    hacked = [ordered]@{ Text = @('{completequest AVG1}', '{spawn QUIC 1}', 'Override accepted. 1x Unprogrammed Identification Chip released. Vernon is waiting.'); Fail = 'full'; Replies = @('nothing') }
    full = [ordered]@{ Text = 'Your hands are full. Make some room in your inventory first.{close}' } }
  Trades = [ordered]@{ hack = [ordered]@{ If = @('HasQuest AVG1'); Items = @(,(TI 'QHKT')); Goto = 'hacked' } }
}

Script 'DrMason.json' @('SimpleChar:78E0FC6C') 'Doctor Mason (3431x796). Guide: Basic Leg Implant, then Agility Shiny, Stamina Bright and Max Health Faded clusters, installed at the Surgery Clinic. He gives Leg Implant: Agility, Shiny. Not a Rollerrat hunt.' @('{delay 1}Doctor Mason. Hold still, this will not hurt. Much.') @(
  (J 'implant' $mason 'Vernon sent me about the chip.' @('Buy a Basic Leg Implant, level 1. Then an Agility cluster, Shiny, for the leg. Then Stamina, Bright. Then Max Health, Faded. Assemble them in that order and install the implant at the Surgery Clinic.', '{delay 1}The clinic combine is not a quest action here. I will fit the finished Leg Implant: Agility, Shiny when you come back. A wrong implant I would refuse.') 'I''ll have it assembled.' 'Basic Leg Implant, then the three clusters, installed at the Surgery Clinic.' $vern.Return @{ Mode = 'talk'; Give = '{spawn QBLI 1}'; Thanks = @('That is the right implant. Your chip can ride with it. Lorelei at the bar knows the way out.'); After = @('{givequest HLOR}') })
) 'Come back if anything itches.' (Arrive $hMason @('Vernon''s patient? Sit down.'))
Script 'LadySheilaBlack.json' @('LDSH') 'Lady Sheila Black (3412x904), shades only. Guide: Spirit Siphon under 21 percent health, a Soul Capsule, Comfortless Spirit of Defense, then the Personalized ICC ID Chip.' @('{delay 1}Lady Sheila Black. I do not bargain with just anyone.') @(
  (J 'spirit' $sheila 'Vernon said shades should see you.' @('Spirit Siphon, while you are under 21 percent health. Then a Soul Capsule. Then a Comfortless Spirit of Defense.', '{delay 1}The siphon and the capsule are not a single quest action here. Come back and I will give you the Personalized ICC ID Chip, and the spirit of defense.') 'I will return.' 'Spirit Siphon under 21 percent health, a Soul Capsule, then the Comfortless Spirit of Defense.' $vern.Return @{ Mode = 'talk'; Prof = '15'; Give = @('{spawn QSIP 1}', '{spawn QCSD 1}'); Thanks = @('The chip is personalized. Lorelei is next.'); After = @('{givequest HLOR}') })
) 'Do not waste my time.' $null

$lolly = @{ Openers = @([ordered]@{ If = @('HasQuest ALOR', '!has QPCR'); Say = 'cookie' })
  Lines = [ordered]@{ cookie = [ordered]@{ Text = 'She takes the cookie, then the cage.{opentrade}' }
    caged = [ordered]@{ Text = @('{spawn QPCR 1}', 'Lolly hops into the cage.'); Fail = 'full'; Replies = @('nothing') }
    full = [ordered]@{ Text = 'Your hands are full. Make some room in your inventory first.{close}' } }
  Trades = [ordered]@{ cookie = [ordered]@{ If = @('HasQuest ALOR'); Items = @((TI 'QCKE'), (TI 'QCAG')); Goto = 'caged' } } }
$lorExtra = Merge (Arrive $hLor @('You have the chip? Then we can talk about Vaughn.')) @{ Hello = @('shop'); Replies = [ordered]@{ shop = [ordered]@{ Text = 'What are you serving?'; Goto = 'shop' } }; Lines = [ordered]@{ shop = [ordered]@{ Text = 'Take a look.{shop}' } } }
Script 'LoreleiTheBartender.json' @('SimpleChar:78E0FC6B') 'Lorelei the Bartender. Guide: Tasty Peanut Butter Cookie, her cage, give the cookie to Lolly, combine Lolly with the cage, return the bird and the ID chip. Reward: Identification Card.' @('{delay 1}Welcome to my bar. What will it be?') @(
  (J 'pet' $lor 'Mason, or Sheila, said you know a way out.' @('Buy a Tasty Peanut Butter Cookie. I will hand you the cookie and a cage, the counter is not stocked for this.', '{delay 1}Lolly is a level 10 reet at the oasis. Give her the cookie, put her in the cage, and bring her back with your ID chip.') 'I''ll find her.' 'Cookie for Lolly, then the caged reet and your ID chip.' $vern.Return @{ Mode = 'trade'; Items = @((TI 'QPCR'), (TI 'QICC')); Give = @('{spawn QCKE 1}', '{spawn QCAG 1}'); Thanks = @('Lolly! And the chip. This Identification Card is yours.'); After = @('{givequest AVHR}', '{delay 1}Vaughn Hammond is by the ICC door, about 3369 by 828.') })
) 'Come back any time.' (Merge $lorExtra $lolly)
Script 'LollyTheReet.json' @('SimpleChar:7985CAEC') 'Lolly the Reet, Lorelei''s pet. Guide: give her the cookie, then cage her.' @('*The reet eyes you suspiciously.*') @() '*The reet goes back to sniffing the sand.*' $lolly
Script 'VaughnHammond.json' @('SimpleChar:78E0FC73') 'Vaughn Hammond (3369x828). Guide: hand over the Identification Card and the identity is uploaded. The 15000-credit bribe is not a quest action here.' @('{delay 1}Vaughn Hammond. An Identification Card, or we are not having this conversation.', '{delay 1}There is a story about a 15000 credit bribe. I am not taking a bribe. The card is the way.') @() 'Safe travels.' @{
  Openers = @([ordered]@{ If = @('HasQuest AVHR'); Say = 'card' })
  Lines = [ordered]@{ card = [ordered]@{ Text = 'The Identification Card. Trade it.{opentrade}' }
    uploaded = [ordered]@{ Text = @('{completequest AVHR}', 'Uploaded. Whenever you are ready, the ICC shuttle will take you on.'); Replies = @('nothing') } }
  Trades = [ordered]@{ card = [ordered]@{ If = @('HasQuest AVHR'); Items = @(,(TI 'QIDC')); Goto = 'uploaded' } }
}

Script 'DesmondCalitri.json' @('SimpleChar:78E0FC77') 'Desmond Calitri (3605x827). Guide: a plain Bronto Burger, two Protesters, then Cedric Harding. 1120 credits, up to 2229 xp, and the three suits. Not the docker robots.' @('{delay 1}Desmond Calitri. The strikers outside are the problem, not the docker robots.') @(
  (J 'strike' $desmond 'What do you want done?' @('Buy a plain Bronto Burger from Barry, 30 credits, first food row on the far right. I will hand you one because Barry''s counter is not stocked.', '{delay 1}Bring it back. Then kill two Protesters, level 2. Then Cedric Harding, level 6, the organizer. Violent Protesters may join him; they are not a separate count.') 'Consider it done.' 'The burger, two Protesters, then Cedric Harding.' $null @{ Mode = 'talk'; Give = '{spawn QBRG 1}'; Luck = 'The burger first. Then the strikers.'; Thanks = @('Cedric is down. The pay is 1120 credits, and the suits.'); After = @() })
) 'Later.' @{
  Openers = @([ordered]@{ If = @('HasQuest ADP0'); Say = 'burger' })
  Lines = [ordered]@{ burger = [ordered]@{ Text = 'Plain, I hope. Trade the Bronto Burger.{opentrade}' }
    burger_done = [ordered]@{ Text = @('{completequest ADP0}', 'Greasy enough. Two Protesters, then Cedric Harding.'); Replies = @('nothing') } }
  Trades = [ordered]@{ burger = [ordered]@{ If = @('HasQuest ADP0'); Items = @(,(TI 'QBRG')); Goto = 'burger_done' } }
}
Script 'PatrickSun.json' @('SimpleChar:78E0FC7B') 'Patrick Sun (3463x812). Guide: touch the Insurance Terminal, he kills you as a joke, reward Belt Component Platform 300X. The reets are not his job.' @('{delay 1}Patrick Sun. The insurance terminal is right there. Give it a poke.') @(
  (J 'insurance' $patrick 'What does the terminal do?' 'Touch it. I will do the rest. It is a joke, and you will not actually die.' 'I touched it.' 'Touch the Insurance Terminal.' $null @{ Mode = 'talk'; Thanks = @('Ha! You should have seen your face. The Belt Component Platform 300X is yours. Computer Literacy comes with it.') })
) 'The Greedy Desert Reet is a different story. I am not hiring you for it.' $null
Script 'ShadyGuy.json' @('SimpleChar:78E0FC79') 'Shady Guy, south of the landing (3541x748). Guide: crowbar, the white desert car, Mario Carles, return the Stolen Yalmaha and the crowbar. 5000 credits, up to 1540 xp, Kodaik Velocity IV.' @('{delay 1}What are you looking at?') @(
  (J 'car' $shady 'You selling something?' @('Take the crowbar. Smash the fancy white desert car and kill Mario Carles. Level 10.', '{delay 1}He is just east of me. Bring the Stolen Yalmaha and the crowbar back.') 'Hand me the crowbar.' 'Kill Mario Carles and bring back the Stolen Yalmaha and the crowbar.' $null @{ Mode = 'trade'; Items = @((TI 'QYAL'), (TI 'QCBR')); Give = '{spawn QCBR 1}'; Thanks = @('5000 credits, and a Kodaik Velocity IV. We are done.') })
) 'Nothing to see here.' $null
Script 'RemiGallois.json' @('RMGL') 'Remi Gallois, stall by Antonio (3434x833). Subscriber field test. The SANDSTORM Marauders are not on this landing, so he explains the test and does not offer a quest that cannot be finished.' @('{delay 1}Remi Gallois. Subscribers get the field test: an Experimental Hellfyre Rocket Launcher against SANDSTORM Marauders.', '{delay 1}The launcher burns out after about a minute. The marauders are not spawned on Arete, so I am not sending you after them. The pay, when the test exists, is four composite nano crystals.') @() 'Ask Antonio if you need parts.' $null
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
# ---------------- Nascence Training Ground (4313), Drake Rodriguez. Bits continue from 645. ----------------
# Retail tests, without the live timers. Heckler of Evolution drops 6 Delicate Metal Devices per kill (Randy 2026-10-14).
# Screenshot root: gear, "Mandatory?!", refuse, bracer, goodbye. Jobe-bridge geography is not used.
$dStr = Job 'SCDR' 'XDS' @(@('KillOne','DCWA',$null,'Kill a Deceitful Weaver'),@('KillOne','SICL',$null,'Kill a Shining Coiler'),@('KillOne','BEGR',$null,'Kill a Beit Gras')) 'Return to Drake' 195 360 @(Item 223771 'Bracer of Strength')
$dAgi = Job 'SCDR' 'XDA' @(@('TargetNpc','HUPD',$null,'Tag a Hungry Predator'),@('TargetNpc','LUCH',$null,'Tag a Lurking Chimera'),@('TargetNpc','PODR',$null,'Tag a Pond Dryad')) 'Return to Drake' 195 360 @(Item 223766 'Protective Shirt of Exploration')
$dSen = Job 'SCDR' 'XDN' @(,@('TargetNpc','DOWL',$null,'Find a Shadowleet')) 'Return to Drake' 175 350 @(Item 223768 'Nano Crystal (Overview of Nascence)')
$dEl = Job 'EDTH' 'XDE' @(,@('TargetNpc','YOSI',$null,'Guide the Young Silvertail')) 'Return to El-Domo' 50 0 $null
$dCe = Job 'CTTC' 'XDC' @(,@('KillOne','SIMR',$null,'Silence the Silvertail Mare')) 'Return to Celeth-El' 50 0 @(Item 233804 'Scourged Bracer of Jobe')
$dPsy = Job 'SCDR' 'XDP' @() 'Report the psychic test' 195 360 @(Item 223769 'Ring of Enhanced Psychic')
$dInt = Job 'SCDR' 'XDI' @(@('TargetNpc','GSTD',$null,'Ask Brad Willis for help'),@('TargetNpc','STSH',$null,'Deliver supplies to Sarah')) 'Return to Drake' 195 360 @(Item 223773 'Bonfire Kit')
$dBr = Job 'SCDR' 'XDB' @() 'Bring 6 metal devices' 235 440 @(Item 223770 'Smart Backpack') @{ Ret = 'FindItem'; Spec = 'QDMD' }
$drakeExtra = @{
  Hello = @('gear','mandatory','bracer','helped_el','helped_ce','refuse')
  Openers = @([ordered]@{ If = @('HasQuest XDPR'); Say = 'psy_done' })
  Replies = [ordered]@{
    gear = [ordered]@{ Text = 'I need the standard issue gear.'; If = @('!has QBRJ'); Goto = 'gear' }
    mandatory = [ordered]@{ Text = 'Mandatory?!'; Goto = 'mandatory' }
    bracer = [ordered]@{ Text = 'Tell me more about that bracer.'; Goto = 'bracer' }
    refuse = [ordered]@{ Text = 'Forget about it. I''m not going to participate in any of your tests.'; Goto = 'refuse' }
    helped_el = [ordered]@{ Text = 'I helped El-Domo.'; If = @('HasCompletedQuest XDER','!HasQuest XDPR','!HasCompletedQuest XDPR'); Goto = 'give_psy' }
    helped_ce = [ordered]@{ Text = 'I helped Celeth-El.'; If = @('HasCompletedQuest XDCR','!HasQuest XDPR','!HasCompletedQuest XDPR'); Goto = 'give_psy' }
  }
  Lines = [ordered]@{
    gear = [ordered]@{ Text = @('{spawn QBRJ 1}','{spawn QJSV 1}','{spawn QJSB 1}','{spawn QJSP 1}','Standard issue. The bracer measures your performance. While you are level 15 or under, it can return you to me. Press I to check your inventory.'); Fail = 'full'; Replies = @('more','nothing') }
    full = [ordered]@{ Text = 'Your hands are full. Make some room in your inventory first.{close}' }
    mandatory = [ordered]@{ Text = @('We have a healthy specimen here. I will take you through the usual tests before we send you to the brink.', '{delay 1}Rodriguez out. The rewards are real, and I have never had anyone refuse.'); Replies = @('ask_strength','bracer','refuse','nothing') }
    bracer = [ordered]@{ Text = 'That bracer records the examination. Wear it while you take the tests. Under level 15 it can also bring you back to me if you are lost.'; Replies = @('more','nothing') }
    refuse = [ordered]@{ Text = 'Noted. The offer stands when you change your mind.{close}' }
    give_psy = [ordered]@{ Text = @('{givequest XDPR}','Then that test is ready to close. Speak to me again and I will record it.') }
    psy_done = [ordered]@{ Text = @('{completequest XDPR}','{spawn QBRJ 1}','Your mind held. This ring is yours, and a fresh bracer if Celeth-El kept the other one.'); Fail = 'full'; Replies = @('more','nothing') }
  }
}
Script 'DrakeRodriguez.json' @('SCDR') 'Scientist Drake Rodriguez, Nascence Training Ground tower (414x354). Full test chain. Heckler of Evolution drops 6 Delicate Metal Devices.' @(
  '{delay 1}I am Drake Rodriguez and I will be handling your case today. Here, take this standard issue gear. You''ll need it for your mandatory examination.',
  '{delay 1}Are you ready?') @(
  (J 'strength' $dStr 'I will take the Test of Strength.' @('Kill a Deceitful Weaver, then a Shining Coiler, then a Beit Gras. They are around the tower and the water.', '{delay 1}Bring the result back to me. The reward is a Bracer of Strength.') 'I''ll do it.' 'A Deceitful Weaver, a Shining Coiler, then a Beit Gras.' $null),
  (J 'agility' $dAgi 'I will take the Test of Agility and Stamina.' @('Take this biomarker. Tag a Hungry Predator, a Lurking Chimera and a Pond Dryad. Target each one up close.', '{delay 1}The reward is a Protective Shirt of Exploration.') 'I''ll tag them.' 'Tag a Hungry Predator, a Lurking Chimera and a Pond Dryad.' $dStr.Return @{ Give = '{spawn QBMS 1}' }),
  (J 'sense' $dSen 'I will take the Test of Sense.' 'A Shadowleet is hiding in the grass near the start of the grounds. Get close and target it. On retail it is invisible until you search; here you can see it.' 'I''ll find one.' 'Target a Shadowleet.' $dAgi.Return),
  (J 'mind' $dInt 'I will take the Test of Intelligence.' @('These supplies go to Scientist Sarah Willis. An Imp Disruptor guards her crevice.', '{delay 1}Assistant Brad Willis is outside. Target him, then target Sarah and leave the supplies. The reward is a Bonfire Kit.') 'I''ll get them through.' 'Target Brad Willis, then Sarah Willis.' $dPsy.Return @{ Give = '{spawn QSUP 1}' }),
  (J 'brink' $dBr 'I will take the final test.' @('Out at the brink, kill the Heckler of Evolution. It drops six Delicate Metal Devices.', '{delay 1}Bring all six back. The other rock piles are Hecklers of Nature. Leave them if you only need the device.') 'I''ll bring the devices.' 'Six Delicate Metal Devices from the Heckler of Evolution.' $dInt.Return @{ Mode = 'trade'; Items = @(,(TI 'QDMD' 6)); Thanks = @('An ancient device. There is a civilization under this place. If you want to go further, find Scientist Donna Red or Scientist Veronica Escobar out in Nascence.') })
) 'The tests are done. Nascence is through the way you came, when you are ready.' $drakeExtra
Script 'ElDomo.json' @('EDTH') 'El-Domo the Forest Ranger, Training Ground ruins. Redeemed side of Drake''s psychic test.' @('{delay 1}I am El-Domo. One of the silvertail young has lost the herd.', '{delay 1}If Drake sent you, I can use the help.') @(
  (J 'young' $dEl 'Drake sent me. I will help the young silvertail.' @('The Young Silvertail is in the grove to the southwest. Target it and it will follow the scent home.', '{delay 1}Then come back.') 'I will find it.' 'Target the Young Silvertail southwest of here.' $dSen.Return @{ Mode = 'talk'; Thanks = @('The young one is safe. Tell Drake you helped the Redeemed.') })
) 'The herd is calmer today.' $null
Script 'CelethEl.json' @('CTTC') 'Celeth-El, the Crystal Guardian. Unredeemed side of Drake''s psychic test. Talkable.' @('{delay 1}Celeth-El. You wear Jobe''s bracer. Give me a moment with it.', '{delay 1}There is a Silvertail Mare crying outside this cleft. Silence her.') @(
  (J 'mare' $dCe 'I will deal with the Silvertail Mare.' 'She stands just outside. Kill her, then come back. I will return your bracer... changed.' 'Consider it done.' 'Kill the Silvertail Mare.' $dSen.Return @{ Mode = 'talk'; Thanks = @('It is scourged. The bracer is stronger, and Drake will not accept this one. Ask him for another, and tell him the Chosen are satisfied.') })
) 'The crystals are quiet.' $null
Script 'YoungSilvertail.json' @('YOSI') 'Young Silvertail for El-Domo''s test. Friendly.' @('*The foal noses at your hand and starts toward the herd.*') @() '*The foal stays close.*' $null
Script 'SarahWillis.json' @('STSH') 'Scientist Sarah Willis, in the crevice with the Imp Disruptor.' @('{delay 1}You brought supplies? Leave them. Brad has the imp''s attention, so do not waste it.', '{delay 1}Tell Drake the delivery landed.') @() 'Go, before it turns around.' $null
Script 'BradWillis.json' @('GSTD') 'Assistant Brad Willis, outside the imp crevice.' @('{delay 1}That Imp Disruptor will end you if you walk in cold. I''ll hold it.', '{delay 1}Target me so Drake''s bracer logs it, then get to Sarah.') @() 'I have the imp. Move.' $null
Script 'SundanceSmith.json' @('SDSM') 'Sundance Smith, J.A.M.E. Depot. Placement guessed near the lower bridge. Shop stock is not invented.' @('{delay 1}Sundance Smith, J.A.M.E. Depot. Armor, NCU, ammunition and a fair price for whatever the wildlife dropped.', '{delay 1}The counter is not stocked on this server yet. I can still point you at Drake if you have not taken his tests.') @() 'Come back when the depot is open.' $null
# ---------------- Insipid Ring, Nascence to Elysium. Bits 653+. Randy: oil to Ergo, not the longer menace chain. ----------------
# Level 50+ may skip (AO-Universe). The ring is profession-unit specific. Portal item-check is not wired.
if ($script:bit -ne 653) { throw "Insipid expected bit 653, got $($script:bit)" }
$trial = Job 'ERGO' 'EIN' @(
  ,@('FindItem',$null,$null,'Reach a Yutto')
  ,@('KillOne','EMOL',$null,'Kill Embryonic Molokhai')
  ,@('KillOne','LMOL',$null,'Kill Larval Molokhai')
  ,@('KillOne','MMOL',$null,'Kill Metamorphosing Molokhai')
  ,@('KillOne','YMOL',$null,'Kill Young Molokhai')
) 'Hand Baby Molok Oil to Ergo' 8000 4000 $null @{ Ret = 'FindItem'; Spec = 'BMOL' }
$skip = Job 'ERGO' 'EIS' @() 'Skip the Nascence trial' 2000 0 $null @{ Ret = 'FindItem' }
$fresh = @('!HasCompletedQuest EINR','!HasCompletedQuest EISR','!HasAnyQuest EIN0,EIN1,EIN2,EIN3,EIN4,EINR,EISR')
function Ring($quest, $hash, $words) {
  [ordered]@{ Text = @('{completequest ' + $quest + '}', ('{spawn ' + $hash + ' 1}'), $words); Fail = 'full'; Replies = @('nothing') }
}
$ergoExtra = @{
  Openers = @(
    [ordered]@{ If = @('HasQuest EIN0'); Say = 'busy_yutto' }
    [ordered]@{ If = @('HasQuest EIN1'); Say = 'busy_em' }
    [ordered]@{ If = @('HasQuest EIN2'); Say = 'busy_la' }
    [ordered]@{ If = @('HasQuest EIN3'); Say = 'busy_me' }
    [ordered]@{ If = @('HasQuest EIN4'); Say = 'busy_yo' }
    [ordered]@{ If = @('HasQuest EINR'); Say = 'oil' }
  )
  Hello = @('trial','skip_ar','skip_co','skip_ex','skip_in','skip_su')
  Replies = [ordered]@{
    trial = [ordered]@{ Text = 'I will take the trial.'; If = $fresh; Goto = 'give_trial' }
    skip_ar = [ordered]@{ Text = 'I am past this trial.'; If = @('minlevel 50','prof 1,9') + $fresh; Goto = 'skip_ar' }
    skip_co = [ordered]@{ Text = 'I am past this trial.'; If = @('minlevel 50','prof 8,11,7') + $fresh; Goto = 'skip_co' }
    skip_ex = [ordered]@{ Text = 'I am past this trial.'; If = @('minlevel 50','prof 5,4,15') + $fresh; Goto = 'skip_ex' }
    skip_in = [ordered]@{ Text = 'I am past this trial.'; If = @('minlevel 50','prof 6,2') + $fresh; Goto = 'skip_in' }
    skip_su = [ordered]@{ Text = 'I am past this trial.'; If = @('minlevel 50','prof 10,3,14,12') + $fresh; Goto = 'skip_su' }
    handin = [ordered]@{ Text = 'Here is the Baby Molok Oil.'; If = @('HasQuest EINR'); Goto = 'handin' }
  }
  Lines = [ordered]@{
    give_trial = [ordered]@{ Text = @('{spawn FCAN 1}','{givequest EIN0}','This Fumigating Canister goes to a Yutto. Deep Breath Nine is in the Embryo in the Wilds, about 430 by 450. Long Stare at Danger is in the Swamp, about 1401 by 578.', '{delay 1}Either of them starts the molokhai. Kill the Embryonic, then the Larval, then the Metamorphosing, then the Young. The Young drops Baby Molok Oil. Bring the oil back to me.'); Fail = 'full'; Replies = @('nothing') }
    full = [ordered]@{ Text = 'Your hands are full. Make some room in your inventory first.{close}' }
    busy_yutto = [ordered]@{ Text = 'Deep Breath Nine, Embryo, Wilds, about 430 by 450. Or Long Stare at Danger, Swamp, about 1401 by 578. Give one of them the canister, or just talk to Long Stare.'; Replies = @('bye') }
    busy_em = [ordered]@{ Text = 'The Embryonic Molokhai is first. One is west of Deep Breath Nine, about 385 by 490. The other is just north of Long Stare, about 1414 by 604.'; Replies = @('bye') }
    busy_la = [ordered]@{ Text = 'The Larval Molokhai is next, standing with the others.'; Replies = @('bye') }
    busy_me = [ordered]@{ Text = 'The Metamorphosing Molokhai is next.'; Replies = @('bye') }
    busy_yo = [ordered]@{ Text = 'The Young Molokhai drops the Baby Molok Oil. Bring it to me.'; Replies = @('bye') }
    oil = [ordered]@{ Text = 'The oil. Put it in the trade window.'; Replies = @('handin','bye') }
    handin = [ordered]@{ Text = 'Let me see it.{opentrade}' }
    skip_ar = (Ring 'EISR' 'INAR' 'You have outgrown the trial. The Artillery ring is yours. Let it touch the portal to Elysium.')
    skip_co = (Ring 'EISR' 'INCO' 'You have outgrown the trial. The Control ring is yours. Let it touch the portal to Elysium.')
    skip_ex = (Ring 'EISR' 'INEX' 'You have outgrown the trial. The Extermination ring is yours. Let it touch the portal to Elysium.')
    skip_in = (Ring 'EISR' 'ININ' 'You have outgrown the trial. The Infantry ring is yours. Let it touch the portal to Elysium.')
    skip_su = (Ring 'EISR' 'INSU' 'You have outgrown the trial. The Support ring is yours. Let it touch the portal to Elysium.')
    ring_ar = (Ring 'EINR' 'INAR' 'Baby Molok Oil. The Artillery ring is yours. Let it touch the portal to Elysium.')
    ring_co = (Ring 'EINR' 'INCO' 'Baby Molok Oil. The Control ring is yours. Let it touch the portal to Elysium.')
    ring_ex = (Ring 'EINR' 'INEX' 'Baby Molok Oil. The Extermination ring is yours. Let it touch the portal to Elysium.')
    ring_in = (Ring 'EINR' 'ININ' 'Baby Molok Oil. The Infantry ring is yours. Let it touch the portal to Elysium.')
    ring_su = (Ring 'EINR' 'INSU' 'Baby Molok Oil. The Support ring is yours. Let it touch the portal to Elysium.')
  }
  Trades = [ordered]@{
    oil_ar = [ordered]@{ If = @('HasQuest EINR','prof 1,9'); Items = @(,(TI 'BMOL')); Goto = 'ring_ar' }
    oil_co = [ordered]@{ If = @('HasQuest EINR','prof 8,11,7'); Items = @(,(TI 'BMOL')); Goto = 'ring_co' }
    oil_ex = [ordered]@{ If = @('HasQuest EINR','prof 5,4,15'); Items = @(,(TI 'BMOL')); Goto = 'ring_ex' }
    oil_in = [ordered]@{ If = @('HasQuest EINR','prof 6,2'); Items = @(,(TI 'BMOL')); Goto = 'ring_in' }
    oil_su = [ordered]@{ If = @('HasQuest EINR','prof 10,3,14,12'); Items = @(,(TI 'BMOL')); Goto = 'ring_su' }
  }
}
Script 'Ergo.json' @('ERGO') 'Ergo, Nascence Swamp (4312). Guide spot 1428x195 is south of the client spawn cloud, so the position is guessed. Insipid Ring: canister to Deep Breath Nine, or Long Stare at Danger, then Embryonic, Larval, Metamorphosing, Young Molokhai, Baby Molok Oil back to Ergo. Level 50+ can skip.' @('{delay 1}I am Ergo. Words matter, and so does the ring that opens the way from Nascence to Elysium.', '{delay 1}Under level 50 the price is Baby Molok Oil. At 50 and above I can give you the ring without the trial.') @() 'The portal is the way on, when you have the ring.' $ergoExtra
Script 'DeepBreathNine.json' @('SimpleChar:7A1C0001') 'Deep Breath Nine, the Embryo, Nascence Wilds (4311, 430x450). Trades Ergo''s Fumigating Canister and points at the molokhai west of him.' @('{delay 1}Sshhh!... Excuse me, but I am a bit preoccupied here... The Yuttos no doubt is using some very deep simile...') @() 'Quietly, please.' @{
  Hello = @('ergo','where')
  Replies = [ordered]@{
    ergo = [ordered]@{ Text = 'I come from Ergo. I have the Fumigating Canister.'; If = @('HasQuest EIN0'); Goto = 'show' }
    where = [ordered]@{ Text = 'Where are the molokhai?'; If = @('HasAnyQuest EIN1,EIN2,EIN3,EIN4,EINR'); Goto = 'where' }
  }
  Lines = [ordered]@{
    show = [ordered]@{ Text = 'Oh! Sorry, please show me what it is, but do it quietly.{opentrade}' }
    opened = [ordered]@{ Text = @('{completequest EIN0}','The hive is just west of here, about 385 by 490. Kill the Embryonic Molokhai, then the Larval, then the Metamorphosing, then the Young. The Young carries Baby Molok Oil for Ergo.'); Replies = @('bye') }
    where = [ordered]@{ Text = 'West of here, about 385 by 490. Embryonic, Larval, Metamorphosing, then Young.'; Replies = @('bye') }
  }
  Trades = [ordered]@{ canister = [ordered]@{ If = @('HasQuest EIN0'); Items = @(,(TI 'FCAN')); Goto = 'opened' } }
}
Script 'LongStareAtDanger.json' @('LSSR') 'Long Stare at Danger, Nascence Swamp (4312, 1401x578). Client spawn. Starts the molokhai chain just north of him. No key item: the retail key id was not identified, so the entrance is open.' @('{delay 1}Long Stare at Danger. The molokhai under this swamp are hatching, and Ergo wants the oil from the youngest.') @() 'Watch the ground.' @{
  Hello = @('key','where')
  Replies = [ordered]@{
    key = [ordered]@{ Text = 'Ergo sent me for the molokhai.'; If = @('HasQuest EIN0'); Goto = 'key' }
    where = [ordered]@{ Text = 'Where are they?'; If = @('HasAnyQuest EIN1,EIN2,EIN3,EIN4,EINR'); Goto = 'where' }
  }
  Lines = [ordered]@{
    key = [ordered]@{ Text = @('{completequest EIN0}','The entrance is just north, about 1414 by 604. Kill the Embryonic Molokhai, then the Larval, then the Metamorphosing, then the Young. Bring Ergo the oil.'); Replies = @('bye') }
    where = [ordered]@{ Text = 'North of here, about 1414 by 604. Same order: Embryonic, Larval, Metamorphosing, Young.'; Replies = @('bye') }
  }
}
# ---------------- Janos, freighter 7101. Bit 655 replaces the guessed Deck Officer check-in. ----------------
# The sealed-parcel item id is not in itemnames, so the journal errand is completed by RALPH rather than a wrongly named item.
if ($script:bit -ne 655) { throw "Janos expected bit 655, got $($script:bit)" }
$janos = Job 'TPIB' 'JNS' @() 'Deliver the parcel to RALPH' 11266 0 $null @{ Ret = 'FindItem' }
if ($script:bit -ne 656) { throw "after Janos expected the counter at 656, got $($script:bit)" }
# Explicit Arete bits 656-662 are already assigned and do not move this counter.
$script:bit = 663
Script 'FreighterOfficer.json' @('TPIB') 'Janos, back of the Arete starting ship (playfield 7101). AO-Universe section 22, A Special Delivery. Bit 655.' @('{delay 1}Janos. Are you heading to ICC Headquarters?', '{delay 1}I have a parcel for the robot butler at the desk in the tallest tower. It is marked for Arbiter Vincenzo Palmiero.') @(
  (J 'parcel' $janos 'I can take it.' @('Leave with the blue android. Nobody else.', '{delay 1}The parcel item is not in the item list, so I am not handing you a stand-in. When you are done on Arete, the Arrival Hall is in Andromeda. Turn right to the Holo Deck tower, take the telepad behind the reception desk, and give the errand to RALPH.', '{delay 1}You cannot come back to Arete once you leave.') 'I''ll deliver it.' 'RALPH, the blue android in the Holo Deck at ICC Headquarters.' $null @{ Mode = 'elsewhere'; MinLevel = 5; Luck = 'RALPH is upstairs in the Holo Deck. He is not expecting you.' })
) 'Do not open it.' $null
Script 'Ralph.json' @('ddogs-ralph') 'RALPH, Holo Deck, Andromeda playfield 655, at the guide pin 3246 by 940. Y is the floor the building''s spawns stand on. He was not in the spawn dump upstairs.' @('{delay 1}RALPH. State your business.') @() 'I have a desk to keep.' @{
  Hello = @('who','delivery')
  Replies = [ordered]@{
    who = [ordered]@{ Text = 'Who are you?'; Goto = 'who' }
    delivery = [ordered]@{ Text = 'I have a delivery from Janos.'; If = @('HasQuest JNSR'); Goto = 'delivery' }
  }
  Lines = [ordered]@{
    who = [ordered]@{ Text = 'I keep the Holo Deck desk. The arbiter is not always in. If you have nothing to hand me, we are done.'; Replies = @('delivery', 'nothing') }
    delivery = [ordered]@{ Text = @('{completequest JNSR}', 'I was not expecting a delivery. Scanning. It is acceptable. It will be delivered.', '{delay 1}The published bounty is experience, up to 11266. The credit amount is not in the guide, so I am not inventing one.'); Replies = @('nothing') }
  }
}
# ---------------- write Quests.json ----------------
$qpath = Join-Path $GameData 'Quests.json'
$existing = ([IO.File]::ReadAllText($qpath) | ConvertFrom-Json) | ForEach-Object { $_ } | Where-Object { $_.QasInstance -lt 920000 -or $_.QasInstance -gt 929999 }
$all = @($existing) + $script:quests
$dup = $all | ForEach-Object { $_.Hash } | Group-Object | Where-Object Count -gt 1; if ($dup) { throw ('duplicate quest hashes: ' + ($dup.Name -join ',')) }
$badAct = $script:quests | Where-Object { $_['Action'] -isnot [string] -or $_['Name'] -isnot [string] }
if ($badAct) { throw ('quest action or name is not a string: ' + (($badAct | ForEach-Object { $_['Hash'] }) -join ',')) }
$dupBits = $script:quests | Where-Object { $null -ne $_['MissionBit'] } | Group-Object { $_['MissionBit'] } | Where-Object { $_.Count -gt 1 }
if ($dupBits) { throw ('duplicate mission bits: ' + (($dupBits | ForEach-Object { $_.Name }) -join ',')) }
[IO.File]::WriteAllText($qpath, (ConvertTo-Json @($all) -Depth 10), $u)
"quests total=$($all.Count) generated=$($script:quests.Count) bits=610-639,640-$($script:bit - 1)"
