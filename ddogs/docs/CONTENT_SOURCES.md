# Dungeon Dogs content sources (ddogs fork)

This file records where our fork's content comes from. No Funcom files are stored here, and none of this is published.

## NPC catalog (`AORebirth/GameData/NpcTemplates.json`)
It's built by `ddogs/tools/Build-NpcTemplates.ps1`. `Build-NpcTemplates.last.txt` lists every generated leaf with its level band.

| Input | Used for |
|---|---|
| DB `mobspawns_stats` (captured spawns in pf 954) | Full stat sets for LE01, LE02, AD01, RR01, RR02, CL01, CL02, BFC1/2, EM01/2, HW01, BTTM. Also the "skeleton" stats for every derived leaf. |
| DB `mobtemplate` (old CellAO catalog) | A0xx leaves (name, level, health, model, family) |
| `Playfields/*/Districts.json` (extracted locally from the client RDB) | Real server NPC hash names (CL02-CL14, RR01-RR10, TM01-TM14, SH02-SH14, UB02-UB14, LE01-LE06, AD01-AD06, EY01-EY14) and the level range of each district that spawns them. A leaf takes the narrowest range found. Hashes that only appear in broad ranges (over 40 levels) are skipped. |
| `docs/evidence/MISSION_DUNGEON_NPC_POPULATION_EVIDENCE.md` (upstream) | Names TM01, UB01, RR08, AD04, CL08, TM08, SH08 and UB08. Models: UNBR 44143 / family 73. |
| AO-Universe dyna-camp table | Cross-check of family level ranges, e.g. Unfinished Breed 60-65 in Varmint Woods, Clawfingers 40-45, Shadowmutants 50-55 / 80-85. Only levels are used; nothing copied. |

**Formulas (approximate, can be tuned in the script):**
- Health is log-log interpolated between real DB points: (1,20) (3,98) (5,115) (9,287) (12,365) (15,504) (24,500) (104,6236) (139,10032) (190,16798) (214,20077).
- Damage for levels above 5: MaxDamage = 1.5 x level, MinDamage = 0.5 x level. The captured low-level mobs all carry 3/1.
- Scale is set to 100 for levels above 30.
- Leaves without an evidence name get the family's generic name (Clawfinger, Rollerrat, and so on).

**Caveats:** derived NPCs have no equipment, skills or loot tables. Their combat strength comes only from level, health and the damage formula. BIOF (Biofreak) has no family because no model or level data above level 4 exists.

## Missions (`DungeonGeneration.json`)
- The mission population uses ANDR, ROLL, CLAW, TENT, SHAD and UNBR.
- The Kill Person and Find Person targets use the same families, replacing MFPB. In AO the mission "boss" is a regular mob. The engine resolves each family to the leaf that matches the mission's level.

## Not yet sourced
- Static-dungeon spawns. Steps of Madness (1933), Crypt of Home (4805) and Temple of Home (1404) have no client district spawns and still need hand-made `Spawns.json` points. Temple of Three Winds (1931) is built, see below.
- Dynas: the engine has no dyna buff or loot hook (no code references), so they aren't implemented.
- About 1,200 other district hashes (other families) have no model source yet, and spawn as the AAAA placeholder or not at all.

## Condemned Subway (PF 127), added 2026-10-13
- 28 leaves (DRSH + 27 codes). Names and recorded stats (model 359, family 455, side, look) come from docs/accepted/npc/delmus/NpcTemplate.json. Level bands come from the client PF127 Spawns.json. Health is rescaled from the recorded level with the shared curve.
- DRSH "Deranged Shopper": the name is from Randy. There is no upstream record, so it reuses the Looter (DCQV) stats and look.
- Spawns: the engine's HashSpawnSystem already reads the client PF127 Spawns.json (gitignored, local-only), so it uses the exact positions and respawn times. 313 of 326 points now resolve.
- Points the engine skips on purpose, because they have no template: CARO x5 (Randy: no L20-40 mobs there), ASSU (Abmouth: no model/stats source), PCBN, and the 6 vendor codes (the vendors come from PlayfieldContent/127/Npcs.json).
- Design images and the CSV are in I:\AO\design\subway (local, not committed).

