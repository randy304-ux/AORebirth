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
- Static-dungeon spawns. Temple of Three Winds (pf 1931), Steps of Madness (1933), Crypt of Home (4805) and Temple of Home (1404) have no client district spawns, so they need hand-made `Spawns.json` points. TOTW bosses from AO-Universe/AOWiki: Defender of the Three (lvl 40-42, Nanofreak), Windcaller Yatila 55-57, Gartua 62-65, Lien 64-66, Nematet 66-68, Khalum 73, Aztur 74 (HP 38630). The cultist/human models still need a source.
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
