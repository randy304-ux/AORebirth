# Current Task

Stage (no deploy) merc menu + ICC Buffbot/terminal fixes on
`cursor/merc-buffbot-icc-0049`.

1. Nested `text://` opens merc detail/Macros/hire windows client-side; say-commands
   remain for cast/summon/dismiss/preset/filter (buffbot-style actions).
2. Merc detail buff lists copy that profession's Buffbot.json section layout.
3. Merc casts land from the summoned merc NPC onto the player; failures logged.
4. ICC HQ Buffbot + Contract Terminal positions in PlayfieldContent/655/Npcs.json;
   live miss was stale `Built/Debug/ZoneEngine_New/GameData` (not DB).

Deploy only when Randy approves. Local commit only; no push/PR.
