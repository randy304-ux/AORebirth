# Whereis NPC location cross-check (2026-10-19)

Sources (downloaded to the agent box only; nothing deployed to the server):
- Nadybot `whereis.csv`, GPL-3.0
- Budabot `whereis.sql`, no license file found via the GitHub API (treat as unknown/GPL-ish, do not redistribute verbatim)
- BeBot `Modules/Ao/Whereis.php`, GPL-2.0
- Tyrbot: repo not found on GitHub

The three lists are largely the same dataset. Combined, de-duplicated by name+playfield: **572 entries with coordinates** (`whereis_combined.csv`). Their coords are (x, north); ours are Position [X, height, north], so we matched X to X and north to Position[2].

## Results
| Check | Result |
|---|---|
| Placeholder spawns (placeholders.csv, 11,196 rows over 459 playfields) | 999 have a whereis entry within 80 m on the same playfield: 347 high (≤15 m), 274 medium (15–40 m), 378 low (40–80 m). 10,197 unmatched. |
| Whereis named NPCs that no spawn of ours uses by name | 539 (`whereis_missing.csv`). This includes landmarks, dungeons and cities, not only NPCs. |
| Our named NPCs far (>50 m) from whereis, or on another playfield | 18 (`whereis_conflicts.csv`) |

Notes
- The 347 high matches are mostly at 0–1 m. Our placeholder sits at the same point as the whereis entry, so the whereis name tells us who that placeholder NPC is (Inferno/Adonis/Penumbra spirits and bosses, Milky Way prisoners, and others).
- Only 69 of the 459 placeholder playfields have any whereis data at all. Whereis covers mainland bosses, landmarks and SL. It has nothing for Arete (6553), dungeons, or instanced/mission playfields, and Harvey is not in it.
- Conflicts to look at: the Adonis golems (R019–R01C) and Corrupt Spirit (R011) are 1.2–1.8 km away from whereis. Chimera Monitor/Trainer and Pursued Spirit are on 4605 for us but 4005 in whereis. Neleb/Unrelenting Fear/Pulsing hatred are on 1933 for us; whereis gives the 716 entrance. That one is probably fine (dungeon vs. entrance).
- Medium/low matches are proximity only. Check them before using.

## Coverage / need more data?
Yes. Whereis resolves roughly 9% of placeholders, and only about 3% with confidence. Other public sources:
- **CellAO** (github.com/CellAO/CellAO), GPL-2.0. The emulator has an extracted playfield/statel/NPC data pipeline.
- **AORebirth upstream**: check its license file. Our own base, so it may hold more named spawn tables.
- **Nadybot modules** (GPL-3.0): `boss`/`bosslist` loot module data (boss names and playfields, partly with coords).
- **AO-Universe / AOMainframe guides**: text guides, not open-licensed. Use as reference only.
- Best remaining source: the client's own data (dyneldump / RDB playfield statels), as used for the strongbox.

Files: whereis_combined.csv, whereis_placeholder_matches.csv, whereis_missing.csv, whereis_conflicts.csv (this folder).

## Update 2026-10-19 6:40 PM ET: applied high-confidence names + more sources
Randy: license doesn't matter, use the community data (Budabot rows kept).

Applied by `ddogs/tools/Build-WhereisNamed.ps1` (rerunnable, chained at the end of Build-NpcTemplates.ps1). Input: `ddogs/tools/whereis_named_spawns.csv`.
- 347 high rows: 14 codes already had real templates and were left alone. In 58 cases several placeholders sat within 15 m of the same whereis entry; only the closest got the name. The other 87 are in `whereis_high_not_applied.csv`.
- 246 codes → **238 templates written**. 8 were skipped as landmarks, not NPCs: Another Face In The Sand, Stoltz Outpost, The Eremite Statue, Eroded Ancient Statue, Rome Red Grid, The Satellite Dish, Crete, the Mordeth Statue garden access.
- Levels come from the client spawn level of the placeholder. Look and stats come from: DB mobtemplate with the same name (3), an existing template with the same name (19), a same-playfield creature of the same kind (2), a same-playfield template nearest in level (119), hostile human HW01 for person names (86 + 8 with no donor), or civilian NoCombat ADAF for friendly titles (4).
- Every template carries ContentProvenance (Guessed flag, donor, source, distance). The per-code list with GUESS/real is in `ddogs/tools/Build-WhereisNamed.last.txt`.
- No Spawns.json changes: a template overrides the SpawnSkips hidden list, so these spawns show up after the next restart.

### Other public sources checked
| Source | What it has | Placeholders it could resolve |
|---|---|---|
| Budabot/Tyrbot (GitHub, active) `whereis.sql` | same data as Budabot whereis | 0 new |
| Tyrbot `pocketboss.sql` | 128 pocket bosses: name, playfield, level, text location (no coordinates) | 127 not in our data; 63 have a same-playfield placeholder with the exact level (mostly SL: 4605, 4873, 4881, 4880, 4320, 4542). Needs a level+area match, medium confidence. |
| Tyrbot/Nadybot `boss.sql` / bossloot | boss names + loot, no location | 0 by itself (loot tables are useful) |
| subarumike/AORebirth upstream | PlayfieldContent for 6553 (4 NPCs), 127, 4676, 4677, 7010; mobspawns/mobtemplate SQL | 0 new: we already have all of them |
| CellAO/CellAO-NightPredator | mobtemplate.sql, staticdynels.sql, playfields | 0 new: already in our local cellao_codex_clean DB (only 3 name hits) |
| AOCapture, AOReloaded, others | tools, no spawn data | 0 |

Arete (6553: 78 placeholders), ICC Shuttleport (1) and the dungeons (Steps of Madness 1933: 29, Temple 1931: 0) are **not covered by any public source**. Whereis has no Arete or dungeon entries, and upstream's 6553 content is only Stan, Sarah, Marco and Lorelei. For those, the remaining sources are the client data (dyneldump / RDB statels and district tables), the AO-Universe/aodb guides (by hand), and live screenshots from Randy.
