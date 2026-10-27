# Elite, champion, and uber mercenaries

Research date: 2026-10-26 (updated after Randy's post-bio tier design). The hireable companions are the fourteen pet mercs in `Mercs.cs` / `Mercs.json` / `Build-Mercs.ps1`. This is not the retail mercenary-camp loot table.

## How a merc is built today

`PetService.HireMerc` spawns the profession hash (MRSO and the rest) into a pet slot at 10 and up. The level passed in is the owner's level clamped to 1–220. Template bands supply health and the body. `FillEquipment` then mints a monster weapon (item 144742–144743 when the template lists none) at that same level. That weapon is what `DamageCalculator` swings. The weapon from `Mercs.TierWeaponFor` / `Mercs.json` is put in the hand by `DressMerc` and is a look only. `NpcCharacter` says so: the monster weapon rolls the damage.

Nanos come from `Mercs.json` bands (profession spells). Doctor, Adventurer, and Metaphysicist use the doctor heal line. Soldier and Enforcer upload their combat programs. `NanoRuntime` skips skill checks and nano cost for any pet. Merc pets (MP / engi / crat) spawn at `PowerLevel`. Contracts are bit flags in server stat 10280; tier bits in 10284.

## Body near the owner; tiers are twinked

Randy's rule: keep mercs near the owner's level. Tiers are **twinked**, never level-bumped.

1. Body level = min(owner level, 220). No tier bonus on the level.
2. Gear QL = min(300, body + tier `GearQlBonus`). Armor and the monster weapon mint at that QL.
3. Fixed package on top: flat health, flat min/max damage, flat add to abilities / AC / weapon and nano skills.
4. Merc pets and nano bands use min(220, body + `PowerLevelBonus`).

## Tiers

| Tier | Gear | Weapon (look) | Gear QL over body (cap 300) | Pet/nano over body (cap 220) | Health / damage / skills |
| --- | --- | --- | --- | --- | --- |
| Basic | Mercs.json bands | Mercs.json mesh | 0 | 0 | none |
| Elite | Full Penultimate Ofab set for that profession | Ofab Mk 6 (profession animal) | +80 | +80 | +800 / +40 / +200 |
| Champion | Combined Alien armor (family mapped to profession) | Kyr'Ozch typed weapon | **+180** | +120 | +2000 / +90 / +400 |
| Uber | Beast helm / shoulders / back + Combined Alien body | Ofab Mk 6 | +220 | +160 | +4000 / +160 / +700 |

Stored as two bits per profession in server stat 10284. Display name for value 3 is **Uber** (`super` still parses as an alias). The terminal and `{hiremerc}` still sign a basic contract. `.merc hire <prof> elite|champion|uber` is GM level 1 until a drop is chosen.

### Champion: higher QL, not a damage hack

Alien (Combined) gear is weaker than Ofab. Fix chosen: **raise Champion `GearQlBonus` to +180** (was +140). Same mint path as every other piece and the monster weapon — no special weapon-damage branch. Higher QL is the robust lever because armor AC/stats and the swing weapon all scale together.

## Gear IDs (verified in items.dat)

### Elite — Penultimate Ofab (6 pieces each; low–high QL pairs)

| Profession | Helmet | Body | Sleeves | Gloves | Pants | Boots |
| --- | --- | --- | --- | --- | --- | --- |
| Adventurer | 264184–85 | 264178–79 | 264180–81 | 264182–83 | 264174–75 | 264176–77 |
| Agent | 264283–84 | 264301–02 | 264295–96 | 264289–90 | 264313–14 | 264307–08 |
| Bureaucrat | 264499–500 | 264517–18 | 264511–12 | 264505–06 | 264529–30 | 264523–24 |
| Doctor | 264646–47 | 264664–65 | 264658–59 | 264652–53 | 264676–77 | 264670–71 |
| Enforcer | 264245–46 | 264227–28 | 264233–34 | 264239–40 | 264215–16 | 264221–22 |
| Engineer | 264571–72 | 264592–93 | 264583–84 | 264577–78 | 264604–05 | 264598–99 |
| Fixer | 264463–64 | 264481–82 | 264475–76 | 264469–70 | 264493–94 | 264487–88 |
| Keeper | 264610–11 | 264628–29 | 264622–23 | 264616–17 | 264640–41 | 264634–35 |
| Martial Artist | 264319–20 | 264337–38 | 264331–32 | 264325–26 | 264349–50 | 264343–44 |
| Metaphysicist | 264355–56 | 264373–74 | 264367–68 | 264361–62 | 264385–86 | 264379–80 |
| Nano Technician | 264391–92 | 264409–10 | 264403–04 | 264397–98 | 264421–22 | 264415–16 |
| Shade | 264535–36 | 264553–54 | 264547–48 | 264541–42 | 264565–66 | 264559–60 |
| Soldier | 264427–28 | 264445–46 | 264439–40 | 264433–34 | 264457–58 | 264451–52 |
| Trader | 264247–48 | 264265–66 | 264259–60 | 264253–54 | 264271–72 | 264277–78 |

Elite / Uber Mk 6 Ofab weapons: Soldier Shark 265097–103, Agent Peregrine 265055–61, Enforcer Bear 265272–78, MA Mongoose 264929–35, Adventurer Tiger 265139–45, Fixer Silverback 264892–98, Keeper Wolf 265230–36, Shade Panther 264971–77, Engineer/casters Cobra 265314–20.

### Champion — Combined Alien armor

Only six Combined families exist. Mapping: Soldier→Commando, Enforcer/MA/Shade→Mercenary, Doctor→Paramedic, Adventurer/Fixer→Scout, Agent→Sharpshooter, everyone else→Officer. Piece IDs are in `Mercs.Combined*` (246631–246704 range). Weapons: Kyr'Ozch typed lines (Carbine Type 13, Rifle Type 3, Sledge Type 76, Nunchacko Type 48, Axe Type 240, SMG Type 12, Grenade Type 1, Spear Type 112, Energy Sword Type 76, Energy Pistol Type 5, Pistol Type 4) — see `Mercs.KyrAlienWeapon`.

### Uber — Beast + Alien

| Slot | Item | ID |
| --- | --- | --- |
| Helmet | Awakened Helmet of Hypocrisy | 302728 |
| Shoulders | Awakened Shoulderplates of Sabotage | 302725 |
| Back | Awakened Burden of Competence | 302730 |
| Body / sleeves / gloves / legs / feet | Combined Alien (same family map as Champion, no Combined head) | 246631–246704 |

Base (non-Awakened) Beast pieces also exist: 244716 / 244713 / 244718. Uber uses Awakened.

### Not found in this items.dat

- Soft Cap / Hard Cap of the Beast, Soft/Hard Shoulderpad of the Beast, and similar "of the Beast" alien-named shoulders/helmets (string scan of items.dat returned none). Pandemonium Beast armor under the Hypocrisy / Sabotage / Competence names is what we use instead.
- Profession-specific Combined sets beyond the six families above (no Combined NT / Keeper / etc. templates).

## How a player would get one

Still GM-granted for elite/champion/uber (`.merc hire …`). Options for later: nodrop contract items from ALGT / invasion general, or a steep credits price. Recommendation unchanged: GM until a raid drop is wired.

## Commands

See `ddogs/docs/merc-commands.txt` for every hire / dismiss / list line.

## Restart

ZoneEngine_New must be restarted to load the new gear tables. No database change. Existing basic contracts stay basic. Stored tier value 3 that used to display as "Super" now displays as "Uber".
