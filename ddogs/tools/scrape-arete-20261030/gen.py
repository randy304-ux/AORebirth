import json, copy, re, os
G='/workspace/gd'
TAG='scrape-arete-20261030'
t=json.load(open(f'{G}/NpcTemplates.json',encoding='utf-8'))
def band(donor, name, lvl, hp, side, breed, md, sc, mesh, head, fam, wander=None, feats=None, keepwander=False):
    b=copy.deepcopy(t[donor]['Templates'][0])
    b['Name']=name; b['Level']=lvl
    st=b['Stats']; st['54']=lvl; st['33']=side; st['359']=md; st['360']=sc; st['455']=fam
    if hp: st['1']=hp; st['27']=hp
    if breed: st['4']=breed
    if head: st['64']=head
    if mesh: st['12']=mesh
    if not keepwander: b.pop('Wander',None); 
    if 'Features' in b and not keepwander: b['Features']=[f for f in b['Features'] if f!='Wander']
    if wander: b['Wander']={'Home':wander[0],'Radius':wander[1],'MinPauseSeconds':3,'MaxPauseSeconds':15}
    if feats is not None: b['Features']=feats
    b['LootTable']=b.get('LootTable',[]) if donor in ('REGD','SOCW','STCW') else []
    b['ContentProvenance']={'Guessed':False,'Confirmed':True,'Donor':donor,'Source':f'{TAG}: live NPC scrape 2026-10-30 (look/level/hp/side/breed from RC-PC scraped_npcs_20261030.csv); donor {donor} supplies the remaining stats'}
    return b
S,O,N,A=1,2,3,4
civ=dict(feats=['NoCombat','NoMove'])
# name, lvl, hp, breed, md, sc, mesh, head, (x,y,z), heading
civs=[
 ('Alta Fuchs',1,25,N,26149,90,5941,223905,(3439.727,9.010,887.680),231.1),
 ('Britni Annunziata',12,294,S,26090,96,5927,40655,(3472.173,9.01,851.728),269.9),
 ('Chauncey Varela',11,261,A,26097,96,5900,40121,(3405.27,9.01,834.664),138.9),
 ('Donte Clendennen',20,559,A,26097,99,5900,40110,(3460.738,9.165,812.654),349.2),
 ('Douglass Guynes',20,559,A,26097,99,5900,236704,(3420.93,9.165,811.62),0.2),
 ('Effie Depaolo',7,160,S,26090,94,5927,40641,(3439.939,9.01,860.551),0.2),
 ('Elsie Clancy',13,327,S,26090,96,5927,223830,(3422.562,9.145,839.863),192.5),
 ('Herschel Musco',15,393,O,26139,97,5914,223891,(3441.177,9.01,860.747),0.4),
 ('Jeanne Strimple',20,559,S,26090,99,5927,40631,(3465.024,9.01,849.869),89.8),
 ('Kirby Schatz',24,691,O,26139,100,5914,40250,(3450.136,9.145,864.901),0.5),
 ('Lashaunda Cratty',11,261,S,26090,96,5927,40636,(3429.217,9.215,800.099),160.1),
 ('Milford Heiliger',27,848,A,26097,101,5900,40111,(3470.66,9.01,831.889),179.8),
 ('Mohamed Reineman',26,786,A,26097,101,5900,40110,(3470.925,9.01,858.486),359.5),
 ('Myrtle Phare',6,138,S,26090,93,5927,40645,(3438.5,9.01,846.915),25.3),
 ('Pamela Borroel',1,25,S,26090,90,5927,40636,(3437.904,9.01,834.576),180),
 ('Ryan Daken',16,426,A,26097,97,5900,40116,(3436.179,9.01,887.364),0.3),
 ('Sadye Washko',19,526,N,26149,98,5941,40162,(3463.826,9.01,858.831),359.5),
 ('Suzie Mirabelli',2,48,N,26149,91,5941,223911,(3459.957,9.135,801.89),89.9),
]
wand={'Elsie Clancy':([3418.6,9.145,857.8],21),'Donte Clendennen':([3419.4,9.115,811.9],16),'Kirby Schatz':([3436.4,9.145,847.8],43)}
used=set(t)
new={}; spawns=[]
def nh(i):
    h='AQ%02d'%i
    assert h not in used, h
    used.add(h); return h
i=1
for (n,l,hp,br,md,sc,me,hd,p,a) in civs:
    h=nh(i); i+=1
    w=wand.get(n)
    new[h]=band('LEMY',n,l,hp,0,br,md,sc,me,hd,103,wander=w,**civ)
    spawns.append((h,l,p,a))
# violent protesters
h=nh(i); i+=1
new[h]=band('ROTE','Violent Protester',3,14,3,A,203740,92,23365,40127,0)
for p,a in [((3502.557,5.11,819.652),54.7),((3505.191,5.11,820.072),24.9)]: spawns.append((h,3,p,a))
# marauders
mh='AQMR'; assert mh not in used; used.add(mh)
new[mh]={'Templates':[band('REGD','SANDSTORM Marauder',7,650,3,S,287217,94,287198,0,100,feats=[])]}
for p,a in [((4032.05,0.01,667.841),270.5),((4033.056,0.01,676.942),270),((4054.297,1.618,651.211),270.1),((4039.737,0.587,695.923),270.2),((4075.564,3.851,719.8),250.9)]:
    spawns.append((mh,7,p,a))
