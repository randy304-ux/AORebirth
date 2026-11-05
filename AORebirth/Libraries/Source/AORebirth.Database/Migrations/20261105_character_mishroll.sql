-- Per-character mishroll preferred terminal reward pin (solo RK missions).
-- Additive and safe to re-run. Do not apply until Mike authorizes a DB deploy.

CREATE TABLE IF NOT EXISTS `character_mishroll` (
	`CharacterId` INT(32) NOT NULL,
	`ItemId` INT(32) NOT NULL,
	`UpdatedAtUtc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
	PRIMARY KEY (`CharacterId`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;
