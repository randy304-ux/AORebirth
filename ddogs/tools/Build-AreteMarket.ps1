# ddogs/tools/Build-AreteMarket.ps1 - Arete market/street NPC names and the Credit Card side quest (AO-Universe Arete #19). Called by Build-AreteReetsFactions.ps1.
# Real: names from Randy's live screenshots; Credit Card at 3448,889 in the Underground (AO-Universe, wiki), steal = 15000 credits,
#   return to Leonora Marty = up to 2080 xp + Vacuum Packed Omni-Med Suit (297054), dialog lines from the guide.
# Guessed: pink-code mapping FACS=Logistics Manager Fausto, MJUY=Robotic Guard Dog (the strongbox room's security dog), LEMY=Leonora Marty,
#   FMXT=Furniture Merchant, FPBY=Food Provider, CGSA=Cyrus Cervenak, SPAU/SLPW=Tasty Chocolates!/Romantic Flowers! stall signs, BBOC=the Credit Card;
#   looks (human 26092, mech dog 17720, sign/card cargo-box look 253599); talk-to on the card instead of right-clicking the item. Shops have no stock (none sourced).
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
Remove-TypeData System.Array -ErrorAction SilentlyContinue
$u = [Text.UTF8Encoding]::new($false)
$gd = [IO.Path]::GetFullPath($GameData)
$npath = Join-Path $gd 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($npath, $u) | ConvertFrom-Json
function Clone($o) { $o | ConvertTo-Json -Depth 8 | ConvertFrom-Json }
function Npc($name, $lvl, $mesh, $scale, $feat, $src) {
  $t = Clone ($nt.ADAF.Templates | Select-Object -First 1); $t.Name = $name; $t.Level = $lvl; $t.Stats.'54' = $lvl; $t.Stats.'359' = $mesh; $t.Stats.'360' = $scale
  $t | Add-Member -Force NoteProperty Features @($feat); $t | Add-Member -Force NoteProperty LootTable @()
  $t | Add-Member -Force NoteProperty ContentProvenance ([pscustomobject]@{ Guessed = $true; Confirmed = $false; Donor = 'ADAF'; Source = $src }); [pscustomobject]@{ Templates = @($t) }
}
$new = [ordered]@{}
$new.FACS = Npc 'Logistics Manager Fausto' 20 26092 100 @('NoCombat','NoMove') 'Market, by the barrels (Randy). Pink FACS mapping and look guessed; Emiel delivery not built'
$new.MJUY = Npc 'Robotic Guard Dog' 13 17720 110 @('NoMove') 'Guards the strongbox room door (AO-Universe #5 security dog, Randy). Pink MJUY mapping guessed; attackable'
$new.LEMY = Npc 'Leonora Marty' 10 26092 100 @('NoCombat','NoMove') 'Street near the Weapons/Implants signs (Randy). Pink LEMY mapping guessed'
$new.FMXT = Npc 'Furniture Merchant' 20 26092 100 @('NoCombat','NoMove') 'Market (Randy). Pink FMXT mapping guessed; no stock'
$new.FPBY = Npc 'Food Provider' 10 26092 100 @('NoCombat','NoMove') 'Market food stall (Randy). Pink FPBY mapping guessed; no stock'
$new.CGSA = Npc 'Cyrus Cervenak' 10 26092 100 @('NoCombat','NoMove') 'Market food stall (Randy). Pink CGSA mapping guessed; no stock'
$new.SPAU = Npc 'Tasty Chocolates!' 1 253599 50 @('NoCombat','NoMove') 'Stall sign (Randy). Pink SPAU mapping and look guessed'
$new.SLPW = Npc 'Romantic Flowers!' 1 253599 50 @('NoCombat','NoMove') 'Stall sign (Randy). Pink SLPW mapping and look guessed'
$new.BBOC = Npc 'Bank of Rubi-Ka Credit Card' 1 253599 30 @('NoCombat','NoMove') 'Lost credit card behind the barrels, Underground 3448,889 (AO-Universe #19). Talk-to pickup and look guessed'
$nraw = [IO.File]::ReadAllText($npath, $u) -replace "`r`n", "`n"
$cut = $nraw.IndexOf(",`n    `"FACS`":")
if ($cut -ge 0) { $nraw = $nraw.Substring(0, $cut) + "`n}`n" }
$frag = ''; foreach ($k in $new.Keys) { $frag += ",`n    `"$k`": " + ($new.$k | ConvertTo-Json -Depth 8) }
$end = $nraw.TrimEnd().LastIndexOf('}'); $last = $nraw.LastIndexOf('}', $end - 1)
[IO.File]::WriteAllText($npath, $nraw.Substring(0, $last + 1) + $frag + "`n}`n", $u)
$qpath = Join-Path $gd 'Quests.json'
$q = @(); foreach ($x in (ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($qpath, $u)))) { if ($x.Hash -notin 'CCS1','CCR1') { $q += $x } }
$used = $q | ForEach-Object { $_.MissionBit } | Where-Object { $_ -ne $null }
foreach ($b in 708,709) { if ($used -contains $b) { throw "bit $b taken" } }
$q += [pscustomobject]@{ Hash='CCS1'; Name='Steal the credits'; QasInstance=920178; Scope='Solo'; Action='TargetNpc'; Summary='Drain the credits off the Bank of Rubi-Ka Credit Card'
  Objective=[pscustomobject]@{ Count=$null; Npc='BBOC'; SpecItem=$null; WorldItem=$null }; Flags='None'; MissionBit=708; NextStep=$null; Reward=[pscustomobject]@{ Hash='RCCS'; Credits=15000; Xp=0; Records=$null } }
$q += [pscustomobject]@{ Hash='CCR1'; Name='Find the owner of the credit card'; QasInstance=920179; Scope='Solo'; Action='TargetNpc'; Summary='Return the credit card to Leonora Marty in the market'
  Objective=[pscustomobject]@{ Count=$null; Npc='LEMY'; SpecItem=$null; WorldItem=$null }; Flags='NoCash'; MissionBit=709; NextStep=$null
  Reward=[pscustomobject]@{ Hash='RCCR'; Credits=0; Xp=2080; Records=@(,@([pscustomobject]@{ Id=297054; HighId=297054; Name='Vacuum Packed Omni-Med Suit'; Quality=10 })) } }
[IO.File]::WriteAllText($qpath, (ConvertTo-Json -InputObject @($q) -Depth 8) + "`n", $u)
$kd = Join-Path $gd 'Knubot'
$card = [ordered]@{ Notes = 'Lost Bank of Rubi-Ka Credit Card (AO-Universe #19). Talk-to stands in for picking up / right-clicking the card: guessed. Generated by ddogs/tools/Build-AreteMarket.ps1.'
  Npcs = @('BBOC'); Openers = @( [ordered]@{ If = @('HasCompletedQuest CCS1'); Say = 'empty' }, [ordered]@{ If = @('HasCompletedQuest CCR1'); Say = 'empty' }, [ordered]@{ If = @('HasQuest CCS1'); Say = 'held' }, [ordered]@{ Say = 'found' } )
  Replies = [ordered]@{ take = [ordered]@{ Text = 'Pick up the card.'; Goto = 'take' }; steal = [ordered]@{ Text = 'Transfer the credits to yourself. Yes.'; Goto = 'steal' }; keep = [ordered]@{ Text = 'Leave it be for now.'; Goto = 'shut' } }
  Lines = [ordered]@{ found = [ordered]@{ Text = 'Behind the barrels lies a Bank of Rubi-Ka Credit Card. Someone must have lost it.'; Replies = @('take','keep') }
    take = [ordered]@{ Text = @('{givequest CCS1}', '{givequest CCR1}', 'The card belongs to a Leonora Marty. You could find her in the market... or empty it.'); Replies = @('steal','keep') }
    held = [ordered]@{ Text = 'The card still shows 15000 credits.'; Replies = @('steal','keep') }
    steal = [ordered]@{ Text = @('{completequest CCS1}', 'The transfer goes through. 15000 credits richer.{close}') }
    empty = [ordered]@{ Text = 'Nothing left here.{close}' }; shut = [ordered]@{ Text = '{close}' } } }
$leo = [ordered]@{ Notes = 'Leonora Marty (AO-Universe #19); dialog from the guide. Generated by ddogs/tools/Build-AreteMarket.ps1.'
  Npcs = @('LEMY'); Openers = @( [ordered]@{ Say = 'hello' } )
  Replies = [ordered]@{ card = [ordered]@{ Text = 'I am here to make your day brighter. I have found your credit card!'; If = @('HasQuest CCR1','!HasCompletedQuest CCS1'); Goto = 'thanks' }; bye = [ordered]@{ Text = 'Goodbye.'; Goto = 'shut' } }
  Lines = [ordered]@{ hello = [ordered]@{ Text = 'How could I be so stupid... Oh hello there, I''m having a really bad day... What do you want?'; Replies = @('card','bye') }
    thanks = [ordered]@{ Text = @('{completequest CCR1}', 'Really? Thank you so much. I really hope that the vendor still have the t-shirt I wanted! Finally I can go shopping again!{close}') }
    shut = [ordered]@{ Text = '{close}' } } }
[IO.File]::WriteAllText((Join-Path $kd 'CreditCard.json'), ($card | ConvertTo-Json -Depth 8) + "`n", $u)
[IO.File]::WriteAllText((Join-Path $kd 'LeonoraMarty.json'), ($leo | ConvertTo-Json -Depth 8) + "`n", $u)
foreach ($p in @($npath, $qpath)) { [IO.File]::WriteAllText($p, ([IO.File]::ReadAllText($p, $u) -replace "`r`n", "`n"), $u) }
# dead thief (Sarah Greene #7) lies on the ground next to the Mutated Garbage Flea: mesh 296008 lying body, movement mode 11 (guessed)
$cp = Join-Path $gd 'PlayfieldContent\6553\Npcs.json'; $cj = [IO.File]::ReadAllText($cp, $u) | ConvertFrom-Json
foreach ($n in $cj.Npcs) { if ($n.Key -eq 'ddogs:arete:dead-thief') { $n.Stats.'359' = 296008; $n.Stats | Add-Member -Force NoteProperty '173' 11; $n.Stats | Add-Member -Force NoteProperty '174' 11; $n.Meshes = @(); $n.Textures = @() } }
[IO.File]::WriteAllText($cp, (($cj | ConvertTo-Json -Depth 8) -replace "`r`n", "`n") + "`n", $u)
'market written'