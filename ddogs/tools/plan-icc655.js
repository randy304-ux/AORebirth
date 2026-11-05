// ddogs/tools/plan-icc655.js - plan for Build-ScrapeImportICC655.ps1 (pf 655 ICC HQ fixes from live scrape, 2026-11-05).
// Reads live scrape CSVs + GameData (+ gitignored client Spawns.json from the main checkout, read-only). Writes the plan JSON + SQL only.
// Only scraped data: names/levels/stats/positions/rotations come from scrape rows. No-Y (Oct 29 v4) rows are never placed.
const fs=require('fs'),path=require('path');
const W=path.resolve(__dirname,'..','..'), GD=path.join(W,'AORebirth','GameData');
const CLIENT_PF=process.env.ICC_CLIENT_PF||'I:/AO/src/AORebirth/GameData/Playfields';
const SC=process.env.ICC_SCRAPE||'I:/AO/imports/scrape-20261105/extracted/output', PF=655, TAG='scrape-icc655-20261105';
function parseCsv(t){const rows=[];let row=[],f='',q=false;for(let i=0;i<t.length;i++){const c=t[i];if(q){if(c=='"'){if(t[i+1]=='"'){f+='"';i++}else q=false}else f+=c}else if(c=='"')q=true;else if(c==','){row.push(f);f=''}else if(c=='\n'){row.push(f.replace(/\r$/,''));rows.push(row);row=[];f=''}else f+=c}if(f.length||row.length){row.push(f);rows.push(row)}const h=rows.shift();return rows.filter(r=>r.length>=h.length-1).map(r=>Object.fromEntries(h.map((k,i)=>[k,r[i]])))}
const rd=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const d2=(a,b)=>Math.hypot(a.x-b.x,a.z-b.z);
const ICC=p=>p.x>=2980&&p.x<=3460&&p.z>=680&&p.z<=1110;
const SIDE=s=>({Neutral:0,Clan:1,Omni:2,OmniTek:2,'Omni-Tek':2}[s]??3), BREED=b=>({Solitus:1,Opifex:2,Nanomage:3,Atrox:4}[b]??0);
const PETLIKE=/ATTACK|MEDIC|Balto|RUDOLPHO|[\x00-\x1f]/;
const KEEP=new Set(['ddogs:icc:buffbot','ddogs:merc:655:terminal']);
// ---- scrape
const raw=[];for(const f of fs.readdirSync(SC).filter(f=>/^scraped_npcs_.*\.csv$/.test(f)))for(const r of parseCsv(fs.readFileSync(path.join(SC,f),'utf8')))if(r.PlayfieldID==String(PF)){r._f=f;raw.push(r)}
const noY=r=>/^scraped_npcs_20261029/.test(r._f)||+r.Y===0;
const byInst={};for(const r of raw.filter(r=>r.Kind=='npc'&&r.IsNpc=='1'&&r.IsPet!='1'&&r.Name))(byInst[r.Instance]=byInst[r.Instance]||[]).push(r);
let insts=Object.values(byInst).map(rs=>{rs.sort((a,b)=>a.Timestamp<b.Timestamp?-1:1);const good=rs.filter(r=>!noY(r));const L=(good.length?good:rs)[(good.length?good:rs).length-1];
 return {inst:L.Instance,name:L.Name,row:L,hasY:good.length>0,pos:rs.map(r=>({x:+r.X,z:+r.Z})),x:+L.X,y:+L.Y,z:+L.Z,files:[...new Set(rs.map(r=>r._f))],lvls:[...new Set(rs.map(r=>+r.Level))],hpBy:Object.fromEntries(rs.map(r=>[+r.Level,+r.MaxHealth]))}});
