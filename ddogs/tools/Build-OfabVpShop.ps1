# Stocks the Ofab vending machines in the Unicorn Defence Hub (playfield 6007)
# and writes the nano shops plus the battle-station queue terminals into PlayfieldContent/6007/Npcs.json.
# Strips old Ofab content terminals from playfield 655. Tower Shop stays.
# House design: Randy's ofab-shop-list (2026-10-26), prices at about one tenth of the list formulas.
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
function NeedQl([int]$id) {
  if ($qlOf.ContainsKey($id)) { return [int]$qlOf[$id] }
  return 300
}
function ScaleVp([int]$retail) {
  $n = [Math]::Max(1, [int][Math]::Round($retail / 10.0))
  if ($n -ge 50) { return [Math]::Max(1, 5 * [int][Math]::Round($n / 5.0)) }
  return $n
}
# Slot multipliers from Randy's list (pre-/10).
$slotMul = @{
  Body = 40; Pants = 30; Helmet = 25; Sleeves = 20; Gloves = 15; Boots = 15
  Back = 20; Shoulder = 20; Ring = 20; Special = 25; Weapon = 25
}
function ArmorPrice([string]$slot, [int]$ql) {
  $m = 20
  if ($slotMul.ContainsKey($slot)) { $m = [int]$slotMul[$slot] }
  return (ScaleVp ($m * $ql))
}
function WeaponPrice([int]$ql) { return (ScaleVp (25 * $ql)) }

# Nano tiers from Randy's list (retail 1000/2500/5000 -> shop 100/250/500).
$nanoLow = @(269871,269880,301836,279341,279373,279350,279375,279380,279346)
$nanoMid = @(269909,269910,302224,265445,267539,265451,301289,267636,267270,265458,265454,301898)
$nanoHigh = @(
  268170,302244,302413,267644,267646,287754,267638,265444,265453,301154,
  287556,218167,265454,265441,267645,265447,267118,267111,
  267629,267630,267631,267632,267639,267640
)
$nanoTier = @{}
foreach ($id in $nanoLow) { $nanoTier[$id] = 100 }
foreach ($id in $nanoMid) { $nanoTier[$id] = 250 }
foreach ($id in $nanoHigh) { $nanoTier[$id] = 500 }
function NanoPrice([int]$id) {
  if ($nanoTier.ContainsKey($id)) { return [int]$nanoTier[$id] }
  return 250
}

