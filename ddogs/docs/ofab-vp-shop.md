# Ofab Victory Point shop

The armor, weapons, general nanos, attunement devices, and the two targeting stations are the vending machines already standing in the Unicorn Defence Hub, playfield 6007. Stock is `GameData/OfabHubStock.json`, filled when those machines spawn. The four profession-nano terminals and the seven battle-station queue terminals are content NPCs in `PlayfieldContent/6007/Npcs.json`.

The Grid exit already works. Playfield 152 dynel 95351, vicinity line-teleport, lands on 6007 line 1 beside the return terminal. Computer Literacy over 90 to enter. The return terminal (95350) needs Computer Literacy over 19 and the character out of combat. No teleport-table row was added.

The Tower Shop stays on Andromeda (655). The old Ofab content terminals were removed from 655.

## Where VP comes from

Stat 669.

- Invasion and city-raid bosses: `max(1, level / 10) * 5`.
- Team alien mothership missions (ZXTE, ZXT1, ZXTH) pay the same formula from the mission's objective level, written onto the quest reward when the mission is accepted. Solo ships (ZXKE, ZXK1, ZXKH) do not pay VP. A mission already in the log keeps the reward it was given.
- A battle-station win pays `level * 4`. A loss pays `level * 1`. Leaving, idling out, or logging out pays nothing.

A level 200 team ship is about 100 VP. A QL 300 Ofab piece is 2250 VP. The formula is a house rule.

## Prices

Retail VP prices are not in `shopinventorytemplates`. House rule for gear and nanos: `max(1, floor(QL * QL / 40))`.

| QL | VP |
| --- | --- |
| 1 | 1 |
| 25 | 15 |
| 50 | 62 |
| 100 | 250 |
| 150 | 562 |
| 200 | 1000 |
| 250 | 1562 |
| 300 | 2250 |

Attunement devices are 200 / 600 / 1500. Level 1 is the retail 200. Level 2 and 3 are a house scale. Buying any tier from the vendor does not check level. The two targeting stations are 250 each.

The shop packet only carries low id, high id, and QL. Sell modifier 0 is there so the pane shows 0 credits. The server charges the Price field as stat 669.

## What is stocked

Fourteen profession machines, the hub's own Ofab vendor dynels (templates 266562-266575). Six pieces, QL 1, 25, 50, 100, 150, 200, 250, 300. Agent armor is on the Agent machine (266563). Item 266578, Ofab Agent Armor, is one armor item and is not a placed terminal. Skipped families: Improved, Penultimate, Special, Replica, Protective, Shoulder.

Melee machine 266577: Bear, Mongoose, Panther, Viper, Wolf, Mk 1 through Mk 6, QL 100 and QL 300 (60 lines).

Ranged machine 266576: Cobra, Hawk, Peregrine, Shark, Silverback, Tiger, Mk 1 through Mk 6, QL 300 only (36 lines). QL 100 as well would be 72 lines, past a 64-slot pane.

General machine 266579: the six general nanos (Pet Steal Back and the pet-control set, plus Release Me Now!), the seven attunement families at tiers 1-3, and the two targeting stations.

Profession nanos, from the AO-Universe shopping guide, are four content terminals because a pane stops at 64 slots:

- Melee and Hybrid: Enforcer, Martial Artist, Adventurer, Keeper, Shade (25).
- Soldier, Agent, Fixer, Doctor, Engineer (29).
- Trader and Bureaucrat (37).
- MP and NT (38).

Every crystal's use is Upload Nano, which the server already runs.

## Attunement

Right-click upgrades the device. The path is `GameData/OfabAttunement.json`. The item's own use is Spawn Item plus Destroy Item, and that spawn hash is not trusted, so the server consumes the lower tier and grants the next id itself. Level 3 has no next tier.

House level gates, because this server has no Ofab global-research track: tier 2 needs level 150, tier 3 needs level 200. Wearing the device uses the item's own OnWear modifiers.

## Battle stations

Built. It loads on the next zone restart. Rules and coordinates are `GameData/BattleStations.json`. Queue by talking to the terminal whose name is the bracket. Teams are Red and Blue, assigned at random. Faction side is not changed. Four consoles: Core is 2 points a tick, each arm is 1. Hack is 60 seconds rooted, then 5 seconds to click again. Ticks are 10 seconds, 2.5 when one team holds all four. First to 500 wins. Death respawns at that team's spawn inside the station. Loyalty is stat 670: start 10, accept costs 1, a finished match adds 2, cap 10, 0 will not queue you. Three minutes idle sends you back with no loyalty refund. One to three humans in a queue fill both sides to 6 with level-matched guards.

## Not found, or stocked but not wired

- SMG Peregrine. No item named SMG Peregrine, Peregrine SMG, or Ofab SMG. Not stocked.
- Airstrike (265402 and 265407), Laser Bombardment (265403), and Orbital Laser Strike (267044) are the strike effects. Not stocked.
- Orbital Bombardment Targeting Station (265405) and Airstrike Targeting Station (265406) are on the General vendor. Their use is an input box the server does not handle, so clicking one does not call a strike.
- Orbital Alien Detonator and Orbital Strike were left out. They were not re-checked as shop stock this pass.
