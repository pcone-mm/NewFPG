using System;
using System.Collections.Generic;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal readonly struct FpgSkillEventKey :
        IEquatable<FpgSkillEventKey>,
        IComparable<FpgSkillEventKey>
    {
        private readonly int storedLocalIndex;
        private readonly int storedPresentationTrackIndex;

        public FpgSkillEventKey(
            FpgSkillEventTrackKind track,
            FpgSkillActionKind actionKind,
            int localIndex)
            : this(track, actionKind, -1, localIndex)
        {
        }

        public FpgSkillEventKey(
            FpgSkillEventTrackKind track,
            FpgSkillActionKind actionKind,
            int presentationTrackIndex,
            int localIndex)
        {
            if (localIndex < 0
                || localIndex == int.MaxValue
                || presentationTrackIndex == int.MaxValue)
            {
                Track = default;
                ActionKind = default;
                storedLocalIndex = 0;
                storedPresentationTrackIndex = 0;
                return;
            }

            Track = track;
            ActionKind = actionKind;
            storedLocalIndex = localIndex + 1;
            storedPresentationTrackIndex = presentationTrackIndex + 1;
        }

        public static FpgSkillEventKey Invalid => default;
        public FpgSkillEventTrackKind Track { get; }
        public FpgSkillActionKind ActionKind { get; }
        public int PresentationTrackIndex =>
            storedPresentationTrackIndex - 1;
        public int LocalIndex => storedLocalIndex - 1;
        public bool IsValid => storedLocalIndex > 0;

        public int CompareTo(FpgSkillEventKey other)
        {
            if (!IsValid || !other.IsValid)
            {
                return IsValid.CompareTo(other.IsValid);
            }

            int trackComparison = Track.CompareTo(other.Track);
            if (trackComparison != 0)
            {
                return trackComparison;
            }

            int actionComparison = ActionKind.CompareTo(other.ActionKind);
            if (actionComparison != 0)
            {
                return actionComparison;
            }

            int presentationTrackComparison = PresentationTrackIndex.CompareTo(
                other.PresentationTrackIndex);
            return presentationTrackComparison != 0
                ? presentationTrackComparison
                : LocalIndex.CompareTo(other.LocalIndex);
        }

        public bool Equals(FpgSkillEventKey other)
        {
            return Track == other.Track
                && ActionKind == other.ActionKind
                && storedPresentationTrackIndex
                    == other.storedPresentationTrackIndex
                && storedLocalIndex == other.storedLocalIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is FpgSkillEventKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Track;
                hash = (hash * 397) ^ (int)ActionKind;
                hash = (hash * 397) ^ storedPresentationTrackIndex;
                return (hash * 397) ^ storedLocalIndex;
            }
        }

        public override string ToString()
        {
            return IsValid
                ? Track + "/" + ActionKind + "/"
                    + PresentationTrackIndex + "/" + LocalIndex
                : "Invalid";
        }

        public static bool operator ==(
            FpgSkillEventKey left,
            FpgSkillEventKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            FpgSkillEventKey left,
            FpgSkillEventKey right)
        {
            return !left.Equals(right);
        }
    }

    internal readonly struct FpgSkillEditorLocation
    {
        public FpgSkillEditorLocation(
            FpgSkillEventKey eventKey,
            int tick)
        {
            EventKey = eventKey;
            Tick = tick;
        }

        public FpgSkillEventKey EventKey { get; }
        public int Tick { get; }
    }

    internal sealed class FpgSkillEventSelection
    {
        private readonly List<FpgSkillEventKey> ordered =
            new List<FpgSkillEventKey>();
        private readonly HashSet<FpgSkillEventKey> selected =
            new HashSet<FpgSkillEventKey>();

        public IReadOnlyList<FpgSkillEventKey> Items => ordered;
        public int Count => ordered.Count;
        public FpgSkillEventKey PrimaryEventKey { get; private set; }

        public bool Contains(FpgSkillEventKey eventKey)
        {
            return selected.Contains(eventKey);
        }

        public void Clear()
        {
            ordered.Clear();
            selected.Clear();
            PrimaryEventKey = FpgSkillEventKey.Invalid;
        }

        public void SetSingle(FpgSkillEventKey eventKey)
        {
            Clear();
            if (!eventKey.IsValid)
            {
                return;
            }

            selected.Add(eventKey);
            ordered.Add(eventKey);
            PrimaryEventKey = eventKey;
        }

        public void Set(
            IEnumerable<FpgSkillEventKey> eventKeys,
            FpgSkillEventKey primaryEventKey = default)
        {
            Clear();
            if (eventKeys != null)
            {
                foreach (FpgSkillEventKey eventKey in eventKeys)
                {
                    if (eventKey.IsValid && selected.Add(eventKey))
                    {
                        ordered.Add(eventKey);
                    }
                }
            }

            if (selected.Contains(primaryEventKey))
            {
                PrimaryEventKey = primaryEventKey;
            }
            else if (ordered.Count > 0)
            {
                PrimaryEventKey = ordered[ordered.Count - 1];
            }
        }

        public void Add(FpgSkillEventKey eventKey)
        {
            if (!eventKey.IsValid)
            {
                return;
            }

            if (selected.Add(eventKey))
            {
                ordered.Add(eventKey);
            }

            PrimaryEventKey = eventKey;
        }

        public void MakePrimary(FpgSkillEventKey eventKey)
        {
            if (selected.Contains(eventKey))
            {
                PrimaryEventKey = eventKey;
            }
        }

        public void Toggle(FpgSkillEventKey eventKey)
        {
            if (!eventKey.IsValid)
            {
                return;
            }

            if (selected.Remove(eventKey))
            {
                ordered.Remove(eventKey);
                PrimaryEventKey = ordered.Count == 0
                    ? FpgSkillEventKey.Invalid
                    : ordered[ordered.Count - 1];
                return;
            }

            selected.Add(eventKey);
            ordered.Add(eventKey);
            PrimaryEventKey = eventKey;
        }

        public void Retain(ISet<FpgSkillEventKey> validEventKeys)
        {
            if (validEventKeys == null)
            {
                Clear();
                return;
            }

            for (int index = ordered.Count - 1; index >= 0; index--)
            {
                FpgSkillEventKey eventKey = ordered[index];
                if (validEventKeys.Contains(eventKey))
                {
                    continue;
                }

                ordered.RemoveAt(index);
                selected.Remove(eventKey);
            }

            if (!selected.Contains(PrimaryEventKey))
            {
                PrimaryEventKey = ordered.Count == 0
                    ? FpgSkillEventKey.Invalid
                    : ordered[ordered.Count - 1];
            }
        }
    }

    internal sealed class FpgSkillEditorSession
    {
        public FpgSkillEditorSession()
        {
            Selection = new FpgSkillEventSelection();
        }

        public int DurationTicks { get; private set; } = 120;
        public int CurrentTick { get; private set; }
        public int TargetCount { get; private set; } = 1;
        public FpgSkillEventSelection Selection { get; }

        public void SetDuration(int durationTicks)
        {
            DurationTicks = Mathf.Max(0, durationTicks);
            CurrentTick = Mathf.Clamp(CurrentTick, 0, DurationTicks);
        }

        public int ScrubAbsolute(int tick)
        {
            CurrentTick = Mathf.Clamp(tick, 0, DurationTicks);
            return CurrentTick;
        }

        public int SetTargetCount(int count)
        {
            TargetCount = Mathf.Clamp(count, 1, 4);
            return TargetCount;
        }

        public FpgSkillEditorLocation Locate(FpgSkillValidationItem item)
        {
            if (item == null)
            {
                return new FpgSkillEditorLocation(
                    FpgSkillEventKey.Invalid,
                    CurrentTick);
            }

            if (item.Tick >= 0)
            {
                ScrubAbsolute(item.Tick);
            }

            return new FpgSkillEditorLocation(
                item.EventKey,
                CurrentTick);
        }
    }
}
