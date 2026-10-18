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

## Alien Invasion - deployed 2026-10-15 ET (ddogs/tools/Build-AlienInvasion.ps1)
Source: AO-Universe Alien Invasion guides (hierarchy, AI levels, alien armor process, dailies, Sector 10). Item ids from items.dat, meshes from the client RDB.
Own overlay files (engine now merges `X.*.json` next to NpcTemplates/ItemTemplates/Quests): NpcTemplates.Alien.json, ItemTemplates.Alien.json, Quests.Alien.json, plus Tradeskills.json.
- Engine: tradeskill window (TradeskillSource/Target/Build -> Core/Tradeskills/TradeskillService.cs, recipes in Tradeskills.json). Reply packet layout copied from CellAO memory, NOT checked against a capture (GUESSED). Only main-inventory items.
- Engine: quest reward `AlienXpPercent` (percent of the current AI level bar, uncapped).
- Mobs: 144 Rank - Class'Race leaves (6 ranks x Ankari/Xoch/Ilari/Rimah/Jaax/Cha x Sinuh/Uri/Heru/Nakthi) + 6 General - Class'Khaz. All NPCFamily 220 = alien XP.
  - REAL: names pattern, class -> Lead Viralbot, race looks (frog mini-bug, centaur, hover snake, floater bug, Khaz battle-spider with rider).
  - GUESSED: rank level bands (Scout 10-60, Guard 40-100, Commando 80-140, Specialist 110-180, Major 160-240, Sergeant 220-300); class style (Ankari/Xoch caster, Ilari melee+ranged, Rimah/Jaax melee, Cha ranged); stats scaled from the Mercenary Soldier curve; Generals 8x HP.
