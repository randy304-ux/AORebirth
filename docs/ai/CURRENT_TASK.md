# Current Task

## Active: Pet Warp / merc come (staged, not deployed)

Branch `cursor/anvil-fix-1114` @ worktree `I:\AO\worktrees\anvil-fix-1114`.
Prior live tip `e23eb3fe`. Local commits only — no push/PR/deploy.

### What
1. Retail Pet Warp nano 209488 (`FunctionType.SummonPets` 53181): warp owned pets+mercs to caster.
2. `.merc come` / `.merc warp` (10s cooldown) + merc menu link.
3. Merc TickMercs auto-warp: >40 m or no progress ~8 s, not in combat.

### Pet Warp args
Nano 209488 OnUse SummonPets Arguments=`[0]` — 0 means all living owned pets/mercs on the playfield; `>0` treated as max range in meters.

### Build
`I:\AO\src\tools-temp\zbuild-petwarp` (Debug). zeload vs live Built GameData.
