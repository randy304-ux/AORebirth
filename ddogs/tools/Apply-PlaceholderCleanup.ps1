# Applies the placeholder cleanup: new templates for confident matches, SpawnSkips.json (hidden codes, removed points),
# SpawnHash on content NPCs that stand on an official hash point, and removal of duplicate content NPCs.
param([string]$Gd = "$PSScriptRoot\..\..\AORebirth\GameData", [string]$Docs = "$PSScriptRoot\..\docs")
$ErrorActionPreference = 'Stop'
$MySql = 'I:\AO\mysql\server\bin\mysql.exe'; $Cnf = 'I:\AO\secrets\root-client.cnf'
$rows = Import-Csv "$Docs\placeholders.csv"; $ov = Import-Csv "$Docs\placeholder-overlaps.csv"
$tplText = [IO.File]::ReadAllText("$Gd\NpcTemplates.json"); $tpl = $tplText | ConvertFrom-Json
$skel = $tpl.AAAA.Templates[0].Stats
# 1. Confident matches: old CellAO mobtemplate rows and recorded delmus templates with the same official hash.
$codes = @{}; $rows | % { $codes[$_.code] = 1 }
$new = [ordered]@{}; $prov = @{}
foreach ($r in (& $MySql "--defaults-extra-file=$Cnf" -N -B cellao_codex_clean -e "select Hash,Name,MinLvl,MaxLvl,Side,Fatness,Breed,Sex,Race,Flags,NPCFamily,Health,MonsterData,MonsterScale from mobtemplate")) {
  $a = $r -split "`t"; $h = $a[0]
  if (-not $codes[$h] -or $tpl.PSObject.Properties[$h] -or $h -eq 'BRBL') { continue }
  $bands = foreach ($lvl in (@([int]$a[2], [int]$a[3]) | sort -Unique)) {
    $s = [ordered]@{}; foreach ($p in $skel.PSObject.Properties) { $s[$p.Name] = $p.Value }
    $o = @{ '0'=[int]$a[9]; '1'=[int]$a[11]; '27'=[int]$a[11]; '33'=[int]$a[4]; '47'=[int]$a[5]; '4'=[int]$a[6]; '59'=[int]$a[7]; '89'=[int]$a[8]; '455'=[int]$a[10]; '359'=[int]$a[12]; '360'=[int]$a[13]; '54'=$lvl }
    foreach ($k in $o.Keys) { $s[$k] = $o[$k] }
    [ordered]@{ Name = $a[1]; Level = $lvl; Stats = $s }
  }
  $new[$h] = [ordered]@{ Templates = @($bands); ContentProvenance = [ordered]@{ Guessed = $false; Source = "DB mobtemplate row $h (old CellAO catalog): name, levels, health, model, family, side." } }
}
foreach ($d in (Get-Content "$PSScriptRoot\..\..\docs\accepted\npc\delmus\NpcTemplate.json" -Raw | ConvertFrom-Json)) {
  if (-not $codes[$d.Hash] -or $tpl.PSObject.Properties[$d.Hash] -or $new.Contains($d.Hash)) { continue }
  $lvl = if ($d.Stats.'54') { [int]$d.Stats.'54' } else { 1 }
  $b = [ordered]@{ Name = $d.Name; Level = $lvl; Stats = $d.Stats; HasHeadMesh = [bool]$d.HasHeadMesh; Equipment = $d.Equipment }
  if (-not $d.Attackable) { $b.Features = @('NoCombat') }
  $new[$d.Hash] = [ordered]@{ Templates = @($b); ContentProvenance = [ordered]@{ Guessed = $false; Source = 'docs/accepted/npc/delmus/NpcTemplate.json recorded template.' } }
}
if ($new.Count) {
  $json = ($new.GetEnumerator() | % { '    "' + $_.Key + '": ' + ($_.Value | ConvertTo-Json -Depth 8 -Compress) }) -join ",`r`n"
  $end = $tplText.LastIndexOf('}'); $tplText = $tplText.Substring(0, $end).TrimEnd() + ",`r`n" + $json + "`r`n}`r`n"
  [IO.File]::WriteAllText("$Gd\NpcTemplates.json", $tplText, (New-Object Text.UTF8Encoding $false))
}
# 2. Quest / Knubot references must keep their binding.
$ref = @{}; Select-String "$Gd\Quests.json" -Pattern '"[A-Za-z]+"\s*:\s*"([A-Z0-9]{4})"' -AllMatches | % { $_.Matches } | % { $ref[$_.Groups[1].Value] = 1 }
Get-ChildItem "$Gd\Knubot\*.json" | % { (Get-Content $_.FullName -Raw | ConvertFrom-Json).Npcs } | % { $ref[$_] = 1 }
# 3. Placeholders standing on a real NPC: remove the point; a content NPC takes over the hash binding.
$removed = @(); $contentHash = @{}
foreach ($o in $ov | ? kind -eq 'placeholder-on-npc') {
  $removed += [ordered]@{ playfield = [int]$o.pf; hash = $o.hash; position = @([double]$o.x, [double]$o.y, [double]$o.z); reason = "on top of $($o.other) ($($o.otherSrc), $($o.dist) m)" }
  if ($o.otherSrc -eq 'content') { $contentHash["$($o.pf)|$($o.other)"] = $o.hash }
}
# 4. Content NPC duplicates of a templated hash NPC (Marco Spida): drop the content copy, the hash NPC owns the Knubot.
$dropContent = @{}; foreach ($o in $ov | ? { $_.kind -eq 'duplicate' -and $_.otherSrc -like '*content*' }) { $dropContent["$($o.pf)|$($o.other)"] = $o.hash }
$edited = @()
foreach ($f in Get-ChildItem "$Gd\PlayfieldContent\*\Npcs.json") {
  $c = Get-Content $f.FullName -Raw | ConvertFrom-Json; $changed = $false; $keep = @()
  foreach ($n in $c.Npcs) {
    $k = "$($n.PlayfieldId)|$($n.Name)"
    if ($dropContent[$k]) { $changed = $true; $edited += "removed content $k (dup of hash $($dropContent[$k]))"; continue }
    if ($contentHash[$k]) { $n | Add-Member -NotePropertyName SpawnHash -NotePropertyValue $contentHash[$k] -Force; $changed = $true; $edited += "SpawnHash $($contentHash[$k]) -> $k" }
    $keep += $n
  }
  if ($changed) { $c.Npcs = $keep; [IO.File]::WriteAllText($f.FullName, ($c | ConvertTo-Json -Depth 20), (New-Object Text.UTF8Encoding $false)) }
}
# 5. Hide every other unmatched code, except quest/Knubot references (those stay visible so the quest still works).
$covered = @{}; $removed | % { $covered["$($_.playfield)|$($_.hash)"] = 1 }
$hidden = $rows | ? { -not $new.Contains($_.code) -and -not $ref[$_.code] } | select -ExpandProperty code -Unique | sort | % { [ordered]@{ hash = $_; reason = 'unmatched' } }
$skips = [ordered]@{ note = 'Generated by ddogs/tools/Apply-PlaceholderCleanup.ps1. hiddenHashes never fall back to the pink placeholder; removedPoints drop one Spawns.json point; dedupeRadius drops a point whose hash already spawns within that many metres.'; dedupeRadius = 0.5; hiddenHashes = @($hidden); removedPoints = @($removed) }
[IO.File]::WriteAllText("$Gd\SpawnSkips.json", ($skips | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding $false))
# 6. Status column.
$hid = @{}; $hidden | % { $hid[$_.hash] = 1 }
$rows | % {
  $_.status = if ($new.Contains($_.code)) { 'templated' } elseif ($covered["$($_.playfield)|$($_.code)"]) { 'removed-overlap' } elseif ($ref[$_.code]) { 'placeholder-quest' } elseif ($hid[$_.code]) { 'hidden' } else { 'placeholder' }
  if ($new.Contains($_.code)) { $_.note = $new[$_.code].Templates[0].Name }
}
$rows | Export-Csv "$Docs\placeholders.csv" -NoTypeInformation
"templates added: $($new.Count) [$($new.Keys -join ',')]"; "hidden codes: $(@($hidden).Count)"; "removed points: $($removed.Count)"; $edited
$rows | group status | % { "$($_.Name): rows=$($_.Count) spawns=$(($_.Group | measure count -Sum).Sum)" }
