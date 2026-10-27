# The Reck — full build plan (2026-10-27)

**Verdict: SPARSE. Do not implement Reck dailies yet.**  
This document is the build plan required before any Reck daily or boss wiring.

Sources (one read each): AO-Universe The Reck location guide + The Reck daily quests guide.

## Inventory (current checkout)

| Layer | State |
|---|---|
| Playfield id | **750** (`playfields.Name` = The Reck). Not 795 (The Longest Road). |
| Geometry | Present under `GameData/Playfields/750/` (Collision, Surfaces, Dynels, Districts, Spawns, …) |
| Districts | 22 (Sentinum, Unicorn, The Dam, Island of the Dead, …) — level bands mostly 80–200 |
| Client `Spawns.json` | **454** points, **69** unique hashes |
| DB `mobspawns` for PF 750 | **0** rows |
| `NpcTemplates.json` coverage | **14 / 69** hashes present; **55 missing** |
| `PlayfieldContent/750` | **Missing** (no Sovic / Tettle / Observer / teleporters as content NPCs) |
| Whompas Tir / Omni-1 Trade → Reck | Not verified in this pass; check teleports before soft-open |

### Hashes already in NpcTemplates (14)
`AAHO, APDE, COWL, CYEX, ETGE, FCCT, HGCJ, PREU, RPRY, RR07, RR08, SGMO, WASK, WPPN`

### Missing hashes (55) — must be sourced before HashSpawnSystem can populate the field
`ACHN, BEMJ, BESK, BGGO, BMME, BMMS, BPKR, BSMS, CCOI, CCSN, CDEE, CGCR, CGCT, CLSC, CN19, DACT, DRKN, EISE, ELSR, ENFR, ENLI, EOSH, EVAS, EVBE, EVCN, EVMA, EVPR, EVSA, IOVA, KMZU, LEEV, LSEO, ME06, MEEL, OBCE, OOSF, PDQM, PILO, PNSY, RR10, SAMG, SAMV, SAWM, SDAT, SDLG, SHCV, SILZ, SISW, SLDT, SNGU, SNRD, SRVI, SWCN, WAND, WASV`

Likely daily-related among missing (confirm with client MonsterData / evidence): Sentinel/Omni guards, SANDSTORM towers/soldiers, Silent Death, Entvined, Observer island packs, factory commanders.

---

## Daily quests to support (after population)

### Omni / Neutral — Sergeant Major Sovic (Sentium ~1843×3342)
| Daily | Objective (guide) | Notes |
|---|---|---|
| Reclaiming the Supplies | Kill 3 Silent Death (~1625×2970), L100–110, stealth | Omni-only flavour |
| The Mutant Warchief | Kill Warchief Skawt (~1835×965), L170; area L140–160 | Beacon on corpse for late credit |
| Putting Down Wardog | Kill Wardog (~1785×1725), L165 | Beacon |
| Drawing Notum from the Sand | Kill SANDSTORM Control Tower (~1117×1126), L150 | Beacon |
| Defending the Factory (PvP) | Kill 3 Sentinel Guards L150 + High Commander Jameson (~1310×1475) L200 | PvP flag; **no** beacon |

### Clan / Neutral — Field Commander Tettle (Unicorn ~640×282)
| Daily | Objective | Notes |
|---|---|---|
| Clawing Through the Crates | Use eastern crate at Abandoned Mining Outpost (~1155×202); turn-in by targeting Tettle | Interact, not kill |
| The Mutant Warchief | Same Skawt | Shared |
| Putting Down Wardog | Same Wardog | Shared |
| Drawing Notum from the Sand | Same tower | Shared |
| Assaulting The Factory (PvP) | Kill 3 Omni Special Forces L150 + Commander Williams (~1439×1905) L200 | PvP flag; no beacon |

### Shared — An Observer (~1800×485, Watchtower)
| Daily | Objective |
|---|---|
| Paradise Lost | Kill 3 Painlords + 3 Arachnobots + 3 Seismic Lords on Island of the Dead (L170–210) |

Neutrals: up to 8 Reck dailies (both officers + Observer). Omni/Clan: 5 from faction officer + Observer = 6.

---

## Boss / POI checklist (location guide — not dailies, but same population pass)

