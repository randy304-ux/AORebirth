-- Cyborg Barracks (pf 1833) scrape import companion SQL.
-- GameData change is authoritative for spawns/templates:
--   Build-ScrapeImportCB.ps1 + scrape_import_cb_20261103.json
--   fills pink hashes in Playfields/1833/Spawns.json (THVI = Commander Jocasta).
-- This migration only mirrors BossHash onto merc_definitions for ops visibility.
-- STAGED ONLY - do not apply to live DB until Mike authorizes deploy.
-- Safe to re-run.

UPDATE `merc_definitions`
SET
	`BossHash` = 'THVI',
	`Notes` = 'Boss hash THVI from client Spawns.json + Whozewhatzit 2026-11-03 scrape (MonsterData 154698)'
WHERE `MercId` = 'soldier_cyborg';
