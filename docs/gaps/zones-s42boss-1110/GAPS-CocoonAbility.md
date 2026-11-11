# Cocoon ability — nothing stripped

Randy asked to remove the cocoon ability from aliens. The hated APF ability is Kyr'Ozch Sticky Field (wiki: the Alien Cocoon's root-like field), not the daily-quest Alien Cocoon NPCs, not the Sector 42 trash named "Alien Coccoon", and not player Harmonic/Energy Cocoon nanos or the Bio Cocoon perk.

## Searches (no ability found to remove)

- itemnames.sql: no item or nano crystal named "Sticky Field" or "Kyr'Ozch Sticky Field".
- NpcTemplates.Alien.json, AlienMothership, AlienScoutship, Sector13, Sector28, Sector35, Sector42: no Sticky Field / Encase nano or spell list on alien NPCs. Hits are only daily "Alien Cocoon" mobs, typo "Alien Coccoon" trash, and a Sector 28 loot note ("run ended in the cocoon").
- GameData root: no NanoList / Spell catalog containing Sticky Field. ItemTemplates.NanoContainers.json had no match.
- ZoneEngine_New Core *.cs: searched for "Sticky Field" / StickyField (see inspect log).

No template Nanos/Spells rows were removed. Daily Alien Cocoon quest mobs and S42C "Alien Coccoon" trash were left in place. If Sticky Field lives only in client-side mob nano data that this server never imported, it is not present to strip.
