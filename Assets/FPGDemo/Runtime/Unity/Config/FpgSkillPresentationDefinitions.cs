using System;
using System.Collections.Generic;
using FPG.Demo.Core;
using FPG.Demo.Skills;
using UnityEngine;
using UnityEngine.Serialization;

namespace FPG.Demo.Unity
{
    public enum FpgVfxPresentationAnchor
    {
        OwnerRoot = 0,
        OwnerSocket = 1
    }

    public enum FpgAudioPresentationSpace
    {
        TwoDimensional = 0,
        WorldPositioned = 1
    }

    public enum FpgAudioPresentationAnchor
    {
        OwnerRoot = 0,
        OwnerSocket = 1
    }

    public enum FpgAudioPresentationPlaybackMode
    {
        OneShot = 0,
        HeldLoop = 1
    }

    [Serializable]
    public sealed class FpgVfxPresentationDefinition
    {
        [SerializeField]
        private GameObject prefab;

        [SerializeField, Min(0.01f)]
        private float durationSeconds = 1f;

        [SerializeField]
        private Vector3 scale = Vector3.one;

        [SerializeField]
        private Vector3 rotationOffsetEuler;

        public GameObject Prefab => prefab;
        public float DurationSeconds => durationSeconds;
        public Vector3 Scale => scale;
        public Vector3 RotationOffsetEuler => rotationOffsetEuler;

        internal bool TryValidate(out string error)
        {
            if (prefab == null
                || !FpgPresentationAuthoringHash.IsFinitePositive(
                    durationSeconds)
                || !FpgPresentationAuthoringHash.IsFinitePositive(scale.x)
                || !FpgPresentationAuthoringHash.IsFinitePositive(scale.y)
                || !FpgPresentationAuthoringHash.IsFinitePositive(scale.z)
                || !FpgPresentationAuthoringHash.IsFinite(
                    rotationOffsetEuler))
            {
                error =
                    "VFX presentation requires a prefab, positive duration, positive scale and finite rotation offset.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal ulong ComputeContentHash()
        {
            ulong hash = FpgPresentationAuthoringHash.Begin(1UL);
            hash = FpgPresentationAuthoringHash.AppendObject(hash, prefab);
            hash = FpgPresentationAuthoringHash.AppendFloat(
                hash,
                durationSeconds);
            hash = FpgPresentationAuthoringHash.AppendVector(hash, scale);
            return FpgPresentationAuthoringHash.AppendVector(
                hash,
                rotationOffsetEuler);
        }
    }

    [Serializable]
    public sealed class FpgAudioPresentationDefinition
    {
        [SerializeField]
        private AudioClip clip;

        [SerializeField]
        private AudioClip[] variations = Array.Empty<AudioClip>();

        [SerializeField, Range(0f, 1f)]
        private float volume = 1f;

        [SerializeField]
        private FpgAudioPresentationSpace space =
            FpgAudioPresentationSpace.TwoDimensional;

        [SerializeField]
        private FpgAudioPresentationAnchor anchor =
            FpgAudioPresentationAnchor.OwnerRoot;

        [SerializeField]
        private FpgAudioPresentationPlaybackMode playbackMode =
            FpgAudioPresentationPlaybackMode.OneShot;

        [SerializeField]
        private string socketId = string.Empty;

        [SerializeField, Min(0.01f)]
        private float minDistance = 1f;

        [SerializeField, Min(0.01f)]
        private float maxDistance = 20f;

        public AudioClip Clip => clip;
        public int ClipCount => (clip == null ? 0 : 1)
            + (variations == null ? 0 : variations.Length);
        public float Volume => volume;
        public FpgAudioPresentationSpace Space => space;
        public FpgAudioPresentationAnchor Anchor => anchor;
        public FpgAudioPresentationPlaybackMode PlaybackMode => playbackMode;
        public string OwnerSocketId => socketId;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;

        public AudioClip GetClip(int index)
        {
            int primaryCount = clip == null ? 0 : 1;
            if (index < 0 || index >= ClipCount)
            {
                return null;
            }

            return index < primaryCount
                ? clip
                : variations[index - primaryCount];
        }

        internal bool TryValidate(out string error)
        {
            if (ClipCount <= 0
                || HasInvalidOrDuplicateVariations()
                || !FpgPresentationAuthoringHash.IsFinite(volume)
                || volume < 0f
                || volume > 1f
                || !Enum.IsDefined(typeof(FpgAudioPresentationSpace), space)
                || !Enum.IsDefined(typeof(FpgAudioPresentationAnchor), anchor)
                || !Enum.IsDefined(
                    typeof(FpgAudioPresentationPlaybackMode),
                    playbackMode)
                || !FpgPresentationAuthoringHash.IsFinitePositive(minDistance)
                || !FpgPresentationAuthoringHash.IsFinitePositive(maxDistance)
                || maxDistance < minDistance
                || (space == FpgAudioPresentationSpace.TwoDimensional
                    && (anchor != FpgAudioPresentationAnchor.OwnerRoot
                        || !string.IsNullOrEmpty(socketId)))
                || (space == FpgAudioPresentationSpace.WorldPositioned
                    && (anchor == FpgAudioPresentationAnchor.OwnerSocket
                        ? !FpgSkillStableId.IsValid(socketId)
                        : !string.IsNullOrEmpty(socketId))))
            {
                error =
                    "Audio presentation requires a clip, finite volume, valid space/anchor and positive distance parameters.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal ulong ComputeContentHash()
        {
            ulong hash = FpgPresentationAuthoringHash.Begin(2UL);
            hash = StableHash.Append(hash, unchecked((ulong)ClipCount));
            for (int index = 0; index < ClipCount; index++)
            {
                hash = FpgPresentationAuthoringHash.AppendObject(
                    hash,
                    GetClip(index));
            }

            hash = FpgPresentationAuthoringHash.AppendFloat(hash, volume);
            hash = StableHash.Append(hash, unchecked((ulong)(int)space));
            hash = StableHash.Append(hash, unchecked((ulong)(int)anchor));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)(int)playbackMode));
            hash = FpgPresentationAuthoringHash.AppendString(hash, socketId);
            hash = FpgPresentationAuthoringHash.AppendFloat(hash, minDistance);
            return FpgPresentationAuthoringHash.AppendFloat(hash, maxDistance);
        }

        private bool HasInvalidOrDuplicateVariations()
        {
            for (int index = 0; index < ClipCount; index++)
            {
                AudioClip candidate = GetClip(index);
                if (candidate == null)
                {
                    return true;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (GetClip(previous) == candidate)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    [Serializable]
    public sealed class FpgCameraShakePresentationDefinition
    {
        [SerializeField, Min(0f)]
        private float strength;

        [SerializeField, Min(0.01f)]
        private float durationSeconds = 0.1f;

        public float Strength => strength;
        public float DurationSeconds => durationSeconds;

        internal bool TryValidate(out string error)
        {
            if (!FpgPresentationAuthoringHash.IsFinite(strength)
                || strength < 0f
                || !FpgPresentationAuthoringHash.IsFinitePositive(
                    durationSeconds))
            {
                error =
                    "Camera shake presentation requires non-negative strength and positive duration.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal ulong ComputeContentHash()
        {
            ulong hash = FpgPresentationAuthoringHash.Begin(3UL);
            hash = FpgPresentationAuthoringHash.AppendFloat(hash, strength);
            return FpgPresentationAuthoringHash.AppendFloat(
                hash,
                durationSeconds);
        }
    }

    [Serializable]
    public sealed class FpgImpactPresentationBundleDefinition
    {
        [SerializeReference]
        private FpgVfxPresentationDefinition baseVfx;

        [SerializeReference]
        private FpgAudioPresentationDefinition baseAudio;

        [SerializeReference]
        private FpgAudioPresentationDefinition environmentAudioOverride;

        [SerializeReference]
        private FpgAudioPresentationDefinition interceptionAudioOverride;

        [SerializeReference]
        private FpgCameraShakePresentationDefinition baseCameraShake;

        [SerializeReference]
        private FpgVfxPresentationDefinition weakpointVfxOverride;

        [SerializeReference]
        private FpgAudioPresentationDefinition weakpointAudioOverride;

        [SerializeReference]
        private FpgCameraShakePresentationDefinition
            weakpointCameraShakeOverride;

        public FpgVfxPresentationDefinition BaseVfx => baseVfx;
        public FpgAudioPresentationDefinition BaseAudio => baseAudio;
        public FpgAudioPresentationDefinition EnvironmentAudioOverride =>
            environmentAudioOverride;
        public FpgAudioPresentationDefinition InterceptionAudioOverride =>
            interceptionAudioOverride;
        public FpgCameraShakePresentationDefinition BaseCameraShake =>
            baseCameraShake;
        public FpgVfxPresentationDefinition WeakpointVfxOverride =>
            weakpointVfxOverride;
        public FpgAudioPresentationDefinition WeakpointAudioOverride =>
            weakpointAudioOverride;
        public FpgCameraShakePresentationDefinition
            WeakpointCameraShakeOverride => weakpointCameraShakeOverride;

        internal bool HasAny => baseVfx != null
            || baseAudio != null
            || environmentAudioOverride != null
            || interceptionAudioOverride != null
            || baseCameraShake != null
            || weakpointVfxOverride != null
            || weakpointAudioOverride != null
            || weakpointCameraShakeOverride != null;

        internal bool TryValidate(out string error)
        {
            if (!TryValidateOptional(baseVfx, out error)
                || !TryValidateOptional(baseAudio, out error)
                || !TryValidateOptional(environmentAudioOverride, out error)
                || !TryValidateOptional(interceptionAudioOverride, out error)
                || !TryValidateOptional(baseCameraShake, out error)
                || !TryValidateOptional(weakpointVfxOverride, out error)
                || !TryValidateOptional(weakpointAudioOverride, out error)
                || !TryValidateOptional(
                    weakpointCameraShakeOverride,
                    out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal FpgCompiledImpactPresentation Compile(string keyPrefix)
        {
            return new FpgCompiledImpactPresentation(
                CompileOptional(baseVfx, keyPrefix + ":base.vfx"),
                CompileOptional(baseAudio, keyPrefix + ":base.audio"),
                CompileOptional(
                    environmentAudioOverride,
                    keyPrefix + ":environment.audio"),
                CompileOptional(
                    interceptionAudioOverride,
                    keyPrefix + ":interception.audio"),
                CompileOptional(
                    baseCameraShake,
                    keyPrefix + ":base.camera-shake"),
                CompileOptional(
                    weakpointVfxOverride,
                    keyPrefix + ":weakpoint.vfx"),
                CompileOptional(
                    weakpointAudioOverride,
                    keyPrefix + ":weakpoint.audio"),
                CompileOptional(
                    weakpointCameraShakeOverride,
                    keyPrefix + ":weakpoint.camera-shake"),
                ComputeContentHash());
        }

        internal ulong ComputeContentHash()
        {
            ulong hash = FpgPresentationAuthoringHash.Begin(5UL);
            hash = AppendOptional(hash, baseVfx);
            hash = AppendOptional(hash, baseAudio);
            hash = AppendOptional(hash, environmentAudioOverride);
            hash = AppendOptional(hash, interceptionAudioOverride);
            hash = AppendOptional(hash, baseCameraShake);
            hash = AppendOptional(hash, weakpointVfxOverride);
            hash = AppendOptional(hash, weakpointAudioOverride);
            return AppendOptional(hash, weakpointCameraShakeOverride);
        }

        private static bool TryValidateOptional(
            FpgVfxPresentationDefinition value,
            out string error)
        {
            if (value == null)
            {
                error = string.Empty;
                return true;
            }

            return value.TryValidate(out error);
        }

        private static bool TryValidateOptional(
            FpgAudioPresentationDefinition value,
            out string error)
        {
            if (value == null)
            {
                error = string.Empty;
                return true;
            }

            return value.TryValidate(out error);
        }

        private static bool TryValidateOptional(
            FpgCameraShakePresentationDefinition value,
            out string error)
        {
            if (value == null)
            {
                error = string.Empty;
                return true;
            }

            return value.TryValidate(out error);
        }

        private static FpgPresentationHandle CompileOptional(
            FpgVfxPresentationDefinition value,
            string key)
        {
            return value == null
                ? default(FpgPresentationHandle)
                : FpgSkillStableId.CompilePresentationHandle(key);
        }

        private static FpgPresentationHandle CompileOptional(
            FpgAudioPresentationDefinition value,
            string key)
        {
            return value == null
                ? default(FpgPresentationHandle)
                : FpgSkillStableId.CompilePresentationHandle(key);
        }

        private static FpgPresentationHandle CompileOptional(
            FpgCameraShakePresentationDefinition value,
            string key)
        {
            return value == null
                ? default(FpgPresentationHandle)
                : FpgSkillStableId.CompilePresentationHandle(key);
        }

        private static ulong AppendOptional(
            ulong hash,
            FpgVfxPresentationDefinition value)
        {
            return StableHash.Append(
                hash,
                value == null ? 0UL : value.ComputeContentHash());
        }

        private static ulong AppendOptional(
            ulong hash,
            FpgAudioPresentationDefinition value)
        {
            return StableHash.Append(
                hash,
                value == null ? 0UL : value.ComputeContentHash());
        }

        private static ulong AppendOptional(
            ulong hash,
            FpgCameraShakePresentationDefinition value)
        {
            return StableHash.Append(
                hash,
                value == null ? 0UL : value.ComputeContentHash());
        }
    }

    [Serializable]
    public abstract class FpgActivePresentationEventDefinition
    {
        [SerializeField]
        private string eventId = "presentation";

        [SerializeField, Min(0)]
        private int tick;

        [FormerlySerializedAs("sortOrder")]
        [SerializeField, Min(0)]
        private int authoredOrdinal;

        [SerializeField]
        private string boundGameplayEventId = string.Empty;

        public string EventId => eventId;
        public int Tick => tick;
        public int AuthoredOrdinal => authoredOrdinal;
        public string BoundGameplayEventId => boundGameplayEventId;

        internal abstract FpgActivePresentationKind PresentationKind { get; }
        internal virtual string SocketId => string.Empty;
        internal abstract ulong PresentationContentHash { get; }

        internal bool TryValidate(
            int durationTicks,
            Func<string, int> resolveGameplayEventTick,
            out string error)
        {
            if (!FpgSkillStableId.IsValid(eventId)
                || tick < 0
                || tick > durationTicks
                || authoredOrdinal < 0)
            {
                error =
                    "Active presentation requires a stable event ID, valid tick and authored ordinal.";
                return false;
            }

            if (!string.IsNullOrEmpty(boundGameplayEventId))
            {
                if (!FpgSkillStableId.IsValid(boundGameplayEventId)
                    || resolveGameplayEventTick == null)
                {
                    error =
                        $"Active presentation '{eventId}' references missing gameplay event '{boundGameplayEventId}'.";
                    return false;
                }

                int gameplayTick =
                    resolveGameplayEventTick(boundGameplayEventId);
                if (gameplayTick < 0)
                {
                    error =
                        $"Active presentation '{eventId}' references missing gameplay event '{boundGameplayEventId}'.";
                    return false;
                }

                if (tick < gameplayTick)
                {
                    error =
                        $"Active presentation '{eventId}' cannot run before bound gameplay event '{boundGameplayEventId}' (presentation Tick {tick}, gameplay Tick {gameplayTick}).";
                    return false;
                }
            }

            return TryValidatePresentation(out error);
        }

        internal FpgCompiledSkillEvent Compile(
            int presentationTrackId,
            string scopePrefix)
        {
            return new FpgCompiledSkillEvent(
                FpgSkillStableId.CompileEvent(eventId),
                tick,
                PresentationKind,
                FpgSkillStableId.CompilePresentationHandle(
                    scopePrefix + ":" + eventId),
                presentationTrackId,
                PresentationContentHash,
                authoredOrdinal,
                FpgSkillStableId.CompileOptionalSocket(SocketId),
                FpgSkillStableId.CompileOptionalEvent(
                    boundGameplayEventId));
        }

        internal abstract bool TryValidatePresentation(out string error);
    }

    [Serializable]
    public sealed class FpgVfxPresentationEventDefinition :
        FpgActivePresentationEventDefinition
    {
        [SerializeField]
        private FpgVfxPresentationDefinition presentation =
            new FpgVfxPresentationDefinition();

        [SerializeField]
        private FpgVfxPresentationAnchor anchor =
            FpgVfxPresentationAnchor.OwnerRoot;

        [SerializeField]
        private string socketId = string.Empty;

        public FpgVfxPresentationDefinition Presentation => presentation;
        public FpgVfxPresentationAnchor Anchor => anchor;
        public string OwnerSocketId => socketId;

        internal override FpgActivePresentationKind PresentationKind =>
            FpgActivePresentationKind.Vfx;
        internal override string SocketId => socketId;
        internal override ulong PresentationContentHash
        {
            get
            {
                ulong hash = presentation == null
                    ? FpgPresentationAuthoringHash.Begin(11UL)
                    : presentation.ComputeContentHash();
                return StableHash.Append(
                    hash,
                    unchecked((ulong)(int)anchor));
            }
        }

        internal override bool TryValidatePresentation(out string error)
        {
            error = string.Empty;
            if (presentation == null
                || !presentation.TryValidate(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "VFX event requires a presentation definition.";
                }

                return false;
            }

            if (!Enum.IsDefined(typeof(FpgVfxPresentationAnchor), anchor)
                || (anchor == FpgVfxPresentationAnchor.OwnerRoot
                    && !string.IsNullOrEmpty(socketId))
                || (anchor == FpgVfxPresentationAnchor.OwnerSocket
                    && !FpgSkillStableId.IsValid(socketId)))
            {
                error =
                    "VFX event requires OwnerRoot with no socket or OwnerSocket with a stable socket ID.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class FpgAudioPresentationEventDefinition :
        FpgActivePresentationEventDefinition
    {
        [SerializeField]
        private FpgAudioPresentationDefinition presentation =
            new FpgAudioPresentationDefinition();

        public FpgAudioPresentationDefinition Presentation => presentation;

        internal override FpgActivePresentationKind PresentationKind =>
            FpgActivePresentationKind.Audio;
        internal override ulong PresentationContentHash =>
            presentation == null
                ? FpgPresentationAuthoringHash.Begin(12UL)
                : presentation.ComputeContentHash();

        internal override bool TryValidatePresentation(out string error)
        {
            error = string.Empty;
            if (presentation != null
                && presentation.TryValidate(out error))
            {
                return true;
            }

            if (string.IsNullOrEmpty(error))
            {
                error = "Audio event requires a presentation definition.";
            }

            return false;
        }
    }

    [Serializable]
    public sealed class FpgCameraShakePresentationEventDefinition :
        FpgActivePresentationEventDefinition
    {
        [SerializeField]
        private FpgCameraShakePresentationDefinition presentation =
            new FpgCameraShakePresentationDefinition();

        public FpgCameraShakePresentationDefinition Presentation =>
            presentation;

        internal override FpgActivePresentationKind PresentationKind =>
            FpgActivePresentationKind.CameraShake;
        internal override ulong PresentationContentHash =>
            presentation == null
                ? FpgPresentationAuthoringHash.Begin(13UL)
                : presentation.ComputeContentHash();

        internal override bool TryValidatePresentation(out string error)
        {
            error = string.Empty;
            if (presentation != null
                && presentation.TryValidate(out error))
            {
                return true;
            }

            if (string.IsNullOrEmpty(error))
            {
                error =
                    "Camera shake event requires a presentation definition.";
            }

            return false;
        }
    }

    [Serializable]
    public sealed class FpgSkillActivePresentationTrackDefinition
    {
        [SerializeField]
        private string trackId = "presentation.track";

        [SerializeField]
        private string displayName = "Presentation";

        [SerializeField]
        private FpgVfxPresentationEventDefinition[] vfxEvents =
            Array.Empty<FpgVfxPresentationEventDefinition>();

        [SerializeField]
        private FpgAudioPresentationEventDefinition[] audioEvents =
            Array.Empty<FpgAudioPresentationEventDefinition>();

        [SerializeField]
        private FpgCameraShakePresentationEventDefinition[] cameraShakeEvents =
            Array.Empty<FpgCameraShakePresentationEventDefinition>();

        public string TrackId => trackId;
        public string DisplayName => displayName;
        public IReadOnlyList<FpgVfxPresentationEventDefinition> VfxEvents =>
            vfxEvents ?? Array.Empty<FpgVfxPresentationEventDefinition>();
        public IReadOnlyList<FpgAudioPresentationEventDefinition> AudioEvents =>
            audioEvents ?? Array.Empty<FpgAudioPresentationEventDefinition>();
        public IReadOnlyList<FpgCameraShakePresentationEventDefinition>
            CameraShakeEvents =>
                cameraShakeEvents
                ?? Array.Empty<FpgCameraShakePresentationEventDefinition>();

        internal bool TryValidateHeader(out string error)
        {
            if (!FpgSkillStableId.IsValid(trackId)
                || string.IsNullOrWhiteSpace(displayName))
            {
                error =
                    "Active presentation track requires a stable ID and display name.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    internal static class FpgPresentationAuthoringHash
    {
        private const ulong Seed = 0x4650475F50524531UL;

        public static ulong Begin(ulong discriminator)
        {
            return StableHash.Append(StableHash.Mix(Seed), discriminator);
        }

        public static ulong AppendObject(
            ulong hash,
            UnityEngine.Object value)
        {
            if (value == null)
            {
                return StableHash.Append(hash, 0UL);
            }

            hash = StableHash.Append(hash, 1UL);
            hash = AppendString(hash, value.GetType().FullName);
            return AppendString(hash, value.name);
        }

        public static ulong AppendFloat(ulong hash, float value)
        {
            return StableHash.Append(
                hash,
                unchecked((ulong)(uint)value.GetHashCode()));
        }

        public static ulong AppendVector(ulong hash, Vector3 value)
        {
            hash = AppendFloat(hash, value.x);
            hash = AppendFloat(hash, value.y);
            return AppendFloat(hash, value.z);
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        public static ulong AppendString(ulong hash, string value)
        {
            string text = value ?? string.Empty;
            hash = StableHash.Append(
                hash,
                unchecked((ulong)text.Length));
            for (int index = 0; index < text.Length; index++)
            {
                hash = StableHash.Append(hash, text[index]);
            }

            return hash;
        }
    }
}
