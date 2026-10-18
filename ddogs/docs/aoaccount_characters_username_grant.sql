-- Account API /account-characters (launcher first-login UI seed) reads characters.Username, so the
-- column-level read grant set for aoaccount (Id, Name, Online) gains Username. Revert:
--   REVOKE SELECT (Username) ON cellao_codex_clean.characters FROM 'aoaccount'@'127.0.0.1';
GRANT SELECT (Username) ON cellao_codex_clean.characters TO 'aoaccount'@'127.0.0.1';
