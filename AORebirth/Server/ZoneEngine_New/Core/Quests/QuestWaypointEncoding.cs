namespace ZoneEngine_New.Core.Quests
{
    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Public hook for tests and the staging harness: template-quest journal marker encoding.
    /// Generated-mission encoding is unchanged and lives only in <see cref="QuestJournal"/>.
    /// </summary>
    public static class QuestWaypointEncoding
    {
        /// <summary>IdentityType.Playfield2 (40016) with the playfield instance, or an empty Identity when omitted.</summary>
        public static Identity PlayfieldId(QuestTemplate template) => QuestJournal.TemplateWaypointId(template);

        /// <summary>Waypoint position, or null when omitted (the packet writer then emits 0,0,0, as today).</summary>
        public static Vector3? Position(QuestTemplate template) => QuestJournal.TemplateWaypointPosition(template);
    }
}
