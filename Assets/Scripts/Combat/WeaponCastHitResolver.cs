using NewFPG.Combat.SkillIndicators;
using UnityEngine;

namespace NewFPG.Combat
{
    public readonly struct WeaponCastHitResult
    {
        public WeaponCastHitResult(IDamageable damageable, Collider collider, Vector3 hitPoint)
        {
            Damageable = damageable;
            Collider = collider;
            HitPoint = hitPoint;
        }

        public IDamageable Damageable { get; }
        public Collider Collider { get; }
        public Vector3 HitPoint { get; }
    }

    public static class WeaponCastHitResolver
    {
        private const float MinRadius = 0.05f;
        private const float MinLength = 0.1f;
        private const float MinWidth = 0.05f;
        private const float MinHeight = 0.05f;

        public static Collider[] QueryCandidates(CastCommandData command, LayerMask targetMask)
        {
            float height = ResolveHeight(command);
            Vector3 up = Vector3.up;
            switch (ResolveShapeType(command))
            {
                case SkillIndicatorShapeType.Line:
                case SkillIndicatorShapeType.Rectangle:
                {
                    Vector3 origin = ResolveShapeOrigin(command);
                    Vector3 direction = ResolveDirection(command);
                    float length = ResolveLength(command);
                    float width = ResolveWidth(command);
                    Vector3 center = ResolveVerticalQueryCenter(command, origin + direction * (length * 0.5f), height);
                    Vector3 halfExtents = new Vector3(width * 0.5f, height * 0.5f, length * 0.5f);
                    Quaternion rotation = Quaternion.LookRotation(direction, up);
                    return Physics.OverlapBox(center, halfExtents, rotation, targetMask, QueryTriggerInteraction.Collide);
                }
                case SkillIndicatorShapeType.Cone:
                {
                    Vector3 origin = ResolveShapeOrigin(command);
                    float length = ResolveLength(command);
                    Vector3 center = ResolveVerticalQueryCenter(command, origin, height);
                    return Physics.OverlapBox(
                        center,
                        new Vector3(length, height * 0.5f, length),
                        Quaternion.identity,
                        targetMask,
                        QueryTriggerInteraction.Collide);
                }
                default:
                {
                    float radius = ResolveRadius(command);
                    Vector3 center = ResolveVerticalQueryCenter(command, ResolveAreaCenter(command), height);
                    return Physics.OverlapBox(
                        center,
                        new Vector3(radius, height * 0.5f, radius),
                        Quaternion.identity,
                        targetMask,
                        QueryTriggerInteraction.Collide);
                }
            }
        }

        public static bool TryResolveHit(
            CastCommandData command,
            Collider candidate,
            LayerMask targetMask,
            Transform casterRoot,
            out WeaponCastHitResult hit)
        {
            hit = default;
            if (candidate == null || casterRoot != null && candidate.transform.IsChildOf(casterRoot))
            {
                return false;
            }

            if (!IsInLayerMask(candidate.gameObject.layer, targetMask))
            {
                return false;
            }

            IDamageable damageable = candidate.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive || !damageable.IsTargetable)
            {
                return false;
            }

            Bounds bounds = candidate.bounds;
            if (!OverlapsHeight(command, bounds))
            {
                return false;
            }

            HorizontalBounds horizontalBounds = HorizontalBounds.From(bounds);
            bool intersects;
            switch (ResolveShapeType(command))
            {
                case SkillIndicatorShapeType.Line:
                case SkillIndicatorShapeType.Rectangle:
                    intersects = RectangleIntersectsBounds(
                        ResolveShapeOrigin(command),
                        ResolveDirection(command),
                        ResolveLength(command),
                        ResolveWidth(command),
                        horizontalBounds);
                    break;
                case SkillIndicatorShapeType.Cone:
                    intersects = ConeIntersectsBounds(
                        ResolveShapeOrigin(command),
                        ResolveDirection(command),
                        ResolveLength(command),
                        ResolveAngle(command),
                        horizontalBounds);
                    break;
                default:
                    intersects = CircleIntersectsBounds(ResolveAreaCenter(command), ResolveRadius(command), horizontalBounds);
                    break;
            }

            if (!intersects)
            {
                return false;
            }

            Vector3 referencePoint = ResolveHitReferencePoint(command);
            Vector3 hitPoint = candidate.ClosestPoint(referencePoint);
            hit = new WeaponCastHitResult(damageable, candidate, hitPoint);
            return true;
        }

        public static bool TryResolveHit(
            CastCommandData command,
            Collider candidate,
            Transform casterRoot,
            out WeaponCastHitResult hit)
        {
            return TryResolveHit(command, candidate, ~0, casterRoot, out hit);
        }

        private static bool IsInLayerMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        private static bool OverlapsHeight(CastCommandData command, Bounds bounds)
        {
            float height = ResolveHeight(command);
            float minY;
            float maxY;
            if (command.PlacementMode == SkillIndicatorPlacementMode.GroundSurface)
            {
                minY = command.SceneOrigin.y;
                maxY = minY + height;
            }
            else
            {
                float centerY = ResolveAreaCenter(command).y;
                minY = centerY - height * 0.5f;
                maxY = centerY + height * 0.5f;
            }

            return bounds.max.y + 0.001f >= minY && bounds.min.y - 0.001f <= maxY;
        }

        private static bool CircleIntersectsBounds(Vector3 center, float radius, HorizontalBounds bounds)
        {
            float x = Mathf.Clamp(center.x, bounds.MinX, bounds.MaxX);
            float z = Mathf.Clamp(center.z, bounds.MinZ, bounds.MaxZ);
            float dx = center.x - x;
            float dz = center.z - z;
            return dx * dx + dz * dz <= radius * radius;
        }

        private static bool RectangleIntersectsBounds(
            Vector3 origin,
            Vector3 direction,
            float length,
            float width,
            HorizontalBounds bounds)
        {
            Vector2 forward = ToVector2(direction);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector2.up;
            }

            forward.Normalize();
            Vector2 right = new Vector2(forward.y, -forward.x);
            Vector2 basePoint = ToVector2(origin);
            Vector2 endPoint = basePoint + forward * length;
            float halfWidth = width * 0.5f;
            Vector2[] rectangle =
            {
                basePoint - right * halfWidth,
                basePoint + right * halfWidth,
                endPoint + right * halfWidth,
                endPoint - right * halfWidth,
            };

            return PolygonsOverlap(rectangle, bounds.Corners, forward, right);
        }

        private static bool ConeIntersectsBounds(
            Vector3 origin,
            Vector3 direction,
            float length,
            float angle,
            HorizontalBounds bounds)
        {
            if (angle >= 359.9f)
            {
                return CircleIntersectsBounds(origin, length, bounds);
            }

            Vector2 origin2 = ToVector2(origin);
            Vector2 direction2 = ToVector2(direction);
            if (direction2.sqrMagnitude <= 0.0001f)
            {
                direction2 = Vector2.up;
            }

            direction2.Normalize();
            Vector2[] corners = bounds.Corners;
            for (int i = 0; i < corners.Length; i++)
            {
                if (PointInCone(corners[i], origin2, direction2, length, angle))
                {
                    return true;
                }
            }

            if (bounds.Contains(origin2))
            {
                return true;
            }

            float halfAngle = Mathf.Clamp(angle, 1f, 360f) * 0.5f;
            Vector2 leftEnd = origin2 + Rotate(direction2, -halfAngle) * length;
            Vector2 rightEnd = origin2 + Rotate(direction2, halfAngle) * length;
            if (SegmentIntersectsBounds(origin2, leftEnd, bounds) || SegmentIntersectsBounds(origin2, rightEnd, bounds))
            {
                return true;
            }

            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 a = corners[i];
                Vector2 b = corners[(i + 1) % corners.Length];
                if (SegmentIntersectsCone(a, b, origin2, direction2, length, angle))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SegmentIntersectsCone(
            Vector2 a,
            Vector2 b,
            Vector2 origin,
            Vector2 direction,
            float length,
            float angle)
        {
            if (PointInCone(a, origin, direction, length, angle) || PointInCone(b, origin, direction, length, angle))
            {
                return true;
            }

            Vector2 segment = b - a;
            float segmentLengthSqr = segment.sqrMagnitude;
            if (segmentLengthSqr <= 0.0001f)
            {
                return false;
            }

            float radiusSqr = length * length;
            Vector2 toStart = a - origin;
            float aCoef = Vector2.Dot(segment, segment);
            float bCoef = 2f * Vector2.Dot(toStart, segment);
            float cCoef = Vector2.Dot(toStart, toStart) - radiusSqr;
            float discriminant = bCoef * bCoef - 4f * aCoef * cCoef;
            if (discriminant >= 0f)
            {
                float sqrt = Mathf.Sqrt(discriminant);
                float t1 = (-bCoef - sqrt) / (2f * aCoef);
                float t2 = (-bCoef + sqrt) / (2f * aCoef);
                if (IntersectionPointInCone(a, segment, t1, origin, direction, length, angle)
                    || IntersectionPointInCone(a, segment, t2, origin, direction, length, angle))
                {
                    return true;
                }
            }

            float halfAngle = Mathf.Clamp(angle, 1f, 360f) * 0.5f;
            return SegmentsIntersect(a, b, origin, origin + Rotate(direction, -halfAngle) * length)
                || SegmentsIntersect(a, b, origin, origin + Rotate(direction, halfAngle) * length);
        }

        private static bool IntersectionPointInCone(
            Vector2 start,
            Vector2 segment,
            float t,
            Vector2 origin,
            Vector2 direction,
            float length,
            float angle)
        {
            return t >= 0f && t <= 1f && PointInCone(start + segment * t, origin, direction, length, angle);
        }

        private static bool PointInCone(Vector2 point, Vector2 origin, Vector2 direction, float length, float angle)
        {
            Vector2 toPoint = point - origin;
            if (toPoint.sqrMagnitude > length * length)
            {
                return false;
            }

            if (toPoint.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            float signedAngle = Vector2.SignedAngle(direction, toPoint.normalized);
            return Mathf.Abs(signedAngle) <= Mathf.Clamp(angle, 1f, 360f) * 0.5f;
        }

        private static bool PolygonsOverlap(Vector2[] first, Vector2[] second, Vector2 forward, Vector2 right)
        {
            return AxisOverlaps(first, second, Vector2.right)
                && AxisOverlaps(first, second, Vector2.up)
                && AxisOverlaps(first, second, forward)
                && AxisOverlaps(first, second, right);
        }

        private static bool AxisOverlaps(Vector2[] first, Vector2[] second, Vector2 axis)
        {
            Project(first, axis, out float minFirst, out float maxFirst);
            Project(second, axis, out float minSecond, out float maxSecond);
            return maxFirst + 0.0001f >= minSecond && maxSecond + 0.0001f >= minFirst;
        }

        private static void Project(Vector2[] points, Vector2 axis, out float min, out float max)
        {
            min = Vector2.Dot(points[0], axis);
            max = min;
            for (int i = 1; i < points.Length; i++)
            {
                float projection = Vector2.Dot(points[i], axis);
                min = Mathf.Min(min, projection);
                max = Mathf.Max(max, projection);
            }
        }

        private static bool SegmentIntersectsBounds(Vector2 a, Vector2 b, HorizontalBounds bounds)
        {
            if (bounds.Contains(a) || bounds.Contains(b))
            {
                return true;
            }

            Vector2[] corners = bounds.Corners;
            for (int i = 0; i < corners.Length; i++)
            {
                if (SegmentsIntersect(a, b, corners[i], corners[(i + 1) % corners.Length]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float denominator = Cross(b - a, d - c);
            float numerator = Cross(c - a, b - a);
            if (Mathf.Abs(denominator) <= 0.0001f)
            {
                return Mathf.Abs(numerator) <= 0.0001f
                    && RangesOverlap(a.x, b.x, c.x, d.x)
                    && RangesOverlap(a.y, b.y, c.y, d.y);
            }

            float t = Cross(c - a, d - c) / denominator;
            float u = numerator / denominator;
            return t >= -0.0001f && t <= 1.0001f && u >= -0.0001f && u <= 1.0001f;
        }

        private static bool RangesOverlap(float a, float b, float c, float d)
        {
            if (a > b)
            {
                (a, b) = (b, a);
            }

            if (c > d)
            {
                (c, d) = (d, c);
            }

            return b + 0.0001f >= c && d + 0.0001f >= a;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static Vector2 Rotate(Vector2 value, float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
        }

        private static Vector3 ResolveAreaCenter(CastCommandData command)
        {
            if (ResolveShapeType(command) == SkillIndicatorShapeType.TargetReticle
                || ResolveShapeType(command) == SkillIndicatorShapeType.GroundCircle)
            {
                return command.HasTargetPoint ? command.TargetPoint : ResolveShapeOrigin(command);
            }

            return ResolveShapeOrigin(command);
        }

        private static Vector3 ResolveHitReferencePoint(CastCommandData command)
        {
            switch (ResolveShapeType(command))
            {
                case SkillIndicatorShapeType.Line:
                case SkillIndicatorShapeType.Rectangle:
                    return ResolveShapeOrigin(command) + ResolveDirection(command) * (ResolveLength(command) * 0.5f);
                case SkillIndicatorShapeType.Cone:
                    return ResolveShapeOrigin(command) + ResolveDirection(command) * (ResolveLength(command) * 0.5f);
                default:
                    return ResolveAreaCenter(command);
            }
        }

        private static Vector3 ResolveVerticalQueryCenter(CastCommandData command, Vector3 center, float height)
        {
            return command.PlacementMode == SkillIndicatorPlacementMode.GroundSurface
                ? center + Vector3.up * (height * 0.5f)
                : center;
        }

        private static Vector3 ResolveShapeOrigin(CastCommandData command)
        {
            return command.PlacementMode == SkillIndicatorPlacementMode.GroundSurface
                ? command.SceneOrigin
                : command.Origin;
        }

        private static SkillIndicatorShapeType ResolveShapeType(CastCommandData command)
        {
            return command.ShapeType == SkillIndicatorShapeType.None
                ? SkillIndicatorShapeType.GroundCircle
                : command.ShapeType;
        }

        private static Vector3 ResolveDirection(CastCommandData command)
        {
            Vector3 direction = command.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f && command.HasTargetPoint)
            {
                direction = command.TargetPoint - ResolveShapeOrigin(command);
                direction.y = 0f;
            }

            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        private static float ResolveRadius(CastCommandData command)
        {
            return Mathf.Max(MinRadius, command.Radius);
        }

        private static float ResolveWidth(CastCommandData command)
        {
            return Mathf.Max(MinWidth, command.Width);
        }

        private static float ResolveLength(CastCommandData command)
        {
            return Mathf.Max(MinLength, command.Length);
        }

        private static float ResolveAngle(CastCommandData command)
        {
            return Mathf.Clamp(command.Angle > 0f ? command.Angle : 90f, 1f, 360f);
        }

        private static float ResolveHeight(CastCommandData command)
        {
            return Mathf.Max(MinHeight, command.Height);
        }

        private static Vector2 ToVector2(Vector3 value)
        {
            return new Vector2(value.x, value.z);
        }

        private readonly struct HorizontalBounds
        {
            private HorizontalBounds(float minX, float maxX, float minZ, float maxZ)
            {
                MinX = minX;
                MaxX = maxX;
                MinZ = minZ;
                MaxZ = maxZ;
                Corners = new[]
                {
                    new Vector2(minX, minZ),
                    new Vector2(maxX, minZ),
                    new Vector2(maxX, maxZ),
                    new Vector2(minX, maxZ),
                };
            }

            public float MinX { get; }
            public float MaxX { get; }
            public float MinZ { get; }
            public float MaxZ { get; }
            public Vector2[] Corners { get; }

            public static HorizontalBounds From(Bounds bounds)
            {
                return new HorizontalBounds(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z);
            }

            public bool Contains(Vector2 point)
            {
                return point.x >= MinX && point.x <= MaxX && point.y >= MinZ && point.y <= MaxZ;
            }
        }
    }
}
