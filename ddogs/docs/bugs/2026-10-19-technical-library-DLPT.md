# BUG: Hack the Technical Library (AVG0) cannot be done; "DLPT is not implemented yet"

Randy, 2026-10-19 3:12 PM ET. Status: fixed locally, data only.

## Symptoms
1. Hacker Tool (87810) + Omni-Tek Technical Library (248377), in inventory or the Tradeskill window: "It is not possible to assemble those two items." The library is an inventory item, not a statel. No recipe existed; Vernon only accepted both items in his trade window.
2. The cut-off "... is not implemented yet" line was DLPT, from clicking the Remains of Shop Thief statel (295620; Sarah Greene's stolen armor, in the Underground). Zone log: "Use blocked char=23 template=295620: spawn hash DLPT is not defined" x10.

## Guide (AO-Universe Arete #8)
Tradeskill window: Hacker Tool (source) + Omni-Tek Technical Library (target) = Hacked Technical Library. Then give the Hacked Technical Library to Vernon; he hands it back and keeps you on the Shipping Manifest step. You keep the Hacker Tool.

## Fix
- Tradeskills.arete.json: 87810 + 248377 -> 295756 Hacked Technical Library, keeps the tool (DeleteSource false), Computer Literacy 20 (guessed, same as Antonio's).
- Vernon (Build-AreteVernon.ps1): a trade taking QHKL (the hacked library) completes AVG0 and hands it back. The old both-items trade still works.
- ItemTemplates.Arete.json: DLPT = DNA-Locked Prototype Raven Combat Armor 295618 (QL200), the item next to the Remains in items.dat and the one the Dead Thief Knubot gives (QDLA).
- Follow-up (Randy 3:20 PM): DLPT gives the armor once per character. Nothing if the armor is held, equipped or in the loaded bank, or 'Recover the DNA-Locked Armor' is done (mission bit 635); then 'You find nothing more on the remains.' The flea still spawns.
