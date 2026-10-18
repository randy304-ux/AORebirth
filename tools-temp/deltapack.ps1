# Shared by stage-launcher.ps1, publish-staged.ps1 and the update tests.
# Deterministic zips (ordinal-sorted entries, fixed timestamps, no directory entries) and the per-file manifest
# (files-manifest-<version>.json + content-addressed blobs files/<sha256>.bin). Only launcher and patch files.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$script:DdFixedTime = New-Object DateTimeOffset 2000, 1, 1, 0, 0, 0, ([TimeSpan]::Zero)

function Get-DdRelFiles([string]$Root) {
  $full = (Resolve-Path -LiteralPath $Root).ProviderPath.TrimEnd('\')
  [string[]]$rel = @(Get-ChildItem -LiteralPath $full -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($full.Length + 1).Replace('\', '/') })
  [Array]::Sort($rel, [StringComparer]::Ordinal)
  return ,$rel
}

function Get-DdSha([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLower() }

function Test-DdClientPayload([string]$Rel) {
  # Never publish Funcom/client files: game executables, installer data, or anything that looks like the client's own data.
  return ($Rel -match '(^|/)(AnarchyOnline|Anarchy|AnarchyOnline64|ClientControl)\.exe$' -or $Rel -match '(^|/)install-[^/]*\.bin$' -or
          $Rel -match '(^|/)cd_image/data/' -or $Rel -match '\.(rdb|idx)$')
}

function New-DdZip([string]$Root, [string[]]$Rel, [string]$Out) {
  [string[]]$sorted = $Rel.Clone(); [Array]::Sort($sorted, [StringComparer]::Ordinal)
  if (Test-Path -LiteralPath $Out) { Remove-Item -LiteralPath $Out -Force }
  $fs = [IO.File]::Open($Out, [IO.FileMode]::CreateNew)
  try {
    $za = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create, $false)
    try {
      foreach ($r in $sorted) {
        $e = $za.CreateEntry($r, [IO.Compression.CompressionLevel]::Optimal)
        $e.LastWriteTime = $script:DdFixedTime
        $dst = $e.Open(); $src = [IO.File]::OpenRead((Join-Path $Root $r.Replace('/', '\')))
        try { $src.CopyTo($dst) } finally { $src.Dispose(); $dst.Dispose() }
      }
    } finally { $za.Dispose() }
  } finally { $fs.Dispose() }
}

# path -> sha256 for every file entry inside a zip (streamed, nothing extracted).
function Get-DdZipContent([string]$Zip) {
  $map = @{}
  $za = [IO.Compression.ZipFile]::OpenRead($Zip)
  try {
    $sha = [Security.Cryptography.SHA256]::Create()
    foreach ($e in $za.Entries) {
      if ([string]::IsNullOrEmpty($e.Name)) { continue }
      $s = $e.Open(); try { $h = $sha.ComputeHash($s) } finally { $s.Dispose() }
      $map[$e.FullName.Replace('\', '/')] = ([BitConverter]::ToString($h) -replace '-', '').ToLower()
    }
  } finally { $za.Dispose() }
  return $map
}

function Get-DdEntries([string]$Root, [string[]]$Rel, [string]$Prefix = '') {
  $list = New-Object System.Collections.ArrayList
  foreach ($r in $Rel) {
    $p = Join-Path $Root $r.Replace('/', '\')
    [void]$list.Add([ordered]@{ path = ($Prefix + $r); size = [long](Get-Item -LiteralPath $p).Length; sha256 = (Get-DdSha $p) })
  }
  return ,$list
}

function Write-DdJson([string]$Path, $Object) {
  $json = ($Object | ConvertTo-Json -Depth 8).Trim() + "`r`n"
  [IO.File]::WriteAllText($Path, $json, (New-Object Text.UTF8Encoding $false))
}

<#
 Builds a complete staged release in $Out:
   version.json (all fields old launchers read, plus filesManifestUrl / filesManifestSha256)
   DungeonDogs-AO-Launcher-<version>.zip, DungeonDogs-AO-Patch.zip (deterministic)
   files-manifest-<version>.json, files/<sha256>.bin
 $LauncherRoot holds the launcher files; $PatchRoot holds client-files/ and client-patch/.
 $PublishedDir (optional): when its patch zip has exactly the same files, that zip is reused byte-for-byte so its hash
 (and every installed launcher's patch.json) stays valid and nobody re-downloads it.
#>
function New-DdStage([string]$LauncherRoot, [string]$PatchRoot, [string]$Version, [string]$ClientPatchDll, [string]$Out, [string]$PublishedDir = '') {
  if (Test-Path -LiteralPath $Out) { Remove-Item -LiteralPath $Out -Recurse -Force }
  New-Item -ItemType Directory -Force (Join-Path $Out 'files') | Out-Null
  $lRel = Get-DdRelFiles $LauncherRoot
  $pRel = Get-DdRelFiles $PatchRoot
  foreach ($r in $lRel) {
    if (Test-DdClientPayload $r) { throw "refusing: client file in launcher payload: $r" }
    if ($r -match '^(client-files|client-patch|update|backups|pristine|Tools)/' -or $r -in @('user-settings.json', 'patch.json', 'update.log')) { throw "refusing: not a launcher file: $r" }
  }
  foreach ($r in $pRel) {
    if (Test-DdClientPayload $r) { throw "refusing: client file in patch payload: $r" }
    if ($r -notmatch '^(client-files|client-patch)/' -or $r -match '\.exe$') { throw "refusing: unexpected patch file: $r" }
  }
  if (-not ($lRel -contains 'DungeonDogsLauncher.exe')) { throw 'launcher payload has no DungeonDogsLauncher.exe' }

  $zipName = "DungeonDogs-AO-Launcher-$Version.zip"
  $zip = Join-Path $Out $zipName
  New-DdZip $LauncherRoot $lRel $zip
  $patchName = 'DungeonDogs-AO-Patch.zip'
  $patchZip = Join-Path $Out $patchName
  $launcher = Get-DdEntries $LauncherRoot $lRel
  $patch = Get-DdEntries $PatchRoot $pRel

  $reused = $false
  if ($PublishedDir -and (Test-Path -LiteralPath (Join-Path $PublishedDir $patchName))) {
    $pub = Get-DdZipContent (Join-Path $PublishedDir $patchName)
    $same = ($pub.Count -eq $patch.Count)
    if ($same) { foreach ($e in $patch) { if ($pub[$e.path] -ne $e.sha256) { $same = $false; break } } }
    if ($same) { Copy-Item -LiteralPath (Join-Path $PublishedDir $patchName) $patchZip; $reused = $true }
  }
  if (-not $reused) { New-DdZip $PatchRoot $pRel $patchZip }

  foreach ($set in @(@{ root = $LauncherRoot; list = $launcher }, @{ root = $PatchRoot; list = $patch })) {
    foreach ($e in $set.list) {
      $blob = Join-Path $Out ("files\" + $e.sha256 + ".bin")
      if (-not (Test-Path -LiteralPath $blob)) { Copy-Item -LiteralPath (Join-Path $set.root $e.path.Replace('/', '\')) $blob }
    }
  }
  $manifestName = "files-manifest-$Version.json"
  Write-DdJson (Join-Path $Out $manifestName) ([ordered]@{ format = 1; version = $Version; blobBase = 'files/'; launcher = [object[]]$launcher; patch = [object[]]$patch })
  $ver = [ordered]@{
    version = $Version
    zipUrl = $zipName
    sha256 = (Get-DdSha $zip)
    size = (Get-Item -LiteralPath $zip).Length
    patchZipUrl = $patchName
    patchSha256 = (Get-DdSha $patchZip)
    patchSize = (Get-Item -LiteralPath $patchZip).Length
    clientPatchSha256 = (Get-DdSha $ClientPatchDll)
    filesManifestUrl = $manifestName
    filesManifestSha256 = (Get-DdSha (Join-Path $Out $manifestName))
  }
  Write-DdJson (Join-Path $Out 'version.json') $ver
  return [pscustomobject]@{ Out = $Out; Version = $Version; PatchZipReused = $reused; LauncherFiles = $launcher.Count; PatchFiles = $patch.Count }
}

# Full consistency check of a staged release: hashes, blob contents, zip contents == manifest lists, and the payload rules.
function Test-DdStage([string]$Stage) {
  $vb = [IO.File]::ReadAllBytes((Join-Path $Stage 'version.json'))
  if ($vb[0] -ne 123) { throw 'version.json must start with { and have no BOM' }
  $ver = Get-Content (Join-Path $Stage 'version.json') -Raw | ConvertFrom-Json
  foreach ($k in 'version', 'zipUrl', 'sha256', 'size', 'patchZipUrl', 'patchSha256', 'patchSize', 'clientPatchSha256') { if (-not $ver.$k) { throw "version.json missing $k (old launchers need it)" } }
  $zip = Join-Path $Stage $ver.zipUrl; $patchZip = Join-Path $Stage $ver.patchZipUrl
  if ((Get-DdSha $zip) -ne $ver.sha256 -or (Get-Item $zip).Length -ne $ver.size) { throw 'staged launcher zip hash/size mismatch' }
  if ((Get-DdSha $patchZip) -ne $ver.patchSha256 -or (Get-Item $patchZip).Length -ne $ver.patchSize) { throw 'staged patch zip hash/size mismatch' }
  $lz = Get-DdZipContent $zip; $pz = Get-DdZipContent $patchZip
  foreach ($k in $lz.Keys) { if ((Test-DdClientPayload $k) -or $k -match '^(client-files|client-patch)/') { throw "refusing: launcher zip has client/patch payload $k" } }
  foreach ($k in $pz.Keys) { if ((Test-DdClientPayload $k) -or $k -notmatch '^(client-files|client-patch)/' -or $k -match '\.exe$') { throw "refusing: unexpected file in patch zip $k" } }
  if ($pz['client-patch/version.dll'] -ne $ver.clientPatchSha256) { throw 'clientPatchSha256 does not match version.dll in the patch zip' }
  if (-not $ver.filesManifestUrl) { return [pscustomobject]@{ Version = $ver.version; Blobs = 0; Manifest = $false } }
  $mPath = Join-Path $Stage $ver.filesManifestUrl
  if ((Get-DdSha $mPath) -ne $ver.filesManifestSha256) { throw 'files manifest hash mismatch' }
  $m = Get-Content $mPath -Raw | ConvertFrom-Json
  if ($m.format -ne 1 -or $m.version -ne $ver.version -or $m.blobBase -ne 'files/') { throw 'files manifest header mismatch' }
  foreach ($pair in @(@{ list = @($m.launcher); zip = $lz; name = 'launcher' }, @{ list = @($m.patch); zip = $pz; name = 'patch' })) {
    if ($pair.list.Count -ne $pair.zip.Count) { throw "$($pair.name) manifest has $($pair.list.Count) files but the zip has $($pair.zip.Count)" }
    foreach ($e in $pair.list) {
      if ($pair.zip[$e.path] -ne $e.sha256) { throw "$($pair.name) manifest entry $($e.path) does not match the zip" }
    }
  }
  $need = @{}; foreach ($e in @($m.launcher) + @($m.patch)) { $need[$e.sha256] = [long]$e.size }
  foreach ($sha in $need.Keys) {
    $b = Join-Path $Stage "files\$sha.bin"
    if (-not (Test-Path -LiteralPath $b) -or (Get-Item -LiteralPath $b).Length -ne $need[$sha] -or (Get-DdSha $b) -ne $sha) { throw "blob $sha missing or wrong" }
  }
  $extra = @(Get-ChildItem (Join-Path $Stage 'files') -File | Where-Object { -not $need.ContainsKey($_.BaseName) })
  if ($extra.Count) { throw "unexpected blob(s) in stage: $($extra[0].Name)" }
  return [pscustomobject]@{ Version = $ver.version; Blobs = $need.Count; Manifest = $true }
}