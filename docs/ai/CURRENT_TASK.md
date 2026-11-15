# Current Task

## Active: BFG Fling range and BFG9000 (staged, not deployed)

Branch `cursor/bfg9000-1115` @ worktree `I:\AO\worktrees\bfg9000-1115`, from `d7f4d65d`.
Local commit only. No push, no deploy, engines left running.

### What
1. BFG Fling blast uses `WeaponFlingEffects.json` DamageByQl (QL300 1070-2310), not 75% of a normal hit.
2. BFG 307100-307106 uses Hellfury mesh 154139 / icon 154362. BFG9000 stays on Hellspinner mesh 154141 / icon 154361. Both fire projectile 2670 and impact 2100. The two cannons' bolt colors are the same client value.
3. Soldier BFG9000 items 307107-307113. 307114+ left for the later illegal version.

### Build
`I:\AO\src\tools-temp\zbuild-bfg9000` (Debug). Offline load vs a copy of live GameData plus the new slice.
