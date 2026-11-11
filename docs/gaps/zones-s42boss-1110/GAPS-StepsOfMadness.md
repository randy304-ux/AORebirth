# Steps of Madness (PF 1933) — gaps

Templates already exist in NpcTemplates.json at tip 09e49e0b (NETH, THOF, BYOM, SAED, GLGR, and the rest of the Build-StepsOfMadness hashes), including guide loot rates. No PlayfieldContent/1933 was added so the loader would not see a second loot table or unknown JSON. Client Playfields/1933/Spawns.json stays the spawn source.

## Loot

- 33 loot rows were captured 2026-11-10. They agree with existing unique assignments except:
  - Thief of Reason (THOF) had no loot row on this run (gap). The existing template already has Brutal Hands at 25% from the AO-Universe guide (rate not published). That rate was not overwritten.
  - Sanity's Edge dropped Brutal Hands on this run; existing templates assign Brutal Hands to THOF and Loving Hands to SAED. Guide assignment kept; one-run 100% drops were not applied.
- Neleb captured: Fractured Sanity, Neleb's Nano-circuit Robe, Neleb's Notum Battlerod, Dream Mesh Circuit (existing template also has two Fractured Sanity and Dark Dreams 55%).
- Foundation of Sanity and Gleeful Greeter were not up on this run (long client respawns). Existing templates left as-is.
- Engineer Gladiatorbot L64 seen in the scrape is treated as a player pet, not a zone spawn.
