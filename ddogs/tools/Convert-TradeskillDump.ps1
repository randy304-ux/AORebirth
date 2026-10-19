<#
.SYNOPSIS
  Converts the legacy CellAO tradeskill dump (AORebirth.Database/SqlTables/tradeskill.sql) into
  GameData/Tradeskills.cellao.json, the recipe overlay TradeskillService loads after Tradeskills.json.
  Every column is kept: Id1 -> Source, Id2 -> Target, ResultIds -> Result [low, high], MinTarget -> MinTargetQl,
  QlRangePercent, DeleteFlag (bit 0 source, bit 1 target), Skill/SkillPercent/SkillPerBump -> Skills
  [[stat, percent, perBump]], MaxBump, MinXP/MaxXP, IsImplant -> Implant. Exact duplicate rows are dropped; a
  second row for the same source+target with a different result is dropped and counted (first row wins).
  Corrections checked against AO-Universe (2026-10-18):
   - Implant Disassembly Clinic: Breaking and Entry 4.75x the implant QL, not 4.25x (pharmacy guide and AOWiki).
   - Implant Disassembly Clinic on Jobe or Refined implants: dropped; those cannot be cleaned (implant-creation guide).
   - Symbol Library + Instruction Disc: the library may be as low as 25% of the disc QL (QlRangePercent 75, not 25;
     nanocrystal-creation guide).
#>
param(
  [string]$Sql = (Join-Path $PSScriptRoot '..\..\AORebirth\Libraries\Source\AORebirth.Database\SqlTables\tradeskill.sql'),
  [string]$Out = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData\Tradeskills.cellao.json'),
  [string]$Names = (Join-Path $PSScriptRoot '..\..\AORebirth\Libraries\Source\AORebirth.Database\SqlTables\itemnames.sql')
)
$ErrorActionPreference = 'Stop'
$re = [regex]'^INSERT INTO `tradeskill` VALUES \((\d+),(\d+),(\d+),''([\d, ]+)'',(\d+),(\d+),"([\d, ]*)","([\d, ]*)","([\d, ]*)",(\d+),(\d+),(\d+),(\d+)\);'
$itemNames = @{}
foreach ($nm in [regex]::Matches([IO.File]::ReadAllText((Resolve-Path $Names)), "\( (\d+) , '((?:[^']|'')*)'")) { $itemNames[[int]$nm.Groups[1].Value] = $nm.Groups[2].Value }
$fixClinic = 0; $dropClinic = 0; $fixLibrary = 0
$seen = New-Object 'System.Collections.Generic.Dictionary[long,string]'
$sb = New-Object System.Text.StringBuilder (20MB)
[void]$sb.Append("[`n")
$rows = 0; $dupes = 0; $conflicts = 0; $bad = 0; $kept = 0
foreach ($line in [IO.File]::ReadLines((Resolve-Path $Sql))) {
  if (-not $line.StartsWith('INSERT')) { continue }
  $rows++
  $m = $re.Match($line)
  if (-not $m.Success) { $bad++; continue }
  $g = $m.Groups
  $src = [int]$g[1].Value; $tgt = [int]$g[2].Value
  $res = ($g[4].Value -split ',') | % { [int]$_.Trim() }
  if ($res.Count -lt 2) { $res = @($res[0], $res[0]) }
  $key = ([long]$src -shl 32) -bor [uint32]$tgt
  $sig = $line.Substring($line.IndexOf("'"))
  if ($seen.ContainsKey($key)) { if ($seen[$key] -eq $sig) { $dupes++ } else { $conflicts++ }; continue }
  $seen[$key] = $sig
  $stats = @($g[7].Value -split ',' | ? { $_.Trim() -ne '' } | % { [int]$_.Trim() })
  $pcts = @($g[8].Value -split ',' | ? { $_.Trim() -ne '' } | % { [int]$_.Trim() })
  $bumps = @($g[9].Value -split ',' | ? { $_.Trim() -ne '' } | % { [int]$_.Trim() })
  $skills = for ($i = 0; $i -lt $stats.Count; $i++) {
    $p = if ($i -lt $pcts.Count) { $pcts[$i] } else { 0 }
    $b = if ($i -lt $bumps.Count) { $bumps[$i] } else { 0 }
    "[$($stats[$i]),$p,$b]"
  }
  $range = [int]$g[5].Value
  $srcName = [string]$itemNames[$src]; $tgtName = [string]$itemNames[$tgt]
  if ($srcName -eq 'Implant Disassembly Clinic') {
    if ($tgtName -match 'Jobe|Refined') { $dropClinic++; continue }
    $skills = @($skills | % { if ($_ -eq '[165,425,0]') { $fixClinic++; '[165,475,0]' } else { $_ } })
  }
  if ($tgtName -like 'Instruction Disc*' -and $range -eq 25) { $range = 75; $fixLibrary++ }
  $del = [int]$g[6].Value
  $e = '{"Source":[' + $src + '],"Target":[' + $tgt + '],"Result":[' + $res[0] + ',' + $res[1] + ']'
  if ($del -band 1) { $e += ',"DeleteSource":true' }
  if (-not ($del -band 2)) { $e += ',"DeleteTarget":false' }
  if ([int]$g[3].Value -ne 0) { $e += ',"MinTargetQl":' + $g[3].Value }
  if ($range -ne 0) { $e += ',"QlRangePercent":' + $range }
  if ($skills) { $e += ',"Skills":[' + ($skills -join ',') + ']' }
  if ([int]$g[10].Value -ne 0) { $e += ',"MaxBump":' + $g[10].Value }
  if ([int]$g[11].Value -ne 0) { $e += ',"MinXp":' + $g[11].Value }
  if ([int]$g[12].Value -ne 0) { $e += ',"MaxXp":' + $g[12].Value }
  if ([int]$g[13].Value -ne 0) { $e += ',"Implant":true' }
  $e += '}'
  if ($kept -gt 0) { [void]$sb.Append(",`n") }
  [void]$sb.Append($e); $kept++
}
[void]$sb.Append("`n]`n")
[IO.File]::WriteAllText([IO.Path]::GetFullPath($Out), $sb.ToString(), (New-Object Text.UTF8Encoding $false))
"clinic B&E fixed=$fixClinic clinic Jobe/Refined dropped=$dropClinic library range fixed=$fixLibrary"
"rows=$rows kept=$kept duplicates=$dupes conflicts=$conflicts unparsed=$bad -> $Out ($([math]::Round((Get-Item $Out).Length/1MB,1)) MB)"


