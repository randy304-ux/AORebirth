namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Summons, commands and dismisses pets on one playfield. A pet is an ordinary NPC spawned from its mob hash
    /// with PetMaster (196) set to its owner before it is first sent, so observers get it as a pet
    /// (SimpleCharFullUpdate IsPet); its owner's client is told with AddPet and RemovePet, and the owner's Pets
    /// stat (251) carries one bit per occupied pet slot. Owners keep one pet per slot. Every call runs on this
    /// playfield's thread, where the owner and its pets live.
    /// </summary>
    public sealed class PetService
    {
        /// <summary>A player's pet farther than this from its owner rejoins the owner at once.</summary>
        public const double MaxPlayerPetDistance = 50.0;

        /// <summary>Orders <see cref="Command"/> honors. The client sends these codes from /pet. Other codes are ignored.</summary>
        public static readonly (PetCommandCode Code, string Syntax, string Summary)[] Orders =
        [
            (PetCommandCode.Follow, "/pet follow", "The pet follows you."),
            (PetCommandCode.Behind, "/pet behind", "The pet follows behind you."),
            (PetCommandCode.Guard, "/pet guard", "The pet guards. A doctor merc treats this as its heal duty."),
            (PetCommandCode.Wait, "/pet wait", "The pet stays where it is."),
            (PetCommandCode.Attack, "/pet attack", "The pet attacks your current target."),
            (PetCommandCode.Heal, "/pet heal", "A heal pet heals its target."),
            (PetCommandCode.Terminate, "/pet terminate", "Dismisses that pet."),
            (PetCommandCode.Report, "/pet report", "Reports health, nano, and position for that pet.")
        ];

        readonly Playfield _playfield;
        readonly IGameData _gameData;
        readonly IZoneLogger _logger;
        readonly MercUnlockService? _mercUnlocks;

        public PetService(Playfield playfield, IGameData gameData, IZoneLogger logger, MercUnlockService? mercUnlocks = null)
        {
            _playfield = playfield ?? throw new ArgumentNullException(nameof(playfield));
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mercUnlocks = mercUnlocks;
        }

        PetTypeCatalog Types => PetTypeCatalog.For(_gameData.RootPath);

        /// <summary>
        /// FunctionType.SummonPet: summons <paramref name="hash"/> at <paramref name="level"/> for
        /// <paramref name="owner"/>, lasting <paramref name="durationSeconds"/> (0 or less: until dismissed).
        /// A pet already in the same slot is dismissed first.
        /// </summary>
        public NpcCharacter? Summon(Character owner, string hash, int level, int durationSeconds,
            IReadOnlyList<ItemRequirement>? summonRequirements = null)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (string.IsNullOrWhiteSpace(hash) || owner.IsDead || !ReferenceEquals(owner.Playfield, _playfield))
                return null;
            if (owner is NpcCharacter { PetOwner: not null })
                return null;

            DateTime? expires = durationSeconds > 0 ? DateTime.UtcNow.AddSeconds(durationSeconds) : null;
            int type = Types.TypeOf(hash.Trim());
            return Spawn(owner, hash.Trim(), type, level, expires, DefaultMode(type), healthPercent: 100, nanoPercent: 100,
                summonRequirements ?? []);
        }

        NpcCharacter? Spawn(Character owner, string hash, int type, int level, DateTime? expires, PetMode mode, int healthPercent,
            int nanoPercent, IReadOnlyList<ItemRequirement> summonRequirements)
        {
            if (!_gameData.TryResolveMobTemplate(hash, level > 0 ? level : null, out MobTemplate template)
                || !NpcTemplateValidation.CanSpawn(template))
            {
                _logger.Warn("SummonPet: mob hash " + hash + " does not resolve to a spawnable template");
                return null;
            }

            if (owner.OwnedPets.InSlotOf(type) is NpcCharacter previous)
                Dismiss(previous, "replaced");

            var controller = new PetController(owner, type, hash, level, expires, summonRequirements);
            if (owner is Player && mode is PetMode.Follow or PetMode.Guard)
                controller.Order(mode);

            int index = owner.OwnedPets.Count;
            Vector3 at = _playfield.SnapNpcSpawn(PetFormation.PointFor(owner, index));
            // A formation spot with no floor under it (behind a dungeon wall, past a ledge) would leave the pet
            // in the void at the owner's height; it starts on the owner's own floor instead.
            if (!_playfield.TrySnapFeetToFloor(at, out _) && _playfield.TrySnapFeetToFloor(owner.Position, out Vector3 ownerFloor))
                at = ownerFloor;
            NpcCharacter pet = _playfield.GetRequiredService<SpawnService>().SpawnMob(
                template,
                at,
                owner.Rotation,
                level > 0 ? level : null,
                SpawnSource.Summoned,
                spawnHash: hash,
                configure: npc =>
                {
                    npc.Stats.Set(CharacterStat.PetMaster, owner.Identity.Instance);
                    npc.Stats.Set(CharacterStat.PetType, type);
                    npc.Stats.Set(CharacterStat.Side, owner.Stats.GetOrZero(CharacterStat.Side, StatDetail.Base));
                    // Battle station: a pet/merc wears its owner's BS team so the client's tab skips it like a teammate.
                    int ownerBattleSide = owner.Stats.GetOrZero(CharacterStat.BattlestationSide);
                    if (ownerBattleSide != 0)
                        npc.Stats.Set(CharacterStat.BattlestationSide, ownerBattleSide);
                    MatchOwnerRunSpeed(npc, owner);
                    int petLevel = level > 0 ? level : npc.Stats.GetOrOne(CharacterStat.Level);
                    // Con color reads SCFU Level. Force the resolved pet/merc level (and matching TitleLevel)
                    // onto the body so a template TitleLevel=1 band or a level-0 summon never leaves Level at 1/0.
                    npc.Stats.Set(CharacterStat.Level, petLevel, StatDetail.Base, dirty: true);
                    npc.Stats.Set(CharacterStat.TitleLevel, Character.TitleLevelFor(petLevel), StatDetail.Base, dirty: true);
                    controller.Heals = IsHealPet(type) || Mercs.Heals(_gameData.RootPath, hash);
                    npc.BindPet(controller);
                    Mercs.Kind? mercKind = Mercs.ByHash(hash);
                    Mercs.Tier tier = mercKind != null && owner is Player tierOwner
                        ? Mercs.GetTier(tierOwner, mercKind)
                        : Mercs.Tier.Basic;
                    int nanoLevel = mercKind != null ? Mercs.PowerLevel(petLevel, tier) : petLevel;
                    if (mercKind != null)
                    {
                        // Own-profession spells/buffs/specials; stronger lists at higher tiers (basic < elite < champion < twink).
                        npc.UploadedNanoIds.AddRange(Mercs.TierNanosFor(mercKind, tier));
                    }
                    else
                    {
                        npc.UploadedNanoIds.AddRange(Mercs.NanosFor(_gameData.RootPath, hash, nanoLevel));
                    }

                    // A heal pet (the MP's Salvinous) has no merc profession of its own: it casts the doctor's heal line.
                    if (npc.UploadedNanoIds.Count == 0 && IsHealPet(type)
                        && Mercs.Find("doctor") is { } docKind)
                    {
                        npc.UploadedNanoIds.AddRange(Mercs.TierNanosFor(docKind, Mercs.Tier.Basic));
                    }

                    if (mercKind != null)
                    {
                        int gearQl = Mercs.GearQuality(petLevel, tier);
                        DressMerc(npc, mercKind, nanoLevel, gearQl, owner.Stats.GetOrZero(CharacterStat.Side, StatDetail.Base), tier);
                        if (gearQl != petLevel)
                            npc.RaiseCombatWeaponQuality(gearQl, _logger);
                        ApplyMercTier(npc, tier);
                        if (tier != Mercs.Tier.Basic)
                        {
                            npc.NanoCastQuality = gearQl;
                            npc.Name = npc.Name + " (" + Mercs.TierName(tier) + ")";
                        }
                    }
                    if (healthPercent is > 0 and < 100)
                    {
                        int max = npc.Stats.GetOrZero(CharacterStat.MaxHealth);
                        if (max > 0)
                            npc.Stats.Set(CharacterStat.Health, Math.Max(1, (int)(max * (long)healthPercent / 100)));
                    }

                    if (nanoPercent is >= 0 and < 100)
                    {
                        int max = npc.Stats.GetOrZero(CharacterStat.MaxNanoEnergy);
                        if (max > 0)
                            npc.Stats.Set(CharacterStat.CurrentNano, (int)(max * (long)nanoPercent / 100));
                    }
                },
                attachDefaultBrain: false);
            NpcBrain.CreatePet(pet, controller);

            owner.OwnedPets.Add(pet);
            PublishPetsStat(owner);
            if (owner is Player player)
                SendAddPet(player, pet);
            RefreshOverEquip(pet);
            if (owner is Player mercOwner && Mercs.ByHash(hash) is Mercs.Kind mercPetKind)
            {
                EnsureMercPets(pet, Mercs.PowerLevel(pet.Stats.GetOrOne(CharacterStat.Level), Mercs.GetTier(mercOwner, mercPetKind)));
                // Dungeon merc rename / NT nuke filter after hire or TickMercs revive.
                _mercUnlocks?.ConfigureSummonedMerc(mercOwner, pet);
            }

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Summoned pet id={0} hash={1} type={2} level={3} owner={4} expires={5}",
                pet.Identity.Instance, hash, type, pet.Stats.GetOrZero(CharacterStat.Level), owner.Identity.Instance,
                expires?.ToString("o", CultureInfo.InvariantCulture) ?? "never"));
            return pet;
        }

        /// <summary>
        /// The merc's own pets (Crat worker-droid, Engineer robot, MP attack/mezz/heal): owned by the merc, so they
        /// assist it and leave with it on dismiss, death and zone change (see <see cref="DismissMercPets"/>).
        /// </summary>
        void EnsureMercPets(NpcCharacter merc, int level)
        {
            if (Mercs.ByHash(merc.Pet?.Hash) is not Mercs.Kind kind)
                return;
            foreach (Mercs.MercPetSpec spec in Mercs.PetsFor(_gameData.RootPath, kind, level))
            {
                bool present = false;
                foreach (NpcCharacter sub in merc.OwnedPets.All)
                {
                    if (sub.Pet != null && !sub.IsDead && string.Equals(sub.Pet.Hash, spec.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        present = true;
                        break;
                    }
                }

                if (!present)
                    SpawnMercPet(merc, spec.Hash, level, spec.Type);
            }
        }

        void SpawnMercPet(NpcCharacter merc, string hash, int level, int type)
        {
            if (type <= 0)
                type = Types.TypeOf(hash);
            NpcCharacter? pet = Spawn(merc, hash, type, level, null, PetMode.Assist, 100, 100, []);
            _logger.Info(string.Format(CultureInfo.InvariantCulture, "Merc pet hash={0} type={1} merc={2} spawned={3}",
                hash, type, merc.Identity.Instance, pet != null));
        }

        /// <summary>Removes the pets a merc (or any pet) owns before the merc itself leaves.</summary>
        void DismissMercPets(NpcCharacter pet, string reason)
        {
            if (pet.OwnedPets.Count == 0)
                return;
            pet.OwnedPets.Stash.Clear();
            foreach (NpcCharacter sub in pet.OwnedPets.All.ToArray())
                Dismiss(sub, reason);
        }

        /// <summary>GameData/Mercs.json toggle for hiring mercenaries.</summary>
        public bool MercsEnabled => Mercs.Enabled(_gameData.RootPath);

        /// <summary>
        /// Hires a mercenary for <paramref name="owner"/> at <paramref name="level"/>: a pet of the merc's own slot that
        /// guards its owner until dismissed. A merc of the same profession already hired is replaced.
        /// </summary>
        public NpcCharacter? HireMerc(Player owner, Mercs.Kind kind, int level, Mercs.Tier tier = Mercs.Tier.Basic)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(kind);
            if (!MercsEnabled || owner.IsDead || !ReferenceEquals(owner.Playfield, _playfield))
                return null;

            owner.OwnedPets.MercReviveUtc.Remove(kind.Key);
            Mercs.Tier previous = Mercs.GetTier(owner, kind);
            Mercs.SetTier(owner, kind, tier);
            int spawnLevel = Mercs.BodyLevel(level);
            NpcCharacter? merc = Spawn(owner, kind.Hash, kind.PetType, spawnLevel, null, PetMode.Guard, 100, 100, []);
            if (merc == null)
            {
                Mercs.SetTier(owner, kind, previous);
                return null;
            }

            SetContract(owner, kind, true);
            return merc;
        }

        /// <summary>Ends one merc contract and sends that merc home; false when there was no such contract or merc.</summary>
        public bool DismissMerc(Player owner, Mercs.Kind kind)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(kind);
            bool had = HasContract(owner, kind);
            SetContract(owner, kind, false);
            Mercs.SetTier(owner, kind, Mercs.Tier.Basic);
            owner.OwnedPets.MercReviveUtc.Remove(kind.Key);
            owner.OwnedPets.Stash.RemoveAll(stash => string.Equals(stash.Hash, kind.Hash, StringComparison.OrdinalIgnoreCase));
            foreach (NpcCharacter pet in owner.OwnedPets.All.ToArray())
            {
                if (pet.Pet != null && string.Equals(pet.Pet.Hash, kind.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    Dismiss(pet, "merc dismissed");
                    had = true;
                }
            }

            return had;
        }

        public static bool HasContract(Player owner, Mercs.Kind kind)
            => (owner.Stats.GetOrZero(Mercs.ContractsStat, StatDetail.Base) & (1 << kind.Bit)) != 0;

        static void SetContract(Player owner, Mercs.Kind kind, bool on)
        {
            int bits = owner.Stats.GetOrZero(Mercs.ContractsStat, StatDetail.Base);
            int next = on ? bits | (1 << kind.Bit) : bits & ~(1 << kind.Bit);
            if (next != bits)
            {
                owner.Stats.Set(Mercs.ContractsStat, next, StatDetail.Base);
                owner.SaveState.MarkDirty();
            }
        }

        /// <summary>
        /// Armor and hand weapon for <paramref name="tier"/>. Basic uses Mercs.json bands.
        /// Elite/Champion/Twink use verified Ofab / Combined Alien / Beast template ids from <see cref="Mercs.TierArmorFor"/>.
        /// Pieces are minted at <paramref name="itemQuality"/> (tier gear-QL bonus already applied by the caller).
        /// </summary>
        void DressMerc(NpcCharacter npc, Mercs.Kind kind, int armorLevel, int itemQuality, int ownerSide, Mercs.Tier tier)
        {
            Mercs.Catalog catalog = Mercs.Load(_gameData.RootPath);
            npc.WearsArmorStats = catalog.ArmorStats;
            int worn = 0;
            IReadOnlyList<Mercs.GearPiece>? tierArmor = Mercs.TierArmorFor(kind, tier);
            if (tierArmor != null)
            {
                foreach (Mercs.GearPiece piece in tierArmor)
                {
                    if (piece.Low <= 0)
                        continue;
                    if (npc.WearExtra(piece.Low, piece.High > 0 ? piece.High : piece.Low, itemQuality, _logger))
                        worn++;
                }
            }
            else
            {
                foreach (List<int> pair in Mercs.ArmorFor(_gameData.RootPath, kind, armorLevel, ownerSide))
                {
                    if (pair == null || pair.Count == 0)
                        continue;
                    if (npc.WearExtra(pair[0], pair.Count > 1 ? pair[1] : pair[0], itemQuality, _logger))
                        worn++;
                }
            }

            // Held prop (look) and combat weapon (damage + skill type) use the same profession pair.
            // Without ReplaceCombatWeapon, FillEquipment leaves SIW1 (breed hammer) as the swing for every merc.
            int weaponLow = 0;
            int weaponHigh = 0;
            Mercs.GearPiece? tierWeapon = Mercs.TierWeaponFor(kind, tier);
            if (tierWeapon is { } tw && tw.Low > 0)
            {
                weaponLow = tw.Low;
                weaponHigh = tw.High > 0 ? tw.High : tw.Low;
            }
            else if (Mercs.WeaponFor(_gameData.RootPath, kind) is { Count: > 0 } weapon)
            {
                weaponLow = weapon[0];
                weaponHigh = weapon.Count > 1 ? weapon[1] : weapon[0];
            }

            if (weaponLow > 0)
            {
                if (npc.WearExtra(weaponLow, weaponHigh, itemQuality, _logger))
                    worn++;
                npc.ReplaceCombatWeapon(weaponLow, weaponHigh, itemQuality, _logger);
            }

            npc.Rebase();
            int max = npc.Stats.GetOrZero(CharacterStat.MaxHealth);
            if (max > 0)
                npc.Stats.Set(CharacterStat.Health, max);
            int maxNano = npc.Stats.GetOrZero(CharacterStat.MaxNanoEnergy);
            if (maxNano > 0)
                npc.Stats.Set(CharacterStat.CurrentNano, maxNano);
            _logger.Info(string.Format(CultureInfo.InvariantCulture, "Merc dressed hash={0} armorLevel={1} ql={2} side={3} tier={4} pieces={5}",
                kind.Hash, armorLevel, itemQuality, ownerSide, Mercs.TierName(tier), worn));
        }

        /// <summary>
        /// Flat health, damage, ability, AC, and skill adds. The swing itself is the monster weapon, re-minted at
        /// the gear QL before this runs. Basic adds nothing.
        /// </summary>
        static void ApplyMercTier(NpcCharacter npc, Mercs.Tier tier)
        {
            Mercs.Package package = Mercs.PackageFor(tier);
            if (package.IsBasic)
                return;
            AddBase(npc, CharacterStat.MaxHealth, package.HealthAdd);
            AddBase(npc, CharacterStat.MaxDamage, package.DamageAdd);
            AddBase(npc, CharacterStat.MinDamage, package.DamageAdd);
            foreach (CharacterStat skill in TwinkSkills)
                AddBase(npc, skill, package.SkillAdd);
            npc.Rebase();
            int max = npc.Stats.GetOrZero(CharacterStat.MaxHealth);
            if (max > 0)
                npc.Stats.Set(CharacterStat.Health, max);
        }

        /// <summary>Abilities, weapon and nano skills, and AC. Not level, side, or run speed (the owner already sets that).</summary>
        static readonly CharacterStat[] TwinkSkills =
        [
            CharacterStat.Strength, CharacterStat.Agility, CharacterStat.Stamina,
            CharacterStat.Intelligence, CharacterStat.Sense, CharacterStat.Psychic,
            CharacterStat.ProjectileAC, CharacterStat.MeleeAC, CharacterStat.EnergyAC,
            CharacterStat.ChemicalAC, CharacterStat.RadiationAC, CharacterStat.ColdAC,
            CharacterStat.PoisonAC, CharacterStat.FireAC, CharacterStat.NanoAC,
            CharacterStat.MartialArts, CharacterStat.MultiMelee, CharacterStat._1hBlunt,
            CharacterStat._1hEdged, CharacterStat.MeleeEnergy, CharacterStat.Skill2hEdged,
            CharacterStat.Piercing, CharacterStat._2hBlunt, CharacterStat.SharpObject,
            CharacterStat.Grenade, CharacterStat.HeavyWeapons, CharacterStat.Bow,
            CharacterStat.Pistol, CharacterStat.Rifle, CharacterStat.MGSMG,
            CharacterStat.Shotgun, CharacterStat.AssaultRifle,
            CharacterStat.MeleeInit, CharacterStat.RangedInit, CharacterStat.PhysicalInit,
            CharacterStat.FirstAid, CharacterStat.Treatment,
            CharacterStat.MaterialMetamorphosis, CharacterStat.BiologicalMetamorphosis,
            CharacterStat.PsychologicalModification, CharacterStat.MaterialCreation,
            CharacterStat.SpaceTime, CharacterStat.NanoPool,
            CharacterStat.RangedEnergy, CharacterStat.MultiRanged,
            CharacterStat.Brawl, CharacterStat.Dimach, CharacterStat.SneakAttack,
            CharacterStat.FastAttack, CharacterStat.Burst, CharacterStat.NanoCInit,
            CharacterStat.FlingShot, CharacterStat.AimedShot, CharacterStat.BodyDevelopment,
            CharacterStat.FullAuto
        ];

        static void AddBase(NpcCharacter npc, CharacterStat stat, int add)
        {
            if (add == 0)
                return;
            int value = npc.Stats.GetOrZero(stat, StatDetail.Base);
            int next = (int)Math.Min(int.MaxValue, (long)value + add);
            npc.Stats.Set(stat, next, StatDetail.Base, dirty: true);
        }

        /// <summary>
        /// Keeps every pet/merc (and a merc's own pets) on its owner's battle-station team: set when the owner joins
        /// a match, cleared when they leave. Combat already resolves pets through their owner; the client reads this
        /// stat to treat them as teammates, so tab targeting skips them.
        /// </summary>
        public static void SyncBattleSide(Character owner)
        {
            int side = owner.Stats.GetOrZero(CharacterStat.BattlestationSide);
            foreach (NpcCharacter pet in owner.OwnedPets.All)
            {
                if (pet.Stats.GetOrZero(CharacterStat.BattlestationSide) != side)
                {
                    pet.Stats.Set(CharacterStat.BattlestationSide, side, StatDetail.Base, dirty: true);
                    pet.FlushDirtyStats();
                }

                if (pet.OwnedPets.Count > 0)
                    SyncBattleSide(pet);
            }
        }

        /// <summary>
        /// Brings back every contracted merc the owner is missing: on login, after the owner's or the merc's death
        /// (once its revive time is up), and after a zone change that lost it. Runs about once a second per player.
        /// </summary>
        public void TickMercs(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            SyncBattleSide(owner);
            int bits = owner.Stats.GetOrZero(Mercs.ContractsStat, StatDetail.Base);
            if (bits == 0)
                return;
            DateTime now = DateTime.UtcNow;
            OwnedPets roster = owner.OwnedPets;
            if (now < roster.MercCheckUtc)
                return;
            roster.MercCheckUtc = now.AddSeconds(1);
            if (owner.IsDead || owner.ZoneReconnectPending || !ReferenceEquals(owner.Playfield, _playfield) || !MercsEnabled
                || roster.Stash.Count > 0)
                return;

            int level = owner.Stats.GetOrOne(CharacterStat.Level);
            foreach (Mercs.Kind kind in Mercs.Kinds)
            {
                if ((bits & (1 << kind.Bit)) == 0)
                    continue;
                if (roster.MercReviveUtc.TryGetValue(kind.Key, out DateTime due) && now < due)
                    continue;
                int spawnLevel = Mercs.BodyLevel(level);
                NpcCharacter? merc = null;
                foreach (NpcCharacter pet in roster.All)
                {
                    if (pet.Pet != null && !pet.IsDead && string.Equals(pet.Pet.Hash, kind.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        merc = pet;
                        break;
                    }
                }

                if (merc != null)
                {
                    // Merc level must track the owner (BodyLevel). A stale hire-time Level cons grey once the
                    // owner outlevels it; dismiss with "replaced" (no revive timer) and respawn at spawnLevel.
                    int mercLevel = merc.Stats.GetOrOne(CharacterStat.Level);
                    if (mercLevel != spawnLevel)
                    {
                        Dismiss(merc, "replaced");
                        roster.MercReviveUtc.Remove(kind.Key);
                        if (Spawn(owner, kind.Hash, kind.PetType, spawnLevel, null, PetMode.Guard, 100, 100, []) != null)
                            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                                "Merc level-synced hash={0} owner={1} from={2} to={3}",
                                kind.Hash, owner.Identity.Instance, mercLevel, spawnLevel));
                        continue;
                    }

                    EnsureMercPets(merc, Mercs.PowerLevel(merc.Stats.GetOrOne(CharacterStat.Level), Mercs.GetTier(owner, kind)));
                    continue;
                }
                roster.MercReviveUtc.Remove(kind.Key);
                if (Spawn(owner, kind.Hash, kind.PetType, spawnLevel, null, PetMode.Guard, 100, 100, []) != null)
                    _logger.Info(string.Format(CultureInfo.InvariantCulture, "Merc returned hash={0} owner={1}", kind.Hash, owner.Identity.Instance));
            }
        }

        void ScheduleMercRevive(Character owner, PetController controller)
        {
            if (owner is not Player || Mercs.ByHash(controller.Hash) is not Mercs.Kind kind)
                return;
            int seconds = Math.Max(0, Mercs.Load(_gameData.RootPath).ReviveSeconds);
            owner.OwnedPets.MercReviveUtc[kind.Key] = DateTime.UtcNow.AddSeconds(seconds);
        }

        /// <summary>Dismisses every merc <paramref name="owner"/> has; returns how many.</summary>
        public int DismissMercs(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            int count = 0;
            foreach (NpcCharacter pet in owner.OwnedPets.All.ToArray())
            {
                if (pet.Pet != null && Mercs.IsMercHash(pet.Pet.Hash))
                {
                    Dismiss(pet, "merc dismissed");
                    count++;
                }
            }

            owner.OwnedPets.Stash.RemoveAll(stash => Mercs.IsMercHash(stash.Hash));
            owner.OwnedPets.MercReviveUtc.Clear();
            if (owner.Stats.GetOrZero(Mercs.ContractsStat, StatDetail.Base) != 0)
            {
                owner.Stats.Set(Mercs.ContractsStat, 0, StatDetail.Base);
                owner.SaveState.MarkDirty();
            }

            Mercs.ClearTiers(owner);
            return count;
        }
        /// <summary>Removes a pet from the world and from its owner.</summary>
        public void Dismiss(NpcCharacter pet, string reason)
        {
            ArgumentNullException.ThrowIfNull(pet);
            PetController? controller = pet.Pet;
            if (controller == null)
                return;

            DismissMercPets(pet, "owner " + reason);
            Character owner = controller.Owner;
            if (reason != "merc dismissed" && reason != "replaced")
                ScheduleMercRevive(owner, controller);
            bool owned = owner.OwnedPets.Remove(pet);
            pet.Brain?.StopPathing();
            if (pet.Playfield != null)
                pet.Playfield.GetRequiredService<SpawnService>().DespawnNpc(pet);
            pet.UnbindPet();

            if (owned)
            {
                PublishPetsStat(owner);
                if (owner is Player player)
                    SendRemovePet(player, pet);
            }

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Dismissed pet id={0} hash={1} owner={2} reason={3}",
                pet.Identity.Instance, controller.Hash, owner.Identity.Instance, reason));
        }

        /// <summary>Removes every pet <paramref name="owner"/> has, and any it was carrying through a zone change.</summary>
        public void DismissAll(Character owner, string reason)
        {
            ArgumentNullException.ThrowIfNull(owner);
            owner.OwnedPets.Stash.Clear();
            NpcCharacter[] pets = [.. owner.OwnedPets.All];
            foreach (NpcCharacter pet in pets)
                Dismiss(pet, reason);
        }

        /// <summary>A pet died: it leaves its owner (its body is not kept; pets leave no corpse).</summary>
        public void OnPetDied(NpcCharacter pet)
        {
            ArgumentNullException.ThrowIfNull(pet);
            PetController? controller = pet.Pet;
            if (controller == null)
                return;

            DismissMercPets(pet, "owner died");
            Character owner = controller.Owner;
            ScheduleMercRevive(owner, controller);
            if (owner.OwnedPets.Remove(pet))
            {
                PublishPetsStat(owner);
                if (owner is Player player)
                    SendRemovePet(player, pet);
            }
        }

        /// <summary>The owner is leaving this playfield for another: its pets go with it.</summary>
        public void StashForTransfer(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            owner.OwnedPets.Stash.Clear();
            NpcCharacter[] pets = [.. owner.OwnedPets.All];
            DateTime now = DateTime.UtcNow;
            foreach (NpcCharacter pet in pets)
            {
                PetController? controller = pet.Pet;
                if (controller == null || pet.IsDead || controller.HasExpired(now))
                    continue;

                // Pets arrive in the mode they left in. A waiting pet cannot keep its post on another playfield, so it
                // follows; an attack order's target stays behind, so the pet goes back to what it did before it.
                PetMode mode = controller.Mode switch
                {
                    PetMode.Wait => PetMode.Follow,
                    PetMode.Attack => controller.ModeBeforeAttack,
                    _ => controller.Mode
                };
                owner.OwnedPets.Stash.Add(new PetStash(
                    controller.Hash,
                    controller.Type,
                    controller.Level,
                    controller.ExpiresUtc,
                    mode,
                    HealthPercent(pet),
                    NanoPercent(pet),
                    controller.SummonRequirements,
                    SnapshotBuffs(pet, now),
                    SnapshotBuffsFromSource(owner, pet.Identity, now)));
            }

            foreach (NpcCharacter pet in pets)
            {
                PetController? controller = pet.Pet;
                DismissMercPets(pet, "owner zoned");
                owner.OwnedPets.Remove(pet);
                pet.Brain?.StopPathing();
                if (pet.Playfield != null)
                    pet.Playfield.GetRequiredService<SpawnService>().DespawnNpc(pet);
                pet.UnbindPet();
                _logger.Info(string.Format(CultureInfo.InvariantCulture,
                    "Pet id={0} hash={1} leaves with owner={2}", pet.Identity.Instance, controller?.Hash, owner.Identity.Instance));
            }
        }

        /// <summary>The owner arrived here from another playfield: the pets it brought are summoned next to it.</summary>
        public void RestoreAfterTransfer(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            List<PetStash> stash = owner.OwnedPets.Stash;
            if (stash.Count == 0)
                return;

            PetStash[] carried = [.. stash];
            stash.Clear();
            DateTime now = DateTime.UtcNow;
            foreach (PetStash pet in carried)
            {
                if (pet.ExpiresUtc is DateTime expires && now >= expires)
                    continue;
                NpcCharacter? spawned = Spawn(owner, pet.Hash, pet.Type, pet.Level, pet.ExpiresUtc, pet.Mode,
                    pet.HealthPercent, pet.NanoPercent, pet.SummonRequirements);
                if (spawned == null)
                    continue;
                RestoreBuffs(spawned, pet.SelfBuffs, spawned.Identity);
                RestoreBuffs(owner, pet.OwnerBuffs, spawned.Identity);
            }
        }

        static List<StashedBuff> SnapshotBuffs(Character character, DateTime nowUtc)
        {
            var list = new List<StashedBuff>();
            IReadOnlyList<Buff> buffs = character.Buffs;
            for (int i = 0; i < buffs.Count; i++)
            {
                Buff buff = buffs[i];
                if (buff.IsExpired(nowUtc) || buff.Id <= 0)
                    continue;
                list.Add(new StashedBuff(buff.Id, buff.NanoInstance, buff.ExpiresAtUtc.Ticks));
            }

            return list;
        }

        static List<StashedBuff> SnapshotBuffsFromSource(Character character, Identity source, DateTime nowUtc)
        {
            var list = new List<StashedBuff>();
            IReadOnlyList<Buff> buffs = character.Buffs;
            for (int i = 0; i < buffs.Count; i++)
            {
                Buff buff = buffs[i];
                if (buff.IsExpired(nowUtc) || buff.Id <= 0)
                    continue;
                if (buff.Source.Type != source.Type || buff.Source.Instance != source.Instance)
                    continue;
                list.Add(new StashedBuff(buff.Id, buff.NanoInstance, buff.ExpiresAtUtc.Ticks));
            }

            return list;
        }

        void RestoreBuffs(Character target, IReadOnlyList<StashedBuff> stash, Identity source)
        {
            if (stash == null || stash.Count == 0 || target.Playfield == null)
                return;
            IItemBuilder items = target.Playfield.GetRequiredService<IItemBuilder>();
            DateTime nowUtc = DateTime.UtcNow;
            for (int i = 0; i < stash.Count; i++)
            {
                StashedBuff entry = stash[i];
                var expires = new DateTime(entry.ExpiresAtUtcTicks, DateTimeKind.Utc);
                if (expires <= nowUtc || entry.NanoId <= 0)
                    continue;
                if (target.TryGetBuff(entry.NanoId, out _))
                    continue;
                try
                {
                    NanoSpell spell = NanoSpell.From(items.CreateTemplate(entry.NanoId, entry.NanoId, quality: 1));
                    target.TryRestoreBuff(spell, source, entry.NanoInstance, expires);
                }
                catch (Exception exception)
                {
                    _logger.Warn("Pet buff restore failed nano=" + entry.NanoId.ToString(CultureInfo.InvariantCulture)
                        + ": " + exception.Message);
                }
            }
        }

        /// <summary>
        /// Tells the owner's client about every pet it has (after it finishes zoning in or reconnects), so its pet
        /// window lists them. The client ignores a pet it already lists.
        /// </summary>
        public void AnnounceOwnedPets(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            foreach (NpcCharacter pet in owner.OwnedPets.All)
                SendAddPet(owner, pet);
            PublishPetsStat(owner);
        }

        /// <summary>
        /// A PetCommand from the owner's client. Only the owner's own pets on this playfield obey; an attack order
        /// goes at the owner's current target.
        /// </summary>
        public void Command(Player owner, PetCommandCode command, IReadOnlyList<Identity> petIdentities)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(petIdentities);
            if (owner.IsDead || !ReferenceEquals(owner.Playfield, _playfield))
                return;

            foreach (NpcCharacter pet in ResolveOwned(owner, petIdentities))
            {
                PetController controller = pet.Pet!;
                // An over-equipped pet ignores every command; its owner can still dismiss it or ask for a report.
                if (controller.IsOverEquipped && command is not (PetCommandCode.Terminate or PetCommandCode.Report))
                    continue;

                bool healPet = IsHealPet(controller.Type);
                switch (command)
                {
                    case PetCommandCode.Follow:
                    case PetCommandCode.Behind:
                        controller.Order(PetMode.Follow);
                        if (healPet)
                            controller.EndHeal();
                        StandDown(pet);
                        break;
                    case PetCommandCode.Guard:
                        // A heal pet's guard is a heal order on its owner; a mezz pet's guard is follow, so it
                        // only fights when sent at a target.
                        if (healPet)
                        {
                            controller.Order(PetMode.Guard);
                            controller.EndHeal();
                        }
                        else if (IsMezzPet(controller.Type))
                        {
                            controller.Order(PetMode.Follow);
                            StandDown(pet);
                        }
                        else
                        {
                            controller.Order(PetMode.Guard);
                        }
                        break;
                    case PetCommandCode.Wait:
                        controller.Order(PetMode.Wait, pet.Position);
                        if (healPet)
                            controller.EndHeal();
                        StandDown(pet);
                        break;
                    case PetCommandCode.Attack:
                        if (ResolveAttackTarget(owner, pet) is Character target)
                            controller.Attack(target.Identity);
                        break;
                    case PetCommandCode.Heal:
                        // A heal pet only heals out of follow and wait, so a heal order puts it back on duty.
                        if (healPet && controller.Mode is PetMode.Follow or PetMode.Wait)
                            controller.Order(PetMode.Guard);
                        controller.Heal(ResolveHealTarget(owner).Identity);
                        break;
                    case PetCommandCode.Terminate:
                        Dismiss(pet, "terminated by owner");
                        break;
                    case PetCommandCode.Report:
                        Report(owner, pet);
                        break;
                }
            }
        }

        /// <summary>
        /// /pet report: one plain-text feedback line per pet. Live (capture 2026-10-07T02:52:27Z) sends it as
        /// FormatFeedback 110/707 with one string, "Anger Manifestation: Health: 100% Nano: 100% NCU: 0/9 Position: 935,756".
        /// TBD: pet MaxNCU. Pet templates carry no MaxNCU (181), so this reports 0 where live reported 9; the live
        /// source of a pet's NCU is not known yet.
        /// </summary>
        static void Report(Player owner, NpcCharacter pet)
        {
            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0}: Health: {1}% Nano: {2}% NCU: {3}/{4} Position: {5},{6}",
                pet.Name ?? string.Empty,
                Percent(pet.Stats.GetOrZero(CharacterStat.Health), pet.Stats.GetOrZero(CharacterStat.MaxHealth)),
                Percent(pet.Stats.GetOrZero(CharacterStat.CurrentNano), pet.Stats.GetOrZero(CharacterStat.MaxNanoEnergy)),
                pet.UsedNcu,
                pet.MaxNcu,
                (int)pet.Position.x,
                (int)pet.Position.z);
            Helpers.ClientFeedback.SendFormatted(owner, Helpers.ClientFeedback.PlainText, line);
        }

        static int Percent(int current, int max) => max <= 0 ? 100 : (int)(100L * Math.Max(0, current) / max);

        static bool IsHealPet(int type) => PetTypes.Slot(type) == PetTypes.Slot(PetTypes.Heal);

        /// <summary>The Metaphysicist's mezz pet: it only fights when sent at a target.</summary>
        static bool IsMezzPet(int type) => PetTypes.Slot(type) == PetTypes.Slot(PetTypes.Support);

        /// <summary>
        /// A newly summoned heal pet follows until told to heal or guard, and a mezz pet follows until sent at a
        /// target; other pets guard.
        /// </summary>
        static PetMode DefaultMode(int type) => IsHealPet(type) || IsMezzPet(type) ? PetMode.Follow : PetMode.Guard;

        IEnumerable<NpcCharacter> ResolveOwned(Player owner, IReadOnlyList<Identity> petIdentities)
        {
            // The command names the pets it is for; an empty list means all of them.
            NpcCharacter[] owned = [.. owner.OwnedPets.All];
            foreach (NpcCharacter pet in owned)
            {
                if (pet.Pet == null || !ReferenceEquals(pet.Playfield, _playfield))
                    continue;
                if (petIdentities.Count == 0 || Contains(petIdentities, pet.Identity))
                    yield return pet;
            }
        }

        static bool Contains(IReadOnlyList<Identity> identities, Identity identity)
        {
            for (int i = 0; i < identities.Count; i++)
            {
                if (identities[i].Instance == identity.Instance && identities[i].Type == identity.Type)
                    return true;
            }

            return false;
        }

        Character? ResolveAttackTarget(Player owner, NpcCharacter pet)
        {
            Identity target = owner.Target.Instance != 0 ? owner.Target : owner.FightingTarget;
            if (target.Instance == 0)
                return null;
            if (!_playfield.GetRequiredService<DynelRegistry>().TryGet(target, out Dynel? dynel)
                || dynel is not Character character || character.IsDead)
                return null;

            return Helpers.CombatRules.CanAttack(pet, character) ? character : null;
        }

        /// <summary>
        /// A heal order is for the owner's current target when that is someone friendly (a player, or a player's
        /// pet), otherwise for the owner.
        /// </summary>
        Character ResolveHealTarget(Player owner)
        {
            if (owner.Target.Instance != 0
                && _playfield.GetRequiredService<DynelRegistry>().TryGet(owner.Target, out Dynel? dynel)
                && dynel is Character target && !target.IsDead
                && (target is Player || target is NpcCharacter { PetOwner: Player }))
                return target;

            return owner;
        }

        /// <summary>
        /// Re-judges whether each of <paramref name="owner"/>'s pets is over-equipped (after the owner's stats were
        /// rebased). A pet that becomes OE drops what it was doing and follows.
        /// </summary>
        public void RefreshOverEquip(Character owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (owner is not Player || owner.OwnedPets.Count == 0)
                return;

            foreach (NpcCharacter pet in owner.OwnedPets.All)
                RefreshOverEquip(pet);
        }

        void RefreshOverEquip(NpcCharacter pet)
        {
            PetController? controller = pet.Pet;
            if (controller == null || controller.Owner is not Player owner)
                return;

            bool overEquipped = OverEquip.ComputeLevel(controller.SummonRequirements, owner.Stats) > 0;
            if (overEquipped == controller.IsOverEquipped)
                return;

            controller.IsOverEquipped = overEquipped;
            if (overEquipped)
            {
                controller.EndHeal();
                controller.Order(PetMode.Follow);
                StandDown(pet);
            }

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Pet id={0} hash={1} owner={2} over-equipped={3}",
                pet.Identity.Instance, controller.Hash, owner.Identity.Instance, overEquipped));
        }

        static void StandDown(NpcCharacter pet)
        {
            pet.Brain?.StopFighting();
            pet.Brain?.StopPathing();
        }

        /// <summary>
        /// A pet moves at its owner's run speed (buffs and all), so it keeps up instead of trailing behind.
        /// Returns true when the pet's speed changed.
        /// </summary>
        public static bool MatchOwnerRunSpeed(NpcCharacter pet, Character owner)
        {
            int speed = owner.Stats.GetOrZero(CharacterStat.RunSpeed);
            if (speed <= 0 || pet.Stats.GetOrZero(CharacterStat.RunSpeed, StatDetail.Base) == speed)
                return false;

            pet.Stats.Set(CharacterStat.RunSpeed, speed, StatDetail.Base, dirty: true);
            return true;
        }

        static int HealthPercent(Character character)
        {
            int max = character.Stats.GetOrZero(CharacterStat.MaxHealth);
            return max <= 0 ? 100 : (int)Math.Clamp(100L * Math.Max(0, character.Stats.GetOrZero(CharacterStat.Health)) / max, 1, 100);
        }

        static int NanoPercent(Character character)
        {
            int max = character.Stats.GetOrZero(CharacterStat.MaxNanoEnergy);
            return max <= 0 ? 100 : (int)Math.Clamp(100L * Math.Max(0, character.Stats.GetOrZero(CharacterStat.CurrentNano)) / max, 0, 100);
        }

        static void PublishPetsStat(Character owner)
        {
            int flags = owner.OwnedPets.Flags();
            if (owner.Stats.GetOrZero(CharacterStat.Pets, StatDetail.Base) == flags)
                return;

            owner.Stats.Set(CharacterStat.Pets, flags, StatDetail.Base, dirty: true);
            if (owner is Player player)
                player.FlushDirtyStats();
        }

        static void SendAddPet(Player owner, NpcCharacter pet)
            => owner.Session?.Send(new AddPetMessage { Identity = owner.Identity, Unknown = 0, PetIdentity = pet.Identity });

        static void SendRemovePet(Player owner, NpcCharacter pet)
            => owner.Session?.Send(new RemovePetMessage { Identity = owner.Identity, Unknown = 0, PetIdentity = pet.Identity });
    }
}
