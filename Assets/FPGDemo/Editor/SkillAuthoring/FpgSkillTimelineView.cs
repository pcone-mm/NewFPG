using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal sealed class FpgSkillTimelineEventViewModel
    {
        public FpgSkillEventKey Key;
        public int Tick;
        public int DurationTicks;
        public int AuthoredOrdinal;
        public string Label;
        public int Lane;
        public FpgSkillEventTrackKind Track;
        public int PresentationTrackIndex = -1;
        public string LaneLabel;
        public string PreviewSummary;
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

    internal sealed class FpgSkillTimelinePresentationTrackViewModel
    {
        public int Index;
        public string Label;
    }

    internal enum FpgSkillTimelineBlockKind
    {
        Animation = 0
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
            : this(track, -1, tick, durationTicks)
        {
        }

        public FpgSkillTimelineCreateRequest(
            FpgSkillEventTrackKind track,
            int presentationTrackIndex,
            int tick,
            int durationTicks)
        {
            Track = track;
            PresentationTrackIndex = presentationTrackIndex;
            Tick = tick;
            DurationTicks = durationTicks;
        }

        public FpgSkillEventTrackKind Track { get; }
        public int PresentationTrackIndex { get; }
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
        private const int FirstEventLane = 1;
        private const float EventLaneSpacing = 2f;
        private const float EventDragAxisLockThreshold = 4f;

        private readonly ScrollView scrollView;
        private readonly VisualElement canvas;
        private readonly List<FpgSkillTimelineEventViewModel> events =
            new List<FpgSkillTimelineEventViewModel>();
        private readonly List<FpgSkillTimelineBlockViewModel> blocks =
            new List<FpgSkillTimelineBlockViewModel>();
        private readonly Dictionary<FpgSkillEventKey, VisualElement> markers =
            new Dictionary<FpgSkillEventKey, VisualElement>();
        private readonly List<BlockVisualElement> blockElements =
            new List<BlockVisualElement>();
        private readonly Dictionary<FpgSkillEventKey, int> eventLayoutLanes =
            new Dictionary<FpgSkillEventKey, int>();
        private readonly List<EventLaneDefinition> eventLanes =
            new List<EventLaneDefinition>();
        private readonly List<FpgSkillTimelinePresentationTrackViewModel>
            presentationTracks =
                new List<FpgSkillTimelinePresentationTrackViewModel>();
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
            availableTracks.Add(FpgSkillEventTrackKind.GameplayAction);
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
        public event Action<IReadOnlyList<FpgSkillEventKey>>
            EventSelectionChanged;
        public event Action<IReadOnlyList<FpgSkillEventKey>, int>
            EventsTickDeltaChanged;
        public event Action<FpgSkillTimelineCreateRequest> EventCreateRequested;
        public event Action<FpgSkillTimelineBlockKind, int> BlockSelected;
        public event Action<FpgSkillEventKey, int> EventOrderDeltaChanged;
        public event Action<FpgSkillTimelineBlockKind, int, int>
            BlockTickDeltaChanged;
        public event Action<
            FpgSkillTimelineBlockKind,
            int,
            FpgSkillTimelineBlockEditMode,
            int,
            int> BlockRangeChanged;

        public int PlayheadTick => playheadTick;
        public bool IsDirectManipulationActive =>
            scrubDrag != null
            || creationDrag != null
            || eventDrag != null
            || blockDrag != null;
        public FpgSkillEventKey SelectedEventKey => selection.PrimaryEventKey;
        public IReadOnlyList<FpgSkillEventKey> SelectedEventKeys =>
            selection.Items;
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
            HashSet<FpgSkillEventKey> validKeys =
                new HashSet<FpgSkillEventKey>();
            if (nextEvents != null)
            {
                for (int index = 0; index < nextEvents.Count; index++)
                {
                    FpgSkillTimelineEventViewModel model = nextEvents[index];
                    events.Add(model);
                    validKeys.Add(model.Key);
                }
            }

            if (nextBlocks != null)
            {
                for (int index = 0; index < nextBlocks.Count; index++)
                {
                    blocks.Add(nextBlocks[index]);
                }
            }

            selection.Retain(validKeys);
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

        public void SetPresentationTracks(
            IEnumerable<FpgSkillTimelinePresentationTrackViewModel> tracks)
        {
            presentationTracks.Clear();
            if (tracks != null)
            {
                foreach (FpgSkillTimelinePresentationTrackViewModel track
                    in tracks)
                {
                    if (track != null && track.Index >= 0)
                    {
                        presentationTracks.Add(track);
                    }
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

        public void SelectEvent(
            FpgSkillEventKey eventKey,
            bool notify = false)
        {
            SelectEvents(
                eventKey.IsValid
                    ? new[] { eventKey }
                    : Array.Empty<FpgSkillEventKey>(),
                eventKey,
                notify);
        }

        public void SelectEvents(
            IEnumerable<FpgSkillEventKey> eventKeys,
            FpgSkillEventKey primaryEventKey = default,
            bool notify = false)
        {
            HashSet<FpgSkillEventKey> valid =
                new HashSet<FpgSkillEventKey>();
            if (eventKeys != null)
            {
                foreach (FpgSkillEventKey eventKey in eventKeys)
                {
                    if (ContainsEventKey(eventKey))
                    {
                        valid.Add(eventKey);
                    }
                }
            }

            bool rebuild = valid.Count > 0 && selectedBlockIndex >= 0;
            if (valid.Count > 0)
            {
                selectedBlockIndex = -1;
            }

            selection.Set(valid, primaryEventKey);
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
            RequestCreateFromDrag(track, -1, startTick, endTick);
        }

        public void RequestCreateFromDrag(
            FpgSkillEventTrackKind track,
            int presentationTrackIndex,
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
                presentationTrackIndex,
                normalizedStart,
                duration));
        }

        private void Rebuild()
        {
            BuildEventLaneLayout();
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
            block.AddToClassList("timeline-block--animation");
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
                GetBlockDurationLabel(startTick, endTick));
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
            int startTick,
            int endTick)
        {
            int duration = Mathf.Max(0, endTick - startTick);
            return duration + " 帧";
        }


        private void AddEventElement(FpgSkillTimelineEventViewModel model)
        {
            string fullLabel = string.IsNullOrWhiteSpace(model.Label)
                ? "事件 " + (model.Key.LocalIndex + 1)
                : model.Label;
            bool isPointEvent = model.DurationTicks <= 0;
            Label marker = new Label(
                isPointEvent ? GetPointEventIcon(model.Track) : fullLabel);
            marker.AddToClassList("timeline-event");
            if (selection.Contains(model.Key))
            {
                marker.AddToClassList("timeline-event--selected");
            }

            if (model.IsInvalid)
            {
                marker.AddToClassList("timeline-event--invalid");
            }

            int normalizedTick = Mathf.Clamp(model.Tick, 0, durationTicks);
            marker.style.left = TimelineOrigin + normalizedTick * pixelsPerTick;
            marker.style.top = LaneTop + GetEventLayoutLane(model) * LaneHeight
                + 4f;
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
            markers[model.Key] = marker;
            canvas.Add(marker);
        }

        private static string GetPointEventIcon(
            FpgSkillEventTrackKind track)
        {
            switch (track)
            {
                case FpgSkillEventTrackKind.PresentationVfx:
                    return "\u2726";
                case FpgSkillEventTrackKind.PresentationAudio:
                    return "\u266a";
                case FpgSkillEventTrackKind.PresentationCameraShake:
                    return "\u224b";
                case FpgSkillEventTrackKind.Warning:
                    return "\u26a0";
                default:
                    return "\u25b6";
            }
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
                if (lane < FirstEventLane)
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
                    PresentationTrackIndex =
                        GetPresentationTrackIndexForLane(lane),
                    Lane = lane,
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
                    completed.PresentationTrackIndex,
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
                selection.Toggle(model.Key);
            }
            else if (evt.shiftKey)
            {
                selection.Add(model.Key);
            }
            else if (!selection.Contains(model.Key))
            {
                selection.SetSingle(model.Key);
            }
            else
            {
                selection.MakePrimary(model.Key);
            }

            selectedBlockIndex = -1;
            ApplySelectionStyles();
            ApplyBlockSelectionStyles();
            NotifySelectionChanged();
            if (!selection.Contains(model.Key))
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
                if (!selection.Contains(selectedEvent.Key))
                {
                    continue;
                }

                state.StartTicks[selectedEvent.Key] = selectedEvent.Tick;
                state.StartTops[selectedEvent.Key] =
                    LaneTop + GetEventLayoutLane(selectedEvent) * LaneHeight
                    + 4f;
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

            foreach (KeyValuePair<FpgSkillEventKey, int> pair
                in eventDrag.StartTicks)
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
                    selection.PrimaryEventKey,
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
            FpgSkillEventKey primaryEventKey = selection.PrimaryEventKey;
            if (completed.CurrentDeltaTicks != 0)
            {
                EventsTickDeltaChanged?.Invoke(
                    SnapshotSelection(),
                    completed.CurrentDeltaTicks);
            }
            else if (completed.CurrentOrderDelta != 0
                && primaryEventKey.IsValid)
            {
                EventOrderDeltaChanged?.Invoke(
                    primaryEventKey,
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
            int lane = creationDrag.Lane;
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
            foreach (KeyValuePair<FpgSkillEventKey, VisualElement> pair
                in markers)
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

        private IReadOnlyList<FpgSkillEventKey> SnapshotSelection()
        {
            FpgSkillEventKey[] snapshot =
                new FpgSkillEventKey[selection.Count];
            for (int index = 0; index < selection.Count; index++)
            {
                snapshot[index] = selection.Items[index];
            }

            return snapshot;
        }

        private void BuildEventLaneLayout()
        {
            eventLayoutLanes.Clear();
            eventLanes.Clear();

            int lane = FirstEventLane;
            lane = AddEventLanes(
                lane,
                FpgSkillEventTrackKind.GameplayAction,
                true);
            if (presentationTracks.Count > 0)
            {
                for (int index = 0; index < presentationTracks.Count; index++)
                {
                    lane = AddPresentationTrackLanes(
                        lane,
                        presentationTracks[index]);
                }
            }
            AddEventLanes(
                lane,
                FpgSkillEventTrackKind.Warning,
                availableTracks.Contains(FpgSkillEventTrackKind.Warning));
        }

        private int AddEventLanes(
            int firstLane,
            FpgSkillEventTrackKind trackFamily,
            bool includeEmptyLane)
        {
            List<FpgSkillTimelineEventViewModel> matching =
                new List<FpgSkillTimelineEventViewModel>();
            for (int index = 0; index < events.Count; index++)
            {
                FpgSkillTimelineEventViewModel model = events[index];
                if (IsInTrackFamily(model.Track, trackFamily))
                {
                    matching.Add(model);
                }
            }

            if (matching.Count == 0)
            {
                if (includeEmptyLane)
                {
                    FpgSkillEventTrackKind emptyTrack =
                        GetDefaultTrackForFamily(trackFamily);
                    eventLanes.Add(new EventLaneDefinition(
                        firstLane,
                        emptyTrack,
                        -1,
                        GetDefaultTrackLabel(emptyTrack)));
                    return firstLane + 1;
                }

                return firstLane;
            }

            FpgSkillEventTrackKind displayTrack = GetDisplayTrack(
                trackFamily,
                matching);
            return AddMatchingEventLanes(
                firstLane,
                matching,
                displayTrack,
                -1,
                GetEventLaneLabel(displayTrack, matching));
        }

        private int AddPresentationTrackLanes(
            int firstLane,
            FpgSkillTimelinePresentationTrackViewModel presentationTrack)
        {
            List<FpgSkillTimelineEventViewModel> matching =
                new List<FpgSkillTimelineEventViewModel>();
            for (int index = 0; index < events.Count; index++)
            {
                FpgSkillTimelineEventViewModel model = events[index];
                if (model.PresentationTrackIndex == presentationTrack.Index
                    && IsActivePresentationEvent(model.Track))
                {
                    matching.Add(model);
                }
            }

            string label = string.IsNullOrWhiteSpace(presentationTrack.Label)
                ? "Presentation " + (presentationTrack.Index + 1)
                : presentationTrack.Label;
            if (matching.Count == 0)
            {
                eventLanes.Add(new EventLaneDefinition(
                    firstLane,
                    FpgSkillEventTrackKind.PresentationVfx,
                    presentationTrack.Index,
                    label));
                return firstLane + 1;
            }

            return AddMatchingEventLanes(
                firstLane,
                matching,
                FpgSkillEventTrackKind.PresentationVfx,
                presentationTrack.Index,
                label);
        }

        private int AddMatchingEventLanes(
            int firstLane,
            List<FpgSkillTimelineEventViewModel> matching,
            FpgSkillEventTrackKind displayTrack,
            int presentationTrackIndex,
            string label)
        {
            matching.Sort((left, right) =>
            {
                int tickComparison = left.Tick.CompareTo(right.Tick);
                if (tickComparison != 0)
                {
                    return tickComparison;
                }

                int ordinalComparison = left.AuthoredOrdinal.CompareTo(
                    right.AuthoredOrdinal);
                return ordinalComparison != 0
                    ? ordinalComparison
                    : left.Key.CompareTo(right.Key);
            });

            List<float> rightEdges = new List<float>();
            for (int index = 0; index < matching.Count; index++)
            {
                FpgSkillTimelineEventViewModel model = matching[index];
                float leftEdge = TimelineOrigin
                    + Mathf.Clamp(model.Tick, 0, durationTicks) * pixelsPerTick;
                float rightEdge = leftEdge + GetEventVisualWidth(model);
                int subLane = 0;
                while (subLane < rightEdges.Count
                    && leftEdge < rightEdges[subLane] + EventLaneSpacing)
                {
                    subLane++;
                }

                if (subLane == rightEdges.Count)
                {
                    rightEdges.Add(rightEdge);
                }
                else
                {
                    rightEdges[subLane] = rightEdge;
                }

                eventLayoutLanes[model.Key] = firstLane + subLane;
            }

            for (int subLane = 0; subLane < rightEdges.Count; subLane++)
            {
                eventLanes.Add(new EventLaneDefinition(
                    firstLane + subLane,
                    displayTrack,
                    presentationTrackIndex,
                    subLane == 0
                        ? label
                        : label + " " + (subLane + 1)));
            }

            return firstLane + rightEdges.Count;
        }

        private static bool IsActivePresentationEvent(
            FpgSkillEventTrackKind track)
        {
            return track == FpgSkillEventTrackKind.PresentationVfx
                || track == FpgSkillEventTrackKind.PresentationAudio
                || track == FpgSkillEventTrackKind.PresentationCameraShake;
        }

        private static bool IsInTrackFamily(
            FpgSkillEventTrackKind track,
            FpgSkillEventTrackKind family)
        {
            return track == family;
        }

        private FpgSkillEventTrackKind GetDefaultTrackForFamily(
            FpgSkillEventTrackKind trackFamily)
        {
            return trackFamily;
        }

        private static FpgSkillEventTrackKind GetDisplayTrack(
            FpgSkillEventTrackKind trackFamily,
            IReadOnlyList<FpgSkillTimelineEventViewModel> matching)
        {
            return trackFamily;
        }

        private static string GetEventLaneLabel(
            FpgSkillEventTrackKind track,
            IReadOnlyList<FpgSkillTimelineEventViewModel> matching)
        {
            for (int index = 0; index < matching.Count; index++)
            {
                FpgSkillTimelineEventViewModel model = matching[index];
                if (model.Track == track
                    && !string.IsNullOrWhiteSpace(model.LaneLabel))
                {
                    return model.LaneLabel;
                }
            }

            for (int index = 0; index < matching.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(matching[index].LaneLabel))
                {
                    return matching[index].LaneLabel;
                }
            }

            return GetDefaultTrackLabel(track);
        }

        private static string GetDefaultTrackLabel(FpgSkillEventTrackKind track)
        {
            switch (track)
            {
                case FpgSkillEventTrackKind.Warning:
                    return "\u9884\u8b66";
                case FpgSkillEventTrackKind.GameplayAction:
                    return "\u73a9\u6cd5\u52a8\u4f5c";
                default:
                    return "\u4e8b\u4ef6";
            }
        }

        private float GetEventVisualWidth(FpgSkillTimelineEventViewModel model)
        {
            return Mathf.Max(
                PointEventWidth,
                Mathf.Max(0, model.DurationTicks) * pixelsPerTick);
        }

        private int GetEventLayoutLane(FpgSkillTimelineEventViewModel model)
        {
            return eventLayoutLanes.TryGetValue(model.Key, out int lane)
                ? lane
                : Mathf.Max(0, model.Lane);
        }

        private int GetLaneCount()
        {
            int laneCount = FirstEventLane;
            for (int index = 0; index < eventLanes.Count; index++)
            {
                laneCount = Mathf.Max(laneCount, eventLanes[index].Lane + 1);
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
                return "\u4e3b\u52a8\u753b";
            }

            for (int index = 0; index < eventLanes.Count; index++)
            {
                if (eventLanes[index].Lane == lane)
                {
                    return eventLanes[index].Label;
                }
            }

            return "\u8f68\u9053 " + (lane + 1);
        }

        private FpgSkillEventTrackKind GetTrackForLane(int lane)
        {
            for (int index = 0; index < eventLanes.Count; index++)
            {
                if (eventLanes[index].Lane == lane)
                {
                    return eventLanes[index].Track;
                }
            }

            return GetDefaultTrackForFamily(FpgSkillEventTrackKind.GameplayAction);
        }

        private int GetPresentationTrackIndexForLane(int lane)
        {
            for (int index = 0; index < eventLanes.Count; index++)
            {
                if (eventLanes[index].Lane == lane)
                {
                    return eventLanes[index].PresentationTrackIndex;
                }
            }

            return -1;
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

        private bool ContainsEventKey(FpgSkillEventKey eventKey)
        {
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index].Key == eventKey)
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

        private sealed class EventLaneDefinition
        {
            public EventLaneDefinition(
                int lane,
                FpgSkillEventTrackKind track,
                int presentationTrackIndex,
                string label)
            {
                Lane = lane;
                Track = track;
                PresentationTrackIndex = presentationTrackIndex;
                Label = label;
            }

            public int Lane { get; }
            public FpgSkillEventTrackKind Track { get; }
            public int PresentationTrackIndex { get; }
            public string Label { get; }
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
            public readonly Dictionary<FpgSkillEventKey, int> StartTicks =
                new Dictionary<FpgSkillEventKey, int>();
            public readonly Dictionary<FpgSkillEventKey, float> StartTops =
                new Dictionary<FpgSkillEventKey, float>();
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
            public int PresentationTrackIndex = -1;
            public int Lane;
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
