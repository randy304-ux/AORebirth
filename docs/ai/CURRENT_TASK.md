# Current Task

## Active: Player/merc buff persistence after login and zoning

Branch `cursor/buff-persist-rootcause-724e` @ worktree `I:\AO\worktrees\buffpersist-1115`, from `255d83fd`.

### Root causes
1. **Player login**: DB save/load works; `TryRestoreBuff` could silently no-op on `!spell.IsBuff`. Client NCU needs `SetNanoDuration` after FullCharacter (SCFU ActiveNanos alone insufficient for self).
2. **Merc zoning**: `RestoreBuffs` only mutated memory after empty spawn SCFU — no client packets; AI either blocked by `HasNano` or recast from scratch.
3. **Merc logout**: `DismissAll` clears stash; `TickMercs` respawns from contracts with no buff carry (no schema change).

### Fixes
- Drop `IsBuff` gate on restore; announce restored buffs with **remaining** duration after login/reconnect FullCharacter; merc `RestoreBuffs` calls `AnnounceRestoredBuff`.
