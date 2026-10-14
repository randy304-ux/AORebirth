# ddogs/tools/Build-MissionBosses.ps1 - team mission bosses: one boss family per mission population family
# (DungeonGeneration.json Population.BossNpcHashes). Each leaf is a copy of the regular leaf, named "<name> Overseer",
# 3x health, loot rolled 3x with doubled chances. Server-side design (no retail capture of mission bosses): Guessed = true.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$GameData = [IO.Path]::GetFullPath($GameData)
$npath = Join-Path $GameData 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
$map = [ordered]@{ ANDR = 'BAND'; ROLL = 'BROL'; CLAW = 'BCLA'; TENT = 'BTEN'; SHAD = 'BSHA'; UNBR = 'BUNB' }
$n = 0
foreach ($fam in $map.Keys) {
  $kids = @()
  $i = 0
  foreach ($c in $nt.$fam.Children) {
    $src = $nt.$c; if (-not $src.Templates) { continue }
    $leaf = '{0}{1:X2}' -f $map[$fam].Substring(0, 2), $i++
    if ($nt.$leaf -and -not $nt.$leaf.Templates[0].ContentProvenance) { throw "hash collision $leaf" }
    $copy = $src | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    foreach ($b in $copy.Templates) {
      $b.Name = "$($b.Name) Overseer"
      foreach ($k in '1', '27') { if ($b.Stats.$k) { $b.Stats.$k = [int]$b.Stats.$k * 3 } }
      $b | Add-Member -Force NoteProperty LootTable @(@($b.LootTable) | Where-Object { $_ } | ForEach-Object { [pscustomobject]@{ Hash = $_.Hash; Repeats = [math]::Max(1, $_.Repeats) * 3; Chance = [math]::Min(100, $_.Chance * 2); LevelMod = $_.LevelMod } })
      $b | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Source = "team mission boss from $c"; Donor = $c })
    }
    $nt | Add-Member -Force NoteProperty $leaf $copy; $kids += $leaf; $n++
  }
  $nt | Add-Member -Force NoteProperty $map[$fam] ([pscustomobject]@{ Children = $kids })
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 8) + "`n", $u)
"mission bosses families=$($map.Count) leaves=$n"
