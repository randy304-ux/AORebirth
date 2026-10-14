# Handoff: remaining content wave (dynas, gardens, RK statics, Elysium)

State at 2026-10-13 22:20 ET: team missions live (commit below), Nascence filled (8dd80eac, 235aea1a).

## Conventions (follow these)
- Content is generated: edit builders in ddogs/tools, then run `powershell -File ddogs\tools\Build-NpcTemplates.ps1` (runs Arete, TotW, Garden, Guessed, MissionBosses sub-builders). Run Build-NewbieQuests.ps1 after quest/Knubot changes. The builders are deterministic, so a run with no changes produces no diff.
- Guessed data: put it in Build-GuessedContent.ps1 rows (code, name, minL, maxL, donor hash, source). They are marked `ContentProvenance.Guessed=true`. Real captures always win.
- Spawn coverage: compare `Playfields/<pf>/Spawns.json` `spawns[].hashText` against the top-level keys of NpcTemplates.json.
- Mission bits for new quests: 645 and up (server-only block 640-767). Document them in ddogs/docs/MISSION_BITS.md.
- Deploy: `tools-temp\totw-deploy.ps1`, run detached (Start-Process). It handles backup, stop, migrations, build, copy, boot. Add any new PlayfieldContent PF ids to its copy list. Check the ZoneEngine_NewLog for rejected=0/skipped=0 and for "npc templates leaves=".
- Commit as `-c user.name="Randy Craig (Grok Bot)" -c user.email=randy@localhost`. Never commit client-patch artifacts.
- PowerShell gotchas: wrap single nested arrays as `@(,@(...))`. .NET file APIs use [Environment]::CurrentDirectory, so use absolute paths.

## 3. Dynas (RK outdoor camps)
- Sources: Saavick's map v2.0 is already in client `cd_image\textures\PlanetMap\SaavicksMap` (saavicksmap.txt has the dyna markers with levels per playfield; parse its label/coords entries). v3.3 is on the Funcom forum (topic 75503). Use AO-Universe dyna guides and Imoutochan's dyna master-weapon list (Funcom forum) for boss names and loot. Get item IDs from items.dat/itemnames. aopocket.tngk.com (boss loot tables) returned 502; try the web.archive.org copy.
- Build: one Build-DynaContent.ps1. Each camp gets a named boss template (higher HP, LootTable with real IDs) plus 3-6 camp adds placed as PlayfieldContent spawns. Respawn is about 15-30 min (respawnTime). Mark positions taken from the map image as Guessed.
- Engine check: the dyna "buff" (the boss ends up buffed when its camp is left alive) is not implemented. A BossTriggers On=FirstAggro SpawnAdds is the closest existing mechanism.

## 4. Gardens (other than Aban/Thrak)
- Pattern: Build-GardenContent.ps1 plus the garden section of Build-NewbieQuests.ps1. Statue, pet and vendors are content NPCs. Key quests follow AO-Universe "<garden> garden key quest". Use bits 645 and up.

## 5. RK statics (Steps of Madness, Smuggler's Den, Cyborg Barracks, Reet Retreat, Crypt of Home)
- Pattern: Build-TotwContent.ps1 (spawn hash to template, CommonLoot, named bosses with loot, BossTriggers). Saavick's map has indoor maps for SoM, Smuggler's Den and Crypt of Home. Mob lists and loot come from AO-Universe; IDs from aogalaxy (auno returns 403).

## 6. Elysium
- Pattern: section 8d in Build-NpcTemplates.ps1 (position match against retired capture ElysiumEastMobRuntime.cs in git history ecefa673^). Fill the rest through Build-GuessedContent.ps1.

## Team missions (done; follow-ups)
- The boss is not in the frozen token census, so it respawns if the dungeon is rebuilt after a restart.
- Boss loot is regular family loot x3 rolls at doubled chance. Real retail mission-boss loot is still to be researched.
- The roll response wire is unchanged. The client sees team-mission status only through the journal flag 0x100.

