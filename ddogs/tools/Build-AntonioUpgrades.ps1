# Build-AntonioUpgrades.ps1 - Antonio Stacklund's Adaptation Factory upgrades (Randy 2026-10-19 12:49 ET).
# Sources: AO-Universe "Antonio Stacklund's Upgrades" and Arete Landing spoiler #13; AOWiki (wiki.aodb.us) "ICC Shuttleport Tradeskills"
# (skills: Computer Literacy 20, Polished Eliminator Weapon Smithing 8). Item ids from the client item dump (Auno/AOSetups names match).
# What it does (data only, rerunnable):
#   1. GameData/Tradeskills.antonio.json: 19 part recipes (Factory + raw part) and 19 final recipes (part + worn weapon), both orders,
#      DeleteSource/DeleteTarget (the Factory is used up each combine). Nothing for these existed in Tradeskills.cellao.json.
#   2. ItemTemplates.json: drop hashes for the raw parts that no mob dropped.
#   3. NpcTemplates.json LootTable rows (drop %, guessed 25) on the mobs the guides name: Minibull (Bone Plates, Large Skull, Leather Hide),
#      Desert/Island Reet (Reet Beak), Leet (Spinal Section), Garbage Flea/Shore Snake (Venom Sample, Fluid Sample on fleas),
#      Waste Collector (Fluid Sample, Fuel Chamber, Compression Chamber), 32-V Docker/Cargo Droid (Power Supply, Optical Enhancer),
#      Cliff Malle (Large Skull, Bone Plates). Rollerrat Intestines (QRRI) already dropped.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
Remove-TypeData System.Array -ErrorAction SilentlyContinue
$u = New-Object Text.UTF8Encoding $false
$gd = [IO.Path]::GetFullPath($GameData)
# ---- 1. recipes
$F = 248306
$parts = @(   # name, raw item, part item
  @('Reet Beak Whistling Plug', 248328, 248327), @('Adapted Power Supply', 248307, 248308), @('Reinforced Hammer Head', 248330, 248331),
  @('Leather Grip Tape', 248325, 248326), @('Bone Cross Guard', 248323, 248324), @('Venom Cartridge', 248322, 248321),
  @('Chemical Tempering Fluid', 248315, 248316), @('Bone Stock', 248310, 248312), @('Resonating Chamber', 248319, 248320),
  @('Recalibrated Scope Unit', 248318, 248317), @('Gut Bowstring', 248333, 248332), @('Pneumatic Compression Chamber', 248334, 248335))
$finals = @(  # name, part, weapon/target, result, skill stat, skill points
  @('Wailing Bat', 248327, 121564, 248341), @('Surge Baseball Bat', 248308, 121564, 248355), @('Balanced War Hammer', 248331, 218406, 248353),
  @('Grip Blade', 248326, 218403, 248352), @('Strong Oak Bo', 248326, 121565, 301071), @('Spine Sword', 248324, 218404, 248351),
  @('Injector Dagger', 248321, 218395, 248350), @('Hand Staff of Naja', 248321, 302163, 302602),
  @('Electrical Surge Pistol', 248308, 121567, 248343), @('Electrical Surge Pistol (Adventuring)', 248308, 218405, 248343),
  @('BO-18 (Blue Offset)', 248316, 121569, 248347), @('Stabilized Silent Spitter', 248312, 121571, 248344),
  @('Wave Heated Plasma Gun', 248320, 248340, 248349), @('Cerset Zapper Rifle', 248317, 121568, 248348),
  @('Shaolin Sporting Bow', 248332, 248339, 248354), @('Nizno''s Bomb Thrower', 248335, 248338, 248346),
  @('Range Meter', 248308, 248317, 248374), @('Poison Injector Bracelet', 248321, 248335, 248375), @('Leather Vest', 248332, 248325, 248373))