## Abmouth Supremus and subway loot, added 2026-10-13
- ASSU: level 30, HP 10324, melee damage 73-148, from AOWiki/Fandom. AO-Universe (updated 05.27.2026) puts him in the final room with Vergil spawning nearby. The spawn point's respawn of 550 s and 100% chance come from the client Spawns.json. Fandom says 20-30 min with less than 100% chance; the client value is kept.
- Model: no recorded Abmouth model exists. He borrows the Infector look (INFE, model 31909, NPC family 150; his two guards), at scale 150.
- Not implemented: the "Summoned" nano (teleports the target to Abmouth and roots for 5 s), and the two Infectors that spawn when he is attacked. The engine has no hook for either.
- Loot uses the engine's per-band LootTable (Hash/Repeats/Chance/LevelMod). New item hashes in ItemTemplates.json, with ids matched by name from the itemnames table:

  | Hash | Item | Ids |
  |---|---|---|
  | SWRN | Ring of the Nucleus Basalis | 202717/202718 |
  | SWBJ | Belt of Justice | 202733/202734 (lowest of 3 id pairs) |
  | SWJT | Juggler's Treat | 202741/202742 |
  | SWBS | Boots of Infinite Speed | 202722/202723 |
  | SWCS | Construction Sleeves | 202756 |
  | SWSB | Support Beam of Intolerance | 202719/202720 |
  | SWVC | Vagabond Cloak | 204397 |
  | SWFT | The fr00b T-shirt | 258543 |
  | SWED | Eleet Doll | 204396 |
  | SWLS / SWLP / SWLH / SWLG | Living Cyber Armor Sleeves / Pants / Helmet / Gloves | |
  | SWMM | Morphing Memory | 202726, stage 1 |

- Tables are wired for ASSU, VEAE, MENI, STFO, WOST, EHDL, VOVG, PMPE, DIPE, DOPT, MDPA, FASO, MEML, IORB, NUBR and EMSE. Only Abmouth's Ring at 100% is sourced (Fandom); all other chances are placeholders, since the guides only say rare or uncommon.
- Not wired: the Illegally Modified Ofab weapons (any subway mob, mostly Workman Strikers), the Ergo-ring parts, stims and kits, and generic loot.

## Shared common loot (reusable) and the Subway regular loot, added 2026-10-13
- Item QLs were read from the local client RDB (record type 1000020, stat 54) with I:\AO\cache\itemql. Randy picked the stim and kit; aogalaxy.com confirms the names and QL1 values.
  - HNST: Health and Nano Stim (combat stim), 291043 QL1 / 291044 QL200.
  - HNRC: Health and Nano Recharger (sit-down kit), 291082 QL1 / 291083 QL100.
- Families in ItemTemplates.json:
  - CMTR (trash/vendor): Monster Parts 42640/42641, Blood Plasma 154358/154359, Small Power Supply 234877, Circuit board 234875, A Burnt Out Memory Chip 234876, Copper Wire 234874.
  - CMHK: HNST and HNRC.
  - OFAB: OF01-OF12, the Illegally Modified Ofab Bear/Boar/Cobra/Hawk/Mongoose/Panther/Peregrine/Shark/Silverback/Tiger/Viper/Wolf, ids 301707-301718. These are single-id QL1 items.
- CommonLoot(level) in Build-NpcTemplates.ps1 gives rows that any zone's leaves can reuse:
  - Below level 15: CMTR 40%, CMHK 8%, OFAB 1%.
  - Level 15 and up: CMTR 50%, CMHK 12%, OFAB 1%.
  - All rows use LevelMod 20, so the item QL is the mob level +/-20%, which the engine interpolates.
