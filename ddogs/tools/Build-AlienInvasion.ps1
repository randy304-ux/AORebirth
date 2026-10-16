# ddogs/tools/Build-AlienInvasion.ps1 - Alien Invasion content: alien mob set, alien loot, alien armor tradeskills,
# AXP dailies, Sector 10 missions. Writes its own overlay files so it never rewrites shared GameData:
#   NpcTemplates.Alien.json, ItemTemplates.Alien.json, Quests.Alien.json, Tradeskills.json,
#   Knubot/Alien*.json, PlayfieldContent/{655,4374,6052,6112}/Npcs.json (ddogs:alien:* keys only),
#   Playfields/6112/Spawns.json (cocoons; Playfields is gitignored, rerun after a data re-extract).
# Real (AO-Universe Alien Invasion guides): Rank - Class'Race naming; class -> Lead Viralbot (Ankari Arithmetic,
#   Xoch Enduring, Ilari Spiritual, Rimah Observant, Jaax Strong, Cha Supple); race looks (Sinuh frog, Uri centaur,
#   Heru snake, Nakthi floater, Khaz city-boss General); full alien armor process steps, skills and the 80% rule;
#   combined armor pairs; daily mission texts, counts and level ranges; Sector 10 Duty Sergeant missions.
# Item ids: items.dat names (itemnames.sql), QL1/QL300 pairs checked with tools-temp/aiprobe.
# Meshes: client RDB mesh names (tools-temp/rdbscan2/mesh.txt).
# Guessed (see ddogs/docs/PROGRESS.md): rank level bands, class fighting style, stats (scaled from the Mercenary
#   Soldier bands), drop chances, clump -> bio-material type mapping, native spawn hash -> mob mapping, handler and
#   vendor spots, AXP reward size, combined armor skill, dailies are repeatable (no 24 h lockout).
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
Remove-TypeData System.Array -ErrorAction SilentlyContinue
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData)
function ReadJson($p) { for ($i = 0; $i -lt 10; $i++) { try { return ([IO.File]::ReadAllText($p, $u) | ConvertFrom-Json) } catch { Start-Sleep -Seconds 3 } }; throw "cannot parse $p" }
function Save($p, $o, $depth = 10) { [IO.File]::WriteAllText($p, ((($o | ConvertTo-Json -Depth $depth) -replace "`r`n", "`n") -replace '\{\s*"value":\s*(\[[^\]]*\]),\s*"Count":\s*\d+\s*\}', '$1') + "`n", $u) }
function Clone($o) { $o | ConvertTo-Json -Depth 8 | ConvertFrom-Json }
$nt = ReadJson (Join-Path $gd 'NpcTemplates.json')
$taken = @{}; foreach ($p in $nt.PSObject.Properties) { $taken[$p.Name] = 1 }
$base = $nt.ACFJ.Templates[0]          # Scout - Jaax'Sinuh: alien stat shape, NPCFamily 220 (alien XP)
$merc = @($nt.MRSO.Templates | Sort-Object Level)   # level curve donor 1..220
function Curve($lvl, $stat) {
  $lo = $merc[0]; $hi = $merc[-1]
  foreach ($b in $merc) { if ($b.Level -le $lvl) { $lo = $b } }
  foreach ($b in ($merc | Sort-Object Level -Descending)) { if ($b.Level -ge $lvl) { $hi = $b } }
  $a = [double]$lo.Stats.$stat; $c = [double]$hi.Stats.$stat
  if ($lvl -gt $merc[-1].Level) { return [int]($c * $lvl / $merc[-1].Level) }
  if ($hi.Level -eq $lo.Level) { return [int]$a }
  return [int]($a + ($c - $a) * ($lvl - $lo.Level) / ($hi.Level - $lo.Level))
}
function Band($name, $lvl, $mesh, $scale, $hpMul = 1.0, $loot = @(), $features = $null, $dmgMul = 1.0) {
  $t = Clone $base
  $t.Name = $name; $t.Level = $lvl
  $t.Stats.'54' = $lvl; $t.Stats.'359' = $mesh; $t.Stats.'360' = $scale
  $hp = [int]([math]::Max(30, (Curve $lvl '1')) * $hpMul); $t.Stats.'1' = $hp; $t.Stats.'27' = $hp
  foreach ($k in '285','286','156') { $t.Stats.$k = [int]([math]::Max(1, (Curve $lvl $k)) * $dmgMul) }
  $t.Stats.'61' = 10 * $lvl
  $t.LootTable = @($loot)
  if ($features) { $t | Add-Member -Force NoteProperty Features @($features) }
  $t
}
$tpl = [ordered]@{}; $guess = New-Object System.Collections.Generic.List[string]
function AddLeaf($key, $bands, $why) {
  if ($taken[$key] -and -not $tpl.Contains($key)) { throw "template key $key already in NpcTemplates.json" }
  foreach ($b in $bands) { $b | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Source = $why }) }
  $tpl[$key] = [pscustomobject]@{ Templates = @($bands | Sort-Object Level) }
}

