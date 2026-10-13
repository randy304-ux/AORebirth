# Builds AORebirth/GameData/NpcTemplates.json from local, non-redistributed sources:
#   1. DB mobspawns_stats: full stat sets of captured spawns (pf 954). Used as-is.
#   2. DB mobtemplate: older catalog rows (identity, levels, health). Other stats copied from a captured sibling.
#   3. Client district spawn tables (GameData/Playfields/*/Districts.json, extracted from the local client RDB):
#      the real server hash names (e.g. CL02..CL14) and the level range of every district that spawns them.
#      Each family-prefixed hash becomes a leaf at the level band of its narrowest district range.
#      Health follows a level curve fitted to the DB's real (level, health) points; damage is a tunable formula.
#      Model (MonsterData) and NPCFamily per family come from captured spawns / DB rows / upstream evidence docs.
# Families with no model source are not generated. See ddogs/docs/CONTENT_SOURCES.md.
param(
  [string]$Out = "$PSScriptRoot\..\..\AORebirth\GameData\NpcTemplates.json",
  [string]$Playfields = "$PSScriptRoot\..\..\AORebirth\GameData\Playfields",
  [string]$Cnf = 'I:\AO\secrets\root-client.cnf',
  [string]$MySql = 'I:\AO\mysql\server\bin\mysql.exe'
)
$ErrorActionPreference = 'Stop'
function Q($sql) { & $MySql "--defaults-extra-file=$Cnf" -N -B cellao_codex_clean -e $sql }

$existing = Get-Content $Out -Raw | ConvertFrom-Json
$catalog = [ordered]@{ AAAA = $existing.AAAA }

function CapturedBands($name) {
  $ids = Q "select s.Id, l.Value from mobspawns s join mobspawns_stats l on l.Id=s.Id and l.Playfield=s.Playfield and l.Stat=54 where s.Name='$name' order by l.Value, s.Id"
  if (-not $ids) { throw "no captured spawn named $name" }
  $seen = @{}; $bands = @()
  foreach ($row in $ids) {
    $id, $lvl = $row -split "`t"
    if ($seen[$lvl]) { continue }; $seen[$lvl] = 1
    $stats = [ordered]@{}
    foreach ($s in (Q "select Stat, Value from mobspawns_stats where Id=$id order by Stat")) { $k, $v = $s -split "`t"; $stats[$k] = [int]$v }
    $bands += [ordered]@{ Name = $name; Level = [int]$lvl; Stats = $stats }
  }
  ,$bands
}

# Health by level: real DB points (captured spawns + mobtemplate ordinary mobs), log-log interpolated.
$curve = @(@(1,20),@(3,98),@(5,115),@(9,287),@(12,365),@(15,504),@(24,500),@(104,6236),@(139,10032),@(190,16798),@(214,20077))
function HealthAt([int]$lvl) {
  if ($lvl -le $curve[0][0]) { return $curve[0][1] }
  for ($i = 1; $i -lt $curve.Count; $i++) {
    $a = $curve[$i-1]; $b = $curve[$i]
    if ($lvl -le $b[0]) { $t = ([math]::Log($lvl) - [math]::Log($a[0])) / ([math]::Log($b[0]) - [math]::Log($a[0])); return [int][math]::Round([math]::Exp([math]::Log($a[1]) + $t * ([math]::Log($b[1]) - [math]::Log($a[1])))) }
  }
  $a = $curve[-2]; $b = $curve[-1]; $k = ([math]::Log($b[1]) - [math]::Log($a[1])) / ([math]::Log($b[0]) - [math]::Log($a[0]))
  [int][math]::Round($b[1] * [math]::Pow($lvl / $b[0], $k))
}

