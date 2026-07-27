using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Prefab-owned trajectory adapter. Gameplay supplies only the committed
    /// endpoints; materials and visual construction remain inside the prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgTrajectoryVfxView : MonoBehaviour
    {
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private Transform stretchRoot;

        private Vector3 authoredStretchScale = Vector3.one;
        private float remaining;
        private bool prepared;

        public bool IsActive => prepared && remaining > 0f;

        private void Awake()
        {
            CaptureAuthoredState();
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            remaining = 0f;
        }

        public bool TryValidate(out string error)
        {
            if (lineRenderer == null && stretchRoot == null)
            {
                error = "Trajectory VFX prefab requires a LineRenderer or a stretch root.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryActivate(
            Vector3 start,
            Vector3 end,
            float duration,
            Vector3 authoredScale,
            Vector3 rotationOffsetEuler,
            out string error)
        {
            if (!TryValidate(out error)
                || !IsFinite(start)
                || !IsFinite(end)
                || !IsFinite(authoredScale)
                || !IsFinite(rotationOffsetEuler)
                || !IsFinitePositive(duration)
                || start == end)
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Trajectory VFX requires finite, distinct endpoints and a positive duration.";
                }

                return false;
            }

            CaptureAuthoredState();
            Vector3 delta = end - start;
            float length = delta.magnitude;
            Quaternion rotation = Quaternion.LookRotation(delta / length, Vector3.up)
                * Quaternion.Euler(rotationOffsetEuler);
            transform.SetPositionAndRotation(start, rotation);
            transform.localScale = authoredScale;

            if (lineRenderer != null)
            {
                lineRenderer.useWorldSpace = true;
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, start);
                lineRenderer.SetPosition(1, end);
                lineRenderer.enabled = true;
            }

            if (stretchRoot != null)
            {
                Vector3 scale = authoredStretchScale;
                scale.z *= length;
                stretchRoot.localScale = scale;
            }

            remaining = duration;
            prepared = true;
            gameObject.SetActive(true);
            error = string.Empty;
            return true;
        }

        public bool Advance(float unscaledDeltaTime, bool paused)
        {
            if (!IsActive || paused || unscaledDeltaTime <= 0f)
            {
                return IsActive;
            }

            remaining = Mathf.Max(0f, remaining - unscaledDeltaTime);
            if (remaining <= 0f)
            {
                Deactivate();
            }

            return IsActive;
        }

        public void Deactivate()
        {
            remaining = 0f;
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }

            if (stretchRoot != null)
            {
                stretchRoot.localScale = authoredStretchScale;
            }

            gameObject.SetActive(false);
        }

        private void CaptureAuthoredState()
        {
            if (prepared)
            {
                return;
            }

            if (stretchRoot != null)
            {
                authoredStretchScale = stretchRoot.localScale;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
