// match-pinks.js <scrape.csv> <pfs comma> <out.json> : pink spawn points (audit problems_all.csv) vs live scrape positions
const fs=require('fs');const [,,SC,PFS,OUT,RAD='12']=process.argv;const pfs=new Set(PFS.split(','));const R=+RAD;
function csv(t){const rows=[];let row=[],f='',q=false;for(let i=0;i<t.length;i++){const c=t[i];if(q){if(c=='"'){if(t[i+1]=='"'){f+='"';i++}else q=false}else f+=c}else if(c=='"')q=true;else if(c==','){row.push(f);f=''}else if(c=='\n'){row.push(f.replace(/\r$/,''));rows.push(row);row=[];f=''}else f+=c}if(f||row.length){row.push(f);rows.push(row)}const h=rows.shift();return rows.filter(r=>r.length>=h.length).map(r=>Object.fromEntries(h.map((k,i)=>[k,r[i]])))}
const sc=csv(fs.readFileSync(SC,'utf8')).filter(r=>pfs.has(r.PlayfieldID));
const pk=csv(fs.readFileSync('I:/AO/tools-temp/audit-whereis/problems_all.csv','utf8')).filter(r=>r.category=='f_placeholder_pink'&&pfs.has(r.playfield)&&r.source=='client');
const side={Neutral:0,Clan:1,OmniTek:2,Omni:2,Monster:3},breed={Monster:0,Solitus:1,Opifex:2,Nanomage:3,Atrox:4};
const by={};for(const p of pk){const [lo,hi]=p.levels.split('-').map(Number);const near=sc.filter(s=>{const L=+s.Level;return L>=lo-1&&L<=hi+1&&Math.hypot(+s.X-+p.x,+s.Z-+p.z)<=R}).sort((a,b)=>Math.hypot(+a.X-+p.x,+a.Z-+p.z)-Math.hypot(+b.X-+p.x,+b.Z-+p.z));(by[p.hash]??={pf:p.playfield,pts:0,hits:[]}).pts++;if(near[0])by[p.hash].hits.push(near[0])}
const templates=[],unres=[];
for(const [h,v] of Object.entries(by)){const cnt={};for(const s of v.hits)cnt[s.Name]=(cnt[s.Name]||0)+1;const top=Object.entries(cnt).sort((a,b)=>b[1]-a[1])[0];
 if(!top){unres.push({hash:h,pf:v.pf,points:v.pts,why:'no live NPC within '+R+'m at matching level'});continue}
 if(top[1]/v.hits.length<0.6){unres.push({hash:h,pf:v.pf,points:v.pts,why:'ambiguous: '+JSON.stringify(cnt)});continue}
 const ss=v.hits.filter(s=>s.Name==top[0]);const lv={};for(const s of ss)lv[s.Level]??=s;
 const bands=Object.values(lv).sort((a,b)=>a.Level-b.Level).slice(0,8).map(s=>({Name:s.Name,Level:+s.Level,hp:+s.MaxHealth||0,side:side[s.Side]??3,breed:breed[s.Breed]??0,md:+s.MonsterData,sc:+s.Scale||100,mesh:+s.Mesh||0,head:+s.HeadMesh||0,tex:+s.MonsterTexture||0,fam:+s.NPCFamily||0,garden:false,guard:false}));
 if(!bands[0].md){unres.push({hash:h,pf:v.pf,points:v.pts,why:'scrape had no MonsterData for '+top[0]});continue}
 templates.push({hash:h,src:'hashpoint',pf:v.pf,points:v.pts,matched:v.hits.length,agree:top[1],bands})}
fs.writeFileSync(OUT,JSON.stringify({templates,unresolved:unres},null,1));
console.log('pinkPts',pk.length,'hashes',Object.keys(by).length,'resolved',templates.length,'ptsResolved',templates.reduce((a,t)=>a+t.points,0),'unresolved',unres.length);
for(const t of templates)console.log(t.hash,t.points,t.agree+'/'+t.matched,t.bands[0].Name,t.bands.map(b=>b.Level).join('/'));
