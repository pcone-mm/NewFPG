using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FPG.Demo.Skills;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal sealed class FpgSkillAuthoringChoice
    {
        public FpgSkillAuthoringChoice(string value, string label)
        {
            Value = value ?? string.Empty;
            Label = string.IsNullOrWhiteSpace(label) ? Value : label;
        }

        public string Value { get; }
        public string Label { get; }

        public override string ToString()
        {
            return Label;
        }
    }
    internal sealed class FpgSkillActionAuthoringOptions
    {
        private static readonly IReadOnlyList<FpgSkillTargetSource>
            NoTargetSourceChoices = Array.Empty<FpgSkillTargetSource>();

        public FpgSkillActionAuthoringOptions(
            bool isKnownAction,
            FpgSkillTargetSource defaultTargetSource,
            bool hasFixedTargetSource,
            IReadOnlyList<FpgSkillTargetSource> targetSourceChoices,
            bool supportsSocket,
            bool supportsTargetOffset)
        {
            IsKnownAction = isKnownAction;
            DefaultTargetSource = defaultTargetSource;
            HasFixedTargetSource = hasFixedTargetSource;
            TargetSourceChoices = targetSourceChoices
                ?? NoTargetSourceChoices;
            SupportsSocket = supportsSocket;
            SupportsTargetOffset = supportsTargetOffset;
        }

        public bool IsKnownAction { get; }
        public FpgSkillTargetSource DefaultTargetSource { get; }
        public bool HasFixedTargetSource { get; }
        public IReadOnlyList<FpgSkillTargetSource> TargetSourceChoices { get; }
        public bool SupportsTargetSourceSelection =>
            TargetSourceChoices.Count > 0;
        public bool SupportsSocket { get; }
        public bool SupportsTargetOffset { get; }
    }

    internal static class FpgSkillActionAuthoringRules
    {
        private static readonly FpgSkillTargetSource[] EnemyProjectileSources =
        {
            FpgSkillTargetSource.CurrentAim,
            FpgSkillTargetSource.CurrentTarget,
            FpgSkillTargetSource.SocketForward
        };

        private static readonly FpgSkillTargetSource[] EnemyTargetedSources =
        {
            FpgSkillTargetSource.CurrentAim,
            FpgSkillTargetSource.CurrentTarget
        };

        private static readonly FpgSkillActionAuthoringOptions Unknown =
            new FpgSkillActionAuthoringOptions(
                false,
                FpgSkillTargetSource.None,
                false,
                Array.Empty<FpgSkillTargetSource>(),
                false,
                false);

        private static readonly FpgSkillActionAuthoringOptions PlayerAttack =
            new FpgSkillActionAuthoringOptions(
                true,
                FpgSkillTargetSource.CurrentAim,
                true,
                Array.Empty<FpgSkillTargetSource>(),
                false,
                true);

        private static readonly FpgSkillActionAuthoringOptions PlayerReload =
            new FpgSkillActionAuthoringOptions(
                true,
                FpgSkillTargetSource.Self,
                true,
                Array.Empty<FpgSkillTargetSource>(),
                false,
                false);

        private static readonly FpgSkillActionAuthoringOptions EnemyProjectile =
            new FpgSkillActionAuthoringOptions(
                true,
                FpgSkillTargetSource.CurrentTarget,
                false,
                EnemyProjectileSources,
                true,
                true);

        private static readonly FpgSkillActionAuthoringOptions EnemyTargeted =
            new FpgSkillActionAuthoringOptions(
                true,
                FpgSkillTargetSource.CurrentTarget,
                true,
                Array.Empty<FpgSkillTargetSource>(),
                false,
                false);

        private static readonly FpgSkillActionAuthoringOptions
            EnemyActionTargeted =
                new FpgSkillActionAuthoringOptions(
                    true,
                    FpgSkillTargetSource.CurrentTarget,
                    false,
                    EnemyTargetedSources,
                    false,
                    false);

        public static FpgSkillActionAuthoringOptions Get(
            FpgSkillPreviewActionKind actionKind)
        {
            switch (actionKind)
            {
                case FpgSkillPreviewActionKind.PlayerPelletRay:
                case FpgSkillPreviewActionKind.PlayerAreaAtFirstSurface:
                    return PlayerAttack;

                case FpgSkillPreviewActionKind.PlayerReload:
                    return PlayerReload;

                case FpgSkillPreviewActionKind.EnemyProjectile:
                    return EnemyProjectile;

                case FpgSkillPreviewActionKind.EnemyTimedImpact:
                case FpgSkillPreviewActionKind.EnemySummon:
                    return EnemyTargeted;

                default:
                    return Unknown;
            }
        }

        public static FpgSkillActionAuthoringOptions Get(
            string actionKindName)
        {
            switch (actionKindName)
            {
                case "PelletRay":
                case "AreaAtFirstSurface":
                case "ProjectileAreaAtFirstSurface":
                    return PlayerAttack;

                case "ReloadCommit":
                    return PlayerReload;

                case "Projectile":
                    return EnemyProjectile;

                case "TimedImpact":
                case "Summon":
                    return EnemyTargeted;

                default:
                    return Unknown;
            }
        }

        public static FpgSkillActionAuthoringOptions Get(
            FpgSkillActionKind actionKind,
            bool enemy)
        {
            if (!enemy)
            {
                switch (actionKind)
                {
                    case FpgSkillActionKind.Attack:
                    case FpgSkillActionKind.LaunchProjectile:
                        return PlayerAttack;

                    case FpgSkillActionKind.CommitReload:
                        return PlayerReload;

                    default:
                        return Unknown;
                }
            }

            switch (actionKind)
            {
                case FpgSkillActionKind.Attack:
                case FpgSkillActionKind.SummonActors:
                    return EnemyActionTargeted;

                case FpgSkillActionKind.LaunchProjectile:
                    return EnemyProjectile;

                default:
                    return Unknown;
            }
        }
    }

    /// <summary>
    /// Editor-only typed choices. Stable references remain serialized internally,
    /// while authoring never requires entering those identifiers by hand.
    /// </summary>
    internal static class FpgSkillAuthoringChoices
    {
        private static readonly string[] FormalAnimationNames =
        {
            "idle",
            "attack",
            "attack_play1",
            "attack_play2",
            "normal_skill1",
            "normal_skill2",
            "u1_buff_play",
            "u4_attack_ready",
            "u4_attack_end",
            "defense_play",
            "die&broken"
        };

        private static readonly FpgSkillAuthoringChoice[] FormalWarningChoices =
        {
            new FpgSkillAuthoringChoice(
                "enemy-source-volley",
                "怪物 · 齐射预警"),
            new FpgSkillAuthoringChoice(
                "enemy-summon-warning",
                "怪物 · 召唤预警"),
            new FpgSkillAuthoringChoice(
                "enemy-source-fast",
                "怪物 · 快速预警"),
            new FpgSkillAuthoringChoice(
                "enemy-weakpoint-heavy",
                "怪物 · 弱点重击预警")
        };

        private static readonly FpgSkillAuthoringChoice[] FormalSocketChoices =
        {
            new FpgSkillAuthoringChoice("", "无 Socket"),
            new FpgSkillAuthoringChoice(
                "weapon.primary.muzzle",
                "主武器 · 枪口"),
            new FpgSkillAuthoringChoice(
                "weapon.secondary.muzzle",
                "副武器 · 枪口"),
            new FpgSkillAuthoringChoice(
                "attack.default.origin",
                "攻击 · 默认起点")
        };

        public static List<FpgSkillAuthoringChoice> BuildAnimationChoices(
            GameObject previewPrefab,
            IEnumerable<string> currentValues)
        {
            List<FpgSkillAuthoringChoice> choices =
                new List<FpgSkillAuthoringChoice>();
            HashSet<string> values = new HashSet<string>(
                StringComparer.Ordinal);
            AddChoice(choices, values, string.Empty, "未选择动画");

            bool hasPrefabAnimations = AddAnimationChoicesFromPrefab(
                previewPrefab,
                choices,
                values);
            if (!hasPrefabAnimations)
            {
                for (int index = 0;
                    index < FormalAnimationNames.Length;
                    index++)
                {
                    AddChoice(
                        choices,
                        values,
                        FormalAnimationNames[index],
                        FormalAnimationNames[index]);
                }
            }

            if (currentValues != null)
            {
                foreach (string current in currentValues)
                {
                    if (string.IsNullOrWhiteSpace(current)
                        || values.Contains(current))
                    {
                        continue;
                    }

                    AddChoice(
                        choices,
                        values,
                        current,
                        "当前动画 · "
                        + current
                        + "（Prefab 中不存在）");
                }
            }

            return choices;
        }

        public static List<FpgSkillAuthoringChoice> BuildWarningChoices(
            string currentValue)
        {
            return BuildFixedChoices(FormalWarningChoices, currentValue);
        }

        public static List<FpgSkillAuthoringChoice> BuildSocketChoices(
            GameObject previewPrefab,
            string currentValue)
        {
            List<FpgSkillAuthoringChoice> choices =
                new List<FpgSkillAuthoringChoice>();
            HashSet<string> values = new HashSet<string>(
                StringComparer.Ordinal);
            for (int index = 0; index < FormalSocketChoices.Length; index++)
            {
                AddChoice(
                    choices,
                    values,
                    FormalSocketChoices[index].Value,
                    FormalSocketChoices[index].Label);
            }

            Component registry = FindComponentByTypeName(
                previewPrefab,
                "FPG.Demo.Unity.D0ActorSocketRegistry");
            object bindings = GetPropertyValue(
                registry,
                "Bindings",
                "SocketBindings");
            IEnumerable enumerable = bindings as IEnumerable;
            if (enumerable != null)
            {
                foreach (object binding in enumerable)
                {
                    string socketId = ReadStringProperty(binding, "SocketId");
                    if (string.IsNullOrWhiteSpace(socketId))
                    {
                        continue;
                    }

                    AddChoice(
                        choices,
                        values,
                        socketId,
                        "当前 Prefab · " + socketId);
                }
            }

            AddCurrentChoice(choices, values, currentValue, "当前 Socket（未在选项中找到）");
            return choices;
        }

        public static List<FpgSkillAuthoringChoice> BuildGameplayEventChoices(
            IList<FpgSkillEventRecord> events,
            string currentValue)
        {
            List<FpgSkillAuthoringChoice> choices =
                new List<FpgSkillAuthoringChoice>();
            HashSet<string> values = new HashSet<string>(
                StringComparer.Ordinal);
            AddChoice(choices, values, string.Empty, "不绑定逻辑事件");
            if (events != null)
            {
                for (int index = 0; index < events.Count; index++)
                {
                    FpgSkillEventRecord record = events[index];
                    if (record == null
                        || record.Track != FpgSkillEventTrackKind.GameplayAction
                        || string.IsNullOrWhiteSpace(record.EventId))
                    {
                        continue;
                    }

                    string label = string.IsNullOrWhiteSpace(record.Name)
                        ? "逻辑事件 " + (index + 1)
                        : record.Name;
                    label += " · Tick " + record.Tick;
                    AddChoice(choices, values, record.EventId, label);
                }
            }

            AddCurrentChoice(
                choices,
                values,
                currentValue,
                "当前绑定（未找到）");
            return choices;
        }

        public static string FindLabel(
            IList<FpgSkillAuthoringChoice> choices,
            string value)
        {
            string normalized = value ?? string.Empty;
            if (choices != null)
            {
                for (int index = 0; index < choices.Count; index++)
                {
                    if (string.Equals(
                            choices[index].Value,
                            normalized,
                            StringComparison.Ordinal))
                    {
                        return choices[index].Label;
                    }
                }
            }

            return normalized;
        }

        private static List<FpgSkillAuthoringChoice> BuildFixedChoices(
            IList<FpgSkillAuthoringChoice> source,
            string currentValue)
        {
            List<FpgSkillAuthoringChoice> choices =
                new List<FpgSkillAuthoringChoice>();
            HashSet<string> values = new HashSet<string>(
                StringComparer.Ordinal);
            if (source != null)
            {
                for (int index = 0; index < source.Count; index++)
                {
                    AddChoice(
                        choices,
                        values,
                        source[index].Value,
                        source[index].Label);
                }
            }

            AddCurrentChoice(choices, values, currentValue, "当前值（未在正式选项中找到）");
            return choices;
        }

        private static void AddCurrentChoice(
            ICollection<FpgSkillAuthoringChoice> choices,
            ISet<string> values,
            string value,
            string label)
        {
            if (string.IsNullOrWhiteSpace(value) || values.Contains(value))
            {
                return;
            }

            AddChoice(choices, values, value, label);
        }

        private static void AddChoice(
            ICollection<FpgSkillAuthoringChoice> choices,
            ISet<string> values,
            string value,
            string label)
        {
            if (value == null || !values.Add(value))
            {
                return;
            }

            choices.Add(new FpgSkillAuthoringChoice(value, label));
        }

        private static bool AddAnimationChoicesFromPrefab(
            GameObject previewPrefab,
            ICollection<FpgSkillAuthoringChoice> choices,
            ISet<string> values)
        {
            Component spine = FindComponentByTypeName(
                previewPrefab,
                "Spine.Unity.SkeletonAnimation")
                ?? FindComponentByTypeName(
                    previewPrefab,
                    "Spine.Unity.SkeletonMecanim");
            object dataAsset = GetPropertyValue(spine, "SkeletonDataAsset");
            object skeletonData = InvokeMethod(
                dataAsset,
                "GetSkeletonData",
                new[] { typeof(bool) },
                new object[] { true });
            object animations = GetPropertyValue(skeletonData, "Animations");
            IEnumerable enumerable = animations as IEnumerable;
            if (enumerable == null)
            {
                return false;
            }

            bool foundAnimation = false;
            foreach (object animation in enumerable)
            {
                string name = ReadStringProperty(animation, "Name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                foundAnimation = true;
                AddChoice(choices, values, name, name);
            }

            return foundAnimation;
        }

        private static Component FindComponentByTypeName(
            GameObject root,
            string fullTypeName)
        {
            if (root == null || string.IsNullOrWhiteSpace(fullTypeName))
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
                        fullTypeName,
                        StringComparison.Ordinal))
                {
                    return component;
                }
            }

            return null;
        }

        private static object GetPropertyValue(object target, params string[] names)
        {
            if (target == null || names == null)
            {
                return null;
            }

            BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            Type type = target.GetType();
            for (int index = 0; index < names.Length; index++)
            {
                PropertyInfo property = type.GetProperty(names[index], flags);
                if (property != null)
                {
                    try
                    {
                        return property.GetValue(target, null);
                    }
                    catch
                    {
                        return null;
                    }
                }

                FieldInfo field = type.GetField(names[index], flags);
                if (field != null)
                {
                    try
                    {
                        return field.GetValue(target);
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            return null;
        }

        private static object InvokeMethod(
            object target,
            string methodName,
            Type[] parameterTypes,
            object[] arguments)
        {
            if (target == null)
            {
                return null;
            }

            BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                flags,
                null,
                parameterTypes,
                null);
            if (method == null)
            {
                return null;
            }

            try
            {
                return method.Invoke(target, arguments);
            }
            catch
            {
                return null;
            }
        }

        private static string ReadStringProperty(object target, string name)
        {
            object value = GetPropertyValue(target, name);
            return value as string ?? string.Empty;
        }
    }
}
