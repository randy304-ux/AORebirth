# Mission bits (quest completion flags)

The engine keeps 768 mission bits per character (`Core/Quests/MissionBits.cs`), 32 to a stat.

| Bits | Stats | Who sees them | Use |
|---|---|---|---|
| 0-575 | MissionBits1-18 (256, 257, 303, 432, 65, 66, 67, 544, 545, 617, 618, 619, 198, 685, 686, 692, 693, 694) | sent to the client | **Do not use for ddogs quests.** These are the retail client stats. Retail item and statel data read and write them with SetFlag/ClearFlag/BitAnd, and stat 198 (bits 384-415) is also SessionTime. A bit that no quest file uses can still be touched by a client item, so "unused in Quests.json" does not mean it is free. |
| 576-639 | MissionBits19-20 (server-only 10001, 10002) | server only | Original block. Used: 600-604, then the newbie/Arete quests 610-639. **Full.** |
| 640-767 | MissionBits21-24 (server-only 10004-10007, `CharacterStat.MissionBits21..24`) | server only | **ddogs block, added 2026-10-13.** Garden keys use 640-644 (Fala 640, Lux-Wei 641, Yutt Thrak 642, Urga-Lum device 643, Urga-Lum key 644). Drake's Training Ground tests use 645-652 (strength 645, agility 646, sense 647, El-Domo 648, Celeth-El 649, psychic report 650, intelligence 651, brink 652). Insipid Ring uses 653 (trial) and 654 (level 50 skip). Next free bit: **655**. |

How this was checked (2026-10-13):
- Quests.json uses only 600-604 and 610-639. No other GameData JSON has a MissionBit field.
- The DB `stats` table had no rows for any mission-bit stat (only one character in cellao_codex_clean). So no character data needed migrating.
- Bits 0-575 were rejected because the client owns them, not because they were found in use.

Adding more: `Build-NewbieQuests.ps1` hands out bits in file order from `$script:bit`. The garden section sets it to 640 and the check stops at 767. Append new chains at the end and update the "next free bit" above. Beyond 767, add MissionBits25+ (0x2718 onward) to CharacterStat.cs, the `MissionBits.Stats` array, `StatCollection.IsServerOnly` and `MissionBits.Count`.