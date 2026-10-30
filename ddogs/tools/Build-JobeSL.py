# ddogs/tools/Build-JobeSL.py - 2026-10-30 live Shadowlands start (Randy's scrape 16:40-16:47 ET, dialogs_raw 4561/4313).
# Patches Knubot/DrakeRodriguez.json (SCDK in Jobe Research 4001, SCDR in Nascense Training Ground 4313)
# with the live intro / Mandatory?! / contract / Test of Strength text and the once-per-character start kit (quest XDGK).
# Input: the current DrakeRodriguez.json (Build-NewbieQuests.ps1 output). Rerunnable.
import json,sys
src,outdir=sys.argv[1],sys.argv[2]
LIVE="Live text from Randy's scrape 2026-10-30 16:40-16:47 ET (dialogs_raw). "
def E(t): return '{emote}'+t+'{/emote}'
# 4313 landing: on the tower platform ~4 m in front of Drake (SCDR spawn 413.76,76.96,353.91)
d=json.load(open(src,encoding='utf-8-sig'))
d['Npcs']=['SCDR','SCDK']
d['Notes']=LIVE+"Scientist Drake Rodriguez: SCDK in Jobe Research 4001 (854.5,34.8,958.6; the Arrival Hall Shadowlands door lands in front of him) and SCDR in Nascense Training Ground 4313. First talk (once per character, quest XDGK): the Bracer of Jobe only. The profession heal crystal and the live meds (Health and Nano Recharger x50 / Stim x25) come with the creation kit (StartPackages.json). Live text: intro, Mandatory?!, contract, Carry on, Test of Strength upload. Other tests, the bracer answer, refusal and later greetings were not captured (stand-ins). Base script from Build-NewbieQuests.ps1, patched by ddogs/tools/Build-JobeSL.py."
R=d['Replies'];L=d['Lines']
for k in ['gear']: R.pop(k,None); L.pop(k,None)
R['mandatory']={'Text':'Mandatory?!','Goto':'mandatory'}
R['refuse']={'Text':"Forget about it. I'm not going to participate in any of your tests.",'Goto':'refuse'}
R['bracer']={'Text':'Tell me more about that bracer.','Goto':'bracer'}
R['carry_on']={'Text':'Sounds reasonable. Carry on with your testing, Drake.','Goto':'carry_on'}
R['rewards']={'Text':'Rewards?','Goto':'rewards'}
R['bye']={'Text':'Goodbye.','Goto':'bye'}
R['ask_strength']['Text']='Test of Strength.'; R['ask_strength']['Goto']='give_strength'; R.pop('take_strength',None)
R['ask_agility']['Text']='Test of Agility and Stamina.'
R['ask_sense']['Text']='Test of Sense.'
R['ask_mind']['Text']='Test of Intelligence.'
kit=['{givequest XDGK}','{completequest XDGK}','{spawn QBRJ 1}']
intro=['I am Drake Rodriguez and I will be handling your case today. Here, take this standard issue gear. You\'ll need it for your mandatory examination.',
 '{delay 1}'+E('You notice that Drake is holding a bracer.'),
 '{delay 1}'+E('Drake nods his head slightly and covertly lowers his voice.'),
 '{delay 1}We have a healthy specimen here. I will take him through the usual tests before we send him to the brink. Rodriguez out.',
 "{delay 1}You can open your inventory by pressing 'I' or using the inventory icon on your gui.",
 '\\nAre you ready?']
tests=['ask_strength','ask_agility','ask_sense','ask_mind','ask_brink','helped_el','helped_ce']
L['intro']={'Text':[''.join(kit)+intro[0]]+intro[1:],'Fail':'full','Replies':['mandatory','refuse','bracer','bye']}
L['mandatory']={'Text':[E('Drake pulls out a printed contract and waves it in front of you.'),'{delay 1}Take a look!','{delay 2}\\nYou agreed to let us examine the effects of environmental contaminants introduced to both your body and mind through carefully conducted tests, as part of the experiment.','{delay 1}\\nWhy, I have never heard of anyone refusing these tests - considering the rewards!'],'Replies':['carry_on','rewards','bye']}
L['rewards']={'Text':'\\nEvery test you pass is paid for, in credits and in equipment. The bracer will see to it that nothing goes unrecorded.','Replies':['carry_on','bye']}
L['carry_on']={'Text':["Excellent. You'll be well compensated for your trouble, of course.",'{delay 1}\\nLet\'s begin!'],'Replies':tests+['bracer','bye']}
L['give_strength']={'Text':['To measure your strength, we want to see you defeat three creatures.','{delay 1}\\nI am uploading the details right now.{givequest XDS0}','{delay 1}\\nThey are all, as described in the briefing, located in this area.','{delay 2}\\nReturn for evaluation when you have completed your assignment.','{delay 2}'+E('Drake nods his head and lowers his voice.'),'{delay 1}I am sending the specimen on test one. Track its progress. Rodriguez out.'],'Replies':['bye']}
L.pop('offer_strength',None)
L['busy_strength']={'Text':'A Deceitful Weaver, a Shining Coiler and a Beit Gras. They are all located in this area. Return for evaluation when you have completed your assignment.','Replies':['bye']}
L['hello']={'Text':'\\nAre you ready?','Replies':['mandatory']+tests+['bracer','refuse','bye']}
L['bye']={'Text':'Goodbye.{close}'}
# every line that offered 'later'/'nothing'/'more' keeps working; drop replies that no longer exist
for v in L.values():
    if v.get('Replies'): v['Replies']=[r for r in v['Replies'] if r in R]
d['Openers']=[{'If':['!HasCompletedQuest XDGK'],'Say':'intro'}]+d['Openers']
json.dump(d,open(outdir+'/DrakeRodriguez.json','w'),indent=2,ensure_ascii=False)
for f,x in (('DrakeRodriguez',d),):
    R2=set(x['Replies']);L2=set(x['Lines'])
    bad=[(k,r) for k,v in x['Lines'].items() for r in v.get('Replies') or [] if r not in R2]+[(k,v['Goto']) for k,v in x['Replies'].items() if v.get('Goto') not in L2]+[o['Say'] for o in x['Openers'] if o['Say'] not in L2]+[t['Goto'] for t in (x.get('Trades') or {}).values() if t['Goto'] not in L2]+[(k,v['Fail']) for k,v in x['Lines'].items() if v.get('Fail') and v['Fail'] not in L2]
    print(f,'bad',bad)
