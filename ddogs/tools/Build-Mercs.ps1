# Build-Mercs.ps1 - Dungeon Dogs mercenary overhaul (2026-10-16). Chained from Build-NpcTemplates.ps1; safe to run alone.
# Writes, under -GameData:
#   NpcTemplates.json  MR** templates for all 14 professions (13 level bands 1-220, level curve from the original MRSO bands).
#                      Every look is a MOBILE human body (MonsterData Features has CanMove 0x4). The old Soldier/Enforcer look,
#                      ICC Shuttle Guard 254118, is an immobile body: the server never pathed them, which was the follow bug.
#   Mercs.json         heals flag, heal nanos, and armor per level: Jobe Suit (1+), First Tier (100+), Second Tier (160+),
#                      Faithful / Chosen (210+, Chosen for Omni owners). Item ids by name from I:\AO\tools\migrate\SqlTables\itemnames.sql.
#   PlayfieldContent   the 4 recruiters at each hub (4582, 6553, 4310) become one "Contract Terminal" terminal; the Arete strike recruiter too.
#   Knubot             MercContracts.json replaces MercRecruiter/MercDoctor/MercEnforcer/MercSoldier/MercKeeper.json.
# GUESSES: HP/damage multipliers, body per profession, tier level bands, Adventurer/MP using Doctor heal nanos.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'), [string]$ItemNames = 'I:\AO\tools\migrate\SqlTables\itemnames.sql')
$ErrorActionPreference = 'Stop'
$GameData = [IO.Path]::GetFullPath($GameData)
$utf8 = New-Object Text.UTF8Encoding $false
function Save($path, $obj) { [IO.File]::WriteAllText($path, ($obj | ConvertTo-Json -Depth 12) + "`n", $utf8) }

# Professions: key, hash, title, profession id, look, hp, dmg, heals. Keys/hashes must match Core/Pets/Mercs.cs.
$looks = @{
  SolM1 = @{ Md = 26092; Head = 40694; Breed = 1; Gender = 2 }; SolM2 = @{ Md = 26084; Head = 40689; Breed = 1; Gender = 2 }
  SolM3 = @{ Md = 26088; Head = 40687; Breed = 1; Gender = 2 }; SolM4 = @{ Md = 204985; Head = 40700; Breed = 1; Gender = 2 }
  SolF1 = @{ Md = 26076; Head = 40635; Breed = 1; Gender = 3 }; SolF2 = @{ Md = 26082; Head = 40634; Breed = 1; Gender = 3 }
  Atrox = @{ Md = 26097; Head = 40111; Breed = 4; Gender = 1 }
}
$profs = @(
  @('enforcer','MREN','Enforcer','Enforcer',9,'Atrox',1.6,0.9,$false),
  @('soldier','MRSO','Soldier','Soldier',1,'SolM1',1.0,1.3,$false),
  @('keeper','MRKE','Keeper','Keeper',14,'SolM2',1.3,1.0,$false),
  @('doctor','MRDR','Doctor','Doctor',10,'SolM4',0.8,0.6,$true),
  @('adventurer','MRAD','Adventurer','Adventurer',6,'SolF1',1.0,1.0,$true),
  @('agent','MRAG','Agent','Agent',5,'SolF2',0.9,1.3,$false),
  @('bureaucrat','MRBU','Bureaucrat','Bureaucrat',8,'SolM3',1.0,0.9,$false),
  @('engineer','MREG','Engineer','Engineer',3,'SolM1',1.1,1.1,$false),
  @('fixer','MRFI','Fixer','Fixer',4,'SolM3',0.9,1.1,$false),
  @('martialartist','MRMA','Martial Artist','Martial Artist',2,'SolM2',1.2,1.2,$false),
  @('metaphysicist','MRMP','Metaphysicist','Metaphysicist',12,'SolF2',0.9,1.0,$true),
  @('nanotechnician','MRNT','Nano-Technician','Nano Technician',11,'SolF1',0.7,1.4,$false),
  @('shade','MRSH','Shade','Shade',15,'SolF2',0.9,1.4,$false),
  @('trader','MRTR','Trader','Trader',7,'SolM4',1.0,0.9,$false)
)

# 1. Templates
$ntPath = Join-Path $GameData 'NpcTemplates.json'
$nt = [IO.File]::ReadAllText($ntPath) | ConvertFrom-Json
$donor = @($nt.MRSO.Templates | Sort-Object Level)
if (-not $donor -or $donor.Count -lt 5) { throw 'MRSO donor bands missing' }
$donorDmg = 1.3; $donorHp = 1.0
if ($nt.MRSO.PSObject.Properties['DdogsMercBuild']) { $donorDmg = 1.3 }   # MRSO bands always carry Hp 1.0 (both builds)
foreach ($p in $profs) {
  $key, $hash, $title, $armorName, $profId, $lookKey, $hp, $dmg, $heals = $p
  $look = $looks[$lookKey]
  $bands = @()
  foreach ($d in $donor) {
    $b = $d | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $b.Name = "Mercenary $title"
    $lvl = [int]$b.Level
    $s = $b.Stats
    $baseHp = [double]$d.Stats.'1' / $donorHp
    $s.'1' = [int][math]::Max(30, $baseHp * $hp)
    if ($s.PSObject.Properties['27']) { $s.'27' = $s.'1' }
    $s.'285' = [int][math]::Max(3, $lvl * 1.5 * $dmg)
    $s.'286' = [int][math]::Max(1, $lvl * 0.5 * $dmg)
    $s.'60' = $profId; $s.'359' = $look.Md; $s.'64' = $look.Head; $s.'4' = $look.Breed; $s.'59' = $look.Gender
    $bands += $b
  }
  $nt | Add-Member -NotePropertyName $hash -NotePropertyValue ([pscustomobject]@{ Templates = $bands }) -Force
}
Save $ntPath $nt

# Mobile-body check against MonsterData.json
$md = [IO.File]::ReadAllText((Join-Path $GameData 'MonsterData.json')) | ConvertFrom-Json
foreach ($l in $looks.Values) { $r = $md | Where-Object { $_.monsterData -eq $l.Md } | Select-Object -First 1; if (-not $r -or ($r.features -band 4) -eq 0) { throw "merc look $($l.Md) is not a mobile body" } }

# 2. Mercs.json
$names = @{}
$rx = [regex]"\(\s*(\d+)\s*,\s*'((?:[^']|'')*)'"
foreach ($m in $rx.Matches([IO.File]::ReadAllText($ItemNames))) {
  $n = $m.Groups[2].Value.Replace("''", "'")
  if ($n -match '^(Jobe Suit|First Tier|Second Tier|Chosen|Faithful) ') { if (-not $names[$n]) { $names[$n] = @() }; $names[$n] += [int]$m.Groups[1].Value }
}
function Pieces($prefix) {
  $out = New-Object System.Collections.ArrayList
  foreach ($n in ($names.Keys | Where-Object { $_ -like "$prefix *" } | Sort-Object)) {
    if ($n -match 'Spirit|Robe|Reconstruction') { continue }
    $ids = @($names[$n] | Sort-Object)
    [void]$out.Add([int[]]@([int]$ids[0], [int]$ids[-1]))
  }
  return ,([object[]]$out.ToArray())
}
$jobe = Pieces 'Jobe Suit'
$doctorHeals = @(@{ MinLevel = 1; Nanos = @(26465) }, @{ MinLevel = 25; Nanos = @(26466) }, @{ MinLevel = 75; Nanos = @(28665) }, @{ MinLevel = 150; Nanos = @(28676) })
$catalog = [ordered]@{ Enabled = $true; ReviveSeconds = 10; ArmorStats = $true; Professions = [ordered]@{} }
$report = @()
foreach ($p in $profs) {
  $key, $hash, $title, $armorName = $p[0], $p[1], $p[2], $p[3]
  $t1 = Pieces "First Tier $armorName"; $t2 = Pieces "Second Tier $armorName"; $fa = Pieces "Faithful $armorName"; $ch = Pieces "Chosen $armorName"
  # Tier 1 has only 5 pieces: keep the Jobe helmet, shoulderpads and support system with it.
  $extraIds = @($names['Jobe Suit Helmet']) + @($names['Jobe Suit Shoulderpad']) + @($names['Jobe Suit Support System'])
  $list = New-Object System.Collections.ArrayList
  foreach ($x in $t1) { [void]$list.Add([int[]]@($x[0], $x[1])) }
  foreach ($x in $jobe) { if ($extraIds -contains $x[0]) { [void]$list.Add([int[]]@($x[0], $x[1])) } }
  $t1Full = [object[]]$list.ToArray()
  $armor = @(
    [ordered]@{ MinLevel = 1; Items = $jobe },
    [ordered]@{ MinLevel = 100; Items = $t1Full },
    [ordered]@{ MinLevel = 160; Items = $t2 },
    [ordered]@{ MinLevel = 210; Items = $fa; OmniItems = $ch }
  )
  $report += "$key T1=$($t1.Count) T2=$($t2.Count) Faithful=$($fa.Count) Chosen=$($ch.Count)"
  if ($t1.Count -lt 5 -or $t2.Count -lt 5 -or $fa.Count -lt 5 -or $ch.Count -lt 5) { throw "armor lookup short for $key : $($report[-1])" }
  $nanos = New-Object System.Collections.ArrayList
  if ($p[8]) { foreach ($h in $doctorHeals) { [void]$nanos.Add($h) } }
  $catalog.Professions[$key] = [ordered]@{ Heals = [bool]$p[8]; Nanos = [object[]]$nanos.ToArray(); Armor = $armor }
}
Save (Join-Path $GameData 'Mercs.json') $catalog

# 3. Contract terminals replace the recruiters
foreach ($pf in 4582, 6553, 4310) {
  $path = Join-Path $GameData "PlayfieldContent\$pf\Npcs.json"
  $doc = [IO.File]::ReadAllText($path) | ConvertFrom-Json
  $keep = @(); $anchor = $null
  foreach ($n in $doc.Npcs) {
    if ($n.ContentNpcIdentity -in 'ddogs-merc-doctor', 'ddogs-merc-terminal') { if (-not $anchor) { $anchor = $n }; continue }
    if ($n.ContentNpcIdentity -in 'ddogs-merc-enforcer', 'ddogs-merc-soldier', 'ddogs-merc-keeper') { continue }
    $keep += $n
  }
  $terminals = @()
  if ($anchor) { $anchor.Key = "ddogs:merc:$($pf):terminal"; $anchor.ContentNpcIdentity = 'ddogs-merc-terminal'; $terminals += $anchor }
  foreach ($n in $keep) { if ($n.ContentNpcIdentity -eq 'ddogs-merc-strike') { $terminals += $n } }
  foreach ($n in $terminals) {
    $n.Name = 'Contract Terminal'
    $n.Provenance = 'ddogs merc contract terminal (Build-Mercs.ps1); body 279184 = Shipping Manifest Terminal look'
    foreach ($k in '4','59','64','455') { $n.Stats.$k = 0 }; $n.Stats.'4' = 6; $n.Stats.'59' = 1; $n.Stats.'359' = 279184; $n.Stats.'360' = 100
    $n.Textures = @(0..4 | ForEach-Object { [ordered]@{ Place = $_; Id = 0 } }); $n.Meshes = @()
  }
  $doc.Npcs = @($keep) + @($terminals | Where-Object { $_.ContentNpcIdentity -eq 'ddogs-merc-terminal' })
  Save $path $doc
  $report += "pf $pf terminals: $(@($terminals).Count)"
}

# 4. Knubot
$kb = Join-Path $GameData 'Knubot'
foreach ($old in 'MercRecruiter','MercDoctor','MercEnforcer','MercSoldier','MercKeeper') { $f = Join-Path $kb "$old.json"; if (Test-Path $f) { Remove-Item $f } }
$replies = [ordered]@{ hire = @{ Text = 'Show me the contracts.'; Goto = 'hire' }; end = @{ Text = 'End a contract.'; Goto = 'end' }; how = @{ Text = 'How do contracts work?'; Goto = 'how' }; back = @{ Text = 'Back.'; Goto = 'hello' } }
$lines = [ordered]@{
  hello = [ordered]@{ Text = 'MERCENARY CONTRACTS. All fourteen professions available. Contracts are free for now. One of each profession per client.'; Replies = @('hire','end','how') }
  busy = [ordered]@{ Text = 'Contract could not be filed right now. Try again in a moment.'; Replies = @('back') }
  how = [ordered]@{ Text = 'Each contract puts one mercenary of that profession at your side, at your level, in armor for your level (Jobe, then Tier 1, 2 and 3). Use the pet window or /pet to give orders. A fallen mercenary is back beside you in 10 seconds, and contracts stay signed when you log off. Healers (Doctor, Adventurer, Metaphysicist) look after you and your other mercenaries. Type .merc in chat to see your contracts.'; Replies = @('hire','end') }
  endall = [ordered]@{ Text = '{dismissmercs}All contracts ended.'; Replies = @('back') }
}
$hireList = @(); $endList = @()
foreach ($p in $profs) {
  $key, $title = $p[0], $p[2]
  $art = $(if ($title -match '^[AEIOU]') { 'an' } else { 'a' }); $replies["h_$key"] = @{ Text = "Hire $art $title."; Goto = "h_$key" }
  $replies["e_$key"] = @{ Text = "End my $title contract."; Goto = "e_$key" }
  $lines["h_$key"] = [ordered]@{ Text = "{hiremerc $key}Contract signed. Your $title is on the way."; Fail = 'busy'; Replies = @('hire','back') }
  $lines["e_$key"] = [ordered]@{ Text = "{dismissmerc $key}Your $title contract has ended."; Replies = @('end','back') }
  $hireList += "h_$key"; $endList += "e_$key"
}
$replies['endall'] = @{ Text = 'End every contract.'; Goto = 'endall' }
$lines['hire'] = [ordered]@{ Text = 'Available contracts:'; Replies = @($hireList + 'back') }
$lines['end'] = [ordered]@{ Text = 'Which contract do you want to end?'; Replies = @($endList + 'endall','back') }
Save (Join-Path $kb 'MercContracts.json') ([ordered]@{ Notes = 'Mercenary Contracts terminal (Build-Mercs.ps1). Hires any of the 14 professions; contracts are saved per character.'; Npcs = @('ddogs-merc-terminal','ddogs-merc-strike'); Openers = @(@{ Say = 'hello' }); Replies = $replies; Lines = $lines })

$report | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'Build-Mercs.last.txt')
"Build-Mercs: $($report -join '; ')"