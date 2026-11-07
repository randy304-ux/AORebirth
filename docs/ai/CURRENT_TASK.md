# Current Task

## Active: NPC patrol import (GO deploy)

Branch `cursor/rk-desert-dyna-loot-15f4`. Tip base `1cd4db32` + prior local `c66703c1`.

### What
- Wired existing `PatrolNode` for spawn-entry `patrolWaypoints` (walk speed, 2–6 s pauses, resume nearest after leash).
- Matched `I:\AO\imports\gpt-moves-20261107-r2\patrols.json` to installed spawns (15 m XZ, same name/PF).
- Validation PASS (200/200 scraped spot-check; synthetic Y within 10 m).
- Build: `tools-temp\zbuild-patrol`. Deploy script: `tools-temp\deploy-patrol.ps1`.

### Counts
Matched **3508** / unmatched **850** / skipped synthetic **0**. Newland Desert: 518 matched, 35 unmatched.

### Deploy
Randy authorized GO after validation+build. MySQL 33070. Rollback `tools-temp\rb-patrol`.
