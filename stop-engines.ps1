param(
    [ValidateSet("ChatEngine", "LoginEngine", "ZoneEngine_New", "WebEngine")]
    [string[]]$EngineName,

    [switch]$CoreOnly,

    [switch]$StaleCheckoutsOnly,

    [switch]$IdentifyOnly
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDir = Join-Path $root "logs\engines"
$engineDir = Join-Path $root "AORebirth\Built\Debug"
$configPath = $env:AO_REBIRTH_CONFIG_PATH
if ([string]::IsNullOrWhiteSpace($configPath)) {
    $configPath = Join-Path $root "AORebirth\Config\Config.xml"
}
$statusProbe = Join-Path $root "Tools\engine_status_probe.js"
$cscript = Join-Path $env:SystemRoot "System32\cscript.exe"
$failed = $false
$coreEngineNames = @("ChatEngine", "LoginEngine", "ZoneEngine_New")

if ($CoreOnly -and $EngineName -and $EngineName.Count -gt 0) {
    throw "CoreOnly cannot be combined with EngineName."
}

$engineDefinitions = @(
    @{ Name = "ZoneEngine_New"; File = "ZoneEngine_New\ZoneEngine_New.exe" },
    @{ Name = "WebEngine"; File = "WebEngine.exe" },
    @{ Name = "LoginEngine"; File = "LoginEngine.exe" },
    @{ Name = "ChatEngine"; File = "ChatEngine.exe" }
)

$engines = if ($CoreOnly) {
    @($engineDefinitions | Where-Object { $coreEngineNames -contains $_.Name })
}
elseif ($EngineName -and $EngineName.Count -gt 0) {
    @($engineDefinitions | Where-Object { $EngineName -contains $_.Name })
}
else {
    @($engineDefinitions)
}

function Wait-ProcessExit {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ProcessId,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) {
            return $true
        }

        Start-Sleep -Milliseconds 500
    }

    return -not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)
}

function Stop-EngineProcess {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory = $true)]
        [string]$EngineName,

        [string]$ShutdownFile
    )

    Write-Host "Stopping $EngineName pid=$($Process.Id)"

    if (-not [string]::IsNullOrWhiteSpace($ShutdownFile)) {
        $shutdownDir = Split-Path -Parent $ShutdownFile
        if (-not (Test-Path $shutdownDir)) {
            New-Item -Path $shutdownDir -ItemType Directory | Out-Null
        }

        "stop requested $(Get-Date -Format o)" | Set-Content -Path $ShutdownFile -Encoding UTF8
        if (Wait-ProcessExit -ProcessId $Process.Id -TimeoutSeconds 15) {
            Write-Host "$EngineName stopped after shutdown request."
            return
        }
    }

    try {
        $Process.Refresh()
        if ($Process.MainWindowHandle -ne 0) {
            [void]$Process.CloseMainWindow()
            if (Wait-ProcessExit -ProcessId $Process.Id -TimeoutSeconds 5) {
                Write-Host "$EngineName stopped after close-window request."
                return
            }
        }
    }
    catch {
    }

    if (Get-Process -Id $Process.Id -ErrorAction SilentlyContinue) {
        Write-Warning "$EngineName did not exit cleanly; forcing process stop."
        Stop-Process -Id $Process.Id -Force
        if (-not (Wait-ProcessExit -ProcessId $Process.Id -TimeoutSeconds 5)) {
            throw "$EngineName pid=$($Process.Id) remained running after confirmed force termination."
        }
    }
}