# ---------------- items (hash families for loot)
$it = [ordered]@{}
function Item($h, $lo, $hi) { $it[$h] = [pscustomobject]@{ Templates = @($lo, $hi) } }
$classes = @(
  @{ N='Ankari'; L='A'; Style='c';  Lead=@(247144,247145); Bot='Arithmetic' },
  @{ N='Xoch';   L='X'; Style='c';  Lead=@(247136,247137); Bot='Enduring' },
  @{ N='Ilari';  L='I'; Style='mr'; Lead=@(247146,247147); Bot='Spiritual' },
  @{ N='Rimah';  L='R'; Style='m';  Lead=@(247142,247143); Bot='Observant' },
  @{ N='Jaax';   L='J'; Style='m';  Lead=@(247138,247139); Bot='Strong' },
  @{ N='Cha';    L='C'; Style='r';  Lead=@(247140,247141); Bot='Supple' })
foreach ($c in $classes) { Item ('LV' + $c.L + 'B') $c.Lead[0] $c.Lead[1] }
# Solid Clump pairs (QL1/QL300). Mapping clump -> result is guessed (pairs in id order).
$clumps = @(@(247102,247103),@(247104,247105),@(247697,247698),@(247699,247700),@(247701,247702),@(247703,247704),@(247705,247706),@(247707,247708),@(247709,247710),@(247711,247712),@(247713,247714),@(247715,247716),@(247717,247718),@(247719,247720),@(247764,254804),@(288699,288700))
$ci = 0; $clumpKids = @(); foreach ($p in $clumps) { $ci++; $h = 'KC{0:D2}' -f $ci; Item $h $p[0] $p[1]; $clumpKids += $h }
# armor clumps (Pristine/Mutated paths) twice as likely as each weapon type
$it['KCLM'] = [pscustomobject]@{ Children = @('KC01','KC01','KC01','KC02','KC02','KC02','KC15','KC15') + $clumpKids }
Item 'KVBT' 247113 247114   # Kyr'Ozch Viralbots (boss drop)
Item 'KART' 247099 247099   # Kyr'Ozch Atomic Re-Structuralizing Tool QL100
Item 'AARM' 268508 268508   # Alien Armor Materials (Sector 10 common drop)

# ---------------- alien mob set
$races = @(
  @{ N='Sinuh';  L='S'; Mesh=@{ m=251782; r=251783; mr=251781; c=251784 }; Scale=100 },
  @{ N='Uri';    L='U'; Mesh=@{ m=251785; r=251788; mr=251786; c=251787 }; Scale=100 },
  @{ N='Heru';   L='H'; Mesh=@{ m=251774; r=251775; mr=251772; c=251773 }; Scale=110 },
  @{ N='Nakthi'; L='N'; Mesh=@{ m=251776; r=251778; mr=251779; c=251777 }; Scale=100 })
$ranks = @(@('Scout',10,60), @('Guard',40,100), @('Commando',80,140), @('Specialist',110,180), @('Major',160,240), @('Sergeant',220,300))
$rankLeaves = @{}
for ($ri = 0; $ri -lt $ranks.Count; $ri++) {
  $rk = $ranks[$ri]; $rankLeaves[$ri] = @()
  foreach ($c in $classes) { foreach ($r in $races) {
    $key = 'A' + $c.L + $r.L + ($ri + 1)
    $name = "$($rk[0]) - $($c.N)'$($r.N)"
    $loot = @(
      [pscustomobject]@{ Hash='KCLM'; Repeats=1; Chance=25; LevelMod=0 },
      [pscustomobject]@{ Hash=('LV' + $c.L + 'B'); Repeats=1; Chance=3; LevelMod=0 },
      [pscustomobject]@{ Hash='KART'; Repeats=1; Chance=1; LevelMod=0 })
    if ($ri -ge 4) { $loot += [pscustomobject]@{ Hash='KVBT'; Repeats=1; Chance=2; LevelMod=0 } }
    $mid = [int](($rk[1] + $rk[2]) / 2)
    $bands = @(); foreach ($l in @($rk[1], $mid, $rk[2])) { $bands += Band $name $l $r.Mesh[$c.Style] $r.Scale 1.0 $loot }
    AddLeaf $key $bands "Hierarchy name (AO-Universe); rank level band $($rk[1])-$($rk[2]), style $($c.Style), stats and drops guessed"
    $rankLeaves[$ri] += $key
  } }
}
# Generals: city-boss rank, always 'Khaz (AO-Universe). Lead Viralbots are General drops in the real game.
$khaz = @{ m=247856; r=247857; mr=247860; c=247858 }
$generals = @()
foreach ($c in $classes) {
  $key = 'GK' + $c.L + 'Z'; $name = "General - $($c.N)'Khaz"
  $loot = @(
    [pscustomobject]@{ Hash=('LV' + $c.L + 'B'); Repeats=2; Chance=60; LevelMod=0 },
    [pscustomobject]@{ Hash='KVBT'; Repeats=1; Chance=50; LevelMod=0 },
    [pscustomobject]@{ Hash='KCLM'; Repeats=2; Chance=60; LevelMod=0 },
    [pscustomobject]@{ Hash='KART'; Repeats=1; Chance=10; LevelMod=0 })
  $bands = @(); foreach ($l in 100,175,250) { $bands += Band $name $l $khaz[$c.Style] 80 8.0 $loot }
  AddLeaf $key $bands 'General - Class''Khaz (AO-Universe hierarchy); levels, 8x HP, drop chances guessed'
  $generals += $key
}

