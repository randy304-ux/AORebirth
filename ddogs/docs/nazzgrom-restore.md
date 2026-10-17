# Nazzgrom restore plan

Not applied. Do this only after Nazzgrom (character 20, username randy) is offline, the disconnect has flushed, and a fresh backup of `cellao_codex_clean` has been taken. Do not full-restore the 18:25 database. Do not restore cash or research. Do not change `.give setup`.

Source for both the stats and the two items: `I:\AO\backups\cellao_codex_clean-20261016-1825-nascence.sql`. He was level 15 in that backup. The level 5 he sees is the 20:09 aosetups import of "Sharkie", not the 23:41 restart.

## Stats

`stats` rows with `Type=50000` and `Instance=20`:

| Stat | Value |
| --- | --- |
| Level 54 | 15 |
| XP 52 | 81757 |
| LastXP 57 | 80150 |
| NextXP 350 | 90550 |
| TitleLevel 37 | 2 |
| Strength 16, Agility 17, Stamina 18, Sense 20, Psychic 21 | 51 |
| Assault Rifle 116 | 65 |
| Treatment 124 | 59 |
| Ranged Energy 133 | 5 |
| Multi Ranged 134 | 5 |
| Burst 148 | 59 |
| Fling Shot 150 | 5 |
| Full Auto 167 | 12 |

Intelligence 19 is already 51. Leave Used IP 39070. Leave research 263=80 and 265=2820. Do not add the 172 XP earned after the 23:41 login onto 81757.

## Gear

Same backup, `item_instances`. Reinsert only these two rows, and only when those instance ids are not already in use. If an id is taken, allocate a new one and keep the rest of the row.

Right hand, weapon page (`ContainerType` 101, placement 6):

`(7,101,20,6,0,121569,121569,1,1,1)`

Solar-Powered Assault Rifle, QL 1.

Back slot, armor page (`ContainerType` 102, placement 19):

`(5,102,20,19,0,296977,296977,1,1,1)`

Colonist Survival Pack, QL 1. That is the starter backpack. It had no items inside it in this backup.