function Write-AcwLog {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host ("[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss.fff'), $Message)
}

# Child cmd with hard timeout. Returns stdout text, or $null on timeout/failure (treated as unknown).
function Invoke-TimedCmdCapture {
    param(
        [Parameter(Mandatory = $true)][string]$CommandLine,
        [int]$TimeoutMs = 10000,
        [string]$Label = 'cmd'
    )

    $tmp = Join-Path $env:TEMP ("ao-acw-{0}-{1}.txt" -f $Label.Replace(':','_'), [guid]::NewGuid().ToString('n'))
    $err = "$tmp.err"
    try {
        $p = Start-Process -FilePath 'cmd.exe' -ArgumentList @('/d', '/c', $CommandLine) -NoNewWindow -PassThru `
            -RedirectStandardOutput $tmp -RedirectStandardError $err
        if (-not $p.WaitForExit($TimeoutMs)) {
            try { $p.Kill() } catch { }
            Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] probe-timeout label={0} after={1}ms treating=unknown" -f $Label, $TimeoutMs)
            return $null
        }
        return (Get-Content -LiteralPath $tmp -Raw -ErrorAction SilentlyContinue)
    }
    catch {
        Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] probe-error label={0} err={1}" -f $Label, $_.Exception.Message)
        return $null
    }
    finally {
        Remove-Item -LiteralPath $tmp, $err -Force -ErrorAction SilentlyContinue
    }
}

function Get-ProcessesByExecutablePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExpectedPath
    )

    $normalizedExpectedPath = [System.IO.Path]::GetFullPath($ExpectedPath)
    $image = [System.IO.Path]::GetFileName($normalizedExpectedPath)
    $raw = Invoke-TimedCmdCapture -CommandLine ('tasklist /FO CSV /NH /FI "IMAGENAME eq {0}"' -f $image) -TimeoutMs 10000 -Label ("tasklist:$image")
    if ($null -eq $raw) {
        return @()
    }

    $matches = New-Object System.Collections.Generic.List[object]
    foreach ($line in @($raw -split "`r?`n")) {
        if ($line -notmatch '^"([^"]+)","(\d+)"') { continue }
        $procId = [int]$Matches[2]
        $pathRaw = Invoke-TimedCmdCapture -CommandLine (
            'powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "try { (Get-Process -Id {0} -ErrorAction Stop).Path } catch {{ }}"' -f $procId
        ) -TimeoutMs 10000 -Label ("path:${image}:${procId}")
        if ([string]::IsNullOrWhiteSpace($pathRaw)) { continue }
        try {
            $candidatePath = [System.IO.Path]::GetFullPath($pathRaw.Trim())
            if ($candidatePath -ieq $normalizedExpectedPath) {
                $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
                if ($proc) { $matches.Add($proc) }
            }
        }
        catch { }
    }
    return @($matches)
}

function Get-AORebirthEngineProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Engine
    )

    if ($coreEngineNames -notcontains $Engine.Name) {
        return @()
    }

    $relativeEnginePath = Join-Path "AORebirth\Built\Debug" ([string]$Engine.File)
    $pathSuffix = [System.IO.Path]::DirectorySeparatorChar + $relativeEnginePath
    $image = [System.IO.Path]::GetFileName([string]$Engine.File)
    $procIds = @()

    Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] begin-identify engine={0} via timed tasklist" -f $Engine.Name)
    $raw = Invoke-TimedCmdCapture -CommandLine ('tasklist /FO CSV /NH /FI "IMAGENAME eq {0}"' -f $image) -TimeoutMs 5000 -Label ("tasklist:$image")
    if ($null -eq $raw) {
        # Filtered tasklist hangs on this host for some images; fall back to Get-Process -Name (not WMI).
        Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] tasklist-timeout engine={0}; fallback Get-Process -Name" -f $Engine.Name)
        try {
            $procIds = @(Get-Process -Name $Engine.Name -ErrorAction SilentlyContinue | ForEach-Object { [int]$_.Id })
        }
        catch {
            $procIds = @()
        }
    }
    else {
        foreach ($line in @($raw -split "`r?`n")) {
            if ($line -notmatch '^"([^"]+)","(\d+)"') { continue }
            if ($Matches[1] -ine $image) { continue }
            $procIds += [int]$Matches[2]
        }
    }

    $found = @()
    foreach ($procId in $procIds) {
        Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] path-probe engine={0} pid={1}" -f $Engine.Name, $procId)
        $candidatePath = $null
        try {
            $proc = Get-Process -Id $procId -ErrorAction Stop
            if ($proc.Path) {
                $candidatePath = [System.IO.Path]::GetFullPath([string]$proc.Path)
            }
        }
        catch {
            Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] skip engine={0} pid={1} reason=checkout-path-unknown" -f $Engine.Name, $procId)
            continue
        }

        if ([string]::IsNullOrWhiteSpace($candidatePath)) {
            Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] skip engine={0} pid={1} reason=checkout-path-unknown" -f $Engine.Name, $procId)
            continue
        }

        try {
            if ([System.IO.Path]::GetFileNameWithoutExtension($candidatePath) -ine $Engine.Name) { continue }
            if (-not $candidatePath.EndsWith($pathSuffix, [System.StringComparison]::OrdinalIgnoreCase)) { continue }

            $checkoutRootText = $candidatePath.Substring(0, $candidatePath.Length - $pathSuffix.Length)
            if ([string]::IsNullOrWhiteSpace($checkoutRootText)) { continue }

            $checkoutRoot = [System.IO.Path]::GetFullPath($checkoutRootText)
            $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
            if (-not $proc) { continue }

            $found += [pscustomobject]@{
                Process = $proc
                Path = $candidatePath
                CheckoutRoot = $checkoutRoot
                ShutdownFile = Join-Path $checkoutRoot "logs\engines\$($Engine.Name).shutdown"
                IsCurrentCheckout = $checkoutRoot -ieq [System.IO.Path]::GetFullPath($root)
            }
        }
        catch {
            Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] skip engine={0} pid={1} reason=path-parse-error" -f $Engine.Name, $procId)
        }
    }

    Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] end-identify engine={0} matches={1}" -f $Engine.Name, $found.Count)
    if ($null -eq $found -or $found.Count -eq 0) {
        return @()
    }
    return @($found)
}

function Wait-EnginePrestartState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EngineName,

        [switch]$AllowCurrentCheckoutOwner,

        [int]$TimeoutSeconds = 10
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $probeOutput = @()
    $probeExit = 1
    do {
        Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] prestart-probe begin engine={0}" -f $EngineName)
        $tmp = Join-Path $env:TEMP ("ao-prestart-{0}-{1}.txt" -f $EngineName, [guid]::NewGuid().ToString('n'))
        $err = "$tmp.err"
        try {
            $p = Start-Process -FilePath $cscript -ArgumentList @(
                '//nologo', $statusProbe,
                '--config', $configPath,
                '--engine-dir', $engineDir,
                '--prestart', $EngineName
            ) -NoNewWindow -PassThru -RedirectStandardOutput $tmp -RedirectStandardError $err
            # Probe itself uses ~10s tasklist/netstat budgets; allow ~45s wall clock for a full pass.
            if (-not $p.WaitForExit(45000)) {
                try { $p.Kill() } catch { }
                Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] prestart-probe timeout engine={0} treating=unknown-skip" -f $EngineName)
                return -1
            }
            $probeOutput = @(Get-Content -LiteralPath $tmp -ErrorAction SilentlyContinue)
            # cscript ExitCode is sometimes null under redirected Start-Process; infer from probe text.
            $probeExit = $p.ExitCode
            $joined = ($probeOutput -join "`n")
            if ($joined -match 'ALREADY_RUNNING') {
                $probeExit = 3
            }
            elseif ($joined -match 'ownership=CLEAR' -or $joined -match 'prestart-clear' -or $joined -match 'PASS - selected engine is absent') {
                $probeExit = 0
            }
            elseif ($null -eq $probeExit) {
                $probeExit = 1
            }
            else {
                $probeExit = [int]$probeExit
            }
        }
        finally {
            Remove-Item -LiteralPath $tmp, $err -Force -ErrorAction SilentlyContinue
        }

        Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] prestart-probe end engine={0} exit={1}" -f $EngineName, $probeExit)
        if ($probeExit -eq 0 -or ($AllowCurrentCheckoutOwner -and $probeExit -eq 3)) {
            $probeOutput | ForEach-Object { Write-Host $_ }
            return $probeExit
        }
        # Unknown/timeout from soft probes inside cscript still returns quickly; mismatch retries until deadline.
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)

    $probeOutput | ForEach-Object { Write-Host $_ }
    return $probeExit
}

foreach ($engine in @($engines | Where-Object { $coreEngineNames -contains $_.Name })) {
    foreach ($identified in @(Get-AORebirthEngineProcesses -Engine $engine)) {
        Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] identified engine={0} pid={1} checkout={2} path={3}" -f `
            $engine.Name, $identified.Process.Id, $identified.CheckoutRoot, $identified.Path)

        if ($IdentifyOnly) {
            continue
        }
        if ($StaleCheckoutsOnly -and $identified.IsCurrentCheckout) {
            Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] keep current-checkout engine={0} pid={1}" -f $engine.Name, $identified.Process.Id)
            continue
        }

        try {
            Stop-EngineProcess `
                -Process $identified.Process `
                -EngineName $engine.Name `
                -ShutdownFile $identified.ShutdownFile
            if (Test-Path -LiteralPath $identified.ShutdownFile) {
                Remove-Item -LiteralPath $identified.ShutdownFile -Force
            }
        }
        catch {
            Write-Warning ("Could not stop positively identified AORebirth {0} pid={1} from checkout {2}." -f `
                $engine.Name, $identified.Process.Id, $identified.CheckoutRoot)
            $failed = $true
        }
    }
}

if ($IdentifyOnly) {
    Write-AcwLog "ACTIVE_CHECKOUT_WINS identification complete; no process was stopped."
    return
}