# ---------------- named / native-hash mobs (hash mapping guessed)
$small = @([pscustomobject]@{ Hash='KCLM'; Repeats=1; Chance=15; LevelMod=0 })
AddLeaf 'AALV' @((Band 'Alien Larva' 20 247740 55 0.6 $small), (Band 'Alien Larva' 40 247740 65 0.6 $small)) 'We Come in Peace larvae L20-38 at the Andromeda crash site (AO-Universe); native hash AALV L20-40 at the crash north of ICC; mini-bug mesh scaled down'
AddLeaf 'VEZI' @((Band 'Alien Spider - Zix' 100 247728 119 0.15 @() $null 0.05)) 'Bugs daily: Zixes on Serenity Islands that aggro but do almost no damage (AO-Universe); native hash VEZI x251 L100 in 6052 "Serenity Island Infested by Zix"; low HP so 100 kills are quick'
AddLeaf 'KYBR' @((Band 'Kyr''Ozch Guardian' 125 248110 70 4.0 $small)) 'Cocoon daily: a Kyr''Ozch Guardian guards the main cocoon room (AO-Universe); native single L125 in Alien Scoutship 6112'
$cocoonDeath = @([pscustomobject]@{ On='Death'; Pct=0; Actions=@([pscustomobject]@{ Type='SpawnAdds'; Hash='AALV'; Count=1; Radius=2; Level=30; DespawnSeconds=120 }) })
$coc = Band 'Alien Cocoon' 60 257283 100 0.5 @() @('NoMove') 0.01
$coc | Add-Member -Force NoteProperty BossTriggers $cocoonDeath
AddLeaf 'AICO' @($coc) 'Terminate the Alien Cocoons: 10 cocoons, one larva each (AO-Universe); killed instead of injected with the Cocoon Terminator'
$big = Band 'Alien Cocoon' 95 257283 220 6.0 @([pscustomobject]@{ Hash='KCLM'; Repeats=2; Chance=80; LevelMod=0 }) @('NoMove') 0.01
$big | Add-Member -Force NoteProperty BossTriggers @([pscustomobject]@{ On='HealthBelow'; Pct=60; Actions=@([pscustomobject]@{ Type='SpawnAdds'; Hash='AALV'; Count=3; Radius=4; Level=40 }) })
AddLeaf 'AIMC' @($big) '...And you will leave in pieces: kill the main cocoon in the crashed ship (AO-Universe); placed in Alien Scoutship 6112'
$pod = Band 'Alien Pod' 200 55647 100 6.0 @([pscustomobject]@{ Hash='KCLM'; Repeats=2; Chance=80; LevelMod=0 }) @('NoMove') 0.3
$pod | Add-Member -Force NoteProperty BossTriggers @(
  [pscustomobject]@{ On='FirstAggro'; Pct=0; Actions=@([pscustomobject]@{ Type='SpawnAdds'; Hash='AIPS'; Count=2; Radius=5; Level=200 }) },
  [pscustomobject]@{ On='HealthBelow'; Pct=60; Actions=@([pscustomobject]@{ Type='SpawnAdds'; Hash='AIPS'; Count=2; Radius=5; Level=200 }) },
  [pscustomobject]@{ On='HealthBelow'; Pct=25; Actions=@([pscustomobject]@{ Type='SpawnAdds'; Hash='AIPS'; Count=2; Radius=5; Level=200 }) })
AddLeaf 'ALPO' @($pod) 'A-Pods: pods in Perpetual Wastelands spawn two adds at a time (AO-Universe); native hash ALPO x15 L200 in 570; killed outright instead of the 80% + Laser Tagging Device strike'
AddLeaf 'AIPS' @((Band 'Alien Pod Spawn' 200 55648 100 0.5)) 'Pod adds; mesh "alienpod spider"; name guessed'
$s10 = @(
  @('AEMD','Alien Medic',251773,110), @('AOCI','Alien Communication Officer',251787,100),
  @('ALLR','Alien Leader',247856,70), @('IUAI','Alien Immature Vector',251776,90))
foreach ($m in $s10) {
  $loot = @([pscustomobject]@{ Hash='KCLM'; Repeats=1; Chance=30; LevelMod=0 }, [pscustomobject]@{ Hash='AARM'; Repeats=1; Chance=10; LevelMod=0 })
  AddLeaf $m[0] @((Band $m[1] 130 $m[2] $m[3] 0.7 $loot), (Band $m[1] 170 $m[2] $m[3] 0.7 $loot)) "Sector 10 Duty Sergeant mission target (AO-Universe); native hash $($m[0]) L130-170 in 4374 mapped by guess; 0.7x HP for a small group"
}
AddLeaf 'TPFY' @((Band 'Regeneration Conduct' 130 260034 60 0.7 @() @('NoMove') 0.1), (Band 'Regeneration Conduct' 170 260034 60 0.7 @() @('NoMove') 0.1)) 'Sector 10 Regeneration Conducts (AO-Universe; real ones snare and heal nearby aliens, not done); hash TPFY guessed; signal-tower mesh'

