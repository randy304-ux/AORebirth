import json,re
K='/workspace/gd/Knubot'
def L(f): return json.load(open(f'{K}/{f}.json',encoding='utf-8-sig'))
def W(f,d): open(f'{K}/{f}.json','wb').write(json.dumps(d,indent=4,ensure_ascii=False).replace('\n','\r\n').encode())
def prose(line, new, close=None):
    tx=line['Text']; tx=[tx] if isinstance(tx,str) else tx
    eff=[x for x in tx if re.sub(r'\{[^}]*\}','',x).strip()=='' and re.sub(r'\{delay \d+\}','',x).strip()!='']
    tail=''
    for tok in ('{close}','{opentrade}','{shop}'):
        if any(tok in x for x in tx if re.sub(r'\{[^}]*\}','',x).strip()): tail=tok
    if close is not None: tail=close
    new=list(new); new[-1]+=tail
    line['Text']=eff+new if eff else (new if len(new)>1 else new[0])
LIVE='live dialog capture 2026-10-30 (RC-PC dialogs_20261030.csv)'
# Leet King
d=L('LeetKing'); R=d['Replies']; Ln=d['Lines']
Ln['angry']={'Text':'Silence j00! I r teh King, j00 no speek to me liek dat!','Replies':['sorry']}
R['sorry']['Goto']='mad'
R['small']={'Text':'What do you want me to do.... Your smallness...?','Goto':'sulk'}
Ln['mad']={'Text':'U maekz mad nao! U needz to deserve forgivenesseses! J00 do teh j0bz and I forgievz. We has understandings?','Replies':['small','bye']}
Ln['sulk']['Text']='Dewd u sux. Now I r so angreh I dun wanna speek wif u againz! Go awayz! Shoo! Leave teh king alonez!'
R['woman']={'Text':'a woman... thing?','Goto':'womanthing'}
Ln['queen']={'Text':['{givequest LKQ2}','!U did gewd. Nao u hazs to fiend me a womanthing! Yus!'],'Replies':['woman']}
Ln['womanthing']={'Text':'Wat isz wrong wif u? U not many smartz. Ofc king needzz a queen. U go fiend oen for me nao.','Replies':['where']}
Ln['where']['Text']="U hazs eyeseses don'tz u? Hurry I r vereh loneleh! And giev her chocolateseses and fl0warz fr0m teh kiiiing!{close}"
R['bye']['Text']='Goodbye'
d['Notes']+=f' Text swapped to the {LIVE}: angry/mad/sulk split and the womanthing step.'
W('LeetKing',d)
# Princess
d=L('PrincessLeet'); R=d['Replies']; Ln=d['Lines']
Ln['maybe']['Text']='Maybeh.. Letz me see what he sended u to giefz.{opentrade}'
Ln['taken']={'Text':['{completequest LKQ2}','He gotz me all diz stuffz... Kewl he muszt liekz meh! I found diz junkz in teh desart, u canz has it forz urz troublez!'],'Replies':['thanks']}
R['thanks']={'Text':'Thank you, I guess.','Goto':'date'}
Ln['date']={'Text':'Movez along nao u. I haz a daet!{close}'}
R['bye']['Text']='Goodbye'
d['Notes']=d['Notes'].replace("the 'Givez!' trade prompt is ours","trade prompt and thank-you step now the "+LIVE)
W('PrincessLeet',d)
# Sarah
d=L('SarahGreene'); R=d['Replies']; Ln=d['Lines']
Ln['hello']['Text']=["Well howdy, y'all. Welcome and step on up to Sarah Greene's Armor Imporium. As a new arrival you may not know that it's dangerous to go alone through the wilds of Rubi-Ka, without proper protection. Lucky for you, I've got the best custom-made protection available in the shuttleport!"]
Ln['hello']['Replies']=['ask_armor','questions','nothing']
R['questions']={'Text':'I have some questions.','Goto':'whatsup'}
R['sale']={'Text':'What do you have for sale?','Goto':'sale'}
R['anyone']={'Text':'Do you know anyone I should talk to around here?','Goto':'anyone'}
R['goodbye']={'Text':'Goodbye','Goto':'nothing'}
Ln['whatsup']={'Text':'Whats up?','Replies':['sale','anyone','goodbye']}
Ln['anyone']={'Text':"You should talk to Remi Gallois. He ain't such a bad guy.",'Replies':['sale','goodbye']}
Ln['sale']={'Text':'I offer several types of armor, some which aren\'t too common, and some which you won\'t find anywhere else. Come check out my wares.  <font color="#ff0000">Press the Shopping Cart icon towards the bottom of the chat window to engage trade with Sarah.</font>','Replies':['goodbye']}
t=Ln['done_armor']['Text']; t.insert(1,"I'm so happy I could kiss ya!")
d['Notes']+=f' Hand-edited after generation: greeting, questions branch and the turn-in line are the {LIVE}; quest offer text still guessed. NOTE: re-running Build-NewbieQuests.ps1 would overwrite this.'
W('SarahGreene',d)
# Vernon
d=L('VernonGodfray'); R=d['Replies']; Ln=d['Lines']
Ln['offer_chip']['Text']=['You. Take these.']; R['take_chip']['Text']='Fine, I will hold the items for you.'
prose(Ln['give_chip'],['His outstretched hand offers you the two objects: One a small pill-sized object with electrical connectors poking from the sides, and the other some fashion of scan and input device. He pulls another object from his pocket with his free hand, a strangely green metallic tube. He waits for you to follow his instructions.',
 'As you take the two objects in-hand the man begins running his fingers along the tube, pressing at various points on its featureless, smooth surface. As he works a glow begins eminating from within, steadily growing brighter and brighter. After a moment he pauses and looks up to you for the first time, an expectant expression on his face.','... Well?'])
