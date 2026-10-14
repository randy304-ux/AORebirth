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

# 4. Condemned Subway (PF 127) leaves. Names and recorded stats (model, family, side, look) from the upstream
#    capture list docs/accepted/npc/delmus/NpcTemplate.json; level bands from the client PF127 Spawns.json;
#    health rescaled from the recorded level with the curve above. CARO/PCBN have no source data (ASSU is built in section 5) and are left out
#    (the engine then skips those spawn points).
$upstream = Get-Content "$PSScriptRoot\..\..\docs\accepted\npc\delmus\NpcTemplate.json" -Raw | ConvertFrom-Json
$pf127 = (Get-Content "$Playfields\127\Spawns.json" -Raw | ConvertFrom-Json).spawns
$subway = 'DRSH DCQV DIPE DOPT EHDL EMSE FASO FLFE INFE INPE IORB LPPE MDPA MEML MENI MUGG NUBR NXZP PMPE RESC SLRU SS01 STFE STFO UNPE VEAE VOVG WOST'.Split(' ')
foreach ($h in $subway) {
  $u = $upstream | Where-Object { $_.Hash -eq $h } | Select-Object -First 1
  # DRSH: name from Randy (Deranged Shopper); no upstream record, so it borrows the Looter (DCQV, same mall human family 138) look/stats.
  if ($h -eq 'DRSH') { $u = ($upstream | Where-Object { $_.Hash -eq 'DCQV' } | Select-Object -First 1 | ConvertTo-Json -Depth 6 | ConvertFrom-Json); $u.Name = 'Deranged Shopper' }
  if (-not $u) { throw "no upstream template for $h" }
  if ($catalog.Contains($h)) { throw "subway hash $h collides with an existing leaf" }
  $pts = $pf127 | Where-Object { $_.hashText -eq $h }
  $lo = ($pts | Measure-Object minLevel -Minimum).Minimum; $hi = ($pts | Measure-Object maxLevel -Maximum).Maximum
  $skel = [ordered]@{ Stats = [ordered]@{} }
  foreach ($p in $u.Stats.PSObject.Properties) { $skel.Stats[$p.Name] = [int]$p.Value }
  $recLvl = [int]$u.Stats.'54'; $recHp = [int]$u.Stats.'1'
  $bands = @()
  foreach ($lvl in (@([int]$lo, [int]$hi) | Select-Object -Unique)) {
    $hp = [int][math]::Round($recHp * (HealthAt $lvl) / (HealthAt $recLvl))
    $bands += Band $skel $u.Name $lvl ([ordered]@{ '1' = $hp })
  }
  $catalog[$h] = [ordered]@{ Templates = $bands }
  $report += "subway $h $($u.Name) L$lo-$hi recorded L$recLvl HP$recHp model $($u.Stats.'359') points $(@($pts).Count)"
}

# 5. Abmouth Supremus (ASSU). Level 30, HP 10324, melee 73-148 (AOWiki/Fandom; AO-Universe confirms the final-room boss).
#    No recorded model exists, so it borrows the Infector (INFE, family 150, its two guards in the final room) look at a larger scale.
$inf = $upstream | Where-Object { $_.Hash -eq 'INFE' } | Select-Object -First 1
$skel = [ordered]@{ Stats = [ordered]@{} }; foreach ($p in $inf.Stats.PSObject.Properties) { $skel.Stats[$p.Name] = [int]$p.Value }
$b = Band $skel 'Abmouth Supremus' 30 ([ordered]@{ '1' = 10324; '359' = 155962; '360' = 100 })   # client MonsterData 155962 is named 'abmouth' (catmesh 155548 abmouth.cir); native size, tune 360 if needed
$b.Stats['285'] = 148; $b.Stats['286'] = 73
# Randy's rule: 2 Infectors on first aggro and again under 75/50/25% health (engine BossTriggers, once per fight, reset on death/leash).
$adds = @([ordered]@{ Type = 'SpawnAdds'; Hash = 'INFE'; Count = 2; Radius = 4 })
$b['BossTriggers'] = @([ordered]@{ On = 'FirstAggro'; Pct = 0; Actions = $adds }) + @(75, 50, 25 | ForEach-Object { [ordered]@{ On = 'HealthBelow'; Pct = $_; Actions = $adds } })
$catalog['ASSU'] = [ordered]@{ Templates = @($b) }
$report += "subway ASSU Abmouth Supremus L30 HP10324 dmg73-148 model 155962 (client abmouth) scale100"

