using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;

namespace FPG.Demo.Player
{
    public enum WeaponState
    {
        Ready = 0,
        PrimaryRecovery,
        AltCharging,
        AltRecovery,
        Reloading,
        Disabled
    }

    public enum WeaponReleaseKind
    {
        None = 0,
        Primary,
        Secondary
    }

    public enum SecondaryTriggerMode
    {
        ChargeRelease = 0,
        ImmediateRepeatWhileHeld
    }

    public enum WeaponSkillActionKind
    {
        None = 0,
        Primary,
        Secondary,
        Reload
    }

    public readonly struct WeaponSkillReleaseSpec
    {
        public WeaponSkillReleaseSpec(
            WeaponReleaseKind releaseKind,
            DamageSpec damage,
            QueryPolicy queryPolicy,
            AttackQueryMode queryMode,
            int payloadCount,
            int maxImpactCount,
            int ammoCost,
            int additionalPenetrationCount,
            int areaCombatantLimit,
            int areaProjectileLimit,
            AttackTargetKinds allowedTargetKinds)
        {
            ReleaseKind = releaseKind;
            Damage = damage;
            QueryPolicy = queryPolicy;
            QueryMode = queryMode;
            PayloadCount = payloadCount;
            MaxImpactCount = maxImpactCount;
            AmmoCost = ammoCost;
            AdditionalPenetrationCount = additionalPenetrationCount;
            AreaCombatantLimit = areaCombatantLimit;
            AreaProjectileLimit = areaProjectileLimit;
            AllowedTargetKinds = allowedTargetKinds;
        }

        public WeaponReleaseKind ReleaseKind { get; }
        public DamageSpec Damage { get; }
        public QueryPolicy QueryPolicy { get; }
        public AttackQueryMode QueryMode { get; }
        public int PayloadCount { get; }
        public int MaxImpactCount { get; }
        public int AmmoCost { get; }
        public int AdditionalPenetrationCount { get; }
        public int AreaCombatantLimit { get; }
        public int AreaProjectileLimit { get; }
        public AttackTargetKinds AllowedTargetKinds { get; }

        public bool IsValid
        {
            get
            {
                if (ReleaseKind != WeaponReleaseKind.Primary
                    && ReleaseKind != WeaponReleaseKind.Secondary)
                {
                    return false;
                }

                if (AmmoCost <= 0 || PayloadCount <= 0 || MaxImpactCount <= 0
                    || AllowedTargetKinds == AttackTargetKinds.None
                    || (AllowedTargetKinds & ~WeaponDefinition.PlayerAttackTargetKinds)
                        != AttackTargetKinds.None)
                {
                    return false;
                }

                if (QueryPolicy == QueryPolicy.PelletRays)
                {
                    return QueryMode == AttackQueryMode.FirstSurfacePenetration
                        && PayloadCount <= WeaponDefinition.PrimaryPelletCount
                        && AdditionalPenetrationCount >= 0
                        && AreaCombatantLimit == 0
                        && AreaProjectileLimit == 0;
                }

                if (QueryPolicy == QueryPolicy.DirectThenArea)
                {
                    return QueryMode == AttackQueryMode.AreaAtFirstSurface
                        && PayloadCount == 1
                        && AdditionalPenetrationCount == 0
                        && AreaCombatantLimit > 0
                        && AreaProjectileLimit >= 0;
                }

                return false;
            }
        }
    }

    public readonly struct WeaponDefinition
    {
        public const int PrimaryPelletCount = 8;
        public const int DefaultSecondaryAreaProjectileLimit = 4;
        public const AttackTargetKinds PlayerAttackTargetKinds =
            AttackTargetKinds.Combatant | AttackTargetKinds.Projectile;

        public WeaponDefinition(
            int definitionId,
            int magazineCapacity,
            int primaryAmmoCost,
            TickDuration primaryInterval,
            DamageSpec primaryDamage,
            int secondaryAmmoCost,
            TickDuration secondaryRecovery,
            DamageSpec secondaryDamage,
            TickDuration reloadDuration,
            int secondaryMaxImpactCount,
            SecondaryTriggerMode secondaryTriggerMode = SecondaryTriggerMode.ChargeRelease,
            AttackQueryMode primaryQueryMode = AttackQueryMode.FirstSurfacePenetration,
            int primaryAdditionalPenetrationCount = 0,
            AttackQueryMode secondaryQueryMode = AttackQueryMode.AreaAtFirstSurface,
            int secondaryAreaProjectileLimit = DefaultSecondaryAreaProjectileLimit,
            AttackTargetKinds primaryAllowedTargetKinds = PlayerAttackTargetKinds,
            AttackTargetKinds secondaryAllowedTargetKinds = PlayerAttackTargetKinds,
            int primaryPayloadCount = PrimaryPelletCount,
            int maximumAttackImpactCount = 0)
            : this(
                definitionId,
                magazineCapacity,
                primaryAmmoCost,
                primaryInterval,
                primaryDamage,
                secondaryAmmoCost,
                TickDuration.Zero,
                secondaryRecovery,
                secondaryDamage,
                reloadDuration,
                secondaryMaxImpactCount,
                secondaryTriggerMode,
                primaryQueryMode,
                primaryAdditionalPenetrationCount,
                secondaryQueryMode,
                secondaryAreaProjectileLimit,
                primaryAllowedTargetKinds,
                secondaryAllowedTargetKinds,
                primaryPayloadCount,
                maximumAttackImpactCount)
        {
        }

        public WeaponDefinition(
            int definitionId,
            int magazineCapacity,
            int primaryAmmoCost,
            TickDuration primaryInterval,
            DamageSpec primaryDamage,
            int secondaryAmmoCost,
            TickDuration secondaryMinimumCharge,
            TickDuration secondaryRecovery,
            DamageSpec secondaryDamage,
            TickDuration reloadDuration,
            int secondaryMaxImpactCount,
            SecondaryTriggerMode secondaryTriggerMode = SecondaryTriggerMode.ChargeRelease,
            AttackQueryMode primaryQueryMode = AttackQueryMode.FirstSurfacePenetration,
            int primaryAdditionalPenetrationCount = 0,
            AttackQueryMode secondaryQueryMode = AttackQueryMode.AreaAtFirstSurface,
            int secondaryAreaProjectileLimit = DefaultSecondaryAreaProjectileLimit,
            AttackTargetKinds primaryAllowedTargetKinds = PlayerAttackTargetKinds,
            AttackTargetKinds secondaryAllowedTargetKinds = PlayerAttackTargetKinds,
            int primaryPayloadCount = PrimaryPelletCount,
            int maximumAttackImpactCount = 0)
        {
            if (definitionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(definitionId));
            }

            if (magazineCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazineCapacity));
            }

            if (primaryAmmoCost <= 0 || primaryAmmoCost > magazineCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(primaryAmmoCost));
            }

            if (secondaryAmmoCost <= 0 || secondaryAmmoCost > magazineCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(secondaryAmmoCost));
            }

            if (primaryInterval.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(primaryInterval));
            }

            if (secondaryMinimumCharge.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(secondaryMinimumCharge));
            }

            if (secondaryRecovery.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(secondaryRecovery));
            }

            if (reloadDuration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reloadDuration));
            }

            if (secondaryMaxImpactCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(secondaryMaxImpactCount));
            }

            if (!Enum.IsDefined(typeof(SecondaryTriggerMode), secondaryTriggerMode))
            {
                throw new ArgumentOutOfRangeException(nameof(secondaryTriggerMode));
            }

            if (primaryQueryMode != AttackQueryMode.FirstSurfacePenetration)
            {
                throw new ArgumentOutOfRangeException(nameof(primaryQueryMode));
            }

            if (primaryPayloadCount <= 0
                || primaryPayloadCount > PrimaryPelletCount)
            {
                throw new ArgumentOutOfRangeException(nameof(primaryPayloadCount));
            }

            if (primaryAdditionalPenetrationCount < 0
                || primaryAdditionalPenetrationCount
                    > (int.MaxValue / primaryPayloadCount) - 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(primaryAdditionalPenetrationCount));
            }

            if (secondaryQueryMode != AttackQueryMode.AreaAtFirstSurface)
            {
                throw new ArgumentOutOfRangeException(nameof(secondaryQueryMode));
            }

            if (secondaryAreaProjectileLimit < 0
                || secondaryAreaProjectileLimit
                    > int.MaxValue - secondaryMaxImpactCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(secondaryAreaProjectileLimit));
            }

            if (primaryAllowedTargetKinds == AttackTargetKinds.None
                || (primaryAllowedTargetKinds & ~PlayerAttackTargetKinds)
                    != AttackTargetKinds.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(primaryAllowedTargetKinds));
            }

            if (secondaryAllowedTargetKinds == AttackTargetKinds.None
                || (secondaryAllowedTargetKinds & ~PlayerAttackTargetKinds)
                    != AttackTargetKinds.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(secondaryAllowedTargetKinds));
            }

            if (maximumAttackImpactCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumAttackImpactCount));
            }

            DefinitionId = definitionId;
            MagazineCapacity = magazineCapacity;
            PrimaryAmmoCost = primaryAmmoCost;
            PrimaryInterval = primaryInterval;
            PrimaryDamage = primaryDamage;
            SecondaryAmmoCost = secondaryAmmoCost;
            SecondaryMinimumCharge = secondaryMinimumCharge;
            SecondaryRecovery = secondaryRecovery;
            SecondaryDamage = secondaryDamage;
            ReloadDuration = reloadDuration;
            SecondaryMaxImpactCount = secondaryMaxImpactCount;
            SecondaryTriggerMode = secondaryTriggerMode;
            PrimaryQueryMode = primaryQueryMode;
            PrimaryAdditionalPenetrationCount =
                primaryAdditionalPenetrationCount;
            PrimaryPayloadCount = primaryPayloadCount;
            PrimaryMaxImpactCount = primaryPayloadCount
                * (primaryAdditionalPenetrationCount + 1);
            SecondaryQueryMode = secondaryQueryMode;
            SecondaryAreaCombatantLimit = secondaryMaxImpactCount;
            SecondaryAreaProjectileLimit = secondaryAreaProjectileLimit;
            SecondaryQueryMaxImpactCount = secondaryMaxImpactCount
                + secondaryAreaProjectileLimit;
            PrimaryAllowedTargetKinds = primaryAllowedTargetKinds;
            SecondaryAllowedTargetKinds = secondaryAllowedTargetKinds;
            MaximumAttackImpactCount = Math.Max(
                Math.Max(PrimaryMaxImpactCount, SecondaryQueryMaxImpactCount),
                maximumAttackImpactCount);
        }

        public int DefinitionId { get; }
        public int MagazineCapacity { get; }
        public int PrimaryAmmoCost { get; }
        public TickDuration PrimaryInterval { get; }
        public DamageSpec PrimaryDamage { get; }
        public int SecondaryAmmoCost { get; }
        public TickDuration SecondaryMinimumCharge { get; }
        public TickDuration SecondaryRecovery { get; }
        public DamageSpec SecondaryDamage { get; }
        public TickDuration ReloadDuration { get; }
        public int SecondaryMaxImpactCount { get; }
        public SecondaryTriggerMode SecondaryTriggerMode { get; }
        public AttackQueryMode PrimaryQueryMode { get; }
        public int PrimaryAdditionalPenetrationCount { get; }
        public int PrimaryPayloadCount { get; }
        public int PrimaryMaxImpactCount { get; }
        public AttackQueryMode SecondaryQueryMode { get; }
        public int SecondaryAreaCombatantLimit { get; }
        public int SecondaryAreaProjectileLimit { get; }
        public int SecondaryQueryMaxImpactCount { get; }
        public AttackTargetKinds PrimaryAllowedTargetKinds { get; }
        public AttackTargetKinds SecondaryAllowedTargetKinds { get; }
        public int MaximumAttackImpactCount { get; }
    }

    public sealed class Magazine
    {
        public Magazine(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
            Ammo = capacity;
        }

        public int Capacity { get; }
        public int Ammo { get; private set; }

        public bool CanConsume(int amount)
        {
            return amount > 0 && Ammo >= amount;
        }

        public void ConsumeValidated(int amount)
        {
            if (!CanConsume(amount))
            {
                throw new InvalidOperationException("Ammo was not validated before commit.");
            }

            Ammo -= amount;
        }

        public void Refill()
        {
            Ammo = Capacity;
        }

        public DomainResult RestoreAmmo(int ammo)
        {
            if (ammo < 0 || ammo > Capacity)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            Ammo = ammo;
            return DomainResult.Success;
        }
    }

    public sealed class WeaponReleaseBuffer
    {
        private readonly PelletSample[] pellets;

        public WeaponReleaseBuffer(int pelletCapacity = WeaponDefinition.PrimaryPelletCount)
        {
            if (pelletCapacity < WeaponDefinition.PrimaryPelletCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pelletCapacity));
            }

            pellets = new PelletSample[pelletCapacity];
        }

        public bool HasRelease { get; private set; }
        public WeaponReleaseKind Kind { get; private set; }
        public AttackSnapshot Attack { get; private set; }
        public PelletSample[] Pellets => pellets;
        public int PelletCount { get; private set; }
        public bool IsCommitted { get; private set; }
        public bool IsSkillRelease { get; private set; }

        internal AttackShotReservation PreparedReservation { get; private set; }

        public void Reset()
        {
            HasRelease = false;
            Kind = WeaponReleaseKind.None;
            Attack = default(AttackSnapshot);
            PelletCount = 0;
            IsCommitted = false;
            IsSkillRelease = false;
            PreparedReservation = default(AttackShotReservation);
        }

        internal void SetPrimary(
            AttackSnapshot attack,
            int pelletCount,
            AttackShotReservation reservation)
        {
            HasRelease = true;
            Kind = WeaponReleaseKind.Primary;
            Attack = attack;
            PelletCount = pelletCount;
            PreparedReservation = reservation;
            IsSkillRelease = false;
        }

        internal void SetSecondary(
            AttackSnapshot attack,
            AttackShotReservation reservation)
        {
            HasRelease = true;
            Kind = WeaponReleaseKind.Secondary;
            Attack = attack;
            PelletCount = 0;
            PreparedReservation = reservation;
            IsSkillRelease = false;
        }

        internal void SetSkillRelease(
            WeaponReleaseKind kind,
            AttackSnapshot attack,
            int pelletCount,
            AttackShotReservation reservation)
        {
            HasRelease = true;
            Kind = kind;
            Attack = attack;
            PelletCount = pelletCount;
            PreparedReservation = reservation;
            IsSkillRelease = true;
        }

        internal void MarkCommitted()
        {
            IsCommitted = true;
        }
    }

    public static class PelletPatternGenerator
    {
        public static void Fill(
            ulong scenarioSeed,
            ShotId shotId,
            PelletSample[] output,
            int pelletCount)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (pelletCount < 0 || pelletCount > output.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(pelletCount));
            }

            for (int pelletIndex = 0; pelletIndex < pelletCount; pelletIndex++)
            {
                int spreadU = DeterministicRandomV1.SampleUInt24(
                    scenarioSeed,
                    RandomDomain.PelletSpread,
                    unchecked((ulong)shotId.Value),
                    (ulong)(pelletIndex * 2));
                int spreadV = DeterministicRandomV1.SampleUInt24(
                    scenarioSeed,
                    RandomDomain.PelletSpread,
                    unchecked((ulong)shotId.Value),
                    (ulong)(pelletIndex * 2 + 1));
                output[pelletIndex] = new PelletSample(shotId, pelletIndex, spreadU, spreadV);
            }
        }
    }

    public readonly struct WeaponRuntimeSnapshot
    {
        public WeaponRuntimeSnapshot(
            int ammo,
            WeaponState state,
            TickIndex stateUntilTick,
            TickIndex secondaryChargeStartedTick,
            TickIndex lastProcessedTick,
            long lastInputSequence,
            RejectReason lastRejectReason,
            int rejectedCommandCount)
            : this(
                ammo,
                state,
                stateUntilTick,
                secondaryChargeStartedTick,
                lastProcessedTick,
                lastInputSequence,
                lastRejectReason,
                rejectedCommandCount,
                TickIndex.Invalid,
                TickIndex.Invalid)
        {
        }

        public WeaponRuntimeSnapshot(
            int ammo,
            WeaponState state,
            TickIndex stateUntilTick,
            TickIndex secondaryChargeStartedTick,
            TickIndex lastProcessedTick,
            long lastInputSequence,
            RejectReason lastRejectReason,
            int rejectedCommandCount,
            TickIndex primaryRecastLockedUntilTick,
            TickIndex secondaryRecastLockedUntilTick)
        {
            Ammo = ammo;
            State = state;
            StateUntilTick = stateUntilTick;
            SecondaryChargeStartedTick = secondaryChargeStartedTick;
            LastProcessedTick = lastProcessedTick;
            LastInputSequence = lastInputSequence;
            LastRejectReason = lastRejectReason;
            RejectedCommandCount = rejectedCommandCount;
            PrimaryRecastLockedUntilTick = primaryRecastLockedUntilTick;
            SecondaryRecastLockedUntilTick = secondaryRecastLockedUntilTick;
        }

        public int Ammo { get; }
        public WeaponState State { get; }
        public TickIndex StateUntilTick { get; }
        public TickIndex SecondaryChargeStartedTick { get; }
        public TickIndex LastProcessedTick { get; }
        public long LastInputSequence { get; }
        public RejectReason LastRejectReason { get; }
        public int RejectedCommandCount { get; }
        public TickIndex PrimaryRecastLockedUntilTick { get; }
        public TickIndex SecondaryRecastLockedUntilTick { get; }
    }

    public sealed class WeaponRuntime
    {
        private readonly WeaponDefinition definition;
        private TickIndex stateUntilTick = TickIndex.Invalid;
        private TickIndex secondaryChargeStartedTick = TickIndex.Invalid;
        private TickIndex lastProcessedTick = TickIndex.Invalid;
        private TickIndex primaryRecastLockedUntilTick = TickIndex.Invalid;
        private TickIndex secondaryRecastLockedUntilTick = TickIndex.Invalid;
        private long lastInputSequence;

        public WeaponRuntime(WeaponDefinition definition)
        {
            this.definition = definition;
            Magazine = new Magazine(definition.MagazineCapacity);
            State = WeaponState.Ready;
        }

        public WeaponDefinition Definition => definition;
        public Magazine Magazine { get; }
        public WeaponState State { get; private set; }
        public TickIndex StateUntilTick => stateUntilTick;
        public TickIndex SecondaryChargeStartedTick => secondaryChargeStartedTick;
        public TickIndex PrimaryRecastLockedUntilTick =>
            primaryRecastLockedUntilTick;
        public TickIndex SecondaryRecastLockedUntilTick =>
            secondaryRecastLockedUntilTick;
        public TickIndex LastProcessedTick => lastProcessedTick;
        public InputSequence LastInputSequence => new InputSequence(lastInputSequence);
        public RejectReason LastRejectReason { get; private set; }
        public int RejectedCommandCount { get; private set; }

        public WeaponRuntimeSnapshot CaptureRoomSnapshot()
        {
            return new WeaponRuntimeSnapshot(
                Magazine.Ammo,
                State,
                stateUntilTick,
                secondaryChargeStartedTick,
                lastProcessedTick,
                lastInputSequence,
                LastRejectReason,
                RejectedCommandCount,
                primaryRecastLockedUntilTick,
                secondaryRecastLockedUntilTick);
        }

        public DomainResult RestoreRoomSnapshot(in WeaponRuntimeSnapshot snapshot)
        {
            if (!Enum.IsDefined(typeof(WeaponState), snapshot.State)
                || !Enum.IsDefined(typeof(RejectReason), snapshot.LastRejectReason)
                || snapshot.LastInputSequence < 0
                || snapshot.RejectedCommandCount < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            DomainResult ammoResult = Magazine.RestoreAmmo(snapshot.Ammo);
            if (!ammoResult.IsSuccess)
            {
                return ammoResult;
            }

            State = snapshot.State;
            stateUntilTick = snapshot.StateUntilTick;
            secondaryChargeStartedTick = snapshot.SecondaryChargeStartedTick;
            primaryRecastLockedUntilTick =
                snapshot.PrimaryRecastLockedUntilTick;
            secondaryRecastLockedUntilTick =
                snapshot.SecondaryRecastLockedUntilTick;
            lastProcessedTick = snapshot.LastProcessedTick;
            lastInputSequence = snapshot.LastInputSequence;
            LastRejectReason = snapshot.LastRejectReason;
            RejectedCommandCount = snapshot.RejectedCommandCount;
            return DomainResult.Success;
        }

        public DomainResult AdvanceSkillFrame(TickIndex tick)
        {
            LastRejectReason = RejectReason.None;
            if (State == WeaponState.Disabled)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!tick.IsValid
                || (lastProcessedTick.IsValid
                    && tick.Value != lastProcessedTick.Value + 1L))
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            Advance(tick);
            lastProcessedTick = tick;
            return DomainResult.Success;
        }

        public bool TryAcceptSkillInputCommand(in InputEdgeCommand command)
        {
            if (!command.Sequence.IsValid)
            {
                RegisterReject(RejectReason.InvalidState);
                return false;
            }

            if (command.Sequence.Value <= lastInputSequence)
            {
                RegisterReject(RejectReason.DuplicateSequence);
                return false;
            }

            lastInputSequence = command.Sequence.Value;
            return true;
        }

        public DomainResult TryBeginSkillAction(
            WeaponSkillActionKind actionKind,
            TickIndex currentTick,
            TickIndex lockedUntilTick,
            int requiredAmmo,
            ExposureRuntime exposure,
            bool allowRecoveryInterrupt = false)
        {
            if (exposure == null)
            {
                throw new ArgumentNullException(nameof(exposure));
            }

            if (!Enum.IsDefined(typeof(WeaponSkillActionKind), actionKind)
                || actionKind == WeaponSkillActionKind.None
                || !currentTick.IsValid
                || !lockedUntilTick.IsValid
                || lockedUntilTick <= currentTick
                || requiredAmmo < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            bool recoveryInterruptAllowed = allowRecoveryInterrupt
                && actionKind != WeaponSkillActionKind.Reload
                && (State == WeaponState.PrimaryRecovery
                    || State == WeaponState.AltRecovery);
            if (State != WeaponState.Ready && !recoveryInterruptAllowed)
            {
                RejectReason reason = actionKind == WeaponSkillActionKind.Reload
                    ? RejectReason.ActionLocked
                    : RejectReason.Cooldown;
                RegisterReject(reason);
                return DomainResult.Rejected(reason);
            }

            if (actionKind == WeaponSkillActionKind.Reload)
            {
                if (requiredAmmo != 0 || Magazine.Ammo >= Magazine.Capacity)
                {
                    RegisterReject(RejectReason.InvalidState);
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                if (exposure.State != PlayerExposureState.Withdrawn)
                {
                    DomainResult withdraw =
                        exposure.ApplyReloadPosture(currentTick, out _);
                    if (!withdraw.IsSuccess)
                    {
                        RegisterReject(withdraw.RejectReason);
                        return withdraw;
                    }
                }

                State = WeaponState.Reloading;
            }
            else
            {
                TickIndex recastLock = actionKind == WeaponSkillActionKind.Primary
                    ? primaryRecastLockedUntilTick
                    : secondaryRecastLockedUntilTick;
                if (recastLock.IsValid && currentTick < recastLock)
                {
                    RegisterReject(RejectReason.Cooldown);
                    return DomainResult.Rejected(RejectReason.Cooldown);
                }

                if (!exposure.IsExposed)
                {
                    RegisterReject(RejectReason.NotExposed);
                    return DomainResult.Rejected(RejectReason.NotExposed);
                }

                if (requiredAmmo <= 0 || Magazine.Ammo < requiredAmmo)
                {
                    RegisterReject(RejectReason.NotEnoughAmmo);
                    return DomainResult.Rejected(RejectReason.NotEnoughAmmo);
                }

                State = actionKind == WeaponSkillActionKind.Primary
                    ? WeaponState.PrimaryRecovery
                    : WeaponState.AltRecovery;
            }

            secondaryChargeStartedTick = TickIndex.Invalid;
            stateUntilTick = lockedUntilTick;
            return DomainResult.Success;
        }

        public DomainResult TryBeginSkillSecondaryCharge(
            TickIndex currentTick,
            ExposureRuntime exposure,
            bool allowRecoveryInterrupt = false)
        {
            if (exposure == null)
            {
                throw new ArgumentNullException(nameof(exposure));
            }

            if (!exposure.IsExposed)
            {
                RegisterReject(RejectReason.NotExposed);
                return DomainResult.Rejected(RejectReason.NotExposed);
            }

            bool recoveryInterruptAllowed = allowRecoveryInterrupt
                && (State == WeaponState.PrimaryRecovery
                    || State == WeaponState.AltRecovery);
            if (State != WeaponState.Ready && !recoveryInterruptAllowed)
            {
                RegisterReject(RejectReason.ActionLocked);
                return DomainResult.Rejected(RejectReason.ActionLocked);
            }

            if (secondaryRecastLockedUntilTick.IsValid
                && currentTick < secondaryRecastLockedUntilTick)
            {
                RegisterReject(RejectReason.Cooldown);
                return DomainResult.Rejected(RejectReason.Cooldown);
            }

            if (!Magazine.CanConsume(definition.SecondaryAmmoCost))
            {
                RegisterReject(RejectReason.NotEnoughAmmo);
                return DomainResult.Rejected(RejectReason.NotEnoughAmmo);
            }

            State = WeaponState.AltCharging;
            secondaryChargeStartedTick = currentTick;
            stateUntilTick = TickIndex.Invalid;
            return DomainResult.Success;
        }

        public DomainResult TryFinishSkillSecondaryCharge(
            TickIndex currentTick,
            out bool charged)
        {
            charged = false;
            if (State != WeaponState.AltCharging)
            {
                RegisterReject(RejectReason.ActionLocked);
                return DomainResult.Rejected(RejectReason.ActionLocked);
            }

            if (!HasReachedSecondaryMinimumCharge(currentTick))
            {
                CancelSecondaryCharge();
                return DomainResult.Success;
            }

            State = WeaponState.Ready;
            stateUntilTick = TickIndex.Invalid;
            secondaryChargeStartedTick = TickIndex.Invalid;
            charged = true;
            return DomainResult.Success;
        }

        public void CancelSkillSecondaryCharge()
        {
            CancelSecondaryCharge();
        }

        public DomainResult PrepareSkillRelease(
            TickIndex tick,
            RuntimeId ownerId,
            SessionIdAllocator idAllocator,
            ulong scenarioSeed,
            in WeaponSkillReleaseSpec releaseSpec,
            WeaponReleaseBuffer output)
        {
            if (idAllocator == null)
            {
                throw new ArgumentNullException(nameof(idAllocator));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.Reset();
            if (!releaseSpec.IsValid || !ownerId.IsValid || !tick.IsValid
                || tick != lastProcessedTick || State == WeaponState.Disabled)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            bool validState = releaseSpec.ReleaseKind == WeaponReleaseKind.Primary
                ? State == WeaponState.PrimaryRecovery
                : State == WeaponState.AltRecovery
                    || State == WeaponState.AltCharging;
            if (!validState)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!Magazine.CanConsume(releaseSpec.AmmoCost))
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            int pelletCount = releaseSpec.QueryPolicy == QueryPolicy.PelletRays
                ? releaseSpec.PayloadCount
                : 0;
            if (pelletCount > output.Pellets.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            AttackShotReservation reservation = idAllocator.ReserveAttackAndShot();
            AttackSnapshot attack = new AttackSnapshot(
                reservation.AttackId,
                reservation.ShotId,
                definition.DefinitionId,
                ownerId,
                Team.Player,
                tick,
                releaseSpec.Damage,
                releaseSpec.QueryPolicy,
                releaseSpec.PayloadCount,
                releaseSpec.MaxImpactCount,
                releaseSpec.AmmoCost,
                DeterministicRandomV1.Version,
                releaseSpec.QueryMode,
                releaseSpec.AdditionalPenetrationCount,
                releaseSpec.AreaCombatantLimit,
                releaseSpec.AreaProjectileLimit,
                releaseSpec.AllowedTargetKinds);

            if (pelletCount > 0)
            {
                PelletPatternGenerator.Fill(
                    scenarioSeed,
                    reservation.ShotId,
                    output.Pellets,
                    pelletCount);
            }

            output.SetSkillRelease(
                releaseSpec.ReleaseKind,
                attack,
                pelletCount,
                reservation);
            return DomainResult.Success;
        }

        public DomainResult CommitPreparedSkillRelease(
            WeaponReleaseBuffer output,
            SessionIdAllocator idAllocator)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (idAllocator == null)
            {
                throw new ArgumentNullException(nameof(idAllocator));
            }

            if (!output.HasRelease || !output.IsSkillRelease
                || output.IsCommitted
                || output.Attack.DefinitionId != definition.DefinitionId
                || output.Attack.ReleaseTick != lastProcessedTick
                || output.PreparedReservation.AttackId != output.Attack.AttackId
                || output.PreparedReservation.ShotId != output.Attack.ShotId
                || output.Attack.AmmoCost <= 0
                || !Magazine.CanConsume(output.Attack.AmmoCost))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!idAllocator.Commit(output.PreparedReservation))
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            Magazine.ConsumeValidated(output.Attack.AmmoCost);
            output.MarkCommitted();
            TickIndex recastUntil = output.Attack.ReleaseTick
                + (output.Kind == WeaponReleaseKind.Primary
                    ? definition.PrimaryInterval
                    : definition.SecondaryRecovery);
            if (output.Kind == WeaponReleaseKind.Primary)
            {
                primaryRecastLockedUntilTick = recastUntil;
            }
            else
            {
                secondaryRecastLockedUntilTick = recastUntil;
            }
            return DomainResult.Success;
        }

        public DomainResult CommitSkillReloadEvent(TickIndex tick)
        {
            if (State != WeaponState.Reloading || tick != lastProcessedTick)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            Magazine.Refill();
            return DomainResult.Success;
        }

        public void CancelSkillAction()
        {
            if (State == WeaponState.PrimaryRecovery
                || State == WeaponState.AltRecovery
                || State == WeaponState.Reloading
                || State == WeaponState.AltCharging)
            {
                State = WeaponState.Ready;
                stateUntilTick = TickIndex.Invalid;
                secondaryChargeStartedTick = TickIndex.Invalid;
            }
        }

        public void InterruptSkillAction(
            WeaponSkillActionKind actionKind,
            TickIndex interruptTick,
            TickIndex recastLockedUntilTick)
        {
            if (State == WeaponState.Disabled)
            {
                return;
            }

            secondaryChargeStartedTick = TickIndex.Invalid;
            TickIndex effectiveRecastLock = recastLockedUntilTick;
            if (actionKind == WeaponSkillActionKind.Primary
                && primaryRecastLockedUntilTick.IsValid
                && (!effectiveRecastLock.IsValid
                    || primaryRecastLockedUntilTick > effectiveRecastLock))
            {
                effectiveRecastLock = primaryRecastLockedUntilTick;
            }
            else if (actionKind == WeaponSkillActionKind.Secondary
                && secondaryRecastLockedUntilTick.IsValid
                && (!effectiveRecastLock.IsValid
                    || secondaryRecastLockedUntilTick > effectiveRecastLock))
            {
                effectiveRecastLock = secondaryRecastLockedUntilTick;
            }

            if (interruptTick.IsValid
                && effectiveRecastLock.IsValid
                && effectiveRecastLock > interruptTick
                && (actionKind == WeaponSkillActionKind.Primary
                    || actionKind == WeaponSkillActionKind.Secondary))
            {
                State = actionKind == WeaponSkillActionKind.Primary
                    ? WeaponState.PrimaryRecovery
                    : WeaponState.AltRecovery;
                stateUntilTick = effectiveRecastLock;
                return;
            }

            State = WeaponState.Ready;
            stateUntilTick = TickIndex.Invalid;
        }

        public DomainResult ProcessFrame(
            PlayerInputFrame frame,
            ExposureRuntime exposure,
            RuntimeId ownerId,
            SessionIdAllocator idAllocator,
            ulong scenarioSeed,
            WeaponReleaseBuffer output)
        {
            DomainResult prepared = PrepareFrame(
                frame,
                exposure,
                ownerId,
                idAllocator,
                scenarioSeed,
                output);
            if (!prepared.IsSuccess || !output.HasRelease)
            {
                return prepared;
            }

            return CommitPreparedRelease(output, idAllocator);
        }

        /// <summary>
        /// Advances input state and prepares an immutable attack snapshot without
        /// consuming ammo, advancing attack IDs or entering weapon recovery.
        /// Formal adapters can query and preflight the complete hit batch before
        /// committing the release.
        /// </summary>
        public DomainResult PrepareFrame(
            PlayerInputFrame frame,
            ExposureRuntime exposure,
            RuntimeId ownerId,
            SessionIdAllocator idAllocator,
            ulong scenarioSeed,
            WeaponReleaseBuffer output)
        {
            if (exposure == null)
            {
                throw new ArgumentNullException(nameof(exposure));
            }

            if (idAllocator == null)
            {
                throw new ArgumentNullException(nameof(idAllocator));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.Reset();
            LastRejectReason = RejectReason.None;

            if (State == WeaponState.Disabled)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!frame.Tick.IsValid
                || (lastProcessedTick.IsValid && frame.Tick.Value != lastProcessedTick.Value + 1L))
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            Advance(frame.Tick);
            bool immediateSecondaryRequested = false;

            for (int commandIndex = 0; commandIndex < frame.EdgeCommandCount; commandIndex++)
            {
                InputEdgeCommand command = frame.EdgeCommands[commandIndex];
                if (!command.Sequence.IsValid)
                {
                    RegisterReject(RejectReason.InvalidState);
                    continue;
                }

                if (command.Sequence.Value <= lastInputSequence)
                {
                    RegisterReject(RejectReason.DuplicateSequence);
                    continue;
                }

                lastInputSequence = command.Sequence.Value;
                switch (command.Type)
                {
                    case InputEdgeType.SecondaryPressed:
                        if (definition.SecondaryTriggerMode
                            == SecondaryTriggerMode.ImmediateRepeatWhileHeld)
                        {
                            immediateSecondaryRequested = true;
                        }
                        else
                        {
                            TryBeginSecondary(frame.Tick, exposure);
                        }
                        break;
                    case InputEdgeType.SecondaryReleased:
                        if (definition.SecondaryTriggerMode
                            == SecondaryTriggerMode.ChargeRelease)
                        {
                            TryReleaseSecondary(
                                frame.Tick,
                                exposure,
                                ownerId,
                                idAllocator,
                                output);
                        }
                        break;
                    case InputEdgeType.ReloadPressed:
                        TryBeginReload(frame.Tick, exposure);
                        break;
                    default:
                        RegisterReject(RejectReason.InvalidState);
                        break;
                }
            }

            if (definition.SecondaryTriggerMode
                    == SecondaryTriggerMode.ImmediateRepeatWhileHeld
                && (frame.SecondaryHeld || immediateSecondaryRequested)
                && !output.HasRelease)
            {
                TryReleaseImmediateSecondary(
                    frame.Tick,
                    exposure,
                    ownerId,
                    idAllocator,
                    output);
            }

            if (frame.PrimaryHeld && !output.HasRelease)
            {
                TryReleasePrimary(
                    frame.Tick,
                    exposure,
                    ownerId,
                    idAllocator,
                    scenarioSeed,
                    output);
            }

            lastProcessedTick = frame.Tick;
            return DomainResult.Success;
        }

        public DomainResult CommitPreparedRelease(
            WeaponReleaseBuffer output,
            SessionIdAllocator idAllocator)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (idAllocator == null)
            {
                throw new ArgumentNullException(nameof(idAllocator));
            }

            if (!output.HasRelease || output.IsCommitted
                || output.Attack.DefinitionId != definition.DefinitionId
                || output.Attack.ReleaseTick != lastProcessedTick
                || output.PreparedReservation.AttackId != output.Attack.AttackId
                || output.PreparedReservation.ShotId != output.Attack.ShotId)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            int ammoCost;
            TickDuration recovery;
            WeaponState recoveryState;
            if (output.Kind == WeaponReleaseKind.Primary)
            {
                if (State != WeaponState.Ready
                    || output.PelletCount != definition.PrimaryPayloadCount)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                ammoCost = definition.PrimaryAmmoCost;
                recovery = definition.PrimaryInterval;
                recoveryState = WeaponState.PrimaryRecovery;
            }
            else if (output.Kind == WeaponReleaseKind.Secondary)
            {
                WeaponState requiredState = definition.SecondaryTriggerMode
                    == SecondaryTriggerMode.ChargeRelease
                        ? WeaponState.AltCharging
                        : WeaponState.Ready;
                if (State != requiredState || output.PelletCount != 0)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                ammoCost = definition.SecondaryAmmoCost;
                recovery = definition.SecondaryRecovery;
                recoveryState = WeaponState.AltRecovery;
            }
            else
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (output.Attack.AmmoCost != ammoCost || !Magazine.CanConsume(ammoCost))
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            if (!idAllocator.Commit(output.PreparedReservation))
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            Magazine.ConsumeValidated(ammoCost);
            if (output.Kind == WeaponReleaseKind.Secondary)
            {
                secondaryChargeStartedTick = TickIndex.Invalid;
            }

            State = recoveryState;
            stateUntilTick = output.Attack.ReleaseTick + recovery;
            if (output.Kind == WeaponReleaseKind.Primary)
            {
                primaryRecastLockedUntilTick = stateUntilTick;
            }
            else
            {
                secondaryRecastLockedUntilTick = stateUntilTick;
            }
            output.MarkCommitted();
            return DomainResult.Success;
        }

        public void CancelForWithdrawn()
        {
            if (State == WeaponState.AltCharging)
            {
                CancelSecondaryCharge();
            }
        }

        public bool CancelReload()
        {
            if (State != WeaponState.Reloading)
            {
                return false;
            }

            State = WeaponState.Ready;
            stateUntilTick = TickIndex.Invalid;
            return true;
        }

        public void Disable()
        {
            CancelSecondaryCharge();
            State = WeaponState.Disabled;
            stateUntilTick = TickIndex.Invalid;
        }

        private void Advance(TickIndex currentTick)
        {
            if (!stateUntilTick.IsValid || currentTick < stateUntilTick)
            {
                return;
            }

            if (State == WeaponState.Reloading)
            {
                Magazine.Refill();
            }

            if (State == WeaponState.PrimaryRecovery
                || State == WeaponState.AltRecovery
                || State == WeaponState.Reloading)
            {
                State = WeaponState.Ready;
                stateUntilTick = TickIndex.Invalid;
            }
        }

        private void TryBeginSecondary(TickIndex currentTick, ExposureRuntime exposure)
        {
            if (!exposure.IsExposed)
            {
                RegisterReject(RejectReason.NotExposed);
                return;
            }

            if (State != WeaponState.Ready)
            {
                RegisterReject(RejectReason.ActionLocked);
                return;
            }

            if (!Magazine.CanConsume(definition.SecondaryAmmoCost))
            {
                RegisterReject(RejectReason.NotEnoughAmmo);
                return;
            }

            State = WeaponState.AltCharging;
            secondaryChargeStartedTick = currentTick;
        }

        private void TryReleaseSecondary(
            TickIndex currentTick,
            ExposureRuntime exposure,
            RuntimeId ownerId,
            SessionIdAllocator idAllocator,
            WeaponReleaseBuffer output)
        {
            if (!exposure.IsExposed)
            {
                RegisterReject(RejectReason.NotExposed);
                return;
            }

            if (State != WeaponState.AltCharging)
            {
                if (State == WeaponState.Ready
                    && !Magazine.CanConsume(definition.SecondaryAmmoCost))
                {
                    RegisterReject(RejectReason.NotEnoughAmmo);
                    return;
                }

                RegisterReject(RejectReason.ActionLocked);
                return;
            }

            if (!HasReachedSecondaryMinimumCharge(currentTick))
            {
                // Early release is an intentional cancel, not a rejected
                // attack. It must neither reserve IDs nor consume shared ammo.
                CancelSecondaryCharge();
                return;
            }

            if (!Magazine.CanConsume(definition.SecondaryAmmoCost))
            {
                RegisterReject(RejectReason.NotEnoughAmmo);
                CancelSecondaryCharge();
                return;
            }

            PrepareSecondary(
                currentTick,
                ownerId,
                idAllocator,
                output);
        }

        private void TryReleaseImmediateSecondary(
            TickIndex currentTick,
            ExposureRuntime exposure,
            RuntimeId ownerId,
            SessionIdAllocator idAllocator,
            WeaponReleaseBuffer output)
        {
            if (!exposure.IsExposed)
            {
                RegisterReject(RejectReason.NotExposed);
                return;
            }

            if (State != WeaponState.Ready)
            {
                RegisterReject(RejectReason.Cooldown);
                return;
            }

            if (!Magazine.CanConsume(definition.SecondaryAmmoCost))
            {
                RegisterReject(RejectReason.NotEnoughAmmo);
                return;
            }

            PrepareSecondary(
                currentTick,
                ownerId,
                idAllocator,
                output);
        }

        private void PrepareSecondary(
            TickIndex currentTick,
            RuntimeId ownerId,
            SessionIdAllocator idAllocator,
            WeaponReleaseBuffer output)
        {
            AttackShotReservation reservation = idAllocator.ReserveAttackAndShot();
            AttackSnapshot attack = new AttackSnapshot(
                reservation.AttackId,
                reservation.ShotId,
                definition.DefinitionId,
                ownerId,
                Team.Player,
                currentTick,
                definition.SecondaryDamage,
                QueryPolicy.DirectThenArea,
                1,
                definition.SecondaryQueryMaxImpactCount,
                definition.SecondaryAmmoCost,
                DeterministicRandomV1.Version,
                definition.SecondaryQueryMode,
                0,
                definition.SecondaryAreaCombatantLimit,
                definition.SecondaryAreaProjectileLimit,
                definition.SecondaryAllowedTargetKinds);

            output.SetSecondary(attack, reservation);
        }

        private bool HasReachedSecondaryMinimumCharge(TickIndex currentTick)
        {
            return secondaryChargeStartedTick.IsValid
                && currentTick.IsValid
                && currentTick.Value - secondaryChargeStartedTick.Value
                    >= definition.SecondaryMinimumCharge.Value;
        }

        private void CancelSecondaryCharge()
        {
            if (State == WeaponState.AltCharging)
            {
                State = WeaponState.Ready;
                stateUntilTick = TickIndex.Invalid;
            }

            secondaryChargeStartedTick = TickIndex.Invalid;
        }

        private void TryBeginReload(
            TickIndex currentTick,
            ExposureRuntime exposure)
        {
            if (State != WeaponState.Ready)
            {
                RegisterReject(RejectReason.ActionLocked);
                return;
            }

            if (Magazine.Ammo >= Magazine.Capacity)
            {
                RegisterReject(RejectReason.InvalidState);
                return;
            }

            if (exposure.State != PlayerExposureState.Withdrawn)
            {
                DomainResult withdrawResult =
                    exposure.ApplyReloadPosture(currentTick, out _);
                if (!withdrawResult.IsSuccess)
                {
                    RegisterReject(withdrawResult.RejectReason);
                    return;
                }
            }

            State = WeaponState.Reloading;
            stateUntilTick = currentTick + definition.ReloadDuration;
        }

        private void TryReleasePrimary(
            TickIndex currentTick,
            ExposureRuntime exposure,
            RuntimeId ownerId,
            SessionIdAllocator idAllocator,
            ulong scenarioSeed,
            WeaponReleaseBuffer output)
        {
            if (!exposure.IsExposed)
            {
                RegisterReject(RejectReason.NotExposed);
                return;
            }

            if (State != WeaponState.Ready)
            {
                RegisterReject(RejectReason.Cooldown);
                return;
            }

            if (!Magazine.CanConsume(definition.PrimaryAmmoCost))
            {
                RegisterReject(RejectReason.NotEnoughAmmo);
                return;
            }

            if (output.Pellets.Length < definition.PrimaryPayloadCount)
            {
                RegisterReject(RejectReason.BufferCapacity);
                return;
            }

            AttackShotReservation reservation = idAllocator.ReserveAttackAndShot();
            AttackSnapshot attack = new AttackSnapshot(
                reservation.AttackId,
                reservation.ShotId,
                definition.DefinitionId,
                ownerId,
                Team.Player,
                currentTick,
                definition.PrimaryDamage,
                QueryPolicy.PelletRays,
                definition.PrimaryPayloadCount,
                definition.PrimaryMaxImpactCount,
                definition.PrimaryAmmoCost,
                DeterministicRandomV1.Version,
                definition.PrimaryQueryMode,
                definition.PrimaryAdditionalPenetrationCount,
                0,
                0,
                definition.PrimaryAllowedTargetKinds);

            PelletPatternGenerator.Fill(
                scenarioSeed,
                reservation.ShotId,
                output.Pellets,
                definition.PrimaryPayloadCount);

            output.SetPrimary(
                attack,
                definition.PrimaryPayloadCount,
                reservation);
        }

        private void RegisterReject(RejectReason reason)
        {
            LastRejectReason = reason;
            RejectedCommandCount++;
        }
    }
}
