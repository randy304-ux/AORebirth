$ErrorActionPreference = 'Stop'
$it = 'I:\AO\src\AORebirth\Built\Debug\ZoneEngine_New\GameData\ItemTemplates.RK.json'
$rk = 'I:\AO\src\AORebirth\Built\Debug\ZoneEngine_New\GameData\NpcTemplates.RK.json'
Write-Output '--- NNC pool keys in ItemTemplates.RK ---'
$matches = Select-String -LiteralPath $it -Pattern '"Hash"\s*:\s*"NNC[1-8]"|"Id"\s*:\s*"NNC[1-8]"|"Name"\s*:\s*"NNC[1-8]'
$matches | Group-Object { if ($_.Line -match 'NNC[1-8]') { $Matches[0] } } | Sort-Object Name | ForEach-Object { Write-Output ("{0} count={1}" -f $_.Name, $_.Count) }
Write-Output ("total_NNC_hash_lines={0}" -f @($matches).Count)
Write-Output '--- first NNC definitions context ---'
Select-String -LiteralPath $it -Pattern '"Hash"\s*:\s*"NNC1"' | Select-Object -First 3 | ForEach-Object {
  Write-Output ("line {0}: {1}" -f $_.LineNumber, $_.Line.Trim())
}
Write-Output '--- MAGS nearby names ---'
Select-String -LiteralPath $rk -Pattern 'M\.A\.G\.S' | Select-Object -First 5 | ForEach-Object {
  Write-Output ("line {0}: {1}" -f $_.LineNumber, $_.Line.Trim())
}
Write-Output '--- Slayerdroid names ---'
Select-String -LiteralPath $rk -Pattern 'Slayerdroid' | Select-Object -First 15 | ForEach-Object {
  Write-Output ("line {0}: {1}" -f $_.LineNumber, $_.Line.Trim())
}
