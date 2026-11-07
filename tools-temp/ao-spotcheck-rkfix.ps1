# Spot-check deployed RK fix data. No secrets printed.
$ErrorActionPreference = 'Stop'
$mysql = 'I:\AO\mysql\server\bin\mysql.exe'
$cnf = 'I:\AO\secrets\aorebirth-client.cnf'
$db = 'cellao_codex_clean'
$gd = 'I:\AO\src\AORebirth\Built\Debug\ZoneEngine_New\GameData'
function Q([string]$sql) {
  (& $mysql --defaults-extra-file=$cnf -P 33070 -h 127.0.0.1 -N -e $sql $db | Out-String).Trim()
}

Write-Output '=== MAGS Slayerdroid (live GameData) ==='
$rk = Join-Path $gd 'NpcTemplates.RK.json'
if (-not (Test-Path $rk)) { Write-Output 'MISSING NpcTemplates.RK.json'; exit 2 }
$mags = Select-String -LiteralPath $rk -Pattern 'M\.A\.G\.S\. Slayerdroid|Slayerdroid|MAGS' -SimpleMatch:$false | Select-Object -First 8
if ($mags) { $mags | ForEach-Object { Write-Output $_.Line.Trim().Substring(0, [Math]::Min(160, $_.Line.Trim().Length)) } }
else { Write-Output 'NO MAGS/Slayerdroid match in live NpcTemplates.RK.json' }

Write-Output '=== Elliot Fairlane givenano 293685 ==='
$ef = Join-Path $gd 'Knubot\ElliotFairlane.json'
if (-not (Test-Path $ef)) { Write-Output 'MISSING ElliotFairlane.json'; exit 2 }
$raw = Get-Content -LiteralPath $ef -Raw
Write-Output ("exists=1 bytes={0}" -f $raw.Length)
if ($raw -match '293685') { Write-Output 'givenano_293685=PRESENT' } else { Write-Output 'givenano_293685=MISSING' }
Select-String -LiteralPath $ef -Pattern '293685|givenano' | Select-Object -First 6 | ForEach-Object { Write-Output $_.Line.Trim() }

Write-Output '=== NNC nano crystal pools (ItemTemplates.RK) ==='
$it = Join-Path $gd 'ItemTemplates.RK.json'
if (-not (Test-Path $it)) { Write-Output 'MISSING ItemTemplates.RK.json'; exit 2 }
$nnc = Select-String -LiteralPath $it -Pattern 'NNC[1-8]|NanoCrystal|nano.?crystal' -CaseSensitive:$false | Select-Object -First 12
if ($nnc) { $nnc | ForEach-Object { Write-Output $_.Line.Trim().Substring(0, [Math]::Min(180, $_.Line.Trim().Length)) } }
else { Write-Output 'NO NNC match in ItemTemplates.RK.json' }

Write-Output '=== DB NNC tables (information_schema) ==='
$tables = Q "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='$db' AND TABLE_NAME LIKE '%NNC%' ORDER BY TABLE_NAME"
Write-Output ("nnc_tables=`n{0}" -f $tables)
$tables2 = Q "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='$db' AND (TABLE_NAME LIKE '%nano%crystal%' OR TABLE_NAME LIKE '%NanoCrystal%' OR TABLE_NAME LIKE '%nnc%') ORDER BY TABLE_NAME"
Write-Output ("nano_crystalish=`n{0}" -f $tables2)

Write-Output '=== live Reck SpawnsAdd tag ==='
$sa = Join-Path $gd 'PlayfieldContent\750\SpawnsAdd.rk.json'
$saObj = Get-Content -LiteralPath $sa -Raw | ConvertFrom-Json
Write-Output ("source={0} count={1}" -f $saObj.source, @($saObj.spawns).Count)

Write-Output '=== zone bin hash vs zbuild ==='
$liveDll = 'I:\AO\src\AORebirth\Built\Debug\ZoneEngine_New\ZoneEngine_New.dll'
$srcDll = 'I:\AO\src\tools-temp\zbuild-ado\ZoneEngine_New.dll'
$h1 = (Get-FileHash -LiteralPath $liveDll -Algorithm SHA256).Hash
$h2 = (Get-FileHash -LiteralPath $srcDll -Algorithm SHA256).Hash
Write-Output ("live={0}" -f $h1)
Write-Output ("zbuild={0}" -f $h2)
Write-Output ("match={0}" -f ($h1 -eq $h2))
