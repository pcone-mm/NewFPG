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
        private readonly List<PreparedSkillContract> preparedSkills =
            new List<PreparedSkillContract>();
        private CombatPresentationProfile preparedProfile;
        private int preparedWorldEffectCapacity;
        private int preparedAudioSourceCapacity;
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
            if (nextVfxWorld == null)
            {
                error = "Skill presentation world requires a VFX world.";
                return false;
            }

            if (prepared)
            {
                // FormalRoom retains this world across room compositions.
                // The prepared pools may be reused, but never rebound.
                if (vfxWorld == nextVfxWorld
                    && cameraFeedback == nextCameraFeedback)
                {
                    error = string.Empty;
                    return true;
                }

                error =
                    "Skill presentation world cannot change its VFX world or camera feedback after preparation.";
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
                return TryValidatePreparedContract(skillDefinitions, profile, out error);
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
            preparedSkills.Clear();
            if (skillDefinitions != null)
            {
                foreach (FpgSkillTimelineDefinition skill in skillDefinitions)
                {
                    if (skill == null)
                    {
                        continue;
                    }

                    if (!skill.TryCompile(
                            out FpgCompiledSkillDefinition compiled,
                            out error)
                        || !registry.TryRegister(skill, out error))
                    {
                        registry.Clear();
                        preparedSkills.Clear();
                        return false;
                    }

                    preparedSkills.Add(new PreparedSkillContract(
                        skill,
                        compiled.SkillId,
                        compiled.PresentationHash));
                }
            }

            List<D0CombatVfxAssetReference> references =
                new List<D0CombatVfxAssetReference>();
            if (!registry.TryCollectVfxReferences(
                references,
                profile.PoolCapacities.WorldEffectCapacity,
                out error)
                || !audioPool.TryPrepare(
                    transform,
                    profile.PoolCapacities.AudioSourceCapacity,
                    out error)
                || !vfxWorld.TrySetGlobalActiveCapacity(
                    profile.PoolCapacities.WorldEffectCapacity,
                    out error)
                || !vfxWorld.TryPrepareForScenario(references, out error))
            {
                registry.Clear();
                preparedSkills.Clear();
                audioPool.Dispose();
                return false;
            }

            PresentationFaultCount = 0;
            preparedProfile = profile;
            preparedWorldEffectCapacity =
                profile.PoolCapacities.WorldEffectCapacity;
            preparedAudioSourceCapacity =
                profile.PoolCapacities.AudioSourceCapacity;
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

        public bool TryBorrowHeldVfx(
            FpgPresentationHandle handle,
            Transform source,
            out GameObject instance)
        {
            instance = null;
            if (source == null)
            {
                return Reject();
            }

            return TryBorrowHeldVfx(
                handle,
                source.position,
                source.rotation,
                source.lossyScale,
                out instance);
        }

        public bool TryBorrowHeldVfx(
            FpgPresentationHandle handle,
            Vector3 position,
            Quaternion rotation,
            Vector3 sourceScale,
            out GameObject instance)
        {
            instance = null;
            if (!TryResolveVfx(handle, out FpgRegisteredPresentation entry))
            {
                return Reject();
            }

            Vector3 scale = Vector3.Scale(sourceScale, entry.Vfx.Scale);
            if (!vfxWorld.TryAcquireHeld(
                    FpgSkillPresentationRegistry.GetPoolKey(handle),
                    position,
                    rotation * Quaternion.Euler(
                        entry.Vfx.RotationOffsetEuler),
                    scale,
                    out instance))
            {
                return Reject();
            }

            ChargeProgressVfxDriver progressDriver = instance == null
                ? null
                : instance.GetComponent<ChargeProgressVfxDriver>();
            progressDriver?.SetProgress(0f);
            return true;
        }

        public bool TryUpdateHeldVfx(
            FpgPresentationHandle handle,
            GameObject instance,
            Transform source,
            float normalizedProgress)
        {
            if (source == null)
            {
                return Reject();
            }

            return TryUpdateHeldVfx(
                handle,
                instance,
                source.position,
                source.rotation,
                source.lossyScale,
                normalizedProgress);
        }

        public bool TryUpdateHeldVfx(
            FpgPresentationHandle handle,
            GameObject instance,
            Vector3 position,
            Quaternion rotation,
            Vector3 sourceScale,
            float normalizedProgress)
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
            visual.localScale = Vector3.Scale(sourceScale, entry.Vfx.Scale);
            instance.GetComponent<ChargeProgressVfxDriver>()
                ?.SetProgress(normalizedProgress);
            return true;
        }

        public bool TryReleaseHeldVfx(GameObject instance)
        {
            if (instance == null || vfxWorld == null)
            {
                return Reject();
            }

            instance.GetComponent<ChargeProgressVfxDriver>()?.ResetForPool();
            return AcceptOrReject(vfxWorld.TryRelease(instance));
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

        private bool TryValidatePreparedContract(
            IEnumerable<FpgSkillTimelineDefinition> skillDefinitions,
            CombatPresentationProfile profile,
            out string error)
        {
            const string ChangedContractError =
                "Skill presentation world preparation inputs cannot change after preparation.";
            if (profile == null
                || profile != preparedProfile
                || profile.PoolCapacities == null
                || profile.PoolCapacities.WorldEffectCapacity
                    != preparedWorldEffectCapacity
                || profile.PoolCapacities.AudioSourceCapacity
                    != preparedAudioSourceCapacity)
            {
                error = ChangedContractError;
                return false;
            }

            int preparedIndex = 0;
            if (skillDefinitions != null)
            {
                foreach (FpgSkillTimelineDefinition skill in skillDefinitions)
                {
                    if (skill == null)
                    {
                        continue;
                    }

                    if (preparedIndex >= preparedSkills.Count)
                    {
                        error = ChangedContractError;
                        return false;
                    }

                    PreparedSkillContract preparedSkill =
                        preparedSkills[preparedIndex];
                    if (skill != preparedSkill.Definition
                        || !skill.TryCompile(
                            out FpgCompiledSkillDefinition compiled,
                            out _)
                        || compiled.SkillId != preparedSkill.SkillId
                        || compiled.PresentationHash
                            != preparedSkill.PresentationHash)
                    {
                        error = ChangedContractError;
                        return false;
                    }

                    preparedIndex++;
                }
            }

            if (preparedIndex != preparedSkills.Count)
            {
                error = ChangedContractError;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private readonly struct PreparedSkillContract
        {
            public PreparedSkillContract(
                FpgSkillTimelineDefinition definition,
                int skillId,
                ulong presentationHash)
            {
                Definition = definition;
                SkillId = skillId;
                PresentationHash = presentationHash;
            }

            public FpgSkillTimelineDefinition Definition { get; }
            public int SkillId { get; }
            public ulong PresentationHash { get; }
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
