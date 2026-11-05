# deploy-icc-655.ps1 - ICC HQ (pf 655) scrape fixes, branch icc-655-fix-20261105 (worktree I:\AO\worktrees\icc-655-fix).
# DO NOT RUN without Randy's explicit go. Rules: online=0, DB backup first, engines up >=5 min, shutdown-file stop, schtasks start,
# verify Zone 7501 / Chat 7012 / Login 7500. Applies tools-temp\icc-vendors-655.sql (16 ICC vendor positions).
$ErrorActionPreference = 'Stop'
$wt = 'I:\AO\worktrees\icc-655-fix'; $root = 'I:\AO\src'; $base = '62dc2770'; $tag = 'scrape-icc655-20261105'
$outLog = Join-Path $wt 'tools-temp\deploy-icc-655.out.txt'; $metaPath = Join-Path $wt 'tools-temp\deploy-icc-655-meta.txt'
Set-Content -LiteralPath $outLog -Value '' -Encoding ascii
function Say([string]$m) { $l = '{0} {1}' -f (Get-Date -Format 'HH:mm:ss'), $m; Write-Output $l; Add-Content -LiteralPath $outLog -Value $l }
$mysql = 'I:\AO\mysql\server\bin\mysql.exe'; $mysqldump = 'I:\AO\mysql\server\bin\mysqldump.exe'
$cnf = 'I:\AO\secrets\aorebirth-client.cnf'; $db = 'cellao_codex_clean'
function Q([string]$sql) { (& $mysql --defaults-extra-file=$cnf -P 33070 -h 127.0.0.1 -N -e $sql $db | Out-String).Trim() }
$zDest = Join-Path $root 'AORebirth\Built\Debug\ZoneEngine_New'; $zDestGd = Join-Path $zDest 'GameData'
$builtGd = Join-Path $root 'AORebirth\Built\Debug\GameData'
$zShutdown = Join-Path $root 'logs\engines\ZoneEngine_New.shutdown'; $cShutdown = Join-Path $root 'logs\engines\ChatEngine.shutdown'
$zSrc = Join-Path $wt 'tools-temp\zbuild-icc655\ze'; $gdSrc = Join-Path $wt 'AORebirth\GameData'
$sqlFile = Join-Path $wt 'tools-temp\icc-vendors-655.sql'
$files = @('Halloween.json', 'SpawnSkips.json', 'NpcTemplates.ICC655.json', 'PlayfieldContent\655\SpawnsAdd.icc655.json')
foreach ($f in $files) { if (-not (Test-Path (Join-Path $gdSrc $f))) { throw "missing $f - run ddogs\tools\Build-ScrapeImportICC655.ps1" } }
foreach ($f in 'ZoneEngine_New.dll', 'ZoneEngine_New.exe') { if (-not (Test-Path (Join-Path $zSrc $f))) { throw "missing build $f in $zSrc" } }
if (-not (Test-Path $sqlFile)) { throw "missing $sqlFile" }

# --- live tip gate: this branch is built on 62dc2770; stop if anything else went live since.
$head = (git -C $root rev-parse --short=8 HEAD).Trim()
$lastMeta = Get-ChildItem (Join-Path $root 'tools-temp') -Filter 'deploy-*-meta.txt' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$metaTip = if ($lastMeta) { ((Get-Content $lastMeta.FullName | Where-Object { $_ -like 'tip=*' } | Select-Object -First 1) -replace '^tip=', '').Trim() } else { '' }
Say ("main checkout HEAD={0} last deploy meta={1} tip={2}" -f $head, $(if ($lastMeta) { $lastMeta.Name } else { 'none' }), $metaTip)
$drift = @()
if ($head -notlike "$base*") { $drift += "I:\AO\src HEAD is $head, not $base" }
if ($metaTip -notlike "$base*") { $drift += "last deploy ($($lastMeta.Name)) was tip $metaTip, not $base" }
foreach ($f in 'Halloween.json', 'SpawnSkips.json') {
  $want = (git -C $root rev-parse "$($base):AORebirth/GameData/$($f -replace '\\','/')").Trim()
  $have = (git -C $root hash-object (Join-Path $zDestGd $f)).Trim()
  if ($want -ne $have) { $drift += "live $f differs from $base (another deploy changed it)" }
}
if ($drift.Count) {
  $drift | ForEach-Object { Say "STOP: $_" }
  Say "STOP: live is no longer the $base build. Rebase icc-655-fix-20261105 onto the new live tip, rerun Build-ScrapeImportICC655.ps1, rebuild tools-temp\zbuild-icc655, then retry."
  exit 3
}

$online = Q 'SELECT COUNT(*) FROM characters WHERE Online=1'
Say ("online={0}" -f $online); if ($online -ne '0') { Say 'ABORT online!=0'; exit 2 }

$zone = Get-Process ZoneEngine_New -ErrorAction SilentlyContinue | Select-Object -First 1
$chat = Get-Process ChatEngine -ErrorAction SilentlyContinue | Select-Object -First 1
$login = Get-Process LoginEngine -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $login) { throw 'LoginEngine must stay up' }
foreach ($p in @($zone, $chat)) { if ($p -and ((Get-Date) - $p.StartTime).TotalMinutes -lt 5) { Say 'ABORT engine uptime <5 min (boot gate)'; exit 2 } }
$bootLog = 'I:\AO\logs\boot-engines.log'
if ((Test-Path $bootLog) -and ((Get-Date) - (Get-Item $bootLog).LastWriteTime).TotalMinutes -lt 5) { Say 'ABORT last boot under 5 min'; exit 2 }

