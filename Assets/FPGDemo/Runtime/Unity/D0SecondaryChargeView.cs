using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// A single prewarmed, presentation-only visual for Fei's secondary cast.
    /// It draws a camera-facing target lock, converging charge strands and a
    /// travelling release core without creating colliders or touching combat
    /// state. The view is reused for every cast; it is never instantiated from
    /// a gameplay update path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class D0SecondaryChargeView : MonoBehaviour
    {
        private const int CornerCount = 4;
        private const int ConvergenceLineCount = 3;
        private const int CoreSegmentCount = 16;
        private const float StopMarkerHoldSeconds = 0.055f;

        private readonly Vector3[][] cornerPoints =
        {
            new Vector3[3],
            new Vector3[3],
            new Vector3[3],
            new Vector3[3]
        };

        private readonly Vector3[] corePoints = new Vector3[CoreSegmentCount + 1];

        private LineRenderer[] lockCorners;
        private LineRenderer[] convergenceLines;
        private LineRenderer releaseCore;
        private Light coreLight;
        private Camera presentationCamera;
        private Vector3 source;
        private Vector3 target;
        private Color baseColor;
        private float chargePulseDuration;
        private float chargeElapsed;
        private float releaseDuration;
        private float releaseElapsed;
        private float stopMarkerDelay;
        private bool stopMarkerFired;
        private bool prepared;
        private bool charging;
        private bool releasing;

        public bool IsPrepared => prepared;
        public bool IsActive => prepared && gameObject.activeSelf;
        public bool IsCharging => IsActive && charging;
        public bool IsReleasing => IsActive && releasing;
        public int HitMarkerCount { get; private set; }
        public int StopMarkerCount { get; private set; }
        public Vector3 LockedTarget => target;
        public Vector3 ReleaseSource => source;

        public bool TryPrepare(
            Material material,
            Camera nextPresentationCamera,
            out string error)
        {
            return TryPrepare(
                material,
                nextPresentationCamera,
                "Default",
                0,
                out error);
        }

        /// <summary>
        /// Prepares the owned lines with the D0 world-effects sort contract.
        /// This overload keeps the scene-free tests and legacy callers on the
        /// neutral Default/zero fallback while the installed slice receives its
        /// profile-controlled ordering.
        /// </summary>
        public bool TryPrepare(
            Material material,
            Camera nextPresentationCamera,
            string sortingLayerName,
            int sortingOrder,
            out string error)
        {
            if (material == null)
            {
                error = "D0 secondary charge view requires a material.";
                return false;
            }

            presentationCamera = nextPresentationCamera;
            if (prepared)
            {
                ApplySorting(sortingLayerName, sortingOrder);
                error = string.Empty;
                return true;
            }

            lockCorners = new LineRenderer[CornerCount];
            convergenceLines = new LineRenderer[ConvergenceLineCount];
            for (int index = 0; index < CornerCount; index++)
            {
                lockCorners[index] = CreateLineRenderer(
                    $"SecondaryLockCorner_{index}",
                    material,
                    3,
                    0.042f,
                    sortingLayerName,
                    sortingOrder);
            }

            for (int index = 0; index < ConvergenceLineCount; index++)
            {
                convergenceLines[index] = CreateLineRenderer(
                    $"SecondaryChargeStrand_{index}",
                    material,
                    2,
                    0.026f,
                    sortingLayerName,
                    sortingOrder);
            }

            releaseCore = CreateLineRenderer(
                "SecondaryReleaseCore",
                material,
                CoreSegmentCount + 1,
                0.055f,
                sortingLayerName,
                sortingOrder);
            coreLight = gameObject.AddComponent<Light>();
            coreLight.type = LightType.Point;
            coreLight.shadows = LightShadows.None;
            coreLight.range = 2.6f;
            coreLight.intensity = 0f;
            coreLight.enabled = false;

            prepared = true;
            Clear();
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Begins the ready-state visuals at a target captured from the free
        /// reticle. This value is intentionally visual-only and stays frozen
        /// until the cast releases.
        /// </summary>
        public void BeginCharge(
            Vector3 nextSource,
            Vector3 nextTarget,
            Color color,
            float pulseDuration)
        {
            if (!prepared)
            {
                return;
            }

            source = nextSource;
            target = nextTarget;
            baseColor = color;
            chargePulseDuration = Mathf.Max(0.01f, pulseDuration);
            chargeElapsed = 0f;
            releasing = false;
            charging = true;
            stopMarkerFired = false;
            gameObject.SetActive(true);
            WriteChargeVisual(0f);
        }

        /// <summary>
        /// Converts the ready visual into the committed release core. Calling
        /// this is the presentation interpretation of the audited CZN HIT
        /// boundary; it never decides whether the game hit or damaged anything.
        /// </summary>
        public void Release(
            Vector3 nextSource,
            Vector3 nextTarget,
            Color color,
            float duration,
            float nextStopMarkerDelay)
        {
            if (!prepared)
            {
                return;
            }

            source = nextSource;
            target = nextTarget;
            baseColor = color;
            releaseDuration = Mathf.Max(0.01f, duration);
            releaseElapsed = 0f;
            stopMarkerDelay = Mathf.Clamp(nextStopMarkerDelay, 0f, releaseDuration);
            stopMarkerFired = false;
            charging = false;
            releasing = true;
            HitMarkerCount++;
            gameObject.SetActive(true);
            WriteReleaseVisual(0f, false);
        }

        public bool Advance(float deltaTime)
        {
            if (!IsActive)
            {
                return false;
            }

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            if (charging)
            {
                chargeElapsed += safeDeltaTime;
                float pulse = Mathf.PingPong(
                    chargeElapsed / Mathf.Max(0.01f, chargePulseDuration),
                    1f);
                WriteChargeVisual(pulse);
                return true;
            }

            if (!releasing)
            {
                Clear();
                return false;
            }

            releaseElapsed += safeDeltaTime;
            bool holdAtStopMarker = false;
            if (!stopMarkerFired && releaseElapsed >= stopMarkerDelay)
            {
                stopMarkerFired = true;
                StopMarkerCount++;
            }

            float progress;
            if (stopMarkerFired
                && releaseElapsed < stopMarkerDelay + StopMarkerHoldSeconds)
            {
                holdAtStopMarker = true;
                progress = stopMarkerDelay / releaseDuration;
            }
            else
            {
                float heldTime = stopMarkerFired ? StopMarkerHoldSeconds : 0f;
                progress = Mathf.Clamp01((releaseElapsed - heldTime) / releaseDuration);
            }

            WriteReleaseVisual(progress, holdAtStopMarker);
            if (progress < 1f)
            {
                return true;
            }

            Clear();
            return false;
        }

        public void CancelCharge()
        {
            if (charging)
            {
                Clear();
            }
        }

        public void Clear()
        {
            charging = false;
            releasing = false;
            stopMarkerFired = false;
            SetEnabled(lockCorners, false);
            SetEnabled(convergenceLines, false);
            if (releaseCore != null)
            {
                releaseCore.enabled = false;
            }

            if (coreLight != null)
            {
                coreLight.enabled = false;
                coreLight.intensity = 0f;
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private LineRenderer CreateLineRenderer(
            string name,
            Material material,
            int positionCount,
            float width,
            string sortingLayerName,
            int sortingOrder)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.positionCount = positionCount;
            line.startWidth = width;
            line.endWidth = width * 0.72f;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingLayerName = NormalizeSortingLayerName(sortingLayerName);
            line.sortingOrder = sortingOrder;
            line.enabled = false;
            return line;
        }

        private void ApplySorting(string sortingLayerName, int sortingOrder)
        {
            string normalizedLayerName = NormalizeSortingLayerName(sortingLayerName);
            ApplySorting(lockCorners, normalizedLayerName, sortingOrder);
            ApplySorting(convergenceLines, normalizedLayerName, sortingOrder);
            if (releaseCore != null)
            {
                releaseCore.sortingLayerName = normalizedLayerName;
                releaseCore.sortingOrder = sortingOrder;
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
                LineRenderer line = lines[index];
                if (line != null)
                {
                    line.sortingLayerName = sortingLayerName;
                    line.sortingOrder = sortingOrder;
                }
            }
        }

        private static string NormalizeSortingLayerName(string sortingLayerName)
        {
            return string.IsNullOrWhiteSpace(sortingLayerName)
                ? "Default"
                : sortingLayerName;
        }

        private void WriteChargeVisual(float pulse)
        {
            Vector3 right = ResolveCameraRight();
            Vector3 up = ResolveCameraUp();
            float frameRadius = Mathf.Lerp(0.42f, 0.56f, pulse);
            Color lockColor = baseColor;
            lockColor.a *= Mathf.Lerp(0.58f, 0.96f, pulse);
            WriteLockFrame(target, right, up, frameRadius, lockColor);

            float strandRadius = Mathf.Lerp(0.16f, 0.06f, pulse);
            for (int index = 0; index < ConvergenceLineCount; index++)
            {
                float angle = (Mathf.PI * 2f * index / ConvergenceLineCount)
                    + chargeElapsed * 2.4f;
                Vector3 offset = right * (Mathf.Cos(angle) * strandRadius)
                    + up * (Mathf.Sin(angle) * strandRadius);
                LineRenderer strand = convergenceLines[index];
                strand.SetPosition(0, source + offset);
                strand.SetPosition(1, target - offset * 0.6f);
                strand.startColor = lockColor;
                strand.endColor = new Color(
                    lockColor.r,
                    lockColor.g,
                    lockColor.b,
                    lockColor.a * 0.32f);
                strand.enabled = true;
            }

            WriteCoreRing(
                source,
                right,
                up,
                Mathf.Lerp(0.09f, 0.16f, pulse),
                lockColor,
                0.68f);
        }

        private void WriteReleaseVisual(float progress, bool holdAtStopMarker)
        {
            Vector3 right = ResolveCameraRight();
            Vector3 up = ResolveCameraUp();
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 coreCenter = Vector3.Lerp(source, target, easedProgress);
            Color coreColor = Color.Lerp(baseColor, Color.white, 0.34f);
            float fade = 1f - progress;
            if (holdAtStopMarker)
            {
                coreColor = Color.white;
                fade = 1f;
            }

            coreColor.a *= fade;
            WriteCoreRing(
                coreCenter,
                right,
                up,
                Mathf.Lerp(0.17f, 0.48f, easedProgress),
                coreColor,
                holdAtStopMarker ? 1.3f : 1f);
            SetEnabled(convergenceLines, false);

            Color lockColor = baseColor;
            lockColor.a *= Mathf.Max(0f, 0.8f * fade);
            WriteLockFrame(
                target,
                right,
                up,
                Mathf.Lerp(0.46f, 0.18f, progress),
                lockColor);
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
                cornerPoints[0],
                center - right * radius + up * radius,
                right,
                -up,
                arm,
                color);
            WriteCorner(
                lockCorners[1],
                cornerPoints[1],
                center + right * radius + up * radius,
                -right,
                -up,
                arm,
                color);
            WriteCorner(
                lockCorners[2],
                cornerPoints[2],
                center - right * radius - up * radius,
                right,
                up,
                arm,
                color);
            WriteCorner(
                lockCorners[3],
                cornerPoints[3],
                center + right * radius - up * radius,
                -right,
                up,
                arm,
                color);
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

        private void WriteCoreRing(
            Vector3 center,
            Vector3 right,
            Vector3 up,
            float radius,
            Color color,
            float intensity)
        {
            for (int index = 0; index <= CoreSegmentCount; index++)
            {
                float fraction = index == CoreSegmentCount
                    ? 0f
                    : index / (float)CoreSegmentCount;
                float angle = fraction * Mathf.PI * 2f;
                corePoints[index] = center
                    + right * (Mathf.Cos(angle) * radius)
                    + up * (Mathf.Sin(angle) * radius);
            }

            releaseCore.SetPositions(corePoints);
            releaseCore.startColor = color;
            releaseCore.endColor = color;
            releaseCore.enabled = color.a > 0.001f;
            if (coreLight != null)
            {
                coreLight.transform.position = center;
                coreLight.color = color;
                coreLight.intensity = Mathf.Max(0f, intensity * color.a * 1.25f);
                coreLight.enabled = coreLight.intensity > 0.001f;
            }
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

        private static void SetEnabled(LineRenderer[] lines, bool enabled)
        {
            if (lines == null)
            {
                return;
            }

            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index] != null)
                {
                    lines[index].enabled = enabled;
                }
            }
        }
    }
}
