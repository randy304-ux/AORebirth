# Elite, champion, and super mercenaries

Research date: 2026-10-26. The hireable companions are the fourteen pet mercs in `Mercs.cs` / `Mercs.json` / `Build-Mercs.ps1` (Doctor, Enforcer, Soldier, Keeper, and the other ten). This is not the retail mercenary-camp loot table.

## How a merc is built today

`PetService.HireMerc` spawns the profession hash (MRSO and the rest) into a pet slot at 10 and up. The level passed in is the owner's level clamped to 1–220. Template bands supply health and the body. `FillEquipment` then mints a monster weapon (item 144742–144743 when the template lists none) at that same level. That weapon is what `DamageCalculator` swings. The weapon in `Mercs.json` (soldier 124348, and one per profession) is put in the hand by `DressMerc` and is a look only. `NpcCharacter` says so: the monster weapon rolls the damage.

Nanos come from `Mercs.json` bands. Only Doctor, Adventurer, and Metaphysicist have any, and they are the doctor heal line (26465, 26466, 28665, 28676). Soldier, Enforcer, and the rest have an empty nano list, so they do not taunt or nuke.

`NanoRuntime` skips skill checks and nano cost for any pet, mercs included (comment dated 2026-10-17: the templates have no nano skills, and the gate was refusing every heal). A merc can cast whatever is in its uploaded list, at any owner level. It cannot cast a nano that was never added to the list.

Merc pets (MP attack / mezz / heal, engineer robot, crat droid) are spawned at the merc's level via `EnsureMercPets`. Over-equip does not apply: mercs are summoned with an empty requirement list, so `RefreshOverEquip` never flags them. Real cast pets go over-equipped when the owner's skills fall under 80% of the summon requirements. Mercs are not on that path.

Contracts are bit flags in server stat 10280 and survive logout. Revive is 10 seconds.

## Can they inherit a percent of the owner?

Not with the current pet code, and it is the wrong model for this server.

- Nothing in `PetService` copies owner skills onto the pet. The only owner stat copied is side, plus run speed so the pet keeps up.
- Retail cast pets are not "10% of the caster" either. The caster's nano skills pick the QL of the pet program, and the pet is then a fixed creature of that QL. The scaling happens once, at cast, by choosing a level.
- Merc swings do not read the owner's attack skills. They read the monster weapon minted at the merc's level. Adding 10% of the owner's rifle skill would not change the swing unless combat was rewritten.
- Merc heals and buffs do not read nano skills either, because pets skip that gate. Inheritance would not unlock a stronger heal. Putting a higher nano id in the list would.
- A level 1000 GM is the failure case. `HireMerc` already clamps the body to 220, but the GM's skills and health are far off that curve. A percent of those stats makes a merc that one-shots the alien waves and does not match any template band. Capping the inherited stats at a level-220 character is just a fixed package with extra steps.

## Recommendation

Hybrid, with the fixed package doing the work.

1. Body level = min(owner level, 220) + a tier bonus, still capped at 220. That one number raises the template health, the monster-weapon QL, the armor band, the nano band, and the merc's own pets together. This is the "twink" knob: a level 60 elite is a level 100 merc wearing level-100 armor and swinging a level-100 monster weapon, with an MP pet at 100.
2. A small extra health and character-damage percent on top, so a tier still matters when the owner is already 220 and the level bonus has nowhere to go. A level 1000 GM gets a level 220 merc of that tier, not a level 1000 merc and not a slice of GM stats.
3. Do not inherit owner skills. If a later tier needs a real high-QL gun's damage (a Perennium blaster, not the look-only hand item), mint that item as the monster weapon. Do not percent-copy the owner.

The held weapon staying a look is a limitation. Soldier 124348 is whatever `Build-Mercs.ps1` picked as a low-QL mesh, worn at the boosted level. It is not a named Perennium blaster. Swapping the mesh is a content edit once the item id is confirmed. It still will not change the swing until it is the monster weapon.

Soldier and enforcer still have no taunt nano. Casting "above level" already works for any nano we put on the list. The elite prototype does not invent taunt ids. Healer mercs do move up the existing heal bands because their level moved up.

## Tiers

| Tier | Level bonus | Extra health and damage | Example at owner level 60 |
| --- | --- | --- | --- |
| Basic | 0 | none | Level 60, same as before |
| Elite | +40 | 125% | Level 100 |
| Champion | +80 | 150% | Level 140 |
| Super | +120 | 175% | Level 180 |

All four cap at 220. Stored as two bits per profession in server stat 10284 (10281–10283 are aura unlocks). The bit survives logout with the contract. The terminal and `{hiremerc}` still sign a basic contract.

## How a player would get one

Not decided. The prototype is GM-only on purpose: `.merc hire soldier elite` requires GM level 1. A normal `.merc hire soldier` is unchanged.

Options that fit a ten-person server, in the order that is least work:

1. Leave them GM-granted. Randy hands an elite contract to someone who earned it.
2. A nodrop contract item (one profession, one tier) that calls the same `HireMerc(..., tier)`. Drop it from ALGT at a low chance, higher tiers from higher raid levels. No new combat code.
3. A credits price at the contract terminal that steps up hard (basic free, elite expensive, champion and super not sold). Easy to regret, because credits are easy here.

Recommendation: (1) until a raid is actually being run, then (2) tied to the invasion general so the rare merc and the city raid are the same carrot. Do not put champion and super on a vendor.

## What was built

- `Mercs.Tier` and stat 10284.
- `.merc hire <profession> basic|elite|champion|super`. Above basic requires GM level 1.
- The spawned merc's level is `GearLevel`. Name gains " (Elite)" and the rest. Health and the character min/max damage stats are scaled after dress. The monster weapon is minted at the new level, which is the swing that matters.
- Revive and login bring the same tier back. Dismiss clears it.
- MP / engineer / crat pets spawn at that raised level, so an elite MP's pet is 40 levels above the owner, capped at 220.

## Still to build

- A real obtainment item, once Randy picks option 2 or stays on GM grants.
- Taunt, nuke, and buff nano ids per profession, in `Mercs.json` bands, if those mercs should cast. The cast gate is already open.
- Optional: replace the look-only hand item with a high-QL mesh (Perennium blaster for the soldier) after the id is confirmed. Damage will not change until that item is the monster weapon.
- Do not let owner level 1000 leak into the merc. The cap is the point.

## Restart

ZoneEngine_New must be restarted. No database change. Existing basic contracts stay basic (tier bits default to 0).
