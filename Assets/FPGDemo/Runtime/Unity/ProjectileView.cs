using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class ProjectileView : MonoBehaviour
    {
        private const int InterceptableVolleyPresentationKey = 2;
        private const float InterceptableVolleyLaneSpacing = 0.12f;
        private const float InterceptableMarkerRadius = 0.78f;
        private const float InterceptableMarkerWidth = 0.055f;

        private static readonly Color TravelColor = new Color(1f, 0.32f, 0.08f, 1f);
        private static readonly Color InterceptableColor = new Color(1f, 0.8f, 0.25f, 1f);
        private static readonly Color InterceptableMarkerColor = new Color(0.55f, 0.92f, 1f, 0.95f);
        private static readonly Color InterceptedColor = new Color(0.45f, 0.85f, 1f, 1f);
        private static readonly Color BlockedColor = new Color(1f, 0.55f, 0.12f, 1f);
        private static readonly Color ExpiredColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Vector3[] InterceptableMarkerPoints =
        {
            new Vector3(0f, InterceptableMarkerRadius, 0f),
            new Vector3(InterceptableMarkerRadius, 0f, 0f),
            new Vector3(0f, -InterceptableMarkerRadius, 0f),
            new Vector3(-InterceptableMarkerRadius, 0f, 0f),
        };

        private Transform cachedTransform;
        private Renderer[] renderers;
        private SpriteRenderer[] spriteRenderers;
        private MaterialPropertyBlock propertyBlock;
        private MaterialPropertyBlock interceptableMarkerPropertyBlock;
        private LineRenderer interceptableMarker;
        private Camera billboardCamera;
        private Transform billboardCameraTransform;
        private Vector3 sourcePosition;
        private Vector3 initialLocalScale;
        private int volleyLane;
        private bool isInterceptableVolley;
        private bool prepared;

        public bool IsPrepared => prepared;
        public Camera BillboardCamera => billboardCamera;
        public Vector3 LogicalPosition => sourcePosition;
        public Vector3 VisualPosition => cachedTransform == null ? sourcePosition : cachedTransform.position;
        public int VolleyLane => volleyLane;
        public bool ShowsInterceptableMarker => interceptableMarker != null
            && interceptableMarker.gameObject.activeSelf;

        public static bool TryValidatePrefab(ProjectileView prefab, out string error)
        {
            if (prefab == null)
            {
                error = "Projectile view prefab is required.";
                return false;
            }

            if (prefab.GetComponentsInChildren<Collider>(true).Length > 0)
            {
                error = "Projectile view prefab must not contain a Collider.";
                return false;
            }

            if (prefab.GetComponentsInChildren<Rigidbody>(true).Length > 0)
            {
                error = "Projectile view prefab must not contain a Rigidbody.";
                return false;
            }

            if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                error = "Projectile view prefab must contain at least one Renderer.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Prepares this pooled projectile against the explicitly supplied
        /// gameplay camera. Projectile sprites must not rely on their prefab
        /// world orientation, because a shoulder camera can orbit freely.
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
            interceptableMarkerPropertyBlock = new MaterialPropertyBlock();
            initialLocalScale = cachedTransform.localScale;
            EnsureInterceptableMarker();
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
                error = "Projectile view requires an explicitly supplied billboard camera.";
                return false;
            }

            billboardCamera = nextBillboardCamera;
            billboardCameraTransform = nextBillboardCamera.transform;
            if (prepared && gameObject.activeSelf)
            {
                ApplyBillboardPose();
            }

            error = string.Empty;
            return true;
        }

        public void Activate(in ProjectilePresentationState state, Vector3 position)
        {
            if (!prepared)
            {
                return;
            }

            sourcePosition = position;
            isInterceptableVolley = state.Request.Interceptable
                && state.Request.PresentationKey == InterceptableVolleyPresentationKey;
            volleyLane = isInterceptableVolley
                ? ResolveInterceptableVolleyLane(state.Request.RuntimeId)
                : 0;
            cachedTransform.localScale = initialLocalScale;
            gameObject.SetActive(true);
            SetPosition(position);
            SetColor(state.Request.Interceptable ? InterceptableColor : TravelColor);
            SetInterceptableMarkerVisible(isInterceptableVolley);
        }

        public void SetPosition(Vector3 position)
        {
            if (prepared)
            {
                sourcePosition = position;
                ApplyBillboardPose();
            }
        }

        public void SetTerminalVisual(ProjectileTerminalReason reason)
        {
            if (!prepared)
            {
                return;
            }

            cachedTransform.localScale = initialLocalScale * 1.35f;
            switch (reason)
            {
                case ProjectileTerminalReason.Intercepted:
                    SetColor(InterceptedColor);
                    break;
                case ProjectileTerminalReason.EnvironmentBlocked:
                    SetColor(BlockedColor);
                    break;
                case ProjectileTerminalReason.Missed:
                case ProjectileTerminalReason.LifetimeExpired:
                case ProjectileTerminalReason.OwnerCanceled:
                case ProjectileTerminalReason.SessionEnded:
                    SetColor(ExpiredColor);
                    break;
                default:
                    SetColor(TravelColor);
                    break;
            }
        }

        public void Deactivate()
        {
            SetInterceptableMarkerVisible(false);
            isInterceptableVolley = false;
            volleyLane = 0;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        public static int ResolveInterceptableVolleyLane(RuntimeId runtimeId)
        {
            if (!runtimeId.IsValid)
            {
                return 0;
            }

            long modulo = runtimeId.Value % 3L;
            if (modulo < 0L)
            {
                modulo += 3L;
            }

            return (int)modulo - 1;
        }

        private void ApplyBillboardPose()
        {
            if (billboardCamera == null || billboardCameraTransform == null)
            {
                return;
            }

            Vector3 visualPosition = ResolveVisualPosition();
            Vector3 toCamera = billboardCameraTransform.position - visualPosition;
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
            // The normal projectile root remains at its frozen simulation
            // position. Only the key-2 triple gets a stable camera-right lane
            // offset, so three same-path simulation projectiles stay legible
            // without changing their hit or collision truth.
            cachedTransform.position = visualPosition;
            cachedTransform.rotation = billboardRotation;

            // Support both the generated root-sprite prefab and older prefabs
            // which keep a horizontal sprite as a child below the effect root.
            for (int index = 0; index < spriteRenderers.Length; index++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[index];
                if (spriteRenderer != null && spriteRenderer.transform != cachedTransform)
                {
                    spriteRenderer.transform.rotation = billboardRotation;
                }
            }
        }

        private Vector3 ResolveVisualPosition()
        {
            if (!isInterceptableVolley || volleyLane == 0 || billboardCameraTransform == null)
            {
                return sourcePosition;
            }

            Vector3 cameraRight = billboardCameraTransform.right;
            if (!IsFinite(cameraRight) || cameraRight.sqrMagnitude <= 0.000001f)
            {
                return sourcePosition;
            }

            return sourcePosition + cameraRight.normalized
                * (volleyLane * InterceptableVolleyLaneSpacing);
        }

        private void EnsureInterceptableMarker()
        {
            if (interceptableMarker != null)
            {
                return;
            }

            GameObject markerObject = new GameObject("InterceptableMarker");
            markerObject.layer = gameObject.layer;
            markerObject.transform.SetParent(cachedTransform, false);
            interceptableMarker = markerObject.AddComponent<LineRenderer>();
            interceptableMarker.useWorldSpace = false;
            interceptableMarker.loop = true;
            interceptableMarker.positionCount = InterceptableMarkerPoints.Length;
            interceptableMarker.SetPositions(InterceptableMarkerPoints);
            interceptableMarker.startWidth = InterceptableMarkerWidth;
            interceptableMarker.endWidth = InterceptableMarkerWidth;
            interceptableMarker.startColor = InterceptableMarkerColor;
            interceptableMarker.endColor = InterceptableMarkerColor;
            interceptableMarker.numCornerVertices = 2;
            interceptableMarker.numCapVertices = 2;
            interceptableMarker.alignment = LineAlignment.TransformZ;
            interceptableMarker.textureMode = LineTextureMode.Stretch;
            interceptableMarker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            interceptableMarker.receiveShadows = false;

            Material sourceMaterial = ResolveMarkerMaterial();
            if (sourceMaterial != null)
            {
                interceptableMarker.sharedMaterial = sourceMaterial;
                interceptableMarker.GetPropertyBlock(interceptableMarkerPropertyBlock);
                interceptableMarkerPropertyBlock.SetColor("_BaseColor", Color.white);
                interceptableMarkerPropertyBlock.SetColor("_Color", Color.white);
                interceptableMarker.SetPropertyBlock(interceptableMarkerPropertyBlock);
            }

            SetInterceptableMarkerVisible(false);
        }

        private Material ResolveMarkerMaterial()
        {
            if (spriteRenderers != null)
            {
                for (int index = 0; index < spriteRenderers.Length; index++)
                {
                    SpriteRenderer spriteRenderer = spriteRenderers[index];
                    if (spriteRenderer != null && spriteRenderer.sharedMaterial != null)
                    {
                        return spriteRenderer.sharedMaterial;
                    }
                }
            }

            if (renderers != null)
            {
                for (int index = 0; index < renderers.Length; index++)
                {
                    Renderer renderer = renderers[index];
                    if (renderer != null && renderer.sharedMaterial != null)
                    {
                        return renderer.sharedMaterial;
                    }
                }
            }

            return null;
        }

        private void SetInterceptableMarkerVisible(bool visible)
        {
            if (interceptableMarker != null && interceptableMarker.gameObject.activeSelf != visible)
            {
                interceptableMarker.gameObject.SetActive(visible);
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
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
    }
}
