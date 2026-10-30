# Dungeon Dogs: Arete 6553 wanderers from the live trails (F:\DungeonDogs\AO_NPC_Scraper\output\npc_moves_20261030.csv, RC-PC).
# Home = trail mean, Radius = 85th-percentile distance from it; pauses 3-15 s. Adds "Wander" to every level entry of each hash.
# Elsie Clancy, Donte Clendennen and Kirby Schatz have no NPC in our data yet, so they are not here. Run once.
$ErrorActionPreference = 'Stop'
$gd = Join-Path $PSScriptRoot '..\..\AORebirth\GameData' | Resolve-Path
$p = Join-Path $gd 'NpcTemplates.json'; $s = [IO.File]::ReadAllText($p)
$specs = [ordered]@{
    'LEMY' = '{ "Home": [ 3445.1, 9.01, 871.3 ], "Radius": 27, "MinPauseSeconds": 3, "MaxPauseSeconds": 15 }'  # Leonora Marty, 90 pts / 20 min
    'JNME' = '{ "Radius": 25, "MinPauseSeconds": 3, "MaxPauseSeconds": 15 }'                                 # Garbage Flea (trails 8-40 m)
    'VUFL' = '{ "Radius": 25, "MinPauseSeconds": 3, "MaxPauseSeconds": 15 }'
    'FCTO' = '{ "Radius": 25, "MinPauseSeconds": 3, "MaxPauseSeconds": 15 }'
    'ROTE' = '{ "Radius": 30, "MinPauseSeconds": 3, "MaxPauseSeconds": 15 }'                                 # Protester (trails 28-31 m)
}
foreach ($h in $specs.Keys) {
    $m = [regex]::Match($s, '(?m)^    "' + $h + '":\s')
    if (-not $m.Success) { throw "hash $h" }
    $next = [regex]::new('(?m)^    "\w+":\s').Match($s, $m.Index + 5)
    $end = if ($next.Success) { $next.Index } else { $s.Length }
    $block = $s.Substring($m.Index, $end - $m.Index)
    if ($block.Contains('"Wander":')) { throw "$h already has Wander" }
    $ind = [regex]::Match($block, '(?m)^( +)"Name":').Groups[1].Value   # level-entry indent (first Name); nested Names are deeper
    $nl = if ($block.Contains("`r`n")) { "`r`n" } else { "`n" }
    $n = [regex]::Replace($block, '(?m)^' + $ind + '("Name":\s+"[^"]*",)', { param($x) $ind + $x.Groups[1].Value + $nl + $ind + '"Wander":  ' + $specs[$h] + ',' })
    if ($n -eq $block) { throw "no Name in $h" }
    $s = $s.Substring(0, $m.Index) + $n + $s.Substring($end)
    "wander $h"
}
[IO.File]::WriteAllText($p, $s, [Text.UTF8Encoding]::new($false))
