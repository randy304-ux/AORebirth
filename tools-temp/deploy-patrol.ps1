# Deploy NPC patrol import (spawn-entry patrolWaypoints + ZoneEngine_New patrol walk/pause/resume).
# Source bins: tools-temp\zbuild-patrol. GameData: Spawns.json + SpawnsAdd*.json with patrolWaypoints.
# Authorized GO for Randy. No WMI/CIM. No schema migration. MySQL 33070 only.
# Engines start ONLY via: schtasks /run /tn "\DungeonDogs\AO Dungeon Dogs Engines"
# Timed tasklist/netstat only (Start-Process + WaitForExit). Leave Docker API Watchdog disabled.
# Rollback bins/GameData from tools-temp\rb-patrol ONLY if ZoneEngine crashes after boot.
$ErrorActionPreference = 'Stop'
$outLog = 'I:\AO\src\tools-temp\deploy-patrol.out.txt'
$metaPath = 'I:\AO\src\tools-temp\deploy-patrol-meta.txt'
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

function TaskPid([string]$image) {
  $tmp = Join-Path $env:TEMP ("ao-tl-{0}.txt" -f [guid]::NewGuid().ToString('n'))
  $err = "$tmp.err"
  $result = 0
  try {
    $p = Start-Process -FilePath 'cmd.exe' -ArgumentList @('/d','/c',("tasklist /FI `"IMAGENAME eq {0}`" /NH /FO CSV" -f $image)) -NoNewWindow -PassThru -RedirectStandardOutput $tmp -RedirectStandardError $err
    if (-not $p.WaitForExit(20000)) {
      try { $p.Kill() } catch {}
      Add-Content -LiteralPath $outLog -Value ("{0} WARN tasklist hung for {1}" -f (Get-Date -Format 'HH:mm:ss'), $image)
      $result = 0
    } else {
      $o = Get-Content -LiteralPath $tmp -Raw -ErrorAction SilentlyContinue
      if ($o -match '"[^"]+","(\d+)"') { $result = [int]$Matches[1] }
    }
  } finally {
    Remove-Item -LiteralPath $tmp,$err -Force -ErrorAction SilentlyContinue
  }
  return ,$result
}
function NetListenPid([int]$port) {
  $tmp = Join-Path $env:TEMP ("ao-ns-{0}.txt" -f [guid]::NewGuid().ToString('n'))
  $err = "$tmp.err"
  try {
    $p = Start-Process -FilePath 'cmd.exe' -ArgumentList @('/d','/c',("netstat -ano -p tcp | findstr `":{0}`"" -f $port)) -NoNewWindow -PassThru -RedirectStandardOutput $tmp -RedirectStandardError $err
    if (-not $p.WaitForExit(20000)) {
      try { $p.Kill() } catch {}
      return 0
    }
    $o = Get-Content -LiteralPath $tmp -Raw -ErrorAction SilentlyContinue
    foreach ($line in ($o -split "`r?`n")) {
      if ($line -match 'LISTENING\s+(\d+)\s*$') { return [int]$Matches[1] }
    }
    return 0
  } finally {
    Remove-Item -LiteralPath $tmp,$err -Force -ErrorAction SilentlyContinue
  }
}

$root = 'I:\AO\src'
$gdSrc = Join-Path $root 'AORebirth\GameData'
$zSrc = Join-Path $root 'tools-temp\zbuild-patrol'
$zDest = Join-Path $root 'AORebirth\Built\Debug\ZoneEngine_New'
$zDestGd = Join-Path $zDest 'GameData'
$builtGd = Join-Path $root 'AORebirth\Built\Debug\GameData'
$zShutdown = Join-Path $root 'logs\engines\ZoneEngine_New.shutdown'
$cShutdown = Join-Path $root 'logs\engines\ChatEngine.shutdown'
$lShutdown = Join-Path $root 'logs\engines\LoginEngine.shutdown'
$zExe = Join-Path $zDest 'ZoneEngine_New.exe'
$hash = (git -C $root rev-parse --short=8 HEAD).Trim()
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$backup = "I:\AO\backups\cellao_codex_clean-$stamp-patrol.sql"
$rollbackDir = Join-Path $root 'tools-temp\rb-patrol'
$validationPath = Join-Path $root 'tools-temp\patrol-import\validation.json'

if (-not (Test-Path -LiteralPath $validationPath)) { throw "missing $validationPath" }
$validation = Get-Content -LiteralPath $validationPath -Raw | ConvertFrom-Json
if (-not $validation.pass) { throw 'validation.json pass!=true — refuse deploy' }

$stageFiles = New-Object System.Collections.Generic.List[string]
foreach ($f in @($validation.dirtyFiles)) {
  if ([string]::IsNullOrWhiteSpace($f)) { continue }
  $rel = ($f -replace '/', '\')
  [void]$stageFiles.Add($rel)
}
if ($stageFiles.Count -eq 0) { throw 'no dirty GameData files listed in validation.json' }

foreach ($f in $stageFiles) {
  $src = Join-Path $gdSrc $f
  if (-not (Test-Path -LiteralPath $src)) { throw "missing $src" }
}
if (-not (Test-Path -LiteralPath $zExe)) { throw "missing $zExe" }
foreach ($n in @('ZoneEngine_New.dll', 'ZoneEngine_New.exe')) {
  if (-not (Test-Path (Join-Path $zSrc $n))) { throw "missing $n in $zSrc" }
}

Say ("deploy tip={0} patrol-files={1} matched={2} unmatched={3}" -f $hash, $stageFiles.Count, $validation.matched, $validation.unmatched)

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

if (-not (Test-Path $rollbackDir)) {
  New-Item -ItemType Directory -Force $rollbackDir | Out-Null
}
foreach ($f in $stageFiles) {
  $live = Join-Path $zDestGd $f
  if (Test-Path -LiteralPath $live) {
    $dst = Join-Path $rollbackDir $f
    $dstDir = Split-Path $dst -Parent
    if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory $dstDir | Out-Null }
    Copy-Item -LiteralPath $live -Destination $dst -Force
  }
}
foreach ($n in @('ZoneEngine_New.dll', 'ZoneEngine_New.exe', 'ZoneEngine_New.pdb')) {
  $live = Join-Path $zDest $n
  if (Test-Path -LiteralPath $live) {
    $binRb = Join-Path $rollbackDir '_bins'
    if (-not (Test-Path $binRb)) { New-Item -ItemType Directory $binRb | Out-Null }
    Copy-Item -LiteralPath $live -Destination (Join-Path $binRb $n) -Force
  }
}
Say ("rollback GameData+bins snapshot -> {0}" -f $rollbackDir)

# Keep Docker API Watchdog disabled (do not enable it during this deploy).
$dwQ = & schtasks.exe /Query /TN '\Docker API Watchdog' /FO LIST 2>&1 | Out-String
if ($dwQ -match 'Status:\s+(\S+)') {
  $dwStatus = $Matches[1]
  Say ("Docker API Watchdog Status={0}" -f $dwStatus)
  if ($dwStatus -ne 'Disabled') {
    $chg = & schtasks.exe /Change /TN '\Docker API Watchdog' /DISABLE 2>&1 | Out-String
    Say ("Docker API Watchdog disable: {0}" -f $chg.Trim())
  }
} else {
  Say 'Docker API Watchdog query: not found or inaccessible'
}

$zonePid = TaskPid 'ZoneEngine_New.exe'
$chatPid = TaskPid 'ChatEngine.exe'
$loginPid = TaskPid 'LoginEngine.exe'
$apiPid = NetListenPid 8098
Say ("pre-stop zone={0} chat={1} login={2} api8098={3}" -f $zonePid, $chatPid, $loginPid, $apiPid)

$bootLog = 'I:\AO\logs\boot-engines.log'
if (Test-Path $bootLog) {
  $sinceBoot = (Get-Date) - (Get-Item $bootLog).LastWriteTime
  Say ('last boot-engines.log write {0:N1}m ago' -f $sinceBoot.TotalMinutes)
  if ($sinceBoot.TotalMinutes -lt 5) { Say 'ABORT last boot under 5 min'; exit 2 }
}

function Stop-ViaShutdown([string]$name, [string]$shutdownFile) {
  $cur = [int](TaskPid "$name.exe")
  if ($cur -eq 0) { Say ("$name already down"); return }
  Say ("stop $name pid=$cur")
  "stop requested $(Get-Date -Format o)" | Set-Content -Path $shutdownFile -Encoding UTF8
  $dl = (Get-Date).AddSeconds(120)
  while ((Get-Date) -lt $dl) {
    Start-Sleep 3
    if ([int](TaskPid "$name.exe") -eq 0) { break }
  }
  Start-Sleep 2
  $left = [int](TaskPid "$name.exe")
  if ($left -ne 0) {
    Say ("force taskkill $name pid=$left")
    $tk = Start-Process -FilePath 'cmd.exe' -ArgumentList @('/d','/c',("taskkill /PID {0} /F" -f $left)) -NoNewWindow -PassThru
    $null = $tk.WaitForExit(30000)
    Start-Sleep 3
  }
  if ([int](TaskPid "$name.exe") -ne 0) { throw "$name still up" }
  Say ("$name down")
}

Stop-ViaShutdown 'ZoneEngine_New' $zShutdown
Stop-ViaShutdown 'ChatEngine' $cShutdown
Stop-ViaShutdown 'LoginEngine' $lShutdown
# API must stay up on 8098

Say 'copy staged GameData files + zbuild-patrol bins'
foreach ($f in $stageFiles) {
  $src = Join-Path $gdSrc $f
  $dst1 = Join-Path $zDestGd $f
  $dst1Dir = Split-Path $dst1 -Parent
  if (-not (Test-Path $dst1Dir)) { New-Item -ItemType Directory $dst1Dir | Out-Null }
  Copy-Item -LiteralPath $src -Destination $dst1 -Force
  if (Test-Path $builtGd) {
    $dst2 = Join-Path $builtGd $f
    $dst2Dir = Split-Path $dst2 -Parent
    if (-not (Test-Path $dst2Dir)) { New-Item -ItemType Directory $dst2Dir | Out-Null }
    Copy-Item -LiteralPath $src -Destination $dst2 -Force
  }
}
foreach ($n in @('ZoneEngine_New.dll', 'ZoneEngine_New.exe', 'ZoneEngine_New.pdb')) {
  $b = Join-Path $zSrc $n
  if (Test-Path $b) { Copy-Item -LiteralPath $b -Destination (Join-Path $zDest $n) -Force }
}

# Spot-check Newland Desert Spawns.json has patrolWaypoints after copy
$liveDesert = Join-Path $zDestGd 'Playfields\565\Spawns.json'
if (-not (Test-Path -LiteralPath $liveDesert)) { throw "live Desert Spawns missing after copy: $liveDesert" }
$desertRaw = Get-Content -LiteralPath $liveDesert -Raw
if ($desertRaw -notmatch 'patrolWaypoints') { throw 'live Desert Spawns missing patrolWaypoints after copy' }
Say 'live Desert Spawns.json has patrolWaypoints'

$apiBefore = NetListenPid 8098
Say ("api8098 before restart pid={0}" -f $apiBefore)
if ($apiBefore -eq 0) { Say 'WARN account API not listening on 8098 before restart' }

foreach ($sf in @($zShutdown, $cShutdown, $lShutdown)) {
  if (Test-Path -LiteralPath $sf) {
    Remove-Item -LiteralPath $sf -Force
    Say ("cleared shutdown marker {0}" -f $sf)
  }
}

$mysqlAo = NetListenPid 33070
Say ("mysql33070 pid={0}" -f $mysqlAo)
if ($mysqlAo -eq 0) { throw 'STOP: MySQL on 33070 not listening - refuse boot' }

$bootStart = Get-Date
Say 'start engines via DungeonDogs schtask'
$schOut = & schtasks.exe /Run /TN '\DungeonDogs\AO Dungeon Dogs Engines' 2>&1 | Out-String
Say ("schtasks: {0}" -f $schOut.Trim())
if ($LASTEXITCODE -ne 0 -and $schOut -notmatch 'SUCCESS') {
  throw "schtasks failed: $schOut"
}

# Timed listen checks (7501 zone, 7012 chat, 7500 login, 8098 api)
$listenOk = $false
$zone2 = 0; $chat2 = 0; $login2 = 0; $api2 = 0
$p7501 = 0; $p7012 = 0; $p7500 = 0; $p8098 = 0
$deadline = (Get-Date).AddSeconds(180)
while ((Get-Date) -lt $deadline) {
  Start-Sleep 10
  $zone2 = TaskPid 'ZoneEngine_New.exe'
  $chat2 = TaskPid 'ChatEngine.exe'
  $login2 = TaskPid 'LoginEngine.exe'
  $p7501 = NetListenPid 7501
  $p7012 = NetListenPid 7012
  $p7500 = NetListenPid 7500
  $p8098 = NetListenPid 8098
  $api2 = $p8098
  Say ("boot-poll zone={0} chat={1} login={2} listen 7501={3} 7012={4} 7500={5} 8098={6}" -f $zone2,$chat2,$login2,$p7501,$p7012,$p7500,$p8098)
  if ($zone2 -eq 0) {
    Say 'ZoneEngine exited during boot — rolling back bins+GameData from rb-patrol'
    foreach ($n in @('ZoneEngine_New.dll', 'ZoneEngine_New.exe', 'ZoneEngine_New.pdb')) {
      $rb = Join-Path $rollbackDir "_bins\$n"
      if (Test-Path $rb) { Copy-Item -LiteralPath $rb -Destination (Join-Path $zDest $n) -Force }
    }
    foreach ($f in $stageFiles) {
      $rb = Join-Path $rollbackDir $f
      if (Test-Path $rb) {
        $dst = Join-Path $zDestGd $f
        $dstDir = Split-Path $dst -Parent
        if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory $dstDir | Out-Null }
        Copy-Item -LiteralPath $rb -Destination $dst -Force
      }
    }
    throw 'ZoneEngine crash — rollback files restored; engines NOT auto-restarted'
  }
  if ($p7501 -ne 0 -and $p7012 -ne 0 -and $p7500 -ne 0 -and $p8098 -ne 0) {
    $listenOk = $true
    break
  }
}

if (-not $listenOk) { throw 'ports not all listening within 180s' }

# Confirm watchdog still disabled
$dwQ2 = & schtasks.exe /Query /TN '\Docker API Watchdog' /FO LIST 2>&1 | Out-String
if ($dwQ2 -match 'Status:\s+(\S+)') {
  Say ("Docker API Watchdog final Status={0}" -f $Matches[1])
  if ($Matches[1] -ne 'Disabled') {
    $null = & schtasks.exe /Change /TN '\Docker API Watchdog' /DISABLE 2>&1
    Say 're-disabled Docker API Watchdog'
  }
}

Say ("post-start zone={0} chat={1} login={2} api8098={3}" -f $zone2, $chat2, $login2, $api2)
Say ("boot_start={0:o}" -f $bootStart)

@(
  "tip=$hash"
  "db_backup=$backup"
  "rollback_gamedata=$rollbackDir"
  "stage_files=$($stageFiles.Count)"
  "matched=$($validation.matched)"
  "unmatched=$($validation.unmatched)"
  "boot_start=$($bootStart.ToString('o'))"
  "zone_pid=$zone2"
  "chat_pid=$chat2"
  "login_pid=$login2"
  "api_pid=$api2"
  "listen_7501=$p7501"
  "listen_7012=$p7012"
  "listen_7500=$p7500"
  "listen_8098=$p8098"
  "when=$(Get-Date -Format o)"
) | Set-Content -LiteralPath $metaPath -Encoding ascii
Say ("meta {0}" -f $metaPath)

if ($apiBefore -ne 0 -and $api2 -eq 0) { throw 'Account API died during deploy - STOP' }
Say 'DONE deploy patrol - authorized GO'
exit 0
