# Alien mothership missions vs alien city-raid ships

Research date: 2026-10-26. Local implementation covers **drop rules only** for Unicorn Recruiter **team** mothership missions (ZXTE / ZXT1 / ZXTH → ALGT). Layout / Admiral / boarding behavior changes stay proposed below for Randy’s approval.

## Sources

| Topic | URL |
| --- | --- |
| LE team mothership missions | https://www.ao-universe.com/guides/lost-eden/encounter-guides/high-level-encounter/le-missions---alien-motherships |
| Alien hierarchy (General vs Fleet Admiral) | https://www.ao-universe.com/guides/alien-invasion/gameplay-guides-3/alien-hierarchy |
| City / org alien raids (boarding ship) | https://www.ao-universe.com/guides/alien-invasion/gameplay-guides-3/alien-raiding-in-your-city |
| Ofab armor bio types | https://www.ao-universe.com/guides/lost-eden/tradeskill-guides/armor/upgrading-ofab-armor |
| AI weapon bio types (analyzer) | https://www.ao-universe.com/guides/alien-invasion/tradeskill-guides-3/weapon-2/upgrading-alien-weapons |
| Alien loot distribution | https://www.ao-universe.com/guides/alien-invasion/gameplay-guides-3/alien-loot-distribution |
| Local notes (prior pass) | `ddogs/docs/alien-ships-notes.md`, `ddogs/docs/city-raids-design.md` |

Auno item pages returned 403 from this environment; item IDs and OnUse Hit 669 amounts were verified from the client **items.dat** via `tools-temp/itemdump`.

---

## Two different “alien ship” activities

### A) Lost Eden mothership missions (Unicorn Recruiter)

- **Entry:** Unicorn Outpost (Andromeda ~3124×905). Speak to the **Unicorn Recruiter** while **teamed**. Easy / Medium / Hard. Shuttle west/NW of the outpost gate.
- **Purpose:** AXP alternative; no level lock; no mission-count lock.
- **Layout / bosses:** Two room styles — **Master of X** (healing towers, nuke markers, Mind Warp /disco, Nanovoid drains, etc.) or **cocoon room** (attendant + Cocoon Accelerator with the good loot). New LE alien forms (Esox, Cornuta, Dasyatis, Cyathus, Oneirides).
- **Boss name:** Guides call these mothership mission bosses “Master of …” / Cocoon Accelerator — **not** the city-raid Fleet Admiral.
- **Loot (AO-U + Randy rules):**
  - **VP clumps** (NODROP, right-click → Victory Points / stat **669**): Cell Templates, Plasmid Cultures, Mitochondria Samples, Protein Mapping Data. Higher QL → more VP (items.dat bakes the amount).
  - **Numbered Kyr’Ozch Bio-Material** for LE/Ofab upgrades from **bosses** (AO-U lists Types 18, 34, 64, 295, 328, 468, 687, 812, 935). Randy: trash does **not** drop numbered types; Generals drop **2–4**; trash + General also drop VP clumps (General **5**).
  - Mission completion: Omni-Tek / Clan **mission token**.

### B) Alien Invasion city raid → boarded mothership

- **Entry:** Org city cloak off → waves → outdoor **General** (`…'Khaz`) → boarding lottery (4–18 players, up to 6 ship instances) if defense succeeds.
- **Layout:** Work to the boss room before the pilot/technician timer orbits the ship. Two retail boss rooms: **mini-boss ladder → Fleet Admiral** (`…'Khazoh`), or **cocoons → Recruitment Director**.
- **Loot:** Class-keyed **Lead Viralbots** on Generals; **Kyr’Ozch Viralbots**, pristine/mutated bio, typed AI weapon bios, AI armor path — the classic AI raid pool. **Not** the LE VP-clump / Ofab-type pool as the primary reward. Hierarchy guide: https://www.ao-universe.com/guides/alien-invasion/gameplay-guides-3/alien-hierarchy

**Admiral appears** on the **city-raid boarded ship** after the mini-boss chain (or Recruitment Director in the cocoon room). LE Unicorn Recruiter missions use Master/Cocoon bosses instead.

---

## GENERAL vs ADMIRAL loot pools (summary)

| Pool | Who | Typical drops | Sources |
| --- | --- | --- | --- |
| **City General** (`Khaz`) | Outdoor city-raid boss | Class Lead Viralbots (Arithmetic/Enduring/…), Solid Clumps / analyzer-typed AI weapon bios, AI armor ingredients | AO-U hierarchy + armor/weapon guides; `city-raids-design.md` |
| **Fleet Admiral** (`Khazoh`) | City-raid ship final boss (mini-boss room) | Ship-boss AI loot (viralbots, high-end AI mats) — **different** from LE mothership VP/Ofab typed list | AO-U city raiding + hierarchy |
| **LE mothership “General” (our ALGT)** | Team ZXTE/ZXT1/ZXTH objective | **5× VP clumps** + **2–4×** numbered bios (weapon 1/2/3/4/5/12/13/18/34 + Ofab 64/295/468/687/812/935 + 328) | Randy 2026-10-26; AO-U LE motherships |
| **LE mothership trash** | Crew on team ships | **VP clumps** (not numbered typed bios) | Randy 2026-10-26 |

Randy’s uploaded notes mixed “right-click → VP” onto Ofab **upgrade** type names (18/34/687/812). items.dat shows those typed bios have **no** OnUse Hit 669; only Cell/Plasmid/Mito/Protein do. Implementation follows Randy’s explicit rules 1–3 and items.dat, not that conflation.

---

## Verified item IDs (items.dat)

### VP clumps (OnUse Hit 669 + “Victory points collected”)

| Name | AOIDs (QL band) | items.dat VP at endpoints |
| --- | --- | --- |
| Plasmid Cultures | 266354 / 266355 | 4 → 20 |
| Mitochondria Samples | 266356 / 266357 | 4 → 20 |
| Protein Mapping Data | 266358 / 266359 | 4 → 40 |
| Cell Templates | 266363 / 266361 (loot pair); also 266360, 266364 | 4 → 8 |

Flags/Can in the dump lack `Can.Use` / `Consume`; runtime conversion is `VictoryPointClumps` → ExecuteOnUseSpells (Hit **669**) → DestroyOne.

### Numbered bios (General / KBMG)

| Type | AOIDs | Role |
| --- | --- | --- |
| 1, 2, 3, 4, 5, 12, 13 | 247683–247696 pairs (existing KB01–KB07) | AI / Ofab weapon specials Randy listed |
| 18 | 265321 / 265322 | Ofab edged/pierce |
| 34 | 265323 / 265324 | Ofab blunt |
| 64 | 265333 / 265334 | Ofab armor Doc/Eng/Kee/MP |
| 295 | 265329 / 265330 | Ofab armor Adv/Enf/MA/Sol |
| 468 | 265335 / 265336 | Ofab armor Crat/NT/Trader |
| 687 | 265327 / 265328 | Ofab heavy ranged |
| 812 | 265325 / 265326 | Ofab light ranged |
| 935 | 265331 / 265332 | Ofab armor Agent/Fix/Shade |
| 328 | 266352 / 266353 | On AO-U mothership loot table; included in KBMG |

All of the above **exist** in items.dat. Nothing from Randy’s numbered list was missing.

Loot groups: `KVCL` (VP children), `KBMG` (mothership General typed set). Script: `ddogs/tools/Build-MothershipBioLoot.ps1`.

---

## What our code already matched

- Team authored dungeons ZXTE / ZXT1 / ZXTH with objective **ALGT**; solo ships stay ALGN.
- Crew hashes KSAU / KGRU / KCIU / KGXH (+ ACFJ / CRDY) with Solid Clumps (`KZBM` 10%).
- ALGT raid-style extras from `Build-AlienRaidLoot.ps1` (typed weapons, pristine/mutated, bio armor) — left in place; not part of rules 1–3.
- Team mission accept already grants VP onto the quest reward (`QuestDungeonService` / stat 669). Ofab hub spends 669.
- Style 386 mothership playfield data exists; full LE room scripts do not.

## What was missing (fixed this pass)

- Trash **KBTY 3%** from `Add-ShipCrewBioMats.ps1` / commit 793a0efe — **removed**; trash gets **KVCL** instead.
- ALGT numbered bios were AI `KBTY`×2 only — now **KBMG** 2–4 and **KVCL×5**.
- VP clump item groups and right-click Hit 669 path (`VictoryPointClumps`).

## Proposed next steps (needs Randy approval — not implemented)

1. Split LE mothership **Master of X / cocoon** scripts from city-raid **Admiral / Recruitment Director** rooms (today ALGT is one General).
2. Difficulty-scaled clump QL (Easy/Med/Hard vs puller level) per AO-U.
3. Mission tokens on completion; LE-only alien forms.
4. Keep city-raid Admiral loot on the AI viralbot / AI-armor path; do **not** merge KBMG/KVCL into Admiral without an explicit call.
5. Revisit whether ALGT should keep AI raid rows (KZTW / KZPM / KAAR) once LE bosses are distinct.
