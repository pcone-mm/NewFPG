using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NewFPG.Combat.SkillIndicators
{
    public static class SkillIndicatorAimSolver
    {
        private const string FirstPersonWeaponLayerName = "FirstPersonWeapon";
        private const string UILayerName = "UI";
        private const string IgnoreRaycastLayerName = "Ignore Raycast";
        private const float GroundSurfaceMinNormalY = 0.65f;

        public static SkillIndicatorPreviewFrame Resolve(
            SkillIndicatorResolvedConfig config,
            Transform owner,
            Transform castOrigin,
            Camera aimCamera,
            Vector2 pointerPosition,
            bool hasPointerPosition,
            float holdDuration,
            int localCastSequence)
        {
            Vector3 origin = castOrigin != null
                ? castOrigin.position
                : owner != null ? owner.position : Vector3.zero;
            Vector3 sceneOrigin = ResolveSceneOrigin(config, origin, owner, castOrigin);

            Vector3 direction = ResolveBaseDirection(config, owner, aimCamera, pointerPosition, hasPointerPosition);
            Vector3 targetPoint = ResolveTargetPoint(config, sceneOrigin, direction, aimCamera, pointerPosition, hasPointerPosition, owner, castOrigin, out Vector3 normal, out bool hitSurface);
            direction = ResolveDirectionFromPoints(config, sceneOrigin, targetPoint, direction);

            IndicatorValidationResult validation = Validate(config, sceneOrigin, targetPoint, hitSurface);
            CastCommandData command = new CastCommandData
            {
                AbilityId = config.abilityId,
                Origin = origin,
                SceneOrigin = sceneOrigin,
                Direction = direction,
                TargetPoint = targetPoint,
                SurfaceNormal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up,
                TargetEntityId = -1,
                PlacementMode = config.placementMode,
                HoldDuration = holdDuration,
                LocalCastSequence = localCastSequence,
                ShapeType = config.shapeType,
                Radius = config.radius,
                Width = config.width,
                Length = config.length,
                Angle = config.angle,
                Height = config.height,
                GroundOffset = config.groundOffset,
                HasTargetPoint = true,
                IsValid = validation.IsValid,
            };

            return new SkillIndicatorPreviewFrame
            {
                Command = command,
                Validation = validation,
                Config = config,
            };
        }

        public static bool TryReadCurrentPointerPosition(out Vector2 pointerPosition)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                pointerPosition = mouse.position.ReadValue();
                return true;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                pointerPosition = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            pointerPosition = Input.mousePosition;
            return true;
#else
            pointerPosition = Vector2.zero;
            return false;
#endif
        }

        private static Vector3 ResolveBaseDirection(
            SkillIndicatorResolvedConfig config,
            Transform owner,
            Camera aimCamera,
            Vector2 pointerPosition,
            bool hasPointerPosition)
        {
            if (config.aimSource == SkillIndicatorAimSource.Self
                || config.aimSource == SkillIndicatorAimSource.OwnerForward)
            {
                return owner != null && owner.forward.sqrMagnitude > 0.001f ? owner.forward.normalized : Vector3.forward;
            }

            Ray ray;
            if (TryBuildAimRay(config, aimCamera, pointerPosition, hasPointerPosition, out ray))
            {
                return ray.direction.sqrMagnitude > 0.001f ? ray.direction.normalized : Vector3.forward;
            }

            if (owner != null && owner.forward.sqrMagnitude > 0.001f)
            {
                return owner.forward.normalized;
            }

            return Vector3.forward;
        }

        private static Vector3 ResolveTargetPoint(
            SkillIndicatorResolvedConfig config,
            Vector3 origin,
            Vector3 direction,
            Camera aimCamera,
            Vector2 pointerPosition,
            bool hasPointerPosition,
            Transform owner,
            Transform castOrigin,
            out Vector3 normal,
            out bool hitSurface)
        {
            normal = Vector3.up;
            hitSurface = false;
            bool sticksToGround = config.SticksToGround();

            if (config.holdPolicy == SkillIndicatorDefaultReleasePolicy.CastOnSelf || config.aimSource == SkillIndicatorAimSource.Self)
            {
                hitSurface = true;
                return origin;
            }

            Ray ray;
            if (TryBuildAimRay(config, aimCamera, pointerPosition, hasPointerPosition, out ray)
                && TryResolveSurfaceHit(
                    config,
                    ray,
                    ResolveAimSurfaceRayDistance(config, ray, origin),
                    sticksToGround,
                    owner,
                    castOrigin,
                    out RaycastHit hit))
            {
                normal = sticksToGround ? Vector3.up : hit.normal.sqrMagnitude > 0.001f ? hit.normal.normalized : Vector3.up;
                hitSurface = true;
                return ClampPoint(origin, hit.point, config);
            }

            if (TryProjectRayToPlane(config, origin, aimCamera, pointerPosition, hasPointerPosition, out Vector3 planePoint))
            {
                return ClampPoint(origin, planePoint, config);
            }

            return ResolveFallbackTarget(origin, direction, config);
        }

        private static bool TryBuildAimRay(
            SkillIndicatorResolvedConfig config,
            Camera aimCamera,
            Vector2 pointerPosition,
            bool hasPointerPosition,
            out Ray ray)
        {
            Camera camera = aimCamera != null ? aimCamera : Camera.main;
            if (camera == null)
            {
                ray = default;
                return false;
            }

            if (config.aimSource == SkillIndicatorAimSource.ScreenCursorRay && hasPointerPosition)
            {
                ray = camera.ScreenPointToRay(pointerPosition);
                return true;
            }

            ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            return true;
        }

        private static bool TryProjectRayToPlane(
            SkillIndicatorResolvedConfig config,
            Vector3 origin,
            Camera aimCamera,
            Vector2 pointerPosition,
            bool hasPointerPosition,
            out Vector3 point)
        {
            point = origin;
            Ray ray;
            if (!TryBuildAimRay(config, aimCamera, pointerPosition, hasPointerPosition, out ray))
            {
                return false;
            }

            Plane plane = new Plane(Vector3.up, origin);
            if (!plane.Raycast(ray, out float distance) || distance < 0f)
            {
                return false;
            }

            point = ray.GetPoint(distance);
            return true;
        }

        private static Vector3 ClampPoint(Vector3 origin, Vector3 point, SkillIndicatorResolvedConfig config)
        {
            if (config.SticksToGround())
            {
                return ClampGroundPoint(origin, point, config);
            }

            if (!config.clampToRange)
            {
                return point;
            }

            Vector3 delta = point - origin;
            float range = Mathf.Max(0.1f, config.range);
            if (delta.sqrMagnitude <= range * range)
            {
                return point;
            }

            return origin + delta.normalized * range;
        }

        private static Vector3 ClampGroundPoint(Vector3 origin, Vector3 point, SkillIndicatorResolvedConfig config)
        {
            if (!config.clampToRange)
            {
                return point;
            }

            Vector3 delta = point - origin;
            delta.y = 0f;
            float range = Mathf.Max(0.1f, config.range);
            if (delta.sqrMagnitude <= range * range)
            {
                return point;
            }

            Vector3 clamped = origin + delta.normalized * range;
            return new Vector3(clamped.x, origin.y, clamped.z);
        }

        private static Vector3 ResolveFallbackTarget(Vector3 origin, Vector3 direction, SkillIndicatorResolvedConfig config)
        {
            float range = Mathf.Max(0.1f, config.range);
            if (config.SticksToGround())
            {
                Vector3 horizontalDirection = ResolveHorizontalDirection(direction, Vector3.forward);
                return origin + horizontalDirection * range;
            }

            Vector3 fallbackDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            return origin + fallbackDirection * range;
        }

        private static Vector3 ResolveSceneOrigin(SkillIndicatorResolvedConfig config, Vector3 origin, Transform owner, Transform castOrigin)
        {
            LayerMask sceneMask = ResolveSceneSurfaceMask(config.surfaceMask);
            Vector3 rayOrigin = origin + Vector3.up * 0.5f;
            float maxDistance = Mathf.Max(8f, Mathf.Max(config.range, config.height) + 8f);
            Ray ray = new Ray(rayOrigin, Vector3.down);
            if (TryResolveSurfaceHit(config, ray, maxDistance, config.SticksToGround(), owner, castOrigin, out RaycastHit hit))
            {
                return hit.point;
            }

            return config.SticksToGround() ? new Vector3(origin.x, 0f, origin.z) : origin;
        }

        private static float ResolveAimSurfaceRayDistance(SkillIndicatorResolvedConfig config, Ray ray, Vector3 sceneOrigin)
        {
            float originDistance = Vector3.Distance(ray.origin, sceneOrigin);
            float shapeDistance = Mathf.Max(config.range, config.length);
            return Mathf.Max(32f, originDistance + shapeDistance + Mathf.Max(2f, config.height) + 8f);
        }

        public static LayerMask ResolveSceneSurfaceMask(LayerMask configuredMask)
        {
            int mask = configuredMask.value;
            mask = ExcludeLayer(mask, FirstPersonWeaponLayerName);
            mask = ExcludeLayer(mask, UILayerName);
            mask = ExcludeLayer(mask, IgnoreRaycastLayerName);

            return new LayerMask { value = mask };
        }

        private static int ExcludeLayer(int mask, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? mask & ~(1 << layer) : mask;
        }

        private static bool TryResolveSurfaceHit(
            SkillIndicatorResolvedConfig config,
            Ray ray,
            float maxDistance,
            bool requireGroundSurface,
            Transform owner,
            Transform castOrigin,
            out RaycastHit resolvedHit)
        {
            resolvedHit = default;
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                maxDistance,
                ResolveSceneSurfaceMask(config.surfaceMask),
                QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, CompareRaycastHitDistance);
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (!IsValidSurfaceHit(hit, requireGroundSurface, owner, castOrigin))
                {
                    continue;
                }

                resolvedHit = hit;
                return true;
            }

            return false;
        }

        private static int CompareRaycastHitDistance(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }

        private static bool IsValidSurfaceHit(RaycastHit hit, bool requireGroundSurface, Transform owner, Transform castOrigin)
        {
            Collider collider = hit.collider;
            if (collider == null)
            {
                return false;
            }

            Transform hitTransform = collider.transform;
            if (hitTransform == null)
            {
                return false;
            }

            if (IsTransformWithin(hitTransform, owner) || IsTransformWithin(hitTransform, castOrigin))
            {
                return false;
            }

            if (requireGroundSurface)
            {
                if (!IsGroundSurfaceNormal(hit.normal))
                {
                    return false;
                }

                if (collider.GetComponentInParent<IDamageable>() != null
                    || collider.GetComponentInParent<PlayerWeaponCaster>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsTransformWithin(Transform candidate, Transform root)
        {
            return candidate != null && root != null && (candidate == root || candidate.IsChildOf(root));
        }

        private static bool IsGroundSurfaceNormal(Vector3 normal)
        {
            return normal.sqrMagnitude > 0.001f && normal.normalized.y >= GroundSurfaceMinNormalY;
        }

        private static Vector3 ResolveDirectionFromPoints(
            SkillIndicatorResolvedConfig config,
            Vector3 origin,
            Vector3 targetPoint,
            Vector3 fallback)
        {
            Vector3 direction = targetPoint - origin;
            if (config.SticksToGround())
            {
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.001f)
                {
                    return direction.normalized;
                }

                return ResolveHorizontalDirection(fallback, Vector3.forward);
            }

            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }

            return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.forward;
        }

        private static Vector3 ResolveHorizontalDirection(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }

            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.forward;
        }

        private static IndicatorValidationResult Validate(
            SkillIndicatorResolvedConfig config,
            Vector3 origin,
            Vector3 targetPoint,
            bool hitSurface)
        {
            if (config.requireSurfaceHit && !hitSurface)
            {
                return IndicatorValidationResult.Invalid(SkillIndicatorValidationReason.NoSurface, "No valid surface.");
            }

            float range = Mathf.Max(0.1f, config.range);
            Vector3 delta = targetPoint - origin;
            if (config.SticksToGround())
            {
                delta.y = 0f;
            }

            if (delta.sqrMagnitude > range * range + 0.01f)
            {
                return IndicatorValidationResult.Invalid(SkillIndicatorValidationReason.OutOfRange, "Target is out of range.");
            }

            return IndicatorValidationResult.Valid();
        }
    }
}
