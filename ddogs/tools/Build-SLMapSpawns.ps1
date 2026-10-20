# ddogs/tools/Build-SLMapSpawns.ps1 - Shadowlands NPCs read off the SLmap v2.22 layers (ddogs/docs/map_npc_points.csv),
# each label checked by eye on the map crop. Input ddogs/tools/slmap_spawns.csv.
#   mode rename: an existing pink placeholder hash gets the name and a body cloned from the donor template.
#   mode new: a new hash (MPnn) gets a template and one Spawns.json point at x/north; height from the nearest spawn.
# Also moves the Cranky Golem camp (R019) to its whereis SW spot 849 x 1177 (Build-SLDynas had 1149 x 870).
# Rerunnable: templates it made carry ContentProvenance.Source 'slmap'; points are added only once. Real data wins.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData); $npath = Join-Path $gd 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
$rows = Import-Csv (Join-Path $PSScriptRoot 'slmap_spawns.csv')
$made = 0; $added = 0; $log = New-Object System.Collections.ArrayList
foreach ($r in $rows) {
  $c = $r.key
  if ($nt.$c -and "$($nt.$c.ContentProvenance.Source)" -notlike 'slmap*') { [void]$log.Add("SKIP $c already has a real template"); continue }
  $d0 = $nt.($r.donor); if (-not $d0) { throw "donor $($r.donor) missing for $c" }
  $lo = [int]$r.minLevel; $hi = [int]$r.maxLevel
  $d = $d0.Templates | sort { [math]::Abs([int]$_.Level - $lo) } | select -First 1; $dl = [math]::Max(1, [int]$d.Level)
  $bands = foreach ($L in (@($lo, $hi) | sort -Unique)) {
    $b = $d | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $b.Name = $r.name; $b.Level = $L; $b.Stats.'54' = $L
    $hp = [int][math]::Max(30, [double]$d.Stats.'1' * $L / $dl); $b.Stats.'1' = $hp; $b.Stats | Add-Member -Force NoteProperty '27' $hp
    if ($r.social -eq '1') { $b | Add-Member -Force NoteProperty Features @(@($b.Features | ? { $_ -and $_ -ne 'Wander' }) + 'NoCombat' | sort -Unique) }
    $b
  }
  $nt | Add-Member -Force NoteProperty $c ([pscustomobject]@{ Templates = @($bands); ContentProvenance = [pscustomobject]@{ Guessed = $true; Donor = $r.donor; Source = "slmap v2.22 ($($r.verified)); $($r.note)" } })
  $made++
  if ($r.mode -ne 'new') { [void]$log.Add("RENAME $c pf $($r.pf) $($r.name) L$lo donor $($r.donor)"); continue }
  $spath = Join-Path $gd "Playfields\$($r.pf)\Spawns.json"; $sraw = [IO.File]::ReadAllText($spath, $u)
  if ($sraw -match ('"hashText":\s*"' + $c + '"')) { [void]$log.Add("NEW $c already in Spawns.json"); continue }
  $sp = ($sraw | ConvertFrom-Json).spawns; $x = [double]$r.x; $z = [double]$r.north
  $best = $sp | sort { ([double]$_.position[0]-$x)*([double]$_.position[0]-$x) + ([double]$_.position[2]-$z)*([double]$_.position[2]-$z) } | select -First 1
  $y = '{0:0.###}' -f [double]$best.position[1]
  $piece = @"
    {
      "districtIndex": 0, "hash": 0, "hashText": "$c", "manifestHash": 0, "minLevel": $lo, "maxLevel": $hi,
      "respawnChance": 100, "flags": 1, "respawnTime": 600, "version7Extra": 0, "hasOptionalFlagBlock": true,
      "nativeFlags": 0, "moreFlags": 0, "assistanceRadius": 0, "unknownOptionalU8": 0, "angle": 0, "angleW": 1,
      "position": [ $x, $y, $z ], "radius": 1, "additionalPoints": [], "extensions": null
    }
"@
  $cut = $sraw.LastIndexOf(']'); $brace = $sraw.LastIndexOf('}', $cut)
  $sraw = $sraw.Substring(0, $brace + 1) + ",`n" + $piece + $sraw.Substring($brace + 1)
  [IO.File]::WriteAllText($spath, $sraw, $u); $added++
  [void]$log.Add("NEW $c pf $($r.pf) $($r.name) L$lo at $x,$y,$z donor $($r.donor)")
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 20), $u)
# Cranky Golem camp R019: whereis 'Adonis, 2402 x 638 SE and 849 x 1177 SW'. Move every point within 30 m of the old spot.
$spath = Join-Path $gd 'Playfields\4872\Spawns.json'; $j = [IO.File]::ReadAllText($spath, $u) | ConvertFrom-Json
$ox = 1149; $oz = 870; $nx = 849; $nz = 1177; $moved = 0
$ground = $j.spawns | ? { $_.hashText -notmatch '^R0' } | sort { ([double]$_.position[0]-$nx)*([double]$_.position[0]-$nx) + ([double]$_.position[2]-$nz)*([double]$_.position[2]-$nz) } | select -First 1
foreach ($s in $j.spawns) {
  if ([math]::Abs([double]$s.position[0] - $ox) -le 30 -and [math]::Abs([double]$s.position[2] - $oz) -le 30 -and $s.manifestHash -eq 0) {
    $s.position = @(([double]$s.position[0] - $ox + $nx), [double]$ground.position[1], ([double]$s.position[2] - $oz + $nz)); $moved++
  }
}
# Build-SLDynas appends at the new spot on a full rebuild; drop same-hash twins within 1 m.
$keep = @(); $twins = 0
foreach ($s in $j.spawns) { if ($s.manifestHash -eq 0 -and ($keep | ? { $_.hashText -eq $s.hashText -and [math]::Abs([double]$_.position[0]-[double]$s.position[0]) -lt 1 -and [math]::Abs([double]$_.position[2]-[double]$s.position[2]) -lt 1 })) { $twins++; continue }; $keep += $s }
if ($twins) { $j.spawns = $keep; $moved += $twins }
if ($moved) { [IO.File]::WriteAllText($spath, ($j | ConvertTo-Json -Depth 20), $u) }
[void]$log.Add("Cranky Golem camp R019 points moved: $moved (to $nx,$nz, y from $($ground.hashText))")
$log | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'Build-SLMapSpawns.last.txt')
"Build-SLMapSpawns: templates=$made new points=$added golem points moved=$moved"