# Base Mk 1 armor: name, low, high, price-slot key.
$armor = [ordered]@{
  'Adventurer' = @(
    @('Helmet',264208,264209,'Helmet'), @('Body',264202,264203,'Body'), @('Sleeves',264204,264205,'Sleeves'),
    @('Gloves',264206,264207,'Gloves'), @('Pants',264198,264199,'Pants'), @('Boots',264200,264201,'Boots'))
  'Agent' = @(
    @('Helmet',264287,264288,'Helmet'), @('Body',264305,264306,'Body'), @('Sleeves',264299,264300,'Sleeves'),
    @('Gloves',264293,264294,'Gloves'), @('Pants',264317,264318,'Pants'), @('Boots',264311,264312,'Boots'))
  'Bureaucrat' = @(
    @('Helmet',264503,264504,'Helmet'), @('Body',264521,264522,'Body'), @('Sleeves',264515,264516,'Sleeves'),
    @('Gloves',264509,264510,'Gloves'), @('Pants',264533,264534,'Pants'), @('Boots',264527,264528,'Boots'))
  'Doctor' = @(
    @('Helmet',264650,264651,'Helmet'), @('Body',264668,264669,'Body'), @('Sleeves',264662,264663,'Sleeves'),
    @('Gloves',264656,264657,'Gloves'), @('Pants',264680,264681,'Pants'), @('Boots',264674,264675,'Boots'))
  'Enforcer' = @(
    @('Helmet',264241,264242,'Helmet'), @('Body',264223,264224,'Body'), @('Sleeves',264229,264230,'Sleeves'),
    @('Gloves',264235,264236,'Gloves'), @('Pants',264211,264212,'Pants'), @('Boots',264217,264218,'Boots'))
  'Engineer' = @(
    @('Helmet',264575,264576,'Helmet'), @('Body',264596,264597,'Body'), @('Sleeves',264590,264591,'Sleeves'),
    @('Gloves',264581,264582,'Gloves'), @('Pants',264608,264609,'Pants'), @('Boots',264602,264603,'Boots'))
  'Fixer' = @(
    @('Helmet',264467,264468,'Helmet'), @('Body',264485,264486,'Body'), @('Sleeves',264479,264480,'Sleeves'),
    @('Gloves',264473,264474,'Gloves'), @('Pants',264497,264498,'Pants'), @('Boots',264491,264492,'Boots'))
  'Keeper' = @(
    @('Helmet',264614,264615,'Helmet'), @('Body',264632,264633,'Body'), @('Sleeves',264626,264627,'Sleeves'),
    @('Gloves',264620,264621,'Gloves'), @('Pants',264644,264645,'Pants'), @('Boots',264638,264639,'Boots'))
  'Martial Artist' = @(
    @('Helmet',264323,264324,'Helmet'), @('Body',264341,264342,'Body'), @('Sleeves',264335,264336,'Sleeves'),
    @('Gloves',264329,264330,'Gloves'), @('Pants',264353,264354,'Pants'), @('Boots',264347,264348,'Boots'))
  'Metaphysicist' = @(
    @('Helmet',264359,264360,'Helmet'), @('Body',264377,264378,'Body'), @('Sleeves',264371,264372,'Sleeves'),
    @('Gloves',264365,264366,'Gloves'), @('Pants',264389,264390,'Pants'), @('Boots',264383,264384,'Boots'))
  'Nano Technician' = @(
    @('Helmet',264395,264396,'Helmet'), @('Body',264413,264414,'Body'), @('Sleeves',264407,264408,'Sleeves'),
    @('Gloves',264401,264402,'Gloves'), @('Pants',264425,264426,'Pants'), @('Boots',264419,264420,'Boots'))
  'Shade' = @(
    @('Helmet',264539,264540,'Helmet'), @('Body',264557,264558,'Body'), @('Sleeves',264551,264552,'Sleeves'),
    @('Gloves',264545,264546,'Gloves'), @('Pants',264569,264570,'Pants'), @('Boots',264563,264564,'Boots'))
  'Soldier' = @(
    @('Helmet',264431,264432,'Helmet'), @('Body',264449,264450,'Body'), @('Sleeves',264443,264444,'Sleeves'),
    @('Gloves',264437,264438,'Gloves'), @('Pants',264461,264462,'Pants'), @('Boots',264455,264456,'Boots'))
  'Trader' = @(
    @('Helmet',264251,264252,'Helmet'), @('Body',264269,264270,'Body'), @('Sleeves',264263,264264,'Sleeves'),
    @('Gloves',264257,264258,'Gloves'), @('Pants',264275,264276,'Pants'), @('Boots',264281,264282,'Boots'))
}
# Back / shoulder / special helmet / ring (single-id templates, usually QL 300).
$extras = [ordered]@{
  'Adventurer' = @{ Back = 267931; Shoulder = 301693; Special = @(267353,267354); Ring = @(267560,267561) }
  'Agent' = @{ Back = 267509; Shoulder = 301698; Special = @(267356,267357); Ring = @(267582,267583) }
  'Bureaucrat' = @{ Back = 267932; Shoulder = 301696; Special = @(267363,267364); Ring = @(268307,268308) }
  'Doctor' = @{ Back = 267933; Shoulder = 301697; Special = @(267351,267352); Ring = @(267562,267563) }
  'Enforcer' = @{ Back = 267510; Shoulder = 267511; Special = @(267365,267366); Ring = @(267564,267565) }
  'Engineer' = @{ Back = 267936; Shoulder = 268003; Special = @(267369,267370); Ring = @(267566,267567) }
  'Fixer' = @{ Back = 267512; Shoulder = 301694; Special = @(267367,267368); Ring = @(267568,267569) }
  'Keeper' = @{ Back = 267513; Shoulder = 267514; Special = @(267375,267376); Ring = @(267570,267571) }
  'Martial Artist' = @{ Back = 267937; Shoulder = 268004; Special = @(267371,267372); Ring = @(267572,267573) }
  'Metaphysicist' = @{ Back = 267515; Shoulder = 301695; Special = @(267373,267374); Ring = @(268305,268306) }
  'Nano Technician' = @{ Back = 267938; Shoulder = 268081; Special = @(267383,267384); Ring = @(267574,267575) }
  'Shade' = @{ Back = 267934; Shoulder = 268005; Special = @(267377,267378); Ring = @(267576,267577) }
  'Soldier' = @{ Back = 267939; Shoulder = 268186; Special = @(267379,267380); Ring = @(267578,267579) }
  'Trader' = @{ Back = 267935; Shoulder = 268006; Special = @(267381,267382); Ring = @(267580,267581) }
}
$machine = [ordered]@{
  'Adventurer' = 266562; 'Agent' = 266563; 'Doctor' = 266564; 'Enforcer' = 266565
  'Engineer' = 266566; 'Fixer' = 266567; 'Keeper' = 266568; 'Bureaucrat' = 266569
  'Martial Artist' = 266570; 'Metaphysicist' = 266571; 'Nano Technician' = 266572
  'Soldier' = 266573; 'Shade' = 266574; 'Trader' = 266575
}
$melee = @{
  Bear = @(265237,265243); Mongoose = @(265146,265152); Panther = @(264936,264942)
  Viper = @(264978,264984); Wolf = @(265195,265201)
}
$ranged = @{
  Cobra = @(265279,265285); Hawk = @(265153,265159); Peregrine = @(265020,265026)
  Shark = @(265062,265068); Silverback = @(264853,264859); Tiger = @(265104,265110)
}

