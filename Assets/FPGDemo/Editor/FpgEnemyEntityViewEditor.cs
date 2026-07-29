using FPG.Demo.Combat;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace FPG.Demo.Editor
{
    [CustomEditor(typeof(FpgEnemyEntityView))]
    internal sealed class FpgEnemyEntityViewEditor : UnityEditor.Editor
    {
        private static readonly Color BodyFill =
            new Color(0.18f, 0.85f, 0.35f, 0.16f);
        private static readonly Color BodyOutline =
            new Color(0.12f, 1f, 0.3f, 0.9f);
        private static readonly Color WeakpointFill =
            new Color(1f, 0.45f, 0.08f, 0.22f);
        private static readonly Color WeakpointOutline =
            new Color(1f, 0.65f, 0.12f, 0.95f);

        private readonly BoxBoundsHandle boxHandle =
            new BoxBoundsHandle();
        private readonly CapsuleBoundsHandle capsuleHandle =
            new CapsuleBoundsHandle();
        private readonly SphereBoundsHandle sphereHandle =
            new SphereBoundsHandle();

        private SerializedProperty hitParts;
        private SerializedProperty hitPartKinds;
        private SerializedProperty hitPartFollowSettings;
        private Collider activeHitbox;

        private void OnEnable()
        {
            hitParts = serializedObject.FindProperty("hitParts");
            hitPartKinds = serializedObject.FindProperty("hitPartKinds");
            hitPartFollowSettings = serializedObject.FindProperty(
                "hitPartFollowSettings");
            activeHitbox = hitParts.arraySize == 0
                ? null
                : hitParts.GetArrayElementAtIndex(0)
                    .objectReferenceValue as Collider;
            Undo.undoRedoPerformed += HandleUndoRedo;
            FpgEnemyHitboxFollowEditorPreview.Rebuild(
                (FpgEnemyEntityView)target);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
        }

        private void HandleUndoRedo()
        {
            FpgEnemyHitboxFollowEditorPreview.Rebuild(
                (FpgEnemyEntityView)target);
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            FpgEnemyEntityView view = (FpgEnemyEntityView)target;
            serializedObject.Update();

            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "hitParts",
                "hitPartKinds",
                "hitPartFollowSettings");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Hitbox Authoring",
                EditorStyles.boldLabel);
            DrawHitPartList(view);

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(view);
                FpgEnemyHitboxFollowEditorPreview.Rebuild(view);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            if (view.TryValidate(out string error))
            {
                EditorGUILayout.HelpBox(
                    $"Valid hitbox contract: {view.HitPartCount} parts, "
                    + $"{view.BoneFollowHitPartCount} Spine Bone Follow.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        private void DrawHitPartList(FpgEnemyEntityView view)
        {
            bool kindsAreParallel = hitPartKinds.arraySize == 0
                || hitPartKinds.arraySize == hitParts.arraySize;
            bool followSettingsAreParallel =
                hitPartFollowSettings.arraySize == 0
                || hitPartFollowSettings.arraySize == hitParts.arraySize;
            if (!kindsAreParallel || !followSettingsAreParallel)
            {
                EditorGUILayout.HelpBox(
                    "Hit-part kinds and follow settings must be empty or "
                    + "parallel the Collider array.",
                    MessageType.Error);
            }

            for (int index = 0; index < hitParts.arraySize; index++)
            {
                SerializedProperty colliderProperty =
                    hitParts.GetArrayElementAtIndex(index);
                Collider collider =
                    colliderProperty.objectReferenceValue as Collider;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    $"Hit Part {index + 1}",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(
                    colliderProperty,
                    new GUIContent("Collider"));
                if (hitPartKinds.arraySize == hitParts.arraySize)
                {
                    EditorGUILayout.PropertyField(
                        hitPartKinds.GetArrayElementAtIndex(index),
                        new GUIContent("Kind"));
                }
                else
                {
                    EditorGUILayout.LabelField("Kind", "Body (default)");
                }

                if (hitPartFollowSettings.arraySize == hitParts.arraySize)
                {
                    FpgEnemyHitboxFollowEditorFields.Draw(
                        view.SkeletonAnimation,
                        hitPartFollowSettings.GetArrayElementAtIndex(index));
                }
                else
                {
                    EditorGUILayout.LabelField(
                        "Follow Mode",
                        "Authored Transform (default)");
                }

                using (new EditorGUI.DisabledScope(collider == null))
                {
                    if (GUILayout.Button("Edit Bounds"))
                    {
                        SetActiveHitbox(collider, true);
                    }
                }

                EditorGUILayout.EndVertical();
            }

            if (hitPartFollowSettings.arraySize == 0
                && hitParts.arraySize > 0
                && GUILayout.Button("Configure Bone Follow"))
            {
                hitPartFollowSettings.arraySize = hitParts.arraySize;
                for (int index = 0;
                    index < hitPartFollowSettings.arraySize;
                    index++)
                {
                    FpgEnemyHitboxFollowEditorFields.Reset(
                        hitPartFollowSettings.GetArrayElementAtIndex(index));
                }
            }
        }

        private void OnSceneGUI()
        {
            FpgEnemyEntityView view = (FpgEnemyEntityView)target;
            Collider collider = activeHitbox;
            if (collider == null
                || !TryGetHitPart(view, collider, out HitPart hitPart))
            {
                if (view.HitPartCount == 0
                    || !view.TryGetHitPart(
                        0,
                        out collider,
                        out hitPart))
                {
                    return;
                }

                activeHitbox = collider;
            }

            bool followsBone =
                FpgEnemyHitboxFollowEditorPreview.IsFollowing(
                    view,
                    collider);
            if (!followsBone)
            {
                DrawTransformHandle(collider.transform);
            }

            Color handleColor = hitPart == HitPart.Weakpoint
                ? WeakpointOutline
                : BodyOutline;
            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;
            Handles.matrix =
                FpgEnemyHitboxFollowEditorPreview.TryGetMatrix(
                    view,
                    collider,
                    out Matrix4x4 previewMatrix)
                    ? previewMatrix
                    : collider.transform.localToWorldMatrix;
            Handles.color = handleColor;
            try
            {
                if (collider is BoxCollider box)
                {
                    DrawBoxHandle(box);
                }
                else if (collider is CapsuleCollider capsule)
                {
                    DrawCapsuleHandle(capsule);
                }
                else if (collider is SphereCollider sphere)
                {
                    DrawSphereHandle(sphere);
                }
            }
            finally
            {
                Handles.matrix = previousMatrix;
                Handles.color = previousColor;
            }
        }

        private void DrawBoxHandle(BoxCollider collider)
        {
            boxHandle.center = collider.center;
            boxHandle.size = collider.size;
            EditorGUI.BeginChangeCheck();
            boxHandle.DrawHandle();
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(collider, "Edit formal enemy box hitbox");
            collider.center = boxHandle.center;
            Vector3 size = boxHandle.size;
            collider.size = new Vector3(
                Mathf.Abs(size.x),
                Mathf.Abs(size.y),
                Mathf.Abs(size.z));
            RecordPrefabModification(collider);
        }

        private void DrawCapsuleHandle(CapsuleCollider collider)
        {
            capsuleHandle.center = collider.center;
            capsuleHandle.heightAxis =
                (CapsuleBoundsHandle.HeightAxis)collider.direction;
            capsuleHandle.radius = collider.radius;
            capsuleHandle.height = collider.height;
            EditorGUI.BeginChangeCheck();
            capsuleHandle.DrawHandle();
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(collider, "Edit formal enemy capsule hitbox");
            collider.center = capsuleHandle.center;
            collider.radius = Mathf.Max(0f, capsuleHandle.radius);
            collider.height = Mathf.Max(
                collider.radius * 2f,
                capsuleHandle.height);
            RecordPrefabModification(collider);
        }

        private void DrawSphereHandle(SphereCollider collider)
        {
            sphereHandle.center = collider.center;
            sphereHandle.radius = collider.radius;
            EditorGUI.BeginChangeCheck();
            sphereHandle.DrawHandle();
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(collider, "Edit formal enemy sphere hitbox");
            collider.center = sphereHandle.center;
            collider.radius = Mathf.Max(0f, sphereHandle.radius);
            RecordPrefabModification(collider);
        }

        private static bool TryGetHitPart(
            FpgEnemyEntityView view,
            Collider candidate,
            out HitPart hitPart)
        {
            for (int index = 0; index < view.HitPartCount; index++)
            {
                if (view.TryGetHitPart(
                        index,
                        out Collider collider,
                        out HitPart candidatePart)
                    && collider == candidate)
                {
                    hitPart = candidatePart;
                    return true;
                }
            }

            hitPart = HitPart.Body;
            return false;
        }

        [DrawGizmo(
            GizmoType.Selected
            | GizmoType.InSelectionHierarchy
            | GizmoType.Pickable)]
        private static void DrawHitboxes(
            FpgEnemyEntityView view,
            GizmoType gizmoType)
        {
            for (int index = 0; index < view.HitPartCount; index++)
            {
                if (!view.TryGetHitPart(
                        index,
                        out Collider collider,
                        out HitPart hitPart))
                {
                    continue;
                }

                DrawColliderGizmo(
                    view,
                    collider,
                    hitPart == HitPart.Weakpoint
                        ? WeakpointFill
                        : BodyFill,
                    hitPart == HitPart.Weakpoint
                        ? WeakpointOutline
                        : BodyOutline);
            }
        }

        private static void DrawColliderGizmo(
            FpgEnemyEntityView view,
            Collider collider,
            Color fillColor,
            Color outlineColor)
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix =
                FpgEnemyHitboxFollowEditorPreview.TryGetMatrix(
                    view,
                    collider,
                    out Matrix4x4 previewMatrix)
                    ? previewMatrix
                    : collider.transform.localToWorldMatrix;
            try
            {
                if (collider is BoxCollider box)
                {
                    Gizmos.color = fillColor;
                    Gizmos.DrawCube(box.center, box.size);
                    Gizmos.color = outlineColor;
                    Gizmos.DrawWireCube(box.center, box.size);
                }
                else if (collider is SphereCollider sphere)
                {
                    Gizmos.color = fillColor;
                    Gizmos.DrawSphere(sphere.center, sphere.radius);
                    Gizmos.color = outlineColor;
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                }
                else if (collider is CapsuleCollider capsule)
                {
                    DrawCapsuleGizmo(capsule, fillColor, outlineColor);
                }
            }
            finally
            {
                Gizmos.matrix = previousMatrix;
                Gizmos.color = previousColor;
            }
        }

        private static void DrawCapsuleGizmo(
            CapsuleCollider capsule,
            Color fillColor,
            Color outlineColor)
        {
            float radius = Mathf.Max(0f, capsule.radius);
            float cylinderLength = Mathf.Max(
                0f,
                capsule.height - radius * 2f);
            Vector3 axis = capsule.direction == 0
                ? Vector3.right
                : capsule.direction == 2
                    ? Vector3.forward
                    : Vector3.up;
            Vector3 endOffset = axis * (cylinderLength * 0.5f);
            Vector3 centerSize = Vector3.one * radius * 2f;
            if (capsule.direction == 0)
            {
                centerSize.x = cylinderLength;
            }
            else if (capsule.direction == 2)
            {
                centerSize.z = cylinderLength;
            }
            else
            {
                centerSize.y = cylinderLength;
            }

            Gizmos.color = fillColor;
            Gizmos.DrawSphere(capsule.center - endOffset, radius);
            Gizmos.DrawSphere(capsule.center + endOffset, radius);
            if (cylinderLength > 0f)
            {
                Gizmos.DrawCube(capsule.center, centerSize);
            }

            Gizmos.color = outlineColor;
            Gizmos.DrawWireSphere(capsule.center - endOffset, radius);
            Gizmos.DrawWireSphere(capsule.center + endOffset, radius);
            if (cylinderLength > 0f)
            {
                Gizmos.DrawWireCube(capsule.center, centerSize);
            }
        }

        private static void DrawTransformHandle(Transform hitboxTransform)
        {
            if (Tools.current == Tool.Move)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 position = Handles.PositionHandle(
                    hitboxTransform.position,
                    hitboxTransform.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(
                        hitboxTransform,
                        "Move formal enemy hitbox");
                    hitboxTransform.position = position;
                    RecordPrefabModification(hitboxTransform);
                }
            }
            else if (Tools.current == Tool.Rotate)
            {
                EditorGUI.BeginChangeCheck();
                Quaternion rotation = Handles.RotationHandle(
                    hitboxTransform.rotation,
                    hitboxTransform.position);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(
                        hitboxTransform,
                        "Rotate formal enemy hitbox");
                    hitboxTransform.rotation = rotation;
                    RecordPrefabModification(hitboxTransform);
                }
            }
        }

        private void SetActiveHitbox(Collider collider, bool frame)
        {
            if (collider == null)
            {
                return;
            }

            activeHitbox = collider;
            EditorGUIUtility.PingObject(collider);
            if (frame && SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Frame(
                    collider.bounds,
                    false);
            }

            SceneView.RepaintAll();
            Repaint();
        }

        private static void RecordPrefabModification(
            Object targetObject)
        {
            EditorUtility.SetDirty(targetObject);
            if (PrefabUtility.IsPartOfPrefabInstance(targetObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    targetObject);
            }
        }
    }
}
