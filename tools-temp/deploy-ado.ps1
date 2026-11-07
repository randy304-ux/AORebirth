# Deploy Adonis + Rubi-Ka scrape fix batch (tip after 9ded57ac review fixes).
# Includes: NpcTemplates.RK (loot/HP/meshes/M.A.G.S./Elliot look), SpawnsAdd.rk,
# gitignored Playfields/*/Spawns.json pink suppress, Knubot/ElliotFairlane.json ({givenano 293685}),
# ZoneEngine_New from tools-temp\zbuild-ado (Knubot givenano support).
# Do NOT run until Mike authorizes GO. No WMI/CIM. No schema migration.
# Engines start ONLY via: schtasks /run /tn "\DungeonDogs\AO Dungeon Dogs Engines"
# Timed tasklist/netstat only (Start-Process + WaitForExit) — never bare tasklist under Stop.
$ErrorActionPreference = 'Stop'
$outLog = 'I:\AO\src\tools-temp\deploy-ado.out.txt'
$metaPath = 'I:\AO\src\tools-temp\deploy-ado-meta.txt'
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
$tagAdo = 'scrape-adonis-20261106'
$tagRk = 'scrape-rk-20261106'
$tagGil = 'scrape-gilnote-20261106'
$gdSrc = Join-Path $root 'AORebirth\GameData'
$zSrc = Join-Path $root 'tools-temp\zbuild-ado'
$zDest = Join-Path $root 'AORebirth\Built\Debug\ZoneEngine_New'
$zDestGd = Join-Path $zDest 'GameData'
$builtGd = Join-Path $root 'AORebirth\Built\Debug\GameData'
$zShutdown = Join-Path $root 'logs\engines\ZoneEngine_New.shutdown'
$cShutdown = Join-Path $root 'logs\engines\ChatEngine.shutdown'
$lShutdown = Join-Path $root 'logs\engines\LoginEngine.shutdown'
$zExe = Join-Path $zDest 'ZoneEngine_New.exe'
$hash = (git -C $root rev-parse --short HEAD).Trim()
$hhmm = Get-Date -Format 'HHmm'
# Prefer the already-taken GO backup if present from the aborted first attempt
$preferredBackup = 'I:\AO\backups\cellao_codex_clean-20261106-2047-ado-rk.sql'
$backup = if (Test-Path $preferredBackup) { $preferredBackup } else { "I:\AO\backups\cellao_codex_clean-20261106-$hhmm-ado-rk.sql" }
$rollbackDir = Join-Path $root 'tools-temp\rb-ado-rk'

$stageFiles = New-Object System.Collections.Generic.List[string]
foreach ($f in @(
  'NpcTemplates.Adonis.json',
  'ItemTemplates.Adonis.json',
  'NpcTemplates.RK.json',
  'ItemTemplates.RK.json',
  'NpcTemplates.GilNote.json',
  'LootStackOverrides.json',
  'Knubot\ElliotFairlane.json',
  'PlayfieldContent\4872\Npcs.json',
  'PlayfieldContent\4872\SpawnsAdd.adonis.json',
  'PlayfieldContent\4872\SpawnsAdd.gilnote.json',
  'PlayfieldContent\4873\SpawnsAdd.adonis.json',
  'PlayfieldContent\4213\SpawnsAdd.adonis.json',
  'PlayfieldContent\4625\SpawnsAdd.adonis.json',
  'PlayfieldContent\4626\SpawnsAdd.adonis.json'
)) { [void]$stageFiles.Add($f) }