# note: the 5th live marauder uses md 265822 / mesh 265819 / head 40101; one template keeps the common look
for k,v in list(new.items()):
    if 'Templates' not in v: new[k]={'Templates':[v]}
# fill / retemplate existing hashes on 6553
fill={
 'BSTP':band('IPPC','Boris the Peacekeeper',25,724,0,A,165212,100,5900,40116,137,feats=['NoCombat','NoMove']),
 'CBUT':band('LEMY','Clan Bartender',200,36434,1,A,26097,121,5900,40111,1,feats=['NoCombat','NoMove']),
 'ALJA':band('ROTE','Bruiser',5,138,3,S,26088,93,17534,40687,0,feats=[]),
 'FCTO':band('ROTE','Obedience Enforcement',5,138,3,A,165196,110,5900,40117,0,feats=[]),
 'DART':band('ONGA','Omni-Med Surgeon',20,709,2,S,26092,99,5907,40694,105,feats=['NoCombat','NoMove']),
 'KLHG':band('ONGA','Omni-Med Guard',20,559,2,O,26139,99,5914,40249,2,feats=['NoCombat','NoMove']),
 'SOCW':band('SOCW','SANDSTORM Control Tower',13,327,3,0,200894,96,201342,0,100,feats=['NoMove']),
 'STCW':band('STCW','SANDSTORM Massive Cannon Turret',13,327,3,0,201634,96,201330,0,100,feats=['NoMove']),
}
for k,v in fill.items(): fill[k]={'Templates':[v]}
fill['CBUT']['Templates'][0]['ContentProvenance']['Source']+='; CBUT is also used on pf 106 and 1893 (was unfilled/pink there too)'
# --- write NpcTemplates textually
raw=open(f'{G}/NpcTemplates.json','rb').read().decode('utf-8')
def ser(k,v):
    s=json.dumps(v,indent=4,ensure_ascii=False).replace('\n','\r\n    ')
    return f'    "{k}":  '+s
for k,v in fill.items():
    m=re.search(r'\r\n    "%s":  \{'%k, raw)
    if m:
        st=m.start()+2
        nx=re.search(r'\r\n    "[^"]+":  ', raw[m.end():])
        en = m.end()+nx.start() if nx else raw.rfind('\r\n}')
        blk=raw[st:en]; comma = blk.rstrip().endswith(',')
        raw=raw[:st]+ser(k,v)+(',' if comma else '')+raw[en:]
    else:
        new[k]=v
end=raw.rfind('\r\n}')
add=''.join(',\r\n'+ser(k,v) for k,v in new.items())
raw=raw[:end]+add+raw[end:]
json.loads(raw)
open(f'{G}/NpcTemplates.json','wb').write(raw.encode('utf-8'))
# --- SpawnsAdd
def sp(h,l,p,a):
    return {"districtIndex":0,"hash":0,"hashText":h,"manifestHash":0,"minLevel":l,"maxLevel":l,"respawnChance":100,"flags":1,"respawnTime":600,"version7Extra":0,"hasOptionalFlagBlock":True,"nativeFlags":0,"moreFlags":0,"assistanceRadius":0,"unknownOptionalU8":0,"angle":round(a),"angleW":1,"position":list(p),"radius":1,"additionalPoints":[],"extensions":None}
sa={"schemaVersion":1,"recordType":1000014,"playfieldId":6553,"source":TAG,"spawns":[sp(*x) for x in spawns]}
os.makedirs(f'{G}/PlayfieldContent/6553',exist_ok=True)
open(f'{G}/PlayfieldContent/6553/SpawnsAdd.json','wb').write(json.dumps(sa,indent=4).replace('\n','\r\n').encode())
# --- SpawnSkips removedPoints
spf=json.load(open(f'{G}/Playfields/6553/Spawns.json',encoding='utf-8-sig'))['spawns']
skr=open(f'{G}/SpawnSkips.json','rb').read().decode('utf-8-sig'); sk=json.loads(skr)
civpos=[(p[0],p[2]) for (_,_,_,_,_,_,_,_,p,_) in civs]
rm=[]
for s in spf:
    x,y,z=s['position']
    if s['hashText']=='3846':
        near=min(civpos,key=lambda c:(c[0]-x)**2+(c[1]-z)**2); d=((near[0]-x)**2+(near[1]-z)**2)**.5
        assert d<3,(x,z,d)
        nm=[c[0] for c in civs if (c[8][0],c[8][2])==near][0]
        rm.append({"playfield":6553,"hash":"3846","position":s['position'],"reason":f"{TAG}: live civilian {nm} stands here (named spawn in PlayfieldContent/6553/SpawnsAdd.json)"})
    if s['hashText']=='CMDI' and x>4000:
        rm.append({"playfield":6553,"hash":"CMDI","position":s['position'],"reason":f"{TAG}: SANDSTORM Marauder camp on live, not Waste Collectors (5 marauders in SpawnsAdd)"})
print('removed',len(rm))
i2=skr.rstrip().rfind(']')
ins=''.join(',\n                          '+json.dumps(r) for r in rm)
j=skr.rstrip()[:i2].rstrip()
skr=j+ins+'\n                      ]\n}'
json.loads(skr)
open(f'{G}/SpawnSkips.json','wb').write(skr.encode())
print(len(new),'new templates',len(spawns),'spawns', {k:v['Templates'][0]['Name'] for k,v in new.items()})
