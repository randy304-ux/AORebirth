# Dailies design (Randy list, 2026-10-27)

Reusable framework: `GameData/DailyMissions.json` + `DailyMissionService` + Knubot `{givedaily HASH}` + Quests.json rows.  
Halloween outdoor note: `halloween-design.md`. Reck build plan: `the-reck-design.md`.

Sources (one read each): AO-Universe Daily Missions overview, DOJA Chips, PvP Daily Mission, The Reck dailies + location.

## Priority status

| # | Track | Status | Blocker / content gap |
|---|---|---|---|
| — | Killing the Competition (Illegal Notum Mining) | **Done** (commit `3c384c75`) | NMA map deferred; field stand-in PF 791 |
| 1 | General Freelancers Inc. dailies | **Deferred** | Agency offices, handlers, lockouts, elite/alien lines |
| 2 | DOJA chips (Shadowlands) | **Deferred** | Drops + Scarlett Dalquist + return mission |
| 3 | PvP daily (John Smith full menu) | **Partial** | KTCM live; BS / Tarasque / Reclaiming deferred |
| 4 | The Reck dailies | **Deferred — build plan only** | Sparse PF 750; see `the-reck-design.md` |

Each official track has its own 18-hour lock-out. Framework does not yet enforce lock-outs (house rule TBD).

---

## Done

### Killing the Competition (`KTCM`)
- John Smith, Borealis Antiques (~690×590), PlayfieldContent + knubot
- Kill 3 `RGNM` Rogue Notum Miners; `{givedaily KTCM}` spawns field + Ganking UPH every run
- Reward: 25 VP (stat 669) + 500 XP
- Catalog entry in `DailyMissions.json`

### PvP daily menu (John Smith) — dialogue wired
Knubot lists all four AO-Universe options. Only Illegal Notum Mining (`KTCM`) assigns. Others tell the player they are not live yet.

---

## 1) General daily missions (Freelancers Inc.) — deferred

AO-Universe overview: Mission Agency offices Omni Rome Blue 660×315 / Clan Old Athen 515×440; Receptionist contract → Agency Handler (RK dailies) → Senior Agency Handler (elite) → Alien Agency Handler (AXP).

### What we need and do not have
| Piece | Our state |
|---|---|
| Rome Blue (735) / Old Athen (540) PlayfieldContent Agency NPCs | **Missing** (no PlayfieldContent folders) |
| Agency Receptionist / Handler / Senior / Alien Handler templates + knubot | **Missing** |
| Freelancers Inc. Token `303237` | Item **exists** in DB; not wired as reward |
| Daily / elite / alien mission roll tables | **Missing** |
| 18h lock-out NCU | **Missing** |
| Unicorn Outpost alien shuttle / transporter | Partial alien content elsewhere; not Agency-linked |
| Elite dungeon kill waves (Subway, T3W, Biomare, CoH, Smuggler's, IS, Escaped Prisoners, Dust Brigade, Biodomes) | Large separate content |

### Framework plan (when content exists)
1. Place faction Agency NPCs in PF 735 / 540.
2. `{givedaily AGxx}` rows in `DailyMissions.json` / Quests.json per handler tier.
3. Alien Handler AXP% table from the guide (AI 1–29).
4. Shared lock-out service keyed by daily track id.

**Catalog stubs** (Status=Deferred) live in `DailyMissions.json` under `Deferred`.

---

## 2) DOJA chips (Shadowlands) — deferred

Right-click chip → return-item mission → Scarlett Dalquist, Jobe Research Lab R1 (~934×910). Shared 18h lock-out except Team Dark Ruins (separate).

### Chip AOIDs (DB `itemnames` — real)
| Id | Name | Level band (guide) |
|---|---|---|
| 284954 | DOJA Chip Nascense | 1–60 |
| 284955 | DOJA Chip Elysium | 61–100 |
| 284956 | DOJA Chip Scheol | 101–130 |
| 284957 | DOJA Chip Adonis | 131–160 |
| 284958 | DOJA Chip Penumbra | 161–204 |
| 284959 | DOJA Chip Inferno | 205–220 |
| 284961 | Special DOJA Chip Alappaa | 201–210 |
| 284962 | Special DOJA Chip Albtraum | 211–219 |
| 284960 | Special DOJA Chip Pandemonium | 220 |
| 282144 | Special DOJA Chip | Team Dark Ruins (&lt;201) |

### What we need and do not have
| Piece | Our state |
|---|---|
| Chip item templates / nodrop unique | IDs exist; **ItemTemplates / use hooks not wired** |
| Outdoor SL trash mob drop tables (Nascence…Pandemonium lists in guide) | **Not wired** for DOJA |
| Jobe Research PF 4001 geometry | **Present** |
| Lab R1 + Scarlett Dalquist NPC | **Missing** (no PlayfieldContent 4001) |
| Return-item quest + token/XP reward | **Missing** |
| Lock-out (shared vs Dark Ruins) | **Missing** |

### Framework plan
1. ItemTemplates leaves for each chip; right-click → `{givequest}` / FindItem2 return.
2. Scarlett content NPC + knubot trade/complete.
3. Attach DOJA loot rows to outdoor SL leaves only (not static dungeons / SL missions).
4. Optional `DailyMissions.json` track `DOJA` for lock-out bookkeeping.

---

## 3) PvP daily mission (John Smith) — partial

Four options from the bar behind Antiques n' such:

| Option | Guide reward | Our state |
|---|---|---|
| Battlestation (UBSL tip) | Full daily XP/SK + tokens + VP; up to 3× / 18h; counts ×3 toward Palmiero | **Deferred** — BS exists; mission update at 425 pts not wired |
| Tarasque (Camelot / Avalon) | Full daily + tokens + VP; counts ×1 Palmiero | **Deferred** — need Tarasque spawn + De'Valos attendant check |
| Illegal Notum Mining | Mini daily + VP; **not** Palmiero | **Done** (`KTCM`) |
| Reclaiming the Land (5 LC tower types) | Mini daily + VP; not Palmiero | **Deferred** — land-control towers / faction targeting |

Palmiero Freelancer's Inc. “3 dailies” meta-quest: **not started**.

---

## 4) The Reck — deferred (sparse)

PF **750** The Reck: geometry + 454 client spawn points exist; **0** `mobspawns` DB rows; **55/69** spawn hashes missing from `NpcTemplates.json`; no Sovic / Tettle / Observer content NPCs.  
**Do not implement dailies until the build plan in `the-reck-design.md` is executed.**

---

## Catalog / code touchpoints

| File | Role |
|---|---|
| `AORebirth/GameData/DailyMissions.json` | Live + Deferred stubs |
| `AORebirth/GameData/Quests.json` | `KTCM` live |
| `AORebirth/GameData/Knubot/JohnSmith.json` | PvP menu |
| `ZoneEngine_New/Core/Events/DailyMissionService.cs` | `{givedaily}` spawner |
| `ddogs/docs/the-reck-design.md` | Reck full build plan |
| `ddogs/docs/halloween-design.md` | Seasonal outdoor (separate) |

## Explicitly not in this pass
- Agency Handler / Senior / Alien Handler implementation
- DOJA drop + Scarlett turn-in
- Battlestation / Tarasque / Reclaiming the Land dailies
- Any Reck daily or boss wiring
- 18h lock-out NCU
- Holo Deck bi-weekly / SL Elite Lab R1 (mentioned on overview only)
