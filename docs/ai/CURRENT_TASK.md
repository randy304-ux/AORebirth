# Current Task

## Active: batch-1111 (content + Battle Station + starter playerbots)

Branch `cursor/batch-1111` @ worktree `I:\AO\worktrees\batch-1111`.
Base tip `959fa5a3`. Local commit only -- no push/PR.

Merges: `cursor/content-1111`, `cursor/bs-score-1111`, `cursor/playerbots-1111`.

### Battle Station
Score HUD + solo bot pressure. Capture in
`I:\AO\imports\batch-20261111\scrape\bs_score_packets_20261111_Killzaw.csv`
(PF 4384, SendScore `0x44483B3A`, end Red 91 / Blue 502).

### Starter-area playerbots
5-10 player-looking bots per starter hub (Arete 6553, ICC 4582, Nascence 4313):
roam POIs via navmesh/straight PathTo, fight level-appropriate mobs, rest when hurt.
Config: `GameData/PlayerBots.json` (Enabled default true, count 8).

### Build / deploy
Matched Core + ZoneEngine_New + Messaging from this merged tree:
`I:\AO\src\tools-temp\zbuild-batch-1111`.
Engines must be started only via `schtasks /run /tn "\DungeonDogs\AO Dungeon Dogs Engines"`.
