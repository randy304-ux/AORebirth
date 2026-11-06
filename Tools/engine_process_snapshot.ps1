# Timed tasklist-based engine process snapshot (WMI-free).
# Prints one line per matching engine: pid|ImageName|Path
# Path is filled via a per-PID Get-Process query; empty path if that times out/fails.
$ErrorActionPreference = 'SilentlyContinue'
$names = @('ChatEngine.exe', 'LoginEngine.exe', 'ZoneEngine_New.exe', 'WebEngine.exe')
foreach ($image in $names) {
    $tmp = Join-Path $env:TEMP ("ao-snap-tl-{0}.txt" -f [guid]::NewGuid().ToString('n'))
    try {
        $p = Start-Process -FilePath 'cmd.exe' -ArgumentList @('/d', '/c', ('tasklist /FO CSV /NH /FI "IMAGENAME eq {0}"' -f $image)) `
            -NoNewWindow -PassThru -RedirectStandardOutput $tmp -RedirectStandardError "$tmp.err"
        if (-not $p.WaitForExit(10000)) {
            try { $p.Kill() } catch { }
            continue
        }
        foreach ($line in @(Get-Content -LiteralPath $tmp -ErrorAction SilentlyContinue)) {
            if ($line -notmatch '^"([^"]+)","(\d+)"') { continue }
            $listed = $Matches[1]
            $procId = [int]$Matches[2]
            if ($listed -ine $image) { continue }
            $path = ''
            try {
                $gp = Get-Process -Id $procId -ErrorAction Stop
                if ($gp.Path) { $path = [string]$gp.Path }
            }
            catch { }
            Write-Output ('{0}|{1}|{2}' -f $procId, $image, $path)
        }
    }
    finally {
        Remove-Item -LiteralPath $tmp, "$tmp.err" -Force -ErrorAction SilentlyContinue
    }
}
