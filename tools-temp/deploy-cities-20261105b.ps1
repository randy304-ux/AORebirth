# deploy-cities-20261105b.ps1 - stage/deploy scrape-cities-20261105b named city NPC fills (worktree I:\AO\worktrees\city-batch).
# DO NOT RUN without Randy's explicit go. Rules: online=0, engines up >=5 min, DB backup first, shutdown-file stop, schtasks start,
# verify Zone 7501 / Chat 7012 / Login 7500. No DB writes (GameData only). Modeled on deploy-store-icc655.ps1.
$ErrorActionPreference = 'Stop'
$wt = 'I:\AO\worktrees\city-batch'; $root = 'I:\AO\src'; $tag = 'scrape-cities-20261105b'
$outLog = Join-Path $root 'tools-temp\deploy-cities-20261105b.out.txt'; $metaPath = Join-Path $root 'tools-temp\deploy-cities-20261105b-meta.txt'
Set-Content -LiteralPath $outLog -Value '' -Encoding ascii
function Say([string]$m) { $l = '{0} {1}' -f (Get-Date -Format 'HH:mm:ss'), $m; Write-Host $l; Add-Content -LiteralPath $outLog -Value $l }
$mysql = 'I:\AO\mysql\server\bin\mysql.exe'; $mysqldump = 'I:\AO\mysql\server\bin\mysqldump.exe'
$cnf = 'I:\AO\secrets\aorebirth-client.cnf'; $db = 'cellao_codex_clean'
function Q([string]$sql) { (& $mysql --defaults-extra-file=$cnf -P 33070 -h 127.0.0.1 -N -e $sql $db | Out-String).Trim() }
$zSrc = Join-Path $wt 'tools-temp\zbuild-cities2'
$rb = Join-Path $root 'tools-temp\rb-cities-20261105b'
$zDest = Join-Path $root 'AORebirth\Built\Debug\ZoneEngine_New'; $zDestGd = Join-Path $zDest 'GameData'
$builtGd = Join-Path $root 'AORebirth\Built\Debug\GameData'; $gdSrc = Join-Path $wt 'AORebirth\GameData'
$zShutdown = Join-Path $root 'logs\engines\ZoneEngine_New.shutdown'; $cShutdown = Join-Path $root 'logs\engines\ChatEngine.shutdown'
$zlog = Join-Path $root 'logs\engines\ZoneEngine_New.out.log'
$cityFiles = @(
  'SpawnSkips.json',
  'NpcTemplates.Cities2.json',
  'PlayfieldContent\540\SpawnsAdd.cities20261105b.json',
  'PlayfieldContent\545\SpawnsAdd.cities20261105b.json',
  'PlayfieldContent\566\SpawnsAdd.cities20261105b.json',
  'PlayfieldContent\700\SpawnsAdd.cities20261105b.json',
  'PlayfieldContent\705\SpawnsAdd.cities20261105b.json',
  'PlayfieldContent\710\SpawnsAdd.cities20261105b.json',
  'PlayfieldContent\800\SpawnsAdd.cities20261105b.json'
)
$bins = @('ZoneEngine_New.dll', 'ZoneEngine_New.exe', 'ZoneEngine_New.pdb')

$hash = (git -C $wt rev-parse --short=8 HEAD).Trim()
Say "deploy tip=$hash (worktree)"
git -C $wt merge-base --is-ancestor e1986150 HEAD
if ($LASTEXITCODE -ne 0) { Say "STOP: worktree HEAD $hash is not on the e1986150 lineage"; exit 3 }
$cities2 = Join-Path $gdSrc 'NpcTemplates.Cities2.json'
if (-not (Test-Path $cities2)) { Say 'STOP: missing NpcTemplates.Cities2.json'; exit 3 }
$srcSample = ((Get-Content -LiteralPath $cities2 -Raw | ConvertFrom-Json).PSObject.Properties | Select-Object -First 1).Value.ContentProvenance.Source
if ($srcSample -ne $tag) { Say "STOP: Cities2 source is '$srcSample', expected $tag"; exit 3 }
foreach ($n in $bins) { if (-not (Test-Path (Join-Path $zSrc $n))) { throw "missing $n in $zSrc - rebuild ZoneEngine_New to tools-temp\zbuild-cities2" } }
foreach ($f in $cityFiles) { if (-not (Test-Path (Join-Path $gdSrc $f))) { throw "missing GameData\$f" } }
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

