using System;
using System.Collections.Generic;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Editor.LevelAuthoring
{
    public static class FpgFormalRoomLoopInstaller
    {
        private const string BootScenePath = "Assets/FPGDemo/Scenes/Boot.unity";
        private const string CombatLabScenePath = "Assets/FPGDemo/Scenes/CombatLab.unity";
        private const string FormalRoomScenePath = "Assets/FPGDemo/Scenes/FormalRoom.unity";
        private const string RoomPath = "Assets/FPGDemo/Config/Level/Rooms/Room_combatlab-forest.asset";
        private const string ConfigPath = "Assets/FPGDemo/Config/GameBootstrapConfig.asset";
        private const string RoomCatalogPath =
            "Assets/FPGDemo/Config/Level/FPG_RoomCatalog.asset";
        private const string ExitRoomRefreshRulePath =
            "Assets/FPGDemo/Config/Level/FPG_ExitRoomRefreshRule.asset";
        private const string PlayableCharacterCatalogPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_PlayableCharacterCatalog.asset";
        private const string FeiCharacterPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei.asset";
        private const string FeiThreeCPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_3C.asset";
        private const string FeiSelectionPreviewPath =
            "Assets/FPGDemo/Presentation/D0Slice/Spine/D0_Fei_30048_StraightAlpha.prefab";
        private const string CombatPresentationProfilePath =
            "Assets/FPGDemo/Config/D0Slice/CombatPresentationProfile.asset";
        private const string ProfilePath = "Assets/FPGDemo/Config/FormalEncounter/Level1/FPG_L1_01_Profile.asset";
        private const string OverridePath = "Assets/FPGDemo/Config/FormalEncounter/Level1/FPG_L1_01_01_Intro.asset";
        private const string EnemyCatalogPath = "Assets/FPGDemo/Config/FormalEncounter/FPG_NormalRoom_EnemyCatalog.asset";
        private const string AttackCatalogPath = "Assets/FPGDemo/Config/FormalEncounter/FPG_NormalRoom_AttackRuntimeCatalog.asset";
        private const string PresentationRoot = "Assets/FPGDemo/Presentation/FormalEncounter";
        private const string ExitPrefabPath = PresentationRoot + "/PF_FPG_RoomExit.prefab";
        private const string HealthBarPrefabPath = PresentationRoot + "/PF_FPG_OverheadHealthBar.prefab";
        private const string DamagePopupPrefabPath =
            PresentationRoot + "/PF_FPG_DamagePopup.prefab";
        private const string HitTipArtRoot = "Assets/Art/HUD/Hit_tip";
        private const string HitTipNormalDigits = HitTipArtRoot + "/zi_normal";
        private const string HitTipCriticalDigits = HitTipArtRoot + "/zi_critcal";
        private const string HitTipElementalDigits = HitTipArtRoot + "/zi_elemental";
        private const string HitTipNormalBackground =
            HitTipArtRoot + "/di_nomal&critical.png";
        private const string HitTipElementalBackground =
            HitTipArtRoot + "/di_elemental.png";
        private const string MaterialRoot = PresentationRoot + "/Materials";
        private const string BootMaterialPath = MaterialRoot + "/M_FPG_BootEntrance.mat";
        private const string FrameMaterialPath = MaterialRoot + "/M_FPG_BootFrame.mat";
        private const string ExitMaterialPath = MaterialRoot + "/M_FPG_RoomExit.mat";

        private const int BlockerLayer = 28;
        private const int HitboxLayer = 29;
        private const int BlockerMask = 1 << BlockerLayer;
        private const int HitboxMask = 1 << HitboxLayer;
        private const int DamagePopupDigitCapacity = 10;
        private const string InstallationMarkerName = "__FormalFirst_v2";

        [MenuItem("FPG Demo/Formal Encounter/Install Boot Formal Room Loop", priority = 131)]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Formal room installation requires Edit Mode.");
            }

            EnsureNoDirtyScenes();
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsureFolder(PresentationRoot);
                EnsureFolder(MaterialRoot);
                FpgFormalEncounterDefaultsInstaller.Install();
                EnsureFormalPresentationProfileSerialized();

                FpgRoomDefinition room = LoadRequired<FpgRoomDefinition>(RoomPath);
                ConfigureRoom(room);
                AssetDatabase.SaveAssets();
                room = LoadRequired<FpgRoomDefinition>(RoomPath);
                FpgExitRoomRefreshRule exitRoomRefreshRule =
                    EnsureExitRoomRefreshRule(room);


                Material bootMaterial = EnsureMaterial(
                    BootMaterialPath,
                    new Color(0.05f, 0.75f, 0.95f, 1f));
                Material frameMaterial = EnsureMaterial(
                    FrameMaterialPath,
                    new Color(0.035f, 0.055f, 0.075f, 1f));
                Material exitMaterial = EnsureMaterial(
                    ExitMaterialPath,
                    new Color(0.9f, 0.15f, 0.1f, 1f));

                GameObject exitPrefab = CreateExitPrefab(exitMaterial);
                FpgOverheadHealthBarView healthBarPrefab = CreateHealthBarPrefab();
                FpgDamagePopupView damagePopupPrefab =
                    CreateDamagePopupPrefab();
                FpgPlayableCharacterCatalog playableCharacterCatalog =
                    EnsurePlayableCharacterCatalog();
                ConfigureBootstrapConfig(exitRoomRefreshRule);
                if (!HasCurrentSceneInstallation(playableCharacterCatalog))
                {
                    BuildFormalRoomScene(
                        room,
                        exitPrefab,
                        healthBarPrefab,
                        damagePopupPrefab,
                        playableCharacterCatalog);
                    ConfigureBootScene(
                        room,
                        bootMaterial,
                        frameMaterial,
                        playableCharacterCatalog);
                }
                EnsureBuildSettings();
                ValidateInstallation(room, playableCharacterCatalog);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    "[FPG Formal Room] Boot shot entrance, L1_01 formal encounter, "
                    + "room-clear exit unlock and build settings are installed.");
            }
            finally
            {
                if (previousSetup != null && previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }
        }

        [MenuItem("FPG Demo/Formal Encounter/Refresh Formal HUD Assets", priority = 132)]
        public static void RefreshFormalHudAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Formal HUD refresh requires Edit Mode.");
            }

            EnsureNoDirtyScenes();
            SceneSetup[] previousSetup =
                EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsureFolder(PresentationRoot);
                EnsureFormalPresentationProfileSerialized();
                FpgOverheadHealthBarView healthBarPrefab =
                    CreateHealthBarPrefab();
                FpgDamagePopupView damagePopupPrefab =
                    CreateDamagePopupPrefab();
                RefreshFormalRoomHudScene(
                    healthBarPrefab,
                    damagePopupPrefab);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    "[FPG Formal Room] RectTransform HUD bars and Sprite damage popups refreshed.");
            }
            finally
            {
                if (previousSetup != null && previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }
        }

        [MenuItem(
            "FPG Demo/Formal Encounter/Refresh Exit Room Flow Assets",
            priority = 133)]
        public static void RefreshExitRoomFlowAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Exit room flow refresh requires Edit Mode.");
            }

            EnsureNoDirtyScenes();
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsureFolder(PresentationRoot);
                EnsureFolder(MaterialRoot);
                FpgRoomDefinition room =
                    LoadRequired<FpgRoomDefinition>(RoomPath);
                FpgExitRoomRefreshRule rule =
                    EnsureExitRoomRefreshRule(room);
                Material exitMaterial = EnsureMaterial(
                    ExitMaterialPath,
                    new Color(0.9f, 0.15f, 0.1f, 1f));
                GameObject exitPrefab = CreateExitPrefab(exitMaterial);

                GameBootstrapConfig bootstrapConfig =
                    LoadRequired<GameBootstrapConfig>(ConfigPath);
                SerializedObject configData =
                    new SerializedObject(bootstrapConfig);
                SetObject(configData, "exitRoomRefreshRule", rule);
                configData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bootstrapConfig);
                AssetDatabase.SaveAssets();
                exitPrefab = LoadRequired<GameObject>(ExitPrefabPath);
                bool ruleValid =
                    rule.TryValidate(out string ruleError);
                bool prefabValid =
                    TryValidateExitPrefab(exitPrefab, out string prefabError);
                if (!ruleValid || !prefabValid)
                {
                    throw new InvalidOperationException(
                        !string.IsNullOrWhiteSpace(ruleError)
                            ? ruleError
                            : prefabError);
                }

                Scene formalScene = EditorSceneManager.OpenScene(
                    FormalRoomScenePath,
                    OpenSceneMode.Single);
                FpgRoomEncounterDirector director =
                    FindSingleSceneComponent<FpgRoomEncounterDirector>(
                        formalScene);
                HitboxRegistry registry =
                    FindSingleSceneComponent<HitboxRegistry>(formalScene);
                SerializedObject directorData = new SerializedObject(director);
                SetObject(directorData, "exitHitboxRegistry", registry);
                directorData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(director);
                EditorSceneManager.MarkSceneDirty(formalScene);
                EditorSceneManager.SaveScene(formalScene);

                if (GetSerializedReference<HitboxRegistry>(
                        director,
                        "exitHitboxRegistry") != registry
                    || bootstrapConfig.ExitRoomRefreshRule != rule)
                {
                    throw new InvalidOperationException(
                        "Exit room flow references are incomplete.");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    "[FPG Formal Room] Exit catalog, refresh rule, prefab "
                    + "and FormalRoom bindings are refreshed.");
            }
            finally
            {
                if (previousSetup != null && previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }
        }

        private static void ConfigureRoom(FpgRoomDefinition room)
        {
            SerializedObject data = new SerializedObject(room);

            SerializedProperty exits = Required(data, "exitSlots");
            exits.arraySize = 1;
            ConfigureMarker(
                exits.GetArrayElementAtIndex(0),
                "exit-main",
                "Main Exit",
                new Vector3(0f, 1.5f, 20.5f),
                Vector3.zero);

            SerializedProperty spawns = Required(data, "enemySpawnPoints");
            spawns.arraySize = 4;
            ConfigureSpawn(
                spawns.GetArrayElementAtIndex(0),
                "enemy-any-01",
                "Enemy Spawn 01",
                new Vector3(-3.5f, 1f, 10.5f));
            ConfigureSpawn(
                spawns.GetArrayElementAtIndex(1),
                "enemy-any-02",
                "Enemy Spawn 02",
                new Vector3(3.5f, 1f, 10.5f));
            ConfigureSpawn(
                spawns.GetArrayElementAtIndex(2),
                "enemy-any-03",
                "Enemy Spawn 03",
                new Vector3(-2f, 1f, 14f));
            ConfigureSpawn(
                spawns.GetArrayElementAtIndex(3),
                "enemy-any-04",
                "Enemy Spawn 04",
                new Vector3(2f, 1f, 14f));

            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(room);

            FpgRoomValidationResult validation = room.Validate();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    validation.FirstError == null
                        ? "Formal room definition is invalid."
                        : validation.FirstError.Message);
            }
        }

        private static void ConfigureMarker(
            SerializedProperty marker,
            string markerId,
            string displayName,
            Vector3 localPosition,
            Vector3 localEulerAngles)
        {
            Required(marker, "markerId").stringValue = markerId;
            Required(marker, "displayName").stringValue = displayName;
            Required(marker, "localPosition").vector3Value = localPosition;
            Required(marker, "localEulerAngles").vector3Value = localEulerAngles;
        }

        private static void ConfigureSpawn(
            SerializedProperty marker,
            string markerId,
            string displayName,
            Vector3 localPosition)
        {
            ConfigureMarker(
                marker,
                markerId,
                displayName,
                localPosition,
                new Vector3(0f, 180f, 0f));
            Required(marker, "role").intValue = 0;
        }

        private static FpgExitRoomRefreshRule EnsureExitRoomRefreshRule(
            FpgRoomDefinition room)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            EnsureFolder("Assets/FPGDemo/Config/Level");
            FpgRoomCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgRoomCatalog>(RoomCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<FpgRoomCatalog>();
                AssetDatabase.CreateAsset(catalog, RoomCatalogPath);
            }

            EnsureCatalogContainsRoom(catalog, room);
            if (!catalog.TryValidate(out string catalogError))
            {
                throw new InvalidOperationException(catalogError);
            }

            FpgExitRoomRefreshRule rule =
                AssetDatabase.LoadAssetAtPath<FpgExitRoomRefreshRule>(
                    ExitRoomRefreshRulePath);
            if (rule == null)
            {
                rule = ScriptableObject.CreateInstance<FpgExitRoomRefreshRule>();
                AssetDatabase.CreateAsset(rule, ExitRoomRefreshRulePath);
            }

            SerializedObject ruleData = new SerializedObject(rule);
            SetObject(ruleData, "roomCatalog", catalog);
            ruleData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rule);
            if (!rule.TryValidate(out string ruleError))
            {
                throw new InvalidOperationException(ruleError);
            }

            AssetDatabase.SaveAssetIfDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(rule);
            return rule;
        }

        private static void EnsureCatalogContainsRoom(
            FpgRoomCatalog catalog,
            FpgRoomDefinition room)
        {
            if (ContainsRoom(catalog, room))
            {
                return;
            }

            UnityEngine.Object[] entries =
                new UnityEngine.Object[catalog.Count + 1];
            for (int index = 0; index < catalog.Count; index++)
            {
                entries[index] = catalog.Rooms[index];
            }

            entries[entries.Length - 1] = room;
            SerializedObject catalogData = new SerializedObject(catalog);
            SetObjectArray(catalogData, "rooms", entries);
            catalogData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static bool ContainsRoom(
            FpgRoomCatalog catalog,
            FpgRoomDefinition room)
        {
            if (catalog == null || room == null)
            {
                return false;
            }

            for (int index = 0; index < catalog.Rooms.Count; index++)
            {
                if (catalog.Rooms[index] == room)
                {
                    return true;
                }
            }

            return false;
        }

        private static FpgPlayableCharacterCatalog EnsurePlayableCharacterCatalog()
        {
            D0CharacterDefinition feiCharacter =
                LoadRequired<D0CharacterDefinition>(FeiCharacterPath);
            D0ThreeCProfile feiThreeC =
                LoadRequired<D0ThreeCProfile>(FeiThreeCPath);
            GameObject feiPreview =
                LoadRequired<GameObject>(FeiSelectionPreviewPath);
            if (feiPreview.GetComponentInChildren<D0ActorEntityView>(true) != null)
            {
                throw new InvalidOperationException(
                    "Fei selection preview must be visual-only and contain no gameplay entity.");
            }

            FpgPlayableCharacterCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgPlayableCharacterCatalog>(
                    PlayableCharacterCatalogPath);
            if (catalog == null)
            {
                catalog =
                    ScriptableObject.CreateInstance<FpgPlayableCharacterCatalog>();
                AssetDatabase.CreateAsset(catalog, PlayableCharacterCatalogPath);
            }

            SerializedObject data = new SerializedObject(catalog);
            SetObject(data, "defaultCharacter", feiCharacter);
            SerializedProperty entries = Required(data, "entries");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            Required(entry, "character").objectReferenceValue = feiCharacter;
            Required(entry, "threeCProfile").objectReferenceValue = feiThreeC;
            Required(entry, "selectionPreviewPrefab").objectReferenceValue =
                feiPreview;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            if (!catalog.TryValidate(out string error))
            {
                throw new InvalidOperationException(error);
            }

            if (!catalog.TryResolveDefault(out _, out error))
            {
                throw new InvalidOperationException(error);
            }

            return catalog;
        }

        private static CombatPresentationProfile
            EnsureFormalPresentationProfileSerialized()
        {
            CombatPresentationProfile profile =
                LoadRequired<CombatPresentationProfile>(
                    CombatPresentationProfilePath);
            SerializedObject data = new SerializedObject(profile);
            data.Update();
            Required(data, "formalHudResources");
            SerializedProperty damagePopup =
                Required(data, "formalDamagePopup");
            EnsureDamagePopupSpriteStyles(damagePopup);
            Required(data, "formalReticle");
            data.ApplyModifiedPropertiesWithoutUndo();

            if (!profile.TryValidateStatic(out string error))
            {
                throw new InvalidOperationException(
                    "Formal combat presentation profile is invalid: " + error);
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        private static void EnsureDamagePopupSpriteStyles(
            SerializedProperty damagePopup)
        {
            SerializedProperty styles = Required(damagePopup, "spriteStyles");
            if (HasCompleteDamagePopupSpriteStyles(styles))
            {
                return;
            }

            styles.arraySize = 3;
            ConfigureDamagePopupSpriteStyle(
                styles.GetArrayElementAtIndex(0),
                CombatHitPresentationKind.Body,
                HitTipNormalBackground,
                HitTipNormalDigits);
            ConfigureDamagePopupSpriteStyle(
                styles.GetArrayElementAtIndex(1),
                CombatHitPresentationKind.Weakpoint,
                HitTipNormalBackground,
                HitTipCriticalDigits);
            ConfigureDamagePopupSpriteStyle(
                styles.GetArrayElementAtIndex(2),
                CombatHitPresentationKind.Intercept,
                HitTipElementalBackground,
                HitTipElementalDigits);
        }

        private static bool HasCompleteDamagePopupSpriteStyles(
            SerializedProperty styles)
        {
            if (styles == null || !styles.isArray || styles.arraySize != 3)
            {
                return false;
            }

            bool[] kinds = new bool[3];
            for (int index = 0; index < styles.arraySize; index++)
            {
                SerializedProperty style = styles.GetArrayElementAtIndex(index);
                int kind = Required(style, "kind").intValue;
                SerializedProperty digits = Required(style, "digitSprites");
                Vector2 minimumSize = Required(style, "backgroundMinSize").vector2Value;
                if (kind < 0 || kind >= kinds.Length || kinds[kind]
                    || Required(style, "backgroundSprite").objectReferenceValue == null
                    || !digits.isArray
                    || digits.arraySize != DamagePopupDigitCapacity
                    || Required(style, "digitHeight").floatValue <= 0f
                    || Required(style, "backgroundHorizontalPadding").floatValue < 0f
                    || minimumSize.x <= 0f || minimumSize.y <= 0f)
                {
                    return false;
                }

                for (int digit = 0; digit < digits.arraySize; digit++)
                {
                    if (digits.GetArrayElementAtIndex(digit).objectReferenceValue == null)
                    {
                        return false;
                    }
                }

                kinds[kind] = true;
            }

            return kinds[0] && kinds[1] && kinds[2];
        }

        private static void ConfigureDamagePopupSpriteStyle(
            SerializedProperty style,
            CombatHitPresentationKind kind,
            string backgroundPath,
            string digitsFolder)
        {
            Required(style, "kind").intValue = (int)kind;
            Required(style, "backgroundSprite").objectReferenceValue =
                LoadRequired<Sprite>(backgroundPath);
            SerializedProperty digits = Required(style, "digitSprites");
            digits.arraySize = DamagePopupDigitCapacity;
            for (int digit = 0; digit < digits.arraySize; digit++)
            {
                digits.GetArrayElementAtIndex(digit).objectReferenceValue =
                    LoadRequired<Sprite>(
                        digitsFolder + "/" + digit + ".png");
            }

            Required(style, "digitHeight").floatValue = 60f;
            Required(style, "digitSpacing").floatValue = -2f;
            Required(style, "backgroundHorizontalPadding").floatValue = 34f;
            Required(style, "backgroundMinSize").vector2Value =
                new Vector2(133f, 50f);
        }


        private static GameObject CreateExitPrefab(Material material)
        {
            Type textType = Type.GetType("UnityEngine.UI.Text, UnityEngine.UI");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (textType == null || font == null)
            {
                throw new InvalidOperationException(
                    "Formal room exit requires Unity UI Text and the built-in runtime font.");
            }

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "PF_FPG_RoomExit";
            root.layer = BlockerLayer;
            root.transform.localScale = new Vector3(4f, 3f, 0.45f);

            BoxCollider collider = root.GetComponent<BoxCollider>();
            collider.isTrigger = false;
            Renderer renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            GameObject labelCanvasObject = new GameObject(
                "DestinationLabelCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            labelCanvasObject.transform.SetParent(root.transform, false);
            RectTransform labelCanvasRect =
                (RectTransform)labelCanvasObject.transform;
            labelCanvasRect.localPosition = new Vector3(0f, 0.66f, -0.57f);
            labelCanvasRect.localRotation = Quaternion.identity;
            labelCanvasRect.localScale = Vector3.one * 0.003f;
            labelCanvasRect.sizeDelta = new Vector2(340f, 64f);
            Canvas labelCanvas = labelCanvasObject.GetComponent<Canvas>();
            labelCanvas.renderMode = RenderMode.WorldSpace;
            labelCanvas.sortingOrder = 30;

            Component destinationLabel = CreateHudText(
                labelCanvasRect,
                "DestinationLabel",
                string.Empty,
                Vector2.zero,
                labelCanvasRect.sizeDelta,
                30,
                TextAnchor.MiddleCenter,
                Color.white,
                textType,
                font);
            Stretch(
                (RectTransform)destinationLabel.transform,
                Vector2.zero,
                Vector2.zero);

            FpgRoomExitRuntime runtime = root.AddComponent<FpgRoomExitRuntime>();
            SerializedObject runtimeData = new SerializedObject(runtime);
            SetObject(runtimeData, "destinationLabel", destinationLabel);
            SetString(runtimeData, "destinationLabelPrefix", "\u524D\u5F80\uFF1A");
            runtimeData.ApplyModifiedPropertiesWithoutUndo();
            runtime.BindComponents(
                new Collider[] { collider },
                new Behaviour[] { labelCanvas });
            runtime.BindStatusRenderers(new Renderer[] { renderer });
            runtime.SetLocked(true);

            GameObject saved = null;
            try
            {
                saved = PrefabUtility.SaveAsPrefabAsset(root, ExitPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            if (saved == null)
            {
                throw new InvalidOperationException("Could not save formal room exit prefab.");
            }

            return saved;
        }

        private static FpgOverheadHealthBarView CreateHealthBarPrefab()
        {
            Type imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI");
            if (imageType == null)
            {
                throw new InvalidOperationException("Unity UI Image type is unavailable.");
            }

            GameObject root = new GameObject(
                "PF_FPG_OverheadHealthBar",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(FpgFormalBarView),
                typeof(FpgOverheadHealthBarView));
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(180f, 18f);
            rootRect.localScale = Vector3.one * 0.01f;

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            GameObject background = new GameObject("Background", typeof(RectTransform));
            background.transform.SetParent(root.transform, false);
            Stretch((RectTransform)background.transform, Vector2.zero, Vector2.zero);
            Component backgroundImage = background.AddComponent(imageType);
            SetImageColor(backgroundImage, new Color(0.02f, 0.025f, 0.03f, 0.92f));
            SetGraphicRaycastTarget(backgroundImage, false);

            GameObject fillArea = new GameObject(
                "FillArea",
                typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Stretch(
                (RectTransform)fillArea.transform,
                new Vector2(2f, 2f),
                new Vector2(-2f, -2f));
            GameObject fillObject = new GameObject("Fill", typeof(RectTransform));
            fillObject.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = (RectTransform)fillObject.transform;
            Stretch(fillRect, Vector2.zero, Vector2.zero);
            fillRect.pivot = new Vector2(0f, 0.5f);
            Component fillImage = fillObject.AddComponent(imageType);
            SetImageColor(fillImage, new Color(0.2f, 0.95f, 0.35f, 1f));
            SetGraphicRaycastTarget(fillImage, false);

            FpgFormalBarView bar = root.GetComponent<FpgFormalBarView>();
            SerializedObject barData = new SerializedObject(bar);
            SetObject(barData, "fillRect", fillRect);
            barData.ApplyModifiedPropertiesWithoutUndo();
            bar.SetNormalizedValue(1f);

            FpgOverheadHealthBarView view = root.GetComponent<FpgOverheadHealthBarView>();
            SerializedObject viewData = new SerializedObject(view);
            SetObject(viewData, "lifeBar", bar);
            SetVector3(viewData, "worldOffset", new Vector3(0f, 2.1f, 0f));
            viewData.ApplyModifiedPropertiesWithoutUndo();
            if (!view.TryValidate(out string viewError))
            {
                throw new InvalidOperationException(viewError);
            }

            GameObject saved = null;
            try
            {
                saved = PrefabUtility.SaveAsPrefabAsset(root, HealthBarPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            if (saved == null)
            {
                throw new InvalidOperationException("Could not save formal overhead health-bar prefab.");
            }

            FpgOverheadHealthBarView savedView = saved.GetComponent<FpgOverheadHealthBarView>();
            string savedViewError = string.Empty;
            if (savedView == null
                || !savedView.TryValidate(out savedViewError))
            {
                throw new InvalidOperationException(
                    savedView == null
                        ? "Saved overhead health-bar prefab has no view component."
                        : savedViewError);
            }

            return savedView;
        }

        private static FpgDamagePopupView CreateDamagePopupPrefab()
        {
            Type imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI");
            if (imageType == null)
            {
                throw new InvalidOperationException(
                    "Formal damage popup requires Unity UI Image.");
            }

            GameObject root = new GameObject(
                "PF_FPG_DamagePopup",
                typeof(RectTransform));
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(133f, 60f);

            GameObject backgroundObject = new GameObject(
                "Background",
                typeof(RectTransform));
            backgroundObject.transform.SetParent(rootRect, false);
            RectTransform backgroundRect =
                (RectTransform)backgroundObject.transform;
            Stretch(backgroundRect, Vector2.zero, Vector2.zero);
            Component background = backgroundObject.AddComponent(imageType);
            SerializedObject backgroundData = new SerializedObject(background);
            SetColor(backgroundData, "m_Color", Color.white);
            SetBool(backgroundData, "m_RaycastTarget", false);
            SetBool(backgroundData, "m_PreserveAspect", false);
            SetInt(backgroundData, "m_Type", 1);
            backgroundData.ApplyModifiedPropertiesWithoutUndo();

            GameObject digitsObject = new GameObject(
                "Digits",
                typeof(RectTransform));
            digitsObject.transform.SetParent(rootRect, false);
            RectTransform digitsRoot = (RectTransform)digitsObject.transform;
            digitsRoot.anchorMin = new Vector2(0.5f, 0.5f);
            digitsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            digitsRoot.pivot = new Vector2(0.5f, 0.5f);
            digitsRoot.anchoredPosition = Vector2.zero;
            digitsRoot.sizeDelta = Vector2.zero;

            Component[] digitImages =
                new Component[DamagePopupDigitCapacity];
            for (int digit = 0; digit < digitImages.Length; digit++)
            {
                GameObject digitObject = new GameObject(
                    "Digit_" + digit,
                    typeof(RectTransform));
                digitObject.transform.SetParent(digitsRoot, false);
                Component digitImage = digitObject.AddComponent(imageType);
                SerializedObject digitData = new SerializedObject(digitImage);
                SetColor(digitData, "m_Color", Color.white);
                SetBool(digitData, "m_RaycastTarget", false);
                SetBool(digitData, "m_PreserveAspect", true);
                SetInt(digitData, "m_Type", 0);
                digitData.ApplyModifiedPropertiesWithoutUndo();
                digitObject.SetActive(false);
                digitImages[digit] = digitImage;
            }

            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            FpgDamagePopupView view =
                root.AddComponent<FpgDamagePopupView>();
            SerializedObject viewData = new SerializedObject(view);
            SetObject(viewData, "root", rootRect);
            SetObject(viewData, "background", background);
            SetObject(viewData, "digitsRoot", digitsRoot);
            SerializedProperty digitReferences =
                Required(viewData, "digitImages");
            digitReferences.arraySize = digitImages.Length;
            for (int digit = 0; digit < digitImages.Length; digit++)
            {
                digitReferences.GetArrayElementAtIndex(digit)
                    .objectReferenceValue = digitImages[digit];
            }
            SetObject(viewData, "canvasGroup", canvasGroup);
            viewData.ApplyModifiedPropertiesWithoutUndo();
            if (!view.TryValidate(out string viewError))
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw new InvalidOperationException(viewError);
            }

            root.SetActive(false);
            GameObject saved = null;
            try
            {
                saved = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    DamagePopupPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            if (saved == null)
            {
                throw new InvalidOperationException(
                    "Could not save formal damage-popup prefab.");
            }

            FpgDamagePopupView savedView =
                saved.GetComponent<FpgDamagePopupView>();
            string savedViewError = string.Empty;
            if (savedView == null
                || !savedView.TryValidate(out savedViewError))
            {
                throw new InvalidOperationException(
                    savedView == null
                        ? "Saved formal damage-popup prefab has no view component."
                        : savedViewError);
            }

            return savedView;
        }

        private static void RefreshFormalRoomHudScene(
            FpgOverheadHealthBarView healthBarPrefab,
            FpgDamagePopupView damagePopupPrefab)
        {
            LoadRequired<SceneAsset>(FormalRoomScenePath);
            Scene scene = EditorSceneManager.OpenScene(
                FormalRoomScenePath,
                OpenSceneMode.Single);
            healthBarPrefab =
                LoadRequired<FpgOverheadHealthBarView>(HealthBarPrefabPath);
            damagePopupPrefab =
                LoadRequired<FpgDamagePopupView>(DamagePopupPrefabPath);
            FpgFormalEncounterHost formalHost =
                FindSingleSceneComponent<FpgFormalEncounterHost>(scene);
            FpgFormalPlayerPresentationBridge presentationBridge =
                FindSingleSceneComponent<FpgFormalPlayerPresentationBridge>(
                    scene);
            FpgFormalPlayerTickDriver playerDriver =
                FindSingleSceneComponent<FpgFormalPlayerTickDriver>(scene);
            FpgOverheadHealthBarPool healthBarPool =
                FindSingleSceneComponent<FpgOverheadHealthBarPool>(scene);
            FpgFormalPlayerHudPresenter currentHud =
                FindSingleSceneComponent<FpgFormalPlayerHudPresenter>(scene);
            FpgRoomEncounterDirector director =
                FindSingleSceneComponent<FpgRoomEncounterDirector>(scene);
            FpgFormalCombatPortFactory factory =
                FindSingleSceneComponent<FpgFormalCombatPortFactory>(scene);
            Camera worldCamera = FindSingleSceneComponent<Camera>(scene);
            CombatPresentationProfile presentationProfile =
                LoadRequired<CombatPresentationProfile>(
                    CombatPresentationProfilePath);

            Transform presentationRoot = formalHost.PresentationRoot;
            if (presentationRoot == null)
            {
                throw new InvalidOperationException(
                    "FormalRoom requires a presentation root before HUD refresh.");
            }

            List<FpgFormalCombatFeedbackBridge> feedbackBridges =
                FindSceneComponents<FpgFormalCombatFeedbackBridge>(scene);
            for (int index = 0; index < feedbackBridges.Count; index++)
            {
                UnityEngine.Object.DestroyImmediate(feedbackBridges[index]);
            }

            UnityEngine.Object.DestroyImmediate(currentHud.gameObject);
            CombatAimReticle aimReticle = CreateFormalPlayerHud(
                presentationRoot,
                presentationProfile,
                out FpgFormalPlayerHudPresenter playerHud,
                out Canvas targetCanvas,
                out RectTransform popupRoot);
            FpgFormalCombatFeedbackBridge feedbackBridge =
                presentationRoot.gameObject.AddComponent<
                    FpgFormalCombatFeedbackBridge>();
            ConfigureFormalCombatFeedbackBridge(
                feedbackBridge,
                director,
                playerDriver,
                aimReticle,
                presentationProfile,
                worldCamera,
                targetCanvas,
                popupRoot,
                damagePopupPrefab);

            SerializedObject driverData = new SerializedObject(playerDriver);
            SetObject(driverData, "aimViewportSource", aimReticle);
            driverData.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject bridgeData =
                new SerializedObject(presentationBridge);
            SetObject(bridgeData, "playerHud", playerHud);
            bridgeData.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject poolData = new SerializedObject(healthBarPool);
            SetObject(poolData, "viewPrefab", healthBarPrefab);
            poolData.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject factoryData = new SerializedObject(factory);
            ConfigureAttackQueryTechnicalSettings(factoryData);
            factoryData.ApplyModifiedPropertiesWithoutUndo();

            bool barValid =
                healthBarPrefab.TryValidate(out string barError);
            bool hudValid = playerHud.TryValidate(out string hudError);
            bool bridgeValid = presentationBridge.TryValidateAuthoring(
                out string bridgeError);
            bool feedbackValid = TryValidateFormalCombatFeedbackAuthoring(
                feedbackBridge,
                presentationRoot,
                director,
                playerDriver,
                aimReticle,
                presentationProfile,
                worldCamera,
                targetCanvas,
                damagePopupPrefab,
                out string feedbackError);
            if (!barValid || !hudValid || !bridgeValid || !feedbackValid)
            {
                string error = !string.IsNullOrWhiteSpace(barError)
                    ? barError
                    : !string.IsNullOrWhiteSpace(hudError)
                        ? hudError
                        : !string.IsNullOrWhiteSpace(bridgeError)
                            ? bridgeError
                            : feedbackError;
                throw new InvalidOperationException(error);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, FormalRoomScenePath))
            {
                throw new InvalidOperationException(
                    "Could not save the refreshed FormalRoom HUD.");
            }
        }

        private static void BuildFormalRoomScene(
            FpgRoomDefinition room,
            GameObject exitPrefab,
            FpgOverheadHealthBarView healthBarPrefab,
            FpgDamagePopupView damagePopupPrefab,
            FpgPlayableCharacterCatalog playableCharacterCatalog)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            new GameObject(InstallationMarkerName);
            room = LoadRequired<FpgRoomDefinition>(RoomPath);
            exitPrefab = LoadRequired<GameObject>(ExitPrefabPath);
            healthBarPrefab =
                LoadRequired<FpgOverheadHealthBarView>(HealthBarPrefabPath);
            damagePopupPrefab =
                LoadRequired<FpgDamagePopupView>(DamagePopupPrefabPath);
            playableCharacterCatalog =
                LoadRequired<FpgPlayableCharacterCatalog>(
                    PlayableCharacterCatalogPath);
            CombatPresentationProfile presentationProfile =
                LoadRequired<CombatPresentationProfile>(
                    CombatPresentationProfilePath);

            GameObject hostObject = new GameObject("__FormalRoom");
            FpgFormalEncounterHost formalSceneHost =
                hostObject.AddComponent<FpgFormalEncounterHost>();
            FpgEncounterHost encounterHost =
                hostObject.AddComponent<FpgEncounterHost>();

            Transform worldRoot = Child(hostObject.transform, "World");
            Transform actorsRoot = Child(hostObject.transform, "Actors");
            Transform cameraRoot = Child(hostObject.transform, "CameraRoot");
            Transform presentationRoot = Child(hostObject.transform, "Presentation");
            Transform servicesRoot = Child(hostObject.transform, "Services");
            Transform exitRoot = Child(hostObject.transform, "Exits");
            Transform entrySafetyAnchor =
                Child(hostObject.transform, "EntrySafetyAnchor");
            entrySafetyAnchor.localPosition = new Vector3(0f, 1.04f, 0f);

            FpgRoomInstance roomInstance =
                worldRoot.gameObject.AddComponent<FpgRoomInstance>();

            GameObject staticBlocker = new GameObject("FormalBackBoundary");
            staticBlocker.layer = BlockerLayer;
            staticBlocker.transform.SetParent(worldRoot, false);
            staticBlocker.transform.localPosition = new Vector3(0f, 2.5f, 22.5f);
            BoxCollider blockerCollider = staticBlocker.AddComponent<BoxCollider>();
            blockerCollider.size = new Vector3(18f, 5f, 0.5f);
            blockerCollider.isTrigger = false;

            Transform enemyPoolRoot = Child(actorsRoot, "EnemyPool");
            Transform healthBarRoot = Child(
                presentationRoot,
                "OverheadHealthBars");
            Transform projectileProxyRoot = Child(
                servicesRoot,
                "ProjectileProxies");

            Camera camera = CreateFormalCamera(cameraRoot);
            CreateFormalLight(cameraRoot);

            FpgRoomEncounterDirector director =
                servicesRoot.gameObject.AddComponent<FpgRoomEncounterDirector>();
            FpgEnemyEntityPool enemyPool =
                servicesRoot.gameObject.AddComponent<FpgEnemyEntityPool>();
            FPG.Demo.Unity.FpgCombatantAnchorMap anchorMap =
                servicesRoot.gameObject.AddComponent<
                    FPG.Demo.Unity.FpgCombatantAnchorMap>();
            FpgFormalHitboxRegistry formalHitboxes =
                servicesRoot.gameObject.AddComponent<FpgFormalHitboxRegistry>();
            FpgOverheadHealthBarPool healthBars =
                servicesRoot.gameObject.AddComponent<FpgOverheadHealthBarPool>();
            HitboxRegistry staticHitboxes =
                servicesRoot.gameObject.AddComponent<HitboxRegistry>();
            FpgFormalCombatPortFactory factory =
                servicesRoot.gameObject.AddComponent<FpgFormalCombatPortFactory>();
            FpgFormalPlayerTickDriver playerDriver =
                servicesRoot.gameObject.AddComponent<FpgFormalPlayerTickDriver>();
            FpgFormalPlayerComposer playerComposer =
                servicesRoot.gameObject.AddComponent<FpgFormalPlayerComposer>();

            FpgFormalPlayerCameraFeedback cameraFeedback =
                cameraRoot.gameObject.AddComponent<
                    FpgFormalPlayerCameraFeedback>();
            FpgFormalPlayerPresentationBridge presentationBridge =
                presentationRoot.gameObject.AddComponent<
                    FpgFormalPlayerPresentationBridge>();
            CombatAimReticle aimReticle = CreateFormalPlayerHud(
                presentationRoot,
                presentationProfile,
                out FpgFormalPlayerHudPresenter playerHud,
                out Canvas targetCanvas,
                out RectTransform popupRoot);
            FpgFormalCombatFeedbackBridge feedbackBridge =
                presentationRoot.gameObject.AddComponent<
                    FpgFormalCombatFeedbackBridge>();

            FpgEncounterProfile profile =
                LoadRequired<FpgEncounterProfile>(ProfilePath);
            FpgEncounterOverrideDefinition encounterOverride =
                LoadRequired<FpgEncounterOverrideDefinition>(OverridePath);
            FpgEnemyDefinitionCatalog enemyCatalog =
                LoadRequired<FpgEnemyDefinitionCatalog>(EnemyCatalogPath);
            FpgFormalAttackRuntimeCatalog attackCatalog =
                LoadRequired<FpgFormalAttackRuntimeCatalog>(AttackCatalogPath);

            ConfigureEnemyPool(enemyPool, enemyPoolRoot);
            ConfigureAnchorMap(anchorMap);
            ConfigureFormalHitboxes(formalHitboxes);
            ConfigureHealthBars(healthBars, healthBarPrefab, healthBarRoot);
            ConfigureStaticHitboxes(staticHitboxes, blockerCollider);
            ConfigureFactory(factory, staticHitboxes, projectileProxyRoot);
            ConfigurePlayerDriver(
                playerDriver,
                director,
                camera,
                aimReticle,
                cameraFeedback);
            ConfigureDirector(
                director,
                roomInstance,
                enemyPool,
                anchorMap,
                formalHitboxes,
                healthBars,
                staticHitboxes,
                camera,
                exitPrefab,
                exitRoot,
                entrySafetyAnchor,
                factory,
                playerDriver,
                attackCatalog);
            ConfigureCameraFeedback(cameraFeedback, cameraRoot, camera);
            ConfigurePresentationBridge(
                presentationBridge,
                director,
                playerDriver,
                playerHud,
                cameraFeedback,
                cameraRoot,
                camera);
            ConfigureFormalCombatFeedbackBridge(
                feedbackBridge,
                director,
                playerDriver,
                aimReticle,
                presentationProfile,
                camera,
                targetCanvas,
                popupRoot,
                damagePopupPrefab);
            ConfigurePlayerComposer(
                playerComposer,
                actorsRoot,
                presentationProfile,
                factory,
                playerDriver,
                director,
                presentationBridge);
            ConfigureFormalSceneHost(
                formalSceneHost,
                encounterHost,
                actorsRoot,
                cameraRoot,
                presentationRoot,
                director,
                enemyPool,
                anchorMap,
                playableCharacterCatalog,
                playerComposer,
                playerDriver,
                factory);
            ConfigureEncounterHost(
                encounterHost,
                room,
                profile,
                encounterOverride,
                enemyCatalog,
                attackCatalog,
                director);

            cameraRoot.gameObject.SetActive(false);
            presentationRoot.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, FormalRoomScenePath))
            {
                throw new InvalidOperationException(
                    "Could not save FormalRoom scene.");
            }
        }

        private static Camera CreateFormalCamera(Transform cameraRoot)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraRoot, false);
            cameraObject.transform.localPosition = new Vector3(0f, 7.5f, -12f);
            cameraObject.transform.localRotation = Quaternion.LookRotation(
                new Vector3(0f, 2.2f, 11f) - cameraObject.transform.localPosition,
                Vector3.up);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.045f, 1f);
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static void CreateFormalLight(Transform cameraRoot)
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(cameraRoot, false);
            lightObject.transform.localRotation = Quaternion.Euler(48f, -32f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.86f, 1f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
        }

        private static CombatAimReticle CreateFormalPlayerHud(
            Transform presentationRoot,
            CombatPresentationProfile presentationProfile,
            out FpgFormalPlayerHudPresenter presenter,
            out Canvas targetCanvas,
            out RectTransform popupRoot)
        {
            Type imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI");
            Type textType = Type.GetType("UnityEngine.UI.Text, UnityEngine.UI");
            Type scalerType =
                Type.GetType("UnityEngine.UI.CanvasScaler, UnityEngine.UI");
            if (imageType == null || textType == null || scalerType == null)
            {
                throw new InvalidOperationException(
                    "Formal player HUD requires Unity UI Image, Text and CanvasScaler.");
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                throw new InvalidOperationException(
                    "Formal player HUD could not load Unity's legacy runtime font.");
            }

            GameObject canvasObject = new GameObject(
                "FormalPlayerHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(FpgFormalPlayerHudPresenter));
            canvasObject.transform.SetParent(presentationRoot, false);
            targetCanvas = canvasObject.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 30;

            Component scaler = canvasObject.AddComponent(scalerType);
            SerializedObject scalerData = new SerializedObject(scaler);
            SetInt(scalerData, "m_UiScaleMode", 1);
            SetVector2(
                scalerData,
                "m_ReferenceResolution",
                new Vector2(1920f, 1080f));
            SetInt(scalerData, "m_ScreenMatchMode", 0);
            SetFloat(scalerData, "m_MatchWidthOrHeight", 0.5f);
            scalerData.ApplyModifiedPropertiesWithoutUndo();

            GameObject valuesObject = new GameObject(
                "PlayerValues",
                typeof(RectTransform));
            valuesObject.transform.SetParent(canvasObject.transform, false);
            RectTransform valuesRect = (RectTransform)valuesObject.transform;
            valuesRect.anchorMin = Vector2.zero;
            valuesRect.anchorMax = Vector2.zero;
            valuesRect.pivot = Vector2.zero;
            valuesRect.anchoredPosition = new Vector2(36f, 32f);
            valuesRect.sizeDelta = new Vector2(390f, 156f);

            CreateHudBar(
                valuesRect,
                "Life",
                new Vector2(0f, 102f),
                new Color(0.08f, 0.1f, 0.12f, 0.94f),
                new Color(0.2f, 0.95f, 0.42f, 1f),
                imageType,
                textType,
                font,
                out FpgFormalBarView lifeBar,
                out Component lifeText);
            CreateHudBar(
                valuesRect,
                "Barrier",
                new Vector2(0f, 64f),
                new Color(0.08f, 0.1f, 0.12f, 0.94f),
                new Color(0.25f, 0.8f, 1f, 1f),
                imageType,
                textType,
                font,
                out FpgFormalBarView barrierBar,
                out Component barrierText);
            CreateHudBar(
                valuesRect,
                "Ammo",
                new Vector2(0f, 26f),
                new Color(0.08f, 0.1f, 0.12f, 0.94f),
                new Color(1f, 0.76f, 0.18f, 1f),
                imageType,
                textType,
                font,
                out FpgFormalBarView ammoBar,
                out Component ammoText);
            Component stateText = CreateHudText(
                valuesRect,
                "CombatState",
                "PLAYER UNAVAILABLE",
                new Vector2(0f, 136f),
                new Vector2(390f, 20f),
                16,
                TextAnchor.MiddleLeft,
                new Color(0.86f, 0.92f, 0.96f, 1f),
                textType,
                font);

            GameObject reticleObject = new GameObject(
                "CombatAimReticle",
                typeof(RectTransform),
                typeof(CombatAimReticle));
            reticleObject.transform.SetParent(canvasObject.transform, false);
            RectTransform reticleRect = (RectTransform)reticleObject.transform;
            reticleRect.anchorMin = new Vector2(0.5f, 0.5f);
            reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
            reticleRect.pivot = new Vector2(0.5f, 0.5f);
            reticleRect.sizeDelta = new Vector2(30f, 30f);
            reticleRect.anchoredPosition = Vector2.zero;
            CreateReticleStroke(
                reticleRect,
                "Horizontal",
                new Vector2(30f, 2f),
                imageType);
            CreateReticleStroke(
                reticleRect,
                "Vertical",
                new Vector2(2f, 30f),
                imageType);

            GameObject popupRootObject = new GameObject(
                "DamagePopupRoot",
                typeof(RectTransform));
            popupRootObject.transform.SetParent(canvasObject.transform, false);
            popupRoot = (RectTransform)popupRootObject.transform;
            Stretch(popupRoot, Vector2.zero, Vector2.zero);
            popupRoot.SetAsLastSibling();

            presenter = canvasObject.GetComponent<FpgFormalPlayerHudPresenter>();
            SerializedObject presenterData = new SerializedObject(presenter);
            SetObject(presenterData, "lifeBar", lifeBar);
            SetObject(presenterData, "barrierBar", barrierBar);
            SetObject(presenterData, "ammoBar", ammoBar);
            SetObject(presenterData, "lifeText", lifeText);
            SetObject(presenterData, "barrierText", barrierText);
            SetObject(presenterData, "ammoText", ammoText);
            SetObject(presenterData, "stateText", stateText);
            SetObject(
                presenterData,
                "presentationProfile",
                presentationProfile);
            presenterData.ApplyModifiedPropertiesWithoutUndo();

            if (!presenter.TryValidate(out string hudError))
            {
                throw new InvalidOperationException(hudError);
            }

            CombatAimReticle reticle =
                reticleObject.GetComponent<CombatAimReticle>();
            SerializedObject reticleData = new SerializedObject(reticle);
            SetObject(reticleData, "sessionHost", null);
            SetFloat(reticleData, "pointerSensitivity", 1f);
            SetBool(reticleData, "lockSystemCursor", true);
            SetBool(reticleData, "resetOnApplicationFocus", true);
            reticleData.ApplyModifiedPropertiesWithoutUndo();
            if (!reticle.TryValidate(out string reticleError))
            {
                throw new InvalidOperationException(reticleError);
            }

            return reticle;
        }

        private static void CreateHudBar(
            RectTransform parent,
            string name,
            Vector2 anchoredPosition,
            Color backgroundColor,
            Color fillColor,
            Type imageType,
            Type textType,
            Font font,
            out FpgFormalBarView fillBar,
            out Component valueText)
        {
            GameObject backgroundObject = new GameObject(
                name + "Bar",
                typeof(RectTransform),
                typeof(FpgFormalBarView));
            backgroundObject.transform.SetParent(parent, false);
            RectTransform backgroundRect =
                (RectTransform)backgroundObject.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.zero;
            backgroundRect.pivot = Vector2.zero;
            backgroundRect.anchoredPosition = anchoredPosition;
            backgroundRect.sizeDelta = new Vector2(390f, 30f);
            Component backgroundImage = backgroundObject.AddComponent(imageType);
            SetImageColor(backgroundImage, backgroundColor);
            SetGraphicRaycastTarget(backgroundImage, false);

            GameObject fillAreaObject = new GameObject(
                "FillArea",
                typeof(RectTransform));
            fillAreaObject.transform.SetParent(backgroundObject.transform, false);
            Stretch(
                (RectTransform)fillAreaObject.transform,
                new Vector2(3f, 3f),
                new Vector2(-3f, -3f));

            GameObject fillObject = new GameObject(
                "Fill",
                typeof(RectTransform));
            fillObject.transform.SetParent(fillAreaObject.transform, false);
            RectTransform fillRect = (RectTransform)fillObject.transform;
            Stretch(fillRect, Vector2.zero, Vector2.zero);
            fillRect.pivot = new Vector2(0f, 0.5f);
            Component fillImage = fillObject.AddComponent(imageType);
            SetImageColor(fillImage, fillColor);
            SetGraphicRaycastTarget(fillImage, false);

            fillBar = backgroundObject.GetComponent<FpgFormalBarView>();
            SerializedObject barData = new SerializedObject(fillBar);
            SetObject(barData, "fillRect", fillRect);
            barData.ApplyModifiedPropertiesWithoutUndo();
            fillBar.SetNormalizedValue(0f);

            valueText = CreateHudText(
                backgroundRect,
                name + "Value",
                name.ToUpperInvariant() + " --",
                Vector2.zero,
                new Vector2(390f, 30f),
                15,
                TextAnchor.MiddleLeft,
                Color.white,
                textType,
                font);
            RectTransform textRect = (RectTransform)valueText.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);
        }

        private static Component CreateHudText(
            RectTransform parent,
            string name,
            string value,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            Color color,
            Type textType,
            Font font)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Component text = textObject.AddComponent(textType);
            SerializedObject textData = new SerializedObject(text);
            SetString(textData, "m_Text", value);
            SetColor(textData, "m_Color", color);
            SetBool(textData, "m_RaycastTarget", false);
            SerializedProperty fontData = Required(textData, "m_FontData");
            Required(fontData, "m_Font").objectReferenceValue = font;
            Required(fontData, "m_FontSize").intValue = fontSize;
            Required(fontData, "m_FontStyle").intValue = 1;
            Required(fontData, "m_Alignment").intValue = (int)alignment;
            Required(fontData, "m_HorizontalOverflow").intValue = 1;
            Required(fontData, "m_VerticalOverflow").intValue = 1;
            textData.ApplyModifiedPropertiesWithoutUndo();
            return text;
        }

        private static void CreateReticleStroke(
            RectTransform parent,
            string name,
            Vector2 size,
            Type imageType)
        {
            GameObject strokeObject = new GameObject(
                name,
                typeof(RectTransform));
            strokeObject.transform.SetParent(parent, false);
            RectTransform strokeRect = (RectTransform)strokeObject.transform;
            strokeRect.anchorMin = new Vector2(0.5f, 0.5f);
            strokeRect.anchorMax = new Vector2(0.5f, 0.5f);
            strokeRect.pivot = new Vector2(0.5f, 0.5f);
            strokeRect.anchoredPosition = Vector2.zero;
            strokeRect.sizeDelta = size;
            Component image = strokeObject.AddComponent(imageType);
            SetImageColor(image, new Color(0.2f, 0.9f, 1f, 0.95f));
            SetGraphicRaycastTarget(image, false);
        }

        private static void ConfigureEnemyPool(
            FpgEnemyEntityPool pool,
            Transform poolRoot)
        {
            SerializedObject data = new SerializedObject(pool);
            SetInt(data, "capacity", 24);
            SetObject(data, "poolRoot", poolRoot);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAnchorMap(FPG.Demo.Unity.FpgCombatantAnchorMap anchorMap)
        {
            SerializedObject data = new SerializedObject(anchorMap);
            SetInt(data, "capacity", 32);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureFormalHitboxes(
            FpgFormalHitboxRegistry registry)
        {
            SerializedObject data = new SerializedObject(registry);
            SetInt(data, "capacity", 64);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureHealthBars(
            FpgOverheadHealthBarPool pool,
            FpgOverheadHealthBarView prefab,
            Transform root)
        {
            SerializedObject data = new SerializedObject(pool);
            SetObject(data, "viewPrefab", prefab);
            SetObject(data, "viewRoot", root);
            SetInt(data, "capacity", 8);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureStaticHitboxes(
            HitboxRegistry registry,
            Collider blockerCollider)
        {
            SerializedObject data = new SerializedObject(registry);
            SerializedProperty bindings = Required(data, "staticBindings");
            bindings.arraySize = 1;
            SerializedProperty binding = bindings.GetArrayElementAtIndex(0);
            Required(binding, "enabled").boolValue = true;
            Required(binding, "collider").objectReferenceValue = blockerCollider;
            Required(binding, "targetReference").intValue = 0;
            Required(binding, "targetKind").intValue = 0;
            Required(binding, "hitPart").intValue = 0;
            Required(binding, "geometryId").intValue = 3001;
            Required(binding, "team").intValue = 0;
            Required(binding, "allowTrigger").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureFactory(
            FpgFormalCombatPortFactory factory,
            HitboxRegistry staticHitboxes,
            Transform projectileProxyRoot)
        {
            SerializedObject data = new SerializedObject(factory);
            SetObject(data, "playerDefinition", null);
            SetObject(data, "playerEntity", null);
            SetInt(data, "playerBodyGeometryId", 90001);
            SetObject(data, "staticHitboxRegistry", staticHitboxes);
            SetObject(data, "projectileProxyRoot", projectileProxyRoot);

            ConfigureAttackQueryTechnicalSettings(data);
            SetInt(data, "projectileWorldSettings.hitboxLayerMask", HitboxMask);
            SetInt(data, "projectileWorldSettings.blockerLayerMask", BlockerMask);

            SetInt(data, "enemyCapacity", 16);
            SetInt(data, "playerHitCommandCapacity", 64);
            SetInt(data, "attackScheduleCapacity", 128);
            SetInt(data, "projectileCapacity", 32);
            SetInt(data, "threatAdvanceCapacity", 64);
            SetInt(data, "perEnemyThreatCapacity", 8);
            SetInt(data, "summonCapacity", 16);
            SetInt(data, "maxTotalSummons", 16);
            SetInt(data, "maxSummonRecursionDepth", 2);
            SetInt(data, "attackPatternCapacity", 128);
            SetInt(data, "groggyDurationTicks", 120);
            SetInt(data, "projectileBudgetCapacity", 32);
            SetInt(data, "impactHistoryCapacity", 256);
            SetInt(data, "shotTargetHistoryCapacity", 256);
            SetInt(data, "impactQueueCapacity", 128);
            SetInt(data, "projectileReservationCapacity", 32);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAttackQueryTechnicalSettings(
            SerializedObject data)
        {
            SetInt(
                data,
                "attackQueryTechnicalSettings.hitboxLayerMask",
                HitboxMask);
            SetInt(
                data,
                "attackQueryTechnicalSettings.blockerLayerMask",
                BlockerMask);
        }

        private static void ConfigurePlayerDriver(
            FpgFormalPlayerTickDriver driver,
            FpgRoomEncounterDirector director,
            Camera camera,
            CombatAimReticle aimReticle,
            FpgFormalPlayerCameraFeedback cameraFeedback)
        {
            SerializedObject data = new SerializedObject(driver);
            SetObject(data, "encounterDirector", director);
            SetObject(data, "aimAnchor", null);
            SetObject(data, "aimCamera", camera);
            SetObject(data, "aimViewportSource", aimReticle);
            SetObject(data, "cameraFeedback", cameraFeedback);
            SetBool(data, "aimFromPointerPosition", true);
            SetFloat(data, "aimDistance", 50f);
            SetInt(data, "aimLayerMask", HitboxMask | BlockerMask);
            SetObject(data, "playerRoot", null);
            SetBool(data, "captureFromDevices", true);
            SetBool(data, "handlePauseAndRestart", true);
            SetInt(data, "inputBufferTicks", 8);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureDirector(
            FpgRoomEncounterDirector director,
            FpgRoomInstance roomInstance,
            FpgEnemyEntityPool enemyPool,
            FPG.Demo.Unity.FpgCombatantAnchorMap anchorMap,
            FpgFormalHitboxRegistry formalHitboxes,
            FpgOverheadHealthBarPool healthBars,
            HitboxRegistry staticHitboxes,
            Camera camera,
            GameObject exitPrefab,
            Transform exitRoot,
            Transform entrySafetyAnchor,
            FpgFormalCombatPortFactory factory,
            FpgFormalPlayerTickDriver driver,
            FpgFormalAttackRuntimeCatalog attackCatalog)
        {
            SerializedObject data = new SerializedObject(director);
            SetObject(data, "roomInstance", roomInstance);
            SetObject(data, "enemyEntityPool", enemyPool);
            SetObject(data, "combatantAnchorMap", anchorMap);
            SetObject(data, "formalHitboxRegistry", formalHitboxes);
            SetObject(data, "overheadHealthBarPool", healthBars);
            SetObject(data, "overheadHealthBarCamera", camera);
            SetObjectArray(data, "exitRuntimes", Array.Empty<UnityEngine.Object>());
            SetObject(data, "exitRuntimePrefab", exitPrefab);
            SetObject(data, "exitRuntimeRoot", exitRoot);
            SetObject(data, "exitHitboxRegistry", staticHitboxes);
            SetObject(data, "playerAnchor", null);
            SetObject(data, "entrySafetyAnchor", entrySafetyAnchor);
            SetObject(data, "formalCombatPortFactoryComponent", factory);
            SetObject(data, "formalPlayerTickDriverComponent", driver);
            SetObject(data, "formalAttackRuntimeCatalog", attackCatalog);
            SetInt(data, "presentationLeaseTicks", 12);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCameraFeedback(
            FpgFormalPlayerCameraFeedback feedback,
            Transform cameraRig,
            Camera targetCamera)
        {
            SerializedObject data = new SerializedObject(feedback);
            SetObject(data, "cameraRig", cameraRig);
            SetObject(data, "targetCamera", targetCamera);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePresentationBridge(
            FpgFormalPlayerPresentationBridge bridge,
            FpgRoomEncounterDirector director,
            FpgFormalPlayerTickDriver playerDriver,
            FpgFormalPlayerHudPresenter playerHud,
            FpgFormalPlayerCameraFeedback cameraFeedback,
            Transform cameraRig,
            Camera targetCamera)
        {
            SerializedObject data = new SerializedObject(bridge);
            SetObject(data, "encounterDirector", director);
            SetObject(data, "playerTickDriver", playerDriver);
            SetObject(data, "playerHud", playerHud);
            SetObject(data, "cameraFeedback", cameraFeedback);
            SetObject(data, "cameraRig", cameraRig);
            SetObject(data, "targetCamera", targetCamera);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureFormalCombatFeedbackBridge(
            FpgFormalCombatFeedbackBridge bridge,
            FpgRoomEncounterDirector director,
            FpgFormalPlayerTickDriver playerTickDriver,
            CombatAimReticle aimReticle,
            CombatPresentationProfile presentationProfile,
            Camera worldCamera,
            Canvas targetCanvas,
            RectTransform popupRoot,
            FpgDamagePopupView popupPrefab)
        {
            SerializedObject data = new SerializedObject(bridge);
            SetObject(data, "encounterDirector", director);
            SetObject(data, "playerTickDriver", playerTickDriver);
            SetObject(data, "aimReticle", aimReticle);
            SetObject(data, "presentationProfile", presentationProfile);
            SetObject(data, "worldCamera", worldCamera);
            SetObject(data, "targetCanvas", targetCanvas);
            SetObject(data, "popupRoot", popupRoot);
            SetObject(data, "popupPrefab", popupPrefab);
            SetInt(data, "feedbackReadCapacity", 128);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool TryValidateFormalCombatFeedbackAuthoring(
            FpgFormalCombatFeedbackBridge bridge,
            Transform presentationRoot,
            FpgRoomEncounterDirector director,
            FpgFormalPlayerTickDriver playerTickDriver,
            CombatAimReticle aimReticle,
            CombatPresentationProfile presentationProfile,
            Camera worldCamera,
            Canvas targetCanvas,
            FpgDamagePopupView popupPrefab,
            out string error)
        {
            if (bridge == null)
            {
                error = "Formal combat feedback bridge is missing.";
                return false;
            }
            if (presentationRoot == null)
            {
                error = "Formal combat feedback presentation root is missing.";
                return false;
            }
            if (director == null)
            {
                error = "Formal combat feedback encounter director is missing.";
                return false;
            }
            if (playerTickDriver == null)
            {
                error = "Formal combat feedback player tick driver is missing.";
                return false;
            }
            if (aimReticle == null)
            {
                error = "Formal combat feedback aim reticle is missing.";
                return false;
            }
            if (presentationProfile == null)
            {
                error = "Formal combat feedback presentation profile is missing.";
                return false;
            }
            if (worldCamera == null)
            {
                error = "Formal combat feedback world camera is missing.";
                return false;
            }
            if (targetCanvas == null)
            {
                error = "Formal combat feedback target Canvas is missing.";
                return false;
            }
            if (popupPrefab == null)
            {
                error = "Formal combat feedback popup prefab is missing.";
                return false;
            }

            SerializedObject data = new SerializedObject(bridge);
            RectTransform popupRoot =
                Required(data, "popupRoot").objectReferenceValue
                    as RectTransform;
            if (bridge.transform != presentationRoot
                || Required(data, "encounterDirector").objectReferenceValue
                    != director
                || Required(data, "playerTickDriver").objectReferenceValue
                    != playerTickDriver
                || Required(data, "aimReticle").objectReferenceValue
                    != aimReticle
                || Required(data, "presentationProfile").objectReferenceValue
                    != presentationProfile
                || Required(data, "worldCamera").objectReferenceValue
                    != worldCamera
                || Required(data, "targetCanvas").objectReferenceValue
                    != targetCanvas
                || Required(data, "popupPrefab").objectReferenceValue
                    != popupPrefab
                || Required(data, "feedbackReadCapacity").intValue != 128)
            {
                error =
                    "Formal combat feedback bridge bindings are incomplete.";
                return false;
            }

            if (popupRoot == null || popupRoot.parent != targetCanvas.transform
                || popupRoot.anchorMin != Vector2.zero
                || popupRoot.anchorMax != Vector2.one
                || popupRoot.offsetMin != Vector2.zero
                || popupRoot.offsetMax != Vector2.zero)
            {
                error =
                    "Formal damage-popup root must stretch under the HUD Canvas.";
                return false;
            }

            if (!popupPrefab.TryValidate(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static void ConfigurePlayerComposer(
            FpgFormalPlayerComposer composer,
            Transform actorsRoot,
            CombatPresentationProfile presentationProfile,
            FpgFormalCombatPortFactory factory,
            FpgFormalPlayerTickDriver playerDriver,
            FpgRoomEncounterDirector director,
            FpgFormalPlayerPresentationBridge presentationBridge)
        {
            SerializedObject data = new SerializedObject(composer);
            SetObject(data, "actorsRoot", actorsRoot);
            SetObject(data, "presentationProfile", presentationProfile);
            SetObject(data, "combatPortFactory", factory);
            SetObject(data, "playerTickDriver", playerDriver);
            SetObject(data, "encounterDirector", director);
            SetObject(data, "presentationBridge", presentationBridge);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureFormalSceneHost(
            FpgFormalEncounterHost host,
            FpgEncounterHost encounterHost,
            Transform actorsRoot,
            Transform cameraRoot,
            Transform presentationRoot,
            FpgRoomEncounterDirector director,
            FpgEnemyEntityPool enemyPool,
            FPG.Demo.Unity.FpgCombatantAnchorMap anchorMap,
            FpgPlayableCharacterCatalog playableCharacterCatalog,
            FpgFormalPlayerComposer playerComposer,
            FpgFormalPlayerTickDriver playerDriver,
            FpgFormalCombatPortFactory factory)
        {
            SerializedObject data = new SerializedObject(host);
            SetObject(data, "actorsRoot", actorsRoot);
            SetObject(data, "cameraRoot", cameraRoot);
            SetObject(data, "presentationRoot", presentationRoot);
            SetObject(data, "encounterHost", encounterHost);
            SetObject(data, "encounterDirector", director);
            SetObject(data, "enemyEntityPool", enemyPool);
            SetObject(data, "combatantAnchorMap", anchorMap);
            SetObject(data, "playableCharacterCatalog", playableCharacterCatalog);
            SetObject(data, "playerComposer", playerComposer);
            SetObject(data, "playerInputPort", playerDriver);
            SetObject(data, "physicsQueryPort", factory);
            SetObject(data, "combatPortFactory", factory);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEncounterHost(
            FpgEncounterHost host,
            FpgRoomDefinition room,
            FpgEncounterProfile profile,
            FpgEncounterOverrideDefinition encounterOverride,
            FpgEnemyDefinitionCatalog enemyCatalog,
            FpgFormalAttackRuntimeCatalog attackCatalog,
            FpgRoomEncounterDirector director)
        {
            SerializedObject data = new SerializedObject(host);
            SetObject(data, "roomDefinition", room);
            SetObject(data, "encounterProfile", profile);
            SetObject(data, "encounterOverride", encounterOverride);
            SetObject(data, "enemyCatalog", enemyCatalog);
            SetObject(data, "attackRuntimeCatalog", attackCatalog);
            SetObject(data, "director", director);
            SetString(data, "playerEntryMarkerId", "player-main");
            Required(data, "runSeed").longValue = 1L;
            SetString(data, "regionId", "l1");
            SetInt(data, "depth", 0);
            SetInt(data, "difficultyMultiplierBasisPoints", 10000);
            SetInt(data, "roomVisitOrdinal", 0);
            SetBool(data, "driveFromFixedUpdate", true);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBootstrapConfig(
            FpgExitRoomRefreshRule exitRoomRefreshRule)
        {
            if (exitRoomRefreshRule == null)
            {
                throw new ArgumentNullException(nameof(exitRoomRefreshRule));
            }

            GameBootstrapConfig config =
                LoadRequired<GameBootstrapConfig>(ConfigPath);
            SerializedObject data = new SerializedObject(config);
            SetString(data, "combatLabSceneName", "FormalRoom");
            SetBool(data, "loadCombatLabOnStart", true);
            SetBool(data, "requireEntranceSelection", true);
            SetBool(data, "requireCharacterSelection", true);
            SetObject(data, "exitRoomRefreshRule", exitRoomRefreshRule);
            SetInt(data, "frameRateMode", 1);
            SetInt(data, "lockedFramesPerSecond", 60);
            SetInt(data, "vSyncCount", 0);
            SetBool(data, "developmentDiagnosticsEnabled", true);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            if (!config.TryValidate(out string error))
            {
                throw new InvalidOperationException(error);
            }
        }

        private static void ConfigureBootScene(
            FpgRoomDefinition room,
            Material bootMaterial,
            Material frameMaterial,
            FpgPlayableCharacterCatalog playableCharacterCatalog)
        {
            Scene scene = EditorSceneManager.OpenScene(
                BootScenePath,
                OpenSceneMode.Single);
            room = LoadRequired<FpgRoomDefinition>(RoomPath);
            bootMaterial = LoadRequired<Material>(BootMaterialPath);
            frameMaterial = LoadRequired<Material>(FrameMaterialPath);
            playableCharacterCatalog =
                LoadRequired<FpgPlayableCharacterCatalog>(
                    PlayableCharacterCatalogPath);
            if (!playableCharacterCatalog.TryResolveDefault(
                    out FpgPlayableCharacterSelection defaultSelection,
                    out string selectionError))
            {
                throw new InvalidOperationException(selectionError);
            }

            GameBootstrap bootstrap =
                FindSingleSceneComponent<GameBootstrap>(scene);
            Camera camera = GetSerializedReference<Camera>(
                bootstrap,
                "bootCamera");
            Light light = GetSerializedReference<Light>(bootstrap, "bootLight");
            if (camera == null || light == null)
            {
                throw new InvalidOperationException(
                    "Boot scene requires its authored camera and directional light.");
            }

            RemoveAuthoredPlayers(scene);
            DestroyRootIfPresent(scene, "__CharacterSelection");
            DestroyRootIfPresent(scene, InstallationMarkerName);
            new GameObject(InstallationMarkerName);
            DestroyRootIfPresent(scene, "__FormalRoomEntrances");

            GameObject characterRoot = new GameObject("__CharacterSelection");
            GameObject choiceObject = new GameObject("CharacterChoice_Fei");
            choiceObject.transform.SetParent(characterRoot.transform, false);

            GameObject choiceVisuals = new GameObject("SelectionVisuals");
            choiceVisuals.transform.SetParent(choiceObject.transform, false);

            GameObject shotTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shotTarget.name = "SelectionBackdrop";
            shotTarget.transform.SetParent(choiceVisuals.transform, false);
            shotTarget.transform.localPosition = new Vector3(0f, 2f, 0.8f);
            shotTarget.transform.localScale = new Vector3(4.2f, 5.2f, 0.25f);
            shotTarget.layer = 0;
            Renderer choiceRenderer = shotTarget.GetComponent<Renderer>();
            choiceRenderer.sharedMaterial = bootMaterial;
            UnityEngine.Object.DestroyImmediate(shotTarget.GetComponent<Collider>());

            CreateFramePiece(
                choiceVisuals.transform,
                "FrameLeft",
                new Vector3(-2.35f, 2f, 0.55f),
                new Vector3(0.35f, 5.2f, 0.35f),
                frameMaterial);
            CreateFramePiece(
                choiceVisuals.transform,
                "FrameRight",
                new Vector3(2.35f, 2f, 0.55f),
                new Vector3(0.35f, 5.2f, 0.35f),
                frameMaterial);
            CreateFramePiece(
                choiceVisuals.transform,
                "Pedestal",
                new Vector3(0f, -0.35f, 0.3f),
                new Vector3(5f, 0.45f, 1.6f),
                frameMaterial);

            GameObject preview =
                PrefabUtility.InstantiatePrefab(
                    defaultSelection.SelectionPreviewPrefab,
                    scene) as GameObject;
            if (preview == null)
            {
                throw new InvalidOperationException(
                    "Could not instantiate Fei's visual-only Boot preview.");
            }

            preview.name = "Fei_SelectionPreview";
            preview.transform.SetParent(choiceVisuals.transform, false);
            preview.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            preview.transform.localRotation = Quaternion.identity;
            preview.transform.localScale = Vector3.one;
            if (preview.GetComponentInChildren<D0ActorEntityView>(true) != null)
            {
                throw new InvalidOperationException(
                    "Boot selection preview contains a gameplay entity.");
            }

            Collider choiceCollider = CreateCharacterSelectionTrigger(preview);

            FpgBootCharacterChoice characterChoice =
                choiceObject.AddComponent<FpgBootCharacterChoice>();
            SerializedObject choiceData =
                new SerializedObject(characterChoice);
            SetObject(
                choiceData,
                "character",
                defaultSelection.CharacterDefinition);
            SetObject(choiceData, "previewRoot", choiceVisuals);
            SetObjectArray(
                choiceData,
                "hitColliders",
                new UnityEngine.Object[] { choiceCollider });
            SetObjectArray(
                choiceData,
                "statusRenderers",
                new UnityEngine.Object[] { choiceRenderer });
            SetColor(
                choiceData,
                "availableColor",
                new Color(0.05f, 0.75f, 0.95f, 1f));
            SetColor(
                choiceData,
                "selectedColor",
                new Color(0.2f, 1f, 0.35f, 1f));
            SetColor(
                choiceData,
                "unavailableColor",
                new Color(0.14f, 0.16f, 0.19f, 1f));
            choiceData.ApplyModifiedPropertiesWithoutUndo();
            characterChoice.SetSelectable(true);

            GameObject entranceRoot = new GameObject("__FormalRoomEntrances");
            GameObject entranceObject = new GameObject("RoomEntrance_L1_01");
            entranceObject.transform.SetParent(entranceRoot.transform, false);

            GameObject entranceTarget =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            entranceTarget.name = "ShotTarget";
            entranceTarget.transform.SetParent(entranceObject.transform, false);
            entranceTarget.transform.localPosition = new Vector3(0f, 2f, 0f);
            entranceTarget.transform.localScale =
                new Vector3(3.4f, 3.4f, 0.35f);
            entranceTarget.layer = 0;
            Renderer entranceRenderer =
                entranceTarget.GetComponent<Renderer>();
            entranceRenderer.sharedMaterial = bootMaterial;
            Collider entranceCollider =
                entranceTarget.GetComponent<Collider>();
            entranceCollider.isTrigger = false;

            CreateFramePiece(
                entranceObject.transform,
                "FrameLeft",
                new Vector3(-2.25f, 2f, 0.15f),
                new Vector3(0.55f, 4.8f, 0.55f),
                frameMaterial);
            CreateFramePiece(
                entranceObject.transform,
                "FrameRight",
                new Vector3(2.25f, 2f, 0.15f),
                new Vector3(0.55f, 4.8f, 0.55f),
                frameMaterial);
            CreateFramePiece(
                entranceObject.transform,
                "FrameTop",
                new Vector3(0f, 4.25f, 0.15f),
                new Vector3(5.05f, 0.55f, 0.55f),
                frameMaterial);
            CreateFramePiece(
                entranceObject.transform,
                "Threshold",
                new Vector3(0f, -0.15f, 0.15f),
                new Vector3(5.05f, 0.3f, 1.2f),
                frameMaterial);

            FpgBootRoomEntrance entrance =
                entranceObject.AddComponent<FpgBootRoomEntrance>();
            SerializedObject entranceData = new SerializedObject(entrance);
            SetObject(entranceData, "roomDefinition", room);
            SetObjectArray(
                entranceData,
                "hitColliders",
                new UnityEngine.Object[] { entranceCollider });
            SetObjectArray(
                entranceData,
                "statusRenderers",
                new UnityEngine.Object[] { entranceRenderer });
            SetColor(
                entranceData,
                "availableColor",
                new Color(0.05f, 0.75f, 0.95f, 1f));
            SetColor(
                entranceData,
                "selectedColor",
                new Color(0.2f, 1f, 0.35f, 1f));
            entranceData.ApplyModifiedPropertiesWithoutUndo();
            entrance.SetSelectable(false);
            entranceObject.SetActive(false);

            camera.transform.position = new Vector3(0f, 2f, -10f);
            camera.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 2f, 0f) - camera.transform.position,
                Vector3.up);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.025f, 0.034f, 1f);
            camera.fieldOfView = 48f;

            light.transform.rotation = Quaternion.Euler(38f, -25f, 0f);
            light.color = new Color(0.8f, 0.9f, 1f, 1f);
            light.intensity = 1.1f;

            SerializedObject bootstrapData = new SerializedObject(bootstrap);
            SetObject(
                bootstrapData,
                "config",
                LoadRequired<GameBootstrapConfig>(ConfigPath));
            SetObject(
                bootstrapData,
                "playableCharacterCatalog",
                playableCharacterCatalog);
            SetObjectArray(
                bootstrapData,
                "characterChoices",
                new UnityEngine.Object[] { characterChoice });
            SetObjectArray(
                bootstrapData,
                "roomEntrances",
                new UnityEngine.Object[] { entrance });
            SetFloat(bootstrapData, "entranceShotDistance", 100f);
            SetInt(bootstrapData, "entranceLayerMask", 1);
            bootstrapData.ApplyModifiedPropertiesWithoutUndo();

            if (!characterChoice.TryResolveSelection(
                    playableCharacterCatalog,
                    out _,
                    out string choiceError))
            {
                throw new InvalidOperationException(choiceError);
            }

            if (!bootstrap.TryValidateConfiguration(out string error))
            {
                throw new InvalidOperationException(error);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("Could not save Boot scene.");
            }
        }

        private static void RemoveAuthoredPlayers(Scene scene)
        {
            List<D0PlayerEntityView> players =
                FindSceneComponents<D0PlayerEntityView>(scene);
            for (int index = players.Count - 1; index >= 0; index--)
            {
                D0PlayerEntityView player = players[index];
                if (player != null)
                {
                    UnityEngine.Object.DestroyImmediate(player.gameObject);
                }
            }
        }

        private static void DestroyRootIfPresent(Scene scene, string name)
        {
            GameObject root = FindRoot(scene, name);
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateFramePiece(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = localScale;
            Renderer renderer = piece.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            Collider collider = piece.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static void EnsureBuildSettings()
        {
            LoadRequired<SceneAsset>(BootScenePath);
            LoadRequired<SceneAsset>(CombatLabScenePath);
            LoadRequired<SceneAsset>(FormalRoomScenePath);

            List<EditorBuildSettingsScene> remaining =
                new List<EditorBuildSettingsScene>();
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            for (int index = 0; index < current.Length; index++)
            {
                string path = current[index].path;
                if (string.Equals(path, BootScenePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        path,
                        CombatLabScenePath,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        path,
                        FormalRoomScenePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                remaining.Add(current[index]);
            }

            List<EditorBuildSettingsScene> scenes =
                new List<EditorBuildSettingsScene>(remaining.Count + 3)
                {
                    new EditorBuildSettingsScene(BootScenePath, true),
                    new EditorBuildSettingsScene(CombatLabScenePath, true),
                    new EditorBuildSettingsScene(FormalRoomScenePath, true)
                };
            scenes.AddRange(remaining);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ValidateInstallation(
            FpgRoomDefinition room,
            FpgPlayableCharacterCatalog playableCharacterCatalog)
        {
            room = LoadRequired<FpgRoomDefinition>(RoomPath);
            playableCharacterCatalog =
                LoadRequired<FpgPlayableCharacterCatalog>(
                    PlayableCharacterCatalogPath);
            FpgEncounterProfile profile =
                LoadRequired<FpgEncounterProfile>(ProfilePath);
            FpgEncounterOverrideDefinition encounterOverride =
                LoadRequired<FpgEncounterOverrideDefinition>(OverridePath);
            FpgEnemyDefinitionCatalog enemyCatalog =
                LoadRequired<FpgEnemyDefinitionCatalog>(EnemyCatalogPath);
            FpgFormalAttackRuntimeCatalog attackCatalog =
                LoadRequired<FpgFormalAttackRuntimeCatalog>(AttackCatalogPath);
            CombatPresentationProfile presentationProfile =
                LoadRequired<CombatPresentationProfile>(
                    CombatPresentationProfilePath);
            FpgDamagePopupView damagePopupPrefab =
                LoadRequired<FpgDamagePopupView>(DamagePopupPrefabPath);
            FpgRoomCatalog roomCatalog =
                LoadRequired<FpgRoomCatalog>(RoomCatalogPath);
            FpgExitRoomRefreshRule exitRoomRefreshRule =
                LoadRequired<FpgExitRoomRefreshRule>(ExitRoomRefreshRulePath);

            string roomCatalogError = string.Empty;
            string exitRoomRefreshRuleError = string.Empty;
            bool roomCatalogValid = roomCatalog.TryValidate(out roomCatalogError);
            bool exitRoomRefreshRuleValid =
                exitRoomRefreshRule.TryValidate(out exitRoomRefreshRuleError);
            if (!roomCatalogValid
                || !ContainsRoom(roomCatalog, room)
                || exitRoomRefreshRule.RoomCatalog != roomCatalog
                || !exitRoomRefreshRuleValid)
            {
                throw new InvalidOperationException(
                    !string.IsNullOrWhiteSpace(roomCatalogError)
                        ? roomCatalogError
                        : !string.IsNullOrWhiteSpace(exitRoomRefreshRuleError)
                            ? exitRoomRefreshRuleError
                            : "Formal exit routing catalog must include the combatlab forest room.");
            }

            string catalogError = string.Empty;
            string selectionError = string.Empty;
            FpgPlayableCharacterSelection defaultSelection = default;
            if (!playableCharacterCatalog.TryValidate(out catalogError)
                || !playableCharacterCatalog.TryResolveDefault(
                    out defaultSelection,
                    out selectionError))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(catalogError)
                        ? selectionError
                        : catalogError);
            }

            if (defaultSelection.CharacterDefinition
                    != LoadRequired<D0CharacterDefinition>(FeiCharacterPath)
                || defaultSelection.ThreeCProfile
                    != LoadRequired<D0ThreeCProfile>(FeiThreeCPath)
                || defaultSelection.SelectionPreviewPrefab
                    != LoadRequired<GameObject>(FeiSelectionPreviewPath))
            {
                throw new InvalidOperationException(
                    "Playable character catalog default is not the complete Fei selection.");
            }

            if (!profile.TryValidate(out string profileError))
            {
                throw new InvalidOperationException(profileError);
            }

            if (encounterOverride.Data == null)
            {
                throw new InvalidOperationException(
                    "L1_01 encounter override is invalid.");
            }

            if (!enemyCatalog.TryValidate(out string enemyError))
            {
                throw new InvalidOperationException(enemyError);
            }

            if (!attackCatalog.TryValidate(out string attackError))
            {
                throw new InvalidOperationException(attackError);
            }

            FpgEncounterRunContext context =
                new FpgEncounterRunContext(1UL, "l1", 0, 10000, 0);
            FpgRoomRunRequest request = FpgFormalRoomRequestFactory.Create(
                room,
                profile,
                encounterOverride,
                context);
            FpgEncounterPlanGenerationResult generated =
                FpgEncounterPlanGenerator.Generate(request);
            if (!generated.IsSuccess)
            {
                throw new InvalidOperationException(generated.Error);
            }

            FpgEncounterPreflightResult preflight =
                FpgEncounterPreflight.Validate(
                    request,
                    generated.Plan,
                    enemyCatalog);
            if (!preflight.IsSuccess)
            {
                throw new InvalidOperationException(preflight.Error);
            }

            Scene formalScene = EditorSceneManager.OpenScene(
                FormalRoomScenePath,
                OpenSceneMode.Single);
            room = LoadRequired<FpgRoomDefinition>(RoomPath);
            profile = LoadRequired<FpgEncounterProfile>(ProfilePath);
            encounterOverride =
                LoadRequired<FpgEncounterOverrideDefinition>(OverridePath);

            if (FindSceneComponents<D0PlayerEntityView>(formalScene).Count != 0)
            {
                throw new InvalidOperationException(
                    "FormalRoom must not contain an authored player entity.");
            }

            if (FindSceneComponents<FpgFormalPlayerComposer>(formalScene).Count != 1
                || FindSceneComponents<FpgFormalEncounterHost>(formalScene).Count != 1
                || FindSceneComponents<FpgFormalCombatFeedbackBridge>(
                    formalScene).Count != 1)
            {
                throw new InvalidOperationException(
                    "FormalRoom requires exactly one formal composer, scene host "
                    + "and combat feedback bridge.");
            }

            if (FindSceneComponents<BattleSessionHost>(formalScene).Count != 0
                || FindSceneComponents<BattleSceneContext>(formalScene).Count != 0)
            {
                throw new InvalidOperationException(
                    "FormalRoom must not contain legacy BattleSessionHost or BattleSceneContext.");
            }

            FpgFormalEncounterHost formalHost =
                FindSingleSceneComponent<FpgFormalEncounterHost>(formalScene);
            FpgEncounterHost encounterHost =
                FindSingleSceneComponent<FpgEncounterHost>(formalScene);
            FpgFormalPlayerComposer composer =
                FindSingleSceneComponent<FpgFormalPlayerComposer>(formalScene);
            FpgFormalPlayerPresentationBridge bridge =
                FindSingleSceneComponent<FpgFormalPlayerPresentationBridge>(
                    formalScene);
            FpgFormalCombatFeedbackBridge feedbackBridge =
                FindSingleSceneComponent<FpgFormalCombatFeedbackBridge>(
                    formalScene);
            FpgFormalPlayerHudPresenter playerHud =
                FindSingleSceneComponent<FpgFormalPlayerHudPresenter>(
                    formalScene);
            FpgFormalPlayerCameraFeedback cameraFeedback =
                FindSingleSceneComponent<FpgFormalPlayerCameraFeedback>(
                    formalScene);
            CombatAimReticle reticle =
                FindSingleSceneComponent<CombatAimReticle>(formalScene);
            FpgFormalCombatPortFactory factory =
                FindSingleSceneComponent<FpgFormalCombatPortFactory>(
                    formalScene);
            FpgFormalPlayerTickDriver driver =
                FindSingleSceneComponent<FpgFormalPlayerTickDriver>(
                    formalScene);
            FpgRoomEncounterDirector director =
                FindSingleSceneComponent<FpgRoomEncounterDirector>(formalScene);
            Camera worldCamera =
                FindSingleSceneComponent<Camera>(formalScene);
            Canvas targetCanvas = playerHud.GetComponent<Canvas>();
            HitboxRegistry registry =
                FindSingleSceneComponent<HitboxRegistry>(formalScene);

            if (GetSerializedReference<HitboxRegistry>(
                    director,
                    "exitHitboxRegistry") != registry)
            {
                throw new InvalidOperationException(
                    "FormalRoom director must use the static hitbox registry for exit attacks.");
            }

            string hostError = string.Empty;
            string composerError = string.Empty;
            string bridgeError = string.Empty;
            string hudError = string.Empty;
            string cameraError = string.Empty;
            string feedbackError = string.Empty;
            if (!formalHost.TryValidateAuthoring(out hostError)
                || !composer.TryValidateAuthoring(out composerError)
                || !bridge.TryValidateAuthoring(out bridgeError)
                || !playerHud.TryValidate(out hudError)
                || !cameraFeedback.TryValidate(out cameraError)
                || !TryValidateFormalCombatFeedbackAuthoring(
                    feedbackBridge,
                    formalHost.PresentationRoot,
                    director,
                    driver,
                    reticle,
                    presentationProfile,
                    worldCamera,
                    targetCanvas,
                    damagePopupPrefab,
                    out feedbackError))
            {
                string error = !string.IsNullOrWhiteSpace(hostError)
                    ? hostError
                    : !string.IsNullOrWhiteSpace(composerError)
                        ? composerError
                        : !string.IsNullOrWhiteSpace(bridgeError)
                            ? bridgeError
                            : !string.IsNullOrWhiteSpace(hudError)
                                ? hudError
                                : !string.IsNullOrWhiteSpace(cameraError)
                                    ? cameraError
                                    : feedbackError;
                throw new InvalidOperationException(error);
            }

            if (factory.HasPlayerBinding || driver.IsPlayerConfigured
                || director.HasPlayerBinding
                || factory.PlayerDefinition != null
                || factory.PlayerEntity != null
                || driver.PlayerDefinition != null
                || driver.PlayerEntity != null
                || director.ConfiguredPlayerEntity != null)
            {
                throw new InvalidOperationException(
                    "FormalRoom player ports must remain unconfigured in the scene asset.");
            }

            if (driver.AimViewportSourceComponent != reticle
                || bridge.CameraFeedback != cameraFeedback
                || cameraFeedback.TargetCamera == null
                || cameraFeedback.TargetCamera.transform.parent
                    != cameraFeedback.CameraRig)
            {
                throw new InvalidOperationException(
                    "FormalRoom reticle, presentation bridge and camera rig wiring is incomplete.");
            }

            if (encounterHost.RoomDefinition != room
                || encounterHost.EncounterProfile != profile
                || encounterHost.EncounterOverride != encounterOverride)
            {
                throw new InvalidOperationException(
                    "Formal encounter host asset references are incomplete.");
            }

            if (!registry.TryValidateStaticBindings(
                    UnityAttackQuerySettings.Default,
                    out string hitboxError))
            {
                throw new InvalidOperationException(hitboxError);
            }

            GameObject exitPrefab = LoadRequired<GameObject>(ExitPrefabPath);
            if (!TryValidateExitPrefab(exitPrefab, out string exitPrefabError))
            {
                throw new InvalidOperationException(exitPrefabError);
            }

            FpgOverheadHealthBarView healthBar =
                LoadRequired<FpgOverheadHealthBarView>(HealthBarPrefabPath);
            SerializedObject healthData = new SerializedObject(healthBar);
            if (Required(healthData, "lifeBar").objectReferenceValue == null)
            {
                throw new InvalidOperationException(
                    "Formal overhead health bar has no formal life bar.");
            }

            Scene bootScene = EditorSceneManager.OpenScene(
                BootScenePath,
                OpenSceneMode.Single);
            GameBootstrap bootstrap =
                FindSingleSceneComponent<GameBootstrap>(bootScene);
            if (!bootstrap.TryValidateConfiguration(out string bootstrapError))
            {
                throw new InvalidOperationException(bootstrapError);
            }

            if (bootstrap.PlayableCharacterCatalog != playableCharacterCatalog
                || FindSceneComponents<D0PlayerEntityView>(bootScene).Count != 0
                || FindSceneComponents<FpgBootCharacterChoice>(bootScene).Count != 1
                || FindSceneComponents<FpgBootRoomEntrance>(bootScene).Count != 1)
            {
                throw new InvalidOperationException(
                    "Boot must contain the catalog, one visual-only character choice, one entrance and zero player entities.");
            }

            FpgBootCharacterChoice characterChoice =
                FindSingleSceneComponent<FpgBootCharacterChoice>(bootScene);
            if (!characterChoice.TryResolveSelection(
                    playableCharacterCatalog,
                    out FpgPlayableCharacterSelection bootSelection,
                    out string choiceError)
                || bootSelection.CharacterDefinition
                    != defaultSelection.CharacterDefinition
                || characterChoice.PreviewRoot
                    .GetComponentInChildren<D0ActorEntityView>(true) != null)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(choiceError)
                        ? "Boot character choice does not resolve to the visual-only default selection."
                        : choiceError);
            }

            GameBootstrapConfig config =
                LoadRequired<GameBootstrapConfig>(ConfigPath);
            if (!string.Equals(
                    config.RoomSceneName,
                    "FormalRoom",
                    StringComparison.Ordinal)
                || !config.LoadRoomOnStart
                || !config.RequireCharacterSelection
                || !config.RequireEntranceSelection
                || config.ExitRoomRefreshRule != exitRoomRefreshRule)
            {
                throw new InvalidOperationException(
                    "Bootstrap config must target FormalRoom, require character and room selection, and reference the formal exit refresh rule.");
            }

            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length < 3
                || !buildScenes[0].enabled
                || !buildScenes[1].enabled
                || !buildScenes[2].enabled
                || !string.Equals(
                    buildScenes[0].path,
                    BootScenePath,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    buildScenes[1].path,
                    CombatLabScenePath,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    buildScenes[2].path,
                    FormalRoomScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Build Settings must keep Boot, CombatLab editor harness and FormalRoom at stable indices 0, 1 and 2.");
            }
        }

        private static bool TryValidateExitPrefab(
            GameObject exitPrefab,
            out string error)
        {
            if (exitPrefab == null)
            {
                error = "Formal exit prefab is missing.";
                return false;
            }

            FpgRoomExitRuntime runtime =
                exitPrefab.GetComponent<FpgRoomExitRuntime>();
            if (runtime == null)
            {
                error = "Formal exit prefab has no FpgRoomExitRuntime.";
                return false;
            }

            SerializedObject runtimeData = new SerializedObject(runtime);
            Component destinationLabel =
                Required(runtimeData, "destinationLabel")
                    .objectReferenceValue as Component;
            Canvas destinationCanvas = destinationLabel == null
                ? null
                : destinationLabel.GetComponentInParent<Canvas>(true);
            if (destinationLabel == null
                || !string.Equals(
                    destinationLabel.GetType().FullName,
                    "UnityEngine.UI.Text",
                    StringComparison.Ordinal)
                || destinationCanvas == null
                || destinationCanvas.renderMode != RenderMode.WorldSpace
                || !string.Equals(
                    Required(runtimeData, "destinationLabelPrefix").stringValue,
                    "\u524D\u5F80\uFF1A",
                    StringComparison.Ordinal))
            {
                error =
                    "Formal exit prefab requires a bound world-space UGUI destination label.";
                return false;
            }

            SerializedProperty interactions =
                Required(runtimeData, "interactionBehaviours");
            bool canvasBound = false;
            for (int index = 0; index < interactions.arraySize; index++)
            {
                if (interactions.GetArrayElementAtIndex(index)
                        .objectReferenceValue == destinationCanvas)
                {
                    canvasBound = true;
                    break;
                }
            }

            IReadOnlyList<Collider> attackColliders = runtime.AttackColliders;
            bool hasAttackCollider = false;
            for (int index = 0; index < attackColliders.Count; index++)
            {
                Collider attackCollider = attackColliders[index];
                if (attackCollider != null)
                {
                    hasAttackCollider = true;
                    if (attackCollider.enabled)
                    {
                        error =
                            "Formal exit attack colliders must be disabled while hidden.";
                        return false;
                    }
                }
            }

            if (!canvasBound || !hasAttackCollider
                || runtime.State != FpgRoomExitRuntimeState.Hidden
                || destinationCanvas.enabled
                || (destinationLabel is Behaviour labelBehaviour
                    && labelBehaviour.enabled))
            {
                error =
                    "Formal exit prefab must start hidden with its label and attack collider disabled.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static Material EnsureMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader =
                    Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "No supported shader was found for formal room materials.");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetImageColor(Component image, Color color)
        {
            SerializedObject data = new SerializedObject(image);
            SetColor(data, "m_Color", color);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetGraphicRaycastTarget(
            Component graphic,
            bool value)
        {
            SerializedObject data = new SerializedObject(graphic);
            SetBool(data, "m_RaycastTarget", value);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Stretch(
            RectTransform rect,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static Transform Child(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (string.Equals(roots[index].name, name, StringComparison.Ordinal))
                {
                    return roots[index];
                }
            }

            return null;
        }

        private static List<T> FindSceneComponents<T>(Scene scene)
            where T : Component
        {
            List<T> results = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                results.AddRange(
                    roots[rootIndex].GetComponentsInChildren<T>(true));
            }

            return results;
        }

        private static T FindSingleSceneComponent<T>(Scene scene)
            where T : Component
        {
            T result = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                T[] candidates = roots[rootIndex].GetComponentsInChildren<T>(true);
                for (int index = 0; index < candidates.Length; index++)
                {
                    if (result != null)
                    {
                        throw new InvalidOperationException(
                            "Scene '" + scene.path + "' contains more than one "
                            + typeof(T).Name + ".");
                    }

                    result = candidates[index];
                }
            }

            if (result == null)
            {
                throw new InvalidOperationException(
                    "Scene '" + scene.path + "' contains no " + typeof(T).Name + ".");
            }

            return result;
        }

        private static T GetSerializedReference<T>(
            UnityEngine.Object owner,
            string propertyName)
            where T : UnityEngine.Object
        {
            SerializedObject data = new SerializedObject(owner);
            return Required(data, propertyName).objectReferenceValue as T;
        }

        private static T LoadRequired<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException("Missing required asset: " + path);
            }

            return asset;
        }

        private static void EnsureNoDirtyScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded && scene.isDirty)
                {
                    throw new InvalidOperationException(
                        "Save the dirty scene before installing the formal room loop: "
                        + scene.path);
                }
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static SerializedProperty Required(
            SerializedObject data,
            string name)
        {
            SerializedProperty property = data.FindProperty(name);
            if (property == null)
            {
                throw new InvalidOperationException(
                    data.targetObject.GetType().Name
                    + " has no serialized property '" + name + "'.");
            }

            return property;
        }

        private static SerializedProperty Required(
            SerializedProperty data,
            string name)
        {
            SerializedProperty property = data.FindPropertyRelative(name);
            if (property == null)
            {
                throw new InvalidOperationException(
                    "Serialized property '" + data.propertyPath
                    + "' has no child '" + name + "'.");
            }

            return property;
        }

        private static void SetObject(
            SerializedObject data,
            string name,
            UnityEngine.Object value)
        {
            Required(data, name).objectReferenceValue = value;
        }

        private static void SetObjectArray(
            SerializedObject data,
            string name,
            UnityEngine.Object[] values)
        {
            SerializedProperty array = Required(data, name);
            array.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                array.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void SetString(
            SerializedObject data,
            string name,
            string value)
        {
            Required(data, name).stringValue = value;
        }

        private static void SetInt(
            SerializedObject data,
            string name,
            int value)
        {
            Required(data, name).intValue = value;
        }

        private static void SetFloat(
            SerializedObject data,
            string name,
            float value)
        {
            Required(data, name).floatValue = value;
        }

        private static void SetBool(
            SerializedObject data,
            string name,
            bool value)
        {
            Required(data, name).boolValue = value;
        }

        private static void SetVector2(
            SerializedObject data,
            string name,
            Vector2 value)
        {
            Required(data, name).vector2Value = value;
        }

        private static void SetVector3(
            SerializedObject data,
            string name,
            Vector3 value)
        {
            Required(data, name).vector3Value = value;
        }

        private static void SetColor(
            SerializedObject data,
            string name,
            Color value)
        {
            Required(data, name).colorValue = value;
        }
    

        private static bool HasCurrentSceneInstallation(
            FpgPlayableCharacterCatalog playableCharacterCatalog)
        {
            FpgRoomDefinition room =
                AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(RoomPath);
            FpgRoomCatalog roomCatalog =
                AssetDatabase.LoadAssetAtPath<FpgRoomCatalog>(RoomCatalogPath);
            FpgExitRoomRefreshRule exitRoomRefreshRule =
                AssetDatabase.LoadAssetAtPath<FpgExitRoomRefreshRule>(
                    ExitRoomRefreshRulePath);
            GameObject exitPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(ExitPrefabPath);
            if (playableCharacterCatalog == null
                || room == null
                || roomCatalog == null
                || exitRoomRefreshRule == null
                || exitPrefab == null
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath) == null
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(FormalRoomScenePath) == null
                || AssetDatabase.LoadAssetAtPath<FpgDamagePopupView>(
                    DamagePopupPrefabPath) == null)
            {
                return false;
            }

            if (!roomCatalog.TryValidate(out _)
                || roomCatalog.Count != 1
                || roomCatalog.Rooms[0] != room
                || exitRoomRefreshRule.RoomCatalog != roomCatalog
                || !exitRoomRefreshRule.TryValidate(out _)
                || !TryValidateExitPrefab(exitPrefab, out _))
            {
                return false;
            }

            try
            {
                Scene formalScene = EditorSceneManager.OpenScene(
                    FormalRoomScenePath,
                    OpenSceneMode.Single);
                if (FindRoot(formalScene, InstallationMarkerName) == null
                    || FindSceneComponents<D0PlayerEntityView>(formalScene).Count != 0
                    || FindSceneComponents<FpgFormalPlayerComposer>(formalScene).Count != 1
                    || FindSceneComponents<FpgFormalEncounterHost>(formalScene).Count != 1
                    || FindSceneComponents<FpgFormalPlayerPresentationBridge>(formalScene).Count != 1
                    || FindSceneComponents<FpgFormalCombatFeedbackBridge>(
                        formalScene).Count != 1
                    || FindSceneComponents<FpgFormalPlayerHudPresenter>(formalScene).Count != 1
                    || FindSceneComponents<CombatAimReticle>(formalScene).Count != 1
                    || FindSceneComponents<BattleSessionHost>(formalScene).Count != 0
                    || FindSceneComponents<BattleSceneContext>(formalScene).Count != 0)
                {
                    return false;
                }

                FpgFormalEncounterHost formalHost =
                    FindSingleSceneComponent<FpgFormalEncounterHost>(formalScene);
                FpgFormalCombatPortFactory factory =
                    FindSingleSceneComponent<FpgFormalCombatPortFactory>(formalScene);
                FpgFormalPlayerTickDriver driver =
                    FindSingleSceneComponent<FpgFormalPlayerTickDriver>(formalScene);
                FpgRoomEncounterDirector director =
                    FindSingleSceneComponent<FpgRoomEncounterDirector>(formalScene);
                HitboxRegistry staticHitboxes =
                    FindSingleSceneComponent<HitboxRegistry>(formalScene);
                FpgFormalCombatFeedbackBridge feedbackBridge =
                    FindSingleSceneComponent<FpgFormalCombatFeedbackBridge>(
                        formalScene);
                FpgFormalPlayerHudPresenter playerHud =
                    FindSingleSceneComponent<FpgFormalPlayerHudPresenter>(
                        formalScene);
                Camera worldCamera =
                    FindSingleSceneComponent<Camera>(formalScene);
                CombatPresentationProfile presentationProfile =
                    LoadRequired<CombatPresentationProfile>(
                        CombatPresentationProfilePath);
                FpgDamagePopupView damagePopupPrefab =
                    LoadRequired<FpgDamagePopupView>(DamagePopupPrefabPath);
                if (!formalHost.TryValidateAuthoring(out _)
                    || formalHost.PlayableCharacterCatalog != playableCharacterCatalog
                    || factory.HasPlayerBinding
                    || driver.IsPlayerConfigured
                    || director.HasPlayerBinding
                    || GetSerializedReference<HitboxRegistry>(
                        director,
                        "exitHitboxRegistry") != staticHitboxes
                    || !TryValidateFormalCombatFeedbackAuthoring(
                        feedbackBridge,
                        formalHost.PresentationRoot,
                        director,
                        driver,
                        FindSingleSceneComponent<CombatAimReticle>(formalScene),
                        presentationProfile,
                        worldCamera,
                        playerHud.GetComponent<Canvas>(),
                        damagePopupPrefab,
                        out _))
                {
                    return false;
                }

                Scene bootScene = EditorSceneManager.OpenScene(
                    BootScenePath,
                    OpenSceneMode.Single);
                if (FindRoot(bootScene, InstallationMarkerName) == null
                    || FindSceneComponents<D0PlayerEntityView>(bootScene).Count != 0
                    || FindSceneComponents<FpgBootCharacterChoice>(bootScene).Count != 1
                    || FindSceneComponents<FpgBootRoomEntrance>(bootScene).Count != 1)
                {
                    return false;
                }

                GameBootstrap bootstrap =
                    FindSingleSceneComponent<GameBootstrap>(bootScene);
                return bootstrap.PlayableCharacterCatalog == playableCharacterCatalog
                    && bootstrap.Config != null
                    && bootstrap.Config.ExitRoomRefreshRule
                        == exitRoomRefreshRule
                    && bootstrap.TryValidateConfiguration(out _);
            }
            catch (Exception)
            {
                return false;
            }
        }


        private static Collider CreateCharacterSelectionTrigger(GameObject preview)
        {
            if (preview == null)
            {
                throw new ArgumentNullException(nameof(preview));
            }

            Renderer[] renderers = preview.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "Boot character selection preview requires a Renderer for its trigger bounds.");
            }

            Bounds worldBounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                worldBounds.Encapsulate(renderers[index].bounds);
            }

            bool hasLocalPoint = false;
            Bounds localBounds = default;
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 worldPoint = new Vector3(
                            x == 0 ? worldBounds.min.x : worldBounds.max.x,
                            y == 0 ? worldBounds.min.y : worldBounds.max.y,
                            z == 0 ? worldBounds.min.z : worldBounds.max.z);
                        Vector3 localPoint =
                            preview.transform.InverseTransformPoint(worldPoint);
                        if (!hasLocalPoint)
                        {
                            localBounds = new Bounds(localPoint, Vector3.zero);
                            hasLocalPoint = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(localPoint);
                        }
                    }
                }
            }

            GameObject triggerObject = new GameObject("SelectionTrigger");
            triggerObject.layer = 0;
            triggerObject.transform.SetParent(preview.transform, false);
            BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = localBounds.center;
            Vector3 rendererSize = localBounds.size;
            trigger.size = new Vector3(
                Mathf.Max(0.5f, rendererSize.x + 0.2f),
                Mathf.Max(0.5f, rendererSize.y + 0.2f),
                Mathf.Max(0.5f, rendererSize.z + 0.2f));
            return trigger;
        }
}
}
