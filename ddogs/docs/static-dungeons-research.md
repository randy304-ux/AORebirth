# Static dungeons

Steps of Madness, playfield 1933, is wired by `ddogs/tools/Build-StepsOfMadness.ps1`. The rest of this note is still research.

A spawn counts as populated when its `hashText` is a key in `AORebirth/GameData/NpcTemplates.json`. A client hash with no template does not produce one of our mobs. Item ids are Auno AOIDs from `itemnames.sql`. Boss lists for Biomare, Steps of Madness, and the Crypt of Home are the AOWiki pages read for this pass (https://wiki.aodb.us/wiki/Biomare, https://wiki.aodb.us/wiki/Steps_of_Madness, https://wiki.aodb.us/wiki/Crypt_of_Home). Temple of Three Winds loot is the table already in `ddogs/tools/Build-TotwContent.ps1`, checked against itemnames.

## Order

1. Steps of Madness, playfield 1933. Wired. 114 client spawns, each hash now has a template. No key. Biomare is next and is not started.
2. Biomare, playfield 1941. This is the Foreman's Office. The layout is there (268 client spawns, none of them templates). Level cap 100. Missions and nodrop uniques.
3. Crypt of Home, playfield 4805. Layout is there (139 client spawns, none of them templates). Switch doors and Cerubin. The Inner Sanctum key halves already have ids. The Pyramid of Home is a separate level-201 raid door and was not found as its own playfield name.
4. Elysium garden key dungeons, playfields 4678-4681. Nascence gardens already have the key quest. These four have client spawns and no templates. Keys exist: Enel 226987 and Shere 226993.
5. Scheol gardens (Ocra 226991, Roch 226992), then Adonis, Penumbra, and Inferno gardens. The key items are in itemnames. The playfield pairing past Elysium was not re-checked this pass, except Nascence (Aban and Thrak), which is already live.
6. The thinner RK layouts already on the not-started list: Smuggler's Den 1862 and Smuggler's Rearranged 125, Cyborg Barracks 1833, Reet Retreat 1913. Layouts exist. Their unique lists were not pulled.
7. Condemned Subway leftovers only. Playfield 127 is already populated. Two spawn hashes have no template.
8. Alien sectors (13, 28, 35, 42, and the Sector 10 pocket bosses). Large, and separate from the static-dungeon gear path.
9. Pandemonium gardens and the Pandemonium raids. Raids stay research-only, same as the dyna pass.

## Already populated

### Temple of Three Winds, 1931

`Dng: Temple of Three Winds`. 251 spawns, all 251 in templates, max level 73. This is the interior `Build-TotwContent.ps1` fills. Outside is Greater Tir County. The ICC teleport is Windcaller Karrec. Level cap on the guide is 60; our boss levels run through the 70s.

Standing named mobs include Defender of the Three, The Curator, Nematet the Custodian of Time, The Re-Animator, Lien the Memorystalker, Guardian of Tomorrow, Gartua the Doorkeeper, Uklesh the Frozen, Windcaller Yatila, and the acolyte / reverend / windcaller / exarch ranks. Khalum is not standing. He spawns when Uklesh dies. Aztur the Immortal spawns when Khalum dies. Lien spawns Ju-ju Dolls on aggro and at half health.

A second playfield, 9061 `Temple of Three Winds`, has 205 client spawns and only 1 template (max level 300). That one is not the populated interior.

Pinned loot already on the templates (chance is the builder's figure):

| Who | Item | AOID |
| --- | --- | --- |
| Faithful rank | Robe of the Faithful | 204761 |
| Acolytes | Acolyte Robe | 204763 |
| Reverends | Reverend Robe | 204762 |
| Windcallers | Windcaller Robe | 204760 |
| Exarchs | Exarch Robe | 204759 |
| Guardian of Tomorrow | Guardian Tank Armor | 204748 |
| Aztur | Stygian Desolator | 204755 |
| Aztur | Holy Book of the Immortal | 204765 |
| Uklesh | Fist of Orion | 204757 |
| Several bosses | Barrow Strength | 204653 |
| The Curator | Rockcrusher Gauntlets | 204758 |
| Book halves | Tattered book: Tree of Enlightenment, sections 1-6 | 204720-204725 |

Also on the same table, not all cited here: Withered Flesh 204698, Energy Scythe 204747, Frost Scythe of the Legionnaire 204746, Bracelet of Amplified Sound 204756. Most of the robe line is nodrop on the AO-Universe note already in `ddogs/docs/research-2026-10-16.md`. No key. The martial-artist book halves go to Hermit Sho-li.

### Condemned Subway, 127

`Condemned Subway (dng)`. 326 spawns, 319 in templates. Trash is the captured mall set (Filth Flea, Slum Runner, Shadow, Discarded Pet, Infector, and the rest of the list in `Build-NpcTemplates.ps1`). Named singles include Deranged Shopper, Eumenides, Vergil Aeneid, Strike Foreman (a subway mob, not the Foreman's Office), and Abmouth Supremus at level 30. Abmouth is the final-room boss: HP 10324, melee 73-148, Infector adds on aggro and at 75/50/25 percent. His look is the client abmouth mesh 155962.

Left without a template, so those points do not spawn: PCBN and BQPW. The builder also records CARO as having no source row. Shop NPCs at level 180 (weapons, armor, tailor, tools) are templates, not bosses. This pass did not pull a subway unique-loot list. The dungeon does not need a from-scratch build.

### Nascence garden key dungeons

Redeemed 4676 and unredeemed 4677. Each has about 20 client spawns. The ones in templates are the Or-Mada and Craig-Or shopkeepers. The rest of the hashes are not templates, so the garden interiors are not filled.

The key quest is live via `Build-GardenContent.ps1`. Keys: The Key to the Garden of Aban 226824, The Key to the Garden of Thrak 226994. Insignias: Aban 214788, Thrak 214789. Quest NPCs: Ecclesiast Aban Fala on 4312, Sipius Aban Lux-Wei inside 4676, Prophet Yutt Thrak on 4311, Hypnagogic Urga-Lum Thrak inside 4677. Insignias drop from Barking Chimeras and the other Frontier beasts the builder names.

## Layout present, mobs missing

| Playfield | Name | Client spawns | In templates | Max level |
| --- | --- | --- | --- | --- |
| 1941 | Bio MARE | 268 | 0 | 100 |
| 4805 | The Crypt of Home | 139 | 0 | 150 |
| 1862 | The Smugglers Den | 153 | 13 | not counted |
| 125 | Smuggler's Rearranged | 279 | 2 | 225 |
| 1833 | Dng: Cyborg Barracks | 36 | 0 | 160 |
| 1913 | Reet Retreat (dng) | 8 | 1 | 185 |
| 6211 | Cyborg Citadel (dng) | 26 | 0 | 90 |

Smuggler's Den's 13 template hits are leets and one Clawfinger, not a filled den.

### Biomare / Foreman's Office, 1941

The wiki treats these as the same dungeon. The door on The Longest Road, about 1930, 775 (whomphah waypoint 2000, 800 on playfield 795), is labeled Foreman's Office. There is no separate playfield whose name contains Foreman. The only "Foreman" template we have is Strike Foreman in the subway.

Level band about 40 to 100. Characters over 100 cannot enter. No lower cap. No key for the door. Optional missions, from Dr. Hercules Lincoln (Omni, Outpost 10-3) and Investigator Ace Starr (Clan, north of the facility):

- Security Card, which gives Security Clearance ID Card - Gamma Clearance and stops the grey guards aggroing up to the third golfball.
- Collar of Amplification.
- Kill T.I.M.
- Recapture Beacon, which warps from Borealis to the entrance.

Bosses and the uniques the wiki pins on them. Ids are the itemnames row for that name. Collar of Amplification is a quality family; 259265 is the low id and 259270 is the high id in that run of rows.

| Boss | Where the wiki puts them | Unique |
| --- | --- | --- |
| Reception Clerk | 210, 330 | summons guards and mechdogs by team size |
| Tri Plumbo | 140, 175 | Corroded Ring 200818; Storm Carbonum Armor (Clan). Armor piece ids were not pulled |
| Lab Director | 380, 365 | Customized IMI Desert Reet 1000, 156771. Sealed Order forms were not id-checked |
| T.I.M. | 380, 250 | HUD Upgrade: Enhanced Target Acquisition 156773; HUD Upgrade: Personal S.T.M 156774 |
| Gunbeetle | shuttle hangar | Salvaged Beetle Blaster 156769 |
| Bodyguard / Bodyguard Rik-Rak | | Gamma Ejector 156770; Assault-class Tank Armor 156576 |
| Neutralizer | | Combat Medic's Light Tank Armor 156575 |
| Executive Protector | spawned by the Lab Director | Enhanced NCU Chip with Recompiling Core 156693 and 156694 |
| Chief Base Protector | | Aged Brandy, id not checked |

Collar of Amplification is the mission reward, not a boss drop. Notum chips and fragments drop from the general population, QL 30 to 100. Personal Safes can hold a Grid Armor summon disc; that rate is the wiki's "very rare", not a number we should copy in as a percent.

### Steps of Madness, 1933

Wired. All 114 client spawns have templates. Respawn times on those points were left as they are (Neleb, Betrayer of Memory, and Thief of Reason 600 seconds). Northern Omni Forest, about 800, 2840, playfield 716. The eyeball surface record names destination 1933. There is no row in the teleports table. Zyvania Bagh is the existing Andromeda spawn ZYBA at 3158.1, 35.5, 899.1 on playfield 655. Talking to her teleports to 40, 33.8, 170 on 1933. The Childhood Memory teddy bear is not a key.

Designed as hard, low-level mobs so the loot is the point, not the XP. Teams from about 30, solo from about 50. Final boss Neleb the Deranged, level 70, inside 1933 at 256, 125. Respawn 10 minutes on the wiki. Adds: Thief of Reason, Betrayer of Memory, Guardian of Thought. He casts Hiding in Memory (98 percent absorb for 34 seconds).

Other named camps: Gleeful Greeter, Unrelenting Fear, Pulsing Hatred, Notum Habit, Sanity's Edge, Mind Shard.

Uniques, itemnames ids:

| Item | AOID | Wiki source |
| --- | --- | --- |
| Neleb's Nano-circuit Robe | 151895 | Neleb, 100% |
| Neleb's Notum Battlerod | 152026 | Neleb, 100% |
| Fractured Sanity | 151896 | Neleb, 100% |
| Fear-forged Blade | 151903 | Unrelenting Fear |
| Nervejolter | 152025 | Pulsing Hatred, 100% |
| Neutrino Flash | 152027 | Notum Habit, 100% |
| Emotional Sponge | 152031 | Suppressed Emotion |

The rest of the corpse uniques are in itemnames.sql and are on the mobs. Dream Mesh Circuit 274972 and Dark Dreams 274971 (Neleb, 55 percent for one copy). Loving Hands 152022, Brutal Hands 152021, Gentle Hands 152023. Essence of Pure Jealousy 152030. Cortex of the Executioner 152032. Brainchopper 152024. Nightmare Arachnid Ether 274958, Darkness 274961, Spacial 274959, Burning 274960, Tidal 274879. Sanity Recovery Medication 275003 and Nightmare Therapy Pills 275004. Rates that the guides do not number are 25 percent, except Emotional Sponge at 40 and the Childhood Horror arachnid ether at 40. The upgrade results (Neleb's Nano-master Robe 274956, Neleb's Nightmare Battlerod 274955) are tradeskill, not drops. They are Neleb's worn look: he is a Nanomage male (breed 3, the captured nanomage head 40173) in 274956, and 274955 is the rod in his hand. Roving Eye and Baleful Eye use the Distracting Sphere mesh 96194.

### Crypt of Home, 4805

Broken Shores, about 355, 2215, northwest of the City of Home. Guide level cap 110. Unlike Biomare, a beacon warp can still put a higher level inside. No single key item for the front door. Progress is paired switches (the wiki's points i and ii, then iii, iv, v, vi, viii, ix). The door after the first pair stays open about 5 seconds.

The Inner Sanctum key is two halves, already in itemnames, and is a different door from the Crypt's switch run: top half 206257, bottom half 206258.

Bosses:

- Bane, level 80, first guardian.
- Alpha Skincrawler, level 100.
- Awakened Pit Demon, level 100.
- Crypt Guardians, about 90-100.
- Cerubin the Rejected, level 150. Up to four tentacles (Chill, Power, Cure, Shock, Burning Truth, Lazy). Cure heals him. Wiki respawn 10 minutes, corpse 30 minutes.
- Cenobite Shadow, level 80-85, three halls, one-hour respawn. These drop the Blackbird line.

Uniques that itemnames actually contains:

| Item | AOID |
| --- | --- |
| Cloak of the Revoked | 245135 |
| Necromancer Cloak | 245170 |
| Spirit Tech Circlet of Cerubin | 245139 |
| Sacrificial Ensigns of Cerubin | 246219 |
| Shapeshifter's Vest | 246222 |
| Blinded Blackbird | 246724 |
| Blackbird | 246720-246723 |
| Crispy Chiroptera | 246714 |
| Hungry Howlet | 246709 |
| Pained Panther | 246719 |
| Chiroptera / Howlet / Panther | 246705-246718 |

The wiki's Spanish-named rings and collars ("Anillo Casero", "Collar Casero") were not matched to an English itemnames row this pass. Pit Demon Heart, Human Skin Hood, and Bracer of Recondite Flames were not id-checked. The Pyramid of Home (patch 18.8, level 201, raid, behind Number Nine in the Temple of Home) is not this playfield.

## Garden keys

Every "The Key to the Garden of ..." row in itemnames:

| Key | AOID | Zone note |
| --- | --- | --- |
| Aban | 226824 | Nascence, redeemed. Live |
| Thrak | 226994 | Nascence, unredeemed. Live |
| Enel | 226987 | Elysium, from the Jobe armor note |
| Shere | 226993 | Elysium, same note |
| Ocra | 226991 | Scheol, same note |
| Roch | 226992 | Scheol, same note |
| Cama | 226985 | zone not re-checked |
| Dalja | 226986 | zone not re-checked |
| Gilthar | 226989 | zone not re-checked |
| Lord Galahad | 226988 | Inferno garden chain |
| Lord Mordeth | 226990 | Inferno garden chain |
| Vanya | 226995 | Inferno / temple chain |

Garden interiors besides Nascence have client spawns and zero templates: Elysium 4678, 4679, 4680, 4681 (max level 95); Scheol 4682 and 4698 (max 110); Adonis 4684 and 4685; Penumbra 4688 and 4689; Inferno 4692, 4693, 4694 (max 200); Pandemonium 4696 (90 spawns, max 300) and 4697 (80 spawns, max 258). Redeemed main 4010 has no spawns. Shadow Crypt 4885 has no spawns.

Insignia of the Redeemed 231154 and Insignia of the Unredeemed 231155 exist. So do Lord Galahad 224051, Lord Mordeth 224050, and Vanya 224049. Those are the quest items, not a substitute for the garden mobs.

## Sectors

Not garden-key dungeons. Client spawn counts, none of the hash texts in NpcTemplates:

| Playfield | Name | Spawns |
| --- | --- | --- |
| 4374 | Sector 10 | 399 |
| 4365 | Sector 13 | 167 |
| 4366 | Sector 28 | 6 |
| 4367 | Sector 35 | 166 |
| 4370 | Sector 42 | 577 |
| 4474 | Sector 10 team instance | 1 |
| 4475 | Sector 10 solo instance | 1 |

`Build-AlienInvasion.ps1` places the Unicorn Duty Sergeant and the Sector 10 mission targets against 4374. That is not the same as the 399 client hashes becoming templates. Sector 10 pocket bosses (Ilari, Chemist, Ankari, and Cha Khazoh Ra) and the DNA cage are still the open item in `ddogs/docs/research-2026-10-16.md`. Sectors 13, 28, and 35 are the general-loot sectors on that same note. They are later than the RK statics above.

## Left out on purpose

- Pandemonium raids. Still no coordinates and no item ids. Not spawned.
- City and apartment rooms whose names contain "dng". They are not these dungeons.
- The Reck (750) and Camelot (120). Outdoor or town layouts, not the static list.
- Alien ship interiors. Already in the Alien Invasion work.
