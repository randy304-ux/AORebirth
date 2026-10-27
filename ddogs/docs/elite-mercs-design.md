# Elite, champion, and uber mercenaries

Research date: 2026-10-26 (updated after Randy's post-bio tier design). The hireable companions are the fourteen pet mercs in `Mercs.cs` / `Mercs.json` / `Build-Mercs.ps1`. This is not the retail mercenary-camp loot table.

## How a merc is built today

`PetService.HireMerc` spawns the profession hash (MRSO and the rest) into a pet slot at 10 and up. The level passed in is the owner's level clamped to 1–220. Template bands supply health and the body. `FillEquipment` then mints a monster weapon (item 144742–144743 when the template lists none) at that same level. That weapon is what `DamageCalculator` swings. The weapon from `Mercs.TierWeaponFor` / `Mercs.json` is put in the hand by `DressMerc` and is a look only. `NpcCharacter` says so: the monster weapon rolls the damage.

Nanos come from `Mercs.TierNanosFor` (own-profession spells, buffs, and specials). Lists are cumulative: basic &lt; elite &lt; champion &lt; uber. Ofab crystal item IDs resolve to castable nano programs via OnUse 53019 (verified in items.dat). `NanoRuntime` skips skill checks and nano cost for any pet. Merc pets (MP / engi / crat) spawn at `PowerLevel`. Contracts are bit flags in server stat 10280; tier bits in 10284.

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
| Uber | Beast helm / shoulders / back + Combined Alien body | Ofab Mk 6 | +220 | +160 | **+5500 / +220 / +1000** (largest) |

Uber is clearly the best tier: strongest gear mix, full cumulative nano list, and the largest flat stat package.

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

## Profession spells / buffs / specials by tier

Every merc uploads **its own profession** nanos only (`Mercs.TierNanosFor`). Higher tiers keep everything from lower tiers and add stronger programs. IDs below are castable nano programs (not crystal items).

### Soldier

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Fling Shot 82208, Full Auto 82209, Burst 82211, Aimed Shot 82215, Taunt 227152 |
| Elite | Pierce Nerves 266310, Remedy Inhibitor 267624, Adrenaline Rush 301897 |
| Champion | Fragmentation Grenades 302395 |
| Uber | Notum-Charged Grenades 302399 |

### Enforcer

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Brawl 82210, Channel Rage 211753, Anger Management 261239, Taunt 227152, Mongo Crush! 100195 |
| Elite | Slowdown 266304, You are Next 279347, Charge! 287753 |
| Champion | Element of Impact 301853 |
| Uber | Mongo Break! 301291 |

### Doctor

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Minor Healing 26465, Healing 26466, Team Healing 42405 |
| Elite | Improved Healing 28665, Superior Healing 28676, Fill Inbox 266303, Weak Mutagenic Plague 267649, Tjernberg Soothing Adrenaline 279372 |
| Champion | Complete Healing 28650, Dr Blaze's Mutagenic Compound 267620, Rapid Palsy 301845 |
| Uber | Nanite Enhanced Mutagenic Plague 267623, Nanite Enhanced Mutagenic Compound 267621 |

### Adventurer

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Fling Shot 82208, Aimed Shot 82215, Minor Healing 26465, Healing 26466 |
| Elite | Improved Healing 28665, Misdiagnosis 266306, A Return to Form 301835, Boon of the Enigma 302223 |
| Champion | Superior Healing 28676, Lycanthropic Dexterity 302235, Squeaking from the Darkness 302229, Kin of the Tarasque 302217 |
| Uber | Complete Healing 28650, Claws of the Magal 302243 |

### Agent

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Aimed Shot 82215, Sneak Attack 129082, Fling Shot 82208 |
| Elite | Technical Incompetence 266307, Burst 82211 |
| Champion | Two Left Feet 301838 |
| Uber | Full Auto 82209 |

### Bureaucrat

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Junior Last Minute Negotiations 267538, Weekend Volunteer 279340, Lesser Corporate Insurance Policy 267603 |
| Elite | Last Minute Negotiations 267535, Corporate Insurance Policy 267604, Corporate Strategy 267611, Rabies 266305 |
| Champion | Greater Corporate Insurance Policy 267605, Improved Red Tape 267311, Intensify Stress 222687, Nanite Robot Protection 267612 |
| Uber | Basic Nanite Robot Protection 267616, Droid Damage Matrix 267916, Droid Pressure Matrix 302247, Wasteful Marksmanship 302154, Take the Bullet 267917 |

### Engineer

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Unsteady Hands 266309, Lesser Software Hacking Shielding 267606 |
| Elite | Software Hacking Shielding 267607, Sedative Injectors 302254 |
| Champion | Advanced Software Hacking Shielding 267608, Jamming Tower 274369 |
| Uber | Predator M-30 301855 |

### Fixer

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Limited Grid Jump 93125, Fling Shot 82208, Burst 82211 |
| Elite | Blindside 266308, Wake Up Call 279374, Experienced Survivor 279376 |
| Champion | Communication Disruption 274373, Expeditious Evacuation 301861, Vanish into the Digital Void 301859 |
| Uber | NCU Vulnerability Exploitation 302412, Restock Ammo (Level OP-CCXL) 301867, Partial Grid Jump 93127 |

### Keeper

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Courage Of The Just 279378, Blow Their Cover 266301 |
| Elite | Clarion Call 301153 |
| Champion | Punisher of the Wicked 301602 |
| Uber | Rebirth 301117 |

### Martial Artist

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Brawl 82210, Dimach 82213, Fast Attack 82214 |
| Elite | Induce Stupor 266311, Zazen Stance 287559, Footsteps of The Master 279345 |
| Champion | Irrational Grudge 301936, Team Flourishing Heal 301493 |
| Uber | Mongo Crush! 100195 |

### Metaphysicist

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Lesser Beneficial Scourge 267275, Lesser Hostility Scourge 267272, Nano Division 266299 |
| Elite | Beneficial Scourge 267269, Hostility Scourge 267271, Evocation of The Enraged 267598 |
| Champion | Evocation of The Empowered 267600, Evocation of The Reinforced 267601, Evocation of The Enlightened 267599, Induced Apathy 301888 |
| Uber | Evocation of The Pure 267609, Evocation of The Cleansed 267617, Eye of the Tigress 302257, Sacrificial Bond 300506, Sacrificial Shielding 267281, Nanite Enhanced Nano Shutdown 267283 |

### Nano-Technician

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Izgimmer's Little Nuke 28619, Nullity Sphere 150501, Restrain Enthusiasm 266302 |
| Elite | Explosive Cataclysm 266298, Mastablasta's Cataclysm 266293, Future Vision 266292 |
| Champion | Earthshattering Cataclysm 266297, Notum Cataclysm 266296, Enfraam's Cataclysm 266294, Izgimmer's Blessing 302274 |
| Uber | Izgimmer's Ultimatum 218168, Izgimmer's Tactical Nuke 266287, Nanobot Aegis 302074, Impending Demise 266289 |

### Shade

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Sneak Attack 129082, Fast Attack 82214, Dimach 82213 |
| Elite | Shade's Caress 266300 |
| Champion | Winding Serpent 301593 |
| Uber | Dissolving Vitality 301895 |

### Trader

| Tier | Adds (cumulative) |
| --- | --- |
| Basic | Weak Corporate Protection 301533, Weak Industrial Sabotage 301525, Theft Humidity 280050 |
| Elite | Minor Corporate Protection 301529, Minor Industrial Sabotage 301521, Borrow Reflect 266312, Sudden Diversion 301899 |
| Champion | Lesser Corporate Protection 267121, Lesser Industrial Sabotage 267264, Divest Skills (Nanite Enhanced) 267114 |
| Uber | Corporate Protection 267117, Industrial Sabotage 267266, Plunder Skills (Nanite Enhanced) 267110, Shutdown Skills 302288, Subprime Vitality Mortgage 302401, Take the Bait 302290 |

## How a player would get one

Still GM-granted for elite/champion/uber (`.merc hire …`). Options for later: nodrop contract items from ALGT / invasion general, or a steep credits price. Recommendation unchanged: GM until a raid drop is wired.

## Commands

See `ddogs/docs/merc-commands.txt` for every hire / dismiss / list line.

## Restart

ZoneEngine_New must be restarted to load the new gear tables. No database change. Existing basic contracts stay basic. Stored tier value 3 that used to display as "Super" now displays as "Uber".
