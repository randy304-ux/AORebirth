# ddogs/tools/Build-GardenContent.ps1 - Nascence garden key quests (Clan: Aban, Omni/neutral: Thrak): quest NPCs, items, insignia drops.
# Sources: AO-Universe "Aban Garden Key Quest", AOWiki "Thrak Garden Mission (Nascence Omni)"; NPC look/position from the
# retired captures (git ecefa673^: NascenceLifeSpawn.cs, ThrakOmniGardenSpawn.cs, Content/Thrak/garden-key); item ids from itemnames.
# Run before Build-NewbieQuests.ps1 (its trades and {spawn} tags use these item hashes). Re-runnable.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$GameData = [IO.Path]::GetFullPath($GameData)
$u = [Text.UTF8Encoding]::new($false)
$items = [ordered]@{ GADV = 214998; GINA = 214788; GINT = 214789; GAPG = 214784; GAPF = 214785; GKAB = 226824; GKTH = 226994 }
$ipath = Join-Path $GameData 'ItemTemplates.json'
$lines = [IO.File]::ReadAllLines($ipath, $u) | Where-Object { $_ -notmatch ('^\s*"(' + ($items.Keys -join '|') + ')":') }
$add = foreach ($k in $items.Keys) { '    "{0}": {{"Templates": [{1},{1}]}},' -f $k, $items[$k] }
[IO.File]::WriteAllLines($ipath, [string[]](@($lines[0]) + $add + $lines[1..($lines.Count - 1)]), $u)

# Insignias: Barking Chimeras (AO-Universe: "Insignias drop from creatures like the Barking Chimeras") and the other Frontier beasts.
$npath = Join-Path $GameData 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
$drops = @{ LLBE = @(@('GINA',20),@('GINT',20)); SLCM = @(@('GINA',30),@('GINT',30)); SWSI = @(@('GINA',8),@('GINT',8)) }
foreach ($h in $drops.Keys) {
  if (-not $nt.$h) { throw "no template $h" }
  foreach ($b in $nt.$h.Templates) {
    $keep = @($b.LootTable | Where-Object { $_ -and -not $items.Contains($_.Hash) })
    $b.LootTable = @($keep) + @($drops[$h] | ForEach-Object { [pscustomobject]@{ Hash = $_[0]; Repeats = 1; Chance = $_[1]; LevelMod = 0 } })
  }
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 8) + "`n", $u)

# Quest NPCs (content NPCs, not attackable). pf, instance, name, level, hp, MonsterData, scale, side, pos, rot, mesh
$npcs = @(
  @(4312, 0x7A1B033F, 'Ecclesiast Aban Fala', 40, 9280, 214078, 140, 1, @(1893.47546, 68.465004, 690.309143), @(0, 0.5387633, 0, 0.8424551), 234636),
  @(4676, 0x7A2013BC, 'Sipius Aban Lux-Wei', 40, 2320, 214067, 140, 1, @(468.5054, 116.985, 495.266968), @(0, -0.944331765, 0, 0.328994632), 234635),
  @(4311, 0x78D280F6, 'Prophet Yutt Thrak', 40, 9280, 208635, 150, 2, @(287.974182, 106.696884, 1005.84973), @(0, 0, 0, 1), 233207),
  @(4677, 0x79758F3A, 'Hypnagogic Urga-Lum Thrak', 40, 3078, 208640, 200, 2, @(464.0864, 33.37679, 359.307526), @(0, 0.9436898, 0, 0.330831528), 0)
)
foreach ($pfId in ($npcs | ForEach-Object { $_[0] } | Sort-Object -Unique)) {
  $dir = Join-Path $GameData "PlayfieldContent\$pfId"; [IO.Directory]::CreateDirectory($dir) | Out-Null
  $ppath = Join-Path $dir 'Npcs.json'
  $pf = $(if (Test-Path $ppath) { [IO.File]::ReadAllText($ppath, $u) | ConvertFrom-Json } else { [pscustomobject][ordered]@{ SchemaVersion = 1; PlayfieldId = $pfId; Npcs = @() } })
  $keep = @($pf.Npcs | ForEach-Object { $_ } | Where-Object { $_ -and $_.Key -notlike 'ddogs:garden:*' })
  $mineHere = [Collections.ArrayList]::new(); foreach ($x in $npcs) { if ($x[0] -eq $pfId) { [void]$mineHere.Add($x) } }
  $new = foreach ($n in $mineHere) {
    if ($keep | Where-Object { [int]$_.InstanceId -eq $n[1] }) { throw "instance clash $($n[2])" }
    $mesh = @(); if ($n[10]) { $mesh = @([ordered]@{ Position = 1; OverrideTextureId = 0; Id = $n[10]; Layer = 2 }) }
    [ordered]@{ Key = ('ddogs:garden:{0:X8}' -f $n[1]); Provenance = 'retired capture (ecefa673^ NascenceLifeSpawn.cs / ThrakOmniGardenSpawn.cs); ddogs/tools/Build-GardenContent.ps1'
      ContentNpcIdentity = ('SimpleChar:{0:X8}' -f $n[1]); PlayfieldId = $pfId; InstanceId = $n[1]; Name = $n[2]; Position = $n[8]; Rotation = $n[9]
      Stats = [ordered]@{ '0' = 271061505; '1' = $n[4]; '4' = 6; '27' = $n[4]; '33' = $n[7]; '47' = 1; '54' = $n[3]; '59' = 2; '60' = 0; '64' = 0; '89' = 1; '156' = 0
        '173' = 3; '174' = 3; '359' = $n[5]; '360' = $n[6]; '368' = 0; '389' = 0; '455' = 137; '466' = 0; '660' = 0; '673' = 31 }
      Textures = @(); Meshes = $mesh; Passive = $true; Attackable = $false; TickLiving = $true; Presentation = [ordered]@{} }
  }
  $pf.Npcs = @($keep) + @($new)
  [IO.File]::WriteAllText($ppath, ($pf | ConvertTo-Json -Depth 10), $u)
}
"garden items=$($items.Count) drops=$($drops.Count) npcs=$($npcs.Count)"