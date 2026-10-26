# Writes the Ofab VP terminals into PlayfieldContent/655/Npcs.json.
# Item ids are the base (not Improved / Penultimate) pairs from itemnames. QL 1 and 300 ends were checked for
# Adventurer body (264202/264203) and Bear Mk 1 (265237/265243). Other pieces use the same low/high pair.
# VP price is a house rule: max(1, floor(QL * QL / 40)). Retail VP prices are not in the database.
# The shop pane shows 0 credits (sell modifier 0). The server charges the Price field as Victory Points.
param(
  [string]$Npcs = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData\PlayfieldContent\655\Npcs.json')
)
$ErrorActionPreference = 'Stop'
$utf8 = New-Object Text.UTF8Encoding $false

# name, low, high. Six pieces each. Slot names follow the item, not a single pattern.
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
    @('Headgear',264539,264540), @('Body',264557,264558), @('Sleeves',264551,264552),
    @('Gloves',264545,264546), @('Pants',264569,264570), @('Boots',264563,264564))
  'Soldier' = @(
    @('Helmet',264431,264432), @('Body',264449,264450), @('Sleeves',264443,264444),
    @('Gloves',264437,264438), @('Pants',264461,264462), @('Boots',264455,264456))
  'Trader' = @(
    @('Helmet',264251,264252), @('Body',264269,264270), @('Sleeves',264263,264264),
    @('Gloves',264257,264258), @('Pants',264275,264276), @('Boots',264281,264282))
}
# Each mark is its own QL 1-300 family (7 ids). Melee stocks QL 100 and 300 (60 lines).
# Ranged stocks QL 300 only (36 lines). QL 100 as well would be 72 lines, past a 64-slot shop pane.
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
function Vp([int]$ql) { return [Math]::Max(1, [int][Math]::Floor($ql * $ql / 40.0)) }
function StockArmor($pieces) {
  $qls = 1,25,50,100,150,200,250,300
  $slot = 0; $rows = @()
  foreach ($p in $pieces) {
    foreach ($ql in $qls) {
      $rows += [ordered]@{ Slot = $slot; LowId = [int]$p[1]; HighId = [int]$p[2]; Quality = $ql; Price = (Vp $ql) }
      $slot++
    }
  }
  return $rows
}
function StockWeapons($table, $qls) {
  $slot = 0; $rows = @()
  foreach ($name in ($table.Keys | Sort-Object)) {
    $ids = $table[$name]
    for ($m = 0; $m -lt 6; $m++) {
      $low = [int]$ids[$m * 2]; $high = [int]$ids[$m * 2 + 1]
      foreach ($ql in $qls) {
        $rows += [ordered]@{ Slot = $slot; LowId = $low; HighId = $high; Quality = $ql; Price = (Vp $ql) }
        $slot++
      }
    }
  }
  return $rows
}

$terminals = @()
foreach ($prof in $armor.Keys) { $terminals += [pscustomobject]@{ Key = ($prof.ToLower() -replace ' ',''); Name = "Ofab $prof Armor"; Stock = (StockArmor $armor[$prof]); Shop = $true } }
$terminals += [pscustomobject]@{ Key = 'melee'; Name = 'Ofab Melee Weapons'; Stock = (StockWeapons $melee @(100,300)); Shop = $true }
$terminals += [pscustomobject]@{ Key = 'ranged'; Name = 'Ofab Ranged Weapons'; Stock = (StockWeapons $ranged @(300)); Shop = $true }
$terminals += [pscustomobject]@{ Key = 'nano-ranged'; Name = 'Ofab Nanos (Ranged and Support)'; Stock = @(); Shop = $false }
$terminals += [pscustomobject]@{ Key = 'nano-melee'; Name = 'Ofab Nanos (Melee and Hybrid)'; Stock = @(); Shop = $false }

$built = @()
for ($i = 0; $i -lt @($terminals).Count; $i++) {
  $t = @($terminals)[$i]
  $col = $i % 6; $row = [Math]::Floor($i / 6)
  $identity = 'ddogs-ofab-nano'
  if ($t.Shop) { $identity = 'ddogs-ofab-vp' }
  $placement = 'ddogs:ofab:655:' + $t.Key
  $x = 3080 + ($col * 4)
  $z = 880 + ($row * 5)
  $npc = [ordered]@{
    Key = $placement
    Provenance = 'Ofab VP terminal at the Unicorn outpost landmark (Andromeda 3124 x 905). Y 35 is the nearby ICC buffbot height, not a measured floor. Build-OfabVpShop.ps1'
    ContentNpcIdentity = $identity
    PlayfieldId = 655
    InstanceId = 2110910000 + $i
    Name = $t.Name
    Position = @($x, 35, $z)
    Rotation = @(0, 0, 0, 1)
    Stats = [ordered]@{ '0' = 277352961; '1' = 1650; '4' = 6; '27' = 1650; '33' = 0; '47' = 1; '54' = 40; '59' = 1; '60' = 0; '64' = 0; '89' = 1; '156' = 137; '173' = 3; '174' = 3; '359' = 279184; '360' = 100; '368' = 0; '389' = 0; '455' = 0; '466' = 0; '660' = 0; '673' = 31 }
    Textures = @(0..4 | ForEach-Object { [ordered]@{ Place = $_; Id = 0 } })
    Meshes = @()
    Passive = $true
    Attackable = $false
    TickLiving = $true
    Presentation = [ordered]@{}
  }
  if ($t.Shop) {
    $npc.Vendor = [ordered]@{
      InstanceId = 3180010000 + $i
      TemplateId = 99634
      SpendsVictoryPoints = $true
      Stock = @($t.Stock)
    }
  }
  $built = $built + @(,$npc)
}

$doc = [IO.File]::ReadAllText($Npcs) | ConvertFrom-Json
$keep = @($doc.Npcs | Where-Object { $_.Key -notlike 'ddogs:ofab:*' })
$doc.Npcs = @($keep) + @($built)
[IO.File]::WriteAllText($Npcs, (($doc | ConvertTo-Json -Depth 12) + "`n"), $utf8)
Write-Output ("terminals=" + $terminals.Count + " armorLines=" + (StockArmor $armor['Soldier']).Count + " meleeLines=" + (StockWeapons $melee @(100,300)).Count + " rangedLines=" + (StockWeapons $ranged @(300)).Count)
