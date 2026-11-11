# Current Task

## Active: Starter-area playerbots (local commit only)

Branch `cursor/playerbots-1111` @ worktree `I:\AO\worktrees\playerbots-1111`.
Base tip `959fa5a3`. Local commit only — no push/PR/deploy.

### Goal
5-10 player-looking bots per starter hub (Arete 6553, ICC 4582, Nascence 4313):
roam POIs via navmesh/straight PathTo, fight level-appropriate mobs, rest when hurt.
Config: `GameData/PlayerBots.json` (Enabled default true, count 8).

### Build
`I:\AO\src\tools-temp\zbuild-playerbots-1111` (matched Core + ZoneEngine_New).
Engines must be started only via `schtasks /run /tn "\DungeonDogs\AO Dungeon Dogs Engines"`.
