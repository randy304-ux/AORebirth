# Dungeon Dogs: Officer Milne "Assist Omni-Tek" 3 steps from the live scrape (2026-10-30 17:48-17:51 ET). Run once.
# MIL0 (1/3) Command an Omni-AF Private -> MIL1 (2/3) Arrest Henri Zure -> MIL2 (3/3) Return to Omni-AF Officer Milne.
# Credits/XP kept from ours (scraper does not log them). Knubot\OfficerMilne.json is written from OfficerMilne.template.json beside this script.
$ErrorActionPreference = 'Stop'
$gd = Join-Path $PSScriptRoot '..\..\AORebirth\GameData' | Resolve-Path
$u = [Text.UTF8Encoding]::new($false)
$p = Join-Path $gd 'Quests.json'; $s = [IO.File]::ReadAllText($p); $nl = if ($s.Contains("`r`n")) { "`r`n" } else { "`n" }
if ($s -match '"Hash":\s+"MIL0"') { throw 'MIL0 already present' }
function Set-Field([string]$s, [string]$hash, [string]$name, [string]$value) {
    $i = $s.IndexOf('"Hash":  "' + $hash + '"'); if ($i -lt 0) { throw $hash }
    $m = [regex]::new('"' + $name + '":\s+"(?:[^"\\]|\\.)*"').Match($s, $i)
    if (-not $m.Success -or $m.Index - $i -gt 1500) { throw "$hash.$name" }
    $s.Remove($m.Index, $m.Length).Insert($m.Index, '"' + $name + '":  ' + (ConvertTo-Json $value))
}
$s = Set-Field $s 'MIL1' 'Name' 'Assist Omni-Tek (2/3): Arrest Henri Zure'
$s = Set-Field $s 'MIL1' 'Summary' "Hurry up and bring your Omni-AF Private to Henri Zure's location."
$s = Set-Field $s 'MIL2' 'Name' 'Assist Omni-Tek (3/3): Return to Omni-AF Officer Milne'
$s = Set-Field $s 'MIL2' 'Summary' 'You sucessfully arrested Henri Zure. Return to Omni-AF Officer Milne to claim your reward.'
# Authored FindItem never auto-completes: MIL1 completes on escort arrival, MIL2 at Milne's 'I did what you asked' turn-in (TargetNpc would complete on targeting).
$s = Set-Field $s 'MIL1' 'Action' 'FindItem'
$s = Set-Field $s 'MIL2' 'Action' 'FindItem'
$i = $s.IndexOf('"Hash":  "RMI2"'); $a = $s.IndexOf('"Records":', $i)
$e = [regex]::new('\]\s*\]').Match($s, $a); if ($a -lt 0 -or -not $e.Success -or $e.Index - $a -gt 3000) { throw 'RMI2' }
$rec = '"Records":  [ [ { "Id":  81753, "HighId":  81753, "Name":  "Low Level Omni-Tek Application Form", "Quality":  15 }, { "Id":  96349, "HighId":  96349, "Name":  "Omni-Tek Mission Token", "Quality":  1 }, { "Id":  301068, "HighId":  301068, "Name":  "Omni-Tek Shoulderpad", "Quality":  3 } ] ]'
$s = $s.Remove($a, $e.Index + $e.Length - $a).Insert($a, $rec)
$mil0 = (@(
'    {',
'        "Hash":  "MIL0",',
'        "Name":  "Assist Omni-Tek (1/3): Command an Omni-AF Private",',
'        "QasInstance":  944004,',
'        "Scope":  "Solo",',
'        "Action":  "FindItem",',
'        "Summary":  "Target an Omni-AF Private and use the OTAF Official Notice by right clicking this item in your inventory.",',
'        "Objective":  { "Count":  null, "Npc":  "OIUR", "SpecItem":  "OTNT", "WorldItem":  null },',
'        "Flags":  "NoCash",',
'        "MissionBit":  719,',
'        "NextStep":  "MIL1",',
'        "Reward":  { "Hash":  "RMI0", "Credits":  0, "Xp":  0, "Records":  null }',
'    },') -join $nl) + $nl
$i = $s.IndexOf('"Hash":  "MIL1"'); $o = $s.LastIndexOf('{', $i); $ls = $s.LastIndexOf($nl, $o) + $nl.Length
$s = $s.Insert($ls, $mil0)
[IO.File]::WriteAllText($p, $s, $u); 'edited Quests.json'

$p = Join-Path $gd 'EscortQuests.json'; $s = [IO.File]::ReadAllText($p)
$s = $s.Replace('"Quest": "MIL1",', '"Quest": "MIL1",' + $nl + '        "RecruitQuest": "MIL0",'); if (-not $s.Contains('RecruitQuest')) { throw 'escort' }
[IO.File]::WriteAllText($p, $s, $u); 'edited EscortQuests.json'
Copy-Item (Join-Path $PSScriptRoot 'OfficerMilne.template.json') (Join-Path $gd 'Knubot\OfficerMilne.json') -Force; 'wrote Knubot\OfficerMilne.json'
