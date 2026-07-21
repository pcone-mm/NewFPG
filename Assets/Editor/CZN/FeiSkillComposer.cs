using System;
using System.Collections.Generic;
using System.Globalization;
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
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace NewFPG.CZN.Editor
{
    public static class FeiSkillComposer
    {
        private const string CharacterRoot = "Assets/Imported/CZN/Fei_30048";
        private const string EffectConfigRoot = CharacterRoot + "/Configs/effect";
        private const string SpineRoot = CharacterRoot + "/SpineSource";
        private const string AncillaryRoot = CharacterRoot + "/AncillarySource";
        private const string SrmdPath = CharacterRoot + "/Configs/model_data/30048.srmd.json";
        private const string BrmdPath = CharacterRoot + "/Configs/model_data/30048_battle_ready.brmd.json";
        private const string MainPrefabPath = CharacterRoot + "/Preview/Prefabs/Fei_30048_Main.prefab";
        private const string BattleReadyPrefabPath = CharacterRoot + "/Preview/Prefabs/Fei_30048_BattleReady.prefab";
        private const string GeneratedRoot = CharacterRoot + "/Preview/SkillCompositions";
        private const string SkillAssetRoot = GeneratedRoot + "/Skills";
        private const string TimelineRoot = GeneratedRoot + "/Timelines";
        private const string GeneratedAssetRoot = GeneratedRoot + "/Generated";
        private const string SkillPreviewScenePath = CharacterRoot + "/Preview/Fei_30048_SkillPreview.unity";
        private const string SkillPreviewPrefabPath = CharacterRoot + "/Preview/Prefabs/Fei_30048_SkillComposer.prefab";
        private const string ReportPath = CharacterRoot + "/Metadata/skill-composition-report.md";
        private const string ResourceMapPath = CharacterRoot + "/Metadata/skill-resource-map.json";

        private static readonly SkillSpec[] SkillSpecs =
        {
            new SkillSpec("attack_play1", "普通攻击一", "attack_play1"),
            new SkillSpec("attack_play2", "普通攻击二", "attack_play2"),
            new SkillSpec("u1_buff", "U1 增益", "u1_buff_ready", "u1_buff_play"),
            new SkillSpec("u2_buff", "U2 增益", "u2_buff_ready", "u2_buff_play"),
            new SkillSpec("u3_buff", "U3 增益", "u3_buff_ready", "u3_buff_play"),
            new SkillSpec("u4_attack", "U4 攻击", "u4_attack_ready", "u4_attack_play", "u4_attack_end"),
            new SkillSpec("u5_buff", "U5 增益", "u5_buff_ready", "u5_buff_play"),
            new SkillSpec("ug_attack", "UG 终结技", "ug_attack"),
            new SkillSpec("ux_buff", "UX 特殊增益", "ux_buff"),
            new SkillSpec("fatal", "Fatal 连段", "fatal_intro", "fatal1", "fatal2", "fatal3"),
            new SkillSpec("enter", "战斗入场", "enter_ready", "enter_play", "enter_end"),
            new SkillSpec("victory", "胜利动作", "victory_ready", "victory"),
        };

        private static readonly Dictionary<string, SkeletonDataAsset> SkeletonAssets =
            new Dictionary<string, SkeletonDataAsset>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> JsonPaths =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Dictionary<string, object>> CfxCache =
            new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> CfxDurationCache =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> TransformDurationCache =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static string standbyTransformSource;

        [MenuItem("Tools/CZN/Fei 30048/Build Skill Compositions")]
        public static void BuildSkillCompositions()
        {
            EnsureInputExists();
            EnsureFolder(GeneratedRoot);
            EnsureFolder(SkillAssetRoot);
            EnsureFolder(TimelineRoot);
            EnsureFolder(GeneratedAssetRoot);
            BuildSourceIndexes();
            standbyTransformSource = ReadStandbyTransformSource();

            JObject srmd = JObject.Parse(File.ReadAllText(AbsolutePath(SrmdPath), Encoding.UTF8));
            JObject commands = srmd["command"] as JObject;
            if (commands == null)
            {
                throw new InvalidDataException("30048.srmd.json has no command object.");
            }

            List<CznSpineSkillSequence> sequences = new List<CznSpineSkillSequence>();
            List<TimelineAsset> timelines = new List<TimelineAsset>();
            for (int i = 0; i < SkillSpecs.Length; i++)
            {
                GeneratedSkill generated = GenerateSkill(SkillSpecs[i], commands);
                CznSpineSkillSequence sequence = CreateOrUpdateSequence(generated);
                TimelineAsset timeline = CreateTimeline(sequence);
                sequences.Add(sequence);
                timelines.Add(timeline);
            }

            Material particleMaterial = CreateOrUpdateParticleMaterial(false);
            Material additiveParticleMaterial = CreateOrUpdateParticleMaterial(true);
            Material lineMaterial = CreateOrUpdateLineMaterial();
            AssetDatabase.SaveAssets();
            CreatePreviewScene(sequences, timelines, particleMaterial, additiveParticleMaterial, lineMaterial);
            WriteReport(sequences);
            WriteResourceMap(sequences);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[CZN] Built {sequences.Count} Fei skill compositions at {SkillPreviewScenePath}. " +
                $"Spine layers: {sequences.Sum(item => item.SpineLayers.Count)}, " +
                $"particle emitters: {sequences.Sum(item => item.ParticleLayers.Count)}.");
        }

        private static GeneratedSkill GenerateSkill(SkillSpec spec, JObject commands)
        {
            GeneratedSkill result = new GeneratedSkill(spec.Id, spec.DisplayName);
            float phaseStart = 0f;
            for (int i = 0; i < spec.Commands.Length; i++)
            {
                string commandName = spec.Commands[i];
                JObject command = commands[commandName] as JObject;
                if (command == null)
                {
                    result.AddUnresolved("Missing SRMD command: " + commandName);
                    continue;
                }

                float phaseDuration = BuildCommand(commandName, command, phaseStart, result);
                phaseStart += Mathf.Max(0.05f, phaseDuration);
            }

            result.Duration = Mathf.Max(0.1f, phaseStart);
            result.SortCues();
            return result;
        }

        private static float BuildCommand(
            string commandName,
            JObject command,
            float phaseStart,
            GeneratedSkill result)
        {
            Dictionary<string, JObject> nodes = CollectNodes(command);
            Dictionary<string, float> startCache = new Dictionary<string, float>(StringComparer.Ordinal);
            Dictionary<string, float> durationCache = new Dictionary<string, float>(StringComparer.Ordinal);

            float NodeDuration(JObject node)
            {
                string guid = Text(node, "guid");
                if (!string.IsNullOrEmpty(guid) && durationCache.TryGetValue(guid, out float cached))
                {
                    return cached;
                }

                float duration = ResolveNodeDuration(node);
                if (!string.IsNullOrEmpty(guid))
                {
                    durationCache[guid] = duration;
                }
                return duration;
            }

            float NodeStart(JObject node, HashSet<string> chain = null)
            {
                string guid = Text(node, "guid");
                if (!string.IsNullOrEmpty(guid) && startCache.TryGetValue(guid, out float cached))
                {
                    return cached;
                }

                chain ??= new HashSet<string>(StringComparer.Ordinal);
                if (!string.IsNullOrEmpty(guid) && !chain.Add(guid))
                {
                    return Seconds(node, "delay");
                }

                float start = Seconds(node, "delay");
                string predecessorGuid = Text(node, "from_guid");
                if (!string.IsNullOrEmpty(predecessorGuid) && nodes.TryGetValue(predecessorGuid, out JObject predecessor))
                {
                    start += NodeStart(predecessor, chain);
                    if (Flag(predecessor, "wait_until_end"))
                    {
                        start += NodeDuration(predecessor);
                    }
                }

                if (!string.IsNullOrEmpty(guid))
                {
                    chain.Remove(guid);
                    startCache[guid] = start;
                }
                return start;
            }

            float phaseDuration = 0f;
            foreach (JObject node in nodes.Values)
            {
                phaseDuration = Mathf.Max(phaseDuration, NodeStart(node) + NodeDuration(node));
            }

            phaseDuration = Mathf.Clamp(phaseDuration, 0.05f, 20f);

            foreach (JObject animationNode in ObjectArray(command["ani"]))
            {
                string animationName = Text(animationNode, "animation_name");
                float duration = Mathf.Max(0.01f, NodeDuration(animationNode));
                result.ActorAnimations.Add(new CznActorAnimationCue
                {
                    phaseName = commandName,
                    animationName = animationName,
                    startTime = phaseStart + NodeStart(animationNode),
                    duration = duration,
                    loop = Flag(animationNode, "loop"),
                });
            }

            foreach (JObject effectNode in ObjectArray(command["effect"]))
            {
                string cfxName = Text(effectNode, "file_name");
                float effectStart = phaseStart + NodeStart(effectNode);
                BuildCfxCues(cfxName, effectNode, effectStart, result);
            }

            BuildCameraMoves(command, phaseStart, phaseDuration, NodeStart, result);
            BuildCameraZooms(command, phaseStart, NodeStart, NodeDuration, result);
            BuildTransformNodes(command, "shake", CznTransformTarget.Camera, phaseStart, NodeStart, result);
            BuildTransformNodes(command, "cam_spine", CznTransformTarget.Camera, phaseStart, NodeStart, result);

            foreach (JObject node in ObjectArray(command["node_ani"]))
            {
                string targetName = Text(node, "target");
                CznTransformTarget target = string.Equals(targetName, "SELF", StringComparison.OrdinalIgnoreCase)
                    ? CznTransformTarget.Self
                    : string.Equals(targetName, "STANDBY", StringComparison.OrdinalIgnoreCase)
                        ? CznTransformTarget.Standby
                        : CznTransformTarget.Target;
                BuildTransformNode(node, target, phaseStart + NodeStart(node), result);
            }

            BuildMarkers(nodes.Values, phaseStart, NodeStart, NodeDuration, result);
            foreach (JObject cutinNode in ObjectArray(command["cutin"]))
            {
                if (string.IsNullOrWhiteSpace(Text(cutinNode, "file_name")) &&
                    string.IsNullOrWhiteSpace(Text(cutinNode, "id")))
                {
                    result.AddUnresolved(
                        "CUTIN node has neither id nor file_name; retained as a diagnostic marker.");
                }
            }
            if (string.Equals(commandName, "ug_attack", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(standbyTransformSource))
            {
                JObject standbyOn = nodes.Values.FirstOrDefault(
                    node => string.Equals(Text(node, "etty"), "STANDBY_ON", StringComparison.OrdinalIgnoreCase));
                CznTransformCue standbyCue = LoadTransformCue(
                    standbyTransformSource,
                    string.Empty,
                    CznTransformTarget.Standby);
                if (standbyCue != null)
                {
                    standbyCue.startTime = phaseStart + (standbyOn != null ? NodeStart(standbyOn) : 0f);
                    result.TransformCues.Add(standbyCue);
                }
                else
                {
                    result.AddUnresolved("Missing BattleReady standby transform: " + standbyTransformSource);
                }
            }
            return phaseDuration;
        }

        private static void BuildCfxCues(
            string cfxName,
            JObject effectNode,
            float outerStart,
            GeneratedSkill result)
        {
            if (string.IsNullOrWhiteSpace(cfxName))
            {
                result.AddUnresolved("Effect node without file_name");
                return;
            }

            Dictionary<string, object> cfx = LoadCfx(cfxName);
            if (cfx == null)
            {
                result.AddUnresolved("Missing CFX: " + cfxName);
                return;
            }

            string anchorType = Text(effectNode, "type");
            CznSkillAnchor anchor = ParseAnchor(anchorType);
            if (string.Equals(anchorType, "FRONT", StringComparison.OrdinalIgnoreCase))
            {
                result.AddUnresolved(
                    "FRONT CFX anchor mapped to the screen foreground: " + cfxName);
            }
            Vector2 outerOffset = ParseJsonOffset(effectNode) * 0.01f;
            float outerScaleValue = Number(effectNode, "scale", 1f);
            if (outerScaleValue < 0f)
            {
                result.AddUnresolved($"Negative CFX outer scale interpreted as a default positive scale: {cfxName}={outerScaleValue}");
            }
            float outerScale = PositiveScale(outerScaleValue);
            float outerOpacityValue = Number(effectNode, "opacity", -1f);
            float outerAlpha = outerOpacityValue < 0f
                ? 1f
                : Mathf.Clamp01(outerOpacityValue > 1f ? outerOpacityValue / 255f : outerOpacityValue);
            float outerRotation = Number(effectNode, "rotation");
            int outerSorting = Mathf.RoundToInt(Number(effectNode, "zorder") + Number(effectNode, "global_z"));
            float explicitDuration = Number(effectNode, "duration", -1f) > 0f
                ? Seconds(effectNode, "duration")
                : -1f;

            foreach (object value in CznPlistReader.Array(cfx, "primitive"))
            {
                if (!(value is Dictionary<string, object> primitive))
                {
                    continue;
                }

                string sourceName = CznPlistReader.String(primitive, "source");
                string format = CznPlistReader.String(primitive, "format").Trim().ToLowerInvariant();
                float primitiveDelay = MillisecondsToSeconds(CznPlistReader.Float(primitive, "delay"));
                Vector2 offset = outerOffset + new Vector2(
                    CznPlistReader.Float(primitive, "x") * 0.01f,
                    CznPlistReader.Float(primitive, "y") * 0.01f);
                float primitiveScale = CznPlistReader.Float(primitive, "scale", 1f);
                if (primitiveScale < 0f)
                {
                    result.AddUnresolved(
                        $"Negative CFX primitive scale interpreted as a default positive scale: {cfxName}/{sourceName}={primitiveScale}");
                }
                float scale = outerScale * PositiveScale(primitiveScale);
                float rotation = outerRotation + CznPlistReader.Float(primitive, "rotate");
                int sorting = outerSorting + CznPlistReader.Integer(primitive, "z");

                if (format == "spine" || format == "spine2" || string.IsNullOrEmpty(format))
                {
                    BuildSpinePrimitive(
                        cfxName,
                        sourceName,
                        primitive,
                        anchor,
                        offset,
                        scale,
                        outerAlpha,
                        rotation,
                        sorting,
                        outerStart + primitiveDelay,
                        explicitDuration,
                        effectNode,
                        result);
                }
                else if (format == "particle")
                {
                    BuildParticlePrimitive(
                        cfxName,
                        sourceName,
                        primitive,
                        anchor,
                        offset,
                        scale,
                        rotation,
                        sorting,
                        outerStart + primitiveDelay,
                        explicitDuration,
                        result);
                }
                else
                {
                    result.AddUnresolved($"Unsupported CFX primitive format {format}: {cfxName}/{sourceName}");
                }
            }
        }

        private static void BuildSpinePrimitive(
            string cfxName,
            string sourceName,
            Dictionary<string, object> primitive,
            CznSkillAnchor anchor,
            Vector2 offset,
            float scale,
            float alpha,
            float rotation,
            int sorting,
            float startTime,
            float explicitDuration,
            JObject effectNode,
            GeneratedSkill result)
        {
            if (!SkeletonAssets.TryGetValue(sourceName, out SkeletonDataAsset skeletonDataAsset) || skeletonDataAsset == null)
            {
                result.AddUnresolved("Missing Spine layer: " + sourceName + " (from " + cfxName + ")");
                return;
            }

            string requestedAnimation = CznPlistReader.String(primitive, "ani");
            string animationName = ResolveAnimationName(skeletonDataAsset, requestedAnimation);
            float animationDuration = ResolveAnimationDuration(skeletonDataAsset, animationName);
            float lifetime = MillisecondsToSeconds(CznPlistReader.Float(primitive, "lifeTime"));
            int repeat = CznPlistReader.Integer(primitive, "repeat", 1);
            bool loop = repeat < 0 || Flag(effectNode, "loop");
            float duration = lifetime > 0f ? lifetime : animationDuration * Mathf.Max(1, repeat);
            if (loop && explicitDuration <= 0f)
            {
                duration = Mathf.Max(duration, 3f);
            }
            if (explicitDuration > 0f)
            {
                duration = Mathf.Min(Mathf.Max(0.01f, duration), explicitDuration);
            }

            result.SpineLayers.Add(new CznSpineLayerCue
            {
                compositeName = cfxName,
                sourceName = sourceName,
                skeletonDataAsset = skeletonDataAsset,
                animationName = animationName,
                anchor = anchor,
                offset = offset,
                startTime = startTime,
                duration = Mathf.Clamp(duration, 0.01f, 20f),
                scale = scale,
                alpha = alpha,
                rotation = rotation,
                sortingOrder = sorting,
                loop = loop,
                attachmentBone = Text(effectNode, "slot"),
            });

            string slot = Text(effectNode, "slot");
            string id = Text(effectNode, "id");
            if (!string.IsNullOrEmpty(slot) || (!string.IsNullOrEmpty(id) && id != "0"))
            {
                result.AddUnresolved($"Bone/slot attachment approximated at anchor root: {cfxName} slot={slot} id={id}");
            }
        }

        private static void BuildParticlePrimitive(
            string cfxName,
            string sourceName,
            Dictionary<string, object> primitive,
            CznSkillAnchor anchor,
            Vector2 primitiveOffset,
            float primitiveScale,
            float primitiveRotation,
            int sorting,
            float primitiveStart,
            float explicitDuration,
            GeneratedSkill result)
        {
            string particlePath = EffectConfigRoot + "/" + sourceName + ".particle.xml";
            if (!File.Exists(AbsolutePath(particlePath)))
            {
                result.AddUnresolved("Missing particle config: " + sourceName);
                return;
            }

            Dictionary<string, object> particleRoot = CznPlistReader.ReadDictionary(AbsolutePath(particlePath));
            foreach (object value in CznPlistReader.Array(particleRoot, "emitters"))
            {
                if (!(value is Dictionary<string, object> emitter) || CznPlistReader.Boolean(emitter, "disable"))
                {
                    continue;
                }

                float emitterDelay = CznPlistReader.Float(emitter, "startDelay");
                float emitterDuration = Mathf.Max(0.05f, CznPlistReader.Float(emitter, "duration", 0.5f));
                float interval = Mathf.Max(0f, CznPlistReader.Float(emitter, "interval"));
                int repeat = Mathf.Max(1, CznPlistReader.Integer(emitter, "repeat", 1));
                float lifetime = Mathf.Max(0.01f, CznPlistReader.Float(emitter, "particleLifespan", 0.5f));
                float lifetimeVariance = Mathf.Abs(CznPlistReader.Float(emitter, "particleLifespanVariance"));
                float lifetimeMin = Mathf.Max(0.01f, lifetime - lifetimeVariance);
                float lifetimeMax = Mathf.Max(lifetimeMin, lifetime + lifetimeVariance);
                float activeDuration = emitterDuration * repeat + interval * Mathf.Max(0, repeat - 1) + lifetimeMax;
                if (explicitDuration > 0f)
                {
                    activeDuration = Mathf.Min(activeDuration, explicitDuration);
                }

                float size = CznPlistReader.Float(emitter, "startParticleSize", 10f) * 0.01f;
                float sizeVariance = Mathf.Abs(CznPlistReader.Float(emitter, "startParticleSizeVariance")) * 0.01f;
                float speed = CznPlistReader.Float(emitter, "speed") * 0.01f;
                float speedVariance = Mathf.Abs(CznPlistReader.Float(emitter, "speedVariance")) * 0.01f;
                string texturePath = CznPlistReader.String(emitter, "textureFileName");
                Texture2D particleTexture = ResolveParticleTexture(texturePath);
                float emitterScaleValue = CznPlistReader.Float(emitter, "scale", 1f);
                if (emitterScaleValue < 0f)
                {
                    result.AddUnresolved(
                        $"Negative particle scale interpreted as a default positive scale: {sourceName}/{CznPlistReader.String(emitter, "name", "emitter")}={emitterScaleValue}");
                }
                float emitterScale = PositiveScale(emitterScaleValue);

                result.ParticleLayers.Add(new CznParticleLayerCue
                {
                    compositeName = cfxName,
                    sourceName = sourceName,
                    emitterName = CznPlistReader.String(emitter, "name", "emitter"),
                    originalTexturePath = texturePath,
                    texture = particleTexture,
                    anchor = anchor,
                    offset = primitiveOffset + new Vector2(
                        CznPlistReader.Float(emitter, "sourcePositionx") * 0.01f,
                        CznPlistReader.Float(emitter, "sourcePositiony") * 0.01f),
                    sourceVariance = new Vector2(
                        Mathf.Abs(CznPlistReader.Float(emitter, "sourcePositionVariancex")) * 0.01f,
                        Mathf.Abs(CznPlistReader.Float(emitter, "sourcePositionVariancey")) * 0.01f),
                    force = new Vector2(
                        CznPlistReader.Float(emitter, "gravityx") * 0.01f,
                        CznPlistReader.Float(emitter, "gravityy") * 0.01f),
                    startTime = primitiveStart + emitterDelay,
                    duration = Mathf.Clamp(activeDuration, 0.05f, 20f),
                    scale = primitiveScale * emitterScale,
                    rotation = primitiveRotation,
                    sortingOrder = sorting,
                    maxParticles = Mathf.Max(1, CznPlistReader.Integer(emitter, "maxParticles", 100)),
                    emissionRate = Mathf.Max(0f, CznPlistReader.Float(emitter, "emissionRate", 10f)),
                    lifetimeMin = lifetimeMin,
                    lifetimeMax = lifetimeMax,
                    speedMin = Mathf.Max(0f, speed - speedVariance),
                    speedMax = Mathf.Max(0f, speed + speedVariance),
                    sizeMin = Mathf.Max(0.001f, size - sizeVariance),
                    sizeMax = Mathf.Max(0.001f, size + sizeVariance),
                    angle = CznPlistReader.Float(emitter, "angle"),
                    angleVariance = Mathf.Abs(CznPlistReader.Float(emitter, "angleVariance")),
                    rotationVariance = Mathf.Abs(CznPlistReader.Float(emitter, "rotationStartVariance")),
                    startColor = ReadColor(emitter, "startColor", Color.white),
                    endColor = ReadColor(emitter, "finishColor", new Color(1f, 1f, 1f, 0f)),
                    additive = CznPlistReader.Integer(emitter, "blendFuncDestination") == 1,
                });

                if (!string.IsNullOrWhiteSpace(texturePath) && particleTexture == null)
                {
                    result.AddUnresolved("Missing shared particle texture; using soft fallback: " + texturePath);
                }
            }
        }

        private static void BuildCameraMoves(
            JObject command,
            float phaseStart,
            float phaseDuration,
            Func<JObject, HashSet<string>, float> nodeStart,
            GeneratedSkill result)
        {
            List<JObject> moves = ObjectArray(command["cam_move"])
                .OrderBy(node => nodeStart(node, null))
                .ToList();
            if (moves.Count == 0)
            {
                return;
            }

            CznTransformCue cue = new CznTransformCue
            {
                sourceName = "SRMD cam_move",
                target = CznTransformTarget.Camera,
                startTime = phaseStart,
                duration = phaseDuration,
                positionScale = 0.01f,
            };

            Vector2 current = Vector2.zero;
            cue.translateKeys.Add(new CznVector2Key { time = 0f, value = current });
            foreach (JObject move in moves)
            {
                float start = nodeStart(move, null);
                float duration = Seconds(move, "duration");
                Vector2 target = new Vector2(Number(move, "x"), Number(move, "y"));
                if (duration > 0.0001f)
                {
                    cue.translateKeys.Add(new CznVector2Key { time = start, value = current });
                    cue.translateKeys.Add(new CznVector2Key { time = start + duration, value = target });
                }
                else
                {
                    cue.translateKeys.Add(new CznVector2Key { time = start, value = target, stepped = true });
                }
                current = target;
            }

            cue.translateKeys = cue.translateKeys.OrderBy(key => key.time).ToList();
            result.TransformCues.Add(cue);
        }

        private static void BuildCameraZooms(
            JObject command,
            float phaseStart,
            Func<JObject, HashSet<string>, float> nodeStart,
            Func<JObject, float> nodeDuration,
            GeneratedSkill result)
        {
            foreach (JObject node in ObjectArray(command["cam_zoom"]))
            {
                result.CameraZoomCues.Add(new CznCameraZoomCue
                {
                    startTime = phaseStart + nodeStart(node, null),
                    duration = nodeDuration(node),
                    zoom = Mathf.Max(0.01f, Number(node, "zoom", 1f)),
                });
            }
        }

        private static void BuildTransformNodes(
            JObject command,
            string propertyName,
            CznTransformTarget target,
            float phaseStart,
            Func<JObject, HashSet<string>, float> nodeStart,
            GeneratedSkill result)
        {
            foreach (JObject node in ObjectArray(command[propertyName]))
            {
                BuildTransformNode(node, target, phaseStart + nodeStart(node, null), result);
            }
        }

        private static void BuildTransformNode(
            JObject node,
            CznTransformTarget target,
            float startTime,
            GeneratedSkill result)
        {
            string sourceName = Text(node, "file_name");
            CznTransformCue cue = LoadTransformCue(sourceName, Text(node, "animation_name"), target);
            if (cue == null)
            {
                result.AddUnresolved("Missing/unsupported transform animation: " + sourceName);
                return;
            }

            cue.startTime = startTime;
            result.TransformCues.Add(cue);
        }

        private static void BuildMarkers(
            IEnumerable<JObject> nodes,
            float phaseStart,
            Func<JObject, HashSet<string>, float> nodeStart,
            Func<JObject, float> nodeDuration,
            GeneratedSkill result)
        {
            HashSet<string> ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ENTRY", "ANI", "EFFECT", "SHAKE", "NODE_ANI", "CAM_SPINE", "CAM_MOVE", "CAM_ZOOM",
            };

            foreach (JObject node in nodes)
            {
                string kind = Text(node, "etty");
                if (string.IsNullOrEmpty(kind) || ignored.Contains(kind))
                {
                    continue;
                }

                float value = Number(node, "value");
                string label = kind;
                if (Mathf.Abs(value) > 0.0001f)
                {
                    label += " " + value.ToString("0.##", CultureInfo.InvariantCulture);
                }

                result.Markers.Add(new CznSkillMarkerCue
                {
                    kind = kind,
                    label = label,
                    startTime = phaseStart + nodeStart(node, null),
                    duration = nodeDuration(node),
                    value = value,
                });
            }
        }

        private static float ResolveNodeDuration(JObject node)
        {
            string type = Text(node, "etty").ToUpperInvariant();
            float declared = Number(node, "duration", 0f);
            if (declared > 0f)
            {
                return Mathf.Clamp(MillisecondsToSeconds(declared), 0f, 20f);
            }

            switch (type)
            {
                case "EFFECT":
                    return Mathf.Clamp(ResolveCfxDuration(Text(node, "file_name")), 0.01f, 20f);
                case "SHAKE":
                case "NODE_ANI":
                case "CAM_SPINE":
                    return Mathf.Clamp(ResolveTransformDuration(Text(node, "file_name")), 0.01f, 20f);
                case "STANDBY_ACTION":
                    return !string.IsNullOrWhiteSpace(standbyTransformSource)
                        ? Mathf.Clamp(ResolveTransformDuration(standbyTransformSource), 0.01f, 20f)
                        : 0f;
                default:
                    return 0f;
            }
        }

        private static float ResolveCfxDuration(string cfxName)
        {
            if (string.IsNullOrWhiteSpace(cfxName))
            {
                return 0f;
            }
            if (CfxDurationCache.TryGetValue(cfxName, out float cached))
            {
                return cached;
            }

            Dictionary<string, object> cfx = LoadCfx(cfxName);
            float duration = 0f;
            if (cfx != null)
            {
                foreach (object value in CznPlistReader.Array(cfx, "primitive"))
                {
                    if (!(value is Dictionary<string, object> primitive))
                    {
                        continue;
                    }

                    string format = CznPlistReader.String(primitive, "format").ToLowerInvariant();
                    string source = CznPlistReader.String(primitive, "source");
                    float delay = MillisecondsToSeconds(CznPlistReader.Float(primitive, "delay"));
                    float localDuration;
                    if (format == "particle")
                    {
                        localDuration = ResolveParticleDuration(source);
                    }
                    else if (SkeletonAssets.TryGetValue(source, out SkeletonDataAsset asset) && asset != null)
                    {
                        string animation = ResolveAnimationName(asset, CznPlistReader.String(primitive, "ani"));
                        localDuration = ResolveAnimationDuration(asset, animation);
                        float lifetime = MillisecondsToSeconds(CznPlistReader.Float(primitive, "lifeTime"));
                        if (lifetime > 0f)
                        {
                            localDuration = lifetime;
                        }
                        int repeat = CznPlistReader.Integer(primitive, "repeat", 1);
                        localDuration *= Mathf.Max(1, repeat);
                        if (repeat < 0)
                        {
                            localDuration = Mathf.Max(3f, localDuration);
                        }
                    }
                    else
                    {
                        localDuration = 0.5f;
                    }

                    duration = Mathf.Max(duration, delay + localDuration);
                }
            }

            duration = Mathf.Max(0.05f, duration);
            CfxDurationCache[cfxName] = duration;
            return duration;
        }

        private static float ResolveParticleDuration(string sourceName)
        {
            string path = EffectConfigRoot + "/" + sourceName + ".particle.xml";
            if (!File.Exists(AbsolutePath(path)))
            {
                return 0.5f;
            }

            float duration = 0f;
            Dictionary<string, object> root = CznPlistReader.ReadDictionary(AbsolutePath(path));
            foreach (object value in CznPlistReader.Array(root, "emitters"))
            {
                if (!(value is Dictionary<string, object> emitter) || CznPlistReader.Boolean(emitter, "disable"))
                {
                    continue;
                }

                float delay = CznPlistReader.Float(emitter, "startDelay");
                float emitterDuration = Mathf.Max(0.05f, CznPlistReader.Float(emitter, "duration", 0.5f));
                float interval = Mathf.Max(0f, CznPlistReader.Float(emitter, "interval"));
                int repeat = Mathf.Max(1, CznPlistReader.Integer(emitter, "repeat", 1));
                float life = Mathf.Max(0.01f,
                    CznPlistReader.Float(emitter, "particleLifespan", 0.5f) +
                    Mathf.Abs(CznPlistReader.Float(emitter, "particleLifespanVariance")));
                duration = Mathf.Max(duration, delay + emitterDuration * repeat + interval * Mathf.Max(0, repeat - 1) + life);
            }
            return Mathf.Max(0.05f, duration);
        }

        private static CznTransformCue LoadTransformCue(
            string sourceName,
            string requestedAnimation,
            CznTransformTarget target)
        {
            if (string.IsNullOrWhiteSpace(sourceName) || !JsonPaths.TryGetValue(sourceName, out string assetPath))
            {
                return null;
            }

            JObject json = JObject.Parse(File.ReadAllText(AbsolutePath(assetPath), Encoding.UTF8));
            JObject animations = json["animations"] as JObject;
            if (animations == null || !animations.Properties().Any())
            {
                return null;
            }

            JProperty animationProperty = !string.IsNullOrWhiteSpace(requestedAnimation) && animations.Property(requestedAnimation) != null
                ? animations.Property(requestedAnimation)
                : animations.Properties().First();
            JObject animation = animationProperty.Value as JObject;
            JObject bones = animation?["bones"] as JObject;
            if (bones == null || !bones.Properties().Any())
            {
                return null;
            }

            JProperty boneProperty = bones.Property("cam") ??
                                     bones.Property("camera") ??
                                     bones.Property("node") ??
                                     bones.Properties().First();
            JObject bone = boneProperty.Value as JObject;
            CznTransformCue cue = new CznTransformCue
            {
                sourceName = sourceName,
                target = target,
                positionScale = 0.01f,
            };

            foreach (JObject key in ObjectArray(bone?["translate"]))
            {
                cue.translateKeys.Add(new CznVector2Key
                {
                    time = Number(key, "time"),
                    value = new Vector2(Number(key, "x"), Number(key, "y")),
                    stepped = IsStepped(key),
                });
            }
            foreach (JObject key in ObjectArray(bone?["rotate"]))
            {
                cue.rotateKeys.Add(new CznFloatKey
                {
                    time = Number(key, "time"),
                    value = Number(key, "angle"),
                    stepped = IsStepped(key),
                });
            }
            foreach (JObject key in ObjectArray(bone?["scale"]))
            {
                cue.scaleKeys.Add(new CznVector2Key
                {
                    time = Number(key, "time"),
                    value = new Vector2(Number(key, "x", 1f), Number(key, "y", 1f)),
                    stepped = IsStepped(key),
                });
            }

            cue.duration = Mathf.Max(
                MaxTime(cue.translateKeys),
                Mathf.Max(MaxTime(cue.rotateKeys), MaxTime(cue.scaleKeys)));
            cue.duration = Mathf.Max(0.01f, cue.duration);
            return cue;
        }

        private static float ResolveTransformDuration(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                return 0f;
            }
            if (TransformDurationCache.TryGetValue(sourceName, out float cached))
            {
                return cached;
            }
            CznTransformCue cue = LoadTransformCue(sourceName, string.Empty, CznTransformTarget.Camera);
            float duration = cue != null ? cue.duration : 0.1f;
            TransformDurationCache[sourceName] = duration;
            return duration;
        }

        private static CznSpineSkillSequence CreateOrUpdateSequence(GeneratedSkill generated)
        {
            string path = SkillAssetRoot + "/Fei_30048_" + generated.Id + ".asset";
            CznSpineSkillSequence sequence = AssetDatabase.LoadAssetAtPath<CznSpineSkillSequence>(path);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<CznSpineSkillSequence>();
                AssetDatabase.CreateAsset(sequence, path);
            }

            sequence.SetGeneratedData(
                generated.Id,
                generated.DisplayName,
                generated.Duration,
                generated.ActorAnimations,
                generated.SpineLayers,
                generated.ParticleLayers,
                generated.TransformCues,
                generated.CameraZoomCues,
                generated.Markers,
                "SRMD graph timing and CFX Spine layering are data-derived. Particle emitter parameters are translated, " +
                "and exact audited particle textures are bound when present; unresolved textures use a soft fallback. " +
                "Particle additive flags use the project's additive particle shader fallback. " +
                "BattleReady standby visibility and its BRMD node transform are composed for UG. FRONT effects are " +
                "placed on the screen foreground. Masks, custom shaders, color-blend and " +
                "post-process nodes remain diagnostic markers. Negative/sentinel CFX scales use a positive preview " +
                "fallback, ancillary Spine Bezier keys are sampled with linear interpolation, and only the primary " +
                "cam/camera/node bone is sampled from multi-bone ancillary skeletons. UX CUTIN remains diagnostic " +
                "because the source node has neither id nor file_name.",
                generated.UnresolvedResources);
            EditorUtility.SetDirty(sequence);
            return sequence;
        }

        private static TimelineAsset CreateTimeline(CznSpineSkillSequence sequence)
        {
            string path = TimelineRoot + "/Fei_30048_" + sequence.SkillId + ".playable";
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = "Fei_30048_" + sequence.SkillId;
            AssetDatabase.CreateAsset(timeline, path);
            CznSpineSkillTrack track = timeline.CreateTrack<CznSpineSkillTrack>(null, "CZN Skill Composition");
            TimelineClip clip = track.CreateClip<CznSpineSkillPlayableAsset>();
            clip.displayName = sequence.DisplayName;
            clip.start = 0d;
            clip.duration = sequence.Duration;
            CznSpineSkillPlayableAsset playableAsset = clip.asset as CznSpineSkillPlayableAsset;
            if (playableAsset != null)
            {
                playableAsset.Sequence = sequence;
                EditorUtility.SetDirty(playableAsset);
            }
            EditorUtility.SetDirty(track);
            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static void CreatePreviewScene(
            IReadOnlyList<CznSpineSkillSequence> sequences,
            IReadOnlyList<TimelineAsset> timelines,
            Material particleMaterial,
            Material additiveParticleMaterial,
            Material lineMaterial)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty && activeScene.path != SkillPreviewScenePath)
            {
                if (!string.IsNullOrEmpty(activeScene.path) &&
                    activeScene.path.StartsWith(CharacterRoot + "/Preview/", StringComparison.OrdinalIgnoreCase))
                {
                    EditorSceneManager.SaveScene(activeScene);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Save or close the unrelated dirty scene before rebuilding the CZN skill preview: " +
                        (string.IsNullOrEmpty(activeScene.path) ? "<Untitled>" : activeScene.path));
                }
            }

            Scene previewScene;
            if (File.Exists(AbsolutePath(SkillPreviewScenePath)))
            {
                previewScene = EditorSceneManager.OpenScene(SkillPreviewScenePath, OpenSceneMode.Single);
                foreach (GameObject existingRoot in previewScene.GetRootGameObjects())
                {
                    UnityEngine.Object.DestroyImmediate(existingRoot);
                }
            }
            else
            {
                previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            SceneManager.SetActiveScene(previewScene);

            GameObject root = new GameObject("Fei 30048 Skill Composition Preview");
            GameObject selfAnchorObject = CreateChild(root.transform, "Self Anchor", new Vector3(-2.25f, 0f, 0f));
            GameObject targetAnchorObject = CreateChild(root.transform, "Target Anchor", new Vector3(2.25f, 0f, 0f));
            GameObject standbyAnchorObject = CreateChild(root.transform, "Standby Anchor", new Vector3(-4.23f, -2.15f, 0f));
            GameObject centerAnchorObject = CreateChild(root.transform, "Center Anchor", new Vector3(0f, 0.5f, 0f));
            GameObject screenAnchorObject = CreateChild(root.transform, "Screen Anchor", new Vector3(0f, 2.2f, 0f));
            GameObject effectRootObject = CreateChild(root.transform, "Runtime Effects", Vector3.zero);
            GameObject cameraShakeObject = CreateChild(root.transform, "Camera Shake Root", Vector3.zero);

            GameObject mainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainPrefabPath);
            GameObject actorObject = mainPrefab != null
                ? PrefabUtility.InstantiatePrefab(mainPrefab) as GameObject
                : null;
            if (actorObject == null)
            {
                throw new InvalidOperationException("Could not instantiate " + MainPrefabPath);
            }
            SceneManager.MoveGameObjectToScene(actorObject, previewScene);
            actorObject.name = "Fei_30048_Actor";
            actorObject.transform.SetParent(selfAnchorObject.transform, false);
            actorObject.transform.localPosition = Vector3.zero;
            actorObject.transform.localRotation = Quaternion.identity;
            actorObject.transform.localScale = Vector3.one;
            SkeletonAnimation actor = actorObject.GetComponentInChildren<SkeletonAnimation>();
            MeshRenderer actorRenderer = actorObject.GetComponentInChildren<MeshRenderer>();
            if (actorRenderer != null)
            {
                actorRenderer.sortingOrder = 0;
            }

            GameObject battleReadyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleReadyPrefabPath);
            GameObject standbyObject = battleReadyPrefab != null
                ? PrefabUtility.InstantiatePrefab(battleReadyPrefab) as GameObject
                : null;
            if (standbyObject == null)
            {
                throw new InvalidOperationException("Could not instantiate " + BattleReadyPrefabPath);
            }
            SceneManager.MoveGameObjectToScene(standbyObject, previewScene);
            standbyObject.name = "Fei_30048_StandbyActor";
            standbyObject.transform.SetParent(standbyAnchorObject.transform, false);
            standbyObject.transform.localPosition = Vector3.zero;
            standbyObject.transform.localRotation = Quaternion.identity;
            standbyObject.transform.localScale = Vector3.one * 0.75f;
            SkeletonAnimation standbyActor = standbyObject.GetComponentInChildren<SkeletonAnimation>();
            MeshRenderer standbyRenderer = standbyObject.GetComponentInChildren<MeshRenderer>();
            if (standbyRenderer != null)
            {
                standbyRenderer.sortingOrder = -20;
            }
            standbyObject.SetActive(false);

            CreateTargetMarker(targetAnchorObject.transform, lineMaterial);

            GameObject cameraObject = new GameObject("Fei Skill Preview Camera", typeof(Camera));
            cameraObject.transform.SetParent(cameraShakeObject.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 2.2f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.025f, 0.055f, 1f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.tag = "MainCamera";

            GameObject lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;

            CznSpineSkillPlayer player = root.AddComponent<CznSpineSkillPlayer>();
            player.Configure(
                actor,
                standbyActor,
                selfAnchorObject.transform,
                targetAnchorObject.transform,
                standbyAnchorObject.transform,
                centerAnchorObject.transform,
                screenAnchorObject.transform,
                effectRootObject.transform,
                cameraShakeObject.transform,
                camera,
                particleMaterial,
                additiveParticleMaterial);
            player.SetIdleAnimation("b_idle");

            PlayableDirector director = root.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;

            int initialIndex = Mathf.Clamp(2, 0, timelines.Count - 1);
            director.playableAsset = timelines[initialIndex];
            BindTimeline(director, timelines[initialIndex], player);

            CznSpineSkillPreviewMenu menu = root.AddComponent<CznSpineSkillPreviewMenu>();
            menu.Configure(
                director,
                player,
                sequences.ToArray(),
                timelines.ToArray(),
                initialIndex,
                "绯（30048）技能组合预览");

            menu.ConfigurePlaybackMode(false, true);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(SkillPreviewPrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(SkillPreviewPrefabPath);
            }
            PrefabUtility.SaveAsPrefabAsset(root, SkillPreviewPrefabPath);
            EditorSceneManager.SaveScene(previewScene, SkillPreviewScenePath);
            SceneManager.SetActiveScene(previewScene);
            Selection.activeGameObject = root;
        }

        private static void BindTimeline(PlayableDirector director, TimelineAsset timeline, CznSpineSkillPlayer player)
        {
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (track is CznSpineSkillTrack)
                {
                    director.SetGenericBinding(track, player);
                }
            }
        }

        private static void CreateTargetMarker(Transform parent, Material lineMaterial)
        {
            GameObject markerObject = new GameObject("Target Position Marker", typeof(LineRenderer));
            markerObject.transform.SetParent(parent, false);
            markerObject.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            LineRenderer line = markerObject.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 48;
            line.startWidth = 0.025f;
            line.endWidth = 0.025f;
            line.startColor = new Color(0.25f, 0.85f, 1f, 0.85f);
            line.endColor = line.startColor;
            line.sharedMaterial = lineMaterial;
            line.sortingOrder = -10;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = Mathf.PI * 2f * i / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.55f, Mathf.Sin(angle) * 0.55f, 0f));
            }
        }

        private static Material CreateOrUpdateParticleMaterial(bool additive)
        {
            const string texturePath = GeneratedAssetRoot + "/CZN_SoftParticleTexture.asset";
            string materialPath = GeneratedAssetRoot +
                                  (additive ? "/CZN_ParticleAdditiveFallback.mat" : "/CZN_ParticleFallback.mat");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
                {
                    name = "CZN Soft Particle Texture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                AssetDatabase.CreateAsset(texture, texturePath);
            }

            Color[] pixels = new Color[texture.width * texture.height];
            Vector2 center = new Vector2((texture.width - 1) * 0.5f, (texture.height - 1) * 0.5f);
            float radius = Mathf.Max(1f, texture.width * 0.5f);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2f);
                    pixels[y * texture.width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);

            Material additiveTemplate = additive
                ? AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/ThirdParty/VFX_Klaus/Materials/Mat_fx_HCFX_set_add.mat")
                : null;
            Shader shader = additive
                ? additiveTemplate != null
                    ? additiveTemplate.shader
                    : Shader.Find("Legacy Shaders/Particles/Additive")
                : Shader.Find("Sprites/Default") ??
                  Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                  Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    additive ? "No additive particle shader is available." : "No alpha particle shader is available.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = additiveTemplate != null
                    ? new Material(additiveTemplate)
                    : new Material(shader);
                material.name = additive ? "CZN Particle Additive Fallback" : "CZN Particle Fallback";
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (additiveTemplate != null)
            {
                material.shader = additiveTemplate.shader;
                material.CopyPropertiesFromMaterial(additiveTemplate);
            }
            else
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("Texture2D_F593E37E")) material.SetTexture("Texture2D_F593E37E", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateLineMaterial()
        {
            const string materialPath = GeneratedAssetRoot + "/CZN_TargetMarker.mat";
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "CZN Target Marker" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void WriteReport(IReadOnlyList<CznSpineSkillSequence> sequences)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("# 绯（30048）Unity skill composition report");
            report.AppendLine();
            report.AppendLine("Generated from `30048.srmd.json`, the referenced CFX files, particle plists, " +
                              "converted Spine 3.8 assets and ancillary camera/node JSON.");
            report.AppendLine();
            report.AppendLine("| Skill | Duration | Actor cues | Spine layers | Particle emitters | Transform cues | Camera zoom | Markers | Unresolved |");
            report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (CznSpineSkillSequence sequence in sequences)
            {
                report.AppendLine(
                    $"| `{sequence.SkillId}` | {sequence.Duration:0.000}s | {sequence.ActorAnimations.Count} | " +
                    $"{sequence.SpineLayers.Count} | {sequence.ParticleLayers.Count} | {sequence.TransformCues.Count} | " +
                    $"{sequence.CameraZoomCues.Count} | {sequence.Markers.Count} | {sequence.UnresolvedResources.Count} |");
            }
            report.AppendLine();
            report.AppendLine("## Recovery boundary");
            report.AppendLine();
            report.AppendLine("- SRMD graph delays, command phases, CFX front/back ordering, anchors, offsets, scale and Spine animation names are data-derived.");
            report.AppendLine("- Camera/node SCSP1U files are converted to JSON and sampled as transform cues.");
            report.AppendLine("- Particle emitter motion/color/lifetime parameters are translated. Four exact config-referenced `particle/*.sct` textures are decoded and bound; additive plist emitters use the project's additive particle shader, and any unresolved texture uses a generated soft fallback.");
            report.AppendLine("- Negative CFX scale values are retained as unresolved sentinel/mirroring semantics; the preview uses a positive fallback scale instead of guessing their engine-specific meaning.");
            report.AppendLine("- Ancillary camera/node keyframe values and durations are recovered; camera scale is combined with SRMD zoom as orthographic zoom, while non-stepped Spine Bezier curves are linearly interpolated in this preview.");
            report.AppendLine("- Multi-bone ancillary skeletons sample only the primary `cam`/`camera`/`node` bone; helper `node`/`pivot` bone motion remains in the source JSON but is not composed here.");
            report.AppendLine("- Original custom masks, shaders, radial RGB blur, speed blur, hit-stop and color-blend nodes are retained as Timeline diagnostic markers, not pixel-identical post-processing.");
            report.AppendLine("- UG composes the BattleReady standby actor and its BRMD node transform. The preview scale/placement is a study approximation based on BRMD standby coordinates.");
            report.AppendLine("- `FRONT` CFX anchors are mapped to the screen foreground because the Unity preview has no native XCent FRONT layer.");
            report.AppendLine("- UX `CUTIN` is retained as a diagnostic marker: the source node has neither `id` nor `file_name`, so no name-only candidate is bound.");
            report.AppendLine();
            report.AppendLine("Preview scene: `Assets/Imported/CZN/Fei_30048/Preview/Fei_30048_SkillPreview.unity`.");
            File.WriteAllText(AbsolutePath(ReportPath), report.ToString(), new UTF8Encoding(false));
        }

        private static void WriteResourceMap(IReadOnlyList<CznSpineSkillSequence> sequences)
        {
            JArray skillArray = new JArray();
            foreach (CznSpineSkillSequence sequence in sequences)
            {
                JObject skill = new JObject
                {
                    ["id"] = sequence.SkillId,
                    ["display_name"] = sequence.DisplayName,
                    ["duration"] = sequence.Duration,
                    ["actor"] = new JArray(sequence.ActorAnimations.Select(cue => new JObject
                    {
                        ["phase"] = cue.phaseName,
                        ["animation"] = cue.animationName,
                        ["start"] = cue.startTime,
                        ["duration"] = cue.duration,
                        ["loop"] = cue.loop,
                    })),
                    ["spine"] = new JArray(sequence.SpineLayers.Select(cue => new JObject
                    {
                        ["composite"] = cue.compositeName,
                        ["source"] = cue.sourceName,
                        ["animation"] = cue.animationName,
                        ["anchor"] = cue.anchor.ToString(),
                        ["start"] = cue.startTime,
                        ["duration"] = cue.duration,
                        ["alpha"] = cue.alpha,
                        ["sorting_order"] = cue.sortingOrder,
                    })),
                    ["particles"] = new JArray(sequence.ParticleLayers.Select(cue => new JObject
                    {
                        ["composite"] = cue.compositeName,
                        ["source"] = cue.sourceName,
                        ["emitter"] = cue.emitterName,
                        ["texture"] = cue.texture != null ? AssetDatabase.GetAssetPath(cue.texture) : cue.originalTexturePath,
                        ["anchor"] = cue.anchor.ToString(),
                        ["start"] = cue.startTime,
                        ["duration"] = cue.duration,
                        ["additive"] = cue.additive,
                    })),
                    ["transforms"] = new JArray(sequence.TransformCues.Select(cue => new JObject
                    {
                        ["source"] = cue.sourceName,
                        ["target"] = cue.target.ToString(),
                        ["start"] = cue.startTime,
                        ["duration"] = cue.duration,
                    })),
                    ["camera_zoom"] = new JArray(sequence.CameraZoomCues.Select(cue => new JObject
                    {
                        ["start"] = cue.startTime,
                        ["duration"] = cue.duration,
                        ["zoom"] = cue.zoom,
                    })),
                    ["markers"] = new JArray(sequence.Markers.Select(cue => new JObject
                    {
                        ["kind"] = cue.kind,
                        ["label"] = cue.label,
                        ["start"] = cue.startTime,
                        ["duration"] = cue.duration,
                    })),
                    ["unresolved"] = new JArray(sequence.UnresolvedResources),
                };
                skillArray.Add(skill);
            }

            JObject root = new JObject
            {
                ["character"] = "绯",
                ["character_id"] = "30048",
                ["source"] = "30048.srmd + 30048_battle_ready.brmd",
                ["skills"] = skillArray,
            };
            File.WriteAllText(
                AbsolutePath(ResourceMapPath),
                root.ToString(Newtonsoft.Json.Formatting.Indented) + Environment.NewLine,
                new UTF8Encoding(false));
        }

        private static Dictionary<string, JObject> CollectNodes(JObject command)
        {
            Dictionary<string, JObject> result = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (JProperty property in command.Properties())
            {
                foreach (JObject node in ObjectArray(property.Value))
                {
                    string guid = Text(node, "guid");
                    if (!string.IsNullOrWhiteSpace(guid))
                    {
                        result[guid] = node;
                    }
                }
            }
            return result;
        }

        private static IEnumerable<JObject> ObjectArray(JToken token)
        {
            if (token is JArray array)
            {
                foreach (JToken value in array)
                {
                    if (value is JObject owner)
                    {
                        yield return owner;
                    }
                }
            }
        }

        private static string ReadStandbyTransformSource()
        {
            JObject brmd = JObject.Parse(File.ReadAllText(AbsolutePath(BrmdPath), Encoding.UTF8));
            JObject ugAttack = brmd["command"]?["ug_attack"] as JObject;
            JObject node = ObjectArray(ugAttack?["node_ani"]).FirstOrDefault();
            return Text(node, "file_name");
        }

        private static Texture2D ResolveParticleTexture(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return null;
            }

            string normalized = sourcePath.Replace('\\', '/');
            string stem = Path.GetFileNameWithoutExtension(normalized);
            string directPath = SpineRoot + "/particle/" + stem + ".png";
            Texture2D direct = AssetDatabase.LoadAssetAtPath<Texture2D>(directPath);
            if (direct != null)
            {
                return direct;
            }

            foreach (string guid in AssetDatabase.FindAssets(stem + " t:Texture2D", new[] { SpineRoot + "/particle" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), stem, StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }
            return null;
        }

        private static Dictionary<string, object> LoadCfx(string cfxName)
        {
            if (CfxCache.TryGetValue(cfxName, out Dictionary<string, object> cached))
            {
                return cached;
            }
            string path = EffectConfigRoot + "/" + cfxName + ".cfx.xml";
            string absolutePath = AbsolutePath(path);
            if (!File.Exists(absolutePath))
            {
                CfxCache[cfxName] = null;
                return null;
            }
            Dictionary<string, object> parsed = CznPlistReader.ReadDictionary(absolutePath);
            CfxCache[cfxName] = parsed;
            return parsed;
        }

        private static void BuildSourceIndexes()
        {
            SkeletonAssets.Clear();
            JsonPaths.Clear();
            CfxCache.Clear();
            CfxDurationCache.Clear();
            TransformDurationCache.Clear();

            string[] skeletonGuids = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { SpineRoot });
            foreach (string guid in skeletonGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SkeletonDataAsset asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);
                string fileName = Path.GetFileNameWithoutExtension(path);
                string baseName = fileName.EndsWith("_SkeletonData", StringComparison.Ordinal)
                    ? fileName.Substring(0, fileName.Length - "_SkeletonData".Length)
                    : fileName;

                if (!SkeletonAssets.TryGetValue(baseName, out SkeletonDataAsset existing) ||
                    (path.Contains("/zhs/") && !AssetDatabase.GetAssetPath(existing).Contains("/zhs/")))
                {
                    SkeletonAssets[baseName] = asset;
                }
            }

            IndexJsonFiles(AncillaryRoot, true);
            IndexJsonFiles(SpineRoot + "/effect", false);
        }

        private static void IndexJsonFiles(string root, bool overwrite)
        {
            string absoluteRoot = AbsolutePath(root);
            if (!Directory.Exists(absoluteRoot))
            {
                return;
            }
            foreach (string absoluteFile in Directory.GetFiles(absoluteRoot, "*.json", SearchOption.AllDirectories))
            {
                string baseName = Path.GetFileNameWithoutExtension(absoluteFile);
                string assetPath = ToAssetPath(absoluteFile);
                if (overwrite || !JsonPaths.ContainsKey(baseName))
                {
                    JsonPaths[baseName] = assetPath;
                }
            }
        }

        private static string ResolveAnimationName(SkeletonDataAsset asset, string requested)
        {
            SkeletonData data = asset != null ? asset.GetSkeletonData(true) : null;
            if (data == null || data.Animations.Count == 0)
            {
                return string.IsNullOrWhiteSpace(requested) ? "animation" : requested;
            }
            if (!string.IsNullOrWhiteSpace(requested) && data.FindAnimation(requested) != null)
            {
                return requested;
            }
            if (data.FindAnimation("animation") != null)
            {
                return "animation";
            }
            return data.Animations.Items[0].Name;
        }

        private static float ResolveAnimationDuration(SkeletonDataAsset asset, string animationName)
        {
            SkeletonData data = asset != null ? asset.GetSkeletonData(true) : null;
            Spine.Animation animation = data != null ? data.FindAnimation(animationName) : null;
            return animation != null ? Mathf.Max(0.01f, animation.Duration) : 0.5f;
        }

        private static CznSkillAnchor ParseAnchor(string value)
        {
            switch ((value ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "TARGET":
                    return CznSkillAnchor.Target;
                case "CENTER":
                    return CznSkillAnchor.Center;
                case "SCREEN":
                case "FRONT":
                    return CznSkillAnchor.Screen;
                default:
                    return CznSkillAnchor.Self;
            }
        }

        private static Vector2 ParseJsonOffset(JObject owner)
        {
            string value = Text(owner, "offset_xy");
            if (!string.IsNullOrWhiteSpace(value))
            {
                return ParseOffset(value);
            }
            if (owner["offset"] is JArray array && array.Count >= 2)
            {
                return new Vector2(TokenNumber(array[0]), TokenNumber(array[1]));
            }
            return Vector2.zero;
        }

        private static Vector2 ParseOffset(string value)
        {
            string[] parts = (value ?? string.Empty).Split(',');
            return new Vector2(
                parts.Length > 0 ? ParseRangeNumber(parts[0]) : 0f,
                parts.Length > 1 ? ParseRangeNumber(parts[1]) : 0f);
        }

        private static float ParseRangeNumber(string value)
        {
            string[] range = (value ?? string.Empty).Split('~');
            if (range.Length == 2 &&
                float.TryParse(range[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float a) &&
                float.TryParse(range[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
            {
                return (a + b) * 0.5f;
            }
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : 0f;
        }

        private static Color ReadColor(Dictionary<string, object> owner, string prefix, Color fallback)
        {
            return new Color(
                CznPlistReader.Float(owner, prefix + "Red", fallback.r),
                CznPlistReader.Float(owner, prefix + "Green", fallback.g),
                CznPlistReader.Float(owner, prefix + "Blue", fallback.b),
                CznPlistReader.Float(owner, prefix + "Alpha", fallback.a));
        }

        private static float Number(JObject owner, string key, float fallback = 0f)
        {
            return owner != null && owner.TryGetValue(key, out JToken token) ? TokenNumber(token, fallback) : fallback;
        }

        private static float TokenNumber(JToken token, float fallback = 0f)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return fallback;
            }
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                return token.Value<float>();
            }
            return float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : fallback;
        }

        private static string Text(JObject owner, string key)
        {
            JToken token = owner?[key];
            return token == null || token.Type == JTokenType.Null ? string.Empty : token.ToString();
        }

        private static bool Flag(JObject owner, string key)
        {
            JToken token = owner?[key];
            if (token == null || token.Type == JTokenType.Null)
            {
                return false;
            }
            if (token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }
            return bool.TryParse(token.ToString(), out bool parsed) && parsed;
        }

        private static float Seconds(JObject owner, string key)
        {
            return MillisecondsToSeconds(Number(owner, key));
        }

        private static float MillisecondsToSeconds(float value)
        {
            return value * 0.001f;
        }

        private static float PositiveScale(float value)
        {
            return value > 0f ? value : 1f;
        }

        private static bool IsStepped(JObject key)
        {
            return string.Equals(Text(key, "curve"), "stepped", StringComparison.OrdinalIgnoreCase);
        }

        private static float MaxTime(IReadOnlyList<CznVector2Key> keys)
        {
            return keys != null && keys.Count > 0 ? keys.Max(key => key.time) : 0f;
        }

        private static float MaxTime(IReadOnlyList<CznFloatKey> keys)
        {
            return keys != null && keys.Count > 0 ? keys.Max(key => key.time) : 0f;
        }

        private static GameObject CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child;
        }

        private static void EnsureInputExists()
        {
            string[] paths =
            {
                SrmdPath,
                BrmdPath,
                MainPrefabPath,
                BattleReadyPrefabPath,
                EffectConfigRoot,
                SpineRoot,
            };
            foreach (string path in paths)
            {
                if (!File.Exists(AbsolutePath(path)) && !Directory.Exists(AbsolutePath(path)))
                {
                    throw new FileNotFoundException("Required Fei input is missing", path);
                }
            }
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }
            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            string name = Path.GetFileName(assetFolder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException("Invalid asset folder: " + assetFolder);
            }
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string AbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ToAssetPath(string absolutePath)
        {
            string normalized = Path.GetFullPath(absolutePath).Replace('\\', '/');
            string projectRoot = (Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            return normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(projectRoot.Length + 1)
                : normalized;
        }

        private sealed class SkillSpec
        {
            public SkillSpec(string id, string displayName, params string[] commands)
            {
                Id = id;
                DisplayName = displayName;
                Commands = commands;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string[] Commands { get; }
        }

        private sealed class GeneratedSkill
        {
            private readonly HashSet<string> unresolvedSet = new HashSet<string>(StringComparer.Ordinal);

            public GeneratedSkill(string id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public float Duration { get; set; }
            public List<CznActorAnimationCue> ActorAnimations { get; } = new List<CznActorAnimationCue>();
            public List<CznSpineLayerCue> SpineLayers { get; } = new List<CznSpineLayerCue>();
            public List<CznParticleLayerCue> ParticleLayers { get; } = new List<CznParticleLayerCue>();
            public List<CznTransformCue> TransformCues { get; } = new List<CznTransformCue>();
            public List<CznCameraZoomCue> CameraZoomCues { get; } = new List<CznCameraZoomCue>();
            public List<CznSkillMarkerCue> Markers { get; } = new List<CznSkillMarkerCue>();
            public List<string> UnresolvedResources { get; } = new List<string>();

            public void AddUnresolved(string value)
            {
                if (!string.IsNullOrWhiteSpace(value) && unresolvedSet.Add(value))
                {
                    UnresolvedResources.Add(value);
                }
            }

            public void SortCues()
            {
                ActorAnimations.Sort((a, b) => a.startTime.CompareTo(b.startTime));
                SpineLayers.Sort((a, b) => a.startTime.CompareTo(b.startTime));
                ParticleLayers.Sort((a, b) => a.startTime.CompareTo(b.startTime));
                TransformCues.Sort((a, b) => a.startTime.CompareTo(b.startTime));
                CameraZoomCues.Sort((a, b) => a.startTime.CompareTo(b.startTime));
                Markers.Sort((a, b) => a.startTime.CompareTo(b.startTime));
                UnresolvedResources.Sort(StringComparer.Ordinal);
            }
        }
    }
}
