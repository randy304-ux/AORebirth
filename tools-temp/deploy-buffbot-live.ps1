# STAGE deploy: live-style Buffbot blob + Motd. Restarts ZoneEngine_New AND ChatEngine.
# House rules: online=0, DB backup I:\AO\backups, boot age >=5m, shutdown-file stop,
# schtasks start with direct Start-Process fallback.

$ErrorActionPreference = "Stop"
$outLog = "I:\AO\src\tools-temp\deploy-buffbot-live.out.txt"
Set-Content -LiteralPath $outLog -Value "" -Encoding ascii
function Say([string]$m) {
  $line = "{0} {1}" -f (Get-Date -Format "HH:mm:ss"), $m
  Write-Output $line
  Add-Content -LiteralPath $outLog -Value $line
}

$mysql = "I:\AO\mysql\server\bin\mysql.exe"
$mysqldump = "I:\AO\mysql\server\bin\mysqldump.exe"
$cnf = "I:\AO\secrets\aorebirth-client.cnf"
$db = "cellao_codex_clean"
function Q([string]$sql) {
  (& $mysql --defaults-extra-file=$cnf -P 33070 -h 127.0.0.1 -N -e $sql $db | Out-String).Trim()
}

$zSrc = "I:\AO\src\tools-temp\zbuild-buffbot-live"
$zDest = "I:\AO\src\AORebirth\Built\Debug\ZoneEngine_New"
$chatDest = "I:\AO\src\AORebirth\Built\Debug"
$cfgSrc = "I:\AO\src\AORebirth\Config\Config.xml"
$cfgDest = "I:\AO\src\AORebirth\Built\Debug\Config.xml"
$zlog = "I:\AO\src\logs\engines\ZoneEngine_New.out.log"
$clog = "I:\AO\src\logs\engines\ChatEngine.out.log"
$zShutdown = "I:\AO\src\logs\engines\ZoneEngine_New.shutdown"
$cShutdown = "I:\AO\src\logs\engines\ChatEngine.shutdown"
$zStderr = "I:\AO\src\logs\engines\ZoneEngine_New.err.log"
$cStderr = "I:\AO\src\logs\engines\ChatEngine.err.log"
$zExe = Join-Path $zDest "ZoneEngine_New.exe"
$cExe = Join-Path $chatDest "ChatEngine.exe"
$hash = (git -C I:\AO\src rev-parse --short HEAD).Trim()
$stamp = Get-Date -Format "yyyyMMdd-HHmm"
$bakDir = "I:\AO\backups\zone-bins-$stamp-$hash"
$backup = "I:\AO\backups\$db-$stamp-buffbot-live.sql"
New-Item -ItemType Directory -Force -Path $bakDir | Out-Null

foreach ($n in @("ZoneEngine_New.dll","ZoneEngine_New.exe","ZoneEngine_New.pdb")) {
  if (-not (Test-Path (Join-Path $zSrc $n))) { throw "missing $n in $zSrc" }
}

$online = Q "SELECT COUNT(*) FROM characters WHERE Online=1"
Say ("pre-stop online={0}" -f $online)
if ($online -ne "0") { Say "ABORT online"; exit 2 }

Say ("backup DB -> {0}" -f $backup)
& $mysqldump --defaults-extra-file=$cnf -P 33070 -h 127.0.0.1 --single-transaction --no-tablespaces --result-file=$backup $db
if ($LASTEXITCODE -ne 0) { throw "mysqldump $LASTEXITCODE" }
$blen = (Get-Item -LiteralPath $backup).Length
Say ("backup bytes={0}" -f $blen)
if ($blen -lt 1000000) { throw "backup too small" }

