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
  # 18:30 pass: the TextureOverride left only the aura in game (Randy 18:18). No override until the material is proven.
  $t.PSObject.Properties.Remove('TextureOverrides')
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

# --- 2026-10-17 18:30 pass from Randy's in-game test (18:14-18:21) ---
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
# Supreme Collector: no texture override (body vanished, only the aura drew), plain dyna size, Fire (red) aura so the random
#   "Big" modifier (x3 HP, x1.3 size on top of x1.5) cannot roll; sane HP for an L10 dyna (Waste Collector L13 = ~150).
foreach ($t in @($nt.SCWS.Templates)) {
  $t.PSObject.Properties.Remove('TextureOverrides'); $t.PSObject.Properties.Remove('Textures')
  SetStat $t '360' 100; SetStat $t '1' 300; SetStat $t '27' 300
  SetProp $t 'DynaModifier' 'Fire'
}
# ICC Peacekeepers: the captured ICC Arbiter Guard (ABGU, same body 254118) carries no breed (4) or sex (59); ours drew as a plain
#   female human. Drop both to match the capture. Unverified in the client.
foreach ($k in 'OIUR', 'IPPC') { foreach ($t in @($nt.$k.Templates)) { $t.Stats.PSObject.Properties.Remove('4'); $t.Stats.PSObject.Properties.Remove('59') } }
# Leonora Marty: AO-U says she lost her card and wants to go shopping. Body was 26092 = "solitus male". Client monster record
#   203736 "deranged shopper" is the only shopper body in the RDB (guess that it is the bag-carrying shopper).
foreach ($t in @($nt.LEMY.Templates)) { SetStat $t '359' 203736; SetStat $t '64' 0; SetProp $t 'HasHeadMesh' $false; $t.Stats.PSObject.Properties.Remove('4'); $t.Stats.PSObject.Properties.Remove('59') }
# Crash site: HX1V/HXHR were made unattackable "dead" bodies. Randy expects live aliens: Zix and Cha'Heru, L10, attackable.
function LiveClone($donorKey, $name, $lvl) {
  $d = First $donorKey; $c = Clone $d; $c.Name = $name; $c.Level = $lvl; SetStat $c '54' $lvl
  $hp = [int]([int]$d.Stats.'1' * $lvl / [Math]::Max(1, [int]$d.Level)); if ($hp -lt 1) { $hp = 1 }; SetStat $c '1' $hp; SetStat $c '27' $hp
  $c.PSObject.Properties.Remove('Features'); $c
}
$t = LiveClone 'ZIXK' 'Alien Spider - Zix' 10; Prov $t 'ZIXK' 'Crash-site HX1V points, live Zix (Randy 18:15: "Dead" Zix could not be attacked)' $null; Put 'HX1V' $t
$t = LiveClone 'CRDY' "Specialist - Cha'Heru" 10; Prov $t 'CRDY' 'Crash-site HXHR points, live Cha''Heru (Randy 18:15)' $null; SetStat $t '1' 250; SetStat $t '27' 250; Put 'HXHR' $t  # CRDY scaled to L10 was 775 HP, too much for the L10 crash-site area
# Crickets: the cricket-looking aliens are client body 251782 alien_mini_bug_melee_unrooted = ACFJ "Scout - Jaax'Sinuh".
$t = LiveClone 'ACFJ' "Scout - Jaax'Sinuh" 10; Prov $t 'ACFJ' 'Crickets around the crashed ship (Randy 18:15). Count and spots guessed' $null; Put 'JXSC' $t
# Leet hill (YODE x20, untemplated pink, L10-13) is leets; GADP on the hill too. Leet King moves to the middle of the hill.
$le = First 'LE04'
$bands = foreach ($lvl in 10, 13) { $c = Clone $le; $c.Name = 'Leet'; $c.Level = $lvl; SetStat $c '54' $lvl; $hp = [int]([int]$le.Stats.'1' * $lvl / [Math]::Max(1, [int]$le.Level)); SetStat $c '1' $hp; SetStat $c '27' $hp; Prov $c 'LE04' 'Leet hill between the Princess and 3709.8x743.3 (Randy 18:21). Levels from the client spawn (10-13)' $null; $c }
SetProp $nt 'YODE' ([pscustomobject]@{ Templates = @($bands) })
$c = Clone $le; $c.Name = 'Leet'; $c.Level = 13; SetStat $c '54' 13; $hp = [int]([int]$le.Stats.'1' * 13 / [Math]::Max(1, [int]$le.Level)); SetStat $c '1' $hp; SetStat $c '27' $hp; Prov $c 'LE04' 'GADP spot on the leet hill (client L15, shown as a leet L13; guessed)' $null; Put 'GADP' $c
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 12) + "`n", $u)