# Native alien playfields: every other unmapped spawn hash above level 1 becomes a family of the hierarchy mobs
# whose rank band overlaps the spawn's levels (guessed). Unicorn outposts are human and left alone.
$alienPfs = @(4374, 6112, 6113, 6121, 6123, 4365, 4366, 4367, 4370)
$skipHashes = @('UOTP')
$families = 0
foreach ($pf in $alienPfs) {
  $sp = Join-Path $gd "Playfields\$pf\Spawns.json"; if (-not (Test-Path $sp)) { continue }
  foreach ($g in ((ReadJson $sp).spawns | Group-Object hashText)) {
    $h = $g.Name
    if ($tpl.Contains($h) -or $taken[$h] -or $h -in $skipHashes -or $h -eq 'AAAA' -or $h.StartsWith('U')) { continue }   # U*: Unicorn soldiers (guess from the outpost hashes)
    $min = ($g.Group.minLevel | Measure-Object -Minimum).Minimum; $max = ($g.Group.maxLevel | Measure-Object -Maximum).Maximum
    if ($min -le 1) { continue }
    $kids = @()
    if (@($g.Group).Count -le 1 -and $min -ge 175) { $kids = $generals }   # single high spawns: city-boss rank
    else { for ($ri = 0; $ri -lt $ranks.Count; $ri++) { if ($ranks[$ri][1] -le $max -and $ranks[$ri][2] -ge $min) { $kids += $rankLeaves[$ri] } } }
    if (-not $kids) { continue }
    $tpl[$h] = [pscustomobject]@{ Children = @($kids) }; $families++
  }
}
# Sector 10 bulk spawn ZK3X: Commando/Specialist only (110-175)
$tpl['ZK3X'] = [pscustomobject]@{ Children = @($rankLeaves[2] + $rankLeaves[3]) }
# KRME: the L300 bulk spawn of sectors 13/35/42 (also single in a ship): Sergeant rank, not Generals
if ($tpl.Contains('KRME')) { $tpl['KRME'] = [pscustomobject]@{ Children = @($rankLeaves[5]) } }
Save (Join-Path $gd 'NpcTemplates.Alien.json') $tpl 10
Save (Join-Path $gd 'ItemTemplates.Alien.json') $it 6
"templates=$($tpl.Count) families=$families items=$($it.Count)"

# ---------------- tradeskills (AO-Universe Alien Armor Process; skills in percent of QL)
$EE=126; $CL=161; $CH=163; $PT=159; $NP=160; $PS=162
$ts = New-Object System.Collections.Generic.List[object]
function Ts($name, $src, $tgt, $res, $skills, $delSrc, $delTgt = $true, $minPct = 0) { $ts.Add([pscustomobject]@{ Name=$name; Source=@($src); Target=@($tgt); Result=@($res); Skills=[object[]]@(foreach ($sk in $skills) { ,([int[]]$sk) }); DeleteSource=$delSrc; DeleteTarget=$delTgt; MinSourcePct=$minPct }) }
$pristine = @(247106,247107); $mutated = @(247108,247109)
$types = @(); for ($i = 0; $i -lt 12; $i++) { $types += ,@((247673 + 2*$i), (247674 + 2*$i)) }
Ts 'Analyze clump (Pristine)' 247100 @(247102,247103) $pristine @(@($EE,450),@($CL,450)) $false
Ts 'Analyze clump (Mutated)' 247100 @(247104,247105,247764,254804) $mutated @(@($EE,450),@($CL,450)) $false
for ($i = 0; $i -lt 12; $i++) { $p = $clumps[2 + $i]; Ts "Analyze clump (type $i)" 247100 @($p[0],$p[1]) $types[$i] @(@($EE,450),@($CL,450)) $false }
Ts 'Analyze clump (Type 48)' 247100 @(288699,288700) @(288672,288673) @(@($EE,450),@($CL,450)) $false
Ts 'DNA-Soup (Pristine)' 247110 $pristine @(247111,247112) @(,@($CH,450)) $true
Ts 'DNA-Soup (Mutated)' 247110 $mutated @(247111,247112) @(,@($CH,700)) $true
Ts 'DNA Cocktail' 247123 @(247111,247112) @(247124,247125) @(,@($PT,600)) $true
Ts 'Memory-Wiped Viralbots' 247099 @(247113,247114) @(247118,247119) @(@($CL,450),@($NP,450)) $false
Ts 'Formatted Viralbots' 161699 @(247118,247119) @(247120,247121) @(@($NP,600),@($CL,450)) $false
Ts 'Formatted Viralbot Solution' @(247120,247121) @(247124,247125) @(247126,247127) @(,@($PT,600)) $true $true 80
$slots = [ordered]@{ Vest=@(247163,@(247172,247173)); Sleeve=@(247164,@(247174,247175)); Legwear=@(247165,@(247176,247177)); Gloves=@(247166,@(247178,247179)); Footwear=@(247167,@(247180,247181)); Headwear=@(247157,@(247170,247171)) }
foreach ($k in $slots.Keys) { Ts "Formatted Viralbot $k" $slots[$k][0] @(247126,247127) $slots[$k][1] @(,@($CH,450)) $true }
# armor[bot][slot] = low (high = low + 1)
$armor = @{
  Arithmetic=@{ Vest=246559; Sleeve=246563; Legwear=246567; Gloves=246565; Footwear=246569; Headwear=246561 }
  Enduring  =@{ Vest=246579; Sleeve=246577; Legwear=246573; Gloves=246575; Footwear=246571; Headwear=246581 }
  Observant =@{ Vest=246591; Sleeve=246589; Legwear=246585; Gloves=246587; Footwear=246583; Headwear=246593 }
  Spiritual =@{ Vest=246599; Sleeve=246601; Legwear=246605; Gloves=246603; Footwear=246595; Headwear=246597 }
  Strong    =@{ Vest=246615; Sleeve=246613; Legwear=246609; Gloves=246611; Footwear=246607; Headwear=246617 }
  Supple    =@{ Vest=246621; Sleeve=246623; Legwear=246629; Gloves=246625; Footwear=246627; Headwear=246619 } }