# 6. Subway loot (item hashes SW** in ItemTemplates.json, ids matched by name in itemnames).
#    Ring 100% on Abmouth is from the Fandom wiki; every other chance is a placeholder for Randy to tune (guides only say rare/uncommon).
$loot = @{
  ASSU = @(@('SWRN',100),@('SWBJ',25),@('SWJT',25),@('SWLS',10),@('SWLP',10),@('SWLH',10),@('SWLG',10))
  VEAE = @(@('SWRN',15),@('SWBJ',10),@('SWLP',20))
  MENI = @(@('SWRN',15),@('SWLS',20))
  STFO = @(@('SWBS',5),@('SWCS',20))
  WOST = @(@('SWSB',40),@('SWCS',5))   # Randy: Support Beam is a common Striker drop
  EHDL = @(,@('SWCS',10))
  VOVG = @(@('SWVC',10),@('SWFT',3))
  PMPE = @(,@('SWED',5))
  DIPE = @(,@('SWMM',3)); DOPT = @(,@('SWMM',3))
  MDPA = @(@('SWLS',3),@('SWLH',3),@('SWLP',3),@('SWLG',3))
}
foreach ($h in @('FASO','MEML','IORB','NUBR','EMSE')) { $loot[$h] = @(@('SWLH',2),@('SWLP',2),@('SWLG',2)) }
foreach ($h in $loot.Keys) {
  $rows = @($loot[$h] | ForEach-Object { [ordered]@{ Hash = $_[0]; Repeats = 1; Chance = $_[1]; LevelMod = $(if ($_[0] -eq 'SWSB') { 25 } else { 0 }) } })
  foreach ($band in $catalog[$h].Templates) { $band['LootTable'] = $rows }
  $report += "loot $h " + (($loot[$h] | ForEach-Object { "$($_[0])@$($_[1])%" }) -join ' ')
}

