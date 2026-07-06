using NewFPG.Level;
using NewFPG.Combat;
using UnityEditor;
using UnityEngine;

namespace NewFPG.EditorTools
{
    [CustomEditor(typeof(LevelFlowDirector))]
    public sealed class LevelFlowDirectorEditor : UnityEditor.Editor
    {
        private const float MinBoundsSize = 1f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawBattleArenaTools();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("A* Bounds Tools", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Select the LevelFlowDirector and use the Scene view handles to move or scale the A* Recast scan box. A* scans only the Astar Graph Layer Mask.",
                    MessageType.Info);

                if (GUILayout.Button("Fit A* Bounds To Battle Arena"))
                {
                    ApplyToTargets(director => director.FitAstarGraphBoundsToBattleArena());
                }

                if (GUILayout.Button("Scan A* Graph Now"))
                {
                    ApplyToTargets(director =>
                    {
                        director.ScanAstarGraphNow();
                        return true;
                    });
                }
            }
        }

        private void DrawBattleArenaTools()
        {
            EditorGUILayout.LabelField("Battle Arena Zone Map", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "BattleArenaZoneMap 定义 3x3 战斗区域；怪物行为树节点“移动到战斗区域”会按这些区域 ID 采样可达点。",
                MessageType.Info);

            if (GUILayout.Button("Create Or Link Battle Arena Zone Map"))
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    LevelFlowDirector director = targets[i] as LevelFlowDirector;
                    if (director == null)
                    {
                        continue;
                    }

                    BattleArenaZoneMapEditorUtility.CreateForDirector(director);
                }
            }

            if (GUILayout.Button("Select Battle Arena Zone Map"))
            {
                LevelFlowDirector director = target as LevelFlowDirector;
                BattleArenaZoneMap zoneMap = director != null
                    ? director.GetComponentInChildren<BattleArenaZoneMap>(true)
                    : null;
                if (zoneMap != null)
                {
                    Selection.activeObject = zoneMap.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }
        }

        private void OnSceneGUI()
        {
            LevelFlowDirector director = (LevelFlowDirector)target;
            Bounds bounds = director.GetAstarGraphPreviewBounds();

            Handles.color = new Color(0.1f, 0.65f, 1f, 0.95f);
            Handles.DrawWireCube(bounds.center, bounds.size);
            Handles.Label(
                bounds.center + Vector3.up * (bounds.extents.y + 0.3f),
                "A* Recast Bounds");

            EditorGUI.BeginChangeCheck();
            Vector3 center = Handles.PositionHandle(bounds.center, Quaternion.identity);
            Vector3 size = Handles.ScaleHandle(
                bounds.size,
                center,
                Quaternion.identity,
                HandleUtility.GetHandleSize(center) * 1.2f);

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(director, "Adjust A* Recast Bounds");
            director.SetAstarGraphPreviewBounds(new Bounds(center, ClampSize(size)));
            EditorUtility.SetDirty(director);
        }

        private void ApplyToTargets(System.Func<LevelFlowDirector, bool> action)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                LevelFlowDirector director = targets[i] as LevelFlowDirector;
                if (director == null)
                {
                    continue;
                }

                Undo.RecordObject(director, "Adjust A* Recast Bounds");
                if (action(director))
                {
                    EditorUtility.SetDirty(director);
                }
            }
        }

        private static Vector3 ClampSize(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(MinBoundsSize, Mathf.Abs(size.x)),
                Mathf.Max(MinBoundsSize, Mathf.Abs(size.y)),
                Mathf.Max(MinBoundsSize, Mathf.Abs(size.z)));
        }
    }
}
