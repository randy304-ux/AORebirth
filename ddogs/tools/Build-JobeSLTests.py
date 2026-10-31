# ddogs/tools/Build-JobeSLTests.py - 2026-10-30 live SL research tests (Randy's scrape 19:49-20:20 ET, 4313, scraper v5.6/v5.7).
# Patches Knubot/DrakeRodriguez.json in place on top of Build-JobeSL.py output: live text for Agility, Sense, Psychic,
# Intelligence uploads, the bracer answer, the Brink hand-in and the Donna/Veronica pointers; adds the live
# "Test of Psychic." option (quests XDPF The Faithful -> El-Domo, XDPC The Chosen -> Celeth-El).
# Live bug fixed on purpose: reporting the psychic test drops whichever psychic quest is still running ({dropquest})
# and offers the Test of Intelligence at once (on live Randy had to delete The Faithful by hand).
# Keeps: XDGK once-per-character bracer, sequential test gating, 6 Delicate Metal Devices for the Brink. Rerunnable.
import json,sys
p=sys.argv[1]
d=json.load(open(p,encoding='utf-8-sig'))
R=d['Replies'];L=d['Lines']
def E(t): return '{emote}'+t+'{/emote}'
note=" 2026-10-30 19:49-20:20 ET scrape (Build-JobeSLTests.py): live Agility/Sense/Psychic/Intelligence uploads, bracer answer, Brink hand-in text; Test of Psychic gives XDPF+XDPC; psychic report drops the leftover side quest and offers Intelligence (live bug fixed)."
if note not in d['Notes']: d['Notes']+=note
R['ask_agility']['Goto']='give_agility'
R['ask_sense']['Goto']='give_sense'
R['ask_mind']['Goto']='give_mind'
R['ask_psychic']={'Text':'Test of Psychic.','If':['!HasCompletedQuest XDPR','!HasAnyQuest XDPF,XDPC,XDE0,XDER,XDC0,XDCR,XDPR','HasCompletedQuest XDNR'],'Goto':'give_psychic'}
R['where_donna']={'Text':'Where can I find Donna Red?','If':['HasCompletedQuest XDBR'],'Goto':'where_donna'}
R['where_veronica']={'Text':'Where can I find Veronica Escobar?','If':['HasCompletedQuest XDBR'],'Goto':'where_veronica'}
for k in ('hello','carry_on'):
    r=L[k]['Replies']
    if 'ask_psychic' not in r: r.insert(r.index('ask_mind'),'ask_psychic')
L['give_agility']={'Text':['{spawn QBMS 1}{givequest XDA0}To measure your agility and stamina we want you to register the location of certain rare species and return with the biomarker sensor for evaluation within given time. I am uploading the details right now.',
 '{delay 2}'+E('Drake nods his head and lowers his voice.'),
 '{delay 1}I am sending the specimen to test two. Track its progress.','{delay 1}\\nRodriguez out.'],'Fail':'full','Replies':['bye']}
L['give_sense']={'Text':['To measure your sensitivity in these surroundings we want you to find a shadowleet.',
 '{delay 1}\\nThe shadowleet is usually in hiding. I would suggest searching for it near the weavers.',
 '{delay 1}\\nI am uploading the details right now.{givequest XDN0}','{delay 2}\\nReturn for evaluation when you are done.'],'Replies':['bye']}
L['give_psychic']={'Text':['To measure your psychic abilities we want to investigate the consequences of exposing your mind to the unknown.',
 '{delay 1}\\nTwo encampments have been found. One west and one east of here.',
 '{delay 1}\\nThey will each present you with a problem, but you should only help one of them.',
 '{delay 1}\\nI will now upload details to your NCU concerning the first part of this test, which will be to locate the representatives from both sides.{givequest XDPF}{givequest XDPC}',
 '{delay 2}\\nAdditional details will be uploaded to you when you decide on who to help.',
 '{delay 1}\\nReturn for evaluation when you are done.','{delay 1}\\nChoose you path carefully, {name}.',
 '{delay 2}'+E('Drake lowers his voice.'),'{delay 1}The specimen is now heading for test four.'],'Replies':['bye']}
