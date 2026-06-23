using NewFPG.Combat;
using NewFPG.Level;
using NewFPG.Monsters;
using NewFPG.Prototype;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewFPG.EditorTools
{
    public static class CombatFoundationInstaller
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Characters/Player.prefab";
        private const string FishPrefabPath = "Assets/Prefabs/Monster/Fish.prefab";
        private const string WeaponViewPrefabPath = "Assets/Prefabs/Prototype/FirstPersonWeaponView.prefab";
        private const string WeaponAssetFolder = "Assets/Settings/Combat";
        private const string DefaultWeaponPath = WeaponAssetFolder + "/FlyingSword.asset";
        private const string ControllerPath = "Assets/Prefabs/Monster/FishAssets/Fish.controller";
        private const string AttackClipPath = "Assets/Prefabs/Monster/FishAssets/Animations/Fish_Attack.anim";
        private const string AttackSpritePath = "Assets/Prefabs/Monster/FishAssets/Attack/fish_attack01.png";
        private const string AttackEndSpritePath = "Assets/Prefabs/Monster/FishAssets/Attack/fish_attack01_end.png";
        private const string WeaponIconPath = "Assets/Art/Weapons/HUD/Xianxia_FlyingSword.png";
        private const string PlayerTag = "Player";
        private const string AttackParameter = "Attack";
        private const string AttackStateName = "Attack";
        private const string IdleStateName = "Idle";
        private const string MoveStateName = "Move";
        private const string IsMovingParameter = "IsMoving";

        [MenuItem("NewFPG/Combat/Install Combat Foundation")]
        public static void InstallCombatFoundation()
        {
            EnsureFolder(WeaponAssetFolder);
            EnsureTag(PlayerTag);
            WeaponDefinition defaultWeapon = CreateOrUpdateDefaultWeapon();
            InstallFishAttackAnimation();

            ConfigurePlayerPrefab(defaultWeapon);
            ConfigureFishPrefab();
            ConfigureWeaponViewPrefab();
            ConfigureSceneObjects(defaultWeapon);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("Installed NewFPG combat foundation: player vitals/resource/HUD, weapon config, fish vitals and attack controller.");
        }

        private static WeaponDefinition CreateOrUpdateDefaultWeapon()
        {
            WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(DefaultWeaponPath);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(weapon, DefaultWeaponPath);
            }

            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(WeaponIconPath);
            SerializedObject serializedWeapon = new SerializedObject(weapon);
            serializedWeapon.FindProperty("displayName").stringValue = "Flying Sword";
            serializedWeapon.FindProperty("icon").objectReferenceValue = icon;
            serializedWeapon.FindProperty("resourceCost").floatValue = 3f;
            serializedWeapon.FindProperty("damage").floatValue = 35f;
            serializedWeapon.FindProperty("cooldown").floatValue = 0.45f;
            serializedWeapon.FindProperty("range").floatValue = 8f;
            serializedWeapon.FindProperty("radius").floatValue = 0.85f;
            serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
            return weapon;
        }

        private static void ConfigurePlayerPrefab(WeaponDefinition weapon)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            ConfigurePlayerObject(prefabRoot, weapon, true);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        private static void ConfigureFishPrefab()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(FishPrefabPath);
            ConfigureFishObject(prefabRoot, true);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, FishPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        private static void ConfigureWeaponViewPrefab()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(WeaponViewPrefabPath);
            EnsureComponent<PrototypeWeaponCombatHud>(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, WeaponViewPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        private static void ConfigureSceneObjects(WeaponDefinition weapon)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                ConfigurePlayerObject(player, weapon, false);
                EditorUtility.SetDirty(player);
            }

            GameObject fish = GameObject.Find("Fish");
            if (fish != null)
            {
                ConfigureFishObject(fish, false);
                EditorUtility.SetDirty(fish);
            }

            LevelFlowDirector director = Object.FindFirstObjectByType<LevelFlowDirector>(FindObjectsInactive.Include);
            if (director != null)
            {
                PrototypeFirstPersonWeaponView weaponView = Object.FindFirstObjectByType<PrototypeFirstPersonWeaponView>(FindObjectsInactive.Include);
                PrototypeWeaponCombatHud weaponHud = null;
                if (weaponView != null)
                {
                    weaponHud = EnsureComponent<PrototypeWeaponCombatHud>(weaponView.gameObject);
                    EditorUtility.SetDirty(weaponView.gameObject);
                }

                SerializedObject serializedDirector = new SerializedObject(director);
                serializedDirector.FindProperty("hidePlayerVisualsDuringCombat").boolValue = true;
                serializedDirector.FindProperty("disablePlayerMovementDuringCombat").boolValue = true;
                serializedDirector.FindProperty("freezePlayerPhysicsDuringCombat").boolValue = true;
                serializedDirector.FindProperty("weaponView").objectReferenceValue = weaponView;
                serializedDirector.FindProperty("weaponCombatHud").objectReferenceValue = weaponHud;
                serializedDirector.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(director);
            }
        }

        private static void ConfigurePlayerObject(GameObject player, WeaponDefinition weapon, bool prefabAsset)
        {
            player.tag = PlayerTag;
            CombatVitals vitals = EnsureComponent<CombatVitals>(player);
            CombatResourcePool resourcePool = EnsureComponent<CombatResourcePool>(player);
            PlayerWeaponCaster caster = EnsureComponent<PlayerWeaponCaster>(player);
            PlayerHitFeedback hitFeedback = EnsureComponent<PlayerHitFeedback>(player);
            RemoveMissingMonoBehaviours(player);

            SerializedObject serializedVitals = new SerializedObject(vitals);
            serializedVitals.FindProperty("maxHealth").floatValue = 100f;
            serializedVitals.FindProperty("startingHealth").floatValue = 100f;
            serializedVitals.FindProperty("maxShield").floatValue = 50f;
            serializedVitals.FindProperty("startingShield").floatValue = 25f;
            serializedVitals.FindProperty("destroyOnDeath").boolValue = false;
            serializedVitals.FindProperty("animator").objectReferenceValue = player.GetComponent<Animator>();
            serializedVitals.FindProperty("spriteRenderer").objectReferenceValue = player.GetComponent<SpriteRenderer>();
            serializedVitals.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedResource = new SerializedObject(resourcePool);
            serializedResource.FindProperty("maxResource").floatValue = 10f;
            serializedResource.FindProperty("startingResource").floatValue = 5f;
            serializedResource.FindProperty("recoveryPerSecond").floatValue = 1f;
            serializedResource.FindProperty("recoverOverTime").boolValue = true;
            serializedResource.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedCaster = new SerializedObject(caster);
            SerializedProperty weapons = serializedCaster.FindProperty("weapons");
            weapons.arraySize = weapon != null ? 1 : 0;
            if (weapon != null)
            {
                weapons.GetArrayElementAtIndex(0).objectReferenceValue = weapon;
            }

            serializedCaster.FindProperty("resourcePool").objectReferenceValue = resourcePool;
            serializedCaster.FindProperty("castOrigin").objectReferenceValue = player.transform;
            serializedCaster.FindProperty("targetMask").intValue = ~0;
            serializedCaster.FindProperty("allowKeyboardShortcuts").boolValue = true;
            serializedCaster.FindProperty("combatEnabled").boolValue = false;
            serializedCaster.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedFeedback = new SerializedObject(hitFeedback);
            serializedFeedback.FindProperty("vitals").objectReferenceValue = vitals;
            serializedFeedback.FindProperty("targetCamera").objectReferenceValue = Camera.main;
            serializedFeedback.ApplyModifiedPropertiesWithoutUndo();

            if (prefabAsset)
            {
                EditorUtility.SetDirty(player);
            }
        }

        private static void ConfigureFishObject(GameObject fish, bool prefabAsset)
        {
            CombatVitals vitals = EnsureComponent<CombatVitals>(fish);
            FishAttackController attack = EnsureComponent<FishAttackController>(fish);
            LevelCombatant levelCombatant = EnsureComponent<LevelCombatant>(fish);

            FishMonsterController movement = fish.GetComponent<FishMonsterController>();
            Animator animator = fish.GetComponent<Animator>();
            SpriteRenderer spriteRenderer = fish.GetComponent<SpriteRenderer>();

            SerializedObject serializedVitals = new SerializedObject(vitals);
            serializedVitals.FindProperty("maxHealth").floatValue = 80f;
            serializedVitals.FindProperty("startingHealth").floatValue = 80f;
            serializedVitals.FindProperty("maxShield").floatValue = 0f;
            serializedVitals.FindProperty("startingShield").floatValue = 0f;
            serializedVitals.FindProperty("destroyOnDeath").boolValue = true;
            serializedVitals.FindProperty("deathDelay").floatValue = 0.25f;
            serializedVitals.FindProperty("animator").objectReferenceValue = animator;
            serializedVitals.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
            serializedVitals.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedAttack = new SerializedObject(attack);
            serializedAttack.FindProperty("target").objectReferenceValue = null;
            serializedAttack.FindProperty("autoFindPlayer").boolValue = true;
            serializedAttack.FindProperty("playerTag").stringValue = PlayerTag;
            serializedAttack.FindProperty("attackRange").floatValue = 2.2f;
            serializedAttack.FindProperty("requestInterval").floatValue = 2f;
            serializedAttack.FindProperty("attackPrepareTime").floatValue = 0.8f;
            serializedAttack.FindProperty("damage").floatValue = 12f;
            serializedAttack.FindProperty("damageRadius").floatValue = 1.35f;
            serializedAttack.FindProperty("warningHeightOffset").floatValue = 1.2f;
            serializedAttack.FindProperty("targetMask").intValue = ~0;
            serializedAttack.FindProperty("movement").objectReferenceValue = movement;
            serializedAttack.FindProperty("animator").objectReferenceValue = animator;
            serializedAttack.FindProperty("warningIndicator").objectReferenceValue = null;
            serializedAttack.FindProperty("attackTriggerParameter").stringValue = AttackParameter;
            serializedAttack.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedLevelCombatant = new SerializedObject(levelCombatant);
            serializedLevelCombatant.FindProperty("maxHp").floatValue = 80f;
            serializedLevelCombatant.FindProperty("destroyOnDeath").boolValue = false;
            serializedLevelCombatant.FindProperty("animator").objectReferenceValue = animator;
            serializedLevelCombatant.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
            serializedLevelCombatant.FindProperty("fishMonsterController").objectReferenceValue = movement;
            serializedLevelCombatant.FindProperty("combatVitals").objectReferenceValue = vitals;
            serializedLevelCombatant.ApplyModifiedPropertiesWithoutUndo();

            if (prefabAsset)
            {
                EditorUtility.SetDirty(fish);
            }
        }

        private static void InstallFishAttackAnimation()
        {
            Sprite attackSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AttackSpritePath);
            Sprite attackEndSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AttackEndSpritePath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (attackSprite == null || attackEndSprite == null || controller == null)
            {
                Debug.LogWarning("Fish attack animation install skipped. Missing sprite or controller.");
                return;
            }

            AnimationClip clip = CreateOrUpdateAttackClip(attackSprite, attackEndSprite);
            EnsureAttackParameter(controller);
            EnsureAttackState(controller, clip);
            EditorUtility.SetDirty(controller);
        }

        private static AnimationClip CreateOrUpdateAttackClip(Sprite attackSprite, Sprite attackEndSprite)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackClipPath);
            if (clip == null)
            {
                clip = new AnimationClip
                {
                    frameRate = 12f,
                    name = "Fish_Attack",
                };
                AssetDatabase.CreateAsset(clip, AttackClipPath);
            }

            clip.wrapMode = WrapMode.Once;
            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite",
            };
            ObjectReferenceKeyframe[] frames =
            {
                new ObjectReferenceKeyframe { time = 0f, value = attackSprite },
                new ObjectReferenceKeyframe { time = 0.12f, value = attackEndSprite },
                new ObjectReferenceKeyframe { time = 0.28f, value = attackEndSprite },
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, frames);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void EnsureAttackParameter(AnimatorController controller)
        {
            for (int i = 0; i < controller.parameters.Length; i++)
            {
                if (controller.parameters[i].name == AttackParameter)
                {
                    return;
                }
            }

            controller.AddParameter(AttackParameter, AnimatorControllerParameterType.Trigger);
        }

        private static void EnsureAttackState(AnimatorController controller, Motion attackClip)
        {
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState attackState = null;
            for (int i = 0; i < stateMachine.states.Length; i++)
            {
                if (stateMachine.states[i].state.name == AttackStateName)
                {
                    attackState = stateMachine.states[i].state;
                    break;
                }
            }

            if (attackState == null)
            {
                attackState = stateMachine.AddState(AttackStateName, new Vector3(570f, 250f, 0f));
            }

            attackState.motion = attackClip;
            attackState.writeDefaultValues = true;

            bool hasAnyStateTransition = false;
            for (int i = 0; i < stateMachine.anyStateTransitions.Length; i++)
            {
                AnimatorStateTransition transition = stateMachine.anyStateTransitions[i];
                if (transition.destinationState == attackState)
                {
                    hasAnyStateTransition = true;
                    ConfigureAttackEntryTransition(transition);
                    break;
                }
            }

            if (!hasAnyStateTransition)
            {
                AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(attackState);
                ConfigureAttackEntryTransition(transition);
            }

            bool hasExitTransition = false;
            for (int i = 0; i < attackState.transitions.Length; i++)
            {
                AnimatorStateTransition transition = attackState.transitions[i];
                if (transition.isExit)
                {
                    hasExitTransition = true;
                    ConfigureAttackExitTransition(transition);
                    break;
                }
            }

            if (!hasExitTransition)
            {
                AnimatorStateTransition transition = attackState.AddExitTransition();
                ConfigureAttackExitTransition(transition);
            }

            EnsureAttackReturnTransitions(attackState, FindState(stateMachine, IdleStateName), FindState(stateMachine, MoveStateName));
        }

        private static void ConfigureAttackEntryTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = true;
            transition.conditions = new[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.If,
                    parameter = AttackParameter,
                    threshold = 0f,
                },
            };
        }

        private static void ConfigureAttackExitTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = true;
            transition.exitTime = 0.9f;
            transition.duration = 0.05f;
            transition.conditions = System.Array.Empty<AnimatorCondition>();
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            for (int i = 0; i < stateMachine.states.Length; i++)
            {
                AnimatorState state = stateMachine.states[i].state;
                if (state != null && state.name == stateName)
                {
                    return state;
                }
            }

            return null;
        }

        private static void EnsureAttackReturnTransitions(AnimatorState attackState, AnimatorState idleState, AnimatorState moveState)
        {
            if (moveState != null)
            {
                AnimatorStateTransition toMove = FindTransition(attackState, moveState);
                if (toMove == null)
                {
                    toMove = attackState.AddTransition(moveState);
                }

                ConfigureAttackReturnTransition(
                    toMove,
                    new AnimatorCondition
                    {
                        mode = AnimatorConditionMode.If,
                        parameter = IsMovingParameter,
                        threshold = 0f,
                    });
            }

            if (idleState != null)
            {
                AnimatorStateTransition toIdle = FindTransition(attackState, idleState);
                if (toIdle == null)
                {
                    toIdle = attackState.AddTransition(idleState);
                }

                ConfigureAttackReturnTransition(
                    toIdle,
                    new AnimatorCondition
                    {
                        mode = AnimatorConditionMode.IfNot,
                        parameter = IsMovingParameter,
                        threshold = 0f,
                    });
            }
        }

        private static AnimatorStateTransition FindTransition(AnimatorState sourceState, AnimatorState destinationState)
        {
            for (int i = 0; i < sourceState.transitions.Length; i++)
            {
                AnimatorStateTransition transition = sourceState.transitions[i];
                if (transition.destinationState == destinationState)
                {
                    return transition;
                }
            }

            return null;
        }

        private static void ConfigureAttackReturnTransition(AnimatorStateTransition transition, AnimatorCondition condition)
        {
            transition.hasExitTime = true;
            transition.exitTime = 0.9f;
            transition.duration = 0.05f;
            transition.canTransitionToSelf = false;
            transition.conditions = new[] { condition };
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private static void RemoveMissingMonoBehaviours(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
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

        private static void EnsureTag(string tag)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                {
                    return;
                }
            }

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
