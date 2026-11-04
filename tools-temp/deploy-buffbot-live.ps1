# STAGE deploy: live-style Buffbot one-tell blob + Motd text (Config.xml).
# Commits on cursor/buffbot-live-blob-1b5e (off d500e0f6 + 324fc351).
#
# DO NOT run while players are online. Aborts if Online>0.
# Stops ZoneEngine_New ONLY via shutdown-file; Chat/Login stay up.
# Starts Zone via schtasks "\DungeonDogs\AO Dungeon Dogs Engines".
# Motd lives in ChatEngine memory — Config.xml is updated on disk; Chat must be
# restarted later (separate empty-server window) before Motd text appears in game.
#
# House rules: backup DB to I:\AO\backups, require prior boot age >= 5m, never touch
# 3306/8080/3000 or I:\War, never deploy ddogs/mercs-1.

$ErrorActionPreference = 'Stop'
$outLog = 'I:\AO\src\tools-temp\deploy-buffbot-live.out.txt'
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

$zSrc = 'I:\AO\src\tools-temp\zbuild-buffbot-live'
$zDest = 'I:\AO\src\AORebirth\Built\Debug\ZoneEngine_New'
$cfgSrc = 'I:\AO\src\AORebirth\Config\Config.xml'
$cfgDest = 'I:\AO\src\AORebirth\Built\Debug\Config.xml'
$zlog = 'I:\AO\src\logs\engines\ZoneEngine_New.out.log'
$shutdownFile = 'I:\AO\src\logs\engines\ZoneEngine_New.shutdown'
$hash = (git -C I:\AO\src rev-parse --short HEAD).Trim()
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$bakDir = "I:\AO\backups\zone-bins-$stamp-$hash"
$backup = "I:\AO\backups\$db-$stamp-buffbot-live.sql"
New-Item -ItemType Directory -Force -Path $bakDir | Out-Null
New-Item -ItemType Directory -Force -Path 'I:\AO\backups' | Out-Null

foreach ($n in @('ZoneEngine_New.dll','ZoneEngine_New.exe','ZoneEngine_New.pdb')) {
  if (-not (Test-Path (Join-Path $zSrc $n))) { throw "missing build artifact $n in $zSrc — run build-buffbot-live.ps1 first" }
}
if (-not (Test-Path $cfgSrc)) { throw "missing $cfgSrc" }

$online = Q 'SELECT COUNT(*) FROM characters WHERE Online=1'
Say ("pre-stop online={0}" -f $online)
if ($online -ne '0') { Say 'ABORT online players — not deploying'; exit 2 }

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

Say ("backup live bins -> {0}" -f $bakDir)
Copy-Item (Join-Path $zDest 'ZoneEngine_New.dll') $bakDir -Force
Copy-Item (Join-Path $zDest 'ZoneEngine_New.pdb') $bakDir -Force
Copy-Item (Join-Path $zDest 'ZoneEngine_New.exe') $bakDir -Force
if (Test-Path $cfgDest) { Copy-Item $cfgDest (Join-Path $bakDir 'Config.xml') -Force }

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
Copy-Item $cfgSrc $cfgDest -Force
$h = (Get-FileHash (Join-Path $zDest 'ZoneEngine_New.dll') -Algorithm SHA256).Hash
$hSrc = (Get-FileHash (Join-Path $zSrc 'ZoneEngine_New.dll') -Algorithm SHA256).Hash
if ($h -ne $hSrc) { throw 'dll hash mismatch after copy' }
Say ("installed dll sha={0}" -f $h)
Say ("installed Config.xml Motd (Chat restart needed later for Motd text)")

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
  Say 'BOOT FAIL — restoring previous bins'
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
  if (Test-Path (Join-Path $bakDir 'Config.xml')) { Copy-Item (Join-Path $bakDir 'Config.xml') $cfgDest -Force }
  if (Test-Path $shutdownFile) { Remove-Item $shutdownFile -Force }
  cmd /c 'schtasks /run /tn "\DungeonDogs\AO Dungeon Dogs Engines"'
  exit 4
}

$bb = (($boot -split "`n") | Where-Object { $_ -match 'Buffbot catalog' }) -join ' | '
$errs = @(($boot -split "`n") | Where-Object { ($_ -match '\[Error\]' -or $_ -match '\bERROR\b' -or $_ -match 'Exception') -and ($_ -notmatch 'rejected=0') } | Select-Object -First 12)
Say ("BUFFBOT {0}" -f $bb)
foreach ($e in $errs) { Say ("ERRLINE {0}" -f $e.Trim()) }
$zone = Get-Process ZoneEngine_New -ErrorAction SilentlyContinue | Select-Object -First 1
Say ("zone pid={0}" -f $(if($zone){$zone.Id}else{0}))

$meta = 'I:\AO\src\tools-temp\deploy-buffbot-live-meta.txt'
@(
  "commit=$hash"
  "pid_zone=$(if($zone){$zone.Id}else{0})"
  "build=$hash (zbuild-buffbot-live)"
  "dll_sha256=$h"
  "db_backup=$backup"
  "bin_backup=$bakDir"
  "buffbot=$bb"
  "notes=live-style one-tell Buff Menu; Motd on disk needs Chat restart"
) -join "`r`n" | Set-Content $meta -Encoding ascii
Say 'DEPLOY OK'
Get-Content $meta
exit 0
