# Lists untemplated hash spawn codes (pink "To Be Determined" placeholders) and overlapping/duplicate NPC spawns.
param([string]$Gd = "$PSScriptRoot\..\..\AORebirth\GameData", [string]$OutDir = "$PSScriptRoot\..\docs")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Web.Extensions
$ser = New-Object System.Web.Script.Serialization.JavaScriptSerializer; $ser.MaxJsonLength = [int]::MaxValue
function J($p) { $ser.DeserializeObject([IO.File]::ReadAllText($p)) }
$tpl = J "$Gd\NpcTemplates.json"; $items = J "$Gd\ItemTemplates.json"
$skipFile = "$Gd\SpawnSkips.json"; $skips = if (Test-Path $skipFile) { J $skipFile } else { @{ hiddenHashes = @(); removedPoints = @() } }
$hidden = @{}; foreach ($h in $skips['hiddenHashes']) { $hidden[$h['hash']] = $h }
function Has($h) { $h -and $h -ne 'AAAA' -and $tpl.ContainsKey($h) }
function TName($h) { $t = $tpl[$h]; if ($t -and $t['Templates']) { $t['Templates'][0]['Name'] } elseif ($t -and $t['Children']) { 'family:' + ($t['Children'] -join '/') } }
$ph = New-Object System.Collections.ArrayList; $real = New-Object System.Collections.ArrayList
foreach ($dir in Get-ChildItem "$Gd\Playfields" -Directory) {
  $pf = [int]$dir.Name; $sp = Join-Path $dir.FullName 'Spawns.json'
  if (Test-Path $sp) {
    $i = -1
    foreach ($e in (J $sp)['spawns']) {
      $i++; $h = $e['hashText']; if (-not $h) { continue }
      $inactive = $false; $ev = $e['extensions']; if ($ev -and $ev['events']) { foreach ($x in $ev['events']) { if ($x['name'] -and -not $x['active']) { $inactive = $true } } }
      if ($inactive) { continue }
      $p = $e['position']; $o = [pscustomobject]@{ pf=$pf; idx=$i; hash=$h; min=$e['minLevel']; max=$e['maxLevel']; x=[double]$p[0]; y=[double]$p[1]; z=[double]$p[2]; src='hash'; name='' }
      if (Has $h) { $o.name = TName $h; [void]$real.Add($o) } elseif (-not $items.ContainsKey($h)) { [void]$ph.Add($o) }
    }
  }
  $ds = Join-Path $dir.FullName 'Districts.json'
  if (Test-Path $ds) {
    foreach ($d in (J $ds)['districts']) {
      $stack = New-Object System.Collections.Stack; foreach ($s in $d['spawnInfos']) { $stack.Push($s) }
      $seen = @{}
      while ($stack.Count) { $s = $stack.Pop(); $h = $s['hashText']; if ($s['spawnInfos']) { foreach ($c in $s['spawnInfos']) { $stack.Push($c) } }
        if (-not $h -or $seen[$h]) { continue }; $seen[$h] = 1
        if (-not (Has $h) -and -not $items.ContainsKey($h)) { $c = $d['centre']; [void]$ph.Add([pscustomobject]@{ pf=$pf; idx=-1; hash=$h; min=$d['npcMinLevel']; max=$d['npcMaxLevel']; x=[double]$c[0]; y=[double]$c[1]; z=[double]$c[2]; src='district:' + $d['name']; name='' }) } }
    }
  }
}
foreach ($f in Get-ChildItem "$Gd\PlayfieldContent\*\Npcs.json") {
  foreach ($n in (J $f.FullName)['Npcs']) { $p = $n['Position']; [void]$real.Add([pscustomobject]@{ pf=[int]$n['PlayfieldId']; idx=-1; hash=$n['Key']; min=0; max=0; x=[double]$p[0]; y=[double]$p[1]; z=[double]$p[2]; src='content'; name=$n['Name'] }) }
}
function D($a, $b) { [math]::Sqrt(($a.x-$b.x)*($a.x-$b.x) + ($a.y-$b.y)*($a.y-$b.y) + ($a.z-$b.z)*($a.z-$b.z)) }
$byPf = $real | Group-Object pf -AsHashTable
$ov = foreach ($p in $ph | ? { $_.src -eq 'hash' }) { foreach ($r in @($byPf[$p.pf])) { if ($r -and (D $p $r) -le 1.0) { [pscustomobject]@{ kind='placeholder-on-npc'; pf=$p.pf; hash=$p.hash; idx=$p.idx; x=$p.x; y=$p.y; z=$p.z; other=$r.name; otherSrc=$r.src; dist=[math]::Round((D $p $r),2) } } } }
$dup = foreach ($g in $byPf.Values) { $a = @($g); for ($i=0; $i -lt $a.Count; $i++) { for ($k=$i+1; $k -lt $a.Count; $k++) { $r=$a[$i]; $s=$a[$k]
  if ((D $r $s) -le 0.5 -and ($r.hash -eq $s.hash -or ($r.name -and $r.name -eq $s.name))) { [pscustomobject]@{ kind='duplicate'; pf=$r.pf; hash="$($r.hash)|$($s.hash)"; idx="$($r.idx)|$($s.idx)"; x=$s.x; y=$s.y; z=$s.z; other=$r.name; otherSrc="$($r.src)|$($s.src)"; dist=[math]::Round((D $r $s),2) } } } } }
$rows = $ph | Group-Object pf, hash, src | % { $f = $_.Group[0]; $hh = $hidden[$f.hash]
  [pscustomobject]@{ code=$f.hash; playfield=$f.pf; source=$f.src; level="$($f.min)-$($f.max)"; x=[math]::Round($f.x,1); y=[math]::Round($f.y,1); z=[math]::Round($f.z,1); count=$_.Count; status=$(if ($hh) { 'hidden' } else { 'placeholder' }); note=$(if ($hh) { $hh['reason'] } else { '' }) } }
$rows | sort playfield, code | Export-Csv "$OutDir\placeholders.csv" -NoTypeInformation
@($ov) + @($dup) | Export-Csv "$OutDir\placeholder-overlaps.csv" -NoTypeInformation
"codes=$(($ph | select -Unique -ExpandProperty hash).Count) rows=$($rows.Count) hashSpawns=$(@($ph | ? src -eq 'hash').Count) districtEntries=$(@($ph | ? src -ne 'hash').Count) overlaps=$(@($ov).Count) duplicates=$(@($dup).Count) hidden=$(@($rows | ? status -eq 'hidden').Count)"
