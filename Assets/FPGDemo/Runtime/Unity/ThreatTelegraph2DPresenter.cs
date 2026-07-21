using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using UnityEngine;
using UnityEngine.Rendering;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Fixed-capacity D0 threat presentation bridge. It receives copied threat
    /// snapshots and committed combat trace entries from a coordinator, then
    /// renders the three authored threat families. It owns no combat state,
    /// performs no Physics queries, and never changes an anchor transform.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThreatTelegraph2DPresenter : MonoBehaviour
    {
        [SerializeField]
        private CombatPresentationProfile presentationProfile;

        [SerializeField]
        private Material effectMaterial;

        [SerializeField]
        private Camera presentationCamera;

        private Transform enemySourceAnchor;

        private Transform playerDangerAnchor;

        private Transform enemyWeakpointAnchor;

        [SerializeField]
        private Transform telegraphRoot;

        private Actor2DPresenter enemyActorPresenter;

        [SerializeField]
        private D0WeakpointPresentationController weakpointPresentation;

        private D0EncounterDefinition encounterDefinition;

        [SerializeField]
        private CombatAudioPresenter audioPresenter;

        [SerializeField, Min(1)]
        private int prewarmCapacity = 8;

        private ThreatTelegraph2DView[] views;
        private RuntimeId[] activeRuntimeIds;
        private int[] activePresentationKeys;
        private bool[] activeSlots;
        private bool[] persistentSlots;
        private RuntimeId[] cachedRuntimeIds;
        private int[] cachedPresentationKeys;
        private int cachedMetadataCount;
        private RuntimeId playerRuntimeId = RuntimeId.Invalid;
        private RuntimeId enemyRuntimeId = RuntimeId.Invalid;
        private Vector3 lastEnemySourcePosition;
        private Vector3 lastPlayerDangerPosition;
        private Vector3 lastWeakpointPosition;
        private bool prepared;
        private bool bound;

        public bool IsPrepared => prepared;
        public bool IsBound => bound;
        public int Capacity => views == null ? 0 : views.Length;
        public int PoolRejectCount { get; private set; }
        public int UnknownPresentationKeyCount { get; private set; }
        public int CachedMetadataCount => cachedMetadataCount;
        public Vector3 LastEnemySourcePosition => lastEnemySourcePosition;
        public Vector3 LastPlayerDangerPosition => lastPlayerDangerPosition;
        public Vector3 LastWeakpointPosition => lastWeakpointPosition;
        public CombatAudioPresenter AudioPresenter => audioPresenter;
        public D0EncounterDefinition EncounterDefinition => encounterDefinition;

        public int ActiveTelegraphCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < Capacity; index++)
                {
                    if (activeSlots[index] && !views[index].IsReleasing)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int ActiveReleaseCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < Capacity; index++)
                {
                    if (activeSlots[index] && views[index].IsReleasing)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Explicit composition seam for the D0 installer and scene-free tests.
        /// Passing zero uses the profile's fixed threat-telegraph capacity.
        /// </summary>
        public void Configure(
            CombatPresentationProfile profile,
            Material material,
            Camera camera,
            Transform enemyAnchor,
            Transform playerAnchor,
            Transform weakpointAnchor,
            Transform nextTelegraphRoot = null,
            Actor2DPresenter enemyPresenter = null,
            D0WeakpointPresentationController nextWeakpointPresentation = null,
            int capacity = 0)
        {
            presentationProfile = profile;
            effectMaterial = material;
            presentationCamera = camera;
            enemySourceAnchor = enemyAnchor;
            playerDangerAnchor = playerAnchor;
            enemyWeakpointAnchor = weakpointAnchor;
            telegraphRoot = nextTelegraphRoot == null ? transform : nextTelegraphRoot;
            enemyActorPresenter = enemyPresenter;
            weakpointPresentation = nextWeakpointPresentation;
            if (audioPresenter != null && weakpointPresentation != null)
            {
                weakpointPresentation.SetAudioPresenter(audioPresenter);
            }
            if (capacity > 0)
            {
                prewarmCapacity = capacity;
            }
            else if (profile != null && profile.PoolCapacities != null)
            {
                prewarmCapacity = profile.PoolCapacities.ThreatTelegraphCapacity;
            }
        }

        /// <summary>
        /// Rebinds presentation anchors to the active prefab-owned enemy entity.
        /// </summary>
        public void RebindEnemyEntity(
            Transform nextEnemySourceAnchor,
            Transform nextEnemyWeakpointAnchor,
            Actor2DPresenter nextEnemyActorPresenter,
            D0EncounterDefinition nextEncounterDefinition = null)
        {
            enemySourceAnchor = nextEnemySourceAnchor;
            enemyWeakpointAnchor = nextEnemyWeakpointAnchor;
            enemyActorPresenter = nextEnemyActorPresenter;
            encounterDefinition = nextEncounterDefinition;
        }

        public void RebindPlayerEntity(Transform nextPlayerDangerAnchor)
        {
            playerDangerAnchor = nextPlayerDangerAnchor;
        }

        /// <summary>
        /// Keeps the owning telegraph and its nested weakpoint view on the same
        /// presentation-only audio bridge. No BattleSession or combat command
        /// enters this composition path.
        /// </summary>
        public void SetAudioPresenter(CombatAudioPresenter nextAudioPresenter)
        {
            audioPresenter = nextAudioPresenter;
            if (weakpointPresentation != null)
            {
                weakpointPresentation.SetAudioPresenter(nextAudioPresenter);
            }
        }

        public bool TryValidateAuthoring(out string error)
        {
            error = string.Empty;
            if (presentationProfile == null
                || !presentationProfile.TryValidateStatic(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Threat telegraph presentation requires a valid CombatPresentationProfile.";
                }

                return false;
            }

            if (effectMaterial == null)
            {
                error = "Threat telegraph presentation requires an effect material.";
                return false;
            }

            if (presentationCamera == null)
            {
                error = "Threat telegraph presentation requires a camera.";
                return false;
            }

            if (prewarmCapacity <= 0)
            {
                error = "Threat telegraph presentation requires a positive prewarm capacity.";
                return false;
            }

            Transform root = telegraphRoot == null ? transform : telegraphRoot;
            if (root.GetComponentsInChildren<Collider>(true).Length > 0
                || root.GetComponentsInChildren<Collider2D>(true).Length > 0
                || root.GetComponentsInChildren<Rigidbody>(true).Length > 0
                || root.GetComponentsInChildren<Rigidbody2D>(true).Length > 0)
            {
                error = "Threat telegraph presentation must not contain Collider or Rigidbody components.";
                return false;
            }

            if (weakpointPresentation != null
                && weakpointPresentation.IsPrepared
                && weakpointPresentation.transform == transform)
            {
                error = "Threat telegraph and weakpoint presentation must use distinct component roots.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (!TryValidateAuthoring(out error))
            {
                return false;
            }

            if (enemySourceAnchor == null
                || playerDangerAnchor == null
                || enemyWeakpointAnchor == null
                || enemyActorPresenter == null
                || encounterDefinition == null)
            {
                error =
                    "Threat telegraph requires runtime-bound player/enemy Entity anchors, presenter and Encounter.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryPrepare(out string error)
        {
            if (!TryValidate(out error))
            {
                return false;
            }

            if (enemyActorPresenter != null)
            {
                if (encounterDefinition == null)
                {
                    error =
                        "Threat telegraph requires the active Encounter definition when an enemy presenter is bound.";
                    return false;
                }

                for (int index = 0;
                     index < encounterDefinition.AttackScheduleCount;
                     index++)
                {
                    D0EnemyAttackDefinition attack =
                        encounterDefinition
                            .GetAttackScheduleEntry(index)
                            .Attack;
                    if (!enemyActorPresenter.TryValidateEnemyAttack(
                            attack,
                            out error))
                    {
                        return false;
                    }
                }
            }

            Transform root = telegraphRoot == null ? transform : telegraphRoot;
            if (prepared)
            {
                if (Capacity < prewarmCapacity)
                {
                    error = "Prepared threat telegraph pool is below the requested fixed capacity.";
                    return false;
                }

                ApplySorting();
                if (weakpointPresentation != null
                    && !weakpointPresentation.IsPrepared
                    && !weakpointPresentation.TryPrepare(out error))
                {
                    return false;
                }

                error = string.Empty;
                return true;
            }

            views = new ThreatTelegraph2DView[prewarmCapacity];
            activeRuntimeIds = new RuntimeId[prewarmCapacity];
            activePresentationKeys = new int[prewarmCapacity];
            activeSlots = new bool[prewarmCapacity];
            persistentSlots = new bool[prewarmCapacity];
            cachedRuntimeIds = new RuntimeId[prewarmCapacity];
            cachedPresentationKeys = new int[prewarmCapacity];
            try
            {
                for (int index = 0; index < prewarmCapacity; index++)
                {
                    GameObject viewObject = new GameObject("D0ThreatTelegraph_" + index);
                    viewObject.transform.SetParent(root, false);
                    ThreatTelegraph2DView view = viewObject.AddComponent<ThreatTelegraph2DView>();
                    if (!view.TryPrepare(
                            effectMaterial,
                            presentationCamera,
                            presentationProfile.Sorting.SortingLayerName,
                            presentationProfile.Sorting.WorldEffectsOrder,
                            out string viewError))
                    {
                        DestroyObject(viewObject);
                        DisposeViews();
                        error = "Unable to prepare D0 threat telegraph view: " + viewError;
                        return false;
                    }

                    views[index] = view;
                }
            }
            catch (Exception exception)
            {
                DisposeViews();
                error = "Unable to prewarm D0 threat telegraph views: " + exception.Message;
                return false;
            }

            prepared = true;
            if (weakpointPresentation != null
                && !weakpointPresentation.IsPrepared
                && !weakpointPresentation.TryPrepare(out error))
            {
                DisposeViews();
                prepared = false;
                return false;
            }

            Clear();
            error = string.Empty;
            return true;
        }

        public bool TryBind(
            RuntimeId nextPlayerRuntimeId,
            RuntimeId nextEnemyRuntimeId,
            out string error)
        {
            if (!prepared)
            {
                error = "Threat telegraph presentation must be prepared before binding.";
                return false;
            }

            if (!nextPlayerRuntimeId.IsValid
                || !nextEnemyRuntimeId.IsValid
                || nextPlayerRuntimeId == nextEnemyRuntimeId)
            {
                error = "Threat telegraph presentation requires distinct valid player and enemy runtime ids.";
                return false;
            }

            Clear();
            playerRuntimeId = nextPlayerRuntimeId;
            enemyRuntimeId = nextEnemyRuntimeId;
            bound = true;
            if (weakpointPresentation != null
                && !weakpointPresentation.TryBind(nextEnemyRuntimeId, out error))
            {
                UnbindAndClear();
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryRebindEnemyRuntimeId(
            RuntimeId nextEnemyRuntimeId,
            out string error)
        {
            if (!prepared || !bound)
            {
                error = "Threat telegraph presentation must be bound before enemy rebinding.";
                return false;
            }

            if (!nextEnemyRuntimeId.IsValid || nextEnemyRuntimeId == playerRuntimeId)
            {
                error = "Threat telegraph presentation requires a valid enemy RuntimeId distinct from the player.";
                return false;
            }

            // Rebinding also clears any cached telegraph/weakpoint state from
            // the old enemy. The domain cancels those threats before this
            // callback, so no old threat is allowed to leak into the butterfly.
            return TryBind(playerRuntimeId, nextEnemyRuntimeId, out error);
        }

        /// <summary>
        /// Reads only the current durable threat snapshots. It creates or
        /// updates telegraphs for Telegraph/Windup and caches every known D0
        /// threat key so the following trace transition can resolve a release.
        /// </summary>
        public void Reconcile(
            ThreatSnapshot[] snapshots,
            int snapshotCount,
            TickIndex currentTick)
        {
            if (!prepared
                || !bound
                || snapshots == null
                || snapshotCount < 0
                || snapshotCount > snapshots.Length)
            {
                return;
            }

            lastEnemySourcePosition = enemySourceAnchor.position;
            lastPlayerDangerPosition = playerDangerAnchor.position;
            lastWeakpointPosition = enemyWeakpointAnchor.position;
            for (int index = 0; index < persistentSlots.Length; index++)
            {
                persistentSlots[index] = false;
            }

            bool heavyThreatFound = false;
            for (int index = 0; index < snapshotCount; index++)
            {
                ThreatSnapshot snapshot = snapshots[index];
                if (!snapshot.RuntimeId.IsValid
                    || !TryGetPresentationDefinition(
                        snapshot,
                        out CombatThreatPresentationDefinition definition))
                {
                    continue;
                }

                CacheMetadata(snapshot.RuntimeId, snapshot.PresentationKey);
                bool persistent = D0ThreatPresentationRouting.RequiresPersistentTelegraph(snapshot);
                if (definition.Kind == CombatThreatPresentationKind.HeavyWeakpoint
                    && persistent)
                {
                    heavyThreatFound = true;
                    weakpointPresentation?.SetHeavyThreat(snapshot, currentTick);
                }

                int slot = FindActiveSlot(snapshot.RuntimeId);
                if (snapshot.IsTerminal)
                {
                    if (slot >= 0)
                    {
                        ReleaseSlot(slot);
                    }

                    continue;
                }

                if (!persistent)
                {
                    continue;
                }

                if (slot < 0 && !TryAcquireSlot(snapshot.RuntimeId, snapshot.PresentationKey, out slot))
                {
                    continue;
                }

                views[slot].ShowTelegraph(
                    definition,
                    snapshot.State,
                    snapshot.StateUntilTick,
                    currentTick,
                    lastEnemySourcePosition,
                    lastPlayerDangerPosition,
                    lastWeakpointPosition);
                persistentSlots[slot] = true;
            }

            if (!heavyThreatFound)
            {
                weakpointPresentation?.ClearHeavyThreat();
            }

            for (int index = 0; index < Capacity; index++)
            {
                if (activeSlots[index]
                    && !persistentSlots[index]
                    && !views[index].IsReleasing)
                {
                    ReleaseSlot(index);
                }
            }
        }

        /// <summary>
        /// Applies one already-committed threat transition. The returned signal
        /// gives the root coordinator an explicit audio hand-off without adding
        /// a global event bus or a battle-state write path.
        /// </summary>
        public bool ConsumeTrace(
            in CombatEvent combatEvent,
            out D0ThreatPresentationSignal signal)
        {
            signal = default(D0ThreatPresentationSignal);
            if (!prepared || !bound)
            {
                return false;
            }

            weakpointPresentation?.ConsumeTrace(combatEvent);
            if (!TryGetCachedPresentationKey(combatEvent.TargetId, out int presentationKey)
                || !D0ThreatPresentationRouting.TryResolve(
                    combatEvent,
                    enemyRuntimeId,
                    combatEvent.TargetId,
                    presentationKey,
                    out signal))
            {
                return false;
            }

            if (!presentationProfile.TryGetThreatDefinition(
                    signal.PresentationKey,
                    out CombatThreatPresentationDefinition definition))
            {
                UnknownPresentationKeyCount++;
                return false;
            }

            int slot = FindActiveSlot(signal.ThreatRuntimeId);
            switch (signal.Command)
            {
                case D0ThreatPresentationCommand.BeginTelegraph:
                case D0ThreatPresentationCommand.EscalateTelegraph:
                    if (slot < 0
                        && !TryAcquireSlot(
                            signal.ThreatRuntimeId,
                            signal.PresentationKey,
                            out slot))
                    {
                        return false;
                    }

                    views[slot].ShowTelegraph(
                        definition,
                        signal.Command == D0ThreatPresentationCommand.BeginTelegraph
                            ? ThreatState.Telegraph
                            : ThreatState.Windup,
                        TickIndex.Invalid,
                        TickIndex.Invalid,
                        lastEnemySourcePosition,
                        lastPlayerDangerPosition,
                        lastWeakpointPosition);
                    break;

                case D0ThreatPresentationCommand.ReleaseFast:
                    PlayRelease(definition, signal, ref slot);
                    break;

                case D0ThreatPresentationCommand.ReleaseVolley:
                case D0ThreatPresentationCommand.ReleaseHeavy:
                    if (signal.Command == D0ThreatPresentationCommand.ReleaseHeavy)
                    {
                        weakpointPresentation?.ClearHeavyThreat();
                    }

                    PlayRelease(definition, signal, ref slot);
                    break;

                case D0ThreatPresentationCommand.Cancel:
                case D0ThreatPresentationCommand.Complete:
                    if (slot >= 0)
                    {
                        ReleaseSlot(slot);
                    }

                    RemoveCachedMetadata(signal.ThreatRuntimeId);
                    if (signal.Kind == CombatThreatPresentationKind.HeavyWeakpoint)
                    {
                        weakpointPresentation?.ClearHeavyThreat();
                    }

                    break;
            }

            return true;
        }

        public void ConsumeSelectedHit(in SelectedAttackHit hit)
        {
            if (prepared && bound)
            {
                weakpointPresentation?.ConsumeSelectedHit(hit);
            }
        }

        /// <summary>
        /// Advances only presentation-local timers. The caller supplies whether
        /// battle simulation is running so pause leaves pulse/release timing and
        /// the externally derived countdown visually frozen.
        /// </summary>
        public void Advance(float deltaTime, bool isRunning)
        {
            if (!prepared)
            {
                return;
            }

            for (int index = 0; index < Capacity; index++)
            {
                if (activeSlots[index]
                    && !views[index].Advance(deltaTime, isRunning))
                {
                    ReleaseSlot(index);
                }
            }

            weakpointPresentation?.Advance(deltaTime, isRunning);
        }

        /// <summary>
        /// A trace gap must not synthesize release feedback. Clear transient
        /// slots then reconstruct only current Telegraph/Windup snapshots.
        /// </summary>
        public void Resynchronize(
            ThreatSnapshot[] snapshots,
            int snapshotCount,
            TickIndex currentTick)
        {
            Clear();
            Reconcile(snapshots, snapshotCount, currentTick);
        }

        public bool TryGetView(RuntimeId threatRuntimeId, out ThreatTelegraph2DView view)
        {
            int slot = FindActiveSlot(threatRuntimeId);
            if (slot < 0)
            {
                view = null;
                return false;
            }

            view = views[slot];
            return true;
        }

        public void Clear()
        {
            if (views != null)
            {
                for (int index = 0; index < views.Length; index++)
                {
                    if (views[index] != null)
                    {
                        views[index].Clear();
                    }
                }
            }

            // Teardown can arrive after a partially failed prewarm or after a
            // sibling presenter has already disposed one backing buffer. Clear
            // every buffer independently so scene shutdown remains idempotent.
            if (activeSlots != null)
            {
                Array.Clear(activeSlots, 0, activeSlots.Length);
            }

            if (activeRuntimeIds != null)
            {
                Array.Clear(activeRuntimeIds, 0, activeRuntimeIds.Length);
            }

            if (activePresentationKeys != null)
            {
                Array.Clear(activePresentationKeys, 0, activePresentationKeys.Length);
            }

            if (persistentSlots != null)
            {
                Array.Clear(persistentSlots, 0, persistentSlots.Length);
            }

            if (cachedRuntimeIds != null)
            {
                Array.Clear(cachedRuntimeIds, 0, cachedRuntimeIds.Length);
            }

            if (cachedPresentationKeys != null)
            {
                Array.Clear(cachedPresentationKeys, 0, cachedPresentationKeys.Length);
            }

            cachedMetadataCount = 0;
            weakpointPresentation?.Clear();
        }

        public void UnbindAndClear()
        {
            Clear();
            playerRuntimeId = RuntimeId.Invalid;
            enemyRuntimeId = RuntimeId.Invalid;
            bound = false;
            weakpointPresentation?.UnbindAndClear();
        }

        private void PlayRelease(
            CombatThreatPresentationDefinition definition,
            in D0ThreatPresentationSignal signal,
            ref int slot)
        {
            if (slot < 0
                && !TryAcquireSlot(
                    signal.ThreatRuntimeId,
                    signal.PresentationKey,
                    out slot))
            {
                return;
            }

            views[slot].PlayRelease(
                definition,
                lastEnemySourcePosition,
                lastPlayerDangerPosition,
                lastWeakpointPosition);
        }

        private bool TryGetPresentationDefinition(
            in ThreatSnapshot snapshot,
            out CombatThreatPresentationDefinition definition)
        {
            definition = null;
            if (!D0ThreatPresentationRouting.TryGetKind(snapshot.PresentationKey, out _)
                || !presentationProfile.TryGetThreatDefinition(
                    snapshot.PresentationKey,
                    out definition))
            {
                UnknownPresentationKeyCount++;
                return false;
            }

            bool payloadMatches = definition.Kind == CombatThreatPresentationKind.HeavyWeakpoint
                ? snapshot.PayloadKind == ThreatPayloadKind.TimedImpact
                : snapshot.PayloadKind == ThreatPayloadKind.SweptProjectile;
            if (!payloadMatches)
            {
                UnknownPresentationKeyCount++;
                definition = null;
                return false;
            }

            return true;
        }

        private bool TryAcquireSlot(RuntimeId runtimeId, int presentationKey, out int slot)
        {
            slot = FindFreeSlot();
            if (slot < 0)
            {
                PoolRejectCount++;
                return false;
            }

            activeSlots[slot] = true;
            activeRuntimeIds[slot] = runtimeId;
            activePresentationKeys[slot] = presentationKey;
            return true;
        }

        private void ReleaseSlot(int slot)
        {
            if (slot < 0 || slot >= Capacity)
            {
                return;
            }

            views[slot].Clear();
            activeSlots[slot] = false;
            activeRuntimeIds[slot] = RuntimeId.Invalid;
            activePresentationKeys[slot] = 0;
            persistentSlots[slot] = false;
        }

        private int FindActiveSlot(RuntimeId runtimeId)
        {
            if (!runtimeId.IsValid)
            {
                return -1;
            }

            for (int index = 0; index < Capacity; index++)
            {
                if (activeSlots[index] && activeRuntimeIds[index] == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeSlot()
        {
            for (int index = 0; index < Capacity; index++)
            {
                if (!activeSlots[index])
                {
                    return index;
                }
            }

            return -1;
        }

        private void CacheMetadata(RuntimeId runtimeId, int presentationKey)
        {
            if (!runtimeId.IsValid || presentationKey <= 0)
            {
                return;
            }

            for (int index = 0; index < cachedMetadataCount; index++)
            {
                if (cachedRuntimeIds[index] == runtimeId)
                {
                    cachedPresentationKeys[index] = presentationKey;
                    return;
                }
            }

            if (cachedMetadataCount >= cachedRuntimeIds.Length)
            {
                PoolRejectCount++;
                return;
            }

            cachedRuntimeIds[cachedMetadataCount] = runtimeId;
            cachedPresentationKeys[cachedMetadataCount] = presentationKey;
            cachedMetadataCount++;
        }

        private void RemoveCachedMetadata(RuntimeId runtimeId)
        {
            for (int index = 0; index < cachedMetadataCount; index++)
            {
                if (cachedRuntimeIds[index] != runtimeId)
                {
                    continue;
                }

                int lastIndex = --cachedMetadataCount;
                cachedRuntimeIds[index] = cachedRuntimeIds[lastIndex];
                cachedPresentationKeys[index] =
                    cachedPresentationKeys[lastIndex];
                cachedRuntimeIds[lastIndex] = RuntimeId.Invalid;
                cachedPresentationKeys[lastIndex] = 0;
                return;
            }
        }

        private bool TryGetCachedPresentationKey(RuntimeId runtimeId, out int presentationKey)
        {
            int activeSlot = FindActiveSlot(runtimeId);
            if (activeSlot >= 0)
            {
                presentationKey = activePresentationKeys[activeSlot];
                return true;
            }

            for (int index = 0; index < cachedMetadataCount; index++)
            {
                if (cachedRuntimeIds[index] == runtimeId)
                {
                    presentationKey = cachedPresentationKeys[index];
                    return true;
                }
            }

            presentationKey = 0;
            return false;
        }

        private void ApplySorting()
        {
            if (views == null || presentationProfile == null)
            {
                return;
            }

            for (int index = 0; index < views.Length; index++)
            {
                views[index]?.ApplySorting(
                    presentationProfile.Sorting.SortingLayerName,
                    presentationProfile.Sorting.WorldEffectsOrder);
            }
        }

        private void DisposeViews()
        {
            if (views != null)
            {
                for (int index = 0; index < views.Length; index++)
                {
                    if (views[index] != null)
                    {
                        DestroyObject(views[index].gameObject);
                    }
                }
            }

            views = null;
            activeRuntimeIds = null;
            activePresentationKeys = null;
            activeSlots = null;
            persistentSlots = null;
            cachedRuntimeIds = null;
            cachedPresentationKeys = null;
            cachedMetadataCount = 0;
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private void OnDestroy()
        {
            UnbindAndClear();
        }
    }

    /// <summary>
    /// One prewarmed visual slot. It renders a source/pulse telegraph, an
    /// intercept glyph or heavy arrow/dashed line, and a short release flare.
    /// It is intentionally unaware of BattleSession, Physics and trace cursors.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThreatTelegraph2DView : MonoBehaviour
    {
        private const int RingSegmentCount = 20;
        private const int DashCount = 6;

        private readonly Vector3[] ringPoints = new Vector3[RingSegmentCount + 1];
        private readonly Vector3[] glyphPoints = new Vector3[4];
        private readonly Vector3[] arrowPoints = new Vector3[3];
        private readonly Vector3[] dashPoints = new Vector3[2];

        private LineRenderer sourceRing;
        private LineRenderer playerDangerRing;
        private LineRenderer interceptGlyph;
        private LineRenderer arrow;
        private LineRenderer[] dashedLines;
        private Material effectMaterial;
        private Camera presentationCamera;
        private CombatThreatPresentationDefinition definition;
        private ThreatState threatState;
        private Vector3 enemySourcePosition;
        private Vector3 playerDangerPosition;
        private Vector3 weakpointPosition;
        private float pulseElapsed;
        private float releaseElapsed;
        private bool prepared;
        private bool releasing;

        public bool IsPrepared => prepared;
        public bool IsActive => prepared && gameObject.activeSelf;
        public bool IsReleasing => IsActive && releasing;
        public CombatThreatPresentationKind Kind => definition == null
            ? default(CombatThreatPresentationKind)
            : definition.Kind;

        public bool TryPrepare(
            Material material,
            Camera camera,
            string sortingLayerName,
            int sortingOrder,
            out string error)
        {
            if (material == null)
            {
                error = "D0 threat telegraph view requires an effect material.";
                return false;
            }

            if (prepared)
            {
                effectMaterial = material;
                presentationCamera = camera;
                ApplySorting(sortingLayerName, sortingOrder);
                error = string.Empty;
                return true;
            }

            effectMaterial = material;
            presentationCamera = camera;
            sourceRing = CreateLineRenderer("ThreatSourcePulse", RingSegmentCount + 1, 0.046f, sortingLayerName, sortingOrder);
            playerDangerRing = CreateLineRenderer("ThreatPlayerDangerPulse", RingSegmentCount + 1, 0.056f, sortingLayerName, sortingOrder + 1);
            interceptGlyph = CreateLineRenderer("ThreatInterceptGlyph", 4, 0.038f, sortingLayerName, sortingOrder + 1);
            arrow = CreateLineRenderer("ThreatHeavyArrow", 3, 0.046f, sortingLayerName, sortingOrder + 1);
            dashedLines = new LineRenderer[DashCount];
            for (int index = 0; index < DashCount; index++)
            {
                dashedLines[index] = CreateLineRenderer(
                    "ThreatHeavyDash_" + index,
                    2,
                    0.028f,
                    sortingLayerName,
                    sortingOrder + 1);
            }

            prepared = true;
            Clear();
            error = string.Empty;
            return true;
        }

        public void ApplySorting(string sortingLayerName, int sortingOrder)
        {
            ApplySorting(sourceRing, sortingLayerName, sortingOrder);
            ApplySorting(playerDangerRing, sortingLayerName, sortingOrder + 1);
            ApplySorting(interceptGlyph, sortingLayerName, sortingOrder + 1);
            ApplySorting(arrow, sortingLayerName, sortingOrder + 1);
            if (dashedLines != null)
            {
                for (int index = 0; index < dashedLines.Length; index++)
                {
                    ApplySorting(dashedLines[index], sortingLayerName, sortingOrder + 1);
                }
            }
        }

        public void ShowTelegraph(
            CombatThreatPresentationDefinition nextDefinition,
            ThreatState nextState,
            TickIndex ignoredStateUntilTick,
            TickIndex ignoredCurrentTick,
            Vector3 nextEnemySourcePosition,
            Vector3 nextPlayerDangerPosition,
            Vector3 nextWeakpointPosition)
        {
            if (!prepared || nextDefinition == null)
            {
                return;
            }

            bool changed = !IsActive
                || releasing
                || definition != nextDefinition
                || threatState != nextState;
            definition = nextDefinition;
            threatState = nextState;
            enemySourcePosition = nextEnemySourcePosition;
            playerDangerPosition = nextPlayerDangerPosition;
            weakpointPosition = nextWeakpointPosition;
            releasing = false;
            if (changed)
            {
                pulseElapsed = 0f;
            }

            gameObject.SetActive(true);
            WriteTelegraph();
        }

        public void PlayRelease(
            CombatThreatPresentationDefinition nextDefinition,
            Vector3 nextEnemySourcePosition,
            Vector3 nextPlayerDangerPosition,
            Vector3 nextWeakpointPosition)
        {
            if (!prepared || nextDefinition == null)
            {
                return;
            }

            definition = nextDefinition;
            enemySourcePosition = nextEnemySourcePosition;
            playerDangerPosition = nextPlayerDangerPosition;
            weakpointPosition = nextWeakpointPosition;
            releasing = true;
            releaseElapsed = 0f;
            gameObject.SetActive(true);
            WriteRelease(0f);
        }

        public bool Advance(float deltaTime, bool isRunning)
        {
            if (!IsActive)
            {
                return false;
            }

            if (!isRunning)
            {
                return true;
            }

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            if (!releasing)
            {
                pulseElapsed += safeDeltaTime;
                WriteTelegraph();
                return true;
            }

            releaseElapsed += safeDeltaTime;
            float duration = Mathf.Max(0.01f, definition.ReleaseDuration);
            float progress = Mathf.Clamp01(releaseElapsed / duration);
            WriteRelease(progress);
            if (progress < 1f)
            {
                return true;
            }

            Clear();
            return false;
        }

        public void Clear()
        {
            releasing = false;
            definition = null;
            SetEnabled(sourceRing, false);
            SetEnabled(playerDangerRing, false);
            SetEnabled(interceptGlyph, false);
            SetEnabled(arrow, false);
            SetEnabled(dashedLines, false);
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void WriteTelegraph()
        {
            if (definition == null)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(pulseElapsed * 5.1f);
            float intensity = threatState == ThreatState.Windup ? 1f : 0.72f;
            Color primary = definition.PrimaryColor;
            primary.a *= Mathf.Lerp(0.48f, intensity, pulse);
            Color secondary = definition.SecondaryColor;
            secondary.a *= Mathf.Lerp(0.42f, intensity, pulse);
            Vector3 right = ResolveCameraRight();
            Vector3 up = ResolveCameraUp();

            switch (definition.Kind)
            {
                case CombatThreatPresentationKind.FastUninterceptable:
                    WriteRing(sourceRing, enemySourcePosition, right, up, 0.38f + pulse * 0.14f, primary);
                    WriteRing(playerDangerRing, playerDangerPosition, right, up, 0.48f + pulse * 0.22f, secondary);
                    SetEnabled(interceptGlyph, false);
                    SetEnabled(arrow, false);
                    SetEnabled(dashedLines, false);
                    break;

                case CombatThreatPresentationKind.InterceptableVolley:
                    WriteRing(sourceRing, enemySourcePosition, right, up, 0.34f + pulse * 0.10f, primary);
                    WriteInterceptGlyph(enemySourcePosition, right, up, 0.30f + pulse * 0.04f, secondary);
                    SetEnabled(playerDangerRing, false);
                    SetEnabled(arrow, false);
                    SetEnabled(dashedLines, false);
                    break;

                case CombatThreatPresentationKind.HeavyWeakpoint:
                    SetEnabled(sourceRing, false);
                    SetEnabled(playerDangerRing, false);
                    SetEnabled(interceptGlyph, false);
                    WriteHeavyGuide(enemySourcePosition, weakpointPosition, primary, secondary);
                    break;
            }
        }

        private void WriteRelease(float progress)
        {
            if (definition == null)
            {
                return;
            }

            Vector3 right = ResolveCameraRight();
            Vector3 up = ResolveCameraUp();
            float fade = 1f - progress;
            Color primary = Color.Lerp(definition.PrimaryColor, Color.white, 0.26f);
            primary.a *= fade;
            Color secondary = Color.Lerp(definition.SecondaryColor, Color.white, 0.44f);
            secondary.a *= fade;
            switch (definition.Kind)
            {
                case CombatThreatPresentationKind.FastUninterceptable:
                    WriteRing(sourceRing, enemySourcePosition, right, up, Mathf.Lerp(0.36f, 1.08f, progress), primary);
                    WriteRing(playerDangerRing, playerDangerPosition, right, up, Mathf.Lerp(0.42f, 0.94f, progress), secondary);
                    SetEnabled(interceptGlyph, false);
                    SetEnabled(arrow, false);
                    SetEnabled(dashedLines, false);
                    break;

                case CombatThreatPresentationKind.InterceptableVolley:
                    WriteRing(sourceRing, enemySourcePosition, right, up, Mathf.Lerp(0.30f, 0.82f, progress), primary);
                    WriteInterceptGlyph(enemySourcePosition, right, up, Mathf.Lerp(0.32f, 0.58f, progress), secondary);
                    SetEnabled(playerDangerRing, false);
                    SetEnabled(arrow, false);
                    SetEnabled(dashedLines, false);
                    break;

                case CombatThreatPresentationKind.HeavyWeakpoint:
                    WriteRing(sourceRing, weakpointPosition, right, up, Mathf.Lerp(0.24f, 1.02f, progress), primary);
                    WriteHeavyGuide(enemySourcePosition, weakpointPosition, primary, secondary);
                    SetEnabled(playerDangerRing, false);
                    SetEnabled(interceptGlyph, false);
                    break;
            }
        }

        private void WriteHeavyGuide(
            Vector3 source,
            Vector3 target,
            Color primary,
            Color secondary)
        {
            Vector3 direction = target - source;
            float distance = direction.magnitude;
            if (distance <= 0.0001f)
            {
                direction = ResolveCameraUp();
                distance = 0.01f;
            }
            else
            {
                direction /= distance;
            }

            Vector3 perpendicular = Vector3.Cross(direction, ResolveCameraRight());
            if (perpendicular.sqrMagnitude <= 0.0001f)
            {
                perpendicular = ResolveCameraUp();
            }
            else
            {
                perpendicular.Normalize();
            }

            float arrowSize = Mathf.Min(0.28f, Mathf.Max(0.08f, distance * 0.22f));
            arrowPoints[0] = target - direction * arrowSize;
            arrowPoints[1] = target;
            arrowPoints[2] = target - direction * arrowSize + perpendicular * arrowSize * 0.65f;
            arrow.SetPositions(arrowPoints);
            arrow.startColor = primary;
            arrow.endColor = secondary;
            arrow.enabled = primary.a > 0.001f || secondary.a > 0.001f;

            for (int index = 0; index < dashedLines.Length; index++)
            {
                float startFraction = index / (float)DashCount;
                float endFraction = Mathf.Min(1f, startFraction + 0.52f / DashCount);
                dashPoints[0] = Vector3.Lerp(source, target, startFraction);
                dashPoints[1] = Vector3.Lerp(source, target, endFraction);
                LineRenderer dash = dashedLines[index];
                dash.SetPositions(dashPoints);
                dash.startColor = primary;
                dash.endColor = secondary;
                dash.enabled = primary.a > 0.001f || secondary.a > 0.001f;
            }
        }

        private void WriteInterceptGlyph(
            Vector3 center,
            Vector3 right,
            Vector3 up,
            float radius,
            Color color)
        {
            glyphPoints[0] = center + up * radius;
            glyphPoints[1] = center - right * radius;
            glyphPoints[2] = center + right * radius;
            glyphPoints[3] = glyphPoints[0];
            interceptGlyph.SetPositions(glyphPoints);
            interceptGlyph.startColor = color;
            interceptGlyph.endColor = color;
            interceptGlyph.enabled = color.a > 0.001f;
        }

        private void WriteRing(
            LineRenderer line,
            Vector3 center,
            Vector3 right,
            Vector3 up,
            float radius,
            Color color)
        {
            for (int index = 0; index <= RingSegmentCount; index++)
            {
                float fraction = index == RingSegmentCount
                    ? 0f
                    : index / (float)RingSegmentCount;
                float angle = fraction * Mathf.PI * 2f;
                ringPoints[index] = center
                    + right * (Mathf.Cos(angle) * radius)
                    + up * (Mathf.Sin(angle) * radius);
            }

            line.SetPositions(ringPoints);
            line.startColor = color;
            line.endColor = color;
            line.enabled = color.a > 0.001f;
        }

        private LineRenderer CreateLineRenderer(
            string name,
            int positionCount,
            float width,
            string sortingLayerName,
            int sortingOrder)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = effectMaterial;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.positionCount = positionCount;
            line.startWidth = width;
            line.endWidth = width * 0.72f;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            ApplySorting(line, sortingLayerName, sortingOrder);
            line.enabled = false;
            return line;
        }

        private static void ApplySorting(
            LineRenderer line,
            string sortingLayerName,
            int sortingOrder)
        {
            if (line == null)
            {
                return;
            }

            line.sortingLayerName = string.IsNullOrWhiteSpace(sortingLayerName)
                ? "Default"
                : sortingLayerName;
            line.sortingOrder = sortingOrder;
        }

        private Vector3 ResolveCameraRight()
        {
            return presentationCamera == null
                ? Vector3.right
                : presentationCamera.transform.right;
        }

        private Vector3 ResolveCameraUp()
        {
            return presentationCamera == null
                ? Vector3.up
                : presentationCamera.transform.up;
        }

        private static void SetEnabled(LineRenderer line, bool enabled)
        {
            if (line != null)
            {
                line.enabled = enabled;
            }
        }

        private static void SetEnabled(LineRenderer[] lines, bool enabled)
        {
            if (lines == null)
            {
                return;
            }

            for (int index = 0; index < lines.Length; index++)
            {
                SetEnabled(lines[index], enabled);
            }
        }
    }
}