# 7. Shared common loot, reusable by any zone: CommonLoot <level> returns rows by level band.
#    Item families in ItemTemplates.json: CMTR (trash/vendor: Monster Parts, Blood Plasma, Ergo-ring parts),
#    CMHK (Health and Nano Stim 291043/291044 QL1-200, Health and Nano Recharger 291082/291083 QL1-100), OFAB (12 Illegally Modified Ofab).
#    QL comes from the engine's roll (mob level +/- LevelMod %), interpolated between each low/high id pair. Chances are tunable placeholders.
function CommonLoot([int]$lvl) {
  if ($lvl -lt 15) { $trash = 40; $heal = 8 } else { $trash = 50; $heal = 12 }
  @([ordered]@{ Hash = 'CMTR'; Repeats = 1; Chance = $trash; LevelMod = 20 },
    [ordered]@{ Hash = 'CMHK'; Repeats = 1; Chance = $heal; LevelMod = 20 },
    [ordered]@{ Hash = 'OFAB'; Repeats = 1; Chance = 1; LevelMod = 0 })
}
$extra = @{ STFE = @(@('HNST', 60, 2), @('HNRC', 35, 1)); WOST = @(,@('OFAB', 5, 1)) }
foreach ($h in @($subway) + 'ASSU') {
  foreach ($band in $catalog[$h].Templates) {
    $rows = @(); if ($band.Contains('LootTable')) { $rows += $band['LootTable'] }
    $rows += CommonLoot $band.Level
    if ($extra.ContainsKey($h)) { foreach ($e in $extra[$h]) { $rows += [ordered]@{ Hash = $e[0]; Repeats = $e[2]; Chance = $e[1]; LevelMod = 20 } } }
    $band['LootTable'] = $rows
    $band.Stats['61'] = [int]($band.Level * 10)   # credits on the corpse (CharacterStat.Cash); first opener takes them
  }
}
$report += "common loot attached to $(@($subway).Count + 1) subway codes; STFE extra stims/rechargers; WOST extra OFAB 5%"
# 8. Newbie zones: ICC Shuttleport (PF 4582) and Nascense Frontier (PF 4310, the Shadowlands starter zone).
#    Spawn codes come from the client Spawns.json of each playfield. Names and recorded stats come from the upstream
#    capture-backed spawn lists retired in commit ecefa673 (IccShuttleportSpawn.cs, NascenceLifeSpawn.cs), read with git show.
#    A code is matched to a captured NPC by position: the captured NPC nearest each spawn point (within 25 m) votes.
#    PF 4582 hostile codes use the old CellAO mobtemplate rows named in docs/evidence/PF4582_TEMPLATE_HASH_RESOLUTION_AUDIT_20260824.md.
$repo = [IO.Path]::GetFullPath("$PSScriptRoot\..\..")
function RetiredBlocks($file, $kind) {
  $txt = (& git -C $repo show "ecefa673^:AORebirth/Server/ZoneEngine/Core/Playfields/$file") -join "`n"
  $out = @()
  foreach ($m in [regex]::Matches($txt, "new $kind\s*\{(.*?)\n\s*\},", 'Singleline')) {
    $b = $m.Groups[1].Value; $o = [ordered]@{}
    $n = [regex]::Match($b, 'Name = "([^"]+)"'); if (-not $n.Success) { continue }; $o.Name = $n.Groups[1].Value
    foreach ($f in 'PlayfieldId','Level','Health','MonsterData','Scale','VisualFlags','CharacterFlags','HeadMesh','NpcFamily','Side','Breed','Gender','Race','RunSpeed') {
      $mm = [regex]::Match($b, "\b$f = (-?\d+)"); if ($mm.Success) { $o[$f] = [int]$mm.Groups[1].Value } }
    foreach ($f in 'X','Z') { $mm = [regex]::Match($b, "\b$f = (-?[\d\.]+)f"); if ($mm.Success) { $o[$f] = [double]$mm.Groups[1].Value } }
    if ($o.Contains('X')) { $out += [pscustomobject]$o }
  }
  $out
}
$shutCap = @(RetiredBlocks 'IccShuttleportSpawn.cs' 'ShuttleportNpc')
$lifeCap = @(RetiredBlocks 'NascenceLifeSpawn.cs' 'LifeNpc' | Where-Object { $_.PlayfieldId -eq 4310 })
$hostileFlags = 268964353; $socialFlags = 277352961
$newbieLeaves = New-Object System.Collections.Generic.List[string]

function NewbieLeaf($h, $cap, [int]$lo, [int]$hi, [bool]$social, $equip) {
  $r = $cap | Sort-Object Level | Select-Object -Last 1
  $skel = [ordered]@{ Stats = [ordered]@{} }
  foreach ($p in $catalog['CL02'].Templates[-1].Stats.GetEnumerator()) { $skel.Stats[$p.Key] = $p.Value }
  $o = [ordered]@{ '0' = $(if ($social) { $socialFlags } elseif ($r.CharacterFlags) { $r.CharacterFlags } else { $hostileFlags })
    '359' = $r.MonsterData; '360' = $(if ($r.Scale) { $r.Scale } else { 100 }); '673' = $(if ($r.VisualFlags) { $r.VisualFlags } else { 31 })
    '455' = $(if ($null -ne $r.NpcFamily) { $r.NpcFamily } else { 0 }); '64' = $(if ($r.HeadMesh) { $r.HeadMesh } else { 0 }) }
  foreach ($f in @(@('Side','33'),@('Breed','4'),@('Gender','59'),@('Race','89'),@('RunSpeed','156'))) { if ($null -ne $r.($f[0])) { $o[$f[1]] = $r.($f[0]) } }
  $bands = @()
  foreach ($lvl in (@($lo, $hi) | Select-Object -Unique)) {
    $hp = if ($r.Health -and $r.Level) { [int][math]::Round($r.Health * (HealthAt $lvl) / (HealthAt $r.Level)) } else { HealthAt $lvl }
    $o['1'] = [math]::Max(1, $hp)
    $b = Band $skel $r.Name $lvl $o
    if ($social) { $b['Features'] = @('NoCombat'); $b['HasHeadMesh'] = [bool]$r.HeadMesh; if ($equip) { $b['Equipment'] = @(,@($equip, $equip)) } }
    else { $b['LootTable'] = @(CommonLoot $lvl); $b.Stats['61'] = [int]($lvl * 10) }
    $bands += $b
  }
  if ($catalog.Contains($h)) { throw "newbie hash $h collides with an existing leaf" }
  $catalog[$h] = [ordered]@{ Templates = $bands }; $newbieLeaves.Add($h)
}