# 6 body pieces x 7 QLs = 42; + back + shoulder + special + 3 ring QLs = 48 (<=64).
$armorQl = 1,50,100,150,200,250,300
$weaponQl = 1,50,100,150,200,250,300
$ringQl = 100,200,300

function GearRows($prof, $pieces) {
  $rows = @()
  foreach ($p in $pieces) {
    $slot = [string]$p[3]
    foreach ($q in $armorQl) {
      $rows += [ordered]@{ Low = [int]$p[1]; High = [int]$p[2]; Ql = [int]$q; Price = (ArmorPrice $slot $q) }
    }
  }
  $ex = $extras[$prof]
  $back = [int]$ex.Back
  foreach ($q in $armorQl) {
    $rows += [ordered]@{ Low = $back; High = $back; Ql = [int]$q; Price = (ArmorPrice 'Back' $q) }
  }
  $sh = [int]$ex.Shoulder
  foreach ($q in $armorQl) {
    $rows += [ordered]@{ Low = $sh; High = $sh; Ql = [int]$q; Price = (ArmorPrice 'Shoulder' $q) }
  }
  # 6*7 + 7 + 7 = 56; special + 3 rings = 60.
  $sp = $ex.Special
  $rows += [ordered]@{ Low = [int]$sp[0]; High = [int]$sp[1]; Ql = 300; Price = (ArmorPrice 'Special' 300) }
  $rg = $ex.Ring
  foreach ($q in $ringQl) {
    $rows += [ordered]@{ Low = [int]$rg[0]; High = [int]$rg[1]; Ql = [int]$q; Price = (ArmorPrice 'Ring' $q) }
  }
  return $rows
}
function WeaponRows($table) {
  $rows = @()
  foreach ($name in ($table.Keys | Sort-Object)) {
    $ids = $table[$name]
    $low = [int]$ids[0]; $high = [int]$ids[1]
    foreach ($q in $weaponQl) {
      $rows += [ordered]@{ Low = $low; High = $high; Ql = [int]$q; Price = (WeaponPrice $q) }
    }
  }
  return $rows
}

$vendors = [ordered]@{}
foreach ($prof in $armor.Keys) {
  $id = [string]$machine[$prof]
  $rows = @(GearRows $prof $armor[$prof])
  if ($rows.Count -gt 64) { throw "armor $prof has $($rows.Count) lines" }
  $vendors[$id] = $rows
}
$vendors['266577'] = @(WeaponRows $melee)
$vendors['266576'] = @(WeaponRows $ranged)

