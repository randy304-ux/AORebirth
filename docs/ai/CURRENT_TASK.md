# Current Task

## Active: RK scrape fix batch STAGED (not deployed)

Branch `cursor/rk-desert-dyna-loot-15f4`. Tag `scrape-rk-20261106`.

### Included fixes
- Empty RK loot → DYB1–8 for named dynas; CMTR/CMHK/OFAB for trash
- Pinks/Guessed never count as “already present” for 12 m skip; pink fills overwrite Guessed
- Suppress pink Spawns.json points within 12 m of SpawnsAdd.rk or scrape sightings
- Textures/Meshes from scrape wired into RK templates (Elliot outfit, etc.)
- M.A.G.S. (WPPN) pink/Rollerrat donor → scrape MD 22821 / mesh 21715 / scale 303 / HP 145735
- Elliot Fairlane Knubot + textures; buff nano ID **unknown** (not invented)
- Desert dual guide-dyna suppress (via pink/Guessed suppress)

### Validation (`ddogs/tools/Validate-RkScrapeImport.js`)
- Template vs scrape MD/mesh/scale/name/tex mismatches: **0**
- Pinks with scraped real mob nearby: **0**
- Dialog gaps on RK templates: **4** (Robin Raag, Arbiter Vincenzo Palmiero, Adeline Guerra, Omni Clothing Salesman)
- Guards compared bad: **0**

### Do not deploy
Mike accepts / live-verifies before deploy.