function Band($skeleton, $name, [int]$lvl, $over) {
  $stats = [ordered]@{}
  foreach ($p in $skeleton.Stats.GetEnumerator()) { $stats[$p.Key] = $p.Value }
  foreach ($p in $over.GetEnumerator()) { $stats[$p.Key] = $p.Value }
  $hp = if ($over.Contains('1')) { $over['1'] } else { HealthAt $lvl }
  $stats['54'] = $lvl; $stats['1'] = $hp; $stats['27'] = $hp
  $max = [math]::Max(3, [int][math]::Round($lvl * 1.5)); $min = [math]::Max(1, [int][math]::Round($lvl * 0.5))
  if ($lvl -gt 5) { $stats['285'] = $max; $stats['286'] = $min }
  if ($lvl -gt 30 -and $stats['360'] -lt 100) { $stats['360'] = 100 }
  [ordered]@{ Name = $name; Level = $lvl; Stats = $stats }
}

function DbBands($dbHash, $skeleton) {
  $r = (Q "select Name, MinLvl, MaxLvl, Side, Fatness, Breed, Sex, Race, Flags, NPCFamily, Health, MonsterData, MonsterScale from mobtemplate where Hash='$dbHash'") -split "`t"
  $o = [ordered]@{ '33' = [int]$r[3]; '47' = [int]$r[4]; '4' = [int]$r[5]; '59' = [int]$r[6]; '89' = [int]$r[7]; '0' = [int]$r[8]; '455' = [int]$r[9]; '1' = [int]$r[10]; '359' = [int]$r[11]; '360' = [int]$r[12] }
  $bands = @(); foreach ($lvl in (@([int]$r[1], [int]$r[2]) | Select-Object -Unique)) { $bands += Band $skeleton $r[0] $lvl $o }
  ,$bands
}

# 1. Captured leaves. Hashes from upstream evidence where known; others use non-district hashes to avoid collisions.
$captured = [ordered]@{ LE01 = 'Leet'; LE02 = 'Eleet'; AD01 = '34-I Helper'; RR02 = 'Contagious Rollerrat'; RR01 = 'Diseased Rollerrat'
  CL02 = 'Clawfinger Ruffian'; CL01 = 'Clawfinger Bully'; BFC1 = 'Biofreak Carcass'; BFC2 = 'Biofreak Cadaver'
  EM01 = 'Eyemutant Bully'; EM02 = 'Eyemutant Ruffian'; HW01 = 'Fresh Highwayman'; BTTM = 'Blackout the TechnoMaster' }
foreach ($h in $captured.Keys) { $catalog[$h] = [ordered]@{ Templates = (CapturedBands $captured[$h]) } }

# 2. DB mobtemplate leaves.
$derived = [ordered]@{ A027 = 'AD01'; A038 = 'AD01'; A036 = 'AD01'; A012 = 'RR02'; A106 = 'RR02'; A092 = 'RR02'; A114 = 'RR02'
  A093 = 'CL02'; A084 = 'CL02'; A088 = 'CL02'; A091 = 'CL02'; A083 = 'CL02'; A085 = 'CL02'; A095 = 'CL02' }
foreach ($h in $derived.Keys) { $catalog[$h] = [ordered]@{ Templates = (DbBands $h $catalog[$derived[$h]].Templates[-1]) } }

