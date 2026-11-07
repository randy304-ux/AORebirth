# Current Task

## Active: RK scrape fix batch STAGED (not deployed) — independent review pass

Branch `cursor/rk-desert-dyna-loot-15f4`. Tag `scrape-rk-20261106`.

### Review findings vs 9ded57ac claim
- Prior validator compared **plan JSON → templates**, not raw scrape; only **23** guards. Too clean.
- Independent validator now: raw today + older CSVs; **4643** bands; **51** guards; HP/meshes/tex; today pinks=0.

### Included fixes (this tip)
- Empty RK loot → DYB1–8 / CMTR/CMHK/OFAB (Galway trash baseline; CMHK = Health/Nano stims, not NanoCrystal programs)
- Pinks/Guessed never count as present; Spawns.json suppress (gitignored — **deploy-ado now copies them**)
- Textures/Meshes from richest scrape sighting (Omni Special Forces / SANDSTORM shoulders restored)
- M.A.G.S. WPPN MD 22821 / mesh 21715 / scale 303 / side **3** (scrape Side=OmniTek was wrong; Breed=Monster) + DYB7 loot
- Elliot Fairlane look + Knubot; buff **{givenano 293685}** from dialogs_raw SetNanoDuration (2h)
- Placeholder `* Dyna` still **31–33** guide markers without 1:1 scrape rename (Lizards Dyna etc.)

### Validation (`ddogs/tools/Validate-RkScrapeImport.js`)
- Template vs scrape MD/mesh/scale/name/tex/meshes/HP: **0**
- Pinks with today’s scrape nearby: **0** (historical scrapes advisory: 392)
- Guards compared: **51**, bad: **0**
- Dialog gaps: **4**
- Elliot givenano 293685: **true**

### Deploy
`tools-temp/deploy-ado.ps1` updated (Knubot + Spawns.json + zbuild-ado). **Do not run** until Mike GO.
zbuild-ado rebuilt (givenano support).
