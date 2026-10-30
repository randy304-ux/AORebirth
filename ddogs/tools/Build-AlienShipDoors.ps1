# Dungeon Dogs: Crashed Alien Ship doors are real walk-through doors now (Core/Knubot/AlienShip.cs, live scrape 2026-10-30).
# Removes the guessed talk-to stand-ins: content NPC ddogs:alienship:entry (6553) and hides SHXT (8009 hatch). Run once.
$ErrorActionPreference = 'Stop'
$gd = Join-Path $PSScriptRoot '..\..\AORebirth\GameData' | Resolve-Path
$u = [Text.UTF8Encoding]::new($false)
$p = Join-Path $gd 'PlayfieldContent\6553\Npcs.json'; $j = [IO.File]::ReadAllText($p)
$i = $j.IndexOf('"Key":  "ddogs:alienship:entry"'); if ($i -lt 0) { throw 'entry npc not found' }
$st = $j.LastIndexOf('{', $i); $d = 0; $k = $st
do { if ($j[$k] -eq '{') { $d++ } elseif ($j[$k] -eq '}') { $d-- }; $k++ } while ($d -gt 0)
$prev = $j.LastIndexOf(',', $st); $between = $j.Substring($prev + 1, $st - $prev - 1)
if ($between.Trim().Length -eq 0) { $j = $j.Remove($prev, $k - $prev) }
else { $next = $j.IndexOf(',', $k); $j = $j.Remove($st, $next + 1 - $st) }
[IO.File]::WriteAllText($p, $j, $u); 'removed 6553 ddogs:alienship:entry'
$p = Join-Path $gd 'SpawnSkips.json'; $s = [IO.File]::ReadAllText($p); $nl = if ($s.Contains("`r`n")) { "`r`n" } else { "`n" }
$old = '    "hiddenHashes":  [' + $nl
if (-not $s.Contains($old)) { throw 'hiddenHashes' }
$add = $old + '                         {' + $nl + '                             "hash":  "SHXT",' + $nl + '                             "reason":  "Crashed Alien Ship exit talk-to stand-in (8009): the Exit door is a real walk-through door now (AlienShip.cs). 2026-10-30"' + $nl + '                         },' + $nl
[IO.File]::WriteAllText($p, $s.Replace($old, $add), $u); 'hid SHXT'