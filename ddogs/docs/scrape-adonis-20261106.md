# Adonis full scrape import — 2026-11-06

**Source:** `I:\AO\imports\scrape-20261106-ado\` (raw + gpt-review)  
**Tag:** `scrape-adonis-20261106`  
**Status:** staged only (GameData committed locally; deploy script written, not run)  
**Base tip:** `cursor/gil-note-drop-0f04` (Gil Notes + Joker's Glyph stack-of-6)

## Staged counts (hostable playfields)

| PF | Zone | SpawnsAdd | Notes |
|---|---|---:|---|
| 4872 | Adonis City | 671 | +7 provisional Yuttos vendors in Npcs.json |
| 4873 | Adonis Abyss | 580 | Abyssal Stalkers at calm/start |
| 4213 | Maze_Styx_ado_red | 88 | named bosses + pink fills |
| 4625 | Redeemed Temple | 32 | standing residents only |
| 4626 | Unredeemed Temple | 38 | standing residents only |
| 4335 | Dark Ruins Solo | 0 | omitted from scrape extract |
| — | New templates (NpcTemplates.Adonis) | 296 | includes pink fills + gen-dungeon templates |
| — | Loot item families (ItemTemplates.Adonis) | 150 | |
| — | Pink hash fills (no SpawnsAdd) | 591 | spawn slot already in Spawns.json |
| — | Gen-dungeon template-only records | 208 | ACG instance IDs not hostable |

## Holdbacks

| Holdback | Why |
|---|---|
| 9 loot duplicate pairs | early double-log; counted once (prefer named/high-slot) |
| Abyssal Stalkers / large chase spans | placed at calm/start (`movement-review.csv` first pos); 95 calm uses |
| Pets (Tempo/Harmony/Tone/Alarm Sentry) | `excluded-pets.csv` / IsPet — already absent from observed-locations |
| World objects | `world-objects.csv` — already absent from observed-locations |
| Guard Turret / Security Camera (9) | worldish; not standing NPC spawns |
| Temple Ancient-Container summon chain | guide names not present as standing rows; not invented |
| Generated dungeon SpawnsAdd (−1073474808 / −1073343735 / −1073278199) | engine cannot host signed ACG instance IDs as permanent SpawnsAdd |
| Dark Ruins Solo 4335 / gen −1073278199 detail | omitted from extract; no rows |
| Gloves | blank in scrape; left blank |
| Near existing templated / claimed pink | skip duplicate placement |

## Unresolved

- **7 Yuttos vendors:** stock imported at matching named Yuttos positions using nearby-dialogue provisional names; typed `VendingMachine`→NPC instance join unproven (listed unresolved).
- **24 name/MonsterData collisions:** skipped (not guessed).
- **Doors:** 16 actual door rows kept as evidence only — no DoorsAdd loader; maze 4213 has no `Doors.dat` in Playfields.
- **Eradicus / gen-dungeon named bosses:** templates + loot staged (Eradicus includes Joker's Glyph 227905; stack-of-6 override already present). Interior positions not copied outdoors (would invent). Report: place into a future hostable dungeon layout or keep template-only until ACG hosting exists.
- **Mooching / Skulking Dryad:** loot appended to existing templates; no SpawnsAdd (loot-only, no spawn row).

## Rollback snapshot plan (deploy-time)

`tools-temp\deploy-ado.ps1` (when authorized) will:

1. Refuse if `Online!=0` or last boot &lt; 5 minutes.
2. `mysqldump` → `I:\AO\backups\cellao_codex_clean-<stamp>-scrape-adonis.sql`.
3. Copy current live GameData stage files → `I:\AO\backups\gamedata-rollback-adonis-<stamp>\`.
4. Stage overlays + SpawnsAdd + vendors; restart Zone/Chat via schtasks.

Manual rollback: restore files from that rollback dir into `AORebirth\Built\Debug\ZoneEngine_New\GameData` (and Built\Debug\GameData), restart Zone/Chat. DB dump is belt-and-suspenders (no schema change expected).

## How to apply staging

```cmd
node ddogs\tools\plan-adonis.js
powershell -NoProfile -File ddogs\tools\Build-ScrapeImportAdonis.ps1
```

## Deploy later (do not run until authorized)

```cmd
powershell -NoProfile -File tools-temp\deploy-ado.ps1
```

## Files

- `ddogs/tools/plan-adonis.js`
- `ddogs/tools/scrape_import_adonis_20261106.json`
- `ddogs/tools/Build-ScrapeImportAdonis.ps1`
- `AORebirth/GameData/NpcTemplates.Adonis.json`
- `AORebirth/GameData/ItemTemplates.Adonis.json`
- `AORebirth/GameData/PlayfieldContent/{4872,4873,4213,4625,4626}/SpawnsAdd.adonis.json`
- `AORebirth/GameData/PlayfieldContent/4872/Npcs.json`
- `tools-temp/deploy-ado.ps1`
