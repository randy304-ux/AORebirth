# One-shot bounded boot verify. No retry loops. No WMI/CIM.
$ErrorActionPreference = 'Stop'
$outLog = 'I:\AO\src\tools-temp\ao-boot-verify.out.txt'
Set-Content -LiteralPath $outLog -Value '' -Encoding ascii
function Say([string]$m) {
  $line = '{0} {1}' -f (Get-Date -Format 'HH:mm:ss'), $m
  Write-Output $line
  Add-Content -LiteralPath $outLog -Value $line
}
function TaskPid([string]$image) {
  $tmp = Join-Path $env:TEMP ("ao-tl-{0}.txt" -f [guid]::NewGuid().ToString('n'))
  $err = "$tmp.err"
  $result = 0
  try {
    $p = Start-Process -FilePath 'cmd.exe' -ArgumentList @('/d','/c',("tasklist /FI `"IMAGENAME eq {0}`" /NH /FO CSV" -f $image)) -NoNewWindow -PassThru -RedirectStandardOutput $tmp -RedirectStandardError $err
    if (-not $p.WaitForExit(20000)) {
      try { $p.Kill() } catch {}
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

$bootStart = Get-Date '2026-11-07T10:16:26'
$deadline = $bootStart.AddMinutes(6)
Say ("wait until {0:o} for listen ports" -f $deadline)
while ((Get-Date) -lt $deadline) {
  $z = NetListenPid 7501
  $l = NetListenPid 7500
  $c = NetListenPid 7012
  $a = NetListenPid 8098
  Say ("probe 7501={0} 7500={1} 7012={2} 8098={3}" -f $z,$l,$c,$a)
  if ($z -ne 0 -and $l -ne 0 -and $c -ne 0 -and $a -ne 0) { break }
  Start-Sleep 20
}

$zonePid = TaskPid 'ZoneEngine_New.exe'
$chatPid = TaskPid 'ChatEngine.exe'
$loginPid = TaskPid 'LoginEngine.exe'
$apiPid = NetListenPid 8098
$p7501 = NetListenPid 7501
$p7500 = NetListenPid 7500
$p7012 = NetListenPid 7012
$elapsed = ((Get-Date) - $bootStart).TotalSeconds
Say ("FINAL zone_pid={0} chat_pid={1} login_pid={2} api_pid={3}" -f $zonePid,$chatPid,$loginPid,$apiPid)
Say ("FINAL ports 7501={0} 7500={1} 7012={2} 8098={3}" -f $p7501,$p7500,$p7012,$apiPid)
Say ("boot_elapsed_sec={0:N0}" -f $elapsed)

# Watchdog status check (report only)
$dw = (& schtasks.exe /Query /TN '\Docker API Watchdog' /FO LIST 2>&1 | Out-String)
$bw = (& schtasks.exe /Query /TN '\DungeonDogs-Bitebot-Watchdog' /FO LIST 2>&1 | Out-String)
if ($dw -match 'Status:\s+(\S+)') { Say ("Docker API Watchdog Status={0}" -f $Matches[1]) }
if ($bw -match 'Status:\s+(\S+)') { Say ("Bitebot Watchdog Status={0}" -f $Matches[1]) }

@(
  "zone_pid=$zonePid"
  "chat_pid=$chatPid"
  "login_pid=$loginPid"
  "api_pid=$apiPid"
  "port_7501=$p7501"
  "port_7500=$p7500"
  "port_7012=$p7012"
  "port_8098=$apiPid"
  "boot_elapsed_sec=$([int]$elapsed)"
  "when=$(Get-Date -Format o)"
) | Set-Content -LiteralPath 'I:\AO\src\tools-temp\ao-boot-verify-meta.txt' -Encoding ascii

if ($p7501 -eq 0 -or $p7500 -eq 0 -or $p7012 -eq 0) { exit 2 }
if ($apiPid -eq 0) { exit 3 }
exit 0
