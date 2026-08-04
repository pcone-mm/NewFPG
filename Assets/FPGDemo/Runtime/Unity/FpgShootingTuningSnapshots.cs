using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Immutable, resolved view of the authoritative shooting configuration
    /// chain. Asset references are retained only as provenance; runtime users
    /// should consume the captured primitive values.
    /// </summary>
    public readonly struct FpgShootingTuningSnapshot
    {
        public const int TickRate = FpgSkillRuntimeConstants.TickRate;

        private FpgShootingTuningSnapshot(
            D0CharacterDefinition character,
            D0ThreeCProfile threeCProfile,
            D0CombatFeelProfile combatFeelProfile,
            D0WeaponDefinition weapon,
            FpgPlayerSkillDefinition primarySkill,
            FpgPlayerSkillDefinition secondarySkill,
            FpgPlayerSkillDefinition reloadSkill,
            SecondaryTriggerMode secondaryTriggerMode,
            float maximumAimDistance,
            Rect reticleSafeViewport,
            float mouseReticleSensitivity,
            Vector2 mouseReferenceResolution,
            float gamepadReticleSpeed,
            float gamepadReticleDeadzone,
            float gamepadReticleResponseExponent,
            int inputBufferTicks,
            float peekTransitionSeconds,
            float facingFlipDelaySeconds,
            float facingFlipDurationSeconds,
            float retractTransitionSeconds,
            float coverTraversalSeconds,
            float primarySpreadTangent,
            float secondaryAreaRadius,
            int magazineCapacity,
            int primaryPelletCount,
            DamageSpec primaryDamage,
            DamageSpec secondaryDamage,
            int primaryAmmoCost,
            int secondaryAmmoCost,
            int primaryActionLockTicks,
            int secondaryActionLockTicks,
            int primaryCooldownTicks,
            int secondaryCooldownTicks,
            int primaryAttackCommitTick,
            int secondaryAttackCommitTick,
            int reloadCommitTick,
            int reloadDurationTicks,
            int reloadLockTicks,
            float primaryCameraKick,
            float secondaryCameraKick,
            float cameraKickRecoverySeconds,
            FpgResolvedSkillTimingSnapshot primaryTiming)
        {
            Character = character;
            ThreeCProfile = threeCProfile;
            CombatFeelProfile = combatFeelProfile;
            Weapon = weapon;
            PrimarySkill = primarySkill;
            SecondarySkill = secondarySkill;
            ReloadSkill = reloadSkill;
            SecondaryTriggerMode = secondaryTriggerMode;
            CharacterId = character.CharacterId;
            ThreeCProfileId = threeCProfile.ProfileId;
            CombatFeelProfileId = combatFeelProfile.FeelProfileId;
            WeaponId = weapon.WeaponId;
            MaximumAimDistance = maximumAimDistance;
            ReticleSafeViewport = reticleSafeViewport;
            MouseReticleSensitivity = mouseReticleSensitivity;
            MouseReferenceResolution = mouseReferenceResolution;
            GamepadReticleSpeed = gamepadReticleSpeed;
            GamepadReticleDeadzone = gamepadReticleDeadzone;
            GamepadReticleResponseExponent =
                gamepadReticleResponseExponent;
            InputBufferTicks = inputBufferTicks;
            PeekTransitionSeconds = peekTransitionSeconds;
            FacingFlipDelaySeconds = facingFlipDelaySeconds;
            FacingFlipDurationSeconds = facingFlipDurationSeconds;
            RetractTransitionSeconds = retractTransitionSeconds;
            CoverTraversalSeconds = coverTraversalSeconds;
            PrimarySpreadTangent = primarySpreadTangent;
            SecondaryAreaRadius = secondaryAreaRadius;
            MagazineCapacity = magazineCapacity;
            PrimaryPelletCount = primaryPelletCount;
            PrimaryDamage = primaryDamage;
            SecondaryDamage = secondaryDamage;
            PrimaryAmmoCost = primaryAmmoCost;
            SecondaryAmmoCost = secondaryAmmoCost;
            PrimaryActionLockTicks = primaryActionLockTicks;
            SecondaryActionLockTicks = secondaryActionLockTicks;
            PrimaryCooldownTicks = primaryCooldownTicks;
            SecondaryCooldownTicks = secondaryCooldownTicks;
            PrimaryAttackCommitTick = primaryAttackCommitTick;
            SecondaryAttackCommitTick = secondaryAttackCommitTick;
            ReloadCommitTick = reloadCommitTick;
            ReloadDurationTicks = reloadDurationTicks;
            ReloadLockTicks = reloadLockTicks;
            PrimaryCameraKick = primaryCameraKick;
            SecondaryCameraKick = secondaryCameraKick;
            CameraKickRecoverySeconds = cameraKickRecoverySeconds;
            PrimaryTiming = primaryTiming;

            InputBufferSeconds = inputBufferTicks / (float)TickRate;
            PrimarySpreadHalfAngleDegrees =
                Mathf.Atan(primarySpreadTangent) * Mathf.Rad2Deg;
            PrimarySpreadRadiusAtMaximumAimDistance =
                maximumAimDistance * primarySpreadTangent;
            PrimaryIntervalTicks = primaryTiming.IntervalTicks;
            PrimaryIntervalSeconds =
                PrimaryIntervalTicks / (float)TickRate;
            SecondaryActionLockSeconds =
                secondaryActionLockTicks / (float)TickRate;
            PrimaryShotsPerSecond =
                TickRate / (float)PrimaryIntervalTicks;
            PrimaryRoundsPerMinute = PrimaryShotsPerSecond * 60f;
            ReloadDurationSeconds =
                reloadDurationTicks / (float)TickRate;
            ReloadLockSeconds = reloadLockTicks / (float)TickRate;
        }

        public D0CharacterDefinition Character { get; }
        public D0ThreeCProfile ThreeCProfile { get; }
        public D0CombatFeelProfile CombatFeelProfile { get; }
        public D0WeaponDefinition Weapon { get; }
        public FpgPlayerSkillDefinition PrimarySkill { get; }
        public FpgPlayerSkillDefinition SecondarySkill { get; }
        public FpgPlayerSkillDefinition ReloadSkill { get; }
        public SecondaryTriggerMode SecondaryTriggerMode { get; }

        public string CharacterId { get; }
        public string ThreeCProfileId { get; }
        public string CombatFeelProfileId { get; }
        public string WeaponId { get; }

        public float MaximumAimDistance { get; }
        public Rect ReticleSafeViewport { get; }
        public float MouseReticleSensitivity { get; }
        public float ReticleSensitivity => MouseReticleSensitivity;
        public Vector2 MouseReferenceResolution { get; }
        public float GamepadReticleSpeed { get; }
        public float GamepadReticleDeadzone { get; }
        public float GamepadReticleResponseExponent { get; }
        public int InputBufferTicks { get; }
        public float InputBufferSeconds { get; }
        public float PeekTransitionSeconds { get; }
        public float FacingFlipDelaySeconds { get; }
        public float FacingFlipDurationSeconds { get; }
        public float RetractTransitionSeconds { get; }
        public float CoverTraversalSeconds { get; }
        public float PrimarySpreadTangent { get; }
        public float PrimarySpreadHalfAngleDegrees { get; }
        public float PrimarySpreadRadiusAtMaximumAimDistance { get; }
        public float SecondaryAreaRadius { get; }
        public int MagazineCapacity { get; }
        public int PrimaryPelletCount { get; }
        public DamageSpec PrimaryDamage { get; }
        public DamageSpec SecondaryDamage { get; }
        public int PrimaryAmmoCost { get; }
        public int SecondaryAmmoCost { get; }
        public int PrimaryActionLockTicks { get; }
        public int SecondaryActionLockTicks { get; }
        public int PrimaryCooldownTicks { get; }
        public int SecondaryCooldownTicks { get; }
        public int PrimaryAttackCommitTick { get; }
        public int SecondaryAttackCommitTick { get; }
        public int ReloadCommitTick { get; }
        public FpgResolvedSkillTimingSnapshot PrimaryTiming { get; }
        public FpgAttackTimingMode PrimaryAttackTimingMode =>
            PrimaryTiming.Mode;
        public float PrimaryEffectiveAttackSpeed =>
            (float)PrimaryTiming.EffectiveAttackSpeed;
        public int PrimaryIntervalTicks { get; }
        public int PrimaryWindupTicks => PrimaryTiming.WindupTicks;
        public int PrimaryRecoveryTicks => PrimaryTiming.RecoveryTicks;
        public int PrimarySameAttackReadyTick =>
            PrimaryTiming.IntervalTicks;
        public int PrimaryDifferentAttackInterruptTick =>
            PrimaryTiming.DifferentAttackInterruptRelativeTick;
        public float PrimaryIntervalSeconds { get; }
        public float PrimaryActionLockSeconds => PrimaryIntervalSeconds;
        public float SecondaryActionLockSeconds { get; }
        public float PrimaryShotsPerSecond { get; }
        public float PrimaryRoundsPerMinute { get; }
        public float RoundsPerMinute => PrimaryRoundsPerMinute;
        public int ReloadDurationTicks { get; }
        public float ReloadDurationSeconds { get; }
        public int ReloadLockTicks { get; }
        public float ReloadLockSeconds { get; }
        public float PrimaryCameraKick { get; }
        public float SecondaryCameraKick { get; }
        public float CameraKickRecoverySeconds { get; }

        public bool IsValid => TryValidate(out _);

        public bool MatchesSelection(
            in FpgPlayableCharacterSelection selection)
        {
            return ReferenceEquals(Character, selection.CharacterDefinition)
                && ReferenceEquals(ThreeCProfile, selection.ThreeCProfile)
                && ReferenceEquals(
                    CombatFeelProfile,
                    selection.CombatFeelProfile)
                && SecondaryTriggerMode
                    == selection.SelectedSecondaryTriggerMode;
        }

        public FpgShootingTuningSnapshot WithInputAndMovement(
            float mouseReticleSensitivity,
            Vector2 mouseReferenceResolution,
            float gamepadReticleSpeed,
            float gamepadReticleDeadzone,
            float gamepadReticleResponseExponent,
            int inputBufferTicks,
            float peekTransitionSeconds,
            float facingFlipDelaySeconds,
            float facingFlipDurationSeconds,
            float retractTransitionSeconds,
            float coverTraversalSeconds)
        {
            return WithInputAndMovement(
                ReticleSafeViewport,
                mouseReticleSensitivity,
                mouseReferenceResolution,
                gamepadReticleSpeed,
                gamepadReticleDeadzone,
                gamepadReticleResponseExponent,
                inputBufferTicks,
                peekTransitionSeconds,
                facingFlipDelaySeconds,
                facingFlipDurationSeconds,
                retractTransitionSeconds,
                coverTraversalSeconds);
        }

        public FpgShootingTuningSnapshot WithInputAndMovement(
            Rect reticleSafeViewport,
            float mouseReticleSensitivity,
            Vector2 mouseReferenceResolution,
            float gamepadReticleSpeed,
            float gamepadReticleDeadzone,
            float gamepadReticleResponseExponent,
            int inputBufferTicks,
            float peekTransitionSeconds,
            float facingFlipDelaySeconds,
            float facingFlipDurationSeconds,
            float retractTransitionSeconds,
            float coverTraversalSeconds)
        {
            return new FpgShootingTuningSnapshot(
                Character,
                ThreeCProfile,
                CombatFeelProfile,
                Weapon,
                PrimarySkill,
                SecondarySkill,
                ReloadSkill,
                SecondaryTriggerMode,
                MaximumAimDistance,
                reticleSafeViewport,
                mouseReticleSensitivity,
                mouseReferenceResolution,
                gamepadReticleSpeed,
                gamepadReticleDeadzone,
                gamepadReticleResponseExponent,
                inputBufferTicks,
                peekTransitionSeconds,
                facingFlipDelaySeconds,
                facingFlipDurationSeconds,
                retractTransitionSeconds,
                coverTraversalSeconds,
                PrimarySpreadTangent,
                SecondaryAreaRadius,
                MagazineCapacity,
                PrimaryPelletCount,
                PrimaryDamage,
                SecondaryDamage,
                PrimaryAmmoCost,
                SecondaryAmmoCost,
                PrimaryActionLockTicks,
                SecondaryActionLockTicks,
                PrimaryCooldownTicks,
                SecondaryCooldownTicks,
                PrimaryAttackCommitTick,
                SecondaryAttackCommitTick,
                ReloadCommitTick,
                ReloadDurationTicks,
                ReloadLockTicks,
                PrimaryCameraKick,
                SecondaryCameraKick,
                CameraKickRecoverySeconds,
                PrimaryTiming);
        }

        public FpgShootingTuningSnapshot WithBallistics(
            float maximumAimDistance,
            float primarySpreadTangent,
            float secondaryAreaRadius)
        {
            return new FpgShootingTuningSnapshot(
                Character,
                ThreeCProfile,
                CombatFeelProfile,
                Weapon,
                PrimarySkill,
                SecondarySkill,
                ReloadSkill,
                SecondaryTriggerMode,
                maximumAimDistance,
                ReticleSafeViewport,
                MouseReticleSensitivity,
                MouseReferenceResolution,
                GamepadReticleSpeed,
                GamepadReticleDeadzone,
                GamepadReticleResponseExponent,
                InputBufferTicks,
                PeekTransitionSeconds,
                FacingFlipDelaySeconds,
                FacingFlipDurationSeconds,
                RetractTransitionSeconds,
                CoverTraversalSeconds,
                primarySpreadTangent,
                secondaryAreaRadius,
                MagazineCapacity,
                PrimaryPelletCount,
                PrimaryDamage,
                SecondaryDamage,
                PrimaryAmmoCost,
                SecondaryAmmoCost,
                PrimaryActionLockTicks,
                SecondaryActionLockTicks,
                PrimaryCooldownTicks,
                SecondaryCooldownTicks,
                PrimaryAttackCommitTick,
                SecondaryAttackCommitTick,
                ReloadCommitTick,
                ReloadDurationTicks,
                ReloadLockTicks,
                PrimaryCameraKick,
                SecondaryCameraKick,
                CameraKickRecoverySeconds,
                PrimaryTiming);
        }

        public FpgShootingTuningSnapshot WithMagazineCapacity(
            int magazineCapacity)
        {
            return new FpgShootingTuningSnapshot(
                Character,
                ThreeCProfile,
                CombatFeelProfile,
                Weapon,
                PrimarySkill,
                SecondarySkill,
                ReloadSkill,
                SecondaryTriggerMode,
                MaximumAimDistance,
                ReticleSafeViewport,
                MouseReticleSensitivity,
                MouseReferenceResolution,
                GamepadReticleSpeed,
                GamepadReticleDeadzone,
                GamepadReticleResponseExponent,
                InputBufferTicks,
                PeekTransitionSeconds,
                FacingFlipDelaySeconds,
                FacingFlipDurationSeconds,
                RetractTransitionSeconds,
                CoverTraversalSeconds,
                PrimarySpreadTangent,
                SecondaryAreaRadius,
                magazineCapacity,
                PrimaryPelletCount,
                PrimaryDamage,
                SecondaryDamage,
                PrimaryAmmoCost,
                SecondaryAmmoCost,
                PrimaryActionLockTicks,
                SecondaryActionLockTicks,
                PrimaryCooldownTicks,
                SecondaryCooldownTicks,
                PrimaryAttackCommitTick,
                SecondaryAttackCommitTick,
                ReloadCommitTick,
                ReloadDurationTicks,
                ReloadLockTicks,
                PrimaryCameraKick,
                SecondaryCameraKick,
                CameraKickRecoverySeconds,
                PrimaryTiming);
        }

        public FpgShootingTuningSnapshot WithCameraFeedback(
            float primaryCameraKick,
            float secondaryCameraKick,
            float recoverySeconds)
        {
            return new FpgShootingTuningSnapshot(
                Character,
                ThreeCProfile,
                CombatFeelProfile,
                Weapon,
                PrimarySkill,
                SecondarySkill,
                ReloadSkill,
                SecondaryTriggerMode,
                MaximumAimDistance,
                ReticleSafeViewport,
                MouseReticleSensitivity,
                MouseReferenceResolution,
                GamepadReticleSpeed,
                GamepadReticleDeadzone,
                GamepadReticleResponseExponent,
                InputBufferTicks,
                PeekTransitionSeconds,
                FacingFlipDelaySeconds,
                FacingFlipDurationSeconds,
                RetractTransitionSeconds,
                CoverTraversalSeconds,
                PrimarySpreadTangent,
                SecondaryAreaRadius,
                MagazineCapacity,
                PrimaryPelletCount,
                PrimaryDamage,
                SecondaryDamage,
                PrimaryAmmoCost,
                SecondaryAmmoCost,
                PrimaryActionLockTicks,
                SecondaryActionLockTicks,
                PrimaryCooldownTicks,
                SecondaryCooldownTicks,
                PrimaryAttackCommitTick,
                SecondaryAttackCommitTick,
                ReloadCommitTick,
                ReloadDurationTicks,
                ReloadLockTicks,
                primaryCameraKick,
                secondaryCameraKick,
                recoverySeconds,
                PrimaryTiming);
        }

        public bool TryCreateAttackQuerySettings(
            in UnityAttackQueryTechnicalSettings technicalSettings,
            out UnityAttackQuerySettings settings,
            out string error)
        {
            settings = default(UnityAttackQuerySettings);
            if (!TryValidate(out error) || !technicalSettings.IsValid)
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Shooting preview requires valid technical query layers."
                    : error;
                return false;
            }

            try
            {
                settings = new UnityAttackQuerySettings(
                    MaximumAimDistance,
                    PrimarySpreadTangent,
                    SecondaryAreaRadius,
                    technicalSettings.HitboxLayerMask,
                    technicalSettings.BlockerLayerMask);
                error = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryCreateWeaponDefinition(
            out WeaponDefinition definition,
            out string error)
        {
            definition = default(WeaponDefinition);
            if (!TryValidate(out error)
                || !Weapon.TryCreate(
                    SecondaryTriggerMode,
                    out WeaponDefinition authored,
                    out error))
            {
                return false;
            }

            try
            {
                definition = new WeaponDefinition(
                    authored.DefinitionId,
                    MagazineCapacity,
                    authored.PrimaryAmmoCost,
                    authored.PrimaryInterval,
                    authored.PrimaryDamage,
                    authored.SecondaryAmmoCost,
                    authored.SecondaryMinimumCharge,
                    authored.SecondaryRecovery,
                    authored.SecondaryDamage,
                    authored.ReloadDuration,
                    authored.SecondaryMaxImpactCount,
                    authored.SecondaryTriggerMode,
                    authored.PrimaryQueryMode,
                    authored.PrimaryAdditionalPenetrationCount,
                    authored.SecondaryQueryMode,
                    authored.SecondaryAreaProjectileLimit,
                    authored.PrimaryAllowedTargetKinds,
                    authored.SecondaryAllowedTargetKinds,
                    authored.PrimaryPayloadCount,
                    authored.MaximumAttackImpactCount);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is OverflowException)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool TryCapture(
            FpgPlayableCharacterSelection selection,
            out FpgShootingTuningSnapshot snapshot,
            out string error)
        {
            snapshot = default(FpgShootingTuningSnapshot);
            D0CharacterDefinition character = selection.CharacterDefinition;
            if (character == null
                || string.IsNullOrWhiteSpace(character.CharacterId))
            {
                error =
                    "Shooting tuning requires a character with a stable ID.";
                return false;
            }

            D0ThreeCProfile threeCProfile = selection.ThreeCProfile;
            if (threeCProfile == null)
            {
                error = "Shooting tuning requires a valid 3C profile.";
                return false;
            }

            if (!threeCProfile.TryValidate(out error))
            {
                error = "Shooting 3C profile is invalid: " + error;
                return false;
            }

            D0CombatFeelProfile combatFeelProfile =
                selection.CombatFeelProfile;
            if (combatFeelProfile == null)
            {
                error =
                    "Shooting tuning requires a valid combat-feel profile.";
                return false;
            }

            if (!combatFeelProfile.TryValidate(out error))
            {
                error = "Shooting combat-feel profile is invalid: " + error;
                return false;
            }

            if (!Enum.IsDefined(
                    typeof(SecondaryTriggerMode),
                    selection.SelectedSecondaryTriggerMode)
                || !selection.SupportsSecondaryMode(
                    selection.SelectedSecondaryTriggerMode))
            {
                error =
                    "Shooting tuning requires a supported secondary trigger mode.";
                return false;
            }

            D0WeaponDefinition weapon = character.Weapon;
            if (weapon == null)
            {
                error = "Shooting tuning requires a weapon definition.";
                return false;
            }

            if (!weapon.TryCreate(
                    selection.SelectedSecondaryTriggerMode,
                    out WeaponDefinition resolvedWeapon,
                    out error))
            {
                error = "Shooting weapon configuration is invalid: " + error;
                return false;
            }

            if (!weapon.TryResolveSecondarySkill(
                    selection.SelectedSecondaryTriggerMode,
                    out FpgPlayerSkillDefinition secondarySkill,
                    out error))
            {
                error = "Shooting secondary skill configuration is invalid: "
                    + error;
                return false;
            }

            FpgPlayerSkillDefinition primarySkill = weapon.PrimarySkill;
            FpgPlayerSkillDefinition reloadSkill = weapon.ReloadSkill;
            if (primarySkill == null || reloadSkill == null)
            {
                error =
                    "Shooting tuning requires valid primary and reload skill timelines.";
                return false;
            }

            if (!weapon.TryCompileSkills(
                    selection.SelectedSecondaryTriggerMode,
                    out FpgCompiledPlayerSkillDefinition compiledPrimary,
                    out FpgCompiledPlayerSkillDefinition compiledSecondary,
                    out FpgCompiledPlayerSkillDefinition compiledReload,
                    out error))
            {
                error = "Shooting skill summary compilation failed: " + error;
                return false;
            }

            FpgSkillSequenceKind secondarySequenceKind =
                selection.SelectedSecondaryTriggerMode
                    == SecondaryTriggerMode.ChargeRelease
                    ? FpgSkillSequenceKind.Release
                    : FpgSkillSequenceKind.Execute;
            if (!compiledPrimary.TryGetSequenceSummary(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledPlayerSkillSequenceSummary primarySummary)
                || !compiledPrimary.Timeline.TryGetSequence(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledSkillSequence primarySequence)
                || !compiledPrimary.TryGetTimingDefinition(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledSkillTimingDefinition primaryTimingDefinition)
                || !compiledSecondary.TryGetSequenceSummary(
                    secondarySequenceKind,
                    out FpgCompiledPlayerSkillSequenceSummary secondarySummary)
                || !compiledReload.Timeline.TryGetSequence(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledSkillSequence reloadSequence))
            {
                error =
                    "Shooting skill summary requires executable primary, secondary and reload sequences.";
                return false;
            }

            if (!FpgAttackTimingResolver.TryResolve(
                    primarySequence,
                    primaryTimingDefinition,
                    compiledPrimary.SequenceCooldownTicks,
                    character.AttackSpeedProfile,
                    0d,
                    new TickIndex(0L),
                    out FpgResolvedSkillSchedule primarySchedule,
                    out string timingError))
            {
                error = "Shooting primary timing resolution failed: "
                    + timingError;
                return false;
            }

            int reloadCommitTick = -1;
            for (int eventIndex = 0;
                eventIndex < reloadSequence.EventCount;
                eventIndex++)
            {
                FpgCompiledSkillEvent skillEvent =
                    reloadSequence.GetEvent(eventIndex);
                if (skillEvent.Kind == FpgSkillEventKind.GameplayAction
                    && compiledReload.TryResolveAction(
                        skillEvent,
                        out FpgCompiledPlayerSkillAction payload)
                    && payload.Kind == FpgPlayerSkillActionKind.ReloadCommit)
                {
                    reloadCommitTick = Math.Max(
                        reloadCommitTick,
                        skillEvent.Tick);
                }
            }

            if (primarySummary.LastAttackTick < 0
                || secondarySummary.LastAttackTick < 0
                || reloadCommitTick < 0
                || reloadSequence.DurationTicks <= 0)
            {
                error =
                    "Shooting skill summary is missing an attack or reload commit tick.";
                return false;
            }

            snapshot = new FpgShootingTuningSnapshot(
                character,
                threeCProfile,
                combatFeelProfile,
                weapon,
                primarySkill,
                secondarySkill,
                reloadSkill,
                selection.SelectedSecondaryTriggerMode,
                combatFeelProfile.MaximumAimDistance,
                threeCProfile.ReticleSafeViewport,
                threeCProfile.MouseReticleSensitivity,
                threeCProfile.MouseReferenceResolution,
                threeCProfile.GamepadReticleSpeed,
                threeCProfile.GamepadReticleDeadzone,
                threeCProfile.GamepadReticleResponseExponent,
                threeCProfile.InputBufferTicks,
                threeCProfile.PeekTransitionSeconds,
                threeCProfile.FacingFlipDelaySeconds,
                threeCProfile.FacingFlipDurationSeconds,
                threeCProfile.RetractTransitionSeconds,
                threeCProfile.CoverTraversalSeconds,
                combatFeelProfile.PrimaryBaseSpreadTangent,
                combatFeelProfile.SecondaryAreaRadius,
                resolvedWeapon.MagazineCapacity,
                resolvedWeapon.PrimaryPayloadCount,
                resolvedWeapon.PrimaryDamage,
                resolvedWeapon.SecondaryDamage,
                resolvedWeapon.PrimaryAmmoCost,
                resolvedWeapon.SecondaryAmmoCost,
                resolvedWeapon.PrimaryInterval.Value,
                resolvedWeapon.SecondaryRecovery.Value,
                compiledPrimary.SequenceCooldownTicks,
                compiledSecondary.SequenceCooldownTicks,
                primarySummary.LastAttackTick,
                secondarySummary.LastAttackTick,
                reloadCommitTick,
                reloadSequence.DurationTicks,
                resolvedWeapon.ReloadDuration.Value,
                threeCProfile.PrimaryShotCameraKick,
                threeCProfile.SecondaryShotCameraKick,
                threeCProfile.ShotCameraKickRecoverySeconds,
                primarySchedule.Timing);

            if (!snapshot.TryValidate(out error))
            {
                snapshot = default(FpgShootingTuningSnapshot);
                error = "Resolved shooting tuning is invalid: " + error;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public float GetPrimarySpreadRadius(float distance)
        {
            if (!IsFinite(distance) || distance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distance));
            }

            return distance * PrimarySpreadTangent;
        }

        public static float SpreadTangentToHalfAngleDegrees(float tangent)
        {
            if (!IsFinite(tangent) || tangent < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tangent));
            }

            return Mathf.Atan(tangent) * Mathf.Rad2Deg;
        }

        public static float SpreadHalfAngleDegreesToTangent(
            float halfAngleDegrees)
        {
            if (!IsFinite(halfAngleDegrees)
                || halfAngleDegrees < 0f
                || halfAngleDegrees >= 90f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(halfAngleDegrees));
            }

            return Mathf.Tan(halfAngleDegrees * Mathf.Deg2Rad);
        }

        public bool TryValidate(out string error)
        {
            if (Character == null || ThreeCProfile == null
                || CombatFeelProfile == null || Weapon == null
                || PrimarySkill == null || SecondarySkill == null
                || ReloadSkill == null)
            {
                error =
                    "Shooting tuning snapshot is missing an authoritative asset reference.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(CharacterId)
                || string.IsNullOrWhiteSpace(ThreeCProfileId)
                || string.IsNullOrWhiteSpace(CombatFeelProfileId)
                || string.IsNullOrWhiteSpace(WeaponId))
            {
                error = "Shooting tuning snapshot is missing a stable asset ID.";
                return false;
            }

            if (!Enum.IsDefined(
                    typeof(SecondaryTriggerMode),
                    SecondaryTriggerMode))
            {
                error = "Shooting tuning snapshot has an invalid secondary mode.";
                return false;
            }

            if (!IsFinitePositive(MaximumAimDistance)
                || !IsValidViewportRect(ReticleSafeViewport)
                || !IsFinitePositive(MouseReticleSensitivity)
                || !IsFinitePositive(MouseReferenceResolution.x)
                || !IsFinitePositive(MouseReferenceResolution.y)
                || !IsFinitePositive(GamepadReticleSpeed)
                || !IsFiniteNonNegative(GamepadReticleDeadzone)
                || GamepadReticleDeadzone >= 1f
                || !IsFinitePositive(GamepadReticleResponseExponent)
                || InputBufferTicks < 1
                || InputBufferTicks > 32
                || !IsFinitePositive(InputBufferSeconds)
                || !IsFiniteNonNegative(PeekTransitionSeconds)
                || !IsFiniteNonNegative(FacingFlipDelaySeconds)
                || !IsFiniteNonNegative(FacingFlipDurationSeconds)
                || !IsFiniteNonNegative(RetractTransitionSeconds)
                || !IsFinitePositive(CoverTraversalSeconds)
                || !IsFiniteNonNegative(PrimarySpreadTangent)
                || !IsFiniteNonNegative(PrimarySpreadHalfAngleDegrees)
                || !IsFiniteNonNegative(
                    PrimarySpreadRadiusAtMaximumAimDistance)
                || !IsFinitePositive(SecondaryAreaRadius)
                || MagazineCapacity <= 0
                || PrimaryPelletCount <= 0
                || PrimaryPelletCount > WeaponDefinition.PrimaryPelletCount
                || !IsValidDamage(PrimaryDamage)
                || !IsValidDamage(SecondaryDamage)
                || PrimaryAmmoCost <= 0
                || PrimaryAmmoCost > MagazineCapacity
                || SecondaryAmmoCost <= 0
                || SecondaryAmmoCost > MagazineCapacity
                || PrimaryActionLockTicks <= 0
                || SecondaryActionLockTicks <= 0
                || PrimaryCooldownTicks < 0
                || SecondaryCooldownTicks < 0
                || PrimaryActionLockTicks
                    != Math.Max(1, PrimaryCooldownTicks)
                || SecondaryActionLockTicks
                    != Math.Max(1, SecondaryCooldownTicks)
                || PrimaryAttackCommitTick < 0
                || SecondaryAttackCommitTick < 0
                || ReloadCommitTick < 0
                || !PrimaryTiming.IsValid
                || PrimaryTiming.StartTick != new TickIndex(0L)
                || PrimaryIntervalTicks != PrimaryTiming.IntervalTicks
                || PrimaryWindupTicks != PrimaryTiming.WindupTicks
                || PrimaryRecoveryTicks != PrimaryTiming.RecoveryTicks
                || PrimarySameAttackReadyTick != PrimaryIntervalTicks
                || PrimaryDifferentAttackInterruptTick < 0
                || !IsFinitePositive(PrimaryEffectiveAttackSpeed)
                || !IsFinitePositive(PrimaryIntervalSeconds)
                || !IsFinitePositive(SecondaryActionLockSeconds)
                || !IsFinitePositive(PrimaryShotsPerSecond)
                || !IsFinitePositive(PrimaryRoundsPerMinute)
                || ReloadDurationTicks <= 0
                || ReloadCommitTick > ReloadDurationTicks
                || !IsFinitePositive(ReloadDurationSeconds)
                || ReloadLockTicks < ReloadDurationTicks
                || !IsFinitePositive(ReloadLockSeconds)
                || !IsFiniteNonNegative(PrimaryCameraKick)
                || !IsFiniteNonNegative(SecondaryCameraKick)
                || !IsFinitePositive(CameraKickRecoverySeconds))
            {
                error = "Shooting tuning snapshot contains invalid values.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsValidDamage(DamageSpec damage)
        {
            return damage.BaseDamage >= 0
                && damage.BreakDamage >= 0
                && damage.WeakpointDamageMultiplierBasisPoints >= 0
                && damage.WeakpointBreakMultiplierBasisPoints >= 0;
        }

        private static bool IsValidViewportRect(Rect value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.width)
                && IsFinite(value.height)
                && value.x >= 0f
                && value.y >= 0f
                && value.width > 0f
                && value.height > 0f
                && value.xMax <= 1f
                && value.yMax <= 1f;
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Tick-driver-independent read model for inspecting the current shooting
    /// decision, aim geometry and most recent result.
    /// </summary>
    public readonly struct FpgShootingDiagnosticsSnapshot
    {
        public const long UnavailableTick = -1L;

        /// <summary>
        /// Compatibility constructor for diagnostics providers that only expose
        /// the original flattened aim and last-result fields.
        /// </summary>
        public FpgShootingDiagnosticsSnapshot(
            long tick,
            int ammo,
            int magazineCapacity,
            string weaponState,
            string reticleState,
            string targetState,
            string targetLabel,
            bool canAttack,
            string attackBlockReason,
            Vector3 aimOrigin,
            Vector3 aimDirection,
            Vector3 aimPoint,
            bool hasLastShot,
            long lastShotTick,
            Vector3 lastShotOrigin,
            Vector3 lastShotDirection,
            Vector3 lastShotEndPoint,
            bool hasLastHit,
            long lastHitTick,
            Vector3 lastHitPoint,
            string lastHitTarget,
            float lastHitDamage)
        {
            Tick = tick;
            Ammo = ammo;
            MagazineCapacity = magazineCapacity;
            WeaponState = weaponState;
            ReticleState = reticleState;
            TargetState = targetState;
            TargetLabel = targetLabel ?? string.Empty;
            CanAttack = canAttack;
            AttackBlockReason = attackBlockReason ?? string.Empty;
            AimOrigin = aimOrigin;
            AimDirection = aimDirection;
            AimPoint = aimPoint;
            HasLastShot = hasLastShot;
            LastShotTick = lastShotTick;
            LastShotOrigin = lastShotOrigin;
            LastShotDirection = lastShotDirection;
            LastShotEndPoint = lastShotEndPoint;
            HasLastHit = hasLastHit;
            LastHitTick = lastHitTick;
            LastHitPoint = lastHitPoint;
            LastHitTarget = lastHitTarget ?? string.Empty;
            LastHitDamage = lastHitDamage;

            HasAuthoritativeAim = false;
            WeaponRuntimeState = TryParseEnum(
                weaponState,
                FPG.Demo.Player.WeaponState.Disabled);
            ReticleBaseState = TryParseEnum(
                reticleState,
                FpgAimIndicatorBaseState.Hidden);
            ExposureState = PlayerExposureState.Withdrawn;
            ReloadProgress01 = 0f;
            IsCoverPeekRequested = false;
            CoverPeekStartedTick = UnavailableTick;
            PelletCount = 0;
            PrimarySpreadTangent = 0f;
            PelletConeHalfAngleDegrees = 0f;
            PelletConeRadiusAtAimDistance = 0f;
            LiveAimContext = FpgResolvedAimContext.Invalid;
            ResolvedAimContext = FpgResolvedAimContext.Invalid;
            LiveAimVersion = 0L;
            ResolvedAimVersion = 0L;
            FrozenAimVersion = 0L;
            ReticleViewport = Vector2.zero;
            ShotOrigin = aimOrigin;
            CenterDirection = aimDirection;
            SurfacePoint = aimPoint;
            AimDistance = Vector3.Distance(aimOrigin, aimPoint);
            TargetType = TryParseEnum(
                targetState,
                FpgResolvedAimTargetType.None);
            TargetId = RuntimeId.Invalid;
            TargetKind = default(QueryTargetKind);
            HitPart = default(HitPart);
            GeometryId = FPG.Demo.Core.GeometryId.Invalid;
            TargetCoverId = string.Empty;
            CurrentCoverId = string.Empty;
            IsCurrentCoverBlocked = false;
            PrimaryAttackAvailability =
                default(FpgAttackAvailability);
            SecondaryAttackAvailability =
                default(FpgAttackAvailability);

            if (!TryValidate(out string error))
            {
                throw new ArgumentException(error, nameof(tick));
            }
        }

        public FpgShootingDiagnosticsSnapshot(
            long tick,
            int ammo,
            int magazineCapacity,
            FPG.Demo.Player.WeaponState weaponState,
            FpgAimIndicatorBaseState reticleState,
            PlayerExposureState exposureState,
            float reloadProgress01,
            bool isCoverPeekRequested,
            long coverPeekStartedTick,
            int pelletCount,
            float primarySpreadTangent,
            in FpgResolvedAimContext liveAim,
            in FpgResolvedAimContext resolvedAim,
            in FpgAttackAvailability primaryAttackAvailability,
            in FpgAttackAvailability secondaryAttackAvailability)
        {
            Tick = tick;
            Ammo = ammo;
            MagazineCapacity = magazineCapacity;
            WeaponRuntimeState = weaponState;
            WeaponState = weaponState.ToString();
            ReticleBaseState = reticleState;
            ReticleState = reticleState.ToString();
            ExposureState = exposureState;
            ReloadProgress01 = reloadProgress01;
            IsCoverPeekRequested = isCoverPeekRequested;
            CoverPeekStartedTick = isCoverPeekRequested
                ? coverPeekStartedTick
                : UnavailableTick;

            HasAuthoritativeAim = true;
            LiveAimContext = liveAim;
            ResolvedAimContext = resolvedAim;
            LiveAimVersion = liveAim.Version;
            ResolvedAimVersion = resolvedAim.Version;
            FrozenAimVersion = resolvedAim.IsFrozen
                ? resolvedAim.FrozenVersion
                : 0L;
            ReticleViewport = resolvedAim.ReticleViewport;
            AimOrigin = resolvedAim.CameraOrigin;
            AimDirection = resolvedAim.CameraDirection;
            AimPoint = resolvedAim.TargetPoint;
            ShotOrigin = resolvedAim.ShotOrigin;
            CenterDirection = resolvedAim.CenterDirection;
            SurfacePoint = resolvedAim.SurfacePoint;
            AimDistance = resolvedAim.Distance;

            TargetType = resolvedAim.TargetType;
            TargetState = resolvedAim.TargetType.ToString();
            TargetId = resolvedAim.TargetId;
            TargetKind = resolvedAim.TargetKind;
            HitPart = resolvedAim.HitPart;
            GeometryId = resolvedAim.GeometryId;
            TargetCoverId = resolvedAim.TargetCoverId ?? string.Empty;
            CurrentCoverId = resolvedAim.CurrentCoverId ?? string.Empty;
            IsCurrentCoverBlocked = resolvedAim.IsCurrentCoverBlocked;
            TargetLabel = ResolveTargetLabel(resolvedAim);

            PrimaryAttackAvailability = primaryAttackAvailability;
            SecondaryAttackAvailability = secondaryAttackAvailability;
            CanAttack = primaryAttackAvailability.Ready
                || secondaryAttackAvailability.Ready;
            AttackBlockReason = ResolveAttackBlockReason(
                primaryAttackAvailability,
                secondaryAttackAvailability);

            PelletCount = pelletCount;
            PrimarySpreadTangent = primarySpreadTangent;
            PelletConeHalfAngleDegrees =
                Mathf.Atan(primarySpreadTangent) * Mathf.Rad2Deg;
            PelletConeRadiusAtAimDistance =
                resolvedAim.Distance * primarySpreadTangent;

            HasLastShot = false;
            LastShotTick = UnavailableTick;
            LastShotOrigin = Vector3.zero;
            LastShotDirection = Vector3.zero;
            LastShotEndPoint = Vector3.zero;
            HasLastHit = false;
            LastHitTick = UnavailableTick;
            LastHitPoint = Vector3.zero;
            LastHitTarget = string.Empty;
            LastHitDamage = 0f;

            if (!TryValidate(out string error))
            {
                throw new ArgumentException(error, nameof(tick));
            }
        }

        public long Tick { get; }
        public int Ammo { get; }
        public int MagazineCapacity { get; }
        public string WeaponState { get; }
        public string ReticleState { get; }
        public string TargetState { get; }
        public string TargetLabel { get; }
        public bool CanAttack { get; }
        public string AttackBlockReason { get; }
        public Vector3 AimOrigin { get; }
        public Vector3 AimDirection { get; }
        public Vector3 AimPoint { get; }
        public bool HasLastShot { get; }
        public long LastShotTick { get; }
        public Vector3 LastShotOrigin { get; }
        public Vector3 LastShotDirection { get; }
        public Vector3 LastShotEndPoint { get; }
        public bool HasLastHit { get; }
        public long LastHitTick { get; }
        public Vector3 LastHitPoint { get; }
        public string LastHitTarget { get; }
        public float LastHitDamage { get; }

        public bool HasAuthoritativeAim { get; }
        public FPG.Demo.Player.WeaponState WeaponRuntimeState { get; }
        public FpgAimIndicatorBaseState ReticleBaseState { get; }
        public PlayerExposureState ExposureState { get; }
        public bool IsReloading =>
            WeaponRuntimeState == FPG.Demo.Player.WeaponState.Reloading;
        public float ReloadProgress01 { get; }
        public bool IsCoverPeekRequested { get; }
        public long CoverPeekStartedTick { get; }
        public int PelletCount { get; }
        public float PrimarySpreadTangent { get; }
        public float PelletConeHalfAngleDegrees { get; }
        public float PelletConeRadiusAtAimDistance { get; }
        public FpgResolvedAimContext LiveAimContext { get; }
        public FpgResolvedAimContext ResolvedAimContext { get; }
        public long LiveAimVersion { get; }
        public long ResolvedAimVersion { get; }
        public long FrozenAimVersion { get; }
        public bool IsAimFrozen => FrozenAimVersion > 0L;
        public Vector2 ReticleViewport { get; }
        public Ray CameraRay => new Ray(AimOrigin, AimDirection);
        public Vector3 CameraRayOrigin => AimOrigin;
        public Vector3 CameraRayDirection => AimDirection;
        public Vector3 TargetPoint => AimPoint;
        public Vector3 ShotOrigin { get; }
        public Vector3 CenterDirection { get; }
        public Vector3 SurfacePoint { get; }
        public float AimDistance { get; }
        public FpgResolvedAimTargetType TargetType { get; }
        public RuntimeId TargetId { get; }
        public QueryTargetKind TargetKind { get; }
        public HitPart HitPart { get; }
        public GeometryId GeometryId { get; }
        public string TargetCoverId { get; }
        public string CurrentCoverId { get; }
        public bool IsCurrentCoverBlocked { get; }
        public FpgAttackAvailability PrimaryAttackAvailability { get; }
        public FpgAttackAvailability SecondaryAttackAvailability { get; }
        public FpgAttackUnavailableReason PrimaryAttackUnavailableReason =>
            PrimaryAttackAvailability.Reason;
        public FpgAttackUnavailableReason SecondaryAttackUnavailableReason =>
            SecondaryAttackAvailability.Reason;

        public bool IsValid => TryValidate(out _);

        public bool TryValidate(out string error)
        {
            if (Tick < 0L)
            {
                error = "Shooting diagnostics requires a valid simulation tick.";
                return false;
            }

            if (MagazineCapacity <= 0 || Ammo < 0
                || Ammo > MagazineCapacity)
            {
                error = "Shooting diagnostics contains invalid ammunition.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(WeaponState)
                || string.IsNullOrWhiteSpace(ReticleState)
                || string.IsNullOrWhiteSpace(TargetState))
            {
                error =
                    "Shooting diagnostics requires weapon, reticle and target states.";
                return false;
            }

            if (CanAttack && !string.IsNullOrEmpty(AttackBlockReason)
                || !CanAttack
                && string.IsNullOrWhiteSpace(AttackBlockReason))
            {
                error =
                    "Shooting diagnostics attack readiness conflicts with its block reason.";
                return false;
            }

            if (!IsFinite(AimOrigin) || !IsUsableDirection(AimDirection)
                || !IsFinite(AimPoint))
            {
                error = "Shooting diagnostics contains invalid aim geometry.";
                return false;
            }

            if (HasAuthoritativeAim
                && !TryValidateAuthoritativeState(out error))
            {
                return false;
            }

            if (HasLastShot != (LastShotTick >= 0L)
                || !IsFinite(LastShotOrigin)
                || !IsFinite(LastShotDirection)
                || !IsFinite(LastShotEndPoint)
                || HasLastShot && !IsUsableDirection(LastShotDirection))
            {
                error = "Shooting diagnostics contains invalid last-shot data.";
                return false;
            }

            if (HasLastHit != (LastHitTick >= 0L)
                || HasLastHit && !HasLastShot
                || !IsFinite(LastHitPoint)
                || !IsFiniteNonNegative(LastHitDamage)
                || HasLastHit && string.IsNullOrWhiteSpace(LastHitTarget))
            {
                error = "Shooting diagnostics contains invalid last-hit data.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateAuthoritativeState(out string error)
        {
            if (!Enum.IsDefined(
                    typeof(FPG.Demo.Player.WeaponState),
                    WeaponRuntimeState)
                || !Enum.IsDefined(
                    typeof(FpgAimIndicatorBaseState),
                    ReticleBaseState)
                || !Enum.IsDefined(
                    typeof(PlayerExposureState),
                    ExposureState)
                || !IsFinite(ReloadProgress01)
                || ReloadProgress01 < 0f
                || ReloadProgress01 > 1f
                || !IsReloading && ReloadProgress01 > 0f
                || IsCoverPeekRequested != (CoverPeekStartedTick >= 0L))
            {
                error =
                    "Shooting diagnostics contains invalid weapon, reload or peek state.";
                return false;
            }

            bool liveVersionValid = LiveAimContext.IsValid
                ? LiveAimVersion == LiveAimContext.Version
                : LiveAimVersion == 0L;
            if (!liveVersionValid || !ResolvedAimContext.IsValid
                || ResolvedAimVersion != ResolvedAimContext.Version
                || FrozenAimVersion != (ResolvedAimContext.IsFrozen
                    ? ResolvedAimContext.FrozenVersion
                    : 0L))
            {
                error =
                    "Shooting diagnostics contains invalid live or resolved aim versions.";
                return false;
            }

            if (ReticleViewport != ResolvedAimContext.ReticleViewport
                || AimOrigin != ResolvedAimContext.CameraOrigin
                || AimDirection != ResolvedAimContext.CameraDirection
                || AimPoint != ResolvedAimContext.TargetPoint
                || ShotOrigin != ResolvedAimContext.ShotOrigin
                || CenterDirection != ResolvedAimContext.CenterDirection
                || SurfacePoint != ResolvedAimContext.SurfacePoint
                || !Mathf.Approximately(
                    AimDistance,
                    ResolvedAimContext.Distance)
                || TargetType != ResolvedAimContext.TargetType
                || TargetId != ResolvedAimContext.TargetId
                || TargetKind != ResolvedAimContext.TargetKind
                || HitPart != ResolvedAimContext.HitPart
                || GeometryId != ResolvedAimContext.GeometryId
                || !string.Equals(
                    TargetCoverId,
                    ResolvedAimContext.TargetCoverId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    CurrentCoverId,
                    ResolvedAimContext.CurrentCoverId,
                    StringComparison.Ordinal)
                || IsCurrentCoverBlocked
                    != ResolvedAimContext.IsCurrentCoverBlocked)
            {
                error =
                    "Shooting diagnostics flattened aim data does not match its resolved context.";
                return false;
            }

            if (PelletCount <= 0
                || !IsFiniteNonNegative(PrimarySpreadTangent)
                || !IsFiniteNonNegative(PelletConeHalfAngleDegrees)
                || !IsFiniteNonNegative(PelletConeRadiusAtAimDistance))
            {
                error = "Shooting diagnostics contains an invalid pellet cone.";
                return false;
            }

            if (PrimaryAttackAvailability.Slot
                    != FpgPlayerSkillSlot.Primary
                || SecondaryAttackAvailability.Slot
                    != FpgPlayerSkillSlot.Secondary
                || PrimaryAttackAvailability.Ammo != Ammo
                || SecondaryAttackAvailability.Ammo != Ammo
                || CanAttack != (PrimaryAttackAvailability.Ready
                    || SecondaryAttackAvailability.Ready))
            {
                error =
                    "Shooting diagnostics contains invalid attack availability.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string ResolveTargetLabel(
            in FpgResolvedAimContext aim)
        {
            if (aim.TargetId.IsValid)
            {
                return aim.TargetId.ToString();
            }

            return aim.GeometryId.IsValid
                ? "Geometry " + aim.GeometryId
                : string.Empty;
        }

        private static string ResolveAttackBlockReason(
            in FpgAttackAvailability primary,
            in FpgAttackAvailability secondary)
        {
            if (primary.Ready || secondary.Ready)
            {
                return string.Empty;
            }

            return primary.Reason == secondary.Reason
                ? primary.Reason.ToString()
                : "Primary: " + primary.Reason
                    + "; Secondary: " + secondary.Reason;
        }

        private static TEnum TryParseEnum<TEnum>(
            string value,
            TEnum fallback)
            where TEnum : struct
        {
            return Enum.TryParse(value, true, out TEnum parsed)
                && Enum.IsDefined(typeof(TEnum), parsed)
                    ? parsed
                    : fallback;
        }

        private static bool IsUsableDirection(Vector3 value)
        {
            return IsFinite(value) && value.sqrMagnitude > 0.0000001f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public interface IFpgShootingDiagnosticsProvider
    {
        bool TryGetShootingDiagnostics(
            out FpgShootingDiagnosticsSnapshot snapshot,
            out string error);
    }

    public interface IFpgShootingTuningPreviewHost :
        IFpgShootingDiagnosticsProvider
    {
        bool TryGetShootingTuning(
            out FpgShootingTuningSnapshot snapshot,
            out string error);

        bool TryApplyShootingLivePreview(
            in FpgShootingTuningSnapshot snapshot,
            out string error);

        bool TryApplyShootingPreviewAndRebuild(
            in FpgShootingTuningSnapshot snapshot,
            out string error);
    }

    public static class FpgShootingTuningRuntimeRegistry
    {
        private static IFpgShootingTuningPreviewHost current;

        public static IFpgShootingTuningPreviewHost Current => current;

        public static void Register(IFpgShootingTuningPreviewHost host)
        {
            current = host ?? throw new ArgumentNullException(nameof(host));
        }

        public static void Unregister(IFpgShootingTuningPreviewHost host)
        {
            if (ReferenceEquals(current, host))
            {
                current = null;
            }
        }
    }
}
