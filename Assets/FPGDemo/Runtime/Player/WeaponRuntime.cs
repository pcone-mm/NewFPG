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

    public readonly struct WeaponDefinition
    {
        public const int PrimaryPelletCount = 8;

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
            int secondaryMaxImpactCount)
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
                secondaryMaxImpactCount)
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
            int secondaryMaxImpactCount)
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

        public void Reset()
        {
            HasRelease = false;
            Kind = WeaponReleaseKind.None;
            Attack = default(AttackSnapshot);
            PelletCount = 0;
        }

        internal void SetPrimary(AttackSnapshot attack, int pelletCount)
        {
            HasRelease = true;
            Kind = WeaponReleaseKind.Primary;
            Attack = attack;
            PelletCount = pelletCount;
        }

        internal void SetSecondary(AttackSnapshot attack)
        {
            HasRelease = true;
            Kind = WeaponReleaseKind.Secondary;
            Attack = attack;
            PelletCount = 0;
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
        {
            Ammo = ammo;
            State = state;
            StateUntilTick = stateUntilTick;
            SecondaryChargeStartedTick = secondaryChargeStartedTick;
            LastProcessedTick = lastProcessedTick;
            LastInputSequence = lastInputSequence;
            LastRejectReason = lastRejectReason;
            RejectedCommandCount = rejectedCommandCount;
        }

        public int Ammo { get; }
        public WeaponState State { get; }
        public TickIndex StateUntilTick { get; }
        public TickIndex SecondaryChargeStartedTick { get; }
        public TickIndex LastProcessedTick { get; }
        public long LastInputSequence { get; }
        public RejectReason LastRejectReason { get; }
        public int RejectedCommandCount { get; }
    }

    public sealed class WeaponRuntime
    {
        private readonly WeaponDefinition definition;
        private TickIndex stateUntilTick = TickIndex.Invalid;
        private TickIndex secondaryChargeStartedTick = TickIndex.Invalid;
        private TickIndex lastProcessedTick = TickIndex.Invalid;
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
                RejectedCommandCount);
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
            lastProcessedTick = snapshot.LastProcessedTick;
            lastInputSequence = snapshot.LastInputSequence;
            LastRejectReason = snapshot.LastRejectReason;
            RejectedCommandCount = snapshot.RejectedCommandCount;
            return DomainResult.Success;
        }

        public DomainResult ProcessFrame(
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
                        TryBeginSecondary(frame.Tick, exposure);
                        break;
                    case InputEdgeType.SecondaryReleased:
                        TryReleaseSecondary(
                            frame.Tick,
                            exposure,
                            ownerId,
                            idAllocator,
                            output);
                        break;
                    case InputEdgeType.ReloadPressed:
                        TryBeginReload(frame.Tick, exposure);
                        break;
                    default:
                        RegisterReject(RejectReason.InvalidState);
                        break;
                }
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
                definition.SecondaryMaxImpactCount,
                definition.SecondaryAmmoCost,
                DeterministicRandomV1.Version);

            if (!idAllocator.Commit(reservation))
            {
                throw new InvalidOperationException("Attack/Shot ID reservation changed before commit.");
            }

            Magazine.ConsumeValidated(definition.SecondaryAmmoCost);
            secondaryChargeStartedTick = TickIndex.Invalid;
            State = WeaponState.AltRecovery;
            stateUntilTick = currentTick + definition.SecondaryRecovery;
            output.SetSecondary(attack);
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

            if (output.Pellets.Length < WeaponDefinition.PrimaryPelletCount)
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
                WeaponDefinition.PrimaryPelletCount,
                WeaponDefinition.PrimaryPelletCount,
                definition.PrimaryAmmoCost,
                DeterministicRandomV1.Version);

            PelletPatternGenerator.Fill(
                scenarioSeed,
                reservation.ShotId,
                output.Pellets,
                WeaponDefinition.PrimaryPelletCount);

            if (!idAllocator.Commit(reservation))
            {
                throw new InvalidOperationException("Attack/Shot ID reservation changed before commit.");
            }

            Magazine.ConsumeValidated(definition.PrimaryAmmoCost);
            State = WeaponState.PrimaryRecovery;
            stateUntilTick = currentTick + definition.PrimaryInterval;
            output.SetPrimary(attack, WeaponDefinition.PrimaryPelletCount);
        }

        private void RegisterReject(RejectReason reason)
        {
            LastRejectReason = reason;
            RejectedCommandCount++;
        }
    }
}

