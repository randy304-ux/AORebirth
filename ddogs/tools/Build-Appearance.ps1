# Build-Appearance.ps1 - worn looks for humanoid NPC templates (2026-10-16). Chained last from Build-NpcTemplates.ps1 and
# Build-Mercs.ps1, so regenerating keeps these looks. Template "Equipment" items are worn by the NPC: their wear Texture/Mesh
# spells give the look (Character.RebaseWearAppearance), and a held prop with a WeaponMesh shows in the right hand.
#   Protesters (ROTE Arete Protester, CPPS/CSPE Clan Protester): Atrox (Randy, live screenshot 2026-10-16) in Dockworker Overalls
#     295698, holding "No Less Than Three Placard" 284185 (white sign, red prohibition circle over <3, as in the screenshot).
#   Cedric Harding (CEHA): Solitus male in Dockworker Overalls. Dockworkers (IESY) and Wounded Dockworkers (WODO): overalls.
# Atrox body 26097 / head 40111 is the only Atrox look in NpcTemplates (mobile, MonsterData features 32775).
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$path = [IO.Path]::GetFullPath((Join-Path $GameData 'NpcTemplates.json'))
$nt = [IO.File]::ReadAllText($path) | ConvertFrom-Json
$overalls = 295698; $placard = 284185
function Pairs([int[]]$ids) { $l = New-Object System.Collections.ArrayList; foreach ($i in $ids) { [void]$l.Add([int[]]@($i, $i)) }; return ,([object[]]$l.ToArray()) }
$looks = @(
  @{ Hash = 'ROTE'; Md = 26097; Head = 40111; Breed = 4; Gender = 1; Items = @($overalls, $placard) },
  @{ Hash = 'CPPS'; Md = 26097; Head = 40111; Breed = 4; Gender = 1; Items = @($overalls, $placard) },
  @{ Hash = 'CSPE'; Md = 26097; Head = 40111; Breed = 4; Gender = 1; Items = @($overalls, $placard) },
  @{ Hash = 'CEHA'; Md = 26092; Head = 40694; Breed = 1; Gender = 2; Items = @($overalls) },
  @{ Hash = 'IESY'; Items = @($overalls) },
  @{ Hash = 'WODO'; Items = @($overalls) }
)
$done = @()
foreach ($l in $looks) {
  $entry = $nt.($l.Hash)
  if (-not $entry) { $done += "$($l.Hash) missing"; continue }
  foreach ($t in @($entry.Templates)) {
    if ($l.Md) { $t.Stats.'359' = $l.Md; $t.Stats.'64' = $l.Head; $t.Stats.'4' = $l.Breed; $t.Stats.'59' = $l.Gender }
    $t | Add-Member -NotePropertyName Equipment -NotePropertyValue (Pairs $l.Items) -Force
  }
  $done += "$($l.Hash) x$(@($entry.Templates).Count)"
}
[IO.File]::WriteAllText($path, ($nt | ConvertTo-Json -Depth 12) + "`n", (New-Object Text.UTF8Encoding $false))
"Build-Appearance: $($done -join ', ')"