$stamp = Get-Date -Format 'yyyyMMdd-HHmm'; $backup = "I:\AO\backups\$db-$stamp-cities-20261105b-$hash.sql"
Say "backup DB -> $backup"
& $mysqldump --defaults-extra-file=$cnf -P 33070 -h 127.0.0.1 --single-transaction --no-tablespaces --result-file=$backup $db
if ($LASTEXITCODE -ne 0) { throw "mysqldump $LASTEXITCODE" }
$blen = (Get-Item -LiteralPath $backup).Length; Say "backup bytes=$blen"; if ($blen -lt 1000000) { throw 'backup too small' }

New-Item -ItemType Directory -Path $rb, "$rb\gd-zone", "$rb\gd-built" | Out-Null
foreach ($n in $bins) { Copy-Item (Join-Path $zDest $n) $rb -Force }
foreach ($pair in @(@($zDestGd, "$rb\gd-zone"), @($builtGd, "$rb\gd-built"))) {
  foreach ($f in $cityFiles) {
    $s = Join-Path $pair[0] $f
    if (Test-Path $s) { $d = Join-Path $pair[1] $f; New-Item -ItemType Directory -Force (Split-Path $d) | Out-Null; Copy-Item $s $d -Force }
  }
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
  foreach ($n in $bins) { if (Test-Path (Join-Path $rb $n)) { Copy-Item (Join-Path $rb $n) $zDest -Force } }
  foreach ($pair in @(@("$rb\gd-zone", $zDestGd), @("$rb\gd-built", $builtGd))) {
    if (-not (Test-Path $pair[1])) { continue }
    foreach ($f in $cityFiles) {
      $s = Join-Path $pair[0] $f; $d = Join-Path $pair[1] $f
      if (Test-Path $s) { New-Item -ItemType Directory -Force (Split-Path $d) | Out-Null; Copy-Item $s $d -Force }
      elseif (Test-Path $d) { Remove-Item $d -Force }
    }
  }
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
  foreach ($n in $bins) { Copy-Item (Join-Path $zSrc $n) $zDest -Force }
  $h = (Get-FileHash (Join-Path $zDest 'ZoneEngine_New.dll') -Algorithm SHA256).Hash
  if ($h -ne (Get-FileHash (Join-Path $zSrc 'ZoneEngine_New.dll') -Algorithm SHA256).Hash) { throw 'dll hash mismatch' }
  Say "installed dll sha=$h"
  foreach ($f in $cityFiles) {
    $d = Join-Path $zDestGd $f; New-Item -ItemType Directory -Force (Split-Path $d) | Out-Null
    Copy-Item (Join-Path $gdSrc $f) $d -Force
    if (Test-Path $builtGd) { $d2 = Join-Path $builtGd $f; New-Item -ItemType Directory -Force (Split-Path $d2) | Out-Null; Copy-Item (Join-Path $gdSrc $f) $d2 -Force }
  }
  Say 'GameData cities files deployed'
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
@("tip=$hash", "db_backup=$backup", "dll_sha256=$h",
  "pid_zone=$($zp.Id)", "pid_chat=$($cp.Id)", "pid_login=$($lp.Id)", "listen7501=$p7501", "listen7012=$p7012", "listen7500=$p7500",
  "when=$(Get-Date -Format o)") | Set-Content -LiteralPath $metaPath -Encoding ascii
Say 'DEPLOY OK (cities-20261105b)'
exit 0
