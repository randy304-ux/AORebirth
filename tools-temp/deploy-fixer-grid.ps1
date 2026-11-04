# STAGE deploy: Fixer Grid SpawnItem hashes (FIGI Data Receptacle 160978, QIGR Quiescent
# Grid Waveform Template 265479) via ItemTemplates.FixerGrid.json overlay.
#
# DO NOT run while players are online. Aborts if Online>0.
# Stops ZoneEngine_New only via shutdown-file; Chat/Login stay up.
# Starts Zone via schtasks "\DungeonDogs\AO Dungeon Dogs Engines" (boot-engines.cmd).
# Copies bins from tools-temp\zbuild-fixer-grid and GameData\ItemTemplates.FixerGrid.json.
#
# Do NOT run this script from the agent unless Mike/Randy explicitly asks.

$ErrorActionPreference = 'Stop'
$outLog = 'I:\AO\src\tools-temp\deploy-fixer-grid.out.txt'
Set-Content -LiteralPath $outLog -Value '' -Encoding ascii
function Say([string]$m) {
  $line = '{0} {1}' -f (Get-Date -Format 'HH:mm:ss'), $m
  Write-Output $line
  Add-Content -LiteralPath $outLog -Value $line
}

$mysql = 'I:\AO\mysql\server\bin\mysql.exe'
$mysqldump = 'I:\AO\mysql\server\bin\mysqldump.exe'
$cnf = 'I:\AO\secrets\aorebirth-client.cnf'
$db = 'cellao_codex_clean'
function Q([string]$sql) {
  (& $mysql --defaults-extra-file=$cnf -P 33070 -h 127.0.0.1 -N -e $sql $db | Out-String).Trim()
}

$zSrc = 'I:\AO\src\tools-temp\zbuild-fixer-grid'
$zDest = 'I:\AO\src\AORebirth\Built\Debug\ZoneEngine_New'
$overlaySrc = 'I:\AO\src\AORebirth\GameData\ItemTemplates.FixerGrid.json'
$overlayDest = Join-Path $zDest 'GameData\ItemTemplates.FixerGrid.json'
$zlog = 'I:\AO\src\logs\engines\ZoneEngine_New.out.log'
$shutdownFile = 'I:\AO\src\logs\engines\ZoneEngine_New.shutdown'
$stderrLog = 'I:\AO\src\logs\engines\ZoneEngine_New.err.log'
$exe = Join-Path $zDest 'ZoneEngine_New.exe'
$hash = (git -C I:\AO\src rev-parse --short HEAD).Trim()
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$bakDir = "I:\AO\backups\zone-bins-$stamp-$hash-fixer-grid"
$backup = "I:\AO\backups\$db-$stamp-fixer-grid.sql"
New-Item -ItemType Directory -Force -Path $bakDir | Out-Null
New-Item -ItemType Directory -Force -Path 'I:\AO\backups' | Out-Null

foreach ($n in @('ZoneEngine_New.dll','ZoneEngine_New.exe','ZoneEngine_New.pdb')) {
  if (-not (Test-Path (Join-Path $zSrc $n))) { throw "missing build artifact $n in $zSrc" }
}
if (-not (Test-Path $overlaySrc)) { throw "missing $overlaySrc" }
$overlayInBuild = Join-Path $zSrc 'GameData\ItemTemplates.FixerGrid.json'
if (-not (Test-Path $overlayInBuild)) { throw "missing staged overlay $overlayInBuild" }

$online = Q 'SELECT COUNT(*) FROM characters WHERE Online=1'
Say ("pre-stop online={0}" -f $online)
if ($online -ne '0') { Say 'ABORT online players - not deploying'; exit 2 }

Say ("backup DB -> {0}" -f $backup)
& $mysqldump --defaults-extra-file=$cnf -P 33070 -h 127.0.0.1 --single-transaction --no-tablespaces --result-file=$backup $db
if ($LASTEXITCODE -ne 0) { throw "mysqldump $LASTEXITCODE" }
$blen = (Get-Item -LiteralPath $backup).Length
Say ("backup bytes={0}" -f $blen)
if ($blen -lt 1000000) { throw 'backup too small' }

