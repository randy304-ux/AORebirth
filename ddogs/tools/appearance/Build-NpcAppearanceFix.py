import json,collections
P='tools-temp/ScfuProbe/'
IMPORT=('scrape-city-20261030','scrape-sl-20261029','scrape-nasc-20261030','rome-20261029','arrival-hall-20261030')
hum=collections.defaultdict(list)
for l in open(P+'scraped_humanoids.txt',encoding='utf-8-sig'):
    l=l.strip()
    if l: n,md,sc,hm=l.rsplit('|',3); hum[n].append((int(md),int(sc),int(hm or 0)))
live=collections.defaultdict(list); livemd=collections.defaultdict(list)
for l in open(P+'live_decoded.tsv',encoding='utf-8'):
    p=l.rstrip('\n').split('\t'); n=p[0].split('@')[0]
    rec={'name':n,'md':int(p[1]),'scale':int(p[2]),'head':int(p[3] or 0),
         'tex':[{'Place':int(a),'Id':int(b)} for a,b,_ in (x.split(':') for x in p[4].split(';') if x) if int(b)>0],
         'mesh':[{'Position':int(a),'Id':int(b),'OverrideTextureId':int(c),'Layer':int(e)} for a,b,c,e in (x.split(':') for x in p[5].split(';') if x)]}
    live[n].append(rec); livemd[rec['md']].append(rec)
def outfit(r):
    m=[x for x in r['mesh'] if not (x['Position']==0 and x['Layer']==4)]
    seen=set(); out=[]
    for x in m:
        k=(x['Position'],x['Id'],x['Layer'])
        if k not in seen: seen.add(k); out.append(x)
    return r['tex'],out
p='AORebirth/GameData/NpcTemplates.json'
b=open(p,'rb').read(); bom=b[:3]==b'\xef\xbb\xbf'; d=json.loads(b.decode('utf-8-sig'))
import glob
over=set()
for f in glob.glob('AORebirth/GameData/NpcTemplates.*.json'): over|=set(json.load(open(f,encoding='utf-8-sig')).keys())
stats=collections.Counter(); log=[]; changed={}
for k,v in d.items():
    if not isinstance(v,dict) or 'Templates' not in v: continue
    s=(v.get('ContentProvenance') or {}).get('Source','')
    if s not in IMPORT or k in over: continue
    for t in v['Templates']:
        n=t['Name']
        if n not in hum: continue
        st=t['Stats']; obs=hum[n]
        md=st.get('359')
        o=next((x for x in obs if x[0]==md),None) or collections.Counter(obs).most_common(1)[0][0]
        ch=[]
        if st.get('359')!=o[0]: st['359']=o[0]; ch.append('md')
        if st.get('360')!=o[1]: st['360']=o[1]; ch.append('scale')
        if o[2] and st.get('64')!=o[2]: st['64']=o[2]; ch.append('head')
        r=None; how=None
        if n in live:
            r=next((x for x in live[n] if x['md']==o[0]),live[n][0]); how='name'
        elif any(x.split()[0]==n.split()[0] and '-' in n.split()[0] for x in live):
            fw=n.split()[0]; r=next(x for nn,xs in live.items() if nn.split()[0]==fw for x in xs); how='kin:'+r['name']
        elif o[0] in livemd:
            c=livemd[o[0]]; fw=n.split()[0]
            r=next((x for x in c if x['name'].split()[0]==fw),None) or collections.Counter(json.dumps(x,sort_keys=True) for x in c).most_common(1) and json.loads(collections.Counter(json.dumps(x,sort_keys=True) for x in c).most_common(1)[0][0]); how='md:'+r['name']
        if r and not t.get('Equipment'):
            tex,mesh=outfit(r)
            if tex or mesh:
                t['Textures']=tex; t['Meshes']=mesh; ch.append('outfit('+how+')')
        elif not t.get('Equipment'):
            stats['no-outfit']+=1; log.append(f'NO-OUTFIT {k} {n} md={o[0]}')
        for c in ch: stats[c.split('(')[0]]+=1
        if ch:
            changed[k]=v; stats['templates']+=1; log.append(f'{k} {n}: '+', '.join(ch))
            cp=t.setdefault('ContentProvenance',{}) 
            if isinstance(cp,dict): cp['AppearanceFix']='2026-10-30: '+', '.join(ch)+' (live SCFU textures/meshes, scraped Scale/MonsterData)'
open('AORebirth/GameData/NpcTemplates.Appearance.json','w',encoding='utf-8',newline='\n').write(json.dumps(dict(sorted(changed.items())),indent=2,ensure_ascii=False)+'\n')
open(P+'appearance_fix.log','w',encoding='utf-8').write('\n'.join(log))
print(stats)