foreach ($c in $classes) { foreach ($k in $slots.Keys) { $a = $armor[$c.Bot][$k]; Ts "$($c.Bot) $k" $c.Lead $slots[$k][1] @($a, ($a + 1)) @(,@($PS,600)) $true $true 80 } }
$combined = @(
  @('Commando','Strong','Supple', @{ Vest=246659; Sleeve=246661; Legwear=246665; Gloves=246663; Footwear=246667; Headwear=246633 }),
  @('Mercenary','Strong','Enduring', @{ Vest=246637; Sleeve=246639; Legwear=246643; Gloves=246641; Footwear=246645; Headwear=246635 }),
  @('Officer','Spiritual','Arithmetic', @{ Vest=246671; Sleeve=246673; Legwear=246677; Gloves=246675; Footwear=246679; Headwear=246669 }),
  @('Paramedic','Spiritual','Enduring', @{ Vest=246647; Sleeve=246649; Legwear=246653; Gloves=246651; Footwear=246655; Headwear=246631 }),
  @('Scout','Observant','Arithmetic', @{ Vest=246683; Sleeve=246685; Legwear=246689; Gloves=246687; Footwear=246691; Headwear=246681 }),
  @('Sharpshooter','Observant','Supple', @{ Vest=246695; Sleeve=246697; Legwear=246701; Gloves=246699; Footwear=246703; Headwear=246693 }))
foreach ($cb in $combined) { foreach ($k in $slots.Keys) {
  $a = $armor[$cb[1]][$k]; $b = $armor[$cb[2]][$k]; $r = $cb[3][$k]
  Ts "Combined $($cb[0])'s $k" @($a,($a+1)) @($b,($b+1)) @($r,($r+1)) @(,@($PS,600)) $true $true 80
  Ts "Combined $($cb[0])'s $k (swapped)" @($b,($b+1)) @($a,($a+1)) @($r,($r+1)) @(,@($PS,600)) $true $true 80
} }
Save (Join-Path $gd 'Tradeskills.json') ([object[]]$ts.ToArray()) 6
"tradeskills=$($ts.Count)"

# ---------------- quests (repeatable; giver re-offers when not active)
$quests = @()
function Q($hash, $qas, $name, $action, $count, $npc, $credits, $xp, $axp, $summary) {
  [pscustomobject]@{ Hash=$hash; Name=$name; QasInstance=$qas; Scope='Solo'; Action=$action; Summary=$summary
    Objective=[pscustomobject]@{ Count=$count; Npc=$npc; SpecItem=$null; WorldItem=$null }; Flags='None'; MissionBit=$null; NextStep=$null
    Reward=[pscustomobject]@{ Hash=('R' + $hash.Substring(1)); Credits=$credits; Xp=$xp; AlienXpPercent=$axp; Records=$null } }
}
$quests += Q 'AIWP' 920201 'We Come in Peace...' 'KillMultiple' 10 'AALV' 5000 15000 25 'Kill 10 alien larvae at the crashed ship north of ICC in Andromeda'
$quests += Q 'AIBG' 920202 'Bugs' 'KillMultiple' 100 'VEZI' 20000 60000 25 'Get rid of 100 Zixes on the infested Serenity Island'
$quests += Q 'AICC' 920203 'Terminate the Alien Cocoons' 'KillMultiple' 10 'AICO' 15000 40000 25 'Board the alien breeding ship and terminate 10 alien cocoons'
$quests += Q 'AILP' 920204 '...And you will leave in pieces' 'KillOne' $null 'AIMC' 15000 50000 25 'Enter the alien ship and kill the cocoon that spawns the larvae'
$quests += Q 'AIPD' 920205 'A-Pods' 'KillMultiple' 2 'ALPO' 60000 400000 25 'Destroy two alien pods in the Perpetual Wastelands'
$quests += Q 'S10M' 920206 'Kill 5 Alien Medics' 'KillMultiple' 5 'AEMD' 20000 80000 10 'Sector 10: kill 5 Alien Medics'
$quests += Q 'S10C' 920207 'Kill 5 Alien Communication Officers' 'KillMultiple' 5 'AOCI' 20000 80000 10 'Sector 10: kill 5 Alien Communication Officers'
$quests += Q 'S10L' 920208 'Kill 5 Alien Leaders' 'KillMultiple' 5 'ALLR' 20000 80000 10 'Sector 10: kill 5 Alien Leaders'
$quests += Q 'S10V' 920209 'Kill 20 Alien Immature Vectors' 'KillMultiple' 20 'IUAI' 30000 120000 15 'Sector 10: kill 20 Alien Immature Vectors'
Save (Join-Path $gd 'Quests.Alien.json') @($quests) 8
"quests=$($quests.Count)"

