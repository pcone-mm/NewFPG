using System;
using System.Collections.Generic;
using FPG.Demo.Skills;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal enum FpgSkillPreviewPayloadKind
    {
        Unknown = 0,
        PlayerPelletRay,
        PlayerAreaAtFirstSurface,
        PlayerReload,
        EnemyProjectile,
        EnemyTimedImpact,
        EnemySummon
    }
    internal enum FpgSkillIssueSeverity
    {
        Info = 0,
        Warning,
        Error
    }

    internal enum FpgSkillEventTrackKind
    {
        Generic = 0,
        Logic,
        Presentation,
        Warning
    }

    internal sealed class FpgSkillAssetRecord
    {
        public UnityEngine.Object Asset;
        public string Path;
        public string SkillId;
        public string DisplayName;
        public string OwnerType;
    }

    internal sealed class FpgSkillPayloadRecord
    {
        public int Index;
        public string Id;
        public string Name;
        public string Kind;
        public string HitShape;
        public int ImpactDelayTicks;
        public int BaseDamage;
        public int BreakDamage;
        public int WeakpointDamage;
        public int WeakpointBreakDamage;
        public int MaxHitCount;
        public int PelletCount;
        public int AdditionalPenetrationCount;
        public int AreaCombatantLimit;
        public int AreaProjectileLimit;
        public int ProjectileCount;
        public int SummonCandidateCount;
        public FpgSkillPreviewPayloadKind PreviewKind;
        public bool HasDamagePreview;
        public int UseCount;
        public Color Color;

        public string BuildPreviewSummary(int eventTick)
        {
            string timing = "预计命中 Tick "
                + Mathf.Max(0, eventTick + ImpactDelayTicks);
            string capacity = "最大命中 " + Mathf.Max(0, MaxHitCount);
            if (!HasDamagePreview)
            {
                return HitShape + " · " + timing + " · " + capacity;
            }

            return HitShape + " · " + timing
                + " · Body 生命 " + BaseDamage + " / 削韧 " + BreakDamage
                + " · Weakpoint 生命 " + WeakpointDamage
                + " / 削韧 " + WeakpointBreakDamage
                + " · " + capacity;
        }
    }

    internal sealed class FpgSkillEventRecord
    {
        public int Index;
        public int ArrayIndex;
        public int Tick;
        public int DurationTicks;
        public int AuthoredOrdinal;
        public int PayloadIndex;
        public string EventId;
        public string PayloadId;
        public string SocketId;
        public string Name;
        public string Kind;
        public FpgSkillTargetSource TargetSource;
        public Vector3 TargetOffset;
        public FpgSkillEventTrackKind Track;
        public bool IsInvalid;
        public Color Color;

        public FpgSkillTimelineEventViewModel ToViewModel()
        {
            return new FpgSkillTimelineEventViewModel
            {
                Index = Index,
                Tick = Tick,
                DurationTicks = DurationTicks,
                AuthoredOrdinal = AuthoredOrdinal,
                Label = Name,
                Lane = GetLane(Track),
                LaneLabel = GetTrackLabel(Track),
                PayloadPreview = PayloadPreview,
                Color = Color,
                IsInvalid = IsInvalid
            };
        }

        public string PayloadPreview;

        private static int GetLane(FpgSkillEventTrackKind track)
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

        private static string GetTrackLabel(FpgSkillEventTrackKind track)
        {
            switch (track)
            {
                case FpgSkillEventTrackKind.Logic:
                    return "逻辑";
                case FpgSkillEventTrackKind.Presentation:
                    return "演出";
                case FpgSkillEventTrackKind.Warning:
                    return "预警";
                default:
                    return "事件";
            }
        }
    }

    internal sealed class FpgSkillValidationItem
    {
        public FpgSkillIssueSeverity Severity;
        public string Message;
        public int EventIndex = -1;
        public int PayloadIndex = -1;
        public int Tick = -1;
    }

    internal sealed class FpgSkillCompiledTriggerRecord
    {
        public int CompiledEventId;
        public int Tick;
        public int AuthoredOrdinal;
        public int EventIndex = -1;
        public string Kind;
        public string Name;
        public FpgCompiledSkillEvent CompiledEvent;
    }

    internal static class FpgSkillSerializedAdapter
    {
        private static readonly string[] SequenceNames = { "sequences" };
        private static readonly string[] PayloadArrayNames =
            { "payloadSlots", "payloads", "attackPayloads", "slots" };
        private static readonly string[] GenericEventArrayNames =
            { "events", "attackEvents", "timelineEvents" };
        private static readonly string[] LogicEventArrayNames = { "logicEvents" };
        private static readonly string[] PresentationEventArrayNames =
            { "presentationCues" };
        private static readonly string[] WarningEventArrayNames = { "warnings" };
        private static readonly string[] DurationNames =
            { "durationTicks", "totalTicks", "lengthTicks", "endTick" };
        private static readonly string[] TickNames =
            { "tick", "triggerTick", "startTick", "releaseTick" };
        private static readonly string[] EventDurationNames =
            { "durationTicks", "activeTicks", "lengthTicks" };
        private static readonly string[] AuthoredOrdinalNames =
            { "authoredOrdinal" };
        private static readonly string[] PayloadIndexNames =
            { "payloadIndex", "payloadSlotIndex", "slotIndex" };
        private static readonly string[] PayloadIdNames =
            { "payloadSlotId", "payloadId", "slotId" };

        private static readonly Color[] Palette =
        {
            new Color(0.24f, 0.57f, 0.76f),
            new Color(0.74f, 0.42f, 0.27f),
            new Color(0.39f, 0.65f, 0.39f),
            new Color(0.64f, 0.43f, 0.72f),
            new Color(0.76f, 0.61f, 0.24f),
            new Color(0.33f, 0.66f, 0.65f),
            new Color(0.73f, 0.36f, 0.48f),
            new Color(0.50f, 0.53f, 0.70f)
        };

        public static List<FpgSkillAssetRecord> FindAssets()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:ScriptableObject",
                new[] { "Assets/FPGDemo" });
            List<FpgSkillAssetRecord> records = new List<FpgSkillAssetRecord>();
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (string.IsNullOrWhiteSpace(path) || !paths.Add(path))
                {
                    continue;
                }

                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!IsCompatible(asset))
                {
                    continue;
                }

                SerializedObject serializedObject = new SerializedObject(asset);
                records.Add(new FpgSkillAssetRecord
                {
                    Asset = asset,
                    Path = path,
                    SkillId = ReadString(serializedObject.FindProperty("skillId"), asset.name),
                    DisplayName = ReadString(serializedObject.FindProperty("displayName"), asset.name),
                    OwnerType = InferOwnerType(serializedObject)
                });
            }

            records.Sort((left, right) => string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.OrdinalIgnoreCase));
            return records;
        }

        public static bool IsCompatible(UnityEngine.Object asset)
        {
            if (asset == null || asset is MonoScript)
            {
                return false;
            }

            try
            {
                SerializedObject serializedObject = new SerializedObject(asset);
                SerializedProperty sequences = serializedObject.FindProperty("sequences");
                return serializedObject.FindProperty("skillId") != null
                    && serializedObject.FindProperty("displayName") != null
                    && sequences != null
                    && sequences.isArray;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static SerializedProperty GetSequences(SerializedObject serializedObject)
        {
            return FindFirst(serializedObject, SequenceNames);
        }

        public static SerializedProperty GetSequence(
            SerializedObject serializedObject,
            int sequenceIndex)
        {
            SerializedProperty sequences = GetSequences(serializedObject);
            return sequences != null
                && sequences.isArray
                && sequenceIndex >= 0
                && sequenceIndex < sequences.arraySize
                    ? sequences.GetArrayElementAtIndex(sequenceIndex)
                    : null;
        }

        public static SerializedProperty GetPayloads(SerializedProperty sequence)
        {
            SerializedProperty rootPayloads = sequence == null
                ? null
                : FindFirst(sequence.serializedObject, PayloadArrayNames);
            return rootPayloads != null && rootPayloads.isArray
                ? rootPayloads
                : FindFirstRelative(sequence, PayloadArrayNames);
        }

        public static SerializedProperty GetEvents(SerializedProperty sequence)
        {
            return GetEventArray(sequence, GetDefaultEventTrack(sequence));
        }

        public static int GetDurationTicks(SerializedProperty sequence)
        {
            SerializedProperty property = FindFirstRelative(sequence, DurationNames);
            return ReadInt(property, 120, 0);
        }

        public static string GetMainAnimation(SerializedProperty sequence)
        {
            string value = ReadFirstString(
                sequence,
                "mainAnimation",
                "animation",
                "animationName",
                "clipName");
            return string.IsNullOrWhiteSpace(value) ? "未指定动作" : value;
        }

        public static string GetAnimationPlaybackMode(
            SerializedProperty sequence)
        {
            SerializedProperty property =
                sequence?.FindPropertyRelative("animationPlaybackMode");
            if (property != null
                && property.propertyType == SerializedPropertyType.Enum
                && property.enumValueIndex >= 0
                && property.enumValueIndex < property.enumNames.Length)
            {
                return property.enumNames[property.enumValueIndex];
            }

            string value = ReadDisplayValue(property);
            return string.IsNullOrWhiteSpace(value)
                ? "NaturalSpeed"
                : value;
        }

        private static FpgSkillAnimationPlaybackMode GetAnimationPlaybackModeValue(
            SerializedProperty sequence)
        {
            SerializedProperty property =
                sequence?.FindPropertyRelative("animationPlaybackMode");
            int value = property != null
                && property.propertyType == SerializedPropertyType.Enum
                    ? property.intValue
                    : (int)FpgSkillAnimationPlaybackMode.NaturalSpeed;
            return Enum.IsDefined(typeof(FpgSkillAnimationPlaybackMode), value)
                ? (FpgSkillAnimationPlaybackMode)value
                : FpgSkillAnimationPlaybackMode.NaturalSpeed;
        }


        public static int GetAnimationStartTick(SerializedProperty sequence)
        {
            return ReadInt(sequence?.FindPropertyRelative("animationStartTick"), 0, 0);
        }

        public static int GetAnimationEndTick(SerializedProperty sequence)
        {
            int duration = GetDurationTicks(sequence);
            int value = ReadInt(
                sequence?.FindPropertyRelative("animationEndTick"),
                duration,
                0);
            return value == 0 && duration > 0 ? duration : value;
        }

        public static int GetAnimationSourceDurationTicks(
            SerializedProperty sequence)
        {
            return ReadInt(
                sequence?.FindPropertyRelative("sourceAnimationDurationTicks"),
                0,
                0);
        }

        public static bool SetAnimationSourceDurationTicks(
            SerializedObject serializedObject,
            int sequenceIndex,
            int durationTicks)
        {
            if (serializedObject == null || serializedObject.targetObject == null)
            {
                return false;
            }

            SerializedProperty sequence = GetSequence(
                serializedObject,
                sequenceIndex);
            SerializedProperty property = sequence?.FindPropertyRelative(
                "sourceAnimationDurationTicks");
            if (property == null
                || property.propertyType != SerializedPropertyType.Integer)
            {
                return false;
            }

            Undo.RecordObject(serializedObject.targetObject, "记录源动画长度");
            property.intValue = Mathf.Max(0, durationTicks);
            Apply(serializedObject);
            return true;
        }

        public static bool GetAnimationLoop(SerializedProperty sequence)
        {
            SerializedProperty property = sequence?.FindPropertyRelative("loop");
            return property != null
                && property.propertyType == SerializedPropertyType.Boolean
                && property.boolValue;
        }

        public static List<FpgSkillTimelineBlockViewModel> ReadTimelineBlocks(
            SerializedProperty sequence,
            int durationTicks,
            int measuredAnimationDurationTicks = -1)
        {
            List<FpgSkillTimelineBlockViewModel> result =
                new List<FpgSkillTimelineBlockViewModel>();
            if (sequence == null)
            {
                return result;
            }

            string animationName = GetMainAnimation(sequence);
            string playbackMode = GetAnimationPlaybackMode(sequence);
            FpgSkillAnimationPlaybackMode playbackModeValue =
                GetAnimationPlaybackModeValue(sequence);
            bool animationLoop = GetAnimationLoop(sequence);
            int animationStartTick = GetAnimationStartTick(sequence);
            int authoredAnimationEndTick = GetAnimationEndTick(sequence);
            int sourceAnimationTicks = measuredAnimationDurationTicks > 0
                ? measuredAnimationDurationTicks
                : GetAnimationSourceDurationTicks(sequence);
            bool naturalSpeed = playbackModeValue
                == FpgSkillAnimationPlaybackMode.NaturalSpeed;
            bool showCompleteNaturalClip = naturalSpeed
                && !animationLoop
                && sourceAnimationTicks > 0;
            long completeNaturalEnd = (long)animationStartTick
                + sourceAnimationTicks;
            int animationEndTick = showCompleteNaturalClip
                ? (int)Math.Min(int.MaxValue, completeNaturalEnd)
                : authoredAnimationEndTick;
            int animationIntervalTicks = Mathf.Max(
                0,
                animationEndTick - animationStartTick);
            string sourceAnimationLabel = sourceAnimationTicks > 0
                ? sourceAnimationTicks + "帧@60Hz"
                : "源帧数未测量";
            string animationLabel;
            string animationTooltip;
            if (naturalSpeed)
            {
                animationLabel = animationName + " · "
                    + (animationLoop ? "单次" : string.Empty)
                    + sourceAnimationLabel;
                animationTooltip = animationLoop
                    ? string.Format(
                        "主动画 {0} · NaturalSpeed · Loop 是\n单次完整源动画 {1}\n当前循环区间 Tick {2}-{3}；当前序列截止 Tick {4}\n变体 {5}",
                        animationName,
                        sourceAnimationLabel,
                        animationStartTick,
                        authoredAnimationEndTick,
                        durationTicks,
                        sequence.FindPropertyRelative("alternateAnimations")
                            ?.arraySize ?? 0)
                    : string.Format(
                        "主动画 {0} · NaturalSpeed · Loop 否\n完整片段 Tick {1}-{2} · {3}\n当前序列截止 Tick {4}\n变体 {5}",
                        animationName,
                        animationStartTick,
                        animationEndTick,
                        sourceAnimationLabel,
                        durationTicks,
                        sequence.FindPropertyRelative("alternateAnimations")
                            ?.arraySize ?? 0);
            }
            else
            {
                animationLabel = animationName + " · "
                    + (sourceAnimationTicks > 0 ? "源" : string.Empty)
                    + sourceAnimationLabel + " · 区间"
                    + animationIntervalTicks + "帧";
                animationTooltip = string.Format(
                    "主动画 {0} · {1} · Loop {2}\n完整源动画 {3}\n适配区间 Tick {4}-{5} · {6} 帧；当前序列截止 Tick {7}\n变体 {8}",
                    animationName,
                    playbackMode,
                    animationLoop ? "是" : "否",
                    sourceAnimationLabel,
                    animationStartTick,
                    animationEndTick,
                    animationIntervalTicks,
                    durationTicks,
                    sequence.FindPropertyRelative("alternateAnimations")
                        ?.arraySize ?? 0);
            }

            bool animationInvalid = string.IsNullOrWhiteSpace(animationName)
                || animationName == "未指定动作"
                || animationStartTick < 0
                || authoredAnimationEndTick < animationStartTick
                || authoredAnimationEndTick > durationTicks
                || animationEndTick < animationStartTick
                || animationEndTick > durationTicks;
            result.Add(new FpgSkillTimelineBlockViewModel
            {
                Kind = FpgSkillTimelineBlockKind.Animation,
                Index = 0,
                StartTick = animationStartTick,
                EndTick = animationEndTick,
                Lane = 0,
                Label = animationLabel,
                Tooltip = animationTooltip,
                Color = new Color(0.24f, 0.53f, 0.78f, 0.92f),
                IsInvalid = animationInvalid,
                MinimumStartTick = 0,
                MaximumEndTick = int.MaxValue,
                CanResize = playbackModeValue
                    == FpgSkillAnimationPlaybackMode.FitInterval,
                AllowSequenceExtension = true
            });

            SerializedProperty phases = sequence.FindPropertyRelative("phases");
            if (phases == null || !phases.isArray)
            {
                return result;
            }

            for (int index = 0; index < phases.arraySize; index++)
            {
                SerializedProperty phase = phases.GetArrayElementAtIndex(index);
                string phaseId = ReadFirstString(phase, "phaseId", "id");
                SerializedProperty kindProperty =
                    phase.FindPropertyRelative("kind");
                FpgSkillPhaseKind phaseKind =
                    kindProperty != null
                    && kindProperty.propertyType == SerializedPropertyType.Enum
                    && Enum.IsDefined(
                        typeof(FpgSkillPhaseKind),
                        kindProperty.enumValueIndex)
                        ? (FpgSkillPhaseKind)kindProperty.enumValueIndex
                        : FpgSkillPhaseKind.None;
                int startTick = ReadRawInt(
                    phase.FindPropertyRelative("startTick"),
                    -1);
                int endTick = ReadRawInt(
                    phase.FindPropertyRelative("endTick"),
                    -1);
                int minimumStartTick = index > 0
                    ? ReadRawInt(
                        phases.GetArrayElementAtIndex(index - 1)
                            .FindPropertyRelative("endTick"),
                        0)
                    : 0;
                int maximumEndTick = index + 1 < phases.arraySize
                    ? ReadRawInt(
                        phases.GetArrayElementAtIndex(index + 1)
                            .FindPropertyRelative("startTick"),
                        durationTicks)
                    : durationTicks;
                minimumStartTick = Mathf.Clamp(
                    minimumStartTick,
                    0,
                    Mathf.Max(0, durationTicks));
                maximumEndTick = Mathf.Clamp(
                    maximumEndTick,
                    0,
                    Mathf.Max(0, durationTicks));
                bool invalid = string.IsNullOrWhiteSpace(phaseId)
                    || phaseKind == FpgSkillPhaseKind.None
                    || startTick < 0
                    || endTick < startTick
                    || endTick > durationTicks
                    || startTick < minimumStartTick
                    || endTick > maximumEndTick;
                string phaseLabel = GetPhaseLabel(phaseKind);
                int phaseDurationTicks = Mathf.Max(0, endTick - startTick);
                result.Add(new FpgSkillTimelineBlockViewModel
                {
                    Kind = FpgSkillTimelineBlockKind.Phase,
                    Index = index,
                    StartTick = startTick,
                    EndTick = endTick,
                    Lane = 1,
                    Label = phaseLabel + " · " + phaseDurationTicks + "帧",
                    Tooltip = string.Format(
                        "{0}阶段 · {1} 帧\nTick {2}-{3} · 可编辑边界 {4}-{5}\n阶段只描述动作节奏，不会直接触发伤害；伤害时机由逻辑事件决定。",
                        phaseLabel,
                        phaseDurationTicks,
                        startTick,
                        endTick,
                        minimumStartTick,
                        maximumEndTick),
                    Color = GetPhaseColor(phaseKind),
                    IsInvalid = invalid,
                    MinimumStartTick = minimumStartTick,
                    MaximumEndTick = maximumEndTick,
                    CanResize = true,
                    AllowSequenceExtension = false
                });
            }

            return result;
        }


        public static string GetSequenceLabel(SerializedProperty sequence, int index)
        {
            string explicitLabel = ReadFirstString(
                sequence,
                "displayName",
                "sequenceName",
                "name",
                "sequenceId");
            if (!string.IsNullOrWhiteSpace(explicitLabel))
            {
                return explicitLabel;
            }

            string kind = ReadDisplayValue(sequence, "kind");
            return string.IsNullOrWhiteSpace(kind)
                ? "序列 " + (index + 1)
                : kind;
        }

        public static List<FpgSkillPayloadRecord> ReadPayloads(
            SerializedProperty sequence)
        {
            SerializedProperty payloads = GetPayloads(sequence);
            List<FpgSkillPayloadRecord> result = new List<FpgSkillPayloadRecord>();
            if (payloads == null || !payloads.isArray)
            {
                return result;
            }

            for (int index = 0; index < payloads.arraySize; index++)
            {
                SerializedProperty payload = payloads.GetArrayElementAtIndex(index);
                string id = ReadFirstString(payload, "slotId", "payloadId", "id");
                string name = ReadFirstString(
                    payload,
                    "displayName",
                    "name",
                    "label");
                string kind = ReadDisplayValue(payload, "kind", "payloadKind", "type");
                FpgSkillPayloadRecord record = new FpgSkillPayloadRecord
                {
                    Index = index,
                    Id = string.IsNullOrWhiteSpace(id) ? "slot-" + index : id,
                    Name = !string.IsNullOrWhiteSpace(name)
                        ? name
                        : !string.IsNullOrWhiteSpace(id)
                            ? id
                            : "载荷 " + (index + 1),
                    Kind = string.IsNullOrWhiteSpace(kind) ? "未分类" : kind,
                    Color = GetPaletteColor(index)
                };
                PopulatePayloadPreview(payload, record);
                result.Add(record);
            }

            PopulatePayloadUseCounts(sequence.serializedObject, result);

            return result;
        }

        public static List<FpgSkillEventRecord> ReadEvents(
            SerializedProperty sequence,
            IList<FpgSkillPayloadRecord> payloads,
            int durationTicks)
        {
            List<FpgSkillEventRecord> result = new List<FpgSkillEventRecord>();
            if (sequence == null)
            {
                return result;
            }

            Dictionary<string, int> payloadIdToIndex = new Dictionary<string, int>(
                StringComparer.Ordinal);
            for (int index = 0; index < payloads.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(payloads[index].Id))
                {
                    payloadIdToIndex[payloads[index].Id] = payloads[index].Index;
                }
            }

            AppendEventRecords(
                result,
                GetEventArray(sequence, FpgSkillEventTrackKind.Generic),
                FpgSkillEventTrackKind.Generic,
                payloads,
                payloadIdToIndex,
                durationTicks);
            AppendEventRecords(
                result,
                GetEventArray(sequence, FpgSkillEventTrackKind.Logic),
                FpgSkillEventTrackKind.Logic,
                payloads,
                payloadIdToIndex,
                durationTicks);
            AppendEventRecords(
                result,
                GetEventArray(sequence, FpgSkillEventTrackKind.Presentation),
                FpgSkillEventTrackKind.Presentation,
                payloads,
                payloadIdToIndex,
                durationTicks);
            AppendEventRecords(
                result,
                GetEventArray(sequence, FpgSkillEventTrackKind.Warning),
                FpgSkillEventTrackKind.Warning,
                payloads,
                payloadIdToIndex,
                durationTicks);

            return result;
        }

        private static void PopulatePayloadPreview(
            SerializedProperty payload,
            FpgSkillPayloadRecord record)
        {
            string kind = record.Kind ?? string.Empty;
            string queryMode = ReadDisplayValue(payload, "queryMode", "queryPolicy");
            string discriminator = kind + " " + queryMode;
            if (ContainsAny(discriminator, "PelletRay", "Pellet Ray", "Ray", "射线"))
            {
                record.PreviewKind = FpgSkillPreviewPayloadKind.PlayerPelletRay;
                record.HitShape = "射线";
                record.PelletCount = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(payload, "pelletCount", "payloadCount"),
                        1));
                record.AdditionalPenetrationCount = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(payload, "additionalPenetrationCount"),
                        0));
                record.MaxHitCount = SaturatingMultiply(
                    record.PelletCount,
                    record.AdditionalPenetrationCount + 1);
            }
            else if (ContainsAny(
                         discriminator,
                         "AreaAtFirstSurface",
                         "Area At First Surface",
                         "Area",
                         "范围"))
            {
                record.PreviewKind =
                    FpgSkillPreviewPayloadKind.PlayerAreaAtFirstSurface;
                record.HitShape = "范围";
                record.AreaCombatantLimit = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(payload, "areaCombatantLimit"),
                        0));
                record.AreaProjectileLimit = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(payload, "areaProjectileLimit"),
                        0));
                record.MaxHitCount = SaturatingAdd(
                    record.AreaCombatantLimit,
                    record.AreaProjectileLimit);
            }
            else if (ContainsAny(discriminator, "Projectile", "弹道", "投射"))
            {
                record.PreviewKind = FpgSkillPreviewPayloadKind.EnemyProjectile;
                record.HitShape = "弹道";
                record.ImpactDelayTicks = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(payload, "projectileFlightTicks"),
                        0));
                record.ProjectileCount = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(payload, "projectileCount"),
                        1));
                record.MaxHitCount = record.ProjectileCount;
            }
            else if (ContainsAny(
                         discriminator,
                         "TimedImpact",
                         "Timed Impact",
                         "延迟"))
            {
                record.PreviewKind = FpgSkillPreviewPayloadKind.EnemyTimedImpact;
                record.HitShape = "延迟命中";
                record.ImpactDelayTicks = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(payload, "timedImpactDelayTicks"),
                        0));
                record.MaxHitCount = 1;
            }
            else if (ContainsAny(discriminator, "Summon", "召唤"))
            {
                record.PreviewKind = FpgSkillPreviewPayloadKind.EnemySummon;
                record.HitShape = "召唤";
                SerializedProperty candidates = FindFirstRelative(
                    payload,
                    "summonCandidates");
                record.SummonCandidateCount = candidates != null
                    && candidates.isArray
                        ? candidates.arraySize
                        : 0;
                record.MaxHitCount = record.SummonCandidateCount;
            }
            else if (ContainsAny(discriminator, "Reload", "装填"))
            {
                record.PreviewKind = FpgSkillPreviewPayloadKind.PlayerReload;
                record.HitShape = "装填";
                record.MaxHitCount = 0;
            }
            else
            {
                record.PreviewKind = FpgSkillPreviewPayloadKind.Unknown;
                record.HitShape = string.IsNullOrWhiteSpace(kind)
                    ? "未分类"
                    : kind;
                record.ImpactDelayTicks = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(
                            payload,
                            "impactDelayTicks",
                            "delayTicks"),
                        0));
                record.MaxHitCount = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(
                            payload,
                            "maxHitCount",
                            "maxImpactCount"),
                        0));
            }

            SerializedProperty baseDamage = FindFirstRelative(
                payload,
                "baseDamage",
                "damage");
            SerializedProperty breakDamage = FindFirstRelative(
                payload,
                "breakDamage");
            record.HasDamagePreview = baseDamage != null || breakDamage != null;
            record.BaseDamage = Mathf.Max(0, ReadRawInt(baseDamage, 0));
            record.BreakDamage = Mathf.Max(0, ReadRawInt(breakDamage, 0));
            int weakpointDamageBasisPoints = Mathf.Max(
                0,
                ReadRawInt(
                    FindFirstRelative(
                        payload,
                        "weakpointDamageMultiplierBasisPoints"),
                    10000));
            int weakpointBreakBasisPoints = Mathf.Max(
                0,
                ReadRawInt(
                    FindFirstRelative(
                        payload,
                        "weakpointBreakMultiplierBasisPoints"),
                    10000));
            record.WeakpointDamage = RoundBasisPoints(
                record.BaseDamage,
                weakpointDamageBasisPoints);
            record.WeakpointBreakDamage = RoundBasisPoints(
                record.BreakDamage,
                weakpointBreakBasisPoints);
        }

        private static int SaturatingMultiply(int left, int right)
        {
            long value = (long)Mathf.Max(0, left) * Mathf.Max(0, right);
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static int SaturatingAdd(int left, int right)
        {
            long value = (long)Mathf.Max(0, left) + Mathf.Max(0, right);
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static int RoundBasisPoints(int value, int basisPoints)
        {
            if (value <= 0 || basisPoints <= 0)
            {
                return 0;
            }

            long scaled = (long)value * basisPoints + 5000L;
            long rounded = scaled / 10000L;
            return rounded > int.MaxValue ? int.MaxValue : (int)rounded;
        }

        private static void PopulatePayloadUseCounts(
            SerializedObject serializedObject,
            IList<FpgSkillPayloadRecord> payloads)
        {
            if (serializedObject == null || payloads == null || payloads.Count == 0)
            {
                return;
            }

            Dictionary<string, int> payloadIdToIndex = new Dictionary<string, int>(
                StringComparer.Ordinal);
            for (int index = 0; index < payloads.Count; index++)
            {
                payloads[index].UseCount = 0;
                if (!string.IsNullOrWhiteSpace(payloads[index].Id))
                {
                    payloadIdToIndex[payloads[index].Id] = index;
                }
            }

            SerializedProperty sequences = GetSequences(serializedObject);
            if (sequences == null || !sequences.isArray)
            {
                return;
            }

            for (int sequenceIndex = 0;
                sequenceIndex < sequences.arraySize;
                sequenceIndex++)
            {
                SerializedProperty sequence =
                    sequences.GetArrayElementAtIndex(sequenceIndex);
                CountPayloadUses(
                    GetEventArray(sequence, FpgSkillEventTrackKind.Generic),
                    payloads,
                    payloadIdToIndex);
                CountPayloadUses(
                    GetEventArray(sequence, FpgSkillEventTrackKind.Logic),
                    payloads,
                    payloadIdToIndex);
            }
        }

        private static void CountPayloadUses(
            SerializedProperty eventArray,
            IList<FpgSkillPayloadRecord> payloads,
            IReadOnlyDictionary<string, int> payloadIdToIndex)
        {
            if (eventArray == null || !eventArray.isArray)
            {
                return;
            }

            for (int index = 0; index < eventArray.arraySize; index++)
            {
                SerializedProperty eventProperty =
                    eventArray.GetArrayElementAtIndex(index);
                int payloadIndex = ResolvePayloadIndex(
                    eventProperty,
                    payloads,
                    payloadIdToIndex);
                if (payloadIndex >= 0 && payloadIndex < payloads.Count)
                {
                    payloads[payloadIndex].UseCount++;
                }
            }
        }

        private static int ResolvePayloadIndex(
            SerializedProperty eventProperty,
            IList<FpgSkillPayloadRecord> payloads,
            IReadOnlyDictionary<string, int> payloadIdToIndex)
        {
            string payloadId = ReadFirstString(
                eventProperty,
                PayloadIdNames);
            if (!string.IsNullOrWhiteSpace(payloadId))
            {
                return payloadIdToIndex != null
                    && payloadIdToIndex.TryGetValue(
                        payloadId,
                        out int resolvedIndex)
                    && resolvedIndex >= 0
                    && resolvedIndex < payloads.Count
                        ? resolvedIndex
                        : -1;
            }

            int payloadIndex = ReadRawInt(
                FindFirstRelative(eventProperty, PayloadIndexNames),
                -1);
            return payloadIndex >= 0 && payloadIndex < payloads.Count
                ? payloadIndex
                : -1;
        }

        private static void AppendEventRecords(
            ICollection<FpgSkillEventRecord> result,
            SerializedProperty eventArray,
            FpgSkillEventTrackKind track,
            IList<FpgSkillPayloadRecord> payloads,
            IReadOnlyDictionary<string, int> payloadIdToIndex,
            int durationTicks)
        {
            if (eventArray == null || !eventArray.isArray)
            {
                return;
            }

            for (int arrayIndex = 0; arrayIndex < eventArray.arraySize; arrayIndex++)
            {
                SerializedProperty eventProperty =
                    eventArray.GetArrayElementAtIndex(arrayIndex);
                SerializedProperty tickProperty = track == FpgSkillEventTrackKind.Warning
                    ? eventProperty.FindPropertyRelative("startTick")
                    : FindFirstRelative(eventProperty, TickNames);
                int tick = ReadRawInt(tickProperty, 0);
                int eventDuration;
                if (track == FpgSkillEventTrackKind.Warning)
                {
                    int endTick = ReadRawInt(
                        eventProperty.FindPropertyRelative("endTick"),
                        tick);
                    eventDuration = endTick - tick;
                }
                else
                {
                    eventDuration = ReadRawInt(
                        FindFirstRelative(eventProperty, EventDurationNames),
                        0);
                }

                int authoredOrdinal = ReadRawInt(
                    FindFirstRelative(eventProperty, AuthoredOrdinalNames),
                    arrayIndex);
                int payloadIndex = ResolvePayloadIndex(
                    eventProperty,
                    payloads,
                    payloadIdToIndex);
                string payloadId = ReadFirstString(
                    eventProperty,
                    PayloadIdNames);

                string eventId = ReadFirstString(eventProperty, "eventId", "id");
                string socketId = ReadFirstString(
                    eventProperty,
                    "socketId",
                    "socket");
                FpgSkillTargetSource targetSource = ReadTargetSource(
                    eventProperty);
                Vector3 targetOffset = ReadVector3(
                    FindFirstRelative(eventProperty, "targetOffset"),
                    Vector3.zero);
                string kind = GetTrackLabel(track, eventProperty);
                string name = ReadFirstString(
                    eventProperty,
                    "displayName",
                    "name",
                    "label");
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = BuildEventName(
                        eventProperty,
                        track,
                        arrayIndex,
                        eventId,
                        payloadIndex,
                        payloads);
                }

                bool requiresPayload = track == FpgSkillEventTrackKind.Logic
                    || track == FpgSkillEventTrackKind.Generic
                        && (!string.IsNullOrWhiteSpace(payloadId)
                            || FindFirstRelative(eventProperty, PayloadIndexNames) != null);
                Color color = payloadIndex >= 0 && payloadIndex < payloads.Count
                    ? payloads[payloadIndex].Color
                    : GetTrackColor(track, arrayIndex);
                bool invalid = tick < 0
                    || tick > durationTicks
                    || eventDuration < 0
                    || tick + eventDuration > durationTicks
                    || authoredOrdinal < 0
                    || requiresPayload
                        && (payloadIndex < 0 || payloadIndex >= payloads.Count);
                result.Add(new FpgSkillEventRecord
                {
                    Index = MakeEventKey(track, arrayIndex),
                    ArrayIndex = arrayIndex,
                    Tick = tick,
                    DurationTicks = eventDuration,
                    AuthoredOrdinal = authoredOrdinal,
                    PayloadIndex = payloadIndex,
                    EventId = eventId,
                    PayloadId = payloadId,
                    SocketId = socketId,
                    TargetSource = targetSource,
                    TargetOffset = targetOffset,
                    PayloadPreview = payloadIndex >= 0
                        && payloadIndex < payloads.Count
                            ? payloads[payloadIndex].BuildPreviewSummary(tick)
                            : string.Empty,
                    Name = name,
                    Kind = kind,
                    Track = track,
                    IsInvalid = invalid,
                    Color = color
                });
            }
        }

        public static List<FpgSkillValidationItem> Validate(
            SerializedObject serializedObject,
            int sequenceIndex,
            IList<FpgSkillPayloadRecord> payloads,
            IList<FpgSkillEventRecord> events,
            int durationTicks,
            int actualAnimationDurationTicks = -1,
            GameObject previewPrefab = null)
        {
            List<FpgSkillValidationItem> result = new List<FpgSkillValidationItem>();
            if (serializedObject == null || serializedObject.targetObject == null)
            {
                result.Add(Error("未选择有效的动作资产。"));
                return result;
            }

            if (string.IsNullOrWhiteSpace(ReadString(
                    serializedObject.FindProperty("skillId"),
                    string.Empty)))
            {
                result.Add(Error("动作缺少 skillId。"));
            }

            if (string.IsNullOrWhiteSpace(ReadString(
                    serializedObject.FindProperty("displayName"),
                    string.Empty)))
            {
                result.Add(Warning("动作缺少 displayName。"));
            }

            SerializedProperty sequences = GetSequences(serializedObject);
            if (sequences == null || !sequences.isArray || sequences.arraySize == 0)
            {
                result.Add(Error("动作至少需要一个 sequences 元素。"));
                return result;
            }

            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            if (sequence == null)
            {
                result.Add(Error("当前序列无法读取。"));
                return result;
            }

            if (durationTicks < 0)
            {
                result.Add(Error("序列时长不能小于 0 Tick。"));
            }

            if (GetPayloads(sequence) == null)
            {
                result.Add(Warning("当前序列尚未提供 payloadSlots/payloads 字段。"));
            }

            if (!HasAnyEventArray(sequence))
            {
                result.Add(Warning("当前序列尚未提供可编辑的事件轨道。"));
            }

            int sourceAnimationDurationTicks =
                GetAnimationSourceDurationTicks(sequence);
            if (actualAnimationDurationTicks > 0
                && sourceAnimationDurationTicks <= 0)
            {
                result.Add(Warning(
                    "源动画长度基准尚未初始化；当前预览实测为 "
                    + actualAnimationDurationTicks
                    + " Tick。编辑器不会移动任何逻辑事件。"));
            }
            else if (actualAnimationDurationTicks > 0
                     && sourceAnimationDurationTicks
                        != actualAnimationDurationTicks)
            {
                result.Add(Warning(
                    "源动画长度发生变化：基准 "
                    + sourceAnimationDurationTicks
                    + " Tick，当前预览实测 "
                    + actualAnimationDurationTicks
                    + " Tick。编辑器不会移动任何逻辑事件。"));
            }

            int effectiveSourceAnimationTicks =
                actualAnimationDurationTicks > 0
                    ? actualAnimationDurationTicks
                    : sourceAnimationDurationTicks;
            if (GetAnimationPlaybackModeValue(sequence)
                    == FpgSkillAnimationPlaybackMode.NaturalSpeed
                && !GetAnimationLoop(sequence)
                && effectiveSourceAnimationTicks > 0)
            {
                int animationStartTick = GetAnimationStartTick(sequence);
                long completeAnimationEndTick =
                    (long)animationStartTick + effectiveSourceAnimationTicks;
                if (completeAnimationEndTick > durationTicks)
                {
                    result.Add(Warning(
                        "自然速度动画完整片段 Tick "
                        + animationStartTick
                        + "-"
                        + Math.Min(int.MaxValue, completeAnimationEndTick)
                        + " 超出当前序列截止 Tick "
                        + durationTicks
                        + "。编辑器不会静默修改资产；拖动动画节点或使用延长操作可显式扩展序列，逻辑事件 Tick 保持不变。"));
                }
            }

            for (int index = 0; index < payloads.Count; index++)
            {
                FpgSkillPayloadRecord payload = payloads[index];
                if (payload.UseCount == 0)
                {
                    result.Add(new FpgSkillValidationItem
                    {
                        Severity = FpgSkillIssueSeverity.Warning,
                        Message = "载荷槽“" + payload.Name + "”未被任何事件引用。",
                        PayloadIndex = payload.Index
                    });
                }
            }

            for (int index = 0; index < events.Count; index++)
            {
                FpgSkillEventRecord eventRecord = events[index];
                if (eventRecord.Tick < 0 || eventRecord.Tick > durationTicks)
                {
                    result.Add(new FpgSkillValidationItem
                    {
                        Severity = FpgSkillIssueSeverity.Error,
                        Message = "事件“" + eventRecord.Name + "”超出序列时长。",
                        EventIndex = eventRecord.Index,
                        Tick = eventRecord.Tick
                    });
                }

                if (eventRecord.DurationTicks < 0
                    || eventRecord.Tick + eventRecord.DurationTicks > durationTicks)
                {
                    result.Add(new FpgSkillValidationItem
                    {
                        Severity = FpgSkillIssueSeverity.Error,
                        Message = "事件“" + eventRecord.Name + "”的有效区间非法。",
                        EventIndex = eventRecord.Index,
                        Tick = eventRecord.Tick
                    });
                }

                bool requiresPayload = eventRecord.Track == FpgSkillEventTrackKind.Logic
                    || eventRecord.Track == FpgSkillEventTrackKind.Generic
                        && !string.IsNullOrWhiteSpace(eventRecord.PayloadId);
                if (requiresPayload
                    && (eventRecord.PayloadIndex < 0
                        || eventRecord.PayloadIndex >= payloads.Count))
                {
                    result.Add(new FpgSkillValidationItem
                    {
                        Severity = FpgSkillIssueSeverity.Error,
                        Message = "事件“" + eventRecord.Name + "”没有可解析的载荷槽。",
                        EventIndex = eventRecord.Index,
                        Tick = eventRecord.Tick
                    });
                }
            }

            AppendAuthoredPositionValidation(result, events);
            AppendPreviewPrefabValidation(
                result,
                sequence,
                events,
                previewPrefab);
            AppendRuntimeValidation(result, serializedObject, events);

            if (result.Count == 0)
            {
                result.Add(Info("当前序列通过编辑器基础校验。"));
            }

            return result;
        }

        public static bool TryReadCompiledSchedule(
            SerializedObject serializedObject,
            int sequenceIndex,
            IList<FpgSkillEventRecord> authoredEvents,
            ICollection<FpgSkillCompiledTriggerRecord> result,
            out FpgCompiledSkillSequence compiledSequence,
            out string error)
        {
            compiledSequence = default(FpgCompiledSkillSequence);
            error = string.Empty;
            if (serializedObject == null
                || serializedObject.targetObject == null
                || result == null)
            {
                error = "没有可编译的技能资产。";
                return false;
            }

            try
            {
                System.Reflection.MethodInfo compileMethod =
                    FindRuntimeCompileMethod(serializedObject.targetObject.GetType());
                if (compileMethod == null)
                {
                    error = "技能资产没有公开的 TryCompile 接口。";
                    return false;
                }

                object[] arguments = { null, null };
                bool success = (bool)compileMethod.Invoke(
                    serializedObject.targetObject,
                    arguments);
                if (!success || arguments[0] == null)
                {
                    error = arguments[1] as string ?? "运行时技能编译失败。";
                    return false;
                }

                FpgCompiledSkillDefinition compiledTimeline =
                    ExtractCompiledTimeline(arguments[0])
                        as FpgCompiledSkillDefinition;
                SerializedProperty authoredSequence = GetSequence(
                    serializedObject,
                    sequenceIndex);
                SerializedProperty kindProperty = authoredSequence
                    ?.FindPropertyRelative("kind");
                if (compiledTimeline == null
                    || kindProperty == null
                    || kindProperty.propertyType != SerializedPropertyType.Enum
                    || !Enum.IsDefined(
                        typeof(FpgSkillSequenceKind),
                        kindProperty.enumValueIndex)
                    || !compiledTimeline.TryGetSequence(
                        (FpgSkillSequenceKind)kindProperty.enumValueIndex,
                        out compiledSequence))
                {
                    error = "编译结果中找不到当前序列。";
                    return false;
                }

                for (int index = 0;
                    index < compiledSequence.EventCount;
                    index++)
                {
                    FpgCompiledSkillEvent compiledEvent =
                        compiledSequence.GetEvent(index);
                    string kind = compiledEvent.Kind.ToString();
                    FpgSkillEventRecord authored = FindAuthoredEvent(
                        authoredEvents,
                        kind,
                        compiledEvent.Tick,
                        compiledEvent.SortOrder);
                    result.Add(new FpgSkillCompiledTriggerRecord
                    {
                        CompiledEventId = compiledEvent.EventId,
                        Tick = compiledEvent.Tick,
                        AuthoredOrdinal = compiledEvent.SortOrder,
                        EventIndex = authored?.Index ?? -1,
                        Kind = GetCompiledKindLabel(kind),
                        Name = authored?.Name ?? kind,
                        CompiledEvent = compiledEvent
                    });
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                return false;
            }
        }



        public static SerializedProperty GetPayloadProperty(
            SerializedObject serializedObject,
            int sequenceIndex,
            int payloadIndex)
        {
            SerializedProperty payloads = GetPayloads(GetSequence(serializedObject, sequenceIndex));
            return payloads != null
                && payloads.isArray
                && payloadIndex >= 0
                && payloadIndex < payloads.arraySize
                    ? payloads.GetArrayElementAtIndex(payloadIndex)
                    : null;
        }

        public static SerializedProperty GetPhaseProperty(
            SerializedObject serializedObject,
            int sequenceIndex,
            int phaseIndex)
        {
            SerializedProperty sequence = GetSequence(
                serializedObject,
                sequenceIndex);
            SerializedProperty phases = sequence?.FindPropertyRelative("phases");
            return phases != null
                && phases.isArray
                && phaseIndex >= 0
                && phaseIndex < phases.arraySize
                    ? phases.GetArrayElementAtIndex(phaseIndex)
                    : null;
        }


        public static SerializedProperty GetEventProperty(
            SerializedObject serializedObject,
            int sequenceIndex,
            int eventIndex)
        {
            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            DecodeEventKey(
                eventIndex,
                out FpgSkillEventTrackKind track,
                out int arrayIndex);
            SerializedProperty events = GetEventArray(sequence, track);
            return events != null
                && events.isArray
                && arrayIndex >= 0
                && arrayIndex < events.arraySize
                    ? events.GetArrayElementAtIndex(arrayIndex)
                    : null;
        }

        public static bool CanAddEventTrack(
            SerializedProperty sequence,
            FpgSkillEventTrackKind track)
        {
            SerializedProperty array = GetEventArray(sequence, track);
            return array != null && array.isArray;
        }

        public static bool SetEventTick(
            SerializedObject serializedObject,
            int sequenceIndex,
            int eventIndex,
            int tick)
        {
            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            SerializedProperty eventProperty = GetEventProperty(
                serializedObject,
                sequenceIndex,
                eventIndex);
            DecodeEventKey(
                eventIndex,
                out FpgSkillEventTrackKind track,
                out _);
            SerializedProperty tickProperty = track == FpgSkillEventTrackKind.Warning
                ? eventProperty?.FindPropertyRelative("startTick")
                : FindFirstRelative(eventProperty, TickNames);
            if (tickProperty == null || tickProperty.propertyType != SerializedPropertyType.Integer)
            {
                return false;
            }

            Undo.RecordObject(serializedObject.targetObject, "移动技能事件");
            int normalizedTick = Mathf.Max(0, tick);
            if (track == FpgSkillEventTrackKind.Warning)
            {
                SerializedProperty endProperty =
                    eventProperty.FindPropertyRelative("endTick");
                int duration = endProperty == null
                    ? 0
                    : Mathf.Max(0, endProperty.intValue - tickProperty.intValue);
                int maximumStart = Mathf.Max(0, GetDurationTicks(sequence) - duration);
                normalizedTick = Mathf.Clamp(normalizedTick, 0, maximumStart);
                if (endProperty != null
                    && endProperty.propertyType == SerializedPropertyType.Integer)
                {
                    endProperty.intValue = normalizedTick + duration;
                }
            }

            tickProperty.intValue = normalizedTick;
            Apply(serializedObject);
            return true;
        }

        public static bool SetEventPayloadReference(
            SerializedObject serializedObject,
            int sequenceIndex,
            int eventIndex,
            int payloadIndex)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null)
            {
                return false;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty sequence = GetSequence(
                serializedObject,
                sequenceIndex);
            List<FpgSkillPayloadRecord> authoredPayloads =
                ReadPayloads(sequence);
            FpgSkillPayloadRecord payload = authoredPayloads.Find(item =>
                item.Index == payloadIndex);
            DecodeEventKey(
                eventIndex,
                out FpgSkillEventTrackKind track,
                out _);
            SerializedProperty eventProperty = GetEventProperty(
                serializedObject,
                sequenceIndex,
                eventIndex);
            if (payload == null
                || eventProperty == null
                || (track != FpgSkillEventTrackKind.Logic
                    && track != FpgSkillEventTrackKind.Generic))
            {
                return false;
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                "替换事件载荷");
            WriteInt(
                eventProperty,
                payload.Index,
                PayloadIndexNames);
            WriteString(
                eventProperty,
                payload.Id,
                PayloadIdNames);
            SetDefaultEventTargetSource(eventProperty, payload);
            Apply(serializedObject);
            return true;
        }


        public static int AddEvent(
            SerializedObject serializedObject,
            int sequenceIndex,
            int tick,
            FpgSkillPayloadRecord payload)
        {
            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            return AddEvent(
                serializedObject,
                sequenceIndex,
                tick,
                payload,
                GetDefaultEventTrack(sequence));
        }

        public static int AddEvent(
            SerializedObject serializedObject,
            int sequenceIndex,
            int tick,
            FpgSkillPayloadRecord payload,
            FpgSkillEventTrackKind track,
            int eventDurationTicks = 0)
        {
            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            SerializedProperty eventArray = GetEventArray(sequence, track);
            if (eventArray == null || !eventArray.isArray)
            {
                return -1;
            }

            Undo.RecordObject(serializedObject.targetObject, "添加技能事件");
            int index = eventArray.arraySize;
            eventArray.InsertArrayElementAtIndex(index);
            SerializedProperty eventProperty = eventArray.GetArrayElementAtIndex(index);
            ResetProperty(eventProperty);
            SetDefaultEventTargetSource(eventProperty, payload);
            int normalizedTick = Mathf.Clamp(tick, 0, GetDurationTicks(sequence));
            if (track == FpgSkillEventTrackKind.Warning)
            {
                WriteInt(eventProperty, normalizedTick, "startTick");
                WriteInt(
                    eventProperty,
                    Mathf.Min(
                        GetDurationTicks(sequence),
                        normalizedTick + Mathf.Max(1, eventDurationTicks)),
                    "endTick");
            }
            else
            {
                WriteInt(eventProperty, normalizedTick, TickNames);
                if (eventDurationTicks > 0)
                {
                    WriteInt(
                        eventProperty,
                        eventDurationTicks,
                        EventDurationNames);
                }
            }

            WriteInt(
                eventProperty,
                FindNextAuthoredOrdinal(sequence, MakeEventKey(track, index)),
                AuthoredOrdinalNames);
            string uniqueSuffix = Guid.NewGuid().ToString("N");
            WriteString(eventProperty, "event." + uniqueSuffix, "eventId", "id");
            WriteString(eventProperty, "攻击事件 " + (index + 1), "displayName", "name", "label");
            if (track == FpgSkillEventTrackKind.Logic
                || track == FpgSkillEventTrackKind.Generic)
            {
                if (payload != null)
                {
                    WriteInt(eventProperty, payload.Index, PayloadIndexNames);
                    WriteString(eventProperty, payload.Id, PayloadIdNames);
                }
            }
            else if (track == FpgSkillEventTrackKind.Presentation)
            {
                WriteString(eventProperty, "cue." + uniqueSuffix, "cueId");
            }
            else if (track == FpgSkillEventTrackKind.Warning)
            {
                WriteString(eventProperty, "warning." + uniqueSuffix, "warningId");
            }

            Apply(serializedObject);
            return MakeEventKey(track, index);
        }

        public static int DuplicateEvent(
            SerializedObject serializedObject,
            int sequenceIndex,
            int eventIndex,
            int durationTicks)
        {
            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            DecodeEventKey(
                eventIndex,
                out FpgSkillEventTrackKind track,
                out int arrayIndex);
            SerializedProperty eventArray = GetEventArray(sequence, track);
            if (eventArray == null
                || !eventArray.isArray
                || arrayIndex < 0
                || arrayIndex >= eventArray.arraySize)
            {
                return -1;
            }

            Undo.RecordObject(serializedObject.targetObject, "复制技能事件");
            int insertionIndex = arrayIndex + 1;
            eventArray.InsertArrayElementAtIndex(insertionIndex);
            SerializedProperty copy = eventArray.GetArrayElementAtIndex(insertionIndex);
            if (track == FpgSkillEventTrackKind.Warning)
            {
                int startTick = ReadRawInt(copy.FindPropertyRelative("startTick"), 0);
                int endTick = ReadRawInt(copy.FindPropertyRelative("endTick"), startTick);
                int duration = Mathf.Max(0, endTick - startTick);
                int nextStart = Mathf.Min(
                    Mathf.Max(0, durationTicks - duration),
                    startTick + 1);
                WriteInt(copy, nextStart, "startTick");
                WriteInt(copy, nextStart + duration, "endTick");
            }
            else
            {
                int eventTick = ReadRawInt(FindFirstRelative(copy, TickNames), 0);
                WriteInt(copy, Mathf.Min(durationTicks, eventTick + 1), TickNames);
            }

            WriteInt(
                copy,
                FindNextAuthoredOrdinal(
                    sequence,
                    MakeEventKey(track, insertionIndex)),
                AuthoredOrdinalNames);
            WriteString(copy, "event." + Guid.NewGuid().ToString("N"), "eventId", "id");
            Apply(serializedObject);
            return MakeEventKey(track, insertionIndex);
        }

        public static bool CopyEvents(
            SerializedObject serializedObject,
            int sequenceIndex,
            IEnumerable<int> eventIndices,
            FpgSkillEventClipboard clipboard)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null
                || eventIndices == null
                || clipboard == null)
            {
                return false;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            List<FpgSkillPayloadRecord> payloads = ReadPayloads(sequence);
            List<FpgSkillEventRecord> authored = ReadEvents(
                sequence,
                payloads,
                GetDurationTicks(sequence));
            HashSet<int> selected = new HashSet<int>(eventIndices);
            List<FpgSkillEventRecord> copied = new List<FpgSkillEventRecord>();
            for (int index = 0; index < authored.Count; index++)
            {
                if (selected.Contains(authored[index].Index))
                {
                    copied.Add(authored[index]);
                }
            }

            copied.Sort(CompareAuthoredEvents);
            if (copied.Count == 0)
            {
                clipboard.Clear();
                return false;
            }

            int minimumTick = copied[0].Tick;
            int minimumOrdinal = copied[0].AuthoredOrdinal;
            int maximumEndTick = copied[0].Tick + copied[0].DurationTicks;
            for (int index = 1; index < copied.Count; index++)
            {
                minimumTick = Mathf.Min(minimumTick, copied[index].Tick);
                minimumOrdinal = Mathf.Min(
                    minimumOrdinal,
                    copied[index].AuthoredOrdinal);
                maximumEndTick = Mathf.Max(
                    maximumEndTick,
                    copied[index].Tick + copied[index].DurationTicks);
            }

            List<FpgSkillEventClipboardItem> items =
                new List<FpgSkillEventClipboardItem>(copied.Count);
            for (int index = 0; index < copied.Count; index++)
            {
                FpgSkillEventRecord record = copied[index];
                SerializedProperty property = GetEventProperty(
                    serializedObject,
                    sequenceIndex,
                    record.Index);
                if (property == null)
                {
                    continue;
                }

                items.Add(new FpgSkillEventClipboardItem
                {
                    Track = record.Track,
                    RelativeTick = record.Tick - minimumTick,
                    DurationTicks = record.DurationTicks,
                    RelativeAuthoredOrdinal =
                        record.AuthoredOrdinal - minimumOrdinal,
                    SourceEventId = record.EventId,
                    Snapshot = FpgSerializedPropertySnapshot.Capture(property)
                });
            }

            clipboard.Set(items, maximumEndTick - minimumTick);
            return !clipboard.IsEmpty;
        }

        public static List<int> PasteEvents(
            SerializedObject serializedObject,
            int sequenceIndex,
            FpgSkillEventClipboard clipboard,
            int anchorTick)
        {
            List<int> result = new List<int>();
            if (serializedObject == null
                || serializedObject.targetObject == null
                || clipboard == null
                || clipboard.IsEmpty)
            {
                return result;
            }

            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            int durationTicks = GetDurationTicks(sequence);
            int maximumAnchor = Mathf.Max(0, durationTicks - clipboard.TickSpan);
            int normalizedAnchor = Mathf.Clamp(anchorTick, 0, maximumAnchor);
            int nextOrdinal = FindNextAuthoredOrdinal(sequence, -1);
            int maximumRelativeOrdinal = 0;
            for (int index = 0; index < clipboard.Items.Count; index++)
            {
                maximumRelativeOrdinal = Mathf.Max(
                    maximumRelativeOrdinal,
                    clipboard.Items[index].RelativeAuthoredOrdinal);
            }

            if ((long)nextOrdinal + maximumRelativeOrdinal > int.MaxValue)
            {
                return result;
            }

            for (int index = 0; index < clipboard.Items.Count; index++)
            {
                if (GetEventArray(sequence, clipboard.Items[index].Track) == null)
                {
                    return result;
                }
            }

            Undo.RecordObject(serializedObject.targetObject, "粘贴技能事件");
            for (int index = 0; index < clipboard.Items.Count; index++)
            {
                FpgSkillEventClipboardItem item = clipboard.Items[index];
                SerializedProperty eventArray = GetEventArray(sequence, item.Track);
                int arrayIndex = eventArray.arraySize;
                eventArray.InsertArrayElementAtIndex(arrayIndex);
                SerializedProperty copy =
                    eventArray.GetArrayElementAtIndex(arrayIndex);
                ResetProperty(copy);
                item.Snapshot?.ApplyTo(copy);

                int tick = normalizedAnchor + item.RelativeTick;
                if (item.Track == FpgSkillEventTrackKind.Warning)
                {
                    WriteInt(copy, tick, "startTick");
                    WriteInt(
                        copy,
                        Mathf.Min(durationTicks, tick + item.DurationTicks),
                        "endTick");
                }
                else
                {
                    WriteInt(copy, tick, TickNames);
                }

                WriteInt(
                    copy,
                    nextOrdinal + item.RelativeAuthoredOrdinal,
                    AuthoredOrdinalNames);
                WriteString(
                    copy,
                    "event." + Guid.NewGuid().ToString("N"),
                    "eventId",
                    "id");
                result.Add(MakeEventKey(item.Track, arrayIndex));
            }

            Apply(serializedObject);
            return result;
        }

        public static bool MoveEventsByDelta(
            SerializedObject serializedObject,
            int sequenceIndex,
            IEnumerable<int> eventIndices,
            int requestedDeltaTicks,
            out int appliedDeltaTicks)
        {
            appliedDeltaTicks = 0;
            if (serializedObject == null
                || serializedObject.targetObject == null
                || eventIndices == null)
            {
                return false;
            }

            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            int durationTicks = GetDurationTicks(sequence);
            List<FpgSkillPayloadRecord> payloads = ReadPayloads(sequence);
            List<FpgSkillEventRecord> authored = ReadEvents(
                sequence,
                payloads,
                durationTicks);
            HashSet<int> selected = new HashSet<int>(eventIndices);
            List<FpgSkillEventRecord> moving = new List<FpgSkillEventRecord>();
            int minimumDelta = int.MinValue;
            int maximumDelta = int.MaxValue;
            for (int index = 0; index < authored.Count; index++)
            {
                FpgSkillEventRecord record = authored[index];
                if (!selected.Contains(record.Index))
                {
                    continue;
                }

                moving.Add(record);
                minimumDelta = Mathf.Max(minimumDelta, -record.Tick);
                maximumDelta = Mathf.Min(
                    maximumDelta,
                    durationTicks - record.Tick - record.DurationTicks);
            }

            if (moving.Count == 0)
            {
                return false;
            }

            appliedDeltaTicks = Mathf.Clamp(
                requestedDeltaTicks,
                minimumDelta,
                maximumDelta);
            if (appliedDeltaTicks == 0)
            {
                return true;
            }

            Undo.RecordObject(serializedObject.targetObject, "移动技能事件");
            for (int index = 0; index < moving.Count; index++)
            {
                FpgSkillEventRecord record = moving[index];
                SerializedProperty property = GetEventProperty(
                    serializedObject,
                    sequenceIndex,
                    record.Index);
                int nextTick = record.Tick + appliedDeltaTicks;
                if (record.Track == FpgSkillEventTrackKind.Warning)
                {
                    WriteInt(property, nextTick, "startTick");
                    WriteInt(
                        property,
                        nextTick + record.DurationTicks,
                        "endTick");
                }
                else
                {
                    WriteInt(property, nextTick, TickNames);
                }
            }

            Apply(serializedObject);
            return true;
        }

        public static bool MoveTimelineBlockByDelta(
            SerializedObject serializedObject,
            int sequenceIndex,
            FpgSkillTimelineBlockKind kind,
            int blockIndex,
            int requestedDeltaTicks,
            out int appliedDeltaTicks)
        {
            appliedDeltaTicks = 0;
            if (serializedObject == null
                || serializedObject.targetObject == null)
            {
                return false;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty sequence = GetSequence(
                serializedObject,
                sequenceIndex);
            int startTick;
            int endTick;
            if (kind == FpgSkillTimelineBlockKind.Animation)
            {
                if (blockIndex != 0 || sequence == null)
                {
                    return false;
                }

                startTick = GetAnimationStartTick(sequence);
                endTick = GetAnimationEndTick(sequence);
            }
            else if (kind == FpgSkillTimelineBlockKind.Phase)
            {
                SerializedProperty phase = GetPhaseProperty(
                    serializedObject,
                    sequenceIndex,
                    blockIndex);
                startTick = ReadRawInt(
                    phase?.FindPropertyRelative("startTick"),
                    -1);
                endTick = ReadRawInt(
                    phase?.FindPropertyRelative("endTick"),
                    -1);
            }
            else
            {
                return false;
            }

            if (startTick < 0 || endTick < startTick)
            {
                return false;
            }

            long requestedStart = (long)startTick + requestedDeltaTicks;
            long requestedEnd = (long)endTick + requestedDeltaTicks;
            int clampedRequestedStart = (int)Math.Max(
                int.MinValue,
                Math.Min(int.MaxValue, requestedStart));
            int clampedRequestedEnd = (int)Math.Max(
                int.MinValue,
                Math.Min(int.MaxValue, requestedEnd));
            if (!EditTimelineBlockRange(
                    serializedObject,
                    sequenceIndex,
                    kind,
                    blockIndex,
                    FpgSkillTimelineBlockEditMode.Move,
                    clampedRequestedStart,
                    clampedRequestedEnd,
                    out int appliedStartTick,
                    out _))
            {
                return false;
            }

            appliedDeltaTicks = appliedStartTick - startTick;
            return true;
        }


        public static bool MoveEventOrder(
            SerializedObject serializedObject,
            int sequenceIndex,
            int eventIndex,
            int requestedDelta)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null
                || requestedDelta == 0)
            {
                return false;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty sequence = GetSequence(
                serializedObject,
                sequenceIndex);
            int durationTicks = GetDurationTicks(sequence);
            List<FpgSkillPayloadRecord> payloads = ReadPayloads(sequence);
            List<FpgSkillEventRecord> authored = ReadEvents(
                sequence,
                payloads,
                durationTicks);
            FpgSkillEventRecord moving = authored.Find(item =>
                item.Index == eventIndex);
            if (moving == null)
            {
                return false;
            }

            List<FpgSkillEventRecord> sameTick = authored.FindAll(item =>
                item.Tick == moving.Tick);
            sameTick.Sort((left, right) =>
            {
                int orderComparison = left.AuthoredOrdinal.CompareTo(
                    right.AuthoredOrdinal);
                return orderComparison != 0
                    ? orderComparison
                    : left.Index.CompareTo(right.Index);
            });
            int currentIndex = sameTick.FindIndex(item =>
                item.Index == eventIndex);
            int targetIndex = Mathf.Clamp(
                currentIndex + requestedDelta,
                0,
                sameTick.Count - 1);
            if (currentIndex < 0 || targetIndex == currentIndex)
            {
                return true;
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                "调整同 Tick 技能事件顺序");
            int direction = targetIndex > currentIndex ? 1 : -1;
            while (currentIndex != targetIndex)
            {
                int adjacentIndex = currentIndex + direction;
                FpgSkillEventRecord current = sameTick[currentIndex];
                FpgSkillEventRecord adjacent = sameTick[adjacentIndex];
                SerializedProperty currentProperty = GetEventProperty(
                    serializedObject,
                    sequenceIndex,
                    current.Index);
                SerializedProperty adjacentProperty = GetEventProperty(
                    serializedObject,
                    sequenceIndex,
                    adjacent.Index);
                WriteInt(
                    currentProperty,
                    adjacent.AuthoredOrdinal,
                    AuthoredOrdinalNames);
                WriteInt(
                    adjacentProperty,
                    current.AuthoredOrdinal,
                    AuthoredOrdinalNames);

                int ordinal = current.AuthoredOrdinal;
                current.AuthoredOrdinal = adjacent.AuthoredOrdinal;
                adjacent.AuthoredOrdinal = ordinal;
                sameTick[currentIndex] = adjacent;
                sameTick[adjacentIndex] = current;
                currentIndex = adjacentIndex;
            }

            Apply(serializedObject);
            return true;
        }


        public static bool DeleteEvent(
            SerializedObject serializedObject,
            int sequenceIndex,
            int eventIndex)
        {
            DecodeEventKey(
                eventIndex,
                out FpgSkillEventTrackKind track,
                out int arrayIndex);
            SerializedProperty eventArray = GetEventArray(
                GetSequence(serializedObject, sequenceIndex),
                track);
            return DeleteArrayElement(
                serializedObject,
                eventArray,
                arrayIndex,
                "删除技能事件");
        }

        public static bool DeleteEvents(
            SerializedObject serializedObject,
            int sequenceIndex,
            IEnumerable<int> eventIndices)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null
                || eventIndices == null)
            {
                return false;
            }

            List<int> keys = new List<int>(new HashSet<int>(eventIndices));
            keys.Sort((left, right) =>
            {
                DecodeEventKey(left, out FpgSkillEventTrackKind leftTrack, out int leftIndex);
                DecodeEventKey(right, out FpgSkillEventTrackKind rightTrack, out int rightIndex);
                int trackComparison = leftTrack.CompareTo(rightTrack);
                return trackComparison != 0
                    ? trackComparison
                    : rightIndex.CompareTo(leftIndex);
            });
            if (keys.Count == 0)
            {
                return false;
            }

            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            for (int index = 0; index < keys.Count; index++)
            {
                DecodeEventKey(
                    keys[index],
                    out FpgSkillEventTrackKind track,
                    out int arrayIndex);
                SerializedProperty eventArray = GetEventArray(sequence, track);
                if (eventArray == null
                    || !eventArray.isArray
                    || arrayIndex < 0
                    || arrayIndex >= eventArray.arraySize)
                {
                    return false;
                }
            }

            Undo.RecordObject(serializedObject.targetObject, "删除技能事件");
            for (int index = 0; index < keys.Count; index++)
            {
                DecodeEventKey(
                    keys[index],
                    out FpgSkillEventTrackKind track,
                    out int arrayIndex);
                DeleteArrayElementWithoutApply(
                    GetEventArray(sequence, track),
                    arrayIndex);
            }

            Apply(serializedObject);
            return true;
        }

        public static int AddPayload(
            SerializedObject serializedObject,
            int sequenceIndex)
        {
            SerializedProperty payloadArray = GetPayloads(GetSequence(serializedObject, sequenceIndex));
            if (payloadArray == null || !payloadArray.isArray)
            {
                return -1;
            }

            Undo.RecordObject(serializedObject.targetObject, "添加载荷槽");
            int index = payloadArray.arraySize;
            payloadArray.InsertArrayElementAtIndex(index);
            SerializedProperty payload = payloadArray.GetArrayElementAtIndex(index);
            ResetProperty(payload);
            WriteString(payload, "payload-" + Guid.NewGuid().ToString("N"), "slotId", "payloadId", "id");
            WriteString(payload, "载荷 " + (index + 1), "displayName", "name", "label");
            SerializedProperty kind = FindFirstRelative(
                payload,
                "kind",
                "payloadKind",
                "type");
            if (kind != null
                && kind.propertyType == SerializedPropertyType.Enum
                && kind.enumDisplayNames.Length > 1)
            {
                kind.enumValueIndex = 1;
            }
            ConfigureDefaultPayload(payload);
            Apply(serializedObject);
            return index;
        }

        public static bool SetPayloadKindAndNormalize(
            SerializedObject serializedObject,
            int sequenceIndex,
            int payloadIndex,
            int enumValueIndex)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null)
            {
                return false;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty payload = GetPayloadProperty(
                serializedObject,
                sequenceIndex,
                payloadIndex);
            SerializedProperty kind = FindFirstRelative(
                payload,
                "kind",
                "payloadKind",
                "type");
            if (payload == null
                || kind == null
                || kind.propertyType != SerializedPropertyType.Enum
                || enumValueIndex <= 0
                || enumValueIndex >= kind.enumNames.Length)
            {
                return false;
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                "修改载荷类型");
            kind.enumValueIndex = enumValueIndex;
            string kindName = GetEnumName(kind);
            NormalizePayloadForCurrentKind(payload);
            string payloadId = ReadFirstString(
                payload,
                "slotId",
                "payloadId",
                "id");
            FpgSkillTargetSource targetSource =
                GetDefaultTargetSourceForPayloadKind(kind);
            bool clearSpatialMetadata =
                string.Equals(
                    kindName,
                    "TimedImpact",
                    StringComparison.Ordinal)
                || string.Equals(
                    kindName,
                    "Summon",
                    StringComparison.Ordinal);
            UpdateReferencedEventTargets(
                serializedObject,
                payloadIndex,
                payloadId,
                targetSource,
                clearSpatialMetadata);
            Apply(serializedObject);
            return true;
        }


        public static int DuplicatePayload(
            SerializedObject serializedObject,
            int sequenceIndex,
            int payloadIndex)
        {
            SerializedProperty payloadArray = GetPayloads(GetSequence(serializedObject, sequenceIndex));
            if (payloadArray == null
                || !payloadArray.isArray
                || payloadIndex < 0
                || payloadIndex >= payloadArray.arraySize)
            {
                return -1;
            }

            Undo.RecordObject(serializedObject.targetObject, "复制载荷槽");
            int insertionIndex = payloadIndex + 1;
            payloadArray.InsertArrayElementAtIndex(insertionIndex);
            SerializedProperty copy = payloadArray.GetArrayElementAtIndex(insertionIndex);
            WriteString(
                copy,
                "payload-" + Guid.NewGuid().ToString("N"),
                "slotId",
                "payloadId",
                "id");
            AdjustPayloadIndicesAfterInsert(
                serializedObject,
                insertionIndex);
            Apply(serializedObject);
            return insertionIndex;
        }

        public static bool DeletePayload(
            SerializedObject serializedObject,
            int sequenceIndex,
            int payloadIndex)
        {
            if (!CanDeletePayload(
                    serializedObject,
                    sequenceIndex,
                    payloadIndex,
                    out _))
            {
                return false;
            }

            SerializedProperty payloadArray = GetPayloads(
                GetSequence(serializedObject, sequenceIndex));
            Undo.RecordObject(serializedObject.targetObject, "删除载荷槽");
            DeleteArrayElementWithoutApply(payloadArray, payloadIndex);
            AdjustPayloadIndicesAfterDelete(serializedObject, payloadIndex);
            Apply(serializedObject);
            return true;
        }

        public static bool CanDeletePayload(
            SerializedObject serializedObject,
            int sequenceIndex,
            int payloadIndex,
            out int referenceCount)
        {
            referenceCount = 0;
            if (serializedObject == null
                || serializedObject.targetObject == null)
            {
                return false;
            }

            List<FpgSkillPayloadRecord> records = ReadPayloads(
                GetSequence(serializedObject, sequenceIndex));
            if (payloadIndex < 0 || payloadIndex >= records.Count)
            {
                return false;
            }

            referenceCount = records[payloadIndex].UseCount;
            return referenceCount == 0;
        }

        public static int ReplacePayloadReferences(
            SerializedObject serializedObject,
            int sequenceIndex,
            int sourcePayloadIndex,
            int targetPayloadIndex)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null
                || sourcePayloadIndex == targetPayloadIndex)
            {
                return 0;
            }

            List<FpgSkillPayloadRecord> payloads = ReadPayloads(
                GetSequence(serializedObject, sequenceIndex));
            if (sourcePayloadIndex < 0
                || sourcePayloadIndex >= payloads.Count
                || targetPayloadIndex < 0
                || targetPayloadIndex >= payloads.Count)
            {
                return 0;
            }

            string sourceId = payloads[sourcePayloadIndex].Id;
            string targetId = payloads[targetPayloadIndex].Id;
            SerializedProperty sequences = GetSequences(serializedObject);
            if (sequences == null || !sequences.isArray)
            {
                return 0;
            }

            int replacementCount = 0;
            Undo.RecordObject(serializedObject.targetObject, "替换载荷引用");
            for (int currentSequence = 0;
                currentSequence < sequences.arraySize;
                currentSequence++)
            {
                SerializedProperty sequence =
                    sequences.GetArrayElementAtIndex(currentSequence);
                replacementCount += ReplacePayloadReferencesInArray(
                    GetEventArray(sequence, FpgSkillEventTrackKind.Generic),
                    sourcePayloadIndex,
                    sourceId,
                    targetPayloadIndex,
                    targetId);
                replacementCount += ReplacePayloadReferencesInArray(
                    GetEventArray(sequence, FpgSkillEventTrackKind.Logic),
                    sourcePayloadIndex,
                    sourceId,
                    targetPayloadIndex,
                    targetId);
            }

            if (replacementCount > 0)
            {
                Apply(serializedObject);
            }

            return replacementCount;
        }

        public static Color GetPaletteColor(int index)
        {
            int normalized = Mathf.Abs(index) % Palette.Length;
            return Palette[normalized];
        }

        private static int CompareAuthoredEvents(
            FpgSkillEventRecord left,
            FpgSkillEventRecord right)
        {
            int tickComparison = left.Tick.CompareTo(right.Tick);
            if (tickComparison != 0)
            {
                return tickComparison;
            }

            int ordinalComparison = left.AuthoredOrdinal.CompareTo(
                right.AuthoredOrdinal);
            if (ordinalComparison != 0)
            {
                return ordinalComparison;
            }

            int trackComparison = left.Track.CompareTo(right.Track);
            return trackComparison != 0
                ? trackComparison
                : left.ArrayIndex.CompareTo(right.ArrayIndex);
        }

        private static int ReplacePayloadReferencesInArray(
            SerializedProperty eventArray,
            int sourcePayloadIndex,
            string sourcePayloadId,
            int targetPayloadIndex,
            string targetPayloadId)
        {
            if (eventArray == null || !eventArray.isArray)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < eventArray.arraySize; index++)
            {
                SerializedProperty eventProperty =
                    eventArray.GetArrayElementAtIndex(index);
                int referencedIndex = ReadRawInt(
                    FindFirstRelative(eventProperty, PayloadIndexNames),
                    -1);
                string referencedId = ReadFirstString(
                    eventProperty,
                    PayloadIdNames);
                bool hasReferencedId =
                    !string.IsNullOrWhiteSpace(referencedId);
                bool matchesId = hasReferencedId
                    && !string.IsNullOrWhiteSpace(sourcePayloadId)
                    && string.Equals(
                        referencedId,
                        sourcePayloadId,
                        StringComparison.Ordinal);
                bool matchesIndex = !hasReferencedId
                    && referencedIndex == sourcePayloadIndex;
                if (!matchesId && !matchesIndex)
                {
                    continue;
                }

                WriteInt(eventProperty, targetPayloadIndex, PayloadIndexNames);
                WriteString(eventProperty, targetPayloadId, PayloadIdNames);
                count++;
            }

            return count;
        }

        private static void AdjustPayloadIndicesAfterInsert(
            SerializedObject serializedObject,
            int insertedPayloadIndex)
        {
            SerializedProperty sequences = GetSequences(serializedObject);
            if (sequences == null || !sequences.isArray)
            {
                return;
            }

            for (int sequenceIndex = 0;
                sequenceIndex < sequences.arraySize;
                sequenceIndex++)
            {
                SerializedProperty sequence =
                    sequences.GetArrayElementAtIndex(sequenceIndex);
                AdjustPayloadIndicesInArrayAfterInsert(
                    GetEventArray(sequence, FpgSkillEventTrackKind.Generic),
                    insertedPayloadIndex);
                AdjustPayloadIndicesInArrayAfterInsert(
                    GetEventArray(sequence, FpgSkillEventTrackKind.Logic),
                    insertedPayloadIndex);
            }
        }

        private static void AdjustPayloadIndicesInArrayAfterInsert(
            SerializedProperty eventArray,
            int insertedPayloadIndex)
        {
            if (eventArray == null || !eventArray.isArray)
            {
                return;
            }

            for (int index = 0; index < eventArray.arraySize; index++)
            {
                SerializedProperty eventProperty =
                    eventArray.GetArrayElementAtIndex(index);
                string payloadId = ReadFirstString(
                    eventProperty,
                    PayloadIdNames);
                if (!string.IsNullOrWhiteSpace(payloadId))
                {
                    continue;
                }

                SerializedProperty payloadIndex = FindFirstRelative(
                    eventProperty,
                    PayloadIndexNames);
                if (payloadIndex != null
                    && payloadIndex.propertyType == SerializedPropertyType.Integer
                    && payloadIndex.intValue >= insertedPayloadIndex)
                {
                    payloadIndex.intValue++;
                }
            }
        }

        private static void AdjustPayloadIndicesAfterDelete(
            SerializedObject serializedObject,
            int deletedPayloadIndex)
        {
            SerializedProperty sequences = GetSequences(serializedObject);
            if (sequences == null || !sequences.isArray)
            {
                return;
            }

            for (int sequenceIndex = 0;
                sequenceIndex < sequences.arraySize;
                sequenceIndex++)
            {
                SerializedProperty sequence =
                    sequences.GetArrayElementAtIndex(sequenceIndex);
                AdjustPayloadIndicesInArray(
                    GetEventArray(sequence, FpgSkillEventTrackKind.Generic),
                    deletedPayloadIndex);
                AdjustPayloadIndicesInArray(
                    GetEventArray(sequence, FpgSkillEventTrackKind.Logic),
                    deletedPayloadIndex);
            }
        }

        private static void AdjustPayloadIndicesInArray(
            SerializedProperty eventArray,
            int deletedPayloadIndex)
        {
            if (eventArray == null || !eventArray.isArray)
            {
                return;
            }

            for (int index = 0; index < eventArray.arraySize; index++)
            {
                SerializedProperty payloadIndex = FindFirstRelative(
                    eventArray.GetArrayElementAtIndex(index),
                    PayloadIndexNames);
                if (payloadIndex != null
                    && payloadIndex.propertyType == SerializedPropertyType.Integer
                    && payloadIndex.intValue > deletedPayloadIndex)
                {
                    payloadIndex.intValue--;
                }
            }
        }

        private static SerializedProperty GetEventArray(
            SerializedProperty sequence,
            FpgSkillEventTrackKind track)
        {
            switch (track)
            {
                case FpgSkillEventTrackKind.Logic:
                    return FindFirstRelative(sequence, LogicEventArrayNames);
                case FpgSkillEventTrackKind.Presentation:
                    return FindFirstRelative(sequence, PresentationEventArrayNames);
                case FpgSkillEventTrackKind.Warning:
                    return FindFirstRelative(sequence, WarningEventArrayNames);
                default:
                    return FindFirstRelative(sequence, GenericEventArrayNames);
            }
        }

        private static FpgSkillEventTrackKind GetDefaultEventTrack(
            SerializedProperty sequence)
        {
            if (GetEventArray(sequence, FpgSkillEventTrackKind.Generic) != null)
            {
                return FpgSkillEventTrackKind.Generic;
            }

            if (GetEventArray(sequence, FpgSkillEventTrackKind.Logic) != null)
            {
                return FpgSkillEventTrackKind.Logic;
            }

            if (GetEventArray(sequence, FpgSkillEventTrackKind.Presentation) != null)
            {
                return FpgSkillEventTrackKind.Presentation;
            }

            return FpgSkillEventTrackKind.Warning;
        }

        private static bool HasAnyEventArray(SerializedProperty sequence)
        {
            return GetEventArray(sequence, FpgSkillEventTrackKind.Generic) != null
                || GetEventArray(sequence, FpgSkillEventTrackKind.Logic) != null
                || GetEventArray(sequence, FpgSkillEventTrackKind.Presentation) != null
                || GetEventArray(sequence, FpgSkillEventTrackKind.Warning) != null;
        }

        private static int MakeEventKey(
            FpgSkillEventTrackKind track,
            int arrayIndex)
        {
            const int trackStride = 1000000;
            return (int)track * trackStride + arrayIndex;
        }

        private static void DecodeEventKey(
            int eventKey,
            out FpgSkillEventTrackKind track,
            out int arrayIndex)
        {
            const int trackStride = 1000000;
            if (eventKey < 0)
            {
                track = FpgSkillEventTrackKind.Generic;
                arrayIndex = -1;
                return;
            }

            int trackValue = eventKey / trackStride;
            track = Enum.IsDefined(typeof(FpgSkillEventTrackKind), trackValue)
                ? (FpgSkillEventTrackKind)trackValue
                : FpgSkillEventTrackKind.Generic;
            arrayIndex = eventKey % trackStride;
        }

        private static string BuildEventName(
            SerializedProperty eventProperty,
            FpgSkillEventTrackKind track,
            int arrayIndex,
            string eventId,
            int payloadIndex,
            IList<FpgSkillPayloadRecord> payloads)
        {
            string primary = string.IsNullOrWhiteSpace(eventId)
                ? GetTrackLabel(track, eventProperty) + " " + (arrayIndex + 1)
                : eventId;
            string secondary = string.Empty;
            switch (track)
            {
                case FpgSkillEventTrackKind.Logic:
                    secondary = payloadIndex >= 0 && payloadIndex < payloads.Count
                        ? payloads[payloadIndex].Name
                        : ReadFirstString(eventProperty, PayloadIdNames);
                    break;
                case FpgSkillEventTrackKind.Presentation:
                    secondary = ReadFirstString(eventProperty, "cueId");
                    break;
                case FpgSkillEventTrackKind.Warning:
                    secondary = ReadFirstString(eventProperty, "warningId");
                    break;
            }

            return string.IsNullOrWhiteSpace(secondary)
                ? primary
                : primary + " · " + secondary;
        }

        private static string GetTrackLabel(
            FpgSkillEventTrackKind track,
            SerializedProperty eventProperty)
        {
            switch (track)
            {
                case FpgSkillEventTrackKind.Logic:
                    return "逻辑事件";
                case FpgSkillEventTrackKind.Presentation:
                    return "演出提示";
                case FpgSkillEventTrackKind.Warning:
                    return "预警区间";
                default:
                    string value = ReadDisplayValue(
                        eventProperty,
                        "kind",
                        "eventKind",
                        "type");
                    return string.IsNullOrWhiteSpace(value) ? "事件" : value;
            }
        }

        private static Color GetTrackColor(
            FpgSkillEventTrackKind track,
            int arrayIndex)
        {
            switch (track)
            {
                case FpgSkillEventTrackKind.Logic:
                    return GetPaletteColor(arrayIndex);
                case FpgSkillEventTrackKind.Presentation:
                    return Palette[3];
                case FpgSkillEventTrackKind.Warning:
                    return Palette[1];
                default:
                    return GetPaletteColor(arrayIndex);
            }
        }

        private static void AppendAuthoredPositionValidation(
            ICollection<FpgSkillValidationItem> result,
            IList<FpgSkillEventRecord> events)
        {
            Dictionary<ulong, FpgSkillEventRecord> positions =
                new Dictionary<ulong, FpgSkillEventRecord>();
            for (int index = 0; index < events.Count; index++)
            {
                FpgSkillEventRecord eventRecord = events[index];
                AppendAuthoredPosition(
                    result,
                    positions,
                    eventRecord,
                    eventRecord.Tick,
                    eventRecord.AuthoredOrdinal);
                if (eventRecord.Track == FpgSkillEventTrackKind.Warning)
                {
                    AppendAuthoredPosition(
                        result,
                        positions,
                        eventRecord,
                        eventRecord.Tick + eventRecord.DurationTicks,
                        eventRecord.AuthoredOrdinal + 1);
                }
            }
        }

        private static void AppendAuthoredPosition(
            ICollection<FpgSkillValidationItem> result,
            IDictionary<ulong, FpgSkillEventRecord> positions,
            FpgSkillEventRecord eventRecord,
            int tick,
            int authoredOrdinal)
        {
            if (tick < 0 || authoredOrdinal < 0)
            {
                return;
            }

            ulong key = unchecked(
                ((ulong)(uint)tick << 32) | (uint)authoredOrdinal);
            if (positions.ContainsKey(key))
            {
                result.Add(new FpgSkillValidationItem
                {
                    Severity = FpgSkillIssueSeverity.Error,
                    Message = "Tick " + tick
                        + " 存在重复的 authoredOrdinal "
                        + authoredOrdinal + "。",
                    EventIndex = eventRecord.Index,
                    Tick = tick
                });
                return;
            }

            positions[key] = eventRecord;
        }

        private static void AppendRuntimeValidation(
            ICollection<FpgSkillValidationItem> result,
            SerializedObject serializedObject,
            IList<FpgSkillEventRecord> events)
        {
            UnityEngine.Object target = serializedObject?.targetObject;
            if (target == null)
            {
                return;
            }

            System.Reflection.MethodInfo[] methods = target.GetType().GetMethods(
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public);
            for (int index = 0; index < methods.Length; index++)
            {
                System.Reflection.MethodInfo method = methods[index];
                System.Reflection.ParameterInfo[] parameters = method.GetParameters();
                if (!string.Equals(method.Name, "TryValidate", StringComparison.Ordinal)
                    || method.ReturnType != typeof(bool)
                    || parameters.Length != 1
                    || !parameters[0].IsOut
                    || parameters[0].ParameterType.GetElementType() != typeof(string))
                {
                    continue;
                }

                try
                {
                    object[] arguments = { null };
                    bool valid = (bool)method.Invoke(target, arguments);
                    if (!valid)
                    {
                        string message = arguments[0] as string ?? "未知错误";
                        FpgSkillValidationItem item = Error(
                            "运行时校验失败：" + message);
                        AttachEventLocation(item, message, events);
                        result.Add(item);
                    }
                }
                catch (Exception exception)
                {
                    result.Add(Warning(
                        "无法执行运行时校验：" + exception.GetBaseException().Message));
                }

                return;
            }
        }

        private static void AppendPreviewPrefabValidation(
            ICollection<FpgSkillValidationItem> result,
            SerializedProperty sequence,
            IList<FpgSkillEventRecord> events,
            GameObject previewPrefab)
        {
            if (previewPrefab == null)
            {
                result.Add(Warning(
                    "未选择预览 Prefab，无法验证 mainAnimation 和事件 Socket。"));
                return;
            }

            string animationName = GetMainAnimation(sequence);
            if (!TryFindSpineAnimation(
                    previewPrefab,
                    animationName,
                    out string animationError))
            {
                result.Add(Error(animationError));
            }

            Component socketRegistry = FindComponentByTypeName(
                previewPrefab,
                "FPG.Demo.Unity.D0ActorSocketRegistry");
            for (int index = 0; index < events.Count; index++)
            {
                FpgSkillEventRecord eventRecord = events[index];
                if (string.IsNullOrWhiteSpace(eventRecord.SocketId))
                {
                    continue;
                }

                if (socketRegistry != null
                    && TryResolveSocket(socketRegistry, eventRecord.SocketId))
                {
                    continue;
                }

                result.Add(new FpgSkillValidationItem
                {
                    Severity = FpgSkillIssueSeverity.Error,
                    Message = "事件“" + eventRecord.Name
                        + "”的 Socket “" + eventRecord.SocketId
                        + "”无法由当前预览 Prefab 的 D0ActorSocketRegistry 解析。",
                    EventIndex = eventRecord.Index,
                    Tick = eventRecord.Tick
                });
            }
        }

        private static bool TryFindSpineAnimation(
            GameObject previewPrefab,
            string animationName,
            out string error)
        {
            Component spineComponent = FindComponentByTypeName(
                previewPrefab,
                "Spine.Unity.SkeletonAnimation")
                ?? FindComponentByTypeName(
                    previewPrefab,
                    "Spine.Unity.SkeletonMecanim");
            if (spineComponent == null)
            {
                error = "当前预览 Prefab 未找到 Spine SkeletonAnimation。";
                return false;
            }

            try
            {
                System.Reflection.BindingFlags flags =
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public;
                object dataAsset = spineComponent.GetType()
                    .GetProperty("SkeletonDataAsset", flags)
                    ?.GetValue(spineComponent, null);
                System.Reflection.MethodInfo getSkeletonData = dataAsset?.GetType()
                    .GetMethod(
                        "GetSkeletonData",
                        flags,
                        null,
                        new[] { typeof(bool) },
                        null);
                object skeletonData = getSkeletonData?.Invoke(
                    dataAsset,
                    new object[] { true });
                System.Reflection.MethodInfo findAnimation = skeletonData?.GetType()
                    .GetMethod(
                        "FindAnimation",
                        flags,
                        null,
                        new[] { typeof(string) },
                        null);
                object animation = findAnimation?.Invoke(
                    skeletonData,
                    new object[] { animationName });
                if (animation == null)
                {
                    error = "mainAnimation “" + animationName
                        + "”不存在于当前预览 Prefab 的 Spine SkeletonData。";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "无法读取当前预览 Prefab 的 Spine SkeletonData："
                    + exception.GetBaseException().Message;
                return false;
            }
        }

        private static Component FindComponentByTypeName(
            GameObject root,
            string typeName)
        {
            if (root == null)
            {
                return null;
            }

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component != null
                    && string.Equals(
                        component.GetType().FullName,
                        typeName,
                        StringComparison.Ordinal))
                {
                    return component;
                }
            }

            return null;
        }

        private static bool TryResolveSocket(
            Component socketRegistry,
            string socketId)
        {
            try
            {
                System.Reflection.MethodInfo[] methods = socketRegistry.GetType()
                    .GetMethods(
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public);
                for (int index = 0; index < methods.Length; index++)
                {
                    System.Reflection.MethodInfo method = methods[index];
                    System.Reflection.ParameterInfo[] parameters =
                        method.GetParameters();
                    if (!string.Equals(
                            method.Name,
                            "TryResolve",
                            StringComparison.Ordinal)
                        || method.ReturnType != typeof(bool)
                        || parameters.Length != 2
                        || parameters[0].ParameterType != typeof(string)
                        || !parameters[1].IsOut)
                    {
                        continue;
                    }

                    object[] arguments = { socketId, null };
                    return (bool)method.Invoke(socketRegistry, arguments);
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private static void AttachEventLocation(
            FpgSkillValidationItem item,
            string message,
            IList<FpgSkillEventRecord> events)
        {
            if (item == null
                || string.IsNullOrWhiteSpace(message)
                || events == null)
            {
                return;
            }

            FpgSkillEventRecord bestMatch = null;
            int bestMessageIndex = int.MaxValue;
            for (int index = 0; index < events.Count; index++)
            {
                FpgSkillEventRecord eventRecord = events[index];
                if (string.IsNullOrWhiteSpace(eventRecord.EventId))
                {
                    continue;
                }

                int messageIndex = message.IndexOf(
                    eventRecord.EventId,
                    StringComparison.Ordinal);
                if (messageIndex < 0 || messageIndex >= bestMessageIndex)
                {
                    continue;
                }

                bestMatch = eventRecord;
                bestMessageIndex = messageIndex;
            }

            if (bestMatch != null)
            {
                item.EventIndex = bestMatch.Index;
                item.Tick = bestMatch.Tick;
            }
        }

        private static System.Reflection.MethodInfo FindRuntimeCompileMethod(
            Type targetType)
        {
            System.Reflection.MethodInfo fallback = null;
            System.Reflection.MethodInfo[] methods = targetType.GetMethods(
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public);
            for (int index = 0; index < methods.Length; index++)
            {
                System.Reflection.MethodInfo method = methods[index];
                System.Reflection.ParameterInfo[] parameters = method.GetParameters();
                if (!string.Equals(method.Name, "TryCompile", StringComparison.Ordinal)
                    || method.ReturnType != typeof(bool)
                    || parameters.Length != 2
                    || !parameters[0].IsOut
                    || !parameters[1].IsOut
                    || parameters[1].ParameterType.GetElementType() != typeof(string))
                {
                    continue;
                }

                if (method.DeclaringType == targetType)
                {
                    return method;
                }

                fallback = method;
            }

            return fallback;
        }

        private static object ExtractCompiledTimeline(object compiledDefinition)
        {
            if (compiledDefinition == null)
            {
                return null;
            }

            System.Reflection.PropertyInfo timelineProperty =
                compiledDefinition.GetType().GetProperty("Timeline");
            return timelineProperty == null
                ? compiledDefinition
                : timelineProperty.GetValue(compiledDefinition, null);
        }

        private static FpgSkillEventRecord FindAuthoredEvent(
            IList<FpgSkillEventRecord> authoredEvents,
            string compiledKind,
            int tick,
            int authoredOrdinal)
        {
            if (authoredEvents == null)
            {
                return null;
            }

            for (int index = 0; index < authoredEvents.Count; index++)
            {
                FpgSkillEventRecord authored = authoredEvents[index];
                bool positionMatches = authored.Tick == tick
                    && authored.AuthoredOrdinal == authoredOrdinal;
                if (compiledKind == "WarningEnded")
                {
                    positionMatches = authored.Track == FpgSkillEventTrackKind.Warning
                        && authored.Tick + authored.DurationTicks == tick
                        && authored.AuthoredOrdinal + 1 == authoredOrdinal;
                }

                if (!positionMatches)
                {
                    continue;
                }

                if (compiledKind == "GameplayPayload"
                    && authored.Track != FpgSkillEventTrackKind.Logic
                    && authored.Track != FpgSkillEventTrackKind.Generic)
                {
                    continue;
                }

                if (compiledKind == "PresentationCue"
                    && authored.Track != FpgSkillEventTrackKind.Presentation)
                {
                    continue;
                }

                if ((compiledKind == "WarningStarted"
                        || compiledKind == "WarningEnded")
                    && authored.Track != FpgSkillEventTrackKind.Warning)
                {
                    continue;
                }

                return authored;
            }

            return null;
        }

        private static string GetCompiledKindLabel(string compiledKind)
        {
            switch (compiledKind)
            {
                case "GameplayPayload":
                    return "逻辑事件";
                case "PresentationCue":
                    return "演出提示";
                case "WarningStarted":
                    return "预警开始";
                case "WarningEnded":
                    return "预警结束";
                default:
                    return compiledKind;
            }
        }

        private static string InferOwnerType(SerializedObject serializedObject)
        {
            string value = ReadDisplayValue(
                serializedObject,
                "ownerKind",
                "actorKind",
                "skillOwnerKind",
                "actionType");
            string typeName = serializedObject?.targetObject == null
                ? string.Empty
                : serializedObject.targetObject.GetType().Name;
            value = value + " " + typeName;
            if (ContainsAny(value, "player", "character", "hero", "角色", "玩家"))
            {
                return "角色";
            }

            if (ContainsAny(value, "enemy", "monster", "怪物", "敌人"))
            {
                return "怪物";
            }

            return "通用";
        }

        private static bool ContainsAny(string value, params string[] candidates)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (int index = 0; index < candidates.Length; index++)
            {
                if (value.IndexOf(candidates[index], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadDisplayValue(
            SerializedObject serializedObject,
            params string[] names)
        {
            if (serializedObject == null)
            {
                return string.Empty;
            }

            for (int index = 0; index < names.Length; index++)
            {
                string value = ReadDisplayValue(serializedObject.FindProperty(names[index]));
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static FpgSkillTargetSource ReadTargetSource(
            SerializedProperty eventProperty)
        {
            SerializedProperty source = FindFirstRelative(
                eventProperty,
                "targetSource");
            if (source == null
                || source.propertyType != SerializedPropertyType.Enum
                || !Enum.IsDefined(
                    typeof(FpgSkillTargetSource),
                    source.enumValueIndex))
            {
                return FpgSkillTargetSource.CurrentAim;
            }

            return (FpgSkillTargetSource)source.enumValueIndex;
        }

        private static Vector3 ReadVector3(
            SerializedProperty property,
            Vector3 fallback)
        {
            return property != null
                && property.propertyType == SerializedPropertyType.Vector3
                    ? property.vector3Value
                    : fallback;
        }

        private static string ReadDisplayValue(
            SerializedProperty parent,
            params string[] names)
        {
            SerializedProperty property = names == null || names.Length == 0
                ? parent
                : FindFirstRelative(parent, names);
            return ReadDisplayValue(property);
        }

        private static string ReadDisplayValue(SerializedProperty property)
        {
            if (property == null)
            {
                return string.Empty;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex >= 0
                        && property.enumValueIndex < property.enumDisplayNames.Length
                            ? property.enumDisplayNames[property.enumValueIndex]
                            : property.enumValueIndex.ToString();
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString();
                default:
                    return property.displayName;
            }
        }

        private static SerializedProperty FindFirst(
            SerializedObject serializedObject,
            params string[] names)
        {
            if (serializedObject == null || names == null)
            {
                return null;
            }

            for (int index = 0; index < names.Length; index++)
            {
                SerializedProperty property = serializedObject.FindProperty(names[index]);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static SerializedProperty FindFirstRelative(
            SerializedProperty parent,
            params string[] names)
        {
            if (parent == null || names == null)
            {
                return null;
            }

            for (int index = 0; index < names.Length; index++)
            {
                SerializedProperty property = parent.FindPropertyRelative(names[index]);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static string ReadFirstString(
            SerializedProperty parent,
            params string[] names)
        {
            return ReadString(FindFirstRelative(parent, names), null);
        }

        private static string ReadString(SerializedProperty property, string fallback)
        {
            return property != null && property.propertyType == SerializedPropertyType.String
                ? property.stringValue
                : fallback;
        }

        private static int ReadInt(
            SerializedProperty property,
            int fallback,
            int minimum)
        {
            return property != null && property.propertyType == SerializedPropertyType.Integer
                ? Mathf.Max(minimum, property.intValue)
                : fallback;
        }

        private static int ReadRawInt(
            SerializedProperty property,
            int fallback)
        {
            return property != null
                && property.propertyType == SerializedPropertyType.Integer
                    ? property.intValue
                    : fallback;
        }

        private static void WriteInt(
            SerializedProperty parent,
            int value,
            params string[] names)
        {
            SerializedProperty property = FindFirstRelative(parent, names);
            if (property != null && property.propertyType == SerializedPropertyType.Integer)
            {
                property.intValue = value;
            }
        }

        private static void WriteString(
            SerializedProperty parent,
            string value,
            params string[] names)
        {
            SerializedProperty property = FindFirstRelative(parent, names);
            if (property != null && property.propertyType == SerializedPropertyType.String)
            {
                property.stringValue = value;
            }
        }

        private static void SetFirstNonZeroEnum(
            SerializedProperty parent,
            params string[] names)
        {
            SerializedProperty property = FindFirstRelative(parent, names);
            if (property != null
                && property.propertyType == SerializedPropertyType.Enum
                && property.enumDisplayNames.Length > 1)
            {
                property.enumValueIndex = 1;
            }
        }

        private static bool DeleteArrayElement(
            SerializedObject serializedObject,
            SerializedProperty array,
            int index,
            string undoName)
        {
            if (serializedObject == null
                || array == null
                || !array.isArray
                || index < 0
                || index >= array.arraySize)
            {
                return false;
            }

            Undo.RecordObject(serializedObject.targetObject, undoName);
            DeleteArrayElementWithoutApply(array, index);
            Apply(serializedObject);
            return true;
        }

        private static void DeleteArrayElementWithoutApply(
            SerializedProperty array,
            int index)
        {
            int previousSize = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == previousSize)
            {
                array.DeleteArrayElementAtIndex(index);
            }
        }

        private static int FindNextAuthoredOrdinal(
            SerializedProperty sequence,
            int ignoredEventKey)
        {
            int maximum = -1;
            FpgSkillEventTrackKind[] tracks =
            {
                FpgSkillEventTrackKind.Generic,
                FpgSkillEventTrackKind.Logic,
                FpgSkillEventTrackKind.Presentation,
                FpgSkillEventTrackKind.Warning
            };
            for (int trackIndex = 0; trackIndex < tracks.Length; trackIndex++)
            {
                FpgSkillEventTrackKind track = tracks[trackIndex];
                SerializedProperty eventArray = GetEventArray(sequence, track);
                if (eventArray == null || !eventArray.isArray)
                {
                    continue;
                }

                for (int index = 0; index < eventArray.arraySize; index++)
                {
                    if (MakeEventKey(track, index) == ignoredEventKey)
                    {
                        continue;
                    }

                    SerializedProperty item =
                        eventArray.GetArrayElementAtIndex(index);
                    int ordinal = ReadRawInt(
                        FindFirstRelative(item, AuthoredOrdinalNames),
                        index);
                    maximum = Mathf.Max(maximum, ordinal);
                    if (track == FpgSkillEventTrackKind.Warning
                        && ordinal < int.MaxValue)
                    {
                        maximum = Mathf.Max(maximum, ordinal + 1);
                    }
                }
            }

            return maximum == int.MaxValue ? int.MaxValue : maximum + 1;
        }

        private static void ResetProperty(SerializedProperty property)
        {
            if (property == null)
            {
                return;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = 0;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = false;
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = 0f;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = string.Empty;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = Color.white;
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = null;
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = 0;
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = Vector2.zero;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = Vector3.zero;
                    break;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = Vector4.zero;
                    break;
                case SerializedPropertyType.Rect:
                    property.rectValue = Rect.zero;
                    break;
                case SerializedPropertyType.Bounds:
                    property.boundsValue = new Bounds(Vector3.zero, Vector3.zero);
                    break;
                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = Quaternion.identity;
                    break;
                case SerializedPropertyType.Generic:
                    if (property.isArray && property.propertyType != SerializedPropertyType.String)
                    {
                        property.arraySize = 0;
                        break;
                    }

                    SerializedProperty iterator = property.Copy();
                    SerializedProperty end = iterator.GetEndProperty();
                    bool enterChildren = true;
                    while (iterator.NextVisible(enterChildren)
                           && !SerializedProperty.EqualContents(iterator, end))
                    {
                        enterChildren = false;
                        if (iterator.depth == property.depth + 1)
                        {
                            ResetProperty(iterator);
                        }
                    }

                    break;
            }
        }

        private static void Apply(SerializedObject serializedObject)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(serializedObject.targetObject);
        }

        private static FpgSkillValidationItem Error(string message)
        {
            return new FpgSkillValidationItem
            {
                Severity = FpgSkillIssueSeverity.Error,
                Message = message
            };
        }

        private static FpgSkillValidationItem Warning(string message)
        {
            return new FpgSkillValidationItem
            {
                Severity = FpgSkillIssueSeverity.Warning,
                Message = message
            };
        }

        private static FpgSkillValidationItem Info(string message)
        {
            return new FpgSkillValidationItem
            {
                Severity = FpgSkillIssueSeverity.Info,
                Message = message
            };
        }
    

        private static string GetPhaseLabel(
            FpgSkillPhaseKind phaseKind)
        {
            switch (phaseKind)
            {
                case FpgSkillPhaseKind.Startup:
                    return "前摇";
                case FpgSkillPhaseKind.Active:
                    return "生效";
                case FpgSkillPhaseKind.Recovery:
                    return "后摇";
                default:
                    return "阶段";
            }
        }

        private static Color GetPhaseColor(
            FpgSkillPhaseKind phaseKind)
        {
            switch (phaseKind)
            {
                case FpgSkillPhaseKind.Startup:
                    return new Color(0.38f, 0.48f, 0.62f, 0.92f);
                case FpgSkillPhaseKind.Active:
                    return new Color(0.82f, 0.36f, 0.24f, 0.92f);
                case FpgSkillPhaseKind.Recovery:
                    return new Color(0.30f, 0.62f, 0.42f, 0.92f);
                default:
                    return new Color(0.56f, 0.42f, 0.67f, 0.92f);
            }
        }


        private static void SetEnumRawValue(
            SerializedProperty parent,
            int value,
            params string[] names)
        {
            SerializedProperty property = FindFirstRelative(parent, names);
            if (property != null
                && property.propertyType == SerializedPropertyType.Enum)
            {
                property.intValue = value;
            }
        }


        private static void ConfigureDefaultPayload(
            SerializedProperty payload)
        {
            NormalizePayloadForCurrentKind(payload);
        }

        private static void NormalizePayloadForCurrentKind(
            SerializedProperty payload)
        {
            SerializedProperty kind = FindFirstRelative(
                payload,
                "kind",
                "payloadKind",
                "type");
            string kindName = GetEnumName(kind);
            switch (kindName)
            {
                case "PelletRay":
                    NormalizePlayerAttackPayload(
                        payload,
                        1,
                        8,
                        0);
                    break;

                case "AreaAtFirstSurface":
                    NormalizePlayerAttackPayload(
                        payload,
                        2,
                        0,
                        4);
                    break;

                case "ReloadCommit":
                    WriteInt(payload, 0, "ammoCost");
                    WriteInt(payload, 0, "baseDamage");
                    WriteInt(payload, 0, "breakDamage");
                    SetEnumRawValue(payload, 0, "queryMode");
                    SetEnumRawValue(payload, 0, "allowedTargetKinds");
                    break;

                case "Projectile":
                    NormalizeEnemyDamage(payload);
                    EnsurePositiveInt(payload, 1, "threatDefinitionId");
                    EnsurePositiveInt(payload, 1, "projectileDefinitionId");
                    EnsurePositiveInt(payload, 1, "projectileCount");
                    EnsurePositiveInt(payload, 30, "projectileFlightTicks");
                    EnsureMinimumInt(
                        payload,
                        GetInt(payload, "projectileFlightTicks"),
                        "projectileLifetimeTicks");
                    EnsureNonNegativeInt(payload, 0, "projectileMaxHitPoints");
                    EnsurePositiveInt(payload, 1, "projectileBudgetUnits");
                    EnsurePositiveInt(payload, 1, "projectilePresentationKey");
                    EnsurePositiveInt(payload, 1, "projectileSweepRadiusKey");
                    ClampInt(
                        payload,
                        1,
                        int.MaxValue / Mathf.Max(
                            1,
                            GetInt(payload, "projectileBudgetUnits")),
                        "projectileCount");
                    EnsureInterceptableProjectileHasHitPoints(payload);
                    break;

                case "TimedImpact":
                    NormalizeEnemyDamage(payload);
                    EnsurePositiveInt(payload, 1, "threatDefinitionId");
                    EnsureEnumIndex(payload, 0, "timedImpactTargetPolicy");
                    EnsureNonNegativeInt(payload, 0, "timedImpactDelayTicks");
                    EnsurePositiveInt(payload, 1, "timedImpactPresentationKey");
                    break;

                case "Summon":
                    NormalizeSummonPayload(payload);
                    break;
            }
        }

        private static string GetEnumName(
            SerializedProperty property)
        {
            if (property == null
                || property.propertyType != SerializedPropertyType.Enum
                || property.enumValueIndex < 0
                || property.enumValueIndex >= property.enumNames.Length)
            {
                return string.Empty;
            }

            return property.enumNames[property.enumValueIndex];
        }

        private static void NormalizePlayerAttackPayload(
            SerializedProperty payload,
            int queryModeIndex,
            int pelletCount,
            int areaCombatantLimit)
        {
            EnsurePositiveInt(payload, 1, "ammoCost");
            EnsurePositiveInt(payload, 4, "baseDamage");
            EnsurePositiveInt(payload, 4, "breakDamage");
            EnsurePositiveInt(
                payload,
                12000,
                "weakpointDamageMultiplierBasisPoints");
            EnsurePositiveInt(
                payload,
                25000,
                "weakpointBreakMultiplierBasisPoints");
            SetEnumRawValue(payload, queryModeIndex, "queryMode");
            if (pelletCount > 0)
            {
                EnsurePositiveInt(
                    payload,
                    pelletCount,
                    "pelletCount");
                EnsureNonNegativeInt(
                    payload,
                    0,
                    "additionalPenetrationCount");
                int normalizedPelletCount = Mathf.Max(
                    1,
                    GetInt(payload, "pelletCount"));
                ClampInt(
                    payload,
                    0,
                    Mathf.Max(
                        0,
                        int.MaxValue / normalizedPelletCount - 1),
                    "additionalPenetrationCount");
            }

            if (areaCombatantLimit > 0)
            {
                EnsurePositiveInt(
                    payload,
                    areaCombatantLimit,
                    "areaCombatantLimit");
                EnsureNonNegativeInt(
                    payload,
                    0,
                    "areaProjectileLimit");
                int normalizedCombatantLimit = Mathf.Max(
                    1,
                    GetInt(payload, "areaCombatantLimit"));
                ClampInt(
                    payload,
                    0,
                    int.MaxValue - normalizedCombatantLimit,
                    "areaProjectileLimit");
            }

            SetEnumRawValue(payload, 3, "allowedTargetKinds");
        }

        private static void NormalizeEnemyDamage(
            SerializedProperty payload)
        {
            EnsureNonNegativeInt(payload, 10, "baseDamage");
            EnsureNonNegativeInt(payload, 0, "breakDamage");
            EnsurePositiveInt(
                payload,
                10000,
                "weakpointDamageMultiplierBasisPoints");
            EnsurePositiveInt(
                payload,
                10000,
                "weakpointBreakMultiplierBasisPoints");
        }

        private static void NormalizeSummonPayload(
            SerializedProperty payload)
        {
            EnsureEnumIndex(payload, 0, "summonOccupancyMode");
            EnsureEnumIndex(payload, 0, "summonPlacementMode");
            EnsureEnumIndex(payload, 0, "summonOwnerOutcome");
            SerializedProperty occupancy = FindFirstRelative(
                payload,
                "summonOccupancyMode");
            SerializedProperty ownerOutcome = FindFirstRelative(
                payload,
                "summonOwnerOutcome");
            if (string.Equals(
                    GetEnumName(occupancy),
                    "ReplaceOwner",
                    StringComparison.Ordinal))
            {
                SetEnumByName(
                    ownerOutcome,
                    "DieAfterSuccessfulSummon",
                    0);
                WriteInt(payload, 0, "maxSummonsPerOwner");
                WriteInt(payload, 0, "maxTotalSummonsPerEncounter");
            }
            else
            {
                if (string.Equals(
                        GetEnumName(ownerOutcome),
                        "DieAfterSuccessfulSummon",
                        StringComparison.Ordinal))
                {
                    SetEnumByName(ownerOutcome, "RemainAlive", 0);
                }

                EnsurePositiveInt(payload, 2, "maxSummonsPerOwner");
                EnsurePositiveInt(
                    payload,
                    8,
                    "maxTotalSummonsPerEncounter");
            }

            EnsureNonNegativeInt(
                payload,
                2,
                "maxSummonRecursionDepth");
            ClampInt(
                payload,
                0,
                8,
                "maxSummonRecursionDepth");
            NormalizeSummonWeights(payload);
        }

        private static void NormalizeSummonWeights(
            SerializedProperty payload)
        {
            SerializedProperty candidates = FindFirstRelative(
                payload,
                "summonCandidates");
            SerializedProperty weights = FindFirstRelative(
                payload,
                "summonCandidateWeights");
            if (candidates == null
                || weights == null
                || !candidates.isArray
                || !weights.isArray)
            {
                return;
            }

            if (candidates.arraySize == 0)
            {
                weights.arraySize = 0;
                return;
            }

            if (weights.arraySize == 0)
            {
                return;
            }

            weights.arraySize = candidates.arraySize;
            for (int index = 0; index < weights.arraySize; index++)
            {
                SerializedProperty weight =
                    weights.GetArrayElementAtIndex(index);
                if (weight.propertyType == SerializedPropertyType.Integer
                    && weight.intValue <= 0)
                {
                    weight.intValue = 1;
                }
            }
        }








        private static void EnsurePositiveInt(
            SerializedProperty parent,
            int fallback,
            params string[] names)
        {
            SerializedProperty property = FindFirstRelative(parent, names);
            if (property != null
                && property.propertyType == SerializedPropertyType.Integer
                && property.intValue <= 0)
            {
                property.intValue = Mathf.Max(1, fallback);
            }
        }

        private static void EnsureNonNegativeInt(
            SerializedProperty parent,
            int fallback,
            params string[] names)
        {
            SerializedProperty property = FindFirstRelative(parent, names);
            if (property != null
                && property.propertyType == SerializedPropertyType.Integer
                && property.intValue < 0)
            {
                property.intValue = Mathf.Max(0, fallback);
            }
        }

        private static void EnsureMinimumInt(
            SerializedProperty parent,
            int minimum,
            params string[] names)
        {
            SerializedProperty property = FindFirstRelative(parent, names);
            if (property != null
                && property.propertyType == SerializedPropertyType.Integer
                && property.intValue < minimum)
            {
                property.intValue = minimum;
            }
        }

        private static void ClampInt(
            SerializedProperty parent,
            int minimum,
            int maximum,
            params string[] names)
        {
            SerializedProperty property = FindFirstRelative(parent, names);
            if (property != null
                && property.propertyType == SerializedPropertyType.Integer)
            {
                property.intValue = Mathf.Clamp(
                    property.intValue,
                    minimum,
                    Mathf.Max(minimum, maximum));
            }
        }


        private static int GetInt(
            SerializedProperty parent,
            params string[] names)
        {
            return ReadRawInt(FindFirstRelative(parent, names), 0);
        }

        private static void EnsureEnumIndex(
            SerializedProperty parent,
            int fallbackIndex,
            params string[] names)
        {
            SerializedProperty property = FindFirstRelative(parent, names);
            if (property == null
                || property.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            if (property.enumValueIndex < 0
                || property.enumValueIndex >= property.enumNames.Length)
            {
                property.enumValueIndex = Mathf.Clamp(
                    fallbackIndex,
                    0,
                    Mathf.Max(0, property.enumNames.Length - 1));
            }
        }

        private static void SetEnumByName(
            SerializedProperty property,
            string enumName,
            int fallbackIndex)
        {
            if (property == null
                || property.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            for (int index = 0; index < property.enumNames.Length; index++)
            {
                if (string.Equals(
                        property.enumNames[index],
                        enumName,
                        StringComparison.Ordinal))
                {
                    property.enumValueIndex = index;
                    return;
                }
            }

            property.enumValueIndex = Mathf.Clamp(
                fallbackIndex,
                0,
                Mathf.Max(0, property.enumNames.Length - 1));
        }

        private static void EnsureInterceptableProjectileHasHitPoints(
            SerializedProperty payload)
        {
            SerializedProperty interceptable = FindFirstRelative(
                payload,
                "projectileInterceptable");
            SerializedProperty hitPoints = FindFirstRelative(
                payload,
                "projectileMaxHitPoints");
            if (interceptable != null
                && interceptable.propertyType == SerializedPropertyType.Boolean
                && interceptable.boolValue
                && hitPoints != null
                && hitPoints.propertyType == SerializedPropertyType.Integer
                && hitPoints.intValue <= 0)
            {
                hitPoints.intValue = 1;
            }
        }

        private static FpgSkillTargetSource GetDefaultTargetSourceForPayloadKind(
            SerializedProperty kind)
        {
            switch (GetEnumName(kind))
            {
                case "ReloadCommit":
                    return FpgSkillTargetSource.Self;

                case "Projectile":
                case "TimedImpact":
                case "Summon":
                    return FpgSkillTargetSource.CurrentTarget;

                default:
                    return FpgSkillTargetSource.CurrentAim;
            }
        }

        private static void UpdateReferencedEventTargets(
            SerializedObject serializedObject,
            int payloadIndex,
            string payloadId,
            FpgSkillTargetSource targetSource,
            bool clearSpatialMetadata)
        {
            SerializedProperty sequences = GetSequences(serializedObject);
            if (sequences == null || !sequences.isArray)
            {
                return;
            }

            for (int sequenceIndex = 0;
                sequenceIndex < sequences.arraySize;
                sequenceIndex++)
            {
                SerializedProperty sequence =
                    sequences.GetArrayElementAtIndex(sequenceIndex);
                UpdateReferencedEventTargetsInArray(
                    GetEventArray(sequence, FpgSkillEventTrackKind.Generic),
                    payloadIndex,
                    payloadId,
                    targetSource,
                    clearSpatialMetadata);
                UpdateReferencedEventTargetsInArray(
                    GetEventArray(sequence, FpgSkillEventTrackKind.Logic),
                    payloadIndex,
                    payloadId,
                    targetSource,
                    clearSpatialMetadata);
            }
        }

        private static void UpdateReferencedEventTargetsInArray(
            SerializedProperty eventArray,
            int payloadIndex,
            string payloadId,
            FpgSkillTargetSource targetSource,
            bool clearSpatialMetadata)
        {
            if (eventArray == null || !eventArray.isArray)
            {
                return;
            }

            for (int index = 0; index < eventArray.arraySize; index++)
            {
                SerializedProperty eventProperty =
                    eventArray.GetArrayElementAtIndex(index);
                int referencedIndex = ReadRawInt(
                    FindFirstRelative(eventProperty, PayloadIndexNames),
                    -1);
                string referencedId = ReadFirstString(
                    eventProperty,
                    PayloadIdNames);
                bool hasReferencedId =
                    !string.IsNullOrWhiteSpace(referencedId);
                bool matchesId = hasReferencedId
                    && !string.IsNullOrWhiteSpace(payloadId)
                    && string.Equals(
                        referencedId,
                        payloadId,
                        StringComparison.Ordinal);
                bool matchesIndex = !hasReferencedId
                    && referencedIndex == payloadIndex;
                if (!matchesId && !matchesIndex)
                {
                    continue;
                }

                SetEnumRawValue(
                    eventProperty,
                    (int)targetSource,
                    "targetSource");
                if (clearSpatialMetadata)
                {
                    ClearEventSpatialMetadata(eventProperty);
                }
            }
        }

        private static void SetDefaultEventTargetSource(
            SerializedProperty eventProperty,
            FpgSkillPayloadRecord payload)
        {
            FpgSkillTargetSource source =
                FpgSkillTargetSource.CurrentAim;
            if (payload != null)
            {
                switch (payload.PreviewKind)
                {
                    case FpgSkillPreviewPayloadKind.PlayerReload:
                        source = FpgSkillTargetSource.Self;
                        break;

                    case FpgSkillPreviewPayloadKind.EnemyProjectile:
                    case FpgSkillPreviewPayloadKind.EnemyTimedImpact:
                    case FpgSkillPreviewPayloadKind.EnemySummon:
                        source = FpgSkillTargetSource.CurrentTarget;
                        break;
                }
            }

            SetEnumRawValue(
                eventProperty,
                (int)source,
                "targetSource");
            if (payload != null
                && (payload.PreviewKind
                        == FpgSkillPreviewPayloadKind.EnemyTimedImpact
                    || payload.PreviewKind
                        == FpgSkillPreviewPayloadKind.EnemySummon))
            {
                ClearEventSpatialMetadata(eventProperty);
            }
        }


        private static void ClearEventSpatialMetadata(
            SerializedProperty eventProperty)
        {
            WriteString(
                eventProperty,
                string.Empty,
                "socketId",
                "socket");
            SerializedProperty targetOffset = FindFirstRelative(
                eventProperty,
                "targetOffset");
            if (targetOffset != null
                && targetOffset.propertyType
                    == SerializedPropertyType.Vector3)
            {
                targetOffset.vector3Value = Vector3.zero;
            }
        }


        public static bool EditTimelineBlockRange(
            SerializedObject serializedObject,
            int sequenceIndex,
            FpgSkillTimelineBlockKind kind,
            int blockIndex,
            FpgSkillTimelineBlockEditMode editMode,
            int requestedStartTick,
            int requestedEndTick,
            out int appliedStartTick,
            out int appliedEndTick)
        {
            appliedStartTick = 0;
            appliedEndTick = 0;
            if (serializedObject == null
                || serializedObject.targetObject == null
                || !Enum.IsDefined(typeof(FpgSkillTimelineBlockEditMode), editMode))
            {
                return false;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty sequence = GetSequence(
                serializedObject,
                sequenceIndex);
            SerializedProperty durationProperty =
                sequence?.FindPropertyRelative("durationTicks");
            int durationTicks = GetDurationTicks(sequence);
            if (sequence == null
                || durationProperty == null
                || durationProperty.propertyType
                    != SerializedPropertyType.Integer
                || durationTicks < 0)
            {
                return false;
            }

            SerializedProperty startProperty;
            SerializedProperty endProperty;
            SerializedProperty phases = null;
            int startTick;
            int endTick;
            int rawEndTick;
            if (kind == FpgSkillTimelineBlockKind.Animation)
            {
                if (blockIndex != 0)
                {
                    return false;
                }

                startProperty = sequence.FindPropertyRelative(
                    "animationStartTick");
                endProperty = sequence.FindPropertyRelative(
                    "animationEndTick");
                startTick = GetAnimationStartTick(sequence);
                endTick = GetAnimationEndTick(sequence);
                rawEndTick = ReadRawInt(endProperty, -1);
            }
            else if (kind == FpgSkillTimelineBlockKind.Phase)
            {
                phases = sequence.FindPropertyRelative("phases");
                if (phases == null
                    || !phases.isArray
                    || blockIndex < 0
                    || blockIndex >= phases.arraySize)
                {
                    return false;
                }

                SerializedProperty phase =
                    phases.GetArrayElementAtIndex(blockIndex);
                startProperty = phase.FindPropertyRelative("startTick");
                endProperty = phase.FindPropertyRelative("endTick");
                startTick = ReadRawInt(startProperty, -1);
                endTick = ReadRawInt(endProperty, -1);
                rawEndTick = endTick;
            }
            else
            {
                return false;
            }

            appliedStartTick = startTick;
            appliedEndTick = endTick;
            if (startProperty == null
                || endProperty == null
                || startProperty.propertyType
                    != SerializedPropertyType.Integer
                || endProperty.propertyType
                    != SerializedPropertyType.Integer
                || startTick < 0
                || endTick < startTick
                || endTick > durationTicks)
            {
                return false;
            }

            int nextStartTick = startTick;
            int nextEndTick = endTick;
            int nextDurationTicks = durationTicks;
            if (kind == FpgSkillTimelineBlockKind.Animation)
            {
                FpgSkillAnimationPlaybackMode playbackMode =
                    GetAnimationPlaybackModeValue(sequence);
                if (editMode != FpgSkillTimelineBlockEditMode.Move
                    && playbackMode
                        != FpgSkillAnimationPlaybackMode.FitInterval)
                {
                    return false;
                }

                switch (editMode)
                {
                    case FpgSkillTimelineBlockEditMode.Move:
                    {
                        long requestedInterval =
                            (long)requestedEndTick - requestedStartTick;
                        if (requestedInterval < 0
                            || requestedInterval > int.MaxValue)
                        {
                            return false;
                        }

                        int intervalTicks = (int)requestedInterval;
                        int maximumStart = int.MaxValue - intervalTicks;
                        nextStartTick = Mathf.Clamp(
                            requestedStartTick,
                            0,
                            maximumStart);
                        nextEndTick = nextStartTick + intervalTicks;
                        break;
                    }
                    case FpgSkillTimelineBlockEditMode.ResizeStart:
                    {
                        if (endTick <= 0)
                        {
                            return false;
                        }

                        nextStartTick = Mathf.Clamp(
                            requestedStartTick,
                            0,
                            endTick - 1);
                        break;
                    }
                    case FpgSkillTimelineBlockEditMode.ResizeEnd:
                    {
                        if (startTick == int.MaxValue)
                        {
                            return false;
                        }

                        nextEndTick = Mathf.Max(
                            requestedEndTick,
                            startTick + 1);
                        break;
                    }
                    default:
                        return false;
                }

                nextDurationTicks = Mathf.Max(
                    durationTicks,
                    nextEndTick);
            }
            else
            {
                int minimumStartTick = 0;
                int maximumEndTick = durationTicks;
                if (blockIndex > 0)
                {
                    minimumStartTick = ReadRawInt(
                        phases.GetArrayElementAtIndex(blockIndex - 1)
                            .FindPropertyRelative("endTick"),
                        -1);
                }

                if (blockIndex + 1 < phases.arraySize)
                {
                    maximumEndTick = ReadRawInt(
                        phases.GetArrayElementAtIndex(blockIndex + 1)
                            .FindPropertyRelative("startTick"),
                        -1);
                }

                if (minimumStartTick < 0
                    || maximumEndTick < 0
                    || minimumStartTick > startTick
                    || maximumEndTick < endTick)
                {
                    return false;
                }

                switch (editMode)
                {
                    case FpgSkillTimelineBlockEditMode.Move:
                    {
                        int intervalTicks = endTick - startTick;
                        int maximumStartTick =
                            maximumEndTick - intervalTicks;
                        nextStartTick = Mathf.Clamp(
                            requestedStartTick,
                            minimumStartTick,
                            maximumStartTick);
                        nextEndTick = nextStartTick + intervalTicks;
                        break;
                    }
                    case FpgSkillTimelineBlockEditMode.ResizeStart:
                        nextStartTick = Mathf.Clamp(
                            requestedStartTick,
                            minimumStartTick,
                            endTick);
                        break;
                    case FpgSkillTimelineBlockEditMode.ResizeEnd:
                        nextEndTick = Mathf.Clamp(
                            requestedEndTick,
                            startTick,
                            maximumEndTick);
                        break;
                    default:
                        return false;
                }
            }

            appliedStartTick = nextStartTick;
            appliedEndTick = nextEndTick;
            bool materializeResolvedAnimationEnd =
                kind == FpgSkillTimelineBlockKind.Animation
                && rawEndTick != endTick;
            if (nextStartTick == startTick
                && nextEndTick == endTick
                && nextDurationTicks == durationTicks
                && !materializeResolvedAnimationEnd)
            {
                return true;
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                kind == FpgSkillTimelineBlockKind.Animation
                    ? "编辑技能动画区间"
                    : "编辑技能阶段区间");
            startProperty.intValue = nextStartTick;
            endProperty.intValue = nextEndTick;
            if (nextDurationTicks != durationTicks)
            {
                durationProperty.intValue = nextDurationTicks;
            }

            Apply(serializedObject);
            return true;
        }
}
}
