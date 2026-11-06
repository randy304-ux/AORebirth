# Scrape pilot pf 685 (Galway County) — 2026-11-06

**Source:** GPT/Randy reviewed export `I:\AO\imports\scrape-20261106-review\685\` (`missing.csv`; `wrong.csv` empty)  
**Tag:** `scrape-galway685-20261106`  
**Status:** staged only (GameData committed locally; deploy script written, not run)

## Staged counts

| Item | Count |
|---|---:|
| Sightings in (`missing.csv`) | 1747 |
| Spawn points added (SpawnsAdd) | 1218 |
| Spawns reusing existing hash | 363 |
| Spawns on new templates | 855 |
| New NpcTemplates (P02A…) | 63 |
| Wrong fixed | 0 (`wrong.csv` header-only) |
| Name/md collisions flagged | 3 names (30 spawn points skipped) |

## Dropped

| Reason | Count |
|---|---:|
| temp city (Control Tower / Guardian Conductor / Massive Cannon Turret) | 13 |
| instance multi-pos collapsed | 468 |
| same-name cluster within 3 m | 16 |
| near existing templated spawn (≤12 m) | 2 |
| name exists with different MonsterData | 30 |
| pets / players / no Y / no MonsterData | 0 |

## Collisions flagged (not guessed)

1. **Nighthowler** — scrape MonsterData 30352 vs existing E03F 209022 (same issue as pf 585 pilot).
2. **Ryan Daken** — scrape md 26088 vs AQ16 26097.
3. **Adan Denk** — scrape md 26139 vs Q60A 26097.

## Surfaces / client

`Playfields/685/Surfaces.dat` not modified. Client `Spawns.json` not modified. Outfit/glove/texture-slot data from pre-v6.0 scrapes omitted.

## How to apply staging

```cmd
node ddogs\tools\plan-galway685.js
powershell -NoProfile -File ddogs\tools\Build-ScrapeImportGalway685.ps1
```

## Deploy later (do not run until authorized)

```cmd
powershell -NoProfile -File tools-temp\deploy-scrape-galway685.ps1
```

## Files

- `ddogs/tools/plan-galway685.js`
- `ddogs/tools/scrape_import_galway685_20261106.json`
- `ddogs/tools/Build-ScrapeImportGalway685.ps1`
- `AORebirth/GameData/PlayfieldContent/685/SpawnsAdd.galway685.json`
- `AORebirth/GameData/NpcTemplates.Galway685.json`
- `tools-temp/deploy-scrape-galway685.ps1`