# ---------------- content NPCs
$icc = ReadJson (Join-Path $gd 'PlayfieldContent\655\Npcs.json')
$donor = $icc.Npcs | Where-Object { $_.Stats -and $_.Name -and -not $_.Vendor } | Select-Object -First 1
$vdonor = $icc.Npcs | Where-Object { $_.Vendor } | Select-Object -First 1
function Npc($pf, $inst, $name, $pos, $key, $prov, $src = $donor) {
  $n = Clone $src
  $n.Key = $key; $n.Provenance = $prov; $n.ContentNpcIdentity = ('SimpleChar:{0:X8}' -f $inst); $n.InstanceId = $inst
  $n.PlayfieldId = $pf; $n.Name = $name; $n.Position = @($pos); $n.Rotation = @(0,0,0,1)
  $n | Add-Member -Force NoteProperty Attackable $false; $n | Add-Member -Force NoteProperty Passive $true
  foreach ($drop in 'SpawnHash','Vendor') { if ($n.PSObject.Properties[$drop]) { $n.PSObject.Properties.Remove($drop) } }
  $n
}
$handler = Npc 655 0x7A0EA001 'Alien Agency Handler' @(3256.0, 35.1, 823.4) 'ddogs:alien:handler' 'ddogs/tools/Build-AlienInvasion.ps1 (real role; spot beside the ICC buff droid guessed; real handler is in the mission agency HQs)'
$bazzit = Npc 655 0x7A0EA002 'Uncle Bazzit' @(3246.8, 35.1, 823.4) 'ddogs:alien:bazzit' 'ddogs/tools/Build-AlienInvasion.ps1 (real shop for Generic Nano-Solvent and Essential Human DNA; spot and the extra stock guessed)'
$stock = @(@(247110,247110,100), @(247123,247123,100), @(161699,161699,1), @(247100,247100,100), @(247099,247099,100), @(247163,247163,1), @(247164,247164,1), @(247165,247165,1), @(247166,247166,1), @(247167,247167,1), @(247157,247157,1))
$sl = 0; $items = @(); foreach ($s in $stock) { $items += [ordered]@{ Slot = $sl++; LowId = $s[0]; HighId = $s[1]; Quality = $s[2] } }
$bazzit | Add-Member -Force NoteProperty Vendor ([ordered]@{ InstanceId = 0x130EA002; TemplateId = 99634; Stock = $items })
$icc.Npcs = @($icc.Npcs | Where-Object { $_.Key -notlike 'ddogs:alien:*' }) + $handler + $bazzit
Save (Join-Path $gd 'PlayfieldContent\655\Npcs.json') $icc 10
function PfNpcs($pf, $npcs) {
  $p = Join-Path $gd "PlayfieldContent\$pf\Npcs.json"
  if (Test-Path $p) { $o = ReadJson $p; $o.Npcs = @($o.Npcs | Where-Object { $_.Key -notlike 'ddogs:alien:*' }) + $npcs }
  else { New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null; $o = [pscustomobject]@{ SchemaVersion = 1; PlayfieldId = $pf; Npcs = @($npcs); StandaloneShops = @() } }
  Save $p $o 10
}
$iccBack = '{teleport 655 3252.0 35.1 826.0}{close}'
PfNpcs 6052 @(Npc 6052 0x7A0EA003 'Rookie Exterminator' @(1014.0, 5.6, 1300.0) 'ddogs:alien:exterminator' 'Bugs giver is the Rookie Exterminator (AO-Universe); here only the way back; spot guessed')
PfNpcs 6112 @(Npc 6112 0x7A0EA004 'Unicorn Shuttle Pilot' @(150.0, 6.4, 106.0) 'ddogs:alien:shippilot' 'Way back from the breeding ship; name and spot guessed')
PfNpcs 4374 @(Npc 4374 0x7A0EA005 'Unicorn Duty Sergeant' @(366.0, 10.4, 108.0) 'ddogs:alien:sergeant' 'Unicorn Duty Sergeant gives the Sector 10 missions (AO-Universe); placed inside the sector by the Unicorn line, guessed')
# cocoons in the scoutship (gitignored spawns)
$sp = Join-Path $gd 'Playfields\6112\Spawns.json'; $s = ReadJson $sp
$s.spawns = @($s.spawns | Where-Object { $_.hashText -notin 'AICO','AIMC' })
$cpos = @(@(211,13,162),@(214,13,158),@(220,13,158),@(223,13,162),@(211,13,174),@(223,13,174),@(214,13,178),@(220,13,178),@(208,13,168),@(226,13,168))
foreach ($p in $cpos) { $s.spawns += [pscustomobject]@{ districtIndex=0; hash=0; hashText='AICO'; manifestHash=0; minLevel=60; maxLevel=60; respawnChance=100; flags=1; respawnTime=180; version7Extra=0; hasOptionalFlagBlock=$true; nativeFlags=0; moreFlags=0; assistanceRadius=0; unknownOptionalU8=0; angle=0; angleW=1; position=@([double]$p[0],[double]$p[1],[double]$p[2]); radius=1; additionalPoints=@(); extensions=$null } }
$s.spawns += [pscustomobject]@{ districtIndex=0; hash=0; hashText='AIMC'; manifestHash=0; minLevel=95; maxLevel=95; respawnChance=100; flags=1; respawnTime=300; version7Extra=0; hasOptionalFlagBlock=$true; nativeFlags=0; moreFlags=0; assistanceRadius=0; unknownOptionalU8=0; angle=0; angleW=1; position=@(225.7,13.0,150.0); radius=1; additionalPoints=@(); extensions=$null }
Save $sp $s 8

