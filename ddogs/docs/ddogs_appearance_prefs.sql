-- Per-character look toggles from the Store window's Battlesuits page (account API writes, ZoneEngine_New reads).
-- PrefKey is '<group>.<toggle>', e.g. battlesuit.look / battlesuit.only, so other item groups can reuse the table.
CREATE TABLE IF NOT EXISTS ddogs_appearance_prefs (CharId INT NOT NULL, PrefKey VARCHAR(64) NOT NULL, Value INT NOT NULL, UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP, PRIMARY KEY (CharId, PrefKey)) ENGINE=InnoDB;
GRANT SELECT, INSERT, UPDATE ON cellao_codex_clean.ddogs_appearance_prefs TO 'aoaccount'@'127.0.0.1';
