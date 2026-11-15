# Current Task

## Active: BFG Fling range and BFG9000 (staged, not deployed)

Branch `cursor/bfg9000-1115` @ worktree `I:\AO\worktrees\bfg9000-1115`, from `d7f4d65d`.
Local commit only. No push, no deploy, engines left running.

### What
1. BFG Fling blast uses `WeaponFlingEffects.json` DamageByQl (QL300 1070-2310), not 75% of a normal hit.
2. BFG client projectile is Hellfury 153977 effect 2670; hit impact stays explosion 2100.
3. Soldier BFG9000 items 307107-307113. 307114+ left for the later illegal version.

### Build
`I:\AO\src\tools-temp\zbuild-bfg9000` (Debug). Offline load vs a copy of live GameData plus the new slice.
