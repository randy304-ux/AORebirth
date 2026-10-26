# City controller NPCs and the Andromeda Tower Shop. Re-run safe: drops keys ddogs:city:* then writes them back.
param(
  [string]$Root = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData\PlayfieldContent')
)
$ErrorActionPreference = 'Stop'
$utf8 = New-Object Text.UTF8Encoding $false

function New-CityNpc([int]$pf, [string]$key, [string]$name, [string]$identity, [int]$id, [int]$x, [int]$y, [int]$z, [string]$note) {
  $stats = [ordered]@{
    '0' = 277352961; '1' = 1650; '4' = 6; '27' = 1650; '33' = 0; '47' = 1; '54' = 40
    '59' = 1; '60' = 0; '64' = 0; '89' = 1; '156' = 137; '173' = 3; '174' = 3
    '359' = 279184; '360' = 100; '368' = 0; '389' = 0; '455' = 0; '466' = 0; '660' = 0; '673' = 31
  }
  $textures = @()
  foreach ($place in 0..4) { $textures = $textures + @(,[ordered]@{ Place = $place; Id = 0 }) }
  return [ordered]@{
    Key = $key
    Provenance = $note
    ContentNpcIdentity = $identity
    PlayfieldId = $pf
    InstanceId = $id
    Name = $name
    Position = @($x, $y, $z)
    Rotation = @(0, 0, 0, 1)
    Stats = $stats
    Textures = $textures
    Meshes = @()
    Passive = $true
    Attackable = $false
    TickLiving = $true
    Presentation = [ordered]@{}
  }
}

$places = @(
  [pscustomobject]@{ Pf = 5001; Key = 'ddogs:city:5001:controller'; Name = 'City Controller'; Identity = 'ddogs-city-controller'; Id = 2110920001; X = 242; Y = 5; Z = 242; Note = 'Playa del Desierto controller. Map center, Y is a guess.' },
  [pscustomobject]@{ Pf = 5002; Key = 'ddogs:city:5002:controller'; Name = 'City Controller'; Identity = 'ddogs-city-controller'; Id = 2110920002; X = 502; Y = 5; Z = 502; Note = 'Montroyal controller. Map center, Y is a guess.' },
  [pscustomobject]@{ Pf = 6010; Key = 'ddogs:city:6010:controller'; Name = 'City Controller'; Identity = 'ddogs-city-controller'; Id = 2110920003; X = 1202; Y = 30; Z = 1202; Note = 'Serenity Islands controller. Map center, Y is a guess.' },
  [pscustomobject]@{ Pf = 800; Key = 'ddogs:city:800:controller'; Name = 'City Controller'; Identity = 'ddogs-city-controller'; Id = 2110920004; X = 650; Y = 5; Z = 640; Note = 'Borealis open plot controller. Invasion-pad guess.' },
  [pscustomobject]@{ Pf = 730; Key = 'ddogs:city:730:controller'; Name = 'City Controller'; Identity = 'ddogs-city-controller'; Id = 2110920005; X = 250; Y = 5; Z = 320; Note = 'Rome Red open plot controller. Invasion-pad guess.' },
  [pscustomobject]@{ Pf = 640; Key = 'ddogs:city:640:controller'; Name = 'City Controller'; Identity = 'ddogs-city-controller'; Id = 2110920006; X = 542; Y = 5; Z = 536; Note = 'Tir open plot controller. Invasion-pad guess.' }
)

foreach ($row in $places) {
  $dir = Join-Path $Root ([string]$row.Pf)
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
  $path = Join-Path $dir 'Npcs.json'
  if (Test-Path $path) {
    $doc = [IO.File]::ReadAllText($path) | ConvertFrom-Json
  } else {
    $doc = [pscustomobject]@{ SchemaVersion = 1; PlayfieldId = $row.Pf; Npcs = @(); StandaloneShops = @() }
  }
  $npc = New-CityNpc $row.Pf $row.Key $row.Name $row.Identity $row.Id $row.X $row.Y $row.Z $row.Note
  $keep = @($doc.Npcs | Where-Object { $_ -and $_.Key -notlike 'ddogs:city:*' })
  $doc.Npcs = @($keep) + @($npc)
  if (-not $doc.StandaloneShops) { $doc | Add-Member -NotePropertyName StandaloneShops -NotePropertyValue @() -Force }
  [IO.File]::WriteAllText($path, (($doc | ConvertTo-Json -Depth 8) + "`n"), $utf8)
}

$icc = Join-Path $Root '655\Npcs.json'
$shop = New-CityNpc 655 'ddogs:city:655:towershop' 'Tower Shop' 'ddogs-tower-shop' 2110920100 3060 35 868 'Premade org city buildings. West of the Ofab row. Y 35 is the buffbot height.'
$doc655 = [IO.File]::ReadAllText($icc) | ConvertFrom-Json
$keep655 = @($doc655.Npcs | Where-Object { $_.Key -notlike 'ddogs:city:*' })
$doc655.Npcs = @($keep655) + @($shop)
[IO.File]::WriteAllText($icc, (($doc655 | ConvertTo-Json -Depth 12) + "`n"), $utf8)
Write-Output 'org-city npcs written'