# 3. District-hash leaves per family.
$families = [ordered]@{
  CL = @{ Family = 'CLAW'; Md = 38387; Fam = 18; Skel = 'CL02'; Name = 'Clawfinger'; Extra = @('CL01','A093','A084','A088') }
  RR = @{ Family = 'ROLL'; Md = 17687; Fam = 55; Skel = 'RR02'; Name = 'Rollerrat'; Extra = @('A012','A106','A092','A114') }
  TM = @{ Family = 'TENT'; Md = 17695; Fam = 70; Skel = 'CL02'; Name = 'Tentaclemutant'; Extra = @('A091','A083') }
  SH = @{ Family = 'SHAD'; Md = 30464; Fam = 60; Skel = 'CL02'; Name = 'Shadowmutant'; Extra = @('A085','A095') }
  UB = @{ Family = 'UNBR'; Md = 44143; Fam = 73; Skel = 'CL02'; Name = 'Unfinished Breed'; Extra = @() }
  LE = @{ Family = 'LEET'; Md = 17655; Fam = 36; Skel = 'LE01'; Name = 'Leet'; Extra = @() }
  AD = @{ Family = 'ANDR'; Md = 17649; Fam = 4; Skel = 'AD01'; Name = 'Android'; Extra = @('A027','A038','A036') }
  EY = @{ Family = 'EYEM'; Md = 17653; Fam = 24; Skel = 'EM02'; Name = 'Eyemutant'; Extra = @('EM01','EM02') }
}
# Names from upstream evidence (docs/evidence/MISSION_DUNGEON_NPC_POPULATION_EVIDENCE.md).
$knownNames = @{ TM01 = 'Limp Tentaclemutant'; UB01 = 'Unfinished Breed Bully'; RR08 = 'Plague Rollerrat'; AD04 = '34-V Worker'
  CL08 = 'Claw-C22 Escapee'; TM08 = 'Tac-V85 Escapee'; SH08 = 'Shade-Y42 Escapee'; UB08 = 'Breed-B37 Escapee' }

$ranges = @{}
foreach ($f in Get-ChildItem $Playfields -Recurse -Filter Districts.json) {
  $o = Get-Content $f.FullName -Raw | ConvertFrom-Json
  foreach ($d in $o.districts) { foreach ($s in $d.spawnInfos) {
    if ($d.npcMaxLevel -ge 255 -or $d.npcMinLevel -lt 1) { continue }
    $k = $s.hashText; if (-not $ranges[$k]) { $ranges[$k] = @() }; $ranges[$k] += ,@([int]$d.npcMinLevel, [int]$d.npcMaxLevel)
  } }
}
$report = @()
foreach ($prefix in $families.Keys) {
  $fd = $families[$prefix]; $skel = $catalog[$fd.Skel].Templates[-1]; $children = New-Object System.Collections.Generic.List[string]
  foreach ($h in ($ranges.Keys | Where-Object { $_ -match "^$prefix\d\d$" } | Sort-Object)) {
    if ($catalog.Contains($h)) { $children.Add($h); continue }
    $best = $ranges[$h] | Sort-Object { $_[1] - $_[0] } | Select-Object -First 1
    if (($best[1] - $best[0]) -gt 40) { $report += "$h skipped (only broad ranges)"; continue }
    $name = if ($knownNames[$h]) { $knownNames[$h] } else { $fd.Name }
    $o = [ordered]@{ '359' = $fd.Md; '455' = $fd.Fam }
    $catalog[$h] = [ordered]@{ Templates = @((Band $skel $name $best[0] $o), (Band $skel $name $best[1] $o) | Sort-Object { $_.Level } -Unique) }
    if ($catalog[$h].Templates.Count -gt 1 -and $catalog[$h].Templates[0].Level -eq $catalog[$h].Templates[1].Level) { $catalog[$h].Templates = @($catalog[$h].Templates[0]) }
    $children.Add($h); $report += "$h $name L$($best[0])-$($best[1])"
  }
  foreach ($e in $fd.Extra) { if ($catalog.Contains($e) -and -not $children.Contains($e)) { $children.Add($e) } }
  foreach ($c in @($captured.Keys)) { if ($c.StartsWith($prefix) -and -not $children.Contains($c)) { $children.Add($c) } }
  $catalog[$fd.Family] = [ordered]@{ Children = @($children) }
}

$json = $catalog | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText([IO.Path]::GetFullPath($Out), $json + "`n", (New-Object Text.UTF8Encoding $false))
$report | Set-Content -Encoding UTF8 ([IO.Path]::Combine($PSScriptRoot, 'Build-NpcTemplates.last.txt'))
"wrote leaves=$(($catalog.Values | ? { $_.Templates }).Count) families=$(($catalog.Values | ? { $_.Children }).Count)"
