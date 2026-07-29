using System;
using System.Collections.Generic;
using FPG.Demo.Core;
using FPG.Demo.Skills;
using UnityEngine;
using UnityEngine.Serialization;

namespace FPG.Demo.Unity
{
    [Serializable]
    public sealed class FpgSkillWarningDefinition
    {
        [SerializeField]
        private string eventId = "warning";

        [SerializeField]
        private string warningId = "warning.default";

        [SerializeField, Min(0)]
        private int startTick;

        [SerializeField, Min(0)]
        private int endTick;

        [FormerlySerializedAs("sortOrder")]
        [SerializeField, Min(0)]
        private int authoredOrdinal;

        [SerializeField]
        private string socketId = string.Empty;

        public string EventId => eventId;
        public string WarningId => warningId;
        public int StartTick => startTick;
        public int EndTick => endTick;
        public int AuthoredOrdinal => authoredOrdinal;
        public string SocketId => socketId;

        internal string StartEventId => eventId + ".start";
        internal string EndEventId => eventId + ".end";

        internal bool TryValidate(int durationTicks, out string error)
        {
            if (!FpgSkillStableId.IsValid(eventId)
                || !FpgSkillStableId.IsValid(warningId))
            {
                error = "Skill warning requires stable event and warning IDs.";
                return false;
            }

            if (startTick < 0
                || endTick < startTick
                || endTick > durationTicks
                || authoredOrdinal < 0
                || authoredOrdinal == int.MaxValue)
            {
                error = $"Skill warning '{eventId}' has an invalid tick range or sort order.";
                return false;
            }

            if (!string.IsNullOrEmpty(socketId)
                && !FpgSkillStableId.IsValid(socketId))
            {
                error = $"Skill warning '{eventId}' has an invalid socket ID.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class FpgSkillSequenceDefinition
    {
        [SerializeField]
        private FpgSkillSequenceKind kind = FpgSkillSequenceKind.Execute;

        [SerializeField, Min(0)]
        private int durationTicks;

        [SerializeField]
        private string[] alternateAnimations = Array.Empty<string>();

        [SerializeField]
        private string mainAnimation = "idle";

        [SerializeField]
        private bool loop;

        [SerializeField]
        private bool holdUntilCanceled;

        [SerializeField]
        private FpgSkillAnimationPlaybackMode animationPlaybackMode =
            FpgSkillAnimationPlaybackMode.NaturalSpeed;

        [SerializeField, Min(0)]
        private int animationStartTick;

        [SerializeField, Min(0)]
        private int animationEndTick;

        [SerializeField, Min(0)]
        private int sourceAnimationDurationTicks;

        [SerializeField]
        private FpgSkillAttackEventDefinition[] attackEvents =
            Array.Empty<FpgSkillAttackEventDefinition>();

        [SerializeField]
        private FpgSkillProjectileEventDefinition[] projectileEvents =
            Array.Empty<FpgSkillProjectileEventDefinition>();

        [SerializeField]
        private FpgSkillReloadEventDefinition[] reloadEvents =
            Array.Empty<FpgSkillReloadEventDefinition>();

        [SerializeField]
        private FpgSkillSummonEventDefinition[] summonEvents =
            Array.Empty<FpgSkillSummonEventDefinition>();

        [SerializeField]
        private FpgSkillSelfDestructOwnerEventDefinition[] selfDestructOwnerEvents =
            Array.Empty<FpgSkillSelfDestructOwnerEventDefinition>();

        [SerializeField]
        private FpgSkillActivePresentationTrackDefinition[]
            activePresentationTracks =
                Array.Empty<FpgSkillActivePresentationTrackDefinition>();

        [SerializeField]
        private FpgSkillWarningDefinition[] warnings =
            Array.Empty<FpgSkillWarningDefinition>();

        public FpgSkillSequenceKind Kind => kind;
        public int DurationTicks => durationTicks;
        public IReadOnlyList<string> AlternateAnimations =>
            alternateAnimations ?? Array.Empty<string>();
        public string MainAnimation => mainAnimation;
        public bool Loop => loop;
        public bool HoldUntilCanceled => holdUntilCanceled;
        public FpgSkillAnimationPlaybackMode AnimationPlaybackMode =>
            animationPlaybackMode;
        public int AnimationStartTick => animationStartTick;
        public int AnimationEndTick => ResolvedAnimationEndTick;
        public int SourceAnimationDurationTicks => sourceAnimationDurationTicks;

        private int ResolvedAnimationEndTick =>
            animationEndTick == 0 && durationTicks > 0
                ? durationTicks
                : animationEndTick;

        public IReadOnlyList<FpgSkillAttackEventDefinition> AttackEvents =>
            attackEvents ?? Array.Empty<FpgSkillAttackEventDefinition>();
        public IReadOnlyList<FpgSkillProjectileEventDefinition>
            ProjectileEvents =>
                projectileEvents
                ?? Array.Empty<FpgSkillProjectileEventDefinition>();
        public IReadOnlyList<FpgSkillReloadEventDefinition> ReloadEvents =>
            reloadEvents ?? Array.Empty<FpgSkillReloadEventDefinition>();
        public IReadOnlyList<FpgSkillSummonEventDefinition> SummonEvents =>
            summonEvents ?? Array.Empty<FpgSkillSummonEventDefinition>();
        public IReadOnlyList<FpgSkillSelfDestructOwnerEventDefinition>
            SelfDestructOwnerEvents =>
                selfDestructOwnerEvents
                ?? Array.Empty<FpgSkillSelfDestructOwnerEventDefinition>();
        public IReadOnlyList<FpgSkillActivePresentationTrackDefinition>
            ActivePresentationTracks =>
                activePresentationTracks
                ?? Array.Empty<FpgSkillActivePresentationTrackDefinition>();
        public IReadOnlyList<FpgSkillWarningDefinition> Warnings =>
            warnings ?? Array.Empty<FpgSkillWarningDefinition>();

        public bool HasGameplayActions => AttackEvents.Count > 0
            || ProjectileEvents.Count > 0
            || ReloadEvents.Count > 0
            || SummonEvents.Count > 0
            || SelfDestructOwnerEvents.Count > 0;
        public int GameplayActionCount => checked(
            AttackEvents.Count
            + ProjectileEvents.Count
            + ReloadEvents.Count
            + SummonEvents.Count
            + SelfDestructOwnerEvents.Count);

        internal bool TryValidate(
            HashSet<string> eventIds,
            HashSet<int> compiledEventIds,
            out string error)
        {
            if (!Enum.IsDefined(typeof(FpgSkillSequenceKind), kind)
                || kind == FpgSkillSequenceKind.None
                || durationTicks < 0
                || string.IsNullOrWhiteSpace(mainAnimation))
            {
                error = "Skill sequence requires a valid kind, duration and main animation.";
                return false;
            }

            if (holdUntilCanceled && HasGameplayActions)
            {
                error = $"Skill sequence '{kind}' cannot hold until canceled while containing gameplay actions.";
                return false;
            }

            string[] animationVariants =
                alternateAnimations ?? Array.Empty<string>();
            HashSet<string> animationNames =
                new HashSet<string>(StringComparer.Ordinal)
                {
                    mainAnimation
                };
            for (int index = 0; index < animationVariants.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(animationVariants[index])
                    || !animationNames.Add(animationVariants[index]))
                {
                    error = $"Skill sequence '{kind}' has an empty or duplicate animation variant.";
                    return false;
                }
            }

            if (!Enum.IsDefined(
                    typeof(FpgSkillAnimationPlaybackMode),
                    animationPlaybackMode)
                || animationStartTick < 0
                || ResolvedAnimationEndTick < animationStartTick
                || ResolvedAnimationEndTick > durationTicks
                || (animationPlaybackMode
                        == FpgSkillAnimationPlaybackMode.FitInterval
                    && durationTicks > 0
                    && ResolvedAnimationEndTick == animationStartTick))
            {
                error =
                    $"Skill sequence '{kind}' has invalid animation playback timing.";
                return false;
            }

            HashSet<int> authoredPositions = new HashSet<int>();
            Dictionary<string, int> gameplayEventTicks =
                new Dictionary<string, int>(StringComparer.Ordinal);

            if (!TryValidateActions(
                    attackEvents
                        ?? Array.Empty<FpgSkillAttackEventDefinition>(),
                    "attack",
                    gameplayEventTicks,
                    eventIds,
                    compiledEventIds,
                    authoredPositions,
                    out error)
                || !TryValidateActions(
                    projectileEvents
                        ?? Array.Empty<FpgSkillProjectileEventDefinition>(),
                    "projectile",
                    gameplayEventTicks,
                    eventIds,
                    compiledEventIds,
                    authoredPositions,
                    out error)
                || !TryValidateActions(
                    reloadEvents
                        ?? Array.Empty<FpgSkillReloadEventDefinition>(),
                    "reload",
                    gameplayEventTicks,
                    eventIds,
                    compiledEventIds,
                    authoredPositions,
                    out error)
                || !TryValidateActions(
                    summonEvents
                        ?? Array.Empty<FpgSkillSummonEventDefinition>(),
                    "summon",
                    gameplayEventTicks,
                    eventIds,
                    compiledEventIds,
                    authoredPositions,
                    out error)
                || !TryValidateActions(
                    selfDestructOwnerEvents
                        ?? Array.Empty<FpgSkillSelfDestructOwnerEventDefinition>(),
                    "self-destruct owner",
                    gameplayEventTicks,
                    eventIds,
                    compiledEventIds,
                    authoredPositions,
                    out error))
            {
                return false;
            }

            if (!TryValidateSelfDestructBindings(out error))
            {
                return false;
            }

            FpgSkillActivePresentationTrackDefinition[] activeTracks =
                activePresentationTracks
                ?? Array.Empty<FpgSkillActivePresentationTrackDefinition>();

            HashSet<string> trackIds =
                new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> compiledTrackIds = new HashSet<int>();
            for (int trackIndex = 0;
                trackIndex < activeTracks.Length;
                trackIndex++)
            {
                FpgSkillActivePresentationTrackDefinition track =
                    activeTracks[trackIndex];
                if (track == null)
                {
                    error =
                        $"Skill sequence '{kind}' has a missing active presentation track at index {trackIndex}.";
                    return false;
                }

                int compiledTrackId = FpgSkillStableId
                    .CompilePresentationTrack(track.TrackId);
                if (!track.TryValidateHeader(out error)
                    || !trackIds.Add(track.TrackId)
                    || !compiledTrackIds.Add(compiledTrackId))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error =
                            $"Skill sequence '{kind}' repeats active presentation track '{track.TrackId}' or has a stable-ID collision.";
                    }

                    return false;
                }

                if (!TryValidatePresentationEvents(
                        track.VfxEvents,
                        "VFX",
                        gameplayEventTicks,
                        eventIds,
                        compiledEventIds,
                        authoredPositions,
                        out error)
                    || !TryValidatePresentationEvents(
                        track.AudioEvents,
                        "audio",
                        gameplayEventTicks,
                        eventIds,
                        compiledEventIds,
                        authoredPositions,
                        out error)
                    || !TryValidatePresentationEvents(
                        track.CameraShakeEvents,
                        "camera shake",
                        gameplayEventTicks,
                        eventIds,
                        compiledEventIds,
                        authoredPositions,
                        out error))
                {
                    return false;
                }
            }

            FpgSkillWarningDefinition[] warningValues =
                warnings ?? Array.Empty<FpgSkillWarningDefinition>();
            for (int index = 0; index < warningValues.Length; index++)
            {
                FpgSkillWarningDefinition value = warningValues[index];
                if (value == null)
                {
                    error = $"Skill sequence '{kind}' has a missing warning at index {index}.";
                    return false;
                }

                if (!value.TryValidate(durationTicks, out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = $"Skill sequence '{kind}' has an invalid warning at index {index}.";
                    }

                    return false;
                }

                if (!TryAddAuthoredEventId(value.EventId, eventIds, out error)
                    || !TryAddEventId(
                        value.StartEventId,
                        eventIds,
                        compiledEventIds,
                        out error)
                    || !TryAddEventId(
                        value.EndEventId,
                        eventIds,
                        compiledEventIds,
                        out error))
                {
                    return false;
                }

                if (!TryAddAuthoredPosition(
                        value.StartTick,
                        value.AuthoredOrdinal,
                        authoredPositions,
                        out error)
                    || !TryAddAuthoredPosition(
                        value.EndTick,
                        value.AuthoredOrdinal + 1,
                        authoredPositions,
                        out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        internal FpgCompiledSkillSequence Compile(
            string skillId,
            FpgSkillActionIndexOffsets actionOffsets)
        {
            FpgSkillAttackEventDefinition[] attacks =
                attackEvents ?? Array.Empty<FpgSkillAttackEventDefinition>();
            FpgSkillProjectileEventDefinition[] projectiles =
                projectileEvents
                ?? Array.Empty<FpgSkillProjectileEventDefinition>();
            FpgSkillReloadEventDefinition[] reloads =
                reloadEvents ?? Array.Empty<FpgSkillReloadEventDefinition>();
            FpgSkillSummonEventDefinition[] summons =
                summonEvents ?? Array.Empty<FpgSkillSummonEventDefinition>();
            FpgSkillSelfDestructOwnerEventDefinition[] selfDestructs =
                selfDestructOwnerEvents
                ?? Array.Empty<FpgSkillSelfDestructOwnerEventDefinition>();
            FpgSkillActivePresentationTrackDefinition[] activeTracks =
                activePresentationTracks
                ?? Array.Empty<FpgSkillActivePresentationTrackDefinition>();
            FpgSkillWarningDefinition[] warningValues =
                warnings ?? Array.Empty<FpgSkillWarningDefinition>();
            int gameplayEventCount = checked(
                attacks.Length
                + projectiles.Length
                + reloads.Length
                + summons.Length
                + selfDestructs.Length);
            int activePresentationEventCount = 0;
            for (int index = 0; index < activeTracks.Length; index++)
            {
                activePresentationEventCount = checked(
                    activePresentationEventCount
                    + activeTracks[index].VfxEvents.Count
                    + activeTracks[index].AudioEvents.Count
                    + activeTracks[index].CameraShakeEvents.Count);
            }
            int eventCount = checked(
                gameplayEventCount
                + activePresentationEventCount
                + warningValues.Length * 2);
            FpgCompiledSkillEvent[] compiled = new FpgCompiledSkillEvent[eventCount];
            int writeIndex = 0;
            string presentationScope = skillId + ":" + kind;

            writeIndex = CompileActions(
                attacks,
                FpgSkillActionKind.Attack,
                actionOffsets.Attack,
                compiled,
                writeIndex);
            writeIndex = CompileActions(
                projectiles,
                FpgSkillActionKind.LaunchProjectile,
                actionOffsets.LaunchProjectile,
                compiled,
                writeIndex);
            writeIndex = CompileActions(
                reloads,
                FpgSkillActionKind.CommitReload,
                actionOffsets.CommitReload,
                compiled,
                writeIndex);
            writeIndex = CompileActions(
                summons,
                FpgSkillActionKind.SummonActors,
                actionOffsets.SummonActors,
                compiled,
                writeIndex);
            writeIndex = CompileActions(
                selfDestructs,
                FpgSkillActionKind.SelfDestructOwner,
                actionOffsets.SelfDestructOwner,
                compiled,
                writeIndex);

            for (int trackIndex = 0;
                trackIndex < activeTracks.Length;
                trackIndex++)
            {
                FpgSkillActivePresentationTrackDefinition track =
                    activeTracks[trackIndex];
                int trackId = FpgSkillStableId.CompilePresentationTrack(
                    track.TrackId);
                writeIndex = CompilePresentationEvents(
                    track.VfxEvents,
                    trackId,
                    presentationScope,
                    compiled,
                    writeIndex);
                writeIndex = CompilePresentationEvents(
                    track.AudioEvents,
                    trackId,
                    presentationScope,
                    compiled,
                    writeIndex);
                writeIndex = CompilePresentationEvents(
                    track.CameraShakeEvents,
                    trackId,
                    presentationScope,
                    compiled,
                    writeIndex);
            }

            for (int index = 0; index < warningValues.Length; index++)
            {
                FpgSkillWarningDefinition value = warningValues[index];
                int warningId = FpgSkillStableId.CompileWarning(value.WarningId);
                int socketId = FpgSkillStableId.CompileOptionalSocket(value.SocketId);
                compiled[writeIndex++] = new FpgCompiledSkillEvent(
                    FpgSkillStableId.CompileEvent(value.StartEventId),
                    value.StartTick,
                    FpgSkillEventKind.WarningStarted,
                    warningId,
                    value.AuthoredOrdinal,
                    socketId);
                compiled[writeIndex++] = new FpgCompiledSkillEvent(
                    FpgSkillStableId.CompileEvent(value.EndEventId),
                    value.EndTick,
                    FpgSkillEventKind.WarningEnded,
                    warningId,
                    value.AuthoredOrdinal + 1,
                    socketId);
            }

            string[] authoredVariants =
                alternateAnimations ?? Array.Empty<string>();
            int[] compiledVariants = new int[authoredVariants.Length];
            for (int index = 0; index < authoredVariants.Length; index++)
            {
                compiledVariants[index] =
                    FpgSkillStableId.CompileAnimation(authoredVariants[index]);
            }

            int actionPresentationCount = CountActionPresentations(attacks)
                + CountActionPresentations(projectiles)
                + CountActionPresentations(reloads);
            FpgCompiledSkillActionPresentation[] actionPresentations =
                new FpgCompiledSkillActionPresentation[
                    actionPresentationCount];
            int actionPresentationIndex = 0;
            actionPresentationIndex = CompileActionPresentations(
                attacks,
                FpgSkillActionKind.Attack,
                actionOffsets.Attack,
                presentationScope,
                actionPresentations,
                actionPresentationIndex);
            actionPresentationIndex = CompileActionPresentations(
                projectiles,
                FpgSkillActionKind.LaunchProjectile,
                actionOffsets.LaunchProjectile,
                presentationScope,
                actionPresentations,
                actionPresentationIndex);
            CompileActionPresentations(
                reloads,
                FpgSkillActionKind.CommitReload,
                actionOffsets.CommitReload,
                presentationScope,
                actionPresentations,
                actionPresentationIndex);

            return new FpgCompiledSkillSequence(
                kind,
                durationTicks,
                FpgSkillStableId.CompileAnimation(mainAnimation),
                loop,
                animationPlaybackMode,
                animationStartTick,
                ResolvedAnimationEndTick,
                compiledVariants,
                compiled,
                actionPresentations,
                holdUntilCanceled);
        }

        private static int CountActionPresentations<TAction>(TAction[] values)
            where TAction : FpgSkillGameplayActionDefinition
        {
            int count = 0;
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index].HasPresentation)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CompileActionPresentations<TAction>(
            TAction[] values,
            FpgSkillActionKind actionKind,
            int actionOffset,
            string scopePrefix,
            FpgCompiledSkillActionPresentation[] destination,
            int writeIndex)
            where TAction : FpgSkillGameplayActionDefinition
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index].HasPresentation)
                {
                    destination[writeIndex++] = values[index]
                        .CompilePresentation(
                            actionKind,
                            checked(actionOffset + index),
                            scopePrefix);
                }
            }