$s = [IO.File]::ReadAllText($spath, $u) | ConvertFrom-Json
$proto = @($s.spawns | Where-Object { $_.hashText -eq '01V1' })[0]
function AddPoint($hashText, $x, $y, $z, $lmin, $lmax, $resp) {
  $near = @($s.spawns | Where-Object { $_.hashText -eq $hashText -and [Math]::Abs($_.position[0] - $x) -lt 0.5 -and [Math]::Abs($_.position[2] - $z) -lt 0.5 })
  if ($near.Count) { return }
  $p = $proto | ConvertTo-Json -Depth 8 | ConvertFrom-Json
  $p.hash = 0; $p.hashText = $hashText; $p.manifestHash = 0; $p.minLevel = $lmin; $p.maxLevel = $lmax; $p.respawnTime = $resp; $p.position = @($x, $y, $z); $p.additionalPoints = @()
  $s.spawns = @($s.spawns) + $p
}
foreach ($sp in $s.spawns) {
  if ($sp.hashText -eq 'LKNG') { $sp.position = @(3693.0, 31.6, 672.0) }                      # middle of the YODE hill
  if ($sp.hashText -eq 'MARC') { $sp.position = @(3470.0, 2.5, 690.0); $sp.radius = 40 }      # rollerrat flats (WSSC cluster), Wander
}
# Waste Collector camp tight around the boss so the junkyard reads as his camp (Randy 18:15: regulars not there).
# (19:30: the ring at the old junkyard spot is dropped; the boss moved to FMII and the fleas there became his camp.)
# Crickets around the crash site, between the HX1V/HXHR points.
foreach ($pt in @(@(3825.0, 4.0, 260.0), @(3845.0, 5.0, 235.0), @(3870.0, 6.0, 262.0), @(3880.0, 6.0, 245.0), @(3830.0, 3.0, 228.0), @(3858.0, 8.0, 280.0))) { AddPoint 'JXSC' $pt[0] $pt[1] $pt[2] 9 11 30 }
[IO.File]::WriteAllText($spath, ($s | ConvertTo-Json -Depth 8) + "`n", $u)

# Pink placeholders standing on top of a content NPC (the content NPC is the real one): remove that one spawn point.
$cpath = Join-Path $gd 'PlayfieldContent\6553\Npcs.json'; $content = [IO.File]::ReadAllText($cpath, $u) | ConvertFrom-Json
$real = @($content.Npcs) + @($content.MailTerminals) + @($content.StandaloneShops) | Where-Object { $_.Position }
$keys = @{}; foreach ($p in $nt.PSObject.Properties) { $keys[$p.Name] = 1 }
$kpath = Join-Path $gd 'SpawnSkips.json'; $sk = [IO.File]::ReadAllText($kpath, $u) | ConvertFrom-Json
foreach ($sp in $s.spawns) {
  if ($keys[$sp.hashText]) { continue }
  $best = $null; $bd = 1e9
  foreach ($r in $real) { $d = [Math]::Sqrt([Math]::Pow($r.Position[0] - $sp.position[0], 2) + [Math]::Pow($r.Position[2] - $sp.position[2], 2)); if ($d -lt $bd) { $bd = $d; $best = $r } }
  if ($bd -gt 3) { continue }
  $have = @($sk.removedPoints | Where-Object { $_.playfield -eq 6553 -and $_.hash -eq $sp.hashText })
  if ($have.Count) { continue }
  $name = if ($best.Name) { $best.Name } else { $best.Key }
  $sk.removedPoints = @($sk.removedPoints) + [pscustomobject][ordered]@{ playfield = 6553; hash = $sp.hashText; position = $sp.position; reason = ('on top of {0} (content, {1} m)' -f $name, [Math]::Round($bd, 2)) }
  Write-Host ('  removed pink {0} under {1} ({2:N2} m)' -f $sp.hashText, $name, $bd)
}
[IO.File]::WriteAllText($kpath, ($sk | ConvertTo-Json -Depth 8) + "`n", $u)

# --- 2026-10-17 19:30 pass (Randy 19:13) ---
# Supreme Collector belongs at "Pos: 3506.0, 941.8, 11.1" (AO x, z, y), the market-side spot. The pink FMII client point sits there
#   (3505.8, 11.6, 943.1): FMII becomes the boss point, and the junkyard point (3533.3, 733.7) is dropped.
$s = [IO.File]::ReadAllText($spath, $u) | ConvertFrom-Json
$s.spawns = @($s.spawns | Where-Object { -not ($_.hashText -eq 'SCWS' -and [Math]::Abs($_.position[0] - 3533.3) -lt 1 -and [Math]::Abs($_.position[2] - 733.7) -lt 1) })
$s.spawns = @($s.spawns | Where-Object { -not ($_.hashText -eq 'CMDI' -and $_.hash -eq 0 -and [Math]::Sqrt([Math]::Pow($_.position[0] - 3533.3, 2) + [Math]::Pow($_.position[2] - 733.7, 2)) -lt 15) })
foreach ($sp in $s.spawns) {
  if ($sp.hashText -eq 'FMII') { $sp.hashText = 'SCWS'; $sp.hash = 0; $sp.minLevel = 10; $sp.maxLevel = 10; $sp.respawnTime = 180; $sp.position = @(3506.0, 11.1, 941.8) }
}
# Fleas near the boss and near the IIV-X Advanced Docker (3515.8, 902.5) are Waste Collectors (Randy 19:13). Within 25 m of the boss:
#   CMDI L7 (his camp); the rest within 35 m of either spot: 01V1 L5.
foreach ($sp in $s.spawns) {
  if ($sp.hashText -notin 'JNME', 'VUFL', 'IEGA') { continue }
  $db = [Math]::Sqrt([Math]::Pow($sp.position[0] - 3506.0, 2) + [Math]::Pow($sp.position[2] - 941.8, 2))
  $dd = [Math]::Sqrt([Math]::Pow($sp.position[0] - 3515.8, 2) + [Math]::Pow($sp.position[2] - 902.5, 2))
  if ($db -le 25) { $sp.hashText = 'CMDI'; $sp.hash = 0; $sp.minLevel = 7; $sp.maxLevel = 7; $sp.respawnTime = 30 }
  elseif ($db -le 35 -or $dd -le 35) { $sp.hashText = '01V1'; $sp.hash = 0; $sp.minLevel = 5; $sp.maxLevel = 5; $sp.respawnTime = 30 }
}
[IO.File]::WriteAllText($spath, ($s | ConvertTo-Json -Depth 8) + "`n", $u)

