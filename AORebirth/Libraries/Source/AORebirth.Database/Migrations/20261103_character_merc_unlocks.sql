-- Dungeon-unlock merc progress (batch 1).
-- State: locked (no row), dungeon_only (mission accepted), unlocked (boss killed).
-- Additive and safe to re-run. Deploy script applies this; do not write live by hand.

CREATE TABLE IF NOT EXISTS `character_merc_unlocks` (
	`CharacterId` INT(32) NOT NULL,
	`MercId` VARCHAR(64) NOT NULL,
	`State` VARCHAR(32) NOT NULL,
	`UpdatedAtUtc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
	PRIMARY KEY (`CharacterId`, `MercId`),
	KEY `IX_character_merc_unlocks_state` (`State`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;

-- Optional catalog mirror for ops/SQL visibility. Runtime loads GameData/MercRoster.json.
CREATE TABLE IF NOT EXISTS `merc_definitions` (
	`MercId` VARCHAR(64) NOT NULL,
	`DisplayName` VARCHAR(128) NOT NULL,
	`ProfessionKey` VARCHAR(32) NOT NULL,
	`Level` INT(32) NOT NULL,
	`Role` VARCHAR(32) NOT NULL,
	`Tags` VARCHAR(255) NOT NULL DEFAULT '',
	`DungeonName` VARCHAR(128) NOT NULL,
	`DungeonPlayfields` VARCHAR(255) NOT NULL DEFAULT '',
	`BossName` VARCHAR(128) NOT NULL,
	`BossHash` VARCHAR(16) NULL,
	`UnlockGroup` VARCHAR(64) NOT NULL,
	`NanoStyle` VARCHAR(16) NULL,
	`Notes` VARCHAR(512) NULL,
	PRIMARY KEY (`MercId`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;

INSERT INTO `merc_definitions`
	(`MercId`,`DisplayName`,`ProfessionKey`,`Level`,`Role`,`Tags`,`DungeonName`,`DungeonPlayfields`,`BossName`,`BossHash`,`UnlockGroup`,`NanoStyle`,`Notes`)
VALUES
	('enforcer_subway','TODO-Randy-Name (Enforcer)','enforcer',24,'tank','twink,tank','Condemned Subway','127,128','Abmouth Supremus','ASSU','enforcer_subway',NULL,NULL),
	('doctor_totw','TODO-Randy-Name (Doctor)','doctor',59,'healer','twink,healer','Temple of Three Winds','1931','Aztur the Immortal','AZTR','doctor_totw',NULL,NULL),
	('bureaucrat_som','TODO-Randy-Name (Bureaucrat)','bureaucrat',50,'crowd control','twink,crowd control','Steps of Madness','1933','Neleb the Deranged','NETH','bureaucrat_som',NULL,NULL),
	('fixer_biomare','TODO-Randy-Name (Fixer)','fixer',99,'support','twink,support','Biomare / Foreman''s','1941','Lab Director',NULL,'fixer_biomare',NULL,'TODO: Lab Director hash'),
	('soldier_cyborg','TODO-Randy-Name (Soldier)','soldier',95,'DPS','twink,DPS','Cyborg Barracks','1833','Commander Jocasta',NULL,'soldier_cyborg',NULL,'TODO: Commander Jocasta hash'),
	('adventurer_crypt','TODO-Randy-Name (Adventurer)','adventurer',109,'support','elite,support','Crypt of Home','4805','Cerubin the Rejected',NULL,'adventurer_crypt',NULL,'TODO: Cerubin hash'),
	('keeper_smugglers','TODO-Randy-Name (Keeper)','keeper',149,'support','elite,support','Smuggler''s Den','125,1862','Den Mantis Queen',NULL,'keeper_smugglers',NULL,'TODO: Den Mantis Queen hash'),
	('nt_sanctum_aoe','TODO-Randy-Name (AOE)','nanotechnician',189,'carry','elite,carry,DPS,aoe','Inner Sanctum','','Hezak the Immortal',NULL,'nt_sanctum','aoe','TODO: Inner Sanctum PF + Hezak hash; shares unlock with ST'),
	('nt_sanctum_st','TODO-Randy-Name (ST)','nanotechnician',189,'carry','elite,carry,DPS,st','Inner Sanctum','','Hezak the Immortal',NULL,'nt_sanctum','st','TODO: Inner Sanctum PF + Hezak hash; shares unlock with AOE'),
	('shade_camelot','TODO-Randy-Name (Shade)','shade',115,'DPS','elite,DPS','Camelot','120,505','Lady Morgan Le Faye',NULL,'shade_camelot',NULL,'TODO: Morgan Le Faye hash'),
	('ma_camelot','TODO-Randy-Name (Martial Artist)','martialartist',200,'DPS','elite,DPS','Camelot','120,505','Reborn Lord Ghasap',NULL,'ma_camelot',NULL,'TODO: Reborn Lord Ghasap hash'),
	('trader_elysium','TODO-Randy-Name (Trader)','trader',130,'support','elite,support','Elysium Catacombs','4211,4220','Herne','JBYY','trader_elysium',NULL,'Catacombs = Maze_Acheron'),
	('engineer_scheol','TODO-Randy-Name (Engineer)','engineer',170,'support','elite,support','Scheol Catacombs','4212,4221','Samedi',NULL,'engineer_scheol',NULL,'TODO: Samedi unverified'),
	('mp_adonis','TODO-Randy-Name (Meta-Physicist)','metaphysicist',205,'support','elite,support','Adonis Catacombs','4213,4222','Amatsu',NULL,'mp_adonis',NULL,'TODO: Amatsu unverified'),
	('agent_penumbra','TODO-Randy-Name (Agent)','agent',220,'DPS','elite,DPS','Penumbra Catacombs','4214,4223','Kur',NULL,'agent_penumbra',NULL,'TODO: Kur unverified')
ON DUPLICATE KEY UPDATE
	`DisplayName`=VALUES(`DisplayName`),
	`ProfessionKey`=VALUES(`ProfessionKey`),
	`Level`=VALUES(`Level`),
	`Role`=VALUES(`Role`),
	`Tags`=VALUES(`Tags`),
	`DungeonName`=VALUES(`DungeonName`),
	`DungeonPlayfields`=VALUES(`DungeonPlayfields`),
	`BossName`=VALUES(`BossName`),
	`BossHash`=VALUES(`BossHash`),
	`UnlockGroup`=VALUES(`UnlockGroup`),
	`NanoStyle`=VALUES(`NanoStyle`),
	`Notes`=VALUES(`Notes`);
