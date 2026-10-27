# Stocks the Ofab vending machines that already stand in the Unicorn Defence Hub (playfield 6007)
# and writes the nano shops plus the battle-station queue terminals into PlayfieldContent/6007/Npcs.json.
# Strips the old Ofab content terminals from playfield 655. The Tower Shop and the buffbot stay.
# Armor ids are the base pairs. Weapon stock is Mk 1 only. QL for crystals comes from tools-temp/ofab-item-check.txt.
# Published nano VP prices are in $knownVp (retail wiki numbers). Shop prices are about one tenth
# of those / of QL*QL/40, rounded tidy, minimum 1. Attunement level 1 is 20 VP; markers 25.
param(
  [string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'),
  [string]$QlFile = (Join-Path $PSScriptRoot '..\..\tools-temp\ofab-item-check.txt')
)
$ErrorActionPreference = 'Stop'
$utf8 = New-Object Text.UTF8Encoding $false

$qlOf = @{}
foreach ($line in [IO.File]::ReadAllLines($QlFile)) {
  if ($line -match '^(\d+)\t(\d+)\t') { $qlOf[[int]$Matches[1]] = [int]$Matches[2] }
}
# Retail wiki numbers stay in $knownVp; ScaleVp applies the private-server tenth cut.
function ScaleVp([int]$retail) {
  $n = [Math]::Max(1, [int][Math]::Round($retail / 10.0))
  if ($n -ge 50) { return [Math]::Max(1, 5 * [int][Math]::Round($n / 5.0)) }
  return $n
}
function Vp([int]$ql) { return (ScaleVp ([Math]::Max(1, [int][Math]::Floor($ql * $ql / 40.0)))) }
# Published Victory Point prices from wiki.aodb.us/wiki/OFAB_Nanos (Maori, 2011). TBC rows are not in here.
$knownVp = @{
  218167=8000; 265441=3750; 265442=3750; 265443=3750; 265445=3750; 265446=3750; 265447=3750; 265448=3750
  265449=3750; 265450=3750; 265451=3750; 265452=3750; 265453=3750; 265454=3750; 265455=920; 265456=920
  265457=920; 265458=1800; 265459=1800; 265460=1800; 265461=3520; 265462=3520; 265463=3520; 265464=5915
  265465=5915; 265466=5915; 265467=7310; 265468=7310; 265469=7310; 265470=8000; 265471=8000
  267111=6000; 267115=6000; 267118=8000; 267122=6000; 267267=6000; 267268=8000; 267270=8000; 267273=8000
  267274=6000; 267276=6000; 267282=8500; 267284=8000; 267537=6000; 267539=5000; 267629=5000; 267630=5000
  267631=5000; 267632=5000; 267633=3000; 267634=4000; 267635=5000; 267636=3000; 267637=4000; 267638=5000
  267639=5000; 267640=5000; 267641=5000; 267642=5000; 267643=4000; 267644=3000; 267645=3000; 267646=7000
  267647=6000; 267648=5000; 267650=4000; 267919=4000; 267920=6000; 269871=1200; 269880=8000
  274372=1000; 274374=1000; 279341=1000; 279346=1000; 279350=1000; 279373=1000; 279375=1000; 279377=1000
  279380=1000; 280053=1000
}
function PriceOf([int]$id, [int]$ql) {
  if ($knownVp.ContainsKey($id)) { return (ScaleVp ([int]$knownVp[$id])) }
  return (Vp $ql)
}
function NeedQl([int]$id) {
  if (-not $qlOf.ContainsKey($id)) { throw "missing QL for $id" }
  return [int]$qlOf[$id]
}

# name, low, high. Six pieces. Eight shop QLs. 48 lines, under the 64-slot pane.
$armor = [ordered]@{
  'Adventurer' = @(
    @('Helmet',264208,264209), @('Body',264202,264203), @('Sleeves',264204,264205),
    @('Gloves',264206,264207), @('Pants',264198,264199), @('Boots',264200,264201))
  'Agent' = @(
    @('Helmet',264287,264288), @('Body',264305,264306), @('Sleeves',264299,264300),
    @('Gloves',264293,264294), @('Pants',264317,264318), @('Boots',264311,264312))
  'Bureaucrat' = @(
    @('Headgear',264503,264504), @('Vest',264521,264522), @('Sleeves',264515,264516),
    @('Gloves',264509,264510), @('Pants',264533,264534), @('Boots',264527,264528))
  'Doctor' = @(
    @('Helmet',264650,264651), @('Body',264668,264669), @('Sleeves',264662,264663),
    @('Gloves',264656,264657), @('Pants',264680,264681), @('Boots',264674,264675))
  'Enforcer' = @(
    @('Helmet',264241,264242), @('Breastplate',264223,264224), @('Sleeves',264229,264230),
    @('Gauntlets',264235,264236), @('Pants',264211,264212), @('Boots',264217,264218))
  'Engineer' = @(
    @('Helmet',264575,264576), @('Body',264596,264597), @('Sleeves',264590,264591),
    @('Gloves',264581,264582), @('Pants',264608,264609), @('Boots',264602,264603))
  'Fixer' = @(
    @('Helmet',264467,264468), @('Body',264485,264486), @('Sleeves',264479,264480),
    @('Gloves',264473,264474), @('Pants',264497,264498), @('Boots',264491,264492))
  'Keeper' = @(
    @('Helmet',264614,264615), @('Body',264632,264633), @('Sleeves',264626,264627),
    @('Gloves',264620,264621), @('Pants',264644,264645), @('Boots',264638,264639))
  'Martial Artist' = @(
    @('Helmet',264323,264324), @('Body',264341,264342), @('Sleeves',264335,264336),
    @('Gloves',264329,264330), @('Pants',264353,264354), @('Boots',264347,264348))
  'Metaphysicist' = @(
    @('Headgear',264359,264360), @('Body',264377,264378), @('Sleeves',264371,264372),
    @('Gloves',264365,264366), @('Pants',264389,264390), @('Boots',264383,264384))
  'Nano Technician' = @(
    @('Helmet',264395,264396), @('Body',264413,264414), @('Sleeves',264407,264408),
    @('Gloves',264401,264402), @('Pants',264425,264426), @('Boots',264419,264420))
  'Shade' = @(
    @('Helmet',264539,264540), @('Body',264557,264558), @('Sleeves',264551,264552),
    @('Gloves',264545,264546), @('Pants',264569,264570), @('Boots',264563,264564))
  'Soldier' = @(
    @('Helmet',264431,264432), @('Body',264449,264450), @('Sleeves',264443,264444),
    @('Gloves',264437,264438), @('Pants',264461,264462), @('Boots',264455,264456))
  'Trader' = @(
    @('Helmet',264251,264252), @('Body',264269,264270), @('Sleeves',264263,264264),
    @('Gloves',264257,264258), @('Pants',264275,264276), @('Boots',264281,264282))
}
# Hub machine template id for each profession. Agent armor is this machine, not item 266578.
$machine = [ordered]@{
  'Adventurer' = 266562; 'Agent' = 266563; 'Doctor' = 266564; 'Enforcer' = 266565
  'Engineer' = 266566; 'Fixer' = 266567; 'Keeper' = 266568; 'Bureaucrat' = 266569
  'Martial Artist' = 266570; 'Metaphysicist' = 266571; 'Nano Technician' = 266572
  'Soldier' = 266573; 'Shade' = 266574; 'Trader' = 266575
}
# Each mark is its own QL 1-300 family (low, high) x 6. Melee stocks QL 100 and 300 (60 lines).
# Ranged stocks QL 300 only (36 lines). Both QLs would be 72, past a 64-slot pane.
$melee = @{
  Bear = @(265237,265243,265244,265250,265251,265257,265258,265264,265265,265271,265272,265278)
  Mongoose = @(265146,265152,264901,264907,264908,264914,264915,264921,264922,264928,264929,264935)
  Panther = @(264936,264942,264943,264949,264950,264956,264957,264963,264964,264970,264971,264977)
  Viper = @(264978,264984,264985,264991,264992,264998,264999,265005,265006,265012,265013,265019)
  Wolf = @(265195,265201,265202,265208,265209,265215,265216,265222,265223,265229,265230,265236)
}
$ranged = @{
  Cobra = @(265279,265285,265286,265292,265293,265299,265300,265306,265307,265313,265314,265320)
  Hawk = @(265153,265159,265160,265166,265167,265173,265174,265180,265181,265187,265188,265194)
  Peregrine = @(265020,265026,265027,265033,265034,265040,265041,265047,265048,265054,265055,265061)
  Shark = @(265062,265068,265069,265075,265076,265082,265083,265089,265090,265096,265097,265103)
  Silverback = @(264853,264859,264864,264870,264871,264877,264878,264884,264885,264891,264892,264898)
  Tiger = @(265104,265110,265111,265117,265118,265124,265125,265131,265132,265138,265139,265145)
}
function GearRows($pieces) {
  $qls = 1,25,50,100,150,200,250,300
  $rows = @()
  foreach ($p in $pieces) {
    foreach ($q in $qls) {
      $rows += [ordered]@{ Low = [int]$p[1]; High = [int]$p[2]; Ql = [int]$q; Price = (Vp $q) }
    }
  }
  return $rows
}
function WeaponRows($table, $qls, [int]$marks) {
  $rows = @()
  foreach ($name in ($table.Keys | Sort-Object)) {
    $ids = $table[$name]
    for ($m = 0; $m -lt $marks; $m++) {
      $low = [int]$ids[$m * 2]; $high = [int]$ids[$m * 2 + 1]
      foreach ($q in $qls) {
        $rows += [ordered]@{ Low = $low; High = $high; Ql = [int]$q; Price = (Vp $q) }
      }
    }
  }
  return $rows
}
function CrystalRows($ids) {
  $rows = @()
  foreach ($id in $ids) {
    $q = NeedQl $id
    $rows += [ordered]@{ Low = [int]$id; High = [int]$id; Ql = $q; Price = (PriceOf $id $q) }
  }
  return $rows
}

$vendors = [ordered]@{}
foreach ($prof in $armor.Keys) {
  $id = [string]$machine[$prof]
  $vendors[$id] = @(GearRows $armor[$prof])
}
$step25 = 25,50,75,100,125,150,175,200,225,250,275,300
$vendors['266577'] = @(WeaponRows $melee $step25 1)
# Six ranged Mk 1s at all 12 steps is 72 lines. QL 25 and 50 are left off so the pane stays at 60.
$rangedQl = 75,100,125,150,175,200,225,250,275,300
$vendors['266576'] = @(WeaponRows $ranged $rangedQl 1)

$general = @()
$general += CrystalRows @(269909,269910,268170,269871,269880,301144)
$families = @(
  @(269401,269402,269403),
  @(269412,269410,269411),
  @(269418,269416,269417),
  @(269407,269408,269409),
  @(269406,269404,269405),
  @(269415,269413,269414),
  @(293693,293699,293701)
)
foreach ($fam in $families) {
  $id = [int]$fam[0]
  $q = NeedQl $id
  $general += [ordered]@{ Low = $id; High = $id; Ql = $q; Price = 20 }
}
foreach ($id in @(265405,265406)) {
  $q = NeedQl $id
  $general += [ordered]@{ Low = [int]$id; High = [int]$id; Ql = $q; Price = 25 }
}
$vendors['266579'] = @($general)

foreach ($key in @($vendors.Keys)) {
  $n = @($vendors[$key]).Count
  if ($n -lt 1 -or $n -gt 64) { throw "vendor $key has $n lines" }
}
$stockDoc = [ordered]@{
  Notes = 'Hub vending machines on playfield 6007. Weapons are Mk 1. Shop VP is about one tenth of retail wiki / QL*QL/40, tidy-rounded, min 1. Attunement level 1 is 20 VP. Build-OfabVpShop.ps1'
  Vendors = $vendors
}
$stockPath = Join-Path $GameData 'OfabHubStock.json'
[IO.File]::WriteAllText($stockPath, (($stockDoc | ConvertTo-Json -Depth 6) + "`n"), $utf8)

# Profession crystals from the AO-Universe Ofab nano shopping guide. General nanos are on 266579.
$meleeHybrid = @(
  287754,301854,301289,265449,279350,
  279346,265442,301937,301492,287556,
  301836,302224,302244,302218,302237,265446,302230,
  265453,301154,279380,301601,301119,
  301896,265454,301594
)
$supportA = @(
  301898,302396,302400,265441,267645,
  265445,302413,
  265444,274374,301863,279377,302388,301868,301860,279375,
  267646,265451,267647,267648,301846,279373,267650,
  267638,274372,267636,301856,302255,267637,265443
)
$supportB = @(
  265447,267118,267115,267268,267122,267267,301530,301522,267111,302289,302403,301900,302291,280053,301534,301526,301542,
  267644,267634,267641,267919,302248,267635,220622,302146,301082,267539,267537,267633,267643,267642,265448,267920,302151,302149,302155,279341
)
$supportC = @(
  267270,267640,267631,267630,267629,267639,267632,302258,267273,302189,301889,267276,267274,267284,265452,300507,267282,
  265459,265462,265458,265460,265469,265468,265455,265457,265456,265465,302275,265471,265470,265464,265466,218167,302076,265461,265463,265450,265467
)

function ShopStock($ids) {
  $rows = @(); $slot = 0
  foreach ($id in $ids) {
    $q = NeedQl $id
    $rows += [ordered]@{ Slot = $slot; LowId = [int]$id; HighId = [int]$id; Quality = $q; Price = (PriceOf $id $q) }
    $slot++
  }
  if ($rows.Count -lt 1 -or $rows.Count -gt 64) { throw "nano pane has $($rows.Count) lines" }
  return $rows
}

$stats = [ordered]@{
  '0' = 277352961; '1' = 1650; '4' = 6; '27' = 1650; '33' = 0; '47' = 1; '54' = 40
  '59' = 1; '60' = 0; '64' = 0; '89' = 1; '156' = 137; '173' = 3; '174' = 3
  '359' = 279184; '360' = 100; '368' = 0; '389' = 0; '455' = 0; '466' = 0; '660' = 0; '673' = 31
}
$textures = @()
foreach ($place in 0..4) { $textures += [ordered]@{ Place = $place; Id = 0 } }

function ContentNpc($key, $identity, $instance, $name, $x, $z, $vendor) {
  $npc = [ordered]@{
    Key = $key
    Provenance = 'Unicorn Defence Hub playfield 6007. Build-OfabVpShop.ps1. Y 6.4 is the hub floor band, not a walked pin.'
    ContentNpcIdentity = $identity
    PlayfieldId = 6007
    InstanceId = $instance
    Name = $name
    Position = @([double]$x, 6.4, [double]$z)
    Rotation = @(0, 0, 0, 1)
    Stats = $stats
    Textures = $textures
    Meshes = @()
    Passive = $true
    Attackable = $false
    TickLiving = $true
    Presentation = [ordered]@{}
  }
  if ($null -ne $vendor) { $npc.Vendor = $vendor }
  return $npc
}

$shops = @(
  @{ Key = 'melee-hybrid'; Name = 'Ofab Nanos (Melee and Hybrid)'; Ids = $meleeHybrid; X = 220; Z = 214 }
  @{ Key = 'support-a'; Name = 'Ofab Nanos (Soldier Agent Fixer Doctor Engineer)'; Ids = $supportA; X = 228; Z = 214 }
  @{ Key = 'support-b'; Name = 'Ofab Nanos (Trader and Bureaucrat)'; Ids = $supportB; X = 236; Z = 214 }
  @{ Key = 'support-c'; Name = 'Ofab Nanos (MP and NT)'; Ids = $supportC; X = 244; Z = 214 }
)
$queues = @(
  @{ Name = 'Battle Station 10-30'; X = 248.5; Z = 189 }
  @{ Name = 'Battle Station 31-60'; X = 241; Z = 196.5 }
  @{ Name = 'Battle Station 61-100'; X = 241; Z = 181.5 }
  @{ Name = 'Battle Station 101-150'; X = 225; Z = 196.5 }
  @{ Name = 'Battle Station 151-200'; X = 217.5; Z = 189 }
  @{ Name = 'Battle Station 201-214'; X = 225; Z = 181.5 }
  @{ Name = 'Battle Station 215-220'; X = 231; Z = 189 }
)

$npcs = @()
for ($i = 0; $i -lt $shops.Count; $i++) {
  $s = $shops[$i]
  $key = 'ddogs:ofab:6007:' + $s.Key
  $vendor = [ordered]@{
    InstanceId = 3180030000 + $i
    TemplateId = 99634
    SpendsVictoryPoints = $true
    Stock = @(ShopStock $s.Ids)
  }
  $npc = ContentNpc $key 'ddogs-ofab-nano' (2110930000 + $i) $s.Name $s.X $s.Z $vendor
  $npcs = $npcs + @(,$npc)
}
for ($i = 0; $i -lt $queues.Count; $i++) {
  $q = $queues[$i]
  $slug = ($q.Name.ToLower() -replace '[^a-z0-9]+','-').Trim('-')
  $key = 'ddogs:bs:6007:' + $slug
  $npc = ContentNpc $key 'ddogs-bs-queue' (2110920000 + $i) $q.Name $q.X $q.Z $null
  $npcs = $npcs + @(,$npc)
}

$hubDir = Join-Path $GameData 'PlayfieldContent\6007'
if (-not (Test-Path $hubDir)) { New-Item -ItemType Directory -Path $hubDir | Out-Null }
$hubDoc = [ordered]@{
  SchemaVersion = 1
  PlayfieldId = 6007
  Npcs = @($npcs)
  StandaloneShops = @()
  MailTerminals = @()
}
$hubPath = Join-Path $hubDir 'Npcs.json'
$hubJson = ($hubDoc | ConvertTo-Json -Depth 12) + "`n"
if ($hubJson -notmatch '"StandaloneShops"') { throw 'StandaloneShops missing from 6007 JSON' }
[IO.File]::WriteAllText($hubPath, $hubJson, $utf8)

$andromeda = Join-Path $GameData 'PlayfieldContent\655\Npcs.json'
$doc655 = [IO.File]::ReadAllText($andromeda) | ConvertFrom-Json
$keep = @($doc655.Npcs | Where-Object { $_.Key -notlike 'ddogs:ofab:*' })
$removed = @($doc655.Npcs).Count - $keep.Count
$doc655.Npcs = @($keep)
[IO.File]::WriteAllText($andromeda, (($doc655 | ConvertTo-Json -Depth 12) + "`n"), $utf8)

Write-Output ("vendors=" + $vendors.Count + " armor=" + (@($vendors['266573']).Count) + " melee=" + (@($vendors['266577']).Count) + " ranged=" + (@($vendors['266576']).Count) + " general=" + $general.Count + " nanos=" + $shops.Count + " queues=" + $queues.Count + " stripped655=" + $removed)
