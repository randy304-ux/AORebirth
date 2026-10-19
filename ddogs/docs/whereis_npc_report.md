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
