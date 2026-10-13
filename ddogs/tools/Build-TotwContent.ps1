# ddogs/tools/Build-TotwContent.ps1 - Temple of the Three Winds (PF 1931) static dungeon. Re-runnable; called by Build-NpcTemplates.ps1.
#  Spawn points: client GameData/Playfields/1931/Spawns.json (251 points, level bands per hash).
#  Names/loot: AO-Universe TotW guide + loot table (wins over fandom). Item ids: itemnames (auno/aogalaxy ids), checked by name.
#  Boss level/HP/damage/model: retired capture runtime (git ecefa673^, CapturedTempleOfThreeWindsEncounterRuntimeService.cs);
#  boss hash <-> name confirmed by spawn position (capture position within 0.4 m of the client spawn point).
#  Ordinary looks: retired capture profile seeds (cultist MonsterData/head) and per-rank boss models. Chances are placeholders.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$GameData = [IO.Path]::GetFullPath($GameData)
$u = [Text.UTF8Encoding]::new($false)

# ---------- 1. item hashes (T***), ids from itemnames ----------
$items = [ordered]@{
  TACR=@(204763,204763); TBAR=@(204653,204653); TEXR=@(204759,204759); TFOR=@(204757,204757); TGTA=@(204748,204748); TBAS=@(204756,204756)
  TRVR=@(204762,204762); TRFA=@(204761,204761); TRCG=@(204758,204758); TWCR=@(204760,204760); TWFL=@(204698,204698)
  TSF1=@(204699,204699); TSF2=@(204703,204703); TSF3=@(204701,204701); TSF4=@(204704,204704); TSF5=@(204702,204702); TSF6=@(204700,204700)
  TBLR=@(204596,204596); TBR3=@(204571,204571); TGR3=@(204573,204573); TNR3=@(204577,204579); TPR3=@(204574,204576); TREN=@(204598,204598)
  TRML=@(204569,204569); TRTF=@(204593,204593); TRWF=@(204595,204595); TSR3=@(204572,204572)
  TBSP=@(129071,129072); TMSI=@(130084,130085); TEWK=@(160216,160217); TNWK=@(160210,160211); TSWK=@(160213,160214); TWWK=@(160273,160274)
  TESC=@(204747,204747); TFSL=@(204746,204746); TFTU=@(204739,204739); THSK=@(204750,204750); TPHB=@(204745,204745); TSKL=@(204752,204752)
  TSKW=@(204751,204751); TSDS=@(204755,204755); TTCH=@(204706,204706); TMTI=@(123733,123734)
  TKVI=@(204650,204650); TNIE=@(204613,204613); TNSP=@(204648,204649); TSOA=@(204647,204647); TTOG=@(204651,204651)
  TCFU=@(204708,204708); TDKM=@(204602,204602); TMLP=@(204603,204603); TGCB=@(204601,204601); THBI=@(204765,204765); TLCR=@(204829,204829)
  TPRD=@(204608,204608); TRBW=@(275083,275083); TCPL=@(287143,287143)   # Copper Pail QL200: retail capture on Guardian and Aztur corpses
  TAF1=@(204711,204711); TAF2=@(204712,204712); TAF3=@(204713,204713); TAF4=@(204714,204714); TAF5=@(204715,204715)
  TTE1=@(204720,204720); TTE2=@(204721,204721); TTE3=@(204722,204722); TTE4=@(204723,204723); TTE5=@(204724,204724); TTE6=@(204725,204725)
}
$fam = [ordered]@{ TSFA = @('TSF1','TSF2','TSF3','TSF4','TSF5','TSF6'); TBKA = @('TAF1','TAF2','TAF3','TAF4','TAF5'); TBKT = @('TTE1','TTE2','TTE3','TTE4','TTE5','TTE6') }
$mine = @($items.Keys) + @($fam.Keys)
$ipath = Join-Path $GameData 'ItemTemplates.json'
$lines = [IO.File]::ReadAllLines($ipath, $u) | Where-Object { $_ -notmatch ('^\s*"(' + ($mine -join '|') + ')":') }
$add = @(foreach ($k in $items.Keys) { '    "{0}": {{"Templates": [{1},{2}]}},' -f $k, $items[$k][0], $items[$k][1] })
$add += @(foreach ($k in $fam.Keys) { '    "{0}": {{"Children": [{1}]}},' -f $k, (($fam[$k] | ForEach-Object { '"' + $_ + '"' }) -join ',') })
if ($lines[0].Trim() -ne '{') { throw 'ItemTemplates.json: unexpected first line' }
[IO.File]::WriteAllLines($ipath, [string[]](@($lines[0]) + $add + $lines[1..($lines.Count - 1)]), $u)

