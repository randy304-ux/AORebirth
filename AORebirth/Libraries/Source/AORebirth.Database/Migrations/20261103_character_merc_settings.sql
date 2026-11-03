-- Per-character merc settings (batch 1 follow-up). Stores NT nuke mode and similar flags.
-- Additive and safe to re-run.

CREATE TABLE IF NOT EXISTS `character_merc_settings` (
	`CharacterId` INT(32) NOT NULL,
	`SettingKey` VARCHAR(64) NOT NULL,
	`SettingValue` VARCHAR(64) NOT NULL,
	`UpdatedAtUtc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
	PRIMARY KEY (`CharacterId`, `SettingKey`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;

-- Collapse the two NT roster rows into one definition id if the catalog mirror was seeded earlier.
UPDATE `merc_definitions`
SET `MercId`='nt_sanctum',
	`DisplayName`='TODO-Randy-Name',
	`Tags`='elite,carry,DPS',
	`NanoStyle`=NULL,
	`Notes`='Single NT; AOE/ST mode is character_merc_settings nt_mode'
WHERE `MercId` IN ('nt_sanctum_aoe', 'nt_sanctum');

DELETE FROM `merc_definitions` WHERE `MercId`='nt_sanctum_st';

UPDATE `character_merc_unlocks`
SET `MercId`='nt_sanctum'
WHERE `MercId` IN ('nt_sanctum_aoe', 'nt_sanctum_st');
