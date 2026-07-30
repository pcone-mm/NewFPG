using System;
using FPG.Demo.Core;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Presentation-only peek pose for Fei. The historical class name remains
    /// as a prefab compatibility boundary; independent room cover owns visuals,
    /// collision and durability.
    /// </summary>
    [DefaultExecutionOrder(910)]
    [DisallowMultipleComponent]
    public sealed class FpgPlayerBarrierPresentationController : MonoBehaviour
    {
        private const int OutlinePointCount = 4;

        private IFpgFormalPlayerPresentationSource formalSource;

        [Header("Cover branches")]
        [SerializeField]
        private Transform peekRoot;

        [SerializeField]
        private Transform coverVisualRoot;

        [SerializeField]
        private Renderer coverRenderer;

        [Header("Presentation sockets")]
        [SerializeField]
        private Transform primaryPresentationMuzzle;

        [SerializeField]
        private Transform secondaryPresentationMuzzle;

        [Header("Peek pose")]
        [SerializeField]
        private Vector3 peekLocalOffset = new Vector3(1.35f, 0f, 0f);

        [SerializeField, Min(0f)]
        private float peekTransitionSeconds = 0.08f;

        [SerializeField, Min(0f)]
        private float retractTransitionSeconds;

        [Header("Cover outline")]
        [SerializeField]
        private Material lineMaterial;

        [SerializeField, Min(0.01f)]
        private float fadeInSeconds = 0.18f;

        [SerializeField, Min(0.01f)]
        private float fadeOutSeconds = 0.12f;

        [SerializeField, Range(0f, 1f)]
        private float maximumOpacity = 0.72f;

        [SerializeField]
        private Color barrierColor = new Color(0.34f, 0.88f, 1f, 1f);

        private readonly Vector3[] outlinePoints =
            new Vector3[OutlinePointCount];

        private LineRenderer lineRenderer;
        private Transform capturedPeekRoot;
        private Vector3 authoredPeekLocalPosition;
        private float currentOpacity;
        private float currentPeekProgress;
        private bool coverMeshVisible;
        private int lastAppliedFrame = -1;

        public IFpgFormalPlayerPresentationSource FormalSource => formalSource;
        public bool IsFormalSourceBound => formalSource != null;
        public float CurrentOpacity => currentOpacity;
        public float CurrentPeekProgress => currentPeekProgress;
        public bool IsCoverMeshVisible => coverMeshVisible;
        public bool IsVisible => coverMeshVisible || currentOpacity > 0.001f;
        public Transform PeekRoot => peekRoot;
        public Transform CoverVisualRoot => coverVisualRoot;
        public Renderer CoverRenderer => coverRenderer;
        public Transform PrimaryPresentationMuzzle => primaryPresentationMuzzle;
        public Transform SecondaryPresentationMuzzle => secondaryPresentationMuzzle;
        public Vector3 PeekLocalOffset => peekLocalOffset;

        public static bool ShouldShowBarrier(
            in FpgFormalPlayerPresentationSnapshot snapshot)
        {
            return false;
        }

        public static bool ShouldShowCover(
            in FpgFormalPlayerPresentationSnapshot snapshot)
        {
            return ShouldShowBarrier(snapshot);
        }

        /// <summary>
        /// Binds the formal committed player presentation source.
        /// </summary>
        public bool TryBindFormalSource(
            IFpgFormalPlayerPresentationSource nextSource,
            out string error)
        {
            if (nextSource == null)
            {
                error = "D0 player cover formal binding requires a presentation source.";
                return false;
            }

            formalSource = nextSource;
            ResetPresentation();
            error = string.Empty;
            return true;
        }

        public void UnbindFormalSource()
        {
            formalSource = null;
            ResetPresentation();
        }

        public bool TrySetThreeCProfile(D0ThreeCProfile profile, out string error)
        {
            if (profile == null)
            {
                error = "D0 player cover presentation requires a D0 3C profile.";
                return false;
            }

            if (!profile.TryValidate(out error))
            {
                return false;
            }

            peekTransitionSeconds = profile.PeekTransitionSeconds;
            retractTransitionSeconds = profile.RetractTransitionSeconds;
            error = string.Empty;
            return true;
        }

        public bool TryResolvePresentationSocket(
            string socketId,
            out Transform anchor)
        {
            if (string.Equals(
                    socketId,
                    D0ActorSocketRegistry.PrimaryMuzzleId,
                    StringComparison.Ordinal)
                && primaryPresentationMuzzle != null)
            {
                anchor = primaryPresentationMuzzle;
                return true;
            }

            if (string.Equals(
                    socketId,
                    D0ActorSocketRegistry.SecondaryMuzzleId,
                    StringComparison.Ordinal)
                && secondaryPresentationMuzzle != null)
            {
                anchor = secondaryPresentationMuzzle;
                return true;
            }

            anchor = null;
            return false;
        }

        public bool TryValidate(out string error)
        {
            if (peekRoot == null)
            {
                error = "D0 player cover presentation requires a PeekRoot.";
                return false;
            }

            if (peekRoot == transform || !peekRoot.IsChildOf(transform))
            {
                error = "D0 player PeekRoot must be below the player presentation controller.";
                return false;
            }

            if (!IsStrictChildOf(primaryPresentationMuzzle, peekRoot)
                || !IsStrictChildOf(secondaryPresentationMuzzle, peekRoot))
            {
                error = "D0 player presentation muzzle proxies must be below PeekRoot.";
                return false;
            }

            if (primaryPresentationMuzzle == secondaryPresentationMuzzle)
            {
                error = "D0 player presentation muzzle proxies must be distinct Transforms.";
                return false;
            }

            if (!IsFinite(peekLocalOffset)
                || peekLocalOffset.sqrMagnitude <= 0.000001f)
            {
                error = "D0 player peek local offset must be finite and non-zero.";
                return false;
            }

            if (!IsFinite(peekTransitionSeconds)
                || !IsFinite(retractTransitionSeconds)
                || peekTransitionSeconds < 0f
                || retractTransitionSeconds < 0f)
            {
                error = "D0 player cover presentation durations must be finite and non-negative.";
                return false;
            }

            if (ContainsPhysicsComponents(peekRoot))
            {
                error = "D0 player peek presentation branch must not contain Collider or Rigidbody components.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Applies one already committed player snapshot. Paused snapshots leave
        /// the current visual pose untouched.
        /// </summary>
        public void ApplyCommittedSnapshot(
            in FpgFormalPlayerPresentationSnapshot snapshot,
            float deltaTime)
        {
            lastAppliedFrame = Time.frameCount;
            EnsureAuthoredPeekPoseCaptured();

            if (snapshot.IsPaused)
            {
                return;
            }

            if (!snapshot.IsCombatActive)
            {
                ResetPresentation();
                return;
            }

            SetCoverMeshVisible(false);
            SetOpacity(0f);
            AdvancePeek(snapshot, SanitizeDeltaTime(deltaTime));
        }

        public void ResetPresentation()
        {
            EnsureAuthoredPeekPoseCaptured();
            SetPeekProgress(0f);
            SetCoverMeshVisible(false);
            SetOpacity(0f);
        }

        private void Awake()
        {
            EnsureAuthoredPeekPoseCaptured();
            ResetPresentation();
        }

        private void OnDisable()
        {
            ResetPresentation();
        }

        private void LateUpdate()
        {
            if (lastAppliedFrame == Time.frameCount)
            {
                return;
            }

            if (formalSource == null
                || !formalSource.TryGetPlayerPresentationSnapshot(
                    out FpgFormalPlayerPresentationSnapshot snapshot))
            {
                ResetPresentation();
                return;
            }

            ApplyCommittedSnapshot(snapshot, Time.unscaledDeltaTime);
        }

        private void AdvancePeek(
            in FpgFormalPlayerPresentationSnapshot snapshot,
            float deltaTime)
        {
            if (!snapshot.IsCoverPeekRequested)
            {
                SetPeekProgress(0f);
                return;
            }

            if (peekTransitionSeconds <= 0f || MustForceCompletedPeek(snapshot))
            {
                SetPeekProgress(1f);
                return;
            }

            SetPeekProgress(Mathf.MoveTowards(
                currentPeekProgress,
                1f,
                deltaTime / peekTransitionSeconds));
        }

        private bool MustForceCompletedPeek(
            in FpgFormalPlayerPresentationSnapshot snapshot)
        {
            if (!snapshot.Tick.IsValid || !snapshot.CoverPeekStartedTick.IsValid)
            {
                return false;
            }

            int transitionTicks = Mathf.CeilToInt(
                peekTransitionSeconds * GameplayClock.DefaultTickRate);
            long completionTick = snapshot.CoverPeekStartedTick.Value
                + transitionTicks;
            return snapshot.Tick.Value >= completionTick;
        }

        private void SetPeekProgress(float progress)
        {
            currentPeekProgress = Mathf.Clamp01(progress);
            if (!EnsureAuthoredPeekPoseCaptured())
            {
                return;
            }

            float easedProgress = Mathf.SmoothStep(
                0f,
                1f,
                currentPeekProgress);
            peekRoot.localPosition = authoredPeekLocalPosition
                + peekLocalOffset * easedProgress;
        }

        private bool EnsureAuthoredPeekPoseCaptured()
        {
            if (peekRoot == null)
            {
                capturedPeekRoot = null;
                return false;
            }

            if (capturedPeekRoot != peekRoot)
            {
                capturedPeekRoot = peekRoot;
                authoredPeekLocalPosition = peekRoot.localPosition;
                currentPeekProgress = 0f;
            }

            return true;
        }

        private void EnsureLineRenderer()
        {
            lineRenderer = ResolveLineRenderer();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }

        private LineRenderer ResolveLineRenderer()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            return lineRenderer;
        }

        private void ConfigureLineRenderer()
        {
            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.useWorldSpace = false;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.loop = true;
            lineRenderer.positionCount = OutlinePointCount;
            lineRenderer.startWidth = 0.045f;
            lineRenderer.endWidth = 0.045f;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.numCapVertices = 0;
            lineRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            if (lineMaterial != null)
            {
                lineRenderer.sharedMaterial = lineMaterial;
            }

            SetOutlinePositions();
        }

        private void SetOutlinePositions()
        {
            if (lineRenderer == null || coverRenderer == null)
            {
                return;
            }

            Bounds bounds = coverRenderer.localBounds;
            if (bounds.size.sqrMagnitude <= 0.000001f)
            {
                bounds = new Bounds(Vector3.zero, Vector3.one);
            }

            float frontZ = bounds.min.z;
            SetOutlinePoint(
                0,
                new Vector3(bounds.min.x, bounds.min.y, frontZ));
            SetOutlinePoint(
                1,
                new Vector3(bounds.min.x, bounds.max.y, frontZ));
            SetOutlinePoint(
                2,
                new Vector3(bounds.max.x, bounds.max.y, frontZ));
            SetOutlinePoint(
                3,
                new Vector3(bounds.max.x, bounds.min.y, frontZ));
            lineRenderer.SetPositions(outlinePoints);
        }

        private void SetOutlinePoint(int index, Vector3 rendererLocalPoint)
        {
            Vector3 worldPoint = coverRenderer.transform.TransformPoint(
                rendererLocalPoint);
            outlinePoints[index] = transform.InverseTransformPoint(worldPoint);
        }

        private void AdvanceOpacity(bool targetVisible, float deltaTime)
        {
            float targetOpacity = targetVisible ? maximumOpacity : 0f;
            float duration = targetOpacity > currentOpacity
                ? fadeInSeconds
                : fadeOutSeconds;
            float maximumDelta = duration <= 0f
                ? Mathf.Abs(targetOpacity - currentOpacity)
                : Mathf.Max(maximumOpacity, 0.0001f) * deltaTime / duration;
            SetOpacity(Mathf.MoveTowards(
                currentOpacity,
                targetOpacity,
                maximumDelta));
        }

        private void SetCoverMeshVisible(bool visible)
        {
            coverMeshVisible = visible;
            if (coverRenderer != null)
            {
                coverRenderer.enabled = visible;
            }
        }

        private void SetOpacity(float opacity)
        {
            currentOpacity = Mathf.Clamp(opacity, 0f, maximumOpacity);
            if (lineRenderer == null)
            {
                return;
            }

            Color color = barrierColor;
            color.a *= currentOpacity;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.enabled = currentOpacity > 0.001f;
        }

        private static bool ContainsPhysicsComponents(Transform root)
        {
            return root.GetComponentsInChildren<Collider>(true).Length > 0
                || root.GetComponentsInChildren<Collider2D>(true).Length > 0
                || root.GetComponentsInChildren<Rigidbody>(true).Length > 0
                || root.GetComponentsInChildren<Rigidbody2D>(true).Length > 0;
        }

        private static bool IsStrictChildOf(Transform candidate, Transform root)
        {
            return candidate != null
                && root != null
                && candidate != root
                && candidate.IsChildOf(root);
        }

        private static float SanitizeDeltaTime(float deltaTime)
        {
            return IsFinite(deltaTime) && deltaTime > 0f
                ? deltaTime
                : 0f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
