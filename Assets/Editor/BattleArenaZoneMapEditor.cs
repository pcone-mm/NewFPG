using NewFPG.Combat;
using NewFPG.Level;
using UnityEditor;
using UnityEngine;

namespace NewFPG.EditorTools
{
    [CustomEditor(typeof(BattleArenaZoneMap))]
    public sealed class BattleArenaZoneMapEditor : UnityEditor.Editor
    {
        private const float MinArenaSize = 0.3f;
        private const float MinZoneSize = 0.05f;
        private const float DefaultAstarMargin = 1.4f;

        private static readonly string[] ZoneIds =
        {
            BattleArenaZoneMap.LeftFrontZoneId,
            BattleArenaZoneMap.CenterFrontZoneId,
            BattleArenaZoneMap.RightFrontZoneId,
            BattleArenaZoneMap.LeftMidZoneId,
            BattleArenaZoneMap.CenterMidZoneId,
            BattleArenaZoneMap.RightMidZoneId,
            BattleArenaZoneMap.LeftBackZoneId,
            BattleArenaZoneMap.CenterBackZoneId,
            BattleArenaZoneMap.RightBackZoneId,
        };

        private static readonly Color FillColor = new Color(0.2f, 0.85f, 1f, 0.08f);
        private static readonly Color OutlineColor = new Color(0.2f, 0.85f, 1f, 0.9f);
        private static readonly Color HandleColor = new Color(1f, 0.78f, 0.25f, 1f);

        private SerializedProperty arenaSize;
        private SerializedProperty centerOffset;
        private SerializedProperty columnSplits;
        private SerializedProperty rowSplits;
        private SerializedProperty zonePadding;
        private SerializedProperty sampleAttempts;
        private SerializedProperty occupancyMask;

        private void OnEnable()
        {
            arenaSize = serializedObject.FindProperty("arenaSize");
            centerOffset = serializedObject.FindProperty("centerOffset");
            columnSplits = serializedObject.FindProperty("columnSplits");
            rowSplits = serializedObject.FindProperty("rowSplits");
            zonePadding = serializedObject.FindProperty("zonePadding");
            sampleAttempts = serializedObject.FindProperty("sampleAttempts");
            occupancyMask = serializedObject.FindProperty("occupancyMask");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(arenaSize);
            EditorGUILayout.PropertyField(centerOffset);
            EditorGUILayout.PropertyField(columnSplits, new GUIContent("Column Dividers"));
            EditorGUILayout.PropertyField(rowSplits, new GUIContent("Row Dividers"));
            EditorGUILayout.PropertyField(zonePadding);
            EditorGUILayout.PropertyField(sampleAttempts);
            EditorGUILayout.PropertyField(occupancyMask);

            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Battle Zone IDs", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("front", "left_front / center_front / right_front");
                EditorGUILayout.LabelField("mid", "left_mid / center_mid / right_mid");
                EditorGUILayout.LabelField("back", "left_back / center_back / right_back");
                EditorGUILayout.HelpBox(
                    "怪物行为树节点“移动到战斗区域”使用这些 ID。最终点仍会投影到 A* 图上，并且必须可达。",
                    MessageType.Info);

                if (GUILayout.Button("Frame Battle Arena"))
                {
                    FrameSelectedArena();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            BattleArenaZoneMap zoneMap = (BattleArenaZoneMap)target;
            serializedObject.Update();

            Vector2 size = ClampArenaSize(arenaSize.vector2Value);
            Vector3 offset = centerOffset.vector3Value;
            Vector2 columns = ClampSplits(columnSplits.vector2Value, size.x);
            Vector2 rows = ClampSplits(rowSplits.vector2Value, size.y);
            Transform mapTransform = zoneMap.transform;
            Quaternion rotation = mapTransform.rotation;
            Vector3 worldCenter = mapTransform.TransformPoint(offset);
            float handleSize = HandleUtility.GetHandleSize(worldCenter);

            DrawZones(zoneMap);

            Handles.color = HandleColor;
            EditorGUI.BeginChangeCheck();
            Vector3 adjustedWorldCenter = Handles.PositionHandle(worldCenter, rotation);
            Vector2 adjustedSize = DrawSizeHandles(mapTransform, offset, worldCenter, size, handleSize);
            Vector2 adjustedColumnSplits = DrawColumnSplitHandles(mapTransform, offset, worldCenter, size, columns, handleSize);
            Vector2 adjustedRowSplits = DrawRowSplitHandles(mapTransform, offset, worldCenter, size, rows, handleSize);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zoneMap, "Adjust Battle Arena Zone Map");
                centerOffset.vector3Value = mapTransform.InverseTransformPoint(adjustedWorldCenter);
                arenaSize.vector2Value = ClampArenaSize(adjustedSize);
                columnSplits.vector2Value = ClampSplits(adjustedColumnSplits, adjustedSize.x);
                rowSplits.vector2Value = ClampSplits(adjustedRowSplits, adjustedSize.y);
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(zoneMap);
            }
            else
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        [MenuItem("GameObject/NewFPG/Combat/Battle Arena Zone Map", false, 10)]
        private static void CreateBattleArenaZoneMap(MenuCommand command)
        {
            GameObject context = command.context as GameObject;
            LevelFlowDirector director = ResolveDirector(context != null ? context : Selection.activeGameObject);
            BattleArenaZoneMap zoneMap = director != null
                ? BattleArenaZoneMapEditorUtility.CreateForDirector(director)
                : BattleArenaZoneMapEditorUtility.CreateStandalone(context != null ? context.transform : null);

            Selection.activeObject = zoneMap.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static LevelFlowDirector ResolveDirector(GameObject context)
        {
            if (context == null)
            {
                return null;
            }

            LevelFlowDirector director = context.GetComponent<LevelFlowDirector>();
            if (director != null)
            {
                return director;
            }

            return context.GetComponentInParent<LevelFlowDirector>();
        }

        private void DrawZones(BattleArenaZoneMap zoneMap)
        {
            for (int i = 0; i < ZoneIds.Length; i++)
            {
                if (!zoneMap.TryGetZoneRect(ZoneIds[i], out Rect rect)
                    || !zoneMap.TryGetZoneCenter(ZoneIds[i], out Vector3 center))
                {
                    continue;
                }

                Vector3[] corners = ZoneCorners(zoneMap.transform, centerOffset.vector3Value, rect);
                Handles.DrawSolidRectangleWithOutline(corners, FillColor, OutlineColor);
                Handles.Label(center + Vector3.up * 0.08f, ZoneIds[i], EditorStyles.boldLabel);
            }
        }

        private static Vector3[] ZoneCorners(Transform mapTransform, Vector3 offset, Rect rect)
        {
            return new[]
            {
                mapTransform.TransformPoint(offset + new Vector3(rect.xMin, 0f, rect.yMin)),
                mapTransform.TransformPoint(offset + new Vector3(rect.xMin, 0f, rect.yMax)),
                mapTransform.TransformPoint(offset + new Vector3(rect.xMax, 0f, rect.yMax)),
                mapTransform.TransformPoint(offset + new Vector3(rect.xMax, 0f, rect.yMin)),
            };
        }

        private static Vector2 DrawSizeHandles(
            Transform mapTransform,
            Vector3 offset,
            Vector3 worldCenter,
            Vector2 size,
            float handleSize)
        {
            Vector2 adjustedSize = size;
            float halfX = size.x * 0.5f;
            float halfZ = size.y * 0.5f;
            float capSize = handleSize * 0.08f;

            adjustedSize.x = Mathf.Max(
                LocalHalfXFromSlider(mapTransform, offset, worldCenter, halfX, mapTransform.right, capSize),
                LocalHalfXFromSlider(mapTransform, offset, worldCenter, -halfX, -mapTransform.right, capSize)) * 2f;

            adjustedSize.y = Mathf.Max(
                LocalHalfZFromSlider(mapTransform, offset, worldCenter, halfZ, mapTransform.forward, capSize),
                LocalHalfZFromSlider(mapTransform, offset, worldCenter, -halfZ, -mapTransform.forward, capSize)) * 2f;

            return ClampArenaSize(adjustedSize);
        }

        private static Vector2 DrawColumnSplitHandles(
            Transform mapTransform,
            Vector3 offset,
            Vector3 worldCenter,
            Vector2 size,
            Vector2 splits,
            float handleSize)
        {
            Vector2 adjusted = splits;
            float halfX = size.x * 0.5f;
            float capSize = handleSize * 0.07f;

            adjusted.x = DrawColumnSplitHandle(mapTransform, offset, worldCenter, halfX, adjusted.x, capSize);
            adjusted.y = DrawColumnSplitHandle(mapTransform, offset, worldCenter, halfX, adjusted.y, capSize);
            return ClampSplits(adjusted, size.x);
        }

        private static float DrawColumnSplitHandle(
            Transform mapTransform,
            Vector3 offset,
            Vector3 worldCenter,
            float halfX,
            float split,
            float capSize)
        {
            float localX = -halfX + halfX * 2f * split;
            Vector3 worldPoint = mapTransform.TransformPoint(offset + new Vector3(localX, 0f, 0f));
            Vector3 moved = Handles.Slider(worldPoint, mapTransform.right, capSize, Handles.CubeHandleCap, 0f);
            Vector3 local = mapTransform.InverseTransformPoint(ProjectToArenaPlane(worldCenter, moved));
            return Mathf.InverseLerp(-halfX, halfX, local.x - offset.x);
        }

        private static Vector2 DrawRowSplitHandles(
            Transform mapTransform,
            Vector3 offset,
            Vector3 worldCenter,
            Vector2 size,
            Vector2 splits,
            float handleSize)
        {
            Vector2 adjusted = splits;
            float halfZ = size.y * 0.5f;
            float capSize = handleSize * 0.07f;

            adjusted.x = DrawRowSplitHandle(mapTransform, offset, worldCenter, halfZ, adjusted.x, capSize);
            adjusted.y = DrawRowSplitHandle(mapTransform, offset, worldCenter, halfZ, adjusted.y, capSize);
            return ClampSplits(adjusted, size.y);
        }

        private static float DrawRowSplitHandle(
            Transform mapTransform,
            Vector3 offset,
            Vector3 worldCenter,
            float halfZ,
            float split,
            float capSize)
        {
            float localZ = -halfZ + halfZ * 2f * split;
            Vector3 worldPoint = mapTransform.TransformPoint(offset + new Vector3(0f, 0f, localZ));
            Vector3 moved = Handles.Slider(worldPoint, mapTransform.forward, capSize, Handles.CubeHandleCap, 0f);
            Vector3 local = mapTransform.InverseTransformPoint(ProjectToArenaPlane(worldCenter, moved));
            return Mathf.InverseLerp(-halfZ, halfZ, local.z - offset.z);
        }

        private static float LocalHalfXFromSlider(
            Transform mapTransform,
            Vector3 offset,
            Vector3 worldCenter,
            float localX,
            Vector3 direction,
            float capSize)
        {
            Vector3 worldPoint = mapTransform.TransformPoint(offset + new Vector3(localX, 0f, 0f));
            Vector3 moved = Handles.Slider(worldPoint, direction, capSize, Handles.CubeHandleCap, 0f);
            Vector3 local = mapTransform.InverseTransformPoint(ProjectToArenaPlane(worldCenter, moved));
            return Mathf.Abs(local.x - offset.x);
        }

        private static float LocalHalfZFromSlider(
            Transform mapTransform,
            Vector3 offset,
            Vector3 worldCenter,
            float localZ,
            Vector3 direction,
            float capSize)
        {
            Vector3 worldPoint = mapTransform.TransformPoint(offset + new Vector3(0f, 0f, localZ));
            Vector3 moved = Handles.Slider(worldPoint, direction, capSize, Handles.CubeHandleCap, 0f);
            Vector3 local = mapTransform.InverseTransformPoint(ProjectToArenaPlane(worldCenter, moved));
            return Mathf.Abs(local.z - offset.z);
        }

        private static Vector3 ProjectToArenaPlane(Vector3 worldCenter, Vector3 worldPoint)
        {
            worldPoint.y = worldCenter.y;
            return worldPoint;
        }

        private void FrameSelectedArena()
        {
            BattleArenaZoneMap zoneMap = (BattleArenaZoneMap)target;
            Vector2 size = ClampArenaSize(arenaSize.vector2Value);
            Vector3 center = zoneMap.transform.TransformPoint(centerOffset.vector3Value);
            Bounds bounds = new Bounds(center, new Vector3(size.x, 1f, size.y));
            SceneView.lastActiveSceneView?.Frame(bounds, false);
        }

        private static Vector2 ClampArenaSize(Vector2 size)
        {
            return new Vector2(
                Mathf.Max(MinArenaSize, Mathf.Abs(size.x)),
                Mathf.Max(MinArenaSize, Mathf.Abs(size.y)));
        }

        private static Vector2 ClampSplits(Vector2 splits, float totalSize)
        {
            totalSize = Mathf.Max(MinArenaSize, Mathf.Abs(totalSize));
            float minGap = Mathf.Clamp(MinZoneSize / totalSize, 0.001f, 0.3f);
            if ((!IsFinite(splits.x) || !IsFinite(splits.y)) || splits.x <= 0f && splits.y <= 0f)
            {
                splits = new Vector2(1f / 3f, 2f / 3f);
            }

            float first = Mathf.Clamp(splits.x, minGap, 1f - minGap * 2f);
            float second = Mathf.Clamp(splits.y, first + minGap, 1f - minGap);
            return new Vector2(first, second);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static Vector2 SizeFromAstarBounds(Bounds bounds)
        {
            return ClampArenaSize(new Vector2(
                Mathf.Max(MinArenaSize, bounds.size.x - DefaultAstarMargin),
                Mathf.Max(MinArenaSize, bounds.size.z - DefaultAstarMargin)));
        }
    }

    internal static class BattleArenaZoneMapEditorUtility
    {
        public static BattleArenaZoneMap CreateForDirector(LevelFlowDirector director)
        {
            BattleArenaZoneMap existing = director != null
                ? director.GetComponentInChildren<BattleArenaZoneMap>(true)
                : null;
            if (existing != null)
            {
                AssignToDirector(director, existing);
                return existing;
            }

            GameObject zoneObject = new GameObject("BattleArenaZoneMap");
            Undo.RegisterCreatedObjectUndo(zoneObject, "Create Battle Arena Zone Map");
            zoneObject.transform.SetParent(director.transform, false);

            BattleArenaZoneMap zoneMap = zoneObject.AddComponent<BattleArenaZoneMap>();
            Bounds bounds = director.GetAstarGraphPreviewBounds();
            ApplyInitialBounds(zoneMap, bounds);
            AssignToDirector(director, zoneMap);
            return zoneMap;
        }

        public static BattleArenaZoneMap CreateStandalone(Transform parent)
        {
            GameObject zoneObject = new GameObject("BattleArenaZoneMap");
            Undo.RegisterCreatedObjectUndo(zoneObject, "Create Battle Arena Zone Map");
            if (parent != null)
            {
                zoneObject.transform.SetParent(parent, false);
            }

            return zoneObject.AddComponent<BattleArenaZoneMap>();
        }

        private static void ApplyInitialBounds(BattleArenaZoneMap zoneMap, Bounds bounds)
        {
            Vector3 centerOnArenaPlane = bounds.center;
            centerOnArenaPlane.y = zoneMap.transform.position.y;

            SerializedObject serializedZoneMap = new SerializedObject(zoneMap);
            serializedZoneMap.FindProperty("arenaSize").vector2Value = BattleArenaZoneMapEditor.SizeFromAstarBounds(bounds);
            serializedZoneMap.FindProperty("centerOffset").vector3Value = zoneMap.transform.InverseTransformPoint(centerOnArenaPlane);
            serializedZoneMap.FindProperty("columnSplits").vector2Value = new Vector2(1f / 3f, 2f / 3f);
            serializedZoneMap.FindProperty("rowSplits").vector2Value = new Vector2(1f / 3f, 2f / 3f);
            serializedZoneMap.ApplyModifiedProperties();
            EditorUtility.SetDirty(zoneMap);
        }

        private static void AssignToDirector(LevelFlowDirector director, BattleArenaZoneMap zoneMap)
        {
            if (director == null || zoneMap == null)
            {
                return;
            }

            SerializedObject serializedDirector = new SerializedObject(director);
            SerializedProperty property = serializedDirector.FindProperty("battleArenaZoneMap");
            if (property == null)
            {
                return;
            }

            Undo.RecordObject(director, "Assign Battle Arena Zone Map");
            property.objectReferenceValue = zoneMap;
            serializedDirector.ApplyModifiedProperties();
            EditorUtility.SetDirty(director);
        }
    }
}