Ln['give_chip']['Replies']=['wellwhat']
R['wellwhat']={'Text':'... well what?','Goto':'getto'}
Ln['getto']={'Text':['Well get to it!','He seems less than pleased that you haven\'t done... whatever it is he wanted you to do by now.','The little thing is an Omni-Tek Technical Library. The scope thing is a Hacker Tool. I want the Library hacked because that will remove that absolutely moronic "Omni-Tek Employee Lock" on the damnable thing. Just use the tool on the library and get back to me when you\'re done.','With that he returns his attention to the glowing tube.{close}']}
manifest=['Are you now...','Without even blinking an eye the man continues to poke at the tube in his hands, nodding impassively.','Very well, leave it here if you would.',
 "Well then, you seem like you are the useful kind of stray dog. Don't tell me, you are here to get my help with an ID card, but I won't do it for free. I have a simple task, and you'll need that Hacker Tool again. In the dock area, there's a Shipping Manifest Terminal. I need that terminal... altered. You can either go in via the freight entrance or the personnel entrance. Either way is up to you. Once you're in, find the terminal and use your Hacker Tool on it to reroute some of the data over to one of my... organization's own databases. Fairly simple, and once you're done I should be able to give you a hand.  Do you have any questions?"]
for k in ('lib_done','lib_hacked'):
    prose(Ln[k],manifest); Ln[k]['Replies']=['legal','nope']
