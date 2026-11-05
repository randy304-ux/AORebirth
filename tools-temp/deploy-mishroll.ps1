# deploy-mishroll.ps1 - ZoneEngine bins + Gameplay.json mishroll.enabled + character_mishroll SQL.
# DO NOT RUN until Mike/Randy authorize. Modeled on deploy-store-icc655.ps1.
# Rules: online=0, engines up >=5 min, DB backup first, shutdown-file stop Zone+Chat,
# schtasks start, verify Zone 7501 / Chat 7012 / Login 7500. First boot timeout -> 5-min gate + one retry -> else full rollback.
$ErrorActionPreference = 'Stop'
$root = 'I:\AO\src'
# Minimum mishroll feature commit (accept fee + default ON). Tip may be this or a descendant.
$want = 'fd01ad23'
$outLog = Join-Path $root 'tools-temp\deploy-mishroll.out.txt'
$metaPath = Join-Path $root 'tools-temp\deploy-mishroll-meta.txt'
Set-Content -LiteralPath $outLog -Value '' -Encoding ascii
function Say([string]$m) { $l = '{0} {1}' -f (Get-Date -Format 'HH:mm:ss'), $m; Write-Host $l; Add-Content -LiteralPath $outLog -Value $l }
$mysql = 'I:\AO\mysql\server\bin\mysql.exe'; $mysqldump = 'I:\AO\mysql\server\bin\mysqldump.exe'
$cnf = 'I:\AO\secrets\aorebirth-client.cnf'; $db = 'cellao_codex_clean'
function Q([string]$sql) { (& $mysql --defaults-extra-file=$cnf -P 33070 -h 127.0.0.1 -N -e $sql $db | Out-String).Trim() }
$zSrc = Join-Path $root 'tools-temp\zbuild-mishroll'
$rb = Join-Path $root 'tools-temp\rb-mishroll'
$zDest = Join-Path $root 'AORebirth\Built\Debug\ZoneEngine_New'
$cfgSrc = Join-Path $zSrc 'Config\Gameplay.json'
$cfgDest = Join-Path $zDest 'Config\Gameplay.json'
$migSrc = Join-Path $zSrc 'Migrations\20261105_character_mishroll.sql'
$migDestDir = Join-Path $zDest 'Migrations'
$sqlFile = Join-Path $root 'AORebirth\Libraries\Source\AORebirth.Database\Migrations\20261105_character_mishroll.sql'
$zShutdown = Join-Path $root 'logs\engines\ZoneEngine_New.shutdown'
$cShutdown = Join-Path $root 'logs\engines\ChatEngine.shutdown'
$zlog = Join-Path $root 'logs\engines\ZoneEngine_New.out.log'
$bins = @('ZoneEngine_New.dll', 'ZoneEngine_New.exe', 'ZoneEngine_New.pdb')

$hash = (git -C $root rev-parse --short=8 HEAD).Trim()
Say "deploy tip=$hash want=$want"
git -C $root merge-base --is-ancestor $want HEAD
if ($LASTEXITCODE -ne 0) { Say "STOP: I:\AO\src HEAD $hash does not contain mishroll commit $want"; exit 3 }
foreach ($n in $bins) { if (-not (Test-Path (Join-Path $zSrc $n))) { throw "missing $n in $zSrc" } }
if (-not (Test-Path $cfgSrc)) { throw "missing $cfgSrc" }
if (-not (Test-Path $sqlFile)) { throw "missing $sqlFile" }
if (-not (Test-Path $migSrc)) { throw "missing staged migration $migSrc" }
if (Test-Path $rb) { throw "$rb exists; move it aside first" }

$online = Q 'SELECT COUNT(*) FROM characters WHERE Online=1'
Say "online=$online"; if ($online -ne '0') { Say 'ABORT online!=0'; exit 2 }
$zone = Get-Process ZoneEngine_New -ErrorAction SilentlyContinue | Select-Object -First 1
$chat = Get-Process ChatEngine -ErrorAction SilentlyContinue | Select-Object -First 1
$login = Get-Process LoginEngine -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $login) { throw 'LoginEngine must stay up' }
Say ("pre-stop zone={0} chat={1} login={2}" -f $(if ($zone) { $zone.Id } else { 0 }), $(if ($chat) { $chat.Id } else { 0 }), $login.Id)
foreach ($p in @($zone, $chat)) { if ($p -and $p.StartTime -and ((Get-Date) - $p.StartTime).TotalMinutes -lt 5) { Say 'ABORT engine uptime <5 min'; exit 2 } }
$bootLog = 'I:\AO\logs\boot-engines.log'
if ((Test-Path $bootLog) -and ((Get-Date) - (Get-Item $bootLog).LastWriteTime).TotalMinutes -lt 5) { Say 'ABORT last boot under 5 min'; exit 2 }

