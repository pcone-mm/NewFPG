using System;
using System.Collections.Generic;
using System.Globalization;
using FPG.Demo.Skills;

namespace FPG.Demo.Unity
{
    public enum FpgRegisteredPresentationKind
    {
        None = 0,
        Vfx,
        Audio,
        CameraShake
    }

    public readonly struct FpgRegisteredPresentation
    {
        internal FpgRegisteredPresentation(
            FpgPresentationHandle handle,
            FpgRegisteredPresentationKind kind,
            FpgVfxPresentationDefinition vfx,
            FpgAudioPresentationDefinition audio,
            FpgCameraShakePresentationDefinition cameraShake,
            FpgVfxPresentationAnchor anchor,
            string socketId,
            string authoredPath)
        {
            Handle = handle;
            Kind = kind;
            Vfx = vfx;
            Audio = audio;
            CameraShake = cameraShake;
            Anchor = anchor;
            SocketId = socketId ?? string.Empty;
            AuthoredPath = authoredPath ?? string.Empty;
        }

        public FpgPresentationHandle Handle { get; }
        public FpgRegisteredPresentationKind Kind { get; }
        public FpgVfxPresentationDefinition Vfx { get; }
        public FpgAudioPresentationDefinition Audio { get; }
        public FpgCameraShakePresentationDefinition CameraShake { get; }
        public FpgVfxPresentationAnchor Anchor { get; }
        public string SocketId { get; }
        public string AuthoredPath { get; }
        public bool IsValid => Handle.IsValid
            && Kind != FpgRegisteredPresentationKind.None
            && (Kind == FpgRegisteredPresentationKind.Vfx && Vfx != null
                || Kind == FpgRegisteredPresentationKind.Audio
                    && Audio != null
                || Kind == FpgRegisteredPresentationKind.CameraShake
                    && CameraShake != null);
    }

    /// <summary>
    /// Unity-owned lookup from pure runtime presentation handles to authored
    /// Prefabs, clips and parameters. Registration is preparation-only.
    /// </summary>
    public sealed class FpgSkillPresentationRegistry
    {
        private readonly Dictionary<FpgPresentationHandle,
            FpgRegisteredPresentation> presentations =
                new Dictionary<FpgPresentationHandle,
                    FpgRegisteredPresentation>();

        public int Count => presentations.Count;

