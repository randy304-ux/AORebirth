$ErrorActionPreference = 'Stop'
$log = 'I:\AO\src\tools-temp\buffbot-live-build.log'
cmd /c "call I:\AO\tools\ao-build-env.cmd && cd /d I:\AO\src && I:\AO\tools\dotnet\dotnet.exe msbuild AORebirth\Server\ZoneEngine_New\ZoneEngine_New.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal /p:OutputPath=I:\AO\src\tools-temp\zbuild-buffbot-live\ > I:\AO\src\tools-temp\buffbot-live-build.log 2>&1"
if ($LASTEXITCODE -ne 0) {
  Select-String -Path $log -Pattern 'error CS|error MSB|Build FAILED' | Select-Object -First 20 | ForEach-Object { $_.Line }
  throw "build failed exit=$LASTEXITCODE"
}
Select-String -Path $log -Pattern 'ZoneEngine_New ->' | ForEach-Object { $_.Line }
Write-Output 'BUILD PASS'
