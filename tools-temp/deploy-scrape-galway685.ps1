# Deploy pf 685 Galway outdoor scrape pilot (SpawnsAdd.<tag> + NpcTemplates.Galway685). DO NOT RUN until Mike authorizes.
# House rules: online=0, DB backup to I:\AO\backups, boot age >=5m, shutdown-file stop, schtasks start.
# Stages GameData only (no schema migration). Uses timed tasklist/netstat only — no WMI/CIM.
$ErrorActionPreference = 'Stop'
$outLog = 'I:\AO\src\tools-temp\deploy-scrape-galway685.out.txt'
$metaPath = 'I:\AO\src\tools-temp\deploy-scrape-galway685-meta.txt'
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
$tag = 'scrape-galway685-20261106'
$gdSrc = Join-Path $root 'AORebirth\GameData'
$zSrc = Join-Path $root 'tools-temp\zbuild-galway685'
$zDest = Join-Path $root 'AORebirth\Built\Debug\ZoneEngine_New'
$zDestGd = Join-Path $zDest 'GameData'
$builtGd = Join-Path $root 'AORebirth\Built\Debug\GameData'
$zShutdown = Join-Path $root 'logs\engines\ZoneEngine_New.shutdown'
$cShutdown = Join-Path $root 'logs\engines\ChatEngine.shutdown'
$zExe = Join-Path $zDest 'ZoneEngine_New.exe'
$hash = (git -C $root rev-parse --short HEAD).Trim()
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$backup = "I:\AO\backups\$db-$stamp-scrape-galway685.sql"
$saSrc = Join-Path $gdSrc 'PlayfieldContent\685\SpawnsAdd.galway685.json'
$ntSrc = Join-Path $gdSrc 'NpcTemplates.Galway685.json'
$stageFiles = @(
  'NpcTemplates.Galway685.json',
  'PlayfieldContent\685\SpawnsAdd.galway685.json'
)

if (-not (Test-Path $saSrc)) { throw "missing $saSrc - run Build-ScrapeImportGalway685.ps1 first" }
if (-not (Test-Path $ntSrc)) { throw "missing $ntSrc" }
if (-not (Test-Path $zExe)) { throw "missing $zExe - build first" }
foreach ($n in @('ZoneEngine_New.dll', 'ZoneEngine_New.exe')) {
  if (-not (Test-Path (Join-Path $zSrc $n))) { throw "missing $n in $zSrc - rebuild to tools-temp\zbuild-galway685" }
}

Say ("deploy tip={0} tag={1}" -f $hash, $tag)

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

# Process identity via tasklist only (no WMI/CIM)
function Get-PidByImage([string]$image) {
  $lines = @(cmd /d /c "tasklist /FI `"IMAGENAME eq $image`" /FO CSV /NH" 2>$null)
  foreach ($line in $lines) {
    if ($line -match '^"([^"]+)","(\d+)"') { return [int]$Matches[2] }
  }
  return 0
}
$zonePid = Get-PidByImage 'ZoneEngine_New.exe'
$chatPid = Get-PidByImage 'ChatEngine.exe'
$loginPid = Get-PidByImage 'LoginEngine.exe'
if ($loginPid -eq 0) { throw 'LoginEngine must stay up' }
Say ("pre-stop zone={0} chat={1} login={2}" -f $zonePid, $chatPid, $loginPid)

$bootLog = 'I:\AO\logs\boot-engines.log'
if (Test-Path $bootLog) {
  $sinceBoot = (Get-Date) - (Get-Item $bootLog).LastWriteTime
  Say ('last boot-engines.log write {0:N1}m ago' -f $sinceBoot.TotalMinutes)
  if ($sinceBoot.TotalMinutes -lt 5) { Say 'ABORT last boot under 5 min'; exit 2 }
}

function Stop-ViaShutdown([string]$name, [string]$shutdownFile) {
  if ((Get-PidByImage "$name.exe") -eq 0) { Say ("$name already down"); return }
  Say ("stop $name")
  "stop requested $(Get-Date -Format o)" | Set-Content -Path $shutdownFile -Encoding UTF8
  $dl = (Get-Date).AddSeconds(90)
  while ((Get-Date) -lt $dl -and (Get-PidByImage "$name.exe") -ne 0) { Start-Sleep 2 }
  Start-Sleep 2
  $left = Get-PidByImage "$name.exe"
  if ($left -ne 0) {
    cmd /d /c "taskkill /PID $left /F" | Out-Null
    Start-Sleep 2
  }
  if ((Get-PidByImage "$name.exe") -ne 0) { throw "$name still up" }
}
Stop-ViaShutdown 'ZoneEngine_New' $zShutdown
Stop-ViaShutdown 'ChatEngine' $cShutdown
if ((Get-PidByImage 'LoginEngine.exe') -ne $loginPid -and (Get-PidByImage 'LoginEngine.exe') -eq 0) { throw 'Login died' }

Say 'copy staged GameData files + zbuild bins'
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

$liveSa = Join-Path $zDestGd 'PlayfieldContent\685\SpawnsAdd.galway685.json'
if (-not (Test-Path $liveSa)) { throw "live SpawnsAdd missing after copy: $liveSa" }
$sa = Get-Content -LiteralPath $liveSa -Raw | ConvertFrom-Json
if ($sa.source -ne $tag) { throw "bad SpawnsAdd source $($sa.source)" }
Say ("live SpawnsAdd count={0}" -f @($sa.spawns).Count)

Say 'start ZoneEngine_New + ChatEngine via schtasks'
schtasks /Run /TN 'AORebirth\ZoneEngine_New' | Out-Null
schtasks /Run /TN 'AORebirth\ChatEngine' | Out-Null
Start-Sleep 15
$zone2 = Get-PidByImage 'ZoneEngine_New.exe'
$chat2 = Get-PidByImage 'ChatEngine.exe'
if ($zone2 -eq 0) { throw 'ZoneEngine_New failed to start' }
if ($chat2 -eq 0) { throw 'ChatEngine failed to start' }
if ((Get-PidByImage 'LoginEngine.exe') -eq 0) { throw 'Login died after restart' }
Say ("post-start zone={0} chat={1} login={2}" -f $zone2, $chat2, (Get-PidByImage 'LoginEngine.exe'))

@(
  "tip=$hash"
  "db_backup=$backup"
  "spawnsAdd=$(@($sa.spawns).Count)"
  "tag=$tag"
  "when=$(Get-Date -Format o)"
) | Set-Content -LiteralPath $metaPath -Encoding ascii
Say ("meta {0}" -f $metaPath)
Say 'DONE deploy scrape-galway685 (Mike: live-verify pf 685) — script was authorized to run'
