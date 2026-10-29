# ddogs/tools/Build-RomeBatch.ps1 - 2026-10-29 batch from Randy's live scrape:
#  - hide ambient city citizen placeholders (2CIE) in SpawnSkips hiddenHashes
#  - Rome Park (730) friendly Omni guard templates OMUA/ONUR/UISI/UNSQ/UICO/ALGO (NoCombat+NoMove, scraped look)
#    The 22 static scraped guards sit exactly on the existing Spawns.json points, so no SpawnsAdd is needed.
#  - daily NPCs at scraped spots: 7013 John Smith, 7011 Freelancers Inc HQ (handlers, receptionist, Mr Carlsberg),
#    7010 Scarlett Dalquist hooked to her Knubot script
#  - 4695 Inferno sanctuary nano vendor (Unredeemed minion look) with scraped stock (shop 321181702)
# Rerunnable: entries carry Source/Key tag ddogs:rome20261029.
param([string]$GameData = (Join-Path $PSScriptRoot '..\..\AORebirth\GameData'))
$ErrorActionPreference = 'Stop'
$u = [Text.UTF8Encoding]::new($false); $gd = [IO.Path]::GetFullPath($GameData); $tag = 'rome-20261029'
function RJ($p) { [IO.File]::ReadAllText($p, $u) | ConvertFrom-Json }
# ---------- 1. citizens
$kpath = Join-Path $gd 'SpawnSkips.json'; $sk = RJ $kpath
$sk.hiddenHashes = @(@($sk.hiddenHashes | Where-Object { $_ }) + '2CIE' | Sort-Object -Unique)
[IO.File]::WriteAllText($kpath, ($sk | ConvertTo-Json -Depth 6) + "`n", $u)
# ---------- 2. guards
$npath = Join-Path $gd 'NpcTemplates.json'; $nt = RJ $npath
$guards = @(
  @{ h='OMUA'; n='Omni-AF Urban Trooper'; L=250; hp=201270; br=3; md=26151; sc=130; mesh=5921;   head=40171; fam=2 },
  @{ h='ONUR'; n='Omni-AF Urban Trooper'; L=250; hp=201270; br=2; md=26137; sc=130; mesh=5934;   head=40209; fam=2 },
  @{ h='UISI'; n='Unicorn Specialist';    L=250; hp=800000; br=1; md=163003; sc=130; mesh=163173; head=0;    fam=219 },
  @{ h='UNSQ'; n='Unicorn Squadleader';   L=250; hp=600700; br=1; md=247041; sc=130; mesh=247037; head=0;    fam=219 },
  @{ h='UICO'; n='Unicorn Commander';     L=255; hp=453333; br=1; md=247033; sc=131; mesh=246883; head=40694; fam=219 },
  @{ h='ALGO'; n='Alan Goodwin';          L=204; hp=277306; br=2; md=267323; sc=100; mesh=5914;   head=40279; fam=158 }
)
$pool = New-Object System.Collections.ArrayList
foreach ($p in $nt.PSObject.Properties) { if ($p.Value.ContentProvenance.Guessed) { continue }; foreach ($b in @($p.Value.Templates)) { if ($b -and $b.Stats -and $b.Stats.'359' -and [int]$b.Level -ge 200) { [void]$pool.Add($b) } } }
foreach ($g in $guards) {
  $d = $pool | Sort-Object { [math]::Abs([int]$_.Level - $g.L) } | Select-Object -First 1
  $c = $d | ConvertTo-Json -Depth 10 | ConvertFrom-Json
  $c.Name = $g.n; $c.Level = $g.L
  $set = [ordered]@{ '54' = $g.L; '33' = 2; '359' = $g.md; '360' = $g.sc; '455' = $g.fam; '1' = $g.hp; '27' = $g.hp; '4' = $g.br; '12' = $g.mesh }
  if ($g.head -gt 0) { $set['64'] = $g.head }
  foreach ($k in $set.Keys) { $c.Stats | Add-Member -Force NoteProperty $k $set[$k] }
  $c | Add-Member -Force NoteProperty LootTable @()
  $c | Add-Member -Force NoteProperty Features @(@($c.Features | Where-Object { $_ -and $_ -ne 'Wander' }) + 'NoCombat' + 'NoMove' | Sort-Object -Unique)
  $nt | Add-Member -Force NoteProperty $g.h ([pscustomobject]@{ Templates = @($c); ContentProvenance = [pscustomobject]@{ Guessed = $false; Source = $tag; Note = "live PRK scrape 2026-10-29 Rome Park 730: friendly Omni guard (NoCombat/NoMove), donor $($d.Name) L$($d.Level)" } })
}
[IO.File]::WriteAllText($npath, ($nt | ConvertTo-Json -Depth 10) + "`n", $u)
# ---------- 4. daily NPCs (content NPCs; dialogs bind by SpawnHash or ContentNpcIdentity)
$base = (RJ (Join-Path $gd 'PlayfieldContent\735\Npcs.json')).Npcs | Where-Object { $_.Key -eq 'ddogs:agency:735:handler' } | Select-Object -First 1
$daily = @(
  @{ pf=7013; key='john-smith'; n='John Smith'; id='ddogs-john-smith-7013'; sh=$null; L=150; hp=16042; side=0; br=1; md=285024; sc=117; head=40693; fam=137; fl=277352961; pos=@(114.074,2.015,94.391); ry=-0.349; rw=0.937 },
  @{ pf=7011; key='handler'; n='Agency Handler'; id='ddogs-agency-handler-7011'; sh='AGHN'; L=150; hp=16042; side=2; br=1; md=285023; sc=152; head=223846; fam=88; fl=277352961; pos=@(65.702,2.015,87.869); ry=0.670; rw=0.743 },
  @{ pf=7011; key='alien'; n='Alien Agency Handler'; id='ddogs-agency-alien-7011'; sh='AGAX'; L=150; hp=16042; side=2; br=1; md=283531; sc=152; head=40680; fam=88; fl=277352961; pos=@(134.168,2.035,88.515); ry=-0.735; rw=0.678 },
  @{ pf=7011; key='senior'; n='Senior Agency Handler'; id='ddogs-agency-senior-7011'; sh='AGSR'; L=220; hp=122232; side=2; br=1; md=26090; sc=162; head=40629; fam=88; fl=277352961; pos=@(99.066,2.015,63.298); ry=-0.340; rw=0.940 },
  @{ pf=7011; key='receptionist'; n='Automated Agency Receptionist'; id='ddogs-agency-receptionist-7011'; sh='AGRC'; L=150; hp=16042; side=2; br=1; md=17649; sc=152; head=0; fam=88; fl=277352961; pos=@(100.090,2.015,76.594); ry=-0.155; rw=0.988 },
  @{ pf=7011; key='mr-carlsberg'; n='Mr Carlsberg'; id='ddogs-mr-carlsberg-7011'; sh=$null; L=70; hp=4746; side=0; br=1; md=26088; sc=140; head=40687; fam=137; fl=277352961; pos=@(88.324,2.015,93.456); ry=0.592; rw=0.806 }
)
$inst = 2110960100; $placed = 0
foreach ($grp in ($daily | Group-Object { $_.pf })) {
  $pf = [int]$grp.Name; $path = Join-Path $gd "PlayfieldContent\$pf\Npcs.json"; if (-not (Test-Path (Split-Path $path))) { New-Item -ItemType Directory (Split-Path $path) | Out-Null }
  $doc = if (Test-Path $path) { RJ $path } else { [pscustomobject]@{ SchemaVersion = 1; PlayfieldId = $pf; Npcs = @() } }
  $keep = @($doc.Npcs | Where-Object { $_ -and "$($_.Key)" -notlike 'ddogs:rome20261029:*' })
  foreach ($v in $grp.Group) {
    $inst++
    $n = $base | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $n.Key = "ddogs:rome20261029:$($pf):$($v.key)"; $n.Provenance = "live PRK scrape 2026-10-29 (position, heading, look) $($v.n)"
    $n.ContentNpcIdentity = $v.id; $n.PlayfieldId = $pf; $n.InstanceId = $inst; $n.Name = $v.n
    $n.Position = @($v.pos); $n.Rotation = @(0, $v.ry, 0, $v.rw); $n.Passive = $true; $n.Attackable = $false
    $st = [ordered]@{ '0' = $v.fl; '1' = $v.hp; '27' = $v.hp; '4' = $v.br; '33' = $v.side; '54' = $v.L; '359' = $v.md; '360' = $v.sc; '455' = $v.fam; '156' = $v.fam }
    if ($v.head -gt 0) { $st['64'] = $v.head } else { $n.Stats.PSObject.Properties.Remove('64') }
    foreach ($k in $st.Keys) { $n.Stats | Add-Member -Force NoteProperty $k $st[$k] }
    if ($v.sh) { $n | Add-Member -Force NoteProperty SpawnHash $v.sh } else { $n.PSObject.Properties.Remove('SpawnHash') }
    $keep += $n; $placed++
  }
  $doc.Npcs = @($keep); [IO.File]::WriteAllText($path, ($doc | ConvertTo-Json -Depth 10), $u)
}
# Scarlett 7010 (already at the scraped spot): friendly + hooked to her DOJA script
$sp = Join-Path $gd 'PlayfieldContent\7010\Npcs.json'; $sd = RJ $sp
foreach ($x in $sd.Npcs) { if ($x.Name -eq 'Scarlett Dalquist') { $x.Attackable = $false } }
[IO.File]::WriteAllText($sp, ($sd | ConvertTo-Json -Depth 10), $u)
function AddKnu([string]$file, [string]$id) {
  $p = Join-Path $gd "Knubot\$file"; $t = [IO.File]::ReadAllText($p, $u)
  if ($t.Contains('"' + $id + '"')) { return }
  $rx = [regex]'"Npcs"\s*:\s*\['; $m = $rx.Match($t); if (-not $m.Success) { throw "no Npcs in $file" }
  $t = $t.Insert($m.Index + $m.Length, ' "' + $id + '",'); [IO.File]::WriteAllText($p, $t, $u)
}
AddKnu 'JohnSmith.json' 'ddogs-john-smith-7013'
AddKnu 'ScarlettDalquist.json' 'SimpleChar:7A18B924'
# ---------- 5. Inferno sanctuary nano vendor
$stockTxt = '226719,226719,203;223025,223025,207;224074,224074,205;223286,223286,201;227677,227677,201;227679,227679,210;223198,223198,209;223182,223182,203;223120,223120,203;226432,226432,200;226434,226434,207;225903,225903,207;224164,224164,203;220340,220340,205;235387,235387,205;252058,252058,208;210507,210507,200;227118,227118,200;227116,227116,200;223322,223322,217;229101,229101,201;223206,223206,205;226735,226735,203;224150,224150,209;224148,224148,203;226737,226737,203;223270,223270,207;224078,224078,209;252053,252053,204;230389,230389,205;218085,218085,209;233024,233024,201;226279,226279,207;223031,223031,201;223142,223142,207;229103,229103,209;223252,223252,200;218143,218143,203;218141,218141,201;220350,220350,202;218145,218145,205;218147,218147,207;218149,218149,209;223218,223218,207;252047,252047,205;252051,252051,211;252049,252049,208;252007,252007,205;252009,252009,209;227773,227773,205;223336,223336,215;223110,223110,209;223108,223108,201;253383,253383,201;259353,259353,202;227124,227124,203;227122,227122,203;224160,224160,209;224126,224126,205;223254,223254,207;224170,224170,207;233027,233027,207;223196,223196,200;226442,226442,205;223290,223290,209;223148,223148,207;218067,218067,205;226405,226405,201;227775,227775,210;227395,227395,201;227397,227397,209;218083,218083,203;252055,252055,209;223234,223234,201;225899,225899,201;224415,224415,201;224417,224417,205;224419,224419,209;252156,252156,206;223236,223236,209;234080,234080,200;231025,231025,200;231026,231026,200;223288,223288,205;223132,223132,205;235290,235290,204;235288,235288,199;223268,223268,203;226420,226420,203;226277,226277,200;229063,229063,1'
$vp = Join-Path $gd 'PlayfieldContent\4695\Npcs.json'; $vd = RJ $vp
$vkeep = @($vd.Npcs | Where-Object { $_ -and "$($_.Key)" -notlike 'ddogs:rome20261029:*' })
$look = $vkeep | Where-Object { $_.Name -eq 'Craig-Or of Preservation' -and $_.Vendor } | Select-Object -First 1
$nv = $look | ConvertTo-Json -Depth 10 | ConvertFrom-Json
$stock = @(); $slot = 0; foreach ($e in $stockTxt.Split(';')) { $a = $e.Split(','); $stock += [pscustomobject][ordered]@{ Slot = $slot++; LowId = [int]$a[0]; HighId = [int]$a[1]; Quality = [int]$a[2] } }
$nv.Key = 'ddogs:rome20261029:4695:nano'; $nv.Name = 'Craig-Or of Nano Programs'
$nv.Provenance = 'live PRK scrape 2026-10-29 shop 321181702 (91 items, no position/name logged); look cloned from 4695 Craig-Or of Preservation; placed past the vendor row'
$nv.ContentNpcIdentity = 'SimpleChar:7B5C0F01'; $nv.InstanceId = 0x7B5C0F01; $nv.Position = @(636.6, 17.01, 436.2); $nv.Vendor.InstanceId = 0x13D50F01; $nv.Vendor.Stock = $stock
$vd.Npcs = @($vkeep + $nv); [IO.File]::WriteAllText($vp, ($vd | ConvertTo-Json -Depth 10), $u)
"citizens hidden=2CIE guards=$($guards.Count) daily placed=$placed scarlett hooked nanoVendor stock=$($stock.Count)"