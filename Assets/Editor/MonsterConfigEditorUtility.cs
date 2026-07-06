using System.IO;
using System.Reflection;
using BehaviorDesigner.Editor;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using NewFPG.Combat;
using NewFPG.Monsters.BehaviorDesigner;
using NewFPG.Monsters;
using UnityEditor;
using UnityEngine;
using AIPath = Pathfinding.AIPath;
using Seeker = Pathfinding.Seeker;
using BehaviorTask = BehaviorDesigner.Runtime.Tasks.Task;

namespace NewFPG.EditorTools
{
    public static class MonsterConfigEditorUtility
    {
        [MenuItem("NewFPG/Monsters/Refresh Prefab JSON Bindings")]
        public static void RefreshPrefabJsonBindings()
        {
            MonsterCatalog catalog = LoadCatalog();
            if (catalog.monsters == null)
            {
                return;
            }

            int refreshedCount = 0;
            for (int i = 0; i < catalog.monsters.Count; i++)
            {
                MonsterDefinition definition = catalog.monsters[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.prefabPath))
                {
                    continue;
                }

                if (RefreshPrefabJsonBinding(definition))
                {
                    refreshedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MonsterConfigEditorUtility] Refreshed {refreshedCount} monster prefab JSON binding(s).");
        }

        [MenuItem("NewFPG/Monsters/Rebuild Fish Behavior Tree")]
        public static void RebuildFishBehaviorTree()
        {
            ExternalBehaviorTree externalTree = LoadOrCreateExternalBehaviorTree(MonsterAiDefinition.DefaultFishBehaviorTreePath);
            if (externalTree == null)
            {
                Debug.LogError("[MonsterConfigEditorUtility] Could not load or create BT_Fish behavior tree.");
                return;
            }

            AssignDefaultFishBehaviorTree(externalTree);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MonsterConfigEditorUtility] Rebuilt BT_Fish behavior tree.");
        }

        [MenuItem("NewFPG/Monsters/JSON -> SO Authoring")]
        public static void ImportJsonToAuthoring()
        {
            MonsterCatalogAuthoring authoring = LoadOrCreateAuthoringAsset();
            string absolutePath = Path.GetFullPath(MonsterCatalog.DefaultCatalogPath);
            if (!File.Exists(absolutePath))
            {
                Debug.LogWarning($"[MonsterConfigEditorUtility] Monster catalog JSON not found at {MonsterCatalog.DefaultCatalogPath}.");
                return;
            }

            authoring.ImportFromJson(File.ReadAllText(absolutePath));
            EditorUtility.SetDirty(authoring);
            AssetDatabase.SaveAssets();
            Selection.activeObject = authoring;
            Debug.Log($"[MonsterConfigEditorUtility] Imported {MonsterCatalog.DefaultCatalogPath} into {MonsterCatalog.DefaultAuthoringPath}.");
        }

        [MenuItem("NewFPG/Monsters/SO Authoring -> JSON")]
        public static void ExportAuthoringToJson()
        {
            MonsterCatalogAuthoring authoring = LoadOrCreateAuthoringAsset();
            string absolutePath = Path.GetFullPath(MonsterCatalog.DefaultCatalogPath);
            File.WriteAllText(absolutePath, authoring.ExportToJson());
            AssetDatabase.ImportAsset(MonsterCatalog.DefaultCatalogPath);
            Debug.Log($"[MonsterConfigEditorUtility] Exported {MonsterCatalog.DefaultAuthoringPath} into {MonsterCatalog.DefaultCatalogPath}.");
        }

        [MenuItem("NewFPG/Monsters/Validate Monster JSON")]
        public static void ValidateMonsterJson()
        {
            MonsterCatalog catalog = LoadCatalog();
            catalog.Normalize();
            int monsterCount = catalog.monsters != null ? catalog.monsters.Count : 0;
            Debug.Log($"[MonsterConfigEditorUtility] Monster JSON valid. Monsters: {monsterCount}.");
        }

        public static MonsterCatalog LoadJsonCatalog()
        {
            return LoadCatalog();
        }

        public static MonsterDefinition LoadJsonDefinition(string monsterId)
        {
            MonsterCatalog catalog = LoadCatalog();
            return catalog.FindMonster(monsterId);
        }

        private static MonsterCatalog LoadCatalog()
        {
            string absolutePath = Path.GetFullPath(MonsterCatalog.DefaultCatalogPath);
            if (!File.Exists(absolutePath))
            {
                return new MonsterCatalog();
            }

            return MonsterCatalog.FromJson(File.ReadAllText(absolutePath));
        }

