using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal sealed class FpgSkillTimelineEventViewModel
    {
        public int Index;
        public int Tick;
        public int DurationTicks;
        public int AuthoredOrdinal;
        public string Label;
        public int Lane;
        public string LaneLabel;
        public string PayloadPreview;
        public Color Color;
        public bool IsInvalid;

        public bool IsActiveAt(int tick)
        {
            int duration = Mathf.Max(0, DurationTicks);
            return duration == 0
                ? tick == Tick
                : tick >= Tick && tick < Tick + duration;
        }
    }

    internal enum FpgSkillTimelineBlockKind
    {
        Animation = 0,
        Phase
    }

    internal enum FpgSkillTimelineBlockEditMode
    {
        Move = 0,
        ResizeStart = 1,
        ResizeEnd = 2
    }

    internal sealed class FpgSkillTimelineBlockViewModel
    {
        public FpgSkillTimelineBlockKind Kind;
        public int Index = -1;
        public int StartTick;
        public int EndTick;
        public int MinimumStartTick;
        public int MaximumEndTick = int.MaxValue;
        public int Lane;
        public string Label;
        public string Tooltip;
        public Color Color;
        public bool IsInvalid;
        public bool CanResize;
        public bool AllowSequenceExtension;
    }

    internal readonly struct FpgSkillTimelineCreateRequest
    {
        public FpgSkillTimelineCreateRequest(
            FpgSkillEventTrackKind track,
            int tick,
            int durationTicks)
        {
            Track = track;
            Tick = tick;
            DurationTicks = durationTicks;
        }

        public FpgSkillEventTrackKind Track { get; }
        public int Tick { get; }
        public int DurationTicks { get; }
    }

    internal sealed class FpgSkillTimelineView : VisualElement
    {
        private const float CanvasPadding = 48f;
        private const float TimelineOrigin = 72f;
        private const float LaneTop = 42f;
        private const float LaneHeight = 38f;
        private const float PointEventWidth = 16f;
        private const float EventDragAxisLockThreshold = 4f;

        private readonly ScrollView scrollView;
        private readonly VisualElement canvas;
        private readonly List<FpgSkillTimelineEventViewModel> events =
            new List<FpgSkillTimelineEventViewModel>();
        private readonly List<FpgSkillTimelineBlockViewModel> blocks =
            new List<FpgSkillTimelineBlockViewModel>();
        private readonly Dictionary<int, VisualElement> markers =
            new Dictionary<int, VisualElement>();
        private readonly List<BlockVisualElement> blockElements =
            new List<BlockVisualElement>();
        private readonly HashSet<FpgSkillEventTrackKind> availableTracks =
            new HashSet<FpgSkillEventTrackKind>();
        private readonly FpgSkillEventSelection selection =
            new FpgSkillEventSelection();

        private VisualElement playhead;
        private VisualElement creationPreview;
        private EventDragState eventDrag;
        private BlockDragState blockDrag;
        private ScrubDragState scrubDrag;
        private CreationDragState creationDrag;
        private PanDragState panDrag;
        private int durationTicks = 120;
        private int playheadTick;
        private FpgSkillTimelineBlockKind selectedBlockKind;
        private int selectedBlockIndex = -1;
        private float pixelsPerTick = 10f;

        public FpgSkillTimelineView()
        {
            AddToClassList("skill-timeline");
            focusable = true;
            availableTracks.Add(FpgSkillEventTrackKind.Logic);
            availableTracks.Add(FpgSkillEventTrackKind.Presentation);
            availableTracks.Add(FpgSkillEventTrackKind.Warning);

            scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scrollView.AddToClassList("timeline-scroll");
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scrollView.RegisterCallback<WheelEvent>(OnWheel);
            Add(scrollView);

            canvas = new VisualElement { name = "timeline-canvas" };
            canvas.AddToClassList("timeline-canvas");
            canvas.RegisterCallback<PointerDownEvent>(OnCanvasPointerDown);
            canvas.RegisterCallback<PointerMoveEvent>(OnCanvasPointerMove);
            canvas.RegisterCallback<PointerUpEvent>(OnCanvasPointerUp);
            canvas.RegisterCallback<PointerCancelEvent>(OnCanvasPointerCancel);
            canvas.RegisterCallback<PointerCaptureOutEvent>(
                OnCanvasPointerCaptureOut);
            scrollView.Add(canvas);

            RegisterCallback<KeyDownEvent>(OnKeyDown);
            Rebuild();
        }

        public event Action<int> PlayheadChanged;
        public event Action<IReadOnlyList<int>> EventSelectionChanged;
        public event Action<IReadOnlyList<int>, int> EventsTickDeltaChanged;
        public event Action<FpgSkillTimelineCreateRequest> EventCreateRequested;
        public event Action<FpgSkillTimelineBlockKind, int> BlockSelected;
        public event Action<int, int> EventOrderDeltaChanged;
        public event Action<FpgSkillTimelineBlockKind, int, int>
            BlockTickDeltaChanged;
        public event Action<
            FpgSkillTimelineBlockKind,
            int,
            FpgSkillTimelineBlockEditMode,
            int,
            int> BlockRangeChanged;

        public int PlayheadTick => playheadTick;
        public int SelectedEventIndex => selection.PrimaryEventIndex;
        public IReadOnlyList<int> SelectedEventIndices => selection.Items;
        public float HorizontalScrollValue =>
            scrollView.horizontalScroller.value;
        public FpgSkillTimelineBlockKind SelectedBlockKind =>
            selectedBlockKind;
        public int SelectedBlockIndex => selectedBlockIndex;

        public void SetModel(
            int nextDurationTicks,
            IReadOnlyList<FpgSkillTimelineEventViewModel> nextEvents)
        {
            SetModel(
                nextDurationTicks,
                nextEvents,
                Array.Empty<FpgSkillTimelineBlockViewModel>());
        }

        public void SetModel(
            int nextDurationTicks,
            IReadOnlyList<FpgSkillTimelineEventViewModel> nextEvents,
            IReadOnlyList<FpgSkillTimelineBlockViewModel> nextBlocks)
        {
            durationTicks = Mathf.Max(0, nextDurationTicks);
            playheadTick = Mathf.Clamp(playheadTick, 0, durationTicks);
            events.Clear();
            blocks.Clear();
            HashSet<int> validIndices = new HashSet<int>();
            if (nextEvents != null)
            {
                for (int index = 0; index < nextEvents.Count; index++)
                {
                    FpgSkillTimelineEventViewModel model = nextEvents[index];
                    events.Add(model);
                    validIndices.Add(model.Index);
                }
            }

            if (nextBlocks != null)
            {
                for (int index = 0; index < nextBlocks.Count; index++)
                {
                    blocks.Add(nextBlocks[index]);
                }
            }

            selection.Retain(validIndices);
            if (!ContainsBlock(selectedBlockKind, selectedBlockIndex))
            {
                selectedBlockIndex = -1;
            }

            Rebuild();
        }

        public void SetAvailableTracks(
            IEnumerable<FpgSkillEventTrackKind> tracks)
        {
            availableTracks.Clear();
            if (tracks != null)
            {
                foreach (FpgSkillEventTrackKind track in tracks)
                {
                    availableTracks.Add(track);
                }
            }

            Rebuild();
        }

        public void SetZoom(float nextPixelsPerTick)
        {
            pixelsPerTick = Mathf.Clamp(nextPixelsPerTick, 4f, 28f);
            Rebuild();
            FrameTick(playheadTick);
        }

        public void SetPlayhead(int tick, bool notify = false)
        {
            int normalized = Mathf.Clamp(tick, 0, durationTicks);
            if (playheadTick == normalized)
            {
                UpdatePlayheadPosition();
                return;
            }

            playheadTick = normalized;
            UpdatePlayheadPosition();
            if (notify)
            {
                PlayheadChanged?.Invoke(playheadTick);
            }
        }

        public void SelectEvent(int eventIndex, bool notify = false)
        {
            SelectEvents(
                eventIndex >= 0 ? new[] { eventIndex } : Array.Empty<int>(),
                eventIndex,
                notify);
        }

        public void SelectEvents(
            IEnumerable<int> eventIndices,
            int primaryEventIndex = -1,
            bool notify = false)
        {
            HashSet<int> valid = new HashSet<int>();
            if (eventIndices != null)
            {
                foreach (int eventIndex in eventIndices)
                {
                    if (ContainsEventIndex(eventIndex))
                    {
                        valid.Add(eventIndex);
                    }
                }
            }

            bool rebuild = valid.Count > 0 && selectedBlockIndex >= 0;
            if (valid.Count > 0)
            {
                selectedBlockIndex = -1;
            }

            selection.Set(valid, primaryEventIndex);
            if (rebuild)
            {
                Rebuild();
            }
            else
            {
                ApplySelectionStyles();
            }

            if (notify)
            {
                NotifySelectionChanged();
            }
        }

        public void SelectBlock(
            FpgSkillTimelineBlockKind kind,
            int index,
            bool notify = false)
        {
            if (!ContainsBlock(kind, index))
            {
                selectedBlockIndex = -1;
                Rebuild();
                return;
            }

            selectedBlockKind = kind;
            selectedBlockIndex = index;
            selection.Clear();
            Rebuild();
            if (notify)
            {
                BlockSelected?.Invoke(kind, index);
            }
        }

        public void FrameTick(int tick)
        {
            float target = Mathf.Max(
                0f,
                TimelineOrigin + tick * pixelsPerTick - 140f);
            float maximum = scrollView.horizontalScroller.highValue;
            scrollView.horizontalScroller.value = Mathf.Clamp(target, 0f, maximum);
        }

        public void PanByPixels(float deltaPixels)
        {
            Scroller scroller = scrollView.horizontalScroller;
            scroller.value = Mathf.Clamp(
                scroller.value + deltaPixels,
                scroller.lowValue,
                scroller.highValue);
        }

        public void RequestCreateFromDrag(
            FpgSkillEventTrackKind track,
            int startTick,
            int endTick)
        {
            int normalizedStart = Mathf.Clamp(
                Mathf.Min(startTick, endTick),
                0,
                durationTicks);
            int normalizedEnd = Mathf.Clamp(
                Mathf.Max(startTick, endTick),
                0,
                durationTicks);
            int duration = track == FpgSkillEventTrackKind.Warning
                ? Mathf.Max(1, normalizedEnd - normalizedStart)
                : 0;
            EventCreateRequested?.Invoke(new FpgSkillTimelineCreateRequest(
                track,
                normalizedStart,
                duration));
        }

        private void Rebuild()
        {
            canvas.Clear();
            markers.Clear();
            blockElements.Clear();
            creationPreview = null;
            float width = Mathf.Max(
                640f,
                TimelineOrigin + durationTicks * pixelsPerTick + CanvasPadding);
            int laneCount = GetLaneCount();

            canvas.style.width = width;
            canvas.style.height = LaneTop + laneCount * LaneHeight + 10f;

            VisualElement ruler = new VisualElement();
            ruler.AddToClassList("timeline-ruler");
            ruler.pickingMode = PickingMode.Ignore;
            canvas.Add(ruler);

            int majorStep = pixelsPerTick >= 18f
                ? 5
                : pixelsPerTick >= 8f
                    ? 10
                    : 20;
            int minorStep = Mathf.Max(1, majorStep / 5);
            for (int tick = 0; tick <= durationTicks; tick += minorStep)
            {
                bool major = tick % majorStep == 0;
                VisualElement line = new VisualElement();
                line.AddToClassList("timeline-grid-line");
                if (major)
                {
                    line.AddToClassList("timeline-grid-line--major");
                }

                line.style.left = TimelineOrigin + tick * pixelsPerTick;
                line.pickingMode = PickingMode.Ignore;
                canvas.Add(line);

                if (major)
                {
                    Label label = new Label(tick.ToString());
                    label.AddToClassList("timeline-tick-label");
                    label.style.left = TimelineOrigin + tick * pixelsPerTick + 3f;
                    label.pickingMode = PickingMode.Ignore;
                    canvas.Add(label);
                }
            }

            for (int lane = 0; lane < laneCount; lane++)
            {
                Label laneLabel = new Label(GetLaneLabel(lane));
                laneLabel.AddToClassList("timeline-lane-label");
                laneLabel.style.top = LaneTop + lane * LaneHeight + 8f;
                laneLabel.pickingMode = PickingMode.Ignore;
                canvas.Add(laneLabel);

                VisualElement separator = new VisualElement();
                separator.AddToClassList("timeline-lane-separator");
                separator.style.top = LaneTop + (lane + 1) * LaneHeight;
                separator.pickingMode = PickingMode.Ignore;
                canvas.Add(separator);
            }

            for (int index = 0; index < blocks.Count; index++)
            {
                AddBlockElement(blocks[index]);
            }

            for (int index = 0; index < events.Count; index++)
            {
                AddEventElement(events[index]);
            }

            playhead = new VisualElement();
            playhead.AddToClassList("timeline-playhead");
            playhead.pickingMode = PickingMode.Ignore;
            VisualElement cap = new VisualElement();
            cap.AddToClassList("timeline-playhead-cap");
            cap.pickingMode = PickingMode.Ignore;
            playhead.Add(cap);
            canvas.Add(playhead);
            UpdatePlayheadPosition();
        }

        private void AddBlockElement(
            FpgSkillTimelineBlockViewModel model)
        {
            int visualEndTick = model.AllowSequenceExtension
                ? Mathf.Max(
                    durationTicks,
                    Mathf.Max(model.StartTick, model.EndTick))
                : durationTicks;
            int startTick = Mathf.Clamp(
                model.StartTick,
                0,
                visualEndTick);
            int endTick = Mathf.Clamp(
                model.EndTick,
                startTick,
                visualEndTick);
            if (model.AllowSequenceExtension && endTick > durationTicks)
            {
                UpdateTemporaryCanvasWidth(endTick);
            }

            VisualElement block = new VisualElement
            {
                pickingMode = PickingMode.Position
            };
            block.AddToClassList("timeline-block");
            block.AddToClassList(
                model.Kind == FpgSkillTimelineBlockKind.Animation
                    ? "timeline-block--animation"
                    : "timeline-block--phase");
            block.EnableInClassList(
                "timeline-block--resizable",
                model.CanResize);
            if (model.IsInvalid)
            {
                block.AddToClassList("timeline-event--invalid");
            }

            if (model.Kind == selectedBlockKind
                && model.Index == selectedBlockIndex)
            {
                block.AddToClassList("timeline-block--selected");
            }

            block.style.left = TimelineOrigin + startTick * pixelsPerTick;
            block.style.top = LaneTop + model.Lane * LaneHeight + 5f;
            block.style.width = GetBlockVisualWidth(startTick, endTick);
            block.style.backgroundColor = model.Color;
            block.tooltip = (model.Tooltip ?? model.Label)
                + (model.CanResize
                    ? "\n拖动中部平移；拖动左右边缘调整时长。"
                    : "\n拖动中部平移；片段长度由源动画帧数决定。");

            Label title = new Label(model.Label ?? string.Empty);
            title.AddToClassList("timeline-block__label");
            title.pickingMode = PickingMode.Ignore;
            block.Add(title);

            Label duration = new Label(
                GetBlockDurationLabel(model.Kind, startTick, endTick));
            duration.AddToClassList("timeline-block__duration");
            duration.pickingMode = PickingMode.Ignore;
            block.Add(duration);

            block.RegisterCallback<PointerDownEvent>(evt =>
                BeginBlockDrag(
                    evt,
                    block,
                    duration,
                    model,
                    startTick,
                    endTick,
                    FpgSkillTimelineBlockEditMode.Move));

            if (model.CanResize)
            {
                block.Add(CreateBlockResizeHandle(
                    block,
                    duration,
                    model,
                    startTick,
                    endTick,
                    FpgSkillTimelineBlockEditMode.ResizeStart));
                block.Add(CreateBlockResizeHandle(
                    block,
                    duration,
                    model,
                    startTick,
                    endTick,
                    FpgSkillTimelineBlockEditMode.ResizeEnd));
            }

            blockElements.Add(new BlockVisualElement(model, block));
            canvas.Add(block);
        }

        private VisualElement CreateBlockResizeHandle(
            VisualElement block,
            Label durationLabel,
            FpgSkillTimelineBlockViewModel model,
            int startTick,
            int endTick,
            FpgSkillTimelineBlockEditMode editMode)
        {
            VisualElement handle = new VisualElement
            {
                pickingMode = PickingMode.Position,
                tooltip = editMode
                    == FpgSkillTimelineBlockEditMode.ResizeStart
                        ? "拖动调整开始 Tick"
                        : "拖动调整结束 Tick"
            };
            handle.AddToClassList("timeline-block__resize-handle");
            handle.AddToClassList(
                editMode == FpgSkillTimelineBlockEditMode.ResizeStart
                    ? "timeline-block__resize-handle--start"
                    : "timeline-block__resize-handle--end");
            handle.RegisterCallback<PointerDownEvent>(evt =>
                BeginBlockDrag(
                    evt,
                    block,
                    durationLabel,
                    model,
                    startTick,
                    endTick,
                    editMode));
            return handle;
        }

        private float GetBlockVisualWidth(int startTick, int endTick)
        {
            return Mathf.Max(
                PointEventWidth,
                Mathf.Max(0, endTick - startTick) * pixelsPerTick);
        }

        private static string GetBlockDurationLabel(
            FpgSkillTimelineBlockKind kind,
            int startTick,
            int endTick)
        {
            int duration = Mathf.Max(0, endTick - startTick);
            return duration + " 帧";
        }


        private void AddEventElement(FpgSkillTimelineEventViewModel model)
        {
            string fullLabel = string.IsNullOrWhiteSpace(model.Label)
                ? "事件 " + (model.Index + 1)
                : model.Label;
            bool isPointEvent = model.DurationTicks <= 0;
            Label marker = new Label(isPointEvent ? "◆" : fullLabel);
            marker.AddToClassList("timeline-event");
            if (selection.Contains(model.Index))
            {
                marker.AddToClassList("timeline-event--selected");
            }

            if (model.IsInvalid)
            {
                marker.AddToClassList("timeline-event--invalid");
            }

            int normalizedTick = Mathf.Clamp(model.Tick, 0, durationTicks);
            marker.style.left = TimelineOrigin + normalizedTick * pixelsPerTick;
            marker.style.top = LaneTop + Mathf.Max(0, model.Lane) * LaneHeight
                + 4f + Mathf.Abs(model.AuthoredOrdinal % 3) * 2f;
            float desiredWidth = model.DurationTicks > 0
                ? Mathf.Max(PointEventWidth, model.DurationTicks * pixelsPerTick)
                : PointEventWidth;
            float availableWidth = Mathf.Max(
                PointEventWidth,
                (durationTicks - normalizedTick) * pixelsPerTick);
            marker.style.width = Mathf.Min(desiredWidth, availableWidth);
            marker.style.backgroundColor = model.Color;
            Color borderColor = Color.Lerp(model.Color, Color.white, 0.38f);
            marker.style.borderLeftColor = borderColor;
            marker.style.borderRightColor = borderColor;
            marker.style.borderTopColor = borderColor;
            marker.style.borderBottomColor = borderColor;
            marker.tooltip = string.Format(
                "{0} · {1}\nTick {2}，顺序 {3}{4}\n左右拖动修改 Tick；同 Tick 上下拖动修改执行顺序。",
                string.IsNullOrWhiteSpace(model.LaneLabel) ? "事件" : model.LaneLabel,
                fullLabel,
                model.Tick,
                model.AuthoredOrdinal,
                model.DurationTicks > 0
                    ? "，持续 " + model.DurationTicks + " Tick"
                    : string.Empty);
            marker.RegisterCallback<PointerDownEvent>(evt =>
                BeginEventDrag(evt, model));
            markers[model.Index] = marker;
            canvas.Add(marker);
        }

        private void OnCanvasPointerDown(PointerDownEvent evt)
        {
            bool beginPan = evt.button == 2 || evt.button == 0 && evt.altKey;
            if (beginPan)
            {
                panDrag = new PanDragState
                {
                    PointerId = evt.pointerId,
                    StartWorldX = evt.position.x,
                    StartScrollValue = scrollView.horizontalScroller.value
                };
                canvas.CapturePointer(evt.pointerId);
                Focus();
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0 || evt.target != canvas)
            {
                return;
            }

            Vector2 local = canvas.WorldToLocal(evt.position);
            int tick = PositionToTick(local.x);
            if (IsActionKey(evt))
            {
                int lane = PositionToLane(local.y);
                if (lane < 2)
                {
                    BeginScrubAtTick(evt.pointerId, tick);
                    canvas.CapturePointer(evt.pointerId);
                    Focus();
                    evt.StopPropagation();
                    return;
                }

                FpgSkillEventTrackKind track = GetTrackForLane(lane);
                creationDrag = new CreationDragState
                {
                    PointerId = evt.pointerId,
                    Track = track,
                    StartTick = tick,
                    CurrentTick = tick
                };
                canvas.CapturePointer(evt.pointerId);
                UpdateCreationPreview();
                Focus();
                evt.StopPropagation();
                return;
            }

            selection.Clear();
            selectedBlockIndex = -1;
            ApplySelectionStyles();
            ApplyBlockSelectionStyles();
            NotifySelectionChanged();
            BeginScrubAtTick(evt.pointerId, tick);
            canvas.CapturePointer(evt.pointerId);
            Focus();
            evt.StopPropagation();
        }

        private void OnCanvasPointerMove(PointerMoveEvent evt)
        {
            if (eventDrag != null
                && eventDrag.PointerId == evt.pointerId
                && canvas.HasPointerCapture(evt.pointerId))
            {
                ContinueEventDrag(evt);
                evt.StopPropagation();
                return;
            }

            if (blockDrag != null
                && blockDrag.PointerId == evt.pointerId
                && canvas.HasPointerCapture(evt.pointerId))
            {
                ContinueBlockDrag(evt);
                evt.StopPropagation();
                return;
            }

            if (panDrag != null
                && panDrag.PointerId == evt.pointerId
                && canvas.HasPointerCapture(evt.pointerId))
            {
                float delta = evt.position.x - panDrag.StartWorldX;
                scrollView.horizontalScroller.value = Mathf.Clamp(
                    panDrag.StartScrollValue - delta,
                    scrollView.horizontalScroller.lowValue,
                    scrollView.horizontalScroller.highValue);
                evt.StopPropagation();
                return;
            }

            if (creationDrag != null
                && creationDrag.PointerId == evt.pointerId
                && canvas.HasPointerCapture(evt.pointerId))
            {
                Vector2 local = canvas.WorldToLocal(evt.position);
                creationDrag.CurrentTick = PositionToTick(local.x);
                UpdateCreationPreview();
                SetPlayhead(creationDrag.CurrentTick, true);
                evt.StopPropagation();
                return;
            }

            if (scrubDrag != null
                && scrubDrag.PointerId == evt.pointerId
                && canvas.HasPointerCapture(evt.pointerId))
            {
                Vector2 local = canvas.WorldToLocal(evt.position);
                ContinueScrubAtTick(evt.pointerId, PositionToTick(local.x));
                evt.StopPropagation();
            }
        }

        private void OnCanvasPointerUp(PointerUpEvent evt)
        {
            if (eventDrag != null && eventDrag.PointerId == evt.pointerId)
            {
                EndEventDrag(evt);
                evt.StopPropagation();
                return;
            }

            if (blockDrag != null && blockDrag.PointerId == evt.pointerId)
            {
                EndBlockDrag(evt);
                evt.StopPropagation();
                return;
            }

            if (panDrag != null && panDrag.PointerId == evt.pointerId)
            {
                panDrag = null;
                ReleaseCanvasPointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }

            if (creationDrag != null
                && creationDrag.PointerId == evt.pointerId)
            {
                CreationDragState completed = creationDrag;
                creationDrag = null;
                creationPreview?.RemoveFromHierarchy();
                creationPreview = null;
                ReleaseCanvasPointer(evt.pointerId);
                RequestCreateFromDrag(
                    completed.Track,
                    completed.StartTick,
                    completed.CurrentTick);
                evt.StopPropagation();
                return;
            }

            if (scrubDrag != null && scrubDrag.PointerId == evt.pointerId)
            {
                EndScrub(evt.pointerId);
                ReleaseCanvasPointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        private void BeginEventDrag(
            PointerDownEvent evt,
            FpgSkillTimelineEventViewModel model)
        {
            if (evt.button != 0 || evt.altKey)
            {
                return;
            }

            if (IsActionKey(evt))
            {
                selection.Toggle(model.Index);
            }
            else if (evt.shiftKey)
            {
                selection.Add(model.Index);
            }
            else if (!selection.Contains(model.Index))
            {
                selection.SetSingle(model.Index);
            }
            else
            {
                selection.MakePrimary(model.Index);
            }

            selectedBlockIndex = -1;
            ApplySelectionStyles();
            ApplyBlockSelectionStyles();
            NotifySelectionChanged();
            if (!selection.Contains(model.Index))
            {
                evt.StopPropagation();
                return;
            }

            EventDragState state = new EventDragState
            {
                PointerId = evt.pointerId,
                StartWorldX = evt.position.x,
                StartWorldY = evt.position.y,
                CurrentDeltaTicks = 0,
                CurrentOrderDelta = 0,
                CanReorder = selection.Count == 1,
                Axis = selection.Count == 1
                    ? EventDragAxis.Undecided
                    : EventDragAxis.Tick,
                MinimumDeltaTicks = int.MinValue,
                MaximumDeltaTicks = int.MaxValue
            };
            for (int index = 0; index < events.Count; index++)
            {
                FpgSkillTimelineEventViewModel selectedEvent = events[index];
                if (!selection.Contains(selectedEvent.Index))
                {
                    continue;
                }

                state.StartTicks[selectedEvent.Index] = selectedEvent.Tick;
                state.StartTops[selectedEvent.Index] =
                    LaneTop + Mathf.Max(0, selectedEvent.Lane) * LaneHeight
                    + 4f + Mathf.Abs(selectedEvent.AuthoredOrdinal % 3) * 2f;
                state.MinimumDeltaTicks = Mathf.Max(
                    state.MinimumDeltaTicks,
                    -selectedEvent.Tick);
                state.MaximumDeltaTicks = Mathf.Min(
                    state.MaximumDeltaTicks,
                    durationTicks - selectedEvent.Tick
                        - Mathf.Max(0, selectedEvent.DurationTicks));
            }

            if (state.StartTicks.Count == 0)
            {
                evt.StopPropagation();
                return;
            }

            eventDrag = state;
            canvas.CapturePointer(evt.pointerId);
            SetPlayhead(model.Tick, true);
            Focus();
            evt.StopPropagation();
        }

        private void ContinueEventDrag(PointerMoveEvent evt)
        {
            if (eventDrag == null
                || eventDrag.PointerId != evt.pointerId
                || !canvas.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            float deltaX = evt.position.x - eventDrag.StartWorldX;
            float deltaY = evt.position.y - eventDrag.StartWorldY;
            float absoluteX = Mathf.Abs(deltaX);
            float absoluteY = Mathf.Abs(deltaY);
            if (eventDrag.Axis == EventDragAxis.Undecided)
            {
                if (Mathf.Max(absoluteX, absoluteY)
                    < EventDragAxisLockThreshold)
                {
                    return;
                }

                eventDrag.Axis = absoluteY > absoluteX
                    ? EventDragAxis.Order
                    : EventDragAxis.Tick;
            }

            int nextDelta = 0;
            if (eventDrag.Axis == EventDragAxis.Tick)
            {
                int requestedDelta = Mathf.RoundToInt(
                    deltaX / pixelsPerTick);
                nextDelta = Mathf.Clamp(
                    requestedDelta,
                    eventDrag.MinimumDeltaTicks,
                    eventDrag.MaximumDeltaTicks);
            }

            eventDrag.CurrentDeltaTicks = nextDelta;
            eventDrag.CurrentOrderDelta =
                eventDrag.Axis == EventDragAxis.Order
                && eventDrag.CanReorder
                    ? Mathf.Clamp(
                        Mathf.RoundToInt(deltaY / 12f),
                        -8,
                        8)
                    : 0;

            foreach (KeyValuePair<int, int> pair in eventDrag.StartTicks)
            {
                if (!markers.TryGetValue(
                        pair.Key,
                        out VisualElement selectedMarker))
                {
                    continue;
                }

                selectedMarker.style.left = TimelineOrigin
                    + (pair.Value + nextDelta) * pixelsPerTick;
                if (eventDrag.StartTops.TryGetValue(
                        pair.Key,
                        out float startTop))
                {
                    selectedMarker.style.top = startTop + Mathf.Clamp(
                        eventDrag.CurrentOrderDelta * 5f,
                        -14f,
                        14f);
                }
            }

            if (eventDrag.StartTicks.TryGetValue(
                    selection.PrimaryEventIndex,
                    out int primaryStartTick))
            {
                SetPlayhead(primaryStartTick + nextDelta, true);
            }
        }

        private void EndEventDrag(PointerUpEvent evt)
        {
            if (eventDrag == null
                || eventDrag.PointerId != evt.pointerId)
            {
                return;
            }

            EventDragState completed = eventDrag;
            eventDrag = null;
            ReleaseCanvasPointer(evt.pointerId);
            int primaryEventIndex = selection.PrimaryEventIndex;
            if (completed.CurrentDeltaTicks != 0)
            {
                EventsTickDeltaChanged?.Invoke(
                    SnapshotSelection(),
                    completed.CurrentDeltaTicks);
            }
            else if (completed.CurrentOrderDelta != 0
                && primaryEventIndex >= 0)
            {
                EventOrderDeltaChanged?.Invoke(
                    primaryEventIndex,
                    completed.CurrentOrderDelta);
            }
            else
            {
                Rebuild();
            }
        }

        private void BeginBlockDrag(
            PointerDownEvent evt,
            VisualElement block,
            Label durationLabel,
            FpgSkillTimelineBlockViewModel model,
            int startTick,
            int endTick,
            FpgSkillTimelineBlockEditMode editMode)
        {
            if (evt.button != 0
                || evt.altKey
                || (editMode != FpgSkillTimelineBlockEditMode.Move
                    && !model.CanResize))
            {
                return;
            }

            selectedBlockKind = model.Kind;
            selectedBlockIndex = model.Index;
            selection.Clear();
            ApplySelectionStyles();
            ApplyBlockSelectionStyles();
            BlockSelected?.Invoke(model.Kind, model.Index);

            int minimumStartTick = Mathf.Clamp(
                model.MinimumStartTick,
                0,
                startTick);
            int maximumEndTick = model.MaximumEndTick;
            if (maximumEndTick < 0)
            {
                maximumEndTick = durationTicks;
            }

            if (!model.AllowSequenceExtension)
            {
                maximumEndTick = Mathf.Min(
                    maximumEndTick,
                    durationTicks);
            }

            maximumEndTick = Mathf.Max(endTick, maximumEndTick);
            blockDrag = new BlockDragState
            {
                PointerId = evt.pointerId,
                Kind = model.Kind,
                Index = model.Index,
                EditMode = editMode,
                StartWorldX = evt.position.x,
                StartTick = startTick,
                EndTick = endTick,
                CurrentStartTick = startTick,
                CurrentEndTick = endTick,
                MinimumStartTick = minimumStartTick,
                MaximumEndTick = maximumEndTick,
                AllowSequenceExtension = model.AllowSequenceExtension,
                Element = block,
                DurationLabel = durationLabel
            };
            canvas.CapturePointer(evt.pointerId);
            SetPlayhead(
                editMode == FpgSkillTimelineBlockEditMode.ResizeEnd
                    ? endTick
                    : startTick,
                true);
            Focus();
            evt.StopImmediatePropagation();
        }

        private void ContinueBlockDrag(PointerMoveEvent evt)
        {
            if (blockDrag == null
                || blockDrag.PointerId != evt.pointerId
                || !canvas.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            int delta = Mathf.RoundToInt(
                (evt.position.x - blockDrag.StartWorldX) / pixelsPerTick);
            int nextStart = blockDrag.StartTick;
            int nextEnd = blockDrag.EndTick;
            switch (blockDrag.EditMode)
            {
                case FpgSkillTimelineBlockEditMode.ResizeStart:
                    nextStart = AddAndClamp(
                        blockDrag.StartTick,
                        delta,
                        blockDrag.MinimumStartTick,
                        blockDrag.EndTick);
                    break;
                case FpgSkillTimelineBlockEditMode.ResizeEnd:
                    nextEnd = AddAndClamp(
                        blockDrag.EndTick,
                        delta,
                        blockDrag.StartTick,
                        blockDrag.MaximumEndTick);
                    break;
                default:
                    int length = blockDrag.EndTick - blockDrag.StartTick;
                    int maximumStart = Mathf.Max(
                        blockDrag.MinimumStartTick,
                        blockDrag.MaximumEndTick - length);
                    nextStart = AddAndClamp(
                        blockDrag.StartTick,
                        delta,
                        blockDrag.MinimumStartTick,
                        maximumStart);
                    nextEnd = nextStart + length;
                    break;
            }

            blockDrag.CurrentStartTick = nextStart;
            blockDrag.CurrentEndTick = nextEnd;
            UpdateBlockDragVisual(blockDrag);
            SetPlayhead(
                blockDrag.EditMode
                    == FpgSkillTimelineBlockEditMode.ResizeEnd
                    ? nextEnd
                    : nextStart,
                true);
        }

        private void EndBlockDrag(PointerUpEvent evt)
        {
            if (blockDrag == null || blockDrag.PointerId != evt.pointerId)
            {
                return;
            }

            BlockDragState completed = blockDrag;
            blockDrag = null;
            ReleaseCanvasPointer(evt.pointerId);

            bool changed = completed.CurrentStartTick != completed.StartTick
                || completed.CurrentEndTick != completed.EndTick;
            if (!changed)
            {
                Rebuild();
                return;
            }

            if (BlockRangeChanged != null)
            {
                BlockRangeChanged.Invoke(
                    completed.Kind,
                    completed.Index,
                    completed.EditMode,
                    completed.CurrentStartTick,
                    completed.CurrentEndTick);
                return;
            }

            if (completed.EditMode == FpgSkillTimelineBlockEditMode.Move)
            {
                BlockTickDeltaChanged?.Invoke(
                    completed.Kind,
                    completed.Index,
                    completed.CurrentStartTick - completed.StartTick);
                return;
            }

            Rebuild();
        }

        private void UpdateBlockDragVisual(BlockDragState state)
        {
            state.Element.style.left = TimelineOrigin
                + state.CurrentStartTick * pixelsPerTick;
            state.Element.style.width = GetBlockVisualWidth(
                state.CurrentStartTick,
                state.CurrentEndTick);
            if (state.DurationLabel != null)
            {
                state.DurationLabel.text = GetBlockDurationLabel(
                    state.Kind,
                    state.CurrentStartTick,
                    state.CurrentEndTick);
            }

            if (state.AllowSequenceExtension)
            {
                UpdateTemporaryCanvasWidth(state.CurrentEndTick);
            }
        }

        private void UpdateTemporaryCanvasWidth(int endTick)
        {
            int visibleEndTick = Mathf.Max(durationTicks, endTick);
            canvas.style.width = Mathf.Max(
                640f,
                TimelineOrigin
                    + visibleEndTick * pixelsPerTick
                    + CanvasPadding);
        }

        private static int AddAndClamp(
            int value,
            int delta,
            int minimum,
            int maximum)
        {
            long requested = (long)value + delta;
            return (int)Math.Max(
                minimum,
                Math.Min((long)maximum, requested));
        }


        internal void BeginScrubAtTick(int pointerId, int tick)
        {
            scrubDrag = new ScrubDragState { PointerId = pointerId };
            SetPlayhead(tick, true);
        }

        internal void ContinueScrubAtTick(int pointerId, int tick)
        {
            if (scrubDrag == null || scrubDrag.PointerId != pointerId)
            {
                return;
            }

            SetPlayhead(tick, true);
        }

        internal void EndScrub(int pointerId)
        {
            if (scrubDrag != null && scrubDrag.PointerId == pointerId)
            {
                scrubDrag = null;
            }
        }

        private void OnCanvasPointerCancel(PointerCancelEvent evt)
        {
            if (CancelPointerGesture(evt.pointerId, true))
            {
                evt.StopPropagation();
            }
        }

        private void OnCanvasPointerCaptureOut(
            PointerCaptureOutEvent evt)
        {
            CancelPointerGesture(evt.pointerId, false);
        }

        private bool CancelPointerGesture(
            int pointerId,
            bool releasePointer)
        {
            bool handled = false;
            bool rebuild = false;
            if (eventDrag != null && eventDrag.PointerId == pointerId)
            {
                eventDrag = null;
                handled = true;
                rebuild = true;
            }

            if (blockDrag != null && blockDrag.PointerId == pointerId)
            {
                blockDrag = null;
                handled = true;
                rebuild = true;
            }

            if (scrubDrag != null && scrubDrag.PointerId == pointerId)
            {
                scrubDrag = null;
                handled = true;
            }

            if (panDrag != null && panDrag.PointerId == pointerId)
            {
                panDrag = null;
                handled = true;
            }

            if (creationDrag != null
                && creationDrag.PointerId == pointerId)
            {
                creationDrag = null;
                creationPreview?.RemoveFromHierarchy();
                creationPreview = null;
                handled = true;
            }

            if (releasePointer)
            {
                ReleaseCanvasPointer(pointerId);
            }

            if (rebuild)
            {
                Rebuild();
            }

            return handled;
        }










        private void OnWheel(WheelEvent evt)
        {
            bool horizontalGesture = evt.shiftKey
                || Mathf.Abs(evt.delta.x) > Mathf.Abs(evt.delta.y);
            if (!horizontalGesture)
            {
                return;
            }

            float delta = Mathf.Abs(evt.delta.x) > Mathf.Epsilon
                ? evt.delta.x
                : evt.delta.y;
            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            PanByPixels(delta * 20f);
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.LeftArrow)
            {
                SetPlayhead(playheadTick - 1, true);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.RightArrow)
            {
                SetPlayhead(playheadTick + 1, true);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Home)
            {
                SetPlayhead(0, true);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.End)
            {
                SetPlayhead(durationTicks, true);
                evt.StopPropagation();
            }
        }

        private void UpdateCreationPreview()
        {
            if (creationDrag == null)
            {
                return;
            }

            if (creationPreview == null)
            {
                creationPreview = new VisualElement();
                creationPreview.AddToClassList("timeline-create-preview");
                creationPreview.pickingMode = PickingMode.Ignore;
                canvas.Add(creationPreview);
            }

            int start = Mathf.Min(
                creationDrag.StartTick,
                creationDrag.CurrentTick);
            int end = Mathf.Max(
                creationDrag.StartTick,
                creationDrag.CurrentTick);
            int lane = GetLaneForTrack(creationDrag.Track);
            creationPreview.style.left = TimelineOrigin + start * pixelsPerTick;
            creationPreview.style.top = LaneTop + lane * LaneHeight + 5f;
            creationPreview.style.width = Mathf.Max(
                PointEventWidth,
                (end - start) * pixelsPerTick);
        }

        private void UpdatePlayheadPosition()
        {
            if (playhead != null)
            {
                playhead.style.left = TimelineOrigin + playheadTick * pixelsPerTick;
            }
        }

        private void ApplySelectionStyles()
        {
            foreach (KeyValuePair<int, VisualElement> pair in markers)
            {
                pair.Value.EnableInClassList(
                    "timeline-event--selected",
                    selection.Contains(pair.Key));
            }
        }

        private void ApplyBlockSelectionStyles()
        {
            for (int index = 0; index < blockElements.Count; index++)
            {
                BlockVisualElement visual = blockElements[index];
                visual.Element.EnableInClassList(
                    "timeline-block--selected",
                    visual.Model.Kind == selectedBlockKind
                        && visual.Model.Index == selectedBlockIndex);
            }
        }


        private void NotifySelectionChanged()
        {
            EventSelectionChanged?.Invoke(SnapshotSelection());
        }

        private IReadOnlyList<int> SnapshotSelection()
        {
            int[] snapshot = new int[selection.Count];
            for (int index = 0; index < selection.Count; index++)
            {
                snapshot[index] = selection.Items[index];
            }

            return snapshot;
        }

        private int GetLaneCount()
        {
            int laneCount = availableTracks.Contains(
                    FpgSkillEventTrackKind.Warning)
                ? 5
                : availableTracks.Contains(
                    FpgSkillEventTrackKind.Presentation)
                    ? 4
                    : 3;
            for (int index = 0; index < events.Count; index++)
            {
                laneCount = Mathf.Max(laneCount, events[index].Lane + 1);
            }

            for (int index = 0; index < blocks.Count; index++)
            {
                laneCount = Mathf.Max(laneCount, blocks[index].Lane + 1);
            }

            return laneCount;
        }

        private string GetLaneLabel(int lane)
        {
            if (lane == 0)
            {
                return "主动画";
            }

            if (lane == 1)
            {
                return "动作阶段";
            }

            for (int index = 0; index < events.Count; index++)
            {
                if (events[index].Lane == lane
                    && !string.IsNullOrWhiteSpace(
                        events[index].LaneLabel))
                {
                    return events[index].LaneLabel;
                }
            }

            switch (lane)
            {
                case 2:
                    return availableTracks.Contains(
                            FpgSkillEventTrackKind.Logic)
                        ? "逻辑"
                        : "事件";
                case 3:
                    return "演出";
                case 4:
                    return "预警";
                default:
                    return "轨道 " + (lane + 1);
            }
        }

        private FpgSkillEventTrackKind GetTrackForLane(int lane)
        {
            if (lane == 3)
            {
                return FpgSkillEventTrackKind.Presentation;
            }

            if (lane >= 4)
            {
                return FpgSkillEventTrackKind.Warning;
            }

            return availableTracks.Contains(FpgSkillEventTrackKind.Logic)
                ? FpgSkillEventTrackKind.Logic
                : FpgSkillEventTrackKind.Generic;
        }

        private static int GetLaneForTrack(
            FpgSkillEventTrackKind track)
        {
            switch (track)
            {
                case FpgSkillEventTrackKind.Presentation:
                    return 3;
                case FpgSkillEventTrackKind.Warning:
                    return 4;
                default:
                    return 2;
            }
        }

        private int PositionToTick(float localX)
        {
            return Mathf.Clamp(
                Mathf.RoundToInt((localX - TimelineOrigin) / pixelsPerTick),
                0,
                durationTicks);
        }

        private int PositionToLane(float localY)
        {
            return Mathf.Clamp(
                Mathf.FloorToInt((localY - LaneTop) / LaneHeight),
                0,
                GetLaneCount() - 1);
        }

        private bool ContainsBlock(
            FpgSkillTimelineBlockKind kind,
            int index)
        {
            for (int blockIndex = 0;
                blockIndex < blocks.Count;
                blockIndex++)
            {
                FpgSkillTimelineBlockViewModel block = blocks[blockIndex];
                if (block.Kind == kind && block.Index == index)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsEventIndex(int eventIndex)
        {
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index].Index == eventIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private void ReleaseCanvasPointer(int pointerId)
        {
            if (canvas.HasPointerCapture(pointerId))
            {
                canvas.ReleasePointer(pointerId);
            }
        }

        private static bool IsActionKey(IPointerEvent evt)
        {
            return evt.ctrlKey || evt.commandKey;
        }

        private sealed class BlockVisualElement
        {
            public BlockVisualElement(
                FpgSkillTimelineBlockViewModel model,
                VisualElement element)
            {
                Model = model;
                Element = element;
            }

            public FpgSkillTimelineBlockViewModel Model { get; }
            public VisualElement Element { get; }
        }

        private sealed class BlockDragState
        {
            public int PointerId;
            public FpgSkillTimelineBlockKind Kind;
            public FpgSkillTimelineBlockEditMode EditMode;
            public int Index;
            public float StartWorldX;
            public int StartTick;
            public int EndTick;
            public int CurrentStartTick;
            public int CurrentEndTick;
            public int MinimumStartTick;
            public int MaximumEndTick;
            public bool AllowSequenceExtension;
            public VisualElement Element;
            public Label DurationLabel;
        }

        private sealed class ScrubDragState
        {
            public int PointerId;
        }

        private enum EventDragAxis
        {
            Undecided,
            Tick,
            Order
        }

        private sealed class EventDragState
        {
            public readonly Dictionary<int, int> StartTicks =
                new Dictionary<int, int>();
            public readonly Dictionary<int, float> StartTops =
                new Dictionary<int, float>();
            public int PointerId;
            public float StartWorldX;
            public float StartWorldY;
            public int CurrentDeltaTicks;
            public int CurrentOrderDelta;
            public bool CanReorder;
            public EventDragAxis Axis;
            public int MinimumDeltaTicks;
            public int MaximumDeltaTicks;
        }

        private sealed class CreationDragState
        {
            public int PointerId;
            public FpgSkillEventTrackKind Track;
            public int StartTick;
            public int CurrentTick;
        }

        private sealed class PanDragState
        {
            public int PointerId;
            public float StartWorldX;
            public float StartScrollValue;
        }
    }
}
