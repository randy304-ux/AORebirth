# Timed netstat check - no WMI/CIM
$ErrorActionPreference = 'Stop'
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
foreach ($port in @(33070,7500,7501,7012,8098,3306)) {
  Write-Output ("{0}={1}" -f $port, (NetListenPid $port))
}
