# Deploy pf 585 scrape pilot (SpawnsAdd + NpcTemplates). DO NOT RUN until Mike authorizes.
# House rules: online=0, DB backup to I:\AO\backups, boot age >=5m, shutdown-file stop, schtasks start.
# Stages GameData only (no schema migration for this pilot).
$ErrorActionPreference = 'Stop'
$outLog = 'I:\AO\src\tools-temp\deploy-scrape-585.out.txt'
$metaPath = 'I:\AO\src\tools-temp\deploy-scrape-585-meta.txt'
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
$gdSrc = Join-Path $root 'AORebirth\GameData'
$zDest = Join-Path $root 'AORebirth\Built\Debug\ZoneEngine_New'
$zDestGd = Join-Path $zDest 'GameData'
$builtGd = Join-Path $root 'AORebirth\Built\Debug\GameData'
$zShutdown = Join-Path $root 'logs\engines\ZoneEngine_New.shutdown'
$cShutdown = Join-Path $root 'logs\engines\ChatEngine.shutdown'
$zExe = Join-Path $zDest 'ZoneEngine_New.exe'
$cExe = Join-Path $root 'AORebirth\Built\Debug\ChatEngine.exe'
$hash = (git -C $root rev-parse --short HEAD).Trim()
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$backup = "I:\AO\backups\$db-$stamp-scrape-585.sql"
$saSrc = Join-Path $gdSrc 'PlayfieldContent\585\SpawnsAdd.json'
$ntSrc = Join-Path $gdSrc 'NpcTemplates.json'

if (-not (Test-Path $saSrc)) { throw "missing $saSrc - run Build-ScrapeImport585.ps1 first" }
if (-not (Test-Path $ntSrc)) { throw "missing $ntSrc" }
if (-not (Test-Path $zExe)) { throw "missing $zExe - build first" }

Say ("deploy tip={0}" -f $hash)

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
if (-not (Get-Process -Id $loginId -ErrorAction SilentlyContinue)) { throw 'Login died' }

Say 'robocopy GameData -> Built ZoneEngine_New\GameData'
cmd /c ("robocopy `"{0}`" `"{1}`" /E /NJH /NJS /NDL /NP /R:1 /W:1" -f $gdSrc, $zDestGd) | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy zone gd $LASTEXITCODE" }
if (Test-Path $builtGd) {
  Say 'robocopy GameData -> Built\Debug\GameData'
  cmd /c ("robocopy `"{0}`" `"{1}`" /E /NJH /NJS /NDL /NP /R:1 /W:1" -f $gdSrc, $builtGd) | Out-Null
  if ($LASTEXITCODE -ge 8) { throw "robocopy built gd $LASTEXITCODE" }
}

# Verify SpawnsAdd landed
$liveSa = Join-Path $zDestGd 'PlayfieldContent\585\SpawnsAdd.json'
if (-not (Test-Path $liveSa)) { throw "live SpawnsAdd missing after copy: $liveSa" }
$sa = Get-Content -LiteralPath $liveSa -Raw | ConvertFrom-Json
if ($sa.source -ne 'scrape-585-20261105') { throw "bad SpawnsAdd source $($sa.source)" }
Say ("live SpawnsAdd count={0}" -f @($sa.spawns).Count)

Say 'start ZoneEngine_New + ChatEngine via schtasks'
schtasks /Run /TN 'AORebirth\ZoneEngine_New' | Out-Null
schtasks /Run /TN 'AORebirth\ChatEngine' | Out-Null
Start-Sleep 15
$zone2 = Get-Process ZoneEngine_New -ErrorAction SilentlyContinue | Select-Object -First 1
$chat2 = Get-Process ChatEngine -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $zone2) { throw 'ZoneEngine_New failed to start' }
if (-not $chat2) { throw 'ChatEngine failed to start' }
if (-not (Get-Process -Id $loginId -ErrorAction SilentlyContinue)) { throw 'Login died after restart' }
Say ("post-start zone={0} chat={1} login={2}" -f $zone2.Id, $chat2.Id, $loginId)

@(
  "tip=$hash"
  "db_backup=$backup"
  "spawnsAdd=$(@($sa.spawns).Count)"
  "tag=scrape-585-20261105"
  "when=$(Get-Date -Format o)"
) | Set-Content -LiteralPath $metaPath -Encoding ascii
Say ("meta {0}" -f $metaPath)
Say 'DONE deploy scrape-585 (Mike: live-verify pf 585)'
