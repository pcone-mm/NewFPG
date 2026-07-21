using System;
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

        [SerializeField, Min(0f)]
        private float primaryRetriggerWindow = 0.48f;

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
        private int nextPrimaryVariant;
        private float nextPrimaryAnimationTime;
        private float pulseEndTime;
        private Vector3 pulseBaseScale;
        private bool presentationPaused;
        private float pauseStartedAt;
        private float skeletonTimeScaleBeforePause = 1f;

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
            initialized = true;
            PlayIdle();
            error = string.Empty;
            return true;
        }

        public void PlayPrimaryAttack()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal)
            {
                return;
            }

            if (!TryApplyAnimationCommand(D0ActorAnimationCommand.PrimaryAttack))
            {
                return;
            }

            float now = Time.unscaledTime;
            PulsePrimary();
            if (now < nextPrimaryAnimationTime)
            {
                return;
            }

            D0WeaponShotPresentationDefinition primary =
                runtimeWeaponDefinition.PrimaryPresentation;
            string animation = nextPrimaryVariant == 0
                || string.IsNullOrWhiteSpace(primary.AlternateAnimationName)
                ? primary.AnimationName
                : primary.AlternateAnimationName;
            nextPrimaryVariant = string.IsNullOrWhiteSpace(
                primary.AlternateAnimationName)
                ? 0
                : 1 - nextPrimaryVariant;
            nextPrimaryAnimationTime =
                now + ResolvePrimaryRetriggerWindow();
            PlayOneShotThenIdle(animation);
        }

        public void BeginReload()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal
                || !TryApplyAnimationCommand(
                    D0ActorAnimationCommand.BeginReload))
            {
                return;
            }

            D0WeaponReloadPresentationDefinition reload =
                runtimeWeaponDefinition.ReloadPresentation;
            SetAnimation(reload.PlayAnimation, false);
            AddAnimation(reload.ReadyAnimation, true);
        }

        public void CompleteReload()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal
                || !TryApplyAnimationCommand(D0ActorAnimationCommand.CompleteReload))
            {
                return;
            }

            PlayIdle();
        }

        public void BeginSecondaryCharge()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal)
            {
                return;
            }

            // A feed rebind can rebuild the durable charge visual after its
            // original InputAccepted trace has fallen outside the retained
            // window. Reassert the loop without requiring a duplicate state
            // transition so the presenter can recover independently of short
            // event delivery.
            if (IsChargingSecondary)
            {
                PlayLooping(runtimeWeaponDefinition.SecondaryPresentation.ChargeAnimation);
                return;
            }

            if (!TryApplyAnimationCommand(D0ActorAnimationCommand.BeginSecondaryCharge))
            {
                return;
            }

            PlayLooping(runtimeWeaponDefinition.SecondaryPresentation.ChargeAnimation);
        }

        public void CancelSecondaryCharge()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal
                || !TryApplyAnimationCommand(D0ActorAnimationCommand.CancelSecondaryCharge))
            {
                return;
            }

            PlayOneShotThenIdle(runtimeWeaponDefinition.SecondaryPresentation.EndAnimation);
        }

        public void PlaySecondaryRelease()
        {
            if (!EnsureInitialized() || !playerActor || IsTerminal
                || !TryApplyAnimationCommand(D0ActorAnimationCommand.ReleaseSecondary))
            {
                return;
            }

            PlayOneShotThenIdle(runtimeWeaponDefinition.SecondaryPresentation.ReleaseAnimation);
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
            if (playerActor && IsChargingSecondary)
            {
                // Taking damage does not cancel the domain weapon charge. Keep
                // the durable presentation state in sync by resuming its loop
                // after the short reaction instead of silently falling idle.
                PlayOneShotThenLoop(
                    hitAnimation,
                    runtimeWeaponDefinition.SecondaryPresentation.ChargeAnimation);
                return;
            }

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
                    if (IsChargingSecondary)
                    {
                        PlayOneShotThenLoop(
                            ActivePlayerPresentation.GroggyAnimation,
                            runtimeWeaponDefinition.SecondaryPresentation.ChargeAnimation);
                    }
                    else
                    {
                        PlayOneShotThenIdle(ActivePlayerPresentation.GroggyAnimation);
                    }
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

        public bool PlayEnemyAttack(
            D0EnemyAttackDefinition attackDefinition)
        {
            if (!EnsureInitialized() || playerActor || IsTerminal
                || !TryValidateEnemyAttack(attackDefinition, out _))
            {
                return false;
            }

            D0ActorAnimationCommand command =
                attackDefinition.PresentationKey
                    == CombatPresentationProfile.FastThreatPresentationKey
                ? D0ActorAnimationCommand.EnemyFastThreat
                : D0ActorAnimationCommand.EnemyVolleyThreat;
            if (!TryApplyAnimationCommand(command))
            {
                return false;
            }

            PlayOneShotThenIdle(attackDefinition.ReleaseAnimationName);
            return true;
        }

        public bool TryValidateEnemyAttack(
            D0EnemyAttackDefinition attackDefinition,
            out string error)
        {
            error = string.Empty;
            if (playerActor)
            {
                error =
                    "A player Actor2DPresenter cannot play enemy attacks.";
                return false;
            }

            if (attackDefinition == null
                || !attackDefinition.TryValidatePresentation(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error =
                        "Enemy Actor2DPresenter requires an attack definition.";
                }

                return false;
            }

            SkeletonData data = skeletonAnimation == null
                || skeletonAnimation.SkeletonDataAsset == null
                ? null
                : skeletonAnimation.SkeletonDataAsset.GetSkeletonData(true);
            if (data == null)
            {
                error =
                    "Enemy Actor2DPresenter requires loaded skeleton data.";
                return false;
            }

            return TryValidateAnimations(
                data,
                out error,
                attackDefinition.AttackAnimation,
                attackDefinition.ReleaseAnimationName);
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
            nextPrimaryVariant = 0;
            nextPrimaryAnimationTime = 0f;
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

            if (nextPrimaryAnimationTime > 0f)
            {
                nextPrimaryAnimationTime += pausedDuration;
            }

            skeletonAnimation.timeScale = skeletonTimeScaleBeforePause;
            presentationPaused = false;
            pauseStartedAt = 0f;
        }

        private void OnDisable()
        {
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

        private void PlayOneShotThenLoop(string animation, string loopAnimation)
        {
            SetAnimation(animation, false);
            AddAnimation(loopAnimation, true);
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

        private void PulsePrimary()
        {
            if (pulseRoot == null || primaryPulseDuration <= 0f)
            {
                return;
            }

            pulseEndTime = Time.unscaledTime + primaryPulseDuration;
        }

        private float ResolvePrimaryRetriggerWindow()
        {
            D0WeaponShotPresentationDefinition primary =
                runtimeWeaponDefinition == null
                    ? null
                    : runtimeWeaponDefinition.PrimaryPresentation;
            return primary == null
                ? primaryRetriggerWindow
                : Mathf.Min(
                    primaryRetriggerWindow,
                    primary.TracerDuration);
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

            D0WeaponShotPresentationDefinition primary =
                weaponDefinition.PrimaryPresentation;
            D0WeaponSecondaryPresentationDefinition secondary =
                weaponDefinition.SecondaryPresentation;
            D0WeaponReloadPresentationDefinition reload =
                weaponDefinition.ReloadPresentation;
            if (!TryValidateAnimations(
                    data,
                    out error,
                    primary.AnimationName,
                    secondary.Shot.AnimationName,
                    secondary.ChargeAnimation,
                    secondary.ReleaseAnimation,
                    secondary.EndAnimation,
                    reload.PlayAnimation,
                    reload.ReadyAnimation))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(primary.AlternateAnimationName)
                || TryValidateAnimation(
                    data,
                    primary.AlternateAnimationName,
                    out error);
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