R['legal']={'Text':'Is this... legal?','Goto':'legal'}; R['nope']={'Text':'Nope.','Goto':'forget'}
Ln['legal']={'Text':['Technically speaking, no. But neither is getting a fake ID.','With that the man nods and returns his attention to the glowing green tube nearby, seemingly forgetting about your presence once more. You suppose question and answer time is over...{close}']}
Ln['forget']={'Text':'With that the man nods and returns his attention to the glowing green tube nearby, seemingly forgetting about your presence once more. You suppose question and answer time is over...{close}'}
Ln['lib']['Text']='Are you now...{opentrade}'; 
R['handin_chip']['Text']="I've returned. The terminal was hacked."
Ln['handin_chip']['Text']=['And you finally return. I was starting to think I\'d have to head on over to the docks and fish you out of some trouble.','Good. You should have an ID chip of some fashion. Give it here.{opentrade}']
prose(Ln['done_chip'],['You procure the chip from your pocket, the man quickly snatching it from your grip. He produces his own hacker tool and sets to work, holding the scope end over the small chip as he taps against the interface display. After only a few seconds he nods again and hands it to you once more.',"There. I've programmed your chip with official ICC clearance codes. Same as if you'd gotten them from a bureaucrat yourself. You'll need that for authentication purposes. Doctor Mason over at the hospital can help you out with imprinting your DNA in to the chip."])
d['Notes']+=f' Hand-edited after generation: offer/hack/manifest/turn-in text is the {LIVE} (live "I\'m done now" option folded into the existing library hand-in trade). NOTE: re-running Build-NewbieQuests.ps1 would overwrite this.'
W('VernonGodfray',d)
# Shady
d=L('ShadyGuy'); R=d['Replies']; Ln=d['Lines']
R['handin_car']['Text']='I have your vehicle.'
Ln['handin_car']['Text']=['Rusty piece of junk!','Give it to me, now! And the crowbar too!{opentrade}']
prose(Ln['done_car'],['Nice! I finally have "my" car back. And a beauty she is.'])
d['Notes']+=f' Hand-edited after generation: hand-in/turn-in text is the {LIVE}; opener and offer text were not captured and stay guessed. NOTE: re-running Build-NewbieQuests.ps1 would overwrite this.'
W('ShadyGuy',d)
# Stan
d=L('StanGoodman'); d['Replies']['handin_factory']['Text']='I think I have what you were looking for.'
d['Notes']+=f' Hand-edited after generation: hand-in option text from the {LIVE} (only options were captured, NPC lines not). NOTE: re-running Build-NewbieQuests.ps1 would overwrite this.'
W('StanGoodman',d)
# Karli
d=L('KarliCappelleri'); R=d['Replies']; Ln=d['Lines']
Ln['hello']['Replies']=['what','dressed','goodbye']
R['goodbye']={'Text':'Goodbye','Goto':'bye'}
R['dressed']={'Text':'Why are you dressed up like that?','Goto':'camo'}
Ln['camo']={'Text':"It is the perfect camouflage. The aliens won't expect a thing. Move along now before I have to pretend to kill you!",'Replies':['dead']}
R['dead']={'Text':'You know the aliens in this room are all dead, right?','Goto':'quietly'}
Ln['quietly']={'Text':'Yes, because I quietly killed them! Do you want to help a girl out or not?','Replies':['teammates']}
R['teammates']={'Text':'I need to get some teammates first.','Goto':'letme'}
Ln['letme']={'Text':'Let me know... Now, leave me so you don\'t blow my cover.','Replies':['goodbye']}
d['Notes']+=f' Opener, "Why are you dressed up like that?" branch and Goodbye are the {LIVE}; the "What are you doing?" branch (spy, missions, Friendly Buff) was not captured and stays guessed.'
W('KarliCappelleri',d)
# Remi
d=L('RemiGallois')
d['Notes']="Remi Gallois, stall by Antonio (3434x833). 'Quelling a SANDSTORM' (SDS0 kill 3 SANDSTORM Marauders AQMR -> SDS1 return). Return dialog and the 'Do you sell any weapons?' option are the "+LIVE+"; the offer lines and the weapons answer are STAND-INS (not captured). Launcher 295757 (HFRL) handed out and taken back on turn-in. Hand-written by ddogs/tools/Build-ScrapeImportArete (knubot step); Build-NewbieQuests.ps1 would overwrite it."
d['Openers']=[{'If':['HasQuest SDS1'],'Say':'back'},{'If':['HasQuest SDS0'],'Say':'busy'},{'Say':'hello'}]
d['Replies']={
 'ask':{'Text':'[stand-in] Need a hand with something?','If':['!HasAnyQuest SDS0,SDS1','!HasCompletedQuest SDS1'],'Goto':'offer'},
 'take':{'Text':'[stand-in] I will test it.','Goto':'give'},
 'weapons':{'Text':'Do you sell any weapons?','Goto':'weapons'},
 'complete':{'Text':'Your field test is complete!','Goto':'pay'},
 'later':{'Text':'Maybe later.','Goto':'later'},
 'goodbye':{'Text':'Goodbye','Goto':'bye'}}
d['Lines']={
 'hello':{'Text':'[stand-in] Remi Gallois, weapons engineer. Always looking for field testers.','Replies':['ask','weapons','goodbye']},
 'offer':{'Text':['[stand-in] I built an Experimental Hellfyre Rocket Launcher and it needs a live field test. The SANDSTORM Marauders camped east of here, out past the fence, will do nicely.','{delay 1}[stand-in] Take out three of them with it and come back. The ICC pays the bounty on them.'],'Replies':['take','later']},
 'give':{'Text':['{spawn HFRL 25}','{givequest SDS0}','{delay 1}[stand-in] It is registered to my ID, so do not lose it.{close}'],'Fail':'full'},
 'busy':{'Text':'[stand-in] Three SANDSTORM Marauders, east of here. Use the launcher.','Replies':['weapons','goodbye']},
 'back':{'Text':'You came back! How did my weapon do?','Replies':['complete','goodbye']},
 'pay':{'Text':['{take HFRL}','{completequest SDS1}',"I saw the explosions from here - Good stuff! I think I might have come up with a real winner this time around. Anyway, since the weapon's registered to my ID, the ICC sent over the bounty reward soon as the kills got registered. Thank you for your help!"],'Replies':['goodbye']},
 'weapons':{'Text':'[stand-in] Not right now. Ask Antonio if you need parts.','Replies':['goodbye']},
 'full':{'Text':'Your hands are full. Make some room in your inventory first.{close}'},
 'later':{'Text':"Suit yourself. I'll be here.{close}"},
 'bye':{'Text':'{close}'}}
