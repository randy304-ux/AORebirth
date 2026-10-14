# Content wave progress

Local branch `ddogs/main`. Not pushed.

## Training Ground (4313) — deployed 2026-10-14 13:05

Backup `I:\AO\backups\cellao_codex_clean-20261014-1255-totw.sql`. Zone build passed. Latest ZoneEngine start: npc templates leaves=441, quests=122 skipped=0, Knubot scripts=52 rejected=0. Ports 7500, 7501, 7012, 8098 listening. Commit `7bd69e8e` (local only).

Randy's screenshots are pinned in `ddogs/tools/Build-GuessedContent.ps1`. Drake's tests are in `ddogs/tools/Build-NewbieQuests.ps1` (bits 645-652).

Confirmed looks (captured mesh matches the screenshot):

- Deceitful Weaver, spider. Shining Coiler, green snake. Shadowleet, small leet.
- Hungry Predator and The Predator Rager, black panther (Rager is larger; new spawn near the predators).
- Lurking Chimera, small crawler. Crippler Fiend, winged bat (renamed from Crippler).
- Tempterus, orange flyer. Imp Disruptor, winged imp.
- Heckler of Nature and Heckler of Evolution, rock-pile mesh 214982.
- Watcher of Woods, captured Watcher mesh. Silvertail Mare and Beit Silvertail, silvertail horses (Mare uses empty hash SIMR; Beit Silvertail is a new spawn).
- Scientist Drake Rodriguez, captured scientist body.

Hecklers:

- 4311 Heckler of Metals (MOOM), Stones (MLOF) and Earth (MSOF) are still the captured level-80 templates. No XP bonus and no quest drop were added to them.
- Training Ground only: both hecklers use the rock-pile model. Kill XP is about four times a normal mob of that level (stat 52: Evolution 564, Nature 756), on top of the level kill award.
- Heckler of Evolution drops 6 Delicate Metal Devices per kill. That drop is not on any other heckler.

Drake's chain (no retail timers): strength, agility, sense, psychic choice (El-Domo or Celeth-El), intelligence, brink. The brink turn-in is those six devices. Mail Terminal template 37180 is already in `Playfields/4313/Dynels.dat` (one hit); no second terminal was added.

Still guessed, needs a screenshot after deploy:

- Massive Waggler (long-neck) and Beit Gras (camel) are on the large Spinetooth mesh. No sauropod or camel mesh was captured.
- Pond Dryad is a Malah mesh. Primitive Mortiig and The Devastator (new spawn in the southeast) are a Hiathlin mesh, Devastator larger.
- Celeth-El and Guardian of Crystals are a human guard mesh. El-Domo is a Cascading Spirit mesh. The screenshots show armored humanoids and a blue winged El-Domo.
- Sarah Willis, Brad Willis, the autoname researchers, the nurse, and the two extra L50 staff use scientist or guard bodies. Sundance Smith is talkable only; his shop stock was not invented. His spot, the Beit Silvertail, the Predator Rager, the Young Silvertail and The Devastator are placed beside the related spawns, not from a client point.
- Yellow hazard-suit guards were not given a mesh. Only one ground guard hash (GSTD) was free, and it is Brad Willis.

## Dynas and Insipid Ring — deployed 2026-10-14 14:08

Backup `I:\AO\backups\cellao_codex_clean-20261014-1406-totw.sql`. Zone build passed. ZoneEngine start 14:08: npc templates leaves=683, quests=129 skipped=0, Knubot scripts=54 rejected=0. Ports 7500, 7501, 7012, 8098 listening. Commit `a15b91bd` (local only). Engines left up. Launcher bundle was not changed.

174 AO-Universe dyna camps are hash spawns (boss plus up to four adds) on Aegean 585, Andromeda 655, Belial Forest 605, Central Artery Valley 590, Newland Desert 565, Omni Forest 716, Perpetual Wastelands 570, Varmint Woods 600, and Wailing Wastes 551. Builder: `ddogs/tools/Build-DynaContent.ps1` and `dyna-camps.csv`. Boss respawn 20 minutes, adds 3 minutes. Boss HP is about 4x a normal mob of that level, plus kill-XP stat 52 (`level * 40`). No color buff. Boss loot is common trash plus a low chance of Nullity Sphere (150504), Nullity Sphere MK II (150503), and Empty Data Receptacle Container (275056). Camp-specific weapons were not invented. Positions use the guide x,z and the nearest client spawn's Y. All of it is guessed. Spawn files are gitignored; the builder rewrites them locally and the deploy copies them.

Insipid Ring (bits 653 trial, 654 level-50 skip). Ergo is a new spawn on swamp 4312 at 1428, 56.7, 195 (guide spot, south of the client spawn cloud). Long Stare at Danger uses the client point 1401, 578. Deep Breath Nine takes the Fumigating Canister (234878). The four molokhai are persistent, not a death-chain, at the Embryo (4311, about 385x490) and just north of Long Stare (4312, about 1414x604). Young Molokhai drops Baby Molok Oil (234879). Ergo turns the oil in for the profession ring: Artillery 225753, Control 225758, Extermination 223998, Infantry 225760, Support 225759. Level 50+ can skip. Knubot conditions added: `prof` and `minlevel`. The Elysium portal does not check for the ring. Yuttos stock is still empty.

## Not started

Caliginous, Achromic, Rimy, Sanguine, and Pure Novictum. Other gardens (Elysium garden playfields 4678-4681 are the next garden ids; Aban and Thrak stay as they are). RK statics: Steps of Madness 1933, Smuggler's Den 125, Cyborg Barracks 1833, Reet Retreat 1913, Crypt of Home 4805. Elysium outdoor 4540-4544. When those zones are filled, put Hecklers where the guides show them, using the rock-pile mesh, and leave the captured 4311 camps alone.
