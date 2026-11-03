# stage-cb-spawns.ps1 — Cyborg Barracks scrape import, STAGE ONLY.
# Default: rebuild GameData from plan, copy GameData into Built\Debug\ZoneEngine_New\GameData.
# Does NOT stop engines, does NOT touch live DB.
# -ApplySqlScratch: run merc BossHash UPDATE against a named scratch DB only.
# -ApplyLive: apply SQL to cellao_codex_clean (refuses if anyone Online).
param(
  [switch]$SkipBuild,
  [switch]$SkipGameDataCopy,
  [switch]$ApplySqlScratch,
  [string]$ScratchDb = 'cellao_codex_scratch',
  [switch]$ApplyLive
)
$ErrorActionPreference = 'Stop'
$outLog = 'I:\AO\src\tools-temp\stage-cb-spawns.out.txt'
Set-Content -LiteralPath $outLog -Value '' -Encoding ascii
function Say([string]$m) {
  $line = '{0} {1}' -f (Get-Date -Format 'HH:mm:ss'), $m
  Write-Output $line
  Add-Content -LiteralPath $outLog -Value $line
}

$root = 'I:\AO\src'
$gd = Join-Path $root 'AORebirth\GameData'
$build = Join-Path $root 'ddogs\tools\Build-ScrapeImportCB.ps1'
$plan = Join-Path $root 'ddogs\tools\scrape_import_cb_20261103.json'
$sqlMig = Join-Path $root 'AORebirth\Libraries\Source\AORebirth.Database\Migrations\20261103_cyborg_barracks_thvi.sql'
$zDestGd = Join-Path $root 'AORebirth\Built\Debug\ZoneEngine_New\GameData'
$report = Join-Path $root 'ddogs\docs\cyborg-barracks-scrape-20261103.md'

if (-not (Test-Path $plan)) { throw "missing plan $plan" }
if (-not (Test-Path $build)) { throw "missing builder $build" }
if (-not (Test-Path $report)) { throw "missing report $report" }

if (-not $SkipBuild) {
  Say 'Build-ScrapeImportCB.ps1'
  & powershell -NoProfile -File $build -GameData $gd
  if ($LASTEXITCODE -ne 0) { throw "build failed $LASTEXITCODE" }
}

$u = [Text.UTF8Encoding]::new($false)
$p = [IO.File]::ReadAllText($plan, $u) | ConvertFrom-Json
$nt = [IO.File]::ReadAllText((Join-Path $gd 'NpcTemplates.json'), $u) | ConvertFrom-Json
$hashes = @($p.templates | ForEach-Object { $_.hash })
$missing = @($hashes | Where-Object { -not $nt.$_ })
if ($missing.Count -gt 0) { throw "templates missing after build: $($missing -join ',')" }
$boss = $nt.THVI
if (-not $boss) { throw 'THVI template missing' }
$bossBand = @($boss.Templates)[0]
Say ('boss THVI name={0} L{1} md={2} mesh={3}' -f $bossBand.Name, $bossBand.Level, $bossBand.Stats.'359', $bossBand.Stats.'12')
Say ('templates={0} matched={1} roamers={2}' -f $p.meta.npcTemplates, $p.meta.matched, @($p.roamers).Count)

if (-not $SkipGameDataCopy) {
  if (-not (Test-Path $zDestGd)) {
    Say ('Built GameData dest missing ({0}) - skip copy' -f $zDestGd)
  } else {
    Say 'robocopy GameData -> Built\Debug\ZoneEngine_New\GameData'
    cmd /c ("robocopy `"{0}`" `"{1}`" /E /NJH /NJS /NDL /NP /R:1 /W:1" -f $gd, $zDestGd)
    if ($LASTEXITCODE -ge 8) { throw "robocopy $LASTEXITCODE" }
    Say 'GameData staged into Built\Debug'
  }
}

$mysql = 'I:\AO\mysql\server\bin\mysql.exe'
$cnf = 'I:\AO\secrets\root-client.cnf'
function Q([string]$db, [string]$sql) {
  (& $mysql --defaults-extra-file=$cnf -N -e $sql $db | Out-String).Trim()
}

Say ('SQL migration file: {0}' -f $sqlMig)
Say 'SQL dry-run (not applied unless flagged):'
Get-Content -LiteralPath $sqlMig | ForEach-Object { Say ('  | {0}' -f $_) }

if ($ApplyLive -and $ApplySqlScratch) { throw 'pick one of -ApplyLive or -ApplySqlScratch' }

if ($ApplySqlScratch) {
  if (-not (Test-Path $mysql)) { throw 'mysql.exe missing' }
  Say ('ApplySqlScratch -> {0}' -f $ScratchDb)
  $exists = Q 'mysql' ("SHOW DATABASES LIKE '{0}'" -f $ScratchDb.Replace("'","''"))
  if (-not $exists) { throw "scratch DB $ScratchDb not found" }
  Get-Content -LiteralPath $sqlMig -Raw | & $mysql --defaults-extra-file=$cnf $ScratchDb
  if ($LASTEXITCODE -ne 0) { throw "scratch SQL failed $LASTEXITCODE" }
  $got = Q $ScratchDb "SELECT MercId,BossHash FROM merc_definitions WHERE MercId='soldier_cyborg'"
  Say ('scratch verify: {0}' -f $got)
} elseif ($ApplyLive) {
  if (-not (Test-Path $mysql)) { throw 'mysql.exe missing' }
  $online = Q 'cellao_codex_clean' 'SELECT COUNT(*) FROM characters WHERE Online=1'
  Say ('live online={0}' -f $online)
  if ($online -ne '0') { throw 'ABORT: refuse live SQL while characters Online>0' }
  Say 'WARNING: applying SQL to cellao_codex_clean (explicit -ApplyLive)'
  Get-Content -LiteralPath $sqlMig -Raw | & $mysql --defaults-extra-file=$cnf cellao_codex_clean
  if ($LASTEXITCODE -ne 0) { throw "live SQL failed $LASTEXITCODE" }
} else {
  Say 'SQL not applied (default stage-only). Use -ApplySqlScratch or -ApplyLive when ready.'
}

Say ('report: {0}' -f $report)
Say 'DONE stage-only'