$zone = Get-Process ZoneEngine_New -ErrorAction SilentlyContinue | Select-Object -First 1
$chat = Get-Process ChatEngine -ErrorAction SilentlyContinue | Select-Object -First 1
$login = Get-Process LoginEngine -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $chat -or -not $login) { throw 'Chat/Login must stay up' }
Say ("pre-stop zone={0} chat={1} login={2}" -f $(if($zone){$zone.Id}else{0}), $chat.Id, $login.Id)

$zp0 = Get-Process ZoneEngine_New -ErrorAction SilentlyContinue | Select-Object -First 1
if ($zp0 -and ((Get-Date) - $zp0.StartTime).TotalMinutes -lt 5) { Say 'ABORT uptime<5m'; exit 2 }
$bootLog = 'I:\AO\logs\boot-engines.log'
if (Test-Path $bootLog) {
  $sinceBoot = (Get-Date) - (Get-Item $bootLog).LastWriteTime
  Say ("last boot-engines.log write {0:N1}m ago" -f $sinceBoot.TotalMinutes)
  if ($sinceBoot.TotalMinutes -lt 5) { Say 'ABORT last boot under 5 min'; exit 2 }
}

Say ("backup live bins + overlay -> {0}" -f $bakDir)
Copy-Item (Join-Path $zDest 'ZoneEngine_New.dll') $bakDir -Force
Copy-Item (Join-Path $zDest 'ZoneEngine_New.pdb') $bakDir -Force
Copy-Item (Join-Path $zDest 'ZoneEngine_New.exe') $bakDir -Force
if (Test-Path $overlayDest) { Copy-Item $overlayDest (Join-Path $bakDir 'ItemTemplates.FixerGrid.json') -Force }

Say 'stop ZoneEngine_New ONLY (shutdown-file)'
"stop requested $(Get-Date -Format o)" | Set-Content -Path $shutdownFile -Encoding UTF8
$dl=(Get-Date).AddSeconds(90); while((Get-Date) -lt $dl -and (Get-Process ZoneEngine_New -ErrorAction SilentlyContinue)){ Start-Sleep 2 }
Say ('after shutdown-file zone_up={0}' -f [bool](Get-Process ZoneEngine_New -ErrorAction SilentlyContinue))
Start-Sleep -Seconds 3
if (Get-Process ZoneEngine_New -ErrorAction SilentlyContinue) {
  Get-Process ZoneEngine_New | ForEach-Object { Stop-Process -Id $_.Id -Force }
  Start-Sleep -Seconds 3
}
if (Get-Process ZoneEngine_New -ErrorAction SilentlyContinue) { throw 'zone still up' }
$chat = Get-Process -Id $chat.Id -ErrorAction SilentlyContinue
$login = Get-Process -Id $login.Id -ErrorAction SilentlyContinue
if (-not $chat -or -not $login) { throw 'Chat/Login died during zone stop' }
Say ("after-stop chat={0} login={1}" -f $chat.Id, $login.Id)

Copy-Item (Join-Path $zSrc 'ZoneEngine_New.dll') $zDest -Force
Copy-Item (Join-Path $zSrc 'ZoneEngine_New.pdb') $zDest -Force
Copy-Item (Join-Path $zSrc 'ZoneEngine_New.exe') $zDest -Force
New-Item -ItemType Directory -Force -Path (Split-Path $overlayDest) | Out-Null
Copy-Item $overlaySrc $overlayDest -Force
$h = (Get-FileHash (Join-Path $zDest 'ZoneEngine_New.dll') -Algorithm SHA256).Hash
$hSrc = (Get-FileHash (Join-Path $zSrc 'ZoneEngine_New.dll') -Algorithm SHA256).Hash
if ($h -ne $hSrc) { throw 'dll hash mismatch after copy' }
Say ("installed dll sha={0}" -f $h)
Say ("installed ItemTemplates.FixerGrid.json sha={0}" -f (Get-FileHash $overlayDest -Algorithm SHA256).Hash)

$online = Q 'SELECT COUNT(*) FROM characters WHERE Online=1'
Say ("pre-start online={0}" -f $online)
if ($online -ne '0') { Say 'ABORT online before start'; exit 2 }

if (Test-Path $shutdownFile) { Remove-Item $shutdownFile -Force }
$logLenBefore = 0
if (Test-Path $zlog) { $logLenBefore = (Get-Item $zlog).Length }

