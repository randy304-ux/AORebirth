# Build-Appearance.ps1 - worn looks for humanoid NPC templates (2026-10-16). Chained last from Build-NpcTemplates.ps1 and
# Build-Mercs.ps1, so regenerating keeps these looks. Template "Equipment" items are worn by the NPC: their wear Texture/Mesh
# spells give the look (Character.RebaseWearAppearance), and a held prop with a WeaponMesh shows in the right hand.
#   Protesters (ROTE Arete Protester, CPPS/CSPE Clan Protester): Atrox (Randy, live screenshot 2026-10-16) in Dockworker Overalls
#     295698, holding "No Less Than Three Placard" 284185 (white sign, red prohibition circle over <3, as in the screenshot).
#   Cedric Harding (CEHA): Solitus male in Dockworker Overalls. Dockworkers (IESY) and Wounded Dockworkers (WODO): overalls.
# Atrox body 26097 / head 40111 is the only Atrox look in NpcTemplates (mobile, MonsterData features 32775).
# 2026-10-18 (Randy's live/PRK side-by-sides). Body meshes are from live captures (docs/generated/capture_backed_npc_combat_inventory.json,
# Arete realm 478032/6553): Omni-AF Private L10 26151 "nano male", Officer Milne L35 165186 "solitusmale_ah_merchant", Clan Protester L20
# 26090/26103/26139 (solitus female / atrox fat male / opifex male). OIUR is the Omni-AF Private: the OTAF Official Notice 297243 only works on
# Object & 1330206034 = 0x4F495552 "OIUR", and the four L10 OIUR spawns stand round Milne (3390,856). Privates wear the Omni-Armed Forces set
# 203445-203449/203530 + OT M500 SWAT Rifle (black Omni-AF armour, big gun; armour set by name, worn items not in the captures). Protesters lose
# the orange Dockworker Overalls (the reference wears light clothes; worn items not captured, so they keep their base body clothing) and keep the
# placard. Heads are ones our data already pairs with each body. Mario Carles (MARC) is scaled to 25 (Randy: a quarter of the size).
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$path = [IO.Path]::GetFullPath((Join-Path $GameData 'NpcTemplates.json'))
$nt = [IO.File]::ReadAllText($path) | ConvertFrom-Json
$overalls = 295698; $placard = 284185
function Pairs([int[]]$ids) { $l = New-Object System.Collections.ArrayList; foreach ($i in $ids) { [void]$l.Add([int[]]@($i, $i)) }; return ,([object[]]$l.ToArray()) }
$looks = @(
  @{ Hash = 'ROTE'; Md = 26097; Head = 40111; Breed = 4; Gender = 1; Items = @($overalls, $placard) },
  @{ Hash = 'CPPS'; Md = 26090; Head = 40695; Breed = 1; Gender = 3; Items = @($placard) },
  @{ Hash = 'CSPE'; Md = 26103; Head = 40103; Breed = 4; Gender = 1; Items = @($placard) },
  @{ Hash = 'OIUR'; Name = 'Omni-AF Private'; Md = 26151; Head = 223923; Breed = 3; Gender = 2; Scale = 100; Items = @(203447, 203449, 203530, 203445, 203446, 203448, 124350) },
  @{ Hash = 'OMCN'; Md = 165186; Head = 223803; Breed = 1; Gender = 2; Scale = 100; Items = @() },
  @{ Hash = 'MARC'; Scale = 25 },
  @{ Hash = 'CEHA'; Md = 26092; Head = 40694; Breed = 1; Gender = 2; Items = @($overalls) },
  @{ Hash = 'IESY'; Items = @($overalls) },
  @{ Hash = 'WODO'; Items = @($overalls) }
)
$done = @()
foreach ($l in $looks) {
  $entry = $nt.($l.Hash)
  if (-not $entry) { $done += "$($l.Hash) missing"; continue }
  foreach ($t in @($entry.Templates)) {
    if ($l.Md) { foreach ($kv in @(@('359', $l.Md), @('64', $l.Head), @('4', $l.Breed), @('59', $l.Gender))) { $t.Stats | Add-Member -Force NoteProperty $kv[0] $kv[1] } }
    if ($l.Scale) { $t.Stats | Add-Member -Force NoteProperty '360' $l.Scale }
    if ($l.Name) { $t.Name = $l.Name }
    if ($l.ContainsKey('Items')) { $t | Add-Member -NotePropertyName Equipment -NotePropertyValue (Pairs $l.Items) -Force }
  }
  $done += "$($l.Hash) x$(@($entry.Templates).Count)"
}
[IO.File]::WriteAllText($path, ($nt | ConvertTo-Json -Depth 12) + "`n", (New-Object Text.UTF8Encoding $false))
"Build-Appearance: $($done -join ', ')"