# ddogs/tools/Build-AreteFinish.ps1 - Randy's remaining Arete list (2026-10-17). Run after Build-AreteLiveFixes.ps1 (Build-NpcTemplates.ps1 calls it last).
# Sources: wiki.aodb.us Arete Landing (dyna list: Supreme Collector of Waste "northeast corner", Cleanmeister "around the corner from Flint"),
#   AO-Universe Arete guide (Barry the Food Vendor, Bronto Burger 30 cr; Marco Spida 3407.7x831.0, profession Nanoprogram Containers),
#   items.dat values (containers 1500, Bronto Burger 130623 = 29), itemnames (Omni-Armed Forces set 203445-203449/203530).
# Guessed: SOCW camp = Supreme Collector (client L13 boss + CMDI x7 L7 + STCW L13 around it), VJTN = Barry, IPPC = ICC Peacekeeper,
#   rifle choice (OT M500 SWAT Rifle), gift-box meshes for the Chocolates/Flowers tables, Barry's and the Peacekeepers' looks.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
Remove-TypeData System.Array -ErrorAction SilentlyContinue
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData)
$npath = Join-Path $gd 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
function Clone($o) { $o | ConvertTo-Json -Depth 10 | ConvertFrom-Json }
function SetProp($o, $k, $v) { $o | Add-Member -Force NoteProperty $k $v }
function SetStat($t, $k, $v) { $t.Stats | Add-Member -Force NoteProperty $k $v }
function Prov($t, $donor, $src, $camp) {
  $p = [ordered]@{ Guessed = $true; Confirmed = $false; Donor = $donor; Source = $src }
  if ($camp) { $p.CampKey = $camp }
  SetProp $t 'ContentProvenance' ([pscustomobject]$p)
}
function First($k) { @($nt.$k.Templates) | Sort-Object Level | Select-Object -Last 1 }
function Put($k, $t) { SetProp $nt $k ([pscustomobject]@{ Templates = @($t) }) }
function Stock([int[]]$ids) { foreach ($i in $ids) { [pscustomobject][ordered]@{ LowId = $i; HighId = $i; Quality = 1 } } }

# 1. Supreme Collector of Waste: dyna (glow, size, 5-7 DYAR loot already, 180 s respawn already on the SCWS spawn at 4067.5, 1.0, 666.6).
foreach ($t in @($nt.SCWS.Templates)) { $t.ContentProvenance | Add-Member -Force NoteProperty CampKey 'boss|Supreme Collector of Waste|13' }
#    The pinks around him (CMDI x7 L7, STCW L13) become Waste Collectors (01V1 body, loot and Antonio parts).
$wc = First '01V1'
foreach ($p in @(@('CMDI', 7), @('STCW', 13))) {
  $t = Clone $wc; $lvl = $p[1]
  $t.Name = 'Waste Collector'; $t.Level = $lvl; SetStat $t '54' $lvl; SetStat $t '359' 17714; SetStat $t '360' 92; SetStat $t '455' 0
  $hp = [int]([int]$wc.Stats.'1' * $lvl / [Math]::Max(1, [int]$wc.Level)); SetStat $t '1' $hp; SetStat $t '27' $hp
  Prov $t '01V1' ("Waste Collector around the Supreme Collector (SOCW camp, 4067,667). Client pink {0} L{1} mapped to Waste Collector: guessed" -f $p[0], $lvl) $null
  Put $p[0] $t
}

# 2. Cleanmeister boss (CMBS, 3544.7, 5.3, 865.3): dyna, so the 150% dyna size and the modifier size apply on top of scale 170.
foreach ($t in @($nt.CMBS.Templates)) { $t.ContentProvenance | Add-Member -Force NoteProperty CampKey 'boss|Cleanmeister Intelligence Robot|4' }

# 3. Guard-spot pinks IPPC x4 (L40) -> ICC Peacekeeper, same look as OIUR.
$t = Clone (First 'OIUR'); $t.Level = 40; SetStat $t '54' 40
$hp = [int]([int]$t.Stats.'1' * 40 / [Math]::Max(1, [int](First 'OIUR').Level)); SetStat $t '1' $hp; SetStat $t '27' $hp
Prov $t 'OIUR' 'ICC Peacekeeper at the IPPC guard spots (3378.8,814.0 / 3421.8,781.2 / 3443.2,901.8 / 3466.9,782.2). Mapping and look guessed' $null
Put 'IPPC' $t

# 4. Barry the Food Vendor (VJTN, 3516.7, 7.0, 827.0: due west of Desmond on the raised stall). Content shop item 248368 (sell 105%).
$t = Clone (First 'FPBY'); $t.Name = 'Barry the Food Vendor'; $t.Level = 10; SetStat $t '54' 10
SetProp $t 'Features' @('NoCombat')
SetProp $t 'Equipment' @(, @(248368, 248368))
#   Beer Jug, Fresh Water, Bronto Burger, Bronto Cheese, Choc Chip Muffin, Coffee, Double Bronto Cheese, Ice-Coffee Can, Joka-Poka, Vanilla Ice-cream
SetProp $t 'ShopStock' @(Stock @(130621, 130593, 130623, 130624, 130581, 130612, 130625, 130606, 130602, 130603))
Prov $t 'FPBY' 'Barry the Food Vendor (AO-Universe: west of Desmond down the street, floating food vehicle). VJTN mapping and look guessed; menu from Randy' $null
Put 'VJTN' $t

# 6. Tasty Chocolates / Romantic Flowers tables: gift-box meshes instead of the alien drone 253599 (no chocolate/flower mesh in the client monster table).
foreach ($p in @(@('SPAU', 289979), @('SLPW', 294206))) { foreach ($t in @($nt.($p[0]).Templates)) { SetStat $t '359' $p[1]; SetStat $t '360' 60 } }

