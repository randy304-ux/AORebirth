# ddogs/tools/Build-JobeSL.ps1 - 2026-10-30 Shadowlands start + starter meds (t651u). Rerunnable.
#  StartPackages: each profession's 'Startup Crystal - Quick Heal' added to the creation kit (only Adventurer 28742 exists in the item db).
#  Quests:        XDGK = "Standard issue gear" marker (Drake's once-per-character start kit; given and completed in one line).
#  Knubot:        BrandonThorn.json monster-part reward (line 17): the old-style first-aid / nano kits (BSNR, SFAK, SNKT)
#                 become the live Arete meds, Health and Nano Recharger x50 (HNRC) + Health and Nano Stim x25 (HNST);
#                 the Treatment Laboratory tool stays. DrakeRodriguez.json comes from Build-JobeSL.py.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false); $gd = [IO.Path]::GetFullPath($GameData); $tag = 'jobe-sl-20261030'
# text edit (keeps the file's own formatting): append the crystal as the profession's last item
$sp = Join-Path $gd 'StartPackages.json'; $st = [IO.File]::ReadAllText($sp, $u)
$heal = [ordered]@{ Adventurer = 28742 }   # by name in the item db; no other profession has a 'Startup Crystal - Quick Heal'
foreach ($prof in $heal.Keys) { $id = $heal[$prof]
  $pi = $st.IndexOf('"profession": "' + $prof + '"'); if ($pi -lt 0) { throw "no package $prof" }
  $ui = $st.IndexOf('"uploadedNanos"', $pi); $block = $st.Substring($pi, $ui - $pi)
  if ($block -match ('"lowId": ' + $id + ',')) { continue }
  $last = [regex]::Matches($block, '"placement": (\d+)') | ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Maximum
  $close = $st.LastIndexOf(']', $ui); $endObj = $st.LastIndexOf('}', $close)
  $nl = if ($st.Contains("`r`n")) { "`r`n" } else { "`n" }
  $item = ',' + $nl + '        {' + $nl + '          "placement": ' + ($last.Maximum + 1) + ',' + $nl + '          "lowId": ' + $id + ',' + $nl + '          "highId": ' + $id + ',' + $nl + '          "quality": 1,' + $nl + '          "count": 1' + $nl + '        }'
  $st = $st.Insert($endObj + 1, $item) }
[IO.File]::WriteAllText($sp, $st, $u)
$qp = Join-Path $gd 'Quests.json'; $q = [IO.File]::ReadAllText($qp, $u) | ConvertFrom-Json
$q = @($q | Where-Object Hash -ne 'XDGK')
if ($true) {
  $used = @($q | ForEach-Object { $_.QasInstance }); $inst = 920190; while ($used -contains $inst) { $inst++ }
  $x = [pscustomobject][ordered]@{ Hash = 'XDGK'; Name = 'Standard issue gear'; QasInstance = $inst; Scope = 'Solo'; Action = 'TargetNpc'
    Summary = 'Scientist Drake Rodriguez handed you the standard issue gear for your mandatory examination.'
    Objective = [pscustomobject][ordered]@{ Count = $null; Npc = 'SCDK'; SpecItem = $null; WorldItem = $null }
    Flags = 'NoCash'; MissionBit = 665; NextStep = $null
    Reward = [pscustomobject][ordered]@{ Hash = $null; Credits = 0; Xp = 0; Records = $null } }
  $q = @($q) + $x; [IO.File]::WriteAllText($qp, ($q | ConvertTo-Json -Depth 20), $u) }
$tp = Join-Path $gd 'Knubot\BrandonThorn.json'; $t = [IO.File]::ReadAllText($tp, $u)
$old = '"{spawn BSNR level}",' + "`r`n"; if (-not $t.Contains('{spawn HNRC 1 x50}')) {
  $t2 = [regex]::Replace($t, '"\{spawn BSNR level\}",(\s*)"\{spawn SFAK level\}",(\s*)"\{spawn SNKT level\}"', '"{spawn HNRC 1 x50}",$1"{spawn HNST 1 x25}"')
  if ($t2 -eq $t) { throw 'Thorn med lines not found' }; [IO.File]::WriteAllText($tp, $t2, $u) }
'heal crystals/XDGK/Thorn meds done'




