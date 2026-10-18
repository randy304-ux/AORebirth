# Alien ships: notes for later (saved 2026-10-18)

Sources: AO-Universe "LE Missions - Alien Motherships" (guides/lost-eden/encounter-guides/high-level-encounter/le-missions---alien-motherships, last updated 2026-09-27) and "Place the Bomb" (guides/alien-invasion/daily-missions/place-the-bomb, 2012). Read in full on 2026-10-18; the guides show no hidden "Reveal" text beyond what is summarised here.

## Built now: Karli's crashed alien ship (Arete, 8009)

- 8009 is only the entrance room: Karli Cappelleri, the native YZWI (Specialist - Cha'Heru L13) and one Zix by the back (north) door, the "Ship Hatch" exit to 6553 by the south door, and the landing at 38, 0.5, 28.
- Karli's "We are ready to help you out." gives `{givedungeon ZXK1}` (GameData/AuthoredDungeons.json). The quest is generated, with a mission key, behind the 8009 MissionEntrance "Crashed Alien Ship" (0xC0002049). The dungeon is one floor of style 382 "Alien ACG", 6-9 rooms, and nothing respawns.
- Crew: ACFJ, CRDY, KSAU, KGRU, KCIU and KGXH at QL 12. The objective Zix (ZIXK) is in the deepest room that has 4+ spawn points, and the Alien General (ALGN, L14) stands with it. The quest completes on the Zix kill: 1500 credits / 1500 XP, then everyone inside is sent out. Unfinished, it expires after 24 h.
- Modelled on Place the Bomb: a scout-ship interior with a Kyr'Ozch Brigadier General plus an add on the flight deck. The bomb, the no-aggro arming and the 1-minute evacuation timer are not built.
- Guessed: the style 382 entrance exit door (both doorways are unplugged), the exit door template (the 321 office exit door; no alien door template was found), the population rate and the sizes.

## Later project: LE alien motherships

- **Giver:** the Unicorn Recruiter at the Unicorn Outpost, Andromeda 3124.6 x 905.7 (ICC transport room, roof, west doors past the Unicorns; under the tent at the bottom of the stairs).
- **Requirements:** you must be in a team with at least one other player in teaming range. No level limit and no mission-count lock. An alternative AXP source.
- **Difficulty** (three options):
  - Easy ("Take it Easy on Us"): aliens below or about your level.
  - Medium ("We Can Handle Our Business"): at or slightly above your level.
  - Hard ("I Want the Mission Unicorns are too Chicken to Take"): noticeably higher, with a boss challengingly higher.
  - Boss clump QL follows the puller's level: lower on Easy, about equal on Medium, higher on Hard.
- **Travel:** after pulling, go through the open gate to the west; the shuttle to the mothership is slightly north-west.
- **Two boss-room types.**
  - "Master of X" room:
    - Biological Metamorphoses: 2 healing towers that do not respawn; kill them first or they keep healing the boss.
    - Time & Space: spawns automated defenses that laser-mark a spot on the ground; a nuke lands there a few seconds later.
    - PsyMod: Alien Mind Warp, the whole team is unable to act and /disco.
    - Silence: surrounded by Nanovoid adds that drain nano and respawn fast (some spawn near the button), plus a stun.
    - Nanovoid: like Silence without the globes; the boss drains nano itself.
  - Cocoon room: a cocoon attendant; the cocoons spawn adds when attacked and stay available after the attendant dies (extra AXP). One cocoon holds the Cocoon Accelerator, the real boss with the good loot.
- **New aliens** (same classes as the old ones, e.g. Cha, Rimah):
  - Esox and Cornuta (no notes in the guide).
  - Dasyatis: the nuker.
  - Cyathus: hard hitters.
  - Oneirides: nano casters only.
- **Loot:**
  - Victory Point items in many QLs (higher QL gives more VP, with some randomness): Cell Templates, Mitochondria Samples, Plasmid Cultures, Protein Mapping Data.
  - The bosses also drop a typed Kyr'Ozch Bio-Material (Types 18, 295, 328, 34, 468, 64, 687, 812, 935) used to upgrade LE gear.
  - Completing the mission gives an Omni-Tek or Clan Mission Token.
- **Data on hand:** style 386 "Alien Mothership (dng)" exists in GameData/Playfields/386 (MainHalls 0-7). Its entrance is not mapped in DungeonEntrances.json yet. The `{givedungeon}` / AuthoredDungeons.json path built for Karli is the natural base: add difficulty, team-only and boss-room variants.