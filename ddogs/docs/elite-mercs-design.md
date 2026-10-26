# Elite, champion, and super mercenaries

Research date: 2026-10-26. The hireable companions are the fourteen pet mercs in `Mercs.cs` / `Mercs.json` / `Build-Mercs.ps1` (Doctor, Enforcer, Soldier, Keeper, and the other ten). This is not the retail mercenary-camp loot table.

## How a merc is built today

`PetService.HireMerc` spawns the profession hash (MRSO and the rest) into a pet slot at 10 and up. The level passed in is the owner's level clamped to 1–220. Template bands supply health and the body. `FillEquipment` then mints a monster weapon (item 144742–144743 when the template lists none) at that same level. That weapon is what `DamageCalculator` swings. The weapon in `Mercs.json` (soldier 124348, and one per profession) is put in the hand by `DressMerc` and is a look only. `NpcCharacter` says so: the monster weapon rolls the damage.

Nanos come from `Mercs.json` bands. Doctor, Adventurer, and Metaphysicist use the doctor heal line (26465, 26466, 28665, 28676). Soldier now also uploads Fling Shot 82208, Full Auto 82209, Burst 82211, Aimed Shot 82215, and Taunt 227152. Enforcer uploads Brawl 82210, Channel Rage 211753, Anger Management 261239, and the same taunt. The shot and brawl programs are single catalog ids. Taunt 227152/227153 is a QL 1–500 pair.

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

The body stays at the owner's level. Randy does not want a level 20 walking with a level 100 merc. Rarity twinks the same body.

1. Body level = min(owner level, 220). No tier bonus on the level. A level 1000 GM still gets a level 220 body, not a slice of GM stats.
2. Gear QL = min(300, body + a tier bonus). Armor is chosen from the higher band and minted at that QL. The monster weapon (144742–144743, which is what the swing uses) is minted again at that QL, because item quality cannot be changed after create. A soldier above basic holds a real Perennium Blaster (246422 QL 50 through 246428 QL 200) instead of mesh 124348.
3. A fixed package on top: flat health, flat min/max damage, and a flat add to abilities, AC, weapon skills, and nano skills. Not a percent of the owner.
4. Merc pets (MP, engineer, crat) and the nano band use min(220, body + a second bonus), so the pet and the heals sit well above the body. Where a nano is a two-id QL pair, the cast is resolved at the gear QL. Single-id programs stay at the catalog snapshot.

## Tiers

| Tier | Gear QL over body (cap 300) | Pet and nano level over body (cap 220) | Health / damage / skills | Level 20 body | Level 60 body |
| --- | --- | --- | --- | --- | --- |
| Basic | 0 | 0 | none | level 20, unchanged | level 60, unchanged |
| Elite | +80 | +80 | +800 / +40 / +200 | gear QL 100, pet 100 | gear QL 140, pet 140 |
| Champion | +140 | +120 | +2000 / +90 / +400 | gear QL 160, pet 140 | gear QL 200, pet 180 |
| Super | +220 | +160 | +4000 / +160 / +700 | gear QL 240, pet 180 | gear QL 280, pet 220 |

The 220 cap is only the body and the pet/nano level. Gear QL stops at 300. Stored as two bits per profession in server stat 10284 (10281–10283 are aura unlocks). The bit survives logout with the contract. The terminal and `{hiremerc}` still sign a basic contract.

## How a player would get one

Not decided. The prototype is GM-only on purpose: `.merc hire soldier elite` requires GM level 1. A normal `.merc hire soldier` is unchanged.

Options that fit a ten-person server, in the order that is least work:

1. Leave them GM-granted. Randy hands an elite contract to someone who earned it.
2. A nodrop contract item (one profession, one tier) that calls the same `HireMerc(..., tier)`. Drop it from ALGT at a low chance, higher tiers from higher raid levels. No new combat code.
3. A credits price at the contract terminal that steps up hard (basic free, elite expensive, champion and super not sold). Easy to regret, because credits are easy here.

Recommendation: (1) until a raid is actually being run, then (2) tied to the invasion general so the rare merc and the city raid are the same carrot. Do not put champion and super on a vendor.

## What was built

- `Mercs.Tier` and stat 10284. The first cut raised the body level. That is replaced: the body is `BodyLevel` (owner, cap 220) for every tier.
- `.merc hire <profession> basic|elite|champion|super`. Above basic requires GM level 1. A basic hire does not take the package, does not swap the soldier's gun, and does not set a cast QL.
- Above basic, the name gains " (Elite)" and the rest. Armor band and nano band use `PowerLevel`. Pieces are minted at `GearQuality`. The monster weapon is removed and minted again at that QL. The flat package is added to base stats and then rebased so armor still stacks.
- A soldier above basic wears Perennium Blaster 246422–246428 at the gear QL. The blaster is the hand item (and the range source when it counts as a real weapon). The swing is still the monster weapon at the gear QL.
- Soldier and enforcer upload the nanos listed above. `NanoCastQuality` is the gear QL, so Taunt 227152/227153 interpolates. Fling Shot, Full Auto, Burst, Aimed Shot, Brawl, Channel Rage, and Anger Management are single ids, so their strength is the catalog row.
- Revive and login bring the same tier back at the owner's current level. Dismiss clears the tier. MP / engineer / crat pets spawn at `PowerLevel`.

## Still to build

- A real obtainment item, once Randy picks option 2 or stays on GM grants.
- QL ladders for the single-id nukes, if a second id for those programs turns up. Until then they do not grow with the tier.
- Do not let owner level 1000 leak into the merc. The body cap is the point.

## Restart

ZoneEngine_New must be restarted. No database change. Existing basic contracts stay basic (tier bits default to 0).
