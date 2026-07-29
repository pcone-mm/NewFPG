using System;
using System.Collections.Generic;
using FPG.Demo.Enemy;
using FPG.Demo.Skills;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Presentation-only Spine bridge for the D0 actors. It consumes no combat
    /// state itself: callers feed it already committed battle presentation
    /// events. The component owns only local animation playback and a small
    /// visual pulse, never colliders, hitboxes or simulation transforms.
    /// </summary>
    [DefaultExecutionOrder(910)]
    [DisallowMultipleComponent]
    public sealed class Actor2DPresenter : MonoBehaviour
    {
        private const int MainTrack = 0;

        [SerializeField]
        private SkeletonAnimation skeletonAnimation;

        [SerializeField]
        private CombatPresentationProfile presentationProfile;

        [SerializeField]
        private bool playerActor;

        [SerializeField]
        private Transform pulseRoot;

        [SerializeField, Min(0.01f)]
        private float primaryPulseDuration = 0.075f;

        [SerializeField, Min(0f)]
        private float primaryPulseScale = 0.045f;

        // This is set by the scene presentation coordinator from the active
        // authored scenario. It intentionally remains runtime-only so actor
        // selection stays owned by character/enemy definitions rather than
        // writing scene-specific state back into a shared ScriptableObject.
        private D0ActorPresentationDefinition runtimePresentationOverride;
        private D0WeaponDefinition runtimeWeaponDefinition;
        private bool initialized;
        private D0ActorAnimationStateMachine animationStateMachine;
        private float pulseEndTime;
        private Vector3 pulseBaseScale;
        private bool presentationPaused;
        private float pauseStartedAt;
        private float skeletonTimeScaleBeforePause = 1f;
        private FpgSpineSkillAnimationEvaluator skillAnimationEvaluator;
        private SkillExecutionId activeSkillExecutionId =
            SkillExecutionId.Invalid;

        public SkeletonAnimation SkeletonAnimation => skeletonAnimation;
        public CombatPresentationProfile PresentationProfile => presentationProfile;
        public bool IsPlayerActor => playerActor;
        public bool IsInitialized => initialized;
        public bool IsChargingSecondary => animationStateMachine != null
            && animationStateMachine.IsChargingSecondary;
        public bool IsReloading => animationStateMachine != null
            && animationStateMachine.IsReloading;
        public bool IsTerminal => animationStateMachine != null
            && animationStateMachine.IsTerminal;
        public D0ActorAnimationState AnimationState => animationStateMachine == null
            ? D0ActorAnimationState.Uninitialized
            : animationStateMachine.State;
        public D0ActorPresentationDefinition RuntimePresentationOverride => runtimePresentationOverride;
        public D0WeaponDefinition RuntimeWeaponDefinition => runtimeWeaponDefinition;
        public PlayerActorPresentationDefinition ActivePlayerPresentation => ResolvePlayerPresentation();
        public EnemyActorPresentationDefinition ActiveEnemyPresentation => ResolveEnemyPresentation();
        public string CurrentAnimationName { get; private set; }
        public bool HasActiveSkillAnimation => activeSkillExecutionId.IsValid;
        public SkillExecutionId ActiveSkillExecutionId =>
            activeSkillExecutionId;

        public bool TryConfigureRuntime(
            SkeletonAnimation nextSkeletonAnimation,
            CombatPresentationProfile nextPresentationProfile,
            bool isPlayerActor,
            Transform nextPulseRoot,
            D0ActorPresentationDefinition presentationOverride,
            out string error)
        {
            if (initialized)
            {
                error = "Actor2DPresenter cannot be reconfigured after initialization.";
                return false;
            }

            skeletonAnimation = nextSkeletonAnimation;
            presentationProfile = nextPresentationProfile;
            playerActor = isPlayerActor;
            pulseRoot = nextPulseRoot;
            if (!TrySetRuntimePresentationOverride(presentationOverride, out error))
            {
                return false;
            }

            return TryValidateWithPresentation(
                runtimePresentationOverride,
                out error);
        }

        public bool TrySetRuntimePresentationOverride(
            D0ActorPresentationDefinition definition,
            out string error)
        {
            if (definition == null)
            {
                runtimePresentationOverride = null;
                error = string.Empty;
                return true;
            }

            D0ActorKind expectedKind = playerActor ? D0ActorKind.Player : D0ActorKind.Enemy;
            if (definition.ActorKind != expectedKind)
            {
                error = $"Actor2DPresenter expects a {expectedKind} presentation definition.";
                return false;
            }

            if (!definition.TryValidate(out error))
            {
                return false;
            }

            runtimePresentationOverride = definition;
            error = string.Empty;
            return true;
        }

        public bool TrySetRuntimeWeaponDefinition(
            D0WeaponDefinition definition,
            out string error)
        {
            if (!playerActor)
            {
                error =
                    "Only a player Actor2DPresenter can bind a weapon definition.";
                return false;
            }

            if (definition == null)
            {
                runtimeWeaponDefinition = null;
                error = string.Empty;
                return true;
            }

            if (!definition.TryValidatePresentation(out error))
            {
                return false;
            }

            SkeletonData data = skeletonAnimation == null
                || skeletonAnimation.SkeletonDataAsset == null
                ? null
                : skeletonAnimation.SkeletonDataAsset.GetSkeletonData(true);
            if (data != null
                && !TryValidateWeaponAnimations(data, definition, out error))
            {
                return false;
            }

            runtimeWeaponDefinition = definition;
            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (!TryValidateWithPresentation(
                    runtimePresentationOverride,
                    out error))
            {
                return false;
            }

            if (!playerActor)
            {
                return true;
            }

            if (runtimeWeaponDefinition == null)
            {
                error =
                    "Player Actor2DPresenter requires an explicitly bound weapon definition.";
                return false;
            }

            SkeletonData data =
                skeletonAnimation.SkeletonDataAsset.GetSkeletonData(true);
            return TryValidateWeaponAnimations(
                data,
                runtimeWeaponDefinition,
                out error);
        }

        public bool TryValidateWithPresentation(
            D0ActorPresentationDefinition presentationDefinition,
            out string error)
        {
            error = string.Empty;
            if (skeletonAnimation == null)
            {
                error = "Actor2DPresenter requires a SkeletonAnimation.";
                return false;
            }

            if (presentationProfile == null
                || !presentationProfile.TryValidateStatic(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error =
                        "Actor2DPresenter requires the global combat presentation profile.";
                }

                return false;
            }

            if (presentationDefinition == null)
            {
                error =
                    "Actor2DPresenter requires an explicitly injected actor state presentation.";
                return false;
            }

            D0ActorKind expectedKind =
                playerActor ? D0ActorKind.Player : D0ActorKind.Enemy;
            if (presentationDefinition.ActorKind != expectedKind)
            {
                error =
                    $"Actor2DPresenter expects a {expectedKind} state presentation.";
                return false;
            }

            if (!presentationDefinition.TryValidate(out error))
            {
                return false;
            }

            SkeletonDataAsset dataAsset =
                skeletonAnimation.SkeletonDataAsset;
            SkeletonData data =
                dataAsset == null ? null : dataAsset.GetSkeletonData(true);
            if (data == null)
            {
                error =
                    "Actor2DPresenter requires a loaded SkeletonDataAsset.";
                return false;
            }

            if (playerActor)
            {
                if (!presentationDefinition.TryGetPlayer(
                        out PlayerActorPresentationDefinition player)
                    || player == null)
                {
                    error =
                        "Actor2DPresenter requires player state presentation data.";
                    return false;
                }

                return TryValidateAnimations(
                    data,
                    out error,
                    player.IdleAnimation,
                    player.HitAnimation,
                    player.GroggyAnimation,
                    player.DefeatReadyAnimation,
                    player.DefeatAnimation,
                    player.VictoryReadyAnimation,
                    player.VictoryAnimation);
            }

            if (!presentationDefinition.TryGetEnemy(
                    out EnemyActorPresentationDefinition enemy)
                || enemy == null)
            {
                error =
                    "Actor2DPresenter requires enemy state presentation data.";
                return false;
            }

            return TryValidateAnimations(
                data,
                out error,
                enemy.EnterAnimation,
                enemy.IdleAnimation,
                enemy.HitAnimation,
                enemy.GroggyAnimation,
                enemy.DeathAnimation);
        }

        public bool TryInitialize(out string error)
        {
            if (initialized)
            {
                error = string.Empty;
                return true;
            }

            if (!TryValidate(out error))
            {
                return false;
            }

            skeletonAnimation.Initialize(false);
            if (skeletonAnimation.AnimationState == null)
            {
                error = "Actor2DPresenter could not initialize the SkeletonAnimation state.";
                return false;
            }

            if (pulseRoot == null)
            {
                pulseRoot = transform;
            }

            pulseBaseScale = pulseRoot.localScale;
            skeletonTimeScaleBeforePause = skeletonAnimation.timeScale;
            presentationPaused = false;
            pauseStartedAt = 0f;
            animationStateMachine = new D0ActorAnimationStateMachine(playerActor);
            animationStateMachine.TryApply(D0ActorAnimationCommand.Initialize);
            skillAnimationEvaluator =
                new FpgSpineSkillAnimationEvaluator(skeletonAnimation);
            activeSkillExecutionId = SkillExecutionId.Invalid;
            initialized = true;
            PlayIdle();
            error = string.Empty;
            return true;
        }

        public bool TryEvaluateSkillAnimation(
            SkillExecutionId executionId,
            string animationName,
            FpgCompiledSkillSequence sequence,
            int relativeTick,
            double interpolation,
            out string error)
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal
                || !executionId.IsValid
                || string.IsNullOrWhiteSpace(animationName)
                || !sequence.IsValid
                || relativeTick < 0
                || relativeTick > sequence.DurationTicks
                || double.IsNaN(interpolation)
                || double.IsInfinity(interpolation)
                || interpolation < 0d
                || interpolation >= 1d)
            {
                error =
                    "Player skill animation evaluation requires a valid execution, compiled sequence, animation and absolute tick.";
                return false;
            }

            if (presentationPaused)
            {
                error = string.Empty;
                return true;
            }

            if (activeSkillExecutionId != executionId)
            {
                skillAnimationEvaluator.Reset();
                activeSkillExecutionId = executionId;
            }

            if (!skillAnimationEvaluator.TryEvaluate(
                    animationName,
                    sequence,
                    relativeTick,
                    interpolation,
                    out error))
            {
                return false;
            }

            CurrentAnimationName = animationName;
            return true;
        }

        public void CompleteSkillAnimation(SkillExecutionId executionId)
        {
            if (!executionId.IsValid || activeSkillExecutionId != executionId)
            {
                return;
            }

            ResetSkillAnimationEvaluation();
            if (!initialized || IsTerminal)
            {
                return;
            }

            if (IsChargingSecondary || IsReloading)
            {
                return;
            }

            PlayIdle();
        }

        public void CancelSkillAnimation(SkillExecutionId executionId)
        {
            if (executionId.IsValid && activeSkillExecutionId == executionId)
            {
                ResetSkillAnimationEvaluation();
                if (initialized && !IsTerminal
                    && !IsChargingSecondary && !IsReloading)
                {
                    PlayIdle();
                }
            }
        }

        public bool TryPlaySkillOneShotAnimation(
            string animationName,
            bool loop,
            out string error)
        {
            error = string.Empty;
            if (!EnsureInitialized() || !playerActor || IsTerminal
                || !TryValidateAnimation(
                    skeletonAnimation.Skeleton.Data,
                    animationName,
                    out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error =
                        "Player skill one-shot animation requires an available Spine animation.";
                }

                return false;
            }

            ResetSkillAnimationEvaluation();
            SetAnimation(animationName, loop);
            return true;
        }

        public void NotifyPrimarySkillCommitted()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal)
            {
                return;
            }

            if (!TryApplyAnimationCommand(D0ActorAnimationCommand.PrimaryAttack))
            {
                return;
            }

            PulsePrimary();
        }

        public void NotifyReloadStarted()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal
                || !TryApplyAnimationCommand(
                    D0ActorAnimationCommand.BeginReload))
            {
                return;
            }
        }

        public void NotifyReloadCompleted()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal
                || !TryApplyAnimationCommand(D0ActorAnimationCommand.CompleteReload))
            {
                return;
            }
        }

        public void NotifySecondaryChargeStarted()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal)
            {
                return;
            }

            if (IsChargingSecondary)
            {
                return;
            }

            if (!TryApplyAnimationCommand(D0ActorAnimationCommand.BeginSecondaryCharge))
            {
                return;
            }
        }

        public void NotifySecondaryChargeCanceled()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal
                || !TryApplyAnimationCommand(D0ActorAnimationCommand.CancelSecondaryCharge))
            {
                return;
            }
        }

        public void NotifySecondaryReleaseCommitted()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal
                || !TryApplyAnimationCommand(D0ActorAnimationCommand.ReleaseSecondary))
            {
                return;
            }
        }

        public void PlayHit()
        {
            if (!EnsureInitialized() || IsTerminal
                || !TryApplyAnimationCommand(D0ActorAnimationCommand.Hit))
            {
                return;
            }

            string hitAnimation = playerActor
                ? ActivePlayerPresentation.HitAnimation
                : ActiveEnemyPresentation.HitAnimation;
            PlayOneShotThenIdle(hitAnimation);
        }

        public void PlayGroggy()
        {
            if (!EnsureInitialized() || IsTerminal)
            {
                return;
            }

            // Fei's presentation-only Groggy is driven by BarrierBroken. A
            // barrier hit, including the hit that depletes it, must not
            // replace an in-progress reload animation.
            if (playerActor && IsReloading)
            {
                return;
            }

            if (playerActor)
            {
                // The domain only exposes persistent Groggy for the enemy. Fei
                // uses this animation as the short presentation-only stagger
                // driven by BarrierBroken, so it must return to idle instead
                // of locking the player actor indefinitely.
                if (TryApplyAnimationCommand(D0ActorAnimationCommand.PlayerGroggy))
                {
                    PlayOneShotThenIdle(
                        ActivePlayerPresentation.GroggyAnimation);
                }

                return;
            }

            if (TryApplyAnimationCommand(D0ActorAnimationCommand.EnemyGroggyStarted))
            {
                PlayLooping(ActiveEnemyPresentation.GroggyAnimation);
            }
        }

        /// <summary>
        /// Returns a non-terminal actor to its idle loop without resetting
        /// pooled/transient presentation state. Enemy Groggy recovery uses this
        /// rather than a hard replay so the visual bridge remains read-only.
        /// </summary>
        public void ReturnToIdle()
        {
            D0ActorAnimationCommand command = !playerActor
                && AnimationState == D0ActorAnimationState.EnemyGroggy
                ? D0ActorAnimationCommand.EnemyGroggyEnded
                : D0ActorAnimationCommand.ReturnToIdle;
            if (!EnsureInitialized() || IsTerminal
                || !TryApplyAnimationCommand(command))
            {
                return;
            }

            PlayIdle();
        }

        public void PlayEnemyEnter()
        {
            if (!EnsureInitialized() || playerActor || IsTerminal
                || !TryApplyAnimationCommand(D0ActorAnimationCommand.EnemyEnter))
            {
                return;
            }

            PlayOneShotThenIdle(ActiveEnemyPresentation.EnterAnimation);
        }

        public void PlayVictory()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal
                || !TryApplyAnimationCommand(D0ActorAnimationCommand.PlayerVictory))
            {
                return;
            }

            PlayerActorPresentationDefinition player = ActivePlayerPresentation;
            SetAnimation(player.VictoryReadyAnimation, false);
            AddAnimation(player.VictoryAnimation, true);
        }

        public void PlayDefeat()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal
                || !TryApplyAnimationCommand(D0ActorAnimationCommand.PlayerDefeat))
            {
                return;
            }

            PlayerActorPresentationDefinition player = ActivePlayerPresentation;
            SetAnimation(player.DefeatReadyAnimation, false);
            AddAnimation(player.DefeatAnimation, true);
        }

        public void PlayEnemyDeath()
        {
            if (!EnsureInitialized() || playerActor || IsTerminal
                || !TryApplyAnimationCommand(D0ActorAnimationCommand.EnemyDeath))
            {
                return;
            }

            SetAnimation(ActiveEnemyPresentation.DeathAnimation, false);
        }

        public void ClearAndReturnToIdle()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            animationStateMachine?.TryApply(D0ActorAnimationCommand.Reset);
            ResetSkillAnimationEvaluation();
            pulseEndTime = 0f;
            if (pulseRoot != null)
            {
                pulseRoot.localScale = pulseBaseScale;
            }

            skeletonAnimation.ClearState();
            skeletonAnimation.Initialize(false);
            if (presentationPaused)
            {
                skeletonAnimation.timeScale = 0f;
            }

            if (skeletonAnimation.Skeleton != null)
            {
                skeletonAnimation.Skeleton.SetToSetupPose();
                skeletonAnimation.Skeleton.UpdateWorldTransform();
            }

            PlayIdle();
        }

        /// <summary>
        /// Freezes only this local presentation. The battle session remains the
        /// authority for pause state and continues to own all gameplay timing.
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (!initialized || skeletonAnimation == null || presentationPaused == paused)
            {
                return;
            }

            if (paused)
            {
                presentationPaused = true;
                pauseStartedAt = Time.unscaledTime;
                skeletonTimeScaleBeforePause = skeletonAnimation.timeScale;
                skeletonAnimation.timeScale = 0f;
                return;
            }

            float pausedDuration = Mathf.Max(0f, Time.unscaledTime - pauseStartedAt);
            if (pulseEndTime > 0f)
            {
                pulseEndTime += pausedDuration;
            }

            skeletonAnimation.timeScale = skeletonTimeScaleBeforePause;
            presentationPaused = false;
            pauseStartedAt = 0f;
        }

        private void OnDisable()
        {
            ResetSkillAnimationEvaluation();
            pulseEndTime = 0f;
            if (initialized && presentationPaused && skeletonAnimation != null)
            {
                skeletonAnimation.timeScale = skeletonTimeScaleBeforePause;
                presentationPaused = false;
                pauseStartedAt = 0f;
            }

            if (initialized && pulseRoot != null)
            {
                pulseRoot.localScale = pulseBaseScale;
            }
        }

        private void LateUpdate()
        {
            if (!initialized || presentationPaused || pulseRoot == null || pulseEndTime <= 0f)
            {
                return;
            }

            float remaining = pulseEndTime - Time.unscaledTime;
            if (remaining <= 0f)
            {
                pulseRoot.localScale = pulseBaseScale;
                pulseEndTime = 0f;
                return;
            }

            float normalized = Mathf.Clamp01(remaining / primaryPulseDuration);
            pulseRoot.localScale = pulseBaseScale * (1f + primaryPulseScale * normalized);
        }

        private PlayerActorPresentationDefinition ResolvePlayerPresentation()
        {
            return runtimePresentationOverride != null
                && runtimePresentationOverride.TryGetPlayer(
                    out PlayerActorPresentationDefinition definition)
                ? definition
                : null;
        }

        private EnemyActorPresentationDefinition ResolveEnemyPresentation()
        {
            return runtimePresentationOverride != null
                && runtimePresentationOverride.TryGetEnemy(
                    out EnemyActorPresentationDefinition definition)
                ? definition
                : null;
        }

        private void PlayIdle()
        {
            if (!initialized || skeletonAnimation.AnimationState == null)
            {
                return;
            }

            string idle = playerActor
                ? ActivePlayerPresentation.IdleAnimation
                : ActiveEnemyPresentation.IdleAnimation;
            SetAnimation(idle, true);
        }

        private void PlayOneShotThenIdle(string animation)
        {
            SetAnimation(animation, false);
            AddAnimation(playerActor
                ? ActivePlayerPresentation.IdleAnimation
                : ActiveEnemyPresentation.IdleAnimation, true);
        }

        private void PlayLooping(string animation)
        {
            SetAnimation(animation, true);
        }

        private void SetAnimation(string animation, bool loop)
        {
            if (skeletonAnimation == null || skeletonAnimation.AnimationState == null)
            {
                return;
            }

            skeletonAnimation.AnimationState.SetAnimation(MainTrack, animation, loop);
            CurrentAnimationName = animation;
        }

        private void AddAnimation(string animation, bool loop)
        {
            if (skeletonAnimation == null || skeletonAnimation.AnimationState == null)
            {
                return;
            }

            skeletonAnimation.AnimationState.AddAnimation(MainTrack, animation, loop, 0f);
        }

        private void ResetSkillAnimationEvaluation()
        {
            skillAnimationEvaluator?.Reset();
            activeSkillExecutionId = SkillExecutionId.Invalid;
        }

        private void PulsePrimary()
        {
            if (pulseRoot == null || primaryPulseDuration <= 0f)
            {
                return;
            }

            pulseEndTime = Time.unscaledTime + primaryPulseDuration;
        }

        private bool EnsureInitialized()
        {
            return initialized || TryInitialize(out _);
        }

        private bool TryApplyAnimationCommand(D0ActorAnimationCommand command)
        {
            return animationStateMachine != null && animationStateMachine.TryApply(command);
        }

        private static bool TryValidateWeaponAnimations(
            SkeletonData data,
            D0WeaponDefinition weaponDefinition,
            out string error)
        {
            error = string.Empty;
            if (weaponDefinition == null
                || !weaponDefinition.TryValidatePresentation(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error =
                        "Player actor requires a valid weapon presentation.";
                }

                return false;
            }

            return TryValidateSkillAnimations(
                    data,
                    weaponDefinition.PrimarySkill,
                    out error)
                && TryValidateSkillAnimations(
                    data,
                    weaponDefinition.ImmediateSecondarySkill,
                    out error)
                && TryValidateSkillAnimations(
                    data,
                    weaponDefinition.ChargeSecondarySkill,
                    out error)
                && TryValidateSkillAnimations(
                    data,
                    weaponDefinition.ReloadSkill,
                    out error);
        }

        private static bool TryValidateSkillAnimations(
            SkeletonData data,
            FpgPlayerSkillDefinition skill,
            out string error)
        {
            if (skill == null)
            {
                error = "Player actor requires all formal weapon skills.";
                return false;
            }

            IReadOnlyList<FpgSkillSequenceDefinition> sequences =
                skill.Sequences;
            for (int sequenceIndex = 0;
                sequenceIndex < sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence = sequences[sequenceIndex];
                if (sequence == null)
                {
                    error = "Player skill contains a missing sequence.";
                    return false;
                }

                if (!TryValidateAnimation(
                        data,
                        sequence.MainAnimation,
                        out error))
                {
                    return false;
                }

                IReadOnlyList<string> alternates =
                    sequence.AlternateAnimations;
                for (int animationIndex = 0;
                    animationIndex < alternates.Count;
                    animationIndex++)
                {
                    if (!TryValidateAnimation(
                            data,
                            alternates[animationIndex],
                            out error))
                    {
                        return false;
                    }
                }

                IReadOnlyList<FpgSkillReloadEventDefinition> reloads =
                    sequence.ReloadEvents;
                for (int reloadIndex = 0;
                    reloadIndex < reloads.Count;
                    reloadIndex++)
                {
                    string successAnimation =
                        reloads[reloadIndex].SuccessAnimationName;
                    if (!string.IsNullOrEmpty(successAnimation)
                        && !TryValidateAnimation(
                            data,
                            successAnimation,
                            out error))
                    {
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateAnimations(
            SkeletonData data,
            out string error,
            params string[] animationNames)
        {
            for (int index = 0; index < animationNames.Length; index++)
            {
                if (!TryValidateAnimation(
                        data,
                        animationNames[index],
                        out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateAnimation(
            SkeletonData data,
            string animationName,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(animationName)
                || data.FindAnimation(animationName) == null)
            {
                error = $"Spine animation '{animationName}' is unavailable on this actor.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