$zone = Get-Process ZoneEngine_New -ErrorAction SilentlyContinue | Select-Object -First 1
$chat = Get-Process ChatEngine -ErrorAction SilentlyContinue | Select-Object -First 1
$login = Get-Process LoginEngine -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $login) { throw "LoginEngine must stay up" }
Say ("pre-stop zone={0} chat={1} login={2}" -f $(if($zone){$zone.Id}else{0}), $(if($chat){$chat.Id}else{0}), $login.Id)
foreach ($p in @($zone,$chat)) {
  if ($p -and ((Get-Date) - $p.StartTime).TotalMinutes -lt 5) { Say "ABORT uptime<5m"; exit 2 }
}
$bootLog = "I:\AO\logs\boot-engines.log"
if (Test-Path $bootLog) {
  $sinceBoot = (Get-Date) - (Get-Item $bootLog).LastWriteTime
  Say ("last boot-engines.log write {0:N1}m ago" -f $sinceBoot.TotalMinutes)
  if ($sinceBoot.TotalMinutes -lt 5) { Say "ABORT last boot under 5 min"; exit 2 }
}

Copy-Item (Join-Path $zDest "ZoneEngine_New.dll") $bakDir -Force
Copy-Item (Join-Path $zDest "ZoneEngine_New.pdb") $bakDir -Force
Copy-Item (Join-Path $zDest "ZoneEngine_New.exe") $bakDir -Force
if (Test-Path $cfgDest) { Copy-Item $cfgDest (Join-Path $bakDir "Config.xml") -Force }

function Stop-ViaShutdown([string]$name,[string]$shutdownFile) {
  Say ("stop $name")
  "stop requested $(Get-Date -Format o)" | Set-Content -Path $shutdownFile -Encoding UTF8
  $dl=(Get-Date).AddSeconds(90)
  while((Get-Date) -lt $dl -and (Get-Process $name -ErrorAction SilentlyContinue)){ Start-Sleep 2 }
  Start-Sleep 2
  if (Get-Process $name -ErrorAction SilentlyContinue) {
    Get-Process $name | ForEach-Object { Stop-Process -Id $_.Id -Force }
    Start-Sleep 2
  }
  if (Get-Process $name -ErrorAction SilentlyContinue) { throw "$name still up" }
}
$loginId=$login.Id
Stop-ViaShutdown "ZoneEngine_New" $zShutdown
Stop-ViaShutdown "ChatEngine" $cShutdown
$login=Get-Process -Id $loginId -ErrorAction SilentlyContinue
if (-not $login) { throw "Login died" }

Copy-Item (Join-Path $zSrc "ZoneEngine_New.dll") $zDest -Force
Copy-Item (Join-Path $zSrc "ZoneEngine_New.pdb") $zDest -Force
Copy-Item (Join-Path $zSrc "ZoneEngine_New.exe") $zDest -Force
Copy-Item $cfgSrc $cfgDest -Force
$h=(Get-FileHash (Join-Path $zDest "ZoneEngine_New.dll") -Algorithm SHA256).Hash
Say ("installed dll sha={0}" -f $h)

$online=Q "SELECT COUNT(*) FROM characters WHERE Online=1"
Say ("pre-start online={0}" -f $online)
if ($online -ne "0") { Say "ABORT online"; exit 2 }

if (Test-Path $zShutdown) { Remove-Item $zShutdown -Force }
if (Test-Path $cShutdown) { Remove-Item $cShutdown -Force }
$zLogLen=0; if(Test-Path $zlog){$zLogLen=(Get-Item $zlog).Length}
$cLogLen=0; if(Test-Path $clog){$cLogLen=(Get-Item $clog).Length}

