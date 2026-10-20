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

## Update 2026-10-19 9:30 PM ET: all map levels + applied Shadowlands spawns (option A)

### Map files and levels checked (client-ddogs; client-src and the launcher copy hold the same or a subset)
| Map | Level / file | Size | Result |
|---|---|---|---|
| SLmap v2.22 Wide | L1 SLmapWide1.bin | 2944x11904 | OCR'd: zone and area names only |
| SLmap v2.22 Wide | L2 SLmapWide2.bin (dynabosses) | 5888x23808 | OCR'd 2x: used |
| SLmap v2.22 Wide | L3 SLmapWide3.bin (NPCs, quests, nanos) | 5888x23808 | OCR'd 2x: used |
| SLmap v2.22 Normal | L1-L3 SLmap1/2/3.bin | half the Wide resolution | same artwork as Wide, so not re-OCR'd (an early 1x pass on L2 read worse) |
| Default Shadowlands | SLLevel1.bin, SLLevel2.bin | 441x1776, 1472x5920 | OCR'd L2: zone names only |
| Saavick 3.3 (2x) | L1 1-2x, L2 2-2x | 1024², 2560x2304 | OCR'd L2: zone names only |
| Saavick 3.3 (2x) | L3 3-2x | 9728x9216 | OCR'd: landmarks, camps, dyna levels |
| Saavick 3.3 (2x) | **L4 4-2x (Rubi-Ka NPC layer)** | 9728x9216 | OCR'd 2x: **105 Rubi-Ka NPC points** added to the CSV (55 missing, 28 misplaced?, 22 ok), mostly low confidence. Candidates only, not applied. |
| Saavick 3.3 (1x) | 1-1x .. 4-1x | half size | same artwork as 2x |
| Saavick 2.0, default `normal`, Dovves | single sets | small | landmark maps, nothing new |

- **Catacombs, temples, sanctuaries and gardens** (Elysium, Scheol, Adonis, Penumbra and Inferno catacombs/temples, all sanctuaries and gardens, Pandemonium) are **not separate map files**. SLmap.xml places them as insets on the same SLmap images, so they are covered by L2/L3 above.
- Nothing labels points inside the catacomb dungeons: no NPC names were read inside those insets.
- There are no other per-dungeon map files in cd_image/textures/PlanetMap.

### Applied (Build-SLMapSpawns.ps1 + slmap_spawns.csv, chained after Build-WhereisNamed.ps1)
Every label was checked by eye on the map crop first. The 50 "missing" SL points turned out to be mostly loot lines (PB_...), pattern names, area labels or wrong zone picks. Only 13 were real NPCs, giving 15 spawns.

| Zone (pf) | Applied |
|---|---|
| Burning Marshes / Inferno (4005) | 8 new: Smoky Salamander, Shy/Bold/Calamity Eremite, Coal Lizard, Weakened Chimera, Chimera Crusher, One Who Talks With The Past (friendly) |
| Inferno (4605) | 1 new: Arch Bigot Biap |
| Nascense Frontier (4310) | 1 new: The Mountain Claw (35) |
| Upper Scheol (4880) | placeholder TEFA → Technologist Frank Jobin (friendly); 1 new: One Who Is Full Of Compassion (friendly) |
| Lower Scheol (4881) | 1 new: Leading Blossom (friendly) |
| Adonis Abyss (4873) | placeholder MXJJ → Cold Slither (195) |
| Penumbra Valley (4321) | placeholder VADL → Wadleany (174); the OCR "Brutal Rafter" is her loot |

- Levels come from the map label or the placeholder where given; otherwise the area level (guessed).
- Bodies are cloned from same-kind SL templates: FTER eremite, CIAE chimera, NHTH bigot, CYST slither, OWWW "One Who", SCDR Jobe scientist. Smoky Salamander (BLMM), Coal Lizard (SRLZ), Mountain Claw (ATHL) and Wadleany (P000) are guessed bodies.
- Every template carries ContentProvenance Guessed=true with donor and source.

**Adonis**: the whereis "conflict" is two real spawn spots per boss. Whereis itself says "1524 x 464 S and 1941 x 2241 N" and "2402 x 638 SE and 849 x 1177 SW". The official S/SE points are already in the client spawns, and Build-SLDynas' camps sit on the N/SW spots. So there was no group shift. The only real error was Cranky Golem's camp (R019), which was at 1149 x 870 instead of the whereis SW spot 849 x 1177. It is moved (5 points; height from the nearest official spawn), and Build-SLDynas.ps1 now uses 849 x 1177.

Skipped and why: see the `applied` column of map_npc_points.csv. In short:
- PB_ loot lines: Fester Leila, Beatific Spirit, Ignis Fatuus, Ungulera, Careening Blight, Lethargic Spirit.
- Pattern names: The Adonis Spirit Master, The Watchdog.
- Area or OCR junk.
- Names already spawned elsewhere (zone pick wrong).
- Garden Or-Mada/Craig-Or vendors: the O?O? placeholders are vendor hashes, but which vendor is which is unknown.
- Low-confidence Rubi-Ka points (Saavick L4) are listed but not applied.

Heights for new points are copied from the nearest existing spawn. Check in game for floating or buried mobs.
