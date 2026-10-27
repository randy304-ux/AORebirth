# Dailies design (Randy list, 2026-10-27)

Reusable framework: `GameData/DailyMissions.json` + `DailyMissionService` + `DailyLockoutService` (18h) + Knubot `{givedaily HASH}` + Quests.json.  
Halloween outdoor note: `halloween-design.md`. Reck build plan: `the-reck-design.md`.

## Priority status

| # | Track | Status | Notes |
|---|---|---|---|
| — | Killing the Competition | **Live** | `KTCM` |
| 1 | Freelancers Inc. Agency (RK / Elite / Alien) | **Live (stand-in)** | Rome Blue 735 / Old Athen 540 desks; roll tables; token `303237`; 18h lock-outs |
| 2 | DOJA chips | **Live (stand-in)** | ItemTemplates + use hook; Scarlett PF 4001; outdoor SL loot rows; Dark Ruins separate lock-out |
| 3 | PvP daily (John Smith) | **Live partial** | `KTCM` + `PVBS` (425-pt hook, max 3/18h) + `PVTA` Tarasque stand-in. `PVRL` still deferred |
| 4 | The Reck | **Deferred** | See `the-reck-design.md` |

## Live tracks

### Agency (`AGRK` / `AGEL` / `AGAX`)
- Receptionist / Handler / Senior / Alien stand-in NPCs in PF **735** (Rome Blue ~660×315) and **540** (Old Athen ~515×440)
- `{givedaily AGRK|AGEL|AGAX}` rolls level-banded Quests; rewards Freelancers Inc. Token `303237`
- Elite uses Pumpkinhead culls until dungeon wave catalogs exist
- Alien reuses / fills `AIWP` `AIBG` `AILP` `AICC` `AIPD` with Zix/Pendpod stand-in targets + AXP%

### DOJA
- Right-click chip AOIDs → `DojaChips.TryUse` → assign `DJ**` → Scarlett `{take}` + `{completequest}`
- Outdoor SL loot Chance=8 on selected Nascence / Scheol / Penumbra / Inferno leaves

### PvP (John Smith)
- `PVBS`: BattleStationService fires at 425 either side; `MaxPerWindow=3`
- `PVTA`: Avalon PF **505** Tarasque stand-in (~2092×3822, MonsterData 158175)
- `PVRL` Reclaiming the Land: still deferred (LC towers)

## Gaps still open
- Real Agency mission catalogs (dungeon waves, IS, Escaped Prisoners, Dust Brigade, Biodomes)
- Real Alien larvae / cocoon / A-Pod hashes and Unicorn Outpost Agency link from city desks
- Full AO-Universe DOJA outdoor drop lists (only a sample of SL leaves tagged)
- De'Valos Attendant Tarasque up-check; Camelot dungeon interior spawn
- Land-control tower daily; Palmiero Freelancer meta-quest
- Reck (see build plan)
