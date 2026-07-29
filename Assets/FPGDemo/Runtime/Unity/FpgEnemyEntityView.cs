using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Skills;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Formal entity view contract. It exposes explicit anchors and keeps all
    /// hit parts disabled until the encounter activation boundary.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class FpgEnemyEntityView : MonoBehaviour,
        IFpgFormalEnemyEntityBinder,
        IFpgFormalEnemyPresentationView,
        IFpgFormalEnemyMotionView
    {
        private const int MainAnimationTrack = 0;

        private enum TickDrivenAnimationKind
        {
            None = 0,
            Entry,
            Idle,
            Skill,
            RenderDrivenEntry
        }

        [SerializeField]
        private Transform gameplayAnchor;

        [SerializeField]
        private Transform projectileAnchor;

        [SerializeField]
        private Transform weakpointAnchor;

        [SerializeField]
        private Transform overheadHealthBarAnchor;

        [SerializeField]
        private D0ActorSocketRegistry socketRegistry;
        [SerializeField]
        private Collider[] hitParts = Array.Empty<Collider>();

        [SerializeField]
        private HitPart[] hitPartKinds = Array.Empty<HitPart>();

        [SerializeField]
        private D0EnemyHitboxFollowSettings[] hitPartFollowSettings =
            Array.Empty<D0EnemyHitboxFollowSettings>();

        [SerializeField]
        private bool previewHitboxesInPlayMode = true;

        [SerializeField]
        private Color skillWarningTint =
            new Color(1f, 0.3f, 0.12f, 1f);

        [SerializeField]
        private SkeletonAnimation skeletonAnimation;

        [NonSerialized]
        private string runtimeId;

        [NonSerialized]
        private int spawnSequence = -1;

        [NonSerialized]
        private bool gameplayEnabled;

        [NonSerialized]
        private FpgEnemyDefinition boundDefinition;

        [NonSerialized]
        private FpgEnemyBehaviorDefinition boundBehavior;

        [NonSerialized]
        private bool presentationInitialized;

        [NonSerialized]
        private FpgSpineSkillAnimationEvaluator skillAnimationEvaluator;

        [NonSerialized]
        private SkillExecutionId activeSkillExecutionId =
            SkillExecutionId.Invalid;

        [NonSerialized]
        private SkillExecutionId synchronouslyTerminatedSkillExecutionId =
            SkillExecutionId.Invalid;

        [NonSerialized]
        private FpgFormalEnemySkillSequenceFrame pendingSkillFrame;

        [NonSerialized]
        private bool hasPendingSkillFrame;

        [NonSerialized]
        private int activeSkillWarningCount;

        [NonSerialized]
        private bool hasSkeletonBaseColor;

        [NonSerialized]
        private Color skeletonBaseColor = Color.white;

        [NonSerialized]
        private D0ActorSocketRegistry resolvedSocketRegistry;

        [NonSerialized]
        private FpgEntitySkeletonRootMotionBridge rootMotionBridge;

        [NonSerialized]
        private D0EnemyHitboxBoneFollowRuntime hitboxBoneFollowRuntime;

        [NonSerialized]
        private float authoredSkeletonTimeScale = 1f;

        [NonSerialized]
        private TickDrivenAnimationKind tickDrivenAnimationKind;

        [NonSerialized]
        private TrackEntry tickDrivenTrackEntry;

        [NonSerialized]
        private string tickDrivenAnimationName = string.Empty;

        [NonSerialized]
        private TickIndex tickDrivenStartTick = TickIndex.Invalid;

        [NonSerialized]
        private TickIndex lastMotionTick = TickIndex.Invalid;

        [NonSerialized]
        private float tickDrivenAnimationDuration;

        [NonSerialized]
        private FpgCompiledSkillSequence tickDrivenSkillSequence;

        [NonSerialized]
        private SkillExecutionId tickDrivenSkillExecutionId =
            SkillExecutionId.Invalid;

        [NonSerialized]
        private bool returnToIdleOnNextMotionTick;

        [NonSerialized]
        private string lastRootMotionError = string.Empty;
        public Transform GameplayAnchor => gameplayAnchor == null ? transform : gameplayAnchor;
        public Transform ProjectileAnchor => projectileAnchor == null ? GameplayAnchor : projectileAnchor;
        public Transform WeakpointAnchor => weakpointAnchor == null ? GameplayAnchor : weakpointAnchor;
        public D0ActorSocketRegistry SocketRegistry => socketRegistry != null
            ? socketRegistry
            : resolvedSocketRegistry != null
                ? resolvedSocketRegistry
                : (resolvedSocketRegistry =
                    GetComponentInChildren<D0ActorSocketRegistry>(true));
        public Transform OverheadHealthBarAnchor => overheadHealthBarAnchor == null
            ? GameplayAnchor
            : overheadHealthBarAnchor;
        public IReadOnlyList<Collider> HitParts => hitParts ?? Array.Empty<Collider>();
        public int HitPartCount => hitParts == null ? 0 : hitParts.Length;
        public int BoneFollowHitPartCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < HitPartCount; index++)
                {
                    if (TryGetHitPartFollowSettings(
                            index,
                            out D0EnemyHitboxFollowSettings settings)
                        && settings.FollowMode
                            == D0EnemyHitboxFollowMode.SpineBone)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
        public string RuntimeId => runtimeId ?? string.Empty;
        public int SpawnSequence => spawnSequence;
        public bool GameplayEnabled => gameplayEnabled;
        public SkeletonAnimation SkeletonAnimation => skeletonAnimation;
        public string LastRootMotionError => lastRootMotionError ?? string.Empty;
        public bool PreviewHitboxesInPlayMode => previewHitboxesInPlayMode;

        public bool TryGetHitPart(
            int hitPartOrdinal,
            out Collider collider,
            out HitPart hitPart)
        {
            Collider[] colliders = hitParts ?? Array.Empty<Collider>();
            if (hitPartOrdinal < 0 || hitPartOrdinal >= colliders.Length)
            {
                collider = null;
                hitPart = HitPart.Body;
                return false;
            }

            collider = colliders[hitPartOrdinal];
            HitPart[] kinds = hitPartKinds ?? Array.Empty<HitPart>();
            if (kinds.Length != 0 && hitPartOrdinal >= kinds.Length)
            {
                hitPart = HitPart.Body;
                return false;
            }

            hitPart = kinds.Length == 0 ? HitPart.Body : kinds[hitPartOrdinal];
            return collider != null
                && Enum.IsDefined(typeof(HitPart), hitPart)
                && hitPart != HitPart.Projectile;
        }

        public bool TryGetHitPartFollowSettings(
            int hitPartOrdinal,
            out D0EnemyHitboxFollowSettings settings)
        {
            if (hitPartOrdinal < 0 || hitPartOrdinal >= HitPartCount)
            {
                settings = default(D0EnemyHitboxFollowSettings);
                return false;
            }

            D0EnemyHitboxFollowSettings[] followSettings =
                hitPartFollowSettings
                ?? Array.Empty<D0EnemyHitboxFollowSettings>();
            if (followSettings.Length != 0
                && hitPartOrdinal >= followSettings.Length)
            {
                settings = default(D0EnemyHitboxFollowSettings);
                return false;
            }

            settings = followSettings.Length == 0
                ? default(D0EnemyHitboxFollowSettings)
                : followSettings[hitPartOrdinal];
            return true;
        }

        public void BindRuntime(string nextRuntimeId, int nextSpawnSequence)
        {
            if (string.IsNullOrWhiteSpace(nextRuntimeId))
            {
                throw new ArgumentException("Runtime id is required.", nameof(nextRuntimeId));
            }

            if (nextSpawnSequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nextSpawnSequence));
            }

            runtimeId = nextRuntimeId;
            spawnSequence = nextSpawnSequence;
        }

        public void ClearRuntimeBinding()
        {
            runtimeId = string.Empty;
            spawnSequence = -1;
            gameplayEnabled = false;
        }

        public bool TryBindFormalRuntime(
            RuntimeId nextRuntimeId,
            int nextSpawnSequence,
            FpgEnemyDefinition definition,
            out string error)
        {
            if (!nextRuntimeId.IsValid || definition == null || nextSpawnSequence < 0)
            {
                error = "Formal entity binding requires a valid runtime, sequence and definition.";
                return false;
            }

            if (presentationInitialized
                || hitboxBoneFollowRuntime != null
                || !string.IsNullOrEmpty(runtimeId)
                || spawnSequence >= 0)
            {
                error = "Formal entity is already bound. Unbind it before binding again.";
                return false;
            }

            if (!TryValidate(out error))
            {
                return false;
            }

            if (!TryValidatePresentation(definition, out error)
                || !TryInitializePresentation(definition, out error))
            {
                return false;
            }

            if (!TryCreateHitboxBoneFollowRuntime(
                    out D0EnemyHitboxBoneFollowRuntime nextBoneFollowRuntime,
                    out error))
            {
                ResetPresentation();
                return false;
            }

            BindRuntime(nextRuntimeId.ToString(), nextSpawnSequence);
            boundDefinition = definition;
            boundBehavior = definition.Behavior;
            hitboxBoneFollowRuntime = nextBoneFollowRuntime;
            hitboxBoneFollowRuntime?.Activate();
            SetFormalGameplayEnabled(false);
            if (!TryPlayEntry(out error))
            {
                ResetPresentation();
                ClearRuntimeBinding();
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void SetFormalGameplayEnabled(bool enabled)
        {
            gameplayEnabled = enabled;
            Collider[] colliders = hitParts ?? Array.Empty<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = enabled;
                }
            }
        }

        public void UnbindFormalRuntime()
        {
            SetFormalGameplayEnabled(false);
            ResetPresentation();
            ClearRuntimeBinding();
        }

        public DomainResult AdvanceFormalMotion(TickIndex tick)
        {
            if (!tick.IsValid || !presentationInitialized)
            {
                return RejectRootMotion(
                    RejectReason.InvalidState,
                    "Formal root motion requires a valid tick and bound presentation.");
            }

            if (returnToIdleOnNextMotionTick)
            {
                returnToIdleOnNextMotionTick = false;
                if (!TryStartIdle(tick, out string idleError))
                {
                    return RejectRootMotion(
                        RejectReason.InvariantFault,
                        idleError);
                }
            }

            if (tickDrivenAnimationKind == TickDrivenAnimationKind.None)
            {
                lastRootMotionError = string.Empty;
                return DomainResult.Success;
            }

            if (!tickDrivenStartTick.IsValid)
            {
                tickDrivenStartTick = tick.Value > 0L
                    ? new TickIndex(tick.Value - 1L)
                    : tick;
                lastMotionTick = tickDrivenStartTick;
            }

            if (lastMotionTick.IsValid && tick.Value < lastMotionTick.Value)
            {
                return RejectRootMotion(
                    RejectReason.WrongTick,
                    "Formal root motion cannot move backward in tick time.");
            }

            if (lastMotionTick == tick)
            {
                lastRootMotionError = string.Empty;
                return DomainResult.Success;
            }

            long nextTick = lastMotionTick.IsValid
                ? lastMotionTick.Value + 1L
                : tickDrivenStartTick.Value;
            while (nextTick <= tick.Value)
            {
                if (tickDrivenAnimationKind
                    == TickDrivenAnimationKind.None)
                {
                    break;
                }

                if (!TryAdvanceTickDrivenAnimation(
                        new TickIndex(nextTick),
                        out string error))
                {
                    return RejectRootMotion(
                        RejectReason.InvariantFault,
                        error);
                }

                nextTick++;
            }

            lastRootMotionError = string.Empty;
            return DomainResult.Success;
        }

        public DomainResult StartFormalSkillMotion(
            in FpgFormalEnemySkillSequenceFrame frame)
        {
            if (!presentationInitialized
                || !gameplayEnabled
                || !frame.OwnerRuntimeId.IsValid
                || !string.Equals(
                    frame.OwnerRuntimeId.ToString(),
                    RuntimeId,
                    StringComparison.Ordinal)
                || frame.SpawnSequence != spawnSequence
                || frame.Definition == null
                || !OwnsAttack(frame.Definition)
                || !frame.CompiledSequence.IsValid
                || !frame.ExecutionId.IsValid
                || frame.State != FpgSkillExecutionState.Running
                || frame.RelativeTick != 0
                || frame.Tick != frame.StartTick)
            {
                return RejectRootMotion(
                    RejectReason.InvalidState,
                    "Formal skill root motion requires the matching running tick-zero frame.");
            }

            if (!FpgEnemySkillPresentationResolver.TryResolveAnimationName(
                    frame.Definition,
                    frame.CompiledSequence.Kind,
                    frame.ResolvedAnimationId,
                    out string animationName)
                || !HasAnimation(animationName))
            {
                return RejectRootMotion(
                    RejectReason.InvalidDefinition,
                    "Formal skill root motion cannot resolve its Spine animation variant.");
            }

            pendingSkillFrame = default(FpgFormalEnemySkillSequenceFrame);
            hasPendingSkillFrame = false;
            skillAnimationEvaluator?.Reset();
            activeSkillExecutionId = frame.ExecutionId;
            synchronouslyTerminatedSkillExecutionId =
                SkillExecutionId.Invalid;
            returnToIdleOnNextMotionTick = false;

            bool usesRootMotion = boundBehavior != null
                && boundBehavior.UsesAnimationRootMotion(animationName);
            if (usesRootMotion)
            {
                if (!TryStartTickDrivenAnimation(
                        animationName,
                        frame.CompiledSequence.Loop,
                        TickDrivenAnimationKind.Skill,
                        frame.StartTick,
                        frame.CompiledSequence,
                        frame.ExecutionId,
                        out string error))
                {
                    return RejectRootMotion(
                        RejectReason.InvariantFault,
                        error);
                }
            }
            else if (!TryStartRenderDrivenSkillAtTickZero(
                animationName,
                frame.CompiledSequence,
                out string error))
            {
                return RejectRootMotion(
                    RejectReason.InvariantFault,
                    error);
            }

            lastRootMotionError = string.Empty;
            return DomainResult.Success;
        }

        public DomainResult ApplyFormalSkillMotionFrame(
            in FpgFormalEnemySkillSequenceFrame frame)
        {
            if (!presentationInitialized
                || !frame.OwnerRuntimeId.IsValid
                || !string.Equals(
                    frame.OwnerRuntimeId.ToString(),
                    RuntimeId,
                    StringComparison.Ordinal)
                || frame.SpawnSequence != spawnSequence
                || frame.Definition == null
                || !OwnsAttack(frame.Definition)
                || !frame.CompiledSequence.IsValid
                || !frame.ExecutionId.IsValid
                || !frame.IsTerminal)
            {
                return RejectRootMotion(
                    RejectReason.InvalidState,
                    "Formal skill root motion requires the matching terminal frame.");
            }

            if (!FpgEnemySkillPresentationResolver.TryResolveAnimationName(
                    frame.Definition,
                    frame.CompiledSequence.Kind,
                    frame.ResolvedAnimationId,
                    out string animationName))
            {
                return RejectRootMotion(
                    RejectReason.InvalidDefinition,
                    "Formal skill root motion cannot resolve its terminal animation variant.");
            }

            bool usesRootMotion = boundBehavior != null
                && boundBehavior.UsesAnimationRootMotion(animationName);
            if (usesRootMotion
                && (tickDrivenAnimationKind != TickDrivenAnimationKind.Skill
                    || tickDrivenSkillExecutionId != frame.ExecutionId
                    || !string.Equals(
                        tickDrivenAnimationName,
                        animationName,
                        StringComparison.Ordinal)))
            {
                return RejectRootMotion(
                    RejectReason.InvariantFault,
                    "Terminal root-motion frame does not match the active skill animation.");
            }

            synchronouslyTerminatedSkillExecutionId = frame.ExecutionId;
            MarkTickDrivenSkillMotionTerminal();
            lastRootMotionError = string.Empty;
            return DomainResult.Success;
        }

        public bool TrySetSkillSequenceFrame(
            in FpgFormalEnemySkillSequenceFrame frame)
        {
            if (!presentationInitialized
                || !gameplayEnabled
                || frame.SpawnSequence != spawnSequence
                || frame.Definition == null
                || !OwnsAttack(frame.Definition)
                || !frame.CompiledSequence.IsValid
                || !frame.ExecutionId.IsValid)
            {
                return false;
            }

            if (frame.IsTerminal
                && synchronouslyTerminatedSkillExecutionId
                    == frame.ExecutionId)
            {
                synchronouslyTerminatedSkillExecutionId =
                    SkillExecutionId.Invalid;
                pendingSkillFrame =
                    default(FpgFormalEnemySkillSequenceFrame);
                hasPendingSkillFrame = false;
                return true;
            }

            pendingSkillFrame = frame;
            hasPendingSkillFrame = true;
            return true;
        }

        public bool TrySetSkillWarning(
            in FpgFormalEnemySkillWarningPresentationEvent warningEvent)
        {
            if (!CanPresentSkillEvent(warningEvent.TimelineEvent)
                || !TryValidatePresentationSocket(
                    warningEvent.Resolved.SocketName))
            {
                return false;
            }

            if (warningEvent.IsActive)
            {
                if (activeSkillWarningCount < int.MaxValue)
                {
                    activeSkillWarningCount++;
                }
            }
            else
            {
                activeSkillWarningCount = Math.Max(
                    0,
                    activeSkillWarningCount - 1);
            }

            ApplySkillFeedbackColor();
            return true;
        }

        public void ClearSkillWarnings()
        {
            activeSkillWarningCount = 0;
            ApplySkillFeedbackColor();
        }

        public bool TryValidatePresentation(
            FpgEnemyDefinition definition,
            out string error)
        {
            if (definition == null || definition.Behavior == null)
            {
                error =
                    "Formal enemy presentation requires a definition and behavior.";
                return false;
            }

            SkeletonData data = ResolveSkeletonData();
            if (skeletonAnimation == null || data == null)
            {
                error = $"Formal enemy '{definition.EnemyDefinitionId}' "
                    + "requires a loaded SkeletonAnimation.";
                return false;
            }

            FpgEnemyBehaviorDefinition behavior = definition.Behavior;
            if (!behavior.TryValidate(out error))
            {
                error = $"Formal enemy '{definition.EnemyDefinitionId}' "
                    + "has invalid animation root-motion rules: " + error;
                return false;
            }

            if (!HasAnimation(data, behavior.EntryAnimation)
                || !HasAnimation(data, behavior.IdleAnimation)
                || !HasAnimation(data, behavior.DeathAnimation))
            {
                error = $"Formal enemy '{definition.EnemyDefinitionId}' "
                    + "behavior references a missing Spine animation.";
                return false;
            }

            FpgEntitySkeletonRootMotionBridge bridge =
                ResolveRootMotionBridge();
            if (bridge == null
                || bridge.transform != skeletonAnimation.transform)
            {
                error = $"Formal enemy '{definition.EnemyDefinitionId}' "
                    + "requires FpgEntitySkeletonRootMotionBridge on its SkeletonAnimation node.";
                return false;
            }

            if (!bridge.TryValidateConfiguration(
                    data,
                    behavior,
                    out error))
            {
                error = $"Formal enemy '{definition.EnemyDefinitionId}' "
                    + "has invalid Spine root motion: " + error;
                return false;
            }

            for (int index = 0; index < definition.AttackPatternCount; index++)
            {
                FpgEnemyAttackDefinition attack =
                    definition.GetAttackPattern(index);
                if (attack == null
                    || !TryValidateAttackAnimations(data, attack))
                {
                    string attackId = attack == null
                        ? index.ToString()
                        : attack.SkillId;
                    error = $"Formal enemy '{definition.EnemyDefinitionId}' "
                        + $"attack '{attackId}' references a missing Spine animation.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            Collider[] colliders = hitParts ?? Array.Empty<Collider>();
            HitPart[] kinds = hitPartKinds ?? Array.Empty<HitPart>();
            D0EnemyHitboxFollowSettings[] followSettings =
                hitPartFollowSettings
                ?? Array.Empty<D0EnemyHitboxFollowSettings>();
            if (SocketRegistry == null)
            {
                error = "Formal enemy entity requires a D0ActorSocketRegistry.";
                return false;
            }
            if (colliders.Length == 0)
            {
                error = "Formal enemy entity requires at least one hit part.";
                return false;
            }

            if (kinds.Length != 0 && kinds.Length != colliders.Length)
            {
                error = "Formal enemy entity hit-part kinds must be empty or parallel the Collider array.";
                return false;
            }

            if (followSettings.Length != 0
                && followSettings.Length != colliders.Length)
            {
                error = "Formal enemy entity hit-part follow settings "
                    + "must be empty or parallel the Collider array.";
                return false;
            }

            SkeletonData skeletonData = null;
            bool skeletonDataLoaded = false;

            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] == null)
                {
                    error = $"Formal enemy entity hit part {index} is missing.";
                    return false;
                }

                HitPart kind = kinds.Length == 0 ? HitPart.Body : kinds[index];
                if (!Enum.IsDefined(typeof(HitPart), kind) || kind == HitPart.Projectile)
                {
                    error = $"Formal enemy entity hit part {index} has an invalid combatant kind.";
                    return false;
                }

                D0EnemyHitboxFollowSettings settings =
                    followSettings.Length == 0
                        ? default(D0EnemyHitboxFollowSettings)
                        : followSettings[index];
                if (!TryValidateHitboxFollowSettings(
                        settings,
                        index,
                        ref skeletonData,
                        ref skeletonDataLoaded,
                        out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static long DeriveGeometryId(int spawnSequence, int hitPartOrdinal)
        {
            return FpgFormalGeometryId.Derive(spawnSequence, hitPartOrdinal);
        }

        public static GeometryId DeriveCombatGeometryId(int spawnSequence, int hitPartOrdinal)
        {
            return FpgFormalGeometryId.DeriveCombatGeometryId(spawnSequence, hitPartOrdinal);
        }

        private void Awake()
        {
            SetFormalGameplayEnabled(false);
        }

        private void LateUpdate()
        {
            EvaluatePendingSkillFrame();
        }

        private void OnDisable()
        {
            SetFormalGameplayEnabled(false);
            ResetPresentation();
        }

        private bool TryInitializePresentation(
            FpgEnemyDefinition definition,
            out string error)
        {
            try
            {
                skeletonAnimation.Initialize(false);
                if (skeletonAnimation.AnimationState == null)
                {
                    error =
                        "Formal enemy SkeletonAnimation has no animation state.";
                    return false;
                }

                rootMotionBridge = ResolveRootMotionBridge();
                if (rootMotionBridge == null)
                {
                    error =
                        "Formal enemy requires an initialized root motion bridge.";
                    return false;
                }

                if (!rootMotionBridge.TryInitializeForEntity(
                        transform,
                        out error))
                {
                    return false;
                }

                authoredSkeletonTimeScale = skeletonAnimation.timeScale;
                ResetTickDrivenAnimationState();

                CaptureSkeletonBaseColor();
                activeSkillWarningCount = 0;
                boundDefinition = definition;
                boundBehavior = definition.Behavior;
                skillAnimationEvaluator =
                    new FpgSpineSkillAnimationEvaluator(
                        skeletonAnimation);
                activeSkillExecutionId = SkillExecutionId.Invalid;
                pendingSkillFrame =
                    default(FpgFormalEnemySkillSequenceFrame);
                hasPendingSkillFrame = false;
                presentationInitialized = true;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "Formal enemy presentation initialization failed: "
                    + exception.Message;
                return false;
            }
        }

        private void EvaluatePendingSkillFrame()
        {
            if (!hasPendingSkillFrame
                || !presentationInitialized
                || skillAnimationEvaluator == null)
            {
                return;
            }

            FpgFormalEnemySkillSequenceFrame frame =
                pendingSkillFrame;
            if (!FpgEnemySkillPresentationResolver
                    .TryResolveAnimationName(
                        frame.Definition,
                        frame.CompiledSequence.Kind,
                        frame.ResolvedAnimationId,
                        out string animationName))
            {
                ClearPendingSkillFrameAndReturnToIdle();
                return;
            }

            bool isTickDrivenSkill =
                tickDrivenAnimationKind
                    == TickDrivenAnimationKind.Skill
                && tickDrivenSkillExecutionId == frame.ExecutionId
                && string.Equals(
                    tickDrivenAnimationName,
                    animationName,
                    StringComparison.Ordinal);
            if (isTickDrivenSkill
                || (boundBehavior != null
                    && boundBehavior.UsesAnimationRootMotion(
                        animationName)))
            {
                if (frame.IsTerminal)
                {
                    MarkTickDrivenSkillMotionTerminal();
                }

                return;
            }

            if (frame.State == FpgSkillExecutionState.Canceled)
            {
                ClearPendingSkillFrameAndReturnToIdle();
                return;
            }

            if (activeSkillExecutionId != frame.ExecutionId)
            {
                skillAnimationEvaluator.Reset();
                activeSkillExecutionId = frame.ExecutionId;
            }

            double interpolation = frame.IsTerminal
                ? 0d
                : FpgFormalPlayerSkillAnimationClock
                    .ResolveInterpolation(
                        Time.timeAsDouble,
                        Time.fixedTimeAsDouble,
                        Time.fixedDeltaTime);
            if (!skillAnimationEvaluator.TryEvaluate(
                    animationName,
                    frame.CompiledSequence,
                    frame.RelativeTick,
                    interpolation,
                    out _))
            {
                ClearPendingSkillFrameAndReturnToIdle();
                return;
            }

            if (frame.State == FpgSkillExecutionState.Completed)
            {
                ClearPendingSkillFrameAndReturnToIdle();
            }
        }

        private void MarkTickDrivenSkillMotionTerminal()
        {
            pendingSkillFrame = default(FpgFormalEnemySkillSequenceFrame);
            hasPendingSkillFrame = false;
            activeSkillExecutionId = SkillExecutionId.Invalid;
            skillAnimationEvaluator?.Reset();
            returnToIdleOnNextMotionTick = true;
        }

        private void ClearPendingSkillFrameAndReturnToIdle()
        {
            pendingSkillFrame =
                default(FpgFormalEnemySkillSequenceFrame);
            hasPendingSkillFrame = false;
            activeSkillExecutionId = SkillExecutionId.Invalid;
            skillAnimationEvaluator?.Reset();
            if (presentationInitialized
                && boundBehavior != null
                && HasAnimation(boundBehavior.IdleAnimation))
            {
                if (boundBehavior.UsesAnimationRootMotion(
                        boundBehavior.IdleAnimation))
                {
                    returnToIdleOnNextMotionTick = true;
                }
                else
                {
                    StopTickDrivenAnimation();
                    TryPlayLoop(boundBehavior.IdleAnimation);
                }
            }
        }

        private bool TryPlayEntry(out string error)
        {
            if (!presentationInitialized || boundBehavior == null)
            {
                error = "Formal entry animation requires a bound presentation.";
                return false;
            }

            if (string.Equals(
                    boundBehavior.EntryAnimation,
                    boundBehavior.IdleAnimation,
                    StringComparison.Ordinal))
            {
                return TryStartIdle(TickIndex.Invalid, out error);
            }

            if (boundBehavior.UsesAnimationRootMotion(
                    boundBehavior.EntryAnimation))
            {
                return TryStartTickDrivenAnimation(
                    boundBehavior.EntryAnimation,
                    false,
                    TickDrivenAnimationKind.Entry,
                    TickIndex.Invalid,
                    default(FpgCompiledSkillSequence),
                    SkillExecutionId.Invalid,
                    out error);
            }

            return TryStartRenderDrivenEntry(
                boundBehavior.EntryAnimation,
                out error);
        }

        private bool TryAdvanceTickDrivenAnimation(
            TickIndex tick,
            out string error)
        {
            if (tickDrivenAnimationKind
                == TickDrivenAnimationKind.RenderDrivenEntry)
            {
                return TryAdvanceRenderDrivenEntry(tick, out error);
            }

            if (tickDrivenTrackEntry == null
                || rootMotionBridge == null
                || !rootMotionBridge.MotionEnabled
                || skeletonAnimation == null
                || skeletonAnimation.AnimationState == null
                || skeletonAnimation.AnimationState.GetCurrent(
                    MainAnimationTrack) != tickDrivenTrackEntry)
            {
                error =
                    "Tick-driven Spine root motion lost its active track.";
                return false;
            }

            long relativeTick = tick.Value - tickDrivenStartTick.Value;
            if (relativeTick < 0L || relativeTick > int.MaxValue)
            {
                error = "Tick-driven Spine root motion tick is out of range.";
                return false;
            }

            switch (tickDrivenAnimationKind)
            {
                case TickDrivenAnimationKind.Entry:
                {
                    double seconds = relativeTick
                        / (double)FpgSkillRuntimeConstants.TickRate;
                    float sampleTime = (float)Math.Min(
                        seconds,
                        tickDrivenAnimationDuration);
                    if (!TryEvaluateTickDrivenTime(sampleTime, out error))
                    {
                        return false;
                    }

                    lastMotionTick = tick;
                    if (seconds >= tickDrivenAnimationDuration)
                    {
                        return TryStartIdle(tick, out error);
                    }

                    return true;
                }

                case TickDrivenAnimationKind.Idle:
                {
                    float sampleTime = (float)(relativeTick
                        / (double)FpgSkillRuntimeConstants.TickRate);
                    if (!TryEvaluateTickDrivenTime(sampleTime, out error))
                    {
                        return false;
                    }

                    lastMotionTick = tick;
                    return true;
                }

                case TickDrivenAnimationKind.Skill:
                {
                    int skillTick = Math.Min(
                        (int)relativeTick,
                        tickDrivenSkillSequence.DurationTicks);
                    double seconds = FpgSkillAnimationTime.EvaluateSeconds(
                        tickDrivenSkillSequence,
                        skillTick,
                        0d,
                        tickDrivenAnimationDuration);
                    if (!TryEvaluateTickDrivenTime(
                            (float)seconds,
                            out error))
                    {
                        return false;
                    }

                    lastMotionTick = tick;
                    return true;
                }

                default:
                    error =
                        "Tick-driven Spine root motion has no playback kind.";
                    return false;
            }
        }

        private bool TryAdvanceRenderDrivenEntry(
            TickIndex tick,
            out string error)
        {
            if (tickDrivenTrackEntry == null
                || rootMotionBridge == null
                || rootMotionBridge.MotionEnabled
                || skeletonAnimation == null
                || skeletonAnimation.AnimationState == null
                || skeletonAnimation.AnimationState.GetCurrent(
                    MainAnimationTrack) != tickDrivenTrackEntry)
            {
                error =
                    "Render-driven Spine entry lost its active track.";
                return false;
            }

            long relativeTick = tick.Value - tickDrivenStartTick.Value;
            if (relativeTick < 0L || relativeTick > int.MaxValue)
            {
                error = "Render-driven Spine entry tick is out of range.";
                return false;
            }

            double seconds = relativeTick
                / (double)FpgSkillRuntimeConstants.TickRate;
            lastMotionTick = tick;
            if (seconds < tickDrivenAnimationDuration)
            {
                error = string.Empty;
                return true;
            }

            try
            {
                tickDrivenTrackEntry.TrackTime =
                    tickDrivenAnimationDuration;
                skeletonAnimation.Update(0f);
            }
            catch (Exception exception)
            {
                error = "Render-driven Spine entry '"
                    + tickDrivenAnimationName
                    + "' failed to sample its final frame: "
                    + exception.Message;
                return false;
            }

            return TryStartIdle(tick, out error);
        }

        private bool TryStartTickDrivenAnimation(
            string animationName,
            bool loop,
            TickDrivenAnimationKind kind,
            TickIndex startTick,
            FpgCompiledSkillSequence skillSequence,
            SkillExecutionId skillExecutionId,
            out string error)
        {
            if (kind == TickDrivenAnimationKind.None
                || rootMotionBridge == null
                || skeletonAnimation == null
                || skeletonAnimation.AnimationState == null
                || skeletonAnimation.Skeleton == null)
            {
                error =
                    "Tick-driven Spine root motion requires initialized presentation components.";
                return false;
            }

            Spine.Animation animation = skeletonAnimation.Skeleton.Data
                .FindAnimation(animationName);
            if (animation == null
                || float.IsNaN(animation.Duration)
                || float.IsInfinity(animation.Duration)
                || animation.Duration < 0f)
            {
                error = "Tick-driven Spine animation '" + animationName
                    + "' is unavailable or invalid.";
                return false;
            }

            if (kind == TickDrivenAnimationKind.Skill
                && (!skillSequence.IsValid || !skillExecutionId.IsValid))
            {
                error =
                    "Tick-driven skill animation requires a compiled sequence and execution.";
                return false;
            }

            try
            {
                StopTickDrivenAnimation();
                skeletonAnimation.AnimationState.ClearTrack(
                    MainAnimationTrack);
                skeletonAnimation.timeScale = 0f;
                rootMotionBridge.SetMotionEnabled(true);

                TrackEntry trackEntry =
                    skeletonAnimation.AnimationState.SetAnimation(
                        MainAnimationTrack,
                        animationName,
                        loop);
                trackEntry.MixDuration = 0f;

                tickDrivenAnimationKind = kind;
                tickDrivenTrackEntry = trackEntry;
                tickDrivenAnimationName = animationName;
                tickDrivenStartTick = startTick;
                lastMotionTick = startTick;
                tickDrivenAnimationDuration = animation.Duration;
                tickDrivenSkillSequence = skillSequence;
                tickDrivenSkillExecutionId = skillExecutionId;

                double initialSeconds = kind == TickDrivenAnimationKind.Skill
                    ? FpgSkillAnimationTime.EvaluateSeconds(
                        skillSequence,
                        0,
                        0d,
                        animation.Duration)
                    : 0d;
                trackEntry.TrackTime = (float)initialSeconds;
                skeletonAnimation.Update(0f);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                StopTickDrivenAnimation();
                error = "Tick-driven Spine animation '" + animationName
                    + "' failed to start: " + exception.Message;
                return false;
            }
        }

        private bool TryEvaluateTickDrivenTime(
            float trackTime,
            out string error)
        {
            if (tickDrivenTrackEntry == null
                || float.IsNaN(trackTime)
                || float.IsInfinity(trackTime)
                || trackTime < 0f)
            {
                error = "Tick-driven Spine animation time is invalid.";
                return false;
            }

            try
            {
                tickDrivenTrackEntry.TrackTime = trackTime;
                skeletonAnimation.Update(0f);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "Tick-driven Spine animation '"
                    + tickDrivenAnimationName + "' failed to evaluate: "
                    + exception.Message;
                return false;
            }
        }

        private bool TryStartRenderDrivenSkillAtTickZero(
            string animationName,
            FpgCompiledSkillSequence sequence,
            out string error)
        {
            Spine.Animation animation = skeletonAnimation.Skeleton.Data
                .FindAnimation(animationName);
            if (animation == null)
            {
                error = "Render-driven Spine skill animation '"
                    + animationName + "' is unavailable.";
                return false;
            }

            try
            {
                StopTickDrivenAnimation();
                skeletonAnimation.AnimationState.ClearTrack(
                    MainAnimationTrack);
                TrackEntry trackEntry =
                    skeletonAnimation.AnimationState.SetAnimation(
                        MainAnimationTrack,
                        animationName,
                        sequence.Loop);
                trackEntry.MixDuration = 0f;
                trackEntry.TrackTime = (float)
                    FpgSkillAnimationTime.EvaluateSeconds(
                        sequence,
                        0,
                        0d,
                        animation.Duration);
                skeletonAnimation.Update(0f);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "Render-driven Spine skill animation '"
                    + animationName + "' failed to start: "
                    + exception.Message;
                return false;
            }
        }

        private bool TryStartRenderDrivenEntry(
            string animationName,
            out string error)
        {
            Spine.Animation animation = skeletonAnimation.Skeleton.Data
                .FindAnimation(animationName);
            if (animation == null
                || float.IsNaN(animation.Duration)
                || float.IsInfinity(animation.Duration)
                || animation.Duration < 0f)
            {
                error = "Render-driven Spine entry animation '"
                    + animationName + "' is unavailable or invalid.";
                return false;
            }

            try
            {
                StopTickDrivenAnimation();
                skeletonAnimation.AnimationState.ClearTrack(
                    MainAnimationTrack);
                TrackEntry trackEntry =
                    skeletonAnimation.AnimationState.SetAnimation(
                        MainAnimationTrack,
                        animationName,
                        false);
                trackEntry.MixDuration = 0f;

                tickDrivenAnimationKind =
                    TickDrivenAnimationKind.RenderDrivenEntry;
                tickDrivenTrackEntry = trackEntry;
                tickDrivenAnimationName = animationName;
                tickDrivenStartTick = TickIndex.Invalid;
                lastMotionTick = TickIndex.Invalid;
                tickDrivenAnimationDuration = animation.Duration;
                skeletonAnimation.Update(0f);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                StopTickDrivenAnimation();
                error = "Render-driven Spine entry animation '"
                    + animationName + "' failed to start: "
                    + exception.Message;
                return false;
            }
        }

        private bool TryStartIdle(TickIndex startTick, out string error)
        {
            if (!presentationInitialized
                || boundBehavior == null
                || !HasAnimation(boundBehavior.IdleAnimation))
            {
                error = "Formal idle animation is unavailable.";
                return false;
            }

            if (boundBehavior.UsesAnimationRootMotion(
                    boundBehavior.IdleAnimation))
            {
                return TryStartTickDrivenAnimation(
                    boundBehavior.IdleAnimation,
                    true,
                    TickDrivenAnimationKind.Idle,
                    startTick,
                    default(FpgCompiledSkillSequence),
                    SkillExecutionId.Invalid,
                    out error);
            }

            StopTickDrivenAnimation();
            bool played = TryPlayLoop(boundBehavior.IdleAnimation);
            error = played
                ? string.Empty
                : "Formal idle Spine animation could not start.";
            return played;
        }

        private void StopTickDrivenAnimation()
        {
            rootMotionBridge?.SetMotionEnabled(false);
            if (skeletonAnimation != null)
            {
                skeletonAnimation.timeScale = authoredSkeletonTimeScale;
            }

            ResetTickDrivenAnimationState();
        }

        private void ResetTickDrivenAnimationState()
        {
            tickDrivenAnimationKind = TickDrivenAnimationKind.None;
            tickDrivenTrackEntry = null;
            tickDrivenAnimationName = string.Empty;
            tickDrivenStartTick = TickIndex.Invalid;
            lastMotionTick = TickIndex.Invalid;
            tickDrivenAnimationDuration = 0f;
            tickDrivenSkillSequence =
                default(FpgCompiledSkillSequence);
            tickDrivenSkillExecutionId = SkillExecutionId.Invalid;
            returnToIdleOnNextMotionTick = false;
        }

        private DomainResult RejectRootMotion(
            RejectReason reason,
            string error)
        {
            lastRootMotionError = error ?? string.Empty;
            return DomainResult.Rejected(reason);
        }

        private bool TryPlayLoop(string animationName)
        {
            if (!presentationInitialized
                || skeletonAnimation == null
                || skeletonAnimation.AnimationState == null)
            {
                return false;
            }

            try
            {
                TrackEntry trackEntry =
                    skeletonAnimation.AnimationState.SetAnimation(
                    MainAnimationTrack,
                    animationName,
                    true);
                trackEntry.MixDuration = 0f;
                skeletonAnimation.Update(0f);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ResetPresentation()
        {
            hitboxBoneFollowRuntime?.Dispose();
            hitboxBoneFollowRuntime = null;
            ClearSkillWarnings();
            StopTickDrivenAnimation();
            rootMotionBridge?.ResetForPool();
            if (skeletonAnimation != null && presentationInitialized)
            {
                try
                {
                    skeletonAnimation.ClearState();
                    skeletonAnimation.Initialize(false);
                    if (skeletonAnimation.Skeleton != null)
                    {
                        skeletonAnimation.Skeleton.SetToSetupPose();
                        skeletonAnimation.Skeleton.UpdateWorldTransform();
                    }
                }
                catch (Exception)
                {
                    // Presentation teardown must not block pool release.
                }
            }

            skillAnimationEvaluator?.Reset();
            skillAnimationEvaluator = null;
            activeSkillExecutionId = SkillExecutionId.Invalid;
            synchronouslyTerminatedSkillExecutionId =
                SkillExecutionId.Invalid;
            pendingSkillFrame =
                default(FpgFormalEnemySkillSequenceFrame);
            hasPendingSkillFrame = false;
            presentationInitialized = false;
            boundDefinition = null;
            boundBehavior = null;
            lastRootMotionError = string.Empty;
        }

        private bool TryCreateHitboxBoneFollowRuntime(
            out D0EnemyHitboxBoneFollowRuntime runtime,
            out string error)
        {
            int followCount = BoneFollowHitPartCount;
            if (followCount == 0)
            {
                runtime = null;
                error = string.Empty;
                return true;
            }

            var targets = new D0EnemyHitboxBoneFollowTarget[followCount];
            int targetIndex = 0;
            for (int hitPartOrdinal = 0;
                hitPartOrdinal < HitPartCount;
                hitPartOrdinal++)
            {
                if (!TryGetHitPartFollowSettings(
                        hitPartOrdinal,
                        out D0EnemyHitboxFollowSettings settings)
                    || settings.FollowMode
                        != D0EnemyHitboxFollowMode.SpineBone)
                {
                    continue;
                }

                if (!TryGetHitPart(
                        hitPartOrdinal,
                        out Collider collider,
                        out HitPart hitPart))
                {
                    runtime = null;
                    error = $"Formal enemy hit part {hitPartOrdinal} "
                        + "could not prepare bone following.";
                    return false;
                }

                Transform target = hitPart == HitPart.Weakpoint
                    ? WeakpointAnchor
                    : collider.transform;
                targets[targetIndex++] = new D0EnemyHitboxBoneFollowTarget(
                    target,
                    settings.BoneName,
                    settings.FollowBoneRotation,
                    settings.PositionOffset,
                    settings.RotationOffset);
            }

            return D0EnemyHitboxBoneFollowRuntime.TryCreate(
                skeletonAnimation,
                targets,
                out runtime,
                out error);
        }

        private bool TryValidateHitboxFollowSettings(
            D0EnemyHitboxFollowSettings settings,
            int hitPartOrdinal,
            ref SkeletonData skeletonData,
            ref bool skeletonDataLoaded,
            out string error)
        {
            if (settings.FollowMode
                == D0EnemyHitboxFollowMode.AuthoredTransform)
            {
                error = string.Empty;
                return true;
            }

            if (settings.FollowMode != D0EnemyHitboxFollowMode.SpineBone)
            {
                error = $"Formal enemy hit part {hitPartOrdinal} has an unsupported follow mode.";
                return false;
            }

            if (!settings.HasFiniteOffsets)
            {
                error = $"Formal enemy hit part {hitPartOrdinal} "
                    + "bone-follow offsets must be finite.";
                return false;
            }

            string boneName = settings.BoneName;
            if (string.IsNullOrWhiteSpace(boneName))
            {
                error = $"Formal enemy hit part {hitPartOrdinal} "
                    + "bone-follow mode requires a Spine bone name.";
                return false;
            }

            if (!string.Equals(
                    boneName,
                    boneName.Trim(),
                    StringComparison.Ordinal))
            {
                error = $"Formal enemy hit part {hitPartOrdinal} "
                    + "Spine bone name must not have surrounding whitespace.";
                return false;
            }

            if (skeletonAnimation == null
                || skeletonAnimation.SkeletonDataAsset == null)
            {
                error = $"Formal enemy hit part {hitPartOrdinal} "
                    + "bone-follow mode requires valid Spine skeleton data.";
                return false;
            }

            UpdateMode invisibleMode =
                skeletonAnimation.updateWhenInvisible;
            if (invisibleMode != UpdateMode.EverythingExceptMesh
                && invisibleMode != UpdateMode.FullUpdate)
            {
                error = $"Formal enemy hit part {hitPartOrdinal} "
                    + "bone following requires Spine to update bones "
                    + "while invisible.";
                return false;
            }

            if (!skeletonDataLoaded)
            {
                skeletonDataLoaded = true;
                try
                {
                    skeletonData = skeletonAnimation.SkeletonDataAsset
                        .GetSkeletonData(true);
                }
                catch (Exception exception)
                {
                    error = "Formal enemy hitbox bone validation failed: "
                        + exception.Message;
                    return false;
                }
            }

            if (skeletonData == null
                || skeletonData.FindBone(boneName) == null)
            {
                error = $"Formal enemy hit part {hitPartOrdinal} "
                    + $"Spine bone '{boneName}' does not exist.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool CanPresentSkillEvent(
            in FpgFormalEnemySkillTimelineEvent timelineEvent)
        {
            return presentationInitialized
                && gameplayEnabled
                && timelineEvent.OwnerRuntimeId.IsValid
                && string.Equals(
                    timelineEvent.OwnerRuntimeId.ToString(),
                    RuntimeId,
                    StringComparison.Ordinal)
                && timelineEvent.SpawnSequence == spawnSequence
                && timelineEvent.Definition != null
                && OwnsAttack(timelineEvent.Definition);
        }

        private bool TryValidatePresentationSocket(
            string socketName)
        {
            if (string.IsNullOrEmpty(socketName))
            {
                return true;
            }

            D0ActorSocketRegistry registry = SocketRegistry;
            return registry != null
                && registry.TryResolve(socketName, out _);
        }

        private void CaptureSkeletonBaseColor()
        {
            Spine.Skeleton skeleton = skeletonAnimation == null
                ? null
                : skeletonAnimation.Skeleton;
            if (skeleton == null)
            {
                hasSkeletonBaseColor = false;
                skeletonBaseColor = Color.white;
                return;
            }

            skeletonBaseColor = new Color(
                skeleton.R,
                skeleton.G,
                skeleton.B,
                skeleton.A);
            hasSkeletonBaseColor = true;
        }

        private void ApplySkillFeedbackColor()
        {
            Spine.Skeleton skeleton = skeletonAnimation == null
                ? null
                : skeletonAnimation.Skeleton;
            if (skeleton == null || !hasSkeletonBaseColor)
            {
                return;
            }

            Color color = skeletonBaseColor;
            if (activeSkillWarningCount > 0)
            {
                color.r *= skillWarningTint.r;
                color.g *= skillWarningTint.g;
                color.b *= skillWarningTint.b;
            }

            skeleton.R = color.r;
            skeleton.G = color.g;
            skeleton.B = color.b;
            skeleton.A = color.a;
        }

        private bool OwnsAttack(FpgEnemyAttackDefinition attack)
        {
            if (boundDefinition == null)
            {
                return false;
            }

            for (int index = 0;
                 index < boundDefinition.AttackPatternCount;
                 index++)
            {
                if (boundDefinition.GetAttackPattern(index) == attack)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryValidateAttackAnimations(
            SkeletonData data,
            FpgEnemyAttackDefinition attack)
        {
            if (data == null || attack == null)
            {
                return false;
            }

            IReadOnlyList<FpgSkillSequenceDefinition> sequences =
                attack.Sequences;
            for (int index = 0; index < sequences.Count; index++)
            {
                FpgSkillSequenceDefinition sequence = sequences[index];
                if (sequence == null
                    || sequence.Kind != FpgSkillSequenceKind.Execute)
                {
                    continue;
                }

                if (!HasAnimation(data, sequence.MainAnimation))
                {
                    return false;
                }

                for (int variantIndex = 0;
                    variantIndex < sequence.AlternateAnimations.Count;
                    variantIndex++)
                {
                    if (!HasAnimation(
                            data,
                            sequence.AlternateAnimations[variantIndex]))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
        private SkeletonData ResolveSkeletonData()
        {
            return skeletonAnimation == null
                || skeletonAnimation.SkeletonDataAsset == null
                    ? null
                    : skeletonAnimation.SkeletonDataAsset
                        .GetSkeletonData(true);
        }

        private FpgEntitySkeletonRootMotionBridge ResolveRootMotionBridge()
        {
            if (rootMotionBridge != null)
            {
                return rootMotionBridge;
            }

            rootMotionBridge = skeletonAnimation == null
                ? null
                : skeletonAnimation.GetComponent<
                    FpgEntitySkeletonRootMotionBridge>();
            return rootMotionBridge;
        }

        private bool HasAnimation(string animationName)
        {
            return HasAnimation(ResolveSkeletonData(), animationName);
        }

        private static bool HasAnimation(
            SkeletonData data,
            string animationName)
        {
            return data != null
                && !string.IsNullOrWhiteSpace(animationName)
                && data.FindAnimation(animationName) != null;
        }
    }
}
