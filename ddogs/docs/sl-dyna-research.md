# Shadowlands dynabosses

Nascence camps are wired by `ddogs/tools/Build-NascenceDynas.ps1`: the boss rolls 5 to 7 items, respawn is 180 seconds, and the adds keep normal family loot. The live coordinates are in `ddogs/tools/Build-NascenceDynas.last.txt`. Every other zone in this note is research only and is not loaded.

## The new SL map

The map Randy means is Bitnykk's SLmap v2.22 (April 2022), the update of Onack's AoSL. Its own readme says layer 2 is "everything important dynabosses included." Layer 1 is the small zone layer. Layer 3 is nanos, quests, and NPCs.

It is already on this client and already in the launcher payload. `F:\DungeonDogs\AO-client` does not exist. The live client is `I:\AO\client-ddogs` (marker `.ddogs-client`). Both the normal and wide packs are installed:

`I:\AO\client-ddogs\cd_image\textures\PlanetMap\SLmap\`

Same bytes are in `I:\AO\launcher\client-files\cd_image\textures\PlanetMap\SLmap\` and in `I:\AO\launcher\dist\client-files\...`. SHA-256 matches for `SLmap.txt`, `SLmap2.bin`, and `SLmapWide2.bin`. The published launcher manifest `dist\manifest.json` version `2026.10.15-2236`, `baseUrl` `client-files/`, already lists all 10 SLmap files (txt, xml, three normal bins, wide txt, three wide bins). `Stage-PlanetMaps.ps1` is what copied `SLmap-v2.22.zip` into that payload. Nothing has to be added to ship the map. A player still has to pick it once: P, then i, Select Map, SLmap. If P errors, the map's own fix is `/setoption ShadowlandMapIndexFile "SLmap/SLmap.txt"` or `"SLmap/SLmapWide.txt"`. The launcher does not set that option.

An older `PlanetMap\Shadowlands\` pack (`NewShadowlands`, `SLLevel1.bin` / `SLLevel2.bin`) is also on the client. That is not v2.22.

The label text is burned into the PNG tiles inside `SLmapWide2.bin` (layer 2, 5888×23808, 46×186 tiles of 128). `SLmapWide.txt` is only the tile index. `SLmap.xml` is the playfield origin table, not the dyna list. Boss names are not stored as ASCII in the bin.

Reading those tiles: white label ink was masked and run through Windows OCR, in two passes. The reads below are the ones that match a real camp or region name. OCR spelling is quoted. Pixel x,y is on that wide layer-2 image, origin at the top-left of tile 0. A pixel-to-playfield fit against `SLmap.xml` was tried (Swift Wind, Sifnos, Howling, the Inferno pattern labels) and the scale was not stable, so those pixels are not converted into playfield coordinates here. Camp x,z below stay the guide figures. Where the map and a guide name the same boss, the map is the confirmation that the name is on the atlas.

| OCR | pixel | Reads as |
| --- | --- | --- |
| HOWLING | 2994,4504 and 3730,5008 | Howling Predator, both Elysium camps |
| SWIFTWIND (garbled SWIFTWlNO) | 1672,8252 | Swift Wind, Nascence |
| LIMBER | 4760,7588 and 5280,7564 | Limber Dryad |
| TURNINNIK / ICHIRCHICH, next to THE NUMB ONE and SPIRIT TECH | about 400,13850 | Tuaninnik and Ichiachich, the Elysium mortiig camp. The Numb One and Spirit Tech Apparatus are the drop labels drawn on that camp |
| SIFNOS, with DEVOURER a few pixels away | 824,10180 and 790,10142 | Sifnos, and the Devourer of Scheol pattern on that brink |
| BELLOWING and THUNDEROUS | about 930,8620 and about 1670,9650 | the two Scheol chimera pattern names, two camps |
| CHIMERA | 4217,8866 | a chimera label |
| FRONTIER, and on one window FRONTIER OUTSKI, FERTHERS, MSLRH | frontier labels from y=3614; the outskirts window is tile column 8 row 70 | Nascence Frontier, Feathers, and a Malah name the OCR could not spell |
| STEPPE | 4416,2504 | Steppe of Despair |
| WILD | 4642,3028 | Nascence Wilds |
| SKULK | 2094,6960 | Skulking Dryad, Adonis |
| LURK | 1200,5454 | Lurking Dryad |
| GIRDER | 4986,9185 | a Scheol girder camp |
| BIGOT | 1996,7828 | an Inferno bigot |
| GOLEM | 402,11730 | the Adonis golems |
| EREMITE, MINX | several | those families, not one camp |
| ELYSIUM, SCHEOL, PENUMBRA, INFERNO, THE OUTER ISL, TEMPLE | several | region and warp labels, not bosses |

Names the OCR did not return cleanly (Papageno, Pyiininnik, Anansi, Nyame, most personal names) stay on the guide lists. They are not deleted and they are not invented onto the map.

`SLmap.xml` fills two ids the first pass left blank. Inferno, the main map, is playfield 4605, origin x=31650 z=42610, xscale 0.995, zscale 0.996. Burning Marshes stays 4005. Elysium center is 4542. The map spells the south zone Nascense.

## Adonis outdoor named bosses

Aquarius's Adonis guide is the camp list. Black Ogre's Adonis dynacamp map still returns an error, so these coordinates are Aquarius, not the Bitnykk tiles, except Skulk and Lurk above. Pattern QL is the figure Aquarius prints on the pattern, which is not the mob level. Complete AOIDs are from itemnames.sql. The drop is the pattern piece (Abhan / Bhotaar / Chi / Dom). The complete is crafted.

Piercing Toundra. Bosses drop patterns Moochy (complete 232095) and Skulky (complete 232086), plus Sanguisugent Armor. Named extras on the same rows:

| Name | x, z | Also listed |
| --- | --- | --- |
| Stealing Dryad | 920, 2440 | Bunny Boots |
| Snaking Dryad | 865, 2500 | Ring of Herodotus |
| Mooching Dryad | 985, 2575 | Chief Cook Cleaver |
| Skulking Dryad | 1120, 2635 | CrackerJack Sleeves |
| Lurking Dryad | 1230, 2800 | Jobe Explorer Personal Pistol |

Island east of that, white dryads 142-150, bosses 160. Patterns Slinky (complete 232068) and Lurky (complete 232077), Sanguisugent Armor.

| Name | Places |
| --- | --- |
| Slinking Dryad | 1675, 2695 and 1710, 2830 |
| Mooching Dryad | 1750, 2835 |
| Skulking Dryad | 1615, 2705 |
| Lurking Dryad | 1650, 2835 and 1665, 2780 |

The Platforms. Visitants and soul dredges, level 150. Patterns The Adonian Soul Dredge QL 165 (complete 232149), Viscious Visitant QL 165 (complete 232140, the item is spelled that way), Infernal Demon for the sanctuary quest (complete 232580). Also Provoked Bond / Enraged Pith / Vicious Continuity / Furious Nexus of Novictum, small pieces of metal. Visitants drop ectoplasm tattoos, spirits, and siphons. Soul dredges drop Solid Spiritech Mesh and Killer's Armor.

| Name | x, z |
| --- | --- |
| Snatching Souldredge | 2640, 2480 |
| Gashing Souldredge | 2900, 2580 |
| Scratching Souldredge | 2665, 2560 |
| Tearing Souldredge | 2765, 2640 |
| Wilted Visitant | 2890, 2500 |
| Wobbly Visitant | 2710, 2490 |
| Whispering Visitant | 2810, 2480 |
| Weary Visitant | 2770, 2590 |

Sacellum. Spirits and soul dredges, level 155. Patterns The Adonis Spirit Master QL 165 (complete 232176), The Watchdog QL 165 (complete 232158), Infernal Demon QL 160 (complete 232580). Same novictum band. Spirits drop ectoplasm tattoos. Soul dredges drop Killer's Armor.

| Name | x, z |
| --- | --- |
| Corrupt Spirit | 1940, 2240 |
| Vile Spirit | 1750, 2245 |
| Wiked Souldredge | 1930, 2210 and 2015, 2415 |
| Sinful Souldredge | 1743, 2200, and the rest of that Aquarius cell was cut off in the fetch |

The Silent Pit. Abyssal stalkers and roamers, 130-150. The guide gives the stalkers a 4% drop of the patterns and a 40% monster-part drop. Bosses Trip, Trap, Trup, Tiny, level 155-160, drop the patterns with no percent stated. Completes: The Abyssal Widow 232194, The Abysmal Lord 239496.

| Name | x, z |
| --- | --- |
| Trip | 1770, 1490 |
| Trap | 1715, 1460 |
| Trup | 1730, 1530 |
| Tiny | 1690, 1510 |

The Prominence golems, level 195. These are named outdoor bosses. Their listed drops are the unique items, not a pocket pattern.

| Name | x, z | Drop |
| --- | --- | --- |
| Cranky Golem | 1149, 870 | Cranky Golem Eye, Commander's Ceremonial Chest Plate |
| Irascible Golem | 863, 1306 | Spirit of Opportunity, Golem's Warm Notum Hood |
| Crabby Golem | 786, 1013 | The Cause of Headache, Doubleknit Pants |
| Cantankerous Golem | 784, 896 | Cantankerous's Headwear, Fingerspitzgefuhl |

Aquarius also has an "Uniques" line for The Prominence that is not pinned to one boss: patterns Toss (complete 232113) and Shake (complete 232212), Trench Soldier's Straps, Saboteur's Armor, Spirit Tech Apparatus, Nano Crystal (Boon of Ergo), sheets and bolts of perennium. That line stays unassigned.

Temple Dal names, Eradicus, and Disgusting Parasite (180 at 1792, 2785, the Ergo ring quest) are not outdoor dynas.

Retail Rubi-Ka "dyna camps" are a glowing boss plus adds. AO-Universe's dictionary says the Shadowlands name for that camp is a **unique camp**, and that **named** mobs (a real name, or a special title such as "Unhygienic Rafter") are the SL stand-in for RK dyna bosses. They drop pocket-boss pattern pieces. AO-Universe's Galahad key guide says the named boss has a 100% pattern drop and the adds around it have a smaller chance.

This pass did not find one table that covers every SL playfield the way the RK dyna-camps page covers Rubi-Ka. The lists below are the camps those sources actually name. Empty cells and missing mesh ids are gaps, not guesses filled in.

## Sources

- AO-Universe dictionary: https://www.ao-universe.com/guides/classic-ao/gameplay-guides-6/ao-dictionary (dyna = unique camp; named = SL dyna). No hidden Reveal block on that page.
- AO-Universe pocket-boss guide: https://www.ao-universe.com/guides/shadowlands/tradeskill-guides-5/general-crafting-4/pocket-boss-guide (how pieces become a boss; incarnator levels). Spoilers on that page are the crafting steps, not a camp map.
- AO-Universe Galahad garden key, Camping Inferno section, read in full: https://www.ao-universe.com/guides/shadowlands/quests-guides-2/inferno/galahad-garden-key-quest (updated 2023-04-24, Cariadast / Tepamina). This is the only AO-Universe page in this pass that lists Inferno dyna bosses with levels and coordinates.
- AOWiki Elysium and Scheol "Dyna Camps" sections: https://wiki.aodb.us/wiki/Elysium and https://wiki.aodb.us/wiki/Scheol
- AOWiki Playfields (ids): https://wiki.aodb.us/wiki/Playfield
- AOWiki Pandemonium (raid bosses, not dynas): https://wiki.aodb.us/wiki/Pandemonium
- AOWiki Dyna Camp article lists RK playfields only. It does not list SL camps. https://wiki.aodb.us/wiki/Dyna_Camp
- Aquarius area guides (Boogaloo / Polytric): http://aquarius.aodb.us/guides/nascence/index.htm (v1.2), http://aquarius.aodb.us/guides/penumbra/index.htm (v1.1), http://aquarius.aodb.us/guides/adonis/index.htm , http://aquarius.aodb.us/guides/scheol/index.htm , http://aquarius.aodb.us/guides/elysium/index.htm
- AO-Universe redeemed and unredeemed temple bosses (not dynas): https://www.ao-universe.com/guides/shadowlands/location-guides-4/redeemed-temple-bosses-6 and the unredeemed twin.
- Item ids are the Auno AOIDs, taken from `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/itemnames.sql`. Auno.org itself was not readable in this pass. Where a name has many quality rows, the id cited is the low end of that family.

Pattern drops are the four pieces (Abhan, Bhotaar, Chi, Dom). The "Complete Blueprint Pattern" is crafted from those pieces. The complete id is listed so the boss is identifiable. The Abhan piece id is the actual drop row when it was looked up.

Mesh / model ids are not in any of these guides. `model` below is the mob family the guide names. `meshId` is unknown.

Retail respawn, where a guide gives one, is 20 minutes (1200 s) for the Elysium and Scheol dyna lists, and 25 minutes for the Scheol Greek-name brink bosses. Aquarius does not give respawn times. That is not our 180 s house rule.

## Playfields

| Area | Playfield | Id | Source |
| --- | --- | --- | --- |
| Nascence | Frontier | 4310 | AOWiki Playfields |
| Nascence | Wilds | 4311 | AOWiki Playfields |
| Nascence | Swamp | 4312 | AOWiki Playfields |
| Elysium | South | 4540 | AOWiki Playfields, and waypoints on the Elysium dyna list |
| Elysium | West | 4541 | same |
| Elysium | East | 4543 | same |
| Elysium | North | 4544 | same |
| Scheol | Upper | 4880 | AOWiki Playfields |
| Scheol | Lower | 4881 | AOWiki Playfields |
| Adonis | City | 4872 | AOWiki Playfields |
| Adonis | Abyss | 4873 | AOWiki Playfields |
| Penumbra | Forest | 4320 | AOWiki Playfields |
| Penumbra | Valley | 4321 | AOWiki Playfields |
| Penumbra | Hollow | 4322 | AOWiki Playfields |
| Inferno | Burning Marshes | 4005 | AOWiki Playfields and SLmap.xml |
| Inferno | Inferno | 4605 | SLmap.xml, origin 31650, 42610, scales 0.995 and 0.996 |
| Elysium | center | 4542 | SLmap.xml |
| Inferno | Albtraum | unnamed on that page | pocket bosses inside the instance, not outdoor dynas |
| Pandemonium | Caina, Antenora, Ptolemea, Judecca | 4328, 4329, 4330, 4331 | AOWiki Playfields |

Which Nascence section sits on 4310 / 4311 / 4312 is taken from Aquarius headings (Frontier, Wilds, Swamp). That mapping is flagged in the JSON where the heading is not the wiki name.

## Nascence

Aquarius v1.2. Outdoor named bosses. Training-grounds bosses are included and flagged: AOWiki says that starter area is closed and Arete replaced it. Dungeon bosses (Havaris, floor bosses) are not dynas and are left out.

Hecklers of Metals / Stones / Earth are level 80 everywhere Aquarius mentions them. It does not give them camp coordinates. Lord of the Void is "level 250+", one of the highest mobs in the game, walking the Wilds. Ran-Roth-Or is level 110 at 480, 1822 and also walks. Hwall is level 110 at 833, 1832 and also walks. Those three are uniques, not a normal dyna camp.

Training grounds (retired), bosses that "can drop symbiants":

| Name | Level | x, z | Family |
| --- | --- | --- | --- |
| Shadowleet of Powah | 3 | 316, 422 | leet |
| Leader of the Pack | 5 | 364, 246 | silvertail, guessed from the camp text |
| The Predator Rager | 6 | 362, 286 | predator |
| Hardened Mortiig | 8 | 526, 212 | mortiig |
| Dryad Watcher | 8 | 380, 459 | dryad |
| The Devastator | 14 | 547, 243 | not stated |

Frontier and swamp outdoor bosses (Aquarius section, then name, level, x, z, drop):

- Frontier bridge: Swift Wind, 10, silvertail, 793, 1271. Symbiant, Outsider's Armor (MA, Shade).
- Papageno's Paradise: Starry Feathers, 15, 672, 1379 and 637, 1293. Papageno, 15, walks. Insignias, SL weapons.
- Papagena's Paradise: Rainbow Feathers, 15, 889, 1250. Papagena, 15, 956, 1267.
- Old Frontier spirits: The Lord, 20, 845, 1402. The Lady, 15, 819, 1391. Ectoplasm tattoos, symbiant, Disturbed Sequence of Novictum. Nascence Spirit Hunters, 12, no coordinate.
- Frontier Cauldron: Deadly Predator, 20, 738, 1587. Pattern Sabretooth Slicer, complete AOID 239550. Demonic Subjugator, 20, imp, 732, 1563. Symbiant, sombreous tattoo.
- Frontier Swamp: Malah-Aya, 20, 977, 1598. Pattern Maledicta, complete AOID 239577.
- Frontier Outskirts: Hesosas, 18, 710, 1963. Omathon, 15, 803, 1748. Deadly Predator, 20, at 755, 1876 and 820, 1926. Hiathlin bosses drop Killer's Armor, sombreous tattoos, Die Kleine Nadel (AOID 226474, many QL rows through 226485), spirit baubles. Predator strikers drop the Sabretooth Slicer pattern.
- Nascence Swamp: The Mountain Claw, 35, 1231, 1685. Ectoplasm tattoos, symbiant.
- Wetlands Malah-Ana bosses: Malah-Animos 26 at 1557, 1755; Malah-Aerarium 27 at 1621, 1676; Malah-Audisse 33 at 1721, 1621; Malah-Annos 40 at 1702, 1576; Malah-Animi 32 at 1590, 1523; Malah-Alter 29 at 1507, 1610; Malah-Adsunt 35 at 1702, 1576. Adsunt and Annos share 1702, 1576 in the guide. That collision is unfixed. Drops: Sanguisugent Armor, sombreous tattoos, canister of liquid notum, symbiant, SL weapons and nanos.
- Wetlands spinetooth bosses, pattern Wala (complete AOID 239586), Pernicious Armor: Sashuqa 30 at 1446, 1466; Saliwata 25 at 1492, 1466; Narunkt 32 at 1527, 1579; Kumpari 28 at 1550, 1539; Hawilli 30 at 1663, 1587; Aquqa 34 at 1618, 1603; Waychaw 29 at 1595, 1634; Hatskiri 40 at 1736, 1661; Qallyawi 32 at 1689, 1512.
- The Core Malah-Ana: Malah-Adsunt 35 at 1462, 1223 and 1538, 1393; Malah-Alinos 38 at 1606, 1245; Malah-Appi 37 at 1723, 960; Malah-Annos 40 at 1723, 829 and 1577, 1024. Maynatsk, 37, spinetooth, 1767, 1135. Direful / Morbid Weavers, level 40, pattern Careening Death (239568) and Careening Blight (239559), entophagus tattoos. Coords: Direful 1509, 953; 1650, 987; 1030, 1028; 1795, 1056. Morbid 1804, 865.
- Wilds: Stunter of Growth, 35, crippler, 700, 1404. Exploiter's Armor, pattern Canceroid Cupid (239541). Croakers of Night, level 40, no pattern name filled in by the guide: Ahwere 673, 1193; Heru-Maat 520, 1458; Ehmat 530, 1492.
- Steppe of Despair: Tormentor of Growth 28 at 161, 1783; Blighter of Growth 28 at 154, 1589. Pattern Canceroid Cupid. Hateful Weaver 35 at 211, 1263; Vile Weaver 35 at 221, 1166. Patterns Careening Death and Careening Blight.
- Two Mountains croakers, pattern Tcheser and Upenpet (complete ids are in itemnames; not copied here because the guide names them only as "Tcheser, Upenpet"): Mesut-Ra 38 at 220, 577; Remur-Nefer 38 at 251, 561; Nesbaneb 40 at 143, 663; Takheperu 35 at 176, 748. Exploiter's Armor, Venomous Coolie, symbiant.

Quagmire Cloak of Nascence (AOID 224437) is a general Nascence drop in the Aquarius header, not pinned to one boss.

## Elysium

AOWiki says 18 dyna camps, boss levels 75 to 115, respawn 20 minutes. Counting each coordinate as a camp lands on 18. Three coordinates are printed on more than one camp (930, 1480 and 1164, 1567). Those rows are flagged. Compass labels are the wiki's (SW, NO, W, E, N, S). Where the wiki also gives a `/waypoint`, the third number is the playfield id.

| Names | Family | Level | Places | Drops |
| --- | --- | --- | --- | --- |
| Wounded / Limpig / Howling / Pained Predator | Predator | 75 | 745, 953 SW; 861, 1919 NO | Predator Armor; Outsider armor parts; Notum Saturated Animal Bone AOID 226329 (also 226330). Patterns Black Fang complete 231420, Abhan piece 242675; Silver Fang complete 231402 |
| Ossuz / Zias / Tdecin / Athlar | Hithalin | 85 | 966, 777 on 4541; 490, 1510 on 4543 | Killer's armor; Spirit Ring of Self-Education AOID 246310; a spirit training program the wiki calls "(love)"; sombreous tattoos. Pattern Ungulera complete 231483 |
| Ichiachich / Tuaninnik / Suininnik / Aniuchach / Inicha | Mortiig | 115 | 556, 640 SW; 855, 2448 NO | Spirit Tech Apparatus (perennium weapons); Mortiig Blaster / Beamer / Beater (quality families, blaster ids 246334-246344, beamer 246323-246333, beater of the same shape); Trench Soldier's Straps. Patterns The Numb One complete 231798, Abhan 242717; The Enrapt One complete 231807 |
| Asperous / Disturbing / Screeching / Cacophonous Imp | Croaker | 115 | 634, 1383 on 4541; 1164, 1567 on 4544 | Elysian Protector's Cuirass AOID 246309; Killer's armor; Focus-Funneling Spirit. Patterns The Archbile Queen complete 231780; The Achbile Guardian complete 239695. The wiki spells Achbile that way |
| Swirling / Bending / Coiling Eremite | Eremite | 85 | 1931, 1070 S | Eremite Psychic / Macro / Micro Sensor. Patterns The Talus Suzerain complete 231474; The Dune Suzerain complete 231465 |
| Waning / Comatosed Lost Soul | not stated | 80 | 1434, 939 on 4540; 656, 884 on 4543 | Ectoplasm tattoos; Problem Seeking / Solving / Avoiding Device. Pattern The Elysian Soul Dredge complete 231492. Respawn line is blank in the wiki |
| Limber / Brutish Dryad | Dryad | 75 | 1226, 1302 SW; 696, 914 SW | Bootlegger armor; Pelisse of the Elysian Nymph AOID 246287; Finely Refined Notum (tier 3 armor) |
| Catervauling / Howling Minx | not stated | 115 | 930, 1480 W; 1164, 1567 N | Nano Crystal (Bane of the Challenger) AOID 230417; Shuffling Finger AOID 246289. Same two queen/guardian patterns as the croaker camp. 1164, 1567 is also the croaker's north point |
| Sadistic Soul Dredge | Spirit Hunter | 85 | 701, 940 on 4543; 1548, 1008 on 4540 | Pattern Brutal Soul Dredge complete 231456. Other drops blank |
| (no personal names) | Unredeemed Kuir | 70 | 930, 1480 W | Ranged Weapons Guide's (+2). Pattern The Numb One. 930, 1480 is also the minx's west point |

Aquarius's Elysium page adds faction and quest NPCs (Enel temple, garden key). Those are not this dyna list.

## Scheol

AOWiki dyna section. Respawn 20 minutes unless noted. Levels are the boss level the wiki prints. Several drop lines are "???" in the wiki and stay blank here. "Minuten" in the wiki is German for minutes; the figures are still 20 and 25.

Upper Scheol (playfield 4880 is the upper map; the area names are the wiki's):

| Names | Family | Level | Places | Drops |
| --- | --- | --- | --- | --- |
| Laki / Keppur / Vinstur | Beit | 120-130 | 1077, 803 Wind Top; 1058, 410 and 1130, 618 The Highlands | Outsider armor; Notum Saturated Animal Bone. Pattern blank |
| Boggy / Dirty / Sloughy / Quaggy Girder | Girder | 120-130 | 1395, 335 Temple Bog; 1521, 164 three times | Nonpartisan armor. Patterns Gunk complete 231708; Ooze complete 231753 |
| Bending / Coiling / Crinkling / Swirling / Twirling Eremite | Eremite | 130 | thirteen Western Brink / Highlands points from 985, 993 down to 467, 653 | Smuggler's armor; Unpolluted Crystal. Suzerain completes: Adobe 231528, Argil 231735, Kaoline 231771, Loessial 231744, Marly 231672, Wacky 231699 |
| Beefy / Gross / Heavy / Obese Spider | Spider | 130 | 1339, 1018; 1377, 1077; 1386, 1030; 1397, 1055 Crystal Forest | Entophagus tattoos; Zealot's armor. Pattern Borer complete 231564 |
| Nebamun / Webensenu / Nabur / Sa-en-ank / Thapt / Kheferu | Croaker | 120 | Facet Canyon 1744, 935; 1813, 1037; 1866, 933; 1930, 997; 2007, 1080; Croaker's Pass 1768, 1038 | Exploiter's armor. Patterns Hemut complete 231636; Khemhet complete 231645 |
| Barking / Growling / Snarling / Yapping Chimera | Chimera | 130 | Eastern Passageway 1306, 1580; 1457, 1663; 1439, 1813; 1443, 1933 | Outsider and Pernicious armor. Patterns Bellowing Chimera complete 231726; Thunderous Chimera complete 231717 |
| Harijay Durjand / Gir Bhavina / Madanda Vishudr / Saunik Ray | Rash | 130 | The Plunge 1097, 1750; 1002, 1750; 1001, 1848; 1097, 1847 | Sanguisugent armor; Ravin's Multi-Gyro Component AOID 234885 (Adonis access ring). Pattern Ashmara Ravin complete 231600 |

Lower Scheol:

| Names | Family | Level | Places | Drops |
| --- | --- | --- | --- | --- |
| Haxxor / Roxxor | Leet | 130 | 790, 1173 and 792, 1174 Clutching Light | drop line blank |
| Beefy / Gross / Heavy / Obese Spider | Spider | 130 | eight Twilight Basin points from 291, 891 to 271, 1198 | Entophagus tattoos; Zealot's armor. Patterns Auger complete 231546; Awl complete 231555 |
| Viram Ravata / Yotidar Baliddh / Nab Vangana / Pusu Omankan | Rash | 130 | Through Earth and Stone 627, 1422; 722, 1421; 721, 1324; 624, 1324 | Pattern Bhinaji Navi complete 231663. Other drops blank |
| Benacen / Asatix / Atezac / Coah / Caal / Snaamehiel | Hiathlin | 130 | six Halls of Scheol points from 1337, 1181 to 1427, 1331 | drop line "???". Pattern Loltonunon complete 239667 |
| Shadowy Bravado / Illusion / Deception | Shadow | 130 | 1009, 1231 and 1041, 1228 Halls; 1190, 1498 Beneath the Beast | Sombreous tattoos; Sanguisugent armor; Antigravitational Metal Sphere AOID 234887 (Adonis access ring). Patterns Ignis Fatui complete 231519; Ignis Fatuus complete 231582 |
| Crocked Casuist / Demonic Apparition / Demonic Mirage | Demon | 130 | 1031, 1279 Halls; 1224, 1497; 1212, 1524; 1247, 1527 Beneath the Beast | Sombreous tattoos; Killer's armor; same metal sphere. Same two Ignis patterns |
| Neferu / Tcheser / Shet / Penkha / Sebtitis / Ptahmose | Croaker | 130 | The Void 1658, 1216; 1668, 1251; 1700, 1246; 1722, 1198; 1804, 1306; 1819, 1383 | Exploiter's armor; Venomous Coolie; Unpolluted Crystal. Patterns Iunmin complete 231537; Marem complete 231591 |
| Rigid / Rugged / Solid / Robust Girder | Girder | 120-130 | The Approach 1350, 1482; 1451, 1460; 1468, 1383; Necropolis 1334, 1674 | Nonpartisan armor. Pattern Tough complete 231681 |
| Oppressing / Harrowing / Dismal / Jejune / Prosy / Pleasant / Gentle / Cordial Soul Dredge | Soul Dredge | 130 | eight Necropolis points; 1310, 1628 is printed twice | Sombreous tattoos; Killer's armor; tier-1 novictum; soul globe. Patterns Contorted 231654, Gracious 231501, Spiritless 231510, The Scheolian Soul Dredge 231762, Beatific Spirit 231573, Lethargic Spirit 231609 |
| Mirthful / Euphoric / Jovial / Plagued / Blah / Tormented / Dolored / Tortured / Gloomy / Listless Spirit | not stated, same drop block as the soul dredges | 130 | The Adieu and 1329, 1586 The Approach; 1461, 1661 Necropolis | Ectoplasm tattoos; spirits; the same soul-dredge patterns |
| Noxious / Mephitic Minx | not stated | 130 | 1466, 1799 and 1447, 1799 Necropolis | Thar's armor. Patterns Blight complete 231627; Misery complete 231618 |
| Rancid / Festering / Pustular / Purulent Imp | Demon | 130 | The Adieu 1506, 1815; 1506, 1833; 1518, 1842; 1488, 1841 | Sombreous tattoos; tier-1 novictum. Same Blight and Misery patterns |
| Naxos / Lesvos / Kalymnos | not stated on the first line; later Demon and Hydra rows reuse the names | 200 / 210 | The Void 1854, 1536; 1828, 1578; 1736, 1680 | Spirit Tech Apparatus, perennium sheet and bolts, Saboteur's armor, unpolluted crystal and SL gems up to QL 280, Trench Soldier's Straps. Patterns Defiler 231825, Destroyer 231843, Devastator 231834, Devourer 231816. Respawn 25 minutes |
| Sifnos / Poros | Horror | 200 / 220 | 1790, 1558 The Void; 1556, 1772 The Adieu | drop line blank |
| (one demon, name not on its own line) | Demon | 210 | 1682, 1711 The Void | same perennium set. Respawn 25 minutes |
| Kefallonia | Hydra | 220 | 1755, 1850 The Void | same perennium set. Same four Scheol destroyer patterns. Respawn 25 minutes |

Aquarius Scheol v3.1 says the same Greek brink names (Sifnos, Naxos, Kalymnos) and agrees they drop Destroyer of Scheol at pattern level 220. Its coordinates are rounded (Sifnos 1800, 1565; Naxos 1850, 1540; Kalymnos 1730, 1685) and do not match the wiki's figures. Both are recorded. Neither is "corrected" here.

## Adonis

The outdoor named bosses, coordinates, pattern QLs, and unique drops are in the section above. AOWiki's Adonis page still has no dyna list. Black Ogre's image map (http://www.black-ogre.com/wc/maps/adonis.html) still returns an error. Sinful Souldredge's extra coordinates were cut off in the Aquarius fetch and are not guessed.

## Penumbra

Aquarius v1.1. Outdoor bosses. Levels are the boss band the guide prints. Respawn is not given. Playfield is the section: Valley 4321, Hollow 4322, Forest 4320, with the east plateau left on Valley because that is where the guide starts before it says "Penumbra Valley". That east-plateau id is a guess.

| Area | Names | Level | x, z | Drops |
| --- | --- | --- | --- | --- |
| Frozen Plateau | Ipakut, Akapan, Ikaria, Zakynthos | 190, Zakynthos 208 | Ipakut 2255, 400. The other three have no coordinate | Patterns Quondam complete 231879; Morrow complete 231888 |
| Valley, extreme south | Slender Rafter, Slight Rafter, Meager Stalker, Bony Stalker | 170-180 | 2200, 650; 2340, 700; 2085, 825; 2270, 790 | Pattern Sampsa complete 231852; Outsider armor; Notum Saturated Animal Bone |
| Valley, incarnator | Skyros, Folegandros | 200 | no coordinate | Pattern Hadur complete 231996 |
| Valley, north of incarnator | Vadleany, Abnoba, Rugged Rafter, Nimble Stalker, Lithe Stalker, Boorish Rafter | 170-180 | 2425, 1600; 2605, 1686; 2440, 1720; 2460, 1360; 2527, 1506; 2376, 1326 | Patterns Bagaspati complete 231915; Brutal Rafter complete 231906 |
| Ruins | Skedadding Spirit, Vamoosing Spirit | 172 and 170 | 2345, 1937; 2282, 1917 | Pattern The Penumbral Spirit Hunter complete 231897 |
| Spirit area | Maywaqaham, Waqamawi, Malah-Eadem | 175, 175, 173 | 2510, 2270; 2506, 2167; Malah-Eadem no coordinate | Pattern Anya complete 231861 |
| Hollow, central Hiisi | Ilmatar, Turris, Sampasa, Pajainen, Kul-Uasa, Egres, Rauni, Nyrckes | 170-174 | 1873, 1070; 1825, 1070; 1853, 1195; 1850, 950; 1850, 860; 1880, 830; 1850, 800; 1820, 830 | Patterns Kaleva complete 232023; Juma complete 232014. Die Kleine Nadel. Sanguisugent armor |
| Hollow, north dryads | Cool, Crisp | 171-175, Crisp stated 175 | 1850, 1260; 1850, 1290 | Pattern The Dryad Shuffle complete 231942 |
| Hollow, south | Chilly Gust, Esanell | 170-175 | 1775, 605; 1835, 570 | Pattern Srahir complete 231951; Killer's armor |
| Forest, white dryads | A Dauntless Dryad, A Fearless Dryad, A Daring Dryad, A Resolute Dryad | 170-175 | 1555, 916; 1490, 808; Daring has no coordinate; Resolute 1626, 325 | Patterns The Stupendous Breeder complete 239631; The Brobdingnagian Mother complete 239622 |
| Forest south | Pudgy Stalker, Hefty Rafter, Stubby Stalker, Riven Rafter, Sleet, Floe, Snappy Rafter | 170-175 | 1743, 260; 1367, 283; 1170, 240; 1207, 732; 1326, 575; 1228, 584; 1257, 545 | Outsider or Nonpartisan armor depending on the row. Pattern Glitter complete 239649 on Riven, Sleet, Floe, Snappy |
| Forest eremites | Flegmatic Eremite, Imperturbable Eremite | 170-175 | 1220, 880; 1337, 875 | Pattern The Maggot Lord complete 231870, guide says pattern QL 205 |
| Brink west | Alonissos, Lefkodo | 200 | no coordinate | not stated |
| Slither area | Maar, Wing | not stated | no coordinate | not stated. Nearby slithers drop molock parts, not these two |
| West demons / hiisi | Shadowmane, Breaker Reijo | 172-175 | 820, 1297; 664, 1313 | Pattern Breaker Teuvo complete 231933, guide says QL 178 |
| West dryads | The Hollow's Ranger, The Hollow's Warden | 185 and 182 | 1495, 1710; 1512, 1425 | same breeder / mother patterns, plus a grey glyph |
| Extreme north | Meager Spirit, Exiguous Spirit | 170-175 | 1434, 2571; 1465, 2555 | Pattern Eidolean Soul Dredge complete 239640, guide says QL 180 |
| The Pipe | Malah-Enimvero, Malah-Enim, Kumparink, Wataptu | 172-175 | 1544, 1122; 1529, 1187; 1530, 1263; 1430, 1292 | Pattern Imk. Pernicious armor on the spinetooth. Imk's complete id was not pulled |

Temple named mobs (Ecclesiast, Patriarch, Omega, Unredeemed Vanya) are the insignia chain, not dynas. See the AO-Universe temple guides. Rancid is the static-dungeon boss (175 to 215 depending on the door) and is not an outdoor dyna.

## Inferno

AO-Universe Galahad garden key, Camping Inferno. The guide calls these dyna bosses. Each named boss drops pieces for the pocket boss named in the heading, at 100%, and the adds drop the same pieces less often. Coordinates are x, z on Inferno playfield 4605.

| Pocket boss (crafted) | Complete AOID | Dyna bosses |
| --- | --- | --- |
| The Indomitable Chimera | 234278 | Chimera Monitor 215 at 1375, 560. Chimera Trainer 210 at 1430, 620. Chimera Drivers, no coordinate |
| Ahpta | 234168 | Atarkirh 215 at 1950, 2450. Salahpt 215 at 2150, 2650. Family: dragon. Adds: spinetooths |
| Relief Teals | 234123, Abhan piece 242766 | Pyiininnik's Shadow 215 at 3250, 1690. Pyiininnik 250 at 3320, 1690. Adds: wicked shadows, mortiigs |
| Taille Frees | 234195 | Aniitap's Shadow 220 at 2420, 3230. Aniitap 250 at 2470, 3360 |
| Lya | 234222 | Afreet Ellis 230 at 2570, 2940. Maychwyawi 220 at 2630, 2920. Medusa Philanderer 250 at 2570, 2930. Steele Filar 230 at 2740, 2930. Adds: vortexoid, spinetooth, demon |
| Fester Leila | 234150 | Beast 215 at 3490, 2000. Distracted Snake Tamer 215 at 3600, 1910. Estella Fire 215 at 3610, 2090. Adds: drayds, lifebleeders |
| Pazuzu | 234249 | Limnos, Syros, Crete, all 250, at 3110, 2980; 3200, 2970; 3130, 3010. Adds: hecklers |
| Nyame | 239487 | Anansi Left Hand 250 at 1920, 1750. Anansi Right Hand 250 at 1900, 1600. Nyame's Drudge 250 at 1860, 1770. Adds: anansis, molokhs. Guide says these were boosted in patch 16.3 |
| Arch Bigot Aliel | 234325 | Bigot Nzaemihiel 250 at 745, 1180. Bigot Inmodeah 250 at 700, 1130. Devoted Spirit Hunter 215 at 650, 1300. Pursued Spirit 215 at 710, 1100. These also drop Captured Spirit pieces. Adds: spirit, soul dredge |

The Mordeth garden-key page names more Inferno dynas (spirits in the Yuttos Marshes, Maychwaham, Ushamaham, bigots around 3232, 3225). That page's tables did not come through cleanly enough to copy coordinates without inventing them. They are a gap, not a second list.

Albtraum's five Xan Essence crystals (Divided Loyalty, Gruesome Misery, Sister Merciless, Sister Pestilence, Summoned Terror) drop inside the instance, especially on mini-bosses and the keeper. They are not outdoor dynas.

## Pandemonium

Not dynas. AOWiki: 18 raid bosses.

- Portal bosses, no loot, respawn 30 minutes, in order: Zodiac, Timid Spectral Scourge, Senior Subjugator, Pandemonium Subjugator.
- Twelve zodiac bosses, one of three at each of four places, respawn 6 hours. West: Aries, Leo, Virgo. East: Aquarius, Cancer, Gemini. Middle: Libra, Pisces, Taurus. North: Capricorn, Sagittarius, Scorpio. Coordinates are not on the page.
- The Night Heart, respawn 6 hours. Belts and high symbiants. No item ids on the page.
- The Beast. Respawn was 18 hours, then 9 hours after the 17.2 Lost Eden patch. Unique and nodrop loot. No item ids on the page.

Playfield ids: Caina 4328, Antenora 4329, Ptolemea 4330, Judecca 4331.

## Left out on purpose

- Redeemed and unredeemed temple chains (insignia trade, then Ecclesiast / Patriarch, then the sided boss). AO-Universe has those two guides. They are not unique camps.
- Static dungeon bosses (Havaris, Rancid, Scorchid, floor bosses).
- Pocket bosses themselves. They are crafted and spawned at an incarnator. Incarnator levels from AO-Universe: Nascence 75, Elysium South 115, Elysium North 130, Scheol South 140, Scheol North 160, Adonis East 170, Adonis West 190, Penumbra West 200, Penumbra East 215, Inferno South 225, Inferno North 255, Pandemonium 270.

## Models

Mesh id is stat 359 on a captured NpcTemplates donor. A stand-in is flagged: the guide's creature was not in the catalog, so the camp uses another family's mesh.

| Family | Mesh | Donor | Note |
| --- | --- | --- | --- |
| Silvertail | 208922 | SWSI | |
| Predator | 209022 | PDST | Mountain Claw uses this mesh. That family guess is flagged. |
| Malah | 209229 | ALAH | |
| Weaver | 209354 | VIWE | Hwall (spider) uses this mesh. Stand-in, same donor the Rubi-Ka spider camps use. |
| Crippler | 209333 | CIOG | |
| Hiathlin | 209196 | ATHL | |
| Croaker | 209319 | CEON | Same mesh as Mesut-Ra (ESUT). |
| Spinetooth | 226557 | SIHA | |
| Spirit | 209215 | NASI | Soul Dredge (SUDR) is the same mesh. The Lord and The Lady use it. Family is a guess. |
| Imp | 40515 | IMDR | |
| Leet | 17655 | LE04 | |
| Chimera | 209173 | LLBE | |
| Heckler | 214982 | MOOM | |
| Dryad | 209229 | PODR | Stand-in. This is the Malah mesh. |
| Mortiig | 209196 | PIMR | Stand-in. This is the Hiathlin mesh. |
| Feathers / Papagena | 208922 | SWSI | Stand-in. No feathers mesh was captured. |

Eremite, girder, beit, rash, demon, shadow, hydra, minx, rafter, stalker, hiisi, visitant, golem, dragon, and the Inferno uniques still have no captured mesh. Those zones are not wired.

## Pattern pieces

The drop is the Abhan, Bhotaar, Chi, or Dom piece. The complete blueprint is crafted. Piece QL is the items.dat quality on the Abhan row; Bhotaar, Chi, and Dom of the same pattern share it (checked on the Nascence set, Black Fang, Nyame, and Imk). AOIDs are from itemnames.sql. Auno was not opened.

Imk's complete is 232005, piece QL 182. The earlier gap on that id is closed.

## Guesses and gaps

- The Bitnykk layer-2 labels are pixels. OCR confirmed the names in the map table. It did not produce usable coordinate numerals, and the pixel-to-playfield fit was not stable, so camp x,z stay the guide figures.
- Nascence section-to-playfield is the Aquarius heading, except Malah-Aya: the captured QEGO spawn is on 4310 at 976.4, 31.6, 1599.9, and the training-ground bosses already placed are on 4313. Adsunt was shifted +8 X off 1702, 1576 because Annos is printed on the same point.
- A fixed-QL piece only covers a roll at or above its QL. Where the piece sits above boss level x 1.25, that camp's LevelMod is widened so the top of the window reaches the piece (and the lowest instruction disc in its band). The Empty Data Receptacle (275056, QL 1) is in every Nascence pool so the 5-7 count still resolves on the low rolls. The 1-15 band has no instruction disc at all. The 16-35 band's discs are QL 40 and 43.
- Armor-family lines (Outsider, Sanguisugent, Killer, Pernicious, Exploiter) are not pinned to one AOID. They are not invented onto a boss.
- Elysium coordinate collisions at 930, 1480 and 1164, 1567 are in the wiki. Not resolved.
- Elysium lost-soul respawn is blank. Minx and kuir families are blank.
- Scheol Haxxor drops, Hiathlin drops, Sifnos drops, and several "???" lines are blank in the source.
- Scheol Greek-boss coordinates disagree between AOWiki and Aquarius.
- Nascence playfield id for a section is the Aquarius heading mapped onto the three wiki ids. Training-grounds id is unknown.
- Nascence Adsunt and Annos share 1702, 1576 in Aquarius.
- Adonis Sinful Souldredge is missing the extra coordinates the Aquarius cell cut off. The Prominence "Uniques" loot line is not pinned to one boss.
- Chimera Drivers have no coordinate. The Mordeth-side Inferno dyna list still did not survive a clean fetch. Inferno's playfield id is 4605 from SLmap.xml.
- Penumbra Frozen Plateau bosses other than Ipakut, Skyros, Folegandros, Alonissos, Lefkodo, Maar, Wing, and A Daring Dryad have no coordinate. Imk's complete AOID was not pulled.
- Auno pages were not opened. Ids are from itemnames.sql, which is the same AOID space.
- No source in this pass said SL dynas drop 5 to 7 items. That RK house rule is not applied here.

## Pattern piece AOIDs

Piece QL is items.dat. The four piece ids and the crafted complete are itemnames.sql.

| Pattern | Piece QL | Abhan | Bhotaar | Chi | Dom | Complete |
| --- | --- | --- | --- | --- | --- | --- |
| Adobe Suzerain | 125 | 242687 | 231523 | 231524 | 231525 | 231528 |
| Ahpta | 220 | 242771 | 234163 | 234164 | 234165 | 234168 |
| Anya | 185 | 242724 | 231856 | 231857 | 231858 | 231861 |
| Arch Bigot Aliel | 255 | 242788 | 234320 | 234321 | 234322 | 234325 |
| Argil Suzerain | 125 | 242710 | 231730 | 231731 | 231732 | 231735 |
| Ashmara Ravin | 125 | 242695 | 231595 | 231596 | 231597 | 231600 |
| Auger | 125 | 242689 | 231541 | 231542 | 231543 | 231546 |
| Awl | 125 | 242690 | 231550 | 231551 | 231552 | 231555 |
| Bagaspati | 185 | 242730 | 231910 | 231911 | 231912 | 231915 |
| Beatific Spirit | 125 | 242692 | 231568 | 231569 | 231570 | 231573 |
| Bellowing Chimera | 125 | 242709 | 231721 | 231722 | 231723 | 231726 |
| Bhinaji Navi | 125 | 242702 | 231658 | 231659 | 231660 | 231663 |
| Black Fang | 80 | 242675 | 231415 | 231416 | 231417 | 231420 |
| Blight | 125 | 242698 | 231622 | 231623 | 231624 | 231627 |
| Borer | 125 | 242691 | 231559 | 231560 | 231561 | 231564 |
| Breaker Teuvo | 178 | 242731 | 231928 | 231929 | 231930 | 231933 |
| Brutal Rafter | 175 | 242729 | 231901 | 231902 | 231903 | 231906 |
| Brutal Soul Dredge | 85 | 242679 | 231451 | 231452 | 231453 | 231456 |
| Canceroid Cupid | 40 | 242801 | 239536 | 239537 | 239538 | 239541 |
| Careening Blight | 45 | 242803 | 239554 | 239555 | 239556 | 239559 |
| Careening Death | 45 | 242804 | 239563 | 239564 | 239565 | 239568 |
| Contorted Soul Dredge | 125 | 242701 | 231649 | 231650 | 231651 | 231654 |
| Defiler of Scheol | 220 | 242720 | 231820 | 231821 | 231822 | 231825 |
| Destroyer of Scheol | 220 | 242722 | 231838 | 231839 | 231840 | 231843 |
| Devastator of Scheol | 220 | 242721 | 231829 | 231830 | 231831 | 231834 |
| Devourer of Scheol | 205 | 242719 | 231811 | 231812 | 231813 | 231816 |
| Eidolean Soul Dredge | 180 | 242812 | 239635 | 239636 | 239637 | 239640 |
| Fester Leila | 220 | 242769 | 234145 | 234146 | 234147 | 234150 |
| Glitter | 188 | 242813 | 239644 | 239645 | 239646 | 239649 |
| Gracious Soul Dredge | 115 | 242684 | 231496 | 231497 | 231498 | 231501 |
| Gunk | 125 | 242707 | 231703 | 231704 | 231705 | 231708 |
| Hadur | 210 | 242738 | 231991 | 231992 | 231993 | 231996 |
| Hemut | 125 | 242699 | 231631 | 231632 | 231633 | 231636 |
| Ignis Fatui | 125 | 242686 | 231514 | 231515 | 231516 | 231519 |
| Ignis Fatuus | 125 | 242693 | 231577 | 231578 | 231579 | 231582 |
| Imk | 182 | 242739 | 232000 | 232001 | 232002 | 232005 |
| Infernal Demon | 160 | 242665 | 232575 | 232576 | 232577 | 232580 |
| Iunmin | 125 | 242688 | 231532 | 231533 | 231534 | 231537 |
| Juma | 182 | 242740 | 232009 | 232010 | 232011 | 232014 |
| Kaleva | 182 | 242741 | 232018 | 232019 | 232020 | 232023 |
| Kaoline Suzerain | 125 | 242714 | 231766 | 231767 | 231768 | 231771 |
| Khemhet | 125 | 242700 | 231640 | 231641 | 231642 | 231645 |
| Lethargic Spirit | 125 | 242696 | 231604 | 231605 | 231606 | 231609 |
| Loessial Suzerain | 125 | 242711 | 231739 | 231740 | 231741 | 231744 |
| Loltonunon | 125 | 242815 | 239662 | 239663 | 239664 | 239667 |
| Lurky | 165 | 242747 | 232072 | 232073 | 232074 | 232077 |
| Lya | 220 | 242777 | 234217 | 234218 | 234219 | 234222 |
| Maledicta | 25 | 242805 | 239572 | 239573 | 239574 | 239577 |
| Marem | 125 | 242694 | 231586 | 231587 | 231588 | 231591 |
| Marly Suzerain | 125 | 242703 | 231667 | 231668 | 231669 | 231672 |
| Misery | 125 | 242697 | 231613 | 231614 | 231615 | 231618 |
| Moochy | 165 | 242749 | 232090 | 232091 | 232092 | 232095 |
| Morrow | 215 | 242727 | 231883 | 231884 | 231885 | 231888 |
| Nyame | 255 | 242795 | 239482 | 239483 | 239484 | 239487 |
| Ooze | 125 | 242712 | 231748 | 231749 | 231750 | 231753 |
| Pazuzu | 220 | 242780 | 234244 | 234245 | 234246 | 234249 |
| Quake | 195 | 242752 | 232117 | 232118 | 232119 | 232122 |
| Quondam | 215 | 242726 | 231874 | 231875 | 231876 | 231879 |
| Relief Teals | 220 | 242766 | 234118 | 234119 | 234120 | 234123 |
| Sabretooth Slicer | 35 | 242802 | 239545 | 239546 | 239547 | 239550 |
| Sampsa | 185 | 242723 | 231847 | 231848 | 231849 | 231852 |
| Shake | 192 | 242762 | 232207 | 232208 | 232209 | 232212 |
| Shiver | 192 | 242761 | 232198 | 232199 | 232200 | 232203 |
| Silver Fang | 80 | 242673 | 231397 | 231398 | 231399 | 231402 |
| Skulky | 165 | 242748 | 232081 | 232082 | 232083 | 232086 |
| Slinky | 165 | 242746 | 232063 | 232064 | 232065 | 232068 |
| Spiritless Soul Dredge | 115 | 242685 | 231505 | 231506 | 231507 | 231510 |
| Srahir | 180 | 242733 | 231946 | 231947 | 231948 | 231951 |
| Taille Frees | 220 | 242774 | 234190 | 234191 | 234192 | 234195 |
| Tcheser | 48 | 242819 | 239699 | 239700 | 239701 | 239704 |
| The Abysmal Lord | 160 | 242796 | 239491 | 239492 | 239493 | 239496 |
| The Abyssal Widow | 165 | 242760 | 232189 | 232190 | 232191 | 232194 |
| The Achbile Guardian | 120 | 242818 | 239690 | 239691 | 239692 | 239695 |
| The Adonian Soul Dredge | 165 | 242755 | 232144 | 232145 | 232146 | 232149 |
| The Adonis Spirit Master | 165 | 242758 | 232171 | 232172 | 232173 | 232176 |
| The Archbile Queen | 120 | 242715 | 231775 | 231776 | 231777 | 231780 |
| The Brobdingnagian Mother | 185 | 242810 | 239617 | 239618 | 239619 | 239622 |
| The Dryad Shuffle | 185 | 242732 | 231937 | 231938 | 231939 | 231942 |
| The Dune Suzerain | 90 | 242680 | 231460 | 231461 | 231462 | 231465 |
| The Elysian Soul Dredge | 90 | 242683 | 231487 | 231488 | 231489 | 231492 |
| The Enrapt One | 120 | 242718 | 231802 | 231803 | 231804 | 231807 |
| The Indomitable Chimera | 220 | 242783 | 234273 | 234274 | 234275 | 234278 |
| The Maggot Lord | 205 | 242725 | 231865 | 231866 | 231867 | 231870 |
| The Numb One | 120 | 242717 | 231793 | 231794 | 231795 | 231798 |
| The Penumbral Spirit Hunter | 165 | 242728 | 231892 | 231893 | 231894 | 231897 |
| The Scheolian Soul Dredge | 125 | 242713 | 231757 | 231758 | 231759 | 231762 |
| The Stupendous Breeder | 185 | 242811 | 239626 | 239627 | 239628 | 239631 |
| The Talus Suzerain | 90 | 242681 | 231469 | 231470 | 231471 | 231474 |
| The Watchdog | 165 | 242756 | 232153 | 232154 | 232155 | 232158 |
| Thunderous Chimera | 125 | 242708 | 231712 | 231713 | 231714 | 231717 |
| Toss | 195 | 242751 | 232108 | 232109 | 232110 | 232113 |
| Tough | 125 | 242704 | 231676 | 231677 | 231678 | 231681 |
| Ungulera | 90 | 242682 | 231478 | 231479 | 231480 | 231483 |
| Unredeemed Vanya | 200 | 242668 | 242547 | 242548 | 242549 | 242552 |
| Upenpet | 48 | 242820 | 239708 | 239709 | 239710 | 239713 |
| Vanya | 200 |  | 232602 | 232603 | 232604 |  |
| Viscious Visitant | 165 | 242754 | 232135 | 232136 | 232137 | 232140 |
| Wacky Suzerain | 125 | 242706 | 231694 | 231695 | 231696 | 231699 |
| Wala | 50 | 242806 | 239581 | 239582 | 239583 | 239586 |