- Extras: STFE (Stim Fiend) also gets HNST 60% x2 and HNRC 35%; WOST (Workman Striker) gets OFAB 5%.
- Credits: the Cash stat (61) is set to level x 10 on every subway band. The engine gives the cash to the first corpse opener.
- Fixes: the SWLP id pair is corrected to 160051 (QL1) / 160050 (QL200). Belt of Justice 202733/202734 is QL1/QL20.
- Every chance here is a tunable placeholder.
- Workman Striker (Randy): Support Beam of Intolerance (SWSB, 202719 QL10 / 202720 QL30) at 40% with LevelMod 25, so QL spreads around the mob level and the engine clamps it to the 10-30 range. OFAB is 5% on Strikers and 1% on every other subway mob. The 12 Ofab weapons match AO-Universe's list exactly. Each is a single fixed-QL1 item (aogalaxy: no QL variants), so no QL spread is possible for them.
- Update: the ASSU model is now the client's own Abmouth MonsterData 155962. Its record string is 'abmouth' and its catmesh 155548 contains abmouth.cir, matching Randy's screenshot of the rust-red maw brute with a flail. It is at native scale 100. The raid version 'abmouth indominus' is MonsterData 305016, and the client's sub_boss_abmouth_VeryHigh* weapons are items 305909-305923; neither is used. AO-Universe's raid guide gives no raid stats. Raid specials: Devour (stomach room), flames, knockback, and Harbingers of Pestilence adds.

