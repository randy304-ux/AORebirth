# Halloween design (2026-10-26)

Outdoor / low Halloween content for Dungeon Dogs. GM toggle: `.event halloween on|off|status`.

Sources (one read each): AO-Universe Enigma Grove, It Came From Below, The Hollow Reaper, Uncle Pumpkin-Head hunting guide.

## Done (loads on next Zone restart)

### GM toggle
- `.event halloween on|off|status` (GM 1+)
- `HalloweenService` + `GameData/Halloween.json`
- Spawns city leets and outdoor Uncle Pumpkin-Heads while ON; despawns on OFF; respawns on a 180 s timer while ON

### Quest givers (spawned only while Halloween is ON)
| Hash | Name | Playfield | Pad |
|---|---|---|---|
| DRCL | Draculeet | 800 Borealis | 690,5,590 (Antiques area) |
| FRKL | Frankenleet | 800 Borealis | 695,5,585 |

Knubot scripts: `Knubot/Draculeet.json`, `Knubot/Frankenleet.json`. MonsterData from client: Draculeet 256518, Frankenleet 256519.

### Outdoor / low Uncle Pumpkin-Head
| Key | Zone | PF | Level | Hash |
|---|---|---|---|---|
| uph-borealis | Around Borealis | 800 | 10 | UPH1 |
| uph-icc | ICC HQ floor (Andromeda) | 655 | 40 | UPH1 |
| uph-last-ditch | Last Ditch (Stret West Bank) | 790 | 40 | UPH1 |
| grief-holes | Southern / Borealis Approach | 791 | 30 | GRPH |
| grief-stret-west | Stret West Bank | 790 | 75 | GRPH |

MonsterData: `pumpkinman` 154136. Loot uses real DB item families: Lya's Sangi (LYAS), Tertium Quid (TERT), Pumpkin Helmet, Halloween candy bags, Lil Spider Pet, Digummy Merk's Jacket, plus common trash.

### Daily: Killing the Competition (reusable framework)
- `GameData/DailyMissions.json` + `DailyMissionService` + Knubot `{givedaily HASH}`
- Quest `KTCM` in Quests.json: KillMultiple x3 `RGNM` (Rogue Notum Miner), reward 25 VP (stat 669) + 500 XP
- John Smith always present in Borealis PlayfieldContent (`ddogs-john-smith` / Antiques ~690x590)
- Field stand-in: Stret West Notum Ore district on PF 791 (pads around 220,1.2,1060) until the real Notum Mining Area map is wired
- **Ganking Uncle Pumpkin-Head (`GNKP`)**: 100% chance every accept; official 6-hour respawn ignored; level = BS bracket max for the player (30/60/100/150/200/214/220)
- Rogue miners drop Illegal Notum Chunk (`ILNC`)

Add more dailies later by appending `DailyMissions.json` + a Quests.json row + a `{givedaily HASH}` knubot line.

## Deferred (high tier / instanced raid)

| Encounter | Why deferred |
|---|---|
| Enigma Grove (DAV 3379x258) | QL 300 Entvines waves, 210+ team content |
| The Hollow Reaper (DAV Golem Island) | Level 250 raid UPH, AOE nukes, Spectral / Rollerpumpkin adds |
| It Came From Below (Uturn Forest lab instance) | Full instanced dungeon + lab specimens / Pumpkinman |
| Outdoor UPH Broken Shores 125-150, Mort 170-200, Perpetual Wastelands 200-250 | High outdoor bands |
| Griefing UPH 2HO / 4 Holes / Upper Stret / BS river / NMA up to 300 | Mid-high griefing bands |
| Mission-terminal UPH missions from Draculeet/Frankenleet | Needs mission generator hook |
| Real Notum Mining Area PVP instance + 0% suppression | Map / hub console not wired; daily uses Stret West Notum Ore stand-in |

## Files
- `AORebirth/GameData/Halloween.json`, `DailyMissions.json`
- `AORebirth/GameData/Knubot/Draculeet.json`, `Frankenleet.json`, `JohnSmith.json`
- `AORebirth/GameData/NpcTemplates.json` leaves: UPH1, GRPH, GNKP, DRCL, FRKL, JNSM, RGNM
- `AORebirth/GameData/ItemTemplates.json` families: LYAS/*, TERT, ILNC, PHEL, HWC*, LISP, DMJK
- `AORebirth/GameData/Quests.json` KTCM
- `AORebirth/GameData/PlayfieldContent/800/Npcs.json` John Smith
- `ZoneEngine_New/Core/Events/HalloweenService.cs`, `DailyMissionService.cs`
- `ZoneEngine_New/Core/Commands/EventCommand.cs`
- Knubot `{givedaily}` in `KnubotParser` / `KnubotEffects`
