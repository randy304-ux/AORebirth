# Build-AreteVernon.ps1 - Vernon Godfray AVG0 (AO-Universe Arete #8): hack the library in the Tradeskill window (Hacker Tool source,
# Omni-Tek Technical Library target, Tradeskills.arete.json), then hand Vernon the Hacked Technical Library; he hands it back.
# The old both-items trade stays. Randy 2026-10-19 15:12. Chained from Build-NpcTemplates.ps1.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = New-Object Text.UTF8Encoding $false
$p = Join-Path ([IO.Path]::GetFullPath($GameData)) 'Knubot\VernonGodfray.json'
$v = [IO.File]::ReadAllText($p) | ConvertFrom-Json
$v.Lines.lib.Text = 'Hack the library first: Tradeskill window, Hacker Tool as source, the Omni-Tek Technical Library as target. Then give me the Hacked Technical Library.{opentrade}'
$v.Lines | Add-Member -Force NoteProperty lib_hacked ([pscustomobject]@{ Text = @('{completequest AVG0}', '{spawn QHKL 1}', 'Hacked. Keep it, and the tool. Now the Shipping Manifest Terminal, same building as the Kneebreaker. Use the Hacker Tool on it and bring me the chip it releases.'); Fail = 'full'; Replies = @('nothing') })
$v.Trades | Add-Member -Force NoteProperty lib_hacked ([pscustomobject]@{ If = @('HasQuest AVG0'); Items = @([pscustomobject]@{ Hash = 'QHKL'; Count = 1 }); Goto = 'lib_hacked' })
[IO.File]::WriteAllText($p, ($v | ConvertTo-Json -Depth 12) + "`n", $u)
'Build-AreteVernon: hacked-library hand-in'