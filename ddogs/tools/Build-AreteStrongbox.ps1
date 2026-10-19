# Build-AreteStrongbox.ps1 - Lockpick the Merchant Strongbox (AST0), Arete Landing 6553. Randy 2026-10-19 12:38 ET.
# The fake hand-placed 'Merchant's Strongbox' robot NPC (ddogs:arete:strongbox, a trade hand-in that took the pick and never advanced AST0) is removed;
# the real client statel (Terminal:-1073014375 at 3407.9 x 877.6) is handled by GameData/StatelLocks.json (server: Missions/StatelLocks.cs).
# Stan Goodman gets a 'lost my lock pick' reply while AST0 runs.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = New-Object Text.UTF8Encoding $false
$gd = [IO.Path]::GetFullPath($GameData)
$cpath = Join-Path $gd 'PlayfieldContent\6553\Npcs.json'
$c = [IO.File]::ReadAllText($cpath) | ConvertFrom-Json
$before = @($c.Npcs).Count
$c.Npcs = @($c.Npcs | Where-Object { $_.Key -ne 'ddogs:arete:strongbox' })
if (@($c.Npcs).Count -ne $before) { [IO.File]::WriteAllText($cpath, ($c | ConvertTo-Json -Depth 20), $u) }
$sb = Join-Path $gd 'Knubot\Strongbox.json'
if (Test-Path $sb) { Remove-Item $sb }
$sp = Join-Path $gd 'Knubot\StanGoodman.json'
$s = [IO.File]::ReadAllText($sp) | ConvertFrom-Json
$s.Replies | Add-Member -Force NoteProperty lost_pick ([pscustomobject]@{ Text = 'I lost my Lock Pick.'; If = @('HasQuest AST0', '!has QLOK', '!has BRAN'); Goto = 'regive_pick' })
$s.Lines | Add-Member -Force NoteProperty regive_pick ([pscustomobject]@{ Text = @('{spawn QLOK 1}', 'Here is another Vacuum Packed Lock Pick. Use the lock pick on the strongbox itself, do not hand it to anyone.'); Replies = @('bye') })
$s.Lines.busy_factory.Replies = @(@($s.Lines.busy_factory.Replies | Where-Object { $_ -ne 'lost_pick' }) + @('lost_pick'))
[IO.File]::WriteAllText($sp, ($s | ConvertTo-Json -Depth 12) + "`n", $u)
'Build-AreteStrongbox: fake strongbox NPC removed, Stan lost-pick reply'