using System;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using Spine.Unity;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Scene-facing D0 enemy motion bridge. It consumes only the already
    /// committed session tick and threat snapshots, then moves the visual actor
    /// and its gameplay anchor by the same authored offset. It never writes a
    /// combat command, chooses a target, turns, or navigates.
    /// </summary>
    [DefaultExecutionOrder(1050)]
    [DisallowMultipleComponent]
    public sealed class D0EnemyBehaviorController : MonoBehaviour, IBattleTickObserver
    {
        private const float TicksPerSecond = 60f;

        private enum BehaviorState
        {
            WaitingForSession = 0,
            Entering,
            Patrolling,
            HoldingForAttack,
            HoldingAfterAttack,
            DeathDelay,
            Exiting,
            Stopped
        }

        private BattleSessionHost sessionHost;
        private D0EnemyBehaviorProfile behaviorProfile;
        private D0EncounterDefinition encounter;
        private Transform visualRoot;
        private Transform gameplayAnchor;
        private SkeletonAnimation animationMotionSource;
        private D0LuanSummonHudieDefinition summonAnimationMotionSkill;

        private ThreatSnapshot[] threatBuffer;
        private RuntimeId[] consumedAnimationMotionThreats = Array.Empty<RuntimeId>();
        private D0EnemyBehaviorProfile activeBehaviorProfile;
        private int activeEnemyDefinitionId = 1;
        private BattleSession observedSession;
        private Vector3 visualBaseline;
        private Vector3 gameplayBaseline;
        private Vector3 programMotionOffset;
        private Vector3 programSkillMotionOffset;
        private Vector3 committedAnimationMotionOffset;
        private Vector3 activeAnimationMotionOffset;
        private Vector3 patrolTarget;
        private D0SpineMotionSampler animationMotionSampler;
        private D0AnimationMotionSettings activeAnimationMotionSettings;
        private RuntimeId activeAnimationMotionThreatId = RuntimeId.Invalid;
        private long activeAnimationMotionStartTick = -1L;
        private long lastProcessedTick = -1L;
        private float deathExitElapsedSeconds;
        private int lastResolvedThreatDefinitionId;
        private int consumedAnimationMotionThreatCount;
        private bool movingRight;
        private bool hasBaselines;
        private bool initialized;
        private bool subscribed;
        private bool summonStartAnimationMotionEvaluated;
        private bool appearanceAnimationMotionEvaluated;
        private BehaviorState state;

        public BattleSessionHost SessionHost => sessionHost;
        public D0EnemyBehaviorProfile BehaviorProfile => behaviorProfile;
        public D0EnemyBehaviorProfile ActiveBehaviorProfile => activeBehaviorProfile ?? behaviorProfile;
        public D0EncounterDefinition Encounter => encounter;
        public Transform VisualRoot => visualRoot;
        public Transform GameplayAnchor => gameplayAnchor;
        public SkeletonAnimation AnimationMotionSource => animationMotionSource;
        public D0LuanSummonHudieDefinition SummonAnimationMotionSkill =>
            summonAnimationMotionSkill;
        public bool IsInitialized => initialized;
        public bool IsHoldingForAttack => state == BehaviorState.HoldingForAttack
            || state == BehaviorState.HoldingAfterAttack;
        public bool IsPatrolling => state == BehaviorState.Patrolling;
        public string CurrentState => state.ToString();
        public int ActiveThreatDefinitionId { get; private set; }
        public Vector3 ProgramMotionOffset => programMotionOffset;
        public Vector3 ProgramSkillMotionOffset => programSkillMotionOffset;
        public Vector3 CommittedAnimationMotionOffset => committedAnimationMotionOffset;
        public Vector3 ActiveAnimationMotionOffset => activeAnimationMotionOffset;
        public Vector3 CombinedMotionOffset => ComposeMotionOffsets(
            programMotionOffset,
            programSkillMotionOffset,
            committedAnimationMotionOffset,
            activeAnimationMotionOffset);
        public bool HasActiveAnimationMotion => activeAnimationMotionStartTick >= 0L;

        /// <summary>
        /// Defines the exact threat states that stop patrol. Recovery remains a
        /// blocking state so the actor never resumes in the middle of a move's
        /// after-action pose.
        /// </summary>
        public static bool IsThreatBlockingPatrol(ThreatState threatState)
        {
            switch (threatState)
            {
                case ThreatState.Telegraph:
                case ThreatState.Windup:
                case ThreatState.ReleaseCommitted:
                case ThreatState.Recovery:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Tick-based movement helper used by runtime and EditMode tests. The
        /// fixed 60Hz conversion keeps a replay's patrol path independent of
        /// rendered-frame rate.
        /// </summary>
        public static Vector3 MoveOffsetForTicks(
            Vector3 current,
            Vector3 target,
            float unitsPerSecond,
            long tickDelta)
        {
            if (unitsPerSecond <= 0f || tickDelta <= 0L)
            {
                return current;
            }

            float distance = unitsPerSecond * tickDelta / TicksPerSecond;
            return Vector3.MoveTowards(current, target, distance);
        }

        public static Vector3 ComposeMotionOffsets(
            Vector3 programOffset,
            Vector3 programSkillOffset,
            Vector3 committedArtOffset,
            Vector3 activeArtOffset)
        {
            return programOffset
                + programSkillOffset
                + committedArtOffset
                + activeArtOffset;
        }

        public static float AnimationSecondsAtTick(long currentTick, long startTick)
        {
            if (currentTick < 0L || startTick < 0L || currentTick <= startTick)
            {
                return 0f;
            }

            return (currentTick - startTick) / TicksPerSecond;
        }

        /// <summary>
        /// Queues a program-authored skill offset without touching a Transform.
        /// The value is composed with behavior and animation motion on the next
        /// authoritative battle tick.
        /// </summary>
        public bool TrySetProgramSkillMotionOffset(Vector3 offset)
        {
            if (!IsFinite(offset))
            {
                return false;
            }

            programSkillMotionOffset = offset;
            return true;
        }

        private void Awake()
        {
            CaptureBaselines();
        }

        private void Start()
        {
            if (!TryInitialize(out string error))
            {
                Debug.LogError($"[{nameof(D0EnemyBehaviorController)}] {error}", this);
                return;
            }

            // The scene can finish bootstrapping the session before this
            // component receives its first Update. Apply the authored entry
            // pose here so the first visible D0 frame is truly an off-screen
            // Burstbug entry, rather than the legacy central spawn.
            BattleSession session = sessionHost == null ? null : sessionHost.Session;
            if (session != null && session.State != BattleSessionState.Disposed)
            {
                BeginSession(session);
                Physics.SyncTransforms();
            }
        }

        private void OnEnable()
        {
            SubscribeRestart();
        }

        private void OnDisable()
        {
            FinalizeActiveAnimationMotionOnInterruption();
            UnsubscribeRestart();
        }

        private void OnDestroy()
        {
            UnsubscribeRestart();
        }

        private void Update()
        {
            if (!initialized && !TryInitialize(out _))
            {
                return;
            }

            BattleSession session = sessionHost == null ? null : sessionHost.Session;
            if (session == null
                || session.State == BattleSessionState.Disposed
                || session.State == BattleSessionState.Faulted)
            {
                FinalizeActiveAnimationMotionOnInterruption();
                return;
            }

            if (session != observedSession)
            {
                BeginSession(session);
            }

            if (session.State == BattleSessionState.Paused)
            {
                return;
            }

            if (session.State == BattleSessionState.Completed)
            {
                FinalizeActiveAnimationMotionOnInterruption();
                AdvanceTerminalVisual(session);
            }
        }

        /// <summary>
        /// Synchronizes Burstbug's visual and hitbox anchors exactly once per
        /// simulation tick, immediately before spatial attack queries. This is
        /// the only running-session motion path; Update deliberately never
        /// changes gameplay transforms between ticks.
        /// </summary>
        public void BeforeBattleTick(BattleSession session, TickIndex tick)
        {
            if (!initialized && !TryInitialize(out _))
            {
                return;
            }

            if (session == null || session.State != BattleSessionState.Running)
            {
                return;
            }

            if (session != observedSession)
            {
                BeginSession(session);
            }

            ResolveActiveBehaviorProfile(session);
            long currentTick = tick.Value;
            long tickDelta = lastProcessedTick < 0L
                ? 0L
                : currentTick > lastProcessedTick
                    ? currentTick - lastProcessedTick
                    : 0L;
            lastProcessedTick = currentTick;
            bool threatActive = TryResolveActiveThreat(session, out int definitionId);
            ActiveThreatDefinitionId = definitionId;
            if (definitionId > 0)
            {
                lastResolvedThreatDefinitionId = definitionId;
            }

            FinalizeCanceledAttackAnimationMotion(session);
            TryStartConfiguredAnimationMotion(session, currentTick);
            AdvanceAnimationMotion(currentTick);
            AdvanceRunning(currentTick, tickDelta, threatActive);
            ApplyOffset();
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Resets presentation-side AI state when the domain replaces the
        /// active enemy runtime. A replacement must not inherit the egg's
        /// attack hold, animation-motion history or committed offset.
        /// </summary>
        public void NotifyEnemyRuntimeChanged(EnemyLifecycleChange change)
        {
            if (!initialized || behaviorProfile == null)
            {
                return;
            }

            activeEnemyDefinitionId = change.DefinitionId;
            activeBehaviorProfile = ResolveBehaviorProfile(change.DefinitionId);
            ClearActiveAnimationMotion(false);
            programSkillMotionOffset = Vector3.zero;
            committedAnimationMotionOffset = Vector3.zero;
            consumedAnimationMotionThreatCount = 0;
            summonStartAnimationMotionEvaluated = false;
            appearanceAnimationMotionEvaluated = false;
            ActiveThreatDefinitionId = 0;
            lastResolvedThreatDefinitionId = 0;
            lastProcessedTick = change.Tick.Value - 1L;
            deathExitElapsedSeconds = 0f;
            state = BehaviorState.WaitingForSession;

            // D0EnemyEntityWorld has already rebound the complete Hudie Entity
            // before this lifecycle callback. Start and sample its appearance
            // motion at the authored replacement tick; the first sample is zero
            // and preserves the inherited gameplay pose.
            TryStartSummonAnimationMotion(change.Tick.Value);
            AdvanceAnimationMotion(change.Tick.Value);
            ApplyOffset();
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Binds the motion bridge to the active prefab-owned enemy entity.
        /// The controller component may remain on the scene compatibility
        /// bridge, but its visual and gameplay transforms always belong to the
        /// current entity instance.
        /// </summary>
        public void NotifyEnemyEntityChanged(D0EnemyEntityView entity)
        {
            if (entity == null)
            {
                return;
            }

            FinalizeActiveAnimationMotionOnInterruption();
            visualRoot = entity.VisualRoot;
            gameplayAnchor = entity.GameplayAnchor;
            animationMotionSource = entity.SkeletonAnimation;
            animationMotionSampler = null;
            hasBaselines = false;
            CaptureBaselines();
        }

        public void Configure(
            BattleSessionHost nextSessionHost,
            D0EnemyBehaviorProfile nextProfile,
            D0EncounterDefinition nextEncounter,
            Transform nextVisualRoot,
            Transform nextGameplayAnchor)
        {
            Configure(
                nextSessionHost,
                nextProfile,
                nextEncounter,
                nextVisualRoot,
                nextGameplayAnchor,
                null,
                null);
        }

        public void Configure(
            BattleSessionHost nextSessionHost,
            D0EnemyBehaviorProfile nextProfile,
            D0EncounterDefinition nextEncounter,
            Transform nextVisualRoot,
            Transform nextGameplayAnchor,
            SkeletonAnimation nextAnimationMotionSource,
            D0LuanSummonHudieDefinition nextSummonAnimationMotionSkill)
        {
            UnsubscribeRestart();
            ResetMotionBeforeReconfigure();
            sessionHost = nextSessionHost;
            behaviorProfile = nextProfile;
            activeBehaviorProfile = null;
            activeEnemyDefinitionId = 1;
            encounter = nextEncounter;
            visualRoot = nextVisualRoot;
            gameplayAnchor = nextGameplayAnchor;
            animationMotionSource = nextAnimationMotionSource;
            summonAnimationMotionSkill = nextSummonAnimationMotionSkill;
            animationMotionSampler = null;
            initialized = false;
            observedSession = null;
            hasBaselines = false;
            CaptureBaselines();
            SubscribeRestart();
        }

        private void ResetMotionBeforeReconfigure()
        {
            ClearActiveAnimationMotion(false);
            programMotionOffset = Vector3.zero;
            programSkillMotionOffset = Vector3.zero;
            committedAnimationMotionOffset = Vector3.zero;
            consumedAnimationMotionThreatCount = 0;
            summonStartAnimationMotionEvaluated = false;
            appearanceAnimationMotionEvaluated = false;
            activeBehaviorProfile = null;
            activeEnemyDefinitionId = 1;
            lastProcessedTick = -1L;
            deathExitElapsedSeconds = 0f;
            ActiveThreatDefinitionId = 0;
            lastResolvedThreatDefinitionId = 0;
            state = BehaviorState.WaitingForSession;

            if (!hasBaselines)
            {
                return;
            }

            ApplyOffset();
            Physics.SyncTransforms();
        }

        public bool TryInitialize(out string error)
        {
            if (!TryValidate(out error))
            {
                return false;
            }

            if (!hasBaselines)
            {
                CaptureBaselines();
            }
            SubscribeRestart();
            activeBehaviorProfile = behaviorProfile;
            activeEnemyDefinitionId = 1;
            initialized = true;
            state = BehaviorState.WaitingForSession;
            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (sessionHost == null)
            {
                error = "D0 enemy behavior controller requires a BattleSessionHost.";
                return false;
            }

            if (behaviorProfile == null)
            {
                error = "D0 enemy behavior controller requires a behavior profile.";
                return false;
            }

            if (!behaviorProfile.TryValidate(out error))
            {
                return false;
            }

            if (encounter == null
                || encounter.Enemy == null
                || encounter.Enemy.BehaviorProfile != behaviorProfile
                || !encounter.UsesReusableAttackDefinitions)
            {
                error = "D0 enemy behavior controller requires the matching reusable-attack encounter definition.";
                return false;
            }

            if (visualRoot == null || gameplayAnchor == null)
            {
                error = "D0 enemy behavior controller requires visual and gameplay anchor transforms.";
                return false;
            }

            if (visualRoot == gameplayAnchor)
            {
                error = "D0 enemy behavior controller requires separate visual and gameplay anchors.";
                return false;
            }

            if (visualRoot.IsChildOf(gameplayAnchor)
                || gameplayAnchor.IsChildOf(visualRoot))
            {
                error = "D0 enemy behavior controller requires visual and gameplay anchors on independent transform branches.";
                return false;
            }

            if (!TryValidateAnimationMotionConfiguration(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void AdvanceRunning(long currentTick, long tickDelta, bool threatActive)
        {
            switch (state)
            {
                case BehaviorState.WaitingForSession:
                    ResetForSession(currentTick);
                    break;

                case BehaviorState.Entering:
                    MoveTowardsOffset(
                        MotionProfile.PatrolLeftOffset,
                        MotionProfile.EntrySpeed,
                        tickDelta);
                    if (ApproximatelyAt(MotionProfile.PatrolLeftOffset))
                    {
                        programMotionOffset = MotionProfile.PatrolLeftOffset;
                        movingRight = true;
                        patrolTarget = MotionProfile.PatrolRightOffset;
                        state = threatActive && MotionProfile.StopDuringThreat
                            ? BehaviorState.HoldingForAttack
                            : BehaviorState.Patrolling;
                    }

                    break;

                case BehaviorState.Patrolling:
                    if (threatActive && MotionProfile.StopDuringThreat)
                    {
                        state = BehaviorState.HoldingForAttack;
                        return;
                    }

                    AdvancePatrol(tickDelta);
                    break;

                case BehaviorState.HoldingForAttack:
                    if (!threatActive)
                    {
                        state = ShouldResumeAfterActiveThreat()
                            ? BehaviorState.Patrolling
                            : BehaviorState.HoldingAfterAttack;
                        lastResolvedThreatDefinitionId = 0;
                    }

                    break;

                case BehaviorState.HoldingAfterAttack:
                    // A deliberate designer-authored hold. A subsequent attack
                    // can still play, but patrol does not resume until restart.
                    break;
            }
        }

        private void AdvanceTerminalVisual(BattleSession session)
        {
            FinalSnapshot snapshot = session.GetFinalSnapshot();
            if (snapshot.CompletionReason != BattleCompletionReason.Victory)
            {
                state = BehaviorState.Stopped;
                return;
            }

            if (state != BehaviorState.DeathDelay && state != BehaviorState.Exiting)
            {
                state = BehaviorState.DeathDelay;
                deathExitElapsedSeconds = 0f;
                ActiveThreatDefinitionId = 0;
                return;
            }

            if (state == BehaviorState.DeathDelay)
            {
                deathExitElapsedSeconds += Time.unscaledDeltaTime;
                if (deathExitElapsedSeconds >= MotionProfile.DeathExitDelaySeconds)
                {
                    state = BehaviorState.Exiting;
                }

                return;
            }

            if (state == BehaviorState.Exiting)
            {
                programMotionOffset = Vector3.MoveTowards(
                    programMotionOffset,
                    MotionProfile.DeathExitOffset,
                    MotionProfile.DeathExitSpeed * Time.unscaledDeltaTime);
                ApplyOffset();
                if (ApproximatelyAt(MotionProfile.DeathExitOffset))
                {
                    state = BehaviorState.Stopped;
                }
            }
        }

        private void AdvancePatrol(long tickDelta)
        {
            MoveTowardsOffset(patrolTarget, MotionProfile.PatrolSpeed, tickDelta);
            if (!ApproximatelyAt(patrolTarget))
            {
                return;
            }

            programMotionOffset = patrolTarget;
            movingRight = !movingRight;
            patrolTarget = movingRight
                ? MotionProfile.PatrolRightOffset
                : MotionProfile.PatrolLeftOffset;
        }

        private void MoveTowardsOffset(Vector3 target, float unitsPerSecond, long tickDelta)
        {
            programMotionOffset = MoveOffsetForTicks(
                programMotionOffset,
                target,
                unitsPerSecond,
                tickDelta);
        }

        private bool TryResolveActiveThreat(BattleSession session, out int definitionId)
        {
            definitionId = 0;
            if (threatBuffer == null || threatBuffer.Length < session.ThreatCount)
            {
                return false;
            }

            if (!session.CopyThreatSnapshots(threatBuffer, out int count).IsSuccess)
            {
                return false;
            }

            for (int index = 0; index < count; index++)
            {
                ThreatSnapshot snapshot = threatBuffer[index];
                if (IsThreatBlockingPatrol(snapshot.State))
                {
                    definitionId = snapshot.DefinitionId;
                    return true;
                }
            }

            return false;
        }

        private bool TryValidateAnimationMotionConfiguration(out string error)
        {
            bool requiresSampler = false;
            if (summonAnimationMotionSkill != null)
            {
                D0AnimationMotionSettings summonSettings =
                    summonAnimationMotionSkill.SummonAnimationMotion;
                D0AnimationMotionSettings appearanceSettings =
                    summonAnimationMotionSkill.AppearanceAnimationMotion;
                if (!summonSettings.TryValidate(out error))
                {
                    error = "Luan summon animation motion is invalid: " + error;
                    return false;
                }

                if (!appearanceSettings.TryValidate(out error))
                {
                    error = "Hudie appearance animation motion is invalid: " + error;
                    return false;
                }

                requiresSampler |= summonSettings.Enabled
                    || appearanceSettings.Enabled;
            }

            for (int index = 0; index < encounter.AttackScheduleCount; index++)
            {
                D0EnemyAttackDefinition attack =
                    encounter.GetAttackScheduleEntry(index).Attack;
                if (attack == null)
                {
                    continue;
                }

                D0AnimationMotionSettings settings = attack.AnimationMotion;
                if (!settings.TryValidate(out error))
                {
                    error = $"Attack '{attack.AttackId}' animation motion is invalid: {error}";
                    return false;
                }

                requiresSampler |= settings.Enabled;
            }

            if (!requiresSampler)
            {
                animationMotionSampler = null;
                error = string.Empty;
                return true;
            }

            if (animationMotionSource == null)
            {
                error = "Enabled skill animation motion requires a SkeletonAnimation motion source.";
                return false;
            }

            animationMotionSampler = new D0SpineMotionSampler(animationMotionSource);
            if (summonAnimationMotionSkill != null)
            {
                if (!TryValidateSamplerSettings(
                        summonAnimationMotionSkill.SummonAnimationMotion,
                        "Luan summon",
                        out error))
                {
                    return false;
                }

                // Hudie's Entity becomes the active motion source only at the
                // lifecycle boundary. Validate its settings structurally now;
                // D0SpineMotionSampler validates the concrete Hudie skeleton
                // when that phase begins.
                if (!summonAnimationMotionSkill.AppearanceAnimationMotion.TryValidate(
                        out error))
                {
                    error = "Hudie appearance animation motion is invalid: " + error;
                    return false;
                }
            }

            for (int index = 0; index < encounter.AttackScheduleCount; index++)
            {
                D0EnemyAttackDefinition attack =
                    encounter.GetAttackScheduleEntry(index).Attack;
                if (attack != null
                    && !TryValidateSamplerSettings(
                        attack.AnimationMotion,
                        $"attack '{attack.AttackId}'",
                        out error))
                {
                    return false;
                }
            }

            animationMotionSampler.ClearConfiguration();
            error = string.Empty;
            return true;
        }

        private bool TryValidateSamplerSettings(
            D0AnimationMotionSettings settings,
            string owner,
            out string error)
        {
            if (!settings.Enabled)
            {
                error = string.Empty;
                return true;
            }

            if (animationMotionSampler.TryConfigure(settings, out error))
            {
                return true;
            }

            error = $"Could not configure {owner} animation motion: {error}";
            return false;
        }

        private void TryStartConfiguredAnimationMotion(
            BattleSession session,
            long currentTick)
        {
            TryStartSummonAnimationMotion(currentTick);
            TryStartAttackAnimationMotion(session, currentTick);
        }

        private void FinalizeCanceledAttackAnimationMotion(BattleSession session)
        {
            if (!HasActiveAnimationMotion
                || !activeAnimationMotionThreatId.IsValid
                || session == null)
            {
                return;
            }

            for (int index = 0; index < session.ThreatCount; index++)
            {
                if (TryFinalizeCanceledAttackAnimationMotion(
                        session.GetThreatSnapshot(index)))
                {
                    return;
                }
            }
        }

        private bool TryFinalizeCanceledAttackAnimationMotion(
            ThreatSnapshot snapshot)
        {
            if (!HasActiveAnimationMotion
                || !activeAnimationMotionThreatId.IsValid
                || snapshot.RuntimeId != activeAnimationMotionThreatId
                || snapshot.State != ThreatState.Canceled)
            {
                return false;
            }

            ClearActiveAnimationMotion(true);
            return true;
        }

        private void TryStartSummonAnimationMotion(long currentTick)
        {
            if (summonAnimationMotionSkill == null || encounter == null)
            {
                return;
            }

            D0EncounterSpawnSlot initialSlot = encounter.InitialSpawnSlot;
            if (initialSlot != null
                && activeEnemyDefinitionId == initialSlot.DefinitionId)
            {
                TryStartSummonPhaseAnimationMotion(
                    summonAnimationMotionSkill.SummonAnimationMotion,
                    summonAnimationMotionSkill.SummonTick,
                    ref summonStartAnimationMotionEvaluated,
                    "Luan summon",
                    currentTick);
                return;
            }

            if (encounter.SpawnSlotCount != 2)
            {
                return;
            }

            D0EncounterSpawnSlot hudieSlot = encounter.GetSpawnSlot(1);
            if (hudieSlot != null
                && activeEnemyDefinitionId == hudieSlot.DefinitionId)
            {
                TryStartSummonPhaseAnimationMotion(
                    summonAnimationMotionSkill.AppearanceAnimationMotion,
                    summonAnimationMotionSkill.AppearanceTick,
                    ref appearanceAnimationMotionEvaluated,
                    "Hudie appearance",
                    currentTick);
            }
        }

        private void TryStartSummonPhaseAnimationMotion(
            D0AnimationMotionSettings settings,
            long startTick,
            ref bool evaluated,
            string phaseName,
            long currentTick)
        {
            if (evaluated || currentTick < startTick)
            {
                return;
            }

            evaluated = true;
            if (!settings.Enabled)
            {
                return;
            }

            if (!TryBeginAnimationMotion(
                    settings,
                    startTick,
                    RuntimeId.Invalid,
                    out string error))
            {
                Debug.LogError(
                    $"[{nameof(D0EnemyBehaviorController)}] Could not start {phaseName} animation motion: {error}",
                    this);
            }
        }

        private void TryStartAttackAnimationMotion(
            BattleSession session,
            long currentTick)
        {
            if (session == null || encounter == null)
            {
                return;
            }

            for (int index = 0; index < session.ThreatCount; index++)
            {
                ThreatSnapshot snapshot = session.GetThreatSnapshot(index);
                if (!snapshot.RuntimeId.IsValid
                    || HasConsumedAnimationMotionThreat(snapshot.RuntimeId)
                    || !encounter.TryGetAttackDefinition(
                        snapshot.DefinitionId,
                        out D0EnemyAttackDefinition attack)
                    || !attack.AnimationMotion.Enabled
                    || !TryResolveAttackAnimationMotionStartTick(
                        snapshot,
                        attack,
                        currentTick,
                        out long startTick))
                {
                    continue;
                }

                MarkAnimationMotionThreatConsumed(snapshot.RuntimeId);
                if (!TryBeginAnimationMotion(
                        attack.AnimationMotion,
                        startTick,
                        snapshot.RuntimeId,
                        out string error))
                {
                    Debug.LogError(
                        $"[{nameof(D0EnemyBehaviorController)}] Could not start attack '{attack.AttackId}' animation motion: {error}",
                        this);
                }

                return;
            }
        }

        private static bool TryResolveAttackAnimationMotionStartTick(
            in ThreatSnapshot snapshot,
            D0EnemyAttackDefinition attack,
            long currentTick,
            out long startTick)
        {
            startTick = -1L;
            if (attack == null)
            {
                return false;
            }

            switch (attack.AnimationMotionStartPhase)
            {
                case D0AnimationMotionStartPhase.Windup:
                    if (snapshot.State == ThreatState.Windup
                        && snapshot.StateUntilTick.IsValid)
                    {
                        startTick = snapshot.StateUntilTick.Value
                            - attack.WindupTicks;
                    }
                    else if (snapshot.State == ThreatState.ReleaseCommitted)
                    {
                        startTick = currentTick - attack.WindupTicks;
                    }
                    else if (snapshot.State == ThreatState.Recovery
                             && snapshot.StateUntilTick.IsValid)
                    {
                        long releaseTick = snapshot.StateUntilTick.Value
                            - attack.RecoveryTicks;
                        startTick = releaseTick - attack.WindupTicks;
                    }

                    break;

                case D0AnimationMotionStartPhase.Release:
                    if (snapshot.State == ThreatState.Windup
                        && snapshot.StateUntilTick.IsValid
                        && currentTick >= snapshot.StateUntilTick.Value)
                    {
                        startTick = snapshot.StateUntilTick.Value;
                    }
                    else if (snapshot.State == ThreatState.ReleaseCommitted)
                    {
                        startTick = currentTick;
                    }
                    else if (snapshot.State == ThreatState.Recovery
                             && snapshot.StateUntilTick.IsValid)
                    {
                        startTick = snapshot.StateUntilTick.Value
                            - attack.RecoveryTicks;
                    }

                    break;
            }

            if (startTick < 0L)
            {
                return false;
            }

            startTick = Math.Max(0L, startTick);
            return currentTick >= startTick;
        }

        private bool TryBeginAnimationMotion(
            D0AnimationMotionSettings settings,
            long startTick,
            RuntimeId threatRuntimeId,
            out string error)
        {
            if (!settings.Enabled)
            {
                error = string.Empty;
                return true;
            }

            if (HasActiveAnimationMotion
                && activeAnimationMotionThreatId == threatRuntimeId)
            {
                error = string.Empty;
                return true;
            }

            ClearActiveAnimationMotion(true);
            if (animationMotionSource == null)
            {
                error = "Animation motion source is missing.";
                return false;
            }

            if (animationMotionSampler == null)
            {
                animationMotionSampler =
                    new D0SpineMotionSampler(animationMotionSource);
            }

            if (!animationMotionSampler.TryConfigure(settings, out error))
            {
                return false;
            }

            activeAnimationMotionSettings = settings;
            activeAnimationMotionThreatId = threatRuntimeId;
            activeAnimationMotionStartTick = Math.Max(0L, startTick);
            activeAnimationMotionOffset = Vector3.zero;
            error = string.Empty;
            return true;
        }

        private void AdvanceAnimationMotion(long currentTick)
        {
            if (!HasActiveAnimationMotion || animationMotionSampler == null)
            {
                return;
            }

            float seconds = AnimationSecondsAtTick(
                currentTick,
                activeAnimationMotionStartTick);
            if (!animationMotionSampler.TrySampleAbsoluteOffset(
                    seconds,
                    out Vector3 sampledOffset,
                    out string error))
            {
                Debug.LogError(
                    $"[{nameof(D0EnemyBehaviorController)}] Animation motion sampling failed: {error}",
                    this);
                ClearActiveAnimationMotion(true);
                return;
            }

            activeAnimationMotionOffset = sampledOffset;
            if (seconds >= animationMotionSampler.Duration)
            {
                ClearActiveAnimationMotion(true);
            }
        }

        private void ClearActiveAnimationMotion(bool commitCurrentOffset)
        {
            if (commitCurrentOffset
                && HasActiveAnimationMotion
                && activeAnimationMotionSettings.PersistEndOffset)
            {
                committedAnimationMotionOffset += activeAnimationMotionOffset;
            }

            activeAnimationMotionOffset = Vector3.zero;
            activeAnimationMotionSettings = default(D0AnimationMotionSettings);
            activeAnimationMotionThreatId = RuntimeId.Invalid;
            activeAnimationMotionStartTick = -1L;
            animationMotionSampler?.ClearConfiguration();
        }

        private void FinalizeActiveAnimationMotionOnInterruption()
        {
            if (!HasActiveAnimationMotion)
            {
                return;
            }

            ClearActiveAnimationMotion(true);
            if (!hasBaselines)
            {
                return;
            }

            ApplyOffset();
            Physics.SyncTransforms();
        }

        private bool HasConsumedAnimationMotionThreat(RuntimeId runtimeId)
        {
            for (int index = 0; index < consumedAnimationMotionThreatCount; index++)
            {
                if (consumedAnimationMotionThreats[index] == runtimeId)
                {
                    return true;
                }
            }

            return false;
        }

        private void MarkAnimationMotionThreatConsumed(RuntimeId runtimeId)
        {
            if (!runtimeId.IsValid
                || consumedAnimationMotionThreatCount
                    >= consumedAnimationMotionThreats.Length)
            {
                return;
            }

            consumedAnimationMotionThreats[consumedAnimationMotionThreatCount++] =
                runtimeId;
        }

        private void BeginSession(BattleSession session)
        {
            activeEnemyDefinitionId = session == null ? 1 : session.ActiveEnemyDefinitionId;
            activeBehaviorProfile = ResolveBehaviorProfile(activeEnemyDefinitionId);
            observedSession = session;
            int requiredThreatCapacity = session == null
                ? 0
                : Mathf.Max(session.ThreatCount, session.Definition.ThreatCapacity);
            if (requiredThreatCapacity > 0
                && (threatBuffer == null || threatBuffer.Length < requiredThreatCapacity))
            {
                threatBuffer = new ThreatSnapshot[requiredThreatCapacity];
            }

            int requiredAnimationMotionHistoryCapacity = Mathf.Max(
                requiredThreatCapacity,
                encounter == null ? 0 : encounter.AttackScheduleCount);
            if (requiredAnimationMotionHistoryCapacity > 0
                && consumedAnimationMotionThreats.Length
                    < requiredAnimationMotionHistoryCapacity)
            {
                consumedAnimationMotionThreats =
                    new RuntimeId[requiredAnimationMotionHistoryCapacity];
            }

            ResetForSession(session == null ? 0L : session.CurrentTick.Value);
        }

        private void ResetForSession(long currentTick)
        {
            programMotionOffset = MotionProfile.EntryOffset;
            programSkillMotionOffset = Vector3.zero;
            committedAnimationMotionOffset = Vector3.zero;
            ClearActiveAnimationMotion(false);
            consumedAnimationMotionThreatCount = 0;
            summonStartAnimationMotionEvaluated = false;
            appearanceAnimationMotionEvaluated = false;
            patrolTarget = MotionProfile.PatrolLeftOffset;
            movingRight = false;
            lastProcessedTick = currentTick;
            deathExitElapsedSeconds = 0f;
            ActiveThreatDefinitionId = 0;
            lastResolvedThreatDefinitionId = 0;
            state = BehaviorState.Entering;
            ApplyOffset();
        }

        private void OnSessionRestarted(BattleSessionHost host)
        {
            activeEnemyDefinitionId = 1;
            activeBehaviorProfile = behaviorProfile;
            observedSession = null;
            lastProcessedTick = -1L;
            ActiveThreatDefinitionId = 0;
            lastResolvedThreatDefinitionId = 0;

            // Restart is also the presentation reset point. Reapply the
            // entry offset synchronously so the visual actor and the spatial
            // hitboxes cannot spend one rendered frame at the prior patrol
            // position before the next authoritative tick arrives.
            if (MotionProfile != null && hasBaselines)
            {
                ResetForSession(0L);
                Physics.SyncTransforms();
                return;
            }

            state = BehaviorState.WaitingForSession;
        }

        private bool ShouldResumeAfterActiveThreat()
        {
            if (!MotionProfile.ResumePatrolAfterRecovery)
            {
                return false;
            }

            return !encounter.TryGetAttackDefinition(
                       lastResolvedThreatDefinitionId,
                       out D0EnemyAttackDefinition attack)
                || attack.RecoveryRule == D0AttackRecoveryRule.ResumePatrolAfterRecovery;
        }

        private void ResolveActiveBehaviorProfile(BattleSession session)
        {
            if (session == null)
            {
                return;
            }

            int definitionId = session.ActiveEnemyDefinitionId;
            if (definitionId == activeEnemyDefinitionId
                && activeBehaviorProfile != null)
            {
                return;
            }

            activeEnemyDefinitionId = definitionId;
            activeBehaviorProfile = ResolveBehaviorProfile(definitionId);
        }

        private D0EnemyBehaviorProfile ResolveBehaviorProfile(int definitionId)
        {
            if (encounter != null
                && encounter.TryGetSpawnSlot(
                    definitionId,
                    out D0EncounterSpawnSlot slot)
                && slot.Enemy != null
                && slot.Enemy.BehaviorProfile != null)
            {
                return slot.Enemy.BehaviorProfile;
            }

            return behaviorProfile;
        }

        private void CaptureBaselines()
        {
            if (visualRoot != null)
            {
                visualBaseline = visualRoot.position;
            }

            if (gameplayAnchor != null)
            {
                gameplayBaseline = gameplayAnchor.position;
            }

            hasBaselines = visualRoot != null && gameplayAnchor != null;
        }

        private void ApplyOffset()
        {
            Vector3 combinedOffset = CombinedMotionOffset;
            if (visualRoot != null)
            {
                visualRoot.position = visualBaseline + combinedOffset;
            }

            if (gameplayAnchor != null)
            {
                gameplayAnchor.position = gameplayBaseline + combinedOffset;
            }
        }

        private bool ApproximatelyAt(Vector3 target)
        {
            return (programMotionOffset - target).sqrMagnitude <= 0.000001f;
        }

        private static long SecondsToTick(float seconds)
        {
            return Math.Max(0L, (long)Math.Ceiling(seconds * TicksPerSecond));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void SubscribeRestart()
        {
            if (subscribed || sessionHost == null)
            {
                return;
            }

            sessionHost.SessionRestarted += OnSessionRestarted;
            subscribed = true;
        }

        private void UnsubscribeRestart()
        {
            if (!subscribed || sessionHost == null)
            {
                subscribed = false;
                return;
            }

            sessionHost.SessionRestarted -= OnSessionRestarted;
            subscribed = false;
        }

        private D0EnemyBehaviorProfile MotionProfile => activeBehaviorProfile ?? behaviorProfile;
    }
}
