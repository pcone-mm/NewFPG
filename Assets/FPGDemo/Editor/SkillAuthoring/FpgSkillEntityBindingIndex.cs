using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal enum FpgSkillEntityKind
    {
        Character = 0,
        Enemy = 1
    }

    internal enum FpgSkillBindingState
    {
        Bound = 0,
        Conflict = 1,
        Unbound = 2
    }

    internal sealed class FpgSkillEntityRecord
    {
        public UnityEngine.Object Asset;
        public string Path;
        public string Guid;
        public string StableId;
        public string DisplayName;
        public FpgSkillEntityKind Kind;
        public GameObject PreviewPrefab;

        public string KindLabel => Kind == FpgSkillEntityKind.Character
            ? "角色"
            : "敌人";
    }

    internal sealed class FpgSkillEntityBindingRecord
    {
        private readonly List<string> slots = new List<string>();

        public FpgSkillEntityBindingRecord(FpgSkillEntityRecord entity)
        {
            Entity = entity;
        }

        public FpgSkillEntityRecord Entity { get; }
        public IReadOnlyList<string> Slots => slots;
        public int SortOrder { get; private set; } = int.MaxValue;

        public string Summary => Entity.DisplayName + " · "
            + string.Join(" / ", slots);

        public void AddSlot(string slot, int sortOrder)
        {
            if (!string.IsNullOrWhiteSpace(slot) && !slots.Contains(slot))
            {
                slots.Add(slot);
            }

            SortOrder = Math.Min(SortOrder, sortOrder);
        }
    }

    internal sealed class FpgSkillAssetRecord
    {
        private readonly List<FpgSkillEntityBindingRecord> bindings =
            new List<FpgSkillEntityBindingRecord>();

        public UnityEngine.Object Asset;
        public string Path;
        public string SkillId;
        public string DisplayName;
        public FpgSkillBindingState BindingState = FpgSkillBindingState.Unbound;
        public IReadOnlyList<FpgSkillEntityBindingRecord> Bindings => bindings;

        public FpgSkillEntityBindingRecord FindBinding(string entityGuid)
        {
            return bindings.FirstOrDefault(binding =>
                string.Equals(
                    binding.Entity.Guid,
                    entityGuid,
                    StringComparison.Ordinal));
        }

        public string BuildBindingSummary(FpgSkillEntityRecord context)
        {
            if (BindingState == FpgSkillBindingState.Unbound)
            {
                return "未绑定";
            }

            FpgSkillEntityBindingRecord contextual = context == null
                ? null
                : FindBinding(context.Guid);
            if (contextual != null)
            {
                return contextual.Summary;
            }

            return string.Join(
                "; ",
                bindings.Select(binding => binding.Summary));
        }

        public string BuildBadgeText(FpgSkillEntityRecord context)
        {
            if (BindingState == FpgSkillBindingState.Unbound)
            {
                return "未绑定";
            }

            if (BindingState == FpgSkillBindingState.Conflict)
            {
                return "冲突";
            }

            FpgSkillEntityBindingRecord binding = context == null
                ? bindings.FirstOrDefault()
                : FindBinding(context.Guid) ?? bindings.FirstOrDefault();
            return binding == null ? string.Empty : binding.Entity.KindLabel;
        }

        internal void ClearBindings()
        {
            bindings.Clear();
            BindingState = FpgSkillBindingState.Unbound;
        }

        internal FpgSkillEntityBindingRecord GetOrAddBinding(
            FpgSkillEntityRecord entity)
        {
            FpgSkillEntityBindingRecord binding = FindBinding(entity.Guid);
            if (binding != null)
            {
                return binding;
            }

            binding = new FpgSkillEntityBindingRecord(entity);
            bindings.Add(binding);
            return binding;
        }

        internal void FinalizeBindings()
        {
            bindings.Sort((left, right) =>
                FpgSkillEntityBindingIndex.CompareEntities(
                    left.Entity,
                    right.Entity));
            BindingState = bindings.Count == 0
                ? FpgSkillBindingState.Unbound
                : bindings.Count == 1
                    ? FpgSkillBindingState.Bound
                    : FpgSkillBindingState.Conflict;
        }
    }

    internal sealed class FpgSkillEntityFilterChoice
    {
        public FpgSkillEntityFilterChoice(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; }
        public string Label { get; }
    }

    internal sealed class FpgSkillBindingSnapshot
    {
        private readonly Dictionary<int, FpgSkillAssetRecord> skillsByInstanceId;
        private readonly Dictionary<int, FpgSkillEntityRecord> entitiesByInstanceId;

        public FpgSkillBindingSnapshot(
            List<FpgSkillAssetRecord> skills,
            List<FpgSkillEntityRecord> entities)
        {
            Skills = skills ?? new List<FpgSkillAssetRecord>();
            Entities = entities ?? new List<FpgSkillEntityRecord>();
            skillsByInstanceId = Skills
                .Where(record => record != null && record.Asset != null)
                .GroupBy(record => record.Asset.GetInstanceID())
                .ToDictionary(group => group.Key, group => group.First());
            entitiesByInstanceId = Entities
                .Where(record => record != null && record.Asset != null)
                .GroupBy(record => record.Asset.GetInstanceID())
                .ToDictionary(group => group.Key, group => group.First());
        }

        public IReadOnlyList<FpgSkillAssetRecord> Skills { get; }
        public IReadOnlyList<FpgSkillEntityRecord> Entities { get; }

        public FpgSkillAssetRecord FindSkill(UnityEngine.Object asset)
        {
            return asset != null
                && skillsByInstanceId.TryGetValue(
                    asset.GetInstanceID(),
                    out FpgSkillAssetRecord record)
                        ? record
                        : null;
        }

        public FpgSkillEntityRecord FindEntity(UnityEngine.Object asset)
        {
            return asset != null
                && entitiesByInstanceId.TryGetValue(
                    asset.GetInstanceID(),
                    out FpgSkillEntityRecord record)
                        ? record
                        : null;
        }

        public FpgSkillEntityRecord FindEntity(string entityGuid)
        {
            return Entities.FirstOrDefault(entity => string.Equals(
                entity.Guid,
                entityGuid,
                StringComparison.Ordinal));
        }

        public List<FpgSkillAssetRecord> FilterSkills(
            string filterKey,
            string search)
        {
            List<FpgSkillAssetRecord> result = new List<FpgSkillAssetRecord>();
            for (int index = 0; index < Skills.Count; index++)
            {
                FpgSkillAssetRecord record = Skills[index];
                if (FpgSkillEntityBindingIndex.MatchesFilter(record, filterKey)
                    && FpgSkillEntityBindingIndex.MatchesSearch(record, search))
                {
                    result.Add(record);
                }
            }

            return result;
        }

        public FpgSkillEntityRecord ResolveContext(
            FpgSkillAssetRecord skill,
            string preferredEntityGuid)
        {
            if (skill == null || skill.BindingState == FpgSkillBindingState.Unbound)
            {
                return null;
            }

            FpgSkillEntityBindingRecord preferred =
                skill.FindBinding(preferredEntityGuid);
            return preferred == null
                ? skill.Bindings[0].Entity
                : preferred.Entity;
        }

        public List<FpgSkillEntityFilterChoice> BuildFilterChoices()
        {
            List<FpgSkillEntityFilterChoice> choices =
                new List<FpgSkillEntityFilterChoice>
                {
                    new FpgSkillEntityFilterChoice(
                        FpgSkillEntityBindingIndex.AllFilterKey,
                        "全部技能")
                };
            HashSet<string> labels = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Entities.Count; index++)
            {
                FpgSkillEntityRecord entity = Entities[index];
                string label = entity.KindLabel + " · " + entity.DisplayName;
                if (!labels.Add(label))
                {
                    label += " (" + entity.StableId + ")";
                    labels.Add(label);
                }

                choices.Add(new FpgSkillEntityFilterChoice(entity.Guid, label));
            }

            choices.Add(new FpgSkillEntityFilterChoice(
                FpgSkillEntityBindingIndex.UnboundFilterKey,
                "未绑定"));
            choices.Add(new FpgSkillEntityFilterChoice(
                FpgSkillEntityBindingIndex.ConflictFilterKey,
                "绑定冲突"));
            return choices;
        }
    }

    internal static class FpgSkillEntityBindingIndex
    {
        internal const string AllFilterKey = "@all";
        internal const string UnboundFilterKey = "@unbound";
        internal const string ConflictFilterKey = "@conflict";
        internal const string NoEntityContextKey = "unbound";

        private const string CharacterTypeName =
            "FPG.Demo.Unity.D0CharacterDefinition";
        private const string EnemyTypeName =
            "FPG.Demo.Unity.FpgEnemyDefinition";

        private sealed class SkillReference
        {
            public UnityEngine.Object Skill;
            public string Slot;
            public int Order;
        }

        public static FpgSkillBindingSnapshot Build(
            IList<FpgSkillAssetRecord> skills)
        {
            return Build(skills, FindEntityDefinitions());
        }

        internal static FpgSkillBindingSnapshot Build(
            IList<FpgSkillAssetRecord> skills,
            IEnumerable<UnityEngine.Object> entityDefinitions)
        {
            List<FpgSkillAssetRecord> skillRecords = skills == null
                ? new List<FpgSkillAssetRecord>()
                : skills.Where(record => record != null).ToList();
            Dictionary<int, FpgSkillAssetRecord> skillByInstanceId =
                new Dictionary<int, FpgSkillAssetRecord>();
            for (int index = 0; index < skillRecords.Count; index++)
            {
                FpgSkillAssetRecord skill = skillRecords[index];
                skill.ClearBindings();
                if (skill.Asset != null)
                {
                    skillByInstanceId[skill.Asset.GetInstanceID()] = skill;
                }
            }

            List<FpgSkillEntityRecord> entities =
                new List<FpgSkillEntityRecord>();
            IEnumerable<UnityEngine.Object> definitions = entityDefinitions
                ?? Enumerable.Empty<UnityEngine.Object>();
            foreach (UnityEngine.Object definition in definitions)
            {
                if (!TryReadEntity(
                        definition,
                        out FpgSkillEntityRecord entity,
                        out List<SkillReference> references))
                {
                    continue;
                }

                entities.Add(entity);
                for (int referenceIndex = 0;
                    referenceIndex < references.Count;
                    referenceIndex++)
                {
                    SkillReference reference = references[referenceIndex];
                    if (reference.Skill == null
                        || !skillByInstanceId.TryGetValue(
                            reference.Skill.GetInstanceID(),
                            out FpgSkillAssetRecord skill))
                    {
                        continue;
                    }

                    skill.GetOrAddBinding(entity).AddSlot(
                        reference.Slot, reference.Order);
                }
            }

            entities.Sort(CompareEntities);
            for (int index = 0; index < skillRecords.Count; index++)
            {
                skillRecords[index].FinalizeBindings();
            }

            skillRecords.Sort(CompareSkills);
            return new FpgSkillBindingSnapshot(skillRecords, entities);
        }

        public static bool IsEntityDefinition(UnityEngine.Object asset)
        {
            string typeName = asset == null ? null : asset.GetType().FullName;
            return string.Equals(typeName, CharacterTypeName, StringComparison.Ordinal)
                || string.Equals(typeName, EnemyTypeName, StringComparison.Ordinal);
        }

        public static bool MatchesFilter(
            FpgSkillAssetRecord skill,
            string filterKey)
        {
            if (skill == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(filterKey)
                || string.Equals(filterKey, AllFilterKey, StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(filterKey, UnboundFilterKey, StringComparison.Ordinal))
            {
                return skill.BindingState == FpgSkillBindingState.Unbound;
            }

            if (string.Equals(filterKey, ConflictFilterKey, StringComparison.Ordinal))
            {
                return skill.BindingState == FpgSkillBindingState.Conflict;
            }

            return skill.FindBinding(filterKey) != null;
        }

        public static bool MatchesSearch(
            FpgSkillAssetRecord skill,
            string search)
        {
            if (skill == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            if (Contains(skill.DisplayName, search)
                || Contains(skill.SkillId, search)
                || Contains(skill.Path, search))
            {
                return true;
            }

            for (int bindingIndex = 0;
                bindingIndex < skill.Bindings.Count;
                bindingIndex++)
            {
                FpgSkillEntityBindingRecord binding =
                    skill.Bindings[bindingIndex];
                if (Contains(binding.Entity.DisplayName, search)
                    || Contains(binding.Entity.StableId, search)
                    || Contains(binding.Entity.Path, search))
                {
                    return true;
                }

                for (int slotIndex = 0;
                    slotIndex < binding.Slots.Count;
                    slotIndex++)
                {
                    if (Contains(binding.Slots[slotIndex], search))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void AppendBindingValidation(
            FpgSkillAssetRecord skill,
            ICollection<FpgSkillValidationItem> validation)
        {
            if (skill == null || validation == null)
            {
                return;
            }

            if (skill.BindingState == FpgSkillBindingState.Unbound)
            {
                validation.Add(new FpgSkillValidationItem
                {
                    Severity = FpgSkillIssueSeverity.Error,
                    Message = "技能未被任何实体定义引用。请从角色武器或敌人 attackPatterns 完成绑定。"
                });
                return;
            }

            if (skill.BindingState != FpgSkillBindingState.Conflict)
            {
                return;
            }

            string owners = string.Join(
                "、",
                skill.Bindings.Select(binding =>
                    binding.Entity.DisplayName + " ("
                    + binding.Entity.StableId + ")"));
            validation.Add(new FpgSkillValidationItem
            {
                Severity = FpgSkillIssueSeverity.Error,
                Message = "跨实体绑定冲突：该技能同时被 " + owners
                    + " 引用。技能必须唯一归属一个实体定义。"
            });
        }

        internal static int CompareEntities(
            FpgSkillEntityRecord left,
            FpgSkillEntityRecord right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int comparison = left.Kind.CompareTo(right.Kind);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.OrdinalIgnoreCase);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(
                left.StableId,
                right.StableId,
                StringComparison.OrdinalIgnoreCase);
            return comparison != 0
                ? comparison
                : string.Compare(left.Guid, right.Guid, StringComparison.Ordinal);
        }

        private static int CompareSkills(
            FpgSkillAssetRecord left,
            FpgSkillAssetRecord right)
        {
            FpgSkillEntityRecord leftEntity = left.Bindings.Count == 0
                ? null
                : left.Bindings[0].Entity;
            FpgSkillEntityRecord rightEntity = right.Bindings.Count == 0
                ? null
                : right.Bindings[0].Entity;
            int comparison = CompareEntities(leftEntity, rightEntity);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = GetPrimaryBindingOrder(left).CompareTo(
                GetPrimaryBindingOrder(right));
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.OrdinalIgnoreCase);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(
                left.SkillId,
                right.SkillId,
                StringComparison.OrdinalIgnoreCase);
            return comparison != 0
                ? comparison
                : string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetPrimaryBindingOrder(FpgSkillAssetRecord skill)
        {
            return skill != null
                && skill.Bindings.Count > 0
                    ? skill.Bindings[0].SortOrder
                    : int.MaxValue;
        }

        private static List<UnityEngine.Object> FindEntityDefinitions()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:ScriptableObject",
                new[] { "Assets/FPGDemo" });
            List<string> paths = new List<string>();
            HashSet<string> seenPaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (!string.IsNullOrWhiteSpace(path) && seenPaths.Add(path))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            List<UnityEngine.Object> definitions =
                new List<UnityEngine.Object>();
            for (int index = 0; index < paths.Count; index++)
            {
                UnityEngine.Object asset =
                    AssetDatabase.LoadMainAssetAtPath(paths[index]);
                if (IsEntityDefinition(asset))
                {
                    definitions.Add(asset);
                }
            }

            return definitions;
        }

        private static bool TryReadEntity(
            UnityEngine.Object asset,
            out FpgSkillEntityRecord entity,
            out List<SkillReference> references)
        {
            entity = null;
            references = new List<SkillReference>();
            if (!IsEntityDefinition(asset))
            {
                return false;
            }

            try
            {
                SerializedObject serialized = new SerializedObject(asset);
                string typeName = asset.GetType().FullName;
                FpgSkillEntityKind kind = string.Equals(
                    typeName,
                    CharacterTypeName,
                    StringComparison.Ordinal)
                        ? FpgSkillEntityKind.Character
                        : FpgSkillEntityKind.Enemy;
                string idProperty = kind == FpgSkillEntityKind.Character
                    ? "characterId"
                    : "enemyDefinitionId";
                string prefabProperty = kind == FpgSkillEntityKind.Character
                    ? "entityPrefab"
                    : "entityViewPrefab";
                string path = AssetDatabase.GetAssetPath(asset);
                string guid = string.IsNullOrWhiteSpace(path)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(path);
                string stableId = ReadString(
                    serialized.FindProperty(idProperty),
                    asset.name);
                if (string.IsNullOrWhiteSpace(guid))
                {
                    guid = "instance:" + typeName + ":" + stableId + ":"
                        + asset.GetInstanceID();
                }

                entity = new FpgSkillEntityRecord
                {
                    Asset = asset,
                    Path = path,
                    Guid = guid,
                    StableId = stableId,
                    DisplayName = ReadString(
                        serialized.FindProperty("displayName"),
                        asset.name),
                    Kind = kind,
                    PreviewPrefab = ResolvePrefab(
                        serialized.FindProperty(prefabProperty))
                };

                if (kind == FpgSkillEntityKind.Character)
                {
                    AppendCharacterReferences(serialized, references);
                }
                else
                {
                    AppendEnemyReferences(serialized, references);
                }

                return true;
            }
            catch (ArgumentException)
            {
                entity = null;
                references.Clear();
                return false;
            }
        }

        private static void AppendCharacterReferences(
            SerializedObject character,
            ICollection<SkillReference> references)
        {
            SerializedProperty weaponProperty = character.FindProperty("weapon");
            UnityEngine.Object weapon = weaponProperty == null
                ? null
                : weaponProperty.objectReferenceValue;
            if (weapon == null)
            {
                return;
            }

            SerializedObject serializedWeapon = new SerializedObject(weapon);
            AppendReference(
                serializedWeapon.FindProperty("primarySkill"),
                "主射",
                0,
                references);
            AppendReference(
                serializedWeapon.FindProperty("immediateSecondarySkill"),
                "瞬发副射",
                1,
                references);
            AppendReference(
                serializedWeapon.FindProperty("chargeSecondarySkill"),
                "蓄力副射",
                2,
                references);
            AppendReference(
                serializedWeapon.FindProperty("reloadSkill"),
                "换弹",
                3,
                references);
        }

        private static void AppendEnemyReferences(
            SerializedObject enemy,
            ICollection<SkillReference> references)
        {
            SerializedProperty attacks = enemy.FindProperty("attackPatterns");
            if (attacks == null || !attacks.isArray)
            {
                return;
            }

            for (int index = 0; index < attacks.arraySize; index++)
            {
                AppendReference(
                    attacks.GetArrayElementAtIndex(index),
                    "攻击 " + (index + 1),
                    index,
                    references);
            }
        }

        private static void AppendReference(
            SerializedProperty property,
            string slot,
            int order,
            ICollection<SkillReference> references)
        {
            UnityEngine.Object skill = property == null
                ? null
                : property.objectReferenceValue;
            if (skill != null)
            {
                references.Add(new SkillReference
                {
                    Skill = skill,
                    Slot = slot,
                    Order = order
                });
            }
        }

        private static GameObject ResolvePrefab(SerializedProperty property)
        {
            UnityEngine.Object reference = property == null
                ? null
                : property.objectReferenceValue;
            if (reference is GameObject gameObject)
            {
                return gameObject;
            }

            return reference is Component component ? component.gameObject : null;
        }

        private static string ReadString(
            SerializedProperty property,
            string fallback)
        {
            return property != null
                && property.propertyType == SerializedPropertyType.String
                && !string.IsNullOrWhiteSpace(property.stringValue)
                    ? property.stringValue.Trim()
                    : fallback ?? string.Empty;
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