# ---------------- dialogs
$kd = Join-Path $gd 'Knubot'
$h = [ordered]@{
  Notes = 'Alien Agency Handler (AO-Universe alien daily missions). Generated by ddogs/tools/Build-AlienInvasion.ps1. Teleport destinations guessed.'
  Npcs = @('SimpleChar:7A0EA001')
  Openers = @([ordered]@{ Say = 'hello' })
  Replies = [ordered]@{
    list = [ordered]@{ Text = 'What missions do you have?'; Goto = 'list' }
    wcp = [ordered]@{ Text = 'We Come in Peace... (25-49)'; If = @('!HasQuest AIWP'); Goto = 'wcp' }
    bugs = [ordered]@{ Text = 'Bugs (75-100)'; If = @('!HasQuest AIBG'); Goto = 'bugs' }
    lip = [ordered]@{ Text = '...And you will leave in pieces (75-99)'; If = @('!HasQuest AILP'); Goto = 'lip' }
    coc = [ordered]@{ Text = 'Terminate the Alien Cocoons (all levels)'; If = @('!HasQuest AICC'); Goto = 'coc' }
    pods = [ordered]@{ Text = 'A-Pods (200-220)'; If = @('!HasQuest AIPD'); Goto = 'pods' }
    s10 = [ordered]@{ Text = 'Take me to Sector 10 (100-150).'; Goto = 's10' }
    goisl = [ordered]@{ Text = 'Send me to the infested island.'; Goto = 'goisl' }
    goship = [ordered]@{ Text = 'Send me to the alien ship.'; Goto = 'goship' }
    gopw = [ordered]@{ Text = 'Send me to the pods.'; Goto = 'gopw' }
    gocrash = [ordered]@{ Text = 'Where is the crash site?'; Goto = 'gocrash' }
    bye = [ordered]@{ Text = 'Goodbye.'; Goto = 'bye' }
  }
  Lines = [ordered]@{
    hello = [ordered]@{ Text = 'The Kyr''Ozch are constantly surrounding the Goliath-class battlestations in orbit around Rubi-Ka. The Agency pays well for every alien problem you solve.'; Replies = @('list','gocrash','goisl','goship','gopw','s10','bye') }
    list = [ordered]@{ Text = 'Pick one. Come back for another when it is done.'; Replies = @('wcp','bugs','lip','coc','pods','bye') }
    wcp = [ordered]@{ Text = @('{givequest AIWP}', 'After being damaged by the Notum Cannons, an alien ship crash landed just north of ICC. Alien larvae keep pouring out of it. Kill at least 10 of them before they spread over Rubi-Ka.'); Replies = @('gocrash','bye') }
    bugs = [ordered]@{ Text = @('{givequest AIBG}', 'Zixes have infested one of the Serenity Islands. They are a pestering annoyance more than a danger. Get rid of 100 of them.'); Replies = @('goisl','bye') }
    lip = [ordered]@{ Text = @('{givequest AILP}', 'The larvae all come from one source: a cocoon deep inside the alien ship. Follow the stream of larvae, find the cocoon and kill it.'); Replies = @('goship','bye') }
    coc = [ordered]@{ Text = @('{givequest AICC}', 'Board the alien breeding ship and terminate 10 alien cocoons. A Kyr''Ozch Guardian watches the main cocoon room, and each cocoon releases a larva.'); Replies = @('goship','bye') }
    pods = [ordered]@{ Text = @('{givequest AIPD}', 'There is a grouping of alien pods out in the Perpetual Wastelands. Bring down two of them. They spawn defenders two at a time.'); Replies = @('gopw','bye') }
    gocrash = [ordered]@{ Text = 'The crashed ship lies in a narrow valley just north of ICC. Head north from the building, the larvae are all around it.'; Replies = @('list','bye') }
    goisl = [ordered]@{ Text = '{teleport 6052 1012.0 5.6 1300.0}{close}' }
    goship = [ordered]@{ Text = '{teleport 6112 150.0 6.4 106.0}{close}' }
    gopw = [ordered]@{ Text = '{teleport 570 420.0 50.8 1615.0}{close}' }
    s10 = [ordered]@{ Text = '{teleport 4374 372.0 10.4 106.0}{close}' }
    bye = [ordered]@{ Text = '{close}' }
  }
}
Save (Join-Path $kd 'AlienAgencyHandler.json') $h 8
$b = [ordered]@{
  Notes = 'Uncle Bazzit''s alien tradeskill shop. Generated by ddogs/tools/Build-AlienInvasion.ps1.'
  Npcs = @('SimpleChar:7A0EA002')
  Openers = @([ordered]@{ Say = 'hello' })
  Replies = [ordered]@{ wares = [ordered]@{ Text = 'Show me what you have.'; Goto = 'shop' }; how = [ordered]@{ Text = 'How do I make alien armor?'; Goto = 'how' }; bye = [ordered]@{ Text = 'Goodbye.'; Goto = 'bye' } }
  Lines = [ordered]@{
    hello = [ordered]@{ Text = 'Uncle Bazzit''s! Solvents, human DNA, programming interfaces - everything a budding xeno-tailor needs.'; Replies = @('wares','how','bye') }
    how = [ordered]@{ Text = 'Analyzer on a Solid Clump makes Pristine or Mutated Bio-Material. My Nano-Solvent on that makes DNA-Soup, Essential Human DNA on the soup makes a DNA Cocktail. Atomic Re-Structuralizing Tool on Kyr''Ozch Viralbots wipes them, the Programming Interface formats them, and formatted viralbots on the cocktail make the Viralbot Solution. Pour that on Basic Fashion clothing, then add Lead Viralbots from the Generals. Viralbots must be at least 80% of the armor QL.'; Replies = @('wares','bye') }
    shop = [ordered]@{ Text = '{shop}' }
    bye = [ordered]@{ Text = '{close}' }
  }
}
Save (Join-Path $kd 'UncleBazzit.json') $b 8
function Back($file, $id, $name, $text) { Save (Join-Path $kd $file) ([ordered]@{ Notes = "$name - way back to ICC. Generated by ddogs/tools/Build-AlienInvasion.ps1 (guessed)."; Npcs = @($id); Openers = @([ordered]@{ Say = 'hello' }); Replies = [ordered]@{ go = [ordered]@{ Text = 'Take me back to ICC.'; Goto = 'go' }; stay = [ordered]@{ Text = 'Not yet.'; Goto = 'stay' } }; Lines = [ordered]@{ hello = [ordered]@{ Text = $text; Replies = @('go','stay') }; go = [ordered]@{ Text = $iccBack }; stay = [ordered]@{ Text = '{close}' } } }) 8 }
Back 'AlienExterminator.json' 'SimpleChar:7A0EA003' 'Rookie Exterminator' 'Watch where you step, the place is crawling with Zixes. Need a ride back?'
Back 'AlienShipPilot.json' 'SimpleChar:7A0EA004' 'Unicorn Shuttle Pilot' 'The shuttle leaves whenever you are ready.'
$sg = [ordered]@{
  Notes = 'Unicorn Duty Sergeant, Sector 10 missions (AO-Universe). Generated by ddogs/tools/Build-AlienInvasion.ps1. DNA cage mission not done.'
  Npcs = @('SimpleChar:7A0EA005')
  Openers = @([ordered]@{ Say = 'hello' })
  Replies = [ordered]@{
    med = [ordered]@{ Text = 'Kill 5 Alien Medics'; If = @('!HasQuest S10M'); Goto = 'med' }
    com = [ordered]@{ Text = 'Kill 5 Alien Communication Officers'; If = @('!HasQuest S10C'); Goto = 'com' }
    lead = [ordered]@{ Text = 'Kill 5 Alien Leaders'; If = @('!HasQuest S10L'); Goto = 'lead' }
    vec = [ordered]@{ Text = 'Kill 20 Alien Immature Vectors'; If = @('!HasQuest S10V'); Goto = 'vec' }
    back = [ordered]@{ Text = 'Take me back to ICC.'; Goto = 'back' }
    bye = [ordered]@{ Text = 'Goodbye.'; Goto = 'bye' }
  }
  Lines = [ordered]@{
    hello = [ordered]@{ Text = 'The aliens keep pushing through the fence. Thin them out and the Unicorns will make it worth your while. Kill the Regeneration Conducts first.'; Replies = @('med','com','lead','vec','back','bye') }
    med = [ordered]@{ Text = @('{givequest S10M}', 'Their medics keep the rest alive. Five of them.'); Replies = @('com','lead','vec','bye') }
    com = [ordered]@{ Text = @('{givequest S10C}', 'Cut their communications. Five officers.'); Replies = @('med','lead','vec','bye') }
    lead = [ordered]@{ Text = @('{givequest S10L}', 'Not the big generals, the smaller leaders. Five.'); Replies = @('med','com','vec','bye') }
    vec = [ordered]@{ Text = @('{givequest S10V}', 'Twenty Immature Vectors. A long job, but no timer.'); Replies = @('med','com','lead','bye') }
    back = [ordered]@{ Text = $iccBack }
    bye = [ordered]@{ Text = '{close}' }
  }
}
Save (Join-Path $kd 'UnicornDutySergeant.json') $sg 8
'alien invasion content written'