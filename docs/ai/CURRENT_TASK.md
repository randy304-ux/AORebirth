# Current Task

## Active: Anvil subway merc giver Deal fix (staged, not deployed)

Branch `cursor/anvil-fix-1114` @ worktree `I:\AO\worktrees\anvil-fix-1114`.
Base `3834f8e8`. Local commit only — no push/PR/deploy.

### Cause
`{dungeonmerc}` looks up `playfield.GetService<MercUnlockService>()`, but playfield DI never registered the root singleton. `CanApplyAll` failed on Deal → Fail/close with no quest, no summon, and no Warn.

### Fix
- Register `MercUnlocks` on playfield services.
- Warn when a Knubot line's effects cannot apply (names the failing effect).
- Keep dungeon_only unlock in memory if DB upsert fails; surface summon refusal text.
- Carry live Anvil GameData (MercGiverSubway Replies, Anvil position, PCBN SpawnSkip).

### Build
`I:\AO\src\tools-temp\zbuild-anvil-fix` (Release). Offline zeload PASS (quests=605, Knubot rejected=0).
