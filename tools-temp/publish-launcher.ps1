# Build half + publish half in one go (kept for muscle memory). Randy approves releases: prefer staging only.
$ErrorActionPreference = 'Stop'
$rel = Get-Content "I:\AO\launcher\dist\release.json" -Raw | ConvertFrom-Json
& "$PSScriptRoot\stage-launcher.ps1"
& "$PSScriptRoot\publish-staged.ps1" -Stage "I:\AO\cache\temp\staged-$($rel.version)"