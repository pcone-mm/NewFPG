using System;
using System.Collections.Generic;
using System.Reflection;
using FPG.Demo.Skills;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal sealed class FpgSkillPreviewView : VisualElement, IFpgSkillPreviewPoseProvider
    {
        private const float TickRate = 60f;

        private readonly List<VisualElement> targets = new List<VisualElement>();
        private readonly IMGUIContainer renderedPreview;
        private readonly Label actor;
        private readonly VisualElement beam;
        private readonly VisualElement impact;
        private readonly Label overlayLabel;
        private readonly List<PreviewVfxInstance> presentationVfx =
            new List<PreviewVfxInstance>();
        private readonly List<PreviewShakeImpulse> presentationShakes =
            new List<PreviewShakeImpulse>();

        private PreviewRenderUtility previewUtility;
        private GameObject previewPrefab;
        private GameObject previewInstance;
        private GameObject previewAudioObject;
        private AudioSource previewAudioSource;
        private FpgSkillPreviewSceneContent sceneContent;
        private FpgSkillPreviewSimulationFrame lastSimulationFrame;
        private Component spineComponent;
        private object spineAnimationState;
        private object spineTrackEntry;
        private float spineAnimationDuration;
        private bool showGeometry = true;
        private int targetCount = 1;
        private string animationName = "未指定动作";
        private FpgCompiledSkillSequence animationSequence;
        private int lastSampledTick;
        private Vector3 previewCameraBasePosition;
        private Quaternion previewCameraBaseRotation = Quaternion.identity;
        private bool hasPreviewCameraBaseline;
        private string previewStatus = "未选择预览 Prefab";

        public FpgSkillPreviewView()
        {
            AddToClassList("skill-preview");

            renderedPreview = new IMGUIContainer(DrawRenderedPreview);
            renderedPreview.AddToClassList("preview-render-surface");
            Add(renderedPreview);

            for (int lineIndex = 0; lineIndex < 3; lineIndex++)
            {
                VisualElement line = new VisualElement();
                line.AddToClassList("preview-grid-line");
                line.style.top = Length.Percent((lineIndex + 1) * 25f);
                line.pickingMode = PickingMode.Ignore;
                Add(line);
            }

            VisualElement floor = new VisualElement();
            floor.AddToClassList("preview-floor");
            floor.pickingMode = PickingMode.Ignore;
            Add(floor);

            actor = new Label("施法者");
            actor.AddToClassList("preview-actor");
            actor.pickingMode = PickingMode.Ignore;
            Add(actor);

            AddTarget("主目标", "preview-target--main");
            AddTarget("副目标 1", "preview-target--1");
            AddTarget("副目标 2", "preview-target--2");
            AddTarget("副目标 3", "preview-target--3");

            beam = new VisualElement();
            beam.AddToClassList("preview-beam");
            beam.pickingMode = PickingMode.Ignore;
            Add(beam);

            impact = new VisualElement();
            impact.AddToClassList("preview-impact");
            impact.pickingMode = PickingMode.Ignore;
            Add(impact);

            overlayLabel = new Label("等待动作数据");
            overlayLabel.AddToClassList("preview-overlay-label");
            overlayLabel.pickingMode = PickingMode.Ignore;
            Add(overlayLabel);

            RegisterCallback<DetachFromPanelEvent>(_ => DisposePreview());
            SetTargetCount(1);
            UpdateFallbackVisibility();
            SetTickState(0, null);
        }

        public int TargetCount => targetCount;
        public int PreviewTargetCount => sceneContent == null
            ? targetCount
            : sceneContent.PreviewTargetCount;
        public int PreviewSceneTargetCount => sceneContent == null
            ? 0
            : sceneContent.PreviewTargetCount;
        public bool HasIsolatedPreviewScene =>
            sceneContent != null && sceneContent.HasPreviewScene;
        public FpgSkillPreviewSimulationFrame LastSimulationFrame =>
            lastSimulationFrame;
        public int LastSampledTick => lastSampledTick;
        public string OverlayText => overlayLabel.text;
        public int MeasuredAnimationDurationTicks { get; private set; } = -1;
        public double MeasuredAnimationDurationSeconds => spineAnimationDuration;
        public double LastSampledAnimationSeconds { get; private set; }
        internal int ActivePresentationVfxCount => presentationVfx.Count;
        internal int ActivePresentationShakeCount =>
            presentationShakes.Count;
        internal AudioSource PreviewAudioSource => previewAudioSource;

        public event Action<int> AnimationDurationMeasured;

        public void SetPreviewPrefab(GameObject prefab)
        {
            if (previewPrefab == prefab)
            {
                return;
            }

            DisposePreview();
            previewPrefab = prefab;
            if (prefab == null)
            {
                previewStatus = "未选择预览 Prefab";
                UpdateFallbackVisibility();
                return;
            }

            try
            {
                previewUtility = new PreviewRenderUtility();
                previewUtility.camera.fieldOfView = 30f;
                previewUtility.camera.clearFlags = CameraClearFlags.Color;
                previewUtility.camera.backgroundColor =
                    new Color(0.075f, 0.08f, 0.09f);
                previewUtility.lights[0].intensity = 1.15f;
                previewUtility.lights[0].transform.rotation =
                    Quaternion.Euler(35f, 35f, 0f);
                previewUtility.lights[1].intensity = 0.55f;

                previewInstance = UnityEngine.Object.Instantiate(prefab);
                previewInstance.name = prefab.name + " (Skill Preview)";
                previewInstance.hideFlags = HideFlags.HideAndDontSave;
                previewUtility.AddSingleGO(previewInstance);
                sceneContent = new FpgSkillPreviewSceneContent(
                    previewUtility,
                    previewInstance);
                sceneContent.SetTargetCount(targetCount);
                FindSpineComponent();
                FramePreviewCamera();
                ConfigureSpineAnimation();
                previewStatus = spineComponent == null
                    ? "Prefab 已加载，未找到 Spine SkeletonAnimation"
                    : "Spine 绝对 Tick 采样";
            }
            catch (Exception exception)
            {
                previewStatus = "预览初始化失败："
                    + exception.GetBaseException().Message;
                DisposePreview();
                previewPrefab = prefab;
            }

            UpdateFallbackVisibility();
            renderedPreview.MarkDirtyRepaint();
        }

        public void RefreshPreviewSource()
        {
            GameObject prefab = previewPrefab;
            if (prefab == null)
            {
                return;
            }

            previewPrefab = null;
            SetPreviewPrefab(prefab);
        }

        public bool TryPlayActivePresentation(
            SerializedProperty eventProperty,
            FpgSkillEventTrackKind track,
            bool allowAudio,
            out string error)
        {
            error = string.Empty;
            if (eventProperty == null)
            {
                error = "Active presentation preview requires an event.";
                return false;
            }

            SerializedProperty presentation =
                eventProperty.FindPropertyRelative("presentation");
            if (presentation == null)
            {
                error = "Active presentation preview has no typed data.";
                return false;
            }

            switch (track)
            {
                case FpgSkillEventTrackKind.PresentationVfx:
                    return TryPlayVfx(
                        presentation,
                        eventProperty,
                        out error);

                case FpgSkillEventTrackKind.PresentationAudio:
                    return !allowAudio
                        || TryPlayAudio(presentation, out error);

                case FpgSkillEventTrackKind.PresentationCameraShake:
                    return TryPlayCameraShake(presentation, out error);

                default:
                    error =
                        "Only active VFX, Audio and CameraShake can be previewed.";
                    return false;
            }
        }

        public void UpdatePresentationPreview()
        {
            double now = EditorApplication.timeSinceStartup;
            bool changed = UpdatePreviewVfx(now);
            changed |= UpdatePreviewShakes(now);
            if (changed)
            {
                renderedPreview.MarkDirtyRepaint();
            }
        }

        public void ClearPresentationPreview()
        {
            for (int index = presentationVfx.Count - 1; index >= 0; index--)
            {
                DestroyPreviewObject(presentationVfx[index].Instance);
            }

            presentationVfx.Clear();
            presentationShakes.Clear();
            RestorePreviewCamera();
            if (previewAudioSource != null)
            {
                previewAudioSource.Stop();
            }

            DestroyPreviewObject(previewAudioObject);
            previewAudioObject = null;
            previewAudioSource = null;
            renderedPreview.MarkDirtyRepaint();
        }

        private bool TryPlayVfx(
            SerializedProperty presentation,
            SerializedProperty eventProperty,
            out string error)
        {
            error = string.Empty;
            SerializedProperty prefabProperty =
                presentation.FindPropertyRelative("prefab");
            GameObject prefab = prefabProperty == null
                ? null
                : prefabProperty.objectReferenceValue as GameObject;
            if (prefab == null || previewUtility == null)
            {
                error =
                    "VFX preview requires a prefab and an isolated preview scene.";
                return false;
            }

            float duration = ReadFloat(
                presentation,
                "durationSeconds",
                1f);
            Vector3 scale = ReadVector3(
                presentation,
                "scale",
                Vector3.one);
            Vector3 rotationOffset = ReadVector3(
                presentation,
                "rotationOffsetEuler",
                Vector3.zero);
            SerializedProperty anchorProperty =
                eventProperty.FindPropertyRelative("anchor");
            bool ownerSocket = anchorProperty != null
                && anchorProperty.enumValueIndex == 1;
            string socketId = eventProperty
                .FindPropertyRelative("socketId")?.stringValue
                ?? string.Empty;

            Vector3 position = previewInstance == null
                ? Vector3.zero
                : previewInstance.transform.position;
            Vector3 forward = previewInstance == null
                ? Vector3.right
                : previewInstance.transform.right;
            if (ownerSocket
                && !TryResolvePreviewOrigin(
                    socketId,
                    out position,
                    out forward))
            {
                error = "VFX preview cannot resolve socket '" + socketId + "'.";
                return false;
            }

            GameObject instance;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = prefab.name + " (Active Presentation Preview)";
                instance.hideFlags = HideFlags.HideAndDontSave;
                Quaternion sourceRotation = forward.sqrMagnitude <= 0.000001f
                    ? Quaternion.identity
                    : Quaternion.FromToRotation(
                        Vector3.right,
                        forward.normalized);
                instance.transform.SetPositionAndRotation(
                    position,
                    sourceRotation * Quaternion.Euler(rotationOffset));
                Vector3 sourceScale = previewInstance == null
                    ? Vector3.one
                    : previewInstance.transform.lossyScale;
                instance.transform.localScale =
                    Vector3.Scale(sourceScale, scale);
                previewUtility.AddSingleGO(instance);
            }
            catch (Exception exception)
            {
                error = "VFX preview failed: "
                    + exception.GetBaseException().Message;
                return false;
            }

            ParticleSystem[] particles =
                instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < particles.Length; index++)
            {
                particles[index].Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[index].Play(true);
            }

            presentationVfx.Add(new PreviewVfxInstance(
                instance,
                particles,
                EditorApplication.timeSinceStartup,
                Mathf.Max(0.01f, duration)));
            renderedPreview.MarkDirtyRepaint();
            return true;
        }

        private bool TryPlayAudio(
            SerializedProperty presentation,
            out string error)
        {
            error = string.Empty;
            AudioClip clip = presentation.FindPropertyRelative("clip")
                ?.objectReferenceValue as AudioClip;
            float volume = Mathf.Clamp01(ReadFloat(
                presentation,
                "volume",
                1f));
            if (clip == null)
            {
                error = "Audio preview requires an AudioClip.";
                return false;
            }

            if (previewAudioSource == null)
            {
                previewAudioObject = new GameObject(
                    "Skill Active Audio Preview")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                previewAudioSource =
                    previewAudioObject.AddComponent<AudioSource>();
                previewAudioSource.playOnAwake = false;
                previewAudioSource.loop = false;
                previewAudioSource.spatialBlend = 0f;
                previewAudioSource.dopplerLevel = 0f;
                previewAudioSource.volume = 1f;
            }

            previewAudioSource.PlayOneShot(clip, volume);
            return true;
        }

        private bool TryPlayCameraShake(
            SerializedProperty presentation,
            out string error)
        {
            error = string.Empty;
            float strength = ReadFloat(presentation, "strength", 0f);
            float duration = ReadFloat(
                presentation,
                "durationSeconds",
                0.1f);
            if (float.IsNaN(strength)
                || float.IsInfinity(strength)
                || strength < 0f
                || float.IsNaN(duration)
                || float.IsInfinity(duration)
                || duration <= 0f)
            {
                error =
                    "CameraShake preview requires finite strength and duration.";
                return false;
            }

            if (strength <= 0f)
            {
                return true;
            }

            CapturePreviewCameraBaseline();
            presentationShakes.Add(new PreviewShakeImpulse(
                EditorApplication.timeSinceStartup,
                duration,
                strength));
            return true;
        }

        private bool UpdatePreviewVfx(double now)
        {
            bool changed = false;
            for (int index = presentationVfx.Count - 1; index >= 0; index--)
            {
                PreviewVfxInstance item = presentationVfx[index];
                double elapsed = Math.Max(0d, now - item.StartedAt);
                if (elapsed >= item.DurationSeconds || item.Instance == null)
                {
                    DestroyPreviewObject(item.Instance);
                    presentationVfx.RemoveAt(index);
                    changed = true;
                    continue;
                }

                float sampleTime = (float)Math.Min(
                    elapsed,
                    item.DurationSeconds);
                for (int particleIndex = 0;
                    particleIndex < item.Particles.Length;
                    particleIndex++)
                {
                    ParticleSystem particle = item.Particles[particleIndex];
                    if (particle != null)
                    {
                        particle.Simulate(
                            sampleTime,
                            true,
                            true,
                            false);
                    }
                }

                changed = true;
            }

            return changed;
        }

        private bool UpdatePreviewShakes(double now)
        {
            if (previewUtility == null)
            {
                presentationShakes.Clear();
                hasPreviewCameraBaseline = false;
                return false;
            }

            float combined = 0f;
            for (int index = presentationShakes.Count - 1;
                index >= 0;
                index--)
            {
                PreviewShakeImpulse item = presentationShakes[index];
                double elapsed = Math.Max(0d, now - item.StartedAt);
                if (elapsed >= item.DurationSeconds)
                {
                    presentationShakes.RemoveAt(index);
                    continue;
                }

                float remaining = 1f
                    - (float)(elapsed / item.DurationSeconds);
                combined += item.Strength * remaining;
            }

            if (presentationShakes.Count == 0)
            {
                bool changed = hasPreviewCameraBaseline;
                RestorePreviewCamera();
                return changed;
            }

            CapturePreviewCameraBaseline();
            float normalized = Mathf.Min(1f, combined);
            float phase = (float)(now * 32d);
            Transform cameraTransform = previewUtility.camera.transform;
            cameraTransform.position = previewCameraBasePosition
                + previewCameraBaseRotation * new Vector3(
                    Mathf.Sin(phase) * 0.035f * normalized,
                    Mathf.Cos(phase * 0.83f) * 0.025f * normalized,
                    0f);
            cameraTransform.rotation = previewCameraBaseRotation
                * Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Sin(phase * 1.17f) * 0.65f * normalized);
            return true;
        }

        private void CapturePreviewCameraBaseline()
        {
            if (hasPreviewCameraBaseline || previewUtility == null)
            {
                return;
            }

            previewCameraBasePosition =
                previewUtility.camera.transform.position;
            previewCameraBaseRotation =
                previewUtility.camera.transform.rotation;
            hasPreviewCameraBaseline = true;
        }

        private void RestorePreviewCamera()
        {
            if (!hasPreviewCameraBaseline || previewUtility == null)
            {
                hasPreviewCameraBaseline = false;
                return;
            }

            previewUtility.camera.transform.SetPositionAndRotation(
                previewCameraBasePosition,
                previewCameraBaseRotation);
            hasPreviewCameraBaseline = false;
        }

        private static float ReadFloat(
            SerializedProperty parent,
            string name,
            float fallback)
        {
            SerializedProperty property =
                parent?.FindPropertyRelative(name);
            return property == null ? fallback : property.floatValue;
        }

        private static Vector3 ReadVector3(
            SerializedProperty parent,
            string name,
            Vector3 fallback)
        {
            SerializedProperty property =
                parent?.FindPropertyRelative(name);
            return property == null ? fallback : property.vector3Value;
        }

        private static void DestroyPreviewObject(GameObject value)
        {
            if (value != null)
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }

        public void SetTargetCount(int count)
        {
            targetCount = Mathf.Clamp(count, 1, targets.Count);
            sceneContent?.SetTargetCount(targetCount);
            for (int index = 0; index < targets.Count; index++)
            {
                targets[index].style.display = previewInstance == null
                    && index < targetCount
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }

            if (sceneContent != null)
            {
                FramePreviewCamera();
                sceneContent.ApplyFrame(lastSimulationFrame, showGeometry);
                renderedPreview.MarkDirtyRepaint();
            }
        }

        public void SetGeometryVisible(bool visible)
        {
            showGeometry = visible;
            if (!visible)
            {
                beam.style.display = DisplayStyle.None;
                impact.style.display = DisplayStyle.None;
            }

            sceneContent?.ApplyFrame(lastSimulationFrame, showGeometry);
            renderedPreview.MarkDirtyRepaint();
        }

        public void SetAnimation(
            string name,
            FpgCompiledSkillSequence sequence)
        {
            bool changed = !string.Equals(
                    animationName,
                    name,
                    StringComparison.Ordinal)
                || !HasSameAnimationTiming(animationSequence, sequence);
            animationName = string.IsNullOrWhiteSpace(name)
                ? "未指定动作"
                : name;
            animationSequence = sequence;
            if (changed)
            {
                ConfigureSpineAnimation();
            }
        }

        private static bool HasSameAnimationTiming(
            FpgCompiledSkillSequence left,
            FpgCompiledSkillSequence right)
        {
            if (!left.IsValid || !right.IsValid)
            {
                return left.IsValid == right.IsValid;
            }

            return left.DurationTicks == right.DurationTicks
                && left.Loop == right.Loop
                && left.AnimationPlaybackMode == right.AnimationPlaybackMode
                && left.AnimationStartTick == right.AnimationStartTick
                && left.AnimationEndTick == right.AnimationEndTick;
        }

        public void SetTickState(
            int tick,
            IReadOnlyList<FpgSkillTimelineEventViewModel> activeEvents)
        {
            SetTickState(tick, activeEvents, null);
        }

        public void SetTickState(
            int tick,
            IReadOnlyList<FpgSkillTimelineEventViewModel> activeEvents,
            FpgSkillPreviewSimulationFrame simulationFrame)
        {
            lastSampledTick = tick;
            lastSimulationFrame = simulationFrame;
            SampleSpineAtTick(tick);
            sceneContent?.ApplyFrame(simulationFrame, showGeometry);
            renderedPreview.MarkDirtyRepaint();

            int activeCount = activeEvents == null ? 0 : activeEvents.Count;
            bool active = activeCount > 0;
            bool fallbackGeometry = active
                && showGeometry
                && previewInstance == null;
            beam.style.display = fallbackGeometry
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            impact.style.display = fallbackGeometry
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            string simulationSummary = simulationFrame?.BuildSummary()
                ?? string.Empty;
            if (!active)
            {
                overlayLabel.text = "Tick " + tick + " · " + animationName
                    + (string.IsNullOrWhiteSpace(simulationSummary)
                        ? " · 无技能事件"
                        : " · " + simulationSummary)
                    + " · " + previewStatus;
                return;
            }

            string label = activeEvents[0].Label;
            string headline = activeCount == 1
                ? "Tick " + tick + " · " + animationName + " · " + label
                    + " · " + previewStatus
                : "Tick " + tick + " · " + animationName + " · "
                    + activeCount + " 个技能事件 · " + previewStatus;
            string actionPreview = string.Empty;
            for (int index = 0; index < activeEvents.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(
                        activeEvents[index].PreviewSummary))
                {
                    actionPreview = activeEvents[index].PreviewSummary;
                    break;
                }
            }

            string detail = !string.IsNullOrWhiteSpace(simulationSummary)
                ? simulationSummary
                : actionPreview;
            overlayLabel.text = string.IsNullOrWhiteSpace(detail)
                ? headline
                : headline + "\n" + detail;
        }

        private void AddTarget(string text, string modifierClass)
        {
            Label target = new Label(text);
            target.AddToClassList("preview-target");
            target.AddToClassList(modifierClass);
            target.pickingMode = PickingMode.Ignore;
            VisualElement weakpoint = new VisualElement();
            weakpoint.AddToClassList("preview-weakpoint");
            weakpoint.tooltip = text + " Weakpoint";
            weakpoint.pickingMode = PickingMode.Ignore;
            target.Add(weakpoint);
            targets.Add(target);
            Add(target);
        }

        public FpgSkillPreviewTarget GetPreviewTarget(int index)
        {
            if (sceneContent != null)
            {
                return sceneContent.GetPreviewTarget(index);
            }

            return CreateFallbackTarget(index);
        }

        public bool TryResolvePreviewOrigin(
            string socketId,
            out Vector3 position,
            out Vector3 forward)
        {
            if (sceneContent != null)
            {
                return sceneContent.TryResolvePreviewOrigin(
                    socketId,
                    out position,
                    out forward);
            }

            position = Vector3.zero;
            forward = Vector3.right;
            return string.IsNullOrWhiteSpace(socketId);
        }

        private static FpgSkillPreviewTarget CreateFallbackTarget(
            int index)
        {
            int normalized = Mathf.Clamp(index, 0, 3);
            Vector3[] positions =
            {
                new Vector3(3.2f, 0f, 0f),
                new Vector3(4.7f, 0.85f, 0f),
                new Vector3(4.9f, -0.85f, 0f),
                new Vector3(6.1f, 0.25f, 0f)
            };
            Vector3 root = positions[normalized];
            return new FpgSkillPreviewTarget(
                normalized,
                normalized == 0
                    ? "主假人"
                    : "副假人 " + normalized,
                root + Vector3.up * 0.85f,
                0.5f,
                root + Vector3.up * 1.78f,
                0.24f);
        }

        private void UpdateFallbackVisibility()
        {
            bool fallback = previewInstance == null;
            actor.style.display = fallback
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            renderedPreview.style.display = fallback
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            for (int index = 0; index < targets.Count; index++)
            {
                targets[index].style.display = fallback
                    && index < targetCount
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }
        }

        private void DrawRenderedPreview()
        {
            if (previewUtility == null || previewInstance == null
                || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Rect rect = renderedPreview.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
            {
                return;
            }

            previewUtility.BeginPreview(rect, GUIStyle.none);
            previewUtility.camera.Render();
            Texture texture = previewUtility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        private void FindSpineComponent()
        {
            spineComponent = null;
            Component[] components = previewInstance.GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    continue;
                }

                string fullName = component.GetType().FullName;
                if (string.Equals(
                        fullName,
                        "Spine.Unity.SkeletonAnimation",
                        StringComparison.Ordinal)
                    || string.Equals(
                        fullName,
                        "Spine.Unity.SkeletonMecanim",
                        StringComparison.Ordinal))
                {
                    spineComponent = component;
                    MethodInfo initialize = component.GetType().GetMethod(
                        "Initialize",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new[] { typeof(bool) },
                        null);
                    initialize?.Invoke(component, new object[] { true });
                    return;
                }
            }
        }

        private void ConfigureSpineAnimation()
        {
            spineAnimationState = null;
            spineTrackEntry = null;
            spineAnimationDuration = 0f;
            SetMeasuredAnimationDurationTicks(-1);
            if (spineComponent == null
                || string.IsNullOrWhiteSpace(animationName)
                || animationName == "未指定动作")
            {
                return;
            }

            try
            {
                PropertyInfo stateProperty = spineComponent.GetType().GetProperty(
                    "AnimationState",
                    BindingFlags.Instance | BindingFlags.Public);
                spineAnimationState = stateProperty?.GetValue(spineComponent, null);
                if (spineAnimationState == null)
                {
                    previewStatus = "Spine AnimationState 不可用";
                    return;
                }

                MethodInfo setAnimation = spineAnimationState.GetType().GetMethod(
                    "SetAnimation",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(int), typeof(string), typeof(bool) },
                    null);
                spineTrackEntry = setAnimation?.Invoke(
                    spineAnimationState,
                    new object[]
                    {
                        0,
                        animationName,
                        animationSequence.IsValid && animationSequence.Loop
                    });
                object animation = spineTrackEntry?.GetType()
                    .GetProperty("Animation")?.GetValue(spineTrackEntry, null);
                object duration = animation?.GetType()
                    .GetProperty("Duration")?.GetValue(animation, null);
                if (duration != null)
                {
                    spineAnimationDuration = Convert.ToSingle(duration);
                    if (spineAnimationDuration > 0f
                        && !float.IsNaN(spineAnimationDuration)
                        && !float.IsInfinity(spineAnimationDuration))
                    {
                        SetMeasuredAnimationDurationTicks(Mathf.Max(
                            1,
                            Mathf.RoundToInt(
                                spineAnimationDuration * TickRate)));
                    }
                }

                previewStatus = spineTrackEntry == null
                    ? "Spine 动画不存在：" + animationName
                    : "Spine 绝对 Tick 采样";
            }
            catch (Exception exception)
            {
                previewStatus = "Spine 动画配置失败："
                    + exception.GetBaseException().Message;
            }
        }

        private void SetMeasuredAnimationDurationTicks(int ticks)
        {
            int normalized = ticks > 0 ? ticks : -1;
            if (MeasuredAnimationDurationTicks == normalized)
            {
                return;
            }

            MeasuredAnimationDurationTicks = normalized;
            AnimationDurationMeasured?.Invoke(normalized);
        }

        private void SampleSpineAtTick(int tick)
        {
            if (spineComponent == null || spineTrackEntry == null)
            {
                renderedPreview.MarkDirtyRepaint();
                return;
            }

            try
            {
                if (!animationSequence.IsValid)
                {
                    renderedPreview.MarkDirtyRepaint();
                    return;
                }

                int clampedTick = Mathf.Clamp(
                    tick,
                    0,
                    animationSequence.DurationTicks);
                LastSampledAnimationSeconds =
                    FpgSkillAnimationTime.EvaluateSeconds(
                        animationSequence,
                        clampedTick,
                        0d,
                        spineAnimationDuration);
                float sampleTime = (float)LastSampledAnimationSeconds;

                PropertyInfo trackTime = spineTrackEntry.GetType().GetProperty(
                    "TrackTime",
                    BindingFlags.Instance | BindingFlags.Public);
                trackTime?.SetValue(spineTrackEntry, sampleTime, null);
                MethodInfo update = spineComponent.GetType().GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(float) },
                    null);
                update?.Invoke(spineComponent, new object[] { 0f });
                ApplySpinePose();
                renderedPreview.MarkDirtyRepaint();
            }
            catch (Exception exception)
            {
                previewStatus = "Spine 采样失败：" + exception.GetBaseException().Message;
            }
        }

        private void ApplySpinePose()
        {
            object skeleton = spineComponent.GetType().GetProperty(
                "Skeleton",
                BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(spineComponent, null);
            if (skeleton != null && spineAnimationState != null)
            {
                MethodInfo[] methods = spineAnimationState.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public);
                for (int index = 0; index < methods.Length; index++)
                {
                    ParameterInfo[] parameters = methods[index].GetParameters();
                    if (methods[index].Name == "Apply"
                        && parameters.Length == 1
                        && parameters[0].ParameterType.IsInstanceOfType(skeleton))
                    {
                        methods[index].Invoke(spineAnimationState, new[] { skeleton });
                        break;
                    }
                }
            }

            MethodInfo lateUpdate = spineComponent.GetType().GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            lateUpdate?.Invoke(spineComponent, null);
        }

        private void FramePreviewCamera()
        {
            if (previewUtility == null || previewInstance == null)
            {
                return;
            }

            RestorePreviewCamera();
            Bounds bounds = sceneContent == null
                ? CalculateBounds(previewInstance)
                : sceneContent.GetCombinedBounds();
            float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
            Vector3 center = bounds.center;
            previewUtility.camera.transform.position = center
                + new Vector3(0f, radius * 0.12f, -radius * 2.7f);
            previewUtility.camera.transform.LookAt(center);
            previewUtility.camera.nearClipPlane =
                Mathf.Max(0.01f, radius * 0.01f);
            previewUtility.camera.farClipPlane =
                Mathf.Max(10f, radius * 12f);
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(
                true);
            Bounds bounds = new Bounds(
                root.transform.position,
                new Vector3(2f, 2f, 1f));
            bool found = false;
            for (int index = 0; index < renderers.Length; index++)
            {
                if (!renderers[index].enabled)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderers[index].bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }
            }

            return bounds;
        }

        private void DisposePreview()
        {
            ClearPresentationPreview();
            spineComponent = null;
            spineAnimationState = null;
            spineTrackEntry = null;
            spineAnimationDuration = 0f;
            MeasuredAnimationDurationTicks = -1;
            LastSampledAnimationSeconds = 0d;
            lastSimulationFrame = null;
            sceneContent?.Dispose();
            sceneContent = null;
            previewInstance = null;
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }

            UpdateFallbackVisibility();
        }

        private sealed class PreviewVfxInstance
        {
            public PreviewVfxInstance(
                GameObject instance,
                ParticleSystem[] particles,
                double startedAt,
                float durationSeconds)
            {
                Instance = instance;
                Particles = particles ?? Array.Empty<ParticleSystem>();
                StartedAt = startedAt;
                DurationSeconds = durationSeconds;
            }

            public GameObject Instance { get; }
            public ParticleSystem[] Particles { get; }
            public double StartedAt { get; }
            public float DurationSeconds { get; }
        }

        private readonly struct PreviewShakeImpulse
        {
            public PreviewShakeImpulse(
                double startedAt,
                float durationSeconds,
                float strength)
            {
                StartedAt = startedAt;
                DurationSeconds = durationSeconds;
                Strength = strength;
            }

            public double StartedAt { get; }
            public float DurationSeconds { get; }
            public float Strength { get; }
        }
    }
}
