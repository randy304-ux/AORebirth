# Build-TransitOfficers.ps1 - Dungeon Dogs travel officers at the three start hubs (Randy 2026-10-18).
# ICC Shuttleport 4582, Arete Landing 6553 (two officers) and Nascence Training Grounds 4313 each get an ICC Transit Officer
# (same look as the 4582 officer, cloned from its Npcs.json entry) that sends players to the other two hubs, any time.
# The 4582 officer keeps the one-time new-character start choice ({startarea}) while it is pending.
# Positions are [X, height, north]; Randy's /where "X north y height" maps to Position @(X, height, north).
# Facing: Rotation = (0, sin(yaw/2), 0, cos(yaw/2)), yaw = atan2(forward.X, forward.Z), so yaw 0 faces north (+Z), 90 faces +X
# (same as CharacterMotor / HashSpawnSystem).
# Arrival points ({teleport pf X height north}): ICC 934.9, 24.8, 762.0 (by Brandon Thorn); Arete 3455, 9.5, 865 (the camp,
# KnubotStartArea.AreteLanding); Nascence Training Grounds 412.8, 76.7, 353.5 (2 m in front of the officer at Randy's /where spot).
# Randy 2026-10-18 20:13: Nascence officer moved from 4310 (851.5, 30.25, 1069.5) to 4313 at /where 413.3 351.5 y 76.7;
# second Arete officer on the upper deck by Rex Larsson at /where 3622.1 792.3 y 51.7 (deck height 51.745).
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$GameData = [IO.Path]::GetFullPath($GameData)
$u = New-Object Text.UTF8Encoding $false
function Rot([double]$yawDeg) { $h = $yawDeg * [Math]::PI / 360.0; return @(0.0, [Math]::Round([Math]::Sin($h), 5), 0.0, [Math]::Round([Math]::Cos($h), 5)) }
$hubs = [ordered]@{
  icc      = @{ Pf = 4582; Name = 'the ICC Shuttleport'; Short = 'ICC Shuttleport'; Land = '934.9 24.8 762.0'; Npc = 'ddogs-icc-transit' }
  arete    = @{ Pf = 6553; Name = 'Arete Landing'; Short = 'Arete Landing'; Land = '3455 9.5 865'; Npc = 'ddogs-arete-transit' }
  nascence = @{ Pf = 4313; Name = 'the Nascence Training Grounds'; Short = 'Nascence'; Land = '412.8 76.7 353.5'; Npc = 'ddogs-nascence-transit' }
}
# Officers outside 4582, cloned from the ICC officer. Rotation $null keeps the clone's (identity) rotation.
$officers = @(
  @{ Hub = 'arete'; Key = 'ddogs:transit:6553:officer'; Npc = 'ddogs-arete-transit'; Id = 2110836901; Pos = @(3450.0, 9.36, 862.0); Yaw = $null }
  # Upper deck, faces south along the deck toward Rex Larsson and the Cargo Box (yaw 175).
  @{ Hub = 'arete'; Key = 'ddogs:transit:6553:officer2'; Npc = 'ddogs-arete-transit-2'; Id = 2110836903; Pos = @(3622.1, 51.745, 792.3); Yaw = 175 }
  # Faces roughly north toward the middle of the 804 m Training Grounds map (402, 402): yaw -13.
  @{ Hub = 'nascence'; Key = 'ddogs:transit:4313:officer'; Npc = 'ddogs-nascence-transit'; Id = 2110836902; Pos = @(413.3, 76.7, 351.5); Yaw = -13 }
)
# Retired spots: removed from their playfield files.
$retired = @(@{ Pf = 4310; Key = 'ddogs:transit:4310:officer' })
$iccDoc = [IO.File]::ReadAllText((Join-Path $GameData 'PlayfieldContent\4582\Npcs.json'), $u) | ConvertFrom-Json
$src = @($iccDoc.Npcs) | Where-Object { $_.ContentNpcIdentity -eq 'ddogs-icc-transit' }
if (-not $src) { throw 'ICC Transit Officer not found in 4582 Npcs.json' }
function NpcFile($pf) {
  $p = Join-Path $GameData ("PlayfieldContent\{0}\Npcs.json" -f $pf)
  if (-not (Test-Path $p)) { New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null; return @{ Path = $p; Doc = [pscustomobject][ordered]@{ SchemaVersion = 1; PlayfieldId = $pf; Npcs = @() } } }
  return @{ Path = $p; Doc = ([IO.File]::ReadAllText($p, $u) | ConvertFrom-Json) }
}
foreach ($r in $retired) {
  $f = NpcFile $r.Pf
  if (@($f.Doc.Npcs | Where-Object { $_.Key -eq $r.Key }).Count) { $f.Doc.Npcs = @(@($f.Doc.Npcs) | Where-Object { $_.Key -ne $r.Key }); [IO.File]::WriteAllText($f.Path, ($f.Doc | ConvertTo-Json -Depth 12) + "`n", $u); "removed $($r.Key)" }
}
foreach ($o in $officers) {
  $pf = $hubs[$o.Hub].Pf; $f = NpcFile $pf
  $clone = $src | ConvertTo-Json -Depth 12 | ConvertFrom-Json
  $clone.Key = $o.Key; $clone.ContentNpcIdentity = $o.Npc; $clone.PlayfieldId = $pf; $clone.InstanceId = $o.Id
  $clone.Position = $o.Pos
  if ($null -ne $o.Yaw) { $clone.Rotation = Rot $o.Yaw }
  $clone.Provenance = 'ddogs ICC Transit Officer, travel between the start hubs; look cloned from the 4582 officer. Build-TransitOfficers.ps1'
  $f.Doc.Npcs = @(@($f.Doc.Npcs) | Where-Object { $_.Key -ne $o.Key }) + @($clone)
  [IO.File]::WriteAllText($f.Path, ($f.Doc | ConvertTo-Json -Depth 12) + "`n", $u)
  "placed $($o.Key) at $($o.Pos -join ', ')"
}
function Travel($from) {
  $replies = [ordered]@{}; $lines = [ordered]@{}; $order = @()
  foreach ($k in $hubs.Keys) {
    if ($k -eq $from) { continue }
    $h = $hubs[$k]
    $replies["to_$k"] = [ordered]@{ Text = "Send me to $($h.Short)."; Goto = "go_$k" }
    $lines["go_$k"] = [ordered]@{ Text = @("$($h.Short) it is.", "{delay 2}Hold still, the transfer beacon will pull you over now.{teleport $($h.Pf) $($h.Land)}{close}") }
    $order += "to_$k"
  }
  $replies['stay'] = [ordered]@{ Text = 'Not now.'; Goto = 'bye' }
  $lines['bye'] = [ordered]@{ Text = 'Come back whenever you need a transfer.{close}' }
  return @{ Replies = $replies; Lines = $lines; Order = $order + 'stay' }
}
$kb = Join-Path $GameData 'Knubot'
foreach ($k in $hubs.Keys) {
  $h = $hubs[$k]; $t = Travel $k
  $others = @($hubs.Keys | Where-Object { $_ -ne $k } | ForEach-Object { $hubs[$_].Name })
  $t.Lines['travel'] = [ordered]@{ Text = "{delay 1}Need a transfer? I can send you to $($others[0]) or to $($others[1]).";  Replies = $t.Order }
  $openers = @([ordered]@{ Say = 'travel' })
  if ($k -eq 'icc') {
    # One-time start choice for new Rubi-Ka characters, kept from the original TransitOfficer.json.
    $t.Replies.Insert(0, 'go_arete', [ordered]@{ Text = 'Take me to Arete Landing.'; If = @('StartAreaPending'); Goto = 'arete' })
    $t.Replies.Insert(1, 'go_nascence', [ordered]@{ Text = 'Take me to Nascence.'; If = @('StartAreaPending'); Goto = 'nascence_start' })
    $t.Replies.Insert(2, 'stay_icc', [ordered]@{ Text = "I'll stay here at the ICC Shuttleport."; If = @('StartAreaPending'); Goto = 'icc' })
    $t.Replies['what_arete'] = [ordered]@{ Text = 'What is Arete Landing?'; If = @('StartAreaPending'); Goto = 'about' }
    $t.Lines['offer'] = [ordered]@{ Text = @('{delay 1}Welcome to the ICC Shuttleport. I handle transfers for new arrivals.', '{delay 1}You can start your training right here, or I can send you to the colony camp on Arete Landing, or to Nascence in the Shadowlands. Any transit officer can move you between the three later.'); Replies = @('go_arete', 'go_nascence', 'stay_icc', 'what_arete') }
    $t.Lines['about'] = [ordered]@{ Text = 'Arete is the newer colony landing. The camp there has its own vendors and work for newcomers. The Shuttleport has Brandon Thorn and the classic island training.'; Replies = @('go_arete', 'go_nascence', 'stay_icc') }
    $t.Lines['arete'] = [ordered]@{ Text = @('Arete Landing it is. Good luck out there!', '{delay 2}Hold still, the transfer beacon will pull you over now.{startarea arete}') }
    $t.Lines['nascence_start'] = [ordered]@{ Text = @('Nascence it is. Good luck out there!', "{delay 2}Hold still, the transfer beacon will pull you over now.{startarea icc}{teleport $($hubs.nascence.Pf) $($hubs.nascence.Land)}{close}") }
    $t.Lines['icc'] = [ordered]@{ Text = '{startarea icc}Very well. Brandon Thorn right here will get you started. Welcome aboard!{close}' }
    $openers = @([ordered]@{ If = @('StartAreaPending'); Say = 'offer' }) + $openers
    $file = 'TransitOfficer.json'; $notes = 'Dungeon Dogs ICC Transit Officer next to Brandon Thorn (PlayfieldContent 4582 Npcs.json). One-time start-area choice for new Rubi-Ka characters while pending ({startarea arete} moves to Arete Landing, {startarea icc} stays or goes on to Nascence), then travel to Arete Landing or Nascence any time. Generated by ddogs/tools/Build-TransitOfficers.ps1.'
  } else {
    $file = ('TransitOfficer{0}.json' -f (Get-Culture).TextInfo.ToTitleCase($k))
    $notes = "Dungeon Dogs ICC Transit Officer at $($h.Name) (PF $($h.Pf)); travel to the other two start hubs. Generated by ddogs/tools/Build-TransitOfficers.ps1."
  }
  $ids = @($h.Npc) + @($officers | Where-Object { $_.Hub -eq $k -and $_.Npc -ne $h.Npc } | ForEach-Object { $_.Npc })
  $doc = [ordered]@{ Notes = $notes; Npcs = $ids; Openers = $openers; Replies = $t.Replies; Lines = $t.Lines }
  [IO.File]::WriteAllText((Join-Path $kb $file), ($doc | ConvertTo-Json -Depth 10) + "`n", $u)
  "wrote $file"
}
