# Current Task

## Active: Anvil dungeonmerc Twink body (staged, not deployed)

Branch `cursor/anvil-fix-1114` @ worktree `I:\AO\worktrees\anvil-fix-1114`.
Prior live: `2f6fa402` (DI register). Local commits only — no push/PR/deploy.

### Cause (follow-up)
`MercUnlockService.Summon` called `HireMerc(..., Mercs.Tier.Basic)`, so Deal reused the MREN basic-hire path and brought back a plain Mercenary Enforcer instead of Twink Anvil.

### Fix
Hire dungeon unlocks at `Mercs.Tier.Twink` so gear/name/ConfigureSummonedMerc use the MercRoster body.

### Build
`I:\AO\src\tools-temp\zbuild-anvil-fix2` (Release). zeload vs live Built GameData.
