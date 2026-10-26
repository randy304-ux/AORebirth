# Ofab Victory Point shop

Playfield 655 (Andromeda / ICC), near the Unicorn outpost landmark (3124 x 905). The row starts at 3080, 35, 880. Y 35 is the ICC buffbot height, not a measured floor. Walk it and correct `PlayfieldContent/655/Npcs.json` if the terminals are in a wall.

## Where VP comes from

Stat 669.

- Invasion and city-raid bosses: `max(1, level / 10) * 5`.
- Team alien mothership missions (ZXTE, ZXT1, ZXTH) pay the same formula from the mission's objective level, written onto the quest reward when the mission is accepted. Solo ships (ZXKE, ZXK1, ZXKH) do not pay VP. A mission already in the log keeps the reward it was given.

A level 200 team ship is about 100 VP. A QL 300 Ofab piece is 2250 VP, so about 23 of those ships. That is steep. The formula is a house rule.

## Prices

Retail VP prices are not in `shopinventorytemplates`, and the AO-Universe shopping guide and the wiki pages that answer did not list a number. House rule for every stocked line: `max(1, floor(QL * QL / 40))`.

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

The shop packet only carries low id, high id, and QL. Sell modifier 0 is there so the pane shows 0 credits. The server charges the Price field as stat 669. If the client still blocks the buy on the item's credit value, the pane cannot be used and the fallback is a chat menu.

## What is stocked

Fourteen profession terminals, six pieces, QL 1, 25, 50, 100, 150, 200, 250, 300. Ids are the base QL 1-300 pairs from `itemnames` (Adventurer body 264202/264203 checked). Piece names follow the item: Bureaucrat Vest and Headgear, Enforcer Breastplate and Gauntlets, Metaphysicist and Shade Headgear, Doctor Body. Skipped: Ofab Agent Armor (one id, 266578), Improved, Penultimate, Special, Replica, Protective, Shoulder.

Melee terminal: Bear, Mongoose, Panther, Viper, Wolf, Mk 1 through Mk 6, QL 100 and QL 300. Each mark is its own 7-id family (Bear Mk 1 is 265237-265243).

Ranged terminal: Cobra, Hawk, Peregrine, Shark, Silverback, Tiger, Mk 1 through Mk 6, QL 300 only. QL 100 as well would be 72 lines. A shop pane that stops at 64 would hide the rest. Silverback Mk 2 starts at 264864, not the next id after Mk 1.

## Not identified

- **SMG Peregrine.** No `itemnames` row named SMG Peregrine, Peregrine SMG, or Ofab SMG. Not stocked.
- **Ofab nano programs** for "Ranged & Support" and "Melee & Hybrid". The rows named Ofab Nano Vendor (266313) and Ofab Nano-Technician Vendor (266572) are the terminal items, not the programs. The two nano terminals on 655 talk, and they do not sell. Name the programs and they can be added.
- Vehicles and orbital strikes. Skipped. They are not real stock on this server.

## Battle Stations

Not built.

A Battle Station match is a scored fight: two sides, objectives, a clock, and a VP payout. This server can drop alien waves on pads (`InvasionService`). It does not have a bot opponent, a side score, or a Battle Station playfield wired up. A bot side would need a brain that moves, shoots, and plays the objective, plus a match instance so two teams are not standing in ICC. That is a new system. The wave spawner is the only piece that already exists, and it is not a match.
