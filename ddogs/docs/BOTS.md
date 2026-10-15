# Buffbot and Orgbot

Server-side bots. No external player character is logged in.

The buff window follows the Budabot / Nadybot info-window buffbot (the ChewysBuffs tell window: Buffmacro, Rebuff, Help, Status, then profession sections). Nadybot is Nadya's GPL PHP fork of Budabot. Tyrbot (https://github.com/Budabot/Tyrbot) is the Python 3 rewrite of Budabot; its org-chat, alts, and news model is what the org bot follows. Both are credited here. The behavior is reimplemented in ZoneEngine_New and ChatEngine. This server does not run those programs.

## Buffbot

One NPC, `Buffbot`, on playfield 655 at 3251.4, 35.1, 823.4. That is Randy's outdoor ICC hub pin, not a measured row coordinate. Body is client monster 96056, protocol droid (catMesh 96024). White uniforms are not available on a hash template.

`.buff`, a tell to Buffbot, or talking to the NPC opens the same `text://` info window as `.info`. A buff link runs `.buff cast` with the items.dat id. `GameData/Buffbot.json` stores names only. Startup keeps the highest QL of that exact name whose QL (and any real ToUse level compare) is at or below the player's level. Stat 54 on the nano template is NCU, not the level gate. Landing uses `Character.TryApplyBuff`, so strain, stacking, and NCU are the real rules. There is no credit cost, no nano-energy spend, and no upload check.

Commands: `.buff`, `.buff menu`, `.buff help`, `.buff page N`, `.buff cast id`, `.buff macro` (Buffmacro), `.buff rebuff`, `.buff status`.

items.dat names one rank for these programs (for example Composite Attribute Boost is QL 4). Separate hour-duration names are not on the menu. Other profession lines can be added to Buffbot.json when the exact items.dat name is known.

## Orgbot

`.org create Name` inserts the organization, sets the president's clan stat (stats StatId 5, owner type 50000) and clan level 0, and opens the org channel. The bot name in chat is Orgbot. It is not a character row. Commands work as `.org` in the zone, as a dot command in org chat, or as a tell to Orgbot.

`.org online`, `.org members`, `.org lastseen` (online or offline only; no clock column), `.org motd`, `.org news` (Objective and History on the existing organization row), `.org alts` (same account username), `.org add Name` (president), `.org raid`, `.org loot`. Raid and loot notes are memory and clear on a zone restart. There is no Discord relay and no AO Chat Proxy.
