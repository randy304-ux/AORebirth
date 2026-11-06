# WMI-free engine snapshot for engine_status_probe.js.
# Uses timed tasklist (per image) + timed netstat. Hung probes are killed and treated as empty/unknown.
param(
    [Parameter(Mandatory = $true)][string]$OutFile,
    [Parameter(Mandatory = $true)][string]$Images,
    [int]$TimeoutMs = 12000
)

$ErrorActionPreference = 'Continue'
$lines = New-Object System.Collections.Generic.List[string]

function Invoke-TimedCmd([string]$CommandLine, [string]$Label) {
    $tmp = Join-Path $env:TEMP ("ao-ess-{0}-{1}.txt" -f ($Label -replace '[^a-zA-Z0-9]', '_'), [guid]::NewGuid().ToString('n'))
    $err = "$tmp.err"
    try {
        $p = Start-Process -FilePath 'cmd.exe' -ArgumentList @('/d', '/c', $CommandLine) -NoNewWindow -PassThru `
            -RedirectStandardOutput $tmp -RedirectStandardError $err
        if (-not $p.WaitForExit($TimeoutMs)) {
            try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch { }
            # Also try killing child image if this was tasklist
            Start-Sleep -Milliseconds 200
            return $null
        }
        return (Get-Content -LiteralPath $tmp -Raw -ErrorAction SilentlyContinue)
    }
    catch {
        return $null
    }
    finally {
        Remove-Item -LiteralPath $tmp, $err -Force -ErrorAction SilentlyContinue
    }
}

foreach ($image in @($Images -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
    $raw = Invoke-TimedCmd -CommandLine ('tasklist /FO CSV /NH /FI "IMAGENAME eq {0}"' -f $image) -Label ("tl_$image")
    $found = $false
    if ($null -ne $raw) {
        foreach ($line in @($raw -split "`r?`n")) {
            if ($line -notmatch '^"([^"]+)","(\d+)"') { continue }
            if ($Matches[1] -ine $image) { continue }
            $lines.Add(('PROC|{0}|{1}|' -f [int]$Matches[2], $Matches[1]))
            $found = $true
        }
    }
    else {
        $lines.Add("TIMEOUT|tasklist|$image")
        # Filtered tasklist hangs on this host for some images; fall back to Get-Process -Name (not WMI).
        $procName = [IO.Path]::GetFileNameWithoutExtension($image)
        $gpTmp = Join-Path $env:TEMP ("ao-ess-gp-{0}.txt" -f [guid]::NewGuid().ToString('n'))
        try {
            $ps = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
                '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command',
                ("Get-Process -Name '{0}' -ErrorAction SilentlyContinue | ForEach-Object {{ '{1}|' + `$_.Id }}" -f $procName, $image)
            ) -NoNewWindow -PassThru -RedirectStandardOutput $gpTmp -RedirectStandardError "$gpTmp.err"
            if ($ps.WaitForExit($TimeoutMs)) {
                foreach ($gline in @(Get-Content -LiteralPath $gpTmp -ErrorAction SilentlyContinue)) {
                    if ($gline -match '^(.+)\|(\d+)$') {
                        $lines.Add(('PROC|{0}|{1}|' -f [int]$Matches[2], $Matches[1]))
                        $found = $true
                    }
                }
            }
            else {
                try { Stop-Process -Id $ps.Id -Force -EA SilentlyContinue } catch { }
                $lines.Add("TIMEOUT|Get-Process|$image")
            }
        }
        finally {
            Remove-Item -LiteralPath $gpTmp, "$gpTmp.err" -Force -ErrorAction SilentlyContinue
        }
    }
    if (-not $found -and $null -ne $raw) {
        # tasklist returned but no rows — engine not running
    }
}

$net = $null
for ($netAttempt = 1; $netAttempt -le 2; $netAttempt++) {
    $net = Invoke-TimedCmd -CommandLine 'netstat -ano' -Label ("netstat_$netAttempt")
    if ($null -ne $net) { break }
}
if ($null -eq $net) {
    $lines.Add('TIMEOUT|netstat|')
}
else {
    foreach ($line in @($net -split "`r?`n")) {
        if ($line -notmatch 'LISTENING') { continue }
        if ($line -notmatch ':(\d+)\s+\S+\s+LISTENING\s+(\d+)\s*$') { continue }
        $lines.Add(('LISTEN|{0}|{1}' -f [int]$Matches[1], [int]$Matches[2]))
    }
}

$lines.Add('DONE')
$tmpOut = "$OutFile.partial"
Set-Content -LiteralPath $tmpOut -Value ($lines -join "`r`n") -Encoding ASCII
Move-Item -LiteralPath $tmpOut -Destination $OutFile -Force
