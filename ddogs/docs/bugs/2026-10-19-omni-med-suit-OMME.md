# BUG: Vacuum Packed Omni-Med Suit gives "OMME is not implemented yet"

Reported by Randy, 2026-10-19 1:43 PM ET. Status: fixed locally (data), not live, untested in game.

## Symptom
Using Vacuum Packed Omni-Med Suit (297054, also 304006; the Credit Card quest reward) gives "OMME is not implemented yet"; nothing spawns.

## Cause
The pack's OnUse SpawnItem hashes OMME x2, OMMU, ONMU, DYWA, ONMD, OMMD, SKGN had no ItemTemplates entry. The set was left out of ItemTemplates.VacuumPacks.json because the piece order was ambiguous (unmapped_spawn_item_hashes.csv).

## Live behaviour (AOGalaxy 297054)
Needs 6 free slots. Spawns OMME, OMME, OMMU, ONMU; then by gender: Gender == 3 (female) DYWA and OMMD, otherwise ONMD and SKGN. Omni-Med Suit pieces are QL1 only: Gloves 27381, Boots 27382, Shirt 27383, Skirt 27384, Sleeves 27385, Trousers 27386, Female Shirt 27387.

## Fix
ItemTemplates.VacuumPacks.json: OMME = Sleeves 27385 (spawned twice, one per arm), OMMU = Gloves 27381, ONMU = Boots 27382, DYWA = Female Shirt 27387, ONMD = Shirt 27383, OMMD = Skirt 27384, SKGN = Trousers 27386.
Inferred: which of OMMU/ONMU is gloves vs boots, and which gender pair is body vs legs. Both variants fill the same slots, so a swap would only mix the male and female designs.