namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Pets;

    /// <summary>
    /// Mezz pets (the Support slot: the MP merc's Distracting Sphere, a player MP's mezz pet) keep adds out of the
    /// fight (Randy 2026-11-14): they pacify a mob that is fighting the party but is not what the party's players are
    /// fighting, preferring adds that stand outside weapon splash range of those main targets, since a BFG splash or an
    /// area nuke rolls the mezz's break-on-spell-attack chance.
    /// </summary>
    sealed partial class NpcNanoCaster
    {
        /// <summary>Pacify (strain 147) ladder (castable id, pet level >= QL): Distracted Gaze .. Empowered Peaceful Intentions.</summary>
        internal static readonly (int Id, int Level)[] MezzLadder =
        [
            (30065, 8), (30056, 22), (30093, 36), (100432, 63), (100431, 79), (100430, 98), (100429, 115),
            (100428, 127), (155577, 146), (259362, 208), (259364, 232)
        ];

        /// <summary>Adds this close to a main target are likely to be hit by splash or area damage.</summary>
        const float MezzSplashClearMeters = 7f;

        /// <summary>A mob the mezz did not stick on (resist, out of range) is not retried for this long.</summary>
        const double MezzRetrySeconds = 8;

        readonly Dictionary<int, DateTime> _mezzRetryUtc = new();
        readonly List<Character> _mainTargets = new();

        bool IsMezzPet => Npc.Pet is { } pet && PetTypes.Slot(pet.Type) == PetTypes.Slot(PetTypes.Support);

        /// <summary>A Support-slot pet pacifies the best add. True when it started a cast.</summary>
        bool TryMezzPetRole(DateTime nowUtc)
        {
            if (!IsMezzPet || Npc.Pet is { Mode: PetMode.Wait })
                return false;

            int mezzId = PickRung(MezzLadder, MercLevel);
            if (mezzId == 0 || !IsReady(mezzId, nowUtc) || Resolve(mezzId) is not NanoSpell mezz)
                return false;

            BuildParty();
            CollectEngagedMobs();
            if (_engaged.Count < 2)
                return false;

            CollectMainTargets();
            if (_mainTargets.Count == 0)
                return false;

            NpcCharacter? best = null;
            bool bestClear = false;
            int bestVictimHp = int.MaxValue;
            foreach ((NpcCharacter mob, Character victim) in _engaged)
            {
                if (_mainTargets.Contains(mob) || mob.IsPacified)
                    continue;
                if (_mezzRetryUtc.TryGetValue(mob.Identity.Instance, out DateTime retry) && nowUtc < retry)
                    continue;
                if (!InRange(mezz, mob, NpcAiRules.NearbyRange) || !NpcSupportRules.MayNpcLand(Npc, mob, mezz)
                    || !NanoRuntime.CanStartCast(Npc, mezz, mob, nowUtc))
                    continue;

                bool clear = IsClearOfMainTargets(mob);
                int victimHp = HealthPercent(victim);
                if (best == null || (clear && !bestClear) || (clear == bestClear && victimHp < bestVictimHp))
                {
                    best = mob;
                    bestClear = clear;
                    bestVictimHp = victimHp;
                }
            }

            if (best == null)
                return false;

            _mezzRetryUtc[best.Identity.Instance] = nowUtc.AddSeconds(MezzRetrySeconds);
            if (_mezzRetryUtc.Count > 64)
                PruneMezzRetries(nowUtc);
            return RoleCast(mezz, best, nowUtc, bestClear ? "mezz" : "mezz-near-main", 3);
        }

        /// <summary>What the party's players are fighting; when no player is fighting, what anyone on the side is fighting.</summary>
        void CollectMainTargets()
        {
            _mainTargets.Clear();
            AddFightTargets(players: true);
            if (_mainTargets.Count == 0)
                AddFightTargets(players: false);
        }

        void AddFightTargets(bool players)
        {
            foreach (Character member in _party)
            {
                if ((member is Player) != players || member.FightingTarget.Instance == 0)
                    continue;
                foreach ((NpcCharacter mob, _) in _engaged)
                {
                    if (mob.Identity.Instance == member.FightingTarget.Instance && !_mainTargets.Contains(mob))
                        _mainTargets.Add(mob);
                }
            }
        }

        bool IsClearOfMainTargets(NpcCharacter mob)
        {
            foreach (Character main in _mainTargets)
            {
                if (main.Distance3D(mob) <= MezzSplashClearMeters)
                    return false;
            }

            return true;
        }

        void PruneMezzRetries(DateTime nowUtc)
        {
            List<int>? stale = null;
            foreach (KeyValuePair<int, DateTime> pair in _mezzRetryUtc)
            {
                if (pair.Value <= nowUtc)
                    (stale ??= new List<int>()).Add(pair.Key);
            }

            if (stale == null)
                return;
            foreach (int key in stale)
                _mezzRetryUtc.Remove(key);
        }
    }
}