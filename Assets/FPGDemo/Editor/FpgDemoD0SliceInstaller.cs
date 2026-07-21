using System;
using System.IO;
using FPG.Demo.Unity;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Editor
{
    /// <summary>
    /// Idempotent owner of D0 slice assets and scene roots. It intentionally
    /// does not reuse the removed pre-D0 greybox workflow: that workflow
    /// rebuilt greybox assets and would overwrite CZN bindings,
    /// materials, HUD, camera and Build Settings owned by this slice.
    /// </summary>
    public static class FpgDemoD0SliceInstaller
    {
        public const string ConfigFolder = "Assets/FPGDemo/Config/D0Slice";
        public const string PresentationFolder = "Assets/FPGDemo/Presentation/D0Slice";
        public const string D0SpinePresentationFolder = PresentationFolder + "/Spine";
        public const string ProfilePath = ConfigFolder + "/CombatPresentationProfile.asset";
        public const string AudioBankPath = ConfigFolder + "/CombatAudioBank.asset";
        public const string InstallationStatePath =
            ConfigFolder + "/D0SliceInstallationState.asset";
        public const string CombatLabScenePath = "Assets/FPGDemo/Scenes/CombatLab.unity";
        public const string RootName = "D0Slice2D";

        // These are FPG-owned, Linear-safe straight-alpha presentation
        // derivatives. The CZN source SkeletonData assets remain local-only
        // canonical inputs; no scene or runtime component should bind
        // canonical atlas, texture or material assets directly.
        public const string BurstbugFastFxPrefabPath =
            D0SpinePresentationFolder + "/D0_Burstbug_1001003_Fx_Skill1.prefab";
        public const string BurstbugVolleyFxPrefabPath =
            D0SpinePresentationFolder + "/D0_Burstbug_1001003_Fx_Skill2.prefab";
        public const string BurstbugDeathFx1PrefabPath =
            D0SpinePresentationFolder + "/D0_Burstbug_1001003_Fx_Death1.prefab";
        public const string BurstbugDeathFx2PrefabPath =
            D0SpinePresentationFolder + "/D0_Burstbug_1001003_Fx_Death2.prefab";
        public const string BurstbugDeathFx3PrefabPath =
            D0SpinePresentationFolder + "/D0_Burstbug_1001003_Fx_Death3.prefab";
        public const string BurstbugDeathFx4PrefabPath =
            D0SpinePresentationFolder + "/D0_Burstbug_1001003_Fx_Death4.prefab";
        public const string BurstbugEntityPrefabPath =
            D0SpinePresentationFolder + "/PF_D0_BurstbugEntity.prefab";

        private const string FeiEntityPrefabPath =
            "Assets/FPGDemo/Presentation/Actors/Fei/PF_D0_FeiEntity.prefab";
        private const string LuanEntityPrefabPath =
            "Assets/FPGDemo/Presentation/Luan/Prefabs/PF_D0_LuanEntity.prefab";
        private const string HudieEntityPrefabPath =
            "Assets/FPGDemo/Presentation/Hudie/Prefabs/PF_D0_HudieEntity.prefab";
        private const string FeiCharacterDefinitionPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei.asset";
        private const string BurstbugEnemyDefinitionPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug.asset";
        private const string LuanEnemyDefinitionPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_Enemy.asset";
        private const string HudieEnemyDefinitionPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/D0_Hudie_Enemy.asset";
        private const string FeiPrefabPath =
            "Assets/Imported/CZN/Fei_30048/Preview/Prefabs/Fei_30048_Main.prefab";
        private const string BurstbugPrefabPath =
            "Assets/Imported/CZN/Monsters/Preview/Prefabs/CZN_1001003_Burstbug.prefab";
        private const string BurstbugEffectFolder =
            "Assets/Imported/CZN/Monsters/1001003/SpineSource/effect/";
        private const string FeiDerivedPrefix = "D0_Fei_30048_StraightAlpha";
        private const string BurstbugDerivedPrefix = "D0_Burstbug_1001003_StraightAlpha";
        private const string LuanSkeletonDataPath =
            "Assets/FPGDemo/Presentation/Luan/Spine/D0_Luan_StraightAlpha_SkeletonData.asset";
        private const string HudieSkeletonDataPath =
            "Assets/FPGDemo/Presentation/Hudie/Spine/D0_Hudie_StraightAlpha_SkeletonData.asset";
        private const string LuanGeneratedRenderPrefabPath =
            "Assets/FPGDemo/Presentation/Luan/Prefabs/PF_D0_Luan.prefab";
        private const string HudieGeneratedRenderPrefabPath =
            "Assets/FPGDemo/Presentation/Hudie/Prefabs/PF_D0_Hudie.prefab";

        [MenuItem("FPG Demo/D0 2.5D/Install or Update Combat Slice")]
        public static void InstallOrUpdateD0CombatSlice()
        {
            SceneSetup[] previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsureFolder("Assets/FPGDemo/Config");
                EnsureFolder(ConfigFolder);
                EnsureFolder("Assets/FPGDemo/Presentation");
                EnsureFolder(PresentationFolder);
                EnsureFolder(D0SpinePresentationFolder);

                CombatPresentationProfile profile = GetOrCreateProfile();
                CombatAudioBank audioBank = GetOrCreateAudioBank();
                D0SliceInstallationState installationState = GetOrCreateInstallationState();
                FpgDemoD0AudioBankAuthoring.EnsureCueMappings(audioBank);
                if (!FpgDemoD0ProceduralAudioGenerator.TryGenerateMissingProceduralAudio(
                        audioBank,
                        out string audioGenerationError))
                {
                    throw new InvalidOperationException(
                        $"D0 procedural audio generation failed: {audioGenerationError}");
                }
                EnsureGeneratedActorRenderPrefabs();
                ValidateAuthoredEntityPrefabs();
                EnsureBurstbugEffectPrefabs();
                // Opening a scene in Single mode can unload newly-created
                // editor assets that have not been persisted yet. Save and
                // reacquire all D0 assets after the scene transition instead
                // of holding a stale UnityEngine.Object reference.
                AssetDatabase.SaveAssets();

                Scene combatLab = EditorSceneManager.OpenScene(
                    CombatLabScenePath,
                    OpenSceneMode.Single);
                profile = LoadRequiredAsset<CombatPresentationProfile>(ProfilePath);
                audioBank = LoadRequiredAsset<CombatAudioBank>(AudioBankPath);
                installationState = LoadRequiredAsset<D0SliceInstallationState>(
                    InstallationStatePath);
                BattleSceneContext context = UnityEngine.Object.FindFirstObjectByType<BattleSceneContext>(
                    FindObjectsInactive.Include);
                if (context == null || context.PresentationRoot == null)
                {
                    throw new InvalidOperationException(
                        "CombatLab must provide BattleSceneContext.PresentationRoot before D0 slice installation.");
                }

                Transform root = FindOrCreateOwnedRoot(context.PresentationRoot);
                EnsureOwnedRoots(root);
                FpgDemoD0StageInstaller.Ensure(root, context, profile, audioBank);
                GetOrAddComponent<D0EvidenceCaptureDriver>(root.gameObject);
                D0SliceInstallationMarker marker = GetOrAddComponent<D0SliceInstallationMarker>(root.gameObject);
                ConfigureMarker(marker, profile, audioBank, installationState);

                ConfigureInstallationState(
                    installationState,
                    profile,
                    audioBank,
                    installationState.InstallationRevision);

                if (!installationState.TryValidate(out string stateError))
                {
                    throw new InvalidOperationException(
                        $"D0 slice installation state is invalid: {stateError}");
                }

                if (!marker.TryValidate(out string markerError))
                {
                    throw new InvalidOperationException(
                        $"D0 slice root is invalid: {markerError}");
                }

                if (!context.TryValidate(out string contextError))
                {
                    throw new InvalidOperationException(
                        $"CombatLab D0 scene context is invalid: {contextError}");
                }

                EditorSceneManager.MarkSceneDirty(combatLab);
                EditorSceneManager.SaveScene(combatLab);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
            }
        }

        /// <summary>
        /// Batch-mode entry point. It intentionally does not touch global
        /// Build Settings; G6 owns explicit build-scene selection.
        /// </summary>
        public static void InstallOrUpdateD0CombatSliceFromBatch()
        {
            InstallOrUpdateD0CombatSlice();
        }

        public static bool IsD0SliceInstalled()
        {
            D0SliceInstallationState state = AssetDatabase.LoadAssetAtPath<D0SliceInstallationState>(
                InstallationStatePath);
            return state != null && state.ProtectsCombatLab;
        }

        private static CombatPresentationProfile GetOrCreateProfile()
        {
            CombatPresentationProfile profile = AssetDatabase.LoadAssetAtPath<CombatPresentationProfile>(
                ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CombatPresentationProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            return profile;
        }

        private static CombatAudioBank GetOrCreateAudioBank()
        {
            CombatAudioBank audioBank = AssetDatabase.LoadAssetAtPath<CombatAudioBank>(AudioBankPath);
            if (audioBank == null)
            {
                audioBank = ScriptableObject.CreateInstance<CombatAudioBank>();
                AssetDatabase.CreateAsset(audioBank, AudioBankPath);
            }

            return audioBank;
        }

        private static D0SliceInstallationState GetOrCreateInstallationState()
        {
            D0SliceInstallationState state = AssetDatabase.LoadAssetAtPath<D0SliceInstallationState>(
                InstallationStatePath);
            if (state == null)
            {
                state = ScriptableObject.CreateInstance<D0SliceInstallationState>();
                AssetDatabase.CreateAsset(state, InstallationStatePath);
            }

            return state;
        }

        private static T LoadRequiredAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"D0 slice expected a persisted {typeof(T).Name} at '{path}'.");
            }

            return asset;
        }

        /// <summary>
        /// CZN atlases are stored as gamma-encoded PMA textures. A straight
        /// alpha derivative must therefore unpremultiply after sRGB decoding,
        /// then encode the recovered straight-linear colour back to sRGB.
        /// Dividing the gamma bytes directly makes translucent pixels much
        /// brighter in a Linear project. The resulting D0-owned material chain
        /// is warning-free in the Spine 3.8 runtime and keeps CZN data read-only.
        /// </summary>
        private static void EnsureGeneratedActorRenderPrefabs()
        {
            EnsureD0SpineActorPrefab(
                FeiPrefabPath,
                FeiDerivedPrefix,
                "Fei_30048_D0_StraightAlpha",
                "b_idle",
                false);
            EnsureD0SpineActorPrefab(
                BurstbugPrefabPath,
                BurstbugDerivedPrefix,
                "Burstbug_1001003_D0_StraightAlpha",
                "normal_idle",
                false);
            EnsureGeneratedActorRenderPrefab(
                LuanSkeletonDataPath,
                LuanGeneratedRenderPrefabPath,
                "PF_D0_Luan",
                "idle");
            EnsureGeneratedActorRenderPrefab(
                HudieSkeletonDataPath,
                HudieGeneratedRenderPrefabPath,
                "PF_D0_Hudie",
                "idle");
        }

        private static void EnsureGeneratedActorRenderPrefab(
            string skeletonDataPath,
            string generatedPrefabPath,
            string prefabName,
            string initialAnimation)
        {
            SkeletonDataAsset skeletonData =
                LoadRequiredAsset<SkeletonDataAsset>(skeletonDataPath);
            EnsureD0SpineActorPrefabAsset(
                skeletonData,
                generatedPrefabPath,
                prefabName,
                initialAnimation);
        }

        private static void ValidateAuthoredEntityPrefabs()
        {
            // Entity Prefabs are manually authored assets. The generator owns
            // only render derivatives, so missing or invalid Entity Prefabs
            // fail closed instead of being created or repaired here.
            ValidatePlayerEntityPrefab(
                "Fei",
                FeiEntityPrefabPath,
                FeiCharacterDefinitionPath);
            ValidateEnemyEntityPrefab(
                "Burstbug",
                BurstbugEntityPrefabPath,
                BurstbugEnemyDefinitionPath);
            ValidateEnemyEntityPrefab(
                "Luan",
                LuanEntityPrefabPath,
                LuanEnemyDefinitionPath);
            ValidateEnemyEntityPrefab(
                "Hudie",
                HudieEntityPrefabPath,
                HudieEnemyDefinitionPath);
        }

        private static void ValidatePlayerEntityPrefab(
            string actorName,
            string entityPrefabPath,
            string definitionPath)
        {
            GameObject entityPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(entityPrefabPath);
            if (entityPrefab == null)
            {
                throw new InvalidOperationException(
                    $"Authored {actorName} Entity Prefab is missing at '{entityPrefabPath}'. "
                    + "The D0 generator never creates or repairs Entity Prefabs.");
            }

            D0PlayerEntityView entityView =
                entityPrefab.GetComponent<D0PlayerEntityView>();
            D0ActorEntityView[] entityViews =
                entityPrefab.GetComponentsInChildren<D0ActorEntityView>(true);
            string entityError = entityView == null
                ? "D0PlayerEntityView component is missing from the Prefab root."
                : entityViews.Length != 1 || entityViews[0] != entityView
                    ? "Entity Prefab must contain exactly one root EntityView."
                    : string.Empty;
            if (!string.IsNullOrEmpty(entityError)
                || !entityView.TryValidate(out entityError))
            {
                throw new InvalidOperationException(
                    $"Authored {actorName} Entity Prefab is invalid: {entityError}");
            }

            D0CharacterDefinition character =
                LoadRequiredAsset<D0CharacterDefinition>(definitionPath);
            if (character.EntityPrefab != entityView)
            {
                throw new InvalidOperationException(
                    $"{actorName} definition must reference its authored Entity Prefab.");
            }

            if (!character.TryValidate(out string error))
            {
                throw new InvalidOperationException(
                    $"{actorName} entity definition is invalid: {error}");
            }
        }

        private static void ValidateEnemyEntityPrefab(
            string actorName,
            string entityPrefabPath,
            string definitionPath)
        {
            GameObject entityPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(entityPrefabPath);
            if (entityPrefab == null)
            {
                throw new InvalidOperationException(
                    $"Authored {actorName} Entity Prefab is missing at '{entityPrefabPath}'. "
                    + "The D0 generator never creates or repairs Entity Prefabs.");
            }

            D0EnemyEntityView entityView =
                entityPrefab.GetComponent<D0EnemyEntityView>();
            D0ActorEntityView[] entityViews =
                entityPrefab.GetComponentsInChildren<D0ActorEntityView>(true);
            string entityError = entityView == null
                ? "D0EnemyEntityView component is missing from the Prefab root."
                : entityViews.Length != 1 || entityViews[0] != entityView
                    ? "Entity Prefab must contain exactly one root EntityView."
                    : string.Empty;
            if (!string.IsNullOrEmpty(entityError)
                || !entityView.TryValidate(out entityError))
            {
                throw new InvalidOperationException(
                    $"Authored {actorName} Entity Prefab is invalid: {entityError}");
            }

            D0EnemyDefinition enemy =
                LoadRequiredAsset<D0EnemyDefinition>(definitionPath);
            if (enemy.EntityPrefab != entityView)
            {
                throw new InvalidOperationException(
                    $"{actorName} definition must reference its authored Entity Prefab.");
            }

            if (!enemy.TryValidate(out string error))
            {
                throw new InvalidOperationException(
                    $"{actorName} entity definition is invalid: {error}");
            }
        }

        /// <summary>
        /// Updates the six generated Burstbug VFX render derivatives used by G3.
        /// Imported effects need their D0-owned additive blend materials, so
        /// this remains separate from the generated actor render Prefabs.
        /// </summary>
        private static void EnsureBurstbugEffectPrefabs()
        {
            EnsureD0SpineEffectPrefab(
                BurstbugEffectFolder + "burstbug_skill1_SkeletonData.asset",
                "D0_Burstbug_1001003_Fx_Skill1",
                BurstbugFastFxPrefabPath,
                "Burstbug_1001003_D0_Fx_Skill1_StraightAlpha",
                false);
            EnsureD0SpineEffectPrefab(
                BurstbugEffectFolder + "burstbug_skill2_SkeletonData.asset",
                "D0_Burstbug_1001003_Fx_Skill2",
                BurstbugVolleyFxPrefabPath,
                "Burstbug_1001003_D0_Fx_Skill2_StraightAlpha",
                false);
            EnsureD0SpineEffectPrefab(
                BurstbugEffectFolder + "invader_death_01_f1_SkeletonData.asset",
                "D0_Burstbug_1001003_Fx_Death1",
                BurstbugDeathFx1PrefabPath,
                "Burstbug_1001003_D0_Fx_Death1_StraightAlpha",
                false);
            EnsureD0SpineEffectPrefab(
                BurstbugEffectFolder + "invader_death_01_f2_SkeletonData.asset",
                "D0_Burstbug_1001003_Fx_Death2",
                BurstbugDeathFx2PrefabPath,
                "Burstbug_1001003_D0_Fx_Death2_StraightAlpha",
                false);
            EnsureD0SpineEffectPrefab(
                BurstbugEffectFolder + "invader_death_01_f3_SkeletonData.asset",
                "D0_Burstbug_1001003_Fx_Death3",
                BurstbugDeathFx3PrefabPath,
                "Burstbug_1001003_D0_Fx_Death3_StraightAlpha",
                false);
            EnsureD0SpineEffectPrefab(
                BurstbugEffectFolder + "invader_death_01_f4_SkeletonData.asset",
                "D0_Burstbug_1001003_Fx_Death4",
                BurstbugDeathFx4PrefabPath,
                "Burstbug_1001003_D0_Fx_Death4_StraightAlpha",
                false);
        }

        private static GameObject EnsureD0SpineEffectPrefab(
            string sourceDataPath,
            string derivedPrefix,
            string derivedPrefabPath,
            string prefabName,
            bool usePremultipliedAlpha)
        {
            SkeletonDataAsset sourceData = LoadRequiredAsset<SkeletonDataAsset>(sourceDataPath);
            SpineAtlasAsset sourceAtlas = GetSingleSpineAtlas(
                sourceData,
                $"D0 Burstbug effect '{sourceDataPath}'");
            if (sourceAtlas.MaterialCount != 1)
            {
                throw new InvalidOperationException(
                    $"D0 Burstbug effect '{sourceDataPath}' must use exactly one main atlas material.");
            }

            Material sourceMaterial = sourceAtlas.PrimaryMaterial;
            Texture2D sourceTexture = sourceMaterial == null
                ? null
                : sourceMaterial.mainTexture as Texture2D;
            if (sourceMaterial == null || sourceTexture == null)
            {
                throw new InvalidOperationException(
                    $"D0 Burstbug effect '{sourceDataPath}' requires a Texture2D-backed main atlas material.");
            }

            string sourceTexturePath = AssetDatabase.GetAssetPath(sourceTexture);
            if (string.IsNullOrEmpty(sourceTexturePath))
            {
                throw new InvalidOperationException(
                    $"D0 Burstbug effect '{sourceDataPath}' main atlas texture is not an AssetDatabase asset.");
            }

            string derivedTexturePath = D0SpinePresentationFolder + "/" + derivedPrefix + ".png";
            Texture2D derivedTexture = EnsureD0SpineTexture(
                sourceTexture,
                derivedTexturePath,
                usePremultipliedAlpha);
            SpineAtlasAsset derivedAtlas = EnsureD0SpineAtlas(
                sourceAtlas,
                sourceMaterial,
                derivedTexture,
                sourceTexturePath,
                D0SpinePresentationFolder + "/" + derivedPrefix + ".atlas.txt",
                derivedTexturePath,
                D0SpinePresentationFolder + "/" + derivedPrefix + "_Atlas.asset",
                derivedPrefix + "_Atlas",
                // spine-unity validates the importer-generated material named
                // after the atlas file. Reconfigure that exact material rather
                // than leaving a stale PMA sibling that emits warnings.
                derivedPrefix + "_Material",
                usePremultipliedAlpha);
            SkeletonDataAsset derivedData = EnsureD0SpineEffectSkeletonData(
                sourceData,
                derivedAtlas,
                sourceTexturePath,
                derivedTexture,
                derivedTexturePath,
                D0SpinePresentationFolder + "/" + derivedPrefix + "_SkeletonData.asset",
                derivedPrefix + "_SkeletonData",
                derivedPrefix,
                usePremultipliedAlpha);
            return EnsureD0SpineEffectPrefab(
                derivedData,
                derivedPrefabPath,
                prefabName);
        }

        private static SpineAtlasAsset GetSingleSpineAtlas(
            SkeletonDataAsset sourceData,
            string sourceLabel)
        {
            if (sourceData == null
                || sourceData.atlasAssets == null
                || sourceData.atlasAssets.Length != 1
                || !(sourceData.atlasAssets[0] is SpineAtlasAsset sourceAtlas))
            {
                throw new InvalidOperationException(
                    $"{sourceLabel} must use exactly one SpineAtlasAsset.");
            }

            return sourceAtlas;
        }

        private static SkeletonDataAsset EnsureD0SpineEffectSkeletonData(
            SkeletonDataAsset sourceData,
            SpineAtlasAsset derivedAtlas,
            string sourceTexturePath,
            Texture2D derivedTexture,
            string derivedTexturePath,
            string derivedDataPath,
            string derivedName,
            string derivedPrefix,
            bool usePremultipliedAlpha)
        {
            SkeletonDataAsset derivedData = EnsureD0SpineSkeletonData(
                sourceData,
                derivedAtlas,
                derivedDataPath,
                derivedName);
            ConfigureDerivedBlendModeMaterials(
                sourceData,
                derivedData,
                sourceTexturePath,
                derivedTexture,
                derivedTexturePath,
                derivedPrefix,
                usePremultipliedAlpha);
            derivedData.Clear();
            EditorUtility.SetDirty(derivedData);
            return derivedData;
        }

        private static void ConfigureDerivedBlendModeMaterials(
            SkeletonDataAsset sourceData,
            SkeletonDataAsset derivedData,
            string sourceTexturePath,
            Texture2D derivedTexture,
            string derivedTexturePath,
            string derivedPrefix,
            bool usePremultipliedAlpha)
        {
            if (sourceData == null || derivedData == null)
            {
                throw new ArgumentNullException(
                    sourceData == null ? nameof(sourceData) : nameof(derivedData));
            }

            ConfigureDerivedBlendReplacementList(
                sourceData.blendModeMaterials.additiveMaterials,
                derivedData.blendModeMaterials.additiveMaterials,
                "Additive",
                sourceTexturePath,
                derivedTexture,
                derivedTexturePath,
                derivedPrefix,
                usePremultipliedAlpha);
            ConfigureDerivedBlendReplacementList(
                sourceData.blendModeMaterials.multiplyMaterials,
                derivedData.blendModeMaterials.multiplyMaterials,
                "Multiply",
                sourceTexturePath,
                derivedTexture,
                derivedTexturePath,
                derivedPrefix,
                usePremultipliedAlpha);
            ConfigureDerivedBlendReplacementList(
                sourceData.blendModeMaterials.screenMaterials,
                derivedData.blendModeMaterials.screenMaterials,
                "Screen",
                sourceTexturePath,
                derivedTexture,
                derivedTexturePath,
                derivedPrefix,
                usePremultipliedAlpha);
        }

        private static void ConfigureDerivedBlendReplacementList(
            System.Collections.Generic.List<BlendModeMaterials.ReplacementMaterial> sourceReplacements,
            System.Collections.Generic.List<BlendModeMaterials.ReplacementMaterial> derivedReplacements,
            string blendLabel,
            string sourceTexturePath,
            Texture2D derivedTexture,
            string derivedTexturePath,
            string derivedPrefix,
            bool usePremultipliedAlpha)
        {
            if (derivedReplacements == null)
            {
                throw new InvalidOperationException(
                    $"D0 {blendLabel} blend replacement list is unavailable.");
            }

            derivedReplacements.Clear();
            if (sourceReplacements == null || sourceReplacements.Count == 0)
            {
                return;
            }

            string sourcePageName = Path.GetFileName(sourceTexturePath);
            string derivedPageName = Path.GetFileName(derivedTexturePath);
            if (string.IsNullOrEmpty(sourcePageName) || string.IsNullOrEmpty(derivedPageName))
            {
                throw new InvalidOperationException(
                    "D0 blend-material derivation requires valid atlas page names.");
            }

            for (int index = 0; index < sourceReplacements.Count; index++)
            {
                BlendModeMaterials.ReplacementMaterial sourceReplacement = sourceReplacements[index];
                if (sourceReplacement == null || sourceReplacement.material == null)
                {
                    throw new InvalidOperationException(
                        $"D0 {blendLabel} blend replacement {index} is missing its source material.");
                }

                if (!string.Equals(sourceReplacement.pageName, sourcePageName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"D0 {blendLabel} blend replacement {index} targets unexpected atlas page '{sourceReplacement.pageName}'.");
                }

                string materialName = derivedPrefix + "_" + blendLabel + "_" + index;
                string materialPath = D0SpinePresentationFolder + "/" + materialName + ".mat";
                Material derivedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (derivedMaterial == null)
                {
                    derivedMaterial = new Material(sourceReplacement.material)
                    {
                        name = materialName
                    };
                    AssetDatabase.CreateAsset(derivedMaterial, materialPath);
                }

                ConfigureD0SpineMaterial(
                    sourceReplacement.material,
                    derivedTexture,
                    derivedMaterial,
                    materialName,
                    usePremultipliedAlpha);
                derivedReplacements.Add(new BlendModeMaterials.ReplacementMaterial
                {
                    pageName = derivedPageName,
                    material = derivedMaterial
                });
            }
        }

        private static GameObject EnsureD0SpineEffectPrefab(
            SkeletonDataAsset derivedData,
            string derivedPrefabPath,
            string prefabName)
        {
            GameObject authoredRoot = new GameObject(prefabName);
            try
            {
                SkeletonAnimation skeleton = authoredRoot.AddComponent<SkeletonAnimation>();
                ConfigureDerivedSkeletonAnimation(
                    skeleton,
                    derivedData,
                    "animation",
                    false);
                PrefabUtility.SaveAsPrefabAsset(authoredRoot, derivedPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(authoredRoot);
            }

            GameObject derivedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(derivedPrefabPath);
            if (derivedPrefab == null)
            {
                throw new InvalidOperationException(
                    $"D0 Burstbug effect prefab could not be loaded: '{derivedPrefabPath}'.");
            }

            return derivedPrefab;
        }

        private static GameObject EnsureD0SpineActorPrefab(
            string canonicalPrefabPath,
            string derivedPrefix,
            string prefabName,
            string initialAnimation,
            bool usePremultipliedAlpha)
        {
            SkeletonDataAsset sourceData = FindSingleSkeletonDataAsset(canonicalPrefabPath);
            if (sourceData.atlasAssets == null || sourceData.atlasAssets.Length != 1
                || !(sourceData.atlasAssets[0] is SpineAtlasAsset sourceAtlas)
                || sourceAtlas.MaterialCount != 1)
            {
                throw new InvalidOperationException(
                    $"D0 actor '{canonicalPrefabPath}' must use exactly one SpineAtlasAsset/material.");
            }

            Material sourceMaterial = sourceAtlas.PrimaryMaterial;
            Texture2D sourceTexture = sourceMaterial == null
                ? null
                : sourceMaterial.mainTexture as Texture2D;
            if (sourceMaterial == null || sourceTexture == null)
            {
                throw new InvalidOperationException(
                    $"D0 actor '{canonicalPrefabPath}' requires a Texture2D-backed primary Spine material.");
            }

            string sourceTexturePath = AssetDatabase.GetAssetPath(sourceTexture);
            if (string.IsNullOrEmpty(sourceTexturePath))
            {
                throw new InvalidOperationException(
                    $"D0 actor '{canonicalPrefabPath}' primary Spine texture is not an AssetDatabase asset.");
            }

            Texture2D derivedTexture = EnsureD0SpineTexture(
                sourceTexture,
                D0SpinePresentationFolder + "/" + derivedPrefix + ".png",
                usePremultipliedAlpha);
            SpineAtlasAsset derivedAtlas = EnsureD0SpineAtlas(
                sourceAtlas,
                sourceMaterial,
                derivedTexture,
                sourceTexturePath,
                D0SpinePresentationFolder + "/" + derivedPrefix + ".atlas.txt",
                D0SpinePresentationFolder + "/" + derivedPrefix + ".png",
                D0SpinePresentationFolder + "/" + derivedPrefix + "_Atlas.asset",
                derivedPrefix + "_Atlas",
                derivedPrefix + "_Material",
                usePremultipliedAlpha);
            SkeletonDataAsset derivedData = EnsureD0SpineSkeletonData(
                sourceData,
                derivedAtlas,
                D0SpinePresentationFolder + "/" + derivedPrefix + "_SkeletonData.asset",
                derivedPrefix + "_SkeletonData");
            return EnsureD0SpineActorPrefabAsset(
                derivedData,
                D0SpinePresentationFolder + "/" + derivedPrefix + ".prefab",
                prefabName,
                initialAnimation);
        }

        private static SkeletonDataAsset FindSingleSkeletonDataAsset(string prefabPath)
        {
            string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
            SkeletonDataAsset sourceData = null;
            for (int index = 0; index < dependencies.Length; index++)
            {
                SkeletonDataAsset candidate = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(
                    dependencies[index]);
                if (candidate == null)
                {
                    continue;
                }

                if (sourceData != null && sourceData != candidate)
                {
                    throw new InvalidOperationException(
                        $"D0 actor '{prefabPath}' has more than one SkeletonDataAsset dependency.");
                }

                sourceData = candidate;
            }

            if (sourceData == null)
            {
                throw new InvalidOperationException(
                    $"D0 actor '{prefabPath}' requires one imported SkeletonDataAsset dependency.");
            }

            return sourceData;
        }

        private static Texture2D EnsureD0SpineTexture(
            Texture2D sourceTexture,
            string derivedTexturePath,
            bool usePremultipliedAlpha)
        {
            string sourceTexturePath = AssetDatabase.GetAssetPath(sourceTexture);
            if (string.IsNullOrEmpty(sourceTexturePath)
                || !sourceTexturePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "D0 Spine derivation requires a PNG source texture.");
            }

            byte[] derivedPng = usePremultipliedAlpha
                ? File.ReadAllBytes(GetAbsoluteAssetPath(sourceTexturePath))
                : CreateStraightAlphaPng(sourceTexturePath);
            string absoluteOutputPath = GetAbsoluteAssetPath(derivedTexturePath);
            string outputDirectory = Path.GetDirectoryName(absoluteOutputPath);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                throw new InvalidOperationException(
                    $"D0 Spine output path is invalid: '{derivedTexturePath}'.");
            }

            Directory.CreateDirectory(outputDirectory);
            bool writeRequired = !File.Exists(absoluteOutputPath)
                || !AreEqual(File.ReadAllBytes(absoluteOutputPath), derivedPng);
            if (writeRequired)
            {
                File.WriteAllBytes(absoluteOutputPath, derivedPng);
                AssetDatabase.ImportAsset(
                    derivedTexturePath,
                    ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = AssetImporter.GetAtPath(derivedTexturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"D0 Spine texture importer is unavailable: '{derivedTexturePath}'.");
            }

            bool importerChanged = importer.textureType != TextureImporterType.Default
                || !importer.sRGBTexture
                || importer.alphaIsTransparency == usePremultipliedAlpha
                || importer.mipmapEnabled
                || importer.isReadable
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.filterMode != sourceTexture.filterMode
                || importer.wrapMode != sourceTexture.wrapMode
                || importer.anisoLevel != sourceTexture.anisoLevel;
            if (importerChanged)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                // The output holds sRGB-encoded straight-alpha colour, so
                // dilation is safe and required by Spine's straight-alpha
                // texture validation in this Linear project.
                importer.alphaIsTransparency = !usePremultipliedAlpha;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = sourceTexture.filterMode;
                importer.wrapMode = sourceTexture.wrapMode;
                importer.anisoLevel = sourceTexture.anisoLevel;
                importer.SaveAndReimport();
            }

            Texture2D derivedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(derivedTexturePath);
            if (derivedTexture == null)
            {
                throw new InvalidOperationException(
                    $"D0 Spine texture could not be loaded: '{derivedTexturePath}'.");
            }

            return derivedTexture;
        }

        private static byte[] CreateStraightAlphaPng(string sourceTexturePath)
        {
            Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            Texture2D straightAlpha = null;
            try
            {
                byte[] sourceBytes = File.ReadAllBytes(GetAbsoluteAssetPath(sourceTexturePath));
                if (!ImageConversion.LoadImage(decoded, sourceBytes, false))
                {
                    throw new InvalidOperationException(
                        $"D0 straight-alpha derivation could not decode '{sourceTexturePath}'.");
                }

                Color32[] pixels = decoded.GetPixels32();
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color32 pixel = pixels[index];
                    if (pixel.a == 0)
                    {
                        pixel.r = 0;
                        pixel.g = 0;
                        pixel.b = 0;
                    }
                    else
                    {
                        pixel.r = UnpremultiplyLinear(pixel.r, pixel.a);
                        pixel.g = UnpremultiplyLinear(pixel.g, pixel.a);
                        pixel.b = UnpremultiplyLinear(pixel.b, pixel.a);
                    }

                    pixels[index] = pixel;
                }

                straightAlpha = new Texture2D(
                    decoded.width,
                    decoded.height,
                    TextureFormat.RGBA32,
                    false,
                    false);
                straightAlpha.SetPixels32(pixels);
                straightAlpha.Apply(false, false);
                return ImageConversion.EncodeToPNG(straightAlpha);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decoded);
                if (straightAlpha != null)
                {
                    UnityEngine.Object.DestroyImmediate(straightAlpha);
                }
            }
        }

        private static byte UnpremultiplyLinear(byte gammaPremultiplied, byte alpha)
        {
            float alpha01 = alpha / 255f;
            float premultipliedLinear = Mathf.GammaToLinearSpace(gammaPremultiplied / 255f);
            float straightLinear = Mathf.Clamp01(premultipliedLinear / alpha01);
            float straightGamma = Mathf.LinearToGammaSpace(straightLinear);
            return (byte)Mathf.Clamp(Mathf.RoundToInt(straightGamma * 255f), 0, 255);
        }

        private static void ConfigureD0SpineMaterial(
            Material sourceMaterial,
            Texture2D derivedTexture,
            Material derivedMaterial,
            string derivedName,
            bool usePremultipliedAlpha)
        {
            if (sourceMaterial == null || derivedTexture == null || derivedMaterial == null)
            {
                throw new ArgumentNullException(
                    sourceMaterial == null
                        ? nameof(sourceMaterial)
                        : derivedTexture == null
                            ? nameof(derivedTexture)
                            : nameof(derivedMaterial));
            }

            // Importing a .atlas.txt through spine-unity owns the generated
            // Material asset. Do not create a sibling material before that
            // import: the importer will replace it and leave the manually
            // held object destroyed. Instead copy the source settings onto
            // the generated material after the atlas importer is finished.
            EditorUtility.CopySerialized(sourceMaterial, derivedMaterial);
            if (!derivedMaterial.HasProperty("_StraightAlphaInput"))
            {
                throw new InvalidOperationException(
                    $"D0 Spine material '{sourceMaterial.name}' does not expose its alpha-workflow property.");
            }

            derivedMaterial.name = derivedName;
            derivedMaterial.mainTexture = derivedTexture;
            MaterialChecks.EnablePMAAtMaterial(derivedMaterial, usePremultipliedAlpha);
            EditorUtility.SetDirty(derivedMaterial);
        }

        private static SpineAtlasAsset EnsureD0SpineAtlas(
            SpineAtlasAsset sourceAtlas,
            Material sourceMaterial,
            Texture2D derivedTexture,
            string sourceTexturePath,
            string derivedAtlasTextPath,
            string derivedTexturePath,
            string derivedAtlasPath,
            string derivedName,
            string derivedMaterialName,
            bool usePremultipliedAlpha)
        {
            TextAsset derivedAtlasFile = EnsureD0SpineAtlasText(
                sourceAtlas == null ? null : sourceAtlas.atlasFile,
                sourceTexturePath,
                derivedTexturePath,
                derivedAtlasTextPath);
            SpineAtlasAsset derivedAtlas = AssetDatabase.LoadAssetAtPath<SpineAtlasAsset>(derivedAtlasPath);
            if (derivedAtlas == null)
            {
                // The spine importer normally creates this asset from the atlas
                // text. Keep a direct, D0-owned fallback so an interrupted
                // import cannot make the installer non-recoverable.
                derivedAtlas = ScriptableObject.CreateInstance<SpineAtlasAsset>();
                AssetDatabase.CreateAsset(derivedAtlas, derivedAtlasPath);
            }

            string derivedMaterialPath = Path.Combine(
                    Path.GetDirectoryName(derivedAtlasPath) ?? string.Empty,
                    derivedMaterialName + ".mat")
                .Replace('\\', '/');
            Material derivedMaterial = AssetDatabase.LoadAssetAtPath<Material>(derivedMaterialPath);
            if (derivedMaterial == null)
            {
                derivedMaterial = new Material(sourceMaterial)
                {
                    name = derivedMaterialName
                };
                AssetDatabase.CreateAsset(derivedMaterial, derivedMaterialPath);
            }

            ConfigureD0SpineMaterial(
                sourceMaterial,
                derivedTexture,
                derivedMaterial,
                derivedMaterialName,
                usePremultipliedAlpha);
            derivedAtlas.name = derivedName;
            derivedAtlas.atlasFile = derivedAtlasFile;
            derivedAtlas.materials = new[] { derivedMaterial };
            derivedAtlas.Clear();
            EditorUtility.SetDirty(derivedAtlas);
            return derivedAtlas;
        }

        /// <summary>
        /// spine-unity resolves an atlas page by comparing the atlas page name
        /// with <see cref="Material.mainTexture"/>'s asset name. A copied
        /// D0-owned texture therefore needs a copied atlas text whose sole
        /// page points at the D0-owned PNG, not the canonical CZN page.
        /// </summary>
        private static TextAsset EnsureD0SpineAtlasText(
            TextAsset sourceAtlasFile,
            string sourceTexturePath,
            string derivedTexturePath,
            string derivedAtlasTextPath)
        {
            if (sourceAtlasFile == null)
            {
                throw new InvalidOperationException(
                    "D0 Spine derivation requires a source Spine atlas text asset.");
            }

            string sourcePageName = Path.GetFileName(sourceTexturePath);
            string derivedPageName = Path.GetFileName(derivedTexturePath);
            if (string.IsNullOrEmpty(sourcePageName) || string.IsNullOrEmpty(derivedPageName))
            {
                throw new InvalidOperationException(
                    "D0 Spine derivation requires valid source and derived atlas page names.");
            }

            string sourceText = sourceAtlasFile.text ?? string.Empty;
            string rewrittenText = ReplaceAtlasPageName(
                sourceText,
                sourcePageName,
                derivedPageName,
                out bool replaced);
            if (!replaced)
            {
                throw new InvalidOperationException(
                    $"D0 Spine derivation could not find atlas page '{sourcePageName}'.");
            }

            string absoluteOutputPath = GetAbsoluteAssetPath(derivedAtlasTextPath);
            string outputDirectory = Path.GetDirectoryName(absoluteOutputPath);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                throw new InvalidOperationException(
                    $"D0 Spine atlas output path is invalid: '{derivedAtlasTextPath}'.");
            }

            Directory.CreateDirectory(outputDirectory);
            bool writeRequired = !File.Exists(absoluteOutputPath)
                || !string.Equals(
                    File.ReadAllText(absoluteOutputPath),
                    rewrittenText,
                    StringComparison.Ordinal);
            if (writeRequired)
            {
                File.WriteAllText(absoluteOutputPath, rewrittenText);
                AssetDatabase.ImportAsset(
                    derivedAtlasTextPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }

            TextAsset derivedAtlasFile = AssetDatabase.LoadAssetAtPath<TextAsset>(derivedAtlasTextPath);
            if (derivedAtlasFile == null)
            {
                throw new InvalidOperationException(
                    $"D0 Spine atlas text could not be loaded: '{derivedAtlasTextPath}'.");
            }

            return derivedAtlasFile;
        }

        private static string ReplaceAtlasPageName(
            string atlasText,
            string sourcePageName,
            string derivedPageName,
            out bool replaced)
        {
            string lineEnding = atlasText.IndexOf("\r\n", StringComparison.Ordinal) >= 0
                ? "\r\n"
                : "\n";
            string[] lines = atlasText.Replace("\r\n", "\n").Split('\n');
            replaced = false;
            for (int index = 0; index < lines.Length; index++)
            {
                if (!string.Equals(
                        lines[index].Trim(),
                        sourcePageName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                lines[index] = derivedPageName;
                replaced = true;
                break;
            }

            return string.Join(lineEnding, lines);
        }

        private static SkeletonDataAsset EnsureD0SpineSkeletonData(
            SkeletonDataAsset sourceData,
            SpineAtlasAsset derivedAtlas,
            string derivedDataPath,
            string derivedName)
        {
            SkeletonDataAsset derivedData = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(derivedDataPath);
            if (derivedData == null)
            {
                derivedData = UnityEngine.Object.Instantiate(sourceData);
                AssetDatabase.CreateAsset(derivedData, derivedDataPath);
            }
            else
            {
                EditorUtility.CopySerialized(sourceData, derivedData);
            }

            derivedData.name = derivedName;
            derivedData.atlasAssets = new AtlasAssetBase[] { derivedAtlas };
            derivedData.Clear();
            EditorUtility.SetDirty(derivedData);
            return derivedData;
        }

        private static GameObject EnsureD0SpineActorPrefabAsset(
            SkeletonDataAsset derivedData,
            string derivedPrefabPath,
            string prefabName,
            string initialAnimation)
        {
            GameObject authoredRoot = new GameObject(prefabName);
            try
            {
                SkeletonAnimation skeleton = authoredRoot.AddComponent<SkeletonAnimation>();
                ConfigureDerivedSkeletonAnimation(
                    skeleton,
                    derivedData,
                    initialAnimation,
                    true);
                // Generated assets are render-only. Socket registries and
                // gameplay/presenter components belong to the authored Entity
                // Prefab and must never be regenerated here.
                PrefabUtility.SaveAsPrefabAsset(authoredRoot, derivedPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(authoredRoot);
            }

            GameObject derivedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(derivedPrefabPath);
            if (derivedPrefab == null)
            {
                throw new InvalidOperationException(
                    $"D0 actor prefab could not be loaded: '{derivedPrefabPath}'.");
            }

            return derivedPrefab;
        }

        private static void ConfigureDerivedSkeletonAnimation(
            SkeletonAnimation skeleton,
            SkeletonDataAsset derivedData,
            string initialAnimation,
            bool shouldLoop)
        {
            SerializedObject serialized = new SerializedObject(skeleton);
            SerializedProperty skeletonData = serialized.FindProperty("skeletonDataAsset");
            SerializedProperty animation = serialized.FindProperty("_animationName");
            SerializedProperty loop = serialized.FindProperty("loop");
            SerializedProperty pmaVertexColors = serialized.FindProperty("pmaVertexColors");
            SerializedProperty useClipping = serialized.FindProperty("useClipping");
            SerializedProperty disableRenderingOnOverride = serialized.FindProperty("disableRenderingOnOverride");
            if (skeletonData == null || animation == null || loop == null
                || pmaVertexColors == null || useClipping == null
                || disableRenderingOnOverride == null)
            {
                throw new InvalidOperationException(
                    "SkeletonAnimation no longer exposes the D0 presentation prefab bindings.");
            }

            skeletonData.objectReferenceValue = derivedData;
            animation.stringValue = initialAnimation;
            loop.boolValue = shouldLoop;
            // Spine/Skeleton converts a straight-alpha texture to PMA in its
            // fragment shader. Vertex colours must therefore remain PMA for
            // both input workflows; false breaks fades and additive slots.
            pmaVertexColors.boolValue = true;
            useClipping.boolValue = true;
            disableRenderingOnOverride.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skeleton);
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            const string assetsPrefix = "Assets/";
            if (string.IsNullOrEmpty(assetPath)
                || !assetPath.StartsWith(assetsPrefix, StringComparison.Ordinal))
            {
                throw new ArgumentException("An Assets-relative path is required.", nameof(assetPath));
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("Unable to resolve the Unity project root for D0 asset generation.");
            }

            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static bool AreEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static Transform FindOrCreateOwnedRoot(Transform presentationRoot)
        {
            Transform root = presentationRoot.Find(RootName);
            if (root == null)
            {
                GameObject rootObject = new GameObject(RootName);
                root = rootObject.transform;
                root.SetParent(presentationRoot, false);
            }

            return root;
        }

        private static void EnsureOwnedRoots(Transform root)
        {
            FindOrCreateDirectChild(root, "D0Stage");
            FindOrCreateDirectChild(root, "D0WorldFx");
            FindOrCreateDirectChild(root, "D0ScreenFx");
            FindOrCreateDirectChild(root, "D0Canvas");
            FindOrCreateDirectChild(root, "D0Audio");
            FindOrCreateDirectChild(root, "D0DevelopmentOverlay");
        }

        private static Transform FindOrCreateDirectChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                GameObject childObject = new GameObject(name);
                child = childObject.transform;
                child.SetParent(parent, false);
            }

            if (child.parent != parent)
            {
                throw new InvalidOperationException(
                    $"D0 slice owned root '{name}' must be a direct child of '{parent.name}'.");
            }

            return child;
        }

        private static T GetOrAddComponent<T>(GameObject target)
            where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void ConfigureMarker(
            D0SliceInstallationMarker marker,
            CombatPresentationProfile profile,
            CombatAudioBank audioBank,
            D0SliceInstallationState installationState)
        {
            SerializedObject serializedMarker = new SerializedObject(marker);
            serializedMarker.FindProperty("presentationProfile").objectReferenceValue = profile;
            serializedMarker.FindProperty("audioBank").objectReferenceValue = audioBank;
            serializedMarker.FindProperty("installationState").objectReferenceValue = installationState;
            serializedMarker.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(marker);
        }

        private static void ConfigureInstallationState(
            D0SliceInstallationState state,
            CombatPresentationProfile profile,
            CombatAudioBank audioBank,
            int nextRevision)
        {
            SerializedObject serializedState = new SerializedObject(state);
            serializedState.FindProperty("installationComplete").boolValue = true;
            serializedState.FindProperty("ownedScenePath").stringValue = CombatLabScenePath;
            serializedState.FindProperty("presentationProfile").objectReferenceValue = profile;
            serializedState.FindProperty("audioBank").objectReferenceValue = audioBank;
            // This is an installation format version, not a run counter: a
            // second install must leave authored state stable when no schema
            // migration is required.
            serializedState.FindProperty("installationRevision").intValue = Math.Max(1, nextRevision);
            serializedState.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(state);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slashIndex = path.LastIndexOf('/');
            if (slashIndex <= 0 || slashIndex >= path.Length - 1)
            {
                throw new InvalidOperationException($"Unable to create invalid asset folder '{path}'.");
            }

            string parent = path.Substring(0, slashIndex);
            string child = path.Substring(slashIndex + 1);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