W('RemiGallois',d)
# Boris
d={'Notes':'Boris the Peacekeeper (BSTP, 3614x684). Dialog is the '+LIVE+'; the "Who are you?" answer was not captured and is a STAND-IN.','Npcs':['BSTP'],'Openers':[{'Say':'hello'}],
 'Replies':{'who':{'Text':'Who are you?','Goto':'who'},'noviolence':{'Text':"I haven't seen any violence...",'Goto':'gas'},'what':{'Text':'What is Suppresion Gas?','Goto':'what'},
  'values':{'Text':'Tell me more about the different Suppression Gas values.','Goto':'values'},'low':{'Text':'Are there any areas of low suppression gas around here?','Goto':'low'},'goodbye':{'Text':'Goodbye','Goto':'bye'}},
 'Lines':{'hello':{'Text':['Violence leads to more violence. Conflict leads to more conflict. It will never end.','The Peacekeeper slowly laughs to himself.','Which is a good means of job security.'],'Replies':['who','noviolence','goodbye']},
  'who':{'Text':'[stand-in] Boris. I keep the peace around this landing, when the gas does not do it for me.','Replies':['noviolence','goodbye']},
  'gas':{'Text':'That is because violence is restricted in most areas by Suppression Gas.','Replies':['what']},
  'what':{'Text':["It's what keeps people from randomly assaulting eachother. A good means of control, and creates nice demand for venues for people to get really violent. Higher values of suppression gas keeps you safe, lower values leave you open to attack.  If you take a Suppression Gas reading now, you'll see it's at 75%, this allows for the extermination of creatures and other beastiary on the planet.",'Press F9 to take a reading'],'Replies':['values']},
  'values':{'Text':'At 100%, conflict would be impossible. At 75%, you may fight creatures and people that are flagged. At 25% is when things start to get dangerous, and allows people of opposing factions to fight. At 5%, even people of the same side, but not of the same detachment or organization can fight.  And finally, at 0%, that is when anyone is able to combat anyone.  As a safety precaution, it is not possible to assault a person who is much weaker or stronger than you. This, of course, is not the case when facing the various creatures that roam the planet.','Replies':['low']},
  'low':{'Text':"Yes, you see that fenced area next to me? The Suppression Gas there is very low, only 5%. Be careful if you're venturing out there alone.",'Replies':['goodbye']},
  'bye':{'Text':'{close}'}}}
W('BorisPeacekeeper',d)
# quests, items, escort
G='/workspace/gd'
qraw=open(f'{G}/Quests.json','rb').read().decode('utf-8-sig'); q=json.loads(qraw)
rec=[[{'Id':i,'HighId':i,'Name':n,'Quality':25} for i,n in [(223349,'Nano Crystal (Composite Ranged Expertise)'),(223361,'Nano Crystal (Composite Melee Expertise)'),(215265,'Nano Crystal (Composite Physical Special Expertise)'),(223365,'Nano Crystal (Composite Ranged Special Expertise)')]]]
q+= [{'Hash':'SDS0','Name':'Quelling a SANDSTORM','QasInstance':944005,'Scope':'Solo','Action':'KillMultiple','Summary':'Field test Remi Gallois\'s Experimental Hellfyre Rocket Launcher on 3 SANDSTORM Marauders.','Objective':{'Count':3,'Npc':'AQMR','SpecItem':None,'WorldItem':None},'Flags':'NoCash','MissionBit':None,'NextStep':'SDS1','Reward':{'Hash':'RSD0','Credits':0,'Xp':0,'Records':None}},
     {'Hash':'SDS1','Name':'Quelling a SANDSTORM: return to Remi Gallois','QasInstance':944006,'Scope':'Solo','Action':'TargetNpc','Summary':'Return to Remi Gallois.','Objective':{'Count':None,'Npc':'RMGL','SpecItem':None,'WorldItem':None},'Flags':'NoCash','MissionBit':720,'NextStep':None,'Reward':{'Hash':'RSD1','Credits':0,'Xp':0,'Records':rec}}]
open(f'{G}/Quests.json','wb').write(json.dumps(q,indent=4,ensure_ascii=False).replace('\n','\r\n').encode())
print('quests',len(q))
it=open(f'{G}/ItemTemplates.Arete.json').read(); it=it.rstrip()[:-1].rstrip()+',\n  "HFRL": { "Templates": [ 295757, 295757 ] }\n}\n'; json.loads(it); open(f'{G}/ItemTemplates.Arete.json','w').write(it)
e=open(f'{G}/EscortQuests.json').read(); e=e.replace('["OIUR","LUSN","KLHG","DART"]','["OIUR","LUSN"]'); assert 'KLHG' not in e; open(f'{G}/EscortQuests.json','w').write(e)
