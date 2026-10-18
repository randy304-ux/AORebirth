-- Personal mailbox carrier swap (2026-10-18 19:10 ET). Apply ONLY while the engines are stopped (online = 0).
-- Moves every inventory copy of the earlier carriers (Send Mail 297053, Letter from Penelope 252503) to the
-- Portable Bank Terminal 288762. Idempotent: a second run changes 0 rows.
UPDATE item_instances SET LowId = 288762, HighId = 288762
 WHERE ContainerType = 104 AND LowId IN (297053, 252503);
SELECT InstanceId, ContainerInstance AS CharId, ContainerPlacement AS Slot, LowId
  FROM item_instances WHERE ContainerType = 104 AND LowId IN (297053, 252503, 288762);