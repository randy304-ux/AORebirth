# ddogs/tools/Build-AreteLiveFixes.ps1 - live fixes that no other builder makes (synced from live 2026-10-16). Called last by Build-NpcTemplates.ps1.
# Fleas (incl. dynas D011/D017) on gunbeetle 40069 and Waste Collectors (WTCO) on junkbot 45873: the 17657/275710/17714 models render
#   see-through on the 18.8.50 client (Randy .set test). Crash-site aliens HX1V/HXHR keep NPCFamily 220. JUCO keeps the guide name.
#   YBSH carries the Shoulderpads drop SHSW at 8%.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
Remove-TypeData System.Array -ErrorAction SilentlyContinue
$u = [Text.UTF8Encoding]::new($false)
$npath = Join-Path ([IO.Path]::GetFullPath($GameData)) 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
function SetStat($t, $k, $v) { $t.Stats | Add-Member -Force NoteProperty $k $v }
foreach ($k in 'D011','D017') { foreach ($t in @($nt.$k.Templates)) { if ($t) { SetStat $t '359' 40069; SetStat $t '455' 0 } } }
foreach ($t in @($nt.WTCO.Templates)) { if ($t) { SetStat $t '359' 45873; SetStat $t '455' 0 } }
foreach ($k in 'HX1V','HXHR') { foreach ($t in @($nt.$k.Templates)) { if ($t) { SetStat $t '455' 220 } } }
foreach ($t in @($nt.JUCO.Templates)) { if ($t) { $t.Name = 'Cleanmeister Intelligence Robot' } }
foreach ($t in @($nt.YBSH.Templates)) { if ($t -and -not ($t.LootTable | Where-Object Hash -eq 'SHSW')) { $t.LootTable = @($t.LootTable) + [pscustomobject][ordered]@{ Hash = 'SHSW'; Repeats = 1; Chance = 8; LevelMod = 0 } } }
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 12) + "`n", $u)
Write-Output 'arete live fixes applied'