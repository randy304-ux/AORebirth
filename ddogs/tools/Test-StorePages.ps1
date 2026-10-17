# ddogs/tools/Test-StorePages.ps1 - fetch the in-game Store and Daily pages the way the patched client does
# (TCP to the web host:8098, Host header uwg.store.icc-rk / uwg.daily.icc-rk, path /index.app). Exit 0 only when both pages answer 200
# with their own titles. Used after every engine restart and by the account-API watchdog.
param([string[]]$Bases = @('http://127.0.0.1:8098', 'http://192.168.1.68:8098'), [int]$TimeoutSec = 8)
$want = @{ 'uwg.store.icc-rk' = 'Info'; 'uwg.daily.icc-rk' = 'Daily Reward' }
$bad = 0
foreach ($b in $Bases) {
  foreach ($h in $want.Keys) {
    try {
      $r = Invoke-WebRequest "$b/index.app" -Headers @{ Host = $h } -UseBasicParsing -TimeoutSec $TimeoutSec
      $title = [regex]::Match($r.Content, '<title>([^<]*)').Groups[1].Value
      if ($r.StatusCode -eq 200 -and $title -eq $want[$h]) { Write-Output "PASS $h via $b title=$title" }
      else { Write-Output "FAIL $h via $b status=$($r.StatusCode) title=$title"; $bad++ }
    } catch { Write-Output "FAIL $h via $b $($_.Exception.Message)"; $bad++ }
  }
}
exit $bad
