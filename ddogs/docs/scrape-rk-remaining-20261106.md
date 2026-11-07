# Rubi-Ka remaining scrape import (Phase 2) — 2026-11-06

**Tag:** `scrape-rk-20261106`  
**Stacks on:** Adonis Phase 1 `1369dfef` + Gil Notes / glyph stack  
**Status:** staged only; `tools-temp\deploy-ado.ps1` covers ado+rk, not run

## Sources

- `I:\AO\imports\scrape-20261106-review\` missing.csv (outdoor OK from older scrapers)
- `I:\AO\imports\scrape-20261106-ado\raw\` today RK rows (Reck, Smuggler’s, Shire, cities)
- Guidance: `gpt-rk-gap\REPORT.md`
- Loot: confirmed Reck facts only (v61 has no RK rows; pre-v61 loot held back)

## Skipped as already live

| Zone | PF | Why |
|---|---:|---|
| Galway County | 685 | commit 8f2ad182 SpawnsAdd.galway685 |
| Aegean | 585 | pf 585 pilot SpawnsAdd |
| Andromeda (ICC) | 655 | ICC655 SpawnsAdd / reconciliation hold |
| Adonis / SL Phase 1 | 4872/4873/4213/4625/4626 | Adonis import |
| Near existing spawn ≤12 m | many | 1,862 sightings (pink fill or skip) |

## Held back

| Item | Count / note |
|---|---|
| Galway Shire zone-line guard/city pull cluster | 58 (Male Captain, Patrol mechdog, Waste collector, turrets, …) |
| Clondyke west of X≈963 | 258 (review-only border) |
| Pets | 78 |
| Temp city (Control Tower / SAM / …) | 530 |
| Pre-v6.1.1 Reck loot file rows | 95 held (not used as drop tables) |
| Name/MonsterData collisions | 531 skipped (not guessed) |
| Gloves | blank; outdoor scrapes OK without; city outfits need gloves (none supplied) |
| Signed ACG instance SpawnsAdd | removed if emitted |

## Reck confirmed loot (itemnames.sql IDs)

| Mob | Low/High IDs |
|---|---|
| Abandoned Hope | 303071 Death's Door |
| Warchief Skawt | 303058, 303059 |
| EVO Assassin / Berserker / Baron Skullfaccia (Skawt trash) | 303059 |
| Prime Evolution Huzzum | 303056, 303055 |
| Prototype 73-Omega Ninjadroid | 142695 Augmented Biomech Body Armor; 147687 Instruction Disc (Cascade of the Storm); 162347 Instruction Disc (Frenzy of Fur) |

## Staged totals

- SpawnsAdd points: **29,899** across ~50 RK zones (`SpawnsAdd.rk.json`)
- New/fill templates: **1,603** (`NpcTemplates.RK.json`)
- Pink fills: **1,033**
- Loot item families: **8** (`ItemTemplates.RK.json`)

## Apply / deploy

```cmd
node ddogs\tools\plan-rk-remaining.js
powershell -NoProfile -File ddogs\tools\Build-ScrapeImportRK.ps1
powershell -NoProfile -File tools-temp\deploy-ado.ps1
```

Deploy creates DB dump + `I:\AO\backups\gamedata-rollback-ado-rk-<stamp>\` before staging.
