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
    /// Owns the D0 weakpoint's visual language only: ambient breathing, free
    /// reticle lock, heavy-threat lock state, weakpoint-hit flashes and Break
    /// shards. It receives snapshots and committed presentation signals from a
    /// coordinator; it never queries Physics or reads/writes BattleSession.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class D0WeakpointPresentationController : MonoBehaviour
    {
        private const int RingSegmentCount = 20;
        private const int LockCornerCount = 4;
        private const int ShardCount = 4;

        private readonly Vector3[] ringPoints = new Vector3[RingSegmentCount + 1];
        private readonly Vector3[][] lockCornerPoints =
        {
            new Vector3[3],
            new Vector3[3],
            new Vector3[3],
            new Vector3[3]
        };
        private readonly Vector3[] shardPoints = new Vector3[2];

        [SerializeField]
        private CombatPresentationProfile presentationProfile;

        [SerializeField]
        private Material effectMaterial;

        [SerializeField]
        private Camera presentationCamera;

        private Transform weakpointAnchor;

        private SphereCollider weakpointHitbox;

        [SerializeField]
        private CombatAimReticle combatAimReticle;

        private Actor2DPresenter enemyActorPresenter;

        [SerializeField]
        private CombatAudioPresenter audioPresenter;

        [SerializeField]
        private Font countdownFont;

        private LineRenderer ambientRing;
        private LineRenderer hitFlashRing;
        private LineRenderer[] lockCorners;
        private LineRenderer[] shardLines;
        private TextMesh countdownText;
        private Renderer countdownRenderer;
        private Renderer[] enemyRenderers;
        private MaterialPropertyBlock[] enemyOriginalPropertyBlocks;
        private MaterialPropertyBlock propertyBlock;
        private RuntimeId enemyRuntimeId = RuntimeId.Invalid;
        private Color ambientColor;
        private Color lockPrimaryColor;
        private Color lockSecondaryColor;
        private TickIndex heavyStateUntilTick = TickIndex.Invalid;
        private TickIndex latestTick = TickIndex.Invalid;
        private float pulseElapsed;
        private float hitFlashRemaining;
        private float shardRemaining;
        private float breakFeedbackDuration;
        private int lastWrittenCountdownSeconds = -1;
        private bool prepared;
        private bool bound;
        private bool weakpointAvailable;
        private bool heavyThreatActive;
        private bool enemyDesaturated;

        public bool IsPrepared => prepared;
        public bool IsBound => bound;
        public bool IsWeakpointAvailable => weakpointAvailable;
        public bool IsHeavyThreatActive => heavyThreatActive;
        public bool IsReticleLocked { get; private set; }
        public int DisplayedCountdownSeconds { get; private set; }
        public int WeakpointFlashCount { get; private set; }
        public int BreakFeedbackCount { get; private set; }
        public int ActiveShardCount => shardRemaining > 0f ? ShardCount : 0;
        public CombatAudioPresenter AudioPresenter => audioPresenter;
        public int ReticleLockCueRequestCount { get; private set; }
        public int EnemyDangerTickCueRequestCount { get; private set; }

        /// <summary>
        /// Explicit scene composition seam for the D0 installer and isolated
        /// tests. It stores only presentation references; no combat state is
        /// captured or mutated here.
        /// </summary>
        public void Configure(
            CombatPresentationProfile profile,
            Material material,
            Camera camera,
            Transform nextWeakpointAnchor,
            CombatAimReticle reticle,
            Actor2DPresenter enemyPresenter = null,
            SphereCollider nextWeakpointHitbox = null)
        {
            presentationProfile = profile;
            effectMaterial = material;
            presentationCamera = camera;
            weakpointAnchor = nextWeakpointAnchor;
            weakpointHitbox = nextWeakpointHitbox == null && nextWeakpointAnchor != null
                ? nextWeakpointAnchor.GetComponent<SphereCollider>()
                : nextWeakpointHitbox;
            weakpointAvailable = weakpointAnchor != null && weakpointHitbox != null;
            combatAimReticle = reticle;
            enemyActorPresenter = enemyPresenter;
        }

        /// <summary>
        /// Rebinds the visual weakpoint to the active enemy entity. The
        /// collider remains gameplay-owned by that entity; this presenter only
        /// follows its transform and renders feedback.
        /// </summary>
        public void RebindEnemyEntity(
            Transform nextWeakpointAnchor,
            SphereCollider nextWeakpointHitbox,
            Actor2DPresenter nextEnemyActorPresenter = null,
            bool nextWeakpointAvailable = true)
        {
            if (prepared)
            {
                Clear();
            }

            weakpointAnchor = nextWeakpointAnchor;
            weakpointHitbox = nextWeakpointHitbox == null && nextWeakpointAnchor != null
                ? nextWeakpointAnchor.GetComponent<SphereCollider>()
                : nextWeakpointHitbox;
            weakpointAvailable = nextWeakpointAvailable;
            enemyActorPresenter = nextEnemyActorPresenter;
            EnemyActorPresentationDefinition state = enemyActorPresenter == null
                ? null
                : enemyActorPresenter.ActiveEnemyPresentation;
            breakFeedbackDuration = state == null
                ? 0f
                : state.BreakFeedbackDuration;

            if (prepared)
            {
                CacheEnemyRenderers();
            }
        }

        /// <summary>
        /// Composes the optional presentation-only audio bridge. The weakpoint
        /// view never reads combat state from it; cue acceptance remains owned
        /// by the bridge and has no effect on combat results.
        /// </summary>
        public void SetAudioPresenter(CombatAudioPresenter nextAudioPresenter)
        {
            audioPresenter = nextAudioPresenter;
        }

        public bool TryValidateAuthoring(out string error)
        {
            error = string.Empty;
            if (presentationProfile == null
                || !presentationProfile.TryValidateStatic(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "D0 weakpoint presentation requires a valid CombatPresentationProfile.";
                }

                return false;
            }

            if (effectMaterial == null)
            {
                error = "D0 weakpoint presentation requires an effect material.";
                return false;
            }

            if (presentationCamera == null || combatAimReticle == null)
            {
                error = "D0 weakpoint presentation requires camera and aim reticle references.";
                return false;
            }

            if (GetComponentsInChildren<Collider>(true).Length > 0
                || GetComponentsInChildren<Collider2D>(true).Length > 0
                || GetComponentsInChildren<Rigidbody>(true).Length > 0
                || GetComponentsInChildren<Rigidbody2D>(true).Length > 0)
            {
                error = "D0 weakpoint presentation must not contain Collider or Rigidbody components.";
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

            if (weakpointAvailable
                && (weakpointAnchor == null
                    || weakpointHitbox == null
                    || weakpointHitbox.transform != weakpointAnchor
                    || weakpointHitbox.radius <= 0f
                    || float.IsNaN(weakpointHitbox.radius)
                    || float.IsInfinity(weakpointHitbox.radius)))
            {
                error =
                    "D0 weakpoint presentation requires the runtime-bound enemy Entity weakpoint and finite positive SphereCollider.";
                return false;
            }

            if (enemyActorPresenter == null
                || enemyActorPresenter.ActiveEnemyPresentation == null)
            {
                error =
                    "D0 weakpoint presentation requires the runtime-bound enemy state presentation.";
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

            if (prepared)
            {
                ApplySorting();
                CacheEnemyRenderers();
                error = string.Empty;
                return true;
            }

            string sortingLayerName = presentationProfile.Sorting.SortingLayerName;
            int sortingOrder = presentationProfile.Sorting.WorldEffectsOrder;
            ambientRing = CreateLineRenderer(
                "WeakpointAmbientRing",
                RingSegmentCount + 1,
                0.034f,
                sortingLayerName,
                sortingOrder);
            hitFlashRing = CreateLineRenderer(
                "WeakpointHitFlash",
                RingSegmentCount + 1,
                0.052f,
                sortingLayerName,
                sortingOrder + 1);

            lockCorners = new LineRenderer[LockCornerCount];
            for (int index = 0; index < LockCornerCount; index++)
            {
                lockCorners[index] = CreateLineRenderer(
                    "WeakpointLockCorner_" + index,
                    3,
                    0.045f,
                    sortingLayerName,
                    sortingOrder + 1);
            }

            shardLines = new LineRenderer[ShardCount];
            for (int index = 0; index < ShardCount; index++)
            {
                shardLines[index] = CreateLineRenderer(
                    "WeakpointBreakShard_" + index,
                    2,
                    0.036f,
                    sortingLayerName,
                    sortingOrder + 2);
            }

            countdownText = CreateCountdownText(sortingLayerName, sortingOrder + 2);
            countdownRenderer = countdownText == null
                ? null
                : countdownText.GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            CacheEnemyRenderers();
            CombatThreatPresentationDefinition heavyDefinition;
            if (!presentationProfile.TryGetThreatDefinition(
                    CombatPresentationProfile.HeavyWeakpointThreatPresentationKey,
                    out heavyDefinition))
            {
                error = "D0 weakpoint presentation requires the heavy weakpoint threat definition.";
                return false;
            }

            ambientColor = heavyDefinition.SecondaryColor;
            lockPrimaryColor = heavyDefinition.PrimaryColor;
            lockSecondaryColor = heavyDefinition.SecondaryColor;
            breakFeedbackDuration =
                enemyActorPresenter
                    .ActiveEnemyPresentation
                    .BreakFeedbackDuration;
            prepared = true;
            Clear();
            error = string.Empty;
            return true;
        }

        public bool TryBind(RuntimeId nextEnemyRuntimeId, out string error)
        {
            if (!prepared)
            {
                error = "D0 weakpoint presentation must be prepared before binding.";
                return false;
            }

            if (!nextEnemyRuntimeId.IsValid)
            {
                error = "D0 weakpoint presentation requires a valid enemy runtime id.";
                return false;
            }

            Clear();
            enemyRuntimeId = nextEnemyRuntimeId;
            bound = true;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Applies the durable visual part of a key-3 telegraph. The countdown
        /// is derived from the supplied tick values, so pausing simulation also
        /// pauses the displayed deadline.
        /// </summary>
        public void SetHeavyThreat(in ThreatSnapshot snapshot, TickIndex currentTick)
        {
            if (!prepared
                || !weakpointAvailable
                || snapshot.PresentationKey
                    != CombatPresentationProfile.HeavyWeakpointThreatPresentationKey
                || snapshot.IsTerminal
                || (snapshot.State != ThreatState.Telegraph
                    && snapshot.State != ThreatState.Windup))
            {
                ClearHeavyThreat();
                return;
            }

            heavyThreatActive = true;
            heavyStateUntilTick = snapshot.StateUntilTick;
            latestTick = currentTick;
            UpdateCountdown();
            WriteVisual();
        }

        public void ClearHeavyThreat()
        {
            heavyThreatActive = false;
            heavyStateUntilTick = TickIndex.Invalid;
            DisplayedCountdownSeconds = 0;
            lastWrittenCountdownSeconds = -1;
            SetEnabled(lockCorners, false);
            if (countdownRenderer != null)
            {
                countdownRenderer.enabled = false;
            }
        }

        public void ConsumeSelectedHit(in SelectedAttackHit hit)
        {
            if (!prepared
                || !bound
                || !weakpointAvailable
                || hit.TargetId != enemyRuntimeId
                || hit.HitPart != HitPart.Weakpoint)
            {
                return;
            }

            hitFlashRemaining = Mathf.Max(
                hitFlashRemaining,
                ResolveWeakpointHitFlashDuration());
            WeakpointFlashCount++;
            WriteVisual();
        }

        public void ConsumeTrace(in CombatEvent combatEvent)
        {
            if (!prepared
                || !bound
                || !weakpointAvailable
                || combatEvent.TargetId != enemyRuntimeId)
            {
                return;
            }

            switch (combatEvent.EventType)
            {
                case CombatEventType.BreakTriggered:
                case CombatEventType.GroggyStarted:
                    ClearHeavyThreat();
                    shardRemaining = Mathf.Max(shardRemaining, breakFeedbackDuration);
                    ApplyEnemyDesaturation(true);
                    BreakFeedbackCount++;
                    WriteVisual();
                    break;

                case CombatEventType.GroggyEnded:
                    ApplyEnemyDesaturation(false);
                    break;

                case CombatEventType.Death:
                    ClearHeavyThreat();
                    break;
            }
        }

        public void Advance(float deltaTime, bool isRunning)
        {
            if (!prepared)
            {
                return;
            }

            if (isRunning)
            {
                float safeDeltaTime = Mathf.Max(0f, deltaTime);
                pulseElapsed += safeDeltaTime;
                hitFlashRemaining = Mathf.Max(0f, hitFlashRemaining - safeDeltaTime);
                shardRemaining = Mathf.Max(0f, shardRemaining - safeDeltaTime);
            }

            WriteVisual();
        }

        public void Clear()
        {
            ClearHeavyThreat();
            hitFlashRemaining = 0f;
            shardRemaining = 0f;
            IsReticleLocked = false;
            ReticleLockCueRequestCount = 0;
            EnemyDangerTickCueRequestCount = 0;
            SetEnabled(ambientRing, false);
            SetEnabled(hitFlashRing, false);
            SetEnabled(shardLines, false);
            ApplyEnemyDesaturation(false);
        }

        public void UnbindAndClear()
        {
            Clear();
            enemyRuntimeId = RuntimeId.Invalid;
            bound = false;
            latestTick = TickIndex.Invalid;
        }

        private float ResolveWeakpointHitFlashDuration()
        {
            return presentationProfile != null
                && presentationProfile.TryGetHitDefinition(
                    CombatHitPresentationKind.Weakpoint,
                    out CombatHitPresentationDefinition definition)
                ? Mathf.Max(0.01f, definition.Duration)
                : 0.22f;
        }

        private void WriteVisual()
        {
            if (!prepared || !weakpointAvailable || weakpointAnchor == null)
            {
                return;
            }

            Vector3 center = weakpointAnchor.position;
            Vector3 right = ResolveCameraRight();
            Vector3 up = ResolveCameraUp();
            float pulse = 0.5f + 0.5f * Mathf.Sin(pulseElapsed * 4.2f);
            Color ambient = ambientColor;
            ambient.a *= Mathf.Lerp(0.35f, 0.74f, pulse);
            WriteRing(ambientRing, center, right, up, Mathf.Lerp(0.16f, 0.23f, pulse), ambient);

            bool wasReticleLocked = IsReticleLocked;
            IsReticleLocked = ResolveReticleLock();
            if (bound
                && CombatAudioCueRouting.TryGetReticleLockCue(
                    wasReticleLocked,
                    IsReticleLocked,
                    out CombatAudioCue reticleCue))
            {
                ReticleLockCueRequestCount++;
                audioPresenter?.TryPlayPresentationCue(reticleCue);
            }

            if (heavyThreatActive)
            {
                Color lockColor = Color.Lerp(lockPrimaryColor, lockSecondaryColor, pulse * 0.35f);
                lockColor.a = Mathf.Clamp01(lockColor.a * (IsReticleLocked ? 1f : 0.72f));
                WriteLockFrame(center, right, up, 0.36f + pulse * 0.06f, lockColor);
                WriteCountdown(center, up, lockColor);
            }
            else
            {
                SetEnabled(lockCorners, false);
                if (countdownRenderer != null)
                {
                    countdownRenderer.enabled = false;
                }
            }

            if (hitFlashRemaining > 0f)
            {
                float normalized = Mathf.Clamp01(
                    hitFlashRemaining / ResolveWeakpointHitFlashDuration());
                Color flash = Color.Lerp(lockSecondaryColor, Color.white, 0.85f);
                flash.a = normalized;
                WriteRing(
                    hitFlashRing,
                    center,
                    right,
                    up,
                    Mathf.Lerp(0.16f, 0.42f, 1f - normalized),
                    flash);
            }
            else
            {
                SetEnabled(hitFlashRing, false);
            }

            WriteBreakShards(center, right, up);
        }

        private void WriteCountdown(Vector3 center, Vector3 up, Color color)
        {
            if (countdownText == null || countdownRenderer == null)
            {
                return;
            }

            countdownText.transform.position = center + up * 0.52f;
            countdownText.transform.rotation = ResolveBillboardRotation(center);
            countdownText.color = color;
            if (lastWrittenCountdownSeconds != DisplayedCountdownSeconds)
            {
                int previousDisplayedSeconds = lastWrittenCountdownSeconds;
                countdownText.text = ResolveCountdownText(DisplayedCountdownSeconds);
                lastWrittenCountdownSeconds = DisplayedCountdownSeconds;
                if (bound
                    && CombatAudioCueRouting.TryGetHeavyCountdownCue(
                        previousDisplayedSeconds,
                        DisplayedCountdownSeconds,
                        out CombatAudioCue countdownCue))
                {
                    EnemyDangerTickCueRequestCount++;
                    audioPresenter?.TryPlayPresentationCue(countdownCue);
                }
            }

            countdownRenderer.enabled = true;
        }

        private void WriteBreakShards(Vector3 center, Vector3 right, Vector3 up)
        {
            if (shardRemaining <= 0f || shardLines == null)
            {
                SetEnabled(shardLines, false);
                return;
            }

            float normalized = Mathf.Clamp01(
                shardRemaining / Mathf.Max(0.01f, breakFeedbackDuration));
            Color color = Color.Lerp(lockSecondaryColor, Color.white, 0.4f);
            color.a = normalized;
            for (int index = 0; index < shardLines.Length; index++)
            {
                float angle = Mathf.PI * 2f * index / shardLines.Length;
                Vector3 direction = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                shardPoints[0] = center + direction * (0.14f + (1f - normalized) * 0.42f);
                shardPoints[1] = center + direction * (0.34f + (1f - normalized) * 0.86f);
                LineRenderer shard = shardLines[index];
                shard.SetPositions(shardPoints);
                shard.startColor = color;
                shard.endColor = color;
                shard.enabled = color.a > 0.001f;
            }
        }

        private void WriteLockFrame(
            Vector3 center,
            Vector3 right,
            Vector3 up,
            float radius,
            Color color)
        {
            float arm = radius * 0.42f;
            WriteCorner(
                lockCorners[0],
                lockCornerPoints[0],
                center - right * radius + up * radius,
                right,
                -up,
                arm,
                color);
            WriteCorner(
                lockCorners[1],
                lockCornerPoints[1],
                center + right * radius + up * radius,
                -right,
                -up,
                arm,
                color);
            WriteCorner(
                lockCorners[2],
                lockCornerPoints[2],
                center - right * radius - up * radius,
                right,
                up,
                arm,
                color);
            WriteCorner(
                lockCorners[3],
                lockCornerPoints[3],
                center + right * radius - up * radius,
                -right,
                up,
                arm,
                color);
        }

        private void WriteRing(
            LineRenderer line,
            Vector3 center,
            Vector3 right,
            Vector3 up,
            float radius,
            Color color)
        {
            if (line == null)
            {
                return;
            }

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

        private static void WriteCorner(
            LineRenderer line,
            Vector3[] points,
            Vector3 corner,
            Vector3 horizontalDirection,
            Vector3 verticalDirection,
            float arm,
            Color color)
        {
            points[0] = corner + horizontalDirection * arm;
            points[1] = corner;
            points[2] = corner + verticalDirection * arm;
            line.SetPositions(points);
            line.startColor = color;
            line.endColor = color;
            line.enabled = color.a > 0.001f;
        }

        private void UpdateCountdown()
        {
            if (!heavyStateUntilTick.IsValid || !latestTick.IsValid)
            {
                DisplayedCountdownSeconds = 0;
                return;
            }

            long remainingTicks = Math.Max(0L, heavyStateUntilTick.Value - latestTick.Value);
            long tickRate = GameplayClock.DefaultTickRate;
            long seconds = remainingTicks <= 0L
                ? 0L
                : (remainingTicks + tickRate - 1L) / tickRate;
            DisplayedCountdownSeconds = seconds > 9L ? 9 : (int)seconds;
        }

        private bool ResolveReticleLock()
        {
            if (combatAimReticle == null
                || presentationCamera == null
                || !weakpointAvailable
                || weakpointHitbox == null
                || !combatAimReticle.TryGetViewport(out Vector2 reticleViewport))
            {
                return false;
            }

            Ray reticleRay = presentationCamera.ViewportPointToRay(
                new Vector3(reticleViewport.x, reticleViewport.y, 0f));
            Vector3 center = weakpointHitbox.transform.TransformPoint(weakpointHitbox.center);
            Vector3 scale = weakpointHitbox.transform.lossyScale;
            float radius = weakpointHitbox.radius * Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z));
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
            {
                return false;
            }

            Vector3 rayToCenter = reticleRay.origin - center;
            float projection = Vector3.Dot(rayToCenter, reticleRay.direction);
            float originDistanceSquared = rayToCenter.sqrMagnitude;
            float discriminant = projection * projection
                - (originDistanceSquared - radius * radius);
            if (discriminant < 0f)
            {
                return false;
            }

            float exitDistance = -projection + Mathf.Sqrt(discriminant);
            return exitDistance >= 0f;
        }

        private void ApplyEnemyDesaturation(bool desaturate)
        {
            if (enemyDesaturated == desaturate)
            {
                return;
            }

            enemyDesaturated = desaturate;
            if (!desaturate)
            {
                RestoreEnemyRendererPropertyBlocks();
                return;
            }

            if (enemyRenderers == null || propertyBlock == null)
            {
                return;
            }

            Color color = new Color(0.66f, 0.70f, 0.74f, 1f);
            for (int index = 0; index < enemyRenderers.Length; index++)
            {
                Renderer renderer = enemyRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock original =
                    enemyOriginalPropertyBlocks != null
                    && index < enemyOriginalPropertyBlocks.Length
                        ? enemyOriginalPropertyBlocks[index]
                        : null;
                renderer.SetPropertyBlock(original);
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_Color", color);
                propertyBlock.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void CacheEnemyRenderers()
        {
            RestoreEnemyRendererPropertyBlocks();
            enemyDesaturated = false;
            enemyRenderers = enemyActorPresenter == null
                ? Array.Empty<Renderer>()
                : enemyActorPresenter.GetComponentsInChildren<Renderer>(true);
            enemyOriginalPropertyBlocks =
                new MaterialPropertyBlock[enemyRenderers.Length];
            for (int index = 0; index < enemyRenderers.Length; index++)
            {
                MaterialPropertyBlock original = new MaterialPropertyBlock();
                if (enemyRenderers[index] != null)
                {
                    enemyRenderers[index].GetPropertyBlock(original);
                }

                enemyOriginalPropertyBlocks[index] = original;
            }
        }

        private void RestoreEnemyRendererPropertyBlocks()
        {
            if (enemyRenderers == null || enemyOriginalPropertyBlocks == null)
            {
                return;
            }

            int count = Mathf.Min(
                enemyRenderers.Length,
                enemyOriginalPropertyBlocks.Length);
            for (int index = 0; index < count; index++)
            {
                if (enemyRenderers[index] != null)
                {
                    enemyRenderers[index].SetPropertyBlock(
                        enemyOriginalPropertyBlocks[index]);
                }
            }
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
            line.sortingLayerName = NormalizeSortingLayerName(sortingLayerName);
            line.sortingOrder = sortingOrder;
            line.enabled = false;
            return line;
        }

        private TextMesh CreateCountdownText(string sortingLayerName, int sortingOrder)
        {
            GameObject textObject = new GameObject("WeakpointCountdown");
            textObject.transform.SetParent(transform, false);
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.font = countdownFont != null
                ? countdownFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.09f;
            text.fontSize = 44;
            text.text = "0";
            Renderer renderer = textObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingLayerName = NormalizeSortingLayerName(sortingLayerName);
                renderer.sortingOrder = sortingOrder;
                renderer.enabled = false;
            }

            return text;
        }

        private void ApplySorting()
        {
            if (presentationProfile == null)
            {
                return;
            }

            string sortingLayerName = NormalizeSortingLayerName(
                presentationProfile.Sorting.SortingLayerName);
            int sortingOrder = presentationProfile.Sorting.WorldEffectsOrder;
            ApplySorting(ambientRing, sortingLayerName, sortingOrder);
            ApplySorting(hitFlashRing, sortingLayerName, sortingOrder + 1);
            ApplySorting(lockCorners, sortingLayerName, sortingOrder + 1);
            ApplySorting(shardLines, sortingLayerName, sortingOrder + 2);
            if (countdownRenderer != null)
            {
                countdownRenderer.sortingLayerName = sortingLayerName;
                countdownRenderer.sortingOrder = sortingOrder + 2;
            }
        }

        private static void ApplySorting(
            LineRenderer line,
            string sortingLayerName,
            int sortingOrder)
        {
            if (line != null)
            {
                line.sortingLayerName = sortingLayerName;
                line.sortingOrder = sortingOrder;
            }
        }

        private static void ApplySorting(
            LineRenderer[] lines,
            string sortingLayerName,
            int sortingOrder)
        {
            if (lines == null)
            {
                return;
            }

            for (int index = 0; index < lines.Length; index++)
            {
                ApplySorting(lines[index], sortingLayerName, sortingOrder);
            }
        }

        private Quaternion ResolveBillboardRotation(Vector3 position)
        {
            if (presentationCamera == null)
            {
                return Quaternion.identity;
            }

            Vector3 toCamera = presentationCamera.transform.position - position;
            if (toCamera.sqrMagnitude <= 0.000001f)
            {
                toCamera = -presentationCamera.transform.forward;
            }

            Vector3 up = presentationCamera.transform.up;
            if (Vector3.Cross(toCamera, up).sqrMagnitude <= 0.000001f)
            {
                up = Vector3.up;
            }

            return Quaternion.LookRotation(toCamera.normalized, up);
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

        private static string ResolveCountdownText(int seconds)
        {
            switch (seconds)
            {
                case 0: return "0";
                case 1: return "1";
                case 2: return "2";
                case 3: return "3";
                case 4: return "4";
                case 5: return "5";
                case 6: return "6";
                case 7: return "7";
                case 8: return "8";
                default: return "9+";
            }
        }

        private static string NormalizeSortingLayerName(string sortingLayerName)
        {
            return string.IsNullOrWhiteSpace(sortingLayerName)
                ? "Default"
                : sortingLayerName;
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
