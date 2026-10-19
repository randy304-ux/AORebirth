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
# Arete guide #9 (Randy 2026-10-19, was hand-edited in cd34c8e2): after the chip Vernon sends you on. Non-shades get Talk to Doctor Mason
# (HDMA, compass waypoint); shades (prof 15) are told where Lady Sheila is (she has no handoff quest).
$v.Lines.done_chip.Text = @('{completequest AVGR}', 'Programmed. This Blank ICC ID Chip is yours. It still needs a body to ride in.')
$v.Lines.done_chip.Replies = @('where_mason', 'where_sheila')
$v.Replies | Add-Member -Force NoteProperty where_mason ([pscustomobject]@{ Text = 'Who can finish it?'; If = @('!prof 15'); Goto = 'to_mason' })
$v.Replies | Add-Member -Force NoteProperty where_sheila ([pscustomobject]@{ Text = 'Who can finish it?'; If = @('prof 15'); Goto = 'to_sheila' })
$v.Lines | Add-Member -Force NoteProperty to_mason ([pscustomobject]@{ Text = @('{givequest HDMA}', 'Doctor Mason, in the implant and pharmacy building across from here, southwest. He will put the chip in an implant for you.'); Replies = @('nothing') })
$v.Lines | Add-Member -Force NoteProperty to_sheila ([pscustomobject]@{ Text = @('A shade does not take implants. See Lady Sheila Black, about 3412 by 904. She works with spirits.'); Replies = @('nothing') })
if ($v.Notes -notmatch 'guide #9') { $v.Notes += ' Patched by Build-AreteVernon.ps1: hacked-library hand-in; guide #9 onward direction (HDMA for non-shades, Lady Sheila text for shades).' }
[IO.File]::WriteAllText($p, ($v | ConvertTo-Json -Depth 12) + "`n", $u)
'Build-AreteVernon: hacked-library hand-in, guide #9 direction'