# Mail terminals: the item-based MailTerminals static (template 37180) was never drawn by the client. Place them as content NPCs named
#   "Mail Terminal" with the computer-terminal body 279184 the merc Contract Terminals already use (drawn in game); the engine opens the
#   mailbox when a Mail Terminal NPC is used (NpcCharacter.TryOpenMail).
foreach ($m in @(@{ Pf = 6553; Id = 231769427; Pos = @(3464.1, 9.0, 805.6); Where = 'Arete Landing marketplace' }, @{ Pf = 4582; Id = 231760258; Pos = @(936.4, 47.0, 878.3); Where = 'ICC Shuttleport, beside Vendor Antonio Stacklund' })) {
  $cp = Join-Path $gd ("PlayfieldContent\{0}\Npcs.json" -f $m.Pf); $doc = [IO.File]::ReadAllText($cp, $u) | ConvertFrom-Json
  $doc.MailTerminals = @($doc.MailTerminals | Where-Object { $_.Key -notlike 'ddogs:mail:*' })
  $key = 'ddogs:mail-npc:{0}' -f $m.Pf
  $npcs = @($doc.Npcs | Where-Object { $_.Key -ne $key })
  $stats = [ordered]@{ '0' = 268964353; '1' = 1650; '4' = 6; '27' = 1650; '33' = 0; '47' = 1; '54' = 40; '59' = 1; '60' = 0; '64' = 0; '89' = 1; '156' = 137; '173' = 3; '174' = 3; '359' = 279184; '360' = 100; '368' = 0; '389' = 0; '455' = 0; '466' = 0; '660' = 0; '673' = 31 }
  $tex = foreach ($pl in 0..4) { [ordered]@{ Place = $pl; Id = 0 } }
  $npcs += [pscustomobject][ordered]@{ Key = $key; Provenance = ('ddogs mail terminal, {0}. Body 279184 computer terminal (as the Contract Terminals). Build-AreteFinish.ps1' -f $m.Where)
    ContentNpcIdentity = ('ddogs-mail-{0}' -f $m.Pf); PlayfieldId = $m.Pf; InstanceId = $m.Id; Name = 'Mail Terminal'; Position = $m.Pos; Rotation = @(0, -0.7071068, 0, 0.7071068)
    Stats = [pscustomobject]$stats; Textures = @($tex); Meshes = @(); Passive = $true; Attackable = $false; TickLiving = $true; Presentation = [pscustomobject]@{} }
  $doc.Npcs = $npcs
  [IO.File]::WriteAllText($cp, ($doc | ConvertTo-Json -Depth 12) + "`n", $u)
}
# --- 2026-10-18 00:10 pass (Randy 00:05) ---
# Mail terminal rotation above: yaw -90 deg, the same heading as the Arete insurance terminal static (template 300813 at 3464, 9, 811,
#   Dynels.dat heading 0,-0.7071,0,0.7071) and the statics beside the 4582 spot. Identity rotation stood sideways.
# IIV-X Advanced Docker: one dyna, not four. The original PO51 point (3515.2, 5.5, 904.2) stays the Docker; the three 32-V Docker points
#   Build-ShoulderpadsJunkyard.ps1 also turned into IIVX (within 14 m) go back to 32DO L3, 30 s.
#   Dyna: boss| camp key (glow + 150% size) and HP 240 like the Cleanmeister (L4). DYAR 5-7 loot and 180 s respawn were already set.
#   Level 4 stays a guess: neither the wiki nor the AO-Universe Arete guide gives his level.
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
foreach ($t in @($nt.IIVX.Templates)) {
  $t.ContentProvenance | Add-Member -Force NoteProperty CampKey 'boss|IIV-X Advanced Docker|4'
  SetStat $t '1' 240; SetStat $t '27' 240
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 12) + "`n", $u)
$s = [IO.File]::ReadAllText($spath, $u) | ConvertFrom-Json
foreach ($sp in $s.spawns) {
  if ($sp.hashText -ne 'IIVX') { continue }
  if ([Math]::Abs($sp.position[0] - 3515.2) -lt 1 -and [Math]::Abs($sp.position[2] - 904.2) -lt 1) { continue }
  $sp.hashText = '32DO'; $sp.hash = 858932303; $sp.minLevel = 3; $sp.maxLevel = 3; $sp.respawnTime = 30
}
[IO.File]::WriteAllText($spath, ($s | ConvertTo-Json -Depth 8) + "`n", $u)
Write-Host 'Build-AreteFinish done'
