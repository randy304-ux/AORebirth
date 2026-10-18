# STAGE ONLY: builds a release from I:\AO\launcher\dist into I:\AO\cache\temp\staged-<version>. Nothing is published.
# Randy approves publishing (publish-staged.ps1 -Stage <dir>).
# Output: deterministic launcher + patch zips (old launchers), plus files-manifest-<version>.json and files/<sha256>.bin
# for per-file updates. The patch zip is reused byte-for-byte when its files match the published one.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\deltapack.ps1"
$dist = "I:\AO\launcher\dist"
$cfg = Get-Content (Join-Path $dist "config.json") -Raw
if ($cfg -notmatch 'AO_DDOGS_WEB_PORT') { throw "published config.json missing AO_DDOGS_WEB_PORT" }
if (-not (Test-Path (Join-Path $dist "client-patch\version.dll"))) { throw "missing version.dll" }
$rel = Get-Content (Join-Path $dist "release.json") -Raw | ConvertFrom-Json
Write-Output ("RELEASE " + $rel.version)
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
New-Item -ItemType Directory -Force "I:\AO\cache\temp" | Out-Null
# Launcher files without the client patch payload, user settings, or update leftovers.
$lstage = "I:\AO\cache\temp\launcher-stage-$stamp"
New-Item -ItemType Directory -Force $lstage | Out-Null
& robocopy $dist $lstage /E /XD backups pristine Tools client-files client-patch update /XF user-settings.json *.bak config.json.bak-20261012 tools-state.json DungeonDogsLauncher.pdb patch.json update.log /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed $LASTEXITCODE" }
$pstage = "I:\AO\cache\temp\patch-stage-$stamp"
New-Item -ItemType Directory -Force $pstage | Out-Null
foreach ($d in 'client-files', 'client-patch') {
  & robocopy (Join-Path $dist $d) (Join-Path $pstage $d) /E /NFL /NDL /NJH /NJS | Out-Null
  if ($LASTEXITCODE -ge 8) { throw "$d copy failed $LASTEXITCODE" }
}
$out = "I:\AO\cache\temp\staged-$($rel.version)"
$r = New-DdStage -LauncherRoot $lstage -PatchRoot $pstage -Version $rel.version -ClientPatchDll (Join-Path $dist "client-patch\version.dll") -Out $out -PublishedDir 'H:\amp-dashboard\public\downloads\ao'
$ver = Get-Content (Join-Path $out 'version.json') -Raw | ConvertFrom-Json
if ($ver.size -gt 90000000) { throw "launcher zip is $($ver.size) bytes; the patch payload is still inside it" }
$check = Test-DdStage $out
Remove-Item $lstage, $pstage -Recurse -Force
Write-Output "STAGED $out"
Write-Output ("launcher files=" + $r.LauncherFiles + " patch files=" + $r.PatchFiles + " blobs=" + $check.Blobs + " patchZipReusedFromPublished=" + $r.PatchZipReused)
Get-Content (Join-Path $out 'version.json')