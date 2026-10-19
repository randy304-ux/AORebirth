# ddogs/tools/Build-WhereisNamed.ps1 - names placeholder spawns from the community whereis lists (Nadybot/Budabot/BeBot).
# Input: ddogs/tools/whereis_named_spawns.csv (high-confidence matches only: the closest placeholder within 15 m of a whereis
# entry on the same playfield; see ddogs/docs/whereis_npc_report.md). Rerunnable: only fills codes with no template, or codes
# this script made before (ContentProvenance.Source starts with 'whereis'). Real/captured data always wins.
# Look/stats priority: 1) DB mobtemplate row with the same name, 2) existing template with the same name,
# 3) same-playfield template whose name shares the creature word (Spirit, Weaver, Golem...), 4) person-like name -> civilian
# donor ADAF (NoCombat) if it has a friendly title, otherwise hostile human donor HW01, 5) same-playfield template nearest in level, 6) HW01. Levels come from the client spawn.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'),
      [string]$Cnf = 'I:\AO\secrets\root-client.cnf', [string]$MySql = 'I:\AO\mysql\server\bin\mysql.exe')
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData); $npath = Join-Path $gd 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
$rows = Import-Csv (Join-Path $PSScriptRoot 'whereis_named_spawns.csv')
function Mine($c) { $t = $nt.$c; $t -and "$($t.ContentProvenance.Source)$($t.Templates[0].ContentProvenance.Source)" -like 'whereis*' }
$byName = @{}; foreach ($p in $nt.PSObject.Properties) { $t = $p.Value.Templates; if ($t -and $t[0].Name -and -not (Mine $p.Name)) { if (-not $byName[$t[0].Name.ToLower()]) { $byName[$t[0].Name.ToLower()] = $p.Name } } }
$db = @{}
if (Test-Path $MySql) { try {
  $in = ($rows | % { "'" + ($_.name -replace "'", "''") + "'" } | sort -Unique) -join ','
  foreach ($r in (& $MySql "--defaults-extra-file=$Cnf" -N -B cellao_codex_clean -e "select Name,Side,Fatness,Breed,Sex,Race,Flags,NPCFamily,MonsterData,MonsterScale from mobtemplate where Name in ($in)")) { $a = $r -split "`t"; $db[$a[0].ToLower()] = $a }
} catch { Write-Warning "DB lookup skipped: $_" } }
$pfCodes = @{}
foreach ($pf in ($rows.pf | sort -Unique)) {
  $sp = Join-Path $gd "Playfields\$pf\Spawns.json"; if (-not (Test-Path $sp)) { continue }
  $pfCodes[$pf] = @(([IO.File]::ReadAllText($sp, $u) | ConvertFrom-Json).spawns.hashText | sort -Unique | ? { $nt.$_ -and $nt.$_.Templates -and $nt.$_.Templates[0].Name -and -not (Mine $_) })
}
$person = '(?i)\b(officer|commander|administrator|captain|doctor|dr\.?|mr\.?|mrs\.?|ms\.?|professor|sergeant|lieutenant|general|agent|trader|merchant|shop|vendor|guard|director|president|mayor|priest|nurse|scientist|engineer|technician|manager|sheriff|lady|sir)\b'
$creature = '(?i)\b(spirit|weaver|golem|chimera|spider|dredge|soul|beast|leet|snake|brontosaurus|bronto|anun|reet|minibull|horror|drone|robot|borg|cyborg|mutant|rollerrat|salamander|scorpiod|shark|wolf|dryad|predator|hound|fly|worm|tombstone|shadow|kyr''ozch|alien|imp|malah|hiathlin|ithaki|medusa|bigot|anansi|nippy|hound|crawler|mantis|lizard|eye|vortex|ancient)\b'
$friendly = '(?i)\b(information officer|administrator|herbalist|trader|shop|vendor|merchant|nurse|mixer|bartender|banker)\b'
$landmark = '(?i)\b(statue|outpost|face in the sand|satellite dish|tower|ruins|camp|entrance|whompa|garden|grid|monument|crater|bridge|wreck|base|fortress|dome)$|^(crete)$'
$log = New-Object System.Collections.ArrayList; $n = 0; $skip = 0
foreach ($r in $rows) {
  $c = $r.code; if ($nt.$c -and -not (Mine $c)) { $skip++; continue }
  if ($r.name -match $landmark -or $r.name -match '(?i)\b(statue|outpost)\b' -or $r.name -match '(?i)face in the sand') { [void]$log.Add("$c`tpf $($r.pf)`t$($r.name)`t-`tSKIP`tlandmark name, not an NPC"); continue }
  $lo = [int]$r.minLevel; $hi = [int]$r.maxLevel; if ($hi -lt $lo) { $hi = $lo }; $lvl = [int][math]::Ceiling(($lo + $hi) / 2)
  $key = $r.name.ToLower(); $don = $null; $how = ''; $guess = $true; $social = $false
  if ($byName[$key]) { $don = $byName[$key]; $how = "same-name template $don"; $guess = $false }
  if (-not $don) {
    $cw = [regex]::Matches($r.name, $creature) | % { $_.Value.ToLower() } | select -Last 1
    if ($cw) { $don = @($pfCodes[$r.pf]) | ? { $nt.$_.Templates[0].Name -match "(?i)\b$([regex]::Escape($cw))" } | sort { [math]::Abs([int]$nt.$_.Templates[0].Level - $lvl) } | select -First 1; if ($don) { $how = "same-pf '$cw' donor $don" } }
  }
  if (-not $don -and ($r.name -match $person -or ([int]$r.pf -lt 4000 -and $r.name -notmatch $creature -and $r.name -match '^[A-Z][a-z]+ [A-Z]'))) { if ($r.name -match $friendly) { $don = 'ADAF'; $how = 'friendly title -> civilian donor ADAF (NoCombat)'; $social = $true } else { $don = 'HW01'; $how = 'person-like name -> hostile human donor HW01 (Fresh Highwayman look)' } }
  if (-not $don) { $don = @($pfCodes[$r.pf]) | sort { [math]::Abs([int]$nt.$_.Templates[0].Level - $lvl) } | select -First 1; if ($don) { $how = "same-pf nearest-level donor $don" } }
  if (-not $don) { $don = 'HW01'; $how = 'no same-pf donor -> human donor HW01' }
  $d = $nt.$don.Templates | sort { [math]::Abs([int]$_.Level - $lvl) } | select -First 1
  $dl = [math]::Max(1, [int]$d.Level)
  $bands = foreach ($L in (@($lo, $hi) | sort -Unique)) {
    $b = $d | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $b.Name = $r.name; $b.Level = $L; $b.Stats.'54' = $L
    $hp = [int][math]::Max(30, [double]$d.Stats.'1' * $L / $dl); $b.Stats.'1' = $hp; $b.Stats | Add-Member -Force NoteProperty '27' $hp
    if ($db[$key]) { $a = $db[$key]; $map = @{ '33'=1; '47'=2; '4'=3; '59'=4; '89'=5; '455'=7; '359'=8; '360'=9 }; foreach ($k in $map.Keys) { $b.Stats | Add-Member -Force NoteProperty $k ([int]$a[$map[$k]]) } }
    if ($social -and -not (@($b.Features) -contains 'NoCombat')) { $b | Add-Member -Force NoteProperty Features @(@($b.Features | ? { $_ }) + 'NoCombat') }
    $b
  }
  if ($db[$key]) { $how = "DB mobtemplate look + $how"; $guess = $false }
  $prov = [pscustomobject]@{ Guessed = $guess; Donor = $don; Source = "whereis (Nadybot/Budabot/BeBot) $($r.dist_m) m: $how"; Levels = "$lo-$hi" }
  $nt | Add-Member -Force NoteProperty $c ([pscustomobject]@{ Templates = @($bands); ContentProvenance = $prov }); $n++
  [void]$log.Add("$c`tpf $($r.pf)`t$($r.name)`tL$lo-$hi`t$(if ($guess) {'GUESS'} else {'real'})`t$how")
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 20), $u)
$log | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'Build-WhereisNamed.last.txt')
"Build-WhereisNamed: $n templates written, $skip codes already had real templates"


