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

        /// <summary>
        /// Players and their pets cannot target or damage this NPC, including area attacks.
        /// Other NPCs can still attack it when Attackable is true. Used by Sector 35
        /// Unicorn Service Towers and the Unicorn Energy Accumulator.
        /// </summary>
        PlayerUntargetable,

        /// <summary>
        /// Standing prop that hurts players who walk into it, with moderate damage
        /// (see ContactDamageClock). Used by Sector 42 Alien Laser Fences. Does not
        /// instakill. The prop itself is usually also NoCombat.
        /// </summary>
        ContactDamage,
    }
}
