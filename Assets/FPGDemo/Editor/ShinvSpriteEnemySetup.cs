using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FPG.Demo.Combat;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor
{
    internal static class ShinvSpriteEnemySetup
    {
        private const float SpriteSizeMultiplier = 1.5f;
        private const string Root = "Assets/FPGDemo";
        private const string SourcePrefab =
            Root + "/Presentation/Characters/Enemies/shinv/Prefabs/PF_FPG_shinvEntity.prefab";
        private const string SpriteTemplatePrefab =
            Root + "/Presentation/Characters/Enemies/shinv/Prefabs/PF_FPG_shinvSpriteEntity.prefab";
        private const string SpritePrefab =
            Root + "/Presentation/Characters/Enemies/shinvTest/Prefabs/PF_FPG_shinvTestSpriteEntity.prefab";
        private const string SpriteMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string WalkTexture =
            Root + "/Prototypes/ShinvSpriteSequenceTest/Art/shinv_walk.png";
        private const string HitTexture =
            Root + "/Prototypes/ShinvSpriteSequenceTest/Art/shinv_hit.png";
        private const string WalkClip =
            Root + "/Prototypes/ShinvSpriteSequenceTest/Animation/Shinv_Walk.anim";
        private const string HitClip =
            Root + "/Prototypes/ShinvSpriteSequenceTest/Animation/Shinv_Hit.anim";
        private const string HudieBehavior =
            Root + "/Config/FormalEncounter/FPG_Hudie_Behavior.asset";
        private const string ShinvBehavior =
            Root + "/Config/FormalEncounter/FPG_shinv_Behavior.asset";
        private const string ShinvTestBehavior =
            Root + "/Config/FormalEncounter/FPG_shinvTest_Behavior.asset";
        private const string HudiePositionQuery =
            Root + "/Config/FormalEncounter/FPG_Hudie_PositionQuery.asset";
        private const string ShinvPositionQuery =
            Root + "/Config/FormalEncounter/FPG_shinv_PositionQuery.asset";
        private const string ShinvTestPositionQuery =
            Root + "/Config/FormalEncounter/FPG_shinvTest_PositionQuery.asset";
        private const string HudieAttack =
            Root + "/Config/FormalEncounter/FPG_Hudie_Attack.asset";
        private const string ShinvAttack =
            Root + "/Config/FormalEncounter/FPG_shinv_Attack.asset";
        private const string ShinvTestAttack =
            Root + "/Config/FormalEncounter/FPG_shinvTest_Attack.asset";
        private const string ShinvEnemy =
            Root + "/Config/FormalEncounter/FPG_shinv_Enemy.asset";
        private const string ShinvTestEnemy =
            Root + "/Config/FormalEncounter/FPG_shinvTest_Enemy.asset";
        private const string EnemyCatalog =
            Root + "/Config/FormalEncounter/FPG_NormalRoom_EnemyCatalog.asset";

        [MenuItem("Tools/FPG/Setup Shinv Sprite Enemy")]
        private static void SetupFromMenu()
        {
            EnsureSetup();
        }

        private static void EnsureSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling
                || EditorApplication.isUpdating)
            {
                Debug.LogWarning("Run Shinv Sprite setup from the menu after the Editor is idle and out of Play Mode.");
                return;
            }

            if (IsConfigured())
            {
                return;
            }

            try
            {
                EnsureTestAssets();
                EnsureFolder(Root + "/Presentation/Characters/Enemies/shinvTest");
                EnsureFolder(Root + "/Presentation/Characters/Enemies/shinvTest/Prefabs");
                EnsurePositionQuery();
                BuildSpritePrefab();
                ConfigureBehavior();
                ConfigureAttack();
                ConfigureEnemy();
                ConfigureLegacyEnemy();
                EnsureCatalogContains(
                    AssetDatabase.LoadAssetAtPath<FpgEnemyDefinition>(ShinvEnemy),
                    AssetDatabase.LoadAssetAtPath<FpgEnemyDefinition>(ShinvTestEnemy));
                AssetDatabase.SaveAssets();

                FpgEnemyDefinition enemy =
                    AssetDatabase.LoadAssetAtPath<FpgEnemyDefinition>(ShinvTestEnemy);
                string error = string.Empty;
                if (enemy == null || !enemy.TryValidate(out error))
                {
                    Debug.LogError("Shinv test Sprite setup failed validation: "
                        + (enemy == null ? "enemy definition is missing." : error));
                    return;
                }

                FpgEnemyDefinition legacy =
                    AssetDatabase.LoadAssetAtPath<FpgEnemyDefinition>(ShinvEnemy);
                if (legacy == null || !legacy.TryValidate(out error))
                {
                    Debug.LogError("Legacy Shinv setup failed validation: "
                        + (legacy == null ? "enemy definition is missing." : error));
                    return;
                }

                Debug.Log("Configured both the legacy Shinv and Shinv Test enemies.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static bool IsConfigured()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpritePrefab);
            FpgEnemyDefinition enemy =
                AssetDatabase.LoadAssetAtPath<FpgEnemyDefinition>(ShinvTestEnemy);
            FpgEnemyBehaviorDefinition behavior =
                AssetDatabase.LoadAssetAtPath<FpgEnemyBehaviorDefinition>(ShinvTestBehavior);
            FpgEnemyAttackDefinition attack =
                AssetDatabase.LoadAssetAtPath<FpgEnemyAttackDefinition>(ShinvTestAttack);
            FpgEnemyPositionQueryDefinition query =
                AssetDatabase.LoadAssetAtPath<FpgEnemyPositionQueryDefinition>(ShinvTestPositionQuery);
            FpgEnemyDefinition legacy =
                AssetDatabase.LoadAssetAtPath<FpgEnemyDefinition>(ShinvEnemy);
            if (prefab == null
                || prefab.GetComponent<FpgSpriteEnemyEntityView>() == null
                || enemy == null
                || behavior == null
                || attack == null
                || query == null
                || legacy == null
                || legacy.EnemyDefinitionId != "shinv"
                || legacy.DisplayName != "侍女"
                || legacy.EntityPrefab
                    != AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab)
                || !CatalogContains(enemy)
                || !CatalogContains(legacy))
            {
                return false;
            }

            if (enemy.EnemyDefinitionId == legacy.EnemyDefinitionId
                || !enemy.TryValidate(out _)
                || !legacy.TryValidate(out _)
                || legacy.EntityPrefab.GetComponent<FpgEnemyEntityView>() == null)
            {
                return false;
            }

            FpgSpriteEnemyEntityView spriteView =
                prefab.GetComponent<FpgSpriteEnemyEntityView>();
            SerializedObject spriteViewObject = new SerializedObject(spriteView);
            SerializedProperty frameBindings =
                spriteViewObject.FindProperty("frameBindings");
            if (frameBindings == null || frameBindings.arraySize != 2)
            {
                return false;
            }

            SerializedObject enemyObject = new SerializedObject(enemy);
            SerializedObject behaviorObject = new SerializedObject(behavior);
            SerializedObject attackObject = new SerializedObject(attack);
            SerializedObject queryObject = new SerializedObject(query);
            SerializedProperty sequences = attackObject.FindProperty("sequences");
            if (sequences == null || sequences.arraySize == 0)
            {
                return false;
            }

            SerializedProperty events = sequences.GetArrayElementAtIndex(0)
                .FindPropertyRelative("projectileEvents");
            if (events == null || events.arraySize != 1)
            {
                return false;
            }

            SerializedProperty projectile = events.GetArrayElementAtIndex(0);
            return enemyObject.FindProperty("entityViewPrefab").objectReferenceValue == prefab
                && enemyObject.FindProperty("role").enumValueIndex == 2
                && enemyObject.FindProperty("enemyDefinitionId").stringValue
                    == "shinv-test"
                && enemyObject.FindProperty("displayName").stringValue
                    == "侍女测试"
                && behaviorObject.FindProperty("mode").enumValueIndex == 2
                && behaviorObject.FindProperty("positionQuery").objectReferenceValue == query
                && enemy.Behavior == behavior
                && enemy.AttackPatternCount == 1
                && enemy.GetAttackPattern(0) == attack
                && projectile.FindPropertyRelative("eventId").stringValue
                    == "event.shinv-test.projectile.attack.0"
                && projectile.FindPropertyRelative("tick").intValue == 48
                && projectile.FindPropertyRelative("baseDamage").intValue == 18
                && projectile.FindPropertyRelative("breakDamage").intValue == 6
                && projectile.FindPropertyRelative("threatDefinitionId").intValue == 452
                && projectile.FindPropertyRelative("projectileDefinitionId").intValue == 402
                && queryObject.FindProperty("minimumAttackDistance").floatValue == 3f
                && queryObject.FindProperty("preferredAttackDistance").floatValue == 7f
                && queryObject.FindProperty("maximumAttackDistance").floatValue == 9f
                && queryObject.FindProperty("maximumLocalRepositionRadius").floatValue == 4f
                && queryObject.FindProperty("sampleAroundCurrentPosition").boolValue
                && queryObject.FindProperty("requireCameraViewport").boolValue;
        }

        private static void EnsureTestAssets()
        {
            EnsureAssetCopy(ShinvBehavior, ShinvTestBehavior);
            EnsureAssetCopy(ShinvPositionQuery, ShinvTestPositionQuery);
            EnsureAssetCopy(ShinvAttack, ShinvTestAttack);
            EnsureAssetCopy(ShinvEnemy, ShinvTestEnemy);
        }

        private static void EnsureAssetCopy(string source, string destination)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destination) != null)
            {
                return;
            }

            if (!AssetDatabase.CopyAsset(source, destination))
            {
                throw new InvalidOperationException(
                    "Could not create isolated Shinv test asset: " + destination);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent)
                || !AssetDatabase.IsValidFolder(parent)
                || string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, name)))
            {
                throw new InvalidOperationException(
                    "Could not create Shinv test folder: " + folder);
            }
        }

        private static void EnsurePositionQuery()
        {
            if (AssetDatabase.LoadAssetAtPath<FpgEnemyPositionQueryDefinition>(ShinvTestPositionQuery)
                == null)
            {
                if (!AssetDatabase.CopyAsset(HudiePositionQuery, ShinvTestPositionQuery))
                {
                    throw new InvalidOperationException(
                        "Could not copy Hudie position-query settings for Shinv Test.");
                }
            }

            FpgEnemyPositionQueryDefinition query =
                AssetDatabase.LoadAssetAtPath<FpgEnemyPositionQueryDefinition>(ShinvTestPositionQuery);
            if (query == null)
            {
                throw new InvalidOperationException("Shinv test position-query asset is missing.");
            }

            SerializedObject serialized = new SerializedObject(query);
            serialized.FindProperty("minimumAttackDistance").floatValue = 3f;
            serialized.FindProperty("preferredAttackDistance").floatValue = 7f;
            serialized.FindProperty("maximumAttackDistance").floatValue = 9f;
            serialized.FindProperty("angularSamples").intValue = 8;
            GameObject spritePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpritePrefab)
                ?? AssetDatabase.LoadAssetAtPath<GameObject>(SpriteTemplatePrefab);
            SpriteRenderer renderer = spritePrefab != null
                ? spritePrefab.GetComponentInChildren<SpriteRenderer>(true) : null;
            float sizeMultiplier = renderer != null
                ? Mathf.Abs(renderer.transform.lossyScale.x) / 0.15f
                : SpriteSizeMultiplier;
            serialized.FindProperty("agentRadius").floatValue = 0.38f * sizeMultiplier;
            SerializedProperty heights = serialized.FindProperty("heightLayerOffsets");
            heights.arraySize = 1;
            heights.GetArrayElementAtIndex(0).floatValue = 0f;
            serialized.FindProperty("sampleAroundCurrentPosition").boolValue = true;
            serialized.FindProperty("maximumLocalRepositionRadius").floatValue = 4f;
            serialized.FindProperty("requireCameraViewport").boolValue = true;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(query);
        }

        private static void BuildSpritePrefab()
        {
            // Keep the legacy Spine prefab isolated from the Sprite test setup.
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SpritePrefab);
            string source = existing != null
                && existing.GetComponent<FpgSpriteEnemyEntityView>() != null
                ? SpritePrefab
                : SpriteTemplatePrefab;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(source) == null)
            {
                throw new InvalidOperationException(
                    "Shinv Sprite template prefab is missing: " + source);
            }
            GameObject root = PrefabUtility.LoadPrefabContents(source);
            try
            {
                Transform gameplayAnchor = FindChild(root.transform, "GameplayAnchor");
                Transform projectileAnchor = FindChild(root.transform, "ProjectileSpawnAnchor");
                Transform weakpointAnchor = FindChild(root.transform, "WeakpointAnchor");
                Transform visualRoot = FindChild(root.transform, "VisualRoot");
                D0ActorSocketRegistry socketRegistry =
                    root.GetComponent<D0ActorSocketRegistry>();
                Collider bodyHitbox = FindChild(root.transform, "BodyHitbox")
                    .GetComponent<Collider>();
                Collider weakpointHitbox = weakpointAnchor.GetComponent<Collider>();
                FpgEnemyEntityView spineView = root.GetComponent<FpgEnemyEntityView>();
                if (spineView != null)
                {
                    UnityEngine.Object.DestroyImmediate(spineView, true);
                }

                RemoveSpineComponents(visualRoot);
                SpriteRenderer spriteRenderer = RebuildSpriteRenderer(visualRoot);
                Sprite[] walkSprites = LoadSprites(WalkTexture);
                if (walkSprites.Length == 0)
                {
                    throw new InvalidOperationException("Shinv walk sprites are missing.");
                }

                Sprite[] hitSprites = LoadSprites(HitTexture);
                if (hitSprites.Length == 0)
                {
                    throw new InvalidOperationException("Shinv hit sprites are missing.");
                }

                spriteRenderer.sprite = walkSprites[0];
                spriteRenderer.sortingOrder = 2;
                // Keep the artist's authored pose when rebuilding Sprite data.
                FitStaticHitParts(
                    visualRoot,
                    spriteRenderer.sprite,
                    bodyHitbox,
                    weakpointHitbox,
                    projectileAnchor,
                    FindChild(root.transform, "AttackOrigin"));
                FpgSpriteEnemyEntityView spriteView =
                    root.GetComponent<FpgSpriteEnemyEntityView>();
                if (spriteView == null)
                {
                    spriteView = root.AddComponent<FpgSpriteEnemyEntityView>();
                }

                SerializedObject view = new SerializedObject(spriteView);
                view.FindProperty("gameplayAnchor").objectReferenceValue = gameplayAnchor;
                view.FindProperty("projectileAnchor").objectReferenceValue = projectileAnchor;
                view.FindProperty("weakpointAnchor").objectReferenceValue = weakpointAnchor;
                view.FindProperty("overheadHealthBarAnchor").objectReferenceValue =
                    FitOverheadHealthBarAnchor(root, spriteRenderer);
                view.FindProperty("socketRegistry").objectReferenceValue = socketRegistry;
                view.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
                SerializedProperty hitParts = view.FindProperty("hitParts");
                hitParts.arraySize = 2;
                hitParts.GetArrayElementAtIndex(0).objectReferenceValue = bodyHitbox;
                hitParts.GetArrayElementAtIndex(1).objectReferenceValue = weakpointHitbox;
                SerializedProperty hitKinds = view.FindProperty("hitPartKinds");
                hitKinds.arraySize = 2;
                hitKinds.GetArrayElementAtIndex(0).enumValueIndex = (int)HitPart.Body;
                hitKinds.GetArrayElementAtIndex(1).enumValueIndex = (int)HitPart.Weakpoint;
                SerializedProperty bindings = view.FindProperty("animations");
                bindings.arraySize = 2;
                SetAnimationBinding(
                    bindings.GetArrayElementAtIndex(0),
                    "walk",
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClip));
                SetAnimationBinding(
                    bindings.GetArrayElementAtIndex(1),
                    "hit",
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(HitClip));
                SerializedProperty frameBindings = view.FindProperty("frameBindings");
                frameBindings.arraySize = 2;
                SetFrameBinding(
                    frameBindings.GetArrayElementAtIndex(0),
                    "walk",
                    walkSprites);
                SetFrameBinding(
                    frameBindings.GetArrayElementAtIndex(1),
                    "hit",
                    hitSprites);
                view.FindProperty("hitAnimation").stringValue = "hit";
                view.ApplyModifiedPropertiesWithoutUndo();

                root.name = "PF_FPG_shinvTestSpriteEntity";
                PrefabUtility.SaveAsPrefabAsset(root, SpritePrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static SpriteRenderer RebuildSpriteRenderer(Transform visualRoot)
        {
            if (visualRoot == null)
            {
                throw new InvalidOperationException("Shinv VisualRoot is missing.");
            }

            Component[] components = visualRoot.GetComponents<Component>();
            for (int index = components.Length - 1; index >= 0; index--)
            {
                Component component = components[index];
                if (component != null
                    && !(component is Transform)
                    && !(component is SpriteRenderer))
                {
                    UnityEngine.Object.DestroyImmediate(component, true);
                }
            }

            SpriteRenderer renderer = visualRoot.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            if (renderer == null)
            {
                throw new InvalidOperationException(
                    "Shinv VisualRoot could not create a SpriteRenderer.");
            }

            Material spriteMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            if (spriteMaterial == null)
            {
                throw new InvalidOperationException(
                    "URP Sprite-Unlit-Default material is unavailable.");
            }

            renderer.sharedMaterial = spriteMaterial;

            return renderer;
        }

        private static void RemoveSpineComponents(Transform visualRoot)
        {
            Component[] components = visualRoot.GetComponents<Component>();
            for (int index = components.Length - 1; index >= 0; index--)
            {
                Component component = components[index];
                if (component == null || component is Transform)
                {
                    continue;
                }

                string typeName = component.GetType().FullName;
                if (component is FpgEntitySkeletonRootMotionBridge
                    || (typeName != null
                        && typeName.StartsWith("Spine.Unity.", StringComparison.Ordinal)))
                {
                    UnityEngine.Object.DestroyImmediate(component, true);
                }
            }
        }

        private static void FitStaticHitParts(
            Transform visualRoot,
            Sprite sprite,
            Collider bodyCollider,
            Collider weakpointCollider,
            Transform projectileAnchor,
            Transform socketAnchor)
        {
            BoxCollider body = bodyCollider as BoxCollider;
            SphereCollider weakpoint = weakpointCollider as SphereCollider;
            if (body == null || weakpoint == null)
            {
                throw new InvalidOperationException(
                    "Shinv requires a static body box and weakpoint sphere collider.");
            }

            Bounds spriteBounds = sprite.bounds;
            SpriteRenderer renderer = visualRoot.GetComponent<SpriteRenderer>();
            Vector3 mirror = new Vector3(
                renderer != null && renderer.flipX ? -1f : 1f,
                renderer != null && renderer.flipY ? -1f : 1f,
                1f);
            Vector3 spriteSize = Vector3.Scale(
                spriteBounds.size,
                new Vector3(
                    Mathf.Abs(visualRoot.lossyScale.x),
                    Mathf.Abs(visualRoot.lossyScale.y),
                    Mathf.Abs(visualRoot.lossyScale.z)));
            Vector3 bodyCenter = visualRoot.TransformPoint(
                Vector3.Scale(spriteBounds.center, mirror));
            body.transform.position = bodyCenter;
            body.transform.rotation = visualRoot.rotation;
            // Sprite-space depth follows the same scale as the authored art.
            Vector3 localSize = body.transform.InverseTransformVector(
                visualRoot.TransformVector(new Vector3(
                    spriteBounds.size.x, spriteBounds.size.y, 2.4f)));
            body.size = new Vector3(
                Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            body.center = Vector3.zero;

            Vector3 weakpointLocal = new Vector3(
                spriteBounds.min.x + spriteBounds.size.x * 0.50f,
                spriteBounds.min.y + spriteBounds.size.y * 0.76f,
                spriteBounds.center.z);
            weakpoint.transform.position = visualRoot.TransformPoint(
                Vector3.Scale(weakpointLocal, mirror));
            Vector3 weakpointScale = weakpoint.transform.lossyScale;
            float radiusScale = Mathf.Max(
                Mathf.Abs(weakpointScale.x),
                Mathf.Abs(weakpointScale.y),
                Mathf.Abs(weakpointScale.z));
            weakpoint.radius = spriteSize.x * 0.13f / radiusScale;
            weakpoint.center = Vector3.zero;

            Vector3 projectileLocal = new Vector3(
                spriteBounds.min.x + spriteBounds.size.x * 0.22f,
                spriteBounds.min.y + spriteBounds.size.y * 0.60f,
                spriteBounds.center.z);
            Vector3 projectilePosition = visualRoot.TransformPoint(
                Vector3.Scale(projectileLocal, mirror));
            projectileAnchor.position = projectilePosition;
            socketAnchor.position = projectilePosition;
        }

        private static Transform FitOverheadHealthBarAnchor(
            GameObject root, SpriteRenderer renderer)
        {
            const string healthBarPrefabPath =
                Root + "/Presentation/HUD/Prefabs/PF_FPG_OverheadHealthBar.prefab";
            GameObject healthBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                healthBarPrefabPath);
            FpgOverheadHealthBarView healthBar = healthBarPrefab != null
                ? healthBarPrefab.GetComponent<FpgOverheadHealthBarView>() : null;
            if (healthBar == null)
            {
                throw new InvalidOperationException("Formal overhead health bar prefab is missing.");
            }

            Vector3 offset = new SerializedObject(healthBar)
                .FindProperty("worldOffset").vector3Value;
            Transform anchor = root.transform.Find("GameplayAnchor/OverheadHealthBarAnchor");
            if (anchor == null)
            {
                anchor = new GameObject("OverheadHealthBarAnchor").transform;
                anchor.SetParent(FindChild(root.transform, "GameplayAnchor"), false);
            }

            Bounds bounds = renderer.bounds;
            // The shared HUD adds a world offset after following this anchor.
            anchor.position = new Vector3(
                bounds.center.x, bounds.max.y + 0.25f, bounds.center.z) - offset;
            return anchor;
        }

        private static void ConfigureBehavior()
        {
            FpgEnemyBehaviorDefinition hudie =
                AssetDatabase.LoadAssetAtPath<FpgEnemyBehaviorDefinition>(HudieBehavior);
            FpgEnemyBehaviorDefinition shinv =
                AssetDatabase.LoadAssetAtPath<FpgEnemyBehaviorDefinition>(ShinvTestBehavior);
            FpgEnemyPositionQueryDefinition query =
                AssetDatabase.LoadAssetAtPath<FpgEnemyPositionQueryDefinition>(ShinvTestPositionQuery);
            if (hudie == null || shinv == null || query == null)
            {
                throw new InvalidOperationException("A Shinv Test behavior source asset is missing.");
            }

            EditorUtility.CopySerialized(hudie, shinv);
            shinv.name = "FPG_shinvTest_Behavior";
            SerializedObject serialized = new SerializedObject(shinv);
            serialized.FindProperty("behaviorId").stringValue = "shinv-test-behavior";
            serialized.FindProperty("displayName").stringValue = "侍女测试行为";
            serialized.FindProperty("mode").enumValueIndex = 2;
            serialized.FindProperty("moveSpeed").floatValue = 2f;
            serialized.FindProperty("moveDurationTicks").intValue = 120;
            serialized.FindProperty("positionQuery").objectReferenceValue = query;
            serialized.FindProperty("entryAnimation").stringValue = "walk";
            serialized.FindProperty("idleAnimation").stringValue = "walk";
            serialized.FindProperty("deathAnimation").stringValue = "walk";
            SerializedProperty rootMotionRules =
                serialized.FindProperty("animationRootMotionRules");
            rootMotionRules.arraySize = 0;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(shinv);
        }

        private static void ConfigureAttack()
        {
            FpgEnemyAttackDefinition hudie =
                AssetDatabase.LoadAssetAtPath<FpgEnemyAttackDefinition>(HudieAttack);
            FpgEnemyAttackDefinition shinv =
                AssetDatabase.LoadAssetAtPath<FpgEnemyAttackDefinition>(ShinvTestAttack);
            if (hudie == null || shinv == null)
            {
                throw new InvalidOperationException("A Shinv Test attack source asset is missing.");
            }

            EditorUtility.CopySerialized(hudie, shinv);
            shinv.name = "FPG_shinvTest_Attack";
            SerializedObject serialized = new SerializedObject(shinv);
            serialized.FindProperty("skillId").stringValue = "shinv-test-basic-attack";
            serialized.FindProperty("displayName").stringValue = "侍女测试远程攻击";
            serialized.FindProperty("designerNotes").stringValue =
                "Sprite Battle Test: Walk is the temporary attack animation; projectile damage preserves the 18 / 6 baseline.";
            SerializedProperty sequences = serialized.FindProperty("sequences");
            sequences.arraySize = 1;
            SerializedProperty sequence = sequences.GetArrayElementAtIndex(0);
            sequence.FindPropertyRelative("durationTicks").intValue = 60;
            sequence.FindPropertyRelative("mainAnimation").stringValue = "walk";
            sequence.FindPropertyRelative("animationStartTick").intValue = 0;
            sequence.FindPropertyRelative("animationEndTick").intValue = 60;
            SerializedProperty events = sequence.FindPropertyRelative("projectileEvents");
            if (events.arraySize != 1)
            {
                throw new InvalidOperationException("Hudie attack template must contain one projectile event.");
            }

            SerializedProperty projectile = events.GetArrayElementAtIndex(0);
            projectile.FindPropertyRelative("eventId").stringValue =
                "event.shinv-test.projectile.attack.0";
            projectile.FindPropertyRelative("tick").intValue = 48;
            projectile.FindPropertyRelative("baseDamage").intValue = 18;
            projectile.FindPropertyRelative("breakDamage").intValue = 6;
            projectile.FindPropertyRelative("threatDefinitionId").intValue = 452;
            projectile.FindPropertyRelative("projectileDefinitionId").intValue = 402;

            SerializedProperty warnings = sequence.FindPropertyRelative("warnings");
            if (warnings.arraySize != 1)
            {
                throw new InvalidOperationException("Hudie attack template must contain one warning.");
            }

            SerializedProperty warning = warnings.GetArrayElementAtIndex(0);
            warning.FindPropertyRelative("eventId").stringValue = "warning.shinv-test.projectile";
            warning.FindPropertyRelative("warningId").stringValue = "enemy-source-fast";
            warning.FindPropertyRelative("startTick").intValue = 0;
            warning.FindPropertyRelative("endTick").intValue = 47;

            SerializedProperty tracks = sequence.FindPropertyRelative("activePresentationTracks");
            if (tracks.arraySize > 0)
            {
                SerializedProperty track = tracks.GetArrayElementAtIndex(0);
                track.FindPropertyRelative("trackId").stringValue =
                    "track.shinv-test.projectile.active";
                track.FindPropertyRelative("displayName").stringValue =
                    "Shinv Test Active Presentation";
                SerializedProperty audioEvents = track.FindPropertyRelative("audioEvents");
                if (audioEvents.arraySize > 0)
                {
                    SerializedProperty audio = audioEvents.GetArrayElementAtIndex(0);
                    audio.FindPropertyRelative("eventId").stringValue =
                        "presentation.shinv-test.projectile.launch.0";
                    audio.FindPropertyRelative("tick").intValue = 48;
                    audio.FindPropertyRelative("boundGameplayEventId").stringValue =
                        "event.shinv-test.projectile.attack.0";
                }
            }

            serialized.FindProperty("firstReadyOffsetTicks").intValue = 60;
            serialized.FindProperty("sequenceCooldownTicks").intValue = 60;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(shinv);
        }

        private static void ConfigureEnemy()
        {
            FpgEnemyDefinition enemy =
                AssetDatabase.LoadAssetAtPath<FpgEnemyDefinition>(ShinvTestEnemy);
            FpgEnemyBehaviorDefinition behavior =
                AssetDatabase.LoadAssetAtPath<FpgEnemyBehaviorDefinition>(ShinvTestBehavior);
            FpgEnemyAttackDefinition attack =
                AssetDatabase.LoadAssetAtPath<FpgEnemyAttackDefinition>(ShinvTestAttack);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpritePrefab);
            if (enemy == null || behavior == null || attack == null || prefab == null)
            {
                throw new InvalidOperationException("Shinv Test definition or asset is missing.");
            }

            SerializedObject serialized = new SerializedObject(enemy);
            serialized.FindProperty("enemyDefinitionId").stringValue = "shinv-test";
            serialized.FindProperty("displayName").stringValue = "侍女测试";
            serialized.FindProperty("role").enumValueIndex = 2;
            serialized.FindProperty("entityViewPrefab").objectReferenceValue = prefab;
            serialized.FindProperty("behavior").objectReferenceValue = behavior;
            SerializedProperty attacks = serialized.FindProperty("attackPatterns");
            attacks.arraySize = 1;
            attacks.GetArrayElementAtIndex(0).objectReferenceValue = attack;
            serialized.FindProperty("designerNotes").stringValue =
                "BattleTest Sprite enemy. The authored Walk and Hit sequences are used; Walk temporarily fills attack, idle, entry and death animation keys.";
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(enemy);
        }

        private static void ConfigureLegacyEnemy()
        {
            FpgEnemyDefinition enemy =
                AssetDatabase.LoadAssetAtPath<FpgEnemyDefinition>(ShinvEnemy);
            FpgEnemyBehaviorDefinition behavior =
                AssetDatabase.LoadAssetAtPath<FpgEnemyBehaviorDefinition>(ShinvBehavior);
            FpgEnemyAttackDefinition attack =
                AssetDatabase.LoadAssetAtPath<FpgEnemyAttackDefinition>(ShinvAttack);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
            if (enemy == null || behavior == null || attack == null || prefab == null)
            {
                throw new InvalidOperationException("Legacy Shinv definition or Spine Prefab is missing.");
            }

            if (enemy.EntityPrefab == prefab)
            {
                return;
            }

            SerializedObject behaviorSerialized = new SerializedObject(behavior);
            behaviorSerialized.FindProperty("displayName").stringValue = "侍女旧版行为";
            behaviorSerialized.FindProperty("entryAnimation").stringValue = "ready attack";
            behaviorSerialized.FindProperty("idleAnimation").stringValue = "ready attack";
            behaviorSerialized.FindProperty("deathAnimation").stringValue = "die";
            behaviorSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(behavior);

            SerializedObject attackSerialized = new SerializedObject(attack);
            SerializedProperty sequences = attackSerialized.FindProperty("sequences");
            for (int index = 0; index < sequences.arraySize; index++)
            {
                sequences.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("mainAnimation").stringValue = "attack";
            }

            attackSerialized.FindProperty("displayName").stringValue = "侍女旧版攻击";
            attackSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(attack);

            SerializedObject serialized = new SerializedObject(enemy);
            serialized.FindProperty("enemyDefinitionId").stringValue = "shinv";
            serialized.FindProperty("displayName").stringValue = "侍女";
            serialized.FindProperty("entityViewPrefab").objectReferenceValue = prefab;
            serialized.FindProperty("designerNotes").stringValue =
                "Legacy Spine presentation. Keeps the current shinv combat values and attack configuration.";
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(enemy);
        }

        private static void EnsureCatalogContains(
            FpgEnemyDefinition legacy,
            FpgEnemyDefinition test)
        {
            FpgEnemyDefinitionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgEnemyDefinitionCatalog>(EnemyCatalog);
            if (catalog == null || legacy == null || test == null)
            {
                throw new InvalidOperationException("Shinv enemy catalog or definitions are missing.");
            }

            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty definitions = serialized.FindProperty("definitions");
            AddCatalogDefinition(definitions, legacy);
            AddCatalogDefinition(definitions, test);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
        }

        private static void AddCatalogDefinition(
            SerializedProperty definitions,
            FpgEnemyDefinition enemy)
        {
            for (int index = 0; index < definitions.arraySize; index++)
            {
                if (definitions.GetArrayElementAtIndex(index).objectReferenceValue == enemy)
                {
                    return;
                }
            }

            int newIndex = definitions.arraySize;
            definitions.InsertArrayElementAtIndex(newIndex);
            definitions.GetArrayElementAtIndex(newIndex).objectReferenceValue = enemy;
        }

        private static bool CatalogContains(FpgEnemyDefinition enemy)
        {
            FpgEnemyDefinitionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgEnemyDefinitionCatalog>(EnemyCatalog);
            if (catalog == null || enemy == null)
            {
                return false;
            }

            IReadOnlyList<FpgEnemyDefinition> definitions = catalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] == enemy)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetAnimationBinding(
            SerializedProperty binding,
            string animationName,
            AnimationClip clip)
        {
            if (clip == null)
            {
                throw new InvalidOperationException(
                    "Missing animation clip for Shinv key '" + animationName + "'.");
            }

            SerializedProperty name = binding.FindPropertyRelative("animationName");
            if (!string.Equals(name.stringValue, animationName, StringComparison.Ordinal))
            {
                // Initialize new bindings; rebuilding must preserve artist tuning.
                binding.FindPropertyRelative("playbackSpeed").floatValue =
                    string.Equals(animationName, "hit", StringComparison.Ordinal) ? 2.5f : 1f;
            }

            name.stringValue = animationName;
            binding.FindPropertyRelative("clip").objectReferenceValue = clip;
        }

        private static void SetFrameBinding(
            SerializedProperty binding,
            string animationName,
            Sprite[] frames)
        {
            if (frames == null || frames.Length == 0)
            {
                throw new InvalidOperationException(
                    "Missing Sprite frames for Shinv key '" + animationName + "'.");
            }

            binding.FindPropertyRelative("animationName").stringValue = animationName;
            SerializedProperty frameArray = binding.FindPropertyRelative("frames");
            frameArray.arraySize = frames.Length;
            for (int index = 0; index < frames.Length; index++)
            {
                frameArray.GetArrayElementAtIndex(index).objectReferenceValue = frames[index];
            }
        }

        private static Transform FindChild(Transform root, string childName)
        {
            Transform child = null;
            if (root != null)
            {
                Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < descendants.Length; index++)
                {
                    if (string.Equals(
                            descendants[index].name,
                            childName,
                            StringComparison.Ordinal))
                    {
                        child = descendants[index];
                        break;
                    }
                }
            }

            if (child == null)
            {
                throw new InvalidOperationException("Shinv prefab child is missing: " + childName);
            }

            return child;
        }

        private static Sprite[] LoadSprites(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .OrderBy(sprite => FrameNumber(sprite.name))
                .ToArray();
        }

        private static int FrameNumber(string spriteName)
        {
            string digits = new string(spriteName.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int frame) ? frame : int.MaxValue;
        }
    }
}