## Boss triggers (engine feature, added 2026-10-13)
- An NpcTemplates.json band can carry "BossTriggers": [{ On: "FirstAggro" | "HealthBelow", Pct, Actions: [{ Type: "SpawnAdds", Hash, Count, Radius, Level? }] }].
- Each trigger fires once per fight. NpcCharacter.ResetBossTriggers clears them on death (ReleaseForDeath) and on leash (OnReset).
- Adds spawn in a ring of the given Radius around the boss, at the boss level (clamped to the add template's range), and are given threat on the attacker. Any adds still alive when the boss dies or resets are despawned.
- Adds are runtime-only. They are never written to the DB or to Spawns.json.
- Code: Mobs/MobTemplates.cs (NpcBossTrigger, NpcBossAction), GameData/NpcTemplateCatalog.cs (band to template copy), Entities/NpcCharacter.cs (RunBossTriggers, RunBossAction, ResetBossTriggers).
- Not yet: cast and teleport/root actions (for example Abmouth's Summoned). Unknown action types are logged and skipped.
- ASSU: 2x INFE at FirstAggro, and again below 75%, 50% and 25% health.

## Newbie zones: ICC Shuttleport (PF 4582) and Nascense Frontier (PF 4310), added 2026-10-13
- Built by section 8 of `ddogs/tools/Build-NpcTemplates.ps1`. Spawn points are the client Spawns.json of each playfield (local, gitignored), so positions, respawn times and level ranges are the client's.
- Names and recorded stats (MonsterData, scale, head mesh, health at the captured level, side/breed for PF 4582) come from the upstream capture-backed spawn lists that were retired in commit ecefa673: `IccShuttleportSpawn.cs` (35 NPCs) and `NascenceLifeSpawn.cs` (868 NPCs; 246 on PF 4310). The script reads them with `git show ecefa673^:...`.
- Matching: each spawn point votes for the nearest captured NPC (within 25 m). A code is accepted when at least 60% of its points agree with a mean distance of 10 m or less, or when a single point is within 3 m. Health is rescaled from the captured level with the shared curve.
- PF 4582 hostile codes use the old CellAO `mobtemplate` rows (A000-A038) that `docs/evidence/PF4582_TEMPLATE_HASH_RESOLUTION_AUDIT_20260824.md` lists as candidates. The seven one-point named rares are assigned by level and kind; these are guesses: BTMO Sparepart the TechnoMaster, BDMO Wirebrain, BJMR Chipbrain, BEML Redeye, BDML Burnclaw, BLMM Charscale, BSMG Scurryflutter. The audit names BSMG's live NPC "Dreadknot the Toxictwister", but no record of it exists.
- PF 4582 social NPCs (NoCombat): Brandon Thorn, ICC Bio-Inspector, Adri Afeli, Vendor Antonio Stacklund, Omni-Trans and Clan Equipment Vendors, Clan Recruiter, Omni-Tek Recruitment Officer, Neutral Observer, Omni Unicorn Squadleader Fixx, Clan Field Surgeon Elsa Oosta, Manager Travis Molen, and the 12 ICC Shuttle Guards. The three vendors carry EQVE's recorded shop terminal 248368, so all three show the same stock from VendingMachines.json.
- PF 4310 social NPCs (NoCombat): Joshua Falker, Libbie Hyman, Sabina Florenta, Dr. Rosenblatt, Scientist Donna Red, Scientist Veronica Escobar, Emissary of Jobe, and Guardian of the Weak. Every other matched code is hostile and gets CommonLoot plus credits of level x 10.
- Not matched (no spawn): PF 4582 NCNN. PF 4310: SCWA, AKHE, MAYN, VDCN, RDGA, REGA, SAPD, YTGS, LRWH, LRBC, APAG, DIGO, JHBL, NSWL, THHW, KPGF, CRIM. PF 4311, 4312 and 4001 are not done yet; the same capture list covers 4311 and 4312.
- Caveats: hash-spawned NPC templates carry no clothing textures (the template format has none), so humans show their base MonsterData look. Dialogue exists only where a Knubot script exists (Brandon Thorn, plus the merc recruiters).

## Mercenaries, added 2026-10-13
- Code: `Core/Pets/Mercs.cs`, `PetService.HireMerc/DismissMercs`, and Knubot tags `{hiremerc doctor|enforcer|soldier|keeper}` and `{dismissmercs}` (`Core/Knubot/KnubotMercs.cs`).
- A merc is an ordinary player pet. It is spawned with SpawnSource.Summoned and lives in memory only (ZoneEngine_New never saves NPCs), so mercs cannot pile up in the DB. They follow their owner through zone changes and leave on logout or owner death.
- Templates are MRDR, MREN, MRSO and MRKE (section 9 of the generator), with 13 level bands from 1 to 220. A merc spawns at the hiring player's level. Profession stat 60 is set (Doctor 10, Enforcer 9, Soldier 1, Keeper 14). HP/damage multipliers: Doctor 0.8/0.6, Enforcer 1.6/0.9, Soldier 1.0/1.3, Keeper 1.3/1.0.
- Slots: each player can hire one merc of each profession. Enforcer, Soldier and Keeper use their own pet slots (10, 11 and 12). The Doctor uses the heal slot, so the pet AI heals its owner, but it replaces a player's own heal pet. The Doctor's heal nano depends on level: Minor Healing 26465 (under 25), Healing 26466 (under 75), Improved Healing 28665 (under 150), and Superior Healing 28676 above that.
- Toggle: `GameData/Mercs.json` `"Enabled": false` stops hiring (read at each hire).
- Recruiters are PlayfieldContent NPCs ("Mercenary Recruiter (Doctor)" and so on, ContentNpcIdentity `ddogs-merc-<kind>`). Four stand next to each start point: PF 4582 at about (922-934, 740), PF 6553 Arete Landing at (3445-3457, 857), and PF 4310 by the Emissary of Jobe at (846-858, 1068). Scripts are in `GameData/Knubot/Merc*.json`.

## Temple of the Three Winds (PF 1931), added 2026-10-13
Built by `ddogs/tools/Build-TotwContent.ps1`, which `Build-NpcTemplates.ps1` calls at its end. The build report is in `Build-TotwContent.last.txt`.
- **Spawn points.** The 251 points in `GameData/Playfields/1931/Spawns.json`. Every one of the 51 spawn hashes now has a template; the builder throws if one is missing.
- **Names and boss mapping.** From the AO-Universe TotW guide and loot table. Captured boss positions (retired `CapturedTempleOfThreeWindsEncounterRuntimeService.cs`, git `ecefa673^`) fall within 0.4 m of the DEOF, THCU, NMTH, GUOT, GATH and UKTH spawn points. The other named hashes are mapped by their name codes, e.g. WIYA = Windcaller Yatila, REGU = Reverend Gulard, TRRN = The Re-Animator, LITH = Lien the Memorystalker.
- **Unnamed cultists.** ULTI, COKC, CULT, PCFH, BBOI, KNSL and OLSE (levels 20-35) have no known names, so they are all "Cultist". Each uses one of the 7 captured cultist profile seeds (MonsterData and head).
- **Bosses.** Level, HP, max damage and scale come from the capture for Defender, Curator, Nematet, Guardian, Gartua, Uklesh, Khalum, Aztur, Yatila, Gulard, Re-Animator and Betany. Lien has no capture and uses the Uklesh model with curve HP. Everything else uses curve HP and damage of level x1.5.
- **Boss triggers.**
  - Uklesh death spawns Khalum (KHAL), and Khalum death spawns Aztur (AZTR). This uses the new engine trigger `On = "Death"`, whose adds are not despawned with the boss.
  - Lien calls 3 Ju-ju Dolls (JUJU) on first aggro and again under 50% health. They vanish when he dies, as in the guide.
  - The Guardian's 3 Eternal Guardians are ordinary spawns.
- **Loot.**
  - Item hashes T*** come from ItemTemplates.json, with ids from itemnames, matched by name. TSFA is the 6 Shadowfade pieces; TBKA and TBKT are the Ape Fist and Tree of Enlightenment book sections.
  - Who drops what follows the AO-Universe loot table. Guardian Circuit Board drops 100%; every other chance is a placeholder.
  - Caska (CSTE) drops Red Berry Wine 100%, for Dante Karrall's "Like Fine Wine".
  - CommonLoot is on every mob, plus Enhanced MTI M-2 Bipower at 1% ("any mob").
- **Entrance.** The client door in Greater Tir County (647, about 1803x2695) should be a native TeleportProxy into 1931; the engine logs 1931 exit door C024078B. No teleports row was added. **Not verified in game.** If it lands wrong, add a teleports.sql row as was done for the Subway (806c1d9b).
- **Not built.** Windcaller Karrec's ICC HQ portal quest (Bronto Burger + Maddy's Credit Card) and the level-60 entry cap. The engine has no door level gate yet.

## Nascence garden keys, added 2026-10-13
Built by `Build-GardenContent.ps1` (NPCs, items GADV/GINA/GINT/GAPG/GAPF/GKAB/GKTH, insignia drops on LLBE/SLCM/SWSI) and the garden section of `Build-NewbieQuests.ps1` (quests and Knubot). Mission bits 640-644; see `MISSION_BITS.md`.
- **Aban (Clan), from the AO-Universe guide.**
  1. Donna Red gives the Ancient Device.
  2. Ecclesiast Aban Fala (PF 4312) is shown an Insignia of Aban.
  3. Sipius Aban Lux-Wei (garden, PF 4676) combines device + insignia into the graced Analyzer.
  4. Kill 3 Dreaming Silvertails (DRSL).
  5. Return the Analyzer for The Key to the Garden of Aban (226824).
- **Thrak (Omni/neutral), from AOWiki and the retired capture quest pack `Content/Thrak/garden-key`.**
  1. Veronica Escobar gives the device.
  2. Prophet Yutt Thrak (Brawl, PF 4311) is shown an Insignia of Thrak.
  3. Hypnagogic Urga-Lum Thrak (garden, PF 4677) takes device + insignia, pays 7000 XP and 10000 credits, and gives the favored Analyzer.
  4. Curse 3 Dreaming Silvertails.
  5. Return the Analyzer for The Key to the Garden of Thrak (226994), plus 462 XP and 800 credits.
- **NPCs.** Looks and positions come from retired captures (`NascenceLifeSpawn.cs`, `ThrakOmniGardenSpawn.cs`).
- **Simplified.**
  - The insignia + device combine happens in a trade with the garden NPC.
  - The silvertail chat-trade became a kill objective.
  - Starters have no faction or level gate. The key items carry their own requirements.
