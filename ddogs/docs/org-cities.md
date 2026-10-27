# Org cities

House rule: plot cost is 0 and upkeep is 0. Retail prices are in `GameData/OrgCities.json` and are not charged. `.city` and the Tower Shop on Andromeda (655) do the same thing. The president claims and buys. Any member can enter their own city.

## Plots

Instanced maps, one org each. They are the real playfields, not a private copy per org. A second org needs the first to `.city release`.

| Key | Map | Playfield | Units | Largest building |
| --- | --- | --- | --- | --- |
| desierto | Playa del Desierto | 5001 | 464 | 225 (15x15) |
| montroyal | Montroyal | 5002 | 2270 | 400 (20x20) |
| serenity | Serenity Island | 6010 | 2386 | 400 |

6010 is Serenity Islands. GameData has no separate org-city template under that name. Landings are map centers (Y is a guess). Walk them.

Open-world plots use the same small budget as Desierto, inside an 80 m radius. Borealis, Rome Red, and Tir, on the invasion-pad guesses. Buffs apply only inside that radius. On an instanced map they apply anywhere on the playfield.

## Buildings

Premade, price 0. Headquarters first (one HQ). Large HQ is 20x20 and does not fit a small plot. The pool is 20x15 and does not fit a small plot. Guide numbers are for QL 300 at level 200. The server scales by QL/300 and level/200, and the level stops at 220. Small HQ is QL 100 of the same contract, so about a third.

HQ: +9 all abilities, +90 run speed. Grid House: +54 computer literacy. Notum Mining: +36 on the five nano skills. Notum Silo: +36 nano delta and -6 nano cost. Landing pad: +54 water/air/ground vehicle and map nav. Pool: +54 body dev and swimming. Guard house: +18 heal delta. Sauna: +11 XP modifier. Cafe and sky bar are social and have no stat. Satellite uplink and ECM have no stat. Uplink adds a warning line when a raid is called. ECM only notes that a called raid dropped the cloak.

The HQ lab tradeskill room is not detected, so those bonuses are not applied. Markets are not sold. The sauna is sold here; retail Tower Shops did not sell it.

Buffs hit org members in the city. They are stat bonuses on the next rebase, not NCU icons. Guests do not get them.

## Controller raid

`.city raid` (president, or a GM with a plot name) starts the existing alien waves on four pads around the landing. The wave is level 30, so ALGT loot is the low QL Kyr'Ozch set. The boss still pays Victory Points. Aliens do not walk to the controller or drain a charge. There is no cloak timer and no boarding.

A City Controller stands on each plot. Y and the exact tile still need a walk.