- Native spawn hashes mapped (GUESSED mapping): AALV = Alien Larva (Andromeda crash N of ICC), VEZI = Zix L100 weak (6052 Zix island), KYBR = Kyr'Ozch Guardian (6112), ALPO = Alien Pod (+ Alien Pod Spawn adds) in 570, AEMD/AOCI/ALLR/IUAI/TPFY = Sector 10 Medic/Comm Officer/Leader/Immature Vector/Regeneration Conduct, ZK3X = Sector 10 Commando+Specialist mix. Every other unmapped hash above L1 in 4374, 6112, 6113, 6121, 6123, 4365-4370 = random hierarchy aliens of matching rank (single L175+ spawns = Generals). U* hashes skipped (assumed Unicorn soldiers).
- Loot: Solid Clumps 25% (KCLM), class Lead Viralbot 3% on regulars / 60% x2 on Generals, Kyr'Ozch Viralbots 2% Major+ / 50% Generals, Atomic Re-Structuralizing Tool 1% / 10%. All rates GUESSED (real: Lead Viralbots and Kyr'Ozch Viralbots are boss drops).
- Tradeskills (135): full armor chain per AO-Universe with real skills and 80% rule; 36 armor pieces; 6 combined sets (both orders). GUESSED: which clump id makes Pristine/Mutated/which weapon Type (pairs in id order), combined-armor skill (Psychology 6x).
- Uncle Bazzit (ICC, beside the buff droid) sells Solvent and Essential Human DNA (real) plus NPI, Structural Analyzer, Atomic tool and Basic Fashion pieces (GUESSED - real: tradeskilled / dropped / social clothing quests).
- Alien Agency Handler (ICC, GUESSED spot; real one is in the agency HQs): We Come in Peace (10 larvae), Bugs (100 Zix), ...And you will leave in pieces (main cocoon), Terminate the Alien Cocoons (10), A-Pods (2), Sector 10 teleport. Teleports to 6052/6112/570/4374 spots GUESSED. Dailies are repeatable with no 24 h lockout (no MissionBit). AXP reward 25% of the AI bar (GUESSED). Pods are just killed (no Laser Tagging Device); cocoons are killed (no Cocoon Terminator).
- Sector 10: Unicorn Duty Sergeant inside the Unicorn line with Medics/Comm Officers/Leaders/Immature Vectors missions (real), 0.7x HP for a small group. Not done: DNA cage mission, signal relays, pocket bosses.
- Not done: LE research training (no capture of the research window's client message), viral weapons, city raids, mothership.
## Not started

Caliginous, Achromic, Rimy, Sanguine, and Pure Novictum. Other gardens (Elysium garden playfields 4678-4681 are the next garden ids; Aban and Thrak stay as they are). RK statics: Steps of Madness 1933, Smuggler's Den 125, Cyborg Barracks 1833, Reet Retreat 1913, Crypt of Home 4805. Elysium outdoor 4540-4544. When those zones are filled, put Hecklers where the guides show them, using the rock-pile mesh, and leave the captured 4311 camps alone.

## Ergo floating head - deployed 2026-10-14 15:34

Ergo (ERGO) now uses client monster data 210582 guardian_of_shadows at scale 100, from Randy's PRK screenshots. Spot unchanged: 4312 at 1428, 56.7, 195 matches AO-Universe, aquarius (1428;195) and the client district named Ergo. He has no Wander feature, so he stays put. Height is still a guess. Only one Ergo spawn exists (Nascence); other SL zone Ergos are not placed yet. Backup `I:\AO\backups\cellao_codex_clean-20261014-1531-ergo.sql`.

## Meshes, Yuttos stock - deployed 2026-10-14 16:05

Blubbags 40075, Rhinomen 17664, Molokhai 214479 (client monster data). Yuttos stock from Randy's PRK screenshots: Machines 31, Chemicals 33, Pharmaceuticals 64, Manuals 36, Electronics 34, Tools 102 (Spirit Siphon x5 and Deflection Shield QL66 unmatched). Yuttos Machines moved into the Pharmaceuticals/Manuals tent at 307.27, 61.6, 1613.71 (guessed floor). Ergo Y unchanged. Backup `I:\AO\backups\cellao_codex_clean-20261014-1604-yuttos.sql`. ZoneEngine 16:05: leaves=683, quests=129 skipped=0, Knubot rejected=0, ports 7500/7501/7012/8098 up.

## Natural weapon and Ran-Roth-Or patrol — deployed 2026-10-14 16:46

Backup `I:\AO\backups\cellao_codex_clean-20261014-1643-weapons.sql`. ZoneEngine root container started 16:46:49 ET. leaves=683, quests=129 skipped=0, Knubot scripts=54 rejected=0. Ports 7500, 7501, 7012, and 8098 listening. Engines left up. Account service and launcher were not touched. Commit `91bf6723` (local only). Snake change `0034178f` is in the loaded templates (BSMG monster data 30252, scale 300).

Combat NPCs whose template lists no monster weapon now get SIW1 (144742/144743) at their level in `FillEquipment`, including pets and mercs (same `SpawnMob` path). NoCombat NPCs stay unarmed. The spawn line now includes `weapons=`. A broken comment in `HashSpawnSystem` had been swallowing `PatrolEnabled = true`, so named patrols such as Ran-Roth-Or never started. No playfield has loaded a mob since this boot (preflight online count was 0), so a `weapons=` line from this process is not in the log yet. The `weapons=0` lines at 16:24-16:29 are from the previous process.

## Arete arrival and shop routing — deployed 2026-10-14 18:33 ET

Backup `I:\AO\backups\cellao_codex_clean-20261014-1832-arete.sql` (18:32 ET). ZoneEngine started 18:33 ET. leaves=690, quests=130 skipped=0, Knubot scripts=55 rejected=0. Ports 7500, 7501, 7012, and 8098 listening. Account API was left running. Commit `b3dd2c63` (local only).

New characters start on Arete Landing playfield 6553 at 3609, 52.3, 786.2 (the guide's upper deck, beside Rex). Pink placeholders on that deck (QEMH, CABX, FOJN, MUSN, and the one RELS body rehashed to CGOB) and the freighter's MFSP are guessed small cargo bots, NoCombat. The guessed Deck Officer and FRGO on TPIB were replaced the same evening; see the Arete guide section below. Lower-deck pink placeholders that are not fleas, protesters, or the named mission bodies were left as they are.

Launcher 2026.10.14-1830 writes `ddogs-web.json` (login host, port 8098) into the marked client and the patch arms Store/Daily from that file. Published locally and to the public downloads folder. Not pushed.

## Info page and command appendix — deployed 2026-10-14 18:59 ET

Backup `I:\AO\backups\cellao_codex_clean-20261014-1856-info.sql` (18:56 ET). ZoneEngine started 18:59 ET. leaves=690, quests=130 skipped=0, Knubot scripts=55 rejected=0. Ports 7500, 7501, 7012, and 8098 listening. Account API republished and restarted with the engines. Engines left up.

The Store window (same `vgtp://uwg.store.icc-rk` address, `/`, `/index.app`, and `/store`) is titled Info. Claim Center and Daily Reward say Back to Info. The Commands card says `.info`, which opens the command appendix in the same info window as an item description. Lists come from the command registry plus the team, pet, chat, and stuck handlers. GM commands are included only when that character can run them.

## Arete guide, fleas, and strike recruiter — deployed 2026-10-14 19:50 ET

Backup `I:\AO\backups\cellao_codex_clean-20261014-1944-arete-guide.sql` (19:45 ET, 18643634 bytes). ZoneEngine started 19:50 ET. leaves=700, quests=141 skipped=0, Knubot scripts=64 rejected=0. Ports 7500, 7501, 7012, and 8098 listening. Account API was left running (same process as the Info boot). Engines left up. The first boot of this pass failed because Alex's four steps were written with Action as an array; that was fixed and the engines were started again.

TPIB on freighter 7101 is Janos (AO-Universe section 22, A Special Delivery), bit 655, quest JNSR. He talks from level 5. The journal errand completes when RALPH accepts it. The 19:50 pin on playfield 655 was the outdoor guide coordinate; he was moved the same evening, see below. Reward is the published cap of 11266 xp. No parcel item and no credit amount were invented. FRGO is gone.

The Arete chain now follows the guide: Rex (5 cleaning robots, then the cargo box) to Marcus (suppressant trade) to Flint (7 cleaning robots, computer to Alex) to Alex (surveillance droid, house plant, Bill's monitor, Kneebreaker) to Stan (lockpick the strongbox, then the four parts) to Marco to Sarah (DNA-Locked Armor from the dead thief) to Vernon (library, then the shipping terminal) to Mason and, for Shades, Sheila, then Lorelei (cookie and cage) to Vaughn (ID card). Desmond (burger, 2 protesters, Cedric), Patrick (insurance terminal), and the Shady Guy (Mario and the Yalmaha) are separate jobs. Bits 656-662. Next free bit is 663.

Underground VUFL spawns are Garbage Fleas (same look family as the existing JNME flea, level bands 1-2 and 5-6). The one IEGA spawn is a Mutated Garbage Flea. ROTE bodies are protesters. Cedric, the surveillance droid, Kneebreaker, Marco, Sheila, Remi, and Mario have templates. A mercenary recruiter at 3596, 8.3, 818, beside the striking workers, hires Doctor, Enforcer, Soldier, and Keeper.

Still not simulated, because the guide does not give an item id or a body to stand on: the sealed parcel, Janos's credit and commendation amounts, a captured mesh for Janos, RALPH, Cedric, the protesters, Marco, Sheila, Remi, or Mario (borrowed looks; Mario stands 14 m east of the Shady Guy), the gas-fire world object, the surgery-clinic combine, Spirit Siphon under 21% and the Soul Capsule, Vaughn's 15000 bribe, Remi's SANDSTORM marauders, the wounded dockworkers, faction jobs, and Emiel, Karli, and Leonora. Flint's counter is the Cleanmeister Intelligence Robots only. Marco hands out the Soldier nano can only.

## RALPH in Palmiero's Office — deployed 2026-10-14 20:37 ET

Backup `I:\AO\backups\cellao_codex_clean-20261014-2033-ralph.sql` (20:34 ET, 18643634 bytes). ZoneEngine ready 20:37 ET. leaves=700, quests=141 skipped=0, Knubot scripts=64 rejected=0. Ports 7500, 7501, 7012, and 8098 listening. Account API pid 50968 was left running. Engines left up.

RALPH is a content NPC on playfield 7001, Palmiero's Office, at 187.7, 1.0, 195.9, behind the reception desk. The outdoor copy on playfield 655 is gone. Janos's first line is "You there...are you heading into the ICC Headquarters?" with "I can help!" and "Goodbye". MLCE and JUCO are named Cleanmeister Intelligence Robot. Their mesh is still the borrowed 297023. The client monster-name table has no RALPH, Janos, Protester, Dockworker, Cleanmeister, Garbage Flea, Supreme Collector, or IIV-X. Janos remains on solitus thin male robe 26084. Garbage Fleas remain on flea 17657. No dockworkers, no IIV-X, and no Supreme Collector dyna were added.

Playfield 655 is Andromeda. District "ICC HQ" is centred at 3220, 35.1, 900, and Randy's outdoor pin 3251.4, 35.1, 823.4 sits on that floor. Zone 2681 is locality cell 81,20 of that playfield (40-meter cells, 130 across), not a second map. The towers are the client tilemap already in Playfields/655. Collision, Surfaces, Destinations, Dynels, and Walls contain no destination 7001. The teleports table sends 655 to 1702, 1703, 1721, and 1722, plus two disabled doors. Nothing routes to 7001, and 7001 has no exit row. The Holo Deck door does not lead inside.

Guide NPCs and vendors with no template, so they are not spawned: Arbiter Vincenzo Palmiero, the Arbitration Drone, Adeline Guerra, the ICC commendation store, Natalia Akcora, Fia Lou, the Shopping building vendors, Miir, Fair Trade, the outdoor armor, pharmacy, implant, and Phasefront shops, Des Morck (client hash DMMK), and Windcaller Karrec (client hash WIKA is untemplated; the portal quest is still not built). The other 7001 client bodies (ASDM, ARVC, ARVI, ARVN, RPPI, FLIC, ICRW, ABGU, AGTR) are untemplated and were not renamed. ICRW stands about 5 meters north of RALPH.

## Buffbot and org bot — deployed 2026-10-14 21:28 ET

Backup `I:\AO\backups\cellao_codex_clean-20261014-2112-bots.sql` (21:12 ET, 18643634 bytes). ZoneEngine ready 21:28 ET. leaves=700, quests=141 skipped=0, Knubot scripts=65 rejected=0. Buffbot catalog sections=5 lines=27 missing=0. Ports 7500, 7501, 7012, and 8098 listening. Account API was stopped only while ChatEngine's shared DLLs were copied, then started again. Engines left up.

One NPC, Buffbot, on playfield 655 at 3251.4, 35.1, 823.4. That pin is where Randy was standing, not a measured row of bot characters. Body is client monster 96056, protocol droid (catMesh 96024). White uniforms are not on a hash template. No other hub was given a coordinate, so no second buffbot was placed. Design notes are in `ddogs/docs/BOTS.md`. The window follows Budabot/Nadybot. The org commands follow Tyrbot and Nadybot. Neither program is running here.

`.buff`, a tell to Buffbot, or the knubot line opens the info window. Buffmacro, Rebuff, Help, and Status are the top links. Sections are Generic, Agent, Bureaucrat, Doctor, and Enforcer. A click casts the items.dat nano whose exact name is in `GameData/Buffbot.json`, the highest QL that name has at or under the player's level. items.dat has one named rank for each of these programs. Strain, stacking, and NCU are the real buff rules. There is no credit cost. `.org create Name` inserts the organization and opens the org channel. The chat name is Orgbot. It is not a character row. Commands: online, members, lastseen (online or offline only), motd, news, alts, add, raid, loot. Raid and loot notes are memory and clear on a zone restart. The retail org-window create packet was not found, so `.org create` is the formation path.

Not tested in the client: the tell window, a buff landing, a full NCU bar, org create, and an org-chat tell.

## Holo Deck door and Palmiero's Office guides — deployed 2026-10-14 21:58 ET

Backup `I:\AO\backups\cellao_codex_clean-20261014-2155-holo.sql` (21:55 ET, 18643634 bytes). Zone build passed. ZoneEngine ready 21:58 ET. leaves=705, quests=141 skipped=0, Knubot scripts=66 rejected=0. Ports 7500 pid 57736, 7501 pid 63200, 7012 pid 12408, 8098 pid 35656. Account API was left running. Engines left up.

The Holo Deck entrance is client door 0xC000028F, template 26219, on playfield 655 at 3245.8, 36.1, 943.0. That is about 120 m north of Randy's pin 3251.4, 823.4, on the ICC HQ floor, not on the pin. Its OnTargetInVicinity teleport already names 183, 2, 197 on playfield 7001. A second proxy names playfield 6131 and stays closed, because operator 117 fails. Mesh 258035 has no sphere in DynelCollision.json, so the walk-up pad is 3 m, the reach of the client door spheres that are extracted. No teleports-table row was added. 7001 has no Door dynel, so a door-to-door route cannot name an exit.

The way out is the only dynel on 7001: terminal template 303199 at 178.1, 1.1, 197.0. Its OnEnter teleport sends the character to 3245, 36, 939 on 655, about 4 m south of the entrance. That OnEnter was not baked before. It is now the same kind of 3 m pad. The client walk was not tested.

Guide NPCs are hash templates and NoCombat. RALPH was not moved.

- Des Morck (DMMK) at 3259.5, 36.1, 941.9, level 102. Guessed. No captured monster data, so no mesh was invented. Health 10000 is a placeholder. He may have no body.
- Windcaller Karrec (WIKA) at 3212.4, 36.0, 788.8. Capture 20260717-223626: MonsterData 40818, head 40696, scale 121, level 200. The greeting is the captured line. The follow-up says the temple way is not open yet, and that line is Guessed. No burger quest and no level gate.
- Arbiter Vincenzo Palmiero (ARVC) matches capture 20260719-155043 exactly. MonsterData 26092, head 40694, scale 100, level 220.
- Adeline Guerra (FLIC) is Guessed. The capture stands 0.94 m from the only level 210 body. MonsterData 26155, head 40138, scale 130, level 210.
- Arbitration Drone (RPPI) matches that capture exactly. MonsterData 260229, no head, scale 100, level 100.

ASDM, the guardians, and the other untemplated 7001 bodies were not spawned. Not tested in the client: walking in through the Holo Deck door and back out.

## Karli Cappelleri and the crashed alien ship (Arete 6553) - deployed 2026-10-15 20:40

Backup `I:\AO\backups\cellao_codex_clean-20261015-2035-karli.sql`. ZoneEngine_New built alone (full ao-build blocked by the Account API dotnet PID 76036 locking Built\Debug\AORebirth.Core.dll; Core was not changed). ZoneEngine start 20:40: quests=143 skipped=0, Knubot scripts=68 rejected=0. Ports 7500, 7501, 7012, 8098 listening. Generator: `ddogs/tools/Build-KarliAlienShip.ps1` (rerun after a data re-extract; it also writes the gitignored `Playfields/8009/Spawns.json`). New Knubot tag `{teleport PF X Y Z}`.

Real (AO-Universe Arete Landing #20, AOGalaxy): Karli's dialog lines, quest name Help From a Friend, rewards 2520 credits / 1386 XP / 3x 8-15 NCU Memory, Nano Can: Friendly Buff AOID 301250 (hash FBCN), mob names Alien Spider - Zix, Scout Jaax'Sinuh (ACFJ), Specialist Cha'Heru (CRDY; both existing captured templates).

Guessed, needs PRK screenshots:

- Karli's spot `.tp 3790 0.6 270 6553` (middle of the Zix spawns, not inside a ship) and look (Sarah Greene's female body).
- No alien-ship door exists in the 6553 client dynels and nothing in 6553 links to 8009. The way in is talking to Karli ("Get me into the ship"); the way out is a "Ship Hatch" terminal-look NPC in 8009 at 40, 0.5, 44 that puts you back beside Karli.
- Interior is playfield 8009 "Crashed Alien Ship" (84 m outdoor map with one client spawn, YZWI, still pink). Not a random-room instance and not per team. Spawns added: 2 Zix, 2 Jaax, 2 Cha'Heru, Alien General (new ALGN: Cha'Heru mesh 251772 at scale 140, L14, 3x HP) and the target Zix (new ZIXK, L12, scale 130, 60 s respawn), all at y 0.5 around 50-62, 40-64; heights not checked.
- Solo quest FBUD (bit 663): using the can on a teammate does not complete it; you tell Karli you did. Team quest ZXK1 "The Alien Ship" (bit 664, Scope Team, KillOne ZIXK): reward 1500 credits / 1500 XP invented (guide lists none). NCU chips are id 36778/36780 at QL 30.
### 2026-10-15 21:40 ET - Karli's alien ship: General model + Kyr'ozch hierarchy names
- Sources: AO-Universe Arete #20 (mob list) and AO-Universe Alien Hierarchy (Rank - Class'Race); meshes from docs/reference/enemies/data/ClientRdbNpcTemplateHints.csv.
- Alien General (ALGN): name REAL (AOU lists "Alien General", level ?). Mesh 247742 "alien boss detached" (Khaz battle-spider, no rider) to match Randy's PRK screenshot. GUESSED: mesh choice by eye, scale 75, level 14, 3x HP.
- Kept REAL Arete mobs: Alien Spider - Zix (alien_spider 247728), Scout - Jaax'Sinuh (mini bug 251782), Specialist - Cha'Heru (hover battlesuit 251772).
- Added crew, GUESSED (valid hierarchy combos but not in the Arete list): Scout - Ankari'Sinuh (KSAU, mini_bug_caster 251784, L11), Guard - Rimah'Uri (KGRU, centaur_melee 251785, L12), Commando - Ilari'Uri (KCIU, centaur_melee_ranged 251786, L13), Guard - Xoch'Heru (KGXH, hover_caster 251773, L12). Levels/scales guessed.
- Race->mesh: Sinuh=alien_mini_bug (frog), Uri=alien_battlesuit_centaur, Heru=alien_battlesuit_hover (snake). Uri mapping from mesh names, not a capture.
- Ground: 8009 heightmap is flat 0; native YZWI spawn at y=0.498, all ship spawns at y=0.5.
- Template insert in Build-KarliAlienShip.ps1 now re-appends our keys (idempotent). Data-only deploy; no engine rebuild needed.
### 2026-10-15 22:48 ET - Karli inside the ship, crash-site corpses, minibulls, Harvey, Saltworm, rollerrat loot
Backup `I:\AO\backups\cellao_codex_clean-20261015-2245-arete.sql`. ZoneEngine_New built alone (code change below). Restart 22:47: quests=143 skipped=0, Knubot scripts=69 rejected=0, ports 7500/7501/7012/6996/8098 listening. Generators: `ddogs/tools/Build-KarliAlienShip.ps1` (now also runs `ddogs/tools/Build-AreteWildlife.ps1`).
- Code: `NpcBossAction.DespawnSeconds` - a boss-trigger add can leave the world after N seconds (`NpcCharacter.DespawnAtUtc`, checked in Tick).
- Karli Cappelleri moved into 8009 at 45.4, 0.4, 41.5 (Randy's spot): `.tp 45.4 0.4 41.5 8009`. Look = Uri centaur battlesuit mesh 247464 (GUESS: the veteran/loyalty-store alien disguise costume item was not found in the client RDB). Both quests from her; the "Get me into the ship" option is gone.
- Entry: "Crashed Ship Hatch" talk-to NPC on 6553 at Karli's old spot, `.tp 3790 0.6 270 6553` -> lands at 40, 0.5, 44 in 8009 (GUESS: no client door exists).
- Exit: "Ship Hatch" (hash SHXT, NoCombat/NoMove, alien turret mesh 253599 GUESS) is no longer placed at start. The Alien General's Death trigger spawns it beside him for 300 s, then it despawns; the General respawns in 300 s, so the ship resets. Talking to it returns you to 3790, 0.6, 262 on 6553.
- Crash-site pinks HX1V (7 points near 3866, 252): alternate Zix (247728) and Heru (247468, hash HXHR) bodies, NoCombat/NoMove, movement mode 11 (Sleep) to lie down. GUESS: the client RDB has no dead/corpse alien monster records, so these are live meshes posed lying; may still render standing.
- WTSO (33 pinks incl. 3660, 329) = Angry Minibull L8-13, mesh 30360 (real: AO-Universe Arete mob list). Stats from E03C donor.
- REGD (3769, 565, L10, was 600 s) = Harvey the Bully L10 (real name/level: AO forum Arete guide + wiki.aodb.us, "wanders around 3700, 517"). Hash mapping, 2x HP, scale 125 guessed. Wander (50 m) feature; respawn 900 s guessed. No JSON patrol path support exists, so it roams instead.
- XUDP (3466, 629, L13) = Saltworm L13 (real: "wanders around the lake to the south in the desert area"). Mesh 17712 sandworm, 3x HP, scale guessed; Wander 50 m; respawn 900 s guessed. Loot Unrefined Notum Salt (USLT 247786, 60%) + Monster Parts.
- Rollerrat loot (no rollerrat had any LootTable before): Rollerrat Intestines QRRI 248333 35%, Undamaged Piece of Rubbery Rollerrat Flesh RRFL 206886 30%, Undamaged Roller-Rat tail RRTL 165274 15%, Monster Parts MPRT 42640/42641 50% (QL by mob level). Added to every Rollerrat / Rollerrat Overseer key except Stowaway (wiki: credits only) and Queens. Rates guessed.
## 2026-10-15 Arete junkyard mobs, flea sizes, Shoulderpads part 1 (ddogs/tools/Build-ShoulderpadsJunkyard.ps1, run by Build-AreteWildlife.ps1)
Already real (unchanged): Protester ROTE x5 L2, Waste Collector 01V1 x12 L5, Garbage Flea JNME x16 L2 (+VUFL x14, FCTO x2), 32-V Docker 32DO, cleaning robots JUCO x23 / MLCE x8 (still named Cleanmeister Intelligence Robot from an earlier pass; Randy's live shot shows a plain "Cleaning Robot" beside the Cleanmeister, so JUCO likely should be "Cleaning Robot" - not changed because Rex/Flint dialog says Cleanmeisters).
New: IIV-X Advanced Docker IIVX L4 x4 (PO51 + three 32DO within 14 m of 3515.8,901.7; mesh 17649 android). Supreme Collector of Waste SCWS L8 x1 at FRAG (3533,734), 900 s, mesh 17714 scale 130 (orange skin not settable). Cleanmeister boss CMBS L4 x1 at PRC2 (3545,865, in the cleaning-robot cluster), 900 s, scale 170. GUESSED: all three hash mappings, levels, HP multipliers, scales.
Fleas: regular fleas (JNME, VUFL, FCTO, FLFE) scale 100, Mutated Garbage Flea IEGA 160 (Randy: normal flea is knee-to-waist). Exact scale GUESSED. VUFL/FCTO were 310 from an earlier script; rerunning that one alone would undo this.
Shoulderpads (AO-Universe guide): Alvin Odeleder ALOD (695, 1544,2500), bit 700 quest SPA1 -> Pads of Interest 158000 + Alvin's Card I 155722. Dodga Demercel DODE (585, 606,1122), bit 701 quest SPC1 -> Clan Shoulderpads for the Suspicious 158087 + Dodga's Card I 156950. Parts 155665/155684/155664/155685/155666/155686/155683/155687. GUESSED: turn-in of the 8 raw parts by trade instead of the 7-step combine (ZoneEngine_New has no tradeskill handler), 5000 xp, QL 30, Adri Afeli's look, 12% per part on every machine mob in 6553/4582/127 (40% on the two bosses). Parts 2-3 (Trash King + lackeys, syringes) NOT built.

## 2026-10-15 (later) Arete docks, Mario, Saltworm, reets/leets, Gnarl, faction area, market (Build-AreteDocks / Build-AreteReetsFactions / Build-AreteMarket, chained after Build-ShoulderpadsJunkyard)
Correction: Supreme Collector of Waste moved from FRAG to SOCW (L13, 4067,667, 900 s) per wiki.aodb.us "northeast corner"; FRAG is a pink again. Mapping still GUESSED.
Docks: GAFR x3 = Gas Fire (mesh 234586 campfire, guessed), WODO x7 = Wounded Dockworker (client mesh 296008, lying), IESY x2 = Dockworker (borrowed look). Marcus Stone now also gives GFOT (bit 702, put out a fire by talking to it) and his hand-in requires it; wounded errand WDK1 (703, talk to a worker) -> WDK2 (704, back to Marcus: 1040 cr, 1281 xp, 1 recharger 291082 + 1 stim 291043; retail gives 50 + 25). Talk-to instead of item-on-target: GUESSED. Rerun Build-AreteDocks after Build-NewbieQuests (it patches MarcusStone.json).
Mario Carles MARC: mesh 203157 yalmaha_executive, Wander, moved to 3700,32.5,640 radius 40, 600 s. Car-as-Mario (no crowbar smash step) and spot GUESSED; ASYG quest unchanged (KillOne MARC, drops Stolen Yalmaha).
Saltworm XUDP: mesh 205942 abyss serpent, scale 220, y 0.4 unchanged. Model choice GUESSED (no teal saltworm mesh name in the client table).
Greedy Desert Reet GRDS: Shiny Sword (297289) drops 8% from Desert Reets DERT and Rollerrats WSSC; SSW1 bit 705 trade -> Nano Crystal (Composite Nano Expertise) 223381. Leet King LKNG (new spawn 3345,4.3,640) LKQ1 bit 706 KillOne GRDS + Sledgehammer 297045, return hammer -> LKQ2 bit 707 Princess Leet PRLT (new spawn 3335,0.8,600) -> Light Bio Mesh 246925. Spots, treasure-smash skip, fixed biomesh GUESSED. EJVC (To Be Determined inside GRDS) removed.
Gnarl the Roller YBSH: L7, WSSC rollerrat loot, scale 170, 4x HP, Wander, 900 s. Hash and level GUESSED.
Faction area names (no quests, NoCombat, human mesh 26092): OIUR x7 ICC Peacekeeper, CPPS/CSPE Clan Protester, ONGA x4 Omni-Pol Guard, LUSN x4 + KLHG x2 + DART Omni-AF Private, OMCN Omni-AF Officer Milne, ODDD Omni-Trans Equipment Vendor (mesh 250380, no stock), CSPT x2 Clan Unit Sentinel, CAGD Clan Guard Elina Jones, FDCS Field Commander Ericson, VAHE Vanguard Henri Zure; HEZU removed (same spot as VAHE). All mappings GUESSED by code letters. Ericson/Milne faction quests NOT built.
Market: FACS Logistics Manager Fausto, MJUY Robotic Guard Dog (mesh 17720), LEMY Leonora Marty, FMXT Furniture Merchant, FPBY Food Provider, CGSA Cyrus Cervenak, SPAU/SLPW Tasty Chocolates!/Romantic Flowers! signs, BBOC Credit Card: CCS1 bit 708 steal 15000 cr / CCR1 bit 709 return to Leonora -> 2080 xp + Vacuum Packed Omni-Med Suit 297054. Mappings GUESSED; no shop stock. Not placed (no confident pink): Tabitha Ginsberg, Jacinto, Wilda Dartt, Emile Western, Herschel Musco, Mario Schmeisser.

## LE personal research (2026-10-16)
Real (confirmed):
- 98 profession lines + 10 general lines (Apotheosis etc.), 10 tiers each, from client perk records (type 1000036, flag 0x40). Research XP per tier comes from the client's own Field18: 50k, 450k, 1.6M, 4.7M, 12.75M, 32M, 54M, 64M, 740M, 900M (general lines 100M). Profession filter is Field6 (bit 1<<profession), checked against Doctor/Trader/Shade/Keeper lines. Data: GameData/ResearchCosts.json (tool: tools-temp/rdbperk).
- A share of earned XP (after the normal 10%/20% grant cap) goes to the active line, as AO-Universe describes (slider for XP to research). A finished tier is trained as a perk (its skills/actions apply via the normal perk path), and research moves on to the next tier of the same line. Per-tier level cap = the perk item's own ToWear requirements; extra XP waits in the bank.
GUESSES:
- The client research window protocol (ResearchRequest 0x3115534d has no known fields; ResearchUpdate entries are Unknown1-3) is NOT wired. Use `.research` instead. Picking a line in the window does nothing yet.
- Stat storage: PersonalResearchGoal(265)=active perk id, PersonalResearchLevel(263)=banked research XP, GlobalResearchGoal(266)=XP percent. The client may read these differently.
- Default share is 50% when you first pick a line. SK (Shadowlands) split is not done, only XP.
- Global research (vehicles, Global Advantage, 0x80 lines) is not done. On live it's finished anyway.
- Research perks are not given the research value in the FullCharacter perk map (sent as 0).

## 2026-10-16 Arete reveal pass: #22 Janos rewards, #11a Vaughn bribe, reward recheck (ddogs/tools/Build-AreteRewardsFix.ps1)
- Engine: QuestReward.Stats (stat id -> amount added) and QuestRewardItem.Count (stack size). Compile-checked in a clean worktree; NOT yet in the running binary (the engine was not rebuilt because another job has uncommitted engine changes). Until the next engine build, Stats/Count are ignored (1 item each, no commendation/token stat).
- JNSR (Janos -> RALPH): 11266 xp, +1 ICC Commendations (695), +1 Freelancers Inc. Tokens (696), 1 item. GUESSED: credits 1000 (guide/screenshot give no amount; 90907 was the balance), the item = Freelancers Inc. Token 303237.
- #11a VHBR (bit 710): tell Vaughn you're rich -> "really rich" -> 15000-credit trade -> false ID filed. Money from earning or Credit Card CCS1 (15000). GUESSED: ICC HQ Access Terminal is a reply on Vaughn (after AVHR or VHBR) teleporting to 4582 934.9 24.8 762.0 (by Brandon Thorn); ICC HQ Andromeda isn't modelled.
- WDK2 (Marcus, wounded dockworkers): Health and Nano Recharger x50, Health and Nano Stim x25 (guide; was 1/1).
- Recheck against reveals (no change needed): Rex 1040/1281; Marcus gas fire 1080/1281 + Generic Nano Transmitter; Alex Gibbs; Stanley 1240/2229 + chip; Marco 1120/2229 + crystal; Shady Guy 5000/1540 + Kodaik; Credit Card 15000 / 2080 + Omni-Med Suit; Alien Ship (FBUD) 2520/1386 + 3x 8-15 NCU.
- Known gaps: Desmond (#12) gives all three armors (engine has no reward choice); Emiel Dijkman (#18, Heavy OR Light Bio Mesh) not built.

## Research window wired + dyna modifiers + bigger fleas (2026-10-16)
- Research window (CONFIRMED by RC-LT capture 00:34): opening sends ResearchRequest (0x3115534D, no body; server sends nothing back yet). Picking a line = SetStat 265 PersonalResearchGoal = perk id of the line (2790 = Sweep and Clear tier 1). Personal slider = SetStat 263 PersonalResearchLevel (0-100 %), Global slider = SetStat 264 GlobalResearchLevel (%). Server now stores these and uses 263 as the XP share; picking a line researches its next untrained tier. Banked research XP moved to server-only stat 10263 (GUESS: client never sees progress; ResearchUpdate reply format unknown). Global research % is stored but does nothing.
- Fleas: the 310 scale in old notes had been reset to 100/160 by a later regenerate; set regular 300, Mutated 400, Fleas Dyna base 300 (then x1.5+ as a dyna). Exact sizes GUESSED.
- Dyna modifiers (athenpaladins t=2166): any band with ContentProvenance.CampKey "boss|..." or a DynaModifier field gets one modifier (named or random each spawn): Chemical/Cold/Energy/Fire/Poison/Radiation = DamageType + AC(level*10) + 25 reflect of that type; Crit (tan, +50 crit, fast init, size x1.6); Shield (orange, all-type damage shield level/2, size x1.4); Big (yellow, 3x HP, +3*level damage, 2x attack delay, half run speed, x1.3). All dynas x1.5 size first.
- Aura GUESS: no client aura id found. Sent as EffectType=1 + EffectRed/Green/Blue stats (StatMessage right after spawn). If no glow shows, try `.set EffectType <n>` on a targeted dyna (GM 100) to find the value. Initiative debuff on targets not done; reflect/shield only work if combat honours those stats.

## 2026-10-16 01:30 - Arete wildlife, research bank, Arete reclaim
- Saltworm (XUDP): model changed to giant sandworm 226557 at scale 60, HP set to 1248 (3x the curve), Side 3 and BreedHostility 1 so it aggroes. GUESS: whether it shows teal, and whether scale 60 is the right size.
- Angry Minibull (WTSO): HP 216 (L8) / 416 (L13). Harvey (REGD): HP 876. Both now Side 3 / BreedHostility 1. They were Side 0, which is neutral and never aggroes. Mesh 30360 is still the "minibull" record from the RDB; the invisible model is not explained yet.
- Fleas (VUFL/FCTO/JNME/IEGA, dynas D011/D017): model changed from 17657 "flea" to 275710 "flea mutant", scale 100/130/120. GUESS: the 17657 mesh renders tiny no matter what Scale is.
- Research: changing the line no longer wipes banked XP. The client re-sends 265 when the window opens, and that was resetting the bank to 0. Overflow already carried over between tiers; it waits in the bank when the next tier is level-capped.
- Death respawn: dying on PF 6553 now respawns on Arete Landing at 3609,52.3,786.2. GUESS: these are the start-deck coords, not the real reclaim terminal. Everywhere else still uses Respawn.json (Bor 800).
- .tp arete takes you to that point.

## 2026-10-16 01:48 - Insurance respawn, Arete looks, minibull mesh
- Insurance: using the terminal within 8 m of 3464.0, 9.1 (height), 813.9 on PF 6553 saves your spot. It is stored per character in server-only stats 10270-10273 (playfield, x/y/z times 10). On death you respawn at the saved spot. Characters with nothing saved go to the Arete insurance spot, not Bor (Respawn.json is no longer used). .tp arete goes to the same spot. GUESS: Randy's coordinates are read as x, z, height, the same way the guide gives the credit card spot (3451.4, 891.6, 9.1). Other playfields have no insurance terminals listed yet.
- Jumpsuits: already in Quests.json. They are the choice reward for Desmond Calitri's chain, final step ADPR "Collect Desmond's payment" (1120 credits, 2229 XP): Dockworker Overalls 295698, The Brute Suit 295699, Experimental Nano Armor Exosuit 295703.
- Cedric Harding (CEHA): Solitus male body 26092, head 40694, wearing Dockworker Overalls (template Equipment, which NPC wear textures read from). Dockworker IESY and Wounded Dockworker WODO also wear the overalls. GUESS: that the Dockworker Overalls are the orange suit.
- Protesters: ROTE uses the Antonio Stacklund look (26088/40687). Clan Protesters CPPS and CSPE use female civilian looks (26076/40635 and 26082/40634). Every spawn of one template still shares that template's look.
- Angry Minibull (WTSO) and Harvey (REGD): MonsterData changed from 30360 to 38387, the ID the original E03C "Minibulls" data uses. GUESS.
- Audit: every MonsterData ID in the recent Arete templates exists in MonsterData.json and in the client RDB (types 1040023/1010002), so there were no bad IDs. Waste Collector (17714, organic waste collector, still unchanged from the original data) invisibility cause unknown.

## 2026-10-16 ~02:20 - Mercenary overhaul and worn NPC looks
- Follow bug, root cause: the Soldier and Enforcer used the ICC Shuttle Guard body (MonsterData 254118). Its Features (32771) lack CanMove (0x4), so `NpcCharacter.IsImmobile` made `PathTo` a no-op. They only moved when the 50 m rejoin warp fired. Doctor (204985) and Keeper (26084) are mobile bodies. Every merc look is now a mobile body, and Build-Mercs.ps1 refuses an immobile one.
- All 14 professions hireable (`Core/Pets/Mercs.cs`: key, hash MR**, profession id, own pet slot 10-23). The Doctor is off the heal slot (now slot 13), so it no longer replaces an MP or other player heal pet.
- Heal AI: heal pets and healer mercs (Doctor, Adventurer, Metaphysicist) heal the most hurt of the owner and the owner's other pets/mercs within 30 m (`PetAi.NeediestCharge`).
- Contracts persist on the character (server-only stat 10280, one bit per profession). A merc that dies, or whose owner dies or logs off, comes back beside the owner at full HP after `Mercs.json` ReviveSeconds (10), and on login (`PetService.TickMercs`, once a second from the playfield tick). Hiring is free; no cap; one per profession.
- Mercenary Contracts terminal (body 279184, Shipping Manifest Terminal look) replaces the 4 recruiters at 4582, 6553 Arete Landing and 4310, and the Arete strike recruiter. Knubot `MercContracts.json`: hire list, end-one list, end all, how-it-works. New Knubot tag `{dismissmerc PROF}`.
- `.merc` (everyone): list contracts, `.merc hire <prof>`, `.merc dismiss <prof|all>`, `.merc call`.
- Merc armor (Mercs.json, ids by name from itemnames.sql): Jobe Suit 1+, First Tier 100+ (with Jobe helmet/shoulderpads/support system), Second Tier 160+, Faithful 210+ (Chosen for Omni owners). Worn armor gives mercs its stats (`NpcCharacter.WearsArmorStats`; Mercs.json ArmorStats).
- Worn looks (Build-Appearance.ps1, chained last from Build-NpcTemplates.ps1): Protesters ROTE, CPPS and CSPE are Atrox (26097/40111) in Dockworker Overalls 295698 holding No Less Than Three Placard 284185. Cedric is a Solitus male (26092/40694) in overalls; IESY/WODO dockworkers wear overalls. Held props with a WeaponMesh now show in an NPC's hand (`IsVisualWeapon`) without changing its attack range.
GUESSES: merc HP/damage multipliers and bodies per profession; tier level bands (100/160/210); Adventurer and MP use the Doctor heal nanos; Jobe Suit for levels under 100; Atrox head 40111 (only Atrox look in the data); Dockworker Overalls 295698 being the orange striped suit; the placard showing in hand depends on its WeaponMesh stat (unverified). Not built: offensive nanos, profession specials, pets owned by mercs.
## 2026-10-16 12:17 - Saltworm size, dyna boss items, solid models
- Saltworm XUDP: sandworm 17712, scale 100 (40 was too small per Randy at 3:08 AM).
- Dynas now wear the live "Boss Modifier Item (...)" that matches their modifier (itemnames ids: Chemical 164346, Cold 164301, Energy 164337, Fire 164328, Poison 164356, Radiation 164319, Crit 164365, Shield 164292, Big = Massive Health Booster 164283). Added to the template Equipment by NpcTemplateCatalog.WithDynaBossItem. GUESS: QL tier = level/25 (9 tiers), and that the aura comes from that item. The EffectType=1 stats did nothing in game and are still sent.
- See-through models: Randy's .set test showed rollerrat 17687 renders solid, but 275710 stays see-through, so the flea/collector models themselves draw transparent on 18.8.50. Swapped: fleas (VUFL/FCTO/JNME/IEGA, dynas D011/D017) to 40069 "gunbeetle"; Waste Collectors (WTCO/01V1/SCWS) to 45873 "junkbot". GUESS: both render solid. Neither is tested yet.

2026-10-16 texture probe (read-only): see-through models' textures (fleamonster1 15233, floatingpicker2 17318, minibull 26905, fleamonster_re 275711) are RGBA PNGs whose alpha is below 250 on every pixel (minimum 0), at every mip size (types 1010004/1010016/1010017). Solid clawfinger (38198) is a JPEG with no alpha. GUESS: the reworked textures use alpha as a mask that the 18.8.50 client draws as transparency. No server-side override exists in the monster records. Fixing it on the client means rewriting those RDB records, and the launcher patch zip only ships whole files, so it can't patch single records yet.

2026-10-16 16:00 batch (Arete pinks, mercs, models, aura research):
- Original models restored (fleas/dynas 17657, Waste Collectors/minibulls 17714, WTSO/REGD 30360, NPCFamily 0); the launcher 1515 TextureFix makes them opaque.
- Saltworm XUDP: NoMove + WeaponRange 380=400. GUESS: the lake is off the navmesh, so it treated every target as unreachable and evaded.
- Emiel Dijkman (pink ALPG, guide #18) and Bronto Mama (pink EJIW, pens). GUESS: ALPG/EJIW mapping; Bronto Mama = minibronto 17662 at 200%; Emiel's look (Solitus 26092 + Black Leather Pants 41028).
- Fausto look from the guide screenshot (/workspace/ao-ref/fausto.jpg on the box): white shirt 287552 (kLoathing Squares) + Black Pants 27378. GUESS: closest items; no tie.
- Emiel quest: dialogue from the guide. Emiel gives Emiel's Box of Foodstuff 297048 (item hash EBOX, {spawn}); Fausto takes it (new Knubot tag {take ITEMHASH}) and offers a Heavy (246926) or Light (246925) Bio Mesh. Repeatable: EMB1 pays 500 credits/1500 XP on the first run only (GUESS: amounts), later runs use EMB2 (no pay). Bits 711-714.
- Merc weapons (Mercs.json Weapon, worn as a held prop; the monster weapon still rolls damage). GUESS picks: Enforcer Cracked Sledgehammer, Soldier Battered OT M500 SWAT Rifle, Keeper Broken Immortal Katana, Doctor Cheap Soft Pepper Pistol, Adventurer Rusty Ithaca Ki-8, Agent Cheap Yatamutchy X-3 Counter-Sniper Rifle, Bureaucrat Rusty OT M-10 Flechette Pistol, Engineer Mistreated Vektor M-31 Grenade Launcher, Fixer Pistola Di Macchina Mini Automatico, MA Dreadloch Shen Sticks, MP Kyr'Ozch Pistol, NT Staff of Pelias, Shade Low-Energy Edwards RS Knife, Trader Rusty OT M50 Shotgun.
- Merc-owned pets: MP Anima (MPPT, Shadowmutant 30464), Engineer Robot (MEPT, Android 17649), Bureaucrat Robot (MBPT, junkbot 45873); bands cloned from the merc at 50% HP, 60% damage (GUESS: looks and numbers). Owned by the merc (Assist), removed with it on dismiss, death and zone change, back when the merc respawns.
- Dyna aura research: Boss Modifier Item OnWear CastNano -> e.g. 160084 "Red glow" (Fire), whose only function is GfxEffect 43746; Increase size 160070; Damage Type nano. The engine has no GfxEffect handling at all, and the glow nano lasts TimeExist 10000 (100 s). The cast runs at spawn before the NPC is announced. Next step: `.give buff 160084` on a targeted dyna to see whether the client draws it from the active-nano list; if not, the GFX needs its own packet (not found yet).

## 2026-10-16 19:45 - Store window connection refused
The Shop > Store window loads `vgtp://uwg.store.icc-rk` and `http://aoshop.funcom.com/shop/`. version.dll sends those names to the login server and is supposed to move port 80 to 8098. GUI.dll imports `connect` by ordinal, and the patch only rewrote a slot whose address matched GetProcAddress, so the port move never happened. The client connected to the login server on port 80. Nothing listened there: `192.168.1.68:80` and the public address both refused. `8098` was already open on the LAN and on `71.75.37.88`. Funcom's own shop port 80 answers, so this was our redirected port 80, not Funcom.

Live: the account API listens on `0.0.0.0:80` and `0.0.0.0:8098` (pid 81092, log `also80=True`). LAN `http://192.168.1.68/index.app` and public `http://71.75.37.88:8098/index.app` both return the Info page. The router's public port 80 still refuses; it is not forwarded to this PC. The new version.dll patches `connect` by its ws2_32 ordinal (and when GUI.dll loads) so the window uses 8098. Published in launcher 2026.10.16-1515 client patch sha256 `e8cfd4a9d414f8fb4e48019f2b6234cd149495e3223d900b25cb79ed8122da97`. Zone engines were not restarted. Online count was 0. No database change. Ports 7500, 7501, 7012, 6996, 8098, and 80 were listening after the account API recycle.

## 2026-10-16 20:15 - Merc pets
Crat merc pet is a Worker-Droid on monster 96056. That mesh's texture is `bureuacrat_protocol_android.png` (protocol droid), not the Engineer android 17649 and not junkbot 45873. Engineer pet stays the android. MP merc keeps three pets from the captured metapet bodies: Anger Manifestation (96195, attack, level 1), Distracting Sphere (96194, support/mezz, level 5; Lesser Distracting Sphere is QL 4, pet level 5, MM 36 and TS 36), Salvinous (96193, heal, level 14; Calling of Medinos is QL 17, pet level 14). A merc's heal pet also looks after the player who hired it. Side compile of ZoneEngine_New: 0 errors.

Live after restart 20:15 (online count 0, backup `I:\AO\backups\cellao_codex_clean-20261016-2013-merc-pets.sql`): leaves=1044, quests=167 skipped=0, Knubot scripts=82 rejected=0. Ports 7500, 7501, 7012, 6996, 8098 listening (80 still the account API). No new zone errors. The Emiel and Fausto "bye" warnings are the same ones as before. Commit `1881b13b` (local only). Engines left up.

## 2026-10-16 20:47 - Dyna aura
Boss Modifier Item OnWear casts a glow nano (Fire is 160084 Red glow, GfxEffect 43746 only), plus a size nano and a damage-type nano. Each lasts TimeExist 10000 (100 s). The wear cast runs before the NPC is in a cell, so the first SetNanoDuration reached nobody, and the buff then expired. The zone now refreshes those buffs with the same SetNanoDuration packet before they drop, announces them once the NPC has a cell, and sends the glow again when a player walks into range. No new effect packet. Chemical tier id 164349 is Metallic Mantis Body Armor, so that tier wears 164348 instead.

Unlocks are server-only stat 10281 (one bit per colour). The worn colour is 10282 and "also pets" is 10283; none of those are sent to the client. About 1 in 5 dyna corpses replaces one of the existing 5-7 drops with that colour's container (icon 20407, the spongy container look). The client shows the real item name: Chemical Spongy Novictum 218474, Cold Novictum Mud 218472, Energy Transparent Novictum Gas 218468, Fire Caustic Novictum 218476, Poison Plastic Novictum 218475, Radiation Radiant Novictum Gas 218469, Crit Compact Novictum Stone 218473, Shield Radioactive Isotope Container 100354, Big Dangerous Container 225998. Using it unlocks that aura. `.aura` lists them. `.aura fire` applies it. `.aura fire pets` also keeps it on pets and mercs. `.aura off` removes it. `.aura unlock fire` is GM level 1. The Store picker is not built. The glow still depends on the client drawing GfxEffect from the active nano; that is what Randy is testing.

Live after restart 20:47 (online count 0, backup `I:\AO\backups\cellao_codex_clean-20261016-2045-aura.sql`, 18647190 bytes): leaves=1044, quests=167 skipped=0, Knubot scripts=82 rejected=0. Ports 7500 pid 97592, 7501 pid 80036, 7012/6996 pid 103528, 8098 and 80 pid 81092. No new zone errors. Emiel and Fausto "bye" warnings are unchanged. Commit `6abdb58e` (local only). Engines left up.

## 2026-10-16 - Remaining SL dynas
Elysium, Scheol, Penumbra, and Inferno camps that already have a coordinate, a playfield id, and a captured mesh are in `Build-SLDynas.ps1`. Camp keys are `elysium|`, `scheol|`, `penumbra|`, and `inferno|`, so the Rubi-Ka `boss|` loot pass does not replace them. Those keys now count as dynas, so they wear a boss modifier and keep the glow. Each corpse rolls 5 to 7 items (pattern pieces plus the level-band discs, and a pinned unique when one has an AOID). Respawn is 180 seconds. Adds stand 14 m out and keep the common family table. Re-running the builder added 0 templates and 0 spawns.

38 places went in. Elysium: Hithalin on 4541 at 966, 777 and on 4543 at 490, 1510 (Ungulera pieces, Spirit Ring of Self-Education 246310); Croaker on 4541 at 634, 1383 (Archbile Queen and Achbile Guardian pieces, Elysian Protector's Cuirass 246309). Scheol upper 4880: Chimera at four Eastern Passageway points (Bellowing and Thunderous pieces), Croaker at six Facet Canyon / Croaker's Pass points (Hemut and Khemhet), Spider at four Crystal Forest points (Borer; WROM stand-in, same mesh as Hwall). Scheol lower 4881: Leet at 790, 1173 and 792, 1174; Croaker at six Void points (Iunmin and Marem). Penumbra: Skedadding Spirit and Vamoosing Spirit in the Valley ruins (Penumbral Spirit Hunter), Cool and Crisp in the Hollow (Dryad Shuffle; Cool's level is the top of the 171-175 band), Dauntless / Fearless / Resolute Dryads in the Forest (Breeder and Mother; level is the top of 170-175). Inferno 4605: Chimera Monitor 215 at 1375, 560 and Chimera Trainer 210 at 1430, 620 (Indomitable Chimera pieces); Limnos, Syros, Crete at 250 (Pazuzu pieces; heckler mesh is flagged as a guess); Pursued Spirit 215 at 710, 1100 (Arch Bigot Aliel pieces).

Left out, not invented: Elysium camps that only have a compass label; the shared points 930, 1480 and 1164, 1567; families with no donor (Eremite, girder, beit, rash, demon, shadow, hydra, minx, rafter, stalker, hiisi, visitant, golem, dragon, afreet, medusa, spirit hunter, and blank Inferno families); Scheol Greek brink bosses, because the wiki and Aquarius coordinates disagree; all of Adonis, because the outdoor areas are not tied to City 4872 or Abyss 4873, and Sinful Souldredge's cut-off coordinates; Penumbra rows whose playfield id is flagged as a guess; Chimera Drivers (no coordinate); Pandemonium raids. Personal names that the guide lists as a set are not pinned onto one coordinate, so those mobs use the family name.

Live after restart 21:04 (online count 0, backup `I:\AO\backups\cellao_codex_clean-20261016-2102-sl-dynas.sql`, 18647190 bytes): leaves=1078, quests=167 skipped=0, Knubot scripts=82 rejected=0. Ports 7500 pid 101948, 7501 pid 102656, 7012/6996 pid 104100, 8098 and 80 pid 81092. No new zone errors. Emiel and Fausto "bye" warnings are unchanged. Commit `cb4d05d9` (local only). Engines left up. Build 0 errors.

## 2026-10-16 - Cleaning Robot
The AO-Universe Arete guide names the junkyard mob a Cleaning Robot (Flint's seven robots). JUCO is that mob, and Randy's shot showed a Cleaning Robot beside the Cleanmeister. JUCO is now named Cleaning Robot. Flint's lines and the "Kill 7 Cleaning Robots" step say the same. Rex's outdoor robots stay Cleanmeister Intelligence Robot (MLCE), which is the name his dialog already uses. The Cleanmeister boss in that cluster is unchanged.

Live after restart 21:12 (online count 0, backup `I:\AO\backups\cellao_codex_clean-20261016-2110-cleaning-robot.sql`, 18647190 bytes): leaves=1078, quests=167 skipped=0, Knubot scripts=82 rejected=0. Ports 7500 pid 104052, 7501 pid 96672, 7012/6996 pid 104160, 8098 and 80 pid 81092. No new zone errors. Emiel and Fausto "bye" warnings are unchanged. Commit `5123d042` (local only). Engines left up. Build 0 errors.

## 2026-10-16 - Emiel reward
AO-Universe Arete guide #18 lists one reward: a choice of Heavy Bio Mesh 246926 or Light Bio Mesh 246925. It does not list credits or XP. The first delivery (EMB1) no longer pays the guessed 500 credits and 1500 XP. Later deliveries (EMB2) were already unpaid. The mesh choice is unchanged.

Live after restart 21:20 (online count 0, backup `I:\AO\backups\cellao_codex_clean-20261016-2118-emiel.sql`, 18647190 bytes): leaves=1078, quests=167 skipped=0, Knubot scripts=82 rejected=0. Ports 7500 pid 102304, 7501 pid 98892, 7012/6996 pid 18620, 8098 and 80 pid 81092. No new zone errors. Emiel and Fausto "bye" warnings are unchanged. Commit `6d3fff9b` (local only). Engines left up. Build 0 errors.

## 2026-10-16 - Fausto's tie
Fausto stays in the white shirt and black pants from the guide screenshot. No tie was added.

## 2026-10-16 - Research window bar
Stat 263 is the personal XP-share slider (the percent of earned XP that goes to research), and stat 264 is the global slider. The client sends both. Banked research XP stays on server-only stat 10263. The completion bar would need the ResearchUpdate reply, and that packet layout is still unknown, so it was not invented. `.research` still reports banked XP against the tier cost.

## 2026-10-16 - Store external links
The Info window is the Awesomium shop view. A card whose URL contains `://` is written with `target="_blank"`, which is the click Awesomium delivers to `AweOpenExternalLinkCallback` in GUI.dll. That function already calls `ShellExecuteA` for `http://` and `https://`, but only when `BrowserView::GetExternalLinksEnabled` is true. The Store window leaves the flag off, so the callback returns and the click does nothing. `chatcmd:///` and ordinary in-window links use a different callback.

version.dll now NOPs that one six-byte jump (GUI.dll RVA 0x10B6C6, signature matched once in the 2023-06-02 GUI.dll). The http(s) test stays. Offline self-test passed. The live client folder was not modified.

LAN `http://192.168.1.68/index.app`, `http://192.168.1.68:8098/index.app`, and public `http://71.75.37.88:8098/index.app` all return the Info page with Auno, AO-Universe, and the Dungeon Dogs home link as `target="_blank"`. First published in launcher 2026.10.16-1515. The same NOP is in the later 2026.10.16-2157 version.dll (client patch sha256 `5dc772cd35590c9c199d066482b4ebe4936179458cbb7d8cf16a0029c136ffe7`). Zone engines were not restarted.

## 2026-10-16 - Vulkan renderer
Official package is ao-vk 0.4.1 from https://github.com/wannabuh/ao-vk/releases/tag/v0.4.1 (the Caloss2 Gaming video gJJQRxAfZG0 links that release). Zip sha256 `4ad547a798477582853e2b6cfca049e92d3b2be293459fadac39aa803e430768`. ao-vk is MIT, Copyright (c) 2026 wannabuh. The bundled AOReloaded version.dll is MIT, Copyright (c) 2026 Inorien. Both licenses are copied into the client with the install and removed on revert. 0.4.1 has no file named Randy.DLL. The renderer is randy31.dll. Their version.dll is the optional in-game Renderer tab.

Graphics has a checkbox labeled "Vulkan renderer", off by default. On a marked Dungeon Dogs client, turning it on renames the stock randy31.dll to randy31_orig.dll, copies ao-vk's randy31.dll over it, and copies their version.dll as aovk-reloaded.dll. Our version.dll stays version.dll. Its worker thread LoadLibrary's aovk-reloaded.dll when that file is present, and logs AOVK chain=SKIP reason=not_installed when it is not. Turning the checkbox off puts randy31_orig.dll back and deletes aovk-reloaded.dll and the license files when they still match the payload.

Tested on I:\AO\cache\client-vulkan-copy (a copy of client-ddogs randy31.dll and the .ddogs-client marker, plus our version.dll). Launcher self-test exit 0: install twice, revert twice, version.dll hash unchanged, live client-ddogs randy31.dll still `9d9e0da25ac6f8c7ecdce7a47a2409575945966a29e3b77850f7c62033a04be3`, and no version.dll was written into the live client. Loading aovk-reloaded.dll from a process that is not AnarchyOnline.exe succeeded and unloaded. The game was not launched.

The first game start with the checkbox on also runs AOReloaded. That build can set Large Address Aware on AnarchyOnline.exe on disk, and it adds the Renderer tab plus its other features. Upstream tested Linux/Wine on an NVIDIA RTX 4070 Ti. Windows, AMD, and Intel are untested there. It needs Vulkan 1.3.

Published launcher 2026.10.16-2157. Launcher zip sha256 `29e6d112622fe68c343a30106a4595ff84321e56f5245d4b13b6689e895b1329`, size 60552999. Patch zip sha256 `43b8e31b0246580183c560c990ff15297fc07adc123eec05b4562d84d3a66978`, size 237100485. The patch zip contains client-patch\version.dll and client-patch\aovk (renderer, aovk-reloaded.dll, both licenses). `https://dungeondogs.org/downloads/ao/version.json` returns that record, and the public patch zip Content-Length is 237100485. `http://192.168.1.68:8080/downloads/ao/version.json` is 404; port 8080 is the AMP panel, and the shipped launcher config checks the public URL. Zone engines were not restarted.

## 2026-10-16 22:22 - SL dynas finished
Adonis outdoor camps are City 4872. The Silent Pit (Trip, Trap, Trup, Tiny) is Abyss 4873, the district named The Silent Pit. Elysium 1164, 1567 is the second Croaker, on North 4544. Predator and mortiig south points are South 4540. Their north points do not fit North (z max 1884) and are on East 4543 beside a level-matched spawn. Eremite, dryads, lost souls, and the sadistic soul dredges are in. 930, 1480 is the level-70 kuir on West 4541. No kuir mesh, so it is not spawned, and the minx is not put on either shared point. Scheol brink uses the wiki coordinates (they sit on the client spawns): Naxos 200, Lesvos 210, Kalymnos 210, Scheol Demon 210, Kefallonia 220, Sifnos 200, Poros 220. Sifnos and Poros have a blank drop line, so they get the band discs only. One Heckler of Metals is on Nascence Wilds 4311 at 170, 1950, the Steppe hunting-grounds start, 12 m from a level-80 heckler. Papageno and the Chimera Drivers still have no coordinate. Pandemonium raids were not spawned.

Stand-in meshes, flagged: visitant and horror use the spirit (NASI), golems use the heckler (MOOM), Scheol demons use TEDM, Kefallonia uses the chimera (LLBE). Eremite, predator, and stalker use their own captured meshes. Dryad and mortiig keep the stand-ins already live. Pinned uniques that itemnames.sql contains are on the corpse. CrackerJack Sleeves, Commander's Ceremonial Chest, and Golem's Warm Notum Hood have no row.

Live after restart 22:22 (online count 0, backup `I:\AO\backups\cellao_codex_clean-20261016-2219-sl-dynas-finish.sql`, 18647190 bytes): leaves=1140, quests=167 skipped=0, Knubot scripts=82 rejected=0. Ports 7500 pid 99608, 7501 pid 29872, 7012/6996 pid 100720, 8098 and 80 pid 81092. No new zone errors. Engines left up. Zone build 0 errors.

## 2026-10-16 - Static dungeon research
Research only, in `ddogs/docs/static-dungeons-research.md`. Nothing wired. Temple of Three Winds 1931 and Condemned Subway 127 are already populated. Foreman's Office is Biomare, playfield 1941, and it is layout only. Proposed order: Steps of Madness 1933, Biomare 1941, Crypt of Home 4805, then the Elysium garden key dungeons.

## 2026-10-16 22:56 - Steps of Madness
Playfield 1933 is populated from the 114 client spawn points. No second set of points, and the client respawn times stay (Neleb, Betrayer of Memory, and Thief of Reason 600 seconds). No Childhood Memory key and no level lock. Zyvania Bagh is the existing Andromeda hash ZYBA at 3158.1, 35.5, 899.1. She teleports to 40, 33.8, 170 on 1933. Neleb drops two Fractured Sanity plus the robe 151895, battlerod 152026, and Dream Mesh Circuit 274972 at 100 percent, and one Dark Dreams 274971 at 55 percent. His HP is the wiki figure 15818.

Live after restart 22:56 (online count 0, backup `I:\AO\backups\cellao_codex_clean-20261016-2253-steps-of-madness.sql`, 18647190 bytes): leaves=1170, quests=167 skipped=0, Knubot scripts=83 rejected=0. Ports 7500 pid 93396, 7501 pid 83368, 7012/6996 pid 105008, 8098 and 80 pid 81092. No new zone errors. Engines left up. Zone build 0 errors.

## 2026-10-16 23:41 - Steps of Madness looks
Roving Eye and Baleful Eye use the Distracting Sphere mesh 96194 at scale 91. Neleb is a Nanomage male (breed 3, head 40173, body 26092) wearing Nano-master Robe 274956 and holding Nightmare Battlerod 274955. His drops are unchanged.

Live after restart 23:41 (online count 0, backup `I:\AO\backups\cellao_codex_clean-20261016-2339-steps-of-madness-looks.sql`, 18647190 bytes): leaves=1170, quests=167 skipped=0, Knubot scripts=83 rejected=0. Ports 7500 pid 100532, 7501 pid 105032, 7012/6996 pid 105400, 8098 and 80 pid 81092. No new zone errors. Engines left up. Zone build 0 errors.

## 2026-10-17 00:00 - Dyna aura land packets
`.give buff` draws the glow with CastNanoSpell (Unknown1=1), FinishNanoCasting, then SetNanoDuration. The dyna re-apply was sending only SetNanoDuration, and the wear cast's land had already gone out before the NPC had a cell. The re-apply now calls that same land once the NPC has a cell, and a player who walks into range gets those three packets.

Live after restart 00:00 (online count 0 at 23:58:39, immediately before stop; backup `I:\AO\backups\cellao_codex_clean-20261016-2358-dyna-aura.sql`, 18648156 bytes): leaves=1170 families=79, quests=167 skipped=0, Knubot scripts=83 rejected=0. Ports 7500 pid 106172, 7501 pid 97912, 7012/6996 pid 106232, 8098 and 80 pid 81092. No new zone errors. Engines left up. Zone build 0 errors.

## 2026-10-17 00:13 - Launcher tools folder and AO-VK
The Tools tab was empty with "Tools folder would be on C:" whenever the launcher exe sat on C:. The folder is now `DungeonDogs-Tools` beside the client the launcher uses, or the launcher's own Tools folder when that one is not on C:. A folder chosen in Settings wins. C: is still refused.

AO-VK / aoReloaded is the first Tools row: Install, Uninstall, and Start with Play. Install downloads ao-vk 0.4.1 from https://github.com/wannabuh/ao-vk/releases/download/v0.4.1/ao-vk-0.4.1.zip (sha256 `4ad547a798477582853e2b6cfca049e92d3b2be293459fadac39aa803e430768`) and installs it on a client copy: stock randy31.dll is kept as randy31_orig.dll, ao-vk's randy31.dll replaces it, and AOReloaded's version.dll is stored as aovk-reloaded.dll. Ctrl+Shift+E switches the Vulkan look. The download test installed and removed that package on `I:\AO\cache\client-vulkan-copy`. Live `I:\AO\client-ddogs` randy31.dll stayed `9d9e0da25ac6f8c7ecdce7a47a2409575945966a29e3b77850f7c62033a04be3`.

Published launcher 2026.10.17-0013. Launcher zip sha256 `c178ab27dbafd9bfaa6917d2adf0f0165a9f086413af1eedd7f86c22bcbae2c0`, size 60555550. `https://dungeondogs.org/downloads/ao/version.json` returns that record. Zone engines were not restarted.

## 2026-10-17 00:20 - Tools under the install folder
Randy's clarification: Tools belongs under the install folder the player chose, for example `<install dir>\Tools`, and it follows that folder when they change it. A folder chosen in Settings is kept until the install folder changes, then it is dropped so Tools moves with the new install. C: is still refused. The folder-resolution check passed. AO-VK is unchanged from 0013: the author's 0.4.1 zip still installs beside our version.dll.

Published launcher 2026.10.17-0020. Launcher zip sha256 `d8f715094f855d87f29fb8b215ba65a03e42542b66d1d67f3ad5e8eb8f40f110`, size 60555738. Zone engines were not restarted.

## 2026-10-17 00:32 - Merc pet assist (not live)
A merc's attack and support pets still only pick a fight of their own inside 30 m of the merc. When that merc is attacking a target, those pets assist that target at any distance and path to it the same way a player pet closes on an attack order. A heal pet still does not auto-attack. When the merc stops attacking, the short leash applies again and the pets follow back.

Side compile of ZoneEngine_New: 0 errors. Not live. Nazzgrom (character 20) was online from 00:25 through 00:32, so the engines were not stopped. Restart waits until the online count is 0, rechecked immediately before stop.

## 2026-10-17 00:57 - Milne quest and claim mail (not live)
Omni-AF Officer Milne (OMCN, the white-kiss tent on Arete) now gives the AO-Universe arrest job. He hands over the OTAF Official Notice 297243. Using it on an Omni-AF Private (LUSN, KLHG, or DART) makes that private follow. When the private is within 8 m of Vanguard Henri Zure the arrest finishes and the notice is used up. If the player walks more than 30 m away the private goes home and the notice stays. A lost notice can be asked for again while the job is open. Reporting back pays the Low Level Omni-Tek Application Form 81753, the Omni-Tek Mission Token 96349, and two Omni-Tek Shoulderpads 248360. The shoulderpad is not unique, so the two pads are two items. Ericson's Clan job is still not built. Antonio's give-list is still waiting on Randy.

Claim Center and daily rewards no longer drop straight into inventory. Login and `.claim` raise the unread-mail count and say the item is waiting at a mail terminal. A Mail Terminal (template 37180) stands beside Antonio on Arete Landing (playfield 6553) and beside him on ICC Shuttleport (playfield 4582, hash EQVE). Take All puts that claim in inventory. Quest turn-in rewards still go into inventory. The account API process was left running, so the Info page sentence may still say the old inventory line until that process is rebuilt later.

Side compile of ZoneEngine_New: 0 errors. Not live. Nazzgrom (character 20) was online at 00:57:15, so the engines were not stopped. The next restart, once the online count is 0 immediately before stop, also carries the merc pet assist from 00:32. A heal pet still does not auto-attack. Commit `ad07698a` (local only).

## 2026-10-17 01:19 - Vendor stock and mail spots (live)
Antonio's shop is stocked on Arete (content vendor, template 248368) and on ICC Shuttleport (hash EQVE). The Omni-Trans vendors OITA and ODDD carry the Omni list. The Clan vendor CNTD carries the Joint Clans Badge and Joint Clans Shades, which is the AO-Universe clan list. There is no Neutral equipment vendor. Shop prices still come from the item value plus the 5% sell modifier. Several of Randy's credit amounts differ from items.dat, so the window will show the data price, not a hand-entered price.

Mail terminals moved to Randy's spots: shuttleport 4582 at 936.4, 47.0, 878.3, and Arete 6553 at 3464.1, 9.0, 805.6.

Online count was 0 at 01:17:34, immediately before stop. Backup `I:\AO\backups\cellao_codex_clean-20261017-0117-vendors.sql`. Zone boot 01:19: leaves=1170 families=79, quests=169 skipped=0, Knubot scripts=84 rejected=0, vending machines=2 skipped=0, no ERROR. Ports 7500, 7501, 7012, 6996, 8098, and 80. This boot also brings up merc pet assist and Milne's arrest quest. Account API pid 81092 was left running. Commit `b7ca9d78` (local only).

## 2026-10-17 01:47 - Dyna weapon loot and Antonio's backpack (live)
SOL K-94 Baby Monster and the KIWD crossbows are valid items: they exist, the rolled QL sits inside the template pair, and they are not NoDrop or unique. The corpse inventory packet was sending them as WeaponInstance identities. Live loot rows use an empty item identity, and the client then ignores the drag. Corpse loot now sends that empty identity. The same scan of the wired dyna pools found no other missing templates and no nano programs sitting in the pools. Unique drops still refuse a second copy: the Wen-Wen line, Senior Omni Intern-Op Coat, Power Puller, and the Empty Data Receptacle. Those flags match the item data.

Antonio on Arete and on EQVE now sells Small Backpack 99228 at QL 1. Its items.dat value is 850, so the window price is that value plus the usual 5%.

Nazzgrom's restore is still not applied. The plan is in `ddogs/docs/nazzgrom-restore.md`. From the 18:25 backup it now also puts Solar-Powered Assault Rifle 121569 back in his right hand and Colonist Survival Pack 296977 back on his back. Do it only after he is offline.

Online count was 0 at 01:45:42, immediately before stop. Backup `I:\AO\backups\cellao_codex_clean-20261017-0145-loot.sql`. Zone boot 01:47: leaves=1170 families=79, quests=169 skipped=0, Knubot scripts=84 rejected=0, vending machines=2 skipped=0, no ERROR. Ports 7500 pid 94672, 7501 pid 104980, 7012/6996 pid 94120, 8098 and 80 pid 81092. Engines left up. Account API was left running. Commit `9d0f1675` (local only).

## 2026-10-17 09:53 EDT - Nazzgrom restore applied
- Followed docs/nazzgrom-restore.md with everyone offline (online=0). Pre-change backup: I:\AO\backups\cellao_codex_clean-20261017-0952-pre-nazzgrom-restore.sql.
- Character 20 stats set from the 18:25 backup: Level 15, XP 81757, LastXP 80150, NextXP 90550, TitleLevel 2, abilities 51, AR 65, Treatment 59, Burst 59, Full Auto 12, Ranged Energy/Multi Ranged/Fling 5. Research, IP and cash untouched.
- Instance ids 5 and 7 were taken (orphan ContainerType 0 rows for char 20), so new ids were used: 150513 Solar-Powered Assault Rifle 121569 (101/6), 150514 Colonist Survival Pack 296977 (102/19).
- No engine restart (character was offline, data loads from DB on login). The 7 ContainerType 0 rows for char 20 (ids 4-9,15) were left alone.

## 2026-10-17 10:20 EDT - Arete list finished (live)
Builder `ddogs/tools/Build-AreteFinish.ps1`, called by Build-NpcTemplates.ps1 after Build-AreteLiveFixes.ps1.
1. Supreme Collector of Waste (SCWS, the client SOCW boss spot at 4067.5, 1.0, 666.6, `.tp 4067 1 667 6553`; wiki.aodb.us "northeast corner") is now a dyna: CampKey boss|, so it wears a boss modifier (glow, 150% dyna size on top of scale 130), keeps the 5-7 item DYAR loot and the 180 s respawn. The pinks around it, CMDI x7 (L7) and STCW (L13), are Waste Collectors (01V1 body 17714 at scale 92, Waste Collector loot with the Antonio parts). GUESSED: SOCW camp = this boss, levels from the client spawn (the wiki checklist says L4).
2. Cleanmeister boss (CMBS, 3544.7, 5.3, 865.3) is a dyna too: scale 170 x 150% dyna size (x the modifier's size), glow, same loot and respawn.
3. Guard-spot pinks IPPC x4 (L40) are ICC Peacekeepers (OIUR look, NoCombat): 3378.8, 17.5, 814.0; 3421.8, 5.5, 781.2; 3443.2, 9.4, 901.8; 3466.9, 6.7, 782.2. Mapping and look GUESSED.
4. Barry the Food Vendor is the VJTN pink at 3516.7, 7.0, 827.0 (due west of Desmond, raised stall). Shop item 248368 (105%). Stock: A Beer Jug 130621, Bottle of Fresh Water 130593, Bronto Burger 130623 (value 29, shows 30 cr like the guide), Bronto Cheese 130624, Chocolate Chip Muffin 130581, Cup of Coffee 130612, Double Bronto Cheese 130625, Ice-Coffee Can 130606, Joka-Poka Can 130602, Vanilla Ice-cream 130603. Desmond no longer hands out a burger; his trade hash QBRG now takes 130623 (was the priceless quest copy 297042). Look needs a reference image (borrowed Food Provider body).
5. Leonora Marty was already in: LEMY at 3440, 9.4, 881 in the market, credit-card return quest unchanged. She stands still (guide says she paces).
6. Tasty Chocolates! (SPAU) and Romantic Flowers! (SLPW) at 3434/3435, 10.3, 848 use gift-box meshes 289979 (birthday gift) and 294206 (christmas gift) at scale 60 instead of the alien drone 253599. The client monster table has no chocolate or flower mesh; real look needs a reference image.
7. Omni-AF Privates (LUSN x4, KLHG x2, DART) wear the Omni-Armed Forces set (Armor 203447, Pants 203449, Sleeves 203530, Helmet 203445, Boots 203446, Gloves 203448) and hold OT M500 SWAT Rifle 124350/124351. Rifle choice GUESSED.
8. Marco Spida (ONRP, 3407.7, 9.4, 831.3, NANO room) sells the 14 profession Nanoprogram Containers (248255-248266, Keeper 300892, Shade 300893). items.dat value is 1500; his shop item 46521 sells at 90%, so the window shows 1,350 at Computer Literacy under 40 (CL discount applies above). Cursor's vendor pass had not stocked him. Reva Albe is not built: no guide or wiki source names her or her spot.

Live after restart 10:19:58 (online count 0 at 10:18:50, immediately before stop; backup `I:\AO\backups\cellao_codex_clean-20261017-1018-arete-finish.sql`, 18649082 bytes): leaves=1174 families=79, quests=169 skipped=0, Knubot scripts=84 rejected=0, vending machines=2 skipped=0, no ERROR. Ports 7500 pid 114732, 7501 pid 106820, 7012/6996 pid 94672, 8098 and 80 pid 81092. Zone build 0 errors. Shop attach lines for Barry and Marco appear when 6553 first loads.

## 2026-10-17 10:46 EDT - Arete looks from Randy's reference shots (live)
- Barry the Food Vendor (VJTN, 3516.7, 7.0, 827.0): that point is Funcom's own client spawn, raised 2 m above the street, i.e. inside the striped Bronto Burger stall; not moved. New Knubot `BarryFoodVendor.json`: "Step on up, stranger. I've got the delicous, meaty goodness that you crave." with replies "What do you have to sell?" (opens his shop) and "Goodbye". Body is still the borrowed Food Provider human (no spiky-hair head or teal-shirt texture id identified).
- Supreme Collector of Waste: moved to FRAG (3533.3, 3.4, 733.7, client level 10, `.tp 3533 4 734 6553`) in the junkyard among the 01V1 Waste Collectors and the rollerrats, matching the shot. Still a dyna (glow, size, 5-7 loot, 180 s). Rust skin: mesh 17316 has a single material "Material #22" (floatingpicker2.png = texture 17318); RDB texture 95885 has the same UV layout in red-brown, sent as an SCFU texture override on that material (Unknown1/2 = 0, untested). The old east point SOCW (4067.5, 1.0, 666.6) is now a plain Waste Collector L13 with its CMDI/STCW Waste Collectors. Build-ShoulderpadsJunkyard now maps FRAG, not SOCW, to the boss. The "Anger Manifestation" in the shot is a Meta-Physicist pet, not a camp mob.
- ICC Peacekeeper (OIUR x7, IPPC x4): ICC Shuttle Guard body 254118, head 40627, scale 105 (green/teal helmet suit with rifle), replacing the plain human 26092.

Live after restart 10:46:04 (online 0 at 10:45:04, immediately before stop; backup `I:\AO\backups\cellao_codex_clean-20261017-1045-arete-refs.sql`, 18649082 bytes): leaves=1175, quests=169 skipped=0, Knubot scripts=85 rejected=0, vending skipped=0, no ERROR. Ports 7500 pid 112832, 7501 pid 105340, 7012/6996 pid 117268, 8098 and 80 pid 81092. Zone build 0 errors.

## 2026-10-17 18:43 EDT - Randy's 18:14 test: why "done" items did not show, and fixes (built and booted; not yet seen in game)

Audit: no work was lost or reverted. Commits 4ca1e1b7 and 61ccb7c3 are in HEAD, and the deployed GameData matched. The boot at 10:46 spawned the new templates (IPPC L40, VJTN Barry, SCWS at 3533.6, 733.3, Marco's and Barry's shops attached). The problem was that loaded data did not do what it was meant to in game:
- Shops: template ShopStock makes a "configured" stock snapshot, and TradeService.IsCurrentAcceptedShop only accepted content-file vendors (NpcContentActivationService binding), so every Use on Marco or Barry was refused silently ("TryUse failed on CanbeAffected:1000032" x many). Fixed: a template vendor bound to its own live NPC is accepted.
- Healing: heal pets spawn in Follow, and both PetAi.Charge and NpcNanoCaster refused to heal in Follow. Salvinous (MPHL) also had no heal nano (Mercs nanos are keyed by merc profession). Merc templates have no nano skills or nano pool, so the 26465 heal (needs BioMet>16, MatMet>18, 15 nano) failed the cast gate. Fixed: heal pets heal in Follow (only Wait stops them), a heal pet with no nanos gets the doctor heal band, and pet/merc casters skip the skill and nano-pool gate.
- Supreme Collector: TextureOverride on "Material #22" left only the aura showing. The random dyna modifier could roll "Big" (HP x3, size x1.5 x1.3 on top of scale 130). Now: no override, scale 100, DynaModifier Fire, 300 HP.
- Dynas: IsDyna skipped nascence| camps (49), so they had no aura. Added.
- Corpse loot: the server logged nothing after a corpse opened, and InventoryMoveService drops a failed loot source silently. Added a Warn line with the source identity and the reason, plus an Info line per loot move, so the next try shows exactly why the KIWD 37 Hunter Crossbow will not move. Not fixed yet.
- Data (Build-AreteFinish.ps1 18:30 pass): Peacekeepers drop breed/sex (matches captured ABGU); Leonora body 26092 ("solitus male") -> 203736 "deranged shopper" (guess); HX1V/HXHR are now live Zix (L10) and Cha'Heru (L10, 250 HP); 6 cricket points JXSC (Scout - Jaax'Sinuh, body alien_mini_bug 251782) around the crash site; YODE x20 and GADP on the leet hill are Leets L10-13; Leet King moved to 3693, 31.6, 672 (middle of the hill); Mario Carles moved to the rollerrat flats 3470, 2.5, 690 (Wander); 6 CMDI Waste Collectors L7 around the boss; 2 pink points under standalone shops removed (ICTE, JUSO).
Online count was 0 at the recheck right before the 18:41:05 stop. Backup `I:\AO\backups\cellao_codex_clean-*-arete-fix1830.sql`. Boot 18:42: leaves=1178 families=79, quests=169 skipped=0, Knubot scripts=85 rejected=0, vending skipped=0, skips removedPoints=47, 7501 listening. Playfield 6553 loads only when someone zones in, so spawns, shops, and heals still need a check in game.
Still open: store/daily pages (8098 serves /, /store, /daily on LAN and public; port 80 public is refused; the in-game failure is likely the client patch, not verified), mail terminal (configured at 3464.1, 9.0, 805.6 template 37180 and the item exists; whether the client draws it is unverified), IIV-X look (no reference matched), Harvey (spawn REGD 3769.3, 28.6, 565.2, Wander, minibull body), pink "3846" x14 and other unmatched points in the market.

## 2026-10-17 19:30 EDT - store/daily, mail terminals, Supreme Collector move, fleas
- Store/Daily 502 was Funcom's nginx: client patch aborted when AO-VK replaced randy31.dll, so web routing never armed. dllmain.cpp now logs a WARN and continues. Package built, NOT yet released to clients.
- Server: ddogs/tools/Test-StorePages.ps1 (client-style Host-header check); I:\AO\tools\account-api\watch-account-api.ps1 hooked into boot-engines.cmd (re-check, restart Account API task on failure). Restart script runs the page check after boot.
- Mail Terminal content NPCs (body 279184) on 6553 (3464.1,805.6) and 4582 (936.4,878.3); NpcCharacter.TryUse opens the claims list. Static MailTerminals removed.
- SCWS moved to 3506.0/941.8/11.1; fleas near boss -> CMDI L7, near Docker -> 01V1 L5.
- Restart 19:25 (backup arete-fix1930), quests=169, Knubot rejected=0, page check 4/4 PASS.

## 2026-10-17 19:35 EDT - launcher release 2026.10.17-1935
- Published the AO-VK-safe version.dll (sha f59689e5...) via tools-temp/publish-launcher.ps1: launcher zip 3c2567cb..., patch zip d4024832..., version.json bumped from 0020.
- Verified via https://dungeondogs.org/downloads/ao/version.json (launcher UA, from box and RC-PC), launcher zip downloaded and hash-matched, patch zip range-fetched and matched; zip's version.dll hash = build.
- Rollback: I:\AO\backups\ao-release-2026.10.17-0020 (version.json, Patch.zip, Launcher.zip, release.json, old version.dll); the 0020 launcher zip is still in downloads/ao.

## 2026-10-17 20:50 EDT - battle suit looks
- Research: battle suits (and Divaad's Armor, Charred Abaddon Chassis, Hellfyre Magma Suit, Cyborg/Catgirl costumes) are Armor-class BACK-slot items (Slot 8) whose OnWear MonsterShape (53060) swaps the whole body to a MonsterData record (e.g. Desert 273570). ToWear needs MonsterData 0, so only one suit at a time. The server never blocked other armor; what you see is the client drawing a monster body.
- Engine: Core/Appearance (AppearanceGroups + AppearancePrefsService, polls ddogs_appearance_prefs every 3 s and rebases the player). Player.ApplyAppearanceGroups: look off removes an armor suit's body; a suit in the Social back slot shows its body only (no stats) while social is shown; 'only' clears worn meshes/textures (Character.HideWornAppearance). .battlesuit command sets the same toggles.
- Account API: /battlesuits page (claim code, two toggles) built and staged in I:\AO\cache\temp\account-api-publish but NOT live: the running API (pid 81092) can't be stopped from this account (Access denied), so the hub link was left out until it is swapped.
- DB: ddogs/sql/ddogs_appearance_prefs.sql applied (table + aoaccount grant). Restart 20:43 (backup battlesuit2050), quests=169, Knubot rejected=0, page check 4/4 PASS, prefs poll verified with a test row.

## 2026-10-17 21:10 EDT - account API redeploy (Battlesuits page live)
- Randy killed the old admin-started API (pid 81092). Old bin-publish backed up to I:\AO\backups\account-api-bin-publish-20261017-2100; staged build swapped in and started ONLY via the task (AMP, S4U, RunLevel Limited = not elevated). New pid 90448, session 0.
- Battlesuits link back in store.json; /battlesuits 200 on 127.0.0.1, LAN and ao.dungeondogs.org with hub link, back link and toggles. Test-StorePages now also checks the public route: 6/6 PASS.
- Restart test: Stop-ScheduledTask ends only the task's cmd.exe; dotnet stayed (orphan 90448, still serving) and Stop-Process from our session is Access denied (other logon session, not elevation). Fix: API now exits when its cmd parent exits (ParentWatch, verified on scratch port 18098); watchdog and swap script stop via the task. That build is staged in I:\AO\cache\temp\account-api-publish and needs 90448 killed once more (admin) before it can be swapped in.

## 2026-10-17 21:15 EDT - account API restartable via its task
- Randy killed orphan 90448; ports were free. Backup I:\AO\backups\account-api-bin-publish-20261017-2112, ParentWatch build swapped in, started only via the task (AMP, S4U, Limited).
- Real test: Stop-ScheduledTask -> service exited, 80/8098 closed, task Ready; Start-ScheduledTask -> back on both ports. Watchdog -ForceRestart (new test switch) stopped and restarted it (pid 125004 -> 125880), post-check PASS; normal watchdog run exit 0.
- Test-StorePages 6/6 PASS (local, LAN, ao.dungeondogs.org); /battlesuits 200 on all three with hub link and back link.
- watch-account-api.ps1 now also kept in ddogs/tools/account-api (live copy stays in I:\AO\tools\account-api).