Say 'start via schtasks \DungeonDogs\AO Dungeon Dogs Engines'
cmd /c 'schtasks /run /tn "\DungeonDogs\AO Dungeon Dogs Engines"'
if ($LASTEXITCODE -ne 0) { throw "schtasks exit $LASTEXITCODE" }

$listening = $false
$boot = ''
$deadline = (Get-Date).AddSeconds(180)
while ((Get-Date) -lt $deadline) {
  $zp = Get-Process ZoneEngine_New -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $zp) { Start-Sleep 2; continue }
  if (-not (Test-Path $zlog)) { Start-Sleep 2; continue }
  $fs = [IO.File]::Open($zlog, 'Open', 'Read', 'ReadWrite')
  if ($fs.Length -gt $logLenBefore) {
    $fs.Seek($logLenBefore, 'Begin') | Out-Null
    $sr = New-Object IO.StreamReader($fs)
    $boot = $sr.ReadToEnd()
    $sr.Close(); $fs.Close()
    if ($boot -match 'listening on 0\.0\.0\.0:7501' -and ($boot -match 'root container started' -or $boot -match 'ZONEENGINE_NEW_READY')) {
      $listening = $true
      break
    }
  } else { $fs.Close() }
  Start-Sleep -Seconds 2
}

if (-not $listening) {
  Say 'BOOT FAIL - restoring previous bins + overlay'
  if (Get-Process ZoneEngine_New -ErrorAction SilentlyContinue) {
    "stop requested $(Get-Date -Format o)" | Set-Content -Path $shutdownFile -Encoding UTF8
    $dl=(Get-Date).AddSeconds(60); while((Get-Date) -lt $dl -and (Get-Process ZoneEngine_New -ErrorAction SilentlyContinue)){ Start-Sleep 2 }
    if (Get-Process ZoneEngine_New -ErrorAction SilentlyContinue) {
      Get-Process ZoneEngine_New | ForEach-Object { Stop-Process -Id $_.Id -Force }
      Start-Sleep 2
    }
  }
  Copy-Item (Join-Path $bakDir 'ZoneEngine_New.dll') $zDest -Force
  Copy-Item (Join-Path $bakDir 'ZoneEngine_New.pdb') $zDest -Force
  Copy-Item (Join-Path $bakDir 'ZoneEngine_New.exe') $zDest -Force
  $bakOverlay = Join-Path $bakDir 'ItemTemplates.FixerGrid.json'
  if (Test-Path $bakOverlay) {
    Copy-Item $bakOverlay $overlayDest -Force
  } elseif (Test-Path $overlayDest) {
    Remove-Item $overlayDest -Force
  }
  if (Test-Path $shutdownFile) { Remove-Item $shutdownFile -Force }
  cmd /c 'schtasks /run /tn "\DungeonDogs\AO Dungeon Dogs Engines"'
  exit 4
}

$hashes = (($boot -split "`n") | Where-Object { $_ -match '(?i)hash templates|ItemTemplates|FIGI|Fixer' }) -join ' | '
$errs = @(($boot -split "`n") | Where-Object { ($_ -match '\[Error\]' -or $_ -match '\bERROR\b' -or $_ -match 'Exception') -and ($_ -notmatch 'rejected=0') } | Select-Object -First 12)
Say ("HASHCATALOG {0}" -f $hashes)
foreach ($e in $errs) { Say ("ERRLINE {0}" -f $e.Trim()) }
$zone = Get-Process ZoneEngine_New -ErrorAction SilentlyContinue | Select-Object -First 1
Say ("zone pid={0}" -f $(if($zone){$zone.Id}else{0}))

$meta = 'I:\AO\src\tools-temp\deploy-fixer-grid-meta.txt'
@(
  "commit=$hash"
  "pid_zone=$(if($zone){$zone.Id}else{0})"
  "build=$hash (zbuild-fixer-grid)"
  "dll_sha256=$h"
  "db_backup=$backup"
  "bin_backup=$bakDir"
  "overlay=ItemTemplates.FixerGrid.json FIGI=160978 QIGR=265479"
  "hashcatalog=$hashes"
) -join "`r`n" | Set-Content $meta -Encoding ascii
Say 'DEPLOY OK'
Get-Content $meta
exit 0
