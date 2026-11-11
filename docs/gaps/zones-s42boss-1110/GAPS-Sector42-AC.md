# Sector 42 Artillery Commander — gaps

Guide: https://www.ao-universe.com/guides/alien-invasion/quests-guides/sector-42-alien-playfield and https://wiki.aodb.us/wiki/Sector_42

- AC and Seeker appearance, exact HP, and exact Seeker level were not captured and are not on the guide pages. S42AC uses a labeled Cha'Khaz (S42G) stand-in at L300 with HP 6,533,334 (same as the largest first-cycle mini-boss). S42SK uses a labeled Alien Heavy Patroller (S42A) stand-in at L300 with HP 400,000.
- The ~60 second rebuff window after the second tower kill is not in the current DelaySeconds model (DelaySeconds is from group start, not from wait completion). Stage 2 therefore spawns the three ACs as soon as the second-cycle towers are dead. Noted, not faked with a group-start delay.
- Batter Up (20%) / Hostile Detected / Target Acquired / Hostile Lock (10 m AoE) are named on the wiki but have no nano IDs in itemnames or templates. Not implemented.
- Seeker root immunity / snare-only control is not scripted.
- Assorted "4× Lead Viralbots / Typed BioMaterial / Fused Biomaterial": family S42AL is the six Lead Viralbot IDs plus Fused Biotech Material (251217). Exact typed Kyr'Ozch Bio-Material type list was not specified and is not in the pool.
- Laser fences stay moderate contact damage, no instakill. Unchanged in this pack.
