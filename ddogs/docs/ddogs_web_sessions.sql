-- ddogs_web_sessions: which online character plays from which address (zone engine WebSessionMap, every 10 s).
-- The account service reads it so the in-game Info pages know the character without a claim code.
USE cellao_codex_clean;
CREATE TABLE IF NOT EXISTS ddogs_web_sessions (
  CharId INT NOT NULL PRIMARY KEY,
  Ip VARCHAR(45) NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  KEY ix_ip (Ip)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
GRANT SELECT ON cellao_codex_clean.ddogs_web_sessions TO 'aoaccount'@'127.0.0.1';