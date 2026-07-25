using System;
using System.Collections.Generic;
using FPG.Demo.Core;
using FPG.Demo.Skills;
using UnityEngine;
using UnityEngine.Serialization;

namespace FPG.Demo.Unity
{
    [Serializable]
    public sealed class FpgSkillPhaseDefinition
    {
        [SerializeField]
        private string phaseId = "phase";

        [SerializeField]
        private FpgSkillPhaseKind kind = FpgSkillPhaseKind.Active;

        [SerializeField, Min(0)]
        private int startTick;

        [SerializeField, Min(0)]
        private int endTick;

        public string PhaseId => phaseId;
        public FpgSkillPhaseKind Kind => kind;
        public int StartTick => startTick;
        public int EndTick => endTick;

        internal bool TryValidate(int durationTicks, out string error)
        {
            if (!FpgSkillStableId.IsValid(phaseId))
            {
                error = "Skill phase requires a stable phase ID.";
                return false;
            }

            if (!Enum.IsDefined(typeof(FpgSkillPhaseKind), kind)
                || kind == FpgSkillPhaseKind.None
                || startTick < 0
                || endTick < startTick
                || endTick > durationTicks)
            {
                error = $"Skill phase '{phaseId}' has an invalid kind or tick range.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class FpgSkillLogicEventDefinition
    {
        [SerializeField]
        private string eventId = "payload";

        [SerializeField, Min(0)]
        private int tick;

        [SerializeField]
        private string payloadSlotId = "payload.default";

        [FormerlySerializedAs("sortOrder")]
        [SerializeField, Min(0)]
        private int authoredOrdinal;

        [SerializeField]
        private string socketId = string.Empty;

        [SerializeField]
        private FpgSkillTargetSource targetSource =
            FpgSkillTargetSource.CurrentAim;

        [SerializeField]
        private Vector3 targetOffset;

        public string EventId => eventId;
        public int Tick => tick;
        public string PayloadSlotId => payloadSlotId;
        public int AuthoredOrdinal => authoredOrdinal;
        public string SocketId => socketId;
        public FpgSkillTargetSource TargetSource => targetSource;
        public Vector3 TargetOffset => targetOffset;
        internal int OffsetXMillimeters =>
            Mathf.RoundToInt(targetOffset.x * 1000f);
        internal int OffsetYMillimeters =>
            Mathf.RoundToInt(targetOffset.y * 1000f);
        internal int OffsetZMillimeters =>
            Mathf.RoundToInt(targetOffset.z * 1000f);

        internal bool TryValidate(
            int durationTicks,
            Func<string, bool> containsPayloadSlot,
            out string error)
        {
            if (!FpgSkillStableId.IsValid(eventId)
                || !FpgSkillStableId.IsValid(payloadSlotId))
            {
                error = "Skill logic event requires stable event and payload slot IDs.";
                return false;
            }

            if (tick < 0 || tick > durationTicks || authoredOrdinal < 0)
            {
                error = $"Skill logic event '{eventId}' has an invalid tick or authored ordinal.";
                return false;
            }

            if (!string.IsNullOrEmpty(socketId)
                && !FpgSkillStableId.IsValid(socketId))
            {
                error = $"Skill logic event '{eventId}' has an invalid socket ID.";
                return false;
            }

            if (!Enum.IsDefined(typeof(FpgSkillTargetSource), targetSource)
                || targetSource == FpgSkillTargetSource.None
                || !IsFinite(targetOffset)
                || Mathf.Abs(targetOffset.x) > 2147483f
                || Mathf.Abs(targetOffset.y) > 2147483f
                || Mathf.Abs(targetOffset.z) > 2147483f)
            {
                error = $"Skill logic event '{eventId}' has an invalid target source or offset.";
                return false;
            }

            if (containsPayloadSlot == null || !containsPayloadSlot(payloadSlotId))
            {
                error = $"Skill logic event '{eventId}' references missing payload slot '{payloadSlotId}'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }

    [Serializable]
    public sealed class FpgSkillPresentationCueDefinition
    {
        [SerializeField]
        private string eventId = "cue";

        [SerializeField, Min(0)]
        private int tick;

        [SerializeField]
        private string cueId = "cue.default";

        [FormerlySerializedAs("sortOrder")]
        [SerializeField, Min(0)]
        private int authoredOrdinal;

        [SerializeField]
        private string socketId = string.Empty;

        [SerializeField]
        private string bindGameplayEventId = string.Empty;

        public string EventId => eventId;
        public int Tick => tick;
        public string CueId => cueId;
        public int AuthoredOrdinal => authoredOrdinal;
        public string SocketId => socketId;
        public string BindGameplayEventId => bindGameplayEventId;

        internal bool TryValidate(
            int durationTicks,
            Func<string, int> resolveLogicEventTick,
            out string error)
        {
            if (!FpgSkillStableId.IsValid(eventId)
                || !FpgSkillStableId.IsValid(cueId))
            {
                error = "Skill presentation cue requires stable event and cue IDs.";
                return false;
            }

            if (tick < 0 || tick > durationTicks || authoredOrdinal < 0)
            {
                error = $"Skill presentation cue '{eventId}' has an invalid tick or authored ordinal.";
                return false;
            }

            if (!string.IsNullOrEmpty(socketId)
                && !FpgSkillStableId.IsValid(socketId))
            {
                error = $"Skill presentation cue '{eventId}' has an invalid socket ID.";
                return false;
            }

            if (!string.IsNullOrEmpty(bindGameplayEventId))
            {
                if (!FpgSkillStableId.IsValid(bindGameplayEventId)
                    || resolveLogicEventTick == null)
                {
                    error = $"Skill presentation cue '{eventId}' references missing gameplay event '{bindGameplayEventId}'.";
                    return false;
                }

                int gameplayTick =
                    resolveLogicEventTick(bindGameplayEventId);
                if (gameplayTick < 0)
                {
                    error = $"Skill presentation cue '{eventId}' references missing gameplay event '{bindGameplayEventId}'.";
                    return false;
                }

                if (gameplayTick != tick)
                {
                    error = $"Skill presentation cue '{eventId}' must bind gameplay event '{bindGameplayEventId}' on the same Tick (cue Tick {tick}, gameplay Tick {gameplayTick}).";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

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
        private FpgSkillAnimationPlaybackMode animationPlaybackMode =
            FpgSkillAnimationPlaybackMode.NaturalSpeed;

        [SerializeField, Min(0)]
        private int animationStartTick;

        [SerializeField, Min(0)]
        private int animationEndTick;

        [SerializeField, Min(0)]
        private int sourceAnimationDurationTicks;

        [SerializeField]
        private FpgSkillPhaseDefinition[] phases = Array.Empty<FpgSkillPhaseDefinition>();

        [SerializeField]
        private FpgSkillLogicEventDefinition[] logicEvents =
            Array.Empty<FpgSkillLogicEventDefinition>();

        [SerializeField]
        private FpgSkillPresentationCueDefinition[] presentationCues =
            Array.Empty<FpgSkillPresentationCueDefinition>();

        [SerializeField]
        private FpgSkillWarningDefinition[] warnings =
            Array.Empty<FpgSkillWarningDefinition>();

        public FpgSkillSequenceKind Kind => kind;
        public int DurationTicks => durationTicks;
        public IReadOnlyList<string> AlternateAnimations =>
            alternateAnimations ?? Array.Empty<string>();
        public string MainAnimation => mainAnimation;
        public bool Loop => loop;
        public FpgSkillAnimationPlaybackMode AnimationPlaybackMode =>
            animationPlaybackMode;
        public int AnimationStartTick => animationStartTick;
        public int AnimationEndTick => ResolvedAnimationEndTick;
        public int SourceAnimationDurationTicks => sourceAnimationDurationTicks;

        private int ResolvedAnimationEndTick =>
            animationEndTick == 0 && durationTicks > 0
                ? durationTicks
                : animationEndTick;

        public IReadOnlyList<FpgSkillPhaseDefinition> Phases =>
            phases ?? Array.Empty<FpgSkillPhaseDefinition>();
        public IReadOnlyList<FpgSkillLogicEventDefinition> LogicEvents =>
            logicEvents ?? Array.Empty<FpgSkillLogicEventDefinition>();
        public IReadOnlyList<FpgSkillPresentationCueDefinition> PresentationCues =>
            presentationCues ?? Array.Empty<FpgSkillPresentationCueDefinition>();
        public IReadOnlyList<FpgSkillWarningDefinition> Warnings =>
            warnings ?? Array.Empty<FpgSkillWarningDefinition>();

        internal bool TryValidate(
            Func<string, bool> containsPayloadSlot,
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

            if (!TryValidatePhases(out error))
            {
                return false;
            }

            HashSet<ulong> authoredPositions = new HashSet<ulong>();
            Dictionary<string, int> logicEventTicks =
                new Dictionary<string, int>(StringComparer.Ordinal);

            FpgSkillLogicEventDefinition[] logic =
                logicEvents ?? Array.Empty<FpgSkillLogicEventDefinition>();
            for (int index = 0; index < logic.Length; index++)
            {
                FpgSkillLogicEventDefinition value = logic[index];
                if (value == null)
                {
                    error = $"Skill sequence '{kind}' has a missing logic event at index {index}.";
                    return false;
                }

                if (!value.TryValidate(durationTicks, containsPayloadSlot, out error)
                    || !TryAddEventId(value.EventId, eventIds, compiledEventIds, out error)
                    || !TryAddAuthoredPosition(
                        value.Tick,
                        value.AuthoredOrdinal,
                        authoredPositions,
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = $"Skill sequence '{kind}' has an invalid logic event at index {index}.";
                    }

                    return false;
                }

                logicEventTicks.Add(value.EventId, value.Tick);
            }

            FpgSkillPresentationCueDefinition[] cues =
                presentationCues ?? Array.Empty<FpgSkillPresentationCueDefinition>();
            for (int index = 0; index < cues.Length; index++)
            {
                FpgSkillPresentationCueDefinition value = cues[index];
                if (value == null)
                {
                    error = $"Skill sequence '{kind}' has a missing presentation cue at index {index}.";
                    return false;
                }

                if (!value.TryValidate(
                        durationTicks,
                        logicEventId => logicEventTicks.TryGetValue(
                            logicEventId,
                            out int logicEventTick)
                                ? logicEventTick
                                : -1,
                        out error)
                    || !TryAddEventId(value.EventId, eventIds, compiledEventIds, out error)
                    || !TryAddAuthoredPosition(
                        value.Tick,
                        value.AuthoredOrdinal,
                        authoredPositions,
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = $"Skill sequence '{kind}' has an invalid presentation cue at index {index}.";
                    }

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

        internal FpgCompiledSkillSequence Compile()
        {
            FpgSkillLogicEventDefinition[] logic =
                logicEvents ?? Array.Empty<FpgSkillLogicEventDefinition>();
            FpgSkillPresentationCueDefinition[] cues =
                presentationCues ?? Array.Empty<FpgSkillPresentationCueDefinition>();
            FpgSkillWarningDefinition[] warningValues =
                warnings ?? Array.Empty<FpgSkillWarningDefinition>();
            int eventCount = checked(logic.Length + cues.Length + warningValues.Length * 2);
            FpgCompiledSkillEvent[] compiled = new FpgCompiledSkillEvent[eventCount];
            int writeIndex = 0;

            for (int index = 0; index < logic.Length; index++)
            {
                FpgSkillLogicEventDefinition value = logic[index];
                compiled[writeIndex++] = new FpgCompiledSkillEvent(
                    FpgSkillStableId.CompileEvent(value.EventId),
                    value.Tick,
                    FpgSkillEventKind.GameplayPayload,
                    FpgSkillStableId.CompilePayloadSlot(value.PayloadSlotId),
                    0,
                    0,
                    value.AuthoredOrdinal,
                    FpgSkillStableId.CompileOptionalSocket(value.SocketId),
                    value.TargetSource,
                    value.OffsetXMillimeters,
                    value.OffsetYMillimeters,
                    value.OffsetZMillimeters);
            }

            for (int index = 0; index < cues.Length; index++)
            {
                FpgSkillPresentationCueDefinition value = cues[index];
                compiled[writeIndex++] = new FpgCompiledSkillEvent(
                    FpgSkillStableId.CompileEvent(value.EventId),
                    value.Tick,
                    FpgSkillEventKind.PresentationCue,
                    0,
                    FpgSkillStableId.CompileCue(value.CueId),
                    0,
                    value.AuthoredOrdinal,
                    FpgSkillStableId.CompileOptionalSocket(value.SocketId),
                    FpgSkillTargetSource.CurrentAim,
                    0,
                    0,
                    0,
                    FpgSkillStableId.CompileOptionalEvent(
                        value.BindGameplayEventId));
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
                    0,
                    0,
                    warningId,
                    value.AuthoredOrdinal,
                    socketId);
                compiled[writeIndex++] = new FpgCompiledSkillEvent(
                    FpgSkillStableId.CompileEvent(value.EndEventId),
                    value.EndTick,
                    FpgSkillEventKind.WarningEnded,
                    0,
                    0,
                    warningId,
                    value.AuthoredOrdinal + 1,
                    socketId);
            }

            FpgSkillPhaseDefinition[] authoredPhases =
                phases ?? Array.Empty<FpgSkillPhaseDefinition>();
            FpgCompiledSkillPhase[] compiledPhases =
                new FpgCompiledSkillPhase[authoredPhases.Length];
            for (int index = 0; index < authoredPhases.Length; index++)
            {
                FpgSkillPhaseDefinition value = authoredPhases[index];
                compiledPhases[index] = new FpgCompiledSkillPhase(
                    FpgSkillStableId.CompilePhase(value.PhaseId),
                    value.Kind,
                    value.StartTick,
                    value.EndTick);
            }

            string[] authoredVariants =
                alternateAnimations ?? Array.Empty<string>();
            int[] compiledVariants = new int[authoredVariants.Length];
            for (int index = 0; index < authoredVariants.Length; index++)
            {
                compiledVariants[index] =
                    FpgSkillStableId.CompileAnimation(authoredVariants[index]);
            }

            return new FpgCompiledSkillSequence(
                kind,
                durationTicks,
                FpgSkillStableId.CompileAnimation(mainAnimation),
                loop,
                animationPlaybackMode,
                animationStartTick,
                ResolvedAnimationEndTick,
                compiledVariants,
                compiledPhases,
                compiled);
        }

        private bool TryValidatePhases(out string error)
        {
            FpgSkillPhaseDefinition[] values =
                phases ?? Array.Empty<FpgSkillPhaseDefinition>();
            HashSet<string> phaseIds =
                new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> compiledPhaseIds = new HashSet<int>();
            HashSet<FpgSkillPhaseKind> kinds =
                new HashSet<FpgSkillPhaseKind>();
            FpgSkillPhaseDefinition previous = null;

            for (int index = 0; index < values.Length; index++)
            {
                FpgSkillPhaseDefinition value = values[index];
                if (value == null)
                {
                    error = $"Skill sequence '{kind}' has a missing phase at index {index}.";
                    return false;
                }

                if (!value.TryValidate(durationTicks, out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = $"Skill sequence '{kind}' has an invalid phase at index {index}.";
                    }

                    return false;
                }

                if (!phaseIds.Add(value.PhaseId))
                {
                    error =
                        $"Skill sequence '{kind}' repeats phase ID '{value.PhaseId}'.";
                    return false;
                }

                int compiledPhaseId =
                    FpgSkillStableId.CompilePhase(value.PhaseId);
                if (!compiledPhaseIds.Add(compiledPhaseId))
                {
                    error =
                        $"Skill sequence '{kind}' phase ID '{value.PhaseId}' has a stable-ID collision.";
                    return false;
                }

                if (!kinds.Add(value.Kind))
                {
                    error =
                        $"Skill sequence '{kind}' repeats phase kind '{value.Kind}'.";
                    return false;
                }

                if (previous != null
                    && (value.StartTick < previous.EndTick
                        || value.Kind <= previous.Kind))
                {
                    error = $"Skill sequence '{kind}' phases must be ordered and non-overlapping.";
                    return false;
                }

                previous = value;
            }

            error = string.Empty;
            return true;
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
            HashSet<ulong> positions,
            out string error)
        {
            ulong key = unchecked(
                ((ulong)(uint)tick << 32) | (uint)authoredOrdinal);
            if (!positions.Add(key))
            {
                error = $"Skill sequence repeats authored ordinal {authoredOrdinal} on tick {tick}.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    public abstract class FpgSkillTimelineDefinition : ScriptableObject
    {
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

        public string SkillId => skillId;
        public string DisplayName => displayName;
        public string DesignerNotes => designerNotes;
        public IReadOnlyList<FpgSkillSequenceDefinition> Sequences =>
            sequences ?? Array.Empty<FpgSkillSequenceDefinition>();

        public virtual bool TryValidate(out string error)
        {
            if (!FpgSkillStableId.IsValid(skillId)
                || string.IsNullOrWhiteSpace(displayName))
            {
                error = "Skill timeline requires a stable skill ID and display name.";
                return false;
            }

            if (!TryValidatePayloadSlots(out error))
            {
                return false;
            }

            FpgSkillSequenceDefinition[] values =
                sequences ?? Array.Empty<FpgSkillSequenceDefinition>();
            if (values.Length == 0)
            {
                error = $"Skill '{skillId}' requires an Execute sequence.";
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
                        ContainsPayloadSlot,
                        eventIds,
                        compiledEventIds,
                        out error))
                {
                    error = $"Skill '{skillId}' sequence {index} is invalid: {error}";
                    return false;
                }

                hasExecute |= value.Kind == FpgSkillSequenceKind.Execute;
            }

            if (!hasExecute)
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
                for (int index = 0; index < values.Length; index++)
                {
                    compiled[index] = values[index].Compile();
                }

                definition = new FpgCompiledSkillDefinition(
                    FpgSkillStableId.CompileSkill(skillId),
                    compiled);
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

        protected abstract bool TryValidatePayloadSlots(out string error);
        protected abstract bool ContainsPayloadSlot(string payloadSlotId);

        protected virtual bool TryValidateDefinition(out string error)
        {
            error = string.Empty;
            return true;
        }
    }

    internal static class FpgSkillStableId
    {
        private const ulong SkillDomain = 0x4650475F534B494CUL;
        private const ulong PhaseDomain = 0x4650475F50484153UL;
        private const ulong EventDomain = 0x4650475F45564E54UL;
        private const ulong PayloadDomain = 0x4650475F5041594CUL;
        private const ulong CueDomain = 0x4650475F4355455FUL;
        private const ulong WarningDomain = 0x4650475F5741524EUL;
        private const ulong SocketDomain = 0x4650475F534F434BUL;
        private const ulong AnimationDomain = 0x4650475F414E494DUL;

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

        public static int CompilePhase(string value)
        {
            return Compile(value, PhaseDomain);
        }

        public static int CompileEvent(string value)
        {
            return Compile(value, EventDomain);
        }

        public static int CompileOptionalEvent(string value)
        {
            return string.IsNullOrEmpty(value) ? 0 : Compile(value, EventDomain);
        }

        public static int CompilePayloadSlot(string value)
        {
            return Compile(value, PayloadDomain);
        }

        public static int CompileCue(string value)
        {
            return Compile(value, CueDomain);
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