function MatchCodes($pf, $cap) {
  $pts = (Get-Content "$Playfields\$pf\Spawns.json" -Raw | ConvertFrom-Json).spawns
  $res = @()
  foreach ($g in ($pts | Group-Object hashText)) {
    $votes = @{}; $d = @()
    foreach ($p in $g.Group) {
      $best = $null; $bd = 1e9
      foreach ($n in $cap) { $dd = [math]::Sqrt([math]::Pow($n.X - $p.position[0], 2) + [math]::Pow($n.Z - $p.position[2], 2)); if ($dd -lt $bd) { $bd = $dd; $best = $n } }
      if ($best -and $bd -lt 25) { $votes[$best.Name] = 1 + $votes[$best.Name]; $d += $bd }
    }
    $top = $votes.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1
    $res += [pscustomobject]@{ Code = $g.Name; Points = $g.Count; Min = [int]($g.Group | Measure-Object minLevel -Minimum).Minimum
      Max = [int]($g.Group | Measure-Object maxLevel -Maximum).Maximum; Name = $top.Name; Votes = [int]$top.Value
      Dist = $(if ($d) { ($d | Measure-Object -Average).Average } else { 99 }) }
  }
  $res
}

# 8a. PF 4582 social NPCs and guards: exact capture matches (within 1 m). Island Reet (ISRE) is a level 1 critter.
$shutSocial = 'BNTO ICBI ADAF EQVE OITA CNTD CCRI ONRE NERE OMUI CLFI SHMG ICST'.Split(' ')
$shutShop = @{ EQVE = 248368; OITA = 248368; CNTD = 248368 }   # EQVE's recorded vendor terminal (docs/accepted/npc/delmus); stock in VendingMachines.json
foreach ($m in (MatchCodes 4582 $shutCap)) {
  if ($m.Code -eq 'ISRE') { NewbieLeaf 'ISRE' @($shutCap | Where-Object Name -eq 'Island Reet') 1 1 $false $null; $report += "newbie 4582 ISRE Island Reet x$($m.Points)"; continue }
  if ($shutSocial -notcontains $m.Code) { continue }
  if ($m.Dist -gt 3.5) { $report += "newbie 4582 $($m.Code) skipped (nearest capture $([math]::Round($m.Dist,1)) m)"; continue }
  $cap = @($shutCap | Where-Object Name -eq $m.Name)
  NewbieLeaf $m.Code $cap $m.Min $m.Max $true $shutShop[$m.Code]
  $report += "newbie 4582 $($m.Code) $($m.Name) L$($m.Min)-$($m.Max) social x$($m.Points)"
}
# 8b. PF 4582 hostile codes from mobtemplate. The 17 audit candidates; the 7 one-point named rares (B***) are assigned by level and kind (guesses).
$shutDb = [ordered]@{ ZIXI='A026'; ACFJ='A002'; TPSA='A033'; CIMA='A035'; GISK='A030'; WTCO='A029'; CADR='A027'; FDQO='A004'; SRLZ='A000'
  CRJU='A009'; RFSL='A034'; CBSN='A013'; SORL='A012'; LLER='A012'; SRSK='A003'; LPAK='A014'; CRDY='A016'
  BTMO='A037'; BDMO='A036'; BJMR='A038'; BEML='A010'; BDML='A015'; BLMM='A031'; BSMG='A032' }
