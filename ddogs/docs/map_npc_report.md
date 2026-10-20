# Client map files as an NPC-location source (2026-10-19)

All installs were read-only; files were copied out to the agent box for analysis.

## What the map folders contain
`cd_image/textures/PlanetMap` in `I:\AO\client-src`, `I:\AO\client-ddogs` and `I:\AO\launcher\client-files` holds:
- `normal` (default Rubi-Ka map), `Dovves`, `SaavicksMap` 2.0, `SaavicksMap33` 3.3 (in client-ddogs and launcher), `Shadowlands` (default), and `SLmap` v2.22 by bitnykk.
- The `.txt` files are only tile indexes (Size/Tiles/MapRect/FilePos). The `.xml` files map each playfield to map units. **There are no text label files.** Every label is painted into the PNG tiles inside the `.bin` files.

So I rebuilt the tiles into full images and ran OCR (RapidOCR at 2x scale) on them.

| Map | What the labels are | Use for NPCs |
|---|---|---|
| Saavick 3.3, layer 3 (Rubi-Ka) | Landmarks only: CAMP, OUTPOST, VILLAGE, RUINS, MINE, GUARD, SHOPS, whom-pah codes, dyna level dots (purple numbers), mission markers. 1,652 OCR fragments. | **None.** No NPC or vendor names. Dyna dots and "GUARD" labels could later be matched to our dyna camps/guards. |
| Default / Dovves / Saavick 2.0 | Same kind of landmark map | None |
| SLmap 2.22 Wide, layers 2+3 (Shadowlands) | Layer 2: dynabosses with level and loot. Layer 3: nanos, quest NPCs and other NPCs. Red dot next to each name, or legend codes (E1, W3 ...) per zone. | **Yes**, for the Shadowlands only |

## SLmap results (`map_npc_points.csv`)
- OCR on the pixel font is rough, so each OCR line was fuzzy-matched (≥0.8) against known names: our templates, whereis, and Tyrbot pocketboss. That gave 170 name hits, **131 unique name+zone points**.
- Pixel to world: fitted on 118 name pairs: X = map unit ≈ 1.005 px, north is flipped, per SLmap.xml offsets. Then a per-zone offset was taken from our own matched spawns (4005, 4311, 4312, 4605, 4873). Typical error after correction is about 10–30 m. Zones without a correction are less exact. Inferno needed +68 m north.
- Zone choice: SL zones overlap on the map, so each point goes to the zone whose spawns are nearest. Points that also fit another zone are marked in the note and get low confidence.

| Status | Count |
|---|---|
| ok (our spawn of that name within 40 m) | 43 |
| misplaced? (our spawn of that name is 40 m+ away) | 27 |
| missing (no spawn of ours with that name in that zone) | 50, of which 15 have a pink placeholder within 40 m |
| no zone / legend marker not found | 11 |

Missing by zone: Burning Marshes 7, Inferno 4, Penumbra Valley 3, Upper/Lower Scheol 3+3, Adonis City 3, Adonis Abyss 2, Elysium temple/catacombs 3+3, Neretva Canyons 3, the gardens and sanctuaries 1–2 each, and a few others.

Misplaced, worth a look:
- Adonis Vile Spirit (R012) and Sinful Soul Dredge are about 1.8 km off. This agrees with the whereis conflict for the Adonis golems/spirits, so our Adonis named spawns look shifted.
- Malahanimos N010 (520 m), Medusa Philanderer / Obsolete Soul Dredge (about 230 m), Wicked Soul Dredge (237 m), Lesvos (190 m).
- Several 50–120 m results (Inferno, Nascense) are within the calibration error and are probably fine.
- Lord of the Void appears in several zones on purpose.

Confidence: **medium** only where the OCR match is ≥0.9, there is no legend code and the zone is unambiguous (a handful). Everything else is **low** and should be checked on the image before use. Nothing was applied.

## Other installs
- **RC-PC** (F:\DungeonDogs\AO-client) was offline, so it was not checked.
- **PRK installs:** none found on the R730. The only AnarchyOnline.exe copies are client-src, client-ddogs, cache\temp\ao-client-installed, funcom-client and official-copy-test. They hold standard Funcom data and the same map folders. C:\Users\AMP\AppData\Local\Funcom has only prefs/logs (6 files), with no cached map overlays or waypoint files.
- No non-Funcom data files with spawn info were found.

## Worth doing next
- Read the SL legend blocks by eye: E/W/N codes plus the dot positions give about 100 more dynaboss points.
- OCR SLmap layer 3 at a higher scale, or match by template, to read the NPC names that fuzzy matching missed.
- Check RC-PC's client and the PRK installs when RC-PC is online.
