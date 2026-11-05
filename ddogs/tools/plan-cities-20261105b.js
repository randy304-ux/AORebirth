// ddogs/tools/plan-cities-20261105b.js - plan for Build-ScrapeImportCities2.ps1
// Missing named NPCs (no critters/pets/players) in pfs 800,545,540,566,700,705,710 from scrape-20261105b.
// Only scraped data. Rows without Y are never placed. Dedupe within 3 m. Critters with same-name server point within 200 m skipped.
// Borealis must-includes + Levon Karubian move (SpawnSkips old LEKA + SpawnsAdd at live spot). Leave pf 4374 alone.
const fs=require('fs'),path=require('path');
const W=path.resolve(__dirname,'..','..'), GD=path.join(W,'AORebirth','GameData');
const CLIENT_PF=process.env.CITY_CLIENT_PF||'I:/AO/src/AORebirth/GameData/Playfields';
const SC=process.env.CITY_SCRAPE||'I:/AO/imports/scrape-20261105b/extracted/output';
const PFS=[800,545,540,566,700,705,710], TAG='scrape-cities-20261105b';
const NAMED_SIDE=new Set(['Neutral','Clan','Omni','OmniTek','Omni-Tek']);
const SIDE=s=>({Neutral:0,Clan:1,Omni:2,OmniTek:2,'Omni-Tek':2}[s]??3), BREED=b=>({Solitus:1,Opifex:2,Nanomage:3,Atrox:4}[b]??0);
const PETLIKE=/ATTACK|MEDIC|Balto|RUDOLPHO|[\x00-\x1f]|'s (pet|minion)|Mercenary|^Buffbot$|^Contract Terminal$/i;
const MUST800=new Set(['Derek Pofier','Andre Bottle','Omni-Tek Recruitment Officer','Unicorn Duty Sergeant']);
const LEVON_LIVE={x:667,z:597}; // brief target; pick nearest Y'd Levon scrape
function parseCsv(t){const rows=[];let row=[],f='',q=false;for(let i=0;i<t.length;i++){const c=t[i];if(q){if(c=='"'){if(t[i+1]=='"'){f+='"';i++}else q=false}else f+=c}else if(c=='"')q=true;else if(c==','){row.push(f);f=''}else if(c=='\n'){row.push(f.replace(/\r$/,''));rows.push(row);row=[];f=''}else f+=c}if(f.length||row.length){row.push(f);rows.push(row)}const h=rows.shift();return rows.filter(r=>r.length>=h.length-1).map(r=>Object.fromEntries(h.map((k,i)=>[k,r[i]])))}
const rd=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const d2=(a,b)=>Math.hypot(a.x-b.x,a.z-b.z);
const noY=r=>!(+r.Y>0);

// ---- scrape (named humanoid NPCs only)
const raw=[];for(const f of fs.readdirSync(SC).filter(f=>/^scraped_npcs_.*\.csv$/.test(f)))for(const r of parseCsv(fs.readFileSync(path.join(SC,f),'utf8')))if(PFS.includes(+r.PlayfieldID)){r._f=f;raw.push(r)}
const critterRaw=raw.filter(r=>r.Kind=='npc'&&r.IsNpc=='1'&&r.IsPet!='1'&&r.Name&&r.Side==='Monster');
const byInst={};for(const r of raw.filter(r=>r.Kind=='npc'&&r.IsNpc=='1'&&r.IsPet!='1'&&r.Name&&NAMED_SIDE.has(r.Side)&&!PETLIKE.test(r.Name)))(byInst[r.Instance]=byInst[r.Instance]||[]).push(r);
let insts=Object.values(byInst).map(rs=>{rs.sort((a,b)=>a.Timestamp<b.Timestamp?-1:1);const good=rs.filter(r=>!noY(r));const pool=good.length?good:rs;const L=pool[pool.length-1];
 return {pf:+L.PlayfieldID,inst:L.Instance,name:L.Name,row:L,hasY:good.length>0,pos:rs.map(r=>({x:+r.X,z:+r.Z})),x:+L.X,y:+L.Y,z:+L.Z,files:[...new Set(rs.map(r=>r._f))],lvls:[...new Set(rs.map(r=>+r.Level))],hpBy:Object.fromEntries(rs.map(r=>[+r.Level,+r.MaxHealth]))}});
const isRoam=i=>i.pos.some(p=>d2(p,{x:i.x,z:i.z})>5);
// dedupe within 3 m (same pf+name); keep latest
const pts=[];const skipped=[];
for(const i of insts){
  if(!i.hasY){skipped.push({pf:i.pf,name:i.name,why:'no Y height',x:i.x,z:i.z,level:+i.row.Level});continue}
  const hit=pts.find(p=>p.pf===i.pf&&p.name===i.name&&d2(p,i)<=3);
  if(!hit){pts.push(Object.assign({},i,{roam:isRoam(i)}));continue}
  if(i.row.Timestamp>hit.row.Timestamp){const roam=hit.roam||isRoam(i);Object.assign(hit,i,{roam})} else hit.roam=hit.roam||isRoam(i);
  skipped.push({pf:i.pf,name:i.name,why:'dedupe within 3 m',x:i.x,z:i.z,kept:`${hit.x.toFixed(1)},${hit.z.toFixed(1)}`});
}

