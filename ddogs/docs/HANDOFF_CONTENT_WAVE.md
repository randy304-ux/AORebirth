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
