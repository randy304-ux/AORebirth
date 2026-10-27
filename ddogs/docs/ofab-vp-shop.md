# Ofab Victory Point shop

The armor, weapons, general nanos, attunement devices, and the two targeting stations are the vending machines already standing in the Unicorn Defence Hub, playfield 6007. Stock is `GameData/OfabHubStock.json`, filled when those machines spawn. The four profession-nano terminals and the seven battle-station queue terminals are content NPCs in `PlayfieldContent/6007/Npcs.json`.

The Grid exit already works. Playfield 152 dynel 95351, vicinity line-teleport, lands on 6007 line 1 beside the return terminal. Computer Literacy over 90 to enter. The return terminal (95350) needs Computer Literacy over 19 and the character out of combat. No teleport-table row was added.

The Tower Shop stays on Andromeda (655). The old Ofab content terminals were removed from 655.

## Where VP comes from

Stat 669.

- Invasion and city-raid bosses: `max(1, level / 10) * 5`.
- Team alien mothership missions (ZXTE, ZXT1, ZXTH) pay the same formula from the mission's objective level, written onto the quest reward when the mission is accepted. Solo ships (ZXKE, ZXK1, ZXKH) do not pay VP. A mission already in the log keeps the reward it was given.
- A battle-station win pays `level * 4`. A loss pays `level * 1`. Leaving, idling out, or logging out pays nothing.

A level 200 team ship is about 100 VP. Earn rates are unchanged. Shop prices follow Randy's house list at about one tenth.

## Prices

Armor: body 40, pants 30, helmet 25, sleeves 20, gloves/boots 15, back/shoulder/ring 20, times QL, then /10 (tidy, min 1). Weapons Mk 1: 25*QL/10. Nanos: 100 / 250 / 500. Attunement level 1: 20. Laser Tagging Device: 100. Targeting stations: 25. A QL 300 body is 1200 VP.

Attunement devices are sold at level 1 on the General machine and on every Ofab nano terminal. Level 2 and 3 are free right-click upgrades. This server does not track global research, so the upgrade checks level only.

The shop packet only carries low id, high id, and QL. Sell modifier 0 is there so the pane shows 0 credits. The server charges the Price field as stat 669.

## What is stocked

Fourteen profession machines (templates 266562-266575). Each stocks Mk 1 body pieces (helmet, body, sleeves, gloves, pants, boots), Protective Gear, Shoulder Wear, the QL 300 Special Edition helmet, and that profession's Ofab ring. Armor QL steps are 1, 50, 100, 150, 200, 250, 300 (rings at 100/200/300). Ofab L/R sleeves, L/R shoulders, and Defensive/Offensive/Nano/Medical shields from the AI shop list do not exist as separate items. Agent armor is on 266563.

Melee 266577 and ranged 266576: Mk 1 only, QL 1,50,100,150,200,250,300. Higher marks are bio-material upgrades. Hawk is the SMG.

General 266579: general nanos, seven attunement devices at level 1, Laser Tagging Device (259782), and the two targeting stations. Box of Mech Ammunition was not found in items.dat.

Profession nanos, from the AO-Universe shopping guide, are four content terminals because a pane stops at 64 slots:

- Melee and Hybrid: Enforcer, Martial Artist, Adventurer, Keeper, Shade (25).
- Soldier, Agent, Fixer, Doctor, Engineer (29).
- Trader and Bureaucrat (37).
- MP and NT (38).

Every crystal's use is Upload Nano, which the server already runs.

## Attunement

The shop sells level 1 only, at 20 VP. Right-click upgrades it. The path is `GameData/OfabAttunement.json`. Level 3 has no next tier. The guide also required global research before an upgrade. This server does not track that, so the check is the guide's level band: 150 for level 2, 200 for level 3. Wearing the device uses the item's own OnWear modifiers.

## Battle stations

Built. It loads on the next zone restart. Rules and coordinates are `GameData/BattleStations.json`. Queue by talking to the terminal whose name is the bracket. Teams are Red and Blue, assigned at random. Faction side is not changed. Four consoles: Core is 2 points a tick, each arm is 1. Hack is 60 seconds rooted, then 5 seconds to click again. Ticks are 10 seconds, 2.5 when one team holds all four. First to 500 wins. Death respawns at that team's spawn inside the station. Loyalty is stat 670: start 10, accept costs 1, a finished match adds 2, cap 10, 0 will not queue you. Three minutes idle sends you back with no loyalty refund. One to three humans in a queue fill both sides to 6 with level-matched guards. Guards pick an unowned console (prefer Core, avoid ones other friendlies already work), only start a hack when no enemies are nearby, heal same-side players, and if stuck out of sight they warp to the goal. A dropped human schedules a 15-second backfill; at most one bot spawns per tick.

## Not confirmed online

Armor terminals (all fourteen profession machines) and both weapon machines: the item families are confirmed, the retail Victory Point price per QL is not. Shop prices are about one tenth of the old house formula. The Shade guide says armor starts at QL 25 and steps by 25, which is 12 qualities. Six pieces at that step is 72 lines, and the back piece (Ofab Protective Gear, a seventh part) makes it 84. The pane holds 64, so the armor machines still use the shorter QL set (1, 25, 50, 100, 150, 200, 250, 300) and do not yet include the back piece. A screenshot of one profession terminal and of the ranged terminal will settle the QL rows.

Nanos whose AOWiki row is still TBC keep the house price. That includes most of Adventurer, Agent's Two Left Feet, several Bureaucrat and Trader lines, the Keeper line except Courage and Blow Their Cover, Martial Artist except Footsteps and Induce Stupor, and the pet-steal nanos.

Orbital Bombardment Targeting Station and Airstrike Targeting Station are on the General vendor at 25 VP each. No guide listed them as shop stock or gave a retail price. Airstrike, Laser Bombardment, and Orbital Laser Strike are the strike effects and are not stocked.
