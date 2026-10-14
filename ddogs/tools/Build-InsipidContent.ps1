# ddogs/tools/Build-InsipidContent.ps1 - Nascence Insipid Ring actors and the molokhai chain.
# Randy's spec: Ergo gives a Fumigating Canister for Deep Breath Nine, or Long Stare at Danger opens the molokhai chain.
# Young Molokhai drops Baby Molok Oil. Ergo turns the oil in for the profession Insipid Ring.
# Ergo's guide spot (1428, 195) is on the swamp playfield but south of the client spawn cloud. Y is the nearest spawn. Guessed.
# The four molokhai are persistent (a death-chain is not supported: SpawnAdds despawn with the boss). Kill them in quest order.
# Two copies: the Embryo west of Deep Breath Nine (4311, 385x479) and the swamp entrance by Long Stare (4312, 1414x592).
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$GameData = [IO.Path]::GetFullPath($GameData)
$npath = Join-Path $GameData 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
$items = [ordered]@{
  FCAN = @(234878,234878); BMOL = @(234879,234879)
  INAR = @(225753,225753); INCO = @(225758,225758); INEX = @(223998,223998); ININ = @(225760,225760); INSU = @(225759,225759)
}
$ipath = Join-Path $GameData 'ItemTemplates.json'
$ilines = [IO.File]::ReadAllLines($ipath, $u) | Where-Object { $_ -notmatch '^\s*"(FCAN|BMOL|INAR|INCO|INEX|ININ|INSU)":' }
$iadd = foreach ($k in $items.Keys) { '    "{0}": {{"Templates": [{1},{2}]}},' -f $k, $items[$k][0], $items[$k][1] }
if ($ilines[0].Trim() -ne '{') { throw 'ItemTemplates.json: unexpected first line' }
[IO.File]::WriteAllLines($ipath, [string[]](@($ilines[0]) + $iadd + $ilines[1..($ilines.Count - 1)]), $u)
if (-not $nt.SCDR -or -not $nt.HIAT) { throw 'SCDR or HIAT donor missing' }
$made = @()
function Add-Leaf($code, $name, $lvl, $don, $social, $md, $scale, $hpMul, $loot, $note) {
  if ($script:nt.$code -and -not $script:nt.$code.Templates[0].ContentProvenance.Guessed) { throw "$code exists and is not guessed" }
  if ($script:nt.$code) { return }
  $d = $script:nt.$don.Templates[0]
  $b = $d | ConvertTo-Json -Depth 8 | ConvertFrom-Json
  $dl = [math]::Max(1, [int]$d.Level)
  $b.Name = $name; $b.Level = $lvl; $b.Stats.'54' = $lvl
  $hp = [int][math]::Max(80, [double]$d.Stats.'1' * $lvl / $dl * $hpMul)
  $b.Stats.'1' = $hp; if ($b.Stats.'27') { $b.Stats.'27' = $hp }
  $b.Stats.'0' = $(if ($social) { 277352961 } else { 268964353 })
  if ($md) { $b.Stats.'359' = $md }
  $b.Stats.'360' = $scale
  if ($social) { $b.LootTable = @() } else { $b.LootTable = @($loot) }
  $b | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = $don; Source = $note; Levels = "$lvl" })
  $script:nt | Add-Member -Force NoteProperty $code ([pscustomobject]@{ Templates = @($b) })
  $script:made += $code
}
$common = @(
  [pscustomobject]@{ Hash='CMTR'; Repeats=1; Chance=40; LevelMod=20 }
  [pscustomobject]@{ Hash='CMHK'; Repeats=1; Chance=8; LevelMod=20 }
)
$oil = @($common) + @([pscustomobject]@{ Hash='BMOL'; Repeats=1; Chance=100; LevelMod=0 })
Add-Leaf 'ERGO' 'Ergo' 40 'SCDR' $true 210582 100 2 @() 'Floating head: client monster data 210582 guardian_of_shadows (Randy PRK screenshots). Spot 1428x195 matches AO-Universe/aquarius guides and client district Ergo.'
Add-Leaf 'LSSR' 'Long Stare at Danger' 40 'SCDR' $true 22802 180 2 @() 'Client spawn LSSR 1401x578 on swamp 4312. Yuttos mesh from Deep Breath Nine.'
Add-Leaf 'EMOL' 'Embryonic Molokhai' 35 'HIAT' $false 214479 110 2 $common 'Client monster data 214479 sl_molokhai. Persistent, not a death-chain.'
Add-Leaf 'LMOL' 'Larval Molokhai' 37 'HIAT' $false 214479 120 2.4 $common 'Client monster data 214479 sl_molokhai.'
Add-Leaf 'MMOL' 'Metamorphosing Molokhai' 39 'HIAT' $false 214479 130 2.8 $common 'Client monster data 214479 sl_molokhai.'
Add-Leaf 'YMOL' 'Young Molokhai' 42 'HIAT' $false 214479 150 4 $oil 'Drops Baby Molok Oil. Client monster data 214479 sl_molokhai.'
if ($made.Count) {
  $raw = [IO.File]::ReadAllText($npath, $u).TrimEnd()
  if (-not $raw.EndsWith('}')) { throw 'NpcTemplates.json: unexpected ending' }
  $raw = $raw.Substring(0, $raw.Length - 1).TrimEnd()
  if (-not $raw.EndsWith(',')) { $raw += ',' }
  $chunks = foreach ($h in $made) {
    $body = ($nt.$h | ConvertTo-Json -Depth 8)
    '    "{0}": {1}' -f $h, ($body -replace '(?m)^', '    ').TrimStart()
  }
  [IO.File]::WriteAllText($npath, ($raw + "`n" + ($chunks -join ",`n") + "`n}`n"), $u)
}
function Add-Spawns($pf, $points) {
  $spath = Join-Path $GameData "Playfields\$pf\Spawns.json"
  $sraw = [IO.File]::ReadAllText($spath, $u)
  $sp = ($sraw | ConvertFrom-Json).spawns
  $pieces = @()
  foreach ($a in $points) {
    $best = $null; $bd = [double]::MaxValue
    foreach ($s in $sp) {
      $dx = [double]$s.position[0] - $a.X; $dz = [double]$s.position[2] - $a.Z
      $d = $dx*$dx + $dz*$dz
      if ($d -lt $bd) { $bd = $d; $best = $s }
    }
    $y = '{0:0.###}' -f $(if ($a.Y) { $a.Y } else { [double]$best.position[1] })
    $pos = '[ {0:0.###}, {1}, {2:0.###} ]' -f $a.X, $y, $a.Z
    if ($sraw.Contains($pos)) { continue }
    if ($sraw.Contains('"hashText": "' + $a.H + '"') -and $a.H -in 'ERGO','LSSR') { continue }
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
      "position": $pos,
      "radius": 1,
      "additionalPoints": [],
      "extensions": null
    }
"@
  }
  if (-not $pieces.Count) { return 0 }
  $cut = $sraw.LastIndexOf("  ]")
  $brace = $sraw.LastIndexOf('}', $cut)
  if ($sraw.Substring($brace + 1, [Math]::Min(8, $sraw.Length - $brace - 1)) -notmatch '^\s*,') {
    $sraw = $sraw.Insert($brace + 1, ',')
    $cut = $sraw.LastIndexOf("  ]")
  }
  [IO.File]::WriteAllText($spath, $sraw.Insert($cut, (($pieces -join ",`n") + "`n")), $u)
  $pieces.Count
}
$swamp = @(
  @{ H='ERGO'; L=40; X=1428; Z=195; T=300 }
  @{ H='EMOL'; L=35; X=1414; Z=604; T=180 }
  @{ H='LMOL'; L=37; X=1426; Z=604; T=180 }
  @{ H='MMOL'; L=39; X=1414; Z=616; T=180 }
  @{ H='YMOL'; L=42; X=1426; Z=616; T=180 }
)
$wilds = @(
  @{ H='EMOL'; L=35; X=385; Z=491; T=180 }
  @{ H='LMOL'; L=37; X=397; Z=491; T=180 }
  @{ H='MMOL'; L=39; X=385; Z=503; T=180 }
  @{ H='YMOL'; L=42; X=397; Z=503; T=180 }
)
$n1 = Add-Spawns 4312 $swamp
$n2 = Add-Spawns 4311 $wilds
"insipid templatesNew=$($made.Count) spawns4312=$n1 spawns4311=$n2"
