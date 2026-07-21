using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NewFPG.CZN
{
    [ExecuteAlways]
    public sealed class CznSpineSkillPlayer : MonoBehaviour
    {
        private const float ReplayRewindTolerance = 0.0001f;

        [Header("Scene bindings")]
        [SerializeField] private SkeletonAnimation actor;
        [SerializeField] private SkeletonAnimation standbyActor;
        [SerializeField] private Transform selfAnchor;
        [SerializeField] private Transform targetAnchor;
        [SerializeField] private Transform standbyAnchor;
        [SerializeField] private Transform centerAnchor;
        [SerializeField] private Transform screenAnchor;
        [SerializeField] private Transform effectRoot;
        [SerializeField] private Transform cameraShakeRoot;
        [SerializeField] private Camera previewCamera;

        [Header("Rendering")]
        [SerializeField] private Material fallbackParticleMaterial;
        [SerializeField] private Material fallbackAdditiveParticleMaterial;
        [SerializeField] private string idleAnimation = "idle";
        [SerializeField] private bool showRuntimeObjectsInHierarchy = true;

        private readonly List<SpineLayerRuntime> spineLayerRuntimes = new List<SpineLayerRuntime>();
        private readonly List<ParticleLayerRuntime> particleLayerRuntimes = new List<ParticleLayerRuntime>();
        private CznSpineSkillSequence currentSequence;
        private string currentActorAnimation;
        private TrackEntry currentActorTrack;
        private Vector3 cameraBaseLocalPosition;
        private Quaternion cameraBaseLocalRotation;
        private Vector3 targetBaseLocalPosition;
        private Quaternion targetBaseLocalRotation;
        private Vector3 targetBaseLocalScale;
        private Vector3 selfBaseLocalPosition;
        private Quaternion selfBaseLocalRotation;
        private Vector3 selfBaseLocalScale;
        private Vector3 standbyBaseLocalPosition;
        private Quaternion standbyBaseLocalRotation;
        private Vector3 standbyBaseLocalScale;
        private float cameraBaseOrthographicSize;
        private bool basesCaptured;
        private bool standbyVisible;
        private TrackEntry standbyTrack;

        public CznSpineSkillSequence CurrentSequence => currentSequence;
        public double LastEvaluatedTime { get; private set; }
        public int ActiveSpineLayerCount { get; private set; }
        public int ActiveParticleLayerCount { get; private set; }
        public string ActiveMarkerLabel { get; private set; }
        public string CurrentActorAnimationName => currentActorAnimation;
        public string IdleAnimationName => idleAnimation;
        public bool IsCurrentActorAnimationLooping => currentActorTrack != null && currentActorTrack.Loop;
        public bool IsStandbyVisible => standbyVisible;

        public void SetIdleAnimation(string animationName)
        {
            if (!string.IsNullOrWhiteSpace(animationName))
            {
                idleAnimation = animationName.Trim();
            }
        }

        public void Configure(
            SkeletonAnimation actorBinding,
            Transform selfBinding,
            Transform targetBinding,
            Transform centerBinding,
            Transform screenBinding,
            Transform effectBinding,
            Transform cameraShakeBinding,
            Camera cameraBinding,
            Material particleMaterial)
        {
            Configure(
                actorBinding,
                null,
                selfBinding,
                targetBinding,
                null,
                centerBinding,
                screenBinding,
                effectBinding,
                cameraShakeBinding,
                cameraBinding,
                particleMaterial);
        }

        public void Configure(
            SkeletonAnimation actorBinding,
            SkeletonAnimation standbyActorBinding,
            Transform selfBinding,
            Transform targetBinding,
            Transform standbyBinding,
            Transform centerBinding,
            Transform screenBinding,
            Transform effectBinding,
            Transform cameraShakeBinding,
            Camera cameraBinding,
            Material particleMaterial)
        {
            Configure(
                actorBinding,
                standbyActorBinding,
                selfBinding,
                targetBinding,
                standbyBinding,
                centerBinding,
                screenBinding,
                effectBinding,
                cameraShakeBinding,
                cameraBinding,
                particleMaterial,
                null);
        }

        public void Configure(
            SkeletonAnimation actorBinding,
            SkeletonAnimation standbyActorBinding,
            Transform selfBinding,
            Transform targetBinding,
            Transform standbyBinding,
            Transform centerBinding,
            Transform screenBinding,
            Transform effectBinding,
            Transform cameraShakeBinding,
            Camera cameraBinding,
            Material particleMaterial,
            Material additiveParticleMaterial)
        {
            actor = actorBinding;
            standbyActor = standbyActorBinding;
            selfAnchor = selfBinding;
            targetAnchor = targetBinding;
            standbyAnchor = standbyBinding;
            centerAnchor = centerBinding;
            screenAnchor = screenBinding;
            effectRoot = effectBinding;
            cameraShakeRoot = cameraShakeBinding;
            previewCamera = cameraBinding;
            fallbackParticleMaterial = particleMaterial;
            fallbackAdditiveParticleMaterial = additiveParticleMaterial;
            CaptureBaseTransforms();
        }

        private void Awake()
        {
            CaptureBaseTransforms();
        }

        private void OnEnable()
        {
            CaptureBaseTransforms();
        }

        private void OnDisable()
        {
            ClearRuntimeLayers();
            ResetStandbyPlaybackState();
            RestoreBaseTransforms();
        }

        public void Evaluate(CznSpineSkillSequence sequence, double time)
        {
            if (sequence == null)
            {
                ResetToIdle();
                return;
            }

            if (!basesCaptured)
            {
                CaptureBaseTransforms();
            }

            float sampleTime = Mathf.Clamp((float)time, 0f, sequence.Duration);
            bool rewound = currentSequence == sequence &&
                           sampleTime + ReplayRewindTolerance < LastEvaluatedTime;

            if (currentSequence != sequence)
            {
                PrepareSequence(sequence);
            }
            else if (rewound)
            {
                ResetRuntimePlaybackState();
            }

            LastEvaluatedTime = sampleTime;
            RestoreBaseTransforms();
            EvaluateActor(sequence, sampleTime);
            EvaluateSpineLayers(sampleTime);
            EvaluateParticleLayers(sampleTime);
            float cameraPathZoom = EvaluateTransformCues(sequence, sampleTime);
            EvaluateStandby(sequence, sampleTime);
            EvaluateCameraZoom(sequence, sampleTime, cameraPathZoom);
            EvaluateMarkers(sequence, sampleTime);
        }

        public void RestartSequence(CznSpineSkillSequence sequence)
        {
            if (sequence == null)
            {
                ResetToIdle();
                return;
            }

            if (!basesCaptured)
            {
                CaptureBaseTransforms();
            }

            if (currentSequence != sequence)
            {
                PrepareSequence(sequence);
            }
            else
            {
                ResetRuntimePlaybackState();
            }

            LastEvaluatedTime = 0d;
            ActiveMarkerLabel = string.Empty;
            RestoreBaseTransforms();
        }

        public void ResetToIdle()
        {
            ClearRuntimeLayers();
            currentSequence = null;
            LastEvaluatedTime = 0d;
            ActiveMarkerLabel = string.Empty;
            RestoreBaseTransforms();
            ResetStandbyPlaybackState();
            currentActorAnimation = idleAnimation;
            currentActorTrack = null;

            if (actor == null)
            {
                return;
            }

            actor.Initialize(false);
            actor.ClearState();
            actor.timeScale = 1f;
            if (actor.AnimationState != null && !string.IsNullOrWhiteSpace(idleAnimation))
            {
                currentActorTrack = actor.AnimationState.SetAnimation(0, idleAnimation, true);
            }

            actor.Update(0f);
        }

        private void PrepareSequence(CznSpineSkillSequence sequence)
        {
            ClearRuntimeLayers();
            currentSequence = sequence;
            ResetActorPlaybackState();
            ResetStandbyPlaybackState();

            for (int i = 0; i < sequence.SpineLayers.Count; i++)
            {
                CznSpineLayerCue cue = sequence.SpineLayers[i];
                if (cue == null || cue.skeletonDataAsset == null)
                {
                    continue;
                }

                SkeletonAnimation skeleton = SkeletonAnimation.NewSkeletonAnimationGameObject(cue.skeletonDataAsset, true);
                if (skeleton == null)
                {
                    continue;
                }

                GameObject layerObject = skeleton.gameObject;
                layerObject.name = "[CZN] " + cue.sourceName;
                layerObject.hideFlags = RuntimeHideFlags();
                Transform anchor = ResolveAnchor(cue.anchor);
                layerObject.transform.SetParent(anchor != null ? anchor : effectRoot, false);
                layerObject.transform.localPosition = new Vector3(cue.offset.x, cue.offset.y, 0f);
                layerObject.transform.localRotation = Quaternion.Euler(0f, 0f, cue.rotation);
                layerObject.transform.localScale = Vector3.one * Mathf.Max(0.0001f, cue.scale);
                skeleton.timeScale = 0f;
                skeleton.loop = cue.loop;
                if (skeleton.Skeleton != null)
                {
                    skeleton.Skeleton.A = Mathf.Clamp01(cue.alpha);
                }

                MeshRenderer meshRenderer = layerObject.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sortingOrder = cue.sortingOrder;
                }

                layerObject.SetActive(false);
                spineLayerRuntimes.Add(new SpineLayerRuntime(cue, skeleton));
            }

            for (int i = 0; i < sequence.ParticleLayers.Count; i++)
            {
                CznParticleLayerCue cue = sequence.ParticleLayers[i];
                if (cue == null)
                {
                    continue;
                }

                GameObject particleObject = new GameObject("[CZN Particle] " + cue.sourceName + "/" + cue.emitterName);
                particleObject.hideFlags = RuntimeHideFlags();
                Transform anchor = ResolveAnchor(cue.anchor);
                particleObject.transform.SetParent(anchor != null ? anchor : effectRoot, false);
                particleObject.transform.localPosition = new Vector3(cue.offset.x, cue.offset.y, 0f);
                particleObject.transform.localRotation = Quaternion.Euler(0f, 0f, cue.rotation);
                particleObject.transform.localScale = Vector3.one * Mathf.Max(0.0001f, cue.scale);
                ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
                ConfigureParticleSystem(particleSystem, cue);
                particleObject.SetActive(false);
                particleLayerRuntimes.Add(new ParticleLayerRuntime(cue, particleSystem));
            }
        }

        private void ResetRuntimePlaybackState()
        {
            ResetActorPlaybackState();
            ResetStandbyPlaybackState();

            for (int i = 0; i < spineLayerRuntimes.Count; i++)
            {
                spineLayerRuntimes[i].ResetForReplay();
            }

            for (int i = 0; i < particleLayerRuntimes.Count; i++)
            {
                particleLayerRuntimes[i].ResetForReplay();
            }

            ActiveSpineLayerCount = 0;
            ActiveParticleLayerCount = 0;
            ActiveMarkerLabel = string.Empty;
            RestoreBaseTransforms();
        }

        private void ResetActorPlaybackState()
        {
            currentActorAnimation = null;
            currentActorTrack = null;

            if (actor == null)
            {
                return;
            }

            actor.Initialize(false);
            actor.ClearState();
            actor.timeScale = 0f;
        }

        private void ResetStandbyPlaybackState()
        {
            standbyTrack = null;
            standbyVisible = false;
            if (standbyActor == null)
            {
                return;
            }

            GameObject standbyObject = standbyActor.gameObject;
            if (!standbyObject.activeSelf)
            {
                standbyObject.SetActive(true);
            }
            standbyActor.Initialize(false);
            standbyActor.ClearState();
            standbyActor.timeScale = 0f;
            standbyObject.SetActive(false);
        }

        private void EvaluateActor(CznSpineSkillSequence sequence, float time)
        {
            if (actor == null || actor.AnimationState == null)
            {
                return;
            }

            CznActorAnimationCue activeCue = null;
            for (int i = 0; i < sequence.ActorAnimations.Count; i++)
            {
                CznActorAnimationCue cue = sequence.ActorAnimations[i];
                if (cue == null || time < cue.startTime)
                {
                    continue;
                }

                activeCue = cue;
            }

            float latestIdleTime = float.NegativeInfinity;
            for (int i = 0; i < sequence.Markers.Count; i++)
            {
                CznSkillMarkerCue marker = sequence.Markers[i];
                if (marker != null &&
                    marker.startTime <= time &&
                    string.Equals(marker.kind, "IDLE", StringComparison.OrdinalIgnoreCase))
                {
                    latestIdleTime = Mathf.Max(latestIdleTime, marker.startTime);
                }
            }

            if (activeCue != null && latestIdleTime >= activeCue.startTime)
            {
                activeCue = null;
            }

            string animationName = activeCue != null ? activeCue.animationName : idleAnimation;
            bool loop = activeCue != null && activeCue.loop;
            float localTime = activeCue != null
                ? Mathf.Max(0f, time - activeCue.startTime)
                : 0f;
            if (activeCue != null && !activeCue.loop)
            {
                localTime = Mathf.Min(localTime, Mathf.Max(0.0001f, activeCue.duration));
            }

            if (currentActorAnimation != animationName || currentActorTrack == null)
            {
                currentActorAnimation = animationName;
                currentActorTrack = actor.AnimationState.SetAnimation(0, animationName, loop);
            }

            if (currentActorTrack != null)
            {
                currentActorTrack.TrackTime = localTime;
                currentActorTrack.AnimationLast = localTime;
            }

            actor.Update(0f);
        }

        private void EvaluateStandby(CznSpineSkillSequence sequence, float time)
        {
            if (standbyActor == null)
            {
                return;
            }

            bool visible = false;
            float visibleStart = 0f;
            for (int i = 0; i < sequence.Markers.Count; i++)
            {
                CznSkillMarkerCue marker = sequence.Markers[i];
                if (marker == null || marker.startTime > time)
                {
                    continue;
                }

                switch ((marker.kind ?? string.Empty).Trim().ToUpperInvariant())
                {
                    case "STANDBY_ON":
                        visible = true;
                        visibleStart = marker.startTime;
                        break;
                    case "STANDBY_ACTION":
                        visible = true;
                        break;
                    case "STANDBY_OFF":
                        visible = false;
                        break;
                }
            }

            if (!visible)
            {
                if (standbyVisible)
                {
                    ResetStandbyPlaybackState();
                }
                return;
            }

            if (!standbyVisible)
            {
                standbyActor.gameObject.SetActive(true);
                standbyActor.Initialize(false);
                standbyActor.ClearState();
                standbyActor.timeScale = 0f;
                standbyTrack = standbyActor.AnimationState?.SetAnimation(0, "b_idle", true);
                standbyVisible = true;
            }

            float localTime = Mathf.Max(0f, time - visibleStart);
            if (standbyTrack != null)
            {
                standbyTrack.TrackTime = localTime;
                standbyTrack.AnimationLast = localTime;
            }
            standbyActor.Update(0f);
        }

        private void EvaluateSpineLayers(float time)
        {
            ActiveSpineLayerCount = 0;
            for (int i = 0; i < spineLayerRuntimes.Count; i++)
            {
                SpineLayerRuntime runtime = spineLayerRuntimes[i];
                CznSpineLayerCue cue = runtime.Cue;
                float localTime = time - cue.startTime;
                bool active = localTime >= 0f && localTime <= Mathf.Max(0.0001f, cue.duration);
                if (!active)
                {
                    runtime.SetActive(false);
                    continue;
                }

                ActiveSpineLayerCount++;
                runtime.SetActive(true);
                runtime.Evaluate(localTime);
            }
        }

        private void EvaluateParticleLayers(float time)
        {
            ActiveParticleLayerCount = 0;
            for (int i = 0; i < particleLayerRuntimes.Count; i++)
            {
                ParticleLayerRuntime runtime = particleLayerRuntimes[i];
                CznParticleLayerCue cue = runtime.Cue;
                float localTime = time - cue.startTime;
                bool active = localTime >= 0f && localTime <= Mathf.Max(0.0001f, cue.duration);
                if (!active)
                {
                    runtime.SetActive(false);
                    continue;
                }

                ActiveParticleLayerCount++;
                runtime.SetActive(true);
                runtime.Evaluate(localTime);
            }
        }

        private float EvaluateTransformCues(CznSpineSkillSequence sequence, float time)
        {
            float cameraPathZoom = 1f;
            for (int i = 0; i < sequence.TransformCues.Count; i++)
            {
                CznTransformCue cue = sequence.TransformCues[i];
                if (cue == null)
                {
                    continue;
                }

                float localTime = time - cue.startTime;
                if (localTime < 0f || localTime > Mathf.Max(0.0001f, cue.duration))
                {
                    continue;
                }

                Vector2 translation = Sample(cue.translateKeys, localTime, Vector2.zero) * cue.positionScale;
                float rotation = Sample(cue.rotateKeys, localTime, 0f);
                Vector2 scale = Sample(cue.scaleKeys, localTime, Vector2.one);

                switch (cue.target)
                {
                    case CznTransformTarget.Camera:
                        if (cameraShakeRoot != null)
                        {
                            cameraShakeRoot.localPosition += new Vector3(translation.x, translation.y, 0f);
                            cameraShakeRoot.localRotation *= Quaternion.Euler(0f, 0f, rotation);
                        }
                        cameraPathZoom *= Mathf.Max(0.01f, Mathf.Abs(scale.x));
                        break;

                    case CznTransformTarget.Target:
                        if (targetAnchor != null)
                        {
                            targetAnchor.localPosition += new Vector3(translation.x, translation.y, 0f);
                            targetAnchor.localRotation *= Quaternion.Euler(0f, 0f, rotation);
                            targetAnchor.localScale = Vector3.Scale(targetAnchor.localScale, new Vector3(scale.x, scale.y, 1f));
                        }
                        break;

                    case CznTransformTarget.Self:
                        if (selfAnchor != null)
                        {
                            selfAnchor.localPosition += new Vector3(translation.x, translation.y, 0f);
                            selfAnchor.localRotation *= Quaternion.Euler(0f, 0f, rotation);
                            selfAnchor.localScale = Vector3.Scale(selfAnchor.localScale, new Vector3(scale.x, scale.y, 1f));
                        }
                        break;

                    case CznTransformTarget.Standby:
                        if (standbyAnchor != null)
                        {
                            standbyAnchor.localPosition += new Vector3(translation.x, translation.y, 0f);
                            standbyAnchor.localRotation *= Quaternion.Euler(0f, 0f, rotation);
                            standbyAnchor.localScale = Vector3.Scale(
                                standbyAnchor.localScale,
                                new Vector3(scale.x, scale.y, 1f));
                        }
                        break;
                }
            }
            return cameraPathZoom;
        }

        private void EvaluateCameraZoom(CznSpineSkillSequence sequence, float time, float cameraPathZoom)
        {
            if (previewCamera == null || !previewCamera.orthographic)
            {
                return;
            }

            float zoom = 1f;
            for (int i = 0; i < sequence.CameraZoomCues.Count; i++)
            {
                CznCameraZoomCue cue = sequence.CameraZoomCues[i];
                if (cue == null || time < cue.startTime)
                {
                    continue;
                }

                if (cue.duration <= 0.0001f || time >= cue.startTime + cue.duration)
                {
                    zoom = Mathf.Max(0.01f, cue.zoom);
                    continue;
                }

                float t = Mathf.InverseLerp(cue.startTime, cue.startTime + cue.duration, time);
                zoom = Mathf.Lerp(zoom, Mathf.Max(0.01f, cue.zoom), t);
                break;
            }

            previewCamera.orthographicSize = cameraBaseOrthographicSize /
                                             (zoom * Mathf.Max(0.01f, cameraPathZoom));
        }

        private void EvaluateMarkers(CznSpineSkillSequence sequence, float time)
        {
            ActiveMarkerLabel = string.Empty;
            for (int i = 0; i < sequence.Markers.Count; i++)
            {
                CznSkillMarkerCue cue = sequence.Markers[i];
                if (cue == null)
                {
                    continue;
                }

                float visibleDuration = Mathf.Max(0.08f, cue.duration);
                if (time >= cue.startTime && time <= cue.startTime + visibleDuration)
                {
                    ActiveMarkerLabel = string.IsNullOrWhiteSpace(cue.label) ? cue.kind : cue.label;
                }
            }
        }

        private void ConfigureParticleSystem(ParticleSystem system, CznParticleLayerCue cue)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(1, cue.maxParticles);
            main.duration = Mathf.Max(0.05f, cue.duration - cue.lifetimeMax);
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.01f, cue.lifetimeMin),
                Mathf.Max(cue.lifetimeMin, cue.lifetimeMax));
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.001f, cue.sizeMin),
                Mathf.Max(cue.sizeMin, cue.sizeMax));
            main.startRotation = new ParticleSystem.MinMaxCurve(
                -cue.rotationVariance * Mathf.Deg2Rad,
                cue.rotationVariance * Mathf.Deg2Rad);
            main.startColor = cue.startColor;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Max(0f, cue.emissionRate);

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = cue.sourceVariance.sqrMagnitude > 0.000001f;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(
                Mathf.Max(0.001f, cue.sourceVariance.x * 2f),
                Mathf.Max(0.001f, cue.sourceVariance.y * 2f),
                0.001f);

            float minAngle = (cue.angle - cue.angleVariance) * Mathf.Deg2Rad;
            float maxAngle = (cue.angle + cue.angleVariance) * Mathf.Deg2Rad;
            Vector2 velocityA = new Vector2(Mathf.Cos(minAngle), Mathf.Sin(minAngle)) * cue.speedMin;
            Vector2 velocityB = new Vector2(Mathf.Cos(maxAngle), Mathf.Sin(maxAngle)) * cue.speedMax;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = cue.speedMax > 0.0001f || cue.speedMin > 0.0001f;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(Mathf.Min(velocityA.x, velocityB.x), Mathf.Max(velocityA.x, velocityB.x));
            velocity.y = new ParticleSystem.MinMaxCurve(Mathf.Min(velocityA.y, velocityB.y), Mathf.Max(velocityA.y, velocityB.y));
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
            force.enabled = cue.force.sqrMagnitude > 0.000001f;
            force.space = ParticleSystemSimulationSpace.Local;
            force.x = cue.force.x;
            force.y = cue.force.y;

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(cue.startColor, 0f),
                    new GradientColorKey(cue.endColor, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(cue.startColor.a, 0f),
                    new GradientAlphaKey(cue.endColor.a, 1f),
                });
            color.color = gradient;

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortingOrder = cue.sortingOrder;
            Material particleMaterial = cue.additive && fallbackAdditiveParticleMaterial != null
                ? fallbackAdditiveParticleMaterial
                : fallbackParticleMaterial;
            if (particleMaterial != null)
            {
                renderer.sharedMaterial = particleMaterial;
            }
            if (cue.texture != null)
            {
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetTexture("_MainTex", cue.texture);
                properties.SetTexture("_BaseMap", cue.texture);
                properties.SetTexture("Texture2D_F593E37E", cue.texture);
                renderer.SetPropertyBlock(properties);
            }

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private Transform ResolveAnchor(CznSkillAnchor anchor)
        {
            switch (anchor)
            {
                case CznSkillAnchor.Target:
                    return targetAnchor != null ? targetAnchor : effectRoot;
                case CznSkillAnchor.Center:
                    return centerAnchor != null ? centerAnchor : effectRoot;
                case CznSkillAnchor.Screen:
                    return screenAnchor != null ? screenAnchor : effectRoot;
                default:
                    return selfAnchor != null ? selfAnchor : effectRoot;
            }
        }

        private void CaptureBaseTransforms()
        {
            if (cameraShakeRoot != null)
            {
                cameraBaseLocalPosition = cameraShakeRoot.localPosition;
                cameraBaseLocalRotation = cameraShakeRoot.localRotation;
            }

            if (targetAnchor != null)
            {
                targetBaseLocalPosition = targetAnchor.localPosition;
                targetBaseLocalRotation = targetAnchor.localRotation;
                targetBaseLocalScale = targetAnchor.localScale;
            }

            if (selfAnchor != null)
            {
                selfBaseLocalPosition = selfAnchor.localPosition;
                selfBaseLocalRotation = selfAnchor.localRotation;
                selfBaseLocalScale = selfAnchor.localScale;
            }

            if (standbyAnchor != null)
            {
                standbyBaseLocalPosition = standbyAnchor.localPosition;
                standbyBaseLocalRotation = standbyAnchor.localRotation;
                standbyBaseLocalScale = standbyAnchor.localScale;
            }

            if (previewCamera != null)
            {
                cameraBaseOrthographicSize = previewCamera.orthographicSize;
            }

            basesCaptured = true;
        }

        private void RestoreBaseTransforms()
        {
            if (!basesCaptured)
            {
                return;
            }

            if (cameraShakeRoot != null)
            {
                cameraShakeRoot.localPosition = cameraBaseLocalPosition;
                cameraShakeRoot.localRotation = cameraBaseLocalRotation;
            }

            if (targetAnchor != null)
            {
                targetAnchor.localPosition = targetBaseLocalPosition;
                targetAnchor.localRotation = targetBaseLocalRotation;
                targetAnchor.localScale = targetBaseLocalScale;
            }

            if (selfAnchor != null)
            {
                selfAnchor.localPosition = selfBaseLocalPosition;
                selfAnchor.localRotation = selfBaseLocalRotation;
                selfAnchor.localScale = selfBaseLocalScale;
            }

            if (standbyAnchor != null)
            {
                standbyAnchor.localPosition = standbyBaseLocalPosition;
                standbyAnchor.localRotation = standbyBaseLocalRotation;
                standbyAnchor.localScale = standbyBaseLocalScale;
            }

            if (previewCamera != null && previewCamera.orthographic)
            {
                previewCamera.orthographicSize = cameraBaseOrthographicSize;
            }
        }

        private void ClearRuntimeLayers()
        {
            for (int i = 0; i < spineLayerRuntimes.Count; i++)
            {
                DestroyRuntimeObject(spineLayerRuntimes[i].GameObject);
            }

            for (int i = 0; i < particleLayerRuntimes.Count; i++)
            {
                DestroyRuntimeObject(particleLayerRuntimes[i].GameObject);
            }

            spineLayerRuntimes.Clear();
            particleLayerRuntimes.Clear();
            ActiveSpineLayerCount = 0;
            ActiveParticleLayerCount = 0;
        }

        private HideFlags RuntimeHideFlags()
        {
            if (Application.isPlaying)
            {
                return showRuntimeObjectsInHierarchy ? HideFlags.None : HideFlags.HideInHierarchy;
            }

            return (showRuntimeObjectsInHierarchy ? HideFlags.None : HideFlags.HideInHierarchy) |
                   HideFlags.DontSaveInEditor |
                   HideFlags.DontSaveInBuild;
        }

        private static void DestroyRuntimeObject(GameObject value)
        {
            if (value == null)
            {
                return;
            }

            value.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }

        private static Vector2 Sample(IReadOnlyList<CznVector2Key> keys, float time, Vector2 fallback)
        {
            if (keys == null || keys.Count == 0)
            {
                return fallback;
            }

            if (time <= keys[0].time)
            {
                return keys[0].value;
            }

            for (int i = 0; i < keys.Count - 1; i++)
            {
                CznVector2Key current = keys[i];
                CznVector2Key next = keys[i + 1];
                if (time > next.time)
                {
                    continue;
                }

                if (current.stepped || next.time <= current.time)
                {
                    return current.value;
                }

                float t = Mathf.InverseLerp(current.time, next.time, time);
                return Vector2.LerpUnclamped(current.value, next.value, t);
            }

            return keys[keys.Count - 1].value;
        }

        private static float Sample(IReadOnlyList<CznFloatKey> keys, float time, float fallback)
        {
            if (keys == null || keys.Count == 0)
            {
                return fallback;
            }

            if (time <= keys[0].time)
            {
                return keys[0].value;
            }

            for (int i = 0; i < keys.Count - 1; i++)
            {
                CznFloatKey current = keys[i];
                CznFloatKey next = keys[i + 1];
                if (time > next.time)
                {
                    continue;
                }

                if (current.stepped || next.time <= current.time)
                {
                    return current.value;
                }

                float t = Mathf.InverseLerp(current.time, next.time, time);
                return Mathf.LerpUnclamped(current.value, next.value, t);
            }

            return keys[keys.Count - 1].value;
        }

        private sealed class SpineLayerRuntime
        {
            private TrackEntry track;
            private bool active;

            public SpineLayerRuntime(CznSpineLayerCue cue, SkeletonAnimation skeleton)
            {
                Cue = cue;
                Skeleton = skeleton;
            }

            public CznSpineLayerCue Cue { get; }
            public SkeletonAnimation Skeleton { get; }
            public GameObject GameObject => Skeleton != null ? Skeleton.gameObject : null;

            public void SetActive(bool value)
            {
                if (active == value || GameObject == null)
                {
                    return;
                }

                if (!value)
                {
                    ResetForReplay();
                    return;
                }

                active = true;
                GameObject.SetActive(true);
            }

            public void ResetForReplay()
            {
                track = null;
                active = false;

                if (Skeleton != null)
                {
                    Skeleton.Initialize(false);
                    Skeleton.ClearState();
                }

                if (GameObject != null && GameObject.activeSelf)
                {
                    GameObject.SetActive(false);
                }
            }

            public void Evaluate(float localTime)
            {
                if (Skeleton == null || Skeleton.AnimationState == null)
                {
                    return;
                }

                if (track == null)
                {
                    track = Skeleton.AnimationState.SetAnimation(0, Cue.animationName, Cue.loop);
                }

                if (track != null)
                {
                    track.TrackTime = localTime;
                    track.AnimationLast = localTime;
                }

                if (Skeleton.Skeleton != null)
                {
                    Skeleton.Skeleton.A = Mathf.Clamp01(Cue.alpha);
                }

                Skeleton.Update(0f);
            }
        }

        private sealed class ParticleLayerRuntime
        {
            private bool active;

            public ParticleLayerRuntime(CznParticleLayerCue cue, ParticleSystem particleSystem)
            {
                Cue = cue;
                ParticleSystem = particleSystem;
            }

            public CznParticleLayerCue Cue { get; }
            public ParticleSystem ParticleSystem { get; }
            public GameObject GameObject => ParticleSystem != null ? ParticleSystem.gameObject : null;

            public void SetActive(bool value)
            {
                if (active == value || GameObject == null)
                {
                    return;
                }

                if (!value)
                {
                    ResetForReplay();
                    return;
                }

                active = true;
                GameObject.SetActive(true);
            }

            public void ResetForReplay()
            {
                active = false;

                if (ParticleSystem != null)
                {
                    ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                if (GameObject != null && GameObject.activeSelf)
                {
                    GameObject.SetActive(false);
                }
            }

            public void Evaluate(float localTime)
            {
                if (ParticleSystem != null)
                {
                    ParticleSystem.Simulate(Mathf.Max(0f, localTime), true, true, true);
                }
            }
        }
    }
}