$attuneL1 = @(269401,269412,269418,269407,269406,269415,293693)
$general = @()
foreach ($id in @(269909,269910,268170,269871,269880,301144)) {
  $q = NeedQl $id
  $general += [ordered]@{ Low = [int]$id; High = [int]$id; Ql = $q; Price = (NanoPrice $id) }
}
foreach ($id in $attuneL1) {
  $q = NeedQl $id
  $general += [ordered]@{ Low = [int]$id; High = [int]$id; Ql = $q; Price = 20 }
}
# Laser Tagging Device exists (259782). Box of Mech Ammunition does not.
$general += [ordered]@{ Low = 259782; High = 259782; Ql = 1; Price = 100 }
# Keep targeting stations at 25 until Randy says otherwise.
foreach ($id in @(265405,265406)) {
  $q = NeedQl $id
  $general += [ordered]@{ Low = [int]$id; High = [int]$id; Ql = $q; Price = 25 }
}
# All Ofab nanos go on the original General Vendor (266579) in one scrollable list.
# Profession panes below remain as convenience terminals; General is the full catalog.
$allNanoIds = @(
  287754,301854,301289,265449,279350,279346,265442,301937,301492,287556,
  301836,302224,302244,302218,302237,265446,302230,265453,301154,279380,301601,301119,
  301896,265454,301594,
  301898,302396,302400,265441,267645,265445,302413,265444,274374,301863,279377,302388,301868,301860,279375,
  267646,265451,267647,267648,301846,279373,267650,267638,274372,267636,301856,302255,267637,265443,
  265447,267118,267115,267268,267122,267267,301530,301522,267111,302289,302403,301900,302291,280053,301534,301526,301542,
  267644,267634,267641,267919,302248,267635,220622,302146,301082,267539,267537,267633,267643,267642,265448,267920,302151,302149,302155,279341,
  267270,267640,267631,267630,267629,267639,267632,302258,267273,302189,301889,267276,267274,267284,265452,300507,267282,
  265459,265462,265458,265460,265469,265468,265455,265457,265456,265465,302275,265471,265470,265464,265466,218167,302076,265461,265463,265450,265467
)
$seenNano = @{}
foreach ($row in $general) { $seenNano["$($row.Low)"] = $true }
foreach ($id in $allNanoIds) {
  if ($seenNano.ContainsKey("$id")) { continue }
  $seenNano["$id"] = $true
  $q = NeedQl $id
  $general += [ordered]@{ Low = [int]$id; High = [int]$id; Ql = $q; Price = (NanoPrice $id) }
}
$vendors['266579'] = @($general)

foreach ($key in @($vendors.Keys)) {
  $n = @($vendors[$key]).Count
  if ($n -lt 1) { throw "vendor $key has $n lines" }
  # General Vendor is the full nano catalog and may exceed 64; the client scrolls.
  if ($key -ne '266579' -and $n -gt 64) { throw "vendor $key has $n lines" }
}
$stockDoc = [ordered]@{
  Notes = 'Hub vending machines on playfield 6007. Randy house list: armor/weapon slot*QL then /10; nanos 100/250/500; attunement L1=20; laser tag=100. Mk 1 weapons. Armor QL steps 1,50,100,150,200,250,300. No Ofab shields. General Vendor 266579 holds general/HUD items plus ALL Ofab nanos in one scrollable list. Build-OfabVpShop.ps1'
  Vendors = $vendors
  GeneralNanos = @()
}
$stockPath = Join-Path $GameData 'OfabHubStock.json'
[IO.File]::WriteAllText($stockPath, (($stockDoc | ConvertTo-Json -Depth 6) + "`n"), $utf8)

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

function AttuneRows([int]$startSlot) {
  $rows = @(); $slot = $startSlot
  foreach ($id in $attuneL1) {
    $q = NeedQl $id
    $rows += [ordered]@{ Slot = $slot; LowId = [int]$id; HighId = [int]$id; Quality = $q; Price = 20 }
    $slot++
  }
  return $rows
}
function ShopStock($ids) {
  $rows = @(); $slot = 0
  foreach ($id in $ids) {
    $q = NeedQl $id
    $rows += [ordered]@{ Slot = $slot; LowId = [int]$id; HighId = [int]$id; Quality = $q; Price = (NanoPrice $id) }
    $slot++
  }
  $rows += @(AttuneRows $slot)
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

function ContentNpc($key, $identity, $instance, $name, $x, $z, $vendor, $spawnHash = $null) {
  $npc = [ordered]@{
    Key = $key
    Provenance = 'Unicorn Defence Hub playfield 6007. Build-OfabVpShop.ps1. Y 6.4 is the hub floor band, not a walked pin. Body 279184 = Shipping Manifest Terminal look.'
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
  if ($null -ne $spawnHash) { $npc.SpawnHash = [string]$spawnHash }
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
  @{ Name = 'Battle Station 10-30'; X = 248.5; Z = 189; SpawnHash = '1TTO' }
  @{ Name = 'Battle Station 31-60'; X = 241; Z = 196.5; SpawnHash = '31TO' }
  @{ Name = 'Battle Station 61-100'; X = 241; Z = 181.5; SpawnHash = '60T1' }
  @{ Name = 'Battle Station 101-150'; X = 225; Z = 196.5; SpawnHash = '10T1' }
  @{ Name = 'Battle Station 151-200'; X = 217.5; Z = 189; SpawnHash = '15T2' }
  @{ Name = 'Battle Station 201-214'; X = 225; Z = 181.5; SpawnHash = '17TO' }
  @{ Name = 'Battle Station 215-220'; X = 231; Z = 189; SpawnHash = '20TO' }
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
  $npc = ContentNpc $key 'ddogs-bs-queue' (2110920000 + $i) $q.Name $q.X $q.Z $null $q.SpawnHash
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
