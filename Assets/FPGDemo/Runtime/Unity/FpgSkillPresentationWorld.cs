using System;
using System.Collections.Generic;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Shared player/enemy presenter for all skill-owned presentation handles.
    /// Every failure is presentation-only and never propagates into gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgSkillPresentationWorld : MonoBehaviour
    {
        [SerializeField]
        private D0CombatVfxWorld vfxWorld;

        [SerializeField]
        private FpgFormalPlayerCameraFeedback cameraFeedback;

        private readonly FpgSkillPresentationRegistry registry =
            new FpgSkillPresentationRegistry();
        private readonly FpgSkillAudioSourcePool audioPool =
            new FpgSkillAudioSourcePool();
        private bool prepared;

        public D0CombatVfxWorld VfxWorld => vfxWorld;
        public FpgSkillPresentationRegistry Registry => registry;
        public bool IsPrepared => prepared;
        public int PresentationFaultCount { get; private set; }

        public bool TryConfigure(
            D0CombatVfxWorld nextVfxWorld,
            FpgFormalPlayerCameraFeedback nextCameraFeedback,
            out string error)
        {
            if (prepared)
            {
                error = "Skill presentation world is already prepared.";
                return false;
            }

            if (nextVfxWorld == null)
            {
                error = "Skill presentation world requires a VFX world.";
                return false;
            }

            vfxWorld = nextVfxWorld;
            cameraFeedback = nextCameraFeedback;
            error = string.Empty;
            return true;
        }

        public bool TryPrepare(
            IEnumerable<FpgSkillTimelineDefinition> skillDefinitions,
            CombatPresentationProfile profile,
            out string error)
        {
            error = string.Empty;
            if (prepared)
            {
                error = string.Empty;
                return true;
            }

            if (vfxWorld == null || profile == null
                || !profile.TryValidateStatic(out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error =
                        "Skill presentation world requires VFX world and profile.";
                }

                return false;
            }

            registry.Clear();
            if (skillDefinitions != null)
            {
                foreach (FpgSkillTimelineDefinition skill in skillDefinitions)
                {
                    if (skill != null && !registry.TryRegister(skill, out error))
                    {
                        registry.Clear();
                        return false;
                    }
                }
            }

            List<D0CombatVfxAssetReference> references =
                new List<D0CombatVfxAssetReference>();
            if (!registry.TryCollectVfxReferences(
                references,
                profile.PoolCapacities.WorldEffectCapacity,
                out error)
                || !vfxWorld.TrySetGlobalActiveCapacity(
                    profile.PoolCapacities.WorldEffectCapacity,
                    out error)
                || !vfxWorld.TryPrepareForScenario(references, out error)
                || !audioPool.TryPrepare(
                    transform,
                    profile.PoolCapacities.AudioSourceCapacity,
                    out error))
            {
                registry.Clear();
                audioPool.Dispose();
                return false;
            }

            PresentationFaultCount = 0;
            prepared = true;
            error = string.Empty;
            return true;
        }

        public bool TryPresent(
            FpgPresentationHandle handle,
            Transform source)
        {
            if (!prepared || source == null
                || !registry.TryResolve(handle, out FpgRegisteredPresentation entry))
            {
                return Reject();
            }

            switch (entry.Kind)
            {
                case FpgRegisteredPresentationKind.Vfx:
                    Quaternion rotation = source.rotation
                        * Quaternion.Euler(entry.Vfx.RotationOffsetEuler);
                    Vector3 scale = Vector3.Scale(
                        source.lossyScale,
                        entry.Vfx.Scale);
                    return AcceptOrReject(vfxWorld.TryAcquire(
                        FpgSkillPresentationRegistry.GetPoolKey(handle),
                        source.position,
                        rotation,
                        scale,
                        out _));

                case FpgRegisteredPresentationKind.Audio:
                    return AcceptOrReject(audioPool.TryPlay(
                        entry.Audio.Clip,
                        entry.Audio.Volume));

                case FpgRegisteredPresentationKind.CameraShake:
                    return entry.CameraShake.Strength <= 0f
                        || AcceptOrReject(cameraFeedback != null
                            && cameraFeedback.TryAddShake(
                                entry.CameraShake.Strength,
                                entry.CameraShake.DurationSeconds));

                default:
                    return Reject();
            }
        }

        public bool TryPresentVfxAt(
            FpgPresentationHandle handle,
            Vector3 position,
            Quaternion rotation)
        {
            if (!TryResolveVfx(handle, out FpgRegisteredPresentation entry))
            {
                return Reject();
            }

            return AcceptOrReject(vfxWorld.TryAcquire(
                FpgSkillPresentationRegistry.GetPoolKey(handle),
                position,
                rotation * Quaternion.Euler(entry.Vfx.RotationOffsetEuler),
                entry.Vfx.Scale,
                out _));
        }

        public bool TryPresentTrajectory(
            FpgPresentationHandle handle,
            Vector3 start,
            Vector3 end)
        {
            if (!TryResolveVfx(handle, out FpgRegisteredPresentation entry)
                || !vfxWorld.TryAcquire(
                    FpgSkillPresentationRegistry.GetPoolKey(handle),
                    start,
                    Quaternion.identity,
                    Vector3.one,
                    out GameObject instance)
                || instance == null)
            {
                return Reject();
            }

            FpgTrajectoryVfxView view =
                instance.GetComponent<FpgTrajectoryVfxView>();
            if (view == null
                || !view.TryActivate(
                    start,
                    end,
                    entry.Vfx.DurationSeconds,
                    entry.Vfx.Scale,
                    entry.Vfx.RotationOffsetEuler,
                    out _))
            {
                vfxWorld.TryRelease(instance);
                return Reject();
            }

            return true;
        }

        public bool TryBorrowFlightVfx(
            FpgPresentationHandle handle,
            Vector3 position,
            Quaternion rotation,
            out GameObject instance)
        {
            instance = null;
            if (!TryResolveVfx(handle, out FpgRegisteredPresentation entry))
            {
                return Reject();
            }

            return AcceptOrReject(vfxWorld.TryAcquireHeld(
                FpgSkillPresentationRegistry.GetPoolKey(handle),
                position,
                rotation * Quaternion.Euler(entry.Vfx.RotationOffsetEuler),
                entry.Vfx.Scale,
                out instance));
        }

        public bool TryReleaseFlightVfx(GameObject instance)
        {
            return AcceptOrReject(vfxWorld != null
                && vfxWorld.TryRelease(instance));
        }

        public bool TryUpdateFlightVfx(
            FpgPresentationHandle handle,
            GameObject instance,
            Vector3 position,
            Quaternion rotation)
        {
            if (instance == null
                || !TryResolveVfx(
                    handle,
                    out FpgRegisteredPresentation entry))
            {
                return Reject();
            }

            Transform visual = instance.transform;
            visual.SetPositionAndRotation(
                position,
                rotation * Quaternion.Euler(
                    entry.Vfx.RotationOffsetEuler));
            visual.localScale = entry.Vfx.Scale;
            return true;
        }

        public bool TryPresentImpactVfx(
            in FpgCompiledImpactPresentation bundle,
            bool weakpoint,
            Vector3 point)
        {
            FpgPresentationHandle handle = weakpoint
                && bundle.WeakpointVfxOverride.IsValid
                    ? bundle.WeakpointVfxOverride
                    : bundle.BaseVfx;
            return !handle.IsValid
                || TryPresentVfxAt(handle, point, Quaternion.identity);
        }

        public bool TryPresentImpactGroup(
            in FpgCompiledImpactPresentation bundle,
            bool anyWeakpoint)
        {
            FpgPresentationHandle audio = anyWeakpoint
                && bundle.WeakpointAudioOverride.IsValid
                    ? bundle.WeakpointAudioOverride
                    : bundle.BaseAudio;
            FpgPresentationHandle shake = anyWeakpoint
                && bundle.WeakpointCameraShakeOverride.IsValid
                    ? bundle.WeakpointCameraShakeOverride
                    : bundle.BaseCameraShake;

            bool success = true;
            if (audio.IsValid)
            {
                success &= TryPlayAudio(audio);
            }

            if (shake.IsValid)
            {
                success &= TryPlayShake(shake);
            }

            return success;
        }

        public void ClearRuntimePresentation()
        {
            audioPool.Clear();
            vfxWorld?.ClearActive();
            cameraFeedback?.ClearPresentationShake();
        }

        private void OnDisable()
        {
            ClearRuntimePresentation();
        }

        private void OnDestroy()
        {
            audioPool.Dispose();
        }

        private bool TryPlayAudio(FpgPresentationHandle handle)
        {
            return registry.TryResolve(handle, out FpgRegisteredPresentation entry)
                && entry.Kind == FpgRegisteredPresentationKind.Audio
                && AcceptOrReject(audioPool.TryPlay(
                    entry.Audio.Clip,
                    entry.Audio.Volume));
        }

        private bool TryPlayShake(FpgPresentationHandle handle)
        {
            return registry.TryResolve(handle, out FpgRegisteredPresentation entry)
                && entry.Kind == FpgRegisteredPresentationKind.CameraShake
                && (entry.CameraShake.Strength <= 0f
                    || AcceptOrReject(cameraFeedback != null
                        && cameraFeedback.TryAddShake(
                            entry.CameraShake.Strength,
                            entry.CameraShake.DurationSeconds)));
        }

        private bool TryResolveVfx(
            FpgPresentationHandle handle,
            out FpgRegisteredPresentation entry)
        {
            entry = default(FpgRegisteredPresentation);
            return prepared
                && registry.TryResolve(handle, out entry)
                && entry.Kind == FpgRegisteredPresentationKind.Vfx
                && entry.Vfx != null;
        }

        private bool AcceptOrReject(bool success)
        {
            return success || Reject();
        }

        private bool Reject()
        {
            PresentationFaultCount++;
            return false;
        }
    }
}