# ---------- 2. templates ----------
$curve = @(@(1,20),@(3,98),@(5,115),@(9,287),@(12,365),@(15,504),@(24,500),@(104,6236),@(139,10032),@(190,16798),@(214,20077))
function HealthAt([int]$lvl) {
  for ($i = 1; $i -lt $curve.Count; $i++) { $a = $curve[$i-1]; $b = $curve[$i]
    if ($lvl -le $b[0]) { $t = ([math]::Log($lvl) - [math]::Log($a[0])) / ([math]::Log($b[0]) - [math]::Log($a[0])); return [int][math]::Round([math]::Exp([math]::Log($a[1]) + $t * ([math]::Log($b[1]) - [math]::Log($a[1])))) } }
  $curve[-1][1]
}
function CommonLoot([int]$lvl) {
  @([ordered]@{ Hash = 'CMTR'; Repeats = 1; Chance = 50; LevelMod = 20 }, [ordered]@{ Hash = 'CMHK'; Repeats = 1; Chance = 12; LevelMod = 20 },
    [ordered]@{ Hash = 'OFAB'; Repeats = 1; Chance = 1; LevelMod = 0 }, [ordered]@{ Hash = 'TMTI'; Repeats = 1; Chance = 1; LevelMod = 0 })   # MTI M-2: "any mob"
}
$npath = Join-Path $GameData 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
$up = [IO.File]::ReadAllText([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\docs\accepted\npc\delmus\NpcTemplate.json')), $u) | ConvertFrom-Json
$skel = ($up | Where-Object { $_.Hash -eq 'DCQV' } | Select-Object -First 1).Stats   # hostile human skeleton (Subway Looter)
$sp = ([IO.File]::ReadAllText((Join-Path $GameData 'Playfields\1931\Spawns.json'), $u) | ConvertFrom-Json).spawns
$seeds = @(@(26074,40691),@(26082,40634),@(26103,40103),@(26135,40271),@(26137,40209),@(26147,40172),@(26149,40151))
# hash = name, MonsterData, head, loot rows (hash,chance), [lvl, hp, dmgMax, scale] for captured bosses
$aco = @(@('TACR',20),@('TSR3',10),@('TBKA',4),@('TBKT',4)); $rev = @(@('TSFA',6),@('TBKA',4),@('TBKT',4)); $win = @(,@('TSFA',6))
$exa = @(@('TEXR',25),@('TESC',8),@('THSK',8),@('TRWF',8),@('TBKA',4),@('TBKT',4)); $fai = @(@('TBR3',10),@('TBKA',4),@('TBKT',4)); $cul = @(@('TBR3',5),@('TBKA',2),@('TBKT',2))
$def = [ordered]@{
  ACKA=@('Acolyte Kalen',26143,0,$aco); ACVE=@('Acolyte Verona',26143,0,$aco); ACSE=@('Acolyte Seleen',26143,0,$aco); ACOP=@('Acolyte Opet',26143,0,$aco)
  ACFE=@('Acolyte Felid',26143,0,$aco); ACAM=@('Acolyte Amber',26143,0,$aco); ACDO=@('Acolyte Dominic',26143,0,$aco); ACKE=@('Acolyte Kellian',26143,0,$aco)
  ATBT=@('Acolyte Bryant',26143,0,$aco); ACBT=@('Acolyte Betany',26143,0,$aco,@(32,1734,60,100))
  REOL=@('Reverend Oluay',26147,0,$rev); REHY=@('Reverend Hyuan',26147,0,($rev + @(@('TBSP',15),@('TMSI',15),@('TPHB',10)))); REDA=@('Reverend Dashell',26147,0,($rev + @(,@('TRVR',25))))
  RESX=@('Reverend Saxx',26147,0,$rev); REJU=@('Reverend Julaan',26147,0,$rev); RECA=@('Reverend Cadelly',26147,0,($rev + @(@('TRVR',25),@('TGR3',10),@('TSR3',10))))
  REGU=@('Reverend Gulard',26147,0,($rev + @(@('THSK',20),@('TPHB',20))),@(38,3052,90,100))
  WIDO=@('Windcaller Donnel',26151,0,($win + @(@('TWCR',25),@('TNR3',10),@('TPR3',10)))); WITI=@('Windcaller Tilla',26151,0,($win + @(,@('TWCR',25))))
  WIRE=@('Windcaller Rendal',26151,0,($win + @(@('TESC',15),@('TPHB',15)))); WIYE=@('Windcaller Yen',26151,0,$win)
  WIYA=@('Windcaller Yatila',26151,0,($win + @(@('TBAR',15),@('TWFL',15),@('TBLR',10),@('TLCR',25))),@(56,13863,170,100))
  EXEC=@('Exarch Ecclese',26159,0,($exa + @(@('TNR3',10),@('TPR3',10)))); EXPI=@('Exarch Pilvar',26159,0,($exa + @(,@('TRTF',10))))
  EXTR=@('Exarch Truan',26159,0,($exa + @(@('TRTF',10),@('TPHB',10)))); EXGE=@('Exarch Gevarain',26159,0,($exa + @(,@('TBLR',10))))
  EXLI=@('Exarch Li-Po',26159,0,($exa + @(@('TNR3',10),@('TPR3',10),@('TREN',10))))
  CYTH=@('Cyth the Faithful',26090,0,($fai + @(,@('TRFA',25)))); MLTH=@('Malikai the Faithful',26090,0,($fai + @(,@('TRFA',25)))); NATH=@('Nathan the Faithful',26090,0,$fai)
  ORTH=@('Oran the Faithful',26090,0,$fai); PATH=@('Pallen the Faithful',26090,0,$fai); MUTE=@('Murial the Faithful',26090,0,$fai)
  CSTE=@('Caska the Faithful',26090,0,($fai + @(@('TRFA',25),@('TRBW',100))))   # Red Berry Wine for Dante Karrall's "Like Fine Wine"
  ULTI=@('Cultist',0,0,$cul); COKC=@('Cultist',1,0,$cul); CULT=@('Cultist',2,0,$cul); PCFH=@('Cultist',3,0,$cul); BBOI=@('Cultist',4,0,$cul); KNSL=@('Cultist',5,0,$cul); OLSE=@('Cultist',6,0,$cul)
  DELE=@('Deathless Legionnaire',42981,0,@(,@('TFSL',5))); RACO=@('Reanimated Corpse',41690,0,@())
  ETGU=@('Eternal Guardian',41690,0,@(@('TEWK',4),@('TNWK',4),@('TSWK',4),@('TWWK',4))); ETSE=@('Eternal Sentinel',41690,0,@(@('TEWK',4),@('TNWK',4),@('TSWK',4),@('TWWK',4)))
  DEOF=@('Defender of the Three',38394,0,@(@('THSK',25),@('TPHB',25),@('TNSP',20)),@(42,7091,145,104))
  THCU=@('The Curator',22802,0,@(@('TRCG',20),@('TBR3',25),@('TNR3',25),@('TPR3',25),@('TTOG',20)),@(52,9740,198,106))
  NMTH=@('Nematet the Custodian of Time',26159,0,@(@('TRWF',25),@('TTCH',25),@('TNIE',20),@('TSOA',15),@('TTOG',15)),@(66,25318,255,107))
  TRRN=@('The Re-Animator',26155,0,@(@('TBAR',20),@('TWFL',20),@('TSKL',15),@('TSKW',15),@('TCFU',20),@('TPRD',20)),@(60,12441,220,100))
  LITH=@('Lien the Memorystalker',40515,0,@(@('TRML',25),@('TSKL',15),@('TSKW',15),@('TCFU',15),@('TDKM',15),@('TMLP',15)))
  GUOT=@('Guardian of Tomorrow',22798,0,@(@('TGCB',100),@('TGTA',25),@('TCPL',15),@('TBAS',25),@('TBLR',20),@('TREN',20),@('TRTF',20),@('TPRD',25)),@(68,26500,264,108))
  GATH=@('Gartua the Doorkeeper',159085,40105,@(@('TRCG',25),@('TBLR',20),@('TRWF',20),@('TKVI',20)),@(65,14130,229,107))
  UKTH=@('Uklesh the Frozen',40515,0,@(@('TBAR',20),@('TWFL',20),@('TFOR',15),@('TRTF',20),@('TFTU',20),@('TPRD',25)),@(73,21039,283,108))
  KHAL=@('Khalum',95352,0,@(@('TREN',25),@('TPRD',25)),@(73,25247,283,108))
  AZTR=@('Aztur the Immortal',159966,0,@(@('TREN',25),@('TRTF',25),@('TSDS',20),@('TSOA',15),@('THBI',25),@('TPRD',25),@('TCPL',15)),@(74,38630,522,163))
  JUJU=@('Ju-ju Doll',41690,0,@(),@(45,600,60,50))
}
$cash = @{ GUOT = 2830; GATH = 1592; UKTH = 625; KHAL = 625; AZTR = 3184 }   # corpse credits from retail captures
$report = @()
foreach ($h in $def.Keys) {
  $d = $def[$h]; $md = $d[1]; $head = $d[2]
  if ($d[0] -eq 'Cultist') { $md = $seeds[$d[1]][0]; $head = $seeds[$d[1]][1] }
  $pts = @($sp | Where-Object { $_.hashText -eq $h })
  if ($d.Count -gt 4) { $lv = @($d[4][0]) } elseif ($pts.Count) { $lv = @((($pts | Measure-Object minLevel -Minimum).Minimum), (($pts | Measure-Object maxLevel -Maximum).Maximum)) | Select-Object -Unique } else { throw "no level for $h" }
  if (-not $pts.Count -and $h -notin 'KHAL','AZTR','JUJU') { throw "$h has no spawn point" }
  $bands = @()
  foreach ($l in $lv) {
    $st = [ordered]@{}; foreach ($p in $skel.PSObject.Properties) { $st[$p.Name] = [int]$p.Value }
    $hp = $(if ($d.Count -gt 4) { $d[4][1] } else { HealthAt $l }); $mx = $(if ($d.Count -gt 4) { $d[4][2] } else { [math]::Max(3, [int][math]::Round($l * 1.5)) })
    $st['1'] = $hp; $st['27'] = $hp; $st['54'] = $l; $st['359'] = $md; $st['360'] = $(if ($d.Count -gt 4) { $d[4][3] } else { 100 }); $st['455'] = 136
    $st['285'] = $mx; $st['286'] = [math]::Max(1, [int][math]::Round($mx / 2)); $st['61'] = $(if ($cash.ContainsKey($h)) { $cash[$h] } else { 10 * $l })
    if ($head) { $st['64'] = $head }
    $loot = @($d[3] | Where-Object { $_ } | ForEach-Object { [ordered]@{ Hash = $_[0]; Repeats = 1; Chance = $_[1]; LevelMod = 0 } })
    if ($h -ne 'JUJU' -and $h -ne 'RACO') { $loot += CommonLoot $l }
    $b = [ordered]@{ Name = $d[0]; Level = $l; Stats = $st; LootTable = $loot }
    # Boss triggers (AO-Universe): Khalum appears when Uklesh dies, Aztur when Khalum dies (On=Death adds stay after the boss);
    # Lien calls Ju-ju Dolls when the fight starts and they vanish when he dies (FirstAggro/HealthBelow adds are despawned with him).
    if ($h -eq 'UKTH') { $b['BossTriggers'] = @([ordered]@{ On = 'Death'; Pct = 0; Actions = @([ordered]@{ Type = 'SpawnAdds'; Hash = 'KHAL'; Count = 1; Radius = 6; Level = 73 }) }) }
    if ($h -eq 'KHAL') { $b['BossTriggers'] = @([ordered]@{ On = 'Death'; Pct = 0; Actions = @([ordered]@{ Type = 'SpawnAdds'; Hash = 'AZTR'; Count = 1; Radius = 6; Level = 74 }) }) }
    if ($h -eq 'LITH') { $dolls = @([ordered]@{ Type = 'SpawnAdds'; Hash = 'JUJU'; Count = 3; Radius = 4; Level = 45 })
      $b['BossTriggers'] = @([ordered]@{ On = 'FirstAggro'; Pct = 0; Actions = $dolls }, [ordered]@{ On = 'HealthBelow'; Pct = 50; Actions = $dolls }) }
    $bands += $b
  }
  $nt | Add-Member -NotePropertyName $h -NotePropertyValue ([ordered]@{ Templates = $bands }) -Force
  $report += "totw $h $($d[0]) L$($lv -join '-') points=$($pts.Count) model=$md loot=" + (($d[3] | Where-Object { $_ } | ForEach-Object { "$($_[0])@$($_[1])" }) -join ',')
}
$missing = @($sp | ForEach-Object { $_.hashText } | Sort-Object -Unique | Where-Object { -not $def.Contains($_) })
if ($missing.Count) { throw ('1931 spawn hashes without template: ' + ($missing -join ' ')) }
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 8) + "`n", $u)
$report | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'Build-TotwContent.last.txt')
"totw items=$($items.Count)+$($fam.Count) families templates=$($def.Count) spawnpoints=$($sp.Count)"