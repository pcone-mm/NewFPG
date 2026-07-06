using UnityEngine;

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    public sealed class BattleArenaZoneMap : MonoBehaviour
    {
        public const string LeftFrontZoneId = "left_front";
        public const string CenterFrontZoneId = "center_front";
        public const string RightFrontZoneId = "right_front";
        public const string LeftMidZoneId = "left_mid";
        public const string CenterMidZoneId = "center_mid";
        public const string RightMidZoneId = "right_mid";
        public const string LeftBackZoneId = "left_back";
        public const string CenterBackZoneId = "center_back";
        public const string RightBackZoneId = "right_back";

        private const float MinArenaSize = 0.3f;
        private const float MinZoneSize = 0.05f;
        private const float DefaultFirstSplit = 1f / 3f;
        private const float DefaultSecondSplit = 2f / 3f;

        private static BattleArenaZoneMap current;

        [SerializeField] private Vector2 arenaSize = new Vector2(12f, 8f);
        [SerializeField] private Vector3 centerOffset;
        [SerializeField] private Vector2 columnSplits = new Vector2(DefaultFirstSplit, DefaultSecondSplit);
        [SerializeField] private Vector2 rowSplits = new Vector2(DefaultFirstSplit, DefaultSecondSplit);
        [SerializeField, Min(0f)] private float zonePadding = 0.2f;
        [SerializeField, Min(1)] private int sampleAttempts = 16;
        [SerializeField] private LayerMask occupancyMask = ~0;

        public static BattleArenaZoneMap Current
        {
            get
            {
                if (current != null && current.isActiveAndEnabled)
                {
                    return current;
                }

                current = FindFirstObjectByType<BattleArenaZoneMap>(FindObjectsInactive.Exclude);
                return current;
            }
        }

        public Vector2 ArenaSize => arenaSize;
        public Vector3 CenterOffset => centerOffset;
        public Vector2 ColumnSplits => SanitizedSplits(columnSplits, SanitizedArenaSize().x);
        public Vector2 RowSplits => SanitizedSplits(rowSplits, SanitizedArenaSize().y);
        public float ZonePadding => zonePadding;
        public int SampleAttempts => Mathf.Max(1, sampleAttempts);
        public LayerMask OccupancyMask => occupancyMask;

        public static void SetCurrent(BattleArenaZoneMap zoneMap)
        {
            current = zoneMap != null && zoneMap.isActiveAndEnabled ? zoneMap : null;
        }

        public static void ClearCurrent(BattleArenaZoneMap zoneMap)
        {
            if (current == zoneMap)
            {
                current = null;
            }
        }

        public static string NormalizeZoneId(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return CenterMidZoneId;
            }

            return zoneId.Trim().ToLowerInvariant()
                .Replace('-', '_')
                .Replace(' ', '_')
                .Replace('.', '_');
        }

        public bool TryGetZoneRect(string zoneId, out Rect localRect)
        {
            localRect = default;
            if (!TryResolveZoneIndices(zoneId, out int column, out int row))
            {
                return false;
            }

            Vector2 size = SanitizedArenaSize();
            Vector2 columns = SanitizedSplits(columnSplits, size.x);
            Vector2 rows = SanitizedSplits(rowSplits, size.y);
            float rawXMin = GetEdge(size.x, columns, column);
            float rawXMax = GetEdge(size.x, columns, column + 1);
            float rawZMin = GetEdge(size.y, rows, row);
            float rawZMax = GetEdge(size.y, rows, row + 1);
            float cellWidth = rawXMax - rawXMin;
            float cellDepth = rawZMax - rawZMin;
            float paddingX = Mathf.Min(Mathf.Max(0f, zonePadding), cellWidth * 0.5f - MinZoneSize * 0.5f);
            float paddingZ = Mathf.Min(Mathf.Max(0f, zonePadding), cellDepth * 0.5f - MinZoneSize * 0.5f);

            float xMin = rawXMin + paddingX;
            float xMax = rawXMax - paddingX;
            float zMin = rawZMin + paddingZ;
            float zMax = rawZMax - paddingZ;

            localRect = Rect.MinMaxRect(xMin, zMin, Mathf.Max(xMin + MinZoneSize, xMax), Mathf.Max(zMin + MinZoneSize, zMax));
            return true;
        }

        public bool TryGetZoneCenter(string zoneId, out Vector3 worldCenter)
        {
            worldCenter = default;
            if (!TryGetZoneRect(zoneId, out Rect localRect))
            {
                return false;
            }

            worldCenter = LocalXZToWorld(localRect.center);
            return true;
        }

        public bool TrySampleZonePoint(string zoneId, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (!TryGetZoneRect(zoneId, out Rect localRect))
            {
                return false;
            }

            Vector2 localXZ = new Vector2(
                Random.Range(localRect.xMin, localRect.xMax),
                Random.Range(localRect.yMin, localRect.yMax));
            worldPoint = LocalXZToWorld(localXZ);
            return true;
        }

        public bool ContainsWorldPoint(string zoneId, Vector3 worldPoint)
        {
            if (!TryGetZoneRect(zoneId, out Rect localRect))
            {
                return false;
            }

            Vector3 local = transform.InverseTransformPoint(worldPoint) - centerOffset;
            return local.x >= localRect.xMin
                && local.x <= localRect.xMax
                && local.z >= localRect.yMin
                && local.z <= localRect.yMax;
        }

        private void OnEnable()
        {
            if (current == null)
            {
                current = this;
            }
        }

        private void OnDisable()
        {
            ClearCurrent(this);
        }

        private void OnValidate()
        {
            arenaSize = SanitizedArenaSize();
            columnSplits = SanitizedSplits(columnSplits, arenaSize.x);
            rowSplits = SanitizedSplits(rowSplits, arenaSize.y);
            zonePadding = Mathf.Max(0f, zonePadding);
            sampleAttempts = Mathf.Max(1, sampleAttempts);
        }

        private Vector3 LocalXZToWorld(Vector2 localXZ)
        {
            Vector3 local = centerOffset + new Vector3(localXZ.x, 0f, localXZ.y);
            return transform.TransformPoint(local);
        }

        private Vector2 SanitizedArenaSize()
        {
            return new Vector2(
                Mathf.Max(MinArenaSize, arenaSize.x),
                Mathf.Max(MinArenaSize, arenaSize.y));
        }

        private static Vector2 SanitizedSplits(Vector2 splits, float totalSize)
        {
            totalSize = Mathf.Max(MinArenaSize, totalSize);
            float minGap = Mathf.Clamp(MinZoneSize / totalSize, 0.001f, 0.3f);
            if ((!IsFinite(splits.x) || !IsFinite(splits.y)) || splits.x <= 0f && splits.y <= 0f)
            {
                splits = new Vector2(DefaultFirstSplit, DefaultSecondSplit);
            }

            float first = Mathf.Clamp(splits.x, minGap, 1f - minGap * 2f);
            float second = Mathf.Clamp(splits.y, first + minGap, 1f - minGap);
            return new Vector2(first, second);
        }

        private static float GetEdge(float size, Vector2 splits, int edgeIndex)
        {
            float min = -size * 0.5f;
            switch (edgeIndex)
            {
                case 0:
                    return min;
                case 1:
                    return min + size * splits.x;
                case 2:
                    return min + size * splits.y;
                default:
                    return size * 0.5f;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryResolveZoneIndices(string zoneId, out int column, out int row)
        {
            column = 1;
            row = 1;

            switch (NormalizeZoneId(zoneId))
            {
                case LeftFrontZoneId:
                    column = 0;
                    row = 0;
                    return true;
                case CenterFrontZoneId:
                    column = 1;
                    row = 0;
                    return true;
                case RightFrontZoneId:
                    column = 2;
                    row = 0;
                    return true;
                case LeftMidZoneId:
                    column = 0;
                    row = 1;
                    return true;
                case CenterMidZoneId:
                    column = 1;
                    row = 1;
                    return true;
                case RightMidZoneId:
                    column = 2;
                    row = 1;
                    return true;
                case LeftBackZoneId:
                    column = 0;
                    row = 2;
                    return true;
                case CenterBackZoneId:
                    column = 1;
                    row = 2;
                    return true;
                case RightBackZoneId:
                    column = 2;
                    row = 2;
                    return true;
                default:
                    return false;
            }
        }

        private void OnDrawGizmos()
        {
            DrawGizmos(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmos(true);
        }

        private void DrawGizmos(bool selected)
        {
            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = selected ? new Color(0.2f, 0.85f, 1f, 0.9f) : new Color(0.2f, 0.85f, 1f, 0.45f);
            Vector2 size = SanitizedArenaSize();
            Vector3 center = centerOffset;
            Gizmos.DrawWireCube(center, new Vector3(size.x, 0.05f, size.y));

            Vector2 columns = SanitizedSplits(columnSplits, size.x);
            Vector2 rows = SanitizedSplits(rowSplits, size.y);
            for (int i = 1; i < 3; i++)
            {
                float x = GetEdge(size.x, columns, i);
                Gizmos.DrawLine(center + new Vector3(x, 0f, -size.y * 0.5f), center + new Vector3(x, 0f, size.y * 0.5f));

                float z = GetEdge(size.y, rows, i);
                Gizmos.DrawLine(center + new Vector3(-size.x * 0.5f, 0f, z), center + new Vector3(size.x * 0.5f, 0f, z));
            }

            Gizmos.matrix = previousMatrix;

#if UNITY_EDITOR
            if (selected)
            {
                DrawZoneLabels();
            }
#endif

            Gizmos.color = previousColor;
        }

#if UNITY_EDITOR
        private void DrawZoneLabels()
        {
            string[] ids =
            {
                LeftFrontZoneId,
                CenterFrontZoneId,
                RightFrontZoneId,
                LeftMidZoneId,
                CenterMidZoneId,
                RightMidZoneId,
                LeftBackZoneId,
                CenterBackZoneId,
                RightBackZoneId,
            };

            UnityEditor.Handles.color = new Color(0.2f, 0.85f, 1f, 0.9f);
            for (int i = 0; i < ids.Length; i++)
            {
                if (TryGetZoneCenter(ids[i], out Vector3 center))
                {
                    UnityEditor.Handles.Label(center + Vector3.up * 0.05f, ids[i]);
                }
            }
        }
#endif
    }
}
