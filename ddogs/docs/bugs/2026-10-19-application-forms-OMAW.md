# BUG: OmniTec / Clan Application Forms fail with "OMAW is not implemented yet"

Reported by Randy, 2026-10-19 12:52 PM ET (character id 23, Arete Landing 6553). Status: open, investigated, not fixed.

## Symptom
Using a Low Level Omni-Tek Application Form (81753) gives "OMAW is not implemented yet". Nothing changes: no side change, no item.

## Zone log (log clock is ET-3, so 09:54 = 12:54 PM ET)
```
09:54:56.937 GenericCmd Use inventory item char=23 slot=Inventory:65 name=Low Level Omni-Tek Application Form low=81753 ql=1 ... canUse=True
09:54:56.968 [Warn] Use blocked char=23 template=81753: spawn hash OMAW is not defined
09:55:00.062 (same again)
```
The form came from a vendor/grant at 09:54:54 ("Item created ... 81753"). The pre-check that refuses a use with an undefined spawn hash stops the whole use, so the side change effects never run either.

## Cause
The form's use effects (items.tsv) are: requirements Side(33)==Neutral(0)...; `53026:33|2` set Side = Omni; `53026:62|0`; `53030:13600`; `53033:33|40` (timed side lock); `53057` text "Omni-Tek!"; **`53064:OMAW|1|0` SpawnItem OMAW QL1**; failure text "You will need at least one faction point ... Side change denied. Your form has been destroyed".
OMAW has no entry in ItemTemplates*.json. It is listed in ddogs/docs/unmapped_spawn_item_hashes.csv (14 users). OMAW is the side-token award board: its users are the Omni forms (81753, 81758, 99727, 283651) and the award boards 99719-99725, each spawning OMAW at the next QL (upgrade chain).

## Matching forms and hashes
| Form | Spawn hash | Status |
|---|---|---|
| Omni-Tek Application Form Low/Med/High/Shadow (81753, 99727, 81758, 283651) | OMAW | undefined, fails |
| Clan Application Form Low/Med/High/Shadow (81757, 81756, 81755, 283644) | CLMR | undefined, **fails the same way** |
| Container: Omni-Tek / Clan Application Form (157807/9, 157808/10) | LLLO / LLLC | undefined (they hand out the forms) |
| Premium Omni-Tek Application Form (296484, 303360) | OIHO | undefined, fails |
| Premium Clan Application Form (296485, 303361) | CNAD | undefined, fails |
| Premium Neutral Application Form (297238, 303359) | none | no spawn; should get past this check (not tested) |

## Expected (live behaviour, from the item data; I did not fetch the AO-Universe guide for this report)
A neutral starter-island character uses the form: it sets Side to Omni (or Clan) with a timed side lock, consumes the form, and gives the side-token board at QL1: Omni-Tek Award - Basic Board (99719) / Clan Merits - Basic Board (96358). With no faction points it says "Side change denied. Your form has been destroyed."

## Proposed fix (data first, then verify)
1. Add an ItemTemplates overlay (QL ladder, like the vacuum packs):
   - OMAW: QL1 99719, QL2 99720 ... QL7 99725 (Omni-Tek Award boards, QL = item QL in items.tsv).
   - CLMR: QL1 96358 (Basic Board), QL2-8 96351-96357.
   - LLLO: 81753 at QL1 / 81758 at QL201 (Low/High Omni forms); LLLC: 81757 / 81755.
   - OIHO and CNAD (premium forms): look them up first (probably premium side items or boards); defer if unsure.
2. Check that 53026 (set stat), 53030, 53033 (timed set) and 53057 are implemented, and that the side change persists and updates the client. If they aren't, the form will fail with the next "not implemented".
3. Test as a neutral: Omni form gives Side=2 and board 99719; Clan form gives Side=1 and board 96358; the failure path destroys the form.