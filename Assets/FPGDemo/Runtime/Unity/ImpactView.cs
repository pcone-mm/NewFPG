using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class ImpactView : MonoBehaviour
    {
        /// <summary>
        /// The normal depth nudge used by world-space feedback so its sprite
        /// does not z-fight with the surface that produced it.
        /// </summary>
        public const float DefaultCameraFacingOffset = 0.035f;

        private Transform cachedTransform;
        private Renderer[] renderers;
        private SpriteRenderer[] spriteRenderers;
        private LineRenderer feedbackShapeRenderer;
        private readonly Vector3[] feedbackShapePoints = new Vector3[5];
        private MaterialPropertyBlock propertyBlock;
        private Camera billboardCamera;
        private Transform billboardCameraTransform;
        private Color color;
        private Vector3 sourcePosition;
        private float initialScale;
        private float cameraFacingOffset;
        private CombatHitFeedbackShape feedbackShape;
        private bool prepared;

        public bool IsPrepared => prepared;
        public bool IsActive => prepared && gameObject.activeSelf;
        public Camera BillboardCamera => billboardCamera;
        public float CameraFacingOffset => cameraFacingOffset;

        public static bool TryValidatePrefab(ImpactView prefab, out string error)
        {
            if (prefab == null)
            {
                error = "Impact view prefab is required.";
                return false;
            }

            if (prefab.GetComponentsInChildren<Collider>(true).Length > 0)
            {
                error = "Impact view prefab must not contain a Collider.";
                return false;
            }

            if (prefab.GetComponentsInChildren<Rigidbody>(true).Length > 0)
            {
                error = "Impact view prefab must not contain a Rigidbody.";
                return false;
            }

            if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                error = "Impact view prefab must contain at least one Renderer.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Prepares this pooled view against an explicitly supplied gameplay
        /// camera. The effect must not use Camera.main because scene bootstrap
        /// owns the active camera reference and tests can create multiple cameras.
        /// </summary>
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
            if (!TryPrepareFeedbackShape(out error))
            {
                return false;
            }
            prepared = true;
            if (!TrySetBillboardCamera(nextBillboardCamera, out error))
            {
                prepared = false;
                return false;
            }

            Deactivate();
            return true;
        }

        public bool TrySetBillboardCamera(Camera nextBillboardCamera, out string error)
        {
            if (nextBillboardCamera == null)
            {
                error = "Impact view requires an explicitly supplied billboard camera.";
                return false;
            }

            billboardCamera = nextBillboardCamera;
            billboardCameraTransform = nextBillboardCamera.transform;
            if (IsActive)
            {
                ApplyBillboardPose();
            }

            error = string.Empty;
            return true;
        }

        public void Activate(
            Vector3 position,
            Color nextColor,
            float scale,
            float nextCameraFacingOffset = DefaultCameraFacingOffset)
        {
            Activate(
                position,
                nextColor,
                scale,
                nextCameraFacingOffset,
                CombatHitFeedbackShape.Burst);
        }

        public void Activate(
            Vector3 position,
            Color nextColor,
            float scale,
            float nextCameraFacingOffset,
            CombatHitFeedbackShape nextFeedbackShape)
        {
            if (!prepared)
            {
                return;
            }

            initialScale = Mathf.Max(0.01f, scale);
            color = nextColor;
            feedbackShape = nextFeedbackShape;
            sourcePosition = position;
            cameraFacingOffset = IsFinite(nextCameraFacingOffset)
                ? Mathf.Max(0f, nextCameraFacingOffset)
                : DefaultCameraFacingOffset;
            cachedTransform.localScale = Vector3.one * initialScale;
            gameObject.SetActive(true);
            ApplyBillboardPose();
            SetColor(color);
            SetFeedbackShape(color, initialScale);
        }

        public void SetLifetimeVisual(int elapsedTicks, int lifetimeTicks)
        {
            if (!IsActive)
            {
                return;
            }

            float progress = lifetimeTicks <= 0
                ? 1f
                : Mathf.Clamp01(elapsedTicks / (float)lifetimeTicks);
            cachedTransform.localScale = Vector3.one * initialScale * (1f + progress * 0.65f);
            ApplyBillboardPose();
            Color nextColor = color;
            nextColor.a = color.a * (1f - progress);
            SetColor(nextColor);
            SetFeedbackShape(nextColor, initialScale * (1f + progress * 0.65f));
        }

        public void Deactivate()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            if (feedbackShapeRenderer != null)
            {
                feedbackShapeRenderer.enabled = false;
            }
        }

        private void ApplyBillboardPose()
        {
            if (billboardCamera == null || billboardCameraTransform == null)
            {
                return;
            }

            Vector3 toCamera = billboardCameraTransform.position - sourcePosition;
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

            Quaternion billboardRotation = Quaternion.LookRotation(forward, up);
            // Do not let an intentionally larger surface offset pass through
            // a close third-person camera. This leaves a small near-clip gap
            // while still putting the normal greybox feedback in front of its
            // source surface.
            float maximumOffset = Mathf.Max(
                DefaultCameraFacingOffset,
                toCamera.magnitude - billboardCamera.nearClipPlane * 1.5f);
            float resolvedOffset = Mathf.Min(cameraFacingOffset, maximumOffset);
            cachedTransform.position = sourcePosition + forward * resolvedOffset;
            cachedTransform.rotation = billboardRotation;

            // Existing generated assets contain a horizontal child SpriteRenderer.
            // Set sprite children in world space so old and regenerated prefabs
            // both face the injected gameplay camera without relying on asset YAML.
            for (int index = 0; index < spriteRenderers.Length; index++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[index];
                if (spriteRenderer != null
                    && spriteRenderer.transform != cachedTransform)
                {
                    spriteRenderer.transform.rotation = billboardRotation;
                }
            }
        }

        private bool TryPrepareFeedbackShape(out string error)
        {
            feedbackShapeRenderer = GetComponent<LineRenderer>();
            if (feedbackShapeRenderer == null)
            {
                feedbackShapeRenderer = gameObject.AddComponent<LineRenderer>();
            }

            Material lineMaterial = renderers != null && renderers.Length > 0
                ? renderers[0].sharedMaterial
                : null;
            if (lineMaterial == null)
            {
                error = "Impact view requires a renderer material for hit-shape feedback.";
                return false;
            }

            feedbackShapeRenderer.sharedMaterial = lineMaterial;
            feedbackShapeRenderer.useWorldSpace = true;
            feedbackShapeRenderer.positionCount = feedbackShapePoints.Length;
            feedbackShapeRenderer.alignment = LineAlignment.View;
            feedbackShapeRenderer.numCapVertices = 1;
            feedbackShapeRenderer.numCornerVertices = 1;
            feedbackShapeRenderer.textureMode = LineTextureMode.Stretch;
            feedbackShapeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            feedbackShapeRenderer.receiveShadows = false;
            feedbackShapeRenderer.enabled = false;
            error = string.Empty;
            return true;
        }

        private void SetFeedbackShape(Color nextColor, float scale)
        {
            if (feedbackShapeRenderer == null || billboardCameraTransform == null)
            {
                return;
            }

            Vector3 right = billboardCameraTransform.right;
            Vector3 up = billboardCameraTransform.up;
            Vector3 shapeCenter = cachedTransform == null ? sourcePosition : cachedTransform.position;
            float size = Mathf.Max(0.02f, scale * 0.36f);
            switch (feedbackShape)
            {
                case CombatHitFeedbackShape.Diamond:
                    feedbackShapePoints[0] = shapeCenter + up * size;
                    feedbackShapePoints[1] = shapeCenter + right * size;
                    feedbackShapePoints[2] = shapeCenter - up * size;
                    feedbackShapePoints[3] = shapeCenter - right * size;
                    feedbackShapePoints[4] = feedbackShapePoints[0];
                    break;
                case CombatHitFeedbackShape.Shatter:
                    feedbackShapePoints[0] = shapeCenter - right * size;
                    feedbackShapePoints[1] = shapeCenter - up * (size * 0.35f);
                    feedbackShapePoints[2] = shapeCenter + right * (size * 0.15f);
                    feedbackShapePoints[3] = shapeCenter + up * (size * 0.62f);
                    feedbackShapePoints[4] = shapeCenter + right * size;
                    break;
                default:
                    feedbackShapePoints[0] = shapeCenter - right * size;
                    feedbackShapePoints[1] = shapeCenter + right * size;
                    feedbackShapePoints[2] = shapeCenter;
                    feedbackShapePoints[3] = shapeCenter - up * size;
                    feedbackShapePoints[4] = shapeCenter + up * size;
                    break;
            }

            feedbackShapeRenderer.startWidth = Mathf.Max(0.012f, size * 0.12f);
            feedbackShapeRenderer.endWidth = feedbackShapeRenderer.startWidth * 0.72f;
            feedbackShapeRenderer.startColor = nextColor;
            feedbackShapeRenderer.endColor = nextColor;
            feedbackShapeRenderer.SetPositions(feedbackShapePoints);
            feedbackShapeRenderer.enabled = true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void SetColor(Color nextColor)
        {
            for (int index = 0; index < spriteRenderers.Length; index++)
            {
                spriteRenderers[index].color = nextColor;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer is SpriteRenderer)
                {
                    continue;
                }

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", nextColor);
                propertyBlock.SetColor("_Color", nextColor);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