$shutPts = (Get-Content "$Playfields\4582\Spawns.json" -Raw | ConvertFrom-Json).spawns
foreach ($h in $shutDb.Keys) {
  $r = (Q "select Name, MinLvl, MaxLvl, Side, Fatness, Breed, Sex, Race, Flags, NPCFamily, Health, MonsterData, MonsterScale from mobtemplate where Hash='$($shutDb[$h])'") -split "`t"
  $p = @($shutPts | Where-Object hashText -eq $h)
  $lo = [int]($p | Measure-Object minLevel -Minimum).Minimum; $hi = [int]($p | Measure-Object maxLevel -Maximum).Maximum
  $cap = [pscustomobject]@{ Name = $r[0]; Level = [int]$r[2]; Health = [int]$r[10]; MonsterData = [int]$r[11]; Scale = [int]$r[12]; NpcFamily = [int]$r[9]
    Side = [int]$r[3]; Breed = [int]$r[5]; Gender = [int]$r[6]; Race = [int]$r[7]; CharacterFlags = $hostileFlags }
  NewbieLeaf $h @($cap) $lo $hi $false $null
  $report += "newbie 4582 $h $($r[0]) (mobtemplate $($shutDb[$h])) L$lo-$hi x$($p.Count)"
}
# 8c. PF 4310 Nascense Frontier: every code whose spawn points agree on one captured NPC (>= 60% of points, mean distance <= 10 m,
#     or a single point within 3 m). Humans with a head mesh are the quest/social NPCs; everything else is hostile.
$lifeSocial = 'Dr. Rosenblatt|Emissary of Jobe|Guardian of the Weak|Joshua Falker|Libbie Hyman|Sabina Florenta|Scientist Donna Red|Scientist Veronica Escobar'
foreach ($m in (MatchCodes 4310 $lifeCap)) {
  $ok = ($m.Points -gt 1 -and $m.Votes / $m.Points -ge 0.6 -and $m.Dist -le 10) -or ($m.Points -eq 1 -and $m.Votes -eq 1 -and $m.Dist -le 3)
  if (-not $ok -or -not $m.Name) { $report += "newbie 4310 $($m.Code) unmatched x$($m.Points) L$($m.Min)-$($m.Max)"; continue }
  $social = $m.Name -match "^($lifeSocial)$"
  NewbieLeaf $m.Code @($lifeCap | Where-Object Name -eq $m.Name) $m.Min $m.Max $social $null
  $report += "newbie 4310 $($m.Code) $($m.Name) L$($m.Min)-$($m.Max) $(if ($social) { 'social' } else { 'hostile' }) x$($m.Points) votes $($m.Votes) dist $([math]::Round($m.Dist,1))"
}

