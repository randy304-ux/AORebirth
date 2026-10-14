# ddogs/tools/Apply-YuttosStock.ps1 - fills the Yuttos vendor stock in PlayfieldContent\4311\Npcs.json from
# yuttos-stock-ids.txt (hash|name|lowId|highId|ql, transcribed from Randy's PRK shop screenshots 2026-10-14,
# ids matched from the client item DB) and moves Yuttos Machines into the Pharmaceuticals/Manuals tent (Randy 2026-10-14).
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false)
$ppath = Join-Path ([IO.Path]::GetFullPath($GameData)) 'PlayfieldContent\4311\Npcs.json'
$pf = [IO.File]::ReadAllText($ppath, $u) | ConvertFrom-Json
$rows = Get-Content (Join-Path $PSScriptRoot 'yuttos-stock-ids.txt') | ForEach-Object { $h,$n,$lo,$hi,$q = $_.Split('|'); [pscustomobject]@{ H=$h; Lo=[int]$lo; Hi=[int]$hi; Q=[int]$q } }
$byKey = @{}; foreach ($n in $pf.Npcs) { $byKey[$n.Key] = $n }
$counts = @{}
foreach ($g in ($rows | Group-Object H)) {
  $npc = $byKey["ddogs:yuttos:$($g.Name)"]; if (-not $npc) { throw "no vendor $($g.Name)" }
  $slot = 0
  $npc.Vendor.Stock = @($g.Group | ForEach-Object { [ordered]@{ Slot = $slot++; LowId = $_.Lo; HighId = $_.Hi; Quality = $_.Q } })
  $counts[$g.Name] = $slot
}
# Yuttos Machines: beside Yuttos Pharmaceuticals, 4 m along the line away from Manuals, same facing. Floor Y from Pharmaceuticals. Guessed.
$ph = $byKey['ddogs:yuttos:YUPH']; $ma = $byKey['ddogs:yuttos:YTMA']; $me = $byKey['ddogs:yuttos:YUME']
$dx = $ma.Position[0] - $ph.Position[0]; $dz = $ma.Position[2] - $ph.Position[2]; $len = [math]::Sqrt($dx*$dx + $dz*$dz)
$me.Position = @([math]::Round($ph.Position[0] - 4*$dx/$len, 3), $ph.Position[1], [math]::Round($ph.Position[2] - 4*$dz/$len, 3))
$me.Rotation = @($ph.Rotation)
[IO.File]::WriteAllText($ppath, ($pf | ConvertTo-Json -Depth 10), $u)
($counts.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ' '
"YUME pos=$($me.Position -join ',')"
