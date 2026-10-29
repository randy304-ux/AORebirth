namespace ZoneEngine_New.Core.Mobs
{
    /// <summary>
    /// Behaviour switches an NPC template level entry lists in <c>NpcTemplates.json</c> ("Features": ["NoCombat"]).
    /// </summary>
    public enum NpcFeature
    {
        /// <summary>Cannot be attacked and gets no combat brain (never aggroes or fights back).</summary>
        NoCombat,

        NoMove,

        Wander,

        /// <summary>
        /// Attackable punching-bag: takes hits for DPS testing, never dies (HP clamped/healed), no combat brain,
        /// no XP or loot. Used by Ofab hub Training Dummy (PUBG) on playfield 6007.
        /// </summary>
        Immortal,
    }
}
