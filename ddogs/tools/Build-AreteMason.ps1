# Build-AreteMason.ps1 - Doctor Mason, AO-Universe Arete guide #9b (Randy 2026-10-19 15:38).
# Steps: Assemble an Implant 1/1 (Agility Cluster - Shiny (Leg) into a Basic Leg Implant), 2/2 (Stamina Cluster - Bright (Leg)),
# 3/3 (Max Health Cluster - Faded (Leg)); each finishes when the Tradeskill window makes that implant (QuestTemplate.MadeItems).
# Then show Mason the implant (trade, he hands it back), install it at the Surgery Clinic ("wears" check), and trade him the
# Blank ICC ID Chip and Biological Survey Nanobots for the Personalized ICC ID Chip. Lorelei also takes the personalized chip.
# Result ids are the CellAO recipe chain: 101262+101782 -> 113127/113128, +101786 -> 113186/113187, +101808 -> 113440/113441.
# Mid-chain characters: an old active AMAR ("Fit the agility implant") is now the show step, so they bring the three-cluster
# implant; AVGR done without HDMA still opens the offer. Chained from Build-NpcTemplates.ps1 after Build-AreteVernon.ps1.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
Remove-TypeData System.Array -ErrorAction SilentlyContinue
$u = New-Object Text.UTF8Encoding $false
$gd = [IO.Path]::GetFullPath($GameData)

# Item hashes.
$ip = Join-Path $gd 'ItemTemplates.json'
$it = [IO.File]::ReadAllText($ip, $u)
$nl = if ($it.Contains("`r`n")) { "`r`n" } else { "`n" }
$want = [ordered]@{ QLA1 = '113127,113128'; QLA2 = '113186,113187'; QLA3 = '113440,113441'; QPIC = '296576,296576'; QBSN = '296574,296574' }
$rows = ''; $indent = if ($it -match '(?m)^([ \t]*)"QBLI":') { $matches[1] } else { '' }
foreach ($k in $want.Keys) {
  if ($it -match ('"' + $k + '":\s*\{"Templates":\s*\[([0-9,]+)\]')) { if ($matches[1] -ne $want[$k]) { throw "item hash $k already means $($matches[1])" }; continue }
  $rows += $indent + '"' + $k + '": {"Templates": [' + $want[$k] + ']},' + $nl
}
if ($rows) { $i = $it.IndexOf($indent + '"QBLI":'); if ($i -lt 0) { throw 'QBLI row missing' }; $it = $it.Insert($i, $rows); [IO.File]::WriteAllText($ip, $it, $u) }

# Quests.
function MkQuest($hash, $name, $qas, $summary, $made, $next, $bit, $records) {
  $o = [ordered]@{ Hash = $hash; Name = $name; QasInstance = $qas; Scope = 'Solo'; Action = 'TargetNpc'; Summary = $summary
    Objective = [pscustomobject]@{ Count = $null; Npc = 'ADMA'; SpecItem = $null; WorldItem = $null }; Flags = 'NoCash'; MissionBit = $bit; NextStep = $next
    Reward = [pscustomobject]@{ Hash = 'R' + $hash.Substring(1); Credits = 0; Xp = 0; Records = $records } }
  if ($made) { $o.MadeItems = $made }
  [pscustomobject]$o
}
$implant = @(,@([pscustomobject]@{ Id = 113440; HighId = 113441; Name = 'Leg Implant: Agility, Shiny'; Quality = 1 }))
$chip = @(,@([pscustomobject]@{ Id = 296576; HighId = 296576; Name = 'Personalized ICC ID Chip'; Quality = 1 }))
$new = @(
  (MkQuest 'AMA1' 'Assemble an Implant 1/1' 920187 'Tradeskill: Agility Cluster - Shiny (Leg) as source, Basic Leg Implant as target' @(113127, 113128) 'AMA2' $null $null),
  (MkQuest 'AMA2' 'Assemble an Implant 2/2' 920188 'Tradeskill: Stamina Cluster - Bright (Leg) into the Leg Implant: Agility, Shiny' @(113186, 113187) 'AMA3' $null $null),
  (MkQuest 'AMA3' 'Assemble an Implant 3/3' 920189 'Tradeskill: Max Health Cluster - Faded (Leg) into the Leg Implant: Agility, Shiny' @(113440, 113441) 'AMAR' $null $null),
  (MkQuest 'AMAR' 'Show Doctor Mason the implant' 920079 'Show Doctor Mason the Leg Implant: Agility, Shiny' $null $null 637 $implant),
  (MkQuest 'AMIN' 'Install the Leg Implant' 920190 'Install the Leg Implant: Agility, Shiny at the Surgery Clinic, then talk to Doctor Mason' $null $null 717 $chip)
)
$qp = Join-Path $gd 'Quests.json'
$hashes = $new | ForEach-Object Hash
$q = @(); foreach ($x in (ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($qp, $u)))) { if ($hashes -notcontains $x.Hash) { $q += $x } }
if ($q | Where-Object { $_.MissionBit -eq 717 -and $hashes -notcontains $_.Hash }) { throw 'bit 717 taken' }
foreach ($n in $new) { if ($q | Where-Object { $_.QasInstance -eq $n.QasInstance }) { throw "QasInstance $($n.QasInstance) taken" } }
$q += $new
[IO.File]::WriteAllText($qp, ((ConvertTo-Json -InputObject @($q) -Depth 8) -replace "`r`n", "`n") + "`n", $u)

# Doctor Mason.
function MkLine($text, $replies) { $o = [ordered]@{ Text = @($text) }; if ($replies) { $o.Replies = @($replies) }; [pscustomobject]$o }
function MkReply($text, $goto, $if) { $o = [ordered]@{ Text = $text }; if ($if) { $o.If = @($if) }; $o.Goto = $goto; [pscustomobject]$o }
$fresh = @('!HasCompletedQuest AMIN', '!HasAnyQuest AMA1,AMA2,AMA3,AMAR,AMIN', 'HasCompletedQuest AVGR')
$m = [ordered]@{
  Notes = 'Doctor Mason (3431x796). AO-Universe Arete #9b: Assemble an Implant 1/1, 2/2, 3/3 in the Tradeskill window (Shiny Agility, Bright Stamina, Faded Max Health, all Leg), show him the implant, install it at the Surgery Clinic, trade the Blank ICC ID Chip and Biological Survey Nanobots for the Personalized ICC ID Chip, then Lorelei. Generated by ddogs/tools/Build-AreteMason.ps1.'
  Npcs = @('SimpleChar:78E0FC6C')
  Openers = @(
    [pscustomobject]@{ If = @('HasQuest HDMA'); Say = 'arrive' },
    [pscustomobject]@{ If = @('HasQuest AMIN', 'wears QLA3'); Say = 'installed' },
    [pscustomobject]@{ If = @('HasQuest AMIN'); Say = 'not_installed' },
    [pscustomobject]@{ If = @('HasQuest AMAR'); Say = 'show' },
    [pscustomobject]@{ If = @('HasQuest AMA1'); Say = 'step1' },
    [pscustomobject]@{ If = @('HasQuest AMA2'); Say = 'step2' },
    [pscustomobject]@{ If = @('HasQuest AMA3'); Say = 'step3' },
    [pscustomobject]@{ Say = 'hello' })
  Replies = [ordered]@{
    ask_implant = (MkReply 'Vernon sent me about the chip.' 'offer_implant' $fresh)
    take_implant = (MkReply 'I''ll follow your recipe.' 'give_implant' $fresh)
    lost = (MkReply 'I lost the Basic Leg Implant.' 'lost' @('HasQuest AMA1', '!has QBLI'))
    later = (MkReply 'Maybe later.' 'later' $null)
    bye = (MkReply 'I''m on it.' 'bye' $null)
    more = (MkReply 'Anything else?' 'hello' $null)
    nothing = (MkReply 'Nothing right now, thanks.' 'nothing' $null)
  }
  Lines = [ordered]@{
    hello = (MkLine '{delay 1}Doctor Mason. Hold still, this will not hurt. Much.' @('ask_implant', 'nothing'))
    arrive = (MkLine @('{completequest HDMA}', 'Vernon''s patient? Sit down.') @('ask_implant', 'nothing'))
    offer_implant = (MkLine @('I can unlock that chip for you, but first I want to test my implant assembly process on you. Follow my recipe exactly.', '{delay 1}The terminals to your right sell Shiny, Bright and Faded clusters and blank implants. Basic Leg Implant first, then an Agility Cluster - Shiny (Leg), a Stamina Cluster - Bright (Leg) and a Max Health Cluster - Faded (Leg). Each cluster goes in as the source, the implant as the target, in that order.', '{delay 1}Make it with any other clusters and I will not touch it.') @('take_implant', 'later'))
    give_implant = (MkLine @('{spawn QBLI 1}', '{givequest AMA1}', 'Here is a Basic Leg Implant to start with. Shift+T for the Tradeskill window. Agility Cluster - Shiny (Leg) first.') @('bye'))
    lost = (MkLine @('{spawn QBLI 1}', 'Another one. Try not to sell this one.') @('bye'))
    step1 = (MkLine 'Agility Cluster - Shiny (Leg) as the source, the Basic Leg Implant as the target. The Shiny terminal has it.' @('lost', 'bye'))
    step2 = (MkLine 'Now the Stamina Cluster - Bright (Leg) into that implant. Bright terminal.' @('bye'))
    step3 = (MkLine 'Last, the Max Health Cluster - Faded (Leg). Faded terminal.' @('bye'))
    show = (MkLine 'Let me see it. The Leg Implant: Agility, Shiny, with Stamina Bright and Max Health Faded in it.{opentrade}' $null)
    shown = (MkLine @('{completequest AMAR}', 'Good work. Here, it is yours. Install it at the Surgery Clinic in the corner: use the clinic, then drop the implant in your Leg slot on the Implants tab of the Wear window (Ctrl+1).', '{givequest AMIN}') @('bye'))
    not_installed = (MkLine 'It is not in your leg yet. Use the Surgery Clinic in the corner, then put the implant in the Leg slot.' @('bye'))
    installed = (MkLine 'Installed properly. Now hand me the Blank ICC ID Chip and the Biological Survey Nanobots.{opentrade}' $null)
    done = (MkLine @('{completequest AMIN}', 'He pokes you with a DNA-unlocking device. Done. Your chip is personalized now. Lorelei at the bar can finish your card.', '{givequest HLOR}') @('more', 'nothing'))
    full = (MkLine 'Your hands are full. Make some room in your inventory first.{close}' $null)
    later = (MkLine 'Suit yourself. I''ll be here.{close}' $null)
    bye = (MkLine 'Good.{close}' $null)
    nothing = (MkLine 'Come back if anything itches.{close}' $null)
  }
  Trades = [ordered]@{
    show_implant = [pscustomobject]@{ If = @('HasQuest AMAR'); Items = @([pscustomobject]@{ Hash = 'QLA3'; Count = 1 }); Goto = 'shown' }
    chip = [pscustomobject]@{ If = @('HasQuest AMIN', 'wears QLA3'); Items = @([pscustomobject]@{ Hash = 'QICC'; Count = 1 }, [pscustomobject]@{ Hash = 'QBSN'; Count = 1 }); Goto = 'done' }
  }
}
$m.Lines.shown | Add-Member Fail 'full'
$m.Lines.done | Add-Member Fail 'full'
[IO.File]::WriteAllText((Join-Path $gd 'Knubot\DrMason.json'), ([pscustomobject]$m | ConvertTo-Json -Depth 12) + "`n", $u)

# Lorelei takes the personalized chip as well as the old blank one.
$lp = Join-Path $gd 'Knubot\LoreleiTheBartender.json'
$l = [IO.File]::ReadAllText($lp, $u) | ConvertFrom-Json
$l.Trades | Add-Member -Force NoteProperty turnin_pet_personal ([pscustomobject]@{ If = @('HasQuest ALOR'); Items = @([pscustomobject]@{ Hash = 'QPCR'; Count = 1 }, [pscustomobject]@{ Hash = 'QPIC'; Count = 1 }); Goto = 'done_pet' })
[IO.File]::WriteAllText($lp, ($l | ConvertTo-Json -Depth 12) + "`n", $u)
'Build-AreteMason: guide #9b implant steps AMA1-3, AMAR show, AMIN install, Personalized ICC ID Chip'