// ---- templates catalog (base + overlays; Cities2 not written yet)
const nt={};for(const f of ['NpcTemplates.json',...fs.readdirSync(GD).filter(f=>/^NpcTemplates\..+\.json$/.test(f)&&f!=='NpcTemplates.Cities2.json')])Object.assign(nt,rd(path.join(GD,f)));
function leaves(h,seen=new Set()){if(!h||seen.has(h))return[];seen.add(h);const t=nt[h];if(!t)return[];if(t.Children&&t.Children.length)return t.Children.flatMap(c=>leaves(c,seen));return t.Templates?[[h,t]]:[]}
const names=(h)=>leaves(h).flatMap(([,t])=>t.Templates.filter(x=>x).map(x=>x.Name));
const skips=rd(path.join(GD,'SpawnSkips.json'));const hidden=new Set((skips.hiddenHashes||[]).map(h=>typeof h==='string'?h:h.hash));

function loadServer(pf){
  const removedOld=(skips.removedPoints||[]).filter(p=>p.playfield===pf&&!String(p.reason).includes(TAG));
  const srv=[];
  function addSp(file,src){if(!fs.existsSync(file))return;for(const s of rd(file).spawns||[]){const h=s.hashText||'';const p={x:s.position[0],y:s.position[1],z:s.position[2]};
   if(removedOld.some(r=>r.hash===h&&Math.hypot(r.position[0]-p.x,r.position[1]-p.y,r.position[2]-p.z)<=0.1))continue;
   if(hidden.has(h))continue;if((s.extensions&&s.extensions.events||[]).some(e=>e&&e.name&&!e.active))continue;
   srv.push({src,hash:h,lo:s.minLevel,hi:s.maxLevel,position:s.position,...p,names:names(h)})}}
  addSp(path.join(CLIENT_PF,''+pf,'Spawns.json'),'Spawns.json');
  const addDir=path.join(GD,'PlayfieldContent',''+pf);
  if(fs.existsSync(path.join(addDir,'SpawnsAdd.json')))addSp(path.join(addDir,'SpawnsAdd.json'),'SpawnsAdd.json');
  if(fs.existsSync(addDir))for(const f of fs.readdirSync(addDir).filter(f=>/^SpawnsAdd\..+\.json$/.test(f)&&!f.includes('cities20261105b')))addSp(path.join(addDir,f),f);
  const np=path.join(addDir,'Npcs.json');if(fs.existsSync(np))for(const n of rd(np).Npcs||[])srv.push({src:'Npcs.json',hash:n.Key,x:n.Position[0],y:n.Position[1],z:n.Position[2],names:[n.Name],custom:true});
  return srv;
}

// critter skip bookkeeping (brief): same-name server within 200 m
const critterSkipped=[];
for(const pf of PFS){const srv=loadServer(pf);const byC={};
 for(const r of critterRaw.filter(r=>+r.PlayfieldID===pf&&!noY(r))){const k=r.Instance;(byC[k]=byC[k]||[]).push(r)}
 for(const rs of Object.values(byC)){rs.sort((a,b)=>a.Timestamp<b.Timestamp?-1:1);const L=rs[rs.length-1];const p={x:+L.X,z:+L.Z,name:L.Name,pf};
  if(srv.some(s=>s.names.includes(p.name)&&d2(s,p)<=200))critterSkipped.push({pf,name:p.name,why:'critter same-name server point within 200 m',x:p.x,z:p.z})}}

// ---- Levon move (pf 800 Spawns.json entry / LEKA at old spot -> live near 667,67,597)
const srv800=loadServer(800);
const lekaOld=srv800.find(s=>s.hash==='LEKA'&&Math.hypot(s.x-613.24567,s.z-475.4377)<=1);
const levonCands=pts.filter(p=>p.pf===800&&p.name==='Levon Karubian');
const levonLive=levonCands.slice().sort((a,b)=>d2(a,LEVON_LIVE)-d2(b,LEVON_LIVE))[0];
const removePoints=[];
if(lekaOld)removePoints.push({playfield:800,hash:'LEKA',position:lekaOld.position,reason:`Levon Karubian moved to live scrape spot (${TAG})`});

