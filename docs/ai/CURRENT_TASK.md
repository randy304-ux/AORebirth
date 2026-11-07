# Current Task

## Active: RK scrape fix batch STAGED (not deployed) — three gaps closed

Branch `cursor/rk-desert-dyna-loot-15f4`. Tag `scrape-rk-20261106`.

### Three gaps (this tip)
1. **Nano crystals**: NNC1–8 from MissionRewards `ItemDb_Nanos.json` in `ItemTemplates.RK.json`; Chance **10** / LevelMod 20 on all CMTR/CMHK/OFAB default combat tables (3072 bands).
2. **Placeholder `* Dyna`**: 1 renamed (D00F → Cleantalon the Voracious Mornitor); 0 duplicates removed; **32** left (no matching named-dyna scrape at MD+≤30 m) — listed in `tools-temp/rk-close-three-gaps.report.json`.
3. **Historical pinks**: **170** hashes / ~**363** spawns replaced from older scrapes; **30** left (26 city-humanoid outfit + ~4 guide-placeholder dynas near historical scrapes). Today pinks still **0**. Galway Shire guard-pull: 0 hits.

### Deploy
`tools-temp/deploy-ado.ps1` updated. **Do not run** until Mike GO.
