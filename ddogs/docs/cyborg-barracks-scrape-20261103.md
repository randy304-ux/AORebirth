# Cyborg Barracks (pf 1833) scrape import — 2026-11-03

**Source:** Whozewhatzit live scrape 16:02–16:06 ET  
**Tag:** `scrape-cb-20261103`  
**Status:** staged only (GameData + SQL migration committed; not applied to live DB by default)

## Counts

| Item | Count |
|---|---|
| NPC templates (hashes filled) | 15 |
| Client spawn points (`Playfields/1833/Spawns.json`) | 36 |
| Scrape instances matched 1:1 | 36 |
| SpawnsAdd.json rows | 0 (not needed) |
| Roamer hashes (Wander) | 6 |
| Loot item id pairs | 4 |

## Boss

| Field | Value |
|---|---|
| Name | Commander Jocasta |
| Hash | **THVI** |
| Level | 90 |
| MonsterData | 154698 |
| Mesh | 154695 |
| MaxHealth | 22632 |
| Side / Family | Monster (3) / 131 |
| Merc unlock | `soldier_cyborg` BossHash=THVI (`MercRoster.json` + SQL migration) |

## Templates

- **THVI** Commander Jocasta L90 md=154698 mesh=154695
- **TEYN** Techleader Praetor L84 md=17645 mesh=82132
- **GEXL** General Severus L83 md=17639 mesh=82133 (loot 283744)
- **PRIN** Prototype Inferno L83 md=17639 mesh=82133 Wander (loot 283746, 153976)
- **ERVT** Eradicator Deimos L79 md=17641 mesh=82122
- **AUCO** Augmented Cyborg Hellfury L80–81 md=17641 mesh=82122 Wander (loot 153977)
- **CYHE** Cyborg Hellfury L77–79 md=17639 mesh=82133 Wander (loot 283746)
- **AUCB** Augmented Cyborg Devastator L75–77 md=17637 mesh=82126 (loot 283746)
- **AUCY** Augmented Cyborg Eradicator L75–76 md=17639 mesh=82133
- **CYDE** Cyborg Devastator L74 md=17647 mesh=82126
- **CYER** Cyborg Eradicator L74 md=17645 mesh=82132 Wander
- **CYGA** Cyborg Guardian L70–73 md=17643 mesh=82128 Wander
- **CYGU** Cyborg Guardian L70–73 md=17643 mesh=82128
- **CYTE** Cyborg Technician L70–73 md=17647 mesh=82126 Wander (loot 283744)
- **EIQU** Janella Gheron L160 md=26123 mesh=23373 NoCombat/NoMove (quest NPC; scrape labeled other_player, wiki + client hashpoint EIQU)

## Guesses / fill decisions

- All 15 NPCHash values were empty in the scrape; filled from existing client `Playfields/1833/Spawns.json` pink hashpoints (not invented Bxxx codes).
- Combat secondary stats cloned from nearest-level non-guessed donor band in `NpcTemplates.json`, then overwritten with scraped name/level/hp/side/breed/MonsterData/scale/mesh/head/texture/NPCFamily/Flags.
- Side Monster/Unknown → stat 33 = 3; Neutral (Janella) → 0. Breed Monster → 0; Opifex → 2.
- Roamer hashes CYGA, AUCO, CYTE, CYHE, CYER, PRIN: `Features+=Wander` from `npc_moves` MoveType=moving.
- Loot chances guessed from observation counts (1→25%, 2→50%, 3+→75%); LowID/HighID are real scrape ids (283744, 283746, 153976, 153977). No Jocasta loot this pass.
- Janella included as friendly EIQU despite scrape `other_player_in_dungeon` note (client L160 hashpoint + wiki quest NPC).
- No SpawnsAdd: 36 client points matched 36 scrape instances.

## How to deploy

1. Rebuild GameData if needed:
   `powershell -NoProfile -File ddogs\tools\Build-ScrapeImportCB.ps1`
2. Stage into Built tree (no live DB):
   `powershell -NoProfile -File tools-temp\stage-cb-spawns.ps1`
3. When Mike authorizes live:
   - backup DB
   - apply `AORebirth\Libraries\Source\AORebirth.Database\Migrations\20261103_cyborg_barracks_thvi.sql`
   - robocopy GameData into ZoneEngine_New and restart ZoneEngine_New only
   - or `tools-temp\stage-cb-spawns.ps1 -ApplyLive` after online=0
4. Live verify: enter pf 1833, confirm trash + Commander Jocasta (THVI), Soldier merc unlock on kill.

## Files

- `ddogs/tools/scrape_import_cb_20261103.json`
- `ddogs/tools/Build-ScrapeImportCB.ps1`
- `AORebirth/GameData/NpcTemplates.json`
- `AORebirth/GameData/ItemTemplates.json`
- `AORebirth/GameData/MercRoster.json`
- `AORebirth/Libraries/Source/AORebirth.Database/Migrations/20261103_cyborg_barracks_thvi.sql`
- `AORebirth/Libraries/Source/AORebirth.Database/Migrations/20261103_character_merc_unlocks.sql`
- `tools-temp/stage-cb-spawns.ps1`