Get-ChildItem -LiteralPath (Join-Path $gdSrc 'PlayfieldContent') -Recurse -Filter 'SpawnsAdd.rk.json' | ForEach-Object {
  $rel = $_.FullName.Substring($gdSrc.Length).TrimStart('\')
  [void]$stageFiles.Add($rel)
  # Pink suppress edits gitignored Playfields/<pf>/Spawns.json — must ship with SpawnsAdd.rk.
  $pf = Split-Path (Split-Path $rel -Parent) -Leaf
  $spRel = Join-Path 'Playfields' (Join-Path $pf 'Spawns.json')
  $spSrc = Join-Path $gdSrc $spRel
  if (Test-Path -LiteralPath $spSrc) { [void]$stageFiles.Add($spRel) }
}

foreach ($f in $stageFiles) {
  $src = Join-Path $gdSrc $f
  if (-not (Test-Path $src)) { throw "missing $src" }
}
if (-not (Test-Path $zExe)) { throw "missing $zExe" }
foreach ($n in @('ZoneEngine_New.dll', 'ZoneEngine_New.exe')) {
  if (-not (Test-Path (Join-Path $zSrc $n))) { throw "missing $n in $zSrc" }
}

Say ("deploy tip={0} ado={1} rk={2} gil={3} files={4}" -f $hash, $tagAdo, $tagRk, $tagGil, $stageFiles.Count)

$online = Q 'SELECT COUNT(*) FROM characters WHERE Online=1'
Say ("online={0}" -f $online)
if ($online -ne '0') { Say 'ABORT online!=0'; exit 2 }

if (-not (Test-Path 'I:\AO\backups')) { New-Item -ItemType Directory 'I:\AO\backups' | Out-Null }
if ($backup -eq $preferredBackup -and (Test-Path $preferredBackup) -and ((Get-Item $preferredBackup).Length -gt 1000000)) {
  Say ("reuse DB backup -> {0} bytes={1}" -f $backup, (Get-Item $preferredBackup).Length)
} else {
  Say ("backup DB -> {0}" -f $backup)
  & $mysqldump --defaults-extra-file=$cnf -P 33070 -h 127.0.0.1 --single-transaction --no-tablespaces --result-file=$backup $db
  if ($LASTEXITCODE -ne 0) { throw "mysqldump $LASTEXITCODE" }
  $blen = (Get-Item -LiteralPath $backup).Length
  Say ("backup bytes={0}" -f $blen)
  if ($blen -lt 1000000) { throw 'backup too small' }
}

if (-not (Test-Path $rollbackDir)) {
  New-Item -ItemType Directory -Force $rollbackDir | Out-Null
}
# Refresh snapshot of currently-live staged targets before overwrite
foreach ($f in $stageFiles) {
  $live = Join-Path $zDestGd $f
  if (Test-Path $live) {
    $dst = Join-Path $rollbackDir $f
    $dstDir = Split-Path $dst -Parent
    if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory $dstDir | Out-Null }
    Copy-Item -LiteralPath $live -Destination $dst -Force
  }
}
foreach ($n in @('ZoneEngine_New.dll', 'ZoneEngine_New.exe', 'ZoneEngine_New.pdb')) {
  $live = Join-Path $zDest $n
  if (Test-Path $live) {
    $binRb = Join-Path $rollbackDir '_bins'
    if (-not (Test-Path $binRb)) { New-Item -ItemType Directory $binRb | Out-Null }
    Copy-Item -LiteralPath $live -Destination (Join-Path $binRb $n) -Force
  }
}
Say ("rollback GameData+bins snapshot -> {0}" -f $rollbackDir)

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

$liveSa = Join-Path $zDestGd 'PlayfieldContent\750\SpawnsAdd.rk.json'
if (-not (Test-Path $liveSa)) { throw "live Reck SpawnsAdd missing after copy: $liveSa" }
$sa = Get-Content -LiteralPath $liveSa -Raw | ConvertFrom-Json
if ($sa.source -ne $tagRk) { throw "bad SpawnsAdd source $($sa.source)" }
Say ("live Reck SpawnsAdd count={0}" -f @($sa.spawns).Count)

$apiBefore = NetListenPid 8098
Say ("api8098 before restart pid={0}" -f $apiBefore)
if ($apiBefore -eq 0) { Say 'WARN account API not listening on 8098 before restart' }

$bootStart = Get-Date
Say 'start engines via DungeonDogs schtask'
$schOut = & schtasks.exe /Run /TN '\DungeonDogs\AO Dungeon Dogs Engines' 2>&1 | Out-String
Say ("schtasks: {0}" -f $schOut.Trim())
if ($LASTEXITCODE -ne 0 -and $schOut -notmatch 'SUCCESS') {
  throw "schtasks failed: $schOut"
}

Start-Sleep 25
$zone2 = TaskPid 'ZoneEngine_New.exe'
$chat2 = TaskPid 'ChatEngine.exe'
$login2 = TaskPid 'LoginEngine.exe'
$api2 = NetListenPid 8098
Say ("post-start zone={0} chat={1} login={2} api8098={3}" -f $zone2, $chat2, $login2, $api2)
Say ("boot_start={0:o}" -f $bootStart)

@(
  "tip=$hash"
  "db_backup=$backup"
  "rollback_gamedata=$rollbackDir"
  "tag_ado=$tagAdo"
  "tag_rk=$tagRk"
  "tag_gil=$tagGil"
  "stage_files=$($stageFiles.Count)"
  "boot_start=$($bootStart.ToString('o'))"
  "zone_pid=$zone2"
  "chat_pid=$chat2"
  "login_pid=$login2"
  "api_pid=$api2"
  "when=$(Get-Date -Format o)"
) | Set-Content -LiteralPath $metaPath -Encoding ascii
Say ("meta {0}" -f $metaPath)

if ($apiBefore -ne 0 -and $api2 -eq 0) { throw 'Account API died during deploy - STOP' }
Say 'DONE deploy ado+rk+gil (verify ports next) - authorized GO'
exit 0
