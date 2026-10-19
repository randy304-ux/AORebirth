# Build-MarcoContainers.ps1 - Marco Spida (Arete, quest AMPR) hands each profession its own Nanoprogram Container (Randy 2026-10-19 14:52:
# an Enforcer got the Soldier can: give_nano always spawned QNPC = Soldier 248265). Also a 'wrong container' reply that takes back a
# Soldier can (if carried) and gives the right one. Chained from Build-NpcTemplates.ps1.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = New-Object Text.UTF8Encoding $false
$gd = [IO.Path]::GetFullPath($GameData)
# profession id -> (hash, container id, name)
$rows = @(
  @(1,'QNSO',248265,'Soldier'), @(2,'QNMA',248262,'Martial Artist'), @(3,'QNEN',248260,'Engineer'), @(4,'QNFI',248261,'Fixer'),
  @(5,'QNAG',248256,'Agent'), @(6,'QNAD',248255,'Adventurer'), @(7,'QNTR',248266,'Trader'), @(8,'QNBU',248257,'Bureaucrat'),
  @(9,'QNEF',248259,'Enforcer'), @(10,'QNDO',248258,'Doctor'), @(11,'QNNT',248264,'Nano-Technician'), @(12,'QNMP',248263,'Meta-Physicist'),
  @(14,'QNKE',300892,'Keeper'), @(15,'QNSH',300893,'Shade'))
$it = [ordered]@{}
foreach ($r in $rows) { $it[$r[1]] = [ordered]@{ Templates = @($r[2], $r[2]) } }
[IO.File]::WriteAllText((Join-Path $gd 'ItemTemplates.MarcoContainers.json'), ($it | ConvertTo-Json -Depth 5) + "`n", $u)
$p = Join-Path $gd 'Knubot\MarcoSpida.json'
$m = [IO.File]::ReadAllText($p) | ConvertFrom-Json
$base = $m.Replies.take_nano
$hello = @($m.Lines.hello.Replies | Where-Object { $_ -notlike 'swap_*' })
$offer = @($m.Lines.offer_nano.Replies | Where-Object { $_ -ne 'take_nano' -and $_ -notlike 'take_nano_*' })
$takes = @(); $swaps = @()
foreach ($r in $rows) {
  $id = $r[0]; $h = $r[1]; $n = $r[3]
  $m.Replies | Add-Member -Force NoteProperty "take_nano_$id" ([pscustomobject]@{ Text = $base.Text; If = @($base.If) + @("prof $id"); Goto = "give_nano_$id" })
  $m.Lines | Add-Member -Force NoteProperty "give_nano_$id" ([pscustomobject]@{ Text = @("{spawn $h 1}", '{givequest AMPR}', "{delay 1}The $n Nanoprogram Container. Good luck. Come back to me when it is done.") })
  $takes += "take_nano_$id"
  if ($id -ne 1) {
    $m.Replies | Add-Member -Force NoteProperty "swap_$id" ([pscustomobject]@{ Text = 'You gave me the wrong nano container.'; If = @("prof $id", 'has QNSO', "!has $h"); Goto = "swapped_$id" })
    $m.Lines | Add-Member -Force NoteProperty "swapped_$id" ([pscustomobject]@{ Text = @('{take QNSO}', "{spawn $h 1}", "My mistake. That was the Soldier can. Here is the $n Nanoprogram Container."); Replies = @('nothing') })
    $swaps += "swap_$id"
  }
}
$m.Replies.PSObject.Properties.Remove('take_nano'); $m.Lines.PSObject.Properties.Remove('give_nano')
$m.Lines.offer_nano.Replies = @($takes) + $offer
$m.Lines.hello.Replies = @($swaps) + $hello
$m.Lines.busy_nano.Replies = @($swaps) + @($m.Lines.busy_nano.Replies | Where-Object { $_ -notlike 'swap_*' })
$m.Lines.nothing.Text = 'Open your profession''s nanoprogram container. If I handed you the wrong one, say so.{close}'
[IO.File]::WriteAllText($p, ($m | ConvertTo-Json -Depth 12) + "`n", $u)
'Build-MarcoContainers: 14 profession containers, swap replies'