if ($StaleCheckoutsOnly) {
    foreach ($engine in @($engines | Where-Object { $coreEngineNames -contains $_.Name })) {
        $prestartExit = Wait-EnginePrestartState -EngineName $engine.Name -AllowCurrentCheckoutOwner
        if ($prestartExit -eq -1) {
            Write-AcwLog ("[ACTIVE_CHECKOUT_WINS] skip prestart-verify engine={0} reason=probe-timeout" -f $engine.Name)
            continue
        }
        if ($prestartExit -ne 0 -and $prestartExit -ne 3) {
            Write-Warning "$($engine.Name) pre-start state remains unsafe; an unknown or unverified owner was not killed."
            $failed = $true
        }
    }

    if ($failed) {
        Write-Error "AO Rebirth stale-checkout cleanup did not reach a verified safe state."
    }

    Write-AcwLog "AO Rebirth stale-checkout cleanup complete."
    return
}

foreach ($engine in $engines) {
    $pidFile = Join-Path $logDir "$($engine.Name).pid.json"
    $defaultShutdownFile = Join-Path $logDir "$($engine.Name).shutdown"
    $metadataProcess = $null
    $shutdownFile = $defaultShutdownFile
    $metadataIsTrusted = $false
    $managedStopVerified = $false
    $expectedPath = [System.IO.Path]::GetFullPath((Join-Path $engineDir $engine.File))

    if (Test-Path $pidFile) {
        try {
            $metadata = Get-Content -Path $pidFile -Raw | ConvertFrom-Json
            $metadataProcess = Get-Process -Id ([int]$metadata.Pid) -ErrorAction SilentlyContinue
            if ($metadata.ShutdownFile) {
                $shutdownFile = [string]$metadata.ShutdownFile
            }

            if ($metadataProcess) {
                $actualPath = $null
                try { if (-not [string]::IsNullOrWhiteSpace($metadataProcess.Path)) { $actualPath = [System.IO.Path]::GetFullPath($metadataProcess.Path) } } catch { }
                if (-not $actualPath) {
                    # S4U task-launched engines deny path/handle queries to this session. Identify them instead by
                    # process name + recorded PID + WMI creation time + the exact listening ports from the metadata.
                    $cim = Get-CimInstance Win32_Process -Filter "ProcessId=$($metadataProcess.Id)" -ErrorAction SilentlyContinue
                    $recordedStartAlt = [DateTime]::Parse([string]$metadata.StartedAt, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::RoundtripKind)
                    $expectedPorts = @($metadata.Ports | ForEach-Object { [int]$_ })
                    $ownedPorts = @(Get-NetTCPConnection -State Listen -OwningProcess $metadataProcess.Id -ErrorAction SilentlyContinue | ForEach-Object { [int]$_.LocalPort })
                    $portsOk = $expectedPorts.Count -gt 0 -and @($expectedPorts | Where-Object { $ownedPorts -notcontains $_ }).Count -eq 0
                    $timeOk = $cim -and [Math]::Abs(($cim.CreationDate.ToUniversalTime() - $recordedStartAlt.ToUniversalTime()).TotalSeconds) -le 5
                    if ($metadataProcess.ProcessName -ieq $engine.Name -and $coreEngineNames -contains $engine.Name -and $portsOk -and $timeOk -and
                        [System.IO.Path]::GetFullPath([string]$metadata.Path) -ieq $expectedPath) {
                        Write-Host "$($engine.Name) pid=$($metadataProcess.Id) identified by name, start time and ports $($expectedPorts -join ',') (path unreadable)."
                        $actualPath = $expectedPath
                    }
                    else {
                        $actualPath = '<unverified>'
                    }
                }
                $recordedPath = [System.IO.Path]::GetFullPath([string]$metadata.Path)
                $recordedStart = [DateTime]::Parse(
                    [string]$metadata.StartedAt,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [System.Globalization.DateTimeStyles]::RoundtripKind)
                $procStart = $null; try { $procStart = $metadataProcess.StartTime } catch { }
                if (-not $procStart) { $procStart = (Get-CimInstance Win32_Process -Filter "ProcessId=$($metadataProcess.Id)").CreationDate }
                $startDifferenceSeconds = [Math]::Abs(
                    ($procStart.ToUniversalTime() - $recordedStart.ToUniversalTime()).TotalSeconds)
                if ([string]$metadata.Engine -ieq $engine.Name -and
                    $actualPath -ieq $expectedPath -and
                    $recordedPath -ieq $expectedPath -and
                    [System.IO.Path]::GetFullPath($shutdownFile) -ieq [System.IO.Path]::GetFullPath($defaultShutdownFile) -and
                    $startDifferenceSeconds -le 5) {
                    $metadataIsTrusted = $true
                }
                else {
                    Write-Warning "$($engine.Name) PID metadata does not identify the expected repository executable; no process was stopped."
                    $failed = $true
                }
            }
            else {
                $metadataIsTrusted = $true
                $managedStopVerified = $true
            }
        }
        catch {
            Write-Warning "Could not safely validate PID metadata for $($engine.Name); no process was stopped."
            $failed = $true
        }
    }

    if ($metadataProcess -and $metadataIsTrusted) {
        try {
            Stop-EngineProcess -Process $metadataProcess -EngineName $engine.Name -ShutdownFile $shutdownFile
            $managedStopVerified = $true
        }
        catch {
            Write-Warning "$($engine.Name) managed PID could not be stopped safely."
            $failed = $true
        }
    }
    elseif (-not (Test-Path $pidFile)) {
        Write-Host "$($engine.Name) PID metadata process is not running."
    }

    if ($metadataIsTrusted -and $managedStopVerified -and (Test-Path $pidFile)) {
        Remove-Item -LiteralPath $pidFile -Force
    }

    if ($metadataIsTrusted -and $managedStopVerified -and (Test-Path $shutdownFile)) {
        Remove-Item -LiteralPath $shutdownFile -Force
    }
}

# Stop every selected managed process before checking released listeners.
foreach ($engine in $engines) {
    $expectedPath = [System.IO.Path]::GetFullPath((Join-Path $engineDir $engine.File))
    if ($engine.Name -eq "ZoneEngine_New") {
        # Verify the exact backend path as well as released listener ownership.
        $stillRunning = @(Get-ProcessesByExecutablePath -ExpectedPath $expectedPath)
        if ($stillRunning) {
            Write-Warning "ZoneEngine_New is still running after stop (pid=$($stillRunning.Id -join ','))."
            $failed = $true
        }
        else {
            Write-Host "ZoneEngine_New process is not running."
        }
        $releaseExit = Wait-EnginePrestartState -EngineName "ZoneEngine_New"
        if ($releaseExit -ne 0) {
            Write-Warning "ZoneEngine_New zone port is not fully released; an unknown or unverified owner was not killed."
            $failed = $true
        }
    }
    else {
        $releaseExit = Wait-EnginePrestartState -EngineName $engine.Name
        if ($releaseExit -ne 0) {
            Write-Warning "$($engine.Name) is not fully stopped with its ports released; an unknown or unverified owner was not killed."
            $failed = $true
        }
    }
}

if ($failed) {
    Write-Error "AO Rebirth engine shutdown did not reach a fully verified state."
}

Write-Host "AO Rebirth engine shutdown complete."