// ---- adds per pf
const b36='0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ';let seq=0;const newHash=()=>{for(;;){seq++;const h='Q7'+b36[Math.floor(seq/36)%36]+b36[seq%36];if(!nt[h])return h}};
const byName={};for(const [h,t] of Object.entries(nt))if(t.Templates)for(const b of t.Templates)if(b&&b.Stats)(byName[b.Name]=byName[b.Name]||[]).push({h,b,n:t.Templates.length});
const templates={},spawnsByPf={},counts={};
for(const pf of PFS){
  const srv=loadServer(pf);const used=new Set();const adds=[];
  const live=pts.filter(p=>p.pf===pf);
  for(const r of live){
    // Levon: only place the live target; never re-add the old LEKA spot
    if(pf===800&&r.name==='Levon Karubian'){
      if(!levonLive||d2(r,levonLive)>0.01){skipped.push({pf,name:r.name,why:'Levon old/non-target sighting; moved via SpawnSkips+SpawnsAdd',x:r.x,z:r.z});continue}
    }
    const R=r.roam?30:5;
    const k=srv.find((s,idx)=>!used.has(idx)&&s.names.includes(r.name)&&d2(s,r)<=R);
    if(k){used.add(srv.indexOf(k));continue}
    adds.push(r);
  }
  // force Borealis must-includes if somehow filtered
  if(pf===800){
    for(const name of MUST800){
      if(adds.some(a=>a.name===name))continue;
      const r=live.find(p=>p.name===name);
      if(r&&!adds.includes(r))adds.push(r);
      else if(!r)skipped.push({pf,name,why:'must-include but no Y scrape row found'});
    }
    if(levonLive&&!adds.some(a=>a.name==='Levon Karubian'&&d2(a,levonLive)<=0.01))adds.push(levonLive);
  }
  const spawns=[];
  for(const a of adds){
    const r=a.row,md=+r.MonsterData,L=+r.Level;let hash=null,reuse=false;
    // prefer existing LEKA for Levon
    if(a.name==='Levon Karubian'&&nt.LEKA){hash='LEKA';reuse=true}
    else{
      const ex=(byName[a.name]||[]).find(e=>e.n===1&&+e.b.Stats['359']===md&&+e.b.Level===L);
      if(ex){hash=ex.h;reuse=true}
      else{
        const key=[a.name,md,r.Mesh,r.NPCFamily].join('|');
        if(!templates[key])templates[key]={hash:newHash(),wander:false,friendly:SIDE(r.Side)!==3,src:`live scrape ${r._f.replace(/^scraped_npcs_|\.csv$/g,'')} inst ${r.Instance}`,bands:{}};
        const t=templates[key];t.wander=t.wander||!!a.roam;hash=t.hash;
        for(const lv of a.lvls)if(!t.bands[lv]){
          const band={Name:a.name,Level:lv,hp:a.hpBy[lv]||0,side:SIDE(r.Side),breed:BREED(r.Breed),md,sc:+r.Scale||100,mesh:+r.Mesh||0,head:+r.HeadMesh||0,tex:+r.MonsterTexture||0,fam:+r.NPCFamily||0};
          const textures=(r.Textures||'').split(';').filter(Boolean).map(x=>{const [Place,Id]=x.split(':').map(Number);return {Place,Id}}).filter(t=>t.Id>0);
          const meshes=(r.Meshes||'').split(';').filter(Boolean).map(x=>{const [Position,Id,OverrideTextureId,Layer]=x.split(':').map(Number);return {Position,Id,OverrideTextureId:OverrideTextureId||0,Layer:Layer||0}}).filter(m=>m.Id>0);
          const gloves=(r.EquipmentGloves||'').split(';').map(Number).filter(n=>n>0);
          if(textures.length) band.textures=textures;
          if(meshes.length) band.meshes=meshes;
          if(gloves.length) band.gloves=gloves;
          t.bands[lv]=band;
        }
      }
    }
    spawns.push({pf,hash,name:a.name,reuse,lo:Math.min(...a.lvls),hi:Math.max(...a.lvls),x:+r.X,y:+r.Y,z:+r.Z,angle:Math.round(+r.HeadingDeg||0),wander:!!a.roam,
      posFrom:`live scrape ${r._f} inst ${r.Instance}`});
  }
  spawnsByPf[pf]=spawns;
  counts[pf]={liveNamed:live.length,added:spawns.length,addedReuse:spawns.filter(s=>s.reuse).length};
}

const plan={source:TAG,generated:new Date().toISOString(),playfields:PFS,
 removePoints,templates:Object.values(templates).map(t=>({hash:t.hash,wander:t.wander,friendly:t.friendly,src:t.src,bands:Object.values(t.bands).sort((a,b)=>a.Level-b.Level)})),
 spawnsByPf,skipped:[...skipped,...critterSkipped.map(c=>({...c,kind:'critter'}))],
 counts:{...counts,newTemplates:Object.keys(templates).length,skipped:skipped.length,critterSkipped:critterSkipped.length,removePoints:removePoints.length,
  must800:MUST800.size,levonLive:levonLive?{x:levonLive.x,y:levonLive.y,z:levonLive.z}:null}};
fs.writeFileSync(path.join(__dirname,'scrape_import_cities_20261105b.json'),JSON.stringify(plan,null,1));
console.log(JSON.stringify(plan.counts,null,2));
console.log('skipped sample',JSON.stringify(skipped.slice(0,8)));
console.log('per-pf added',Object.fromEntries(PFS.map(pf=>[pf,spawnsByPf[pf].length])));
