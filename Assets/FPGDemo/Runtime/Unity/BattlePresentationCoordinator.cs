using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class BattlePresentationCoordinator : MonoBehaviour
    {
        private const int DefaultImpactLifetimeTicks = 12;
        private const int EnemyAttackPresentationCapacity = 16;
        // The player's capsule is deliberately kept visible in the shoulder
        // view. A normal 3.5 cm billboard nudge leaves player-hit flashes
        // inside that capsule, so player-targeted feedback gets a visual-only
        // surface lift. It affects neither collision nor combat truth.
        private const float PlayerDamageImpactCameraFacingOffset = 0.72f;

        private static readonly Color BodyImpactColor = new Color(1f, 0.36f, 0.12f, 0.9f);
        private static readonly Color WeakpointImpactColor = new Color(1f, 0.9f, 0.22f, 0.96f);
        private static readonly Color ProjectileImpactColor = new Color(0.4f, 0.88f, 1f, 0.94f);
        private static readonly Color TargetImpactColor = new Color(1f, 0.3f, 0.08f, 0.94f);
        private static readonly Color EnvironmentImpactColor = new Color(1f, 0.68f, 0.2f, 0.86f);
        private static readonly Color InterceptImpactColor = new Color(0.42f, 0.9f, 1f, 0.96f);
        private static readonly Color PerfectRetractColor = new Color(0.35f, 1f, 0.85f, 0.92f);
        private static readonly Color PlayerDamageColor = new Color(1f, 0.22f, 0.16f, 0.94f);
        private static readonly Color BarrierBrokenColor = new Color(0.45f, 0.72f, 1f, 0.95f);
        private static readonly Color BreakTriggeredColor = new Color(1f, 0.88f, 0.2f, 0.98f);
        private static readonly Color DeathColor = new Color(1f, 0.16f, 0.12f, 1f);

        [SerializeField]
        private BattleSessionHost sessionHost;

        private readonly CombatTraceCursor combatTraceCursor = new CombatTraceCursor();
        private readonly SelectedAttackHitCursor selectedAttackHitCursor = new SelectedAttackHitCursor();

        private ProjectileViewPool projectileViewPool;
        private WarningViewPool warningViewPool;
        private ImpactViewPool impactViewPool;
        private BattleHudPresenter hudPresenter;
        private D0HitTipPresenter d0HitTipPresenter;
        private ThreatTelegraph2DPresenter d0ThreatTelegraphPresenter;
        private CombatAudioPresenter d0CombatAudioPresenter;
        private CombatHud2DPresenter d0CombatHud2DPresenter;
        private readonly D0ActorPresentationRouter d0ActorPresentationRouter =
            new D0ActorPresentationRouter();
        private readonly D0LuanHudieSummonPresentationTimeline
            d0LuanHudieSummonTimeline =
                new D0LuanHudieSummonPresentationTimeline();
        private readonly PendingEnemyAttackPresentation[]
            pendingEnemyAttackPresentations =
                new PendingEnemyAttackPresentation[
                    EnemyAttackPresentationCapacity];
        private Camera presentationCamera;
        private BattleSession session;
        private IProjectilePresentationFeed projectileFeed;
        private ProjectilePresentationEvent[] eventBuffer;
        private ProjectilePresentationState[] stateBuffer;
        private ThreatSnapshot[] threatBuffer;
        private CombatEvent[] traceEventBuffer;
        private SelectedAttackHit[] selectedAttackHitBuffer;
        private D0AttackDamageAggregate[] d0AttackDamageAggregates;
        private int d0AttackDamageAggregateCount;
        private int d0ThreatSnapshotCount;
        private int pendingEnemyAttackPresentationCount;
        private long lastSequence;

        public BattleSessionHost SessionHost => sessionHost;
        public ProjectileViewPool ProjectileViewPool => projectileViewPool;
        public WarningViewPool WarningViewPool => warningViewPool;
        public ImpactViewPool ImpactViewPool => impactViewPool;
        public BattleHudPresenter Hud => hudPresenter;
        public BattleHudPresenter HudPresenter => hudPresenter;
        public D0HitTipPresenter D0HitTipPresenter => d0HitTipPresenter;
        public ThreatTelegraph2DPresenter D0ThreatTelegraphPresenter =>
            d0ThreatTelegraphPresenter;
        public CombatAudioPresenter D0CombatAudioPresenter => d0CombatAudioPresenter;
        public CombatHud2DPresenter D0CombatHud2DPresenter => d0CombatHud2DPresenter;
        public bool IsPrepared => projectileViewPool != null && projectileViewPool.IsPrepared;
        public bool IsFeedbackPrepared => IsPrepared
            && warningViewPool != null && warningViewPool.IsPrepared
            && impactViewPool != null && impactViewPool.IsPrepared
            && hudPresenter != null;
        public bool IsBound => session != null && projectileFeed != null;
        public int PresentationFaultCount { get; private set; }
        public int PresentationFeedGapCount { get; private set; }
        public int PresentationTraceGapCount => combatTraceCursor.GapCount;
        public int SelectedAttackFeedbackCount { get; private set; }
        public int CombatTraceFeedbackCount { get; private set; }
        public int DirectLuanSummonPresentationCount =>
            d0LuanHudieSummonTimeline.SummonConsumeCount;
        public int DirectHudieAppearancePresentationCount =>
            d0LuanHudieSummonTimeline.AppearanceConsumeCount;
        public int EnemyAttackPresentationCount { get; private set; }
        public int PendingEnemyAttackPresentationCount =>
            pendingEnemyAttackPresentationCount;

        /// <summary>
        /// Applies only the durable pause state to D0 Spine-backed views. This
        /// may be called from an Input System action callback before regular
        /// MonoBehaviour Update; it intentionally does not advance feeds, HUD,
        /// audio or any combat-owned state.
        /// </summary>
        public void SynchronizePauseState()
        {
            if (!IsBound)
            {
                return;
            }

            ApplyPresentationPauseState(session.State == BattleSessionState.Paused);
        }

        /// <summary>
        /// WP4-5B compatibility path. It prepares only projectile presentation;
        /// C feedback is intentionally opt-in through the full overload below.
        /// </summary>
        public bool TryPrepare(
            ScenarioDefinition definition,
            BattlePresentationCatalog catalog,
            Transform projectileViewRoot,
            out string error)
        {
            return TryPrepareInternal(
                definition,
                catalog,
                projectileViewRoot,
                null,
                null,
                null,
                false,
                out error);
        }

        /// <summary>
        /// WP4-5C preparation path. Projectile, warning, impact and HUD
        /// presentation are all prewarmed and validated before a session binds.
        /// </summary>
        public bool TryPrepare(
            ScenarioDefinition definition,
            BattlePresentationCatalog catalog,
            Transform projectileViewRoot,
            Transform warningViewRoot,
            Transform impactViewRoot,
            BattleHudPresenter nextHudPresenter,
            out string error)
        {
            return TryPrepareInternal(
                definition,
                catalog,
                projectileViewRoot,
                warningViewRoot,
                impactViewRoot,
                nextHudPresenter,
                true,
                out error);
        }

        public bool TryBind(
            BattleSession nextSession,
            IProjectilePresentationFeed nextFeed,
            out string error)
        {
            if (!IsPrepared || nextSession == null || nextFeed == null)
            {
                error = "Prepared projectile presentation pool, BattleSession and presentation feed are required.";
                return false;
            }

            if (nextFeed.ActiveCapacity > stateBuffer.Length
                || nextFeed.EventCapacity > eventBuffer.Length)
            {
                error = "Projectile presentation buffers do not match the supplied feed capacity.";
                return false;
            }

            if (IsFeedbackPrepared
                && !TryPrepareFeedbackReadBuffers(nextSession, out error))
            {
                return false;
            }

            UnbindAndClear();
            session = nextSession;
            projectileFeed = nextFeed;
            lastSequence = 0L;

            if (!d0ActorPresentationRouter.TryBind(
                    nextSession.PlayerRuntimeId,
                    nextSession.EnemyRuntimeId,
                    out error))
            {
                UnbindAndClear();
                return false;
            }

            if (d0ThreatTelegraphPresenter != null
                && !d0ThreatTelegraphPresenter.TryBind(
                    nextSession.PlayerRuntimeId,
                    nextSession.EnemyRuntimeId,
                    out error))
            {
                UnbindAndClear();
                return false;
            }

            if (!ResynchronizePersistentViews())
            {
                UnbindAndClear();
                error = "Battle presentation state could not be synchronized during bind.";
                return false;
            }

            if (IsFeedbackPrepared)
            {
                EstablishFeedbackCursorBaseline();
            }

            // Active projectile state was already synchronized directly. Keeping
            // the feed cursor at its head prevents a rebind from replaying old
            // spawn/terminal events as short-lived presentation.
            lastSequence = projectileFeed.LastSequence;
            error = string.Empty;
            return true;
        }

        public bool TryRebindEnemyRuntimeId(
            RuntimeId nextEnemyRuntimeId,
            out string error)
        {
            if (!IsBound || session == null)
            {
                error = "Battle presentation must be bound before enemy rebinding.";
                return false;
            }

            if (!nextEnemyRuntimeId.IsValid || nextEnemyRuntimeId == session.PlayerRuntimeId)
            {
                error = "Battle presentation requires a valid enemy RuntimeId distinct from the player.";
                return false;
            }

            ClearPendingEnemyAttackPresentations();
            BattleSceneContext context = sessionHost == null
                ? null
                : sessionHost.Context;
            Actor2DPresenter activeEnemyPresenter = context == null
                ? null
                : context.ActiveD0EnemyActorPresenter;
            D0EnemyDefinition activeEnemyDefinition = context == null
                || context.EnemyEntityWorld == null
                ? null
                : context.EnemyEntityWorld.ActiveEnemyDefinition;
            if (activeEnemyPresenter != null
                && activeEnemyDefinition != null)
            {
                if (!activeEnemyPresenter.TrySetRuntimePresentationOverride(
                        activeEnemyDefinition.ActorPresentation,
                        out error)
                    || !d0ActorPresentationRouter.TryReplaceEnemyActor(
                        activeEnemyPresenter,
                        nextEnemyRuntimeId,
                        out error))
                {
                    return false;
                }
            }
            else if (!d0ActorPresentationRouter.TryRebindEnemyRuntimeId(
                         nextEnemyRuntimeId,
                         out error))
            {
                return false;
            }

            if (d0ThreatTelegraphPresenter != null
                && !d0ThreatTelegraphPresenter.TryRebindEnemyRuntimeId(
                    nextEnemyRuntimeId,
                    out error))
            {
                return false;
            }

            AdvanceDirectLuanHudiePresentation();
            error = string.Empty;
            return true;
        }

        public void UnbindAndClear()
        {
            projectileViewPool?.ClearBindings();
            warningViewPool?.ClearBindings();
            impactViewPool?.Clear();
            hudPresenter?.Clear();
            d0HitTipPresenter?.Clear();
            d0ThreatTelegraphPresenter?.UnbindAndClear();
            d0CombatAudioPresenter?.ClearPresentation();
            d0CombatHud2DPresenter?.Clear();
            d0ActorPresentationRouter.ClearTransientState();
            ClearD0AttackDamageAggregates();
            d0ThreatSnapshotCount = 0;
            session = null;
            projectileFeed = null;
            lastSequence = 0L;
            combatTraceCursor.Reset();
            selectedAttackHitCursor.Reset();
            SelectedAttackFeedbackCount = 0;
            CombatTraceFeedbackCount = 0;
            d0LuanHudieSummonTimeline.Reset();
            ClearPendingEnemyAttackPresentations();
            EnemyAttackPresentationCount = 0;
        }

        public void DisposePresentation()
        {
            UnbindAndClear();
            projectileViewPool?.Dispose();
            warningViewPool?.Dispose();
            impactViewPool?.Dispose();
            projectileViewPool = null;
            warningViewPool = null;
            impactViewPool = null;
            hudPresenter = null;
            eventBuffer = null;
            stateBuffer = null;
            threatBuffer = null;
            traceEventBuffer = null;
            selectedAttackHitBuffer = null;
            d0AttackDamageAggregates = null;
            d0AttackDamageAggregateCount = 0;
            d0ThreatSnapshotCount = 0;
            d0HitTipPresenter = null;
            d0ThreatTelegraphPresenter = null;
            d0CombatAudioPresenter = null;
            d0CombatHud2DPresenter = null;
            d0ActorPresentationRouter.Reset();
            presentationCamera = null;
        }

        private bool TryPrepareInternal(
            ScenarioDefinition definition,
            BattlePresentationCatalog catalog,
            Transform projectileViewRoot,
            Transform warningViewRoot,
            Transform impactViewRoot,
            BattleHudPresenter nextHudPresenter,
            bool requiresFeedback,
            out string error)
        {
            if (sessionHost == null)
            {
                error = "BattlePresentationCoordinator must reference a BattleSessionHost.";
                return false;
            }

            if (definition == null || catalog == null || projectileViewRoot == null)
            {
                error = "ScenarioDefinition, BattlePresentationCatalog and projectile view root are required.";
                return false;
            }

            if (requiresFeedback
                && (warningViewRoot == null || impactViewRoot == null || nextHudPresenter == null))
            {
                error = "Warning root, impact root and BattleHudPresenter are required for C feedback presentation.";
                return false;
            }

            // All world-space presentation sprites share the authored gameplay
            // camera. This keeps projectile readability stable as the shoulder
            // camera orbits; it is not an optional feedback-only dependency.
            if (!TryResolveImpactBillboardCamera(out Camera presentationCamera, out error))
            {
                return false;
            }

            D0HitTipPresenter nextD0HitTipPresenter = TryResolveD0HitTipPresenter();
            if (nextD0HitTipPresenter != null)
            {
                if (!nextD0HitTipPresenter.TryValidate(out error))
                {
                    error = $"D0 hit-tip presentation is invalid: {error}";
                    return false;
                }

                // This call is made only from the runtime session host. The
                // editor installer binds fields but never prewarms UI children
                // into the serialized CombatLab scene.
                if (!nextD0HitTipPresenter.TryPrepare(out error))
                {
                    error = $"D0 hit-tip presentation could not prepare: {error}";
                    return false;
                }
            }

            if (!TryConfigureD0ActorPresentation(out error))
            {
                return false;
            }

            if (!TryConfigureD0G3Presentation(out error))
            {
                return false;
            }

            if (!TryConfigureD0G4Presentation(out error))
            {
                return false;
            }

            bool hadProjectilePreparation = IsPrepared;
            bool hadFeedbackPreparation = IsFeedbackPrepared;
            if (hadProjectilePreparation
                && projectileViewPool.Capacity < definition.ProjectileCapacity)
            {
                error = "Prepared projectile view pool is incompatible with scenario capacity.";
                return false;
            }

            ProjectileViewPool nextProjectilePool = null;
            WarningViewPool nextWarningPool = null;
            ImpactViewPool nextImpactPool = null;
            bool prepared = false;
            try
            {
                if (!hadProjectilePreparation)
                {
                    nextProjectilePool = new ProjectileViewPool();
                    if (!nextProjectilePool.TryPrepare(
                            definition,
                            catalog,
                            projectileViewRoot,
                            presentationCamera,
                            out error))
                    {
                        return false;
                    }
                }
                else if (!projectileViewPool.TryPrepare(
                             definition,
                             catalog,
                             projectileViewRoot,
                             presentationCamera,
                             out error))
                {
                    return false;
                }

                if (requiresFeedback)
                {
                    if (!nextHudPresenter.TryValidate(out error))
                    {
                        return false;
                    }

                    if (warningViewPool != null && warningViewPool.IsPrepared)
                    {
                        if (!warningViewPool.TryPrepare(
                                definition,
                                catalog,
                                warningViewRoot,
                                presentationCamera,
                                out error))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        nextWarningPool = new WarningViewPool();
                        if (!nextWarningPool.TryPrepare(
                                definition,
                                catalog,
                                warningViewRoot,
                                presentationCamera,
                                out error))
                        {
                            return false;
                        }
                    }

                    if (impactViewPool != null && impactViewPool.IsPrepared)
                    {
                        if (!impactViewPool.TryPrepare(
                                catalog,
                                impactViewRoot,
                                presentationCamera,
                                out error))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        nextImpactPool = new ImpactViewPool();
                        if (!nextImpactPool.TryPrepare(
                                catalog,
                                impactViewRoot,
                                presentationCamera,
                                out error))
                        {
                            return false;
                        }
                    }
                }

                EnsurePreparedBuffers(definition, requiresFeedback);

                if (nextProjectilePool != null)
                {
                    projectileViewPool = nextProjectilePool;
                    nextProjectilePool = null;
                }

                if (nextWarningPool != null)
                {
                    warningViewPool = nextWarningPool;
                    nextWarningPool = null;
                }

                if (nextImpactPool != null)
                {
                    impactViewPool = nextImpactPool;
                    nextImpactPool = null;
                }

                if (requiresFeedback)
                {
                    hudPresenter = nextHudPresenter;
                    hudPresenter.Clear();
                }

                this.presentationCamera = presentationCamera;
                d0HitTipPresenter = nextD0HitTipPresenter;

                if (!hadProjectilePreparation)
                {
                    PresentationFaultCount = 0;
                    PresentationFeedGapCount = 0;
                }

                if (requiresFeedback && !hadFeedbackPreparation)
                {
                    SelectedAttackFeedbackCount = 0;
                    CombatTraceFeedbackCount = 0;
                    combatTraceCursor.Reset();
                    selectedAttackHitCursor.Reset();
                }

                prepared = true;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Unable to prepare battle presentation: {exception.Message}";
                return false;
            }
            finally
            {
                if (!prepared)
                {
                    nextProjectilePool?.Dispose();
                    nextWarningPool?.Dispose();
                    nextImpactPool?.Dispose();
                    nextD0HitTipPresenter?.Clear();
                }
            }
        }

        private void EnsurePreparedBuffers(ScenarioDefinition definition, bool requiresFeedback)
        {
            int requiredEventCapacity = definition.ProjectileCapacity > int.MaxValue / 4
                ? int.MaxValue
                : Math.Max(64, definition.ProjectileCapacity * 4);
            if (eventBuffer == null || eventBuffer.Length < requiredEventCapacity)
            {
                eventBuffer = new ProjectilePresentationEvent[requiredEventCapacity];
            }

            if (stateBuffer == null || stateBuffer.Length < definition.ProjectileCapacity)
            {
                stateBuffer = new ProjectilePresentationState[definition.ProjectileCapacity];
            }

            if (requiresFeedback
                && (threatBuffer == null || threatBuffer.Length < definition.ThreatCapacity))
            {
                threatBuffer = new ThreatSnapshot[definition.ThreatCapacity];
            }
        }

        private bool TryPrepareFeedbackReadBuffers(BattleSession nextSession, out string error)
        {
            if (threatBuffer == null || threatBuffer.Length < nextSession.ThreatCount)
            {
                error = "Threat snapshot buffer does not match the supplied BattleSession capacity.";
                return false;
            }

            try
            {
                int traceCapacity = nextSession.Trace.Capacity;
                if (traceEventBuffer == null || traceEventBuffer.Length < traceCapacity)
                {
                    traceEventBuffer = new CombatEvent[traceCapacity];
                }

                int selectedHitCapacity = nextSession.SelectedAttackHits.Capacity;
                if (selectedAttackHitBuffer == null
                    || selectedAttackHitBuffer.Length < selectedHitCapacity)
                {
                    selectedAttackHitBuffer = new SelectedAttackHit[selectedHitCapacity];
                }

                if (d0AttackDamageAggregates == null
                    || d0AttackDamageAggregates.Length < selectedHitCapacity)
                {
                    d0AttackDamageAggregates = new D0AttackDamageAggregate[selectedHitCapacity];
                }
            }
            catch (Exception exception)
            {
                error = $"Unable to prepare C presentation read buffers: {exception.Message}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void LateUpdate()
        {
            if (!IsBound)
            {
                return;
            }

            try
            {
                bool paused = session.State == BattleSessionState.Paused;
                ApplyPresentationPauseState(paused);
                if (!paused && session.State == BattleSessionState.Running)
                {
                    AdvancePendingEnemyAttackPresentations();
                    AdvanceDirectLuanHudiePresentation();
                }
                if (d0CombatHud2DPresenter != null)
                {
                    d0CombatHud2DPresenter.Advance(
                        Mathf.Max(0f, Time.unscaledDeltaTime),
                        paused);
                    // The formal D0 HUD is a direct view over FinalSnapshot.
                    // It must remain live even when the optional legacy C
                    // feedback pools were not prepared (or are rebuilding
                    // after a trace gap). Threat data degrades to an empty
                    // list in that case; core combat readouts never freeze.
                    RefreshD0CombatHud();
                }

                if (IsFeedbackPrepared)
                {
                    impactViewPool.Advance(session.CurrentTick);
                    if (session.State == BattleSessionState.Running)
                    {
                        d0HitTipPresenter?.Advance(Mathf.Max(0f, Time.unscaledDeltaTime));
                    }

                    // Threat trace entries carry only a runtime id. Reconcile
                    // the current snapshots first so release transitions can
                    // recover the locked presentation key without changing
                    // the domain trace schema.
                    if (!ResynchronizeThreatWarnings())
                    {
                        throw new InvalidOperationException(
                            "Threat warning presentation could not be synchronized.");
                    }

                    ConsumeCombatTrace(out bool hasTraceGap);
                    if (hasTraceGap)
                    {
                        RecoverFromTraceGap();
                        PresentSelectedAttackFeedback();
                        return;
                    }
                }

                UpdateProjectilePresentation();

                if (IsFeedbackPrepared)
                {
                    PresentSelectedAttackFeedback();
                    RefreshLegacyHud();
                }
            }
            catch (Exception)
            {
                PresentationFaultCount++;
            }
        }

        private void ApplyPresentationPauseState(bool paused)
        {
            d0ActorPresentationRouter.SetPaused(paused);
        }

        private void ConsumeCombatTrace(out bool hasGap)
        {
            // Damage aggregates are a single presentation-frame bridge between
            // post-resolution trace events and the selected-hit feedback below.
            // Keeping them longer could pair a new spatial hit with an old
            // AttackId only after a restart/gap, so clear before each read.
            ClearD0AttackDamageAggregates();
            int eventCount = combatTraceCursor.CopyUnread(
                session.Trace,
                traceEventBuffer,
                out hasGap);
            if (hasGap)
            {
                return;
            }

            for (int index = 0; index < eventCount; index++)
            {
                // Snapshot polling owns persistent visuals. The trace is only a
                // bounded notification source here, so consuming it cannot alter
                // combat truth or make visual effects part of replay state.
                CombatEvent combatEvent = traceEventBuffer[index];
                RecordD0AppliedDamage(combatEvent);
                ConsumeD0G3Trace(combatEvent);
                d0CombatHud2DPresenter?.ConsumeCombatTrace(
                    combatEvent,
                    session.PlayerRuntimeId);
                d0ActorPresentationRouter.Consume(combatEvent);
                PresentCombatTraceFeedback(combatEvent);
                combatTraceCursor.Commit(combatEvent);
            }
        }

        private void ConsumeD0G3Trace(in CombatEvent combatEvent)
        {
            if (d0ThreatTelegraphPresenter != null
                && d0ThreatTelegraphPresenter.ConsumeTrace(
                    combatEvent,
                    out D0ThreatPresentationSignal threatSignal))
            {
                if (IsAttackReleaseCommand(threatSignal.Command)
                    && !TryScheduleEnemyAttackPresentation(threatSignal))
                {
                    PresentationFaultCount++;
                }
            }

            if (combatEvent.EventType == CombatEventType.Death
                && combatEvent.TargetId == session.EnemyRuntimeId)
            {
                TryPlayEnemyDeathStateVfx(ResolveD0EnemyEffectAnchor());
            }
        }

        private bool TryScheduleEnemyAttackPresentation(
            in D0ThreatPresentationSignal signal)
        {
            BattleSceneContext context = sessionHost == null
                ? null
                : sessionHost.Context;
            D0EncounterDefinition encounter = context == null
                || context.ScenarioConfig == null
                || context.ScenarioConfig.AuthoredScenario == null
                ? null
                : context.ScenarioConfig.AuthoredScenario.Encounter;
            D0EnemyEntityView entity = context == null
                || context.EnemyEntityWorld == null
                ? null
                : context.EnemyEntityWorld.ActiveEntity;
            if (encounter == null || entity == null
                || !encounter.TryGetAttackByPresentationKey(
                    signal.PresentationKey,
                    out D0EnemyAttackDefinition attack)
                || attack == null)
            {
                return false;
            }

            long currentTick = session == null ? 0L : session.CurrentTick.Value;
            if (attack.ReleaseMarkerTicks == 0)
            {
                return TryPresentEnemyAttack(entity, attack);
            }

            if (pendingEnemyAttackPresentationCount
                    >= pendingEnemyAttackPresentations.Length
                || currentTick > long.MaxValue - attack.ReleaseMarkerTicks)
            {
                return false;
            }

            pendingEnemyAttackPresentations[
                pendingEnemyAttackPresentationCount++] =
                    new PendingEnemyAttackPresentation(
                        entity,
                        attack,
                        currentTick + attack.ReleaseMarkerTicks);
            return true;
        }

        private void AdvancePendingEnemyAttackPresentations()
        {
            if (session == null || pendingEnemyAttackPresentationCount == 0)
            {
                return;
            }

            long currentTick = session.CurrentTick.Value;
            int index = 0;
            while (index < pendingEnemyAttackPresentationCount)
            {
                PendingEnemyAttackPresentation pending =
                    pendingEnemyAttackPresentations[index];
                if (pending.DueTick > currentTick)
                {
                    index++;
                    continue;
                }

                int lastIndex = --pendingEnemyAttackPresentationCount;
                pendingEnemyAttackPresentations[index] =
                    pendingEnemyAttackPresentations[lastIndex];
                pendingEnemyAttackPresentations[lastIndex] = default;
                if (!TryPresentEnemyAttack(pending.Entity, pending.Attack))
                {
                    PresentationFaultCount++;
                }
            }
        }

        private bool TryPresentEnemyAttack(
            D0EnemyEntityView entity,
            D0EnemyAttackDefinition attack)
        {
            BattleSceneContext context = sessionHost == null
                ? null
                : sessionHost.Context;
            D0CombatVfxWorld vfxWorld = context == null
                ? null
                : context.CombatVfxWorld;
            if (entity == null || attack == null
                || entity.ActorPresenter == null
                || !entity.TryResolveSocket(attack.SocketId, out Transform source)
                || source == null
                || vfxWorld == null || !vfxWorld.IsPrepared
                || d0CombatAudioPresenter == null)
            {
                return false;
            }

            bool animationPlayed =
                entity.ActorPresenter.PlayEnemyAttack(attack);
            bool vfxPlayed = vfxWorld.TryPresent(
                attack.EffectiveVisualEffectKey,
                source,
                out _);
            bool audioPlayed =
                d0CombatAudioPresenter.TryPlayPresentationCue(
                    attack.AudioCue);
            if (!animationPlayed || !vfxPlayed || !audioPlayed)
            {
                return false;
            }

            EnemyAttackPresentationCount++;
            return true;
        }

        private void ClearPendingEnemyAttackPresentations()
        {
            Array.Clear(
                pendingEnemyAttackPresentations,
                0,
                pendingEnemyAttackPresentations.Length);
            pendingEnemyAttackPresentationCount = 0;
        }

        private static bool IsAttackReleaseCommand(
            D0ThreatPresentationCommand command)
        {
            return command == D0ThreatPresentationCommand.ReleaseFast
                || command == D0ThreatPresentationCommand.ReleaseVolley
                || command == D0ThreatPresentationCommand.ReleaseHeavy;
        }

        private bool TryPlayEnemyDeathStateVfx(Transform source)
        {
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            D0EnemyDefinition enemy = context == null
                || context.EnemyEntityWorld == null
                ? null
                : context.EnemyEntityWorld.ActiveEnemyDefinition;
            D0ActorPresentationDefinition actorState =
                enemy == null ? null : enemy.ActorPresentation;
            D0CombatVfxWorld vfxWorld =
                context == null ? null : context.CombatVfxWorld;
            if (source == null || actorState == null
                || vfxWorld == null || !vfxWorld.IsPrepared)
            {
                return false;
            }

            D0EnemyEffectSlot[] slots =
            {
                D0EnemyEffectSlot.DeathLayerF4,
                D0EnemyEffectSlot.DeathLayerF3,
                D0EnemyEffectSlot.DeathLayerF2,
                D0EnemyEffectSlot.DeathLayerF1
            };
            bool played = false;
            for (int index = 0; index < slots.Length; index++)
            {
                string key = "actor."
                    + actorState.ActorId
                    + ".state."
                    + slots[index];
                played |= vfxWorld.TryAcquire(key, source, out _);
            }

            return played;
        }

        private Transform ResolveD0EnemyEffectAnchor()
        {
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            if (context == null && sessionHost != null)
            {
                context = FindUniqueContextInHostScene(sessionHost.gameObject.scene);
            }

            if (context != null && context.ActiveD0EnemyActorPresenter != null)
            {
                return context.ActiveD0EnemyActorPresenter.transform;
            }

            return context == null ? null : context.ActiveEnemyGameplayAnchor;
        }

        private void AdvanceDirectLuanHudiePresentation()
        {
            if (session == null || session.State != BattleSessionState.Running)
            {
                return;
            }

            BattleSceneContext context = sessionHost == null
                ? null
                : sessionHost.Context;
            D0CombatScenarioDefinition scenario = context == null
                || context.ScenarioConfig == null
                ? null
                : context.ScenarioConfig.AuthoredScenario;
            D0EnemyEntityView entity = context == null
                || context.EnemyEntityWorld == null
                ? null
                : context.EnemyEntityWorld.ActiveEntity;
            D0LuanSummonHudieDefinition summon = scenario == null
                ? null
                : scenario.LuanSummonHudie;
            if (scenario == null || summon == null || entity == null)
            {
                return;
            }

            long currentTick = session.CurrentTick.Value;
            int activeDefinitionId = session.ActiveEnemyDefinitionId;
            if (d0LuanHudieSummonTimeline.TryConsumeSummon(
                    scenario,
                    currentTick,
                    activeDefinitionId))
            {
                string idleAnimation = ResolveActiveEnemyIdleAnimation(
                    context,
                    string.Empty);
                PresentDirectLuanHudiePhase(
                    entity,
                    summon.SummonAnimation,
                    idleAnimation,
                    summon.SummonSocketId,
                    summon.SummonVfxKey,
                    summon.SummonAudioCue);
            }

            if (d0LuanHudieSummonTimeline.TryConsumeAppearance(
                    scenario,
                    currentTick,
                    activeDefinitionId))
            {
                string idleAnimation = ResolveActiveEnemyIdleAnimation(
                    context,
                    string.Empty);
                PresentDirectLuanHudiePhase(
                    entity,
                    summon.AppearanceAnimation,
                    idleAnimation,
                    summon.AppearanceSocketId,
                    summon.AppearanceVfxKey,
                    summon.AppearanceAudioCue);
            }
        }

        private void PresentDirectLuanHudiePhase(
            D0EnemyEntityView entity,
            string animationName,
            string idleAnimation,
            string socketId,
            string vfxKey,
            CombatAudioCue audioCue)
        {
            bool animationPlayed = TryPlayDirectEnemyOneShot(
                entity,
                animationName,
                idleAnimation);
            Transform source = null;
            bool socketResolved = entity != null
                && entity.TryResolveSocket(socketId, out source);
            BattleSceneContext context = sessionHost == null
                ? null
                : sessionHost.Context;
            D0CombatVfxWorld vfxWorld = context == null
                ? null
                : context.CombatVfxWorld;
            bool vfxPlayed = socketResolved
                && vfxWorld != null
                && vfxWorld.IsPrepared
                && vfxWorld.TryAcquire(vfxKey, source, out _);
            bool audioPlayed = d0CombatAudioPresenter != null
                && d0CombatAudioPresenter.TryPlayPresentationCue(audioCue);
            if (!animationPlayed || !socketResolved || !vfxPlayed || !audioPlayed)
            {
                PresentationFaultCount++;
            }
        }

        private static bool TryPlayDirectEnemyOneShot(
            D0EnemyEntityView entity,
            string animationName,
            string idleAnimation)
        {
            var skeleton = entity == null ? null : entity.SkeletonAnimation;
            if (skeleton == null
                || string.IsNullOrWhiteSpace(animationName)
                || string.IsNullOrWhiteSpace(idleAnimation))
            {
                return false;
            }

            skeleton.Initialize(false);
            if (skeleton.AnimationState == null
                || skeleton.SkeletonDataAsset == null)
            {
                return false;
            }

            var data = skeleton.SkeletonDataAsset.GetSkeletonData(true);
            if (data == null
                || data.FindAnimation(animationName) == null
                || data.FindAnimation(idleAnimation) == null)
            {
                return false;
            }

            skeleton.AnimationState.SetAnimation(0, animationName, false);
            skeleton.AnimationState.AddAnimation(0, idleAnimation, true, 0f);
            return true;
        }

        private static string ResolveActiveEnemyIdleAnimation(
            BattleSceneContext context,
            string fallback)
        {
            D0EnemyDefinition enemy = context == null
                || context.EnemyEntityWorld == null
                ? null
                : context.EnemyEntityWorld.ActiveEnemyDefinition;
            if (enemy != null
                && enemy.ActorPresentation != null
                && enemy.ActorPresentation.TryGetEnemy(
                    out EnemyActorPresentationDefinition presentation)
                && presentation != null
                && !string.IsNullOrWhiteSpace(presentation.IdleAnimation))
            {
                return presentation.IdleAnimation;
            }

            return fallback;
        }

        private void PresentCombatTraceFeedback(in CombatEvent combatEvent)
        {
            // Player-originated hits are represented by SelectedAttackHit at the
            // precise query point. Only show DamageApplied here when the player
            // was the victim, where that spatial stream has no corresponding hit.
            if (combatEvent.EventType == CombatEventType.DamageApplied
                && combatEvent.TargetId != session.PlayerRuntimeId)
            {
                return;
            }

            if (!TryGetCombatTraceFeedbackStyle(
                    combatEvent.EventType,
                    out Color color,
                    out float scale)
                || !TryResolveCombatantPosition(
                    combatEvent.TargetId.IsValid ? combatEvent.TargetId : combatEvent.SourceId,
                    out Vector3 position))
            {
                return;
            }

            float cameraFacingOffset = combatEvent.TargetId == session.PlayerRuntimeId
                ? PlayerDamageImpactCameraFacingOffset
                : ImpactView.DefaultCameraFacingOffset;

            if (impactViewPool.TrySpawn(
                    position,
                    color,
                    scale,
                    session.CurrentTick,
                    DefaultImpactLifetimeTicks,
                    cameraFacingOffset))
            {
                CombatTraceFeedbackCount++;
            }
        }

        private void RecoverFromTraceGap()
        {
            // A bounded trace lost ordering information. Rebuild only durable
            // state, clear active short feedback, and never synthesize effects
            // for trace entries that may no longer be retained. Selected hits
            // live in an independent non-overwriting stream and remain unread.
            impactViewPool.Clear();
            projectileViewPool.ClearBindings();
            warningViewPool.ClearBindings();
            d0HitTipPresenter?.Clear();
            d0ThreatTelegraphPresenter?.Clear();
            ClearPendingEnemyAttackPresentations();
            ClearD0AttackDamageAggregates();
            d0ActorPresentationRouter.Resynchronize(
                session.GetFinalSnapshot(),
                session.PlayerWeaponState);
            if (!ResynchronizePersistentViews())
            {
                throw new InvalidOperationException(
                    "Persistent presentation could not be rebuilt after a combat trace gap.");
            }

            lastSequence = projectileFeed.LastSequence;
            combatTraceCursor.ResolveGap(session.Trace);
        }

        private void UpdateProjectilePresentation()
        {
            int eventCount = projectileFeed.CopyEventsAfter(
                lastSequence,
                eventBuffer,
                out bool hasGap);
            if (hasGap)
            {
                PresentationFeedGapCount++;
                projectileViewPool.ClearBindings();
                if (!ResynchronizeActiveViews())
                {
                    throw new InvalidOperationException(
                        "Projectile presentation feed could not be synchronized after a gap.");
                }

                lastSequence = projectileFeed.LastSequence;
                return;
            }

            for (int index = 0; index < eventCount; index++)
            {
                ProjectilePresentationEvent item = eventBuffer[index];
                HandleProjectileEvent(item);
                lastSequence = item.Sequence;
            }

            if (!ResynchronizeActiveViews())
            {
                throw new InvalidOperationException(
                    "Projectile presentation feed could not be synchronized.");
            }
        }

        private void PresentProjectileTerminalFeedback(
            ProjectileTerminalReason terminalReason,
            Vector3 position,
            RuntimeId intendedTargetId)
        {
            if (!IsFeedbackPrepared
                || !TryGetProjectileTerminalFeedbackStyle(
                    terminalReason,
                    out Color color,
                    out float scale))
            {
                return;
            }

            bool targetsPlayer = intendedTargetId == session.PlayerRuntimeId;
            if (targetsPlayer)
            {
                // A sweep terminal point lies on the front of the player
                // capsule. Showing it from the shoulder camera would put the
                // sprite behind the avatar, so presentation intentionally
                // reuses the player feedback anchor instead of changing the
                // physics result or projectile transcript.
                position = ResolvePlayerCombatantPosition();
            }

            impactViewPool.TrySpawn(
                position,
                color,
                scale,
                session.CurrentTick,
                DefaultImpactLifetimeTicks,
                targetsPlayer
                    ? PlayerDamageImpactCameraFacingOffset
                    : ImpactView.DefaultCameraFacingOffset);
        }

        private void HandleProjectileEvent(ProjectilePresentationEvent item)
        {
            switch (item.Type)
            {
                case ProjectilePresentationEventType.Spawn:
                    if (projectileViewPool.TryAcquire(
                            item.State,
                            ToPosition(item.State.Path.PositionAtTick(session.CurrentTick)),
                            out ProjectileView spawnedView))
                    {
                        spawnedView.SetPosition(ToPosition(item.State.Path.PositionAtTick(session.CurrentTick)));
                    }
                    break;

                case ProjectilePresentationEventType.Terminal:
                {
                    Vector3 terminalPosition = ToPosition(item.State.LastPoint);
                    if (projectileViewPool.TryGet(item.State.Request.RuntimeId, out ProjectileView terminalView))
                    {
                        terminalView.SetPosition(terminalPosition);
                        terminalView.SetTerminalVisual(item.TerminalReason);
                        projectileViewPool.TryRelease(item.State.Request.RuntimeId);
                    }

                    PresentProjectileTerminalFeedback(
                        item.TerminalReason,
                        terminalPosition,
                        item.State.Request.TargetId);
                    break;
                }
            }
        }

        private void PresentSelectedAttackFeedback()
        {
            int hitCount = selectedAttackHitCursor.CopyUnread(
                session.SelectedAttackHits,
                selectedAttackHitBuffer);
            int index = 0;
            while (index < hitCount)
            {
                SelectedAttackHit first = selectedAttackHitBuffer[index];
                AttackId attackId = first.AttackId;
                SelectedAttackHit representative = default(SelectedAttackHit);
                int endExclusive = index;
                while (endExclusive < hitCount
                    && selectedAttackHitBuffer[endExclusive].AttackId == attackId)
                {
                    SelectedAttackHit candidate = selectedAttackHitBuffer[endExclusive];
                    if (candidate.IsValid
                        && (!representative.IsValid
                            || IsPreferredSelectedHit(candidate, representative)))
                    {
                        representative = candidate;
                    }

                    // Commit every selected pellet hit even though the D0 visual
                    // layer emits only one aggregate burst for this AttackId.
                    // This keeps the source stream one-shot and read-only.
                    selectedAttackHitCursor.CommitOne();
                    endExclusive++;
                }

                if (representative.IsValid)
                {
                    GetImpactStyle(
                        representative,
                        out Color color,
                        out float scale,
                        out CombatHitFeedbackShape feedbackShape);
                    if (impactViewPool.TrySpawn(
                            ToPosition(representative.ImpactPointKey),
                            color,
                            scale,
                            session.CurrentTick,
                            DefaultImpactLifetimeTicks,
                            ImpactView.DefaultCameraFacingOffset,
                            feedbackShape))
                    {
                        SelectedAttackFeedbackCount++;
                    }

                    d0ThreatTelegraphPresenter?.ConsumeSelectedHit(representative);
                    PresentD0HitTip(representative);
                }

                index = endExclusive;
            }
        }

        private void ConsumeSelectedAttackFeedbackWithoutPresentation()
        {
            int hitCount = selectedAttackHitCursor.CopyUnread(
                session.SelectedAttackHits,
                selectedAttackHitBuffer);
            for (int index = 0; index < hitCount; index++)
            {
                selectedAttackHitCursor.CommitOne();
            }
        }

        /// <summary>
        /// Records only already-applied player damage. SelectedAttackHit carries
        /// a location and hit category but deliberately has no damage amount or
        /// ImpactId, so deriving a number from weapon definition data would be
        /// false. The trace values are post-clamp health/projectile-HP values;
        /// grouping by attack and target preserves a single readable number for
        /// the representative visual without mixing two targets.
        /// </summary>
        private void RecordD0AppliedDamage(CombatEvent combatEvent)
        {
            if (d0HitTipPresenter == null
                || combatEvent.EventType != CombatEventType.DamageApplied
                || session == null
                || combatEvent.SourceId != session.PlayerRuntimeId
                || combatEvent.TargetId == session.PlayerRuntimeId
                || !combatEvent.AttackId.IsValid
                || !combatEvent.TargetId.IsValid)
            {
                return;
            }

            int appliedDamage = combatEvent.ValueBefore - combatEvent.ValueAfter;
            if (appliedDamage <= 0 || d0AttackDamageAggregates == null)
            {
                return;
            }

            for (int index = 0; index < d0AttackDamageAggregateCount; index++)
            {
                ref D0AttackDamageAggregate aggregate = ref d0AttackDamageAggregates[index];
                if (aggregate.AttackId != combatEvent.AttackId
                    || aggregate.TargetId != combatEvent.TargetId)
                {
                    continue;
                }

                aggregate.AddDamage(appliedDamage);
                return;
            }

            if (d0AttackDamageAggregateCount >= d0AttackDamageAggregates.Length)
            {
                // A missing number is preferable to a fabricated or mispaired
                // one. The D0 pool capacity is provisioned from the selected-hit
                // stream, so this is a defensive fail-closed path only.
                return;
            }

            d0AttackDamageAggregates[d0AttackDamageAggregateCount++] =
                new D0AttackDamageAggregate(
                    combatEvent.AttackId,
                    combatEvent.TargetId,
                    appliedDamage);
        }

        private void PresentD0HitTip(SelectedAttackHit representative)
        {
            if (d0HitTipPresenter == null
                || !d0HitTipPresenter.IsPrepared
                || presentationCamera == null
                || !TryGetD0AppliedDamage(
                    representative.AttackId,
                    representative.TargetId,
                    out int appliedDamage))
            {
                return;
            }

            Vector3 viewport = presentationCamera.WorldToViewportPoint(
                ToPosition(representative.ImpactPointKey));
            if (viewport.z <= 0f)
            {
                return;
            }

            d0HitTipPresenter.TryShow(
                GetD0HitTipKind(representative),
                appliedDamage,
                new Vector2(viewport.x, viewport.y));
        }

        private bool TryGetD0AppliedDamage(
            AttackId attackId,
            RuntimeId targetId,
            out int appliedDamage)
        {
            for (int index = 0; index < d0AttackDamageAggregateCount; index++)
            {
                D0AttackDamageAggregate aggregate = d0AttackDamageAggregates[index];
                if (aggregate.AttackId == attackId && aggregate.TargetId == targetId)
                {
                    appliedDamage = aggregate.AppliedDamage;
                    return appliedDamage > 0;
                }
            }

            appliedDamage = 0;
            return false;
        }

        private void ClearD0AttackDamageAggregates()
        {
            d0AttackDamageAggregateCount = 0;
        }

        private static CombatHitPresentationKind GetD0HitTipKind(in SelectedAttackHit hit)
        {
            if (hit.TargetKind == QueryTargetKind.Projectile)
            {
                return CombatHitPresentationKind.Intercept;
            }

            return hit.HitPart == HitPart.Weakpoint
                ? CombatHitPresentationKind.Weakpoint
                : CombatHitPresentationKind.Body;
        }

        private void EstablishFeedbackCursorBaseline()
        {
            // Binding reconstructs the current durable state rather than playing
            // the session's retained history. Commit the latest trace sequence
            // directly so a long-lived session does not turn initialization into
            // a synthetic trace gap, then consume existing selected-hit signals
            // without spawning impacts.
            if (session.Trace.Count > 0)
            {
                combatTraceCursor.Commit(session.Trace.GetOldest(session.Trace.Count - 1));
            }

            ConsumeSelectedAttackFeedbackWithoutPresentation();
        }

        private bool TryResolveImpactBillboardCamera(out Camera camera, out string error)
        {
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            if (context == null && sessionHost != null)
            {
                context = FindUniqueContextInHostScene(sessionHost.gameObject.scene);
            }

            if (context == null || context.MainCamera == null)
            {
                camera = null;
                error = "BattleSceneContext must provide MainCamera for world-space billboard presentation.";
                return false;
            }

            camera = context.MainCamera;
            error = string.Empty;
            return true;
        }

        private D0HitTipPresenter TryResolveD0HitTipPresenter()
        {
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            if (context == null && sessionHost != null)
            {
                context = FindUniqueContextInHostScene(sessionHost.gameObject.scene);
            }

            return context == null ? null : context.D0HitTipPresenter;
        }

        private bool TryConfigureD0ActorPresentation(out string error)
        {
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            if (context == null && sessionHost != null)
            {
                context = FindUniqueContextInHostScene(sessionHost.gameObject.scene);
            }

            Actor2DPresenter playerActor = context == null ? null : context.D0PlayerActorPresenter;
            Actor2DPresenter enemyActor = context == null ? null : context.ActiveD0EnemyActorPresenter;
            if (playerActor == null && enemyActor == null)
            {
                return d0ActorPresentationRouter.TryConfigure(null, null, out error);
            }

            if (playerActor == null || enemyActor == null)
            {
                return d0ActorPresentationRouter.TryConfigure(playerActor, enemyActor, out error);
            }

            D0ActorPresentationDefinition playerPresentation = null;
            D0ActorPresentationDefinition enemyPresentation = null;
            if (context.ScenarioConfig != null
                && !D0ScenarioPresentationResolver.TryResolve(
                    context.ScenarioConfig,
                    out playerPresentation,
                    out enemyPresentation,
                    out error))
            {
                return false;
            }

            if (!playerActor.TrySetRuntimePresentationOverride(playerPresentation, out error))
            {
                error = $"D0 player actor presentation is invalid: {error}";
                return false;
            }

            if (!enemyActor.TrySetRuntimePresentationOverride(enemyPresentation, out error))
            {
                error = $"D0 enemy actor presentation is invalid: {error}";
                return false;
            }

            return d0ActorPresentationRouter.TryConfigure(playerActor, enemyActor, out error);
        }

        private bool TryConfigureD0G3Presentation(out string error)
        {
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            if (context == null && sessionHost != null)
            {
                context = FindUniqueContextInHostScene(sessionHost.gameObject.scene);
            }

            ThreatTelegraph2DPresenter nextThreat = context == null
                ? null
                : context.D0ThreatTelegraphPresenter;
            CombatAudioPresenter nextAudio = context == null
                ? null
                : context.D0CombatAudioPresenter;
            if (nextThreat == null && nextAudio == null)
            {
                d0ThreatTelegraphPresenter = null;
                d0CombatAudioPresenter = null;
                error = string.Empty;
                return true;
            }

            if (nextThreat == null || nextAudio == null)
            {
                error = "D0 G3 presentation requires threat and audio presenters.";
                return false;
            }

            if (!nextThreat.TryPrepare(out error))
            {
                error = $"D0 threat presentation could not prepare: {error}";
                return false;
            }

            if (!nextAudio.TryPrepare(out error))
            {
                error = $"D0 audio presentation could not prepare: {error}";
                return false;
            }

            d0ThreatTelegraphPresenter = nextThreat;
            d0CombatAudioPresenter = nextAudio;
            error = string.Empty;
            return true;
        }

        private bool TryConfigureD0G4Presentation(out string error)
        {
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            if (context == null && sessionHost != null)
            {
                context = FindUniqueContextInHostScene(sessionHost.gameObject.scene);
            }

            CombatHud2DPresenter nextHud = context == null
                ? null
                : context.D0CombatHud2DPresenter;
            if (nextHud == null)
            {
                // Legacy and isolated presentation tests intentionally leave
                // the D0 formal HUD absent.
                d0CombatHud2DPresenter = null;
                error = string.Empty;
                return true;
            }

            if (!nextHud.TryPrepare(out error))
            {
                error = $"D0 combat HUD could not prepare: {error}";
                return false;
            }

            d0CombatHud2DPresenter = nextHud;
            error = string.Empty;
            return true;
        }

        private static BattleSceneContext FindUniqueContextInHostScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            BattleSceneContext found = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                BattleSceneContext[] candidates = roots[rootIndex]
                    .GetComponentsInChildren<BattleSceneContext>(true);
                for (int index = 0; index < candidates.Length; index++)
                {
                    BattleSceneContext candidate = candidates[index];
                    if (found != null && found != candidate)
                    {
                        return null;
                    }

                    found = candidate;
                }
            }

            return found;
        }

        private bool ResynchronizePersistentViews()
        {
            bool projectileSynchronized = ResynchronizeActiveViews();
            bool warningSynchronized = ResynchronizeThreatWarnings();
            RefreshLegacyHud();
            RefreshD0CombatHud();
            return projectileSynchronized && warningSynchronized;
        }

        private bool ResynchronizeActiveViews()
        {
            if (!IsBound)
            {
                return false;
            }

            int stateCount = projectileFeed.CopyActiveStates(stateBuffer);
            for (int index = 0; index < stateCount; index++)
            {
                ProjectilePresentationState state = stateBuffer[index];
                if (!projectileViewPool.TryGet(state.Request.RuntimeId, out ProjectileView view)
                    && !projectileViewPool.TryAcquire(
                        state,
                        ToPosition(state.Path.PositionAtTick(session.CurrentTick)),
                        out view))
                {
                    continue;
                }

                view.SetPosition(ToPosition(state.Path.PositionAtTick(session.CurrentTick)));
            }

            return true;
        }

        private bool ResynchronizeThreatWarnings()
        {
            if (!IsFeedbackPrepared)
            {
                return true;
            }

            if (threatBuffer == null)
            {
                return false;
            }

            DomainResult snapshotCopy = session.CopyThreatSnapshots(
                threatBuffer,
                out int threatCount);
            if (!snapshotCopy.IsSuccess)
            {
                return false;
            }

            d0ThreatSnapshotCount = threatCount;

            if (d0ThreatTelegraphPresenter != null)
            {
                d0ThreatTelegraphPresenter.Reconcile(
                    threatBuffer,
                    threatCount,
                    session.CurrentTick);
                d0ThreatTelegraphPresenter.Advance(
                    Mathf.Max(0f, Time.unscaledDeltaTime),
                    session.State == BattleSessionState.Running);

                // D0 owns threat readability with an enemy source pulse,
                // player danger pulse and weakpoint lock. Retaining the legacy
                // ground-circle pool here would duplicate and obscure that
                // language, while non-D0 scenes keep their unchanged path.
                warningViewPool.ClearBindings();
                return true;
            }

            warningViewPool.Reconcile(
                threatBuffer,
                threatCount,
                ResolvePlayerWarningPosition(),
                ResolveEnemyWeakpointWarningPosition());
            return true;
        }

        private void RefreshLegacyHud()
        {
            if (hudPresenter != null && session != null)
            {
                hudPresenter.Refresh(session.GetFinalSnapshot(), session.Definition);
            }
        }

        private void RefreshD0CombatHud()
        {
            if (d0CombatHud2DPresenter != null && session != null)
            {
                ThreatSnapshot[] snapshots = threatBuffer;
                int snapshotCount = snapshots == null
                    ? 0
                    : Mathf.Clamp(d0ThreatSnapshotCount, 0, snapshots.Length);
                d0CombatHud2DPresenter.Refresh(
                    session.GetFinalSnapshot(),
                    session.Definition,
                    snapshots,
                    snapshotCount,
                    session.CurrentTick);
            }
        }

        private Vector3 ResolvePlayerWarningPosition()
        {
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            if (context != null && context.PlayerGroundAnchor != null)
            {
                return context.PlayerGroundAnchor.position;
            }

            if (context != null && context.PlayerAnchor != null)
            {
                return context.PlayerAnchor.position;
            }

            return sessionHost != null ? sessionHost.transform.position : Vector3.zero;
        }

        private Vector3 ResolvePlayerCombatantPosition()
        {
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            if (context != null && context.PlayerAnchor != null)
            {
                return context.PlayerAnchor.position;
            }

            return sessionHost != null ? sessionHost.transform.position : Vector3.zero;
        }

        private Vector3 ResolveEnemyWeakpointWarningPosition()
        {
            // The active enemy prefab owns the warning anchor. This remains a
            // presentation read only; damage and target selection still come
            // exclusively from deterministic query contracts.
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            if (context != null && context.ActiveEnemyWeakpointAnchor != null)
            {
                return context.ActiveEnemyWeakpointAnchor.position;
            }

            if (context != null && context.ActiveEnemyGameplayAnchor != null)
            {
                return context.ActiveEnemyGameplayAnchor.position;
            }

            return sessionHost != null ? sessionHost.transform.position : Vector3.zero;
        }

        private void GetImpactStyle(
            in SelectedAttackHit hit,
            out Color color,
            out float scale,
            out CombatHitFeedbackShape feedbackShape)
        {
            CombatHitPresentationKind presentationKind;
            if (hit.TargetKind == QueryTargetKind.Projectile)
            {
                presentationKind = CombatHitPresentationKind.Intercept;
                color = ResolveHitColor(presentationKind, ProjectileImpactColor);
                scale = 0.68f;
                feedbackShape = ResolveHitShape(
                    presentationKind,
                    CombatHitFeedbackShape.Shatter);
                return;
            }

            if (hit.HitPart == HitPart.Weakpoint)
            {
                presentationKind = CombatHitPresentationKind.Weakpoint;
                color = ResolveHitColor(presentationKind, WeakpointImpactColor);
                scale = 1.15f;
                feedbackShape = ResolveHitShape(
                    presentationKind,
                    CombatHitFeedbackShape.Diamond);
                return;
            }

            presentationKind = CombatHitPresentationKind.Body;
            color = ResolveHitColor(presentationKind, BodyImpactColor);
            scale = 0.9f;
            feedbackShape = ResolveHitShape(
                presentationKind,
                CombatHitFeedbackShape.Burst);
        }

        private Color ResolveHitColor(
            CombatHitPresentationKind kind,
            Color fallback)
        {
            CombatPresentationProfile profile = ResolveD0PresentationProfile();
            return profile != null
                && profile.TryGetHitDefinition(kind, out CombatHitPresentationDefinition definition)
                ? definition.PrimaryColor
                : fallback;
        }

        private CombatHitFeedbackShape ResolveHitShape(
            CombatHitPresentationKind kind,
            CombatHitFeedbackShape fallback)
        {
            CombatPresentationProfile profile = ResolveD0PresentationProfile();
            return profile != null
                && profile.TryGetHitDefinition(kind, out CombatHitPresentationDefinition definition)
                ? definition.FeedbackShape
                : fallback;
        }

        private CombatPresentationProfile ResolveD0PresentationProfile()
        {
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            Actor2DPresenter playerPresenter = context == null
                ? null
                : context.D0PlayerActorPresenter;
            return playerPresenter == null ? null : playerPresenter.PresentationProfile;
        }

        private static bool IsPreferredSelectedHit(
            in SelectedAttackHit candidate,
            in SelectedAttackHit current)
        {
            int candidatePriority = GetSelectedHitPriority(candidate);
            int currentPriority = GetSelectedHitPriority(current);
            if (candidatePriority != currentPriority)
            {
                return candidatePriority > currentPriority;
            }

            return candidate.SampleIndex < current.SampleIndex;
        }

        private static int GetSelectedHitPriority(in SelectedAttackHit hit)
        {
            if (hit.HitPart == HitPart.Weakpoint)
            {
                return 3;
            }

            if (hit.TargetKind == QueryTargetKind.Projectile)
            {
                return 2;
            }

            return 1;
        }

        private static bool TryGetCombatTraceFeedbackStyle(
            CombatEventType eventType,
            out Color color,
            out float scale)
        {
            switch (eventType)
            {
                case CombatEventType.PerfectRetract:
                    color = PerfectRetractColor;
                    scale = 0.95f;
                    return true;

                case CombatEventType.DamageApplied:
                    color = PlayerDamageColor;
                    scale = 1.05f;
                    return true;

                case CombatEventType.BarrierBroken:
                    color = BarrierBrokenColor;
                    scale = 1.3f;
                    return true;

                case CombatEventType.BreakTriggered:
                    color = BreakTriggeredColor;
                    scale = 1.5f;
                    return true;

                case CombatEventType.GroggyStarted:
                    color = BreakTriggeredColor;
                    scale = 1.2f;
                    return true;

                case CombatEventType.Death:
                    color = DeathColor;
                    scale = 1.8f;
                    return true;

                // BattleCompleted is a session-level durable result. The HUD
                // reads FinalSnapshot every LateUpdate and owns that feedback.
                case CombatEventType.BattleCompleted:
                    color = default(Color);
                    scale = 0f;
                    return false;

                default:
                    color = default(Color);
                    scale = 0f;
                    return false;
            }
        }

        private static bool TryGetProjectileTerminalFeedbackStyle(
            ProjectileTerminalReason terminalReason,
            out Color color,
            out float scale)
        {
            switch (terminalReason)
            {
                case ProjectileTerminalReason.TargetImpact:
                    color = TargetImpactColor;
                    scale = 1f;
                    return true;

                case ProjectileTerminalReason.EnvironmentBlocked:
                    color = EnvironmentImpactColor;
                    scale = 0.82f;
                    return true;

                case ProjectileTerminalReason.Intercepted:
                    color = InterceptImpactColor;
                    scale = 0.9f;
                    return true;

                default:
                    color = default(Color);
                    scale = 0f;
                    return false;
            }
        }

        private bool TryResolveCombatantPosition(RuntimeId runtimeId, out Vector3 position)
        {
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            if (runtimeId == session.PlayerRuntimeId)
            {
                position = ResolvePlayerCombatantPosition();
                return true;
            }

            if (runtimeId == session.EnemyRuntimeId
                && context != null
                && context.ActiveEnemyGameplayAnchor != null)
            {
                position = context.ActiveEnemyGameplayAnchor.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private static Vector3 ToPosition(SpatialVectorKey key)
        {
            float inverseScale = 1f / SpatialContract.PositionUnitsPerMeter;
            return new Vector3(key.X * inverseScale, key.Y * inverseScale, key.Z * inverseScale);
        }

        private readonly struct PendingEnemyAttackPresentation
        {
            public PendingEnemyAttackPresentation(
                D0EnemyEntityView entity,
                D0EnemyAttackDefinition attack,
                long dueTick)
            {
                Entity = entity;
                Attack = attack;
                DueTick = dueTick;
            }

            public D0EnemyEntityView Entity { get; }
            public D0EnemyAttackDefinition Attack { get; }
            public long DueTick { get; }
        }

        private struct D0AttackDamageAggregate
        {
            public D0AttackDamageAggregate(
                AttackId attackId,
                RuntimeId targetId,
                int appliedDamage)
            {
                AttackId = attackId;
                TargetId = targetId;
                AppliedDamage = appliedDamage;
            }

            public AttackId AttackId;
            public RuntimeId TargetId;
            public int AppliedDamage;

            public void AddDamage(int amount)
            {
                if (amount <= 0 || AppliedDamage >= int.MaxValue)
                {
                    return;
                }

                AppliedDamage = amount > int.MaxValue - AppliedDamage
                    ? int.MaxValue
                    : AppliedDamage + amount;
            }
        }

        private void OnDestroy()
        {
            DisposePresentation();
        }
    }
}
