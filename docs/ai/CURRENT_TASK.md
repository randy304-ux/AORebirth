# Current Task

## Active: Adonis full scrape import STAGED (not deployed)

Branch `cursor/gil-note-drop-0f04` (+ local Adonis commit). Tag `scrape-adonis-20261106`.
Stacks on staged tip `b7b2d9a4` (Gil Notes / Joker's Glyph stack-of-6).

- Source: `I:\AO\imports\scrape-20261106-ado\`
- SpawnsAdd: 4872=671, 4873=580, 4213=88, 4625=32, 4626=38
- Templates: `NpcTemplates.Adonis.json` (296); loot `ItemTemplates.Adonis.json` (150)
- Vendors: 7 provisional Yuttos in `PlayfieldContent/4872/Npcs.json` (unresolved typed join)
- Eradicus template+loot only (Joker's Glyph 227905); gen dungeon IDs not hostable as SpawnsAdd
- Deploy script ready, not run: `tools-temp\deploy-ado.ps1`
- ZoneEngine_New build: `tools-temp\zbuild-ado`

No DB writes, no engine restart, no GitHub push for this import.