        private static bool RefreshPrefabJsonBinding(MonsterDefinition definition)
        {
            definition.Normalize();
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(definition.prefabPath);
            try
            {
                EnsureRequiredComponents(prefabRoot);
                EnsureBinding(prefabRoot, definition);
                EnsureBehaviorTree(prefabRoot, definition);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, definition.prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void EnsureBinding(GameObject prefabRoot, MonsterDefinition definition)
        {
            MonsterConfigBinding binding = prefabRoot.GetComponent<MonsterConfigBinding>();
            if (binding == null)
            {
                binding = prefabRoot.AddComponent<MonsterConfigBinding>();
            }

            binding.MonsterId = definition.monsterId;
            binding.ApplyOnAwake = true;
            binding.CatalogJson = AssetDatabase.LoadAssetAtPath<TextAsset>(MonsterCatalog.DefaultCatalogPath);
            EditorUtility.SetDirty(binding);
        }

        private static void EnsureBehaviorTree(GameObject prefabRoot, MonsterDefinition definition)
        {
            if (definition.ai == null || !definition.ai.enabled)
            {
                return;
            }

            BehaviorTree behaviorTree = EnsureComponent<BehaviorTree>(prefabRoot);
            ExternalBehaviorTree externalTree = LoadOrCreateExternalBehaviorTree(definition.ai.behaviorTreePath);
            if (externalTree == null)
            {
                Debug.LogWarning($"[MonsterConfigEditorUtility] Behavior tree asset missing: {definition.ai.behaviorTreePath}");
                return;
            }

            bool assigned = TryAssignExternalBehaviorTree(behaviorTree, externalTree);
            if (!assigned)
            {
                Debug.LogWarning(
                    $"[MonsterConfigEditorUtility] Could not assign {definition.ai.behaviorTreePath} to BehaviorTree on {prefabRoot.name}.",
                    prefabRoot);
            }

            EditorUtility.SetDirty(behaviorTree);
        }

        private static void EnsureRequiredComponents(GameObject prefabRoot)
        {
            RemoveComponentIfExists<MonsterAttackController>(prefabRoot);
            RemoveComponentIfExists<MonsterBrain>(prefabRoot);
            RemoveComponentIfExists<MonsterSkillController>(prefabRoot);
            RemoveComponentIfExists<MonsterMechanicRunner>(prefabRoot);
            RemoveComponentIfExists<MonsterState>(prefabRoot);
            RemoveComponentIfExists<Rigidbody>(prefabRoot);
            RemoveComponentIfExistsByName(prefabRoot, "UnityEngine.AI.NavMeshAgent");
            RemoveComponentIfExistsByName(prefabRoot, "NewFPG.Monsters.FishMonsterController");
            RemoveComponentIfExistsByName(prefabRoot, "NewFPG.Monsters.MonsterRuntimeController");
            RemoveMissingMonoBehaviours(prefabRoot);

            EnsureComponent<Seeker>(prefabRoot);
            EnsureComponent<AIPath>(prefabRoot);
            BoxCollider boxCollider = EnsureComponent<BoxCollider>(prefabRoot);
            // 鱼怪外形碰撞用于受击/技能检测，不参与移动阻挡；A* 通行尺寸由 AIPath.radius 控制。
            boxCollider.isTrigger = true;
            EditorUtility.SetDirty(boxCollider);

            if (prefabRoot.GetComponent<CombatVitals>() == null)
            {
                prefabRoot.AddComponent<CombatVitals>();
            }

            Animator animator = prefabRoot.GetComponent<Animator>();
            SpriteRenderer spriteRenderer = prefabRoot.GetComponent<SpriteRenderer>();

            CombatVitals vitals = prefabRoot.GetComponent<CombatVitals>();
            SerializedObject serializedVitals = new SerializedObject(vitals);
            serializedVitals.FindProperty("animator").objectReferenceValue = animator;
            serializedVitals.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
            serializedVitals.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ExternalBehaviorTree LoadOrCreateExternalBehaviorTree(string assetPath)
        {
            string resolvedPath = string.IsNullOrWhiteSpace(assetPath)
                ? MonsterAiDefinition.DefaultFishBehaviorTreePath
                : assetPath.Trim().Replace('\\', '/');

            ExternalBehaviorTree existing = AssetDatabase.LoadAssetAtPath<ExternalBehaviorTree>(resolvedPath);
            if (existing != null)
            {
                EnsureDefaultFishBehaviorTree(existing, resolvedPath);
                return existing;
            }

            EnsureFolder(Path.GetDirectoryName(resolvedPath).Replace('\\', '/'));
            ExternalBehaviorTree created = ScriptableObject.CreateInstance<ExternalBehaviorTree>();
            created.name = Path.GetFileNameWithoutExtension(resolvedPath);
            AssetDatabase.CreateAsset(created, resolvedPath);
            EnsureDefaultFishBehaviorTree(created, resolvedPath);
            EditorUtility.SetDirty(created);
            return created;
        }

        private static void EnsureDefaultFishBehaviorTree(ExternalBehaviorTree externalTree, string assetPath)
        {
            if (externalTree == null || !IsDefaultFishBehaviorTreePath(assetPath) || HasBehaviorTreeData(externalTree))
            {
                return;
            }

            AssignDefaultFishBehaviorTree(externalTree);
        }

        private static void AssignDefaultFishBehaviorTree(ExternalBehaviorTree externalTree)
        {
            BehaviorSource behaviorSource = BuildDefaultFishBehaviorSource(externalTree);
            JSONSerialization.Save(behaviorSource);
            externalTree.SetBehaviorSource(behaviorSource);
            EditorUtility.SetDirty(externalTree);
        }

        private static bool IsDefaultFishBehaviorTreePath(string assetPath)
        {
            string resolvedPath = string.IsNullOrWhiteSpace(assetPath)
                ? string.Empty
                : assetPath.Trim().Replace('\\', '/');
            return string.Equals(
                resolvedPath,
                MonsterAiDefinition.DefaultFishBehaviorTreePath,
                System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasBehaviorTreeData(ExternalBehaviorTree externalTree)
        {
            BehaviorSource behaviorSource = externalTree.GetBehaviorSource();
            if (behaviorSource == null)
            {
                return false;
            }

            if (behaviorSource.EntryTask != null || behaviorSource.RootTask != null)
            {
                return true;
            }

            if (behaviorSource.DetachedTasks != null && behaviorSource.DetachedTasks.Count > 0)
            {
                return true;
            }

            TaskSerializationData taskData = behaviorSource.TaskData;
            return taskData != null
                && (!string.IsNullOrWhiteSpace(taskData.JSONSerialization)
                    || taskData.types != null && taskData.types.Count > 0);
        }

        private static BehaviorSource BuildDefaultFishBehaviorSource(ExternalBehaviorTree owner)
        {
            int id = 0;
            EntryTask entry = CreateTask<EntryTask>(
                ref id,
                "入口",
                "鱼怪行为树入口：持续重复执行目标查找、技能释放、追踪和巡逻分支。",
                new Vector2(0f, 0f));

            Repeater loop = CreateTask<Repeater>(
                ref id,
                "循环执行",
                "持续重复鱼怪决策；每轮都会重新尝试查找 Player。",
                new Vector2(0f, 120f));
            loop.repeatForever = true;
            loop.endOnFailure = false;

            Selector rootSelector = CreateTask<Selector>(
                ref id,
                "鱼怪主选择",
                "有目标时进入战斗流程；找不到目标时启动出生点巡逻。",
                new Vector2(0f, 260f));

            Sequence combatSequence = CreateTask<Sequence>(
                ref id,
                "找目标并战斗",
                "按 Player Tag 找目标，找到后优先尝试近身咬击，不满足条件则追踪。",
                new Vector2(-360f, 420f));

            MonsterFindTargetByTag findPlayer = CreateTask<MonsterFindTargetByTag>(
                ref id,
                "查找 Player",
                "按 Unity Tag=Player 查找最近有效目标，并写入 MonsterConfigBinding。",
                new Vector2(-560f, 580f));
            findPlayer.targetTag = MonsterBehaviorTaskText.DefaultTargetTag;
            findPlayer.clearWhenMissing = true;

            Selector combatSelector = CreateTask<Selector>(
                ref id,
                "技能或追踪",
                "技能可用且距离/视线满足时释放近身咬击，否则追踪目标。",
                new Vector2(-160f, 580f));
            SetAbortType(combatSelector, AbortType.LowerPriority);

            Sequence biteSequence = CreateTask<Sequence>(
                ref id,
                "近身咬击流程",
                "先尝试移动到 BattleArenaZoneMap 的 near/mid 区域组，再确认施放距离和视线，释放后尝试退到 far 区域组。",
                new Vector2(-420f, 760f));

            MonsterSkillUsable skillUsable = CreateSkillTask<MonsterSkillUsable>(
                ref id,
                "近身咬击可用",
                "检查 melee_bite 是否存在、冷却完成，并且当前没有正在施法。",
                new Vector2(-980f, 940f));

            ReturnSuccess approachOptional = CreateTask<ReturnSuccess>(
                ref id,
                "尝试靠近战斗区域",
                "尝试移动到 BattleArenaZoneMap 的 near/mid 区域组；采样失败也继续让距离/视线节点做最终判断。",
                new Vector2(-700f, 940f));

            MonsterMoveToBattleZoneGroup approach = CreateTask<MonsterMoveToBattleZoneGroup>(
                ref id,
                "移动到 near/mid 区域组",
                "按怪物移动配置把 near/mid 展开为 BattleArenaZoneMap 前排/中排格子，并采样可达站位。",
                new Vector2(-700f, 1120f));
            approach.rows = MonsterBehaviorTaskText.DefaultApproachRows;
            approach.columns = MonsterBehaviorTaskText.DefaultColumns;

            MonsterTargetInSkillRange inRange = CreateSkillTask<MonsterTargetInSkillRange>(
                ref id,
                "确认施放距离",
                "使用技能配置 castRange 判断目标是否已经进入近身咬击范围。",
                new Vector2(-420f, 940f));

            MonsterTargetLineOfSight hasLineOfSight = CreateSkillTask<MonsterTargetLineOfSight>(
                ref id,
                "确认视线",
                "使用技能配置 requireLineOfSight、lineOfSightMask 和 lineOfSightHeightOffset 判断遮挡。",
                new Vector2(-140f, 940f));

            MonsterUseSkill useBite = CreateSkillTask<MonsterUseSkill>(
                ref id,
                "释放近身咬击",
                "释放 melee_bite，并等待施法流程结束。",
                new Vector2(140f, 940f));
            useBite.checkReleaseConditions = true;

            ReturnSuccess retreatOptional = CreateTask<ReturnSuccess>(
                ref id,
                "尝试拉远",
                "近身咬击结束后尝试移动到 BattleArenaZoneMap 的 far 区域组；失败也不阻断主循环。",
                new Vector2(420f, 940f));

            MonsterMoveToBattleZoneGroup retreat = CreateTask<MonsterMoveToBattleZoneGroup>(
                ref id,
                "移动到 far 区域组",
                "按怪物移动配置把 far 展开为 BattleArenaZoneMap 后排格子，并采样可达站位。",
                new Vector2(420f, 1120f));
            retreat.rows = MonsterBehaviorTaskText.DefaultRetreatRows;
            retreat.columns = MonsterBehaviorTaskText.DefaultColumns;

            MonsterChaseTarget chaseTarget = CreateTask<MonsterChaseTarget>(
                ref id,
                "追踪目标",
                "技能不可用、距离不足或视线不通时，下发追踪当前目标的移动指令；默认不等待到达，下一轮继续判断技能条件。",
                new Vector2(160f, 760f));

            MonsterPatrol patrol = CreateTask<MonsterPatrol>(
                ref id,
                "出生点巡逻",
                "找不到 Player 时清空目标，并按出生点周围巡逻参数移动。",
                new Vector2(360f, 420f));

            loop.AddChild(rootSelector, 0);
            rootSelector.AddChild(combatSequence, 0);
            rootSelector.AddChild(patrol, 1);
            combatSequence.AddChild(findPlayer, 0);
            combatSequence.AddChild(combatSelector, 1);
            combatSelector.AddChild(biteSequence, 0);
            combatSelector.AddChild(chaseTarget, 1);
            biteSequence.AddChild(skillUsable, 0);
            biteSequence.AddChild(approachOptional, 1);
            approachOptional.AddChild(approach, 0);
            biteSequence.AddChild(inRange, 2);
            biteSequence.AddChild(hasLineOfSight, 3);
            biteSequence.AddChild(useBite, 4);
            biteSequence.AddChild(retreatOptional, 5);
            retreatOptional.AddChild(retreat, 0);

            BehaviorSource behaviorSource = new BehaviorSource
            {
                Owner = owner,
                behaviorName = "BT_Fish",
                behaviorDescription = "鱼怪默认行为树：找 Player；技能可用时移动到 BattleArenaZoneMap 区域组、确认距离和视线后释放近身咬击，结束后尝试拉远；否则追踪；无目标时巡逻。",
            };
            behaviorSource.Save(entry, loop, new System.Collections.Generic.List<BehaviorTask>());
            return behaviorSource;
        }

        private static T CreateSkillTask<T>(
            ref int id,
            string friendlyName,
            string comment,
            Vector2 offset)
            where T : BehaviorTask, new()
        {
            T task = CreateTask<T>(ref id, friendlyName, comment, offset);
            FieldInfo skillField = typeof(T).GetField("skillId", BindingFlags.Instance | BindingFlags.Public);
            if (skillField != null && typeof(SharedString).IsAssignableFrom(skillField.FieldType))
            {
                skillField.SetValue(task, new SharedString { Value = MonsterBehaviorTaskText.DefaultSkillId });
            }

            return task;
        }

        private static void SetAbortType(Composite composite, AbortType abortType)
        {
            if (composite == null)
            {
                return;
            }

            FieldInfo abortTypeField = typeof(Composite).GetField(
                "abortType",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            abortTypeField?.SetValue(composite, abortType);
        }

        private static T CreateTask<T>(
            ref int id,
            string friendlyName,
            string comment,
            Vector2 offset)
            where T : BehaviorTask, new()
        {
            T task = new T
            {
                ID = id++,
                FriendlyName = friendlyName,
                NodeData = new NodeData
                {
                    FriendlyName = friendlyName,
                    Comment = comment,
                    Offset = offset,
                },
            };
            return task;
        }

        private static bool TryAssignExternalBehaviorTree(BehaviorTree behaviorTree, ExternalBehaviorTree externalTree)
        {
            if (behaviorTree == null || externalTree == null)
            {
                return false;
            }

            bool assigned = TryAssignExternalBehaviorTreeByReflection(behaviorTree, externalTree);
            assigned |= TryAssignExternalBehaviorTreeBySerializedObject(behaviorTree, externalTree);
            return assigned;
        }

        private static bool TryAssignExternalBehaviorTreeByReflection(BehaviorTree behaviorTree, ExternalBehaviorTree externalTree)
        {
            bool assigned = false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (System.Type type = behaviorTree.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo[] fields = type.GetFields(flags);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (!CanHoldExternalBehavior(field.FieldType))
                    {
                        continue;
                    }

                    field.SetValue(behaviorTree, externalTree);
                    assigned = true;
                }

                PropertyInfo[] properties = type.GetProperties(flags);
                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    if (!property.CanWrite || !CanHoldExternalBehavior(property.PropertyType))
                    {
                        continue;
                    }

                    property.SetValue(behaviorTree, externalTree, null);
                    assigned = true;
                }
            }

            return assigned;
        }

        private static bool TryAssignExternalBehaviorTreeBySerializedObject(
            BehaviorTree behaviorTree,
            ExternalBehaviorTree externalTree)
        {
            bool assigned = false;
            SerializedObject serializedTree = new SerializedObject(behaviorTree);
            SerializedProperty property = serializedTree.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                string propertyPath = property.propertyPath.ToLowerInvariant();
                string propertyType = property.type.ToLowerInvariant();
                bool looksLikeExternalBehavior = propertyPath.Contains("external")
                    || propertyType.Contains("externalbehavior")
                    || property.objectReferenceValue is ExternalBehaviorTree;
                if (!looksLikeExternalBehavior)
                {
                    continue;
                }

                property.objectReferenceValue = externalTree;
                assigned = true;
            }

            if (assigned)
            {
                serializedTree.ApplyModifiedPropertiesWithoutUndo();
            }

            return assigned;
        }

        private static bool CanHoldExternalBehavior(System.Type type)
        {
            return type != null
                && typeof(ExternalBehavior).IsAssignableFrom(type);
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void RemoveComponentIfExists<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null)
            {
                Object.DestroyImmediate(component, true);
            }
        }

        private static void RemoveComponentIfExistsByName(GameObject target, string fullTypeName)
        {
            Component[] components = target.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null && component.GetType().FullName == fullTypeName)
                {
                    Object.DestroyImmediate(component, true);
                }
            }
        }

        private static void RemoveMissingMonoBehaviours(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
        }

        private static MonsterCatalogAuthoring LoadOrCreateAuthoringAsset()
        {
            MonsterCatalogAuthoring authoring = AssetDatabase.LoadAssetAtPath<MonsterCatalogAuthoring>(MonsterCatalog.DefaultAuthoringPath);
            if (authoring != null)
            {
                return authoring;
            }

            EnsureFolder(Path.GetDirectoryName(MonsterCatalog.DefaultAuthoringPath).Replace('\\', '/'));
            authoring = ScriptableObject.CreateInstance<MonsterCatalogAuthoring>();
            AssetDatabase.CreateAsset(authoring, MonsterCatalog.DefaultAuthoringPath);
            return authoring;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
