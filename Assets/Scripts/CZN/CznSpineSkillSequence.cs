using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace NewFPG.CZN
{
    public enum CznSkillAnchor
    {
        Self,
        Target,
        Center,
        Screen,
    }

    public enum CznTransformTarget
    {
        Camera,
        Target,
        Self,
        Standby,
    }

    [Serializable]
    public sealed class CznActorAnimationCue
    {
        public string phaseName;
        public string animationName;
        public float startTime;
        public float duration;
        public bool loop;
    }

    [Serializable]
    public sealed class CznSpineLayerCue
    {
        public string compositeName;
        public string sourceName;
        public SkeletonDataAsset skeletonDataAsset;
        public string animationName = "animation";
        public CznSkillAnchor anchor;
        public Vector2 offset;
        public float startTime;
        public float duration;
        public float scale = 1f;
        [Range(0f, 1f)] public float alpha = 1f;
        public float rotation;
        public int sortingOrder;
        public bool loop;
        public string attachmentBone;
    }

    [Serializable]
    public sealed class CznParticleLayerCue
    {
        public string compositeName;
        public string sourceName;
        public string emitterName;
        public string originalTexturePath;
        public Texture2D texture;
        public CznSkillAnchor anchor;
        public Vector2 offset;
        public Vector2 sourceVariance;
        public Vector2 force;
        public float startTime;
        public float duration;
        public float scale = 1f;
        public float rotation;
        public int sortingOrder;
        public int maxParticles = 100;
        public float emissionRate = 10f;
        public float lifetimeMin = 0.25f;
        public float lifetimeMax = 0.5f;
        public float speedMin;
        public float speedMax;
        public float sizeMin = 0.05f;
        public float sizeMax = 0.1f;
        public float angle;
        public float angleVariance;
        public float rotationVariance;
        public Color startColor = Color.white;
        public Color endColor = new Color(1f, 1f, 1f, 0f);
        public bool additive;
    }

    [Serializable]
    public struct CznVector2Key
    {
        public float time;
        public Vector2 value;
        public bool stepped;
    }

    [Serializable]
    public struct CznFloatKey
    {
        public float time;
        public float value;
        public bool stepped;
    }

    [Serializable]
    public sealed class CznTransformCue
    {
        public string sourceName;
        public CznTransformTarget target;
        public float startTime;
        public float duration;
        public float positionScale = 0.01f;
        public List<CznVector2Key> translateKeys = new List<CznVector2Key>();
        public List<CznFloatKey> rotateKeys = new List<CznFloatKey>();
        public List<CznVector2Key> scaleKeys = new List<CznVector2Key>();
    }

    [Serializable]
    public sealed class CznCameraZoomCue
    {
        public float startTime;
        public float duration;
        public float zoom = 1f;
    }

    [Serializable]
    public sealed class CznSkillMarkerCue
    {
        public string kind;
        public string label;
        public float startTime;
        public float duration;
        public float value;
    }

    [CreateAssetMenu(
        fileName = "CznSpineSkillSequence",
        menuName = "NewFPG/CZN/Spine Skill Sequence")]
    public sealed class CznSpineSkillSequence : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string skillId;
        [SerializeField] private string displayName;
        [SerializeField, Min(0.01f)] private float duration = 1f;

        [Header("Generated cues")]
        [SerializeField] private List<CznActorAnimationCue> actorAnimations = new List<CznActorAnimationCue>();
        [SerializeField] private List<CznSpineLayerCue> spineLayers = new List<CznSpineLayerCue>();
        [SerializeField] private List<CznParticleLayerCue> particleLayers = new List<CznParticleLayerCue>();
        [SerializeField] private List<CznTransformCue> transformCues = new List<CznTransformCue>();
        [SerializeField] private List<CznCameraZoomCue> cameraZoomCues = new List<CznCameraZoomCue>();
        [SerializeField] private List<CznSkillMarkerCue> markers = new List<CznSkillMarkerCue>();

        [Header("Recovery notes")]
        [SerializeField, TextArea(2, 8)] private string recoveryNotes;
        [SerializeField] private List<string> unresolvedResources = new List<string>();

        public string SkillId => skillId;
        public string DisplayName => displayName;
        public float Duration => duration;
        public IReadOnlyList<CznActorAnimationCue> ActorAnimations => actorAnimations;
        public IReadOnlyList<CznSpineLayerCue> SpineLayers => spineLayers;
        public IReadOnlyList<CznParticleLayerCue> ParticleLayers => particleLayers;
        public IReadOnlyList<CznTransformCue> TransformCues => transformCues;
        public IReadOnlyList<CznCameraZoomCue> CameraZoomCues => cameraZoomCues;
        public IReadOnlyList<CznSkillMarkerCue> Markers => markers;
        public string RecoveryNotes => recoveryNotes;
        public IReadOnlyList<string> UnresolvedResources => unresolvedResources;

        public void SetGeneratedData(
            string id,
            string label,
            float generatedDuration,
            List<CznActorAnimationCue> generatedActorAnimations,
            List<CznSpineLayerCue> generatedSpineLayers,
            List<CznParticleLayerCue> generatedParticleLayers,
            List<CznTransformCue> generatedTransformCues,
            List<CznCameraZoomCue> generatedCameraZoomCues,
            List<CznSkillMarkerCue> generatedMarkers,
            string notes,
            List<string> unresolved)
        {
            skillId = id ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(label) ? skillId : label;
            duration = Mathf.Max(0.01f, generatedDuration);
            actorAnimations = generatedActorAnimations ?? new List<CznActorAnimationCue>();
            spineLayers = generatedSpineLayers ?? new List<CznSpineLayerCue>();
            particleLayers = generatedParticleLayers ?? new List<CznParticleLayerCue>();
            transformCues = generatedTransformCues ?? new List<CznTransformCue>();
            cameraZoomCues = generatedCameraZoomCues ?? new List<CznCameraZoomCue>();
            markers = generatedMarkers ?? new List<CznSkillMarkerCue>();
            recoveryNotes = notes ?? string.Empty;
            unresolvedResources = unresolved ?? new List<string>();
        }
    }
}
