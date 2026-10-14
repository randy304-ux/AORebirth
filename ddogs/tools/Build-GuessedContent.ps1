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
  @('BEGR','Beit Gras',4,4,'LLBE','aquarius TG'), @('AGGL','Waggler',4,4,'SWSI','aquarius TG'),
  @('HUPD','Hungry Predator',3,5,'PDST','aquarius TG'), @('PIMR','Primitive Mortiig',6,6,'HIAT','aquarius TG'),
  @('PODR','Pond Dryad',6,6,'ALAH','aquarius TG'), @('CRIP','Crippler',5,6,'CIOG','aquarius TG'),
  @('HCOF','Heckler of Nature',8,8,'MOOM','aquarius TG'), @('HCOE','Heckler of Evolution',5,5,'MOOM','aquarius TG unique, final quest'),
  @('TEPD','Tempterus',6,7,'EMPT','aquarius TG faction'), @('WAOW','Watcher of Woods',8,8,'TEMP','aquarius TG faction'),
  @('GROF','Guardian of Crystal',8,8,'EMPT','aquarius TG faction'), @('CTTC','Celeth-El the Crystal Guardian',10,10,'EMPT','aquarius TG quest'),
  @('EDTH','El-Domo the Forest Ranger',10,10,'TEMP','aquarius TG quest'), @('IMDR','Imp Disruptor',20,20,'PDST','aquarius TG unique'),
  # PF 4311 Nascence Wilds / Brawl
  @('ALAN','Calan-Cur',36,40,'RAIG','aquarius Wilds: Unredeemed Calan-Cur 36-37'),
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
  $b | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Donor = $don; Source = $src; Levels = "$lo-$hi" })
  $nt | Add-Member -Force NoteProperty $code ([pscustomobject]@{ Templates = @($b) }); $n++
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 8) + "`n", $u)
"guessed templates=$n"