L['give_mind']={'Text':['{spawn QSUP 1}{givequest XDI0}To measure your intelligence we want you to contact one of our fellow scientists working out in the field.',
 '{delay 1}\\nShe may be hard to find, but that is part of the test.','{delay 1}\\nShe may be found in a cave. The entrance is guarded, so beware.',
 '{delay 1}\\nYou are to give her these supplies...','{delay 2}\\nDetails of your assignment should be uploaded by now.','{delay 1}\\nReturn here for evaluation when you are done.',
 '{delay 2}'+E('Drake turns his head away from you and whispers.'),'{delay 1}The specimen is heading out for a test.',
 "{delay 1}\\nYes, I have high hopes for this one, but remember... Don't get too attached.",'{delay 1}\\nTrack its progress.','{delay 1}\\nRodriguez out.'],'Fail':'full','Replies':['bye']}
drops='{dropquest XDPF}{dropquest XDPC}{dropquest XDE0}{dropquest XDC0}'
L['psy_done']={'Text':['{completequest XDPR}'+drops+'{spawn QBRJ 1}Well done, {name}. Let me have a look at your bracer.',
 '{delay 1}'+E('Drake seems to register some values from your bracer.'),'{delay 1}Well done, {name}! ',
 '{delay 1}\\nAll values are to our satisfaction. Here is your reward, and a fresh bracer in case Celeth-El kept the other one.',
 '{delay 1}\\nYou may now continue with the other tests.'],'Fail':'full','Replies':['ask_mind','bye']}
L['bracer']={'Text':['This bracer was constructed to measure your performance during our examinations. It is extremely important that you wear one throughout your entire trial period here with us in Nascence.',
 '{delay 1}Right-click the Bracer to get warped back to Drake from anywhere in the training grounds.'],'Replies':['more','nothing']}
L['done_brink']={'Text':['{completequest XDBR}What the...','{delay 1}'+E('Drake lowers his voice.'),
 '{delay 1}Hold everything, Donna! You too, Veronica! The specimen found something useful here.','{delay 1}'+E('He turns back to you.'),
 '{delay 1}{name}, what you found here is truly amazing.','{delay 1}'+E('Drake gently brushes away some dust from the device, revealing ancient symbols.'),
 '{delay 1}These symbols clearly indicate the involvement of intellectually advanced beings.','{delay 1}\\nPossibly an entire civilization!',
 '{delay 1}'+E('He tries to translate the symbols.'),"{delay 2}I'm sorry, {name}. I can only make out one word.",'{delay 1}\\nEr...','{delay 1}...Goh...?',
 '{delay 2}\\nNo, wait.','{delay 1}\\nErgo.',"{delay 1}\\nYes, that's it. Whatever that means...",'{delay 1}'+E('Drake reaches into a backpack.'),
 'As promised, {name}. Your reward...','{delay 1}\\nTalk to Donna Red or Veronica Escobar if you want to further aid us in our research.',
 '{delay 1}'+E('Drake turns around and continues to investigate the strange device. You repeat the word a couple of times. Ergo.... Ergo. Why does it sound so familiar?')],
 'Replies':['where_donna','where_veronica','bye']}
L['where_veronica']={'Text':['Last I heard she was somewhere in Nascence.','{delay 1}\\nHead on through Jobe Research and you will find Nascence Frontier.',
 "{delay 1}\\nThat's where I would look.","{delay 1}\\nNow if you'll excuse me...{close}"]}
L['where_donna']={'Text':["Donna went out into Nascence. Head on through Jobe Research to Nascence Frontier and ask around.","{delay 1}\\nNow if you'll excuse me...{close}"]}
with open(p,'w',encoding='utf-8',newline='\n') as f: f.write(json.dumps(d,indent=2,ensure_ascii=False)+'\n')
print('ok',len(R),len(L))
