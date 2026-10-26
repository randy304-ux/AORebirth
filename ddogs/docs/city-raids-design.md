# Org cities and alien city raids

Research date: 2026-10-26. Sources: AO-Universe "Alien Raiding in your City" (updated 2024-08-05) and "Player City Building Guide" (updated 2026-10-01), plus the local ZoneEngine_New tree and client RDB notes already in `tools-temp/rdbscan2/mesh.txt`. Retail numbers below are from those guides. House-rule numbers are marked.

## What the retail guides describe

An org city is an instanced plot (Playa Del Desierto, Montroyal, or Serenity Island), entered from the upper floor of ICC transportation. The City Controller is the heart of it. Aliens attack the controller, not the players, and they only count org members who are outside buildings.

- Controller charge must be over 50% to be attacked, and the attack ends if charge falls under 10% (no outside general, no boarding).
- Cloak / ECM on: about one attack per 7 days. Off, or no cloak: about one per 3 days. Turning the cloak off guarantees an attack within 5 minutes, then the switch locks for an hour.
- Waves scale to org members present. Guests do not add aliens. A Hacker'Uri is the controller-killer each wave. The outside general lands after about 20 minutes, sooner if the aliens cannot win, and never over level 300.
- Killing the general can invite boarding teams (4 to 18 players, up to 6 ship instances) onto the mothership. Two boss rooms: mini-boss waves then a Fleet Admiral, or cocoons and a Recruitment Director. A pilot or technician timer destroys the ship if the room is not cleared.
- CRUs (Controller Recompiler Units) recharge the controller. QL 50 restores 50% charge. Anyone can use one.
- City buildings are buff contracts, not just scenery. At QL 300 for a level 200 character the guide lists: HQ (+9 all abilities, +90 run speed, plus a lab and a grid), Grid House (+54 computer literacy), Notum Mining (+36 nano skills), Notum Silo (+36 nano delta, -6% nano cost, 0% suppression inside), Landing Pad (+54 vehicle and map nav), Pool (+54 body dev and swimming), Guard House (+18 heal delta), Satellite Uplink (5-minute attack warning), ECM/cloak (attack frequency), Sauna (Relaxed State XP buff). Cafe and Sky Bar are social. Markets are dead content.
- Victory Point clumps (Cell Templates, Mitochondria Samples, Plasmid Cultures, Protein Mapping Data) are a separate alien-playfield drop, not the city-raid weapon loot. City and ship bosses drop Kyr'Ozch typed weapons and typed bio-material. Alien XP and 30 titles are their own track.

## What this server already has

- Organizations: `organizations` rows (name, leader, government, description, objective, history) and Orgbot (`.org`). `.org raid` is an in-memory name list for a player raid. It is not an alien attack.
- Alien XP and alien level are real (`Character.cs`). Stat id 669 exists in `AORebirth.Stats` as Victory Points and nothing writes it.
- Notum Wars is an expansion bit only. No tower fields, no control towers.
- The client protocol has `IdentityType.CityController` and a `CityControllerWindowClose` message, and the messaging tests describe an owner / non-org menu. ZoneEngine_New has no handler, no window, and no city dynel.
- Alien bodies and loot from the crashed-ship work: KSAU, KGRU, KCIU, KGXH, ZIXK, ALHT, ALGT. `Build-AlienRaidLoot.ps1` already puts Kyr'Ozch typed weapons, weapon-special bio-material, pristine/mutated bio, and one bio armor piece on ALGT. The loot roller sets QL from the NPC level, so a low-level general already drops low-QL raid loot. VP clumps were left out on purpose because nothing spends VP.
- `BossArenas.json` stores six wave pads for style 382 room 4. Nothing spawned on them.
- One ZoneEngine process hosts many playfields (`PlayfieldManager`). A chat line can be sent to every player on a loaded playfield. That is the server-wide warning.

## What is not feasible yet