insts=insts.filter(i=>ICC(i));
const isRoam=i=>i.pos.some(p=>d2(p,i.pos[0])>5);
const clusters=[];for(const i of insts){let c=clusters.find(c=>c.name==i.name&&c.members.some(m=>{const R=(isRoam(m)||isRoam(i))?30:5;return m.pos.some(p=>i.pos.some(q=>d2(p,q)<=R))}));if(!c){c={name:i.name,members:[]};clusters.push(c)}c.members.push(i)}
for(const c of clusters){const per={};c.members.forEach(i=>i.files.forEach(f=>(per[f]=per[f]||[]).push(i)));
 // representatives: the file with most distinct instances, preferring files with Y
 const cand=Object.entries(per).sort((a,b)=>b[1].length-a[1].length||(+!/20261029/.test(b[0]))-(+!/20261029/.test(a[0])));c.reps=cand[0][1];c.count=c.reps.length;c.roam=c.members.some(isRoam);
 // a no-Y rep takes a Y'd member of the same cluster within 5 m if one exists, else stays unplaceable
 c.reps=c.reps.map(r=>{if(r.hasY)return r;const alt=c.members.find(m=>m.hasY&&d2(m,r)<=5&&!c.reps.includes(m));return alt||r})}
// ---- templates
const nt={};for(const f of ['NpcTemplates.json',...fs.readdirSync(GD).filter(f=>/^NpcTemplates\..+\.json$/.test(f)&&f!='NpcTemplates.ICC655.json')])Object.assign(nt,rd(path.join(GD,f)));
function leaves(h,seen=new Set()){if(!h||seen.has(h))return[];seen.add(h);const t=nt[h];if(!t)return[];if(t.Children&&t.Children.length)return t.Children.flatMap(c=>leaves(c,seen));return t.Templates?[[h,t]]:[]}
const names=(h)=>leaves(h).flatMap(([,t])=>t.Templates.filter(x=>x).map(x=>x.Name));
// ---- server points in ICC box (same load filters as HashSpawnSystem)
const skips=rd(path.join(GD,'SpawnSkips.json'));const hidden=new Set((skips.hiddenHashes||[]).map(h=>typeof h=='string'?h:h.hash));
const removedOld=(skips.removedPoints||[]).filter(p=>p.playfield==PF&&!String(p.reason).includes(TAG));
const srv=[];
function addSp(file,src){if(!fs.existsSync(file))return;for(const s of rd(file).spawns||[]){const h=s.hashText||'';const p={x:s.position[0],y:s.position[1],z:s.position[2]};
 if(removedOld.some(r=>r.hash==h&&Math.hypot(r.position[0]-p.x,r.position[1]-p.y,r.position[2]-p.z)<=0.1))continue;
 if(hidden.has(h))continue;if((s.extensions&&s.extensions.events||[]).some(e=>e&&e.name&&!e.active))continue;
 if(!ICC(p))continue;srv.push({src,hash:h,lo:s.minLevel,hi:s.maxLevel,position:s.position,...p,names:names(h)})}}
addSp(path.join(CLIENT_PF,''+PF,'Spawns.json'),'Spawns.json');addSp(path.join(GD,'PlayfieldContent',''+PF,'SpawnsAdd.json'),'SpawnsAdd.json');
for(const n of rd(path.join(GD,'PlayfieldContent',''+PF,'Npcs.json')).Npcs)srv.push({src:'Npcs.json',hash:n.Key,x:n.Position[0],y:n.Position[1],z:n.Position[2],names:[n.Name],custom:true});
// ---- wrong points to replace: hash list from the 2026-11-05 analysis (only ICC box points of these hashes)
const REPLACE=new Set(['3846','ICFM','ICPA','01V1','ABGU','PPCD','ZYBA','FPBY']);
const remove=srv.filter(s=>!s.custom&&REPLACE.has(s.hash));
const kept=srv.filter(s=>!remove.includes(s));
// ---- live spawns to add: every non-UPH, non-pet live rep not already covered by a kept server point of the same name
const adds=[],skipped=[],used=new Set(),usedRm=new Set();
for(const c of clusters){if(c.name=='Uncle Pumpkin-Head')continue;if(PETLIKE.test(c.name)){skipped.push({name:c.name,why:'pet-like (skipped by Randy)',x:c.reps[0].x,z:c.reps[0].z});continue}
 for(const r of c.reps){const R=c.roam?30:5;const k=kept.find(s=>!used.has(s)&&s.names.includes(c.name)&&d2(s,r)<=R);if(k){used.add(k);continue}
  if(!r.hasY){const sp=remove.filter(s=>!usedRm.has(s)&&(s.names.includes(c.name)||(c.name=='Peacekeeper Constad'&&s.hash=='PPCD'))&&d2(s,r)<=15).sort((a,b)=>d2(a,r)-d2(b,r))[0];if(sp){usedRm.add(sp);adds.push({name:c.name,rep:r,roam:c.roam,at:sp});continue}skipped.push({name:c.name,why:'only seen in Oct 29 scrape (no Y height); not placed',x:r.x,z:r.z,level:r.row.Level});continue}
  adds.push({name:c.name,rep:r,roam:c.roam})}}