        public static string GetPoolKey(FpgPresentationHandle handle)
        {
            return handle.IsValid
                ? "skill.presentation."
                    + handle.Value.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        public bool TryRegister(
            FpgSkillTimelineDefinition definition,
            out string error)
        {
            error = string.Empty;
            if (definition == null
                || !definition.TryCompile(
                    out FpgCompiledSkillDefinition compiled,
                    out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "Skill presentation registry requires a valid V3 skill.";
                }

                return false;
            }

            for (int sequenceIndex = 0;
                sequenceIndex < definition.Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence =
                    definition.Sequences[sequenceIndex];
                if (sequence == null
                    || !compiled.TryGetSequence(
                        sequence.Kind,
                        out FpgCompiledSkillSequence compiledSequence))
                {
                    error = "Skill presentation registry could not align authored and compiled sequences.";
                    return false;
                }

                string scope = definition.SkillId + ":" + sequence.Kind;
                if (!TryRegisterActiveEvents(
                        sequence,
                        compiledSequence,
                        scope,
                        out error)
                    || !TryRegisterActionPresentations(
                        sequence,
                        scope,
                        out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool TryResolve(
            FpgPresentationHandle handle,
            out FpgRegisteredPresentation presentation)
        {
            presentation = default(FpgRegisteredPresentation);
            return handle.IsValid
                && presentations.TryGetValue(handle, out presentation);
        }

        public static bool TryResolveActionPresentation(
            FpgSkillTimelineDefinition definition,
            int gameplayEventId,
            out FpgCompiledSkillActionPresentation presentation)
        {
            presentation = default(FpgCompiledSkillActionPresentation);
            if (definition == null || gameplayEventId <= 0
                || !definition.TryCompile(
                    out FpgCompiledSkillDefinition compiled,
                    out _))
            {
                return false;
            }

            for (int sequenceIndex = 0;
                sequenceIndex < compiled.SequenceCount;
                sequenceIndex++)
            {
                FpgCompiledSkillSequence sequence =
                    compiled.GetSequence(sequenceIndex);
                for (int eventIndex = 0;
                    eventIndex < sequence.EventCount;
                    eventIndex++)
                {
                    FpgCompiledSkillEvent skillEvent =
                        sequence.GetEvent(eventIndex);
                    if (skillEvent.Kind != FpgSkillEventKind.GameplayAction
                        || skillEvent.EventId != gameplayEventId)
                    {
                        continue;
                    }

                    for (int presentationIndex = 0;
                        presentationIndex
                            < sequence.ActionPresentationCount;
                        presentationIndex++)
                    {
                        FpgCompiledSkillActionPresentation candidate =
                            sequence.GetActionPresentation(
                                presentationIndex);
                        if (candidate.ActionKind == skillEvent.ActionKind
                            && candidate.ActionIndex
                                == skillEvent.ActionIndex)
                        {
                            presentation = candidate;
                            return true;
                        }
                    }

                    return false;
                }
            }

            return false;
        }

        public void Clear()
        {
            presentations.Clear();
        }

        public bool TryCollectVfxReferences(
            ICollection<D0CombatVfxAssetReference> output,
            int totalPrewarmCapacity,
            out string error)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (totalPrewarmCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalPrewarmCapacity));
            }

            int vfxCount = 0;
            foreach (KeyValuePair<FpgPresentationHandle,
                FpgRegisteredPresentation> pair in presentations)
            {
                FpgRegisteredPresentation entry = pair.Value;
                if (entry.Kind == FpgRegisteredPresentationKind.Vfx
                    && entry.Vfx != null)
                {
                    vfxCount++;
                }
            }

            if (vfxCount > totalPrewarmCapacity)
            {
                error =
                    "Skill presentation VFX handles exceed the global world-effect pool budget.";
                return false;
            }

            int baseCapacity = vfxCount == 0
                ? 0
                : totalPrewarmCapacity / vfxCount;
            int remainder = vfxCount == 0
                ? 0
                : totalPrewarmCapacity % vfxCount;
            int vfxIndex = 0;
            foreach (KeyValuePair<FpgPresentationHandle,
                FpgRegisteredPresentation> pair in presentations)
            {
                FpgRegisteredPresentation entry = pair.Value;
                if (entry.Kind != FpgRegisteredPresentationKind.Vfx
                    || entry.Vfx == null)
                {
                    continue;
                }

                int poolCapacity = baseCapacity
                    + (vfxIndex < remainder ? 1 : 0);
                vfxIndex++;

                output.Add(new D0CombatVfxAssetReference(
                    GetPoolKey(entry.Handle),
                    entry.Vfx.Prefab,
                    poolCapacity,
                    entry.Vfx.DurationSeconds,
                    "presentation",
                    0,
                    D0CombatVfxCategory.SkillPresentation));
            }

            error = string.Empty;
            return true;
        }

        private bool TryRegisterActiveEvents(
            FpgSkillSequenceDefinition sequence,
            FpgCompiledSkillSequence compiledSequence,
            string scope,
            out string error)
        {
            error = string.Empty;
            for (int trackIndex = 0;
                trackIndex < sequence.ActivePresentationTracks.Count;
                trackIndex++)
            {
                FpgSkillActivePresentationTrackDefinition track =
                    sequence.ActivePresentationTracks[trackIndex];
                if (track == null)
                {
                    error = "Active presentation track is missing.";
                    return false;
                }

                for (int index = 0; index < track.VfxEvents.Count; index++)
                {
                    FpgVfxPresentationEventDefinition authored =
                        track.VfxEvents[index];
                    if (!TryFindCompiledActiveEvent(
                            compiledSequence,
                            authored.EventId,
                            out FpgCompiledSkillEvent compiledEvent)
                        || !TryAdd(
                            new FpgRegisteredPresentation(
                                compiledEvent.PresentationHandle,
                                FpgRegisteredPresentationKind.Vfx,
                                authored.Presentation,
                                null,
                                null,
                                authored.Anchor,
                                authored.OwnerSocketId,
                                scope + ":" + authored.EventId),
                            out error))
                    {
                        return false;
                    }
                }

                for (int index = 0; index < track.AudioEvents.Count; index++)
                {
                    FpgAudioPresentationEventDefinition authored =
                        track.AudioEvents[index];
                    if (!TryFindCompiledActiveEvent(
                            compiledSequence,
                            authored.EventId,
                            out FpgCompiledSkillEvent compiledEvent)
                        || !TryAdd(
                            new FpgRegisteredPresentation(
                                compiledEvent.PresentationHandle,
                                FpgRegisteredPresentationKind.Audio,
                                null,
                                authored.Presentation,
                                null,
                                FpgVfxPresentationAnchor.OwnerRoot,
                                string.Empty,
                                scope + ":" + authored.EventId),
                            out error))
                    {
                        return false;
                    }
                }

                for (int index = 0;
                    index < track.CameraShakeEvents.Count;
                    index++)
                {
                    FpgCameraShakePresentationEventDefinition authored =
                        track.CameraShakeEvents[index];
                    if (!TryFindCompiledActiveEvent(
                            compiledSequence,
                            authored.EventId,
                            out FpgCompiledSkillEvent compiledEvent)
                        || !TryAdd(
                            new FpgRegisteredPresentation(
                                compiledEvent.PresentationHandle,
                                FpgRegisteredPresentationKind.CameraShake,
                                null,
                                null,
                                authored.Presentation,
                                FpgVfxPresentationAnchor.OwnerRoot,
                                string.Empty,
                                scope + ":" + authored.EventId),
                            out error))
                    {
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private bool TryRegisterActionPresentations(
            FpgSkillSequenceDefinition sequence,
            string scope,
            out string error)
        {
            for (int index = 0; index < sequence.AttackEvents.Count; index++)
            {
                FpgSkillAttackEventDefinition action =
                    sequence.AttackEvents[index];
                string actionScope = scope + ":" + action.EventId;
                if (action.TrajectoryPresentation != null
                    && !TryAddVfx(
                        actionScope + ":trajectory.vfx",
                        action.TrajectoryPresentation,
                        out error))
                {
                    return false;
                }

                if (!TryAddImpact(
                    actionScope + ":impact",
                    action.ImpactPresentation,
                    out error))
                {
                    return false;
                }
            }

            for (int index = 0;
                index < sequence.ProjectileEvents.Count;
                index++)
            {
                FpgSkillProjectileEventDefinition action =
                    sequence.ProjectileEvents[index];
                string actionScope = scope + ":" + action.EventId;
                if (action.FlightVfx != null
                    && !TryAddVfx(
                        actionScope + ":flight.vfx",
                        action.FlightVfx,
                        out error))
                {
                    return false;
                }

                if (!TryAddOptional(
                    actionScope + ":flight.audio",
                    action.FlightAudio,
                    out error))
                {
                    return false;
                }

                if (!TryAddImpact(
                    actionScope + ":collision",
                    action.CollisionPresentation,
                    out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private bool TryAddImpact(
            string prefix,
            FpgImpactPresentationBundleDefinition definition,
            out string error)
        {
            if (definition == null)
            {
                error = string.Empty;
                return true;
            }

            return TryAddOptional(prefix + ":base.vfx", definition.BaseVfx,
                    out error)
                && TryAddOptional(prefix + ":base.audio", definition.BaseAudio,
                    out error)
                && TryAddOptional(prefix + ":environment.audio",
                    definition.EnvironmentAudioOverride, out error)
                && TryAddOptional(prefix + ":interception.audio",
                    definition.InterceptionAudioOverride, out error)
                && TryAddOptional(prefix + ":base.camera-shake",
                    definition.BaseCameraShake, out error)
                && TryAddOptional(prefix + ":weakpoint.vfx",
                    definition.WeakpointVfxOverride, out error)
                && TryAddOptional(prefix + ":weakpoint.audio",
                    definition.WeakpointAudioOverride, out error)
                && TryAddOptional(prefix + ":weakpoint.camera-shake",
                    definition.WeakpointCameraShakeOverride, out error);
        }

        private bool TryAddOptional(
            string key,
            FpgVfxPresentationDefinition value,
            out string error)
        {
            if (value == null)
            {
                error = string.Empty;
                return true;
            }

            return TryAddVfx(key, value, out error);
        }

        private bool TryAddOptional(
            string key,
            FpgAudioPresentationDefinition value,
            out string error)
        {
            if (value == null)
            {
                error = string.Empty;
                return true;
            }

            return TryAdd(new FpgRegisteredPresentation(
                FpgSkillStableId.CompilePresentationHandle(key),
                FpgRegisteredPresentationKind.Audio,
                null,
                value,
                null,
                FpgVfxPresentationAnchor.OwnerRoot,
                string.Empty,
                key), out error);
        }

        private bool TryAddOptional(
            string key,
            FpgCameraShakePresentationDefinition value,
            out string error)
        {
            if (value == null)
            {
                error = string.Empty;
                return true;
            }

            return TryAdd(new FpgRegisteredPresentation(
                FpgSkillStableId.CompilePresentationHandle(key),
                FpgRegisteredPresentationKind.CameraShake,
                null,
                null,
                value,
                FpgVfxPresentationAnchor.OwnerRoot,
                string.Empty,
                key), out error);
        }

        private bool TryAddVfx(
            string key,
            FpgVfxPresentationDefinition value,
            out string error)
        {
            return TryAdd(new FpgRegisteredPresentation(
                FpgSkillStableId.CompilePresentationHandle(key),
                FpgRegisteredPresentationKind.Vfx,
                value,
                null,
                null,
                FpgVfxPresentationAnchor.OwnerRoot,
                string.Empty,
                key), out error);
        }

        private bool TryAdd(
            FpgRegisteredPresentation presentation,
            out string error)
        {
            if (!presentation.IsValid)
            {
                error = "Presentation registry entry is invalid.";
                return false;
            }

            if (presentations.TryGetValue(
                presentation.Handle,
                out FpgRegisteredPresentation existing))
            {
                error = "Presentation handle collision between '"
                    + existing.AuthoredPath + "' and '"
                    + presentation.AuthoredPath + "'.";
                return false;
            }

            presentations.Add(presentation.Handle, presentation);
            error = string.Empty;
            return true;
        }

        private static bool TryFindCompiledActiveEvent(
            FpgCompiledSkillSequence sequence,
            string eventId,
            out FpgCompiledSkillEvent compiledEvent)
        {
            int id = FpgSkillStableId.CompileEvent(eventId);
            for (int index = 0; index < sequence.EventCount; index++)
            {
                FpgCompiledSkillEvent candidate = sequence.GetEvent(index);
                if (candidate.Kind == FpgSkillEventKind.ActivePresentation
                    && candidate.EventId == id)
                {
                    compiledEvent = candidate;
                    return true;
                }
            }

            compiledEvent = default(FpgCompiledSkillEvent);
            return false;
        }
    }
}
