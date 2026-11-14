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
    /// only when there is no Doctor merc or the Doctor is struggling; an NT restores the lowest player's nano.
    /// Taunt and nano-restore ladders are picked by merc level, since mercs skip caster requirements.
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

        /// <summary>An NT restores a player's nano under this.</summary>
        public const int NanoRestoreBelowPercent = 60;

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

        /// <summary>NT nano restore ladder (Humidity Extractor line, merc level).</summary>
        internal static readonly (int Id, int Level)[] NanoRestoreLadder =
        [
            (90400, 1), (90405, 18), (90404, 38), (90403, 74), (90402, 93), (90401, 137), (90406, 157)
        ];

        static readonly HashSet<int> RoleOnlyNanoIds = BuildRoleOnly();

        static HashSet<int> BuildRoleOnly()
        {
            var ids = new HashSet<int>();
            foreach ((int id, _) in SingleTauntLadder)
                ids.Add(id);
            foreach ((int id, _) in MongoLadder)
                ids.Add(id);
            foreach ((int id, _) in NanoRestoreLadder)
                ids.Add(id);
            // Mongo area children and perk/skill-item taunts are never cast directly.
            foreach (int id in new[] { 100193, 100194, 100195, 100196, 270788, 301290, 227152 })
                ids.Add(id);
            return ids;
        }

        /// <summary>Taunts, Mongo and nano-restore ids only the role code casts (never the generic loop).</summary>
        internal static bool IsRoleOnlyNano(int nanoId) => RoleOnlyNanoIds.Contains(nanoId);

        internal static int PickRung((int Id, int Level)[] ladder, int level)
        {
            int pick = ladder[0].Id;
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

            if (key == "nanotechnician" && TryNanoRestoreRole(nowUtc))
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
                if (!IsRoleOnlyNano(nanoId) && Resolve(nanoId) is NanoSpell spell && !spell.IsHostile && (IsHeal(spell) || IsTeamHeal(spell)))
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
                if (IsRoleOnlyNano(nanoId) || !IsReady(nanoId, nowUtc) || Resolve(nanoId) is not NanoSpell spell || spell.IsHostile || !IsTeamHeal(spell))
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
                    || !IsHeal(spell) || IsTeamHeal(spell))
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

            int level = MercLevel;
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
                if (IsReady(mongoId, nowUtc) && Resolve(mongoId) is NanoSpell mongo
                    && NanoRuntime.CanStartCast(Npc, mongo, Npc, nowUtc)
                    && RoleCast(mongo, Npc, nowUtc, "mongo", 8))
                {
                    _mongoRefreshUtc = nowUtc.AddSeconds(MongoRefreshSeconds);
                    return true;
                }
            }

            int tauntId = PickRung(SingleTauntLadder, level);
            if (!IsReady(tauntId, nowUtc) || Resolve(tauntId) is not NanoSpell taunt)
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

        // ---------------------------------------------------------------- nano restore

        bool TryNanoRestoreRole(DateTime nowUtc)
        {
            int restoreId = PickRung(NanoRestoreLadder, MercLevel);
            if (!IsReady(restoreId, nowUtc) || Resolve(restoreId) is not NanoSpell restore)
                return false;

            Player? target = null;
            int lowest = NanoRestoreBelowPercent;
            foreach (Character member in _party)
            {
                if (member is not Player player)
                    continue;
                int percent = NanoPercent(player);
                if (percent >= lowest || !InRange(restore, player, DefaultSupportRange)
                    || !NpcSupportRules.MayNpcLand(Npc, player, restore)
                    || !NanoRuntime.CanStartCast(Npc, restore, player, nowUtc))
                    continue;
                lowest = percent;
                target = player;
            }

            return target != null && RoleCast(restore, target, nowUtc, "nano-restore", 8);
        }

        static int NanoPercent(Character character)
        {
            int max = character.Stats.GetOrZero(CharacterStat.MaxNanoEnergy);
            if (max <= 0)
                return 100;
            return (int)(100L * Math.Max(0, character.Stats.GetOrZero(CharacterStat.CurrentNano)) / max);
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
