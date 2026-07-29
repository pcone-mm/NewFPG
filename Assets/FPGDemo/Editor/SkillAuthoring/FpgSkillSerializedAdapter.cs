using System;
using System.Collections.Generic;
using System.Linq;
using FPG.Demo.Core;
using FPG.Demo.Skills;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal enum FpgSkillPreviewActionKind
    {
        Unknown = 0,
        PlayerPelletRay,
        PlayerAreaAtFirstSurface,
        PlayerReload,
        EnemyProjectile,
        EnemyTimedImpact,
        EnemySummon,
        EnemySelfDestruct
    }
    internal enum FpgSkillIssueSeverity
    {
        Info = 0,
        Warning,
        Error
    }

    internal enum FpgSkillEventTrackKind
    {
        GameplayAction = 0,
        Warning,
        PresentationVfx,
        PresentationAudio,
        PresentationCameraShake
    }

    internal sealed class FpgSkillActivePresentationTrackRecord
    {
        public int Index;
        public string Id;
        public string Name;
        public int EventCount;
    }

    internal sealed class FpgSkillAssetRecord
    {
        public UnityEngine.Object Asset;
        public string Path;
        public string SkillId;
        public string DisplayName;
        public string OwnerType;
    }

    internal sealed class FpgSkillActionPreviewRecord
    {
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
        public FpgSkillPreviewActionKind PreviewKind;
        public bool HasDamagePreview;
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
        public FpgSkillEventKey Key;
        // Preview correlation only. Editor selection and mutation use Key.
        public int Index;
        public int ArrayIndex;
        public int Tick;
        public int DurationTicks;
        public int AuthoredOrdinal;
        public string EventId;
        public string SocketId;
        public string Name;
        public string Kind;
        public FpgSkillTargetSource TargetSource;
        public Vector3 TargetOffset;
        public FpgSkillEventTrackKind Track;
        public int PresentationTrackIndex = -1;
        public string PresentationTrackId;
        public string PresentationTrackName;
        public FpgSkillActionPreviewRecord InlineActionPreview;
        public bool IsInvalid;
        public Color Color;

        public FpgSkillTimelineEventViewModel ToViewModel()
        {
            return new FpgSkillTimelineEventViewModel
            {
                Key = Key,
                Tick = Tick,
                DurationTicks = DurationTicks,
                AuthoredOrdinal = AuthoredOrdinal,
                Label = Name,
                Lane = GetLane(Track),
                Track = this.Track,
                PresentationTrackIndex = PresentationTrackIndex,
                LaneLabel = string.IsNullOrWhiteSpace(PresentationTrackName)
                    ? GetTrackLabel(Track)
                    : PresentationTrackName,
                PreviewSummary = PreviewSummary,
                Color = Color,
                IsInvalid = IsInvalid
            };
        }

        public string PreviewSummary;

        private static int GetLane(FpgSkillEventTrackKind track)
        {
            switch (track)
            {
                case FpgSkillEventTrackKind.PresentationVfx:
                case FpgSkillEventTrackKind.PresentationAudio:
                case FpgSkillEventTrackKind.PresentationCameraShake:
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
                case FpgSkillEventTrackKind.PresentationVfx:
                    return "VFX";
                case FpgSkillEventTrackKind.PresentationAudio:
                    return "Audio";
                case FpgSkillEventTrackKind.PresentationCameraShake:
                    return "Camera Shake";
                case FpgSkillEventTrackKind.GameplayAction:
                    return "玩法动作";
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
        public FpgSkillEventKey EventKey;
        public int Tick = -1;
    }

    internal sealed class FpgSkillCompiledTriggerRecord
    {
        public int CompiledEventId;
        public int Tick;
        public int AuthoredOrdinal;
        public FpgSkillEventKey EventKey;
        // Preview simulation still correlates through the merged list position.
        public int EventIndex = -1;
        public string Kind;
        public string Name;
        public FpgCompiledSkillEvent CompiledEvent;
    }

    internal static class FpgSkillSerializedAdapter
    {
        private static readonly string[] SequenceNames = { "sequences" };
        private static readonly string[] ActivePresentationTrackArrayNames =
            { "activePresentationTracks" };
        private static readonly string[] VfxPresentationEventArrayNames =
            { "vfxEvents" };
        private static readonly string[] AudioPresentationEventArrayNames =
            { "audioEvents" };
        private static readonly string[] CameraShakePresentationEventArrayNames =
            { "cameraShakeEvents" };
        private static readonly string[] WarningEventArrayNames = { "warnings" };
        private static readonly string[] AttackEventArrayNames =
            { "attackEvents" };
        private static readonly string[] ProjectileEventArrayNames =
            { "projectileEvents" };
        private static readonly string[] ReloadEventArrayNames =
            { "reloadEvents" };
        private static readonly string[] SummonEventArrayNames =
            { "summonEvents" };
        private static readonly string[] SelfDestructEventArrayNames =
            { "selfDestructOwnerEvents", "selfDestructEvents" };
        private static readonly string[] DurationNames =
            { "durationTicks", "totalTicks", "lengthTicks", "endTick" };
        private static readonly string[] TickNames =
            { "tick", "triggerTick", "startTick", "releaseTick" };
        private static readonly string[] EventDurationNames =
            { "durationTicks", "activeTicks", "lengthTicks" };
        private static readonly string[] AuthoredOrdinalNames =
            { "authoredOrdinal" };

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

        public static SerializedProperty GetActivePresentationTracks(
            SerializedProperty sequence)
        {
            SerializedProperty tracks = FindFirstRelative(
                sequence,
                ActivePresentationTrackArrayNames);
            return tracks != null && tracks.isArray ? tracks : null;
        }

        public static List<FpgSkillActivePresentationTrackRecord>
            ReadActivePresentationTracks(SerializedProperty sequence)
        {
            List<FpgSkillActivePresentationTrackRecord> result =
                new List<FpgSkillActivePresentationTrackRecord>();
            SerializedProperty tracks = GetActivePresentationTracks(sequence);
            if (tracks == null)
            {
                return result;
            }

            for (int index = 0; index < tracks.arraySize; index++)
            {
                SerializedProperty track = tracks.GetArrayElementAtIndex(index);
                string trackId = ReadFirstString(track, "trackId");
                string displayName = ReadFirstString(track, "displayName");
                result.Add(new FpgSkillActivePresentationTrackRecord
                {
                    Index = index,
                    Id = trackId,
                    Name = string.IsNullOrWhiteSpace(displayName)
                        ? "Presentation " + (index + 1)
                        : displayName,
                    EventCount = CountPresentationTrackEvents(track)
                });
            }

            return result;
        }

        public static int AddActivePresentationTrack(
            SerializedObject serializedObject,
            int sequenceIndex)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null)
            {
                return -1;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty tracks = GetActivePresentationTracks(
                GetSequence(serializedObject, sequenceIndex));
            if (tracks == null || !tracks.isArray)
            {
                return -1;
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                "Add presentation track");
            int index = tracks.arraySize;
            tracks.InsertArrayElementAtIndex(index);
            SerializedProperty track = tracks.GetArrayElementAtIndex(index);
            ResetProperty(track);
            WriteString(
                track,
                "presentation.track." + Guid.NewGuid().ToString("N"),
                "trackId");
            WriteString(track, "Presentation " + (index + 1), "displayName");
            Apply(serializedObject);
            return index;
        }

        public static bool RenameActivePresentationTrack(
            SerializedObject serializedObject,
            int sequenceIndex,
            int trackIndex,
            string displayName)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null
                || string.IsNullOrWhiteSpace(displayName))
            {
                return false;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty track = GetActivePresentationTrack(
                GetSequence(serializedObject, sequenceIndex),
                trackIndex);
            SerializedProperty name = track?.FindPropertyRelative("displayName");
            if (name == null
                || name.propertyType != SerializedPropertyType.String)
            {
                return false;
            }

            string normalizedName = displayName.Trim();
            if (string.Equals(
                    name.stringValue,
                    normalizedName,
                    StringComparison.Ordinal))
            {
                return true;
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                "Rename presentation track");
            name.stringValue = normalizedName;
            Apply(serializedObject);
            return true;
        }

        public static bool MoveActivePresentationTrack(
            SerializedObject serializedObject,
            int sequenceIndex,
            int trackIndex,
            int requestedDelta,
            out int movedTrackIndex)
        {
            movedTrackIndex = trackIndex;
            if (serializedObject == null
                || serializedObject.targetObject == null
                || requestedDelta == 0)
            {
                return false;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty tracks = GetActivePresentationTracks(
                GetSequence(serializedObject, sequenceIndex));
            if (tracks == null
                || !tracks.isArray
                || trackIndex < 0
                || trackIndex >= tracks.arraySize)
            {
                return false;
            }

            movedTrackIndex = Mathf.Clamp(
                trackIndex + requestedDelta,
                0,
                tracks.arraySize - 1);
            if (movedTrackIndex == trackIndex)
            {
                return true;
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                "Reorder presentation track");
            tracks.MoveArrayElement(trackIndex, movedTrackIndex);
            Apply(serializedObject);
            return true;
        }

        public static bool CanDeleteActivePresentationTrack(
            SerializedProperty sequence,
            int trackIndex)
        {
            SerializedProperty track = GetActivePresentationTrack(
                sequence,
                trackIndex);
            return track != null && CountPresentationTrackEvents(track) == 0;
        }

        public static bool DeleteActivePresentationTrack(
            SerializedObject serializedObject,
            int sequenceIndex,
            int trackIndex)
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
            SerializedProperty tracks = GetActivePresentationTracks(sequence);
            if (tracks == null
                || !tracks.isArray
                || trackIndex < 0
                || trackIndex >= tracks.arraySize
                || !CanDeleteActivePresentationTrack(sequence, trackIndex))
            {
                return false;
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                "Delete empty presentation track");
            DeleteArrayElementWithoutApply(tracks, trackIndex);
            Apply(serializedObject);
            return true;
        }

        public static int GetDurationTicks(SerializedProperty sequence)
        {
            SerializedProperty property = FindFirstRelative(sequence, DurationNames);
            return ReadInt(property, 120, 0);
        }

        public static int GetChargeProgressTicks(
            SerializedObject serializedObject)
        {
            return ReadInt(
                serializedObject?.FindProperty("chargeProgressTicks"),
                30,
                0);
        }

        public static bool GetHoldUntilCanceled(SerializedProperty sequence)
        {
            SerializedProperty property = sequence?.FindPropertyRelative(
                "holdUntilCanceled");
            return property != null
                && property.propertyType == SerializedPropertyType.Boolean
                && property.boolValue;
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

        public static List<FpgSkillEventRecord> ReadEvents(
            SerializedProperty sequence,
            int durationTicks)
        {
            List<FpgSkillEventRecord> result =
                new List<FpgSkillEventRecord>();
            if (sequence == null)
            {
                return result;
            }

            AppendEventRecords(
                result,
                FindFirstRelative(sequence, AttackEventArrayNames),
                FpgSkillEventTrackKind.GameplayAction,
                durationTicks,
                FpgSkillActionKind.Attack);
            AppendEventRecords(
                result,
                FindFirstRelative(sequence, ProjectileEventArrayNames),
                FpgSkillEventTrackKind.GameplayAction,
                durationTicks,
                FpgSkillActionKind.LaunchProjectile);
            AppendEventRecords(
                result,
                FindFirstRelative(sequence, ReloadEventArrayNames),
                FpgSkillEventTrackKind.GameplayAction,
                durationTicks,
                FpgSkillActionKind.CommitReload);
            AppendEventRecords(
                result,
                FindFirstRelative(sequence, SummonEventArrayNames),
                FpgSkillEventTrackKind.GameplayAction,
                durationTicks,
                FpgSkillActionKind.SummonActors);
            AppendEventRecords(
                result,
                FindFirstRelative(sequence, SelfDestructEventArrayNames),
                FpgSkillEventTrackKind.GameplayAction,
                durationTicks,
                FpgSkillActionKind.SelfDestructOwner);

            SerializedProperty tracks = GetActivePresentationTracks(sequence);
            if (tracks != null)
            {
                for (int trackIndex = 0;
                    trackIndex < tracks.arraySize;
                    trackIndex++)
                {
                    SerializedProperty track =
                        tracks.GetArrayElementAtIndex(trackIndex);
                    string trackId = ReadFirstString(track, "trackId");
                    string trackName = ReadFirstString(track, "displayName");
                    AppendEventRecords(
                        result,
                        FindFirstRelative(
                            track,
                            VfxPresentationEventArrayNames),
                        FpgSkillEventTrackKind.PresentationVfx,
                        durationTicks,
                        FpgSkillActionKind.None,
                        trackIndex,
                        trackId,
                        trackName);
                    AppendEventRecords(
                        result,
                        FindFirstRelative(
                            track,
                            AudioPresentationEventArrayNames),
                        FpgSkillEventTrackKind.PresentationAudio,
                        durationTicks,
                        FpgSkillActionKind.None,
                        trackIndex,
                        trackId,
                        trackName);
                    AppendEventRecords(
                        result,
                        FindFirstRelative(
                            track,
                            CameraShakePresentationEventArrayNames),
                        FpgSkillEventTrackKind.PresentationCameraShake,
                        durationTicks,
                        FpgSkillActionKind.None,
                        trackIndex,
                        trackId,
                        trackName);
                }
            }

            AppendEventRecords(
                result,
                FindFirstRelative(sequence, WarningEventArrayNames),
                FpgSkillEventTrackKind.Warning,
                durationTicks);
            result.Sort(CompareAuthoredEvents);
            for (int index = 0; index < result.Count; index++)
            {
                result[index].Index = index;
            }

            return result;
        }

        private static void PopulatePreviewSummary(
            SerializedProperty action,
            FpgSkillActionPreviewRecord record)
        {
            string kind = record.Kind ?? string.Empty;
            string queryMode = ReadDisplayValue(action, "queryMode", "queryPolicy");
            string discriminator = kind + " " + queryMode;
            if (ContainsAny(discriminator, "PelletRay", "Pellet Ray", "Ray", "射线"))
            {
                record.PreviewKind = FpgSkillPreviewActionKind.PlayerPelletRay;
                record.HitShape = "射线";
                record.PelletCount = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(action, "pelletCount"),
                        1));
                record.AdditionalPenetrationCount = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(action, "additionalPenetrationCount"),
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
                    FpgSkillPreviewActionKind.PlayerAreaAtFirstSurface;
                record.HitShape = "范围";
                record.AreaCombatantLimit = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(action, "areaCombatantLimit"),
                        0));
                record.AreaProjectileLimit = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(action, "areaProjectileLimit"),
                        0));
                record.MaxHitCount = SaturatingAdd(
                    record.AreaCombatantLimit,
                    record.AreaProjectileLimit);
            }
            else if (ContainsAny(discriminator, "Projectile", "弹道", "投射"))
            {
                record.PreviewKind = FpgSkillPreviewActionKind.EnemyProjectile;
                record.HitShape = "弹道";
                record.ImpactDelayTicks = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(action, "projectileFlightTicks"),
                        0));
                record.ProjectileCount = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(action, "projectileCount"),
                        1));
                record.MaxHitCount = record.ProjectileCount;
            }
            else if (ContainsAny(
                         discriminator,
                         "TimedImpact",
                         "Timed Impact",
                         "延迟"))
            {
                record.PreviewKind = FpgSkillPreviewActionKind.EnemyTimedImpact;
                record.HitShape = "延迟命中";
                record.ImpactDelayTicks = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(action, "timedImpactDelayTicks"),
                        0));
                record.MaxHitCount = 1;
            }
            else if (ContainsAny(discriminator, "Summon", "召唤"))
            {
                record.PreviewKind = FpgSkillPreviewActionKind.EnemySummon;
                record.HitShape = "召唤";
                SerializedProperty candidates = FindFirstRelative(
                    action,
                    "summonCandidates");
                record.SummonCandidateCount = candidates != null
                    && candidates.isArray
                        ? candidates.arraySize
                        : 0;
                record.MaxHitCount = record.SummonCandidateCount;
            }
            else if (ContainsAny(
                         discriminator,
                         "SelfDestruct",
                         "Self Destruct"))
            {
                record.PreviewKind =
                    FpgSkillPreviewActionKind.EnemySelfDestruct;
                record.HitShape = "召唤者自毁";
                record.MaxHitCount = 1;
            }
            else if (ContainsAny(discriminator, "Reload", "装填"))
            {
                record.PreviewKind = FpgSkillPreviewActionKind.PlayerReload;
                record.HitShape = "装填";
                record.MaxHitCount = 0;
            }
            else
            {
                record.PreviewKind = FpgSkillPreviewActionKind.Unknown;
                record.HitShape = string.IsNullOrWhiteSpace(kind)
                    ? "未分类"
                    : kind;
                record.ImpactDelayTicks = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(
                            action,
                            "impactDelayTicks",
                            "delayTicks"),
                        0));
                record.MaxHitCount = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(
                            action,
                            "maxHitCount",
                            "maxImpactCount"),
                        0));
            }

            SerializedProperty baseDamage = FindFirstRelative(
                action,
                "baseDamage",
                "damage");
            SerializedProperty breakDamage = FindFirstRelative(
                action,
                "breakDamage");
            record.HasDamagePreview = baseDamage != null || breakDamage != null;
            record.BaseDamage = Mathf.Max(0, ReadRawInt(baseDamage, 0));
            record.BreakDamage = Mathf.Max(0, ReadRawInt(breakDamage, 0));
            int weakpointDamageBasisPoints = Mathf.Max(
                0,
                ReadRawInt(
                    FindFirstRelative(
                        action,
                        "weakpointDamageMultiplierBasisPoints"),
                    10000));
            int weakpointBreakBasisPoints = Mathf.Max(
                0,
                ReadRawInt(
                    FindFirstRelative(
                        action,
                        "weakpointBreakMultiplierBasisPoints"),
                    10000));
            record.WeakpointDamage = RoundBasisPoints(
                record.BaseDamage,
                weakpointDamageBasisPoints);
            record.WeakpointBreakDamage = RoundBasisPoints(
                record.BreakDamage,
                weakpointBreakBasisPoints);
        }

        private static FpgSkillActionPreviewRecord BuildActionPreview(
            SerializedProperty action,
            FpgSkillActionKind actionKind,
            int arrayIndex)
        {
            string kind;
            switch (actionKind)
            {
                case FpgSkillActionKind.Attack:
                    SerializedProperty attackModeProperty =
                        FindFirstRelative(action, "mode");
                    string attackMode = attackModeProperty != null
                        && attackModeProperty.propertyType
                            == SerializedPropertyType.Enum
                        && attackModeProperty.enumValueIndex >= 0
                        && attackModeProperty.enumValueIndex
                            < attackModeProperty.enumNames.Length
                            ? attackModeProperty.enumNames[
                                attackModeProperty.enumValueIndex]
                            : ReadDisplayValue(attackModeProperty);
                    kind = string.Equals(
                        attackMode,
                        "BoundTarget",
                        StringComparison.Ordinal)
                            ? "TimedImpact"
                            : attackMode;
                    break;
                case FpgSkillActionKind.LaunchProjectile:
                    kind = "Projectile";
                    break;
                case FpgSkillActionKind.CommitReload:
                    kind = "ReloadCommit";
                    break;
                case FpgSkillActionKind.SummonActors:
                    kind = "Summon";
                    break;
                case FpgSkillActionKind.SelfDestructOwner:
                    kind = "SelfDestructOwner";
                    break;
                default:
                    kind = string.Empty;
                    break;
            }

            FpgSkillActionPreviewRecord record = new FpgSkillActionPreviewRecord
            {
                Name = GetActionKindLabel(actionKind),
                Kind = kind,
                Color = GetPaletteColor(arrayIndex)
            };
            PopulatePreviewSummary(action, record);
            if (actionKind == FpgSkillActionKind.Attack
                && string.Equals(
                    kind,
                    "TimedImpact",
                    StringComparison.Ordinal))
            {
                record.ImpactDelayTicks = Mathf.Max(
                    0,
                    ReadRawInt(
                        FindFirstRelative(action, "delayTicks"),
                        0));
                record.MaxHitCount = 1;
            }

            return record;
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

        private static void AppendEventRecords(
            ICollection<FpgSkillEventRecord> destination,
            SerializedProperty eventArray,
            FpgSkillEventTrackKind track,
            int durationTicks,
            FpgSkillActionKind actionKind = FpgSkillActionKind.None,
            int presentationTrackIndex = -1,
            string presentationTrackId = null,
            string presentationTrackName = null)
        {
            if (eventArray == null || !eventArray.isArray)
            {
                return;
            }

            for (int arrayIndex = 0;
                arrayIndex < eventArray.arraySize;
                arrayIndex++)
            {
                SerializedProperty eventProperty =
                    eventArray.GetArrayElementAtIndex(arrayIndex);
                int tick = track == FpgSkillEventTrackKind.Warning
                    ? ReadRawInt(
                        eventProperty.FindPropertyRelative("startTick"),
                        0)
                    : ReadRawInt(
                        FindFirstRelative(eventProperty, TickNames),
                        0);
                int eventDuration;
                if (track == FpgSkillEventTrackKind.Warning)
                {
                    int endTick = ReadRawInt(
                        eventProperty.FindPropertyRelative("endTick"),
                        tick);
                    eventDuration = Mathf.Max(0, endTick - tick);
                }
                else
                {
                    eventDuration = ReadRawInt(
                        FindFirstRelative(
                            eventProperty,
                            EventDurationNames),
                        0);
                }

                int authoredOrdinal = ReadRawInt(
                    FindFirstRelative(
                        eventProperty,
                        AuthoredOrdinalNames),
                    arrayIndex);
                string eventId = ReadFirstString(
                    eventProperty,
                    "eventId");
                FpgSkillActionPreviewRecord actionPreview =
                    actionKind == FpgSkillActionKind.None
                        ? null
                        : BuildActionPreview(
                            eventProperty,
                            actionKind,
                            arrayIndex);
                string kind = actionKind == FpgSkillActionKind.None
                    ? GetTrackLabel(track, eventProperty)
                    : GetActionKindLabel(actionKind);
                string detail = track == FpgSkillEventTrackKind.Warning
                    ? ReadFirstString(eventProperty, "warningId")
                    : string.Empty;
                string name = string.IsNullOrWhiteSpace(eventId)
                    ? kind + " " + (arrayIndex + 1)
                    : eventId;
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    name += " / " + detail;
                }

                FpgSkillEventKey key = MakeEventKey(
                    track,
                    actionKind,
                    presentationTrackIndex,
                    arrayIndex);
                destination.Add(new FpgSkillEventRecord
                {
                    Key = key,
                    ArrayIndex = arrayIndex,
                    Tick = tick,
                    DurationTicks = eventDuration,
                    AuthoredOrdinal = authoredOrdinal,
                    EventId = eventId,
                    SocketId = ReadFirstString(eventProperty, "socketId"),
                    Name = name,
                    Kind = kind,
                    TargetSource = ReadTargetSource(eventProperty),
                    TargetOffset = ReadVector3(
                        eventProperty.FindPropertyRelative("targetOffset"),
                        Vector3.zero),
                    Track = track,
                    PresentationTrackIndex = presentationTrackIndex,
                    PresentationTrackId = presentationTrackId,
                    PresentationTrackName = presentationTrackName,
                    InlineActionPreview = actionPreview,
                    IsInvalid = tick < 0
                        || tick > durationTicks
                        || eventDuration < 0
                        || (long)tick + eventDuration > durationTicks,
                    PreviewSummary = actionPreview?.BuildPreviewSummary(tick)
                        ?? string.Empty,
                    Color = GetTrackColor(track, arrayIndex)
                });
            }
        }

        public static List<FpgSkillValidationItem> Validate(
            SerializedObject serializedObject,
            int sequenceIndex,
            IList<FpgSkillEventRecord> events,
            int durationTicks,
            int actualAnimationDurationTicks = -1,
            GameObject previewPrefab = null,
            bool includeRuntimeValidation = true)
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

            if (GetHoldUntilCanceled(sequence)
                && events != null
                && events.Any(item =>
                    item != null
                    && item.Track == FpgSkillEventTrackKind.GameplayAction))
            {
                result.Add(Error(
                    "A sequence held until canceled cannot contain gameplay actions."));
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



            for (int index = 0; index < events.Count; index++)
            {
                FpgSkillEventRecord eventRecord = events[index];
                if (eventRecord.Tick < 0 || eventRecord.Tick > durationTicks)
                {
                    result.Add(new FpgSkillValidationItem
                    {
                        Severity = FpgSkillIssueSeverity.Error,
                        Message = "事件“" + eventRecord.Name + "”超出序列时长。",
                        EventKey = eventRecord.Key,
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
                        EventKey = eventRecord.Key,
                        Tick = eventRecord.Tick
                    });
                }


            }

            AppendActivePresentationValidation(
                result,
                sequence,
                events);
            AppendAuthoredPositionValidation(result, events);
            AppendPreviewPrefabValidation(
                result,
                sequence,
                events,
                previewPrefab);
            if (includeRuntimeValidation)
            {
                AppendRuntimeValidation(result, serializedObject, events);
            }

            if (result.Count == 0)
            {
                result.Add(Info("当前序列通过编辑器基础校验。"));
            }

            return result;
        }

        private static void AppendActivePresentationValidation(
            ICollection<FpgSkillValidationItem> result,
            SerializedProperty sequence,
            IList<FpgSkillEventRecord> events)
        {
            SerializedProperty tracks = GetActivePresentationTracks(sequence);
            if (tracks == null)
            {
                return;
            }

            HashSet<string> trackIds = new HashSet<string>(
                StringComparer.Ordinal);
            for (int index = 0; index < tracks.arraySize; index++)
            {
                SerializedProperty track = tracks.GetArrayElementAtIndex(index);
                string trackId = ReadFirstString(track, "trackId");
                string displayName = ReadFirstString(track, "displayName");
                if (string.IsNullOrWhiteSpace(trackId)
                    || !trackIds.Add(trackId))
                {
                    result.Add(Error(
                        "表现轨道 " + (index + 1)
                        + " 缺少唯一稳定 ID。"));
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    result.Add(Error(
                        "表现轨道 " + (index + 1)
                        + " 缺少显示名称。"));
                }
            }

            Dictionary<string, int> gameplayTicks =
                new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < events.Count; index++)
            {
                FpgSkillEventRecord eventRecord = events[index];
                if (eventRecord.Track == FpgSkillEventTrackKind.GameplayAction
                    && !string.IsNullOrWhiteSpace(eventRecord.EventId))
                {
                    gameplayTicks[eventRecord.EventId] = eventRecord.Tick;
                }
            }

            for (int index = 0; index < events.Count; index++)
            {
                FpgSkillEventRecord eventRecord = events[index];
                if (!IsActivePresentationEventTrack(eventRecord.Track))
                {
                    continue;
                }

                SerializedProperty eventProperty = GetEventArray(
                        sequence,
                        eventRecord.Key)
                    ?.GetArrayElementAtIndex(eventRecord.Key.LocalIndex);
                if (eventProperty == null)
                {
                    continue;
                }

                string binding = ReadFirstString(
                    eventProperty,
                    "boundGameplayEventId");
                if (!string.IsNullOrWhiteSpace(binding)
                    && (!gameplayTicks.TryGetValue(
                            binding,
                            out int gameplayTick)
                        || eventRecord.Tick < gameplayTick))
                {
                    result.Add(EventError(
                        eventRecord,
                        "表现事件“" + eventRecord.Name
                        + "”必须绑定现有逻辑事件，且触发 Tick 不能早于它。"));
                }

                SerializedProperty presentation =
                    eventProperty.FindPropertyRelative("presentation");
                switch (eventRecord.Track)
                {
                    case FpgSkillEventTrackKind.PresentationVfx:
                        ValidateVfxPresentation(
                            result,
                            eventRecord,
                            eventProperty,
                            presentation);
                        break;
                    case FpgSkillEventTrackKind.PresentationAudio:
                        ValidateAudioPresentation(
                            result,
                            eventRecord,
                            presentation);
                        break;
                    case FpgSkillEventTrackKind.PresentationCameraShake:
                        ValidateCameraShakePresentation(
                            result,
                            eventRecord,
                            presentation);
                        break;
                }
            }
        }

        private static void ValidateVfxPresentation(
            ICollection<FpgSkillValidationItem> result,
            FpgSkillEventRecord eventRecord,
            SerializedProperty eventProperty,
            SerializedProperty presentation)
        {
            SerializedProperty prefab =
                presentation?.FindPropertyRelative("prefab");
            float duration = ReadFloat(
                presentation?.FindPropertyRelative("durationSeconds"),
                0f);
            Vector3 scale = ReadVector3(
                presentation?.FindPropertyRelative("scale"),
                Vector3.zero);
            Vector3 rotation = ReadVector3(
                presentation?.FindPropertyRelative("rotationOffsetEuler"),
                new Vector3(float.NaN, 0f, 0f));
            if (prefab == null
                || prefab.objectReferenceValue == null
                || !IsFinitePositive(duration)
                || !IsFinitePositive(scale.x)
                || !IsFinitePositive(scale.y)
                || !IsFinitePositive(scale.z)
                || !IsFinite(rotation.x)
                || !IsFinite(rotation.y)
                || !IsFinite(rotation.z))
            {
                result.Add(EventError(
                    eventRecord,
                    "特效事件“" + eventRecord.Name
                    + "”需要 Prefab、正持续时间和正缩放。"));
            }

            SerializedProperty anchorProperty =
                eventProperty.FindPropertyRelative("anchor");
            int anchor = anchorProperty != null
                && anchorProperty.propertyType == SerializedPropertyType.Enum
                    ? anchorProperty.intValue
                    : -1;
            string socketId = ReadFirstString(eventProperty, "socketId");
            if ((anchor == 0 && !string.IsNullOrEmpty(socketId))
                || (anchor == 1 && string.IsNullOrWhiteSpace(socketId))
                || (anchor != 0 && anchor != 1))
            {
                result.Add(EventError(
                    eventRecord,
                    "特效事件“" + eventRecord.Name
                    + "”的 Owner Root / Owner Socket 配置无效。"));
            }
        }

        private static void ValidateAudioPresentation(
            ICollection<FpgSkillValidationItem> result,
            FpgSkillEventRecord eventRecord,
            SerializedProperty presentation)
        {
            SerializedProperty clip = presentation?.FindPropertyRelative("clip");
            float volume = ReadFloat(
                presentation?.FindPropertyRelative("volume"),
                -1f);
            if (clip == null
                || clip.objectReferenceValue == null
                || !IsFinite(volume)
                || volume < 0f
                || volume > 1f)
            {
                result.Add(EventError(
                    eventRecord,
                    "音效事件“" + eventRecord.Name
                    + "”需要 AudioClip 和 0-1 音量。"));
            }
        }

        private static void ValidateCameraShakePresentation(
            ICollection<FpgSkillValidationItem> result,
            FpgSkillEventRecord eventRecord,
            SerializedProperty presentation)
        {
            float strength = ReadFloat(
                presentation?.FindPropertyRelative("strength"),
                -1f);
            float duration = ReadFloat(
                presentation?.FindPropertyRelative("durationSeconds"),
                0f);
            if (!IsFinite(strength)
                || strength < 0f
                || !IsFinitePositive(duration))
            {
                result.Add(EventError(
                    eventRecord,
                    "震屏事件“" + eventRecord.Name
                    + "”需要非负强度和正持续时间。"));
            }
        }

        private static FpgSkillValidationItem EventError(
            FpgSkillEventRecord eventRecord,
            string message)
        {
            return new FpgSkillValidationItem
            {
                Severity = FpgSkillIssueSeverity.Error,
                Message = message,
                EventKey = eventRecord.Key,
                Tick = eventRecord.Tick
            };
        }

        private static float ReadFloat(
            SerializedProperty property,
            float fallback)
        {
            return property != null
                && property.propertyType == SerializedPropertyType.Float
                    ? property.floatValue
                    : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
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
                        EventKey = authored?.Key
                            ?? FpgSkillEventKey.Invalid,
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



        public static SerializedProperty GetEventProperty(
            SerializedObject serializedObject,
            int sequenceIndex,
            FpgSkillEventKey eventKey)
        {
            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            SerializedProperty events = GetEventArray(sequence, eventKey);
            return events != null
                && events.isArray
                && eventKey.IsValid
                && eventKey.LocalIndex < events.arraySize
                    ? events.GetArrayElementAtIndex(eventKey.LocalIndex)
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
            FpgSkillEventKey eventKey,
            int tick)
        {
            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            SerializedProperty eventProperty = GetEventProperty(
                serializedObject,
                sequenceIndex,
                eventKey);
            FpgSkillEventTrackKind track = eventKey.Track;
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

        public static FpgSkillEventKey AddAction(
            SerializedObject serializedObject,
            int sequenceIndex,
            int tick,
            FpgSkillActionKind actionKind,
            int modeValue = 0)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null
                || actionKind == FpgSkillActionKind.None)
            {
                return FpgSkillEventKey.Invalid;
            }

            SerializedProperty sequence = GetSequence(
                serializedObject,
                sequenceIndex);
            FpgSkillEventKey lookupKey = new FpgSkillEventKey(
                FpgSkillEventTrackKind.GameplayAction,
                actionKind,
                0);
            SerializedProperty eventArray = GetEventArray(sequence, lookupKey);
            if (eventArray == null || !eventArray.isArray)
            {
                return FpgSkillEventKey.Invalid;
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                "添加技能玩法动作");
            int index = eventArray.arraySize;
            eventArray.InsertArrayElementAtIndex(index);
            SerializedProperty action = eventArray.GetArrayElementAtIndex(index);
            ResetProperty(action);
            FpgSkillEventKey eventKey = new FpgSkillEventKey(
                FpgSkillEventTrackKind.GameplayAction,
                actionKind,
                index);
            int normalizedTick = Mathf.Clamp(
                tick,
                0,
                GetDurationTicks(sequence));
            string eventId = "event." + Guid.NewGuid().ToString("N");
            WriteString(action, eventId, "eventId");
            WriteInt(action, normalizedTick, "tick");
            WriteInt(
                action,
                FindNextAuthoredOrdinal(sequence, eventKey),
                "authoredOrdinal");
            ConfigureDefaultAction(
                action,
                actionKind,
                modeValue,
                IsEnemyAsset(serializedObject));
            NormalizeActionSpatialMetadata(
                action,
                actionKind,
                IsEnemyAsset(serializedObject));
            Apply(serializedObject);
            return eventKey;
        }

        public static FpgSkillEventKey AddActivePresentationEvent(
            SerializedObject serializedObject,
            int sequenceIndex,
            int presentationTrackIndex,
            FpgSkillEventTrackKind eventTrack,
            int tick)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null
                || !IsActivePresentationEventTrack(eventTrack))
            {
                return FpgSkillEventKey.Invalid;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty sequence = GetSequence(
                serializedObject,
                sequenceIndex);
            SerializedProperty eventArray = GetActivePresentationEventArray(
                sequence,
                presentationTrackIndex,
                eventTrack);
            if (eventArray == null || !eventArray.isArray)
            {
                return FpgSkillEventKey.Invalid;
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                "Add active presentation event");
            int index = eventArray.arraySize;
            eventArray.InsertArrayElementAtIndex(index);
            SerializedProperty eventProperty =
                eventArray.GetArrayElementAtIndex(index);
            ResetProperty(eventProperty);
            FpgSkillEventKey eventKey = MakeEventKey(
                eventTrack,
                FpgSkillActionKind.None,
                presentationTrackIndex,
                index);
            WriteString(
                eventProperty,
                "event." + Guid.NewGuid().ToString("N"),
                "eventId");
            WriteInt(
                eventProperty,
                Mathf.Clamp(tick, 0, GetDurationTicks(sequence)),
                "tick");
            WriteInt(
                eventProperty,
                FindNextAuthoredOrdinal(sequence, eventKey),
                "authoredOrdinal");
            WriteString(eventProperty, string.Empty, "boundGameplayEventId");
            ConfigureDefaultActivePresentationEvent(
                eventProperty,
                eventTrack);
            Apply(serializedObject);
            return eventKey;
        }

        public static FpgSkillEventKey MoveActivePresentationEventToTrack(
            SerializedObject serializedObject,
            int sequenceIndex,
            FpgSkillEventKey sourceKey,
            int targetPresentationTrackIndex)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null
                || !sourceKey.IsValid
                || !IsActivePresentationEventTrack(sourceKey.Track)
                || sourceKey.PresentationTrackIndex
                    == targetPresentationTrackIndex)
            {
                return FpgSkillEventKey.Invalid;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty sequence = GetSequence(
                serializedObject,
                sequenceIndex);
            SerializedProperty sourceArray = GetEventArray(
                sequence,
                sourceKey);
            FpgSkillEventKey targetArrayKey = MakeEventKey(
                sourceKey.Track,
                FpgSkillActionKind.None,
                targetPresentationTrackIndex,
                0);
            SerializedProperty targetArray = GetEventArray(
                sequence,
                targetArrayKey);
            if (sourceArray == null
                || targetArray == null
                || !sourceArray.isArray
                || !targetArray.isArray
                || sourceKey.LocalIndex < 0
                || sourceKey.LocalIndex >= sourceArray.arraySize)
            {
                return FpgSkillEventKey.Invalid;
            }

            FpgSerializedPropertySnapshot snapshot =
                FpgSerializedPropertySnapshot.Capture(
                    sourceArray.GetArrayElementAtIndex(
                        sourceKey.LocalIndex));
            Undo.RecordObject(
                serializedObject.targetObject,
                "Move active presentation event to track");
            int targetIndex = targetArray.arraySize;
            targetArray.InsertArrayElementAtIndex(targetIndex);
            SerializedProperty target =
                targetArray.GetArrayElementAtIndex(targetIndex);
            ResetProperty(target);
            snapshot.ApplyTo(target);
            DeleteArrayElementWithoutApply(
                sourceArray,
                sourceKey.LocalIndex);
            Apply(serializedObject);
            return MakeEventKey(
                sourceKey.Track,
                FpgSkillActionKind.None,
                targetPresentationTrackIndex,
                targetIndex);
        }

        public static bool SetActionMode(
            SerializedObject serializedObject,
            int sequenceIndex,
            FpgSkillEventKey eventKey,
            int modeValue)
        {
            if (serializedObject == null
                || (eventKey.ActionKind != FpgSkillActionKind.Attack
                    && eventKey.ActionKind
                        != FpgSkillActionKind.LaunchProjectile))
            {
                return false;
            }

            SerializedProperty action = GetEventProperty(
                serializedObject,
                sequenceIndex,
                eventKey);
            if (action == null)
            {
                return false;
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                "切换玩法动作模式");
            ConfigureActionModeDefaults(
                action,
                eventKey.ActionKind,
                modeValue,
                IsEnemyAsset(serializedObject));
            NormalizeActionSpatialMetadata(
                action,
                eventKey.ActionKind,
                IsEnemyAsset(serializedObject));
            Apply(serializedObject);
            return true;
        }

        public static FpgSkillEventKey ConvertAction(
            SerializedObject serializedObject,
            int sequenceIndex,
            FpgSkillEventKey sourceKey,
            FpgSkillActionKind targetKind,
            int targetModeValue)
        {
            if (serializedObject == null
                || !sourceKey.IsValid
                || sourceKey.ActionKind == FpgSkillActionKind.None
                || targetKind == FpgSkillActionKind.None
                || targetKind == sourceKey.ActionKind)
            {
                return FpgSkillEventKey.Invalid;
            }

            SerializedProperty sequence = GetSequence(
                serializedObject,
                sequenceIndex);
            SerializedProperty sourceArray = GetEventArray(sequence, sourceKey);
            SerializedProperty source = GetEventProperty(
                serializedObject,
                sequenceIndex,
                sourceKey);
            FpgSkillEventKey targetLookup = new FpgSkillEventKey(
                FpgSkillEventTrackKind.GameplayAction,
                targetKind,
                0);
            SerializedProperty targetArray = GetEventArray(
                sequence,
                targetLookup);
            if (sourceArray == null
                || source == null
                || targetArray == null
                || !targetArray.isArray)
            {
                return FpgSkillEventKey.Invalid;
            }

            string eventId = ReadFirstString(source, "eventId");
            int tick = ReadRawInt(
                source.FindPropertyRelative("tick"),
                0);
            int authoredOrdinal = ReadRawInt(
                source.FindPropertyRelative("authoredOrdinal"),
                0);

            Undo.RecordObject(
                serializedObject.targetObject,
                "转换玩法动作类型");
            int targetIndex = targetArray.arraySize;
            targetArray.InsertArrayElementAtIndex(targetIndex);
            SerializedProperty target =
                targetArray.GetArrayElementAtIndex(targetIndex);
            ResetProperty(target);
            ConfigureDefaultAction(
                target,
                targetKind,
                targetModeValue,
                IsEnemyAsset(serializedObject));
            WriteString(target, eventId, "eventId");
            WriteInt(target, tick, "tick");
            WriteInt(target, authoredOrdinal, "authoredOrdinal");
            CopyCompatibleInteger(source, target, "ammoCost");
            CopyCompatibleInteger(source, target, "baseDamage");
            CopyCompatibleInteger(source, target, "breakDamage");
            CopyCompatibleInteger(
                source,
                target,
                "weakpointDamageMultiplierBasisPoints");
            CopyCompatibleInteger(
                source,
                target,
                "weakpointBreakMultiplierBasisPoints");
            CopyCompatibleString(source, target, "socketId");
            CopyCompatibleEnum(source, target, "targetSource");
            CopyCompatibleVector3(source, target, "targetOffset");
            NormalizeConvertedActionSpatialMetadata(
                target,
                targetKind,
                IsEnemyAsset(serializedObject));
            sourceArray.DeleteArrayElementAtIndex(sourceKey.LocalIndex);
            Apply(serializedObject);
            return new FpgSkillEventKey(
                FpgSkillEventTrackKind.GameplayAction,
                targetKind,
                targetIndex);
        }

        public static FpgSkillEventKey AddEvent(
            SerializedObject serializedObject,
            int sequenceIndex,
            int tick,
            FpgSkillEventTrackKind track,
            int eventDurationTicks = 0)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null
                || track != FpgSkillEventTrackKind.Warning)
            {
                return FpgSkillEventKey.Invalid;
            }

            SerializedProperty sequence = GetSequence(
                serializedObject,
                sequenceIndex);
            SerializedProperty eventArray = GetEventArray(sequence, track);
            if (eventArray == null || !eventArray.isArray)
            {
                return FpgSkillEventKey.Invalid;
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                "Add skill warning");
            int index = eventArray.arraySize;
            eventArray.InsertArrayElementAtIndex(index);
            SerializedProperty warning =
                eventArray.GetArrayElementAtIndex(index);
            ResetProperty(warning);
            int normalizedTick = Mathf.Clamp(
                tick,
                0,
                GetDurationTicks(sequence));
            WriteInt(warning, normalizedTick, "startTick");
            WriteInt(
                warning,
                Mathf.Min(
                    GetDurationTicks(sequence),
                    normalizedTick + Mathf.Max(1, eventDurationTicks)),
                "endTick");
            WriteInt(
                warning,
                FindNextAuthoredOrdinal(
                    sequence,
                    MakeEventKey(track, index)),
                AuthoredOrdinalNames);
            WriteString(
                warning,
                "warning." + Guid.NewGuid().ToString("N"),
                "warningId");
            Apply(serializedObject);
            return MakeEventKey(track, index);
        }

        public static FpgSkillEventKey DuplicateEvent(
            SerializedObject serializedObject,
            int sequenceIndex,
            FpgSkillEventKey eventKey,
            int durationTicks)
        {
            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            FpgSkillEventTrackKind track = eventKey.Track;
            int arrayIndex = eventKey.LocalIndex;
            SerializedProperty eventArray = GetEventArray(sequence, eventKey);
            if (eventArray == null
                || !eventArray.isArray
                || arrayIndex < 0
                || arrayIndex >= eventArray.arraySize)
            {
                return FpgSkillEventKey.Invalid;
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
                    MakeEventKey(
                        track,
                        eventKey.ActionKind,
                        eventKey.PresentationTrackIndex,
                        insertionIndex)),
                AuthoredOrdinalNames);
            WriteString(copy, "event." + Guid.NewGuid().ToString("N"), "eventId", "id");
            if (eventKey.ActionKind != FpgSkillActionKind.None)
            {
                NormalizeActionSpatialMetadata(
                    copy,
                    eventKey.ActionKind,
                    IsEnemyAsset(serializedObject));
            }

            Apply(serializedObject);
            return MakeEventKey(
                track,
                eventKey.ActionKind,
                eventKey.PresentationTrackIndex,
                insertionIndex);
        }

        public static bool CopyEvents(
            SerializedObject serializedObject,
            int sequenceIndex,
            IEnumerable<FpgSkillEventKey> eventKeys,
            FpgSkillEventClipboard clipboard)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null
                || eventKeys == null
                || clipboard == null)
            {
                return false;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            List<FpgSkillEventRecord> authored = ReadEvents(
                sequence,
                GetDurationTicks(sequence));
            HashSet<FpgSkillEventKey> selected =
                new HashSet<FpgSkillEventKey>(eventKeys);
            List<FpgSkillEventRecord> copied = new List<FpgSkillEventRecord>();
            for (int index = 0; index < authored.Count; index++)
            {
                if (selected.Contains(authored[index].Key))
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
                    record.Key);
                if (property == null)
                {
                    continue;
                }

                items.Add(new FpgSkillEventClipboardItem
                {
                    Track = record.Track,
                    ActionKind = record.Key.ActionKind,
                    PresentationTrackId = record.PresentationTrackId,
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

        public static List<FpgSkillEventKey> PasteEvents(
            SerializedObject serializedObject,
            int sequenceIndex,
            FpgSkillEventClipboard clipboard,
            int anchorTick)
        {
            List<FpgSkillEventKey> result =
                new List<FpgSkillEventKey>();
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
            int nextOrdinal = FindNextAuthoredOrdinal(
                sequence,
                FpgSkillEventKey.Invalid);
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

            int[] presentationTrackIndices =
                new int[clipboard.Items.Count];
            for (int index = 0; index < clipboard.Items.Count; index++)
            {
                FpgSkillEventClipboardItem item = clipboard.Items[index];
                int presentationTrackIndex =
                    IsActivePresentationEventTrack(item.Track)
                        ? FindActivePresentationTrackIndex(
                            sequence,
                            item.PresentationTrackId)
                        : -1;
                presentationTrackIndices[index] = presentationTrackIndex;
                FpgSkillEventKey arrayKey = MakeEventKey(
                    item.Track,
                    item.ActionKind,
                    presentationTrackIndex,
                    0);
                if (GetEventArray(sequence, arrayKey) == null)
                {
                    return result;
                }
            }

            Undo.RecordObject(serializedObject.targetObject, "粘贴技能事件");
            for (int index = 0; index < clipboard.Items.Count; index++)
            {
                FpgSkillEventClipboardItem item = clipboard.Items[index];
                int presentationTrackIndex =
                    presentationTrackIndices[index];
                FpgSkillEventKey arrayKey = MakeEventKey(
                    item.Track,
                    item.ActionKind,
                    presentationTrackIndex,
                    0);
                SerializedProperty eventArray = GetEventArray(sequence, arrayKey);
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
                result.Add(MakeEventKey(
                    item.Track,
                    item.ActionKind,
                    presentationTrackIndex,
                    arrayIndex));
            }

            Apply(serializedObject);
            return result;
        }

        public static bool MoveEventsByDelta(
            SerializedObject serializedObject,
            int sequenceIndex,
            IEnumerable<FpgSkillEventKey> eventKeys,
            int requestedDeltaTicks,
            out int appliedDeltaTicks)
        {
            appliedDeltaTicks = 0;
            if (serializedObject == null
                || serializedObject.targetObject == null
                || eventKeys == null)
            {
                return false;
            }

            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            int durationTicks = GetDurationTicks(sequence);
            List<FpgSkillEventRecord> authored = ReadEvents(
                sequence,
                durationTicks);
            HashSet<FpgSkillEventKey> selected =
                new HashSet<FpgSkillEventKey>(eventKeys);
            List<FpgSkillEventRecord> moving = new List<FpgSkillEventRecord>();
            int minimumDelta = int.MinValue;
            int maximumDelta = int.MaxValue;
            for (int index = 0; index < authored.Count; index++)
            {
                FpgSkillEventRecord record = authored[index];
                if (!selected.Contains(record.Key))
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
                    record.Key);
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

        public static bool EditWarningRange(
            SerializedObject serializedObject,
            int sequenceIndex,
            FpgSkillEventKey eventKey,
            FpgSkillTimelineEventRangeEditMode editMode,
            int requestedStartTick,
            int requestedEndTick,
            out int appliedStartTick,
            out int appliedEndTick)
        {
            appliedStartTick = 0;
            appliedEndTick = 0;
            if (serializedObject == null
                || serializedObject.targetObject == null
                || eventKey.Track != FpgSkillEventTrackKind.Warning)
            {
                return false;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty sequence = GetSequence(
                serializedObject,
                sequenceIndex);
            SerializedProperty warning = GetEventProperty(
                serializedObject,
                sequenceIndex,
                eventKey);
            SerializedProperty startProperty =
                warning?.FindPropertyRelative("startTick");
            SerializedProperty endProperty =
                warning?.FindPropertyRelative("endTick");
            int sequenceDuration = GetDurationTicks(sequence);
            if (sequence == null
                || sequenceDuration <= 0
                || startProperty == null
                || endProperty == null
                || startProperty.propertyType
                    != SerializedPropertyType.Integer
                || endProperty.propertyType
                    != SerializedPropertyType.Integer)
            {
                return false;
            }

            if (editMode
                == FpgSkillTimelineEventRangeEditMode.ResizeStart)
            {
                appliedEndTick = Mathf.Clamp(
                    requestedEndTick,
                    1,
                    sequenceDuration);
                appliedStartTick = Mathf.Clamp(
                    requestedStartTick,
                    0,
                    appliedEndTick - 1);
            }
            else
            {
                appliedStartTick = Mathf.Clamp(
                    requestedStartTick,
                    0,
                    sequenceDuration - 1);
                appliedEndTick = Mathf.Clamp(
                    requestedEndTick,
                    appliedStartTick + 1,
                    sequenceDuration);
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                "Resize skill warning");
            startProperty.intValue = appliedStartTick;
            endProperty.intValue = appliedEndTick;
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
            if (kind != FpgSkillTimelineBlockKind.Animation
                || blockIndex != 0
                || sequence == null)
            {
                return false;
            }

            int startTick = GetAnimationStartTick(sequence);
            int endTick = GetAnimationEndTick(sequence);
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
            FpgSkillEventKey eventKey,
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
            List<FpgSkillEventRecord> authored = ReadEvents(
                sequence,
                durationTicks);
            FpgSkillEventRecord moving = authored.Find(item =>
                item.Key == eventKey);
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
                    : left.Key.CompareTo(right.Key);
            });
            int currentIndex = sameTick.FindIndex(item =>
                item.Key == eventKey);
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
                    current.Key);
                SerializedProperty adjacentProperty = GetEventProperty(
                    serializedObject,
                    sequenceIndex,
                    adjacent.Key);
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
            FpgSkillEventKey eventKey)
        {
            SerializedProperty eventArray = GetEventArray(
                GetSequence(serializedObject, sequenceIndex),
                eventKey);
            return DeleteArrayElement(
                serializedObject,
                eventArray,
                eventKey.LocalIndex,
                "删除技能事件");
        }

        public static bool DeleteEvents(
            SerializedObject serializedObject,
            int sequenceIndex,
            IEnumerable<FpgSkillEventKey> eventKeys)
        {
            if (serializedObject == null
                || serializedObject.targetObject == null
                || eventKeys == null)
            {
                return false;
            }

            List<FpgSkillEventKey> keys = new List<FpgSkillEventKey>(
                new HashSet<FpgSkillEventKey>(eventKeys));
            keys.Sort((left, right) =>
            {
                int trackComparison = left.Track.CompareTo(right.Track);
                if (trackComparison != 0)
                {
                    return trackComparison;
                }

                int actionComparison = left.ActionKind.CompareTo(
                    right.ActionKind);
                if (actionComparison != 0)
                {
                    return actionComparison;
                }

                int presentationTrackComparison =
                    left.PresentationTrackIndex.CompareTo(
                        right.PresentationTrackIndex);
                return presentationTrackComparison != 0
                    ? presentationTrackComparison
                    : right.LocalIndex.CompareTo(left.LocalIndex);
            });
            if (keys.Count == 0)
            {
                return false;
            }

            SerializedProperty sequence = GetSequence(serializedObject, sequenceIndex);
            for (int index = 0; index < keys.Count; index++)
            {
                FpgSkillEventKey key = keys[index];
                SerializedProperty eventArray = GetEventArray(sequence, key);
                if (eventArray == null
                    || !eventArray.isArray
                    || !key.IsValid
                    || key.LocalIndex >= eventArray.arraySize)
                {
                    return false;
                }
            }

            Undo.RecordObject(serializedObject.targetObject, "删除技能事件");
            for (int index = 0; index < keys.Count; index++)
            {
                FpgSkillEventKey key = keys[index];
                DeleteArrayElementWithoutApply(
                    GetEventArray(sequence, key),
                    key.LocalIndex);
            }

            Apply(serializedObject);
            return true;
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
            if (trackComparison != 0)
            {
                return trackComparison;
            }

            int presentationTrackComparison =
                left.PresentationTrackIndex.CompareTo(
                    right.PresentationTrackIndex);
            return presentationTrackComparison != 0
                ? presentationTrackComparison
                : left.ArrayIndex.CompareTo(right.ArrayIndex);
        }

        private static SerializedProperty GetEventArray(
            SerializedProperty sequence,
            FpgSkillEventTrackKind track)
        {
            return track == FpgSkillEventTrackKind.Warning
                ? FindFirstRelative(sequence, WarningEventArrayNames)
                : null;
        }

        private static SerializedProperty GetEventArray(
            SerializedProperty sequence,
            FpgSkillEventKey eventKey)
        {
            if (!eventKey.IsValid)
            {
                return null;
            }

            if (IsActivePresentationEventTrack(eventKey.Track))
            {
                return GetActivePresentationEventArray(
                    sequence,
                    eventKey.PresentationTrackIndex,
                    eventKey.Track);
            }

            if (eventKey.ActionKind == FpgSkillActionKind.None)
            {
                return GetEventArray(sequence, eventKey.Track);
            }

            switch (eventKey.ActionKind)
            {
                case FpgSkillActionKind.Attack:
                    return FindFirstRelative(sequence, AttackEventArrayNames);
                case FpgSkillActionKind.LaunchProjectile:
                    return FindFirstRelative(
                        sequence,
                        ProjectileEventArrayNames);
                case FpgSkillActionKind.CommitReload:
                    return FindFirstRelative(sequence, ReloadEventArrayNames);
                case FpgSkillActionKind.SummonActors:
                    return FindFirstRelative(sequence, SummonEventArrayNames);
                case FpgSkillActionKind.SelfDestructOwner:
                    return FindFirstRelative(
                        sequence,
                        SelfDestructEventArrayNames);
                default:
                    return null;
            }
        }

        public static bool IsActivePresentationEventTrack(
            FpgSkillEventTrackKind track)
        {
            return track == FpgSkillEventTrackKind.PresentationVfx
                || track == FpgSkillEventTrackKind.PresentationAudio
                || track == FpgSkillEventTrackKind.PresentationCameraShake;
        }

        private static SerializedProperty GetActivePresentationEventArray(
            SerializedProperty sequence,
            int presentationTrackIndex,
            FpgSkillEventTrackKind eventTrack)
        {
            SerializedProperty presentationTrack = GetActivePresentationTrack(
                sequence,
                presentationTrackIndex);
            if (presentationTrack == null)
            {
                return null;
            }

            switch (eventTrack)
            {
                case FpgSkillEventTrackKind.PresentationVfx:
                    return FindFirstRelative(
                        presentationTrack,
                        VfxPresentationEventArrayNames);
                case FpgSkillEventTrackKind.PresentationAudio:
                    return FindFirstRelative(
                        presentationTrack,
                        AudioPresentationEventArrayNames);
                case FpgSkillEventTrackKind.PresentationCameraShake:
                    return FindFirstRelative(
                        presentationTrack,
                        CameraShakePresentationEventArrayNames);
                default:
                    return null;
            }
        }

        private static SerializedProperty GetActivePresentationTrack(
            SerializedProperty sequence,
            int presentationTrackIndex)
        {
            SerializedProperty tracks = GetActivePresentationTracks(sequence);
            return tracks != null
                && presentationTrackIndex >= 0
                && presentationTrackIndex < tracks.arraySize
                    ? tracks.GetArrayElementAtIndex(presentationTrackIndex)
                    : null;
        }

        private static int FindActivePresentationTrackIndex(
            SerializedProperty sequence,
            string trackId)
        {
            if (string.IsNullOrWhiteSpace(trackId))
            {
                return -1;
            }

            SerializedProperty tracks = GetActivePresentationTracks(sequence);
            if (tracks == null)
            {
                return -1;
            }

            for (int index = 0; index < tracks.arraySize; index++)
            {
                SerializedProperty track = tracks.GetArrayElementAtIndex(index);
                if (string.Equals(
                        ReadFirstString(track, "trackId"),
                        trackId,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static int CountPresentationTrackEvents(
            SerializedProperty presentationTrack)
        {
            if (presentationTrack == null)
            {
                return 0;
            }

            int count = 0;
            SerializedProperty vfx = FindFirstRelative(
                presentationTrack,
                VfxPresentationEventArrayNames);
            SerializedProperty audio = FindFirstRelative(
                presentationTrack,
                AudioPresentationEventArrayNames);
            SerializedProperty cameraShake = FindFirstRelative(
                presentationTrack,
                CameraShakePresentationEventArrayNames);
            count += vfx != null && vfx.isArray ? vfx.arraySize : 0;
            count += audio != null && audio.isArray ? audio.arraySize : 0;
            count += cameraShake != null && cameraShake.isArray
                ? cameraShake.arraySize
                : 0;
            return count;
        }

        private static bool HasAnyEventArray(SerializedProperty sequence)
        {
            return FindFirstRelative(sequence, AttackEventArrayNames) != null
                || FindFirstRelative(sequence, ProjectileEventArrayNames) != null
                || FindFirstRelative(sequence, ReloadEventArrayNames) != null
                || FindFirstRelative(sequence, SummonEventArrayNames) != null
                || FindFirstRelative(sequence, SelfDestructEventArrayNames)
                    != null
                || GetActivePresentationTracks(sequence) != null
                || GetEventArray(sequence, FpgSkillEventTrackKind.Warning) != null;
        }

        private static FpgSkillEventKey MakeEventKey(
            FpgSkillEventTrackKind track,
            int arrayIndex)
        {
            return MakeEventKey(
                track,
                FpgSkillActionKind.None,
                arrayIndex);
        }

        private static FpgSkillEventKey MakeEventKey(
            FpgSkillEventTrackKind track,
            FpgSkillActionKind actionKind,
            int arrayIndex)
        {
            return MakeEventKey(track, actionKind, -1, arrayIndex);
        }

        private static FpgSkillEventKey MakeEventKey(
            FpgSkillEventTrackKind track,
            FpgSkillActionKind actionKind,
            int presentationTrackIndex,
            int arrayIndex)
        {
            return new FpgSkillEventKey(
                track,
                actionKind,
                presentationTrackIndex,
                arrayIndex);
        }

        private static string GetTrackLabel(
            FpgSkillEventTrackKind track,
            SerializedProperty eventProperty)
        {
            switch (track)
            {
                case FpgSkillEventTrackKind.GameplayAction:
                    return "玩法动作";
                case FpgSkillEventTrackKind.PresentationVfx:
                    return "特效";
                case FpgSkillEventTrackKind.PresentationAudio:
                    return "音效";
                case FpgSkillEventTrackKind.PresentationCameraShake:
                    return "震屏";
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
                case FpgSkillEventTrackKind.GameplayAction:
                    return GetPaletteColor(arrayIndex);
                case FpgSkillEventTrackKind.PresentationVfx:
                    return Palette[2];
                case FpgSkillEventTrackKind.PresentationAudio:
                    return Palette[4];
                case FpgSkillEventTrackKind.PresentationCameraShake:
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
            Dictionary<int, FpgSkillEventRecord> ordinals =
                new Dictionary<int, FpgSkillEventRecord>();
            for (int index = 0; index < events.Count; index++)
            {
                FpgSkillEventRecord eventRecord = events[index];
                AppendAuthoredOrdinal(
                    result,
                    ordinals,
                    eventRecord,
                    eventRecord.AuthoredOrdinal);
                if (eventRecord.Track == FpgSkillEventTrackKind.Warning
                    && eventRecord.AuthoredOrdinal < int.MaxValue)
                {
                    AppendAuthoredOrdinal(
                        result,
                        ordinals,
                        eventRecord,
                        eventRecord.AuthoredOrdinal + 1);
                }
            }
        }

        private static void AppendAuthoredOrdinal(
            ICollection<FpgSkillValidationItem> result,
            IDictionary<int, FpgSkillEventRecord> ordinals,
            FpgSkillEventRecord eventRecord,
            int authoredOrdinal)
        {
            if (authoredOrdinal < 0)
            {
                return;
            }

            if (ordinals.ContainsKey(authoredOrdinal))
            {
                result.Add(new FpgSkillValidationItem
                {
                    Severity = FpgSkillIssueSeverity.Error,
                    Message = "序列存在重复 authoredOrdinal "
                        + authoredOrdinal + "。",
                    EventKey = eventRecord.Key,
                    Tick = eventRecord.Tick
                });
                return;
            }

            ordinals[authoredOrdinal] = eventRecord;
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
                    EventKey = eventRecord.Key,
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
                item.EventKey = bestMatch.Key;
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

                if (compiledKind == "GameplayAction"
                    && authored.Track != FpgSkillEventTrackKind.GameplayAction)
                {
                    continue;
                }

                if (compiledKind == "ActivePresentation"
                    && !IsActivePresentationEventTrack(authored.Track))
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
                case "GameplayAction":
                    return "玩法动作";
                case "ActivePresentation":
                    return "主动表现";
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
            FpgSkillEventKey ignoredEventKey)
        {
            int maximum = -1;
            List<FpgSkillEventKey> arrayKeys =
                new List<FpgSkillEventKey>
            {
                MakeEventKey(
                    FpgSkillEventTrackKind.GameplayAction,
                    FpgSkillActionKind.Attack,
                    0),
                MakeEventKey(
                    FpgSkillEventTrackKind.GameplayAction,
                    FpgSkillActionKind.LaunchProjectile,
                    0),
                MakeEventKey(
                    FpgSkillEventTrackKind.GameplayAction,
                    FpgSkillActionKind.CommitReload,
                    0),
                MakeEventKey(
                    FpgSkillEventTrackKind.GameplayAction,
                    FpgSkillActionKind.SummonActors,
                    0),
                MakeEventKey(
                    FpgSkillEventTrackKind.GameplayAction,
                    FpgSkillActionKind.SelfDestructOwner,
                    0),
                MakeEventKey(FpgSkillEventTrackKind.Warning, 0)
            };

            SerializedProperty activePresentationTracks =
                GetActivePresentationTracks(sequence);
            if (activePresentationTracks != null)
            {
                for (int trackIndex = 0;
                    trackIndex < activePresentationTracks.arraySize;
                    trackIndex++)
                {
                    arrayKeys.Add(MakeEventKey(
                        FpgSkillEventTrackKind.PresentationVfx,
                        FpgSkillActionKind.None,
                        trackIndex,
                        0));
                    arrayKeys.Add(MakeEventKey(
                        FpgSkillEventTrackKind.PresentationAudio,
                        FpgSkillActionKind.None,
                        trackIndex,
                        0));
                    arrayKeys.Add(MakeEventKey(
                        FpgSkillEventTrackKind.PresentationCameraShake,
                        FpgSkillActionKind.None,
                        trackIndex,
                        0));
                }
            }

            for (int keyIndex = 0; keyIndex < arrayKeys.Count; keyIndex++)
            {
                FpgSkillEventKey arrayKey = arrayKeys[keyIndex];
                SerializedProperty eventArray = GetEventArray(
                    sequence,
                    arrayKey);
                if (eventArray == null || !eventArray.isArray)
                {
                    continue;
                }

                for (int index = 0; index < eventArray.arraySize; index++)
                {
                    FpgSkillEventKey eventKey = MakeEventKey(
                        arrayKey.Track,
                        arrayKey.ActionKind,
                        arrayKey.PresentationTrackIndex,
                        index);
                    if (eventKey == ignoredEventKey)
                    {
                        continue;
                    }

                    SerializedProperty item =
                        eventArray.GetArrayElementAtIndex(index);
                    int ordinal = ReadRawInt(
                        FindFirstRelative(item, AuthoredOrdinalNames),
                        index);
                    maximum = Mathf.Max(maximum, ordinal);
                    if (arrayKey.Track == FpgSkillEventTrackKind.Warning
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

        private static void ConfigureDefaultAction(
            SerializedProperty action,
            FpgSkillActionKind actionKind,
            int modeValue,
            bool enemy)
        {
            SetEnumRawValue(
                action,
                enemy
                    ? (int)FpgSkillTargetSource.CurrentTarget
                    : (int)FpgSkillTargetSource.CurrentAim,
                "targetSource");

            switch (actionKind)
            {
                case FpgSkillActionKind.Attack:
                    WriteInt(action, enemy ? 0 : 1, "ammoCost");
                    ConfigureDefaultDamage(action, enemy ? 10 : 4);
                    ConfigureActionModeDefaults(
                        action,
                        actionKind,
                        modeValue,
                        enemy);
                    break;

                case FpgSkillActionKind.LaunchProjectile:
                    WriteInt(action, enemy ? 0 : 1, "ammoCost");
                    ConfigureDefaultDamage(action, 10);
                    WriteInt(action, 1, "threatDefinitionId");
                    WriteInt(action, 1, "projectileDefinitionId");
                    WriteInt(action, 1, "projectileCount");
                    WriteInt(action, 30, "projectileFlightTicks");
                    WriteInt(action, 45, "projectileLifetimeTicks");
                    WriteInt(action, 0, "projectileMaxHitPoints");
                    WriteInt(action, 1, "projectileBudgetUnits");
                    WriteInt(action, 1, "projectileSweepRadiusKey");
                    ConfigureActionModeDefaults(
                        action,
                        actionKind,
                        modeValue,
                        enemy);
                    break;

                case FpgSkillActionKind.CommitReload:
                    SetEnumRawValue(
                        action,
                        (int)FpgSkillTargetSource.Self,
                        "targetSource");
                    break;

                case FpgSkillActionKind.SummonActors:
                    WriteInt(action, 2, "maxSummonsPerOwner");
                    WriteInt(action, 8, "maxTotalSummonsPerEncounter");
                    WriteInt(action, 2, "maxSummonRecursionDepth");
                    break;

                case FpgSkillActionKind.SelfDestructOwner:
                    SetEnumRawValue(
                        action,
                        (int)FpgSkillTargetSource.Self,
                        "targetSource");
                    WriteString(
                        action,
                        string.Empty,
                        "boundGameplayEventId");
                    break;
            }
        }

        private static void ConfigureDefaultActivePresentationEvent(
            SerializedProperty eventProperty,
            FpgSkillEventTrackKind eventTrack)
        {
            SerializedProperty presentation =
                eventProperty?.FindPropertyRelative("presentation");
            if (presentation == null)
            {
                return;
            }

            switch (eventTrack)
            {
                case FpgSkillEventTrackKind.PresentationVfx:
                    SerializedProperty vfxDuration =
                        presentation.FindPropertyRelative("durationSeconds");
                    SerializedProperty scale =
                        presentation.FindPropertyRelative("scale");
                    if (vfxDuration != null
                        && vfxDuration.propertyType
                            == SerializedPropertyType.Float)
                    {
                        vfxDuration.floatValue = 1f;
                    }

                    if (scale != null
                        && scale.propertyType == SerializedPropertyType.Vector3)
                    {
                        scale.vector3Value = Vector3.one;
                    }

                    SetEnumRawValue(eventProperty, 0, "anchor");
                    WriteString(eventProperty, string.Empty, "socketId");
                    break;

                case FpgSkillEventTrackKind.PresentationAudio:
                    SerializedProperty volume =
                        presentation.FindPropertyRelative("volume");
                    if (volume != null
                        && volume.propertyType == SerializedPropertyType.Float)
                    {
                        volume.floatValue = 1f;
                    }

                    break;

                case FpgSkillEventTrackKind.PresentationCameraShake:
                    SerializedProperty shakeDuration =
                        presentation.FindPropertyRelative("durationSeconds");
                    if (shakeDuration != null
                        && shakeDuration.propertyType
                            == SerializedPropertyType.Float)
                    {
                        shakeDuration.floatValue = 0.1f;
                    }

                    break;
            }
        }

        private static string GetActionKindLabel(
            FpgSkillActionKind actionKind)
        {
            switch (actionKind)
            {
                case FpgSkillActionKind.Attack:
                    return "攻击";
                case FpgSkillActionKind.LaunchProjectile:
                    return "发射投射物";
                case FpgSkillActionKind.CommitReload:
                    return "完成换弹";
                case FpgSkillActionKind.SummonActors:
                    return "召唤单位";
                case FpgSkillActionKind.SelfDestructOwner:
                    return "召唤者自毁";
                default:
                    return "未知";
            }
        }

        private static void ConfigureActionModeDefaults(
            SerializedProperty action,
            FpgSkillActionKind actionKind,
            int modeValue,
            bool enemy)
        {
            if (actionKind == FpgSkillActionKind.Attack)
            {
                SetEnumRawValue(action, modeValue, "mode");
                WriteInt(action, 8, "pelletCount");
                WriteInt(action, 0, "additionalPenetrationCount");
                WriteInt(action, 4, "areaCombatantLimit");
                WriteInt(action, 4, "areaProjectileLimit");
                SetEnumRawValue(action, 3, "allowedTargetKinds");
                WriteInt(action, 1, "threatDefinitionId");
                SetEnumRawValue(action, 0, "boundTargetPolicy");
                WriteInt(action, 0, "delayTicks");
            }
            else if (actionKind == FpgSkillActionKind.LaunchProjectile)
            {
                SetEnumRawValue(action, modeValue, "impactMode");
                WriteInt(action, 4, "areaCombatantLimit");
                WriteInt(action, 4, "areaProjectileLimit");
                SetEnumRawValue(action, 3, "allowedTargetKinds");
            }

        }

        private static void CopyCompatibleInteger(
            SerializedProperty source,
            SerializedProperty target,
            string propertyName)
        {
            SerializedProperty sourceValue =
                source?.FindPropertyRelative(propertyName);
            SerializedProperty targetValue =
                target?.FindPropertyRelative(propertyName);
            if (sourceValue != null
                && targetValue != null
                && sourceValue.propertyType
                    == SerializedPropertyType.Integer
                && targetValue.propertyType
                    == SerializedPropertyType.Integer)
            {
                targetValue.intValue = sourceValue.intValue;
            }
        }

        private static void CopyCompatibleString(
            SerializedProperty source,
            SerializedProperty target,
            string propertyName)
        {
            SerializedProperty sourceValue =
                source?.FindPropertyRelative(propertyName);
            SerializedProperty targetValue =
                target?.FindPropertyRelative(propertyName);
            if (sourceValue != null
                && targetValue != null
                && sourceValue.propertyType
                    == SerializedPropertyType.String
                && targetValue.propertyType
                    == SerializedPropertyType.String)
            {
                targetValue.stringValue = sourceValue.stringValue;
            }
        }

        private static void CopyCompatibleEnum(
            SerializedProperty source,
            SerializedProperty target,
            string propertyName)
        {
            SerializedProperty sourceValue =
                source?.FindPropertyRelative(propertyName);
            SerializedProperty targetValue =
                target?.FindPropertyRelative(propertyName);
            if (sourceValue != null
                && targetValue != null
                && sourceValue.propertyType
                    == SerializedPropertyType.Enum
                && targetValue.propertyType
                    == SerializedPropertyType.Enum)
            {
                targetValue.intValue = sourceValue.intValue;
            }
        }

        private static void CopyCompatibleVector3(
            SerializedProperty source,
            SerializedProperty target,
            string propertyName)
        {
            SerializedProperty sourceValue =
                source?.FindPropertyRelative(propertyName);
            SerializedProperty targetValue =
                target?.FindPropertyRelative(propertyName);
            if (sourceValue != null
                && targetValue != null
                && sourceValue.propertyType
                    == SerializedPropertyType.Vector3
                && targetValue.propertyType
                    == SerializedPropertyType.Vector3)
            {
                targetValue.vector3Value = sourceValue.vector3Value;
            }
        }

        private static void NormalizeConvertedActionSpatialMetadata(
            SerializedProperty action,
            FpgSkillActionKind targetKind,
            bool enemy)
        {
            NormalizeActionSpatialMetadata(action, targetKind, enemy);
        }

        private static void NormalizeActionSpatialMetadata(
            SerializedProperty action,
            FpgSkillActionKind actionKind,
            bool enemy)
        {
            FpgSkillActionAuthoringOptions options =
                FpgSkillActionAuthoringRules.Get(actionKind, enemy);
            if (action == null || !options.IsKnownAction)
            {
                return;
            }

            if (options.HasFixedTargetSource)
            {
                SetEnumRawValue(
                    action,
                    (int)options.DefaultTargetSource,
                    "targetSource");
            }
            else if (options.SupportsTargetSourceSelection)
            {
                SerializedProperty targetSource =
                    action.FindPropertyRelative("targetSource");
                FpgSkillTargetSource source = targetSource == null
                    ? FpgSkillTargetSource.None
                    : (FpgSkillTargetSource)targetSource.intValue;
                bool hasAllowedSource = false;
                for (int index = 0;
                    index < options.TargetSourceChoices.Count;
                    index++)
                {
                    if (options.TargetSourceChoices[index] == source)
                    {
                        hasAllowedSource = true;
                        break;
                    }
                }

                if (!hasAllowedSource
                    || (source == FpgSkillTargetSource.SocketForward
                        && string.IsNullOrWhiteSpace(
                            ReadFirstString(action, "socketId"))))
                {
                    SetEnumRawValue(
                        action,
                        (int)options.DefaultTargetSource,
                        "targetSource");
                }
            }

            if (!options.SupportsSocket && !options.SupportsTargetOffset)
            {
                ClearEventSpatialMetadata(action);
                return;
            }

            if (!options.SupportsSocket)
            {
                WriteString(action, string.Empty, "socketId", "socket");
            }

            if (!options.SupportsTargetOffset)
            {
                SerializedProperty targetOffset = FindFirstRelative(
                    action,
                    "targetOffset");
                if (targetOffset != null
                    && targetOffset.propertyType
                        == SerializedPropertyType.Vector3)
                {
                    targetOffset.vector3Value = Vector3.zero;
                }
            }
        }

        private static void ConfigureDefaultDamage(
            SerializedProperty action,
            int baseDamage)
        {
            WriteInt(action, baseDamage, "baseDamage");
            WriteInt(action, baseDamage, "breakDamage");
            WriteInt(
                action,
                10000,
                "weakpointDamageMultiplierBasisPoints");
            WriteInt(
                action,
                10000,
                "weakpointBreakMultiplierBasisPoints");
        }

        private static bool IsEnemyAsset(SerializedObject serializedObject)
        {
            return serializedObject?.targetObject != null
                && serializedObject.targetObject.GetType().Name.IndexOf(
                    "Enemy",
                    StringComparison.OrdinalIgnoreCase) >= 0;
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

            if (kind != FpgSkillTimelineBlockKind.Animation
                || blockIndex != 0)
            {
                return false;
            }

            SerializedProperty startProperty = sequence.FindPropertyRelative(
                "animationStartTick");
            SerializedProperty endProperty = sequence.FindPropertyRelative(
                "animationEndTick");
            int startTick = GetAnimationStartTick(sequence);
            int endTick = GetAnimationEndTick(sequence);
            int rawEndTick = ReadRawInt(endProperty, -1);
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
            appliedStartTick = nextStartTick;
            appliedEndTick = nextEndTick;
            bool materializeResolvedAnimationEnd =
                rawEndTick != endTick;
            if (nextStartTick == startTick
                && nextEndTick == endTick
                && nextDurationTicks == durationTicks
                && !materializeResolvedAnimationEnd)
            {
                return true;
            }

            Undo.RecordObject(
                serializedObject.targetObject,
                "编辑技能动画区间");
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
