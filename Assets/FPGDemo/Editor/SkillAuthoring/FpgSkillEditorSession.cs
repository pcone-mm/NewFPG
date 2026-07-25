using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal readonly struct FpgSkillEditorLocation
    {
        public FpgSkillEditorLocation(int eventIndex, int payloadIndex, int tick)
        {
            EventIndex = eventIndex;
            PayloadIndex = payloadIndex;
            Tick = tick;
        }

        public int EventIndex { get; }
        public int PayloadIndex { get; }
        public int Tick { get; }
    }

    internal sealed class FpgSkillEventSelection
    {
        private readonly List<int> ordered = new List<int>();
        private readonly HashSet<int> selected = new HashSet<int>();

        public IReadOnlyList<int> Items => ordered;
        public int Count => ordered.Count;
        public int PrimaryEventIndex { get; private set; } = -1;

        public bool Contains(int eventIndex)
        {
            return selected.Contains(eventIndex);
        }

        public void Clear()
        {
            ordered.Clear();
            selected.Clear();
            PrimaryEventIndex = -1;
        }

        public void SetSingle(int eventIndex)
        {
            Clear();
            if (eventIndex < 0)
            {
                return;
            }

            selected.Add(eventIndex);
            ordered.Add(eventIndex);
            PrimaryEventIndex = eventIndex;
        }

        public void Set(IEnumerable<int> eventIndices, int primaryEventIndex = -1)
        {
            Clear();
            if (eventIndices != null)
            {
                foreach (int eventIndex in eventIndices)
                {
                    if (eventIndex >= 0 && selected.Add(eventIndex))
                    {
                        ordered.Add(eventIndex);
                    }
                }
            }

            if (selected.Contains(primaryEventIndex))
            {
                PrimaryEventIndex = primaryEventIndex;
            }
            else if (ordered.Count > 0)
            {
                PrimaryEventIndex = ordered[ordered.Count - 1];
            }
        }

        public void Add(int eventIndex)
        {
            if (eventIndex < 0)
            {
                return;
            }

            if (selected.Add(eventIndex))
            {
                ordered.Add(eventIndex);
            }

            PrimaryEventIndex = eventIndex;
        }

        public void MakePrimary(int eventIndex)
        {
            if (selected.Contains(eventIndex))
            {
                PrimaryEventIndex = eventIndex;
            }
        }

        public void Toggle(int eventIndex)
        {
            if (eventIndex < 0)
            {
                return;
            }

            if (selected.Remove(eventIndex))
            {
                ordered.Remove(eventIndex);
                PrimaryEventIndex = ordered.Count == 0
                    ? -1
                    : ordered[ordered.Count - 1];
                return;
            }

            selected.Add(eventIndex);
            ordered.Add(eventIndex);
            PrimaryEventIndex = eventIndex;
        }

        public void Retain(ISet<int> validEventIndices)
        {
            if (validEventIndices == null)
            {
                Clear();
                return;
            }

            for (int index = ordered.Count - 1; index >= 0; index--)
            {
                int eventIndex = ordered[index];
                if (validEventIndices.Contains(eventIndex))
                {
                    continue;
                }

                ordered.RemoveAt(index);
                selected.Remove(eventIndex);
            }

            if (!selected.Contains(PrimaryEventIndex))
            {
                PrimaryEventIndex = ordered.Count == 0
                    ? -1
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
                return new FpgSkillEditorLocation(-1, -1, CurrentTick);
            }

            if (item.Tick >= 0)
            {
                ScrubAbsolute(item.Tick);
            }

            return new FpgSkillEditorLocation(
                item.EventIndex,
                item.PayloadIndex,
                CurrentTick);
        }
    }
}
