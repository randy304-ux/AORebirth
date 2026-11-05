// read-only analysis: pf 655 live scrape vs server GameData. Writes only to this folder.
const fs=require('fs'),path=require('path');
const OUT=process.env.OUTDIR, GD=process.env.GDDIR, SC='I:/AO/imports/scrape-20261105/extracted/output', PF=655;
function parseCsv(t){const rows=[];let row=[],f='',q=false;for(let i=0;i<t.length;i++){const c=t[i];if(q){if(c=='"'){if(t[i+1]=='"'){f+='"';i++}else q=false}else f+=c}else if(c=='"')q=true;else if(c==','){row.push(f);f=''}else if(c=='\n'){row.push(f.replace(/\r$/,''));rows.push(row);row=[];f=''}else f+=c}if(f.length||row.length){row.push(f);rows.push(row)}const h=rows.shift();return rows.filter(r=>r.length>=h.length-1).map(r=>Object.fromEntries(h.map((k,i)=>[k,r[i]])))}
const csvEsc=v=>{v=v==null?'':String(v);return /[",\n]/.test(v)?'"'+v.replace(/"/g,'""')+'"':v};
const writeCsv=(n,cols,rows)=>fs.writeFileSync(path.join(OUT,n),[cols.join(',')].concat(rows.map(r=>cols.map(c=>csvEsc(r[c])).join(','))).join('\n')+'\n');
const d2=(a,b)=>Math.hypot(a.x-b.x,a.z-b.z);
const rd=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const ICC=p=>p.x>=2980&&p.x<=3460&&p.z>=680&&p.z<=1110; // ICC HQ / park area box
// ---------- scrape
let raw=[];const files=fs.readdirSync(SC).filter(f=>/^scraped_npcs_.*\.csv$/.test(f));
for(const f of files){for(const r of parseCsv(fs.readFileSync(path.join(SC,f),'utf8'))){if(r.PlayfieldID!=String(PF))continue;r._file=f;raw.push(r)}}
const kinds={};raw.forEach(r=>{const k=r.Kind+'|'+r.IdType+'|npc'+r.IsNpc+'|pet'+r.IsPet;kinds[k]=(kinds[k]||0)+1});
const npcRows=raw.filter(r=>r.Kind=='npc'&&r.IsNpc=='1'&&r.IsPet!='1'&&r.Name);
const byInst={};for(const r of npcRows){const k=r.Instance;(byInst[k]=byInst[k]||[]).push(r)}
const sideMap={Neutral:0,Clan:1,Omni:2,Monster:3};
let insts=Object.values(byInst).map(rs=>{rs.sort((a,b)=>a.Timestamp<b.Timestamp?-1:1);const L=rs[rs.length-1];const pos=rs.map(r=>({x:+r.X,y:+r.Y,z:+r.Z}));
 return {inst:L.Instance,name:L.Name,level:+L.Level,lvls:[...new Set(rs.map(r=>+r.Level))],side:L.Side,md:L.MonsterData,mesh:L.Mesh,head:L.HeadMesh,scale:L.Scale,breed:L.Breed,prof:L.Profession,x:+L.X,y:+L.Y,z:+L.Z,pos,ts:L.Timestamp,first:rs[0].Timestamp,files:[...new Set(rs.map(r=>r._file.replace(/^scraped_npcs_|\.csv$/g,'')))]}});
// temp / player-ish exclusions
const tempRe=/^(Buffbot|Contract Terminal)$|\b(Merc|Mercenary)\b|'s (pet|minion)/i;
const excluded=insts.filter(i=>tempRe.test(i.name));insts=insts.filter(i=>!tempRe.test(i.name));
// cluster by name: instances of same name with any position within 30 m become one roamer/spawn
const clusters=[];
const isRoam=i=>i.pos.some(p=>d2(p,i.pos[0])>5);
for(const i of insts){let c=clusters.find(c=>c.name==i.name&&c.members.some(m=>{const R=(isRoam(m)||isRoam(i))?30:5;return m.pos.some(p=>i.pos.some(q=>d2(p,q)<=R))}));
 if(!c){c={name:i.name,members:[]};clusters.push(c)}c.members.push(i)}
// a cluster with N distinct instances that were seen at the same time at different spots is N spawns; keep count = max concurrent distinct instances per file-day (approx = distinct instances per file)
for(const c of clusters){const m=c.members;const perFile={};m.forEach(i=>i.files.forEach(f=>perFile[f]=(perFile[f]||0)+1));c.count=Math.max(...Object.values(perFile));
 const best=m.slice().sort((a,b)=>b.ts<a.ts?-1:1)[0];Object.assign(c,{x:best.x,y:best.y,z:best.z,level:best.level,lvlMin:Math.min(...m.flatMap(i=>i.lvls)),lvlMax:Math.max(...m.flatMap(i=>i.lvls)),side:best.side,md:best.md,mesh:best.mesh,head:best.head,breed:best.breed,prof:best.prof,insts:m.length,nPos:m.reduce((a,i)=>a+i.pos.length,0),roam:m.some(i=>i.pos.some(p=>d2(p,i.pos[0])>5)),pos:m.flatMap(i=>i.pos),last:best.ts,files:[...new Set(m.flatMap(i=>i.files))].join(';')});c.icc=ICC(c)}
// ---------- templates (base + overlays; overlay key replaces)
const nt={};for(const f of ['NpcTemplates.json',...fs.readdirSync(GD).filter(f=>/^NpcTemplates\..+\.json$/.test(f))])Object.assign(nt,rd(path.join(GD,f)));
function leaves(h,seen=new Set()){if(!h||seen.has(h))return[];seen.add(h);const t=nt[h];if(!t)return[];if(t.Children&&t.Children.length)return t.Children.flatMap(c=>leaves(c,seen));return t.Templates?[[h,t]]:[]}
function resolve(h,lo,hi){const ls=leaves(h);if(!ls.length)return null;const mid=(lo+hi)/2;const out=[];
 for(const [lh,t] of ls){const b=(t.Templates||[]).filter(x=>x).sort((a,b)=>Math.abs(a.Level-mid)-Math.abs(b.Level-mid))[0];if(b)out.push({leaf:lh,name:b.Name,level:b.Level,md:b.Stats&&b.Stats['359'],side:b.Stats&&b.Stats['33'],mesh:b.Stats&&b.Stats['12'],src:(t.ContentProvenance&&t.ContentProvenance.Source)||'',guessed:!!(t.ContentProvenance&&t.ContentProvenance.Guessed),allNames:[...new Set(t.Templates.filter(x=>x).map(x=>x.Name))].join('/')})}
 return out}
const skips=rd(path.join(GD,'SpawnSkips.json'));const hidden=new Set((skips.hiddenHashes||[]).map(h=>typeof h=='string'?h:h.hash));
const removed=(skips.removedPoints||[]).filter(p=>p.playfield==PF);const dedupe=skips.dedupeRadius||0;
// ---------- server points
const server=[];const notLoaded=[];
function addSpawns(file,kind){if(!fs.existsSync(file))return;const j=rd(file);const seen=[];
 for(const s of j.spawns||[]){const h=s.hashText||'';const p={x:s.position[0],y:s.position[1],z:s.position[2]};const base={src:kind,hash:h,lo:s.minLevel,hi:s.maxLevel,...p};
  if(removed.some(r=>r.hash==h&&Math.hypot(r.position[0]-p.x,r.position[1]-p.y,r.position[2]-p.z)<=0.1)){notLoaded.push({...base,why:'SpawnSkips removedPoint'});continue}
  const ev=(s.extensions&&s.extensions.events||[]).filter(e=>e&&e.name);const inactive=ev.some(e=>!e.active);
  if(dedupe>0&&!inactive){if(seen.some(q=>q.h==h&&Math.hypot(q.x-p.x,q.y-p.y,q.z-p.z)<=dedupe)){notLoaded.push({...base,why:'dedupe'});continue}seen.push({h,...p})}
  if(hidden.has(h)){notLoaded.push({...base,why:'hiddenHash'});continue}
  if(inactive){notLoaded.push({...base,why:'inactive event '+ev.map(e=>e.name).join('/')});continue}
  const r=resolve(h,s.minLevel,s.maxLevel);server.push({...base,res:r,pink:!r,events:ev.map(e=>e.name).join('/')})}}
addSpawns(path.join('I:/AO/src/AORebirth/GameData/Playfields',''+PF,'Spawns.json'),'Spawns.json');for(const f of fs.readdirSync(path.join(GD,'PlayfieldContent',''+PF)).filter(f=>/^SpawnsAdd\..+\.json$/.test(f)))addSpawns(path.join(GD,'PlayfieldContent',''+PF,f),f);
addSpawns(path.join(GD,'PlayfieldContent',''+PF,'SpawnsAdd.json'),'SpawnsAdd.json');
const npcsJ=rd(path.join(GD,'PlayfieldContent',''+PF,'Npcs.json')).Npcs||[];
for(const n of npcsJ)server.push({src:'Npcs.json',hash:n.Key,lo:n.Level,hi:n.Level,x:n.Position[0],y:n.Position[1],z:n.Position[2],res:[{name:n.Name,level:n.Level,md:'',side:'',mesh:'',src:'content',allNames:n.Name}],pink:false,custom:true});
const hw=rd(path.join(GD,'Halloween.json'));
if(hw.EnabledOnBoot)for(const o of (hw.Outdoor||[]).filter(o=>o.Playfield==PF))for(const p of o.Pads){const r=resolve(o.Hash,o.Level,o.Level);server.push({src:'Halloween.json '+o.Key,hash:o.Hash,lo:o.Level,hi:o.Level,x:p[0],y:p[1],z:p[2],res:r,pink:!r,halloween:true})}
// districts (random-position wildlife) -> names only
const dj=rd(path.join('I:/AO/src/AORebirth/GameData/Playfields',''+PF,'Districts.json'));const distNames={};
for(const d of dj.districts)for(const si of d.spawnInfos||[]){const r=resolve(si.hashText,d.npcMinLevel,d.npcMaxLevel)||[];for(const x of r)for(const nm of x.allNames.split('/')){(distNames[nm]=distNames[nm]||[]).push(d.name)}}
const INTENT=s=>s.custom&&/^ddogs:(icc:buffbot|merc:655:terminal)$/.test(s.hash);
// ---------- match
const used=new Set();const ok=[],wrong=[],pinkRes=[],pinkUnres=[],extra=[],intentional=[];
const nm=s=>s.res?s.res.map(r=>r.name):[];
for(const s of server){if(INTENT(s)){intentional.push(s);continue}
 const names=nm(s);const near=clusters.map((c,i)=>({c,i,d:Math.min(...c.pos.map(p=>d2(p,s)))})).sort((a,b)=>a.d-b.d);
 let m=near.find(o=>o.d<=5&&names.includes(o.c.name));let how='name+5m';
 if(!m){m=near.find(o=>o.d<=30&&names.includes(o.c.name));how='name<=30m (moved)'}
 if(m){used.add(m.i);m.c.matched=(m.c.matched||0)+1;const c=m.c,r=s.res.find(r=>r.name==c.name);const issues=[];
  if(how!='name+5m')issues.push('position off '+m.d.toFixed(1)+'m');
  if(c.lvlMax<s.lo||c.lvlMin>s.hi)issues.push(`level server ${s.lo}-${s.hi} live ${c.lvlMin}-${c.lvlMax}`);
  if(r.md&&c.md&&+r.md!=+c.md)issues.push(`MonsterData server ${r.md} live ${c.md}`);
  if(r.side!==undefined&&r.side!==''&&sideMap[c.side]!==undefined&&sideMap[c.side]<3&&+r.side!=sideMap[c.side])issues.push(`side server ${r.side} live ${c.side}`);
  const row={...s,match:c,dist:m.d,how,issues:issues.join('; ')};(issues.length?wrong:ok).push(row);continue}
 const o=s.halloween?null:near.find(o=>o.d<=5);
 if(s.pink){if(o){used.add(o.i);pinkRes.push({...s,match:o.c,dist:o.d})}else pinkUnres.push({...s,match:near[0]&&near[0].c,dist:near[0]&&near[0].d});continue}
 if(o){used.add(o.i);wrong.push({...s,match:o.c,dist:o.d,how:'position only (different name)',issues:`name server "${names.join('/')}" live "${o.c.name}"`});continue}
 extra.push({...s,match:near[0]&&near[0].c,dist:near[0]&&near[0].d})}
const short=clusters.filter((c,i)=>used.has(i)&&c.count>(c.matched||0)).map(c=>({...c,short:c.count-(c.matched||0)}));
const missing=clusters.filter((c,i)=>!used.has(i)).map(c=>({...c,district:(distNames[c.name]||[]).slice(0,3).join('/'),serverHasName:server.some(s=>nm(s).includes(c.name))}));
// ---------- output
const sv=s=>({src:s.src,hash:s.hash,server_name:nm(s).join('/')||'(pink: no template)',tpl_source:s.res?s.res.map(r=>r.src).join('/'):'',lvl:`${s.lo}-${s.hi}`,server_md:s.res?s.res.map(r=>r.md).join('/'):'',server_side:s.res?s.res.map(r=>r.side).join('/'):'',x:s.x.toFixed(1),y:s.y.toFixed(1),z:s.z.toFixed(1),icc_area:ICC(s)?1:0});
const lv=c=>c?{live_name:c.name,live_lvl:`${c.lvlMin}-${c.lvlMax}`,live_md:c.md,live_side:c.side,live_mesh:c.mesh,live_x:c.x.toFixed(1),live_y:c.y.toFixed(1),live_z:c.z.toFixed(1),live_instances:c.insts,live_last:c.last}:{};
const SC_COLS=['live_name','live_lvl','live_md','live_side','live_mesh','live_x','live_y','live_z','live_instances','live_last'];
const SV_COLS=['src','hash','server_name','tpl_source','lvl','server_md','server_side','x','y','z','icc_area'];
writeCsv('3b-undercount-on-server.csv',['live_name','live_lvl','live_x','live_z','icc_area','live_est_spawns','server_matched','shortfall','files'],short.map(c=>({...lv(c),icc_area:c.icc?1:0,live_est_spawns:c.count,server_matched:c.matched||0,shortfall:c.short,files:c.files})));
writeCsv('1-wrong-on-server.csv',[...SV_COLS,'how','dist_m','issues',...SC_COLS],wrong.map(w=>({...sv(w),how:w.how,dist_m:w.dist.toFixed(1),issues:w.issues,...lv(w.match)})));
writeCsv('2-pinks-resolvable.csv',[...SV_COLS,'dist_m',...SC_COLS],pinkRes.map(w=>({...sv(w),dist_m:w.dist.toFixed(1),...lv(w.match)})));
writeCsv('2b-pinks-unresolved.csv',[...SV_COLS,'nearest_dist_m',...SC_COLS],pinkUnres.map(w=>({...sv(w),nearest_dist_m:w.dist!=null?w.dist.toFixed(1):'',...lv(w.match)})));
writeCsv('3-missing-from-server.csv',['live_name','live_lvl','live_md','live_side','live_mesh','live_breed','live_x','live_y','live_z','icc_area','est_spawns','live_instances','positions_seen','roamer','covered_by_district_wildlife','server_has_name_elsewhere','live_last','files'],missing.map(c=>({...lv(c),live_breed:c.breed,icc_area:c.icc?1:0,est_spawns:c.count,positions_seen:c.nPos,roamer:c.roam?1:0,covered_by_district_wildlife:c.district,server_has_name_elsewhere:c.serverHasName?1:0,files:c.files})));
writeCsv('4-extra-on-server.csv',[...SV_COLS,'custom_content','nearest_live_dist_m',...SC_COLS],extra.map(w=>({...sv(w),custom_content:w.custom?1:0,nearest_live_dist_m:w.dist!=null?w.dist.toFixed(1):'',...lv(w.match)})));
writeCsv('0-ok-matched.csv',[...SV_COLS,'dist_m',...SC_COLS],ok.map(w=>({...sv(w),dist_m:w.dist.toFixed(1),...lv(w.match)})));
writeCsv('server-not-loaded.csv',['src','hash','lo','hi','x','y','z','why'],notLoaded);
writeCsv('scrape-clusters.csv',['live_name','live_lvl','live_md','live_side','live_x','live_z','icc_area','est_spawns','live_instances','roamer','files'],clusters.map(c=>({...lv(c),icc_area:c.icc?1:0,est_spawns:c.count,roamer:c.roam?1:0,files:c.files})));
const cnt=(a,f)=>a.filter(f).length,I=x=>ICC(x.match&&!x.x?x.match:x);
const stats={scrapeRows655:raw.length,kinds,npcInstances:Object.keys(byInst).length,excludedTempMerc:excluded.map(e=>e.name),clusters:clusters.length,clustersICC:cnt(clusters,c=>c.icc),
 serverLoaded:server.length,bySrc:server.reduce((a,s)=>(a[s.src.split(' ')[0]]=(a[s.src.split(' ')[0]]||0)+1,a),{}),notLoaded:notLoaded.length,notLoadedWhy:notLoaded.reduce((a,s)=>(a[s.why]=(a[s.why]||0)+1,a),{}),
 ok:ok.length,wrong:wrong.length,wrongICC:cnt(wrong,ICC),pinkRes:pinkRes.length,pinkResICC:cnt(pinkRes,ICC),pinkUnres:pinkUnres.length,pinkUnresICC:cnt(pinkUnres,ICC),
 undercount:short.length,undercountSpawns:short.reduce((a,c)=>a+c.short,0),undercountICC:short.filter(c=>c.icc).map(c=>c.name+' live'+c.count+'/srv'+(c.matched||0)),missing:missing.length,missingICC:cnt(missing,c=>c.icc),missingSpawnsEst:missing.reduce((a,c)=>a+c.count,0),missingDistrictCovered:cnt(missing,c=>c.district),extra:extra.length,extraICC:cnt(extra,ICC),extraHalloween:cnt(extra,s=>s.halloween),intentional:intentional.map(s=>s.hash),
 wrongKinds:wrong.reduce((a,w)=>{for(const k of w.issues.split('; '))a[k.split(' ')[0]+' '+(k.split(' ')[1]||'')]=(a[k.split(' ')[0]+' '+(k.split(' ')[1]||'')]||0)+1;return a},{}),
 missingTopNames:Object.entries(missing.filter(c=>c.icc).reduce((a,c)=>(a[c.name]=(a[c.name]||0)+c.count,a),{})).sort((a,b)=>b[1]-a[1]).slice(0,40),
 extraTopNames:Object.entries(extra.reduce((a,s)=>{const k=nm(s).join('/')+(ICC(s)?' [ICC]':'');a[k]=(a[k]||0)+1;return a},{})).sort((a,b)=>b[1]-a[1]).slice(0,40),
 pinkResNames:Object.entries(pinkRes.reduce((a,s)=>{const k=s.hash+'->'+s.match.name;a[k]=(a[k]||0)+1;return a},{})).sort((a,b)=>b[1]-a[1]),
 wrongNames:Object.entries(wrong.reduce((a,s)=>{const k=nm(s).join('/')+' | '+s.issues.replace(/[\d.]+m/,'Xm');a[k]=(a[k]||0)+1;return a},{})).sort((a,b)=>b[1]-a[1]).slice(0,40),
 scrapeTimes:[...new Set(npcRows.map(r=>r._file))].map(f=>f+':'+npcRows.filter(r=>r._file==f).length)};
fs.writeFileSync(path.join(OUT,'stats.json'),JSON.stringify(stats,null,1));console.log(JSON.stringify(stats,null,1));
