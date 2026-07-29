using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Unity;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor
{
    internal static class FpgEnemyHitboxFollowEditorFields
    {
        public static void Draw(
            SkeletonAnimation skeletonAnimation,
            SerializedProperty followSettings)
        {
            if (followSettings == null)
            {
                return;
            }

            SerializedProperty followMode =
                followSettings.FindPropertyRelative("followMode");
            SerializedProperty boneName =
                followSettings.FindPropertyRelative("boneName");
            SerializedProperty keepAuthoredRotation =
                followSettings.FindPropertyRelative("keepAuthoredRotation");
            SerializedProperty positionOffset =
                followSettings.FindPropertyRelative("positionOffset");
            SerializedProperty rotationOffsetEuler =
                followSettings.FindPropertyRelative("rotationOffsetEuler");
            EditorGUILayout.PropertyField(
                followMode,
                new GUIContent("Follow Mode"));
            if (followMode.intValue
                != (int)D0EnemyHitboxFollowMode.SpineBone)
            {
                return;
            }

            DrawBoneSelector(skeletonAnimation, boneName);
            bool followRotation = !keepAuthoredRotation.boolValue;
            keepAuthoredRotation.boolValue = !EditorGUILayout.Toggle(
                new GUIContent("Follow Bone Rotation"),
                followRotation);
            EditorGUILayout.PropertyField(
                positionOffset,
                new GUIContent(
                    "Position Offset",
                    "Additional offset in the Spine bone's local axes, in Unity units."));
            EditorGUILayout.PropertyField(
                rotationOffsetEuler,
                new GUIContent(
                    "Rotation Offset",
                    "Additional Euler rotation applied after the authored rotation offset."));
            EditorGUILayout.HelpBox(
                "Scene preview and runtime add these values on top of the "
                + "authored setup-pose offset. Use Collider Center for a "
                + "shape-only position adjustment; moving a Weakpoint anchor "
                + "also moves its other anchor consumers.",
                MessageType.None);
        }

        public static void Reset(SerializedProperty followSettings)
        {
            followSettings.FindPropertyRelative("followMode").intValue =
                (int)D0EnemyHitboxFollowMode.AuthoredTransform;
            followSettings.FindPropertyRelative("boneName").stringValue =
                string.Empty;
            followSettings.FindPropertyRelative("keepAuthoredRotation")
                .boolValue = false;
            followSettings.FindPropertyRelative("positionOffset")
                .vector3Value = Vector3.zero;
            followSettings.FindPropertyRelative("rotationOffsetEuler")
                .vector3Value = Vector3.zero;
        }

        private static void DrawBoneSelector(
            SkeletonAnimation skeletonAnimation,
            SerializedProperty boneName)
        {
            if (!TryGetBoneNames(skeletonAnimation, out string[] boneNames))
            {
                EditorGUILayout.PropertyField(
                    boneName,
                    new GUIContent("Spine Bone"));
                return;
            }

            string currentBoneName = boneName.stringValue;
            int boneIndex = Array.IndexOf(boneNames, currentBoneName);
            bool missingBone = !string.IsNullOrEmpty(currentBoneName)
                && boneIndex < 0;
            var options = new string[boneNames.Length + 1];
            options[0] = missingBone
                ? $"<Missing: {currentBoneName}>"
                : "<None>";
            for (int index = 0; index < boneNames.Length; index++)
            {
                options[index + 1] = boneNames[index];
            }

            int selectedIndex = boneIndex < 0 ? 0 : boneIndex + 1;
            int nextIndex = EditorGUILayout.Popup(
                new GUIContent("Spine Bone"),
                selectedIndex,
                options);
            if (nextIndex > 0)
            {
                boneName.stringValue = boneNames[nextIndex - 1];
            }
            else if (selectedIndex > 0)
            {
                boneName.stringValue = string.Empty;
            }

            if (missingBone)
            {
                EditorGUILayout.PropertyField(
                    boneName,
                    new GUIContent("Bone Name"));
            }
        }

        private static bool TryGetBoneNames(
            SkeletonAnimation skeletonAnimation,
            out string[] boneNames)
        {
            boneNames = Array.Empty<string>();
            if (skeletonAnimation == null
                || skeletonAnimation.SkeletonDataAsset == null)
            {
                return false;
            }

            SkeletonData skeletonData;
            try
            {
                skeletonData = skeletonAnimation.SkeletonDataAsset
                    .GetSkeletonData(true);
            }
            catch (Exception)
            {
                return false;
            }

            if (skeletonData == null)
            {
                return false;
            }

            ExposedList<BoneData> bones = skeletonData.Bones;
            boneNames = new string[bones.Count];
            for (int index = 0; index < bones.Count; index++)
            {
                boneNames[index] = bones.Items[index].Name;
            }

            return true;
        }
    }

    internal static class FpgEnemyHitboxFollowEditorPreview
    {
        private sealed class Binding
        {
            public Collider Collider;
            public Transform Target;
            public Transform AuthoredParent;
            public string BoneName;
            public bool FollowBoneRotation;
            public Vector3 PositionOffset;
            public Quaternion RotationOffset;
            public Vector3 AdditionalPositionOffset;
            public Quaternion AdditionalRotationOffset;
            public Vector3 AuthoredLocalPosition;
            public Quaternion AuthoredLocalRotation;
            public Vector3 SkeletonWorldPosition;
            public Quaternion SkeletonWorldRotation;
            public Vector3 SkeletonLossyScale;
        }

        private sealed class ViewState
        {
            public SkeletonAnimation SkeletonAnimation;
            public readonly Dictionary<int, Binding> Bindings =
                new Dictionary<int, Binding>();
        }

        private static readonly Dictionary<int, ViewState> States =
            new Dictionary<int, ViewState>();

        public static void Rebuild(D0EnemyEntityView view)
        {
            Remove(view);
            if (!CanPreview(view, view == null ? null : view.SkeletonAnimation))
            {
                return;
            }

            var state = new ViewState
            {
                SkeletonAnimation = view.SkeletonAnimation
            };
            for (int index = 0; index < view.HitPartCount; index++)
            {
                if (!view.TryGetHitPart(
                        index,
                        out Collider collider,
                        out HitPart hitPart,
                        out _)
                    || !view.TryGetHitPartFollowSettings(
                        index,
                        out D0EnemyHitboxFollowSettings settings)
                    || settings.FollowMode
                        != D0EnemyHitboxFollowMode.SpineBone)
                {
                    continue;
                }

                Transform target = hitPart == HitPart.Weakpoint
                    ? view.WeakpointAnchor
                    : collider.transform;
                TryAddBinding(state, collider, target, settings);
            }

            States[view.GetInstanceID()] = state;

            SceneView.RepaintAll();
        }

        public static void Rebuild(FpgEnemyEntityView view)
        {
            Remove(view);
            if (!CanPreview(view, view == null ? null : view.SkeletonAnimation))
            {
                return;
            }

            var state = new ViewState
            {
                SkeletonAnimation = view.SkeletonAnimation
            };
            for (int index = 0; index < view.HitPartCount; index++)
            {
                if (!view.TryGetHitPart(
                        index,
                        out Collider collider,
                        out HitPart hitPart)
                    || !view.TryGetHitPartFollowSettings(
                        index,
                        out D0EnemyHitboxFollowSettings settings)
                    || settings.FollowMode
                        != D0EnemyHitboxFollowMode.SpineBone)
                {
                    continue;
                }

                TryAddBinding(
                    state,
                    collider,
                    hitPart == HitPart.Weakpoint
                        ? view.WeakpointAnchor
                        : collider.transform,
                    settings);
            }

            States[view.GetInstanceID()] = state;

            SceneView.RepaintAll();
        }

        private static void Remove(UnityEngine.Object view)
        {
            if (view != null)
            {
                States.Remove(view.GetInstanceID());
            }
        }

        public static bool IsFollowing(
            UnityEngine.Object view,
            Collider collider)
        {
            return TryGetBinding(view, collider, out _, out _);
        }

        public static bool TryGetMatrix(
            UnityEngine.Object view,
            Collider collider,
            out Matrix4x4 matrix)
        {
            matrix = collider == null
                ? Matrix4x4.identity
                : collider.transform.localToWorldMatrix;
            if (!TryGetBinding(
                    view,
                    collider,
                    out ViewState state,
                    out Binding binding))
            {
                return false;
            }

            SkeletonAnimation skeletonAnimation = state.SkeletonAnimation;
            if (skeletonAnimation == null)
            {
                return false;
            }

            try
            {
                skeletonAnimation.Initialize(false);
                Skeleton skeleton = skeletonAnimation.Skeleton;
                if (skeleton == null)
                {
                    return false;
                }

                skeleton.UpdateWorldTransform();
                Bone bone = skeleton.FindBone(binding.BoneName);
                if (bone == null
                    || !TryGetBoneWorldPose(
                        skeletonAnimation.transform,
                        bone,
                        out Vector3 bonePosition,
                        out Quaternion boneRotation))
                {
                    return false;
                }

                if (HasAuthoredPoseChanged(binding, skeletonAnimation.transform))
                {
                    if (!TryGetSetupBoneWorldPose(
                            skeletonAnimation,
                            binding.BoneName,
                            out Vector3 setupBonePosition,
                            out Quaternion setupBoneRotation))
                    {
                        return false;
                    }

                    CaptureOffsets(
                        binding,
                        skeletonAnimation.transform,
                        setupBonePosition,
                        setupBoneRotation);
                }

                Vector3 targetPosition = bonePosition
                    + boneRotation * (
                        binding.PositionOffset
                        + binding.AdditionalPositionOffset);
                Quaternion targetRotation = binding.FollowBoneRotation
                    ? boneRotation
                        * binding.RotationOffset
                        * binding.AdditionalRotationOffset
                    : binding.Target.rotation
                        * binding.AdditionalRotationOffset;
                Matrix4x4 sourcePose = Matrix4x4.TRS(
                    binding.Target.position,
                    binding.Target.rotation,
                    Vector3.one);
                Matrix4x4 targetPose = Matrix4x4.TRS(
                    targetPosition,
                    targetRotation,
                    Vector3.one);
                matrix = targetPose
                    * sourcePose.inverse
                    * binding.Collider.transform.localToWorldMatrix;
                return IsFinite(matrix);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool CanPreview(
            Component view,
            SkeletonAnimation skeletonAnimation)
        {
            return !Application.isPlaying
                && view != null
                && view.gameObject.scene.IsValid()
                && skeletonAnimation != null
                && skeletonAnimation.SkeletonDataAsset != null;
        }

        private static void TryAddBinding(
            ViewState state,
            Collider collider,
            Transform target,
            D0EnemyHitboxFollowSettings settings)
        {
            if (collider == null
                || target == null
                || string.IsNullOrWhiteSpace(settings.BoneName))
            {
                return;
            }

            try
            {
                SkeletonAnimation skeletonAnimation = state.SkeletonAnimation;
                if (!TryGetSetupBoneWorldPose(
                        skeletonAnimation,
                        settings.BoneName,
                        out Vector3 bonePosition,
                        out Quaternion boneRotation))
                {
                    return;
                }

                var binding = new Binding
                {
                    Collider = collider,
                    Target = target,
                    BoneName = settings.BoneName,
                    FollowBoneRotation = settings.FollowBoneRotation,
                    AdditionalPositionOffset = settings.PositionOffset,
                    AdditionalRotationOffset = settings.RotationOffset
                };
                CaptureOffsets(
                    binding,
                    skeletonAnimation.transform,
                    bonePosition,
                    boneRotation);
                state.Bindings[collider.GetInstanceID()] = binding;
            }
            catch (Exception)
            {
                // Validation in the Inspector reports incomplete bindings.
            }
        }

        private static void CaptureOffsets(
            Binding binding,
            Transform skeletonTransform,
            Vector3 bonePosition,
            Quaternion boneRotation)
        {
            binding.PositionOffset = Quaternion.Inverse(boneRotation)
                * (binding.Target.position - bonePosition);
            binding.RotationOffset = Quaternion.Inverse(boneRotation)
                * binding.Target.rotation;
            binding.AuthoredParent = binding.Target.parent;
            binding.AuthoredLocalPosition = binding.Target.localPosition;
            binding.AuthoredLocalRotation = binding.Target.localRotation;
            binding.SkeletonWorldPosition = skeletonTransform.position;
            binding.SkeletonWorldRotation = skeletonTransform.rotation;
            binding.SkeletonLossyScale = skeletonTransform.lossyScale;
        }

        private static bool HasAuthoredPoseChanged(
            Binding binding,
            Transform skeletonTransform)
        {
            return binding.Target == null
                || binding.Target.parent != binding.AuthoredParent
                || binding.Target.localPosition
                    != binding.AuthoredLocalPosition
                || Mathf.Abs(Quaternion.Dot(
                    binding.Target.localRotation,
                    binding.AuthoredLocalRotation)) < 0.999999f
                || skeletonTransform.position
                    != binding.SkeletonWorldPosition
                || Mathf.Abs(Quaternion.Dot(
                    skeletonTransform.rotation,
                    binding.SkeletonWorldRotation)) < 0.999999f
                || skeletonTransform.lossyScale
                    != binding.SkeletonLossyScale;
        }

        private static bool TryGetBinding(
            UnityEngine.Object view,
            Collider collider,
            out ViewState state,
            out Binding binding)
        {
            state = null;
            binding = null;
            if (view == null || collider == null)
            {
                return false;
            }

            if (Application.isPlaying)
            {
                States.Clear();
                return false;
            }

            PruneInvalidStates();
            EnsureState(view);
            return States.TryGetValue(view.GetInstanceID(), out state)
                && state.Bindings.TryGetValue(
                    collider.GetInstanceID(),
                    out binding)
                && binding.Target != null;
        }

        private static void EnsureState(UnityEngine.Object view)
        {
            int instanceId = view.GetInstanceID();
            if (States.ContainsKey(instanceId))
            {
                return;
            }

            if (view is D0EnemyEntityView d0View)
            {
                Rebuild(d0View);
            }
            else if (view is FpgEnemyEntityView formalView)
            {
                Rebuild(formalView);
            }
        }

        private static void PruneInvalidStates()
        {
            List<int> invalidIds = null;
            foreach (KeyValuePair<int, ViewState> pair in States)
            {
                if (pair.Value == null
                    || pair.Value.SkeletonAnimation == null)
                {
                    if (invalidIds == null)
                    {
                        invalidIds = new List<int>();
                    }

                    invalidIds.Add(pair.Key);
                }
            }

            if (invalidIds == null)
            {
                return;
            }

            for (int index = 0; index < invalidIds.Count; index++)
            {
                States.Remove(invalidIds[index]);
            }
        }

        private static bool TryGetSetupBoneWorldPose(
            SkeletonAnimation skeletonAnimation,
            string boneName,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = default(Vector3);
            rotation = Quaternion.identity;
            if (skeletonAnimation == null
                || skeletonAnimation.SkeletonDataAsset == null)
            {
                return false;
            }

            SkeletonData skeletonData = skeletonAnimation
                .SkeletonDataAsset.GetSkeletonData(true);
            if (skeletonData == null)
            {
                return false;
            }

            var setupSkeleton = new Skeleton(skeletonData);
            setupSkeleton.SetToSetupPose();
            setupSkeleton.UpdateWorldTransform();
            Bone bone = setupSkeleton.FindBone(boneName);
            return bone != null
                && TryGetBoneWorldPose(
                    skeletonAnimation.transform,
                    bone,
                    out position,
                    out rotation);
        }

        private static bool TryGetBoneWorldPose(
            Transform skeletonTransform,
            Bone bone,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = skeletonTransform.TransformPoint(
                new Vector3(bone.WorldX, bone.WorldY, 0f));
            Vector3 forward = skeletonTransform.TransformDirection(
                Vector3.forward);
            Vector3 right = skeletonTransform.TransformVector(
                new Vector3(bone.A, bone.C, 0f));
            if (forward.sqrMagnitude <= 0.00000001f
                || right.sqrMagnitude <= 0.00000001f)
            {
                rotation = Quaternion.identity;
                return false;
            }

            forward.Normalize();
            right -= Vector3.Dot(right, forward) * forward;
            if (right.sqrMagnitude <= 0.00000001f)
            {
                rotation = Quaternion.identity;
                return false;
            }

            right.Normalize();
            Vector3 up = Vector3.Cross(forward, right).normalized;
            rotation = Quaternion.LookRotation(forward, up);
            return IsFinite(position) && IsFinite(rotation);
        }

        private static bool IsFinite(Matrix4x4 value)
        {
            for (int index = 0; index < 16; index++)
            {
                if (!IsFinite(value[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z)
                && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
