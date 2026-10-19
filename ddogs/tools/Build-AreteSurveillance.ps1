# Build-AreteSurveillance.ps1 - Alex Gibbs' surveillance job on Arete Landing (6553), AO-Universe Arete guide #4 (Randy 2026-10-19 02:48 ET).
# Chained from Build-NpcTemplates.ps1 after Build-AreteClanLooks.ps1.
# Guide: Alex takes Flink's part, gives a Blank Info Chip, and you hack the Surveillance Droid with the Rebuilt HC-12 SecTec Monitor (QMON 295800),
#   drop the RC-P Audio Recording Device (QRCP 295801) on the Prized House Plant inside the house at the end of the road where Bodyguard
#   Logan Fixx is, give the monitor to ICC Immigration Officer Bill, defeat the Kneebreaker, report to Alex.
# Fixes:
#   - Alex's Bio Analyzing Computer hand-in (bac_done) gave AAX0 without the monitor and the recorder (only the unused 'take_watch' path spawned
#     them), so nobody coming from Flint got them. bac_done now spawns both, and while the job runs Alex hands out a missing one again.
#   - The plant was a content NPC outside the house (3602.2, 824.5) on a borrowed minidrone body. The client's own spawn point inside the
#     house is PRHO (L1, 3611.7, 8.2, 814.9: "PRized HOuse plant"), which had no template and showed as a pink 'To Be Determined'. PRHO is now
#     the Prized House Plant (rdb 'stone plant' 30377, the only plant body; scale guessed) with the HousePlant Knubot; the outside NPC is gone.
#   - The other pink point in the house, SWOO (L100, 3602.8, 8.3, 819.8), is Bodyguard Logan Fixx (live capture: L100, body 247041 'unicorn
#     elite', the 4582 Omni Unicorn Squadleader Fixx look). The SWOO -> Logan mapping is guessed from level, body and the guide's house.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = New-Object Text.UTF8Encoding $false
$gd = [IO.Path]::GetFullPath($GameData)
function Clone($o) { $o | ConvertTo-Json -Depth 12 | ConvertFrom-Json }
# NPC templates
$npath = Join-Path $gd 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath) | ConvertFrom-Json
$src = 'Arete guide #4 (AO-Universe), Randy 2026-10-19; Build-AreteSurveillance.ps1'
foreach ($d in @(
    @{ Hash = 'PRHO'; Name = 'Prized House Plant'; Level = 1; Md = 30377; Scale = 40 },
    @{ Hash = 'SWOO'; Name = 'Bodyguard Logan Fixx'; Level = 100; Md = 247041; Scale = 100 })) {
  $t = Clone (@($nt.OMUI.Templates)[0])
  $t.Name = $d.Name; $t.Level = $d.Level; $t.Stats | Add-Member -Force NoteProperty '54' $d.Level
  $t.Stats | Add-Member -Force NoteProperty '359' $d.Md; $t.Stats | Add-Member -Force NoteProperty '360' $d.Scale
  $t | Add-Member -Force NoteProperty Features @('NoCombat', 'NoMove'); $t | Add-Member -Force NoteProperty LootTable @()
  $t | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = 'OMUI'; Source = "6553 spawn $($d.Hash): $src" })
  $nt | Add-Member -Force NoteProperty $d.Hash ([pscustomobject]@{ Templates = @($t) })
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 12) + "`n", $u)
# the old outside plant
$cpath = Join-Path $gd 'PlayfieldContent\6553\Npcs.json'
$c = [IO.File]::ReadAllText($cpath) | ConvertFrom-Json
$before = @($c.Npcs).Count
$c.Npcs = @($c.Npcs | Where-Object { $_.Key -ne 'ddogs:arete:house-plant' })
if (@($c.Npcs).Count -ne $before) { [IO.File]::WriteAllText($cpath, ($c | ConvertTo-Json -Depth 20), $u) }
# Knubot
$kd = Join-Path $gd 'Knubot'
$hp = [IO.File]::ReadAllText((Join-Path $kd 'HousePlant.json')) | ConvertFrom-Json
$hp.Npcs = @('PRHO')
$hp.Notes = 'Prized House Plant (spawn PRHO) inside the house at the end of the road past the Surveillance Droid, where Bodyguard Logan Fixx stands (AO-Universe Arete #4). Retail: drop the RC-P on the plant; here it goes in the trade window. Patched by ddogs/tools/Build-AreteSurveillance.ps1.'
$hp.Lines.plant.Text = 'A prized house plant. The RC-P Audio Recording Device would fit between the leaves. Put it in the trade window.{opentrade}'
[IO.File]::WriteAllText((Join-Path $kd 'HousePlant.json'), ($hp | ConvertTo-Json -Depth 12) + "`n", $u)
$ap = Join-Path $kd 'AlexGibbs.json'
$a = [IO.File]::ReadAllText($ap) | ConvertFrom-Json
$t = @($a.Lines.bac_done.Text | Where-Object { $_ -notmatch '^\{spawn Q(MON|RCP) 1\}$' })
$i = [array]::IndexOf($t, '{givequest AAX0}')
$a.Lines.bac_done.Text = @($t[0..($i - 1)]) + @('{spawn QMON 1}', '{spawn QRCP 1}') + @($t[$i..($t.Count - 1)])
$a.Lines.bac_done.Text[1] = 'This is the one. Here is a Blank Info Chip. Hold onto it. And take this Rebuilt HC-12 SecTec Monitor and this RC-P Audio Recording Device.'
$a.Replies | Add-Member -Force NoteProperty lost_mon ([pscustomobject]@{ Text = 'I lost the SecTec Monitor.'; If = @('HasAnyQuest AAX0,AAX1,AAX2', '!has QMON'); Goto = 'regive_mon' })
$a.Replies | Add-Member -Force NoteProperty lost_rcp ([pscustomobject]@{ Text = 'I lost the recording device.'; If = @('HasAnyQuest AAX0,AAX1', '!has QRCP'); Goto = 'regive_rcp' })
$a.Lines | Add-Member -Force NoteProperty regive_mon ([pscustomobject]@{ Text = @('{spawn QMON 1}', 'Here is another Rebuilt HC-12 SecTec Monitor. Try not to drop this one.'); Replies = @('bye') })
$a.Lines | Add-Member -Force NoteProperty regive_rcp ([pscustomobject]@{ Text = @('{spawn QRCP 1}', 'Another RC-P Audio Recording Device. The prized house plant is in the house at the end of the road, past the droid.'); Replies = @('bye') })
$a.Lines.busy_watch.Replies = @(@($a.Lines.busy_watch.Replies | Where-Object { $_ -notin 'lost_mon', 'lost_rcp' }) + @('lost_mon', 'lost_rcp'))
$last = $a.Lines.bac_done.Text.Count - 1
$a.Lines.bac_done.Text[$last] = '{delay 1}Next: the Surveillance Droid at 3563 by 823. Target it and use the monitor. Then plant the recorder on the prized house plant in the house at the end of that road, where Bodyguard Logan Fixx stands. Take the monitor to Immigration Officer Bill, then deal with Kneebreaker Alfonzo.'
[IO.File]::WriteAllText($ap, ($a | ConvertTo-Json -Depth 12) + "`n", $u)
'Build-AreteSurveillance: PRHO plant, SWOO Logan Fixx, outside plant removed, Alex gives QMON/QRCP'