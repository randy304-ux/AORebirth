# Build-TransitOfficers.ps1 - Dungeon Dogs travel officers at the three start hubs (Randy 2026-10-18).
# ICC Shuttleport 4582, Arete Landing 6553 and Nascence Frontier 4310 each get an ICC Transit Officer (same look as the
# 4582 officer, cloned from its Npcs.json entry) that sends players to the other two hubs, any time.
# The 4582 officer keeps the one-time new-character start choice ({startarea}) while it is pending.
# Arrival points: ICC 934.9, 24.8, 762.0 (by Brandon Thorn, as Vaughn Hammond's ride); Arete 3455, 9.5, 865 (KnubotStartArea.AreteLanding,
# the camp); Nascence 849.0, 30.4, 1073.0 (open ground beside the Contract Terminal / Emissary of Jobe; spawn heights there are 30.25).
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$GameData = [IO.Path]::GetFullPath($GameData)
$u = New-Object Text.UTF8Encoding $false
$hubs = [ordered]@{
  icc      = @{ Pf = 4582; Name = 'the ICC Shuttleport'; Short = 'ICC Shuttleport'; Land = '934.9 24.8 762.0'; Npc = 'ddogs-icc-transit' }
  arete    = @{ Pf = 6553; Name = 'Arete Landing'; Short = 'Arete Landing'; Land = '3455 9.5 865'; Npc = 'ddogs-arete-transit'; Id = 2110836901; Pos = @(3450.0, 9.36, 862.0) }
  nascence = @{ Pf = 4310; Name = 'Nascence'; Short = 'Nascence'; Land = '849.0 30.4 1073.0'; Npc = 'ddogs-nascence-transit'; Id = 2110836902; Pos = @(851.5, 30.25, 1069.5) }
}
# NPC entries for Arete and Nascence, cloned from the ICC officer.
$iccDoc = [IO.File]::ReadAllText((Join-Path $GameData 'PlayfieldContent\4582\Npcs.json'), $u) | ConvertFrom-Json
$src = @($iccDoc.Npcs) | Where-Object { $_.ContentNpcIdentity -eq 'ddogs-icc-transit' }
if (-not $src) { throw 'ICC Transit Officer not found in 4582 Npcs.json' }
foreach ($k in 'arete', 'nascence') {
  $h = $hubs[$k]
  $p = Join-Path $GameData ("PlayfieldContent\{0}\Npcs.json" -f $h.Pf)
  $doc = [IO.File]::ReadAllText($p, $u) | ConvertFrom-Json
  $key = 'ddogs:transit:{0}:officer' -f $h.Pf
  $clone = $src | ConvertTo-Json -Depth 12 | ConvertFrom-Json
  $clone.Key = $key; $clone.ContentNpcIdentity = $h.Npc; $clone.PlayfieldId = $h.Pf; $clone.InstanceId = $h.Id
  $clone.Position = $h.Pos
  $clone.Provenance = 'ddogs ICC Transit Officer, travel between the start hubs; look cloned from the 4582 officer. Build-TransitOfficers.ps1'
  $doc.Npcs = @(@($doc.Npcs) | Where-Object { $_.Key -ne $key }) + @($clone)
  [IO.File]::WriteAllText($p, ($doc | ConvertTo-Json -Depth 12) + "`n", $u)
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
    $t.Lines['nascence_start'] = [ordered]@{ Text = @('Nascence it is. Good luck out there!', "{delay 2}Hold still, the transfer beacon will pull you over now.{startarea icc}{teleport 4310 $($hubs.nascence.Land)}{close}") }
    $t.Lines['icc'] = [ordered]@{ Text = '{startarea icc}Very well. Brandon Thorn right here will get you started. Welcome aboard!{close}' }
    $openers = @([ordered]@{ If = @('StartAreaPending'); Say = 'offer' }) + $openers
    $file = 'TransitOfficer.json'; $notes = 'Dungeon Dogs ICC Transit Officer next to Brandon Thorn (PlayfieldContent 4582 Npcs.json). One-time start-area choice for new Rubi-Ka characters while pending ({startarea arete} moves to Arete Landing, {startarea icc} stays or goes on to Nascence), then travel to Arete Landing or Nascence any time. Generated by ddogs/tools/Build-TransitOfficers.ps1.'
  } else {
    $file = ('TransitOfficer{0}.json' -f (Get-Culture).TextInfo.ToTitleCase($k))
    $notes = "Dungeon Dogs ICC Transit Officer at $($h.Name) (PF $($h.Pf)); travel to the other two start hubs. Generated by ddogs/tools/Build-TransitOfficers.ps1."
  }
  $doc = [ordered]@{ Notes = $notes; Npcs = @($h.Npc); Openers = $openers; Replies = $t.Replies; Lines = $t.Lines }
  [IO.File]::WriteAllText((Join-Path $kb $file), ($doc | ConvertTo-Json -Depth 10) + "`n", $u)
  "wrote $file"
}