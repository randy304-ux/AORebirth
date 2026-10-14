-- Team-rolled generated missions: 1 when the offer was rolled in a team of two or more, 0 (default) = solo.
-- Additive; existing offers stay solo. Applied by checking information_schema first (MySQL has no ADD COLUMN IF NOT EXISTS).
ALTER TABLE generatedmissionoffers ADD COLUMN IsTeam INT NOT NULL DEFAULT 0 AFTER RewardCount;
