# ddogs/tools/Build-GuessedContent.ps1 - best-guess fills for client spawn points with no capture (approved "option A").
# Every template made here carries ContentProvenance.Guessed = true plus the donor it was cloned from, so it can be replaced
# when real data turns up. Names and levels come from the guides; the look/stats come from a donor template of the same kind.
# Sources: aquarius.aodb.us/guides/nascence (mob lists per zone), wiki.aodb.us Nascence_Training_Grounds, AO-Universe Nascence guide.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$npath = Join-Path ([IO.Path]::GetFullPath($GameData)) 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
# code, name, minL, maxL, donor, source note
$g = @(
  # PF 4313 Nascence Training Ground (aquarius guide: "Mobs: Shadoleet 1, Shining Coiler 1-2, Deceitful Weaver 1-3, ...")
  @('SICL','Shining Coiler',1,2,'SLCM','aquarius TG'), @('DCWA','Deceitful Weaver',1,3,'WROM','aquarius TG'),
  @('DOWL','Shadowleet',1,1,'SWSI','aquarius TG'), @('LUCH','Lurking Chimera',3,4,'LLBE','aquarius TG'),
  @('BEGR','Beit Gras',4,4,'LLBE','aquarius TG'), @('AGGL','Massive Waggler',4,4,'SWSI','aquarius TG'),
  @('HUPD','Hungry Predator',3,5,'PDST','aquarius TG'), @('PIMR','Primitive Mortiig',6,6,'HIAT','aquarius TG'),
  @('PODR','Pond Dryad',6,6,'ALAH','aquarius TG'), @('CRIP','Crippler Fiend',5,6,'CIOG','aquarius TG'),
  @('HCOF','Heckler of Nature',8,8,'MOOM','Randy 2026-10-14: rock-pile Heckler. TG only; 4311 Metals/Stones/Earth stay captured'),
  @('HCOE','Heckler of Evolution',5,5,'MOOM','Randy 2026-10-14: quest Heckler, drops 6 Delicate Metal Devices'),
  @('TEPD','Tempterus',6,7,'EMPT','aquarius TG faction'), @('WAOW','Watcher of Woods',8,8,'TEMP','aquarius TG faction'),
  @('GROF','Guardian of Crystals',8,8,'EMPT','aquarius TG faction'), @('CTTC','Celeth-El, the Crystal Guardian',10,10,'EMPT','aquarius TG quest'),
  @('EDTH','El-Domo the Forest Ranger',10,10,'TEMP','aquarius TG quest'), @('IMDR','Imp Disruptor',20,20,'PDST','aquarius TG unique'),
  @('SIMR','Silvertail Mare',2,2,'SWSI','Randy 2026-10-14: pale ghost horse, empty hash SIMR'),
  @('BESI','Beit Silvertail',3,3,'SWSI','Randy 2026-10-14: pale ghost horse, added near SIMR'),
  @('PRAG','The Predator Rager',7,7,'PDST','Randy 2026-10-14: named darker panther, added near Hungry Predators'),
  @('TDVA','The Devastator',12,12,'HIAT','Randy 2026-10-14: named brute in the rocky southeast, added near Primitive Mortiigs'),
  @('YOSI','Young Silvertail',2,2,'SWSI','Drake psychic test, grove southwest of El-Domo'),
  @('SCDR','Scientist Drake Rodriguez',50,50,'HIAT','PF 4313 tower, hash SCDR'),
  @('STSH','Scientist Sarah Willis',50,50,'HIAT','PF 4313 beside Imp Disruptor'),
  @('GSTD','Assistant Brad Willis',20,20,'HIAT','PF 4313 guard outside Sarah''s cave'),
  @('_RES','NASCENCE_AUTONAME_researcher_MALE',50,50,'HIAT','PF 4313 researcher'),
  @('E_RE','NASCENCE_AUTONAME_researcher_FEMALE',50,50,'HIAT','PF 4313 researcher'),
  @('NURS','Training Ground Nurse',50,50,'HIAT','PF 4313 empty L50, name guessed from hash NURS'),
  @('SUJU','Jobe Researcher',50,50,'HIAT','PF 4313 empty L50 near Drake, role guessed'),
  @('DCRB','Jobe Researcher',50,50,'HIAT','PF 4313 empty L50 at the second building, role guessed'),
  @('SDSM','Sundance Smith - J.A.M.E. Depot',50,50,'HIAT','PF 4313 vendor, placement guessed near the lower bridge')
  # PF 4311 Nascence Wilds / Brawl
  @('ALAN','Calan-Cur',36,40,'RAIG','aquarius Wilds; CONFIRMED by Randy live screenshot 2026-10-14'),
  @('REMU','Mesut-Ra',38,38,'ESUT','Randy live screenshot 2026-10-14 (by Two Mountains); look = captured Mesut-Ra ESUT'),
  @('THRO','Ran-Roth-Or',110,110,'ORAD','aquarius: unique L110'), @('LFOH','Lord of the Void',250,250,'MOOM','aquarius: unique L250+'),
  # PF 4312 Nascence Swamp
  @('XAIY','Malah-Ana',32,38,'ALAH','aquarius Swamp: Malah-Anas 32-38'), @('MOCA','Hiathlin Prime',22,25,'ATHL','aquarius: Hiathlins Prime 22-25'),
  @('DIWE','Direful Weaver',40,40,'VIWE','aquarius Swamp: Direful and Morbid Weavers 40'), @('MOWA','Morbid Weaver',43,45,'VIWE','aquarius Swamp')
)
$n = 0
foreach ($x in $g) {
  $code, $name, $lo, $hi, $don, $src = $x
  if ($nt.$code -and -not $nt.$code.Templates[0].ContentProvenance.Guessed) { continue }  # real data wins
  if (-not $nt.$don) { throw "donor $don missing" }
  $d = $nt.$don.Templates[0]; $dl = [math]::Max(1, [int]$d.Level)
  $b = $d | ConvertTo-Json -Depth 8 | ConvertFrom-Json
  $lvl = [int][math]::Ceiling(($lo + $hi) / 2)
  $b.Name = $name; $b.Level = $lvl; $b.Stats.'54' = $lvl
  $hp = [int][math]::Max(30, [double]$d.Stats.'1' * $lvl / $dl)
  $b.Stats.'1' = $hp; if ($b.Stats.'27') { $b.Stats.'27' = $hp }
  $b | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = ($code -notin 'ALAN','REMU'); Confirmed = ($code -in 'ALAN','REMU'); Donor = $don; Source = $src; Levels = "$lo-$hi" })
  if ($code -eq 'THRO') { $b | Add-Member -Force NoteProperty PatrolWaypoints @(@(170,106.6,1370),@(179,106.9,1421),@(164,109,1469),@(157,110,1517),@(186,107,1565),@(209,106.1,1612),@(208,106.1,1669),@(194,106.3,1717),@(198,105.4,1765),@(235,105.5,1799),@(272,90,1780),@(316,78,1749),@(364,66,1718),@(427,53.8,1718),@(492,53.8,1725),@(524,56.5,1765),@(540,73.5,1780)); if ($b.Features) { $b.Features = @($b.Features | Where-Object { $_ -ne 'Wander' }) } }  # Randy 2026-10-14: yellow patrol path
  $nt | Add-Member -Force NoteProperty $code ([pscustomobject]@{ Templates = @($b) }); $n++
}
# Randy's 2026-10-14 Training Ground screenshots. Mesh ids are captured MonsterData unless noted.
# Confirmed = the screenshot matches that captured creature family. Guessed mesh stays replaceable.
# Stat 52 is kill XP added on top of the level KillAward. Captured 4311 hecklers (MOOM/MSOF/MLOF) are not in this table.
$blue = 277352961
$look = @{
  DCWA = @{ Name='Deceitful Weaver'; Md=209354; Scale=39; Fam=0; Ok=$true; Note='spider weaver, captured Weaver of Malice' }
  SICL = @{ Name='Shining Coiler'; Md=30252; Scale=80; Fam=27; Ok=$true; Note='green snake, Shore/Giant Snake mesh' }
  DOWL = @{ Name='Shadowleet'; Md=17655; Scale=75; Fam=36; Ok=$true; Note='small brown leet' }
  LUCH = @{ Name='Lurking Chimera'; Md=209173; Scale=65; Fam=187; Ok=$true; Note='small dark crawler, Barking Chimera mesh' }
  HUPD = @{ Name='Hungry Predator'; Md=209022; Scale=97; Fam=207; Ok=$true; Note='black panther, Predator Striker mesh' }
  TEPD = @{ Name='Tempterus'; Md=209189; Scale=238; Fam=202; Ok=$true; Note='small orange flyer, captured Tempterus' }
  CRIP = @{ Name='Crippler Fiend'; Md=209333; Scale=90; Fam=0; Ok=$true; Note='black winged bat, captured Crippler of Growth' }
  IMDR = @{ Name='Imp Disruptor'; Md=40515; Scale=90; Fam=0; Ok=$true; Note='winged imp, captured Corrupting Imp' }
  HCOF = @{ Name='Heckler of Nature'; Md=214982; Scale=100; Fam=0; Xp=756; Ok=$true; Note='rock-pile Heckler. XP stat 52 makes the kill about 4x a normal level 8. 4311 hecklers untouched' }
  HCOE = @{ Name='Heckler of Evolution'; Md=214982; Scale=100; Fam=0; Xp=564; Ok=$true; Note='rock-pile quest Heckler. XP about 4x a normal level 5. Drops 6 Delicate Metal Devices' }
  WAOW = @{ Name='Watcher of Woods'; Md=214067; Scale=100; Fam=0; Ok=$true; Note='slim armored humanoid, captured Watcher Aban Wei-Nuir mesh' }
  SIMR = @{ Name='Silvertail Mare'; Md=208922; Scale=100; Fam=172; Ok=$true; Note='pale ghost horse, Swift Silvertail mesh' }
  BESI = @{ Name='Beit Silvertail'; Md=208922; Scale=90; Fam=172; Ok=$true; Note='pale ghost horse, Swift Silvertail mesh' }
  PRAG = @{ Name='The Predator Rager'; Md=209022; Scale=130; Fam=207; Ok=$true; Note='larger darker panther, Predator Striker mesh' }
  YOSI = @{ Name='Young Silvertail'; Md=208922; Scale=55; Fam=172; Social=$true; Ok=$true; Note='Drake test, small silvertail' }
  SCDR = @{ Name='Scientist Drake Rodriguez'; Md=26092; Scale=100; Fam=0; Social=$true; Head=40694; Ok=$true; Note='quest giver. Body is the captured Jobe scientist mesh' }
  STSH = @{ Name='Scientist Sarah Willis'; Md=26092; Scale=100; Fam=0; Social=$true; Head=40694; Ok=$false; Note='friendly, beside the Imp Disruptor. Face guessed' }
  GSTD = @{ Name='Assistant Brad Willis'; Md=26097; Scale=100; Fam=0; Social=$true; Ok=$false; Note='friendly guard outside the imp cave. Body is Guardian of the Weak' }
  _RES = @{ Name='NASCENCE_AUTONAME_researcher_MALE'; Md=26092; Scale=100; Fam=0; Social=$true; Head=40694; Ok=$false; Note='exact autoname. Body guessed from Drake''s scientist mesh' }
  E_RE = @{ Name='NASCENCE_AUTONAME_researcher_FEMALE'; Md=26092; Scale=100; Fam=0; Social=$true; Head=40694; Ok=$false; Note='exact autoname. Body guessed from Drake''s scientist mesh' }
  NURS = @{ Name='Training Ground Nurse'; Md=26092; Scale=100; Fam=0; Social=$true; Ok=$false; Note='empty L50 hash NURS, role guessed' }
  SUJU = @{ Name='Jobe Researcher'; Md=26092; Scale=100; Fam=0; Social=$true; Ok=$false; Note='empty L50 near Drake, role guessed' }
  DCRB = @{ Name='Jobe Researcher'; Md=26092; Scale=100; Fam=0; Social=$true; Ok=$false; Note='empty L50 at the second building, role guessed' }
  SDSM = @{ Name='Sundance Smith - J.A.M.E. Depot'; Md=26097; Scale=100; Fam=0; Social=$true; Ok=$false; Note='vendor dialog only, shop stock not invented. Placement guessed' }
  EDTH = @{ Name='El-Domo the Forest Ranger'; Md=217008; Scale=110; Fam=0; Social=$true; Ok=$false; Note='screenshot is a blue winged humanoid. Mesh is captured Cascading Spirit until a closer capture exists' }
  CTTC = @{ Name='Celeth-El, the Crystal Guardian'; Md=26097; Scale=105; Fam=0; Social=$true; Ok=$false; Note='screenshot is black-and-yellow armor. Talkable for Drake''s test. Human guard mesh until an armor capture exists' }
  GROF = @{ Name='Guardian of Crystals'; Md=26097; Scale=100; Fam=0; Ok=$false; Note='screenshot is a dark armored humanoid. Human guard mesh until an armor capture exists' }
  PODR = @{ Name='Pond Dryad'; Md=209229; Scale=110; Fam=0; Ok=$false; Note='screenshot is a tall dark humanoid with green veins. Mesh is captured Malah-Ana until a dryad capture exists' }
  PIMR = @{ Name='Primitive Mortiig'; Md=209196; Scale=100; Fam=0; Ok=$false; Note='screenshot is a grey hunched long-armed humanoid. Mesh is captured Hiathlin until a mortiig capture exists' }
  TDVA = @{ Name='The Devastator'; Md=209196; Scale=150; Fam=0; Ok=$false; Note='hulking brute near the mortiigs. Same stand-in mesh as Primitive Mortiig, larger' }
  AGGL = @{ Name='Massive Waggler'; Md=226557; Scale=80; Fam=0; Ok=$false; Note='screenshot is a brown long-necked sauropod. Mesh is the large Spinetooth/reptile family until a waggler capture exists' }
  BEGR = @{ Name='Beit Gras'; Md=226557; Scale=55; Fam=0; Ok=$false; Note='screenshot is a long-legged camel. No camel mesh captured; large reptile family is the stand-in' }
}
foreach ($code in @($look.Keys)) {
  if (-not $nt.$code) { throw "missing template $code" }
  $b = $nt.$code.Templates[0]
  $s = $look[$code]
  $b.Name = $s.Name
  $b.Stats.'359' = [int]$s.Md
  $b.Stats.'360' = [int]$s.Scale
  $b.Stats.'455' = [int]$s.Fam
  if ($s.Head) { $b.Stats | Add-Member -Force NoteProperty '64' ([int]$s.Head) }
  if ($s.Xp) { $b.Stats | Add-Member -Force NoteProperty '52' ([int]$s.Xp) }
  if ($s.Social) { $b.Stats.'0' = $blue; $b.LootTable = @() }
  if ($code -eq 'PRAG') { $b.Stats.'1' = 450; $b.Stats.'27' = 450 }
  if ($code -eq 'TDVA') { $b.Stats.'1' = 1200; $b.Stats.'27' = 1200 }
  $b | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{
    Guessed = -not $s.Ok; Confirmed = [bool]$s.Ok; Donor = $b.ContentProvenance.Donor; Source = $s.Note; Levels = $b.ContentProvenance.Levels
  })
}
# Quest item hashes (ids from itemnames). Heckler of Evolution drops QDMD six times per kill.
$qitems = [ordered]@{
  QDMD = @(223763,223763); QBRJ = @(223762,223762); QBST = @(223771,223771); QSCR = @(233804,233804)
  QPEX = @(223766,223766); QNCN = @(223768,223768); QRNP = @(223769,223769); QBFK = @(223773,223773)
  QSBK = @(223770,223770); QSUP = @(223761,223761); QBMS = @(223760,223760)
  QJSV = @(214727,214727); QJSB = @(214729,214729); QJSP = @(214728,214728)
}
$ipath = Join-Path (Split-Path $npath) 'ItemTemplates.json'
$ilines = [IO.File]::ReadAllLines($ipath, $u) | Where-Object { $_ -notmatch ('^\s*"(' + ($qitems.Keys -join '|') + ')":') }
$iadd = foreach ($k in $qitems.Keys) { '    "{0}": {{"Templates": [{1},{2}]}},' -f $k, $qitems[$k][0], $qitems[$k][1] }
if ($ilines[0].Trim() -ne '{') { throw 'ItemTemplates.json: unexpected first line' }
[IO.File]::WriteAllLines($ipath, [string[]](@($ilines[0]) + $iadd + $ilines[1..($ilines.Count - 1)]), $u)
$hb = $nt.HCOE.Templates[0]
$keep = @($hb.LootTable | Where-Object { $_ -and $_.Hash -ne 'QDMD' })
$hb.LootTable = @($keep) + @([pscustomobject]@{ Hash = 'QDMD'; Repeats = 6; Chance = 100; LevelMod = 0 })
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 8) + "`n", $u)
# Named adds that have no client spawn point. Positions are beside the creatures Randy grouped them with.
$spath = Join-Path (Split-Path $npath) 'Playfields\4313\Spawns.json'
$sraw = [IO.File]::ReadAllText($spath, $u)
$adds = @(
  @{ H='BESI'; L=3; X=268.0; Y=44.0; Z=318.0; T=120 }
  @{ H='PRAG'; L=7; X=390.0; Y=33.5; Z=228.0; T=900 }
  @{ H='TDVA'; L=12; X=540.0; Y=34.0; Z=208.0; T=900 }
  @{ H='YOSI'; L=2; X=500.0; Y=48.0; Z=400.0; T=120 }
  @{ H='SDSM'; L=50; X=370.0; Y=35.0; Z=280.0; T=300 }
)
$pieces = @()
foreach ($a in $adds) {
  if ($sraw -match ('"hashText": "' + $a.H + '"')) { continue }
  $pieces += @"
    {
      "districtIndex": 0,
      "hash": 0,
      "hashText": "$($a.H)",
      "manifestHash": 0,
      "minLevel": $($a.L),
      "maxLevel": $($a.L),
      "respawnChance": 100,
      "flags": 1,
      "respawnTime": $($a.T),
      "version7Extra": 0,
      "hasOptionalFlagBlock": true,
      "nativeFlags": 0,
      "moreFlags": 0,
      "assistanceRadius": 0,
      "unknownOptionalU8": 0,
      "angle": 0,
      "angleW": 1,
      "position": [ $($a.X), $($a.Y), $($a.Z) ],
      "radius": 1,
      "additionalPoints": [],
      "extensions": null
    }
"@
}
if ($pieces.Count) {
  $cut = $sraw.LastIndexOf("  ]")
  if ($cut -lt 0) { throw '4313 Spawns.json has no spawn list' }
  $brace = $sraw.LastIndexOf('}', $cut)
  if ($brace -lt 0) { throw '4313 Spawns.json has no last spawn' }
  if ($sraw.Substring($brace + 1, [Math]::Min(5, $sraw.Length - $brace - 1)) -notmatch '^\s*,') {
    $sraw = $sraw.Insert($brace + 1, ',')
    $cut = $sraw.LastIndexOf("  ]")
  }
  $frag = ($pieces -join ",`n") + "`n"
  $sraw = $sraw.Insert($cut, $frag)
  [IO.File]::WriteAllText($spath, $sraw, $u)
}
"guessed templates=$n looks=$($look.Count) spawnsAdded=$($pieces.Count)"
