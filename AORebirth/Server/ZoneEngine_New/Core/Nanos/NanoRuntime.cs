namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    /// <summary>
    /// Drives nano casts and NCU entries: gates a cast, runs the cast bar, lands or refuses the
    /// buff, starts the recharge lockout, and tells the clients involved.
    /// State lives on <see cref="Character"/>; this type owns the sequencing and the packets.
    /// </summary>
    public static class NanoRuntime
    {
        /// <summary>Advances the cast bar and expires NCU entries. Called from the character tick.</summary>
        public static void Tick(Character character, DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(character);

            PendingNanoCast? cast = character.PendingCast;
            if (cast != null && cast.IsReady(nowUtc))
            {
                // Clear first: the execute-time gate must not see this cast as "already casting".
                character.CancelNanoCast();
                Complete(character, cast, nowUtc);
            }

            TickPeriodicEffects(character, nowUtc);
            AuraNanos.Maintain(character, nowUtc);

            List<Buff> expired = character.DrainExpiredBuffs(nowUtc);
            for (int i = 0; i < expired.Count; i++)
                AnnounceBuffRemoved(character, expired[i]);
        }

        /// <summary>
        /// Repeated hits and child-nano casts due on this character's buffs. Ticks are collected first: a
        /// killing tick empties NCU, so no buff list is walked while effects run.
        /// </summary>
        static void TickPeriodicEffects(Character character, DateTime nowUtc)
        {
            IReadOnlyList<Buff> buffs = character.Buffs;
            if (buffs.Count == 0 || character.IsDead)
                return;

            List<(Buff Buff, ItemSpell Spell)>? due = null;
            var spells = new List<ItemSpell>();
            for (int i = 0; i < buffs.Count; i++)
            {
                spells.Clear();
                buffs[i].CollectDueTicks(nowUtc, spells);
                for (int s = 0; s < spells.Count; s++)
                    (due ??= new()).Add((buffs[i], spells[s]));
            }

            if (due == null)
                return;

            Playfield? playfield = character.Playfield;
            if (playfield == null)
                return;

            IInventoryRepository inventory = playfield.GetRequiredService<IInventoryRepository>();
            IItemBuilder items = playfield.GetRequiredService<IItemBuilder>();
            DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
            for (int i = 0; i < due.Count; i++)
            {
                if (character.IsDead)
                    return;

                (Buff buff, ItemSpell spell) = due[i];
                if (!character.TryGetBuff(buff.Id, out Buff? active) || !ReferenceEquals(buff, active))
                    continue;
                // Attributed to the caster while it is still here; otherwise the owner takes it.
                Character? source = registry.TryGet(buff.Source, out Dynel? dynel) && dynel is Character caster
                    ? caster
                    : null;
                buff.ExecutePeriodicSpell(character, source, spell, inventory, items);
            }
        }

        /// <summary>Death empties NCU and drops a cast in flight, ignoring CanCancel.</summary>
        public static void ClearBuffsOnDeath(Character character)
        {
            ArgumentNullException.ThrowIfNull(character);

            character.CancelNanoCast();
            List<Buff> removed = character.RemoveAllBuffs(BuffRemovalReason.Death);
            for (int i = 0; i < removed.Count; i++)
                AnnounceBuffRemoved(character, removed[i]);
        }

        /// <summary>
        /// Up-front gate for a client cast request. On success the cast bar starts and the world
        /// sees CastNanoSpell; the buff itself lands only when the bar finishes.
        /// </summary>
        public static NanoCastRefusal TryStartCast(
            Character caster,
            int nanoId,
            Identity target,
            DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(caster);

            if (nanoId <= 0)
            {
                LogCastRefused(caster, nanoId, target, NanoCastRefusal.NotUploaded, phase: "start", detail: "nanoId<=0");
                return NanoCastRefusal.NotUploaded;
            }

            //This is smelly! - Delmus
            // Same click often delivers two start packets; the second must not refresh/remove.
            // if (caster.IsDuplicateRecentNanoLand(nanoId, target, nowUtc))
            //     return NanoCastRefusal.None;

            // Resolve the recipient first so twinked mercs can pick the highest QL the target can accept
            // (Fixer NCU and other level-locked buffs fail on low-level teammates at full gear QL).
            Character? recipientPreview = null;
            if (target.Instance != 0)
                recipientPreview = ResolveTarget(caster, target);

            if (!TryResolveSpell(caster, nanoId, out NanoSpell? spell, recipientPreview) || spell == null)
            {
                LogCastRefused(
                    caster,
                    nanoId,
                    target,
                    NanoCastRefusal.NotUploaded,
                    phase: "start",
                    detail: "spell resolve failed");
                Refuse(caster, NanoCastRefusal.NotUploaded);
                return NanoCastRefusal.NotUploaded;
            }

            // A self-only nano lands on the caster whatever the client had targeted.
            if (SelfOnlyFor(caster, spell))
                target = caster.Identity;

            Character? recipient = ResolveTarget(caster, target);
            // Re-resolve when self-only redirected the target or preview missed the recipient.
            if (!ReferenceEquals(recipient, recipientPreview)
                && !TryResolveSpell(caster, nanoId, out spell, recipient))
            {
                LogCastRefused(
                    caster,
                    nanoId,
                    target,
                    NanoCastRefusal.NotUploaded,
                    phase: "start",
                    detail: "spell resolve failed for recipient");
                Refuse(caster, NanoCastRefusal.NotUploaded);
                return NanoCastRefusal.NotUploaded;
            }

            int nanoCost = ResolveNanoCost(caster, spell);
            NanoCastAttempt attempt = BuildAttempt(caster, spell, recipient, nanoCost, nowUtc);
            NanoCastRefusal refusal = NanoCastRules.Evaluate(attempt);
            // Start-only: do not begin a cast the target cannot hold. Land-time NCU is checked
            // again in TryApplyBuff; a mid-cast capacity change still finishes the cast.
            if (refusal == NanoCastRefusal.None && !TargetCanHoldBuff(spell, recipient))
                refusal = NanoCastRefusal.NotEnoughNcu;

            if (refusal == NanoCastRefusal.None && spell.IsHostile && !IsHostileNanoTarget(caster, recipient))
                refusal = NanoCastRefusal.InvalidTarget;

            // Merc/NPC heals must never land on mobs (doctor nanos carry ApplyOnFightingTarget).
            if (refusal == NanoCastRefusal.None && recipient != null
                && !NpcSupportRules.MayNpcLand(caster, recipient, spell))
                refusal = NanoCastRefusal.InvalidTarget;

            if (refusal == NanoCastRefusal.None && (IsNoCombat(caster) || IsNoCombat(recipient)))
                refusal = NanoCastRefusal.InvalidTarget;

            if (refusal != NanoCastRefusal.None)
            {
                LogCastRefused(caster, nanoId, target, refusal, phase: "start", attempt: attempt, spell: spell);
                Refuse(caster, refusal);
                return refusal;
            }

            int attackTime = NanoDelayCalculator.AttackTimeCentiseconds(
                spell.AttackDelayCentiseconds,
                spell.AttackDelayCapCentiseconds,
                caster.Stats.GetOrZero(CharacterStat.AggDef),
                caster.Stats.GetOrZero(CharacterStat.NanoCInit));

            Identity resolvedTarget = recipient!.Identity;
            var cast = new PendingNanoCast(spell, resolvedTarget, nanoCost, attackTime, nowUtc);
            caster.BeginNanoCast(cast);

            // The nano cost is paid as the cast starts; an interrupted or refused cast does not refund it.
            SpendNano(caster, nanoCost);
            AnnounceCastStarted(caster, spell.Id, resolvedTarget);

            // Instant nanos have no cast bar; land them in the same tick.
            if (cast.IsReady(nowUtc))
            {
                caster.CancelNanoCast();
                Complete(caster, cast, nowUtc);
            }

            return NanoCastRefusal.None;
        }

        /// <summary>Reads a nano template. False when the id is not a template.</summary>
        public static bool TryGetSpell(Character caster, int nanoId, out NanoSpell? spell)
        {
            ArgumentNullException.ThrowIfNull(caster);
            spell = null;
            return nanoId > 0 && TryResolveSpell(caster, nanoId, out spell) && spell != null;
        }

        /// <summary>
        /// Resolves a nano for a specific recipient (highest QL a twinked merc's target can accept).
        /// </summary>
        public static bool TryGetSpellForTarget(Character caster, int nanoId, Character recipient, out NanoSpell? spell)
        {
            ArgumentNullException.ThrowIfNull(caster);
            ArgumentNullException.ThrowIfNull(recipient);
            spell = null;
            return nanoId > 0 && TryResolveSpell(caster, nanoId, out spell, recipient) && spell != null;
        }

        /// <summary>
        /// The <see cref="TryStartCast"/> gates without refusing, logging or starting anything:
        /// caster state, requirements, nano cost, target NCU and hostile-target legality.
        /// </summary>
        public static bool CanStartCast(Character caster, NanoSpell spell, Character recipient, DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(caster);
            ArgumentNullException.ThrowIfNull(spell);
            ArgumentNullException.ThrowIfNull(recipient);

            if (IsNoCombat(caster) || IsNoCombat(recipient))
                return false;

            // A self-only nano would land on the caster instead.
            if (SelfOnlyFor(caster, spell) && !ReferenceEquals(caster, recipient))
                return false;

            if (!NpcSupportRules.MayNpcLand(caster, recipient, spell))
                return false;

            NanoCastAttempt attempt = BuildAttempt(caster, spell, recipient, ResolveNanoCost(caster, spell), nowUtc);
            if (NanoCastRules.Evaluate(attempt) != NanoCastRefusal.None)
                return false;
            if (!TargetCanHoldBuff(spell, recipient))
                return false;
            return !spell.IsHostile || IsHostileNanoTarget(caster, recipient);
        }

        /// <summary>
        /// Like <see cref="CanStartCast(Character, NanoSpell, Character, DateTime)"/> but re-resolves the
        /// nano at the highest QL <paramref name="recipient"/> can accept (merc level-locked buffs).
        /// </summary>
        public static bool CanStartCast(Character caster, int nanoId, Character recipient, DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(caster);
            ArgumentNullException.ThrowIfNull(recipient);
            if (!TryGetSpellForTarget(caster, nanoId, recipient, out NanoSpell? spell) || spell == null)
                return false;
            return CanStartCast(caster, spell, recipient, nowUtc);
        }

        /// <summary>
        /// Spell removal of one nano id. Absent ids are a successful no-op.
        /// Uncancellable and hostile buffs are removed.
        /// </summary>
        public static bool TryStripNano(Character owner, int nanoId)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (nanoId <= 0)
                return false;

            if (owner.TryRemoveBuff(nanoId, BuffRemovalReason.Stripped, out Buff? removed)
                == BuffRemovalOutcome.Removed
                && removed != null)
                AnnounceBuffRemoved(owner, removed);

            return true;
        }

        /// <summary>
        /// Spell removal of every buff whose strain matches. Strain 0 is refused so a
        /// strainless buff is not wiped by an empty argument.
        /// </summary>
        public static bool TryStripStrain(Character owner, int strain)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (strain <= 0)
                return false;

            var matching = new List<int>();
            foreach (Buff buff in owner.Buffs)
            {
                if (buff.NanoStrain == strain)
                    matching.Add(buff.Id);
            }

            for (int i = 0; i < matching.Count; i++)
            {
                if (owner.TryRemoveBuff(matching[i], BuffRemovalReason.Stripped, out Buff? removed)
                    == BuffRemovalOutcome.Removed
                    && removed != null)
                    AnnounceBuffRemoved(owner, removed);
            }

            return true;
        }

        /// <summary>
        /// Removes every NCU entry another character put on <paramref name="owner"/>: debuffs, DoTs and
        /// buffs cast by others. What the owner or its own items cast on it (source = owner) stays, as do
        /// entries with no recorded source.
        /// </summary>
        public static int StripForeignBuffs(Character owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            Identity self = owner.Identity;
            List<Buff> removed = owner.RemoveBuffs(
                buff => buff.Source.Instance != 0 && buff.Source != self);
            for (int i = 0; i < removed.Count; i++)
                AnnounceBuffRemoved(owner, removed[i]);
            return removed.Count;
        }

        /// <summary>Spell removal of every NCU entry, including uncancellable buffs.</summary>
        public static bool StripAllBuffs(Character owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            List<Buff> removed = owner.RemoveAllBuffs(BuffRemovalReason.Stripped);
            for (int i = 0; i < removed.Count; i++)
                AnnounceBuffRemoved(owner, removed[i]);
            return true;
        }

        /// <summary>
        /// Owner-driven NCU removal. Refuses buffs that are absent, hostile, or flagged
        /// uncancellable instead of silently dropping the request.
        /// </summary>
        public static BuffRemovalOutcome TryCancelBuff(Character owner, int nanoId)
        {
            ArgumentNullException.ThrowIfNull(owner);

            BuffRemovalOutcome outcome = owner.TryRemoveBuff(
                nanoId,
                BuffRemovalReason.Cancelled,
                out Buff? removed);

            switch (outcome)
            {
                case BuffRemovalOutcome.Removed when removed != null:
                    AnnounceBuffRemoved(owner, removed);
                    break;

                case BuffRemovalOutcome.NotCancellable:
                    Refuse(owner, "That nano program cannot be cancelled.");
                    break;
            }

            return outcome;
        }

        /// <summary>
        /// Drops a cast bar in flight and tells nearby clients. Successful cast completion
        /// clears via <see cref="Character.CancelNanoCast"/> without this packet.
        /// </summary>
        public static bool InterruptCast(Character character)
        {
            ArgumentNullException.ThrowIfNull(character);

            PendingNanoCast? cast = character.PendingCast;
            if (cast == null)
                return false;

            int nanoId = cast.Spell.Id;
            character.CancelNanoCast();
            AnnounceCastInterrupted(character, nanoId);
            return true;
        }

        /// <summary>
        /// Clears a client cast bar even when the server has no <see cref="Character.PendingCast"/>.
        /// Merc menu / cross-character <see cref="TryApplyImmediate"/> can open the recipient's bar
        /// via CastNanoSpell(Identity=recipient) while FinishNanoCasting only names the source.
        /// </summary>
        public static void ForceClearClientCastBar(Character character, int nanoId = 0)
        {
            ArgumentNullException.ThrowIfNull(character);
            character.CancelNanoCast();
            AnnounceCastInterrupted(character, nanoId);
        }

        /// <summary>
        /// Lands a nano immediately: no cast bar, recharge, nano spend, or upload check.
        /// Buffs occupy NCU; instant nanos just run their OnUse effects.
        /// </summary>
        public static bool TryApplyImmediate(
            Character source,
            Character target,
            int nanoId,
            IItemBuilder items,
            IInventoryRepository inventory,
            DateTime nowUtc,
            bool gmApply = false)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(inventory);

            if (nanoId <= 0 || target.IsDead)
                return false;

            ItemTemplate template = items.CreateTemplate(nanoId, nanoId, quality: 1);
            if (template.Id != nanoId)
                return false;

            return LandImmediate(source, target, NanoSpell.From(template), inventory, items, nowUtc, gmApply);
        }

        /// <summary>
        /// Lands <paramref name="spell"/> at once. <paramref name="gmApply"/> (a GM's .give nano): combat legality does
        /// not apply, so a hostile-flagged nano such as PvP Enabled (202732) lands on any living character, and the
        /// target does not treat the GM as an attacker. NCU and strain rules still apply.
        /// </summary>
        static bool LandImmediate(
            Character source,
            Character target,
            NanoSpell spell,
            IInventoryRepository inventory,
            IItemBuilder items,
            DateTime nowUtc,
            bool gmApply = false)
        {
            if (!gmApply)
            {
                if (IsNoCombat(source) || IsNoCombat(target))
                    return false;

                if (spell.IsHostile && !IsHostileNanoTarget(source, target))
                    return false;

                if (spell.IsHostile && target.IsEvading)
                    return false;

                if (!NpcSupportRules.MayNpcLand(source, target, spell))
                    return false;
            }

            AnnounceImmediateLand(source, target, spell.Id);
            AnnounceCastFinished(source, spell.Id);
            // CastNanoSpell uses Identity=target; Finish about the source alone leaves a player
            // recipient's cast bar stuck (merc menu buffs, .give buff on another character).
            if (target is Player && !ReferenceEquals(source, target))
                Announce(target, CastFinishedMessage(target, spell.Id));

            if (!spell.IsBuff)
            {
                ExecuteOnUseEffects(source, target, spell, skipPassiveModifiers: false, inventory, items);
                return true;
            }

            BuffApplyDecision decision = target.TryApplyBuff(
                spell,
                source.Identity,
                nowUtc,
                out Buff? applied,
                out Buff? replaced);

            if (applied == null)
            {
                if (source is Player)
                    Refuse(source, decision);
                return false;
            }

            if (replaced != null && replaced.Id != spell.Id)
                AnnounceBuffRemoved(target, replaced);

            SendNanoDuration(source, target, applied);
            ExecuteOnUseEffects(source, target, spell, skipPassiveModifiers: true, inventory, items);
            if (spell.IsHostile && !gmApply)
                OnDebuffLanded(source, target, applied);
            return true;
        }

        static void Complete(Character caster, PendingNanoCast cast, DateTime nowUtc)
        {
            NanoSpell spell = cast.Spell;
            Character? recipient = ResolveTarget(caster, cast.Target);

            // Everything the up-front gate checked can have changed while the bar ran. The nano cost was
            // already paid at the start, so it is not checked again.
            NanoCastAttempt attempt = BuildAttempt(caster, spell, recipient, nanoCost: 0, nowUtc);
            NanoCastRefusal refusal = NanoCastRules.Evaluate(attempt);
            // Cast bar already went out via CastNanoSpell; refusal must clear it.
            if (refusal != NanoCastRefusal.None)
            {
                LogCastRefused(
                    caster,
                    spell.Id,
                    cast.Target,
                    refusal,
                    phase: "complete",
                    attempt: attempt,
                    spell: spell);
                AnnounceCastInterrupted(caster, spell.Id);
                Refuse(caster, refusal);
                return;
            }

            if ((spell.IsHostile && !IsHostileNanoTarget(caster, recipient)) || IsNoCombat(recipient)
                || (recipient != null && !NpcSupportRules.MayNpcLand(caster, recipient, spell)))
            {
                AnnounceCastInterrupted(caster, spell.Id);
                Refuse(caster, NanoCastRefusal.InvalidTarget);
                return;
            }

            // Duplicate instant-cast packets can both reach Complete before the land window closes.
            if (!caster.TryClaimNanoLand(spell.Id, cast.Target, nowUtc))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Nano cast duplicate land suppressed caster={0} nano={1} target={2}",
                        caster.Identity.Instance,
                        spell.Id,
                        cast.Target.Instance));
                return;
            }

            AnnounceCastFinished(caster, spell.Id);

            // NoTimerNotify nanos finish with no lockout. Everyone else waits the raw template delay.
            int recharge = (spell.Flags & (int)NanoFlags.NoTimerNotify) != 0
                ? 0
                : NanoDelayCalculator.RechargeTimeCentiseconds(spell.RechargeDelayCentiseconds);
            caster.StartNanoRecharge(recharge, nowUtc);

            // A finished hostile cast pulls the NPC whether it lands, is resisted or is refused; a resetting NPC ignores it.
            if (spell.IsHostile)
                AddHostileThreat(caster, recipient!);

            // Resisted: an evading NPC is untouchable on its way home. The cast is spent; nothing lands.
            if (spell.IsHostile && recipient!.IsEvading)
            {
                ClientFeedback.Send(caster, ClientFeedback.TargetResisted);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Nano resisted by evading target caster={0} nano={1} target={2}",
                        caster.Identity.Instance,
                        spell.Id,
                        cast.Target.Instance));
                return;
            }

            if (!spell.IsBuff)
            {
                ClientFeedback.Send(caster, ClientFeedback.NanoExecutedSuccessfully);
                ExecuteOnUseEffects(caster, recipient!, spell, skipPassiveModifiers: false);
                return;
            }

            BuffApplyDecision decision = recipient!.TryApplyBuff(
                spell,
                caster.Identity,
                nowUtc,
                out Buff? applied,
                out Buff? replaced);

            // Cast already finished and spent nano. Apply refusal (e.g. NCU) does not interrupt it.
            if (applied == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Nano apply refused caster={0} nano={1} target={2} decision={3} ncu={4}/{5} strain={6} stacking={7} ncuCost={8}",
                        caster.Identity.Instance,
                        spell.Id,
                        recipient.Identity.Instance,
                        decision,
                        recipient.UsedNcu,
                        recipient.MaxNcu,
                        spell.NanoStrain,
                        spell.StackingOrder,
                        spell.NcuCost));
                Refuse(caster, decision);
                return;
            }

            // Player casts use SetNanoDuration for NCU display (legacy never Buff-adds on land).
            // Buff remove is only for a different nano leaving the same strain.
            if (replaced != null && replaced.Id != spell.Id)
                AnnounceBuffRemoved(recipient, replaced);

            ClientFeedback.Send(caster, ClientFeedback.NanoExecutedSuccessfully);
            SendNanoDuration(caster, recipient, applied);
            ExecuteOnUseEffects(caster, recipient, spell, skipPassiveModifiers: true);
            if (spell.IsHostile)
                OnDebuffLanded(caster, recipient, applied, addThreat: false);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Nano landed caster={0} nano={1} target={2} ncu={3}/{4} duration={5}cs recharge={6}cs",
                    caster.Identity.Instance,
                    spell.Id,
                    recipient.Identity.Instance,
                    recipient.UsedNcu,
                    recipient.MaxNcu,
                    applied.DurationCentiseconds,
                    recharge));
        }

        static NanoCastAttempt BuildAttempt(
            Character caster,
            NanoSpell spell,
            Character? recipient,
            int nanoCost,
            DateTime nowUtc)
            => new()
            {
                CasterIsDead = caster.IsDead,
                CasterIsCasting = caster.IsCastingNano,
                CasterIsRecharging = caster.IsInNanoRecharge(nowUtc),
                // NPC casts are not upload gated; an NPC's list only holds its equipped crystals.
                IsUploaded = !caster.IsPlayer || caster.UploadedNanoIds.Contains(spell.Id),
                // Pets/mercs skip caster skill and nano-pool gates (templates carry none), but still honour
                // OnTarget level locks so Fixer NCU and other level-locked buffs do not fail on low teammates.
                RequirementsMet = caster is NpcCharacter { Pet: not null }
                    ? PetBuffRequirementsMet(spell, caster, recipient)
                    : spell.MeetsActionRequirements(
                        stat => caster.Stats.Get(stat),
                        ActionType.ToUse,
                        resolve: ItemTemplate.WithActionTarget(
                            caster is Player casterPlayer ? casterPlayer.ResolvePerkRequirement : null, caster, recipient),
                        getTargetStat: recipient == null ? null : stat => recipient.Stats.Get(stat)),
                TargetExists = recipient != null,
                TargetIsDead = recipient?.IsDead == true,
                CurrentNano = caster is NpcCharacter { Pet: not null } ? int.MaxValue : caster.Stats.GetOrZero(CharacterStat.CurrentNano),
                NanoCost = nanoCost
            };

        /// <summary>
        /// Start-time NCU preview. Instant, hostile, and NoRemoveNoNCUFriendly nanos skip it.
        /// Land still re-checks via <see cref="BuffApplyRules"/>; failure there finishes the
        /// cast without applying.
        /// </summary>
        /// <summary>
        /// A debuff landing can break crowd control already on the target, and a player's debuff
        /// always puts the player on an NPC's hate list, so debuffing an idle mob pulls it.
        /// A pacified NPC still ignores that threat. <paramref name="addThreat"/> is false when the
        /// finished cast already added it.
        /// </summary>
        static void OnDebuffLanded(Character caster, Character recipient, Buff applied, bool addThreat = true)
        {
            recipient.RollBuffBreaks(BuffBreakCause.Debuff, caster, except: applied);
            if (addThreat)
                AddHostileThreat(caster, recipient);
        }

        /// <summary>A player's hostile nano puts the player on the NPC's hate list (ignored while resetting or pacified).</summary>
        static void AddHostileThreat(Character caster, Character recipient)
        {
            if (recipient is NpcCharacter npc && caster.IsPlayer && !ReferenceEquals(caster, recipient))
                npc.Brain?.AddThreat(caster.Identity, Ai.NpcAiRules.ProximityHate);
        }

        static bool TargetCanHoldBuff(NanoSpell spell, Character? recipient)
        {
            if (recipient == null || !spell.IsBuff || !spell.ConsumesNcu)
                return true;

            // Same cap the landing uses, so a cast is never refused for NCU the buff would not need.
            BuffApplyDecision decision = BuffApplyRules.Evaluate(
                spell,
                recipient.Buffs,
                recipient.BuffNcuCapacity,
                out _);
            return decision != BuffApplyDecision.RefusedNotEnoughNcu;
        }

        /// <summary>
        /// Pets/mercs skip caster skill / profession gates (templates carry none) but still require
        /// the recipient to meet OnTarget checks (level locks on NCU compressors, composite lines, etc.).
        /// Using MaxValue for every caster leaf made Profession EqualTo fail, so no merc buff landed.
        /// </summary>
        /// <summary>
        /// Fixer NCU programs are User-targeted in items.dat (self-only), but live Fixers and buffbots put them on
        /// teammates ("NCU Nanos [Team]"). Mercs may cast them on the owner and team.
        /// </summary>
        static readonly HashSet<int> FixerNcuLine =
        [
            162994, 162995, 163005, 163079, 163006, 163081, 163012, 163087, 163008, 163083,
            163010, 163085, 163015, 163094, 163057, 163095, 275043, 275135
        ];

        static bool SelfOnlyFor(Character caster, NanoSpell spell)
            => spell.IsSelfOnly
               && !(caster is NpcCharacter { Pet: not null } && FixerNcuLine.Contains(spell.Id));

        static bool PetBuffRequirementsMet(NanoSpell spell, Character caster, Character? recipient)
        {
            if (recipient == null)
                return true;
            IReadOnlyList<UnmetRequirement> unmet = spell.UnmetActionRequirements(
                _ => int.MaxValue,
                ActionType.ToUse,
                resolve: ItemTemplate.WithActionTarget(null, caster, recipient),
                getTargetStat: stat => recipient.Stats.Get(stat));
            for (int i = 0; i < unmet.Count; i++)
            {
                if (unmet[i].OnTarget)
                    return false;
            }

            return true;
        }

        static bool TryResolveSpell(Character caster, int nanoId, out NanoSpell? spell)
            => TryResolveSpell(caster, nanoId, out spell, recipient: null);

        /// <summary>
        /// Resolves a nano template. When a twinked merc casts a QL-pair buff on a recipient, picks the
        /// highest quality at or below <see cref="NpcCharacter.NanoCastQuality"/> that the recipient
        /// can accept (target level locks). Hostile and self casts keep full gear QL.
        /// </summary>
        static bool TryResolveSpell(Character caster, int nanoId, out NanoSpell? spell, Character? recipient)
        {
            spell = null;
            Playfield? playfield = caster.Playfield;
            if (playfield == null)
                return false;

            IItemBuilder items = playfield.GetRequiredService<IItemBuilder>();
            int lowId = nanoId;
            int highId = nanoId;
            int quality = 1;
            bool interpolates = false;
            // A twinked merc names a cast QL. Only a two-id pair interpolates; a single catalog row stays as it is.
            if (caster is NpcCharacter { NanoCastQuality: > 1 } npc)
            {
                ItemTemplate anchor = items.CreateTemplate(nanoId, nanoId, 1);
                if (anchor.Id == nanoId && anchor.Relations is { Count: 2 } rel)
                {
                    int a = rel[0];
                    int b = rel[1];
                    if (a > 0 && b > 0 && a != b && (nanoId == a || nanoId == b))
                    {
                        lowId = Math.Min(a, b);
                        highId = Math.Max(a, b);
                        quality = npc.NanoCastQuality;
                        interpolates = true;
                    }
                }
            }

            if (interpolates && recipient != null && !ReferenceEquals(caster, recipient))
            {
                // Highest QL the target can accept (level-locked buffs / NCU). Step down from gear QL.
                for (int q = quality; q >= 1; q--)
                {
                    ItemTemplate candidate = items.CreateTemplate(lowId, highId, q);
                    if (candidate.Id != nanoId && candidate.Id != lowId)
                        continue;
                    NanoSpell trial = NanoSpell.From(candidate);
                    if (!PetBuffRequirementsMet(trial, caster, recipient))
                        continue;
                    if (!TargetCanHoldBuff(trial, recipient))
                        continue;
                    spell = trial;
                    return true;
                }

                return false;
            }

            ItemTemplate template = items.CreateTemplate(lowId, highId, quality);
            if (template.Id != nanoId && template.Id != lowId)
                return false;

            spell = NanoSpell.From(template);
            return true;
        }

        /// <summary>
        /// Lands a friendly buff on the player without an uploaded nano, a nano-energy spend, or a cast bar.
        /// Strain, stacking and NCU still go through <see cref="Character.TryApplyBuff"/>. The player is the
        /// source, so the land is a self-apply. Hostile nanos and non-buffs are refused.
        /// </summary>
        public static bool TryApplyServiceBuff(Player player, int nanoId, bool announceRefusal, out string detail)
        {
            detail = "That nano program had no effect.";
            ArgumentNullException.ThrowIfNull(player);
            if (player.IsDead)
            {
                detail = "You cannot receive a buff while dead.";
                return false;
            }

            if (!TryResolveSpell(player, nanoId, out NanoSpell? spell) || spell == null)
            {
                detail = "That nano is not in the item catalog.";
                return false;
            }

            if (spell.IsHostile || !spell.IsBuff)
            {
                detail = "That nano is not a friendly buff.";
                return false;
            }

            DateTime nowUtc = DateTime.UtcNow;
            BuffApplyDecision decision = player.TryApplyBuff(
                spell,
                player.Identity,
                nowUtc,
                out Buff? applied,
                out Buff? replaced);
            if (applied == null)
            {
                detail = decision switch
                {
                    BuffApplyDecision.RefusedNotEnoughNcu => "Not enough NCU.",
                    BuffApplyDecision.RefusedStrainStronger => "A stronger buff of that strain is already running.",
                    _ => "That nano program had no effect."
                };
                if (announceRefusal)
                    Refuse(player, decision);
                return false;
            }

            if (replaced != null && replaced.Id != spell.Id)
                AnnounceBuffRemoved(player, replaced);

            ClientFeedback.Send(player, ClientFeedback.NanoExecutedSuccessfully);
            SendNanoDuration(player, player, applied);
            ExecuteOnUseEffects(player, player, spell, skipPassiveModifiers: true);
            detail = string.Format(
                CultureInfo.InvariantCulture,
                "{0} QL {1} landed. NCU {2}.",
                spell.Name,
                spell.Quality,
                spell.NcuCost);
            return true;
        }

        static int ResolveNanoCost(Character caster, NanoSpell spell)
            => NanoCostCalculator.Compute(
                spell.NanoPointCost,
                caster.Stats.GetOrZero(CharacterStat.NPCostModifier));

        static void SpendNano(Character caster, int cost)
        {
            if (cost <= 0)
                return;

            int remaining = Math.Max(0, caster.Stats.GetOrZero(CharacterStat.CurrentNano) - cost);
            caster.Stats.Set(CharacterStat.CurrentNano, remaining, StatDetail.Base, dirty: true);
        }

        /// <summary>A NoCombat NPC (not attackable): it neither casts nanos nor has nanos cast on it.</summary>
        static bool IsNoCombat(Character? character) => character is NpcCharacter { Attackable: false };

        /// <summary>
        /// Hostile nanos may land on the caster. Other targets still have to be legal to attack.
        /// </summary>
        static bool IsHostileNanoTarget(Character caster, Character? target)
            => target != null && (ReferenceEquals(caster, target) || CombatRules.CanAttack(caster, target));

        /// <summary>Self, or a character in the same playfield. Null means the target is gone.</summary>
        static Character? ResolveTarget(Character caster, Identity target)
        {
            if (target.Instance == 0 || target.Instance == caster.Identity.Instance)
                return caster;

            Playfield? playfield = caster.Playfield;
            if (playfield == null)
                return null;

            return playfield.GetRequiredService<DynelRegistry>().TryGet(target, out Dynel? dynel)
                && dynel is Character character
                ? character
                : null;
        }

        static void Refuse(Character caster, NanoCastRefusal refusal)
        {
            switch (refusal)
            {
                case NanoCastRefusal.CasterDead:
                    ClientFeedback.Send(caster, "Feedback_TargetIsAlreadyDead");
                    return;
                case NanoCastRefusal.RequirementsNotMet:
                    ClientFeedback.Send(caster, ClientFeedback.CheckItemRequirements);
                    return;
                case NanoCastRefusal.AlreadyCasting:
                    ClientFeedback.Send(caster, "Feedback_WaitForCurrentNanoprogram");
                    return;
                case NanoCastRefusal.Recharging:
                    ClientFeedback.Send(caster, "Feedback_NanobotsAreRecharging");
                    return;
                case NanoCastRefusal.NotEnoughNano:
                    ClientFeedback.Send(caster, "Feedback_NanoprogramDidNotActivateNotEnoughNanoenergy");
                    return;
                case NanoCastRefusal.NotEnoughNcu:
                    ClientFeedback.Send(caster, ClientFeedback.TargetNotEnoughNcu);
                    return;
                case NanoCastRefusal.InvalidTarget:
                    ClientFeedback.Send(caster, "Feedback_UnableToExecuteOnThisTarget");
                    return;
                case NanoCastRefusal.TargetDead:
                    ClientFeedback.Send(caster, "Feedback_TargetIsAlreadyDead");
                    return;
                default:
                    Refuse(caster, NanoCastRules.Describe(refusal));
                    return;
            }
        }

        static void Refuse(Character caster, BuffApplyDecision decision)
        {
            switch (decision)
            {
                case BuffApplyDecision.RefusedNotEnoughNcu:
                    ClientFeedback.Send(caster, ClientFeedback.TargetNotEnoughNcu);
                    return;
                case BuffApplyDecision.RefusedStrainStronger:
                    ClientFeedback.Send(caster, "Feedback_NCUErrorBetterProgramRunning");
                    return;
                default:
                    Refuse(caster, "That nano program had no effect.");
                    return;
            }
        }

        static void AnnounceCastStarted(Character caster, int nanoId, Identity target)
            => Announce(
                caster,
                new CastNanoSpellMessage
                {
                    Identity = caster.Identity,
                    // NPC casts leave Caster empty; player casts name themselves.
                    Caster = caster.IsPlayer ? caster.Identity : Identity.None,
                    Target = target,
                    NanoId = nanoId,
                    Unknown = 0,
                    Unknown1 = 0
                });

        /// <summary>
        /// The effect of a nano an item or perk function lands with no cast (CastNano / AreaCastNano...): live sends
        /// CastNanoSpell addressed to the recipient, Target = recipient, Unknown1 = 1, Caster = the user, then
        /// SetNanoDuration for a buff (captures 2026-10-02T03:54:58Z Impale on a mob, 15:12:44Z Dance of Fools on
        /// self). FinishNanoCasting(1, nano) follows (live 2026-10-02T15:28:59Z): client case 0x1d (Gamecode.dll
        /// 0x1004f322) closes the pending cast entry CastNanoSpell opened and records the success result.
        /// </summary>
        static void AnnounceImmediateLand(Character source, Character target, int nanoId)
            => Announce(target, ImmediateLandMessage(source, target, nanoId));

        static void AnnounceCastFinished(Character caster, int nanoId)
            => Announce(caster, CastFinishedMessage(caster, nanoId));

        static CastNanoSpellMessage ImmediateLandMessage(Character source, Character target, int nanoId)
            => new()
            {
                Identity = target.Identity,
                NanoId = nanoId,
                Target = target.Identity,
                Unknown = 0,
                Unknown1 = 1,
                Caster = source.Identity
            };

        static CharacterActionMessage CastFinishedMessage(Character caster, int nanoId)
            => new()
            {
                Identity = caster.Identity,
                Unknown = 0x00,
                Action = CharacterActionType.FinishNanoCasting,
                Unknown1 = 0,
                Target = Identity.None,
                Parameter1 = 1,
                Parameter2 = nanoId,
                Unknown2 = 0
            };

        // Live IN InterruptNanoCasting: Parameter1=nanoId, Parameter2=reason (observed 7 / 4).
        // FinishNanoCasting keeps Parameter1=1, Parameter2=nanoId — do not reuse that layout here.
        const int DefaultInterruptReason = 7;

        static void AnnounceCastInterrupted(Character caster, int nanoId, int reason = DefaultInterruptReason)
        {
            var message = new CharacterActionMessage
            {
                Identity = caster.Identity,
                Unknown = 0x00,
                Action = CharacterActionType.InterruptNanoCasting,
                Unknown1 = 0,
                Target = Identity.None,
                Parameter1 = nanoId,
                Parameter2 = reason,
                Unknown2 = 0
            };

            // Cast bar lives on the caster client; always deliver there first.
            SessionOf(caster)?.Send(message);

            Cell? cell = caster.Cell;
            if (cell != null)
                cell.Announce(message, exclude: caster);
        }

        static void ExecuteOnUseEffects(
            Character caster,
            Character recipient,
            NanoSpell spell,
            bool skipPassiveModifiers,
            IInventoryRepository? inventory = null,
            IItemBuilder? items = null)
        {
            if (inventory == null || items == null)
            {
                Playfield? playfield = recipient.Playfield ?? caster.Playfield;
                if (playfield == null)
                    return;

                inventory ??= playfield.GetRequiredService<IInventoryRepository>();
                items ??= playfield.GetRequiredService<IItemBuilder>();
            }

            spell.ExecuteOnUseSpells(
                recipient,
                inventory,
                items,
                skipPassiveModifiers,
                source: caster);
        }

        static void ExecuteBuffEnd(Character owner, Buff buff)
        {
            Playfield? playfield = owner.Playfield;
            if (playfield != null)
            {
                IInventoryRepository inventory = playfield.GetRequiredService<IInventoryRepository>();
                IItemBuilder items = playfield.GetRequiredService<IItemBuilder>();
                buff.ExecuteTerminateSpells(owner, inventory, items);
            }

            if (owner is not Player)
                owner.MarkRebaseDirty();
        }

        static void AnnounceBuffRemoved(Character owner, Buff buff)
        {
            var message = new BuffMessage
            {
                Identity = owner.Identity,
                Action = 0,
                NanoProgram = new Identity
                {
                    Type = IdentityType.NanoProgram,
                    Instance = buff.Id
                }
            };

            SessionOf(owner)?.Send(message);
            // Owner already has the direct copy. Everyone else who can see them needs the clear.
            owner.Cell?.Announce(message, exclude: owner);

            // End effects run after the clear: an OnTerminate that recasts the same nano (Minor Regeneration of
            // Health and Nano 291081) lands a fresh copy the client must not then see removed.
            ExecuteBuffEnd(owner, buff);
        }

        /// <summary>
        /// Duration is the buff landing. Caster and owner get a direct copy for NCU.
        /// The owner's cell gets the same packet so other players see the buff.
        /// </summary>
        static void SendNanoDuration(Character caster, Character owner, Buff buff)
        {
            CharacterActionMessage message = DurationMessage(caster, owner, buff);

            SessionOf(caster)?.Send(message);
            if (!ReferenceEquals(caster, owner))
                SessionOf(owner)?.Send(message);

            owner.Cell?.Announce(
                message,
                exclude: owner,
                alsoExclude: ReferenceEquals(caster, owner) ? null : caster);
        }

        /// <summary>
        /// The three packets <see cref="TryApplyImmediate"/> sends for a buff (.give buff):
        /// CastNanoSpell Unknown1=1, FinishNanoCasting(1, nano), then SetNanoDuration.
        /// Delivered to one player who was not in the cell when that land happened.
        /// <paramref name="source"/> stays the caster, so FinishNanoCasting is about that character.
        /// </summary>
        public static void ReplayGiveBuff(Player viewer, Character source, Character target, Buff buff)
        {
            ArgumentNullException.ThrowIfNull(viewer);
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(buff);
            IZoneSession? session = viewer.Session;
            if (session == null)
                return;

            session.Send(ImmediateLandMessage(source, target, buff.Id));
            session.Send(CastFinishedMessage(source, buff.Id));
            session.Send(DurationMessage(source, target, buff));
        }

        /// <summary>
        /// Pushes the buff's full template duration out again and matches the server deadline to it.
        /// Same SetNanoDuration packet as a fresh land, without a cast-bar announcement.
        /// </summary>
        public static void RefreshBuffDuration(Character owner, Buff buff, DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(buff);
            int duration = Math.Clamp(buff.DurationCentiseconds, 1, Buff.MaxDurationCentiseconds);
            buff.ExpiresAtUtc = nowUtc.AddMilliseconds(duration * 10L);
            SendNanoDuration(owner, owner, buff);
        }

        static CharacterActionMessage DurationMessage(Character caster, Character owner, Buff buff)
            => new()
            {
                Identity = owner.Identity,
                Unknown = 0x00,
                Action = CharacterActionType.SetNanoDuration,
                Unknown1 = 0,
                Target = new Identity
                {
                    Type = IdentityType.NanoProgram,
                    Instance = buff.Id
                },
                Parameter1 = caster.Identity.Instance,
                Parameter2 = buff.DurationCentiseconds,
                Unknown2 = 0
            };

        static void LogCastRefused(
            Character caster,
            int nanoId,
            Identity target,
            NanoCastRefusal refusal,
            string phase,
            NanoCastAttempt? attempt = null,
            NanoSpell? spell = null,
            string? detail = null)
        {
            string attemptText = attempt is NanoCastAttempt a
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    " dead={0} casting={1} recharging={2} uploaded={3} reqs={4} targetOk={5} targetDead={6} nano={7}/{8}",
                    a.CasterIsDead,
                    a.CasterIsCasting,
                    a.CasterIsRecharging,
                    a.IsUploaded,
                    a.RequirementsMet,
                    a.TargetExists,
                    a.TargetIsDead,
                    a.CurrentNano,
                    a.NanoCost)
                : string.Empty;

            string spellText = spell != null
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    " templateId={0} name={1} actions={2} isBuff={3}",
                    spell.Id,
                    spell.Name ?? string.Empty,
                    spell.Actions.Count,
                    spell.IsBuff)
                : string.Empty;

            string uploadedText = string.Format(
                CultureInfo.InvariantCulture,
                " uploadedCount={0} hasNano={1}",
                caster.UploadedNanoIds.Count,
                caster.UploadedNanoIds.Contains(nanoId));

            string reqDetail = string.Empty;
            if (refusal == NanoCastRefusal.RequirementsNotMet && spell != null)
                reqDetail = DescribeFailedRequirements(caster, spell);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Nano cast refused phase={0} caster={1} nano={2} target={3}:{4} reason={5}{6}{7}{8}{9}{10}",
                    phase,
                    caster.Identity.Instance,
                    nanoId,
                    target.Type,
                    target.Instance,
                    refusal,
                    attemptText,
                    spellText,
                    uploadedText,
                    reqDetail,
                    detail != null ? " detail=" + detail : string.Empty));
        }

        static string DescribeFailedRequirements(Character caster, NanoSpell spell)
        {
            ItemAction? action = null;
            foreach (ItemAction candidate in spell.Actions)
            {
                if (candidate.ActionType == (int)ActionType.ToUse)
                {
                    action = candidate;
                    break;
                }
            }

            if (action == null)
                return " failedReqs=(no ToUse action)";

            var parts = new List<string>();
            foreach (ItemRequirement requirement in action.Requirements)
            {
                if (ItemTemplate.IsRequirementLinkOperator(requirement) || ItemTemplate.IsSubjectSelector(requirement))
                    continue;

                int have = caster.Stats.Get((CharacterStat)requirement.StatNumber);
                if (ItemTemplate.EvaluateLeaf(requirement, stat => caster.Stats.Get(stat)))
                    continue;

                parts.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "stat={0}({1}) have={2} op={3} need={4} child={5}",
                        (CharacterStat)requirement.StatNumber,
                        requirement.StatNumber,
                        have,
                        requirement.Operator,
                        requirement.Value,
                        requirement.ChildOperator));
            }

            return parts.Count == 0
                ? " failedReqs=(expression/link fold)"
                : " failedReqs=[" + string.Join("; ", parts) + "]";
        }

        static void Refuse(Character caster, string text)
        {
            if (text.Length == 0)
                return;

            SessionOf(caster)?.Send(
                new ChatTextMessage
                {
                    Identity = caster.Identity,
                    Text = text,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                });
        }

        static IZoneSession? SessionOf(Character character)
            => character is Player player ? player.Session : null;

        static void Announce(Character character, MessageBody body)
        {
            Cell? cell = character.Cell;
            if (cell != null)
            {
                cell.Announce(body);
                return;
            }

            SessionOf(character)?.Send(body);
        }
    }
}
