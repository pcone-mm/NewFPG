using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace NewFPG.CZN.Editor
{
    public static class FeiSkillValidationUtility
    {
        private const string CharacterRoot = "Assets/Imported/CZN/Fei_30048";
        private const string SpineRoot = CharacterRoot + "/SpineSource";
        private const string SkillRoot = CharacterRoot + "/Preview/SkillCompositions/Skills";
        private const string TimelineRoot = CharacterRoot + "/Preview/SkillCompositions/Timelines";
        private const string ScenePath = CharacterRoot + "/Preview/Fei_30048_SkillPreview.unity";
        private const string MainSkeletonPath = SpineRoot + "/model/30048_SkeletonData.asset";
        private const string JsonReportPath = CharacterRoot + "/Metadata/skill-validation-report.json";
        private const string MarkdownReportPath = CharacterRoot + "/Metadata/skill-validation-report.md";

        [MenuItem("Tools/CZN/Fei 30048/Validate Skill Import")]
        public static void ValidateSkillImport()
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            JArray skillReports = new JArray();

            string[] skeletonGuids = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { SpineRoot });
            string[] atlasGuids = AssetDatabase.FindAssets("t:SpineAtlasAsset", new[] { SpineRoot });
            int totalAnimations = 0;
            foreach (string guid in skeletonGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SkeletonDataAsset asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);
                SkeletonData data = asset != null ? asset.GetSkeletonData(true) : null;
                if (data == null)
                {
                    errors.Add("Unreadable SkeletonDataAsset: " + path);
                }
                else
                {
                    totalAnimations += data.Animations.Count;
                }
            }

            CznSpineSkillSequence[] sequences = AssetDatabase
                .FindAssets("t:CznSpineSkillSequence", new[] { SkillRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<CznSpineSkillSequence>)
                .Where(sequence => sequence != null)
                .ToArray();
            TimelineAsset[] timelines = AssetDatabase
                .FindAssets("t:TimelineAsset", new[] { TimelineRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TimelineAsset>)
                .Where(timeline => timeline != null)
                .ToArray();

            if (sequences.Length != 12)
            {
                errors.Add($"Expected 12 SkillSequence assets, found {sequences.Length}.");
            }
            if (timelines.Length != sequences.Length)
            {
                errors.Add($"Timeline count {timelines.Length} does not match skill count {sequences.Length}.");
            }

            SkeletonDataAsset mainAsset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(MainSkeletonPath);
            SkeletonData mainData = mainAsset != null ? mainAsset.GetSkeletonData(true) : null;
            if (mainData == null)
            {
                errors.Add("Main SkeletonDataAsset is unreadable: " + MainSkeletonPath);
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject sceneRoot = GameObject.Find("Fei 30048 Skill Composition Preview");
            CznSpineSkillPlayer player = sceneRoot != null ? sceneRoot.GetComponent<CznSpineSkillPlayer>() : null;
            CznSpineSkillPreviewMenu menu = sceneRoot != null ? sceneRoot.GetComponent<CznSpineSkillPreviewMenu>() : null;
            PlayableDirector director = sceneRoot != null ? sceneRoot.GetComponent<PlayableDirector>() : null;
            if (player == null)
            {
                errors.Add("Preview scene has no CznSpineSkillPlayer.");
            }
            else
            {
                SerializedObject serializedPlayer = new SerializedObject(player);
                SerializedProperty additiveMaterial = serializedPlayer.FindProperty("fallbackAdditiveParticleMaterial");
                if (additiveMaterial == null || additiveMaterial.objectReferenceValue == null)
                {
                    errors.Add("Preview player has no additive particle material binding.");
                }
                if (!string.Equals(player.IdleAnimationName, "b_idle", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Preview player completion idle must be b_idle.");
                }
            }

            if (menu == null)
            {
                errors.Add("Preview scene has no CznSpineSkillPreviewMenu.");
            }
            else
            {
                if (menu.LoopsPlayback)
                {
                    errors.Add("Preview menu must use single-play mode.");
                }
                if (!menu.ReturnsToIdleOnComplete)
                {
                    errors.Add("Preview menu must return to idle on completion.");
                }
            }

            if (director == null)
            {
                errors.Add("Preview scene has no PlayableDirector.");
            }
            else if (director.extrapolationMode != DirectorWrapMode.None)
            {
                errors.Add("Preview director wrap mode must be None.");
            }

            foreach (CznSpineSkillSequence sequence in sequences)
            {
                ValidateSequenceReferences(sequence, mainData, errors);
                int runtimeCount = 0;
                JArray samples = new JArray();
                if (player != null)
                {
                    player.RestartSequence(sequence);
                    runtimeCount = CountRuntimeObjects(sceneRoot);
                    int expectedRuntimeCount = sequence.SpineLayers.Count + sequence.ParticleLayers.Count;
                    if (runtimeCount != expectedRuntimeCount)
                    {
                        errors.Add(
                            $"{sequence.SkillId}: runtime object count {runtimeCount} != expected {expectedRuntimeCount}.");
                    }

                    foreach (float time in BuildSampleTimes(sequence))
                    {
                        player.Evaluate(sequence, time);
                        samples.Add(new JObject
                        {
                            ["time"] = time,
                            ["active_spine"] = player.ActiveSpineLayerCount,
                            ["active_particles"] = player.ActiveParticleLayerCount,
                            ["attachments"] = CountActiveAttachments(sceneRoot),
                        });
                    }
                }

                skillReports.Add(new JObject
                {
                    ["id"] = sequence.SkillId,
                    ["duration"] = sequence.Duration,
                    ["runtime_objects"] = runtimeCount,
                    ["samples"] = samples,
                    ["unresolved"] = new JArray(sequence.UnresolvedResources),
                });
            }

            ValidateTimelines(sequences, timelines, errors);
            ValidateReplay(player, sceneRoot, sequences, "u4_attack", errors);
            ValidateReplay(player, sceneRoot, sequences, "ug_attack", errors);
            ValidateActorPoseHolds(player, sequences, errors);
            ValidateStandbyWindow(player, sequences, errors);
            ValidateCompletionIdle(player, sceneRoot, errors);

            JObject report = new JObject
            {
                ["character"] = "绯",
                ["character_id"] = "30048",
                ["playback_mode"] = "single",
                ["completion_idle"] = "b_idle",
                ["skeleton_data_assets"] = skeletonGuids.Length,
                ["atlas_assets"] = atlasGuids.Length,
                ["animation_total"] = totalAnimations,
                ["skill_sequences"] = sequences.Length,
                ["timelines"] = timelines.Length,
                ["errors"] = new JArray(errors),
                ["warnings"] = new JArray(warnings),
                ["skills"] = skillReports,
            };

            WriteReports(report, sequences, errors, warnings);
            AssetDatabase.ImportAsset(JsonReportPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(MarkdownReportPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Fei skill validation failed:\n" + string.Join("\n", errors));
            }

            Debug.Log(
                $"[CZN] Fei validation passed: {skeletonGuids.Length} skeletons, " +
                $"{sequences.Length} skills, {timelines.Length} timelines, {totalAnimations} animations.");
        }

        private static void ValidateSequenceReferences(
            CznSpineSkillSequence sequence,
            SkeletonData mainData,
            List<string> errors)
        {
            float lastCueEnd = 0f;
            foreach (CznActorAnimationCue cue in sequence.ActorAnimations)
            {
                if (mainData != null && mainData.FindAnimation(cue.animationName) == null)
                {
                    errors.Add($"{sequence.SkillId}: missing actor animation {cue.animationName}.");
                }
                lastCueEnd = Mathf.Max(lastCueEnd, cue.startTime + cue.duration);
            }

            foreach (CznSpineLayerCue cue in sequence.SpineLayers)
            {
                SkeletonData data = cue.skeletonDataAsset != null
                    ? cue.skeletonDataAsset.GetSkeletonData(true)
                    : null;
                if (data == null)
                {
                    errors.Add($"{sequence.SkillId}: missing Spine layer {cue.sourceName}.");
                }
                else if (data.FindAnimation(cue.animationName) == null)
                {
                    errors.Add(
                        $"{sequence.SkillId}: {cue.sourceName} has no animation {cue.animationName}.");
                }
                lastCueEnd = Mathf.Max(lastCueEnd, cue.startTime + cue.duration);
            }

            foreach (CznParticleLayerCue cue in sequence.ParticleLayers)
            {
                if (!string.IsNullOrWhiteSpace(cue.originalTexturePath) && cue.texture == null)
                {
                    errors.Add(
                        $"{sequence.SkillId}: particle texture did not bind: {cue.originalTexturePath}.");
                }
                lastCueEnd = Mathf.Max(lastCueEnd, cue.startTime + cue.duration);
            }
            foreach (CznTransformCue cue in sequence.TransformCues)
            {
                lastCueEnd = Mathf.Max(lastCueEnd, cue.startTime + cue.duration);
            }
            foreach (CznCameraZoomCue cue in sequence.CameraZoomCues)
            {
                lastCueEnd = Mathf.Max(lastCueEnd, cue.startTime + cue.duration);
            }

            if (sequence.Duration + 0.001f < lastCueEnd)
            {
                errors.Add(
                    $"{sequence.SkillId}: duration {sequence.Duration:0.000} ends before cue {lastCueEnd:0.000}.");
            }
        }

        private static IEnumerable<float> BuildSampleTimes(CznSpineSkillSequence sequence)
        {
            SortedSet<float> times = new SortedSet<float> { 0f, sequence.Duration };
            AddCueTimes(times, sequence.ActorAnimations.Select(cue => (cue.startTime, cue.duration)), sequence.Duration);
            AddCueTimes(times, sequence.SpineLayers.Select(cue => (cue.startTime, cue.duration)), sequence.Duration);
            AddCueTimes(times, sequence.ParticleLayers.Select(cue => (cue.startTime, cue.duration)), sequence.Duration);
            AddCueTimes(times, sequence.TransformCues.Select(cue => (cue.startTime, cue.duration)), sequence.Duration);
            AddCueTimes(times, sequence.CameraZoomCues.Select(cue => (cue.startTime, cue.duration)), sequence.Duration);
            AddCueTimes(times, sequence.Markers.Select(cue => (cue.startTime, cue.duration)), sequence.Duration);
            if (sequence.Duration > 0.02f)
            {
                times.Add(sequence.Duration * 0.5f);
            }
            return times;
        }

        private static void AddCueTimes(
            SortedSet<float> times,
            IEnumerable<(float start, float duration)> cues,
            float sequenceDuration)
        {
            foreach ((float start, float duration) cue in cues)
            {
                float end = cue.start + Mathf.Max(0f, cue.duration);
                times.Add(Mathf.Clamp(cue.start - 0.001f, 0f, sequenceDuration));
                times.Add(Mathf.Clamp(cue.start, 0f, sequenceDuration));
                times.Add(Mathf.Clamp(cue.start + 0.001f, 0f, sequenceDuration));
                times.Add(Mathf.Clamp(end - 0.001f, 0f, sequenceDuration));
                times.Add(Mathf.Clamp(end, 0f, sequenceDuration));
                times.Add(Mathf.Clamp(end + 0.001f, 0f, sequenceDuration));
            }
        }

        private static void ValidateTimelines(
            IReadOnlyList<CznSpineSkillSequence> sequences,
            IReadOnlyList<TimelineAsset> timelines,
            List<string> errors)
        {
            Dictionary<string, CznSpineSkillSequence> byId = sequences.ToDictionary(
                sequence => sequence.SkillId,
                StringComparer.OrdinalIgnoreCase);
            foreach (TimelineAsset timeline in timelines)
            {
                CznSpineSkillPlayableAsset playable = timeline
                    .GetOutputTracks()
                    .SelectMany(track => track.GetClips())
                    .Select(clip => clip.asset as CznSpineSkillPlayableAsset)
                    .FirstOrDefault(asset => asset != null);
                if (playable?.Sequence == null || !byId.ContainsKey(playable.Sequence.SkillId))
                {
                    errors.Add("Timeline has no valid Fei sequence: " + AssetDatabase.GetAssetPath(timeline));
                }
            }
        }

        private static void ValidateReplay(
            CznSpineSkillPlayer player,
            GameObject sceneRoot,
            IReadOnlyList<CznSpineSkillSequence> sequences,
            string skillId,
            List<string> errors)
        {
            if (player == null || sceneRoot == null)
            {
                return;
            }

            CznSpineSkillSequence sequence = sequences.FirstOrDefault(
                item => string.Equals(item.SkillId, skillId, StringComparison.OrdinalIgnoreCase));
            if (sequence == null)
            {
                errors.Add("Replay sample is missing sequence: " + skillId);
                return;
            }

            CznSpineLayerCue visibleCue = sequence.SpineLayers.FirstOrDefault();
            float visibleTime = string.Equals(skillId, "u4_attack", StringComparison.OrdinalIgnoreCase)
                ? Mathf.Min(1.016f, sequence.Duration)
                : string.Equals(skillId, "ug_attack", StringComparison.OrdinalIgnoreCase)
                    ? Mathf.Min(2.4f, sequence.Duration)
                    : visibleCue != null
                        ? Mathf.Clamp(
                            visibleCue.startTime + Mathf.Min(0.05f, visibleCue.duration * 0.5f),
                            0f,
                            sequence.Duration)
                        : Mathf.Min(0.05f, sequence.Duration);
            int baselineAttachments = -1;
            int baselineObjects = -1;
            for (int round = 0; round < 3; round++)
            {
                player.RestartSequence(sequence);
                player.Evaluate(sequence, visibleTime);
                int attachments = CountActiveAttachments(sceneRoot);
                int runtimeObjects = CountRuntimeObjects(sceneRoot);
                if (round == 0)
                {
                    baselineAttachments = attachments;
                    baselineObjects = runtimeObjects;
                }
                else
                {
                    if (attachments != baselineAttachments)
                    {
                        errors.Add(
                            $"{skillId}: replay attachment count changed {baselineAttachments} -> {attachments}.");
                    }
                    if (runtimeObjects != baselineObjects)
                    {
                        errors.Add(
                            $"{skillId}: replay runtime object count changed {baselineObjects} -> {runtimeObjects}.");
                    }
                }

                player.Evaluate(sequence, sequence.Duration);
                player.Evaluate(sequence, 0f);
                player.Evaluate(sequence, visibleTime);
                if (CountActiveAttachments(sceneRoot) != baselineAttachments)
                {
                    errors.Add($"{skillId}: Timeline rewind did not restore attachments in round {round + 1}.");
                }
            }
        }

        private static void ValidateActorPoseHolds(
            CznSpineSkillPlayer player,
            IReadOnlyList<CznSpineSkillSequence> sequences,
            List<string> errors)
        {
            if (player == null)
            {
                return;
            }

            foreach (CznSpineSkillSequence sequence in sequences)
            {
                CznActorAnimationCue[] actorCues = sequence.ActorAnimations
                    .Where(cue => cue != null)
                    .OrderBy(cue => cue.startTime)
                    .ToArray();
                for (int i = 0; i < actorCues.Length; i++)
                {
                    CznActorAnimationCue cue = actorCues[i];
                    if (cue.loop)
                    {
                        continue;
                    }

                    float cueEnd = cue.startTime + Mathf.Max(0f, cue.duration);
                    float nextActorStart = i + 1 < actorCues.Length
                        ? actorCues[i + 1].startTime
                        : sequence.Duration;
                    float nextIdleStart = sequence.Markers
                        .Where(marker => marker != null &&
                                         marker.startTime > cue.startTime &&
                                         string.Equals(marker.kind, "IDLE", StringComparison.OrdinalIgnoreCase))
                        .Select(marker => marker.startTime)
                        .DefaultIfEmpty(sequence.Duration)
                        .Min();
                    float holdUntil = Mathf.Min(nextActorStart, nextIdleStart);
                    if (holdUntil <= cueEnd + 0.002f)
                    {
                        continue;
                    }

                    float sampleTime = Mathf.Lerp(cueEnd, holdUntil, 0.5f);
                    player.RestartSequence(sequence);
                    player.Evaluate(sequence, sampleTime);
                    if (!string.Equals(
                            player.CurrentActorAnimationName,
                            cue.animationName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(
                            $"{sequence.SkillId}: actor pose was not held between {cueEnd:0.000} and " +
                            $"{holdUntil:0.000}; got {player.CurrentActorAnimationName ?? "<null>"}.");
                    }
                }

                foreach (CznSkillMarkerCue idleMarker in sequence.Markers.Where(
                             marker => marker != null &&
                                       string.Equals(marker.kind, "IDLE", StringComparison.OrdinalIgnoreCase)))
                {
                    float sampleTime = Mathf.Clamp(idleMarker.startTime + 0.001f, 0f, sequence.Duration);
                    bool actorStartsAtIdle = actorCues.Any(
                        cue => Mathf.Abs(cue.startTime - idleMarker.startTime) <= 0.001f);
                    if (actorStartsAtIdle)
                    {
                        continue;
                    }

                    player.RestartSequence(sequence);
                    player.Evaluate(sequence, sampleTime);
                    if (!string.Equals(
                            player.CurrentActorAnimationName,
                            player.IdleAnimationName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(
                            $"{sequence.SkillId}: explicit IDLE at {idleMarker.startTime:0.000} did not restore idle.");
                    }
                }
            }
        }

        private static void ValidateStandbyWindow(
            CznSpineSkillPlayer player,
            IReadOnlyList<CznSpineSkillSequence> sequences,
            List<string> errors)
        {
            if (player == null)
            {
                return;
            }

            CznSpineSkillSequence sequence = sequences.FirstOrDefault(
                item => string.Equals(item.SkillId, "ug_attack", StringComparison.OrdinalIgnoreCase));
            CznSkillMarkerCue standbyOn = sequence?.Markers.FirstOrDefault(
                marker => marker != null &&
                          string.Equals(marker.kind, "STANDBY_ON", StringComparison.OrdinalIgnoreCase));
            CznSkillMarkerCue standbyOff = sequence?.Markers.FirstOrDefault(
                marker => marker != null &&
                          string.Equals(marker.kind, "STANDBY_OFF", StringComparison.OrdinalIgnoreCase));
            if (sequence == null || standbyOn == null || standbyOff == null)
            {
                errors.Add("ug_attack: missing STANDBY_ON/STANDBY_OFF markers.");
                return;
            }
            if (standbyOff.startTime <= standbyOn.startTime + 0.001f)
            {
                errors.Add(
                    $"ug_attack: standby closes at {standbyOff.startTime:0.000}, not after opening at " +
                    $"{standbyOn.startTime:0.000}.");
                return;
            }

            player.RestartSequence(sequence);
            player.Evaluate(sequence, Mathf.Max(standbyOn.startTime, standbyOff.startTime - 0.001f));
            if (!player.IsStandbyVisible)
            {
                errors.Add("ug_attack: standby actor is hidden immediately before STANDBY_OFF.");
            }

            player.Evaluate(sequence, Mathf.Min(sequence.Duration, standbyOff.startTime + 0.001f));
            if (player.IsStandbyVisible)
            {
                errors.Add("ug_attack: standby actor remains visible after STANDBY_OFF.");
            }
        }

        private static void ValidateCompletionIdle(
            CznSpineSkillPlayer player,
            GameObject sceneRoot,
            List<string> errors)
        {
            if (player == null)
            {
                return;
            }

            player.ResetToIdle();
            if (!string.Equals(player.CurrentActorAnimationName, "b_idle", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Completion reset did not select b_idle.");
            }
            if (!player.IsCurrentActorAnimationLooping)
            {
                errors.Add("Completion b_idle animation is not looping.");
            }
            if (player.ActiveSpineLayerCount != 0 || player.ActiveParticleLayerCount != 0)
            {
                errors.Add("Completion reset left active Spine or particle layers.");
            }
            if (player.IsStandbyVisible)
            {
                errors.Add("Completion reset left the standby actor visible.");
            }
            if (CountRuntimeObjects(sceneRoot) != 0)
            {
                errors.Add("Completion reset left runtime effect objects in the scene.");
            }
        }

        private static int CountRuntimeObjects(GameObject root)
        {
            return root == null
                ? 0
                : root.GetComponentsInChildren<Transform>(true).Count(
                    transform => transform.name.StartsWith("[CZN] ", StringComparison.Ordinal) ||
                                 transform.name.StartsWith("[CZN Particle] ", StringComparison.Ordinal));
        }

        private static int CountActiveAttachments(GameObject root)
        {
            int count = 0;
            foreach (SkeletonAnimation skeleton in root.GetComponentsInChildren<SkeletonAnimation>(true))
            {
                if (!skeleton.gameObject.activeInHierarchy || skeleton.Skeleton == null)
                {
                    continue;
                }
                foreach (Slot slot in skeleton.Skeleton.Slots)
                {
                    if (slot.Attachment != null)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private static void WriteReports(
            JObject report,
            IReadOnlyList<CznSpineSkillSequence> sequences,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings)
        {
            File.WriteAllText(
                AbsolutePath(JsonReportPath),
                report.ToString(Newtonsoft.Json.Formatting.Indented) + Environment.NewLine,
                new UTF8Encoding(false));

            StringBuilder markdown = new StringBuilder();
            markdown.AppendLine("# 绯（30048）skill validation report");
            markdown.AppendLine();
            markdown.AppendLine($"- Skills: {sequences.Count}");
            markdown.AppendLine($"- Errors: {errors.Count}");
            markdown.AppendLine($"- Warnings: {warnings.Count}");
            markdown.AppendLine("- Playback: single play, completion cleanup, then looping `b_idle`.");
            markdown.AppendLine("- Replay samples: `u4_attack`, `ug_attack`, three hard restarts and three manual time rewinds each.");
            markdown.AppendLine();
            markdown.AppendLine("| Skill | Duration | Spine | Particles | Transforms | Camera zoom | Markers | Unresolved |");
            markdown.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
            foreach (CznSpineSkillSequence sequence in sequences)
            {
                markdown.AppendLine(
                    $"| `{sequence.SkillId}` | {sequence.Duration:0.000}s | {sequence.SpineLayers.Count} | " +
                    $"{sequence.ParticleLayers.Count} | {sequence.TransformCues.Count} | " +
                    $"{sequence.CameraZoomCues.Count} | {sequence.Markers.Count} | {sequence.UnresolvedResources.Count} |");
            }
            if (errors.Count > 0)
            {
                markdown.AppendLine();
                markdown.AppendLine("## Errors");
                foreach (string error in errors)
                {
                    markdown.AppendLine("- " + error);
                }
            }
            File.WriteAllText(
                AbsolutePath(MarkdownReportPath),
                markdown.ToString(),
                new UTF8Encoding(false));
        }

        private static string AbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
