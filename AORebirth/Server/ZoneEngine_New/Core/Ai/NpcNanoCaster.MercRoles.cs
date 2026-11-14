namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Pets;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.Teams;

    using Utility;

    /// <summary>
    /// Merc combat roles, checked faster than the generic nano loop (Randy 2026-11-14):
    /// an Enforcer taunts (Mongo area taunt on 2+ mobs on the team, single taunts on mobs hitting anyone else);
    /// the Doctor is the main healer; other mercs with heals (Adventurer, Martial Artist, Trader, NT, ...) heal
    /// only when there is no Doctor merc or the Doctor is struggling; an NT keeps its nano-delta buff (Humidity Extractor)
    /// on the party; a Trader keeps Outfit (Team) nano-over-time and Umbral Wrangler team skill buff running.
    /// Every ladder is picked by merc level (level >= the nano's QL), since mercs have no real skills.
    /// </summary>
    sealed partial class NpcNanoCaster
    {
        /// <summary>How often a merc re-checks its role (taunt / heal / nano restore).</summary>
        public const double RoleCheckSeconds = 1.5;

        /// <summary>Mongo area taunts reach 20 m around the Enforcer; count mobs a little inside that.</summary>
        public const double MongoReachMeters = 18;

        /// <summary>Mobs this close to the Enforcer are considered for taunts.</summary>
        public const double TauntScanMeters = 30;

        /// <summary>Re-cast Mongo this often while 2+ mobs stay on the team even when all are on the Enforcer.</summary>
        public const double MongoRefreshSeconds = 20;

        /// <summary>The Doctor tops up anyone under this health.</summary>
        public const int PrimaryHealBelowPercent = 90;

        /// <summary>Secondary healers heal under this when there is no Doctor merc.</summary>
        public const int SecondaryHealBelowPercent = 75;

        /// <summary>Secondary healers heal under this while the Doctor is struggling.</summary>
        public const int StrugglingHealBelowPercent = 60;

        /// <summary>Doctor merc counts for the team when this close to the healer.</summary>
        public const double DoctorPresenceMeters = 40;

        /// <summary>Single taunt ladder (item id, merc level): Taunting Glare .. Improved Element of Malice.</summary>
        internal static readonly (int Id, int Level)[] SingleTauntLadder =
        [
            (100213, 1), (100211, 27), (100212, 53), (100210, 100), (100209, 118), (223115, 124),
            (223117, 184), (223119, 202), (223121, 210), (223123, 215), (275014, 220)
        ];

        /// <summary>Mongo area taunt ladder (castable id, merc level): Slam .. Demolish.</summary>
        internal static readonly (int Id, int Level)[] MongoLadder =
        [
            (100198, 1), (100200, 50), (100197, 80), (100199, 119), (301291, 170), (270786, 215)
        ];

        /// <summary>
        /// NT nano-delta buff ladder (Humidity Extractor line; merc level >= QL). Can=0 and its Hit is on Target, so it
        /// goes on any party member, not only self. 4 h, a nano tick every 15 s.
        /// </summary>
        internal static readonly (int Id, int Level)[] NanoDeltaLadder =
        [
            (90400, 1), (90405, 17), (90404, 40), (90403, 80), (90402, 99), (90401, 149), (90406, 169)
        ];

        /// <summary>
        /// Trader Outfit (Team) nano-over-time ladder (merc level >= QL). Self-cast (Can=ApplyOnSelf); its TeamCastNano
        /// pulses the nano heal to the merc, its player and the player's team within 40 m every 8 s for 60 s.
        /// </summary>
        internal static readonly (int Id, int Level)[] OutfitLadder =
        [
            (223241, 25), (223247, 100), (223249, 150), (223251, 200), (223253, 207), (223255, 213), (223257, 217)
        ];

        /// <summary>
        /// Trader Umbral Wrangler team skill buff ladder (merc level >= QL). Self-cast wrapper (1 to 4 h) whose
        /// TeamCastNano re-pulses the skill buff to the team every 2 min.
        /// </summary>
        internal static readonly (int Id, int Level)[] UmbralWranglerLadder =
        [
            (235271, 25), (235273, 55), (235275, 80), (235277, 105), (235279, 127), (235281, 149),
            (235283, 170), (235285, 189), (235287, 199), (235289, 204), (235291, 217)
        ];

        /// <summary>Outfit pulse children, never cast directly.</summary>
        static readonly int[] OutfitChildren = [216580, 216581, 216582, 216583, 216584, 216585, 216586];

        static readonly HashSet<int> RoleOnlyNanoIds = BuildRoleOnly();

        static HashSet<int> BuildRoleOnly()
        {
            var ids = new HashSet<int>();
            foreach ((int id, _) in SingleTauntLadder)
                ids.Add(id);
            foreach ((int id, _) in MongoLadder)
                ids.Add(id);
            foreach ((int id, _) in NanoDeltaLadder)
                ids.Add(id);
            foreach ((int id, _) in OutfitLadder)
                ids.Add(id);
            foreach ((int id, _) in UmbralWranglerLadder)
                ids.Add(id);
            foreach (int id in OutfitChildren)
                ids.Add(id);
            // Mongo area children and perk/skill-item taunts are never cast directly.
            foreach (int id in new[] { 100193, 100194, 100195, 100196, 270788, 301290, 227152 })
                ids.Add(id);
            return ids;
        }

        /// <summary>Taunts, Mongo, nano-delta, Outfit and Umbral Wrangler ids only the role code casts (never the generic loop).</summary>
        internal static bool IsRoleOnlyNano(int nanoId) => RoleOnlyNanoIds.Contains(nanoId);

        /// <summary>The highest rung with level >= its gate; 0 when the merc is below the first gate.</summary>
        internal static int PickRung((int Id, int Level)[] ladder, int level)
        {
            int pick = 0;
            foreach ((int id, int need) in ladder)
            {
                if (level >= need)
                    pick = id;
            }

            return pick;
        }

        readonly Dictionary<int, DateTime> _nanoReadyUtc = new();
        readonly List<Character> _party = new();
        readonly List<(NpcCharacter Mob, Character Victim)> _engaged = new();
        DateTime _nextRoleUtc;
        DateTime _mongoRefreshUtc;
        string? _mercKey;
        bool _mercKeyResolved;

        string? MercKey
        {
            get
            {
                if (!_mercKeyResolved)
                {
                    _mercKey = Npc.Pet?.Hash is string hash ? Mercs.ByHash(hash)?.Key : null;
                    _mercKeyResolved = Npc.Pet != null;
                }

                return _mercKey;
            }
        }

        bool IsReady(int nanoId, DateTime nowUtc)
            => !_nanoReadyUtc.TryGetValue(nanoId, out DateTime ready) || nowUtc >= ready;

        void Cooldown(int nanoId, DateTime nowUtc, double seconds)
        {
            if (seconds > 0)
                _nanoReadyUtc[nanoId] = nowUtc.AddSeconds(seconds);
        }

        int MercLevel => Math.Max(1, Npc.Stats.GetOrOne(CharacterStat.Level));

        /// <summary>This merc's nano QL cap (level + tier bonus, set at spawn as NanoCastQuality); its level if unset.</summary>
        int NanoCap => Npc.NanoCastQuality > 1 ? Npc.NanoCastQuality : MercLevel;

        /// <summary>
        /// Single-id nanos must be at or under the QL cap. QL pairs (two Relations) always pass: NanoCastQuality already
        /// interpolates them at the cap.
        /// </summary>
        bool WithinQlCap(NanoSpell spell)
            => spell.Relations is { Count: 2 } || EstimatedNanoQl(spell) <= NanoCap;

        /// <summary>Skill requirement -> QL points (Umbral Wrangler / Outfit lines from auno).</summary>
        static readonly (int Skill, int Ql)[] SkillToQl =
        [
            (0, 1), (19, 1), (152, 25), (326, 55), (496, 80), (666, 105), (809, 127), (885, 149), (962, 170),
            (1026, 189), (1052, 199), (1194, 204), (1650, 217), (2500, 300)
        ];

        /// <summary>
        /// A nano's QL estimated from its ToUse requirements: the largest nano-skill requirement mapped through
        /// <see cref="SkillToQl"/>, or its level requirement, whichever is higher. items.dat Quality on nanos is not the QL.
        /// </summary>
        internal static int EstimatedNanoQl(ItemTemplate template)
        {
            int skill = 0;
            int level = 0;
            foreach (ItemAction action in template.Actions)
            {
                if (action.ActionType != 3)
                    continue;
                foreach (ItemRequirement req in action.Requirements)
                {
                    if ((int)req.Operator != 2)
                        continue;
                    int stat = (int)req.StatNumber;
                    int value = (int)req.Value;
                    if (stat is 122 or 127 or 128 or 129 or 130 or 131)
                        skill = Math.Max(skill, value + 1);
                    else if (stat == (int)CharacterStat.Level)
                        level = Math.Max(level, value + 1);
                }
            }

            int ql = 1;
            for (int i = 1; i < SkillToQl.Length; i++)
            {
                (int s0, int q0) = SkillToQl[i - 1];
                (int s1, int q1) = SkillToQl[i];
                if (skill <= s1)
                {
                    ql = q0 + (int)Math.Round((double)(skill - s0) * (q1 - q0) / Math.Max(1, s1 - s0));
                    break;
                }

                ql = q1;
            }

            return Math.Max(Math.Max(1, ql), level);
        }

        /// <summary>Runs this merc's role. True when it started a cast.</summary>
        bool TryMercRoles(DateTime nowUtc)
        {
            string? key = MercKey;
            if (key == null || Npc.Pet is { Mode: PetMode.Wait })
                return false;

            BuildParty();
            bool hasHeals = HasAnyHeal();

            // Anyone about to die: a healer acts before anything else.
            if (hasHeals && LowestPartyPercent(out _) < 35 && TryHealRole(key, nowUtc))
                return true;

            if (key == "enforcer" && TryTauntRole(nowUtc))
                return true;

            if (hasHeals && TryHealRole(key, nowUtc))
                return true;

            if (key == "nanotechnician" && TryNanoDeltaRole(nowUtc))
                return true;

            if (key == "trader" && (TryTeamWrapperRole(OutfitLadder, "outfit", nowUtc)
                                     || TryTeamWrapperRole(UmbralWranglerLadder, "umbral-wrangler", nowUtc)))
                return true;

            return false;
        }

        // ---------------------------------------------------------------- party

        /// <summary>The hiring player, that player's teammates here, their mercs and the mercs' pets, and this merc.</summary>
        void BuildParty()
        {
            _party.Clear();
            _party.Add(Npc);
            if (CombatRules.SideOf(Npc) is not Player owner || owner.Playfield == null)
                return;

            AddPlayerAndPets(owner);
            Playfield? playfield = owner.Playfield;
            TeamService? teams = playfield.GetService<TeamService>();
            PlayfieldManager? manager = playfield.GetService<PlayfieldManager>();
            TeamSnapshot? team = teams?.GetTeam(owner);
            if (team == null || manager == null)
                return;

            for (int i = 0; i < team.MemberIds.Count; i++)
            {
                if (manager.FindPlayer(team.MemberIds[i], out Player? mate) && mate != null
                    && !ReferenceEquals(mate, owner) && ReferenceEquals(mate.Playfield, playfield))
                    AddPlayerAndPets(mate);
            }
        }

        void AddPlayerAndPets(Player player)
        {
            AddMember(player);
            foreach (NpcCharacter pet in player.OwnedPets.All)
            {
                AddMember(pet);
                foreach (NpcCharacter sub in pet.OwnedPets.All)
                    AddMember(sub);
            }
        }

        void AddMember(Character member)
        {
            if (member.IsDead || !ReferenceEquals(member.Playfield, Npc.Playfield) || _party.Contains(member))
                return;
            if (Npc.GetEdgeDistanceTo(member) > DoctorPresenceMeters)
                return;
            _party.Add(member);
        }

        int LowestPartyPercent(out Character? lowest)
        {
            lowest = null;
            int best = 101;
            foreach (Character member in _party)
            {
                int percent = HealthPercent(member);
                if (percent < best)
                {
                    best = percent;
                    lowest = member;
                }
            }

            return best;
        }

        int CountBelow(int percent)
        {
            int count = 0;
            foreach (Character member in _party)
            {
                if (HealthPercent(member) < percent)
                    count++;
            }

            return count;
        }

        // ---------------------------------------------------------------- heals

        bool HasAnyHeal()
        {
            foreach (int nanoId in Npc.UploadedNanoIds)
            {
                // Mongo's self-heal does not make an Enforcer a healer: role-only nanos never count.
                if (!IsRoleOnlyNano(nanoId) && Resolve(nanoId) is NanoSpell spell && !spell.IsHostile && WithinQlCap(spell)
                    && (IsHeal(spell) || IsTeamHeal(spell)))
                    return true;
            }

            return false;
        }

        /// <summary>A Doctor merc of this party, other than this one, that is alive and near.</summary>
        NpcCharacter? FindDoctor()
        {
            foreach (Character member in _party)
            {
                if (member is NpcCharacter npc && !ReferenceEquals(npc, Npc) && !npc.IsDead
                    && npc.Pet?.Hash is string hash && Mercs.ByHash(hash) is { Key: "doctor" })
                    return npc;
            }

            return null;
        }

        /// <summary>
        /// The Doctor is struggling: someone is under 45%, two or more are under 65%, or someone is under 60%
        /// while the Doctor is busy casting or recharging.
        /// </summary>
        bool IsDoctorStruggling(NpcCharacter doctor, DateTime nowUtc)
        {
            int lowest = LowestPartyPercent(out _);
            if (lowest < 45 || CountBelow(65) >= 2)
                return true;
            return lowest < 60 && (doctor.IsCastingNano || doctor.IsInNanoRecharge(nowUtc));
        }

        bool TryHealRole(string key, DateTime nowUtc)
        {
            int healBelow;
            bool primary = key == "doctor";
            if (primary)
            {
                healBelow = PrimaryHealBelowPercent;
            }
            else
            {
                NpcCharacter? doctor = FindDoctor();
                if (doctor == null)
                    healBelow = SecondaryHealBelowPercent;
                else if (IsDoctorStruggling(doctor, nowUtc))
                    healBelow = StrugglingHealBelowPercent;
                else
                    return false;
            }

            // Team heal when several are hurt (Doctor: 3 under 80 or 2 under 70; others: 3 under their line).
            int teamHurt = primary ? Math.Max(CountBelow(80) >= 3 ? 3 : 0, CountBelow(70)) : CountBelow(healBelow);
            if (teamHurt >= (primary ? 2 : 3) && TryTeamHeal(nowUtc))
                return true;

            // Lowest health member first (player, mercs, pets, self).
            Character? target = null;
            int lowest = healBelow;
            foreach (Character member in _party)
            {
                int percent = HealthPercent(member);
                if (percent < lowest)
                {
                    lowest = percent;
                    target = member;
                }
            }

            return target != null && TryHealTarget(target, nowUtc);
        }

        bool TryTeamHeal(DateTime nowUtc)
        {
            List<int> nanos = Npc.UploadedNanoIds;
            for (int i = nanos.Count - 1; i >= 0; i--)
            {
                int nanoId = nanos[i];
                if (IsRoleOnlyNano(nanoId) || !IsReady(nanoId, nowUtc) || Resolve(nanoId) is not NanoSpell spell || spell.IsHostile || !IsTeamHeal(spell) || !WithinQlCap(spell))
                    continue;
                if (CostsCasterHealth(spell) && HealthPercent(Npc) < CasterCostHealFloorPercent)
                    continue;
                if (!NanoRuntime.CanStartCast(Npc, spell, Npc, nowUtc))
                    continue;
                if (RoleCast(spell, Npc, nowUtc, "team-heal", 8))
                    return true;
            }

            return false;
        }

        /// <summary>The strongest single-target heal (last uploaded first) that can land on <paramref name="target"/>.</summary>
        bool TryHealTarget(Character target, DateTime nowUtc)
        {
            bool self = ReferenceEquals(target, Npc);
            List<int> nanos = Npc.UploadedNanoIds;
            for (int i = nanos.Count - 1; i >= 0; i--)
            {
                int nanoId = nanos[i];
                if (IsRoleOnlyNano(nanoId) || !IsReady(nanoId, nowUtc) || Resolve(nanoId) is not NanoSpell spell || spell.IsHostile
                    || !IsHeal(spell) || IsTeamHeal(spell) || !WithinQlCap(spell))
                    continue;
                if (CostsCasterHealth(spell) && (self || HealthPercent(Npc) < CasterCostHealFloorPercent))
                    continue;
                if (!self && !CanApplyOnFriendly(spell))
                    continue;
                if (spell.DurationCentiseconds > 0 && HasNano(target, spell))
                    continue;
                if (!self && (!NpcSupportRules.CanSupport(Npc, target) || !InRange(spell, target, DefaultSupportRange)))
                    continue;
                if (!NpcSupportRules.MayNpcLand(Npc, target, spell) || !NanoRuntime.CanStartCast(Npc, spell, target, nowUtc))
                    continue;
                if (RoleCast(spell, target, nowUtc, "heal", 4))
                    return true;
            }

            return false;
        }

        /// <summary>A TeamCastNano (or AreaCastNano) wrapper whose child heals Health.</summary>
        bool IsTeamHeal(NanoSpell spell)
        {
            if (!spell.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                return false;

            for (int i = 0; i < spells.Count; i++)
            {
                if ((spells[i].Is(FunctionType.TeamCastNano) || spells[i].Is(FunctionType.AreaCastNano))
                    && spells[i].TryReadInt(0, out int childId) && childId != spell.Id
                    && Resolve(childId) is NanoSpell child && !child.IsHostile
                    && NpcSupportRules.IsHealthHeal(child))
                    return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- taunts

        bool TryTauntRole(DateTime nowUtc)
        {
            CollectEngagedMobs();
            if (_engaged.Count == 0)
                return false;

            int level = NanoCap;
            int inMongoReach = 0;
            bool looseInReach = false;
            foreach ((NpcCharacter mob, Character victim) in _engaged)
            {
                if (Npc.GetEdgeDistanceTo(mob) > MongoReachMeters)
                    continue;
                inMongoReach++;
                if (!ReferenceEquals(victim, Npc))
                    looseInReach = true;
            }

            if (inMongoReach >= 2 && (looseInReach || nowUtc >= _mongoRefreshUtc))
            {
                int mongoId = PickRung(MongoLadder, level);
                if (mongoId != 0 && IsReady(mongoId, nowUtc) && Resolve(mongoId) is NanoSpell mongo
                    && NanoRuntime.CanStartCast(Npc, mongo, Npc, nowUtc)
                    && RoleCast(mongo, Npc, nowUtc, "mongo", 8))
                {
                    _mongoRefreshUtc = nowUtc.AddSeconds(MongoRefreshSeconds);
                    return true;
                }
            }

            int tauntId = PickRung(SingleTauntLadder, level);
            if (tauntId == 0 || !IsReady(tauntId, nowUtc) || Resolve(tauntId) is not NanoSpell taunt)
                return false;

            // A mob hitting someone else: the one whose victim is lowest on health first.
            NpcCharacter? pick = null;
            int pickPercent = int.MaxValue;
            foreach ((NpcCharacter mob, Character victim) in _engaged)
            {
                if (ReferenceEquals(victim, Npc))
                    continue;
                int percent = HealthPercent(victim);
                if (percent >= pickPercent || !CanTaunt(taunt, mob, nowUtc))
                    continue;
                pick = mob;
                pickPercent = percent;
            }

            return pick != null && RoleCast(taunt, pick, nowUtc, "taunt", 3);
        }

        bool CanTaunt(NanoSpell taunt, NpcCharacter mob, DateTime nowUtc)
            => InRange(taunt, mob, NpcAiRules.NearbyRange)
               && NpcSupportRules.MayNpcLand(Npc, mob, taunt)
               && NanoRuntime.CanStartCast(Npc, taunt, mob, nowUtc);

        /// <summary>Hostile mobs near the Enforcer that are fighting this merc or anyone on its side.</summary>
        void CollectEngagedMobs()
        {
            _engaged.Clear();
            Playfield? playfield = Npc.Playfield;
            if (playfield == null)
                return;

            DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
            playfield.GetRequiredService<PlayfieldLocality>().CollectNearby(Npc, _nearby);
            try
            {
                foreach (Dynel dynel in _nearby)
                {
                    if (dynel is not NpcCharacter mob || mob.IsDead || ReferenceEquals(mob, Npc) || !mob.Attackable)
                        continue;
                    if (mob.FightingTarget.Instance == 0 || Npc.GetEdgeDistanceTo(mob) > TauntScanMeters)
                        continue;
                    if (!CombatRules.CanAttack(Npc, mob) || NpcSupportRules.CanSupport(Npc, mob))
                        continue;
                    if (!registry.TryGet(mob.FightingTarget, out Dynel? hit) || hit is not Character victim || victim.IsDead)
                        continue;
                    if (!ReferenceEquals(victim, Npc) && !NpcSupportRules.CanSupport(Npc, victim))
                        continue;
                    _engaged.Add((mob, victim));
                }
            }
            finally
            {
                _nearby.Clear();
            }
        }

        // ---------------------------------------------------------------- team upkeep

        /// <summary>
        /// NT: keep the highest nano-delta buff the merc's level allows on every party member that lacks it:
        /// players (owner first, then teammates) at any time; mercs and self only out of combat. Merc pets are skipped.
        /// Maintenance only: it checks whether the buff is running, never how low anyone's nano is.
        /// </summary>
        bool TryNanoDeltaRole(DateTime nowUtc)
        {
            int deltaId = PickRung(NanoDeltaLadder, NanoCap);
            if (deltaId == 0 || !IsReady(deltaId, nowUtc) || Resolve(deltaId) is not NanoSpell delta)
                return false;

            bool fighting = PartyInCombat();
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (Character member in _party)
                {
                    bool player = member is Player;
                    if (pass == 0 ? !player : player)
                        continue;
                    if (!player && (fighting || !IsMercOrSelf(member)))
                        continue;
                    if (HasNano(member, delta))
                        continue;
                    bool self = ReferenceEquals(member, Npc);
                    if (!self && (!NpcSupportRules.CanSupport(Npc, member) || !InRange(delta, member, DefaultSupportRange)))
                        continue;
                    if (!NpcSupportRules.MayNpcLand(Npc, member, delta) || !NanoRuntime.CanStartCast(Npc, delta, member, nowUtc))
                        continue;
                    if (RoleCast(delta, member, nowUtc, "nano-delta", 2))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Trader team wrappers (Outfit, Umbral Wrangler): keep the highest the merc's level allows running on the merc;
        /// its TeamCastNano pulses reach the player and teammates within 40 m, so a member who was missing it gets it on
        /// the next pulse. Recast as soon as it expires. Below the first gate nothing is cast.
        /// </summary>
        bool TryTeamWrapperRole((int Id, int Level)[] ladder, string role, DateTime nowUtc)
        {
            int id = PickRung(ladder, NanoCap);
            if (id == 0 || !IsReady(id, nowUtc) || Resolve(id) is not NanoSpell wrapper)
                return false;
            if (HasNano(Npc, wrapper) || !NanoRuntime.CanStartCast(Npc, wrapper, Npc, nowUtc))
                return false;
            return RoleCast(wrapper, Npc, nowUtc, role, 5);
        }

        bool IsMercOrSelf(Character member)
            => ReferenceEquals(member, Npc)
               || (member is NpcCharacter npc && npc.Pet?.Hash is string hash && Mercs.ByHash(hash) != null);

        /// <summary>This merc or any party player is fighting.</summary>
        bool PartyInCombat()
        {
            if (Npc.FightingTarget.Instance != 0)
                return true;
            foreach (Character member in _party)
            {
                if (member is Player && member.FightingTarget.Instance != 0)
                    return true;
            }

            return false;
        }
        // ---------------------------------------------------------------- cast

        bool RoleCast(NanoSpell spell, Character target, DateTime nowUtc, string role, double cooldownSeconds)
        {
            if (NanoRuntime.TryStartCast(Npc, spell.Id, target.Identity, nowUtc) != NanoCastRefusal.None)
                return false;

            Cooldown(spell.Id, nowUtc, cooldownSeconds);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Merc role cast npc={0} role={1} nano={2} {3} target={4} self={5} targetHp={6}% npcHp={7}%",
                    Npc.Identity.Instance,
                    role,
                    spell.Id,
                    spell.Name,
                    target.Identity.Instance,
                    ReferenceEquals(target, Npc),
                    HealthPercent(target),
                    HealthPercent(Npc)));
            return true;
        }
    }
}
