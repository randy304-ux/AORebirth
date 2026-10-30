# ddogs/tools/Build-Live4582Data.ps1 - 2026-10-30, Randy's ICC Shuttleport live scrape (t641u).
#  ItemTemplates: AB01-AB16 basic starter weapons, AU01-AU15 their Adaptation Factory upgrades (adri_weapons_20261030.json).
#  Quests: live mission titles/briefs for Adri, Bio-Inspector, Molen armor, Fixx; DAPR = live "Bring weapon components to Adri"
#          (reward item comes from the trade, credits/xp kept); Molen pants/body rewards swapped to match the live briefs
#          (Climbing Salamanders -> Pants, Giant Snakes -> Body Armor). Counts unchanged.
#  Knubot scripts are written by Build-Live4582.py (run on the box). Rerunnable.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false); $gd = [IO.Path]::GetFullPath($GameData); $tag = 'live-4582-20261030'
$W = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'adri_weapons_20261030.json')) | ConvertFrom-Json
$ip = Join-Path $gd 'ItemTemplates.json'; $it = [IO.File]::ReadAllText($ip, $u) | ConvertFrom-Json
foreach ($p in @($it.PSObject.Properties)) { if ("$($p.Value.Source)" -eq $tag) { $it.PSObject.Properties.Remove($p.Name) } }
$added = 0
foreach ($w in $W) { foreach ($pair in @(@($w[2], $w[0]), @($w[5], $w[3]))) {
  $h = $pair[0]; $id = [int]$pair[1]
  if ($it.$h) { if (@($it.$h.Templates)[0] -ne $id) { throw "$h already used" } else { continue } }
  $it | Add-Member NoteProperty $h ([pscustomobject]@{ Templates = @($id, $id); Source = $tag }); $added++ } }
[IO.File]::WriteAllText($ip, ($it | ConvertTo-Json -Depth 20), $u)
$qp = Join-Path $gd 'Quests.json'; $q = [IO.File]::ReadAllText($qp, $u) | ConvertFrom-Json
$QH = @{}; foreach ($x in $q) { $QH[$x.Hash] = $x }
function Set-Q($h, $name, $summary) { $x = $QH[$h]; if (-not $x) { throw "no quest $h" }; if ($name) { $x.Name = $name }; if ($summary) { $x.Summary = $summary } }
$cull = 'The ICC Bio-Inspector needs help to cull some of the local wildlife before they hurt the eco-system of the island by overpopulating it.'
Set-Q DBC0 'Kill 3 Reef Salamanders' $cull; Set-Q DBC1 'Kill 3 Tropical Stalkers' $cull; Set-Q DBC2 'Kill 3 Cliff Malle' $cull
$lead = 'The ICC Bio-Inspector asked you to travel around the island and mark the location of the leaders of the packs of animals.'
Set-Q DBT0 'Find the salamander leader' $lead; Set-Q DBT1 'Find the leet leader' $lead; Set-Q DBT2 'Find the malle leader' $lead; Set-Q DBT3 'Find the rollerrat leader' $lead
Set-Q DBTR 'Return to the ICC Bio-Inspector' $lead
$dro = 'Adri Afeli, the freelancer you met close to the tunnel asked you to decommission some of the robots roaming the docks plus two of the robot leaders that have put down territory on the piers.'
Set-Q DAD0 'Disable 3 Cargo Droids' $dro; Set-Q DAD1 'Kill 3 Waste Collectors' $dro; Set-Q DAD2 'Kill the android leader' $dro; Set-Q DAD3 'Kill the junkbot leader' $dro; Set-Q DADR 'Return to Adri Afeli' $dro
Set-Q DAPR 'Bring weapon components to Adri' "Adri Afeli dabbles with weapons technology and has found a way to increase the output of some simple weapons. He has uploaded a list of five items he's looking for. As a reward for your hard work, he will give you a better weapon of your choice. The list he gave you is: Power Supply (Cargo Droid), Compression Chamber (Waste Collector), Bone Plates (Cliff Malle), Stalker Limbs (Tropical Stalker), Rollerrat Intestines (Rollerrat), a basic weapon. If you don't have a weapon, you can buy one from Antonio Stacklund."
$QH['DAPR'].Reward.Hash = $null; $QH['DAPR'].Reward.Records = $null
Set-Q DTA0 'Salamander clean up' 'Shuttleport Manager Travis Molen told you about the increasing problems the salamanders are creating on the landing pads. He asked you to make sure they could cause no further trouble. Final reward: Newcomer''s Pants'
Set-Q DTS0 'Landing Pad Investigation' 'Shuttleport Manager Travis Molen told you that there are reports of people sabotaging our shuttles, and he wants you to investigate the matter. Any saboteurs found should be terminated. Final reward: Newcomer''s Sleeves'
Set-Q DTG0 'Shuttle Infestation' 'Shuttleport Manager Travis Molen kindly asked you to remove a few of the annoying rollerrats down at one of the Secondary Landing Pads. Final reward: Newcomer''s Gloves.'
Set-Q DTP0 'Snake in the grass' 'Shuttleport Manager Travis Molen requested that the Giant Snakes should be eliminated, as they might be a source of infection. Final reward: Newcomer''s Body Armor'
Set-Q DTB0 'Deactivate the junkbots' 'Shuttleport Manager Travis Molen told you to pull the plug on the Cross-Wired Junkbots, as something obviously is malfunctioning. Final reward: Newcomer''s Boots.'
foreach ($h in 'DTAR','DTSR','DTGR','DTPR','DTBR') { Set-Q $h 'Return to Travis Molen for your reward' $null }
# live: salamanders -> pants, snakes -> body armor (swap the reward records only if still in the old order)
if ($QH['DTAR'].Reward.Records[0][0].Id -eq 248273) { $tmp = $QH['DTAR'].Reward.Records; $QH['DTAR'].Reward.Records = $QH['DTPR'].Reward.Records; $QH['DTPR'].Reward.Records = $tmp }
Set-Q DTO0 'Talk to the Unicorn Squadleader' 'The Shuttleport Manager, Travis Molen, wants you to make sure Unicorn Squadleader Fixx has everything he needs to finish his operation and leave the Shuttleport. You should try to return with a status report of the Unicorn operation.'
Set-Q DTOR 'Return to Travis Molen' 'Return with the status report to Travis Molen.'
Set-Q DFX0 'The Unicorns - 3 spiders left' 'The Unicorn Squadleader wants you to help the soldiers up north by exterminating some alien spiders.'
Set-Q DFXR 'Return to the Unicorn Squadleader for a debriefing.' $null
[IO.File]::WriteAllText($qp, ($q | ConvertTo-Json -Depth 20), $u)
"items added=$added; quests patched"