// templates: reuse an existing leaf with same name+MonsterData, else new hash per name|md|mesh|fam
const b36='0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ';let seq=0;const newHash=()=>{for(;;){seq++;const h='Q6'+b36[Math.floor(seq/36)%36]+b36[seq%36];if(!nt[h])return h}};
const byName={};for(const [h,t] of Object.entries(nt))if(t.Templates)for(const b of t.Templates)if(b&&b.Stats)(byName[b.Name]=byName[b.Name]||[]).push({h,b,n:t.Templates.length});
const templates={},spawns=[];
for(const a of adds){const r=a.rep.row,md=+r.MonsterData,L=+r.Level;let hash=null,reuse=false;
 const ex=(byName[a.name]||[]).find(e=>e.n==1&&+e.b.Stats['359']==md&&+e.b.Level==L);if(ex){hash=ex.h;reuse=true}
 else{const key=[a.name,md,r.Mesh,r.NPCFamily].join('|');if(!templates[key]){templates[key]={hash:newHash(),wander:false,friendly:SIDE(r.Side)!=3,src:`live scrape ${r._f.replace(/^scraped_npcs_|\.csv$/g,'')} inst ${r.Instance}`,bands:{}}}
  const t=templates[key];t.wander=t.wander||a.roam;hash=t.hash;
  for(const lv of a.rep.lvls)if(!t.bands[lv]){
    const band={Name:a.name,Level:lv,hp:a.rep.hpBy[lv]||0,side:SIDE(r.Side),breed:BREED(r.Breed),md,sc:+r.Scale||100,mesh:+r.Mesh||0,head:+r.HeadMesh||0,tex:+r.MonsterTexture||0,fam:+r.NPCFamily||0};
    // Outfit columns from AO_NPC_Scraper outfit bump (optional; older CSVs omit them).
    const textures=(r.Textures||'').split(';').filter(Boolean).map(x=>{const [Place,Id]=x.split(':').map(Number);return {Place,Id}}).filter(t=>t.Id>0);
    const meshes=(r.Meshes||'').split(';').filter(Boolean).map(x=>{const [Position,Id,OverrideTextureId,Layer]=x.split(':').map(Number);return {Position,Id,OverrideTextureId:OverrideTextureId||0,Layer:Layer||0}}).filter(m=>m.Id>0);
    const gloves=(r.EquipmentGloves||'').split(';').map(Number).filter(n=>n>0);
    if(textures.length) band.textures=textures;
    if(meshes.length) band.meshes=meshes;
    if(gloves.length) band.gloves=gloves;
    t.bands[lv]=band}}
 spawns.push(a.at?{hash,name:a.name,reuse,lo:Math.min(...a.rep.lvls),hi:Math.max(...a.rep.lvls),x:a.at.x+0.15,y:a.at.y,z:a.at.z,angle:0,posFrom:'client Spawns.json point (+0.15 m X so the SpawnSkips removedPoint of the old point does not also drop it) '+a.at.hash+' (live sighting only in no-Y Oct 29 scrape, '+d2(a.at,a.rep).toFixed(1)+' m away)'}:{hash,name:a.name,reuse,lo:Math.min(...a.rep.lvls),hi:Math.max(...a.rep.lvls),x:+r.X,y:+r.Y,z:+r.Z,angle:Math.round(+r.HeadingDeg||0),posFrom:'live scrape '+r._f+' inst '+r.Instance})}
