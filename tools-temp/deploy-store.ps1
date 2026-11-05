# Deploy Claim Center store catalog (claims.staged.json -> claims.json) + ZoneEngine bins from
# tools-temp\zbuild-store + account-api publish. DO NOT RUN until Mike authorizes.
# House rules: online=0, DB backup to I:\AO\backups first, boot age >=5m, shutdown-file stop,
# then schtasks /run /tn "\DungeonDogs\AO Dungeon Dogs Engines"; verify Zone 7501, Chat 7012, Login 7500.
$ErrorActionPreference = 'Stop'
$outLog = 'I:\AO\src\tools-temp\deploy-store.out.txt'
$metaPath = 'I:\AO\src\tools-temp\deploy-store-meta.txt'
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

$root = 'I:\AO\src'
$zSrc = Join-Path $root 'tools-temp\zbuild-store'
$rollbackSrc = Join-Path $root 'tools-temp\rb-store'
$zDest = Join-Path $root 'AORebirth\Built\Debug\ZoneEngine_New'
$gdSrc = Join-Path $root 'AORebirth\GameData'
$zDestGd = Join-Path $zDest 'GameData'
$zShutdown = Join-Path $root 'logs\engines\ZoneEngine_New.shutdown'
$cShutdown = Join-Path $root 'logs\engines\ChatEngine.shutdown'
$zlog = Join-Path $root 'logs\engines\ZoneEngine_New.out.log'
$exe = Join-Path $zDest 'ZoneEngine_New.exe'
$storeDir = Join-Path $root 'ddogs\store'
$stagedClaims = Join-Path $storeDir 'claims.staged.json'
$liveClaims = Join-Path $storeDir 'claims.json'
$apiLive = 'I:\AO\tools\account-api\bin-publish'
$apiNew = Join-Path $zSrc 'account-api'
$hash = (git -C $root rev-parse --short HEAD).Trim()
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$backup = "I:\AO\backups\$db-$stamp-store-$hash.sql"

Say ("deploy tip={0}" -f $hash)

foreach ($n in @('ZoneEngine_New.dll', 'ZoneEngine_New.exe', 'ZoneEngine_New.pdb')) {
  if (-not (Test-Path (Join-Path $zSrc $n))) { throw "missing build artifact $n in $zSrc - run store-rebuild.ps1 first" }
}
if (-not (Test-Path (Join-Path $apiNew 'account-api.dll'))) { throw "missing $apiNew\account-api.dll - publish account-api into zbuild-store first" }
if (-not (Test-Path $stagedClaims)) { throw "missing $stagedClaims" }
$staged = Get-Content -LiteralPath $stagedClaims -Raw | ConvertFrom-Json
$stagedCount = @($staged.items).Count
if ($stagedCount -lt 85) { throw "claims.staged.json item count $stagedCount looks short" }
Say ("claims.staged items={0}" -f $stagedCount)

if (Test-Path $rollbackSrc) { throw "rb-store already exists; move it aside first" }
New-Item -ItemType Directory -Path $rollbackSrc, (Join-Path $rollbackSrc 'account-api') | Out-Null
foreach ($n in @('ZoneEngine_New.dll', 'ZoneEngine_New.exe', 'ZoneEngine_New.pdb')) {
  Copy-Item (Join-Path $zDest $n) $rollbackSrc -Force
}
Copy-Item $liveClaims (Join-Path $rollbackSrc 'claims.json') -Force
Get-ChildItem $apiLive -File | Copy-Item -Destination (Join-Path $rollbackSrc 'account-api') -Force
Say 'rollback snapshot -> rb-store (bins, claims.json, account-api)'

function Swap-AccountApi([string]$from) {
  Stop-ScheduledTask -TaskPath '\DungeonDogs\' -TaskName 'AO Dungeon Dogs Account API' -ErrorAction SilentlyContinue
  Start-Sleep -Seconds 5
  Get-ChildItem $from -File | Copy-Item -Destination $apiLive -Force
  Start-ScheduledTask -TaskPath '\DungeonDogs\' -TaskName 'AO Dungeon Dogs Account API'
  Start-Sleep -Seconds 15
}

$online = Q 'SELECT COUNT(*) FROM characters WHERE Online=1'
Say ("online={0}" -f $online)
if ($online -ne '0') { Say 'ABORT online!=0'; exit 2 }

if (-not (Test-Path 'I:\AO\backups')) { New-Item -ItemType Directory 'I:\AO\backups' | Out-Null }
Say ("backup DB -> {0}" -f $backup)
& $mysqldump --defaults-extra-file=$cnf -P 33070 -h 127.0.0.1 --single-transaction --no-tablespaces --result-file=$backup $db
if ($LASTEXITCODE -ne 0) { throw "mysqldump $LASTEXITCODE" }
$blen = (Get-Item -LiteralPath $backup).Length
Say ("backup bytes={0}" -f $blen)
if ($blen -lt 1000000) { throw 'backup too small' }

$zone = Get-Process ZoneEngine_New -ErrorAction SilentlyContinue | Select-Object -First 1
$chat = Get-Process ChatEngine -ErrorAction SilentlyContinue | Select-Object -First 1
$login = Get-Process LoginEngine -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $login) { throw 'LoginEngine must stay up' }
Say ("pre-stop zone={0} chat={1} login={2}" -f $(if ($zone) { $zone.Id } else { 0 }), $(if ($chat) { $chat.Id } else { 0 }), $login.Id)
foreach ($p in @($zone, $chat)) {
  if ($p -and ((Get-Date) - $p.StartTime).TotalMinutes -lt 5) { Say 'ABORT uptime<5m'; exit 2 }
}
$bootLog = 'I:\AO\logs\boot-engines.log'
if (Test-Path $bootLog) {
  $sinceBoot = (Get-Date) - (Get-Item $bootLog).LastWriteTime
  Say ('last boot-engines.log write {0:N1}m ago' -f $sinceBoot.TotalMinutes)
  if ($sinceBoot.TotalMinutes -lt 5) { Say 'ABORT last boot under 5 min'; exit 2 }
}

function Stop-ViaShutdown([string]$name, [string]$shutdownFile) {
  if (-not (Get-Process $name -ErrorAction SilentlyContinue)) { Say ("$name already down"); return }
  Say ("stop $name")
  "stop requested $(Get-Date -Format o)" | Set-Content -Path $shutdownFile -Encoding UTF8
  $dl = (Get-Date).AddSeconds(90)
  while ((Get-Date) -lt $dl -and (Get-Process $name -ErrorAction SilentlyContinue)) { Start-Sleep 2 }
  Start-Sleep 2
  if (Get-Process $name -ErrorAction SilentlyContinue) {
    Get-Process $name | ForEach-Object { Stop-Process -Id $_.Id -Force }
    Start-Sleep 2
  }
  if (Get-Process $name -ErrorAction SilentlyContinue) { throw "$name still up" }
}
$loginId = $login.Id
Stop-ViaShutdown 'ZoneEngine_New' $zShutdown
Stop-ViaShutdown 'ChatEngine' $cShutdown
if (-not (Get-Process -Id $loginId -ErrorAction SilentlyContinue)) { throw 'Login died during stop' }

Copy-Item (Join-Path $zSrc 'ZoneEngine_New.dll') $zDest -Force
Copy-Item (Join-Path $zSrc 'ZoneEngine_New.pdb') $zDest -Force
Copy-Item (Join-Path $zSrc 'ZoneEngine_New.exe') $zDest -Force
$h = (Get-FileHash (Join-Path $zDest 'ZoneEngine_New.dll') -Algorithm SHA256).Hash
$hSrc = (Get-FileHash (Join-Path $zSrc 'ZoneEngine_New.dll') -Algorithm SHA256).Hash
if ($h -ne $hSrc) { throw 'dll hash mismatch after copy' }
Say ("installed dll sha={0}" -f $h)

Copy-Item $stagedClaims $liveClaims -Force
Say 'claims.json <- claims.staged.json'
Swap-AccountApi $apiNew
Say 'account-api swapped to staged publish'

Say 'robocopy GameData -> Built ZoneEngine_New\GameData'
cmd /c ("robocopy `"{0}`" `"{1}`" /E /NJH /NJS /NDL /NP /R:1 /W:1" -f $gdSrc, $zDestGd) | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy zone gd $LASTEXITCODE" }

$online = Q 'SELECT COUNT(*) FROM characters WHERE Online=1'
Say ("pre-start online={0}" -f $online)
if ($online -ne '0') { Say 'ABORT online!=0'; exit 2 }

