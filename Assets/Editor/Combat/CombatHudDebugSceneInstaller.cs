using NewFPG.Combat;
using NewFPG.Combat.SkillIndicators;
using NewFPG.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NewFPG.EditorTools
{
    public static class CombatHudDebugSceneInstaller
    {
        private const string ScenePath = "Assets/Scenes/CombatHudWeaponDebug.unity";
        private const string AssetFolder = "Assets/Settings/Combat/HudDebug";
        private const string WeaponViewPrefabPath = "Assets/Prefabs/Prototype/FirstPersonWeaponView.prefab";
        private const string TemporaryArtIndexPath = "Assets/Art/SkillIndicators/Temporary/SO_IND_TemporaryArtIndex.asset";

        private static readonly DebugWeaponSpec[] DebugWeapons =
        {
            new DebugWeaponSpec(
                "HUD_Debug_FlyingSword",
                "Flying Sword",
                "Assets/Art/Weapons/HUD/Xianxia_FlyingSword.png",
                "IND_HUD_Debug_GroundCircle",
                SkillIndicatorShapeType.GroundCircle,
                SkillIndicatorDefaultReleasePolicy.CastAtCrosshairHit,
                8f,
                0.85f,
                1.7f,
                8f,
                90f),
            new DebugWeaponSpec(
                "HUD_Debug_MoonDao",
                "Moon Dao",
                "Assets/Art/Weapons/HUD/Xianxia_MoonDao.png",
                "IND_HUD_Debug_Line",
                SkillIndicatorShapeType.Line,
                SkillIndicatorDefaultReleasePolicy.CastAtCrosshairHit,
                9f,
                0.45f,
                1.2f,
                9f,
                90f),
            new DebugWeaponSpec(
                "HUD_Debug_RitualDagger",
                "Ritual Dagger",
                "Assets/Art/Weapons/HUD/Xianxia_RitualDagger.png",
                "IND_HUD_Debug_Cone",
                SkillIndicatorShapeType.Cone,
                SkillIndicatorDefaultReleasePolicy.CastAtCrosshairHit,
                7f,
                0.7f,
                2.4f,
                6.5f,
                72f),
        };

        [MenuItem("NewFPG/Combat/Create HUD Weapon Debug Scene")]
        public static void CreateHudWeaponDebugScene()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder(AssetFolder);
            CleanFirstPersonWeaponViewPrefab();

            WeaponDefinition[] weapons = CreateOrUpdateDebugWeapons();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "CombatHudWeaponDebug";

            Camera battleCamera = CreateBattleCamera();
            CreateLight();
            CreateGround();
            CreateAimTargets();

            GameObject player = CreatePlayerRig(weapons);
            PrototypeFirstPersonWeaponView weaponView = CreateWeaponView(battleCamera);
            PrototypeWeaponCombatHud weaponHud = weaponView.GetComponent<PrototypeWeaponCombatHud>();
            ConfigureWeaponHud(weaponHud, battleCamera);
            BindDebugPreview(weaponView, weaponHud, player, battleCamera);
            CreateBootstrap(battleCamera, weaponView, weaponHud, player);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            Debug.Log("Created HUD weapon debug scene at " + ScenePath + ".");
        }

        private static WeaponDefinition[] CreateOrUpdateDebugWeapons()
        {
            var weapons = new WeaponDefinition[DebugWeapons.Length];
            for (int i = 0; i < DebugWeapons.Length; i++)
            {
                DebugWeaponSpec spec = DebugWeapons[i];
                SkillIndicatorConfig indicator = CreateOrUpdateIndicator(spec);
                WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponPath(spec));
                if (weapon == null)
                {
                    weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                    AssetDatabase.CreateAsset(weapon, WeaponPath(spec));
                }

                SerializedObject serializedWeapon = new SerializedObject(weapon);
                serializedWeapon.FindProperty("displayName").stringValue = spec.displayName;
                serializedWeapon.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(spec.iconPath);
                serializedWeapon.FindProperty("resourceCost").floatValue = 0f;
                serializedWeapon.FindProperty("damage").floatValue = 0f;
                serializedWeapon.FindProperty("cooldown").floatValue = 0.12f;
                serializedWeapon.FindProperty("range").floatValue = spec.range;
                serializedWeapon.FindProperty("radius").floatValue = spec.radius;
                serializedWeapon.FindProperty("indicatorConfig").objectReferenceValue = indicator;
                serializedWeapon.FindProperty("releaseEffectPrefab").objectReferenceValue = null;
                serializedWeapon.FindProperty("hitEffectPrefab").objectReferenceValue = null;
                serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weapon);
                weapons[i] = weapon;
            }

            return weapons;
        }

        private static SkillIndicatorConfig CreateOrUpdateIndicator(DebugWeaponSpec spec)
        {
            SkillIndicatorConfig indicator = AssetDatabase.LoadAssetAtPath<SkillIndicatorConfig>(IndicatorPath(spec));
            if (indicator == null)
            {
                indicator = ScriptableObject.CreateInstance<SkillIndicatorConfig>();
                AssetDatabase.CreateAsset(indicator, IndicatorPath(spec));
            }

            SerializedObject serializedIndicator = new SerializedObject(indicator);
            serializedIndicator.FindProperty("abilityId").stringValue = spec.weaponAssetName;
            serializedIndicator.FindProperty("indicatorId").stringValue = spec.indicatorAssetName;
            serializedIndicator.FindProperty("ownerType").enumValueIndex = (int)SkillIndicatorOwnerType.Player;
            serializedIndicator.FindProperty("inputMode").enumValueIndex = (int)SkillIndicatorInputMode.HoldPreview;
            serializedIndicator.FindProperty("tapPolicy").enumValueIndex = (int)SkillIndicatorDefaultReleasePolicy.AutoSelectBestTarget;
            serializedIndicator.FindProperty("holdPolicy").enumValueIndex = (int)spec.holdPolicy;
            serializedIndicator.FindProperty("invalidReleasePolicy").enumValueIndex = (int)SkillIndicatorInvalidReleasePolicy.FallbackToDefault;
            serializedIndicator.FindProperty("aimSource").enumValueIndex = (int)SkillIndicatorAimSource.ScreenCursorRay;
            serializedIndicator.FindProperty("requireSurfaceHit").boolValue = true;
            serializedIndicator.FindProperty("clampToRange").boolValue = true;
            serializedIndicator.FindProperty("placementMode").enumValueIndex = (int)SkillIndicatorPlacementMode.GroundSurface;
            serializedIndicator.FindProperty("surfaceMask").intValue = ResolveSceneSurfaceMask();
            serializedIndicator.FindProperty("collisionMask").intValue = 0;
            serializedIndicator.FindProperty("shapeType").enumValueIndex = (int)spec.shapeType;
            serializedIndicator.FindProperty("range").floatValue = spec.range;
            serializedIndicator.FindProperty("radius").floatValue = spec.radius;
            serializedIndicator.FindProperty("width").floatValue = spec.width;
            serializedIndicator.FindProperty("length").floatValue = spec.length;
            serializedIndicator.FindProperty("angle").floatValue = spec.angle;
            serializedIndicator.FindProperty("height").floatValue = 1.8f;
            serializedIndicator.FindProperty("groundOffset").floatValue = 0.06f;
            serializedIndicator.FindProperty("tapMaxDuration").floatValue = 0.16f;
            serializedIndicator.FindProperty("holdEnterDelay").floatValue = 0.12f;
            serializedIndicator.FindProperty("castDelay").floatValue = 0f;
            serializedIndicator.FindProperty("warningTime").floatValue = 0f;
            serializedIndicator.FindProperty("duration").floatValue = 0f;
            serializedIndicator.FindProperty("fadeOut").floatValue = 0.12f;
            serializedIndicator.FindProperty("previewPrefabResourceId").stringValue = string.Empty;
            serializedIndicator.FindProperty("validMaterialResourceId").stringValue = "M_IND_OwnerValid";
            serializedIndicator.FindProperty("invalidMaterialResourceId").stringValue = "M_IND_Invalid";
            serializedIndicator.FindProperty("confirmAudioResourceId").stringValue = "S_IND_ConfirmRelease";
            serializedIndicator.FindProperty("invalidAudioResourceId").stringValue = "S_IND_Invalid";
            serializedIndicator.FindProperty("debugDraw").boolValue = false;
            serializedIndicator.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(indicator);
            return indicator;
        }

        private static Camera CreateBattleCamera()
        {
            GameObject cameraObject = new GameObject("BattleCamera", typeof(Camera), typeof(UniversalAdditionalCameraData));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.55f, -5.2f), Quaternion.Euler(9f, 0f, 0f));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 64f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 80f;
            camera.depth = 0f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            return camera;
        }

        private static void CreateLight()
        {
            GameObject lightObject = new GameObject("Main Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.9f, 1f);
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Indicator Aim Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(2.6f, 1f, 2.6f);
            Renderer renderer = ground.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial("HUD Debug Ground", new Color(0.13f, 0.16f, 0.14f, 1f));
        }

        private static void CreateAimTargets()
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                target.name = "HUD Debug Target " + (i + 1).ToString();
                target.transform.position = new Vector3((i - 1) * 1.6f, 0.85f, 3.2f + i * 0.7f);
                target.transform.localScale = new Vector3(0.58f, 0.85f, 0.58f);
                target.GetComponent<Renderer>().sharedMaterial = CreateMaterial("HUD Debug Target Material " + i.ToString(), new Color(0.62f, 0.18f, 0.16f, 1f));
            }
        }

        private static GameObject CreatePlayerRig(WeaponDefinition[] weapons)
        {
            GameObject player = new GameObject("HUD Debug Player", typeof(CombatVitals), typeof(CombatResourcePool), typeof(PlayerWeaponCaster));
            player.transform.position = Vector3.zero;

            CombatVitals vitals = player.GetComponent<CombatVitals>();
            SerializedObject serializedVitals = new SerializedObject(vitals);
            serializedVitals.FindProperty("maxHealth").floatValue = 100f;
            serializedVitals.FindProperty("startingHealth").floatValue = 100f;
            serializedVitals.FindProperty("maxShield").floatValue = 50f;
            serializedVitals.FindProperty("startingShield").floatValue = 50f;
            serializedVitals.FindProperty("destroyOnDeath").boolValue = false;
            serializedVitals.ApplyModifiedPropertiesWithoutUndo();

            CombatResourcePool resourcePool = player.GetComponent<CombatResourcePool>();
            SerializedObject serializedResource = new SerializedObject(resourcePool);
            serializedResource.FindProperty("maxResource").floatValue = 100f;
            serializedResource.FindProperty("startingResource").floatValue = 100f;
            serializedResource.FindProperty("recoveryPerSecond").floatValue = 100f;
            serializedResource.FindProperty("recoverOverTime").boolValue = true;
            serializedResource.ApplyModifiedPropertiesWithoutUndo();

            PlayerWeaponCaster caster = player.GetComponent<PlayerWeaponCaster>();
            SerializedObject serializedCaster = new SerializedObject(caster);
            SerializedProperty casterWeapons = serializedCaster.FindProperty("weapons");
            casterWeapons.arraySize = weapons.Length;
            for (int i = 0; i < weapons.Length; i++)
            {
                casterWeapons.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
            }

            serializedCaster.FindProperty("resourcePool").objectReferenceValue = resourcePool;
            serializedCaster.FindProperty("castOrigin").objectReferenceValue = player.transform;
            serializedCaster.FindProperty("targetMask").intValue = ~0;
            serializedCaster.FindProperty("allowKeyboardShortcuts").boolValue = true;
            serializedCaster.FindProperty("combatEnabled").boolValue = true;
            serializedCaster.ApplyModifiedPropertiesWithoutUndo();
            return player;
        }

        private static PrototypeFirstPersonWeaponView CreateWeaponView(Camera battleCamera)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponViewPrefabPath);
            GameObject weaponViewObject = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
                : new GameObject("FirstPersonWeaponView", typeof(PrototypeFirstPersonWeaponView), typeof(PrototypeWeaponCombatHud));
            weaponViewObject.name = "HUD Debug FirstPersonWeaponView";
            if (PrefabUtility.IsPartOfPrefabInstance(weaponViewObject))
            {
                PrefabUtility.UnpackPrefabInstance(weaponViewObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            PrototypeFirstPersonWeaponView weaponView = weaponViewObject.GetComponent<PrototypeFirstPersonWeaponView>();
            if (weaponView == null)
            {
                weaponView = weaponViewObject.AddComponent<PrototypeFirstPersonWeaponView>();
            }

            SerializedObject serializedView = new SerializedObject(weaponView);
            serializedView.FindProperty("worldCamera").objectReferenceValue = battleCamera;
            serializedView.FindProperty("parentModuleToWorldCamera").boolValue = true;
            serializedView.FindProperty("useUrpCameraStack").boolValue = true;
            serializedView.FindProperty("rebuildOnEnable").boolValue = true;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            if (weaponViewObject.GetComponent<PrototypeWeaponCombatHud>() == null)
            {
                weaponViewObject.AddComponent<PrototypeWeaponCombatHud>();
            }

            weaponView.EnsureWeaponView();
            return weaponView;
        }

        private static void CleanFirstPersonWeaponViewPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponViewPrefabPath);
            if (prefab == null)
            {
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(WeaponViewPrefabPath);
            PrototypeFirstPersonWeaponView weaponView = prefabRoot.GetComponent<PrototypeFirstPersonWeaponView>();
            if (weaponView != null)
            {
                SerializedObject serializedView = new SerializedObject(weaponView);
                serializedView.FindProperty("worldCamera").objectReferenceValue = null;
                serializedView.FindProperty("parentModuleToWorldCamera").boolValue = true;
                serializedView.FindProperty("useUrpCameraStack").boolValue = true;
                serializedView.FindProperty("rebuildOnEnable").boolValue = true;
                serializedView.ApplyModifiedPropertiesWithoutUndo();
            }

            Transform weaponCameraTransform = prefabRoot.transform.Find("FirstPersonWeaponCamera");
            Camera weaponCamera = weaponCameraTransform != null ? weaponCameraTransform.GetComponent<Camera>() : null;
            if (weaponCamera != null)
            {
                weaponCamera.clearFlags = CameraClearFlags.Depth;
                weaponCamera.depth = 10f;
                weaponCamera.nearClipPlane = 0.01f;
                weaponCamera.farClipPlane = 8f;
                weaponCamera.fieldOfView = 42f;
                UniversalAdditionalCameraData cameraData = weaponCamera.GetUniversalAdditionalCameraData();
                cameraData.renderType = CameraRenderType.Overlay;
                cameraData.renderPostProcessing = false;
                EditorUtility.SetDirty(weaponCamera);
                EditorUtility.SetDirty(cameraData);
            }

            Transform rig = prefabRoot.transform.Find("FirstPersonWeaponRig");
            if (rig != null)
            {
                for (int i = rig.childCount - 1; i >= 0; i--)
                {
                    Transform child = rig.GetChild(i);
                    if (child == null)
                    {
                        continue;
                    }

                    bool generatedWeapon = child.GetComponent<Renderer>() != null
                        || child.name.EndsWith(" Pointer Hitbox", System.StringComparison.Ordinal);
                    if (generatedWeapon)
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, WeaponViewPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        private static void ConfigureWeaponHud(PrototypeWeaponCombatHud weaponHud, Camera battleCamera)
        {
            if (weaponHud == null)
            {
                return;
            }

            SerializedObject serializedHud = new SerializedObject(weaponHud);
            serializedHud.FindProperty("aimCamera").objectReferenceValue = battleCamera;
            serializedHud.FindProperty("temporaryArtIndex").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SkillIndicatorTemporaryArtIndex>(TemporaryArtIndexPath);
            serializedHud.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindDebugPreview(
            PrototypeFirstPersonWeaponView weaponView,
            PrototypeWeaponCombatHud weaponHud,
            GameObject player,
            Camera battleCamera)
        {
            if (weaponView != null)
            {
                weaponView.SetWorldCamera(battleCamera);
                weaponView.RefreshRuntimeView(battleCamera);
            }

            if (weaponHud == null || player == null)
            {
                return;
            }

            CombatVitals vitals = player.GetComponent<CombatVitals>();
            CombatResourcePool resourcePool = player.GetComponent<CombatResourcePool>();
            PlayerWeaponCaster caster = player.GetComponent<PlayerWeaponCaster>();
            weaponHud.Bind(vitals, resourcePool, caster);
            weaponHud.SetCombatEnabled(true);

            if (caster != null)
            {
                caster.SetCombatEnabled(true);
            }
        }

        private static void CreateBootstrap(
            Camera battleCamera,
            PrototypeFirstPersonWeaponView weaponView,
            PrototypeWeaponCombatHud weaponHud,
            GameObject player)
        {
            GameObject bootstrapObject = new GameObject("HUD Debug Bootstrap", typeof(CombatHudDebugBootstrap));
            SerializedObject serializedBootstrap = new SerializedObject(bootstrapObject.GetComponent<CombatHudDebugBootstrap>());
            serializedBootstrap.FindProperty("battleCamera").objectReferenceValue = battleCamera;
            serializedBootstrap.FindProperty("weaponView").objectReferenceValue = weaponView;
            serializedBootstrap.FindProperty("weaponHud").objectReferenceValue = weaponHud;
            serializedBootstrap.FindProperty("playerVitals").objectReferenceValue = player.GetComponent<CombatVitals>();
            serializedBootstrap.FindProperty("resourcePool").objectReferenceValue = player.GetComponent<CombatResourcePool>();
            serializedBootstrap.FindProperty("weaponCaster").objectReferenceValue = player.GetComponent<PlayerWeaponCaster>();
            serializedBootstrap.FindProperty("keepResourceFull").boolValue = true;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                name = name,
                color = color,
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private static int ResolveSceneSurfaceMask()
        {
            int mask = ~0;
            int firstPersonWeaponLayer = LayerMask.NameToLayer("FirstPersonWeapon");
            if (firstPersonWeaponLayer >= 0)
            {
                mask &= ~(1 << firstPersonWeaponLayer);
            }

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                mask &= ~(1 << uiLayer);
            }

            return mask;
        }

        private static string WeaponPath(DebugWeaponSpec spec)
        {
            return AssetFolder + "/" + spec.weaponAssetName + ".asset";
        }

        private static string IndicatorPath(DebugWeaponSpec spec)
        {
            return AssetFolder + "/" + spec.indicatorAssetName + ".asset";
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

        private readonly struct DebugWeaponSpec
        {
            public readonly string weaponAssetName;
            public readonly string displayName;
            public readonly string iconPath;
            public readonly string indicatorAssetName;
            public readonly SkillIndicatorShapeType shapeType;
            public readonly SkillIndicatorDefaultReleasePolicy holdPolicy;
            public readonly float range;
            public readonly float radius;
            public readonly float width;
            public readonly float length;
            public readonly float angle;

            public DebugWeaponSpec(
                string weaponAssetName,
                string displayName,
                string iconPath,
                string indicatorAssetName,
                SkillIndicatorShapeType shapeType,
                SkillIndicatorDefaultReleasePolicy holdPolicy,
                float range,
                float radius,
                float width,
                float length,
                float angle)
            {
                this.weaponAssetName = weaponAssetName;
                this.displayName = displayName;
                this.iconPath = iconPath;
                this.indicatorAssetName = indicatorAssetName;
                this.shapeType = shapeType;
                this.holdPolicy = holdPolicy;
                this.range = range;
                this.radius = radius;
                this.width = width;
                this.length = length;
                this.angle = angle;
            }
        }
    }
}