            return writeIndex;
        }

        private static int CompilePresentationEvents<TEvent>(
            IReadOnlyList<TEvent> values,
            int trackId,
            string scopePrefix,
            FpgCompiledSkillEvent[] destination,
            int writeIndex)
            where TEvent : FpgActivePresentationEventDefinition
        {
            for (int index = 0; index < values.Count; index++)
            {
                destination[writeIndex++] = values[index].Compile(
                    trackId,
                    scopePrefix);
            }

            return writeIndex;
        }

        private bool TryValidatePresentationEvents<TEvent>(
            IReadOnlyList<TEvent> values,
            string label,
            Dictionary<string, int> gameplayEventTicks,
            HashSet<string> eventIds,
            HashSet<int> compiledEventIds,
            HashSet<int> authoredPositions,
            out string error)
            where TEvent : FpgActivePresentationEventDefinition
        {
            for (int index = 0; index < values.Count; index++)
            {
                TEvent value = values[index];
                if (value == null)
                {
                    error =
                        $"Skill sequence '{kind}' has a missing {label} presentation event at index {index}.";
                    return false;
                }

                if (!value.TryValidate(
                        durationTicks,
                        eventId => gameplayEventTicks.TryGetValue(
                            eventId,
                            out int gameplayEventTick)
                                ? gameplayEventTick
                                : -1,
                        out error)
                    || !TryAddEventId(
                        value.EventId,
                        eventIds,
                        compiledEventIds,
                        out error)
                    || !TryAddAuthoredPosition(
                        value.Tick,
                        value.AuthoredOrdinal,
                        authoredPositions,
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error =
                            $"Skill sequence '{kind}' has an invalid {label} presentation event at index {index}.";
                    }

                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateActions<TAction>(
            TAction[] values,
            string label,
            Dictionary<string, int> gameplayEventTicks,
            HashSet<string> eventIds,
            HashSet<int> compiledEventIds,
            HashSet<int> authoredPositions,
            out string error)
            where TAction : FpgSkillGameplayActionDefinition
        {
            for (int index = 0; index < values.Length; index++)
            {
                TAction value = values[index];
                if (value == null)
                {
                    error =
                        $"Skill sequence '{kind}' has a missing {label} action at index {index}.";
                    return false;
                }

                if (!value.TryValidate(durationTicks, out error)
                    || !TryAddEventId(
                        value.EventId,
                        eventIds,
                        compiledEventIds,
                        out error)
                    || !TryAddAuthoredPosition(
                        value.Tick,
                        value.AuthoredOrdinal,
                        authoredPositions,
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error =
                            $"Skill sequence '{kind}' has an invalid {label} action at index {index}.";
                    }

                    return false;
                }

                gameplayEventTicks.Add(value.EventId, value.Tick);
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateSelfDestructBindings(out string error)
        {
            IReadOnlyList<FpgSkillSelfDestructOwnerEventDefinition>
                selfDestructs = SelfDestructOwnerEvents;
            IReadOnlyList<FpgSkillSummonEventDefinition> summons =
                SummonEvents;
            HashSet<string> boundSummonEventIds =
                new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < selfDestructs.Count; index++)
            {
                FpgSkillSelfDestructOwnerEventDefinition selfDestruct =
                    selfDestructs[index];
                string boundEventId = selfDestruct.BoundGameplayEventId;
                if (string.IsNullOrEmpty(boundEventId))
                {
                    continue;
                }

                bool foundValidSource = false;
                for (int summonIndex = 0;
                    summonIndex < summons.Count;
                    summonIndex++)
                {
                    FpgSkillSummonEventDefinition summon =
                        summons[summonIndex];
                    if (!string.Equals(
                            summon.EventId,
                            boundEventId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foundValidSource = summon.Tick == selfDestruct.Tick
                        && summon.AuthoredOrdinal
                            < selfDestruct.AuthoredOrdinal;
                    break;
                }

                if (!foundValidSource)
                {
                    error =
                        $"Self-destruct action '{selfDestruct.EventId}' must bind to a same-tick, earlier summon action.";
                    return false;
                }

                if (!boundSummonEventIds.Add(boundEventId))
                {
                    error =
                        $"Summon action '{boundEventId}' cannot drive more than one self-destruct action.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static int CompileActions<TAction>(
            TAction[] values,
            FpgSkillActionKind actionKind,
            int actionOffset,
            FpgCompiledSkillEvent[] destination,
            int writeIndex)
            where TAction : FpgSkillGameplayActionDefinition
        {
            for (int index = 0; index < values.Length; index++)
            {
                TAction value = values[index];
                destination[writeIndex++] = new FpgCompiledSkillEvent(
                    FpgSkillStableId.CompileEvent(value.EventId),
                    value.Tick,
                    actionKind,
                    checked(actionOffset + index),
                    value.AuthoredOrdinal,
                    FpgSkillStableId.CompileOptionalSocket(value.SocketId),
                    value.TargetSource,
                    value.OffsetXMillimeters,
                    value.OffsetYMillimeters,
                    value.OffsetZMillimeters,
                    FpgSkillStableId.CompileOptionalEvent(
                        value.BoundGameplayEventId));
            }

            return writeIndex;
        }

        private static bool TryAddEventId(
            string eventId,
            HashSet<string> eventIds,
            HashSet<int> compiledEventIds,
            out string error)
        {
            int compiledId = FpgSkillStableId.CompileEvent(eventId);
            if (!eventIds.Add(eventId) || !compiledEventIds.Add(compiledId))
            {
                error = $"Skill timeline repeats event ID '{eventId}' or has a stable-ID collision.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryAddAuthoredEventId(
            string eventId,
            HashSet<string> eventIds,
            out string error)
        {
            if (!eventIds.Add(eventId))
            {
                error = $"Skill timeline repeats event ID '{eventId}'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryAddAuthoredPosition(
            int tick,
            int authoredOrdinal,
            HashSet<int> positions,
            out string error)
        {
            if (!positions.Add(authoredOrdinal))
            {
                error = $"Skill sequence repeats authored ordinal {authoredOrdinal}.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    internal readonly struct FpgSkillActionIndexOffsets
    {
        public FpgSkillActionIndexOffsets(
            int attack,
            int launchProjectile,
            int commitReload,
            int summonActors,
            int selfDestructOwner)
        {
            Attack = attack;
            LaunchProjectile = launchProjectile;
            CommitReload = commitReload;
            SummonActors = summonActors;
            SelfDestructOwner = selfDestructOwner;
        }

        public int Attack { get; }
        public int LaunchProjectile { get; }
        public int CommitReload { get; }
        public int SummonActors { get; }
        public int SelfDestructOwner { get; }

        public FpgSkillActionIndexOffsets Advance(
            FpgSkillSequenceDefinition sequence)
        {
            if (sequence == null)
            {
                throw new ArgumentNullException(nameof(sequence));
            }

            return new FpgSkillActionIndexOffsets(
                checked(Attack + sequence.AttackEvents.Count),
                checked(
                    LaunchProjectile + sequence.ProjectileEvents.Count),
                checked(CommitReload + sequence.ReloadEvents.Count),
                checked(SummonActors + sequence.SummonEvents.Count),
                checked(SelfDestructOwner + sequence.SelfDestructOwnerEvents.Count));
        }
    }

    public abstract class FpgSkillTimelineDefinition : ScriptableObject
    {
        public const int CurrentAuthoringSchemaVersion = 3;

        [SerializeField, HideInInspector, Min(0)]
        private int authoringSchemaVersion;

        [SerializeField]
        private string skillId = "skill";

        [SerializeField]
        private string displayName = "Skill";

        [SerializeField, TextArea]
        private string designerNotes = string.Empty;

        [SerializeField]
        private FpgSkillSequenceDefinition[] sequences =
        {
            new FpgSkillSequenceDefinition()
        };

        public int AuthoringSchemaVersion => authoringSchemaVersion;
        public string SkillId => skillId;
        public string DisplayName => displayName;
        public string DesignerNotes => designerNotes;
        public IReadOnlyList<FpgSkillSequenceDefinition> Sequences =>
            sequences ?? Array.Empty<FpgSkillSequenceDefinition>();
        public FpgSkillAuthoringSchemaState AuthoringSchemaState =>
            FpgSkillAuthoringSchemaState.V3Only;

        protected virtual bool RequiresExecuteSequence => true;

        public virtual bool TryValidate(out string error)
        {
            if (authoringSchemaVersion != CurrentAuthoringSchemaVersion)
            {
                error =
                    $"Skill timeline requires authoring schema version {CurrentAuthoringSchemaVersion}, but found {authoringSchemaVersion}.";
                return false;
            }

            if (!FpgSkillStableId.IsValid(skillId)
                || string.IsNullOrWhiteSpace(displayName))
            {
                error = "Skill timeline requires a stable skill ID and display name.";
                return false;
            }

            FpgSkillSequenceDefinition[] values =
                sequences ?? Array.Empty<FpgSkillSequenceDefinition>();
            bool requiresExecuteSequence = RequiresExecuteSequence;
            if (values.Length == 0)
            {
                error = requiresExecuteSequence
                    ? $"Skill '{skillId}' requires an Execute sequence."
                    : $"Skill '{skillId}' requires at least one sequence.";
                return false;
            }

            HashSet<FpgSkillSequenceKind> kinds =
                new HashSet<FpgSkillSequenceKind>();
            HashSet<string> eventIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> compiledEventIds = new HashSet<int>();
            bool hasExecute = false;

            for (int index = 0; index < values.Length; index++)
            {
                FpgSkillSequenceDefinition value = values[index];
                if (value == null)
                {
                    error = $"Skill '{skillId}' has a missing sequence at index {index}.";
                    return false;
                }

                if (!kinds.Add(value.Kind))
                {
                    error = $"Skill '{skillId}' repeats sequence kind '{value.Kind}'.";
                    return false;
                }

                if (!value.TryValidate(
                        eventIds,
                        compiledEventIds,
                        out error))
                {
                    error = $"Skill '{skillId}' sequence {index} is invalid: {error}";
                    return false;
                }

                hasExecute |= value.Kind == FpgSkillSequenceKind.Execute;
            }

            if (requiresExecuteSequence && !hasExecute)
            {
                error = $"Skill '{skillId}' requires an Execute sequence.";
                return false;
            }

            if (!TryValidateDefinition(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryCompile(
            out FpgCompiledSkillDefinition definition,
            out string error)
        {
            definition = null;
            if (!TryValidate(out error))
            {
                return false;
            }

            try
            {
                FpgSkillSequenceDefinition[] values =
                    sequences ?? Array.Empty<FpgSkillSequenceDefinition>();
                FpgCompiledSkillSequence[] compiled =
                    new FpgCompiledSkillSequence[values.Length];
                FpgSkillActionIndexOffsets actionOffsets =
                    default(FpgSkillActionIndexOffsets);
                for (int index = 0; index < values.Length; index++)
                {
                    compiled[index] = values[index].Compile(
                        skillId,
                        actionOffsets);
                    actionOffsets = actionOffsets.Advance(values[index]);
                }

                definition = new FpgCompiledSkillDefinition(
                    FpgSkillStableId.CompileSkill(skillId),
                    compiled,
                    RequiresExecuteSequence);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException
                || exception is OverflowException)
            {
                definition = null;
                error = exception.Message;
                return false;
            }
        }

        protected virtual bool TryValidateDefinition(out string error)
        {
            error = string.Empty;
            return true;
        }
    }

    internal static class FpgSkillStableId
    {
        private const ulong SkillDomain = 0x4650475F534B494CUL;
        private const ulong EventDomain = 0x4650475F45564E54UL;
        private const ulong WarningDomain = 0x4650475F5741524EUL;
        private const ulong SocketDomain = 0x4650475F534F434BUL;
        private const ulong AnimationDomain = 0x4650475F414E494DUL;
        private const ulong PresentationDomain = 0x4650475F50524553UL;
        private const ulong PresentationTrackDomain =
            0x4650475F5054524BUL;

        public static bool IsValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool valid = character >= 'a' && character <= 'z'
                    || character >= 'A' && character <= 'Z'
                    || character >= '0' && character <= '9'
                    || character == '-'
                    || character == '_'
                    || character == '.'
                    || character == ':'
                    || character == '/';
                if (!valid)
                {
                    return false;
                }
            }

            return true;
        }

        public static int CompileSkill(string value)
        {
            return Compile(value, SkillDomain);
        }

        public static int CompileEvent(string value)
        {
            return Compile(value, EventDomain);
        }

        public static int CompileOptionalEvent(string value)
        {
            return string.IsNullOrEmpty(value) ? 0 : Compile(value, EventDomain);
        }

        public static int CompileWarning(string value)
        {
            return Compile(value, WarningDomain);
        }

        public static int CompileOptionalSocket(string value)
        {
            return string.IsNullOrEmpty(value) ? 0 : Compile(value, SocketDomain);
        }

        public static int CompileAnimation(string value)
        {
            return Compile(value, AnimationDomain);
        }

        public static FpgPresentationHandle CompilePresentationHandle(
            string value)
        {
            return new FpgPresentationHandle(
                Compile(value, PresentationDomain));
        }

        public static int CompilePresentationTrack(string value)
        {
            return Compile(value, PresentationTrackDomain);
        }

        private static int Compile(string value, ulong domain)
        {
            ulong hash = StableHash.Mix(domain);
            for (int index = 0; index < value.Length; index++)
            {
                hash = StableHash.Append(hash, value[index]);
            }

            int result = unchecked((int)((hash ^ (hash >> 32)) & 0x7FFFFFFFUL));
            return result == 0 ? 1 : result;
        }
    }
}