# 7. Omni-AF Privates: Omni-Armed Forces armor set + OT M500 SWAT Rifle.
$oaf = @(@(203447, 203447), @(203449, 203449), @(203530, 203530), @(203445, 203445), @(203446, 203446), @(203448, 203448), @(124350, 124351))
foreach ($k in 'LUSN', 'KLHG', 'DART') { foreach ($t in @($nt.$k.Templates)) { SetProp $t 'Equipment' $oaf } }

# 8. Marco Spida (ONRP, 3407.7, 9.4, 831.3, the NANO room): 14 profession Nanoprogram Containers. items.dat value 1500;
#    shop item 46521 sells at 90%, so the window shows 1,350 at low Computer Literacy.
foreach ($t in @($nt.ONRP.Templates)) {
  SetProp $t 'Equipment' @(, @(46521, 46521))
  SetProp $t 'ShopStock' @(Stock (@(248255..248266) + @(300892, 300893)))
}

[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 12) + "`n", $u)

# Desmond takes the burger Barry sells: QBRG now points at Bronto Burger 130623 (was quest copy 297042, which has no price).
$ipath = Join-Path $gd 'ItemTemplates.json'
$iraw = [IO.File]::ReadAllText($ipath, $u)
$iraw = $iraw -replace '"QBRG": \{"Templates": \[297042,297042\]\}', '"QBRG": {"Templates": [130623,130623]}'
[IO.File]::WriteAllText($ipath, $iraw, $u)
# --- 2026-10-17 10:40 pass from Randy's reference shots ---
# Supreme Collector: rust/orange skin = texture 95885 (same UV layout as the Waste Collector texture 17318, red-brown); placed at FRAG in the
#   junkyard among the Waste Collectors (client spawn L10). Mesh 17316 has one material, 'Material #22' (floatingpicker2.png); the SCFU override puts 95885 on it. Unknown1/2 = 0: guessed.
foreach ($t in @($nt.SCWS.Templates)) {
  $t.Level = 10; SetStat $t '54' 10
  $t.PSObject.Properties.Remove('Textures')
  SetProp $t 'TextureOverrides' @([pscustomobject][ordered]@{ Material = 'Material #22'; Texture = 95885; Unknown1 = 0; Unknown2 = 0 })
  $t.ContentProvenance.CampKey = 'boss|Supreme Collector of Waste|10'
  $t.ContentProvenance.Source = 'Randy reference shot: rust/orange collector among Waste Collectors in the junkyard. Spot FRAG (3533.3, 3.4, 733.7, client L10). Texture 95885 on Material #22 (RDB match to collector texture 17318)'
}
#   The old east spot (SOCW, 4067.5, 1.0, 666.6) keeps its Waste Collector camp: SOCW is a plain Waste Collector L13.
$t = Clone $wc; $t.Name = 'Waste Collector'; $t.Level = 13; SetStat $t '54' 13; SetStat $t '359' 17714; SetStat $t '360' 92; SetStat $t '455' 0
$hp = [int]([int]$wc.Stats.'1' * 13 / [Math]::Max(1, [int]$wc.Level)); SetStat $t '1' $hp; SetStat $t '27' $hp
Prov $t '01V1' 'East-side SOCW camp centre after the boss moved to FRAG: Waste Collector, guessed' $null
Put 'SOCW' $t
# ICC Peacekeeper: ICC Shuttle Guard body 254118 + head 40627 at scale 105 (dark green/teal helmet suit with rifle, Randy's shot).
foreach ($k in 'OIUR', 'IPPC') { foreach ($t in @($nt.$k.Templates)) { SetStat $t '359' 254118; SetStat $t '64' 40627; SetStat $t '360' 105; SetProp $t 'HasHeadMesh' $true } }
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 12) + "`n", $u)

# Spawns (Playfields is gitignored; rerun after re-extract): boss to FRAG, SOCW back to its own hash.
$spath = Join-Path $gd 'Playfields\6553\Spawns.json'
$s = [IO.File]::ReadAllText($spath, $u) | ConvertFrom-Json
foreach ($sp in $s.spawns) {
  if ($sp.hashText -eq 'SCWS' -and $sp.position[0] -gt 4000) { $sp.hashText = 'SOCW'; $sp.respawnTime = 33 }
  if ($sp.hashText -eq 'FRAG') { $sp.hashText = 'SCWS'; $sp.hash = 0; $sp.minLevel = 10; $sp.maxLevel = 10; $sp.respawnTime = 180 }
}
[IO.File]::WriteAllText($spath, ($s | ConvertTo-Json -Depth 8) + "`n", $u)

# Barry's greeting (Randy's shot) with the shop reply.
$kd = Join-Path $gd 'Knubot'
$barry = [ordered]@{ Notes = "Barry the Food Vendor (VJTN). Greeting and replies from Randy's reference shot. Generated by ddogs/tools/Build-AreteFinish.ps1."; Npcs = @('VJTN'); Openers = @(@{ Say = 'hello' })
  Replies = [ordered]@{ sell = [ordered]@{ Text = 'What do you have to sell?'; Goto = 'shop' }; bye = [ordered]@{ Text = 'Goodbye'; Goto = 'bye' } }
  Lines = [ordered]@{ hello = [ordered]@{ Text = "Step on up, stranger. I've got the delicous, meaty goodness that you crave."; Replies = @('sell', 'bye') }
    shop = [ordered]@{ Text = '{shop}' }; bye = [ordered]@{ Text = '{close}' } } }
[IO.File]::WriteAllText((Join-Path $kd 'BarryFoodVendor.json'), ($barry | ConvertTo-Json -Depth 8) + "`n", $u)
Write-Host 'Build-AreteFinish done'