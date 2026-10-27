# Alien mothership missions vs alien city-raid ships

Research date: 2026-10-26 (addenda same day). Drop rules for Unicorn Recruiter **team** motherships (ZXTE / ZXT1 / ZXTH → **ALMS**). City invasions keep **ALGT**. Trash multi-drop rates live in `GameData/MothershipVpLoot.json`. Layout / Admiral / LE-form spawn work stays proposed for Randy’s approval.

## Sources

| Topic | URL |
| --- | --- |
| LE team mothership missions | https://www.ao-universe.com/guides/lost-eden/encounter-guides/high-level-encounter/le-missions---alien-motherships |
| Alien hierarchy (General vs Fleet Admiral) | https://www.ao-universe.com/guides/alien-invasion/gameplay-guides-3/alien-hierarchy |
| City / org alien raids (boarding ship) | https://www.ao-universe.com/guides/alien-invasion/gameplay-guides-3/alien-raiding-in-your-city |
| Kyr’ozch morphology (mothership forms) | https://wiki.aodb.us/wiki/Kyr%27ozch |
| Lost Eden Q&A (team mothership ACG) | https://wiki.aodb.us/wiki/Lost_Eden_Q%26A_2 |
| Ofab armor bio types | https://www.ao-universe.com/guides/lost-eden/tradeskill-guides/armor/upgrading-ofab-armor |
| AI weapon bio types (analyzer) | https://www.ao-universe.com/guides/alien-invasion/tradeskill-guides-3/weapon-2/upgrading-alien-weapons |
| Local notes | `ddogs/docs/alien-ships-notes.md`, `ddogs/docs/city-raids-design.md` |

Auno item pages returned 403 here; item IDs / OnUse Hit 669 verified from client **items.dat** (`tools-temp/itemdump`).

---

## Two different “alien ship” activities

### A) Lost Eden mothership missions (Unicorn Recruiter)

- **Entry:** Unicorn Outpost (Andromeda ~3124×905). **Unicorn Recruiter**, must be **teamed**. Easy / Medium / Hard. Shuttle west/NW of the gate.
- **Purpose:** AXP without an org city raid; no level lock; no mission-count lock ([LE Q&A](https://wiki.aodb.us/wiki/Lost_Eden_Q%26A_2)).
- **Layout / bosses:** **Master of X** rooms (healing towers, nuke markers, Mind Warp /disco, Nanovoid drains) or **cocoon room** (attendant + **Cocoon Accelerator**).
- **Boss name:** Master of … / Cocoon Accelerator — **not** the city-raid Fleet Admiral.
- **Loot:** VP clumps + Ofab/LE typed bios from the mothership General; mission tokens on complete.

### B) Alien Invasion city raid → boarded ship

- **Entry:** Org city cloak off → waves → outdoor **General** (`…'Khaz`) → boarding lottery → ship instance.
- **Layout:** Pilot/technician timer; mini-boss ladder → **Fleet Admiral** (`…'Khazoh`), or cocoons → **Recruitment Director**.
- **Loot:** Lead Viralbots / Kyr’Ozch Viralbots / AI armor path — **not** the LE VP-clump / Ofab-type pool.

Randy confirmed these are **completely different** systems (addendum 2026-10-26).

---

## Mothership-only alien types (LE)

AO-Universe “The New Aliens” and [AOWiki Kyr’ozch](https://wiki.aodb.us/wiki/Kyr%27ozch) list forms that appear on the **mothership**, not in classic city-raid hierarchy (Scout/Guard/… Uri/Sinuh/Heru/Nakthi):

| Form | Retail notes | In our DB? |
| --- | --- | --- |
| **Esox** | Pike-like; mothership; still uses AI classes (e.g. Cha, Jaax, Rimah) | **No** `NpcTemplates` / `MonsterData` name. Client **items.dat** has Esox-* animation / equip strings (e.g. 263520–263540). |
| **Cornuta** | C-clamp fish; mothership | **No** NPC template. items.dat has Cornuta_* anims (264043+). |
| **Dasyatis** | Manta-like nuker; **mothership only** (AOWiki) | **No** NPC template. |
| **Cyathus** | Hard hitters; mothership | **No** NPC template. items.dat has Cyathus_* / CyathusCha_* anims (263541+). |
| **Oneirides** / **Oneirodes** | Tentacled nano casters only; mothership | **No** NPC template (AO-U spelling Oneirides; wiki Oneirodes). |
| **Nanovoid** (adds) | Master of Silence companions; nano drain; fast respawn | **No** NPC template. |
| **Masters / Cocoon Accelerator** | LE mission bosses | **No** dedicated hashes; we use **ALMS** “Alien General” + ALHT towers only. |

**Present today (AI / Arete ship crew, not LE forms):** KSAU, KGRU, KCIU, KGXH, ACFJ, CRDY, ALGN, ALGT, ZIXK, ALHT. Team mothership RegularNpcHashes are still those hierarchy crew hashes.

**Client evidence without NPC rows:** animation item names prove LE meshes/anims shipped; we have not authored mothership-only NPC hashes yet. Spawning Esox/etc. is a **proposed next step**, not done.

---

## Loot pools in code (kept separate)

| Pool hash | Used by | Drops |
| --- | --- | --- |
| **ALMS** | ZXTE / ZXT1 / ZXTH `ObjectiveNpcHash` | **KBMG** 2–4 (mothership numbered bios) + **KVCL** ×5 (VP clumps) + shared weapon/armor extras |
| **ALGT** | `Invasions.json` city-wave boss; AI raid table | **KBTY** ×2 (AI weapon-special bios) + KZTW / KZPM / KAAR — **no** KBMG / KVCL |
| **ALGN** | Solo ship boss | Pre-raid General table (no mothership VP/Ofab pool) |
| Crew + `KZBM` 10% | Shared ship crew (incl. team RegularNpcHashes) | Independent **KVCL** rolls (default **90% / 30% / 10%**, max 3); numbered types **not** on trash |

Tunables: `GameData/MothershipVpLoot.json` (re-run `ddogs/tools/Build-MothershipBioLoot.ps1` after edits).

Default trash chances (independent rolls, max 3): **90%** first, **30%** second, **10%** third.

Default tier mix per successful roll (`TierWeights` → `KVCL` Children):

- **50%** Cell Templates (`KVPC` weight 3)
- **~16.7%** Plasmid Cultures (`KVPP` weight 1)
- **~16.7%** Mitochondria Samples (`KVPM` weight 1)
- **~16.7%** Protein Mapping Data (`KVPR` weight 1)

Admiral / city-General viralbot pools are **not** wired to KBMG/KVCL.

---

## GENERAL vs ADMIRAL (retail summary)

| Pool | Who | Typical drops | Sources |
| --- | --- | --- | --- |
| City General (`Khaz`) | Outdoor city boss | Class Lead Viralbots, AI clumps / typed weapon bios | AO-U hierarchy + armor guides |
| Fleet Admiral (`Khazoh`) | City-raid ship final (after mini-bosses) | AI ship-boss loot | AO-U city raiding + hierarchy |
| LE mothership General (**ALMS**) | Unicorn Recruiter team objective | 5× VP clumps + 2–4× KBMG | Randy; AO-U LE motherships |
| LE mothership trash | Crew | 90% one VP bio; tier mix above | Randy addendum |

---

## Verified item IDs (items.dat)

### VP clumps (OnUse Hit 669)

| Name | AOIDs | VP at endpoints |
| --- | --- | --- |
| Plasmid Cultures | 266354 / 266355 | 4 → 20 |
| Mitochondria Samples | 266356 / 266357 | 4 → 20 |
| Protein Mapping Data | 266358 / 266359 | 4 → 40 |
| Cell Templates | 266363 / 266361 (loot pair); also 266360, 266364 | 4 → 8 |

Runtime: `VictoryPointClumps` (no `Can.Use` on dump) → Hit **669** → DestroyOne.

### Numbered bios (`KBMG`)

Types 1/2/3/4/5/12/13/18/34/64/295/328/468/687/812/935 — all present in items.dat (see prior commit notes / KB01–KB22).

---

## What matches / what is still missing

**Matches**

- Team ships → **ALMS** mothership loot; invasions → **ALGT** city/AI loot.
- Trash VP 90% + weighted tiers; General 5 clumps + 2–4 KBMG.
- Right-click VP (stat 669); Ofab spends 669.
- ALHT healing towers for Master-of-Bio style fights.

**Missing (proposed — need Randy approval)**

1. Author NPC templates for Esox / Cornuta / Dasyatis / Cyathus / Oneirides (+ Nanovoid adds) using client meshes/anims; put them on mothership RegularNpcHashes only.
2. Master of X / Cocoon Accelerator scripts (distinct from city Admiral / Recruitment Director).
3. Easy/Med/Hard clump QL vs puller level; mission tokens.
4. Dedicated mothership-only crew hashes if shared KSAU VP rate on solo ships is unwanted.
5. Full Admiral viralbot loot table (separate from ALMS/ALGT).