$stamp = Get-Date -Format 'yyyyMMdd-HHmm'; $backup = "I:\AO\backups\$db-$stamp-mishroll-$hash.sql"
Say "backup DB -> $backup"
& $mysqldump --defaults-extra-file=$cnf -P 33070 -h 127.0.0.1 --single-transaction --no-tablespaces --result-file=$backup $db
if ($LASTEXITCODE -ne 0) { throw "mysqldump $LASTEXITCODE" }
$blen = (Get-Item -LiteralPath $backup).Length; Say "backup bytes=$blen"; if ($blen -lt 1000000) { throw 'backup too small' }

New-Item -ItemType Directory -Path $rb, "$rb\Config", "$rb\Migrations" | Out-Null
foreach ($n in $bins) { Copy-Item (Join-Path $zDest $n) $rb -Force }
if (Test-Path $cfgDest) { Copy-Item $cfgDest "$rb\Config\Gameplay.json" -Force }
if (Test-Path (Join-Path $migDestDir '20261105_character_mishroll.sql')) {
  Copy-Item (Join-Path $migDestDir '20261105_character_mishroll.sql') "$rb\Migrations\20261105_character_mishroll.sql" -Force
}
Say "rollback snapshot -> $rb"

function Stop-ViaShutdown([string]$name, [string]$shutdownFile) {
  if (-not (Get-Process $name -ErrorAction SilentlyContinue)) { Say "$name already down"; return }
  Say "stop $name (shutdown file)"; "stop requested $(Get-Date -Format o)" | Set-Content -Path $shutdownFile -Encoding UTF8
  $dl = (Get-Date).AddSeconds(90); while ((Get-Date) -lt $dl -and (Get-Process $name -ErrorAction SilentlyContinue)) { Start-Sleep 2 }
  Start-Sleep 2
  if (Get-Process $name -ErrorAction SilentlyContinue) { Get-Process $name | ForEach-Object { Stop-Process -Id $_.Id -Force }; Start-Sleep 2 }
  if (Get-Process $name -ErrorAction SilentlyContinue) { throw "$name still up" }
}
function Listen([int]$port) { $l = netstat -ano | Select-String (":$port\s+.*LISTENING") | Select-Object -First 1; if ($l) { $l.Line.Trim() } else { $null } }
function Boot-Once {
  if (Test-Path $zShutdown) { Remove-Item $zShutdown -Force }; if (Test-Path $cShutdown) { Remove-Item $cShutdown -Force }
  $script:zLogLen = if (Test-Path $zlog) { (Get-Item $zlog).Length } else { 0 }
  Say 'start engines: schtasks /run /tn "\DungeonDogs\AO Dungeon Dogs Engines"'
  $st = schtasks /run /tn '\DungeonDogs\AO Dungeon Dogs Engines' 2>&1 | Out-String; Say ("schtasks: {0}" -f $st.Trim())
  $deadline = (Get-Date).AddSeconds(600); $up = $false; $script:fresh = ''
  while ((Get-Date) -lt $deadline) {
    if (Test-Path $zlog) {
      $fs = [IO.File]::Open($zlog, 'Open', 'Read', 'ReadWrite')
      try { if ($fs.Length -gt $script:zLogLen) { $fs.Seek($script:zLogLen, 'Begin') | Out-Null; $sr = New-Object IO.StreamReader($fs); $script:fresh = $sr.ReadToEnd(); $sr.Close() } } finally { $fs.Close() }
      if ($script:fresh -match 'Failed to load|catalog empty') { Say ('FAIL boot log: ' + ((($script:fresh -split "`n") | Where-Object { $_ -match 'Failed to load|catalog empty' } | Select-Object -First 2) -join ' || ')); return $false }
    }
    if ((Listen 7501) -and (Listen 7012) -and (Listen 7500) -and (Get-Process ZoneEngine_New -ErrorAction SilentlyContinue) -and (Get-Process ChatEngine -ErrorAction SilentlyContinue)) { $up = $true; break }
    Start-Sleep 5
  }
  return $up
}
function Stop-Engines { Stop-ViaShutdown 'ZoneEngine_New' $zShutdown; Stop-ViaShutdown 'ChatEngine' $cShutdown }
function Rollback([string]$why) {
  Say "ROLLBACK: $why"
  try { Stop-Engines } catch { Say "stop during rollback: $_" }
  foreach ($n in $bins) { Copy-Item (Join-Path $rb $n) $zDest -Force }
  if (Test-Path "$rb\Config\Gameplay.json") { New-Item -ItemType Directory -Force (Split-Path $cfgDest) | Out-Null; Copy-Item "$rb\Config\Gameplay.json" $cfgDest -Force }
  Say "restore DB from $backup"
  cmd /c ("`"{0}`" --defaults-extra-file={1} -P 33070 -h 127.0.0.1 {2} < `"{3}`"" -f $mysql, $cnf, $db, $backup)
  Say "db restore exit=$LASTEXITCODE"
  Say 'rollback: waiting 5-min boot gate'; Start-Sleep 300
  $ok = [bool]((Boot-Once) | Select-Object -Last 1); Say "rollback boot ok=$ok"
  "rollback=$why`nrollback_boot_ok=$ok`ndb_backup=$backup`nwhen=$(Get-Date -Format o)" | Set-Content -LiteralPath $metaPath -Encoding ascii
  exit 5
}

$loginId = $login.Id
Stop-Engines
if (-not (Get-Process -Id $loginId -ErrorAction SilentlyContinue)) { throw 'Login died during stop' }

try {
  $before = Q "SHOW TABLES LIKE 'character_mishroll'"
  Say "character_mishroll before='$before'"
  cmd /c ("`"{0}`" --defaults-extra-file={1} -P 33070 -h 127.0.0.1 {2} < `"{3}`"" -f $mysql, $cnf, $db, $sqlFile)
  if ($LASTEXITCODE -ne 0) { throw "mishroll sql exit $LASTEXITCODE" }
  $after = Q "SHOW TABLES LIKE 'character_mishroll'"
  Say "character_mishroll after='$after'"
  if ($after -ne 'character_mishroll') { throw 'character_mishroll table missing after migration' }

  foreach ($n in $bins) { Copy-Item (Join-Path $zSrc $n) $zDest -Force }
  $h = (Get-FileHash (Join-Path $zDest 'ZoneEngine_New.dll') -Algorithm SHA256).Hash
  if ($h -ne (Get-FileHash (Join-Path $zSrc 'ZoneEngine_New.dll') -Algorithm SHA256).Hash) { throw 'dll hash mismatch' }
  Say "installed dll sha=$h"
  New-Item -ItemType Directory -Force (Split-Path $cfgDest) | Out-Null
  Copy-Item $cfgSrc $cfgDest -Force
  New-Item -ItemType Directory -Force $migDestDir | Out-Null
  Copy-Item $migSrc (Join-Path $migDestDir '20261105_character_mishroll.sql') -Force
  Say 'Gameplay.json + migration staged into ZoneEngine_New'
} catch { Rollback "apply failed: $_" }

$online = Q 'SELECT COUNT(*) FROM characters WHERE Online=1'
if ($online -ne '0') { Rollback "online=$online before start" }
$ok = [bool]((Boot-Once) | Select-Object -Last 1)
if (-not $ok) {
  Say 'first boot did not come up; stopping, 5-min gate, retry once'
  try { Stop-Engines } catch { Say "stop: $_" }
  Start-Sleep 300
  $ok = [bool]((Boot-Once) | Select-Object -Last 1)
}
if (-not $ok) { Rollback 'engines did not come up after retry' }
$p7501 = Listen 7501; $p7012 = Listen 7012; $p7500 = Listen 7500
Say "listen 7501=$p7501"; Say "listen 7012=$p7012"; Say "listen 7500=$p7500"
$zp = Get-Process ZoneEngine_New | Select-Object -First 1; $cp = Get-Process ChatEngine | Select-Object -First 1; $lp = Get-Process LoginEngine | Select-Object -First 1
Say ("pids zone={0} chat={1} login={2}" -f $zp.Id, $cp.Id, $lp.Id)
$table = Q 'SELECT COUNT(*) FROM character_mishroll'
Say "character_mishroll rows=$table"
@("tip=$hash", "db_backup=$backup", "dll_sha256=$h", "character_mishroll_rows=$table",
  "pid_zone=$($zp.Id)", "pid_chat=$($cp.Id)", "pid_login=$($lp.Id)", "listen7501=$p7501", "listen7012=$p7012", "listen7500=$p7500",
  "when=$(Get-Date -Format o)") | Set-Content -LiteralPath $metaPath -Encoding ascii
Say 'DEPLOY OK (mishroll)'
exit 0