$stamp = Get-Date -Format 'yyyyMMdd-HHmm'; $backup = "I:\AO\backups\$db-$stamp-icc-655.sql"
if (-not (Test-Path 'I:\AO\backups')) { New-Item -ItemType Directory 'I:\AO\backups' | Out-Null }
Say "backup DB -> $backup"
& $mysqldump --defaults-extra-file=$cnf -P 33070 -h 127.0.0.1 --single-transaction --no-tablespaces --result-file=$backup $db
if ($LASTEXITCODE -ne 0) { throw "mysqldump $LASTEXITCODE" }
$blen = (Get-Item -LiteralPath $backup).Length; Say "backup bytes=$blen"; if ($blen -lt 1000000) { throw 'backup too small' }

function Stop-ViaShutdown([string]$name, [string]$shutdownFile) {
  if (-not (Get-Process $name -ErrorAction SilentlyContinue)) { Say "$name already down"; return }
  Say "stop $name (shutdown file)"; "stop requested $(Get-Date -Format o)" | Set-Content -Path $shutdownFile -Encoding UTF8
  $dl = (Get-Date).AddSeconds(90); while ((Get-Date) -lt $dl -and (Get-Process $name -ErrorAction SilentlyContinue)) { Start-Sleep 2 }
  Start-Sleep 2
  if (Get-Process $name -ErrorAction SilentlyContinue) { Get-Process $name | ForEach-Object { Stop-Process -Id $_.Id -Force }; Start-Sleep 2 }
  if (Get-Process $name -ErrorAction SilentlyContinue) { throw "$name still up" }
}
$loginId = $login.Id
Stop-ViaShutdown 'ZoneEngine_New' $zShutdown
Stop-ViaShutdown 'ChatEngine' $cShutdown
if (-not (Get-Process -Id $loginId -ErrorAction SilentlyContinue)) { throw 'Login died' }

Say 'apply icc-vendors-655.sql'
$before = Q 'SELECT COUNT(*) FROM vendors WHERE Playfield=655 AND X=0 AND Y=0 AND Z=0'
cmd /c ("`"{0}`" --defaults-extra-file={1} -P 33070 -h 127.0.0.1 {2} < `"{3}`"" -f $mysql, $cnf, $db, $sqlFile)
if ($LASTEXITCODE -ne 0) { throw "vendor sql $LASTEXITCODE (restore from $backup if needed)" }
$after = Q 'SELECT COUNT(*) FROM vendors WHERE Playfield=655 AND X=0 AND Y=0 AND Z=0'
Say "vendors at 0,0,0 in 655: before=$before after=$after"

Say 'copy ZoneEngine_New dll/exe/pdb'
foreach ($f in 'ZoneEngine_New.dll', 'ZoneEngine_New.exe', 'ZoneEngine_New.pdb') { Copy-Item (Join-Path $zSrc $f) $zDest -Force }
foreach ($dst in @($zDestGd, $builtGd)) {
  if (-not (Test-Path $dst)) { continue }
  foreach ($f in $files) { $t = Join-Path $dst $f; New-Item -ItemType Directory -Force (Split-Path $t) | Out-Null; Copy-Item (Join-Path $gdSrc $f) $t -Force }
  Say "GameData files -> $dst"
}
$sa = Get-Content -LiteralPath (Join-Path $zDestGd 'PlayfieldContent\655\SpawnsAdd.icc655.json') -Raw | ConvertFrom-Json
if ($sa.source -ne $tag) { throw "bad SpawnsAdd.icc655 source $($sa.source)" }

Say 'start engines: schtasks /run /tn "\DungeonDogs\AO Dungeon Dogs Engines"'
$st = schtasks /run /tn '\DungeonDogs\AO Dungeon Dogs Engines' 2>&1 | Out-String; Say ("schtasks: {0}" -f $st.Trim())
function Port-Owner([int]$port) {
  $c = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $c) { return 'none' }; $p = Get-Process -Id $c.OwningProcess -ErrorAction SilentlyContinue
  if ($p) { return ('{0}:{1}' -f $p.ProcessName, $p.Id) }; return ('pid:{0}' -f $c.OwningProcess)
}
# boot gate: allow up to 5 minutes for Zone/Chat/Login to listen
$deadline = (Get-Date).AddMinutes(5); $p7501 = $p7012 = $p7500 = 'none'
while ((Get-Date) -lt $deadline) {
  $p7501 = Port-Owner 7501; $p7012 = Port-Owner 7012; $p7500 = Port-Owner 7500
  if ($p7501 -match 'ZoneEngine' -and $p7012 -match 'ChatEngine' -and $p7500 -match 'LoginEngine') { break }
  Start-Sleep 5
}
Say ("ports zone7501={0} chat7012={1} login7500={2}" -f $p7501, $p7012, $p7500)
$ok = ($p7501 -match 'ZoneEngine' -and $p7012 -match 'ChatEngine' -and $p7500 -match 'LoginEngine')
@("branch=icc-655-fix-20261105", "commit=$((git -C $wt rev-parse --short=8 HEAD).Trim())", "base=$base", "tip=$base+icc655", "db_backup=$backup",
  "spawnsAdd_icc655=$(@($sa.spawns).Count)", "vendors_zero_before=$before after=$after", "tag=$tag",
  "port_7501=$p7501", "port_7012=$p7012", "port_7500=$p7500", "ok=$ok", "when=$(Get-Date -Format o)") | Set-Content -LiteralPath $metaPath -Encoding ascii
if (-not $ok) { Say "ABORT ports not all up after 5 min - check logs; DB backup $backup"; exit 4 }
Say 'DONE deploy icc-655 (Randy: check ICC HQ in game)'