# 8d. PF 4311 Nascense Wilds (Brawl) and PF 4312 Nascense Swamp: same position match against the retired NascenceLifeSpawn.cs
#     captures of those playfields (390 / 197 captured NPCs). Codes already built for 4310 are shared and skipped.
#     Unmatched codes are listed in the report; PF 4313 (Training Ground) has no capture and is filled in Build-GuessedContent.ps1.
$lifeAll = @(RetiredBlocks 'NascenceLifeSpawn.cs' 'LifeNpc')
foreach ($pfn in 4311, 4312) {
  $cap = @($lifeAll | Where-Object { $_.PlayfieldId -eq $pfn })
  foreach ($m in (MatchCodes $pfn $cap)) {
    if ($catalog.Contains($m.Code) -or $m.Code -eq 'PRYU') { continue }  # PRYU: Prophet Yutt Thrak is a garden content NPC
    $ok = ($m.Points -gt 1 -and $m.Votes / $m.Points -ge 0.5 -and $m.Dist -le 15) -or ($m.Points -eq 1 -and $m.Votes -eq 1 -and $m.Dist -le 5)
    if (-not $ok -or -not $m.Name) { $report += "nascence $pfn $($m.Code) unmatched x$($m.Points) L$($m.Min)-$($m.Max)"; continue }
    $c = @($cap | Where-Object Name -eq $m.Name)
    $social = [bool]($c | Where-Object { $_.HeadMesh } | Select-Object -First 1) -and $m.Points -le 2
    if ($m.Points -eq 1 -and $m.Name -match 'Thrak|Yuttos|Craig-Or of|^(Nesbaneb|Takheperu|Ahwere|Heru-Maat|Ehmat|Mesut-Ra)$') { $social = $true }  # garden villagers, vendors, statue keepers
    NewbieLeaf $m.Code $c $m.Min $m.Max $social $null
    $report += "nascence $pfn $($m.Code) $($m.Name) L$($m.Min)-$($m.Max) $(if ($social) { 'social' } else { 'hostile' }) x$($m.Points) votes $($m.Votes) dist $([math]::Round($m.Dist,1))"
  }
}
# 9. Mercenaries (hired companions; GameData/Knubot/Mercs*.json recruiters, GameData/PetTypes.json slots). Human looks from
#    the PF 4582 captures; levels 1-220 so a merc spawns at the hiring player's level. Profession stat 60 is set per type.
$mercs = [ordered]@{
  MRDR = @{ Name = 'Mercenary Doctor';   Look = 'Brandon Thorn';           Prof = 10; Hp = 0.8; Dmg = 0.6 }
  MREN = @{ Name = 'Mercenary Enforcer'; Look = 'ICC Shuttle Guard';       Prof = 9;  Hp = 1.6; Dmg = 0.9 }
  MRSO = @{ Name = 'Mercenary Soldier';  Look = 'ICC Shuttle Guard';       Prof = 1;  Hp = 1.0; Dmg = 1.3 }
  MRKE = @{ Name = 'Mercenary Keeper';   Look = 'Manager Travis Molen';    Prof = 14; Hp = 1.3; Dmg = 1.0 }
}
foreach ($h in $mercs.Keys) {
  $md = $mercs[$h]; $r = $shutCap | Where-Object Name -eq $md.Look | Select-Object -First 1
  $skel = [ordered]@{ Stats = [ordered]@{} }; foreach ($p in $catalog['CL02'].Templates[-1].Stats.GetEnumerator()) { $skel.Stats[$p.Key] = $p.Value }
  $bands = @()
  foreach ($lvl in 1, 5, 10, 15, 25, 50, 75, 100, 125, 150, 175, 200, 220) {
    $o = [ordered]@{ '0' = $hostileFlags; '359' = $r.MonsterData; '360' = 100; '673' = 31; '455' = 0; '64' = $r.HeadMesh; '60' = $md.Prof
      '33' = 0; '4' = $r.Breed; '59' = $r.Gender; '89' = $r.Race; '156' = 129; '1' = [int][math]::Max(30, (HealthAt $lvl) * $md.Hp) }
    $b = Band $skel $md.Name $lvl $o
    $b.Stats['285'] = [int][math]::Max(3, $lvl * 1.5 * $md.Dmg); $b.Stats['286'] = [int][math]::Max(1, $lvl * 0.5 * $md.Dmg)
    $b['HasHeadMesh'] = $true
    $bands += $b
  }
  $catalog[$h] = [ordered]@{ Templates = $bands }
  $report += "merc $h $($md.Name) look=$($md.Look) prof=$($md.Prof)"
}
$report += "newbie leaves: $($newbieLeaves.Count)"
$json = $catalog | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText([IO.Path]::GetFullPath($Out), $json + "`n", (New-Object Text.UTF8Encoding $false))
$report | Set-Content -Encoding UTF8 ([IO.Path]::Combine($PSScriptRoot, 'Build-NpcTemplates.last.txt'))
"wrote leaves=$(($catalog.Values | ? { $_.Templates }).Count) families=$(($catalog.Values | ? { $_.Children }).Count)"
# Arete quest mobs, quest-item drops and Arete quest givers live in Build-AreteContent.ps1; re-apply them on top.
& "$PSScriptRoot\Build-AreteContent.ps1" -GameData (Split-Path ([IO.Path]::GetFullPath($Out)))
# Temple of the Three Winds (PF 1931) mobs, bosses, loot and boss triggers: Build-TotwContent.ps1.
& "$PSScriptRoot\Build-TotwContent.ps1" -GameData (Split-Path ([IO.Path]::GetFullPath($Out)))
# Nascence garden key NPCs, items and insignia drops: Build-GardenContent.ps1 (then run Build-NewbieQuests.ps1).
& "$PSScriptRoot\Build-GardenContent.ps1" -GameData (Split-Path ([IO.Path]::GetFullPath($Out)))
# Best-guess fills (ContentProvenance.Guessed) for uncaptured spawn points: Build-GuessedContent.ps1.
& "$PSScriptRoot\Build-GuessedContent.ps1" -GameData (Split-Path ([IO.Path]::GetFullPath($Out)))
# Team mission boss families (DungeonGeneration.json Population.BossNpcHashes): Build-MissionBosses.ps1.
& "$PSScriptRoot\Build-MissionBosses.ps1" -GameData (Split-Path ([IO.Path]::GetFullPath($Out)))
