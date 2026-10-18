# Publish an already-staged release (stage-launcher.ps1 output) byte-for-byte. Usage: publish-staged.ps1 -Stage <dir>
# Order: blobs + files manifest + zips first, version.json last, so a client never sees a version.json whose files are missing.
# Blobs are content-addressed (files/<sha256>.bin): existing ones are never rewritten, so in-flight downloads are safe.
param([Parameter(Mandatory)][string]$Stage)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\deltapack.ps1"
$check = Test-DdStage $Stage   # hashes, sizes, zip contents == manifest, no client/Funcom payload
$ver = Get-Content (Join-Path $Stage "version.json") -Raw | ConvertFrom-Json
$zip = Join-Path $Stage $ver.zipUrl
$patchZip = Join-Path $Stage $ver.patchZipUrl
$pub = "H:\amp-dashboard\public\downloads\ao"
$newBlobs = 0
if ($check.Manifest) {
  New-Item -ItemType Directory -Force "$pub\files" | Out-Null
  foreach ($b in Get-ChildItem (Join-Path $Stage 'files') -File) {
    $t = "$pub\files\$($b.Name)"
    if ((Test-Path -LiteralPath $t) -and (Get-DdSha $t) -eq $b.BaseName) { continue }
    Copy-Item -LiteralPath $b.FullName "$t.tmp" -Force
    if ((Get-DdSha "$t.tmp") -ne $b.BaseName) { throw "blob copy hash mismatch $($b.Name)" }
    Move-Item -LiteralPath "$t.tmp" $t -Force
    $newBlobs++
  }
  Copy-Item (Join-Path $Stage $ver.filesManifestUrl) "$pub\$($ver.filesManifestUrl)" -Force
  if ((Get-DdSha "$pub\$($ver.filesManifestUrl)") -ne $ver.filesManifestSha256) { throw "published files manifest hash mismatch" }
}
foreach ($t in @("I:\AO\dist\DungeonDogsLauncher.zip", "$pub\DungeonDogs-AO-Launcher.zip", "$pub\$($ver.zipUrl)")) { Copy-Item $zip $t -Force }
if (-not ((Test-Path "$pub\$($ver.patchZipUrl)") -and (Get-DdSha "$pub\$($ver.patchZipUrl)") -eq $ver.patchSha256)) { Copy-Item $patchZip "$pub\$($ver.patchZipUrl)" -Force }
if (-not ((Test-Path "I:\AO\dist\$($ver.patchZipUrl)") -and (Get-DdSha "I:\AO\dist\$($ver.patchZipUrl)") -eq $ver.patchSha256)) { Copy-Item $patchZip "I:\AO\dist\$($ver.patchZipUrl)" -Force }
foreach ($f in @("$pub\$($ver.zipUrl)", "$pub\DungeonDogs-AO-Launcher.zip", "I:\AO\dist\DungeonDogsLauncher.zip")) { if ((Get-DdSha $f) -ne $ver.sha256) { throw "published hash mismatch $f" } }
foreach ($f in @("$pub\$($ver.patchZipUrl)", "I:\AO\dist\$($ver.patchZipUrl)")) { if ((Get-DdSha $f) -ne $ver.patchSha256) { throw "published patch hash mismatch $f" } }
$bytes = [IO.File]::ReadAllBytes((Join-Path $Stage "version.json"))
if ($bytes[0] -ne 123) { throw "version.json must start with { and have no BOM" }
[IO.File]::WriteAllBytes("$pub\version.json.tmp", $bytes)
Move-Item "$pub\version.json.tmp" "$pub\version.json" -Force
$folder = "I:\AO\dist\DungeonDogsLauncher"
if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
New-Item -ItemType Directory -Force $folder | Out-Null
& tar.exe -x -f $zip -C $folder
if ($LASTEXITCODE -ne 0) { throw "dist folder extract failed" }
Write-Output "VERSION $($ver.version)"
Write-Output "NEW BLOBS $newBlobs (of $($check.Blobs))"
Write-Output "PUBLISHED OK"