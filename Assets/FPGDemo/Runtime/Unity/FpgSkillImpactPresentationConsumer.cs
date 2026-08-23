using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Bounded presentation-only consumer for accepted skill contacts.
    /// </summary>
    public sealed class FpgSkillImpactPresentationConsumer
    {
        private IFpgSkillImpactPresentationView feed;
        private FpgSkillPresentationWorld world;
        private FpgSkillImpactPresentationEvent[] eventBuffer =
            Array.Empty<FpgSkillImpactPresentationEvent>();
        private Binding[] bindings = Array.Empty<Binding>();
        private GroupState[] groups = Array.Empty<GroupState>();
        private long cursor;

        public int GapCount { get; private set; }
        public int FaultCount { get; private set; }
        public IFpgSkillImpactPresentationView ObservedFeed => feed;

        public bool TryPrepare(
            IFpgSkillImpactPresentationView nextFeed,
            FpgSkillPresentationWorld nextWorld,
            int bindingCapacity,
            out string error)
        {
            if (nextFeed == null || nextWorld == null
                || !nextWorld.IsPrepared || nextFeed.Capacity <= 0
                || bindingCapacity <= 0)
            {
                error =
                    "Skill impact presenter requires a feed, prepared world and positive capacity.";
                return false;
            }

            feed = nextFeed;
            world = nextWorld;
            eventBuffer = new FpgSkillImpactPresentationEvent[
                nextFeed.Capacity];
            bindings = new Binding[bindingCapacity];
            groups = new GroupState[bindingCapacity];
            cursor = nextFeed.LastSequence;
            GapCount = 0;
            FaultCount = 0;
            error = string.Empty;
            return true;
        }

        public bool TryRegister(
            in FpgSkillImpactCorrelation correlation,
            FpgSkillImpactPresentationGroupKind groupKind,
            in FpgCompiledImpactPresentation presentation)
        {
            if (!correlation.IsValid || !presentation.HasAny
                || !Enum.IsDefined(
                    typeof(FpgSkillImpactPresentationGroupKind),
                    groupKind))
            {
                return false;
            }

            int free = -1;
            for (int index = 0; index < bindings.Length; index++)
            {
                if (!bindings[index].Correlation.IsValid)
                {
                    if (free < 0)
                    {
                        free = index;
                    }

                    continue;
                }

                if (bindings[index].Correlation == correlation
                    && bindings[index].GroupKind == groupKind)
                {
                    bindings[index] = new Binding(
                        correlation,
                        groupKind,
                        presentation);
                    return true;
                }
            }

            if (free < 0)
            {
                FaultCount++;
                return false;
            }

            bindings[free] = new Binding(
                correlation,
                groupKind,
                presentation);
            return true;
        }

        public void Consume()
        {
            if (feed == null || world == null)
            {
                return;
            }

            if (feed.LastSequence < cursor)
            {
                cursor = feed.LastSequence;
                ClearCorrelations();
            }

            int count;
            bool hasGap;
            try
            {
                count = feed.CopyAfter(cursor, eventBuffer, out hasGap);
            }
            catch (Exception)
            {
                FaultCount++;
                cursor = feed.LastSequence;
                ClearCorrelations();
                return;
            }

            if (hasGap)
            {
                GapCount++;
                cursor = feed.LastSequence;
                ClearCorrelations();
                Array.Clear(eventBuffer, 0, count);
                return;
            }

            for (int index = 0; index < count; index++)
            {
                FpgSkillImpactPresentationEvent presentationEvent =
                    eventBuffer[index];
                eventBuffer[index] =
                    default(FpgSkillImpactPresentationEvent);
                cursor = Math.Max(cursor, presentationEvent.Sequence);
                if (presentationEvent.Type
                    == FpgSkillImpactPresentationEventType.Contact)
                {
                    ConsumeContact(presentationEvent.Contact);
                }
                else
                {
                    ConsumeCompletion(presentationEvent.Completion);
                }
            }
        }

        public void Clear()
        {
            if (eventBuffer.Length > 0)
            {
                Array.Clear(eventBuffer, 0, eventBuffer.Length);
            }

            if (bindings.Length > 0)
            {
                Array.Clear(bindings, 0, bindings.Length);
                Array.Clear(groups, 0, groups.Length);
            }

            cursor = feed == null ? 0L : feed.LastSequence;
        }

        private void ConsumeContact(in FpgSkillImpactContact contact)
        {
            int bindingIndex = FindBinding(
                contact.Correlation,
                contact.GroupKind);
            if (bindingIndex < 0)
            {
                FaultCount++;
                return;
            }

            bool weakpoint = contact.HitPart == HitPart.Weakpoint;
            if (!world.TryPresentImpactVfx(
                bindings[bindingIndex].Presentation,
                weakpoint,
                ToWorldPosition(contact.ContactPoint)))
            {
                FaultCount++;
            }

            FpgCompiledImpactPresentation presentation =
                bindings[bindingIndex].Presentation;
            if (contact.ContactKind
                    == FpgSkillImpactContactKind.Intercepted
                && presentation.InterceptionAudioOverride.IsValid)
            {
                if (!world.TryPresentAudioAt(
                    presentation.InterceptionAudioOverride,
                    ToWorldPosition(contact.ContactPoint)))
                {
                    FaultCount++;
                }

                return;
            }

            int groupIndex = FindOrCreateGroup(
                contact.Correlation,
                contact.GroupKind);
            if (groupIndex < 0)
            {
                FaultCount++;
                return;
            }

            GroupState state = groups[groupIndex];
            state.HasContact = true;
            state.AnyWeakpoint |= weakpoint;
            if (contact.ContactKind
                == FpgSkillImpactContactKind.EnvironmentBlocked)
            {
                state.LastEnvironmentContactPoint = contact.ContactPoint;
            }
            else
            {
                state.HasNonEnvironmentContact = true;
                state.LastNonEnvironmentContactPoint = contact.ContactPoint;
            }
            groups[groupIndex] = state;
        }

        private void ConsumeCompletion(
            in FpgSkillImpactGroupCompletion completion)
        {
            int bindingIndex = FindBinding(
                completion.Correlation,
                completion.GroupKind);
            int groupIndex = FindGroup(
                completion.Correlation,
                completion.GroupKind);
            if (bindingIndex >= 0 && groupIndex >= 0
                && groups[groupIndex].HasContact
                && !world.TryPresentImpactGroup(
                    bindings[bindingIndex].Presentation,
                    groups[groupIndex].AnyWeakpoint,
                    !groups[groupIndex].HasNonEnvironmentContact,
                    ToWorldPosition(
                        groups[groupIndex].HasNonEnvironmentContact
                            ? groups[groupIndex]
                                .LastNonEnvironmentContactPoint
                            : groups[groupIndex]
                                .LastEnvironmentContactPoint)))
            {
                FaultCount++;
            }

            if (bindingIndex >= 0)
            {
                bindings[bindingIndex] = default(Binding);
            }

            if (groupIndex >= 0)
            {
                groups[groupIndex] = default(GroupState);
            }
        }

        private int FindBinding(
            in FpgSkillImpactCorrelation correlation,
            FpgSkillImpactPresentationGroupKind groupKind)
        {
            for (int index = 0; index < bindings.Length; index++)
            {
                if (bindings[index].Correlation == correlation
                    && bindings[index].GroupKind == groupKind)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindOrCreateGroup(
            in FpgSkillImpactCorrelation correlation,
            FpgSkillImpactPresentationGroupKind groupKind)
        {
            int existing = FindGroup(correlation, groupKind);
            if (existing >= 0)
            {
                return existing;
            }

            for (int index = 0; index < groups.Length; index++)
            {
                if (!groups[index].Correlation.IsValid)
                {
                    groups[index] = new GroupState(
                        correlation,
                        groupKind);
                    return index;
                }
            }

            return -1;
        }

        private int FindGroup(
            in FpgSkillImpactCorrelation correlation,
            FpgSkillImpactPresentationGroupKind groupKind)
        {
            for (int index = 0; index < groups.Length; index++)
            {
                if (groups[index].Correlation == correlation
                    && groups[index].GroupKind == groupKind)
                {
                    return index;
                }
            }

            return -1;
        }

        private void ClearGroups()
        {
            if (groups.Length > 0)
            {
                Array.Clear(groups, 0, groups.Length);
            }
        }

        private void ClearCorrelations()
        {
            if (bindings.Length > 0)
            {
                Array.Clear(bindings, 0, bindings.Length);
            }

            ClearGroups();
        }

        private static Vector3 ToWorldPosition(SpatialVectorKey value)
        {
            float scale = 1f / SpatialContract.PositionUnitsPerMeter;
            return new Vector3(
                value.X * scale,
                value.Y * scale,
                value.Z * scale);
        }

        private readonly struct Binding
        {
            public Binding(
                FpgSkillImpactCorrelation correlation,
                FpgSkillImpactPresentationGroupKind groupKind,
                FpgCompiledImpactPresentation presentation)
            {
                Correlation = correlation;
                GroupKind = groupKind;
                Presentation = presentation;
            }

            public FpgSkillImpactCorrelation Correlation { get; }
            public FpgSkillImpactPresentationGroupKind GroupKind { get; }
            public FpgCompiledImpactPresentation Presentation { get; }
        }

        private struct GroupState
        {
            public GroupState(
                FpgSkillImpactCorrelation correlation,
                FpgSkillImpactPresentationGroupKind groupKind)
            {
                Correlation = correlation;
                GroupKind = groupKind;
                HasContact = false;
                AnyWeakpoint = false;
                HasNonEnvironmentContact = false;
                LastEnvironmentContactPoint = default(SpatialVectorKey);
                LastNonEnvironmentContactPoint =
                    default(SpatialVectorKey);
            }

            public FpgSkillImpactCorrelation Correlation;
            public FpgSkillImpactPresentationGroupKind GroupKind;
            public bool HasContact;
            public bool AnyWeakpoint;
            public bool HasNonEnvironmentContact;
            public SpatialVectorKey LastEnvironmentContactPoint;
            public SpatialVectorKey LastNonEnvironmentContactPoint;
        }
    }
}
