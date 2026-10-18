# Account API (Store/Daily pages, port 8098) watchdog. Runs from boot-engines.cmd on every engine start and boot.
# Two failed checks in a row restart the account API through its scheduled task (stop, then start).
# -ForceRestart skips the health checks (to test the restart path).
param([switch]$ForceRestart)
$log = 'I:\AO\logs\account-api-watchdog.log'
function Say($m) { Add-Content -Path $log -Value ((Get-Date).ToString('yyyy-MM-dd HH:mm:ss') + ' ' + $m) }
$test = 'I:\AO\src\ddogs\tools\Test-StorePages.ps1'
if ($ForceRestart) { Say 'forced restart (test)' } else {
$out = & powershell -NoProfile -ExecutionPolicy Bypass -File $test -Bases 'http://127.0.0.1:8098' 2>&1 | Out-String
if ($LASTEXITCODE -eq 0) { exit 0 }
Start-Sleep -Seconds 20
$out = & powershell -NoProfile -ExecutionPolicy Bypass -File $test -Bases 'http://127.0.0.1:8098' 2>&1 | Out-String
if ($LASTEXITCODE -eq 0) { Say 'recovered on retry'; exit 0 }
}
Say ('store pages down, restarting account API: ' + ($out -replace '\s+', ' '))
$l = Get-NetTCPConnection -State Listen -LocalPort 8098 -ErrorAction SilentlyContinue | Select-Object -First 1
# Stop the task: that ends its cmd.exe, and the API exits with it (ParentWatch). A task-started process can't be
# killed directly from this session (Access denied), so Stop-Process is only a fallback.
Stop-ScheduledTask -TaskPath '\DungeonDogs\' -TaskName 'AO Dungeon Dogs Account API' -ErrorAction SilentlyContinue
Start-Sleep -Seconds 5
$l = Get-NetTCPConnection -State Listen -LocalPort 8098 -ErrorAction SilentlyContinue | Select-Object -First 1
if ($l) { Say "8098 still held by pid=$($l.OwningProcess) after task stop"; try { Stop-Process -Id $l.OwningProcess -Force -ErrorAction Stop } catch { Say ('stop failed: ' + $_.Exception.Message) } }
Start-Sleep -Seconds 3
Start-ScheduledTask -TaskPath '\DungeonDogs\' -TaskName 'AO Dungeon Dogs Account API'
Start-Sleep -Seconds 15
$out = & powershell -NoProfile -ExecutionPolicy Bypass -File $test -Bases 'http://127.0.0.1:8098' 2>&1 | Out-String
Say ('after restart exit=' + $LASTEXITCODE + ' ' + ($out -replace '\s+', ' '))