if (Test-Path $zShutdown) { Remove-Item $zShutdown -Force }
if (Test-Path $cShutdown) { Remove-Item $cShutdown -Force }
$zLogLen = 0
if (Test-Path $zlog) { $zLogLen = (Get-Item $zlog).Length }

Say 'start engines via schtasks \DungeonDogs\AO Dungeon Dogs Engines'
$st = schtasks /Run /TN '\DungeonDogs\AO Dungeon Dogs Engines' 2>&1 | Out-String
Say ("schtasks: {0}" -f $st.Trim())

$deadline = (Get-Date).AddSeconds(180)
$zone2 = $null; $chat2 = $null; $login2 = $null
$zoneUp = $false
while ((Get-Date) -lt $deadline) {
  $zone2 = Get-Process ZoneEngine_New -ErrorAction SilentlyContinue | Select-Object -First 1
  $chat2 = Get-Process ChatEngine -ErrorAction SilentlyContinue | Select-Object -First 1
  $login2 = Get-Process LoginEngine -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($zone2 -and (Test-Path $zlog)) {
    $fs = [IO.File]::Open($zlog, 'Open', 'Read', 'ReadWrite')
    try {
      if ($fs.Length -gt $zLogLen) {
        $fs.Seek($zLogLen, 'Begin') | Out-Null
        $sr = New-Object IO.StreamReader($fs)
        $fresh = $sr.ReadToEnd()
        $sr.Close()
        if ($fresh -match 'Failed to load|catalog empty') { throw ("FAIL-FAST boot: " + ((($fresh -split "`n") | Where-Object { $_ -match 'Failed to load|catalog empty' } | Select-Object -First 2) -join ' || ')) }
        if ($fresh -match 'listening on 0\.0\.0\.0:7501' -and ($fresh -match 'root container started' -or $fresh -match 'ZONEENGINE_NEW_READY')) {
          $zoneUp = $true
        }
      }
    } finally { $fs.Close() }
  }
  if ($zoneUp -and $chat2 -and $login2) { break }
  Start-Sleep 3
}
if (-not $zone2 -or -not $zoneUp) { throw 'ZoneEngine_New failed to start / listen 7501' }
if (-not $chat2) { throw 'ChatEngine failed to start' }
if (-not $login2) { throw 'LoginEngine not running after schtasks start' }
Say ("post-start zone={0} chat={1} login={2}" -f $zone2.Id, $chat2.Id, $login2.Id)

# Prefer netstat - Get-NetTCPConnection can hang on WMI
$zPort = (netstat -ano | Select-String ':7501\s+.*LISTENING' | Select-Object -First 1)
$cPort = (netstat -ano | Select-String ':7012\s+.*LISTENING' | Select-Object -First 1)
$lPort = (netstat -ano | Select-String ':7500\s+.*LISTENING' | Select-Object -First 1)
if (-not $zPort) { throw 'Zone 7501 not LISTENING' }
if (-not $cPort) { throw 'Chat 7012 not LISTENING' }
if (-not $lPort) { throw 'Login 7500 not LISTENING' }
Say ("listen Zone7501={0}" -f $zPort.Line.Trim())
Say ("listen Chat7012={0}" -f $cPort.Line.Trim())
Say ("listen Login7500={0}" -f $lPort.Line.Trim())

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'I:\AO\src\ddogs\tools\Test-StorePages.ps1'
$storeExit = $LASTEXITCODE
Say ("Test-StorePages exit={0}" -f $storeExit)
if ($storeExit -ne 0) { throw "Test-StorePages failed exit=$storeExit" }

@(
  "tip=$hash"
  "db_backup=$backup"
  "dll_sha256=$h"
  "claims_staged_items=$stagedCount"
  "claims=claims.staged.json -> claims.json"
  "pid_zone=$($zone2.Id)"
  "pid_chat=$($chat2.Id)"
  "pid_login=$($login2.Id)"
  "listen7501=$($zPort.Line.Trim())"
  "listen7012=$($cPort.Line.Trim())"
  "listen7500=$($lPort.Line.Trim())"
  "storepages_exit=$storeExit"
  "when=$(Get-Date -Format o)"
) | Set-Content -LiteralPath $metaPath -Encoding ascii
Say ("meta {0}" -f $metaPath)
Say 'DEPLOY OK (store/claim). Mike: live-verify Claim Center + Pack of Beacons.'
Get-Content -LiteralPath $metaPath
exit 0