Say "schtasks start"
$job=Start-Job { cmd /c "schtasks /run /tn `"\DungeonDogs\AO Dungeon Dogs Engines`""; $LASTEXITCODE }
$waited=Wait-Job $job -Timeout 25
if (-not $waited) {
  Say "schtasks hung - direct start"
  Stop-Job $job -Force -ErrorAction SilentlyContinue
  Remove-Job $job -Force -ErrorAction SilentlyContinue
} else {
  Receive-Job $job | Out-Null
  Remove-Job $job -Force -ErrorAction SilentlyContinue
  Say "schtasks submitted"
}

Start-Sleep 5
cmd /c "call I:\AO\tools\ao-build-env.cmd & call I:\AO\secrets\ao-env.cmd & set" | ForEach-Object {
  if ($_ -match "^([^=]+)=(.*)$") { [Environment]::SetEnvironmentVariable($Matches[1],$Matches[2],"Process") }
}
if (-not (Get-Process ChatEngine -ErrorAction SilentlyContinue)) {
  Say "direct ChatEngine"
  Start-Process -FilePath $cExe -ArgumentList @("/headless","/shutdown-file",$cShutdown,"/stdout-log",$clog,"/stderr-log",$cStderr) -WorkingDirectory $chatDest -WindowStyle Hidden | Out-Null
}
if (-not (Get-Process ZoneEngine_New -ErrorAction SilentlyContinue)) {
  Say "direct ZoneEngine_New"
  Start-Process -FilePath $zExe -ArgumentList @("/headless","/shutdown-file",$zShutdown,"/stdout-log",$zlog,"/stderr-log",$zStderr) -WorkingDirectory $zDest -WindowStyle Hidden | Out-Null
}

$zoneUp=$false; $chatUp=$false; $zBoot=""
$deadline=(Get-Date).AddSeconds(180)
while((Get-Date) -lt $deadline){
  $zp=Get-Process ZoneEngine_New -ErrorAction SilentlyContinue | Select-Object -First 1
  $cp=Get-Process ChatEngine -ErrorAction SilentlyContinue | Select-Object -First 1
  if($cp){$chatUp=$true}
  if($zp -and (Test-Path $zlog)){
    $fs=[IO.File]::Open($zlog,"Open","Read","ReadWrite")
    if($fs.Length -gt $zLogLen){
      $fs.Seek($zLogLen,"Begin")|Out-Null
      $sr=New-Object IO.StreamReader($fs)
      $zBoot=$sr.ReadToEnd(); $sr.Close(); $fs.Close()
      if($zBoot -match "listening on 0\.0\.0\.0:7501" -and ($zBoot -match "root container started" -or $zBoot -match "ZONEENGINE_NEW_READY")){ $zoneUp=$true }
    } else { $fs.Close() }
  }
  if($zoneUp -and $chatUp){break}
  Start-Sleep 2
}
if(-not $zoneUp){ Say "ZONE BOOT FAIL"; exit 4 }
if(-not $chatUp){ Say "CHAT BOOT FAIL"; exit 4 }

$bb=((($zBoot -split "`n")|Where-Object{$_ -match "Buffbot catalog"}) -join " | ")
$zone=Get-Process ZoneEngine_New -ErrorAction SilentlyContinue|Select-Object -First 1
$chat=Get-Process ChatEngine -ErrorAction SilentlyContinue|Select-Object -First 1
$login=Get-Process LoginEngine -ErrorAction SilentlyContinue|Select-Object -First 1
$zPort=(Get-NetTCPConnection -LocalPort 7501 -State Listen -ErrorAction SilentlyContinue|Select-Object -First 1).OwningProcess
$cPort=(Get-NetTCPConnection -LocalPort 7500 -State Listen -ErrorAction SilentlyContinue|Select-Object -First 1).OwningProcess
Say ("BUFFBOT {0}" -f $bb)
Say ("zone pid={0} port7501={1}" -f $zone.Id,$zPort)
Say ("chat pid={0} port7500={1}" -f $chat.Id,$cPort)
Say ("login pid={0}" -f $login.Id)
$blob=""
if(Test-Path "I:\AO\src\tools-temp\buffbot-blob-size.txt"){ $blob=Get-Content "I:\AO\src\tools-temp\buffbot-blob-size.txt" -Raw }
@(
  "commit=$hash"
  "db_backup=$backup"
  "pid_zone=$($zone.Id)"
  "pid_chat=$($chat.Id)"
  "pid_login=$($login.Id)"
  "port7501=$zPort"
  "port7500=$cPort"
  "buffbot=$bb"
  "blob=$blob"
) -join "`r`n" | Set-Content "I:\AO\src\tools-temp\deploy-buffbot-live-meta.txt" -Encoding ascii
Say "DEPLOY OK"
Get-Content "I:\AO\src\tools-temp\deploy-buffbot-live-meta.txt"
exit 0
