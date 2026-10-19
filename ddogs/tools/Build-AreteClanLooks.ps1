# Build-AreteClanLooks.ps1 - Arete Landing (6553) clan-side and ICC HQ NPCs from Randy's live PRK shots (2026-10-18 22:58 ET).
# Chained from Build-NpcTemplates.ps1 after Build-AreteAliens.ps1. Body records (stat 359) are the live captures
# (docs/generated/capture_backed_npc_combat_inventory.json, capture 20260613-172753, realm 478032); names from the client rdb (monsters.tsv).
#   CARP (L100, 3372.0, 884.1, no template before): Field Surgeon Elsa Oosta, capture 26080 'solitus thin female robe' (the 4582 CLFI look).
#   ODDD (L20, 3370.1, 883.9): was a second Omni-Trans Equipment Vendor (guessed, OITA already stands at the counter); live is the Wounded
#     Clan Protester on the hover stretcher beside Elsa, capture 296008 'wounded dock worker' (the WODO look, orange overalls as in the shot).
#   GMUN (L20, 3368.7, 934.4, no template before): Gamma Unit Sentinel beside Field Commander Ericson, capture 249667 'icc shuttle guard'.
#   FDCS Field Commander Ericson: capture 159084 'atrox thin male pilot' (was the solitus 26092 guess); Atrox, head 40111.
#   VAHE Vanguard Henri Zure: capture 40810 'nano male robe' (was 26092); Nanomage male, NPC's Vanguard Cloak 206166 (the V on the back),
#     NPC's Vanguard Helmet 292841 (the mask), rifle. Key unchanged, so the Milne escort (MIL1) still finds him.
# Guessed: the Sentinel Armor set 118180-118195 for the bone chest / red pants / horned helmet look on Ericson (no helmet) and the sentinel;
#   heads (taken from looks we already pair with each body); rifles (OT M500 SWAT Rifle 124350, the Omni-AF Privates' prop).
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$path = [IO.Path]::GetFullPath((Join-Path $GameData 'NpcTemplates.json'))
$nt = [IO.File]::ReadAllText($path) | ConvertFrom-Json
function Pairs([int[]]$ids) { $l = New-Object System.Collections.ArrayList; foreach ($i in $ids) { [void]$l.Add([int[]]@($i, $i)) }; return ,([object[]]$l.ToArray()) }
function Clone($o) { $o | ConvertTo-Json -Depth 12 | ConvertFrom-Json }
$rifle = 124350
$sentinel = @(118183, 118184, 118181, 118180)   # body, sleeves, pants, boots
$looks = @(
  @{ Hash = 'CARP'; Donor = 'CLFI'; Name = 'Field Surgeon Elsa Oosta'; Level = 100; Md = 26080; Head = 40637; Breed = 1; Gender = 3 },
  @{ Hash = 'ODDD'; Donor = 'WODO'; Name = 'Wounded Clan Protester'; Level = 20; Md = 296008; Head = 223803; Breed = 1; Gender = 2; Items = @(295698) },
  @{ Hash = 'GMUN'; Donor = 'FDCS'; Name = 'Gamma Unit Sentinel'; Level = 20; Md = 249667; Head = 40627; Breed = 1; Gender = 2; Items = @(118195) + $sentinel + @($rifle) },
  @{ Hash = 'FDCS'; Md = 159084; Head = 40111; Breed = 4; Gender = 1; Items = $sentinel },
  @{ Hash = 'VAHE'; Md = 40810; Head = 223923; Breed = 3; Gender = 2; Items = @(206166, 292841, $rifle) }
)
$done = @()
foreach ($l in $looks) {
  if ($l.Donor) {
    $t = Clone (@($nt.($l.Donor).Templates)[0])
    $t.Name = $l.Name; $t.Level = $l.Level; $t.Stats | Add-Member -Force NoteProperty '54' $l.Level
    $t | Add-Member -Force NoteProperty Features @('NoCombat', 'NoMove'); $t | Add-Member -Force NoteProperty LootTable @()
    $t | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = $l.Donor; Source = "Arete 6553 spawn $($l.Hash); name/level/body from live capture 20260613-172753, Randy PRK shots 2026-10-18 (Build-AreteClanLooks.ps1)" })
    $nt | Add-Member -Force NoteProperty $l.Hash ([pscustomobject]@{ Templates = @($t) })
  }
  foreach ($t in @($nt.($l.Hash).Templates)) {
    foreach ($kv in @(@('359', $l.Md), @('64', $l.Head), @('4', $l.Breed), @('59', $l.Gender))) { $t.Stats | Add-Member -Force NoteProperty $kv[0] $kv[1] }
    $t | Add-Member -Force NoteProperty HasHeadMesh $true
    if ($l.ContainsKey('Items')) { $t | Add-Member -NotePropertyName Equipment -NotePropertyValue (Pairs $l.Items) -Force }
  }
  $done += $l.Hash
}
[IO.File]::WriteAllText($path, ($nt | ConvertTo-Json -Depth 12) + "`n", (New-Object Text.UTF8Encoding $false))
"Build-AreteClanLooks: $($done -join ', ')"