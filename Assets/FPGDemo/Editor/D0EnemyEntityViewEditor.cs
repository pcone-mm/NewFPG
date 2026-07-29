using Object = UnityEngine.Object;

using FPG.Demo.Combat;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace FPG.Demo.Editor
{
    [CustomEditor(typeof(D0EnemyEntityView))]
    internal sealed class D0EnemyEntityViewEditor : UnityEditor.Editor
    {
        private enum PrimitiveKind
        {
            Box,
            Capsule,
            Sphere
        }

        private static readonly Color BodyFill = new Color(0.18f, 0.85f, 0.35f, 0.16f);
        private static readonly Color BodyOutline = new Color(0.12f, 1f, 0.3f, 0.9f);
        private static readonly Color WeakpointFill = new Color(1f, 0.45f, 0.08f, 0.22f);
        private static readonly Color WeakpointOutline = new Color(1f, 0.65f, 0.12f, 0.95f);

        private readonly BoxBoundsHandle boxHandle = new BoxBoundsHandle();
        private readonly CapsuleBoundsHandle capsuleHandle = new CapsuleBoundsHandle();
        private readonly SphereBoundsHandle sphereHandle = new SphereBoundsHandle();

        private SerializedProperty bodyHitbox;
        private SerializedProperty bodyHitboxFollow;

        private SerializedProperty bodyGeometryId;
        private SerializedProperty weakpointHitbox;
        private SerializedProperty weakpointHitboxFollow;

        private SerializedProperty weakpointGeometryId;
        private SerializedProperty hasWeakpoint;
        private SerializedProperty additionalBodyHitboxes;
        private Collider activeHitbox;

        private void OnEnable()
        {
            bodyHitbox = serializedObject.FindProperty("bodyHitbox");
            bodyHitboxFollow = serializedObject.FindProperty("bodyHitboxFollow");
            bodyGeometryId = serializedObject.FindProperty("bodyGeometryId");
            weakpointHitbox = serializedObject.FindProperty("weakpointHitbox");
            weakpointHitboxFollow = serializedObject.FindProperty("weakpointHitboxFollow");
            weakpointGeometryId = serializedObject.FindProperty("weakpointGeometryId");
            hasWeakpoint = serializedObject.FindProperty("hasWeakpoint");
            additionalBodyHitboxes = serializedObject.FindProperty("additionalBodyHitboxes");
            D0EnemyEntityView view = (D0EnemyEntityView)target;
            activeHitbox = view.BodyHitbox;
            Undo.undoRedoPerformed += HandleUndoRedo;
            FpgEnemyHitboxFollowEditorPreview.Rebuild(view);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
        }

        private void HandleUndoRedo()
        {
            FpgEnemyHitboxFollowEditorPreview.Rebuild(
                (D0EnemyEntityView)target);
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            D0EnemyEntityView view = (D0EnemyEntityView)target;
            serializedObject.Update();

            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "bodyHitbox",
                "bodyHitboxFollow",
                "bodyGeometryId",
                "weakpointHitbox",
                "weakpointHitboxFollow",
                "weakpointGeometryId",
                "hasWeakpoint",
                "additionalBodyHitboxes");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hitbox Authoring", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The primary Body is the compatibility aim anchor and cannot be removed. "
                + "Use the standard Move/Rotate tools and the bounds handles in Scene or Prefab Mode.",
                MessageType.None);

            EditorGUILayout.PropertyField(bodyHitbox, new GUIContent("Primary Body"));
            EditorGUILayout.PropertyField(bodyGeometryId, new GUIContent("Primary Geometry Id"));
            DrawFollowSettings(view, bodyHitboxFollow);
            using (new EditorGUI.DisabledScope(bodyHitbox.objectReferenceValue == null))
            {
                if (GUILayout.Button("Edit Primary Bounds"))
                {
                    SetActiveHitbox(bodyHitbox.objectReferenceValue as Collider, true);
                }
            }

            EditorGUILayout.Space(3f);
            DrawAdditionalBodyList(view);

            EditorGUILayout.Space(3f);
            EditorGUILayout.PropertyField(hasWeakpoint);
            using (new EditorGUI.DisabledScope(!hasWeakpoint.boolValue))
            {
                EditorGUILayout.PropertyField(weakpointHitbox);
                EditorGUILayout.PropertyField(weakpointGeometryId);
                DrawFollowSettings(view, weakpointHitboxFollow);
                if (GUILayout.Button("Edit Weakpoint Bounds"))
                {
                    SetActiveHitbox(weakpointHitbox.objectReferenceValue as Collider, true);
                }
            }

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
                    $"Valid hitbox contract: {view.BodyHitboxCount} Body, "
                    + $"{(view.HasWeakpoint ? 1 : 0)} Weakpoint, "
                    + $"{view.BoneFollowHitPartCount} Spine Bone Follow.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        private void DrawAdditionalBodyList(D0EnemyEntityView view)
        {
            bool canEditStructure = CanEditPrefabStructure(view);
            EditorGUILayout.LabelField(
                $"Additional Bodies ({additionalBodyHitboxes.arraySize}/"
                + $"{D0EnemyEntityView.MaxAdditionalBodyHitboxCount})",
                EditorStyles.boldLabel);

            int removeIndex = -1;
            for (int index = 0; index < additionalBodyHitboxes.arraySize; index++)
            {
                SerializedProperty binding = additionalBodyHitboxes.GetArrayElementAtIndex(index);
                SerializedProperty colliderProperty = binding.FindPropertyRelative("collider");
                SerializedProperty geometryProperty = binding.FindPropertyRelative("geometryId");
                SerializedProperty followProperty = binding.FindPropertyRelative("followSettings");
                Collider collider = colliderProperty.objectReferenceValue as Collider;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Body Part {index + 1}", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(colliderProperty);
                EditorGUILayout.PropertyField(geometryProperty);
                DrawFollowSettings(view, followProperty);

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(collider == null))
                {
                    if (GUILayout.Button(new GUIContent("Select", "Use Scene handles to edit this collider.")))
                    {
                        SetActiveHitbox(collider, false);
                    }

                    if (GUILayout.Button(new GUIContent("Frame", "Frame this collider in Scene view.")))
                    {
                        SetActiveHitbox(collider, true);
                    }
                }

                using (new EditorGUI.DisabledScope(!canEditStructure))
                {
                    if (GUILayout.Button(new GUIContent("Delete", "Remove this binding and delete tool-created hitbox objects.")))
                    {
                        removeIndex = index;
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                DeleteAdditionalBody(view, removeIndex);
                return;
            }

            bool cannotAdd = additionalBodyHitboxes.arraySize
                    >= D0EnemyEntityView.MaxAdditionalBodyHitboxCount
                || !canEditStructure;
            using (new EditorGUI.DisabledScope(cannotAdd))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add Box"))
                {
                    AddBody(view, PrimitiveKind.Box);
                }

                if (GUILayout.Button("Add Capsule"))
                {
                    AddBody(view, PrimitiveKind.Capsule);
                }

                if (GUILayout.Button("Add Sphere"))
                {
                    AddBody(view, PrimitiveKind.Sphere);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (!canEditStructure)
            {
                EditorGUILayout.HelpBox(
                    "Open this prefab in Prefab Mode to create or delete hitbox objects.",
                    MessageType.None);
            }
        }

        private static void DrawFollowSettings(
            D0EnemyEntityView view,
            SerializedProperty followSettings)
        {
            FpgEnemyHitboxFollowEditorFields.Draw(
                view == null ? null : view.SkeletonAnimation,
                followSettings);
        }

        private static void ResetFollowSettings(SerializedProperty followSettings)
        {
            FpgEnemyHitboxFollowEditorFields.Reset(followSettings);
        }

        private void AddBody(D0EnemyEntityView view, PrimitiveKind kind)
        {
            if (!CanEditPrefabStructure(view))
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();
            int geometryId = FindNextGeometryId();
            Transform hitboxRoot = EnsureHitboxRoot(view);
            GameObject hitboxObject = new GameObject($"Body_{kind}_{geometryId}");
            Undo.RegisterCreatedObjectUndo(hitboxObject, "Add enemy body hitbox");
            hitboxObject.layer = D0EnemyEntityView.HitboxLayer;
            Undo.SetTransformParent(hitboxObject.transform, hitboxRoot, "Parent enemy body hitbox");

            Transform primary = view.BodyHitbox == null ? null : view.BodyHitbox.transform;
            if (primary == null)
            {
                hitboxObject.transform.localPosition = Vector3.zero;
                hitboxObject.transform.localRotation = Quaternion.identity;
            }
            else
            {
                hitboxObject.transform.SetPositionAndRotation(primary.position, primary.rotation);
            }

            hitboxObject.transform.localScale = Vector3.one;

            Collider collider;
            switch (kind)
            {
                case PrimitiveKind.Capsule:
                    CapsuleCollider capsule = Undo.AddComponent<CapsuleCollider>(hitboxObject);
                    capsule.direction = 1;
                    capsule.radius = 0.5f;
                    capsule.height = 1.5f;
                    collider = capsule;
                    break;
                case PrimitiveKind.Sphere:
                    SphereCollider sphere = Undo.AddComponent<SphereCollider>(hitboxObject);
                    sphere.radius = 0.5f;
                    collider = sphere;
                    break;
                default:
                    BoxCollider box = Undo.AddComponent<BoxCollider>(hitboxObject);
                    box.size = new Vector3(1.5f, 1.5f, 0.38f);
                    collider = box;
                    break;
            }

            collider.isTrigger = false;
            collider.enabled = true;

            Undo.RecordObject(view, "Add enemy body hitbox binding");
            serializedObject.Update();
            int index = additionalBodyHitboxes.arraySize;
            additionalBodyHitboxes.InsertArrayElementAtIndex(index);
            SerializedProperty binding = additionalBodyHitboxes.GetArrayElementAtIndex(index);
            binding.FindPropertyRelative("collider").objectReferenceValue = collider;
            binding.FindPropertyRelative("geometryId").intValue = geometryId;
            ResetFollowSettings(binding.FindPropertyRelative("followSettings"));
            serializedObject.ApplyModifiedProperties();

            RecordPrefabModification(view);
            EditorUtility.SetDirty(collider);
            FpgEnemyHitboxFollowEditorPreview.Rebuild(view);
            SetActiveHitbox(collider, true);
        }

        private void DeleteAdditionalBody(D0EnemyEntityView view, int index)
        {
            if (!CanEditPrefabStructure(view))
            {
                return;
            }

            SerializedProperty binding = additionalBodyHitboxes.GetArrayElementAtIndex(index);
            Collider collider = binding.FindPropertyRelative("collider").objectReferenceValue as Collider;

            Undo.RecordObject(view, "Remove enemy body hitbox binding");
            additionalBodyHitboxes.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();

            if (IsToolCreatedHitbox(view, collider))
            {
                Undo.DestroyObjectImmediate(collider.gameObject);
            }

            if (activeHitbox == collider)
            {
                activeHitbox = view.BodyHitbox;
            }

            RecordPrefabModification(view);
            FpgEnemyHitboxFollowEditorPreview.Rebuild(view);
            SceneView.RepaintAll();
        }

        private static bool CanEditPrefabStructure(D0EnemyEntityView view)
        {
            if (view == null || EditorApplication.isPlaying)
            {
                return false;
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            return prefabStage != null
                && prefabStage.IsPartOfPrefabContents(view.gameObject);
        }

        private int FindNextGeometryId()
        {
            int candidate = 2003;
            while (candidate == bodyGeometryId.intValue
                || hasWeakpoint.boolValue && candidate == weakpointGeometryId.intValue
                || AdditionalGeometryIdExists(candidate))
            {
                candidate++;
            }

            return candidate;
        }

        private bool AdditionalGeometryIdExists(int candidate)
        {
            for (int index = 0; index < additionalBodyHitboxes.arraySize; index++)
            {
                SerializedProperty binding = additionalBodyHitboxes.GetArrayElementAtIndex(index);
                if (binding.FindPropertyRelative("geometryId").intValue == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform EnsureHitboxRoot(D0EnemyEntityView view)
        {
            Transform root = view.GameplayAnchor.Find("Hitboxes");
            if (root != null)
            {
                return root;
            }

            GameObject rootObject = new GameObject("Hitboxes");
            Undo.RegisterCreatedObjectUndo(rootObject, "Create enemy hitbox root");
            Undo.SetTransformParent(rootObject.transform, view.GameplayAnchor, "Parent enemy hitbox root");
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            return rootObject.transform;
        }

        private static bool IsToolCreatedHitbox(D0EnemyEntityView view, Collider collider)
        {
            if (view == null || view.GameplayAnchor == null || collider == null
                || collider == view.BodyHitbox)
            {
                return false;
            }

            Transform hitboxRoot = view.GameplayAnchor.Find("Hitboxes");
            if (hitboxRoot == null
                || collider.transform.parent != hitboxRoot
                || !collider.gameObject.name.StartsWith("Body_"))
            {
                return false;
            }

            Component[] components = collider.GetComponents<Component>();
            return components.Length == 2
                && components[0] is Transform
                && components[1] is Collider;
        }

        private static void RecordPrefabModification(Object targetObject)
        {
            EditorUtility.SetDirty(targetObject);
            if (PrefabUtility.IsPartOfPrefabInstance(targetObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(targetObject);
            }
        }

        private void OnSceneGUI()
        {
            D0EnemyEntityView view = (D0EnemyEntityView)target;
            Collider collider = activeHitbox;
            if (collider == null || !IsHitPart(view, collider, out HitPart hitPart))
            {
                collider = view.BodyHitbox;
                activeHitbox = collider;
                if (collider == null || !IsHitPart(view, collider, out hitPart))
                {
                    return;
                }
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

            Undo.RecordObject(collider, "Edit enemy box hitbox");
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
            capsuleHandle.heightAxis = (CapsuleBoundsHandle.HeightAxis)collider.direction;
            capsuleHandle.radius = collider.radius;
            capsuleHandle.height = collider.height;
            EditorGUI.BeginChangeCheck();
            capsuleHandle.DrawHandle();
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(collider, "Edit enemy capsule hitbox");
            collider.center = capsuleHandle.center;
            collider.radius = Mathf.Max(0f, capsuleHandle.radius);
            collider.height = Mathf.Max(collider.radius * 2f, capsuleHandle.height);
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

            Undo.RecordObject(collider, "Edit enemy sphere hitbox");
            collider.center = sphereHandle.center;
            collider.radius = Mathf.Max(0f, sphereHandle.radius);
            RecordPrefabModification(collider);
        }

        private static bool IsHitPart(
            D0EnemyEntityView view,
            Collider candidate,
            out HitPart hitPart)
        {
            for (int index = 0; index < view.HitPartCount; index++)
            {
                if (view.TryGetHitPart(
                        index,
                        out Collider collider,
                        out HitPart candidatePart,
                        out _)
                    && collider == candidate)
                {
                    hitPart = candidatePart;
                    return true;
                }
            }

            hitPart = HitPart.Body;
            return false;
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy | GizmoType.Pickable)]
        private static void DrawHitboxes(D0EnemyEntityView view, GizmoType gizmoType)
        {
            for (int index = 0; index < view.HitPartCount; index++)
            {
                if (!view.TryGetHitPart(
                        index,
                        out Collider collider,
                        out HitPart hitPart,
                        out _))
                {
                    continue;
                }

                DrawColliderGizmo(
                    view,
                    collider,
                    hitPart == HitPart.Weakpoint ? WeakpointFill : BodyFill,
                    hitPart == HitPart.Weakpoint ? WeakpointOutline : BodyOutline);
            }
        }

        private static void DrawColliderGizmo(
            D0EnemyEntityView view,
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
            float cylinderLength = Mathf.Max(0f, capsule.height - radius * 2f);
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
                    Undo.RecordObject(hitboxTransform, "Move enemy hitbox");
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
                    Undo.RecordObject(hitboxTransform, "Rotate enemy hitbox");
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
                SceneView.lastActiveSceneView.Frame(collider.bounds, false);
            }

            SceneView.RepaintAll();
            Repaint();
        }
    }
}
