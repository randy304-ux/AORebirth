# Builds AORebirth/GameData/NpcTemplates.json from the local DB (cellao_codex_clean).
# Sources, in order of trust:
#   1. mobspawns_stats: full captured stat sets of real spawns (playfield 954). Used as-is per level.
#   2. mobtemplate: older catalog rows (name, level range, health, MonsterData, scale, family, breed...).
#      These only carry identity stats, so the remaining stats are copied from a captured sibling ("skeleton")
#      and the identity stats are overridden from the DB row. Marked "derived" below.
# No entries are invented for types with no DB source (UNBR, MFPB).
param(
  [string]$Out = "$PSScriptRoot\..\..\AORebirth\GameData\NpcTemplates.json",
  [string]$Cnf = 'I:\AO\secrets\root-client.cnf',
  [string]$MySql = 'I:\AO\mysql\server\bin\mysql.exe'
)
$ErrorActionPreference = 'Stop'
function Q($sql) { & $MySql "--defaults-extra-file=$Cnf" -N -B cellao_codex_clean -e $sql }

# Keep the existing placeholder leaf (engine fallback) verbatim.
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

function DerivedBands($dbHash, $skeleton) {
  $r = (Q "select Name, MinLvl, MaxLvl, Side, Fatness, Breed, Sex, Race, Flags, NPCFamily, Health, MonsterData, MonsterScale from mobtemplate where Hash='$dbHash'") -split "`t"
  if ($r.Count -lt 13) { throw "mobtemplate $dbHash missing" }
  $bands = @()
  foreach ($lvl in (@([int]$r[1], [int]$r[2]) | Select-Object -Unique)) {
    $stats = [ordered]@{}
    foreach ($p in $skeleton.Stats.GetEnumerator()) { $stats[$p.Key] = $p.Value }
    $stats['54'] = $lvl; $stats['33'] = [int]$r[3]; $stats['47'] = [int]$r[4]; $stats['4'] = [int]$r[5]
    $stats['59'] = [int]$r[6]; $stats['89'] = [int]$r[7]; $stats['0'] = [int]$r[8]; $stats['455'] = [int]$r[9]
    $stats['1'] = [int]$r[10]; $stats['27'] = [int]$r[10]; $stats['359'] = [int]$r[11]; $stats['360'] = [int]$r[12]
    $bands += [ordered]@{ Name = $r[0]; Level = $lvl; Stats = $stats }
  }
  ,$bands
}

# Captured leaves (hash names follow docs/evidence/MISSION_DUNGEON_NPC_POPULATION_EVIDENCE.md where known).
$captured = [ordered]@{ LE01 = 'Leet'; AD01 = '34-I Helper'; RR02 = 'Contagious Rollerrat'; RR01 = 'Diseased Rollerrat'; CL02 = 'Clawfinger Ruffian'; CL01 = 'Clawfinger Bully'
  LE02 = 'Eleet'; BF01 = 'Biofreak Carcass'; BF02 = 'Biofreak Cadaver'; EM01 = 'Eyemutant Bully'; EM02 = 'Eyemutant Ruffian'; HW01 = 'Fresh Highwayman'; BTTM = 'Blackout the TechnoMaster' }
foreach ($h in $captured.Keys) { $catalog[$h] = [ordered]@{ Templates = (CapturedBands $captured[$h]) } }

# Derived leaves: DB mobtemplate hash -> captured skeleton leaf.
$derived = [ordered]@{
  A027 = 'AD01'; A038 = 'AD01'; A036 = 'AD01'                    # droids (MonsterData 17649 like 34-I Helper)
  A012 = 'RR02'; A106 = 'RR02'; A092 = 'RR02'; A114 = 'RR02'     # rollerrats
  A093 = 'CL02'; A084 = 'CL02'; A088 = 'CL02'                    # Claw-C22
  A091 = 'CL02'; A083 = 'CL02'                                   # Tac-V85 (tentacle mutants; no captured tentacle, clawfinger skeleton)
  A085 = 'CL02'; A095 = 'CL02'                                   # Shade-Y42 (shadow mutants; no captured shadow, clawfinger skeleton)
}
foreach ($h in $derived.Keys) { $catalog[$h] = [ordered]@{ Templates = (DerivedBands $h $catalog[$derived[$h]].Templates[-1]) } }

# Families used by GameData/DungeonGeneration.json.
$catalog['ANDR'] = [ordered]@{ Children = @('AD01', 'A027', 'A038', 'A036') }
$catalog['ROLL'] = [ordered]@{ Children = @('RR01', 'RR02', 'A012', 'A106', 'A092', 'A114') }
$catalog['CLAW'] = [ordered]@{ Children = @('CL01', 'CL02', 'A093', 'A084', 'A088') }
$catalog['TENT'] = [ordered]@{ Children = @('A091', 'A083') }
$catalog['SHAD'] = [ordered]@{ Children = @('A085', 'A095') }

$json = $catalog | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText((Resolve-Path $Out), $json + "`n", (New-Object Text.UTF8Encoding $false))
"wrote $Out leaves=$(($catalog.Values | ? { $_.Templates }).Count) families=$(($catalog.Values | ? { $_.Children }).Count)"