// ---- Uncle Pumpkin-Head: live spots in ICC box (Y'd rows only), one pad per live instance position (5 m dedupe)
const uph=[];for(const i of insts.filter(i=>i.name=='Uncle Pumpkin-Head'&&i.hasY)){if(!uph.some(p=>Math.hypot(p[0]-i.x,p[2]-i.z)<=5))uph.push([+i.x.toFixed(1),+i.y.toFixed(1),+i.z.toFixed(1)])}
const hw=rd(path.join(GD,'Halloween.json'));const oldPads=hw.Outdoor.find(o=>o.Key=='uph-icc').Pads;
// ---- vendors (DB rows at 0,0,0) from scraped VendingMachine rows by exact name
const vrows=raw.filter(r=>r.Kind=='vendor'&&!noY(r));const vlatest={};for(const r of vrows)if(!vlatest[r.Name]||vlatest[r.Name].Timestamp<r.Timestamp)vlatest[r.Name]=r;
const DBV=JSON.parse(process.env.ICC_DBV||'[]');
const vmatch=[],vmiss=[];for(const v of DBV){const r=vlatest[v.name];if(r)vmatch.push({...v,x:+r.X,y:+r.Y,z:+r.Z,rx:+r.RotX,ry:+r.RotY,rz:+r.RotZ,rw:+r.RotW,inst:r.Instance,ts:r.Timestamp});else vmiss.push(v)}
const plan={source:TAG,generated:new Date().toISOString(),pf:PF,
 removePoints:remove.map(s=>({playfield:PF,hash:s.hash,position:s.position,reason:`${s.names[0]||s.hash} replaced by live scrape NPCs (${TAG})`})),
 templates:Object.values(templates).map(t=>({hash:t.hash,wander:t.wander,friendly:t.friendly,src:t.src,bands:Object.values(t.bands).sort((a,b)=>a.Level-b.Level)})),
 spawns,skipped,uphPads:uph,uphOldPadCount:oldPads.length,vendors:vmatch,vendorsUnmatched:vmiss,
 counts:{removed:remove.length,removedBy:remove.reduce((a,s)=>(a[s.hash]=(a[s.hash]||0)+1,a),{}),added:spawns.length,addedReuse:spawns.filter(s=>s.reuse).length,newTemplates:Object.keys(templates).length,skipped:skipped.length,uphNew:uph.length,uphOld:oldPads.length,vendorsMatched:vmatch.length,vendorsUnmatched:vmiss.length}};
fs.writeFileSync(path.join(__dirname,'scrape_import_icc655_20261105.json'),JSON.stringify(plan,null,1));
// SQL (idempotent: only rows still at 0,0,0 with that Id+Hash)
const sql=['-- icc-vendors-655.sql: place 16 ICC vendor rows (pf 655) at live scraped VendingMachine positions. Generated by ddogs/tools/plan-icc655.js. DO NOT RUN without Randy OK.','-- Idempotent: only updates rows of pf 655 still at 0,0,0.','START TRANSACTION;'];
for(const v of vmatch)sql.push(`UPDATE vendors SET X=${v.x.toFixed(3)}, Y=${v.y.toFixed(3)}, Z=${v.z.toFixed(3)}, HeadingX=${v.rx}, HeadingY=${v.ry}, HeadingZ=${v.rz}, HeadingW=${v.rw} WHERE Id=${v.id} AND Hash='${v.hash}' AND Playfield=655 AND X=0 AND Y=0 AND Z=0; -- ${v.name} (scrape inst ${v.inst} ${v.ts})`);
for(const v of vmiss)sql.push(`-- UNMATCHED, left unchanged: Id=${v.id} Hash=${v.hash} ${v.name}`);
sql.push('COMMIT;');fs.writeFileSync(path.join(W,'tools-temp','icc-vendors-655.sql'),sql.join('\n')+'\n');
console.log(JSON.stringify(plan.counts),'\nskipped:',JSON.stringify(skipped));
