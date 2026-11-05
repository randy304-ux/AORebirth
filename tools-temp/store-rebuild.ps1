$ErrorActionPreference = 'Stop'
$outDir = 'I:\AO\src\tools-temp\zbuild-store'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
cmd /c "call I:\AO\tools\ao-build-env.cmd && cd /d I:\AO\src && I:\AO\tools\dotnet\dotnet.exe msbuild AORebirth\Server\ZoneEngine_New\ZoneEngine_New.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal /p:OutputPath=I:\AO\src\tools-temp\zbuild-store\ > I:\AO\src\tools-temp\store-build.log 2>&1"
$code = $LASTEXITCODE
Select-String -Path 'I:\AO\src\tools-temp\store-build.log' -Pattern 'error CS|ZoneEngine_New ->|Build FAILED|Build succeeded' | ForEach-Object { $_.Line }
if ($code -ne 0) { Write-Output ("ze_exit=" + $code); exit $code }

cmd /c "I:\AO\src\tools-temp\publish-account-api.cmd"
if ($LASTEXITCODE -ne 0) { Write-Output ("api_publish_exit=" + $LASTEXITCODE); exit $LASTEXITCODE }
$apiDest = Join-Path $outDir 'account-api'
if (Test-Path $apiDest) { Remove-Item $apiDest -Recurse -Force }
New-Item -ItemType Directory -Path $apiDest | Out-Null
cmd /c "robocopy I:\AO\cache\temp\account-api-publish I:\AO\src\tools-temp\zbuild-store\account-api /E /NFL /NDL /NJH /NJS /NP"
if ($LASTEXITCODE -ge 8) { Write-Output ("api_robocopy_exit=" + $LASTEXITCODE); exit $LASTEXITCODE }
if (-not (Test-Path (Join-Path $apiDest 'account-api.dll'))) { throw 'account-api.dll missing after publish copy' }
Write-Output 'store-rebuild OK (ZoneEngine_New + account-api -> zbuild-store)'
exit 0
