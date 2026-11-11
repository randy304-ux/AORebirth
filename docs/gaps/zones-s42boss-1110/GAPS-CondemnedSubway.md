# Condemned Subway raid (PF 9070) — gaps

Playfield geometry was extracted from I:\AO\client-ddogs with RDBDataExtractor (--tilemap-id 9070, tilemap 9071). Output is 20 files under AORebirth/GameData/Playfields/9070 (gitignored, like other Playfields). failed=0.

## Entrance

- Andromeda (PF 655) already has a client door named "Condemned Subway" at 3305.73, 35.64, 836.89 (Identity Door:-1072037233). Zone-in arrives at (64.59, 158.01, 15.64) heading 358. No new 655 door was added.
- No regular-subway (PF 127 / 152 / 351) entrance was in the scrape, DungeonEntrances.json, or a text match in those playfields' Destinations.dat. None was invented.

## Zone-in flea

- Client NMNV at (68.2, 159, 25.6) is 10.6 m from arrival. The scrape Filth Flea had patrolled to (62.854, 158.010, 16.712), 2 m from arrival. That point is SpawnSkips-removed and re-added at (78, 159, 35), down the +X/+Z corridor, short of the next NMNV at (86.1, 159, 43.6).
- Garbage Critters also sat 0.9–2.9 m from arrival in the scrape (same mesh family). Randy asked only about the flea; those client points were not moved.

## Unmapped / not invented

- Client hashes PFFC and PPTN have no confirmed scrape name (PFFC levels 2–10, PPTN L2). Left unmatched.
- Scrape had standing/add mobs with no unique client hash: Oblivion, Ire of Gilgamesh, Vergil Doppelganger, Directors (Internal Affairs / Marketing / Operations / Public Relations), Curse-Rotted Grove Sage around Eumenides, Harbinger of Pestilence and Scalding Flames around Abmouth, Searing Flames around The Psion, Hacked Mechdog around Shiro (likely pets/adds), Spider Egg, Personal Transporter, and Punishment effects. Not spawned or looted as new hashes.
- 23 captured boss loot rows applied (Vergil's duplicate corpse collapsed to one table). Trash has no captured loot.
