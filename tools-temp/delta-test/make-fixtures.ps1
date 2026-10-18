# Builds update-test fixtures with the real staging code (deltapack.ps1) on small fake launcher/patch trees.
$ErrorActionPreference = 'Stop'
. "I:\AO\src\tools-temp\deltapack.ps1"
$fx = 'I:\AO\cache\temp\delta-fx'
if (Test-Path $fx) { Remove-Item $fx -Recurse -Force }
New-Item -ItemType Directory -Force $fx | Out-Null
function Rand([string]$p, [int]$n, [int]$seed) { $r = New-Object Random $seed; $b = New-Object byte[] $n; $r.NextBytes($b); New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null; [IO.File]::WriteAllBytes($p, $b) }
function Txt([string]$p, [string]$s) { New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null; [IO.File]::WriteAllText($p, $s) }
$who = [IO.File]::ReadAllBytes('C:\Windows\System32\whoami.exe')
function Exe([string]$p, [int]$pad) { New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null; $b = New-Object byte[] ($who.Length + 3000000); [Array]::Copy($who, $b, $who.Length); (New-Object Random $pad).NextBytes([ArraySegment[byte]]::new($b, $who.Length, 3000000).Array); [Array]::Copy($who, $b, $who.Length); [IO.File]::WriteAllBytes($p, $b) }
function Tree([string]$v, [int]$exeSeed, [int]$mapBSeed) {
  $l = "$fx\src-$v\launcher"; $p = "$fx\src-$v\patch"
  Exe "$l\DungeonDogsLauncher.exe" $exeSeed
  Txt "$l\release.json" "{ `"version`": `"$v`", `"exe`": `"DungeonDogsLauncher.exe`" }"
  Txt "$l\manifest.json" "{ `"version`": `"$v`", `"baseUrl`": `"client-files/`", `"ops`": [], `"files`": [] }"
  Txt "$l\config.json" '{ "selfUpdateUrls": [] }'
  Rand "$l\assets\background.jpg" 200000 11
  Rand "$p\client-files\cd_image\textures\PlanetMap\TestMap\mapA.bin" 2000000 21
  Rand "$p\client-files\cd_image\textures\PlanetMap\TestMap\mapB.bin" 1000000 $mapBSeed
  Txt "$p\client-files\cd_image\textures\PlanetMap\TestMap\map.txt" 'test map'
  Rand "$p\client-patch\version.dll" 218112 41
  Rand "$p\client-patch\aovk\randy31.dll" 300000 42
}
# v1 baseline; v2 = one patch file changed + launcher metadata only; v3 = launcher exe changed
Tree '2099.01.01-0001' 1 31
Tree '2099.01.01-0002' 1 32
Tree '2099.01.01-0003' 2 32
foreach ($v in '2099.01.01-0001', '2099.01.01-0002', '2099.01.01-0003') {
  $r = New-DdStage "$fx\src-$v\launcher" "$fx\src-$v\patch" $v "$fx\src-$v\patch\client-patch\version.dll" "$fx\srv-$v"
  $c = Test-DdStage "$fx\srv-$v"
  "staged $v blobs=$($c.Blobs) reused=$($r.PatchZipReused)"
}
# Fallback servers built from v2: no manifest fields; manifest file missing; a corrupt blob.
foreach ($n in 'nomani', 'mani404', 'badblob') { Copy-Item "$fx\srv-2099.01.01-0002" "$fx\srv-$n" -Recurse }
$vj = Get-Content "$fx\srv-nomani\version.json" -Raw | ConvertFrom-Json
$old = [ordered]@{}; foreach ($k in 'version', 'zipUrl', 'sha256', 'size', 'patchZipUrl', 'patchSha256', 'patchSize', 'clientPatchSha256') { $old[$k] = $vj.$k }
Write-DdJson "$fx\srv-nomani\version.json" $old
Remove-Item "$fx\srv-mani404\files-manifest-2099.01.01-0002.json"
$m = Get-Content "$fx\srv-badblob\files-manifest-2099.01.01-0002.json" -Raw | ConvertFrom-Json
$a = @($m.patch | Where-Object { $_.path -like '*mapA.bin' })[0].sha256
$bb = [IO.File]::ReadAllBytes("$fx\srv-badblob\files\$a.bin"); $bb[100] = $bb[100] -bxor 0xFF; [IO.File]::WriteAllBytes("$fx\srv-badblob\files\$a.bin", $bb)

# Determinism: restage v2 after touching every source mtime -> identical zips; reuse of a matching published patch zip.
$h1 = (Get-Content "$fx\srv-2099.01.01-0002\version.json" -Raw | ConvertFrom-Json)
Get-ChildItem "$fx\src-2099.01.01-0002" -Recurse -File | ForEach-Object { $_.LastWriteTime = (Get-Date).AddDays(-3) }
Start-Sleep -Seconds 2
$r2 = New-DdStage "$fx\src-2099.01.01-0002\launcher" "$fx\src-2099.01.01-0002\patch" '2099.01.01-0002' "$fx\src-2099.01.01-0002\patch\client-patch\version.dll" "$fx\restage-v2"
$h2 = (Get-Content "$fx\restage-v2\version.json" -Raw | ConvertFrom-Json)
"DETERMINISTIC launcherZip=" + ($h1.sha256 -eq $h2.sha256) + " patchZip=" + ($h1.patchSha256 -eq $h2.patchSha256) + " manifest=" + ($h1.filesManifestSha256 -eq $h2.filesManifestSha256)
# Publish-side reuse: a tar-built (non-deterministic) published patch zip with the same files is reused byte-for-byte.
New-Item -ItemType Directory -Force "$fx\pub-tar" | Out-Null
Push-Location "$fx\src-2099.01.01-0002\patch"; & tar.exe -a -c -f "$fx\pub-tar\DungeonDogs-AO-Patch.zip" *; Pop-Location
$r3 = New-DdStage "$fx\src-2099.01.01-0003\launcher" "$fx\src-2099.01.01-0003\patch" '2099.01.01-0003' "$fx\src-2099.01.01-0003\patch\client-patch\version.dll" "$fx\restage-v3-reuse" "$fx\pub-tar"
$h3 = Get-Content "$fx\restage-v3-reuse\version.json" -Raw | ConvertFrom-Json
"REUSE sameFiles reused=" + $r3.PatchZipReused + " hashEqualsPublished=" + ($h3.patchSha256 -eq (Get-DdSha "$fx\pub-tar\DungeonDogs-AO-Patch.zip"))
$r4 = New-DdStage "$fx\src-2099.01.01-0003\launcher" "$fx\src-2099.01.01-0001\patch" '2099.01.01-0003' "$fx\src-2099.01.01-0001\patch\client-patch\version.dll" "$fx\restage-v3-noreuse" "$fx\pub-tar"
"REUSE differentFiles reused=" + $r4.PatchZipReused
Test-DdStage "$fx\restage-v3-reuse" | Out-Null; "restage-v3-reuse passes Test-DdStage"
# Safety rules: a client exe in the patch tree is refused.
Copy-Item "$fx\src-2099.01.01-0001" "$fx\src-evil" -Recurse; Txt "$fx\src-evil\patch\client-files\AnarchyOnline.exe" 'x'
try { New-DdStage "$fx\src-evil\launcher" "$fx\src-evil\patch" '2099.01.01-0009' "$fx\src-evil\patch\client-patch\version.dll" "$fx\srv-evil" | Out-Null; 'SAFETY FAIL: client exe accepted' } catch { 'SAFETY refused: ' + $_.Exception.Message }