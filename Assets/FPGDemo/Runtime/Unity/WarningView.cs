using FPG.Demo.Enemy;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class WarningView : MonoBehaviour
    {
        private Transform cachedTransform;
        private Renderer[] renderers;
        private SpriteRenderer[] spriteRenderers;
        private MaterialPropertyBlock propertyBlock;
        private Camera billboardCamera;
        private Transform billboardCameraTransform;
        private bool prepared;
        private WarningAnchorKind anchorKind;

        public bool IsPrepared => prepared;
        public WarningAnchorKind AnchorKind => anchorKind;
        public Camera BillboardCamera => billboardCamera;

        public static bool TryValidatePrefab(WarningView prefab, out string error)
        {
            if (prefab == null)
            {
                error = "Warning view prefab is required.";
                return false;
            }

            if (prefab.GetComponentsInChildren<Collider>(true).Length > 0)
            {
                error = "Warning view prefab must not contain a Collider.";
                return false;
            }

            if (prefab.GetComponentsInChildren<Rigidbody>(true).Length > 0)
            {
                error = "Warning view prefab must not contain a Rigidbody.";
                return false;
            }

            if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                error = "Warning view prefab must contain at least one Renderer.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryPrepare(out string error)
        {
            return TryPrepare(null, out error);
        }

        public bool TryPrepare(Camera nextBillboardCamera, out string error)
        {
            if (prepared)
            {
                return TrySetBillboardCamera(nextBillboardCamera, out error);
            }

            if (!TryValidatePrefab(this, out error))
            {
                return false;
            }

            cachedTransform = transform;
            renderers = GetComponentsInChildren<Renderer>(true);
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            propertyBlock = new MaterialPropertyBlock();
            prepared = true;
            if (!TrySetBillboardCamera(nextBillboardCamera, out error))
            {
                prepared = false;
                return false;
            }

            Deactivate();
            return true;
        }

        /// <summary>
        /// Changes only the visual camera used by an EnemyWeakpoint warning.
        /// Ground warnings do not need a camera and retain their floor pose.
        /// </summary>
        public bool TrySetBillboardCamera(Camera nextBillboardCamera, out string error)
        {
            billboardCamera = nextBillboardCamera;
            billboardCameraTransform = nextBillboardCamera == null
                ? null
                : nextBillboardCamera.transform;
            if (prepared && gameObject.activeSelf
                && anchorKind == WarningAnchorKind.EnemyWeakpoint)
            {
                ApplyWeakpointBillboardPose(cachedTransform.position);
            }

            error = string.Empty;
            return true;
        }

        public void Activate(
            in ThreatSnapshot snapshot,
            Vector3 position,
            Color tint,
            WarningAnchorKind nextAnchorKind)
        {
            if (!prepared)
            {
                return;
            }

            gameObject.SetActive(true);
            SetState(snapshot, position, tint, nextAnchorKind);
        }

        public void SetState(
            in ThreatSnapshot snapshot,
            Vector3 position,
            Color tint,
            WarningAnchorKind nextAnchorKind)
        {
            if (!prepared)
            {
                return;
            }

            anchorKind = nextAnchorKind;
            bool enemyWeakpoint = anchorKind == WarningAnchorKind.EnemyWeakpoint;
            if (enemyWeakpoint)
            {
                ApplyWeakpointBillboardPose(position);
            }
            else
            {
                cachedTransform.position = position;
                cachedTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            bool windup = snapshot.State == ThreatState.Windup;
            cachedTransform.localScale = Vector3.one * (enemyWeakpoint
                ? (windup ? 1.65f : 1.35f)
                : (windup ? 2.6f : 2.1f));
            Color nextColor = tint;
            nextColor.a = Mathf.Clamp01(nextColor.a <= 0f ? 0.6f : nextColor.a);
            if (windup)
            {
                nextColor = Color.Lerp(nextColor, Color.red, 0.55f);
                nextColor.a = Mathf.Max(nextColor.a, 0.82f);
            }

            if (enemyWeakpoint)
            {
                // The heavy warning is an explicit weakpoint callout, not a
                // floor telegraph. Its placement and orientation remain
                // presentation-only and never alter the threat snapshot.
                nextColor = Color.Lerp(nextColor, new Color(1f, 0.9f, 0.18f, 1f), 0.35f);
                nextColor.a = Mathf.Max(nextColor.a, windup ? 0.9f : 0.74f);
            }

            SetColor(nextColor);
        }

        public void Deactivate()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void SetColor(Color color)
        {
            for (int index = 0; index < spriteRenderers.Length; index++)
            {
                spriteRenderers[index].color = color;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer is SpriteRenderer)
                {
                    continue;
                }

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private Quaternion ResolveWeakpointBillboardRotation(Vector3 position)
        {
            if (billboardCamera == null || billboardCameraTransform == null)
            {
                return Quaternion.identity;
            }

            Vector3 toCamera = billboardCameraTransform.position - position;
            if (!IsFinite(toCamera) || toCamera.sqrMagnitude <= 0.000001f)
            {
                toCamera = -billboardCameraTransform.forward;
            }

            if (!IsFinite(toCamera) || toCamera.sqrMagnitude <= 0.000001f)
            {
                toCamera = Vector3.forward;
            }

            Vector3 forward = toCamera.normalized;
            Vector3 up = billboardCameraTransform.up;
            if (!IsFinite(up) || Vector3.Cross(forward, up).sqrMagnitude <= 0.000001f)
            {
                up = Vector3.up;
            }

            if (Vector3.Cross(forward, up).sqrMagnitude <= 0.000001f)
            {
                up = Vector3.right;
            }

            return Quaternion.LookRotation(forward, up);
        }

        private void ApplyWeakpointBillboardPose(Vector3 position)
        {
            Quaternion billboardRotation = ResolveWeakpointBillboardRotation(position);
            cachedTransform.position = position;
            cachedTransform.rotation = billboardRotation;

            // Warning prefabs may use either a root SpriteRenderer or a
            // horizontal child renderer. Force child sprites into the same
            // world pose so the heavy warning is legible from the gameplay
            // camera regardless of the authored prefab hierarchy.
            for (int index = 0; index < spriteRenderers.Length; index++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[index];
                if (spriteRenderer != null && spriteRenderer.transform != cachedTransform)
                {
                    spriteRenderer.transform.rotation = billboardRotation;
                }
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