$rec = New-Object Collections.ArrayList
function Add-Recipe($name, $src, $tgt, $res, $stat, $pts, $delSrc) {
  foreach ($pair in @(@($src, $tgt, $name), @($tgt, $src, "$name (swapped)"))) {
    if ($pair[2] -like '*swapped*' -and -not $delSrc) { continue }   # the Screwdriver is only ever the source
    [void]$rec.Add([ordered]@{ Name = $pair[2]; Source = @([int]$pair[0]); Target = @([int]$pair[1]); Result = @([int]$res, [int]$res)
      Skills = @(, @([int]$stat, [int]$pts)); DeleteSource = $delSrc; DeleteTarget = $true; MinSourcePct = 0 })
  }
}
# skill points are percent of the target QL (items are QL 1): 2000 = 20, 800 = 8
foreach ($p in $parts) { Add-Recipe ("Antonio: $($p[0])") $F $p[1] $p[2] 161 2000 $true }
foreach ($f in $finals) { Add-Recipe ("Antonio: $($f[0])") $f[1] $f[2] $f[3] 161 2000 $true }
Add-Recipe 'Antonio: Polished Eliminator (Screwdriver)' 150922 121570 248345 158 800 $false
$json = ConvertTo-Json -InputObject ([object[]]$rec.ToArray()) -Depth 6
[IO.File]::WriteAllText((Join-Path $gd 'Tradeskills.antonio.json'), $json + "`n", $u)
# ---- 2. drop hashes
$items = [ordered]@{ QBPL = 248310; QFLS = 248315; QOPE = 248318; QFUC = 248319; QVSA = 248322; QSPS = 248323; QLHD = 248325; QRBK = 248328; QLSK = 248330; QCCH = 248334 }
$ipath = Join-Path $gd 'ItemTemplates.json'
$lines = [IO.File]::ReadAllLines($ipath, $u) | Where-Object { $_ -notmatch ('^\s*"(' + ($items.Keys -join '|') + ')":') }
$add = foreach ($k in $items.Keys) { '    "{0}": {{"Templates": [{1},{1}]}},' -f $k, $items[$k] }
if ($lines[0].Trim() -ne '{') { throw 'ItemTemplates.json: unexpected first line' }
[IO.File]::WriteAllLines($ipath, [string[]](@($lines[0]) + $add + $lines[1..($lines.Count - 1)]), $u)
# ---- 3. loot rows by template hash (all bands)
$drops = [ordered]@{
  WTSO = @('QBPL', 'QLSK', 'QLHD'); DERT = @('QRBK'); ISRE = @('QRBK'); YODE = @('QSPS'); GADP = @('QSPS'); FDQO = @('QSPS')
  VUFL = @('QVSA', 'QFLS'); FCTO = @('QVSA', 'QFLS'); JNME = @('QVSA', 'QFLS'); SRSK = @('QVSA')
  '01V1' = @('QFLS', 'QFUC', 'QCCH'); CMDI = @('QFLS', 'QFUC', 'QCCH'); SOCW = @('QFLS', 'QFUC', 'QCCH'); STCW = @('QFLS', 'QFUC', 'QCCH'); WTCO = @('QFLS', 'QFUC', 'QCCH')
  '32DO' = @('QPWS', 'QOPE'); CADR = @('QOPE'); CIMA = @('QLSK', 'QBPL') }
$npath = Join-Path $gd 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
$all = @($items.Keys) + 'QPWS'
$done = @()
foreach ($h in $drops.Keys) {
  $e = $nt.$h; if (-not $e) { $done += "$h missing"; continue }
  foreach ($t in @($e.Templates)) {
    $keep = @($t.LootTable | Where-Object { $_ -and $_.Hash -notin $drops[$h] })
    $new = foreach ($d in $drops[$h]) { [pscustomobject]@{ Hash = $d; Repeats = 1; Chance = 25; LevelMod = 0 } }
    $t | Add-Member -Force NoteProperty LootTable @($keep + @($new))
  }
  $done += "$h x$(@($e.Templates).Count)"
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 12) + "`n", $u)
"Build-AntonioUpgrades: $($rec.Count) recipes; loot $($done -join ', ')"