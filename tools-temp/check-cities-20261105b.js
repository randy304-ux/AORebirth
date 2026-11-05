// tools-temp/check-cities-20261105b.js - validate scrape-cities-20261105b GameData (modeled on check-icc655.js)
const fs=require('fs'),p=require('path');
const GD=process.argv[2]||'I:/AO/worktrees/city-batch/AORebirth/GameData';
const TAG='scrape-cities-20261105b', PFS=[800,545,540,566,700,705,710];
const MUST800=['Derek Pofier','Andre Bottle','Omni-Tek Recruitment Officer','Unicorn Duty Sergeant','Levon Karubian'];
const rd=f=>JSON.parse(fs.readFileSync(p.join(GD,f),'utf8').replace(/^\uFEFF/,''));
const nt=rd('NpcTemplates.json');
for(const f of fs.readdirSync(GD).filter(f=>/^NpcTemplates\..+\.json$/.test(f)&&f!=='NpcTemplates.Cities2.json'))Object.assign(nt,rd(f));
const ov=rd('NpcTemplates.Cities2.json');
const coll=Object.keys(ov).filter(k=>nt[k]);
Object.assign(nt,ov);
const nameOf=h=>{const t=nt[h];return t&&t.Templates&&t.Templates[0]&&t.Templates[0].Name};
const per={},all=[],bad=[],y0=[],hid=[],lvl=[];
const hidden=new Set(rd('SpawnSkips.json').hiddenHashes.map(h=>h.hash||h));
for(const pf of PFS){
  const f=`PlayfieldContent/${pf}/SpawnsAdd.cities20261105b.json`;
  const j=rd(f);
  if(j.source!==TAG)throw new Error(`${f} source ${j.source}`);
  if(j.playfieldId!==pf)throw new Error(`${f} playfieldId ${j.playfieldId}`);
  const sp=j.spawns||[];
  per[pf]=sp.length;
  for(const s of sp){
    all.push({pf,...s});
    if(!nt[s.hashText])bad.push({pf,hash:s.hashText});
    if(!s.position[1])y0.push({pf,hash:s.hashText});
    if(hidden.has(s.hashText))hid.push({pf,hash:s.hashText});
    const t=nt[s.hashText];
    if(t&&t.Templates&&!t.Templates.some(b=>b&&b.Level>=s.minLevel&&b.Level<=s.maxLevel))lvl.push({pf,hash:s.hashText});
  }
}
const names800=all.filter(s=>s.pf===800).map(s=>nameOf(s.hashText));
const mustMissing=MUST800.filter(n=>!names800.includes(n));
const uds=all.find(s=>s.pf===800&&nameOf(s.hashText)==='Unicorn Duty Sergeant');
const udsT=uds&&nt[uds.hashText]&&nt[uds.hashText].Templates.find(b=>b.Level===220);
const udsOk=!!(udsT&&+udsT.Stats['33']===2&&+udsT.Stats['4']===1&&+udsT.Stats['359']===257040&&+udsT.Stats['12']===246883&&+udsT.Stats['64']===40696);
const levon=all.find(s=>s.pf===800&&nameOf(s.hashText)==='Levon Karubian');
const levonPos=levon&&Math.hypot(levon.position[0]-667,levon.position[2]-597)<5;
const rm=rd('SpawnSkips.json').removedPoints.filter(r=>String(r.reason).includes(TAG));
const lekaRm=rm.some(r=>r.playfield===800&&r.hash==='LEKA'&&Math.hypot(r.position[0]-613.24567,r.position[2]-475.4377)<1);
const out={jsonOk:true,overlayHashes:Object.keys(ov).length,collisions:coll,perPf:per,totalPoints:all.length,
 unresolvedHash:bad,zeroY:y0.length,hiddenHashUsed:hid.length,bandLevelOutsideRange:lvl.map(x=>x.hash),
 mustMissing,unicornDutySergeantOk:udsOk,levonNearLive:!!levonPos,levonRemovedOld:lekaRm,removedPointsTag:rm.length};
console.log(JSON.stringify(out,null,2));
if(coll.length||bad.length||y0.length||hid.length||lvl.length||mustMissing.length||!udsOk||!levonPos||!lekaRm)process.exit(1);