- The real city-controller window, cloak switch, rent, CRU use-on-controller, building placement, and the three instanced city maps. The client UI exists only as an identity type and a close message. There is no captured window layout and no plot geometry wired in this tree. Building that is a protocol project, not a content pass.
- Aliens pathing to a controller and damaging its charge. There is no controller dynel to hate.
- Boarding lottery, six instanced motherships, and the pilot self-destruct timer. The authored-dungeon path used by Karli's ship is the right base later. It is not the capital-city event.
- A mothership hanging in the sky as retail does. The RDB scan shows the strings "alien boss mothership" (resource 263884) and "cheetah alien boss mothership" (302418). MonsterData 263884 (cat mesh 263879) and 302418 (cat mesh 302442) exist, both with move-bit set, radius 5, mass 10000. They are small creature records, not a known overhead prop. NPC spawns are snapped to the floor. `.invasion start <city> overhead` still tries: it spawns a brainless KSAU, swaps MonsterData to 263884, strips the move bit, and lifts it 80 m. Expect it to be wrong until someone looks at it in game. It is off unless you ask for it.
- Old Athen, Omni-1, and ICC have no walked pads, so they are not in the city list.

## What was built

A generic wave encounter in `ZoneEngine_New/Core/Raids/InvasionService.cs`, advanced from the playfield tick.

- Pads, a wave list, and a radius. Trash counts are `base + (players in radius - 1)`, capped at twice the base. Boss and healing towers stay at the authored count (`Fixed` in the JSON).
- Level is the average level of living players inside the radius, each contributor clamped to 220. A level 1000 GM does not pull a level 1000 wave. Nobody in range: level 25, so an empty test is not a max-level spawn.
- A wave advances when it is dead and 20 seconds have passed, or at 75 seconds even if something is still up. Then the next wave. After the last wave, leftovers despawn in 45 seconds. Corpses stay so ALGT loot can be picked up.
- ALHT rows heal the boss of that wave (3% of max health every 4 seconds), same hook the ship towers already use.
- Killing the boss writes stat 669. House rule: `max(1, level / 10) * 5` Victory Points, told in chat. Nothing spends them.
- The same `TryStart(playfield, title, pads, overhead)` is what a future city controller and the full ship raid should call. `.invasion ship` already feeds it the six `BossArenas.json` pads, translated into the boss room. It does not auto-start on every ship run.

GM commands (also accepted with `/` because chat strips both):

- `.invasion start tir|borealis|rome-red|rome-blue|rome-green [overhead]`
- aliases: clan, bor, neutral, rr, rome, rb, blue, rg, green
- `.invasion here [overhead]` — four pads around your feet
- `.invasion ship` — inside a generated ship, on the saved pads
- `.invasion stop [city|all]`
- `.invasion status` and `.invasion cities`

Pads are in `GameData/Invasions.json`. They are about 40 m off AOWiki landmarks (Tir grid 542×536, Rome Red grid 250×320, Borealis between the grid and the whompas, Rome Blue and Green guessed from shop and whompah points). The next `.invasion start` re-reads the file. Walk them with `/pos` and correct the numbers. A bad pad lands aliens on a roof or in a wall; the spawn snaps to the floor under the point.

Low-level city loot is the existing ALGT table at the wave's level. A level 30 invasion general drops low-QL Kyr'Ozch weapons and bio-material. No second item set was added.

## Still to build

1. Walk and fix the five cities' pads. Add Old Athen, Omni-1 HQ / Entertainment / Trade, and an ICC plaza once someone stands there and runs `/pos`.
2. Decide whether stat 669 should show on the client, and what spends it (a vendor, or a direct bio-material trade). Then add the VP clump items the sector guides describe.
3. Org city plots: a data file of buildings and the buff totals from the guide, applied as a real buff while the character is in the city. That can land before the client window exists. The window, cloak UI, rent, and CRUs wait on a capture of the controller panel.
4. Controller charge and "aliens attack the controller". Needs a target dynel and a hate rule. Not started.
5. Full mothership boarding: difficulty, team lock, pilot timer, the two boss-room scripts. The wave spawner and the six pads are the piece that was missing. Wire `.invasion ship`'s call into the boss-room entry when that raid is designed, instead of only the GM command.
6. Confirm or throw out the overhead mesh after one look in game.

## Restart

ZoneEngine_New must be restarted before `.invasion` exists. Login, Chat, and the database do not need a restart. No schema change.