| Encounter | Approx | Notes |
|---|---|---|
| M.A.G.S. | Sentium 1803×3303 L200 | Aggression Enhancer |
| Lord of Sand ×2 | 2267×2902 / 1533×237 L160 | Gem of the Sands |
| Prime Evolution Huzzum | 1152×2344 L120 | Iron Fist / Wizdom |
| Wardog | 1785×1725 L160 | Dog of War / gloves |
| Cyborg Executioner | 1980×1411 L130 | C.O. Wrist Computer |
| SANDSTORM Control Tower | 1117×1126 L150 | Damaged Notum Tower Shield |
| Warchief Skawt | 1861×968 L170 | Plasma emitters |
| Entvined General | 1520×794 L210 | Merit Board / Optic Enhancer |
| High Commander Riker | Unicorn 676×314 | Riker's Resolve |
| Commander Williams / Jameson | Factory north/south | PvP dailies |
| Abandoned Hope | Island 2009×182 L225 | Visors / Death's Door; ~1h respawn |
| Dynas ×4 | NW Plains, E Archipelago, Dam, Port New Thera | Do **not** count for Hunt daily |

Loot item AOIDs: resolve from `itemnames` when wiring LootTables (not guessed here).

---

## Build phases (order)

### Phase 0 — Gate
1. Confirm whompa / teleport into PF 750 from Tir and Omni-1 Trade.
2. Soft-open only after Phase 1 leaves resolve ≥90% of spawn hashes (or HashSpawn skip rate is accepted by Randy).

### Phase 1 — Mob templates (data-driven)
1. For each missing hash: MonsterData + NPCFamily + level band from client districts / evidence dumps (same pipeline as `Build-NpcTemplates.ps1` subway / newbie sections).
2. Prefer capture or RDB MonsterData names over invented looks.
3. Attach CommonLoot + Reck-specific LootTables once item ids are confirmed.
4. Rebuild `NpcTemplates.json`; do not hard-code Reck stats into C#.

### Phase 2 — Named bosses + officers
1. Hand-made leaves (or evidence-backed) for Skawt, Wardog, Jameson, Williams, M.A.G.S., Abandoned Hope, Observer island packs, Silent Death, SANDSTORM tower.
2. `PlayfieldContent/750/Npcs.json`: Sergeant Major Sovic, Field Commander Tettle, An Observer, outpost teleporters if not already dynels.
3. Knubot scripts offering `{givedaily RKxx}` per daily.

### Phase 3 — Dailies on the framework
1. Add Quests.json rows + `DailyMissions.json` entries (kill counts, pads, optional Special).
2. Beacon-on-corpse credit: new small reusable hook (or temporary “kill only if you tag” until beacon exists).
3. Crate interact for Clawing Through the Crates (static dynel / use target).
4. PvP factory dailies: flag rules must match server PvP policy before enable.
5. 18h lock-out track `RECK` (separate from Agency / DOJA / John Smith).

### Phase 4 — Polish
1. Dynacamp verification (4 camps).
2. Boss loot tables vs location guide.
3. Flight / road safety is player problem; no level-scaling redesign.

---

## Framework mapping (when ready)

| Daily | Suggested quest hash | Action |
|---|---|---|
| Reclaiming the Supplies | `RKRS` | KillMultiple ×3 Silent Death |
| Clawing Through the Crates | `RKCC` | UseItem / TargetNpc crate then Tettle |
| Mutant Warchief | `RKMW` | KillOne Skawt |
| Putting Down Wardog | `RKWD` | KillOne Wardog |
| Drawing Notum | `RKDN` | KillOne SANDSTORM tower |
| Defending the Factory | `RKDF` | KillMultiple guards + KillOne Jameson |
| Assaulting the Factory | `RKAF` | KillMultiple OSF + KillOne Williams |
| Paradise Lost | `RKPL` | KillMultiple ×3 of three families (or three quest steps) |

---

## Stop conditions
- Do **not** spawn placeholder pink AAAA packs across 55 missing hashes just to “open” Reck.
- Do **not** implement Reck `{givedaily}` until Phase 1–2 named objectives exist.
- If Linux / public master diverge, Reck content still originates on Windows/public master first.