## Ergo ring (zone access) quests, all SL zones

Each SL zone's Ergo gives the ring quest that is the intended way through that zone's portal (usually at the north end) to the next zone. Higher-level characters can skip the trial and get the ring directly (Adonis: level 160+, per AO-Universe). Sources: AO-Universe Shadowlands quest guides; wiki.aodb.us "Shadowlands Missions" (Playfield Protection and Access Ring Missions).

| From -> To | Ring | Extra protection reward / requirement | Status here |
|---|---|---|---|
| Nascence -> Elysium | Insipid Ring | - | Started: Deep Breath Nine dialog stub (DEBE 430,450 Wilds 4311). No quest yet |
| Elysium -> Scheol | Caliginous Ring | - | Not started |
| Scheol -> Adonis | Achromic Ring | Metawater Repellent Spiritech Suit | Not started |
| Adonis -> Penumbra | Rimy Ring (5 variants by profession unit: Artillery/Control/Extermination/Infantry/Support) | Hyper-Radiation Protection Skin and Glasses; Watcher trial (6x Chunk of smoking rock, Mark of the Watcher, hand in Achromic Ring; Dr. Pons instructions) | Not started |
| Penumbra -> Inferno | Sanguine Ring | Lava Protection Boots, Skin Galvanizer | Not started |
| Inferno -> Pandemonium | Pure Novictum Ring | "Hold Hell at Bay" ring quest | Not started |

### Insipid Ring (Nascence), per Randy
Ergo asks for one of two paths:
1. **Fumigating Canister** -> hand to **Deep Breath Nine** (DEBE, 430,450, Wilds 4311). Needs the Fumigating Canister item and a Knubot Trades section (the {opentrade} was removed from DeepBreathNine.json until then).
2. **Long Stare at Danger** (LSSR, 1401,578, Swamp 4312) gives a key to a one-floor Molokhai dungeon. Kill chain: Embryonic Molokhai L35 -> Larval Molokhai L37 -> Metamorphosing Molokhai L39 -> Young Molokhai L42, which drops **Baby Molok Oil**. Hand the oil to Ergo for the Insipid Ring.

Todo: the item IDs for the rings, the canister, the key and the oil (from items.dat); Ergo dialog per zone; portal checks for the ring; mission bits from 645 up (MISSION_BITS.md). The per-zone steps for the Caliginous, Achromic, Sanguine and Pure Novictum quests still need reading from the AO-Universe guides.

## Yuttos shop stock (Nascence Wilds 4311)
Six content-NPC shops (Build-YuttosVendors.ps1): Machines (YUME 298,1487), Manuals, Pharmaceuticals, Chemicals, Tools, Electronics. YUMA (315,1542) has no template (so there's only one Machines). The stock is **empty until Randy supplies item names**. Each shop's list goes here once he has them:
- Machines: (empty)
- Manuals: (empty)
- Pharmaceuticals: (empty)
- Chemicals: (empty)
- Tools: (empty)
- Electronics: (empty)

## Working rules (agents)
- **PROGRESS file:** keep `ddogs/docs/PROGRESS.md` current. Before you stop, and after each commit or deploy, add a dated entry: what you did, the commit hashes, what you deployed, what's untested, and the next step. Read it first when you pick up work.
- **Stop/deploy routine:** 1) back up the DB first; 2) stop the engines, build and copy, then start them with `boot-engines.cmd`; 3) confirm a clean load with rejected=0 (and skipped=0) in the logs; 4) keep downtime short and batch the deploys.
- **Never** touch `I:\War`, MySQL port 3306, or the WAR server ports.
- **Nothing on C:**. Logs, backups, temp files and caches go on I:.
- AO Cursor worker: `r730xd-ao` (task "Cursor Worker r730xd-ao"; `I:\AO\tools\start-cursor-worker-ao.ps1` / `stop-cursor-worker-ao.ps1`; logs in `I:\AO\tools\cursor-worker\logs`).
