namespace ZoneEngine_New.Core.Playfield;

using System;
using System.Collections.Generic;

using AORebirth.Enums;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Mobs;

/// <summary>
/// Sector 42 laser fences (and any other ContactDamage prop): apply moderate damage to
/// players (and their pets) who stand on the prop. This is intentionally not an instakill.
/// </summary>
public static class ContactDamageClock
{
    public const int DamagePerTick = 2500;
    public const double IntervalSeconds = 1.0;
    public const double Radius = 3.0;
    public const double VerticalSlack = 4.0;

    static readonly Dictionary<int, double> Accumulators = new();

    public static void Tick(Playfield playfield, double deltaTime)
    {
        if (playfield == null)
            return;

        int id = playfield.Identity.Instance;
        Accumulators.TryGetValue(id, out double acc);
        acc += deltaTime;
        if (acc < IntervalSeconds)
        {
            Accumulators[id] = acc;
            return;
        }
        Accumulators[id] = acc - IntervalSeconds;

        DynelRegistry? registry = playfield.GetService<DynelRegistry>();
        if (registry == null)
            return;

        List<NpcCharacter> fences = new();
        foreach (Dynel dynel in registry.Dynels())
        {
            if (dynel is NpcCharacter npc
                && npc.Playfield != null
                && !npc.IsDead
                && npc.MobTemplate != null
                && npc.MobTemplate.Has(NpcFeature.ContactDamage))
            {
                fences.Add(npc);
            }
        }
        if (fences.Count == 0)
            return;

        foreach (Player player in registry.PlayerEntities())
        {
            if (player.Playfield == null || player.IsDead)
                continue;
            MaybeHit(fences, player);
        }

        foreach (Dynel dynel in registry.Dynels())
        {
            if (dynel is not NpcCharacter pet || pet.PetOwner is not Player || pet.IsDead || pet.Playfield == null)
                continue;
            MaybeHit(fences, pet);
        }
    }

    static void MaybeHit(List<NpcCharacter> fences, Character target)
    {
        foreach (NpcCharacter fence in fences)
        {
            double dx = fence.Position.xf - target.Position.xf;
            double dz = fence.Position.zf - target.Position.zf;
            double dy = Math.Abs(fence.Position.yf - target.Position.yf);
            if (dy > VerticalSlack)
                continue;
            if (dx * dx + dz * dz > Radius * Radius)
                continue;
            target.ApplyDamage(fence, DamagePerTick, default);
            return;
        }
    }
}
