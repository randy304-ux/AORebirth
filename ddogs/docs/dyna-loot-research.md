# Dyna loot research

Wired by `ddogs/tools/Build-DynaLoot.ps1` into source `ItemTemplates.json` and the dyna boss rows of `NpcTemplates.json`. Live `Built\...\GameData` is not updated by that script. The data file is `ddogs/tools/DynaLoot.proposed.json`. Randy's rule, now in the roller: every dyna boss drops 5 to 7 items. Nanos are instruction discs when that disc exists. Adds are not on this table.

Primary source for this pass: AO-Universe [Dyna-camps](https://www.ao-universe.com/guides/classic-ao/gameplay-guides-6/dyna-camps) (guide 580, written by Llie, last updated 2019-12-22 by Saavick). The page was read in full from the HTML. It has five sections and no Reveal block. There is no spoiler payload and no extra text loaded by script. Introduction, Where are Dyna-Camps, Types of Bosses, Dyna-Camp strategy, and the camp table are all inline.

Randy's Google AI-mode link still did not load. Google answered with a captcha. The query inside the link is "how do the dyna buffs work in anarchy online". That is the color buff, which this same page covers under Types of Bosses. Those colors are combat, not loot, and they are already implemented. This note stays on loot.

## What the primary page says

The introduction is the drop list. A boss has a chance to drop items that cannot be bought and are not mission rewards:

- Nova Dillon armor
- Kirch Kevlar armor
- Sekutek Chilled Plasteel armor
- Nano programs, usually as instruction discs. The page names three of them in that sentence: Nano Crystal (Nullity Sphere) AOID 150504, Nano Crystal (Nullity Sphere MK II) AOID 150503, and Grid Armor (link to the Grid Armor guide)
- Empty Data Receptacle Container, AOID 275056
- Profession and breed locked weapons, linked to AOFroobs topic 4128 (Imoutochan's list)

Regular mobs at the camp drop whatever that family drops anywhere else. The example on the page is cyborgs dropping Miy's armor.

The map section says the number on Saavick's map is the approximate boss level, and the regular mobs are 5 to 10 levels below the boss. The table at the bottom of the same page gives an exact boss level and an exact mob range for every camp. That table is 174 rows, waypoints 0 through 173, and it matches `ddogs/tools/dyna-camps.csv` row for row. Where the "5 to 10 below" sentence and the table disagree, this proposal uses the table.

Types of Bosses and Dyna-Camp strategy were read with the rest of the page. They are not loot. Basic colors are Deep Blue energy, Green chemical, Light Blue poison, Purple radiation, Red fire, White cold. Light Yellow crits, Orange reflects, Yellow has absorb layers, Pink has a huge health pool, Grey rotates damage type. The page says the same colors apply to Rubi-Ka team mission bosses. Strategy is pulling, calming, and kiting. None of that changes the loot table.

The Grid Armor link on the primary page goes to [Grid Armor](https://www.ao-universe.com/guides/classic-ao/gameplay-guides-6/grid-armor). That guide was read for the drop facts the dyna page points at. The crystals in `items.dat` match the guide:

| Crystal | AOID | QL |
|---|---|---|
| Nano Crystal (Summon Grid Armor Mk I) | 155190 | 60 |
| Nano Crystal (Summon Grid Armor Mk II) | 155192 | 93 |
| Nano Crystal (Summon Grid Armor Mk III) | 155191 | 116 |
| Nano Crystal (Summon Grid Armor Mk IV) | 155193 | 140 |

The guide does not treat these as dyna-only. It also lists missions, plants, Biomare, and Claw Camp in Pleasant Meadows. The same guide says a boss drops more loot when more people are on the team, and it marks that claim as 2003 information that may no longer apply. That is the closest published line to a variable item count. It is a rumor about team size on a Grid Armor farm, not a 5-to-7 formula, and this proposal does not use it.

## Sources

- AO-Universe, [Dyna-camps](https://www.ao-universe.com/guides/classic-ao/gameplay-guides-6/dyna-camps). Primary. Full page, as above. The camp table is `ddogs/tools/dyna-camps.csv` (174 camps).
- AO-Universe, [Grid Armor](https://www.ao-universe.com/guides/classic-ao/gameplay-guides-6/grid-armor). Linked from the primary page. Crystal QLs above, and the 2003 team-size note.
- AO-Universe, [Dyna Camps - Weapons - Nanos](https://www.ao-universe.com/guides/classic-ao/gameplay-guides-6/dyna-camps---weapons---nanos) (Imoutochan via Saavick, updated 2026-08-20). The item tables were read from the page HTML. They were not in the plain-text view. Every item link is an AOID. This page is the item catalog. Where it disagrees with the primary page on drop form, the primary page wins and the disagreement is flagged below.
- Funcom forum, [Imoutochan's list of dynaloot master weapons](https://forums.funcom.com/t/imoutochans-list-of-dynaloot-master-weapons-breed-prof-locked/49443) (Saavick, 2018-12-21). Same weapon brackets as the AO-Universe page.
- Funcom forum, [(guide) AO Tips](https://forums.funcom.com/t/guide-ao-tips/58671). The only explicit QL formula found: lowest QL is boss level times 0.75, highest is boss level times 1.25, for items that vary in quality.
- Funcom forum, [QL item vs dyna boss level](https://forums.funcom.com/t/seeking-info-on-finding-ql-item-to-the-correct-dyna-boss-level-range/227584). One player says plus or minus 20 percent. Another says ordinary mobs are plus or minus 10 levels, and a named single-QL weapon is narrower.
- Reddit, [Finding Nano Crystals](https://www.reddit.com/r/anarchyonline/comments/r2zq8y/finding_nano_crystals/). Players say about plus or minus 10 levels, or plus or minus 15 at QL 150 and up.
- AO forums archive, [How to tell dynaboss only nanos?](https://forums-archive.anarchy-online.com/showthread.php?261151-How-to-tell-dynaboss-only-nanos). Patch 14.6 / 14.71 added the dyna nanos. Keeper and Shade nanos joined in 15.0. Some nanos also come from shops or mission bosses (Nullity is one of the exceptions).
- AOWiki, [Arete Landing](https://wiki.aodb.us/wiki/Arete_Landing), section "Dyna Boss Checklist".
- AO forums archive, [Arete Landing - The NEW Starter Isle](https://forums-archive.anarchy-online.com/showthread.php?613759-Arete-Landing-The-NEW-Starter-Isle). Levels and places for the seven named bosses.
- Steam / Funcom, [Arete quest list](https://steamcommunity.com/sharedfiles/filedetails/?id=884660373). Rings, especially Ring of Luck, from the flea boss, Gnarl, and the Saltworm.
- AOWiki, [Dyna Camps by level](https://wiki.aodb.us/wiki/Dyna_Camps_by_level). Boss level is the mid level, give or take 15 percent, and the minions are lower. Not used for item IDs.
- Local catalog, read only: `AORebirth/GameData/items.dat` (131,807 templates) and `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/itemnames.sql`. Auno itself (`auno.org`) returned an Anubis challenge, so the AOID was checked on AO-Galaxy (same id the AO-Universe page links) and then in `items.dat`. Every AO-Universe AOID in this list is in `items.dat` (0 missing). 209 of those AOIDs are the nano program, not the loot crystal, and those programs are not in `itemnames`. The crystal row is.

Our roller is `LootableDynel.RollItemTable` / `RollQuality`. A row is `Hash`, `Repeats`, `Chance`, `LevelMod`. Quality is uniform from `level * (100 - LevelMod) / 100` to `level * (100 + LevelMod) / 100`. Corpse capacity is 9 (`Corpse.LootCapacity`). A one-template item is treated as valid at every quality at or above its template quality, and the rolled quality is stamped on it (`HashItemMinter.LeafSpan`). That matters for wiring, below.

## How dyna loot works

A camp is a glowing boss plus, usually, regular mobs of the same family. The primary page's map prose puts those mobs 5 to 10 levels under the boss. The camp table is more exact, and it is what the CSV uses. The boss is the loot target. The regular mobs drop whatever that family drops anywhere else (the page's example is Miy's armor on cyborgs). The boss can drop things that are not in shops and not mission rewards.

What the primary page names as boss loot:

- Nova Dillon, Kirch Kevlar, and Sekutek Chilled Plasteel. The weapons/nanos page also lists a lot of other armor and says some of it might be mission-rollable. Treat that extra armor as "claimed, not proven".
- Profession and breed weapons in fixed QL brackets (the Imoutochan list, the same AOFroobs topic the primary page links).
- Nanos, and the primary page says the form is usually an instruction disc. The weapons/nanos page says a nano crystal or an instruction disc. Both rows exist. Bite of the Wind is the checked example: program AOID 161147 is QL 27 and is not the drop; NanoCrystal (Bite of the Wind) 161148 is QL 103; Instruction Disc (Bite of the Wind) 161410 is also QL 103. The proposal still keys the nano bands off the crystal, because that is the row the weapons page tabulates. **GUESS:** which of the two forms retail actually puts on the corpse. Switching the proposal to discs would be a second pass, not a silent change.
- Nano Crystal (Nullity Sphere) 150504, Nano Crystal (Nullity Sphere MK II) 150503, and Empty Data Receptacle Container 275056. The primary page names all three.
- Grid Armor, named inside the nano sentence, so the drop is the summon crystal or disc, not the worn armor the nano creates. The four crystals are QL 60, 93, 116, and 140. They are mid level, not newbie loot, and the Grid Armor guide says they also drop outside dynas.

There is no per-camp loot table. A level 50 flea dyna and a level 50 hound dyna roll the same pool. An item shows up when the boss's QL window covers that item's bracket. That is the "only from dynas in a certain level range" rule. It is the item's QL, not the camp's name.

The QL window is not settled:

| Source | Window |
|---|---|
| Funcom AO Tips | boss level x 0.75 to x 1.25 |
| Forum player | plus or minus 20 percent |
| Forum player, ordinary loot | plus or minus 10 levels |
| Reddit | about plus or minus 10, or 15 at high QL |

The proposal uses x 0.75 to x 1.25. On our roller that is `LevelMod` 25. **GUESS:** that this is the right one of the three reports. The other two are close but not the same.

Item count: nobody published a roll of 5 to 7. That number is Randy's recollection, and it matches what a full boss corpse feels like. The only published line about a changing count is the Grid Armor guide's 2003 claim that a fuller team makes a boss drop more items, and that guide says the claim may be obsolete. This proposal does not scale loot by team size. Our corpse holds 9. The chances in the JSON are a **GUESS** set so a typical boss lands near 6 items and a hot streak stops at 9.

Nanos: the weapons/nanos list is explicit that those lines are dyna boss loot. In `items.dat` the tabulated loot row is `NanoCrystal (Name)` with one template. The crystal quality is the breakpoint used in the bands. A single template has no max QL in the file, so a nano is only placed in the band that contains that breakpoint. **GUESS:** that retail does not also drop that same nano from bosses far above the breakpoint. The forum says the range is narrow for a named single-QL item. **GUESS:** crystal versus instruction disc, as above. The primary page prefers the disc.

The crystal breakpoints we resolved start at QL 40. Bands under that get almost no dyna nanos (0 in levels 1-15, 2 in levels 16-35). Higher bands are where the nano list actually lives. Level 121-160 is the thick part (131 crystals).

## What the server does now

`Build-DynaContent.ps1` puts one rare table on every boss: monster parts, a stim, an Ofab weapon at 2 percent, Nullity Sphere (150504) at 3 percent, Nullity Sphere MK II (150503) at 1 percent, and Empty Data Receptacle Container (275056) at 3 percent. Camp weapons were deliberately not invented. The Ofab row is not in the dyna guides. It belongs to the subway notes. The proposal leaves it off.

Adds keep the ordinary common table. That matches the guide. This proposal does not change adds.

## Named bosses

These seven are the named dynas. They are all on Arete Landing (playfield 6553). Levels and places are from the Arete forum thread and the AOWiki checklist. None of them has a drop that a source gives to that boss alone.

ICC Shuttleport (playfield 4582) has no dyna camps in any of these sources. Its named rares are not glowing dyna bosses.

| Boss | Level | Where |
|---|---|---|
| Cleanmeister Intelligence Robot | 2 | Around the corner from Flint Novak |
| Supreme Collector of Waste | 4 | Northeast corner |
| IIV-X Advanced Docker | 4 | Building near Alex Gibbs |
| Mutated Garbage Flea | 7 | Under the market |
| Gnarl the Roller | 7 | Wanders the southern desert edge |
| Harvey the Bully | 10 | Wanders near 3700, 517, among minibulls |
| Saltworm | 13 | Wanders the southern desert lake |

Shared Arete pool, from the wiki (Nova Dillon, the same nanos the other critters drop, and rarely a profession nano) and the steam guide (rings):

| Item | AOIDs | QL in items.dat | Auno |
|---|---|---|---|
| Nova Dillon Body Armor | 163945 / 163946 | 1 to 200 | https://auno.org/ao/db.php?id=163945 |
| Nova Dillon Armor Helmet | 163949 / 163950 | 1 to 200 | https://auno.org/ao/db.php?id=163949 |
| Ring of Luck - Offensive | 84144 / 84143 | 1 to 200 | https://auno.org/ao/db.php?id=84144 |
| Ring of Luck - Defensive | 84142 / 84141 | 1 to 200 | https://auno.org/ao/db.php?id=84142 |
| Nano Crystal (Composite Utility Expertise) | 287047 | 25 | https://auno.org/ao/db.php?id=287047 |

The other Nova Dillon pieces are in the JSON (gloves, pants, sleeves, boots), same two-id pattern. **GUESS:** all seven bosses share the ring. The wiki describes one list. The steam guide only names the flea, Gnarl, and the Saltworm as the ring camps. **GUESS:** Composite Utility actually drops here. Its crystal is QL 25, and a level 2-13 boss under the 0.75-1.25 rule rolls about QL 2-16, which does not reach 25. The wiki says the Spida nanos drop from these bosses and from ordinary critters, but it does not name this AOID.

Mainland dyna nanos (QL 40 and up) do not belong on these seven. Neither do the QL 40-50 master weapons (Icebreaker and the rest). Those start to fit bosses around level 36-55.

## Low Rubi-Ka camps

These are the outdoor camps with a boss of level 20 or under. They are not individually named. Signature loot is the level band, not a private item. Coordinates are the AO-Universe table.

A boss in this range rolls roughly QL 1-25. That covers Nova Dillon and the other QL 1-200 armor, and the bottom of the QL 1-200 weapon lines (Cheap Joint Clans Scout Pistol 201257/201258, QL 1-109; The Original Electronicum 160288/160289, QL 1-98; the crystal weapons at "I"). It does not cover Icebreaker (208059/208060, QL 40-49, cap 208061 at QL 50).

| Type | Boss | Mobs | Playfield | x, z |
|---|---|---|---|---|
| Reets | 5 | 3-4 | Omni Forest | 660, 2540 |
| Blubbags | 5 | 3-4 | Omni Forest | 540, 2190 |
| Malle | 10 | 3-5 | Omni Forest | 380, 1500 |
| Igruana | 10 | 5-6 | Omni Forest | 380, 3180 |
| Biofreak | 10 | 5-7 | Omni Forest | 380, 1300 |
| Blubbags | 10 | 6-8 | Omni Forest | 540, 2860 |
| Leets | 10 | 7-8 | Omni Forest | 380, 820 |
| Shadowmutants | 10 | 8-10 | Omni Forest | 300, 1060 |
| Rhinomen | 15 | 10-15 | Newland Desert | 1260, 2540 |
| Rhinomen | 15 | 10-15 | Newland Desert | 1340, 2340 |
| Tentacle Mutants | 15 | 11-13 | Omni Forest | 850, 3260 |
| Pareets | 15 | 11-13 | Omni Forest | 300, 620 |
| Aquaans | 15 | 12-14 | Omni Forest | 540, 660 |
| Nighthowler | 15 | 12-14 | Omni Forest | 660, 1340 |
| Blubbags | 20 | 15-20 | Aegean | 1740, 1940 |
| Eyemutants | 20 | 15-20 | Newland Desert | 2180, 2740 |
| Rhinomen | 20 | 15-17 | Omni Forest | 620, 340 |
| Lizards | 20 | 15-20 | Varmint Woods | 1740, 2660 |

The other 156 camps are in the JSON under `camps`, each with a band id. `uniqueSignature` is false on all of them.

## Level bands

Window is boss level x 0.75 to x 1.25 across the whole band. Counts are items whose bracket overlaps that window. Nano counts are crystals only.

| Band | Camps | QL window | Weapons | Armors | Nano crystals |
|---|---|---|---|---|---|
| 1-15 | 14 | 1-19 | 23 | 80 | 0 |
| 16-35 | 16 | 12-44 | 43 | 79 | 2 |
| 36-55 | 28 | 27-69 | 68 | 79 | 12 |
| 56-85 | 43 | 42-107 | 94 | 86 | 48 |
| 86-120 | 13 | 64-150 | 109 | 87 | 84 |
| 121-160 | 18 | 90-200 | 131 | 121 | 131 |
| 161-200 | 31 | 120-250 | 107 | 119 | 102 |
| 201-275 | 11 | 150-344 | 68 | 117 | 66 |

The two nanos in 16-35 are NanoCrystal (Grid Runner (Team)) and NanoCrystal (Presence of the Master). The QL 40-50 master weapons (MTI P87 Happy Days 208046/208047, Icebreaker, and the rest of that block) land in 36-55. Nullity Sphere at QL 90 lands in 86-160. Nullity Sphere MK II at QL 159 lands in 121-200.

## Proposed boss table

Same shape as the current `LootTable` rows. Every chance is a **GUESS**. Expected count is about 6.2 before the cap of 9.

| Hash | Repeats | Chance | LevelMod | What it is |
|---|---|---|---|---|
| CMTR | 5 | 80 | 25 | Parts and junk already on CMTR |
| CMHK | 1 | 40 | 25 | Stim and kit already on CMHK |
| DYNA | 2 | 30 | 25 | One armor piece whose range covers the roll |
| DYNW | 1 | 15 | 25 | One weapon whose range covers the roll |
| DYNN | 2 | 50 | 25 | One nano whose breakpoint is in the roll. Crystal row in the JSON. Disc form is the open guess |
| NS01 | 1 | 3 | 0 | Nullity Sphere, 150504, QL 90 |
| NS02 | 1 | 1 | 0 | Nullity Sphere MK II, 150503, QL 159 |
| EDRC | 1 | 4 | 0 | Empty Data Receptacle Container, 275056, QL 1 |

Arete bosses add one more guessed row: DYNR, 1 repeat, 35 percent, LevelMod 25, the two Ring of Luck pieces.

DYNA, DYNW, DYNN, and DYNR are not in `ItemTemplates.json`. The `items` array in the JSON is the child list, with AOIDs, template pairs, and QLs.

## Guesses, in one place

- Chances and the 5-to-7 count. The count is Randy's memory. The chances were picked to land near 6. Team size does not change the count. The 2003 Grid Armor note about fuller teams is recorded and not used.
- LevelMod 25, chosen from the Funcom x0.75 to x1.25 line over the plus-or-minus-10 and plus-or-minus-20 reports.
- A nano's single template quality is the breakpoint, and it should not drop from bosses whose window misses that number. The primary page says the drop is usually an instruction disc. The weapons page says crystal or disc. The JSON still lists the crystal. Bite of the Wind shows both at QL 103 (crystal 161148, disc 161410).
- No camp has a private drop. Arete rings are on all seven bosses, not only the three the steam guide names.
- Composite Utility on Arete bosses, despite the QL 25 vs level 2-13 gap.
- Armor on the AO-Universe page past Nova Dillon, Kirch Kevlar, and Sekutek. The page itself says some of it may be mission-rollable.
- The 60 AO-Universe nanos with no `NanoCrystal (Name)` row. They are in the JSON with `crystalFound: false` and they are not in the band lists. The primary page's "usually an instruction disc" makes those discs the likely drop, and they are still not in the band lists until each disc AOID is checked.
- Grid Armor crystals are on the boss list because the primary page names them, and they are not exclusive to dynas. The Grid Armor guide also gives missions, plants, Biomare, and Claw Camp.
- Ofab stays off this table.

## Wiring, when someone wants it

1. Add category hashes DYNA, DYNW, DYNN, and DYNR to `ItemTemplates.json`. Children are the `items` rows for that kind, using the `templates` pair (low QL id, high QL id).
2. Do not rely on `LeafSpan` for a one-template item. Today that span runs from the template QL to infinity, and the minter writes the rolled QL onto the item. A QL 50 Cold Morning Icebreaker (208061) or a QL 103 crystal would come out at the boss's level. Fixed breakpoints need an explicit top, or they need to be minted at the template QL.
3. Put `proposedBossLoot.rows` on the boss leaves in `Build-DynaContent.ps1`. Put the ring row only on the seven Arete bosses. Leave the adds on the common table.
4. Drop the OFAB row from dyna bosses.
5. NS01, NS02, EDRC, CMTR, and CMHK can stay as they are.
6. The 60 unresolved nanos need a crystal or disc AOID before they go in DYNN.
