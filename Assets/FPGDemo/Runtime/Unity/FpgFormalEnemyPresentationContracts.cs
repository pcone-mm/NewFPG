using System;
using FPG.Demo.Skills;

namespace FPG.Demo.Unity
{
    public readonly struct FpgResolvedEnemySkillWarning
    {
        public FpgResolvedEnemySkillWarning(
            string eventName,
            string warningName,
            string socketName)
        {
            EventName = eventName ?? string.Empty;
            WarningName = warningName ?? string.Empty;
            SocketName = socketName ?? string.Empty;
        }

        public string EventName { get; }
        public string WarningName { get; }
        public string SocketName { get; }
    }

    public static class FpgEnemySkillPresentationResolver
    {
        public static bool TryResolveAnimationName(
            FpgEnemyAttackDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            int animationId,
            out string animationName)
        {
            animationName = string.Empty;
            if (definition == null || animationId <= 0)
            {
                return false;
            }

            for (int sequenceIndex = 0;
                sequenceIndex < definition.Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence =
                    definition.Sequences[sequenceIndex];
                if (sequence == null || sequence.Kind != sequenceKind)
                {
                    continue;
                }

                if (FpgSkillStableId.CompileAnimation(
                        sequence.MainAnimation)
                    == animationId)
                {
                    animationName = sequence.MainAnimation;
                    return true;
                }

                for (int variantIndex = 0;
                    variantIndex < sequence.AlternateAnimations.Count;
                    variantIndex++)
                {
                    string variant =
                        sequence.AlternateAnimations[variantIndex];
                    if (FpgSkillStableId.CompileAnimation(variant)
                        == animationId)
                    {
                        animationName = variant;
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        public static bool TryResolveWarning(
            FpgEnemyAttackDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            in FpgCompiledSkillEvent compiledWarning,
            out FpgResolvedEnemySkillWarning resolved)
        {
            resolved = default(FpgResolvedEnemySkillWarning);
            if (definition == null
                || (compiledWarning.Kind
                        != FpgSkillEventKind.WarningStarted
                    && compiledWarning.Kind
                        != FpgSkillEventKind.WarningEnded))
            {
                return false;
            }

            if (!TryGetSequence(
                    definition,
                    sequenceKind,
                    out FpgSkillSequenceDefinition sequence))
            {
                return false;
            }

            for (int index = 0; index < sequence.Warnings.Count; index++)
            {
                FpgSkillWarningDefinition warning =
                    sequence.Warnings[index];
                if (warning == null)
                {
                    continue;
                }

                bool started = compiledWarning.Kind
                    == FpgSkillEventKind.WarningStarted;
                string eventName = started
                    ? warning.StartEventId
                    : warning.EndEventId;
                int eventTick = started
                    ? warning.StartTick
                    : warning.EndTick;
                if (warning != null
                    && eventTick == compiledWarning.Tick
                    && FpgSkillStableId.CompileEvent(eventName)
                        == compiledWarning.EventId
                    && FpgSkillStableId.CompileWarning(
                        warning.WarningId)
                        == compiledWarning.WarningId
                    && FpgSkillStableId.CompileOptionalSocket(
                        warning.SocketId)
                        == compiledWarning.SocketId)
                {
                    resolved = new FpgResolvedEnemySkillWarning(
                        eventName,
                        warning.WarningId,
                        warning.SocketId);
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetSequence(
            FpgEnemyAttackDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            out FpgSkillSequenceDefinition sequence)
        {
            for (int index = 0;
                index < definition.Sequences.Count;
                index++)
            {
                FpgSkillSequenceDefinition candidate =
                    definition.Sequences[index];
                if (candidate != null
                    && candidate.Kind == sequenceKind)
                {
                    sequence = candidate;
                    return true;
                }
            }

            sequence = null;
            return false;
        }
    }
    public readonly struct FpgFormalEnemySkillWarningPresentationEvent
    {
        internal FpgFormalEnemySkillWarningPresentationEvent(
            in FpgFormalEnemySkillTimelineEvent timelineEvent,
            in FpgResolvedEnemySkillWarning resolved,
            bool isActive)
        {
            TimelineEvent = timelineEvent;
            Resolved = resolved;
            IsActive = isActive;
        }

        public FpgFormalEnemySkillTimelineEvent TimelineEvent { get; }
        public FpgResolvedEnemySkillWarning Resolved { get; }
        public bool IsActive { get; }
    }
}
