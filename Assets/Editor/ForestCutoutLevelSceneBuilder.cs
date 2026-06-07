using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using VolumetricFogAndMist2;
using VolumetricLights;

namespace NewFPG.EditorTools
{
    [InitializeOnLoad]
    public static class ForestCutoutLevelSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/LevelScene.unity";
        private const string ArtDir = "Assets/Art/Scenes/树林切图";
        private const string RootName = "ForestCutoutScene_Root";
        private const string AtmosphereRootName = "ForestAtmosphere_Root";
        private const string LegacyRootName = "Legacy_PrototypeVisuals_Disabled";
        private const string AutoRunKey = "NewFPG.ForestCutoutLevelSceneBuilder.AutoRun.20260605.02";
        private const string AutoRunSessionKey = "NewFPG.ForestCutoutLevelSceneBuilder.AutoRunScheduled";

        static ForestCutoutLevelSceneBuilder()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (EditorPrefs.GetBool(AutoRunKey, false) || SessionState.GetBool(AutoRunSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(AutoRunSessionKey, true);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    SessionState.SetBool(AutoRunSessionKey, false);
                    return;
                }

                try
                {
                    BuildLevelScene();
                    EditorPrefs.SetBool(AutoRunKey, true);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ForestCutoutLevelSceneBuilder] Auto build failed: {ex}");
                    SessionState.SetBool(AutoRunSessionKey, false);
                }
            };
        }

        [MenuItem("Tools/NewFPG/Build Forest Cutout LevelScene", false, 2100)]
        public static void BuildLevelScene()
        {
            Scene scene = EnsureLevelSceneLoaded();
            if (!scene.IsValid())
            {
                throw new InvalidOperationException($"Could not open scene: {ScenePath}");
            }

            EnsureSpriteImportSettings();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            DestroyRoot(RootName);
            DestroyRoot(AtmosphereRootName);

            GameObject legacyRoot = EnsureRoot(LegacyRootName);
            legacyRoot.SetActive(false);
            OrganizeLegacyVisuals(legacyRoot);
            HidePrototypeRenderers();
            EnsurePlayerVisual();

            GameObject sceneRoot = EnsureRoot(RootName);
            GameObject background = CreateGroup("00_Background", sceneRoot.transform);
            GameObject midground = CreateGroup("10_Midground", sceneRoot.transform);
            GameObject ground = CreateGroup("20_GroundAndPath", sceneRoot.transform);
            GameObject foreground = CreateGroup("30_Foreground", sceneRoot.transform);
            GameObject props = CreateGroup("40_DepthBreakup", sceneRoot.transform);

            BuildCutoutComposition(background.transform, midground.transform, ground.transform, foreground.transform, props.transform);
            BuildAtmosphere();
            ConfigureCameraAndLighting();
            EnsureFogRenderFeatures();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ForestCutoutLevelSceneBuilder] LevelScene forest cutout layout, VolumetricFog2 fog, and VolumetricLights lighting were generated.");
        }

        public static void BuildFromCommandLine()
        {
            BuildLevelScene();
        }

        private static Scene EnsureLevelSceneLoaded()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && string.Equals(active.path, ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                return active;
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void EnsureSpriteImportSettings()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }

                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }

                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    dirty = true;
                }

                if (Math.Abs(importer.spritePixelsPerUnit - 100f) > 0.01f)
                {
                    importer.spritePixelsPerUnit = 100f;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static void BuildCutoutComposition(Transform background, Transform midground, Transform ground, Transform foreground, Transform props)
        {
            Material transparentCutout = CreateOrUpdateSpriteLitMaterial("ForestCutout_TransparentSprite");

            AddSprite("Sky_WarmMist", "天", background, new Vector3(0f, 7.1f, 13.8f), 0.47f, -120, new Color(0.78f, 0.70f, 0.58f, 1f), transparentCutout);
            AddSprite("DistantMountains_Soft", "远山", background, new Vector3(0.4f, 4.45f, 11.2f), 0.34f, -105, new Color(0.72f, 0.68f, 0.58f, 0.82f), transparentCutout);
            AddSprite("PaintedScene_Backdrop", "di", background, new Vector3(0.05f, 2.15f, 8.2f), 0.78f, -98, new Color(0.70f, 0.64f, 0.54f, 0.86f), transparentCutout);

            AddSprite("ForegroundWetGround_Base", "di", ground, new Vector3(0f, -2.05f, -1.35f), 0.33f, 4, new Color(0.38f, 0.34f, 0.28f, 0.68f), transparentCutout);
            AddSprite("ForegroundWetGround_Highlight", "di上", ground, new Vector3(0.75f, -1.85f, -1.2f), 0.29f, 8, new Color(0.64f, 0.58f, 0.46f, 0.40f), transparentCutout);
            AddSprite("PathWater_Base", "di", ground, new Vector3(0f, 1.15f, 4.85f), 0.22f, -88, new Color(0.50f, 0.46f, 0.39f, 0.68f), transparentCutout);
            AddSprite("PathWater_Highlight", "di上", ground, new Vector3(0.65f, 1.02f, 4.65f), 0.20f, -80, new Color(0.72f, 0.68f, 0.58f, 0.52f), transparentCutout);
            AddSprite("PathWater_Reflection_Back", "di上", ground, new Vector3(2.9f, 1.28f, 5.7f), 0.12f, -82, new Color(0.76f, 0.72f, 0.62f, 0.34f), transparentCutout, true);

            AddSprite("DistantHouse_Left", "房子1", midground, new Vector3(-2.9f, 2.05f, 5.45f), 0.18f, -45, new Color(0.76f, 0.72f, 0.62f, 0.78f), transparentCutout);
            AddSprite("DistantHouse_Right", "房子2", midground, new Vector3(4.85f, 2.02f, 4.35f), 0.22f, -30, new Color(0.66f, 0.60f, 0.52f, 0.88f), transparentCutout);
            AddSprite("DistantHouse_Ghost", "房子1", midground, new Vector3(-7.5f, 2.35f, 6.8f), 0.14f, -55, new Color(0.77f, 0.72f, 0.62f, 0.46f), transparentCutout);

            CreateGroundPlane(ground, "ForestGround_DarkWetBase", new Vector3(0f, -0.055f, 1.4f), new Vector3(34f, 1f, 24f), new Color(0.16f, 0.145f, 0.122f, 0.28f), -110);
            CreateGroundPlane(ground, "ForestGround_PathWash", new Vector3(0.45f, -0.050f, 2.45f), new Vector3(9.5f, 1f, 11f), new Color(0.45f, 0.39f, 0.29f, 0.12f), -109);
            CreateTexturedGroundQuad(ground, "PaintedPath_Highlight_Mid", "di上", new Vector3(0.55f, 0.010f, 2.15f), 0.52f, new Color(0.90f, 0.84f, 0.72f, 0.48f), -108);
            CreateTexturedGroundQuad(ground, "PaintedPath_Highlight_Far", "di上", new Vector3(-0.15f, 0.012f, 5.2f), 0.36f, new Color(0.88f, 0.82f, 0.70f, 0.32f), -107);

            AddSprite("LeftTallTree_Frame", "树", foreground, new Vector3(-6.35f, 2.15f, -0.95f), 0.26f, 46, new Color(0.47f, 0.45f, 0.35f, 0.95f), transparentCutout);
            AddSprite("RightTallTree_Haze", "树", midground, new Vector3(7.75f, 2.35f, 2.1f), 0.30f, 22, new Color(0.46f, 0.40f, 0.32f, 0.72f), transparentCutout, true);
            AddSprite("BackLeftTree_Silhouette", "树", midground, new Vector3(-8.6f, 2.4f, 3.4f), 0.21f, -12, new Color(0.38f, 0.39f, 0.31f, 0.66f), transparentCutout);

            AddSprite("LeftRock_ForegroundMass", "大石头", foreground, new Vector3(-4.85f, 0.62f, -1.65f), 0.145f, 58, new Color(0.46f, 0.43f, 0.35f, 0.94f), transparentCutout);
            AddSprite("RightRock_ForegroundShelf", "中石头", foreground, new Vector3(4.95f, 0.58f, -1.25f), 0.18f, 62, new Color(0.54f, 0.50f, 0.42f, 0.94f), transparentCutout, true);
            AddSprite("FrontLeftFlatStone", "矮石头", foreground, new Vector3(-3.85f, 0.12f, -3.2f), 0.20f, 78, new Color(0.44f, 0.41f, 0.35f, 0.90f), transparentCutout);
            AddSprite("FrontRightFlatStone", "矮石头", foreground, new Vector3(3.2f, 0.10f, -3.55f), 0.17f, 74, new Color(0.45f, 0.42f, 0.36f, 0.78f), transparentCutout, true);
            AddSprite("MidLeftRock", "中石头", props, new Vector3(-4.95f, 0.62f, 1.55f), 0.12f, 20, new Color(0.39f, 0.38f, 0.32f, 0.90f), transparentCutout);
            AddSprite("MidRightRock", "大石头", props, new Vector3(5.95f, 0.78f, 1.65f), 0.13f, 24, new Color(0.45f, 0.43f, 0.37f, 0.84f), transparentCutout, true);

            AddSprite("LeftBush_Lush", "灌木-中", foreground, new Vector3(-5.05f, 0.65f, -1.95f), 0.17f, 84, new Color(0.70f, 0.66f, 0.35f, 1f), transparentCutout);
            AddSprite("LeftBush_DarkBase", "灌木低-暗", foreground, new Vector3(-4.0f, 0.46f, -2.55f), 0.12f, 86, new Color(0.50f, 0.49f, 0.29f, 0.94f), transparentCutout);
            AddSprite("RightBush_RockTop", "灌木-中", foreground, new Vector3(5.55f, 0.8f, -1.65f), 0.14f, 82, new Color(0.68f, 0.64f, 0.36f, 0.96f), transparentCutout, true);
            AddSprite("RightBush_Thin", "灌木低", foreground, new Vector3(7.05f, 0.47f, -2.95f), 0.18f, 88, new Color(0.63f, 0.59f, 0.32f, 0.90f), transparentCutout, true);
            AddSprite("CenterSmallPlant", "灌木低-暗", props, new Vector3(1.55f, 0.38f, -1.95f), 0.075f, 67, new Color(0.36f, 0.38f, 0.25f, 0.72f), transparentCutout);
            AddSprite("CenterGreenSprout", "灌木低", props, new Vector3(2.45f, 0.30f, -2.05f), 0.08f, 68, new Color(0.64f, 0.65f, 0.32f, 0.82f), transparentCutout);
            AddSprite("MidLeftSapling", "灌木低", props, new Vector3(-2.9f, 0.45f, 1.1f), 0.11f, 26, new Color(0.68f, 0.63f, 0.34f, 0.88f), transparentCutout);
            AddSprite("MidRightSapling", "灌木低-暗", props, new Vector3(3.7f, 0.43f, 1.18f), 0.11f, 28, new Color(0.44f, 0.45f, 0.28f, 0.78f), transparentCutout, true);
        }

        private static SpriteRenderer AddSprite(string objectName, string assetName, Transform parent, Vector3 position, float uniformScale, int sortingOrder, Color color, Material material, bool flipX = false)
        {
            if (ShouldSkipCompositeGroundSprite(objectName))
            {
                return null;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{assetName}.png");
            if (sprite == null)
            {
                throw new InvalidOperationException($"Missing sprite asset: {ArtDir}/{assetName}.png");
            }

            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * uniformScale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.sortingOrder = sortingOrder;
            sr.color = color;
            sr.flipX = flipX;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return sr;
        }

        private static bool ShouldSkipCompositeGroundSprite(string objectName)
        {
            switch (objectName)
            {
                case "ForegroundWetGround_Base":
                case "ForegroundWetGround_Highlight":
                case "PathWater_Base":
                case "PathWater_Highlight":
                case "PathWater_Reflection_Back":
                    return true;
                default:
                    return false;
            }
        }

        private static MeshRenderer CreateGroundPlane(Transform parent, string objectName, Vector3 position, Vector3 scale, Color color, int sortingOrder)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = objectName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateOrUpdateGroundMaterial(objectName, color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static MeshRenderer CreateTexturedGroundQuad(Transform parent, string objectName, string assetName, Vector3 position, float uniformScale, Color color, int sortingOrder)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/{assetName}.png");
            if (texture == null)
            {
                throw new InvalidOperationException($"Missing texture asset: {ArtDir}/{assetName}.png");
            }

            float width = texture.width / 100f;
            float depth = texture.height / 100f;
            float halfWidth = width * 0.5f;
            float halfDepth = depth * 0.5f;

            Mesh mesh = new Mesh { name = $"{objectName}_Mesh" };
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, 0f, -halfDepth),
                new Vector3(halfWidth, 0f, -halfDepth),
                new Vector3(-halfWidth, 0f, halfDepth),
                new Vector3(halfWidth, 0f, halfDepth)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * uniformScale;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateOrUpdateGroundTextureMaterial(objectName, texture, color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static void BuildAtmosphere()
        {
            GameObject root = EnsureRoot(AtmosphereRootName);

            VolumetricFogProfile fogProfile = CreateOrUpdateFogProfile();
            VolumetricLightProfile sunBeamProfile = CreateOrUpdateVolumetricLightProfile("ForestScene_SunBeam_VLProfile", 0.055f, 1.35f, 0.48f, 0.62f, new Color(1.00f, 0.78f, 0.50f, 1f));
            VolumetricLightProfile lowMistProfile = CreateOrUpdateVolumetricLightProfile("ForestScene_LowMist_VLProfile", 0.032f, 0.85f, 0.62f, 0.35f, new Color(0.88f, 0.76f, 0.58f, 1f));
            VolumetricLightProfile lanternProfile = CreateOrUpdateVolumetricLightProfile("ForestScene_LanternGlow_VLProfile", 0.065f, 1.1f, 0.38f, 0.45f, new Color(1f, 0.68f, 0.34f, 1f));

            Light sun = FindOrCreateDirectionalLight(root.transform);

            GameObject managerGo = new GameObject("VolumetricFog2_Manager");
            managerGo.transform.SetParent(root.transform, false);
            VolumetricFogManager manager = managerGo.AddComponent<VolumetricFogManager>();
            manager.mainManager = true;
            manager.sun = sun;
            manager.scattering = 0.12f;
            manager.scatteringThreshold = 0.50f;
            manager.scatteringIntensity = 0.16f;
            manager.scatteringAbsorption = 0.68f;
            manager.scatteringTint = new Color(1.0f, 0.78f, 0.55f, 1f);
            manager.downscaling = 1.5f;
            manager.blurPasses = 2;
            manager.blurDownscaling = 1.25f;
            manager.blurSpread = 1.15f;
            manager.ditherStrength = 0.035f;
            manager.includeTransparent = 0;
            manager.includeSemiTransparent = 0;

            GameObject fogGo = VolumetricFogManager.CreateFogVolume("VolumetricFog2_ForestLowFog");
            fogGo.transform.SetParent(root.transform, false);
            fogGo.transform.localPosition = new Vector3(0f, 2.65f, 7.25f);
            fogGo.transform.localScale = new Vector3(52f, 6.5f, 12.5f);
            VolumetricFog fog = fogGo.GetComponent<VolumetricFog>();
            fog.profile = fogProfile;
            fog.enableNativeLights = true;
            fog.nativeLightsMultiplier = 0.18f;
            fog.nativeLightFallOff = 0.75f;
            fog.enablePointLights = true;
            fog.enableUpdateModeOptions = false;

            CreateVolumetricSpot("VL_SunShaft_LeftToPath", root.transform, sunBeamProfile, new Vector3(-6.8f, 5.1f, 1.1f), new Vector3(58f, 25f, -18f), 13f, 42f, 2.25f, new Color(1f, 0.78f, 0.48f, 1f));
            CreateVolumetricSpot("VL_PathHaze_LowSkim", root.transform, lowMistProfile, new Vector3(-4.7f, 1.35f, -2.2f), new Vector3(75f, 72f, 6f), 9.5f, 60f, 0.95f, new Color(0.95f, 0.78f, 0.52f, 1f));
            CreateVolumetricPoint("VL_DistantHouse_WarmGlow", root.transform, lanternProfile, new Vector3(-1.85f, 1.45f, 4.05f), 4.2f, 1.25f, new Color(1f, 0.58f, 0.28f, 1f));
            CreateVolumetricPoint("VL_RightBuilding_SoftBounce", root.transform, lowMistProfile, new Vector3(4.85f, 1.1f, 2.4f), 5.4f, 0.58f, new Color(0.85f, 0.62f, 0.42f, 1f));
        }

        private static VolumetricFogProfile CreateOrUpdateFogProfile()
        {
            string path = $"{ArtDir}/ForestScene_VolumetricFog2Profile.asset";
            VolumetricFogProfile profile = AssetDatabase.LoadAssetAtPath<VolumetricFogProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumetricFogProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.raymarchQuality = 7;
            profile.raymarchNearStepping = 10f;
            profile.raymarchMinStep = 0.18f;
            profile.jittering = 0.72f;
            profile.dithering = 1f;
            profile.renderQueue = 3100;
            profile.noiseStrength = 0.34f;
            profile.noiseScale = 34f;
            profile.noiseFinalMultiplier = 0.44f;
            profile.density = 0f;
            profile.shape = VolumetricFogShape.Box;
            profile.border = 0.22f;
            profile.customHeight = true;
            profile.height = 24f;
            profile.verticalOffset = -2.5f;
            profile.distance = 10.5f;
            profile.distanceFallOff = 0.38f;
            profile.maxDistance = 45f;
            profile.maxDistanceFallOff = 0.28f;
            profile.albedo = new Color(0.66f, 0.60f, 0.51f, 1f);
            profile.brightness = 0.07f;
            profile.deepObscurance = 0.04f;
            profile.specularColor = new Color(1f, 0.72f, 0.42f, 1f);
            profile.specularThreshold = 0.72f;
            profile.specularIntensity = 0.18f;
            profile.turbulence = 0.35f;
            profile.windDirection = new Vector3(0.012f, 0.002f, 0.004f);
            profile.dayNightCycle = true;
            profile.ambientLightMultiplier = 0.18f;
            profile.lightDiffusionModel = DiffusionModel.Smooth;
            profile.lightDiffusionPower = 48f;
            profile.lightDiffusionIntensity = 0.36f;
            profile.receiveShadows = false;
            profile.distantFog = true;
            profile.distantFogShowInEditMode = true;
            profile.distantFogStartDistance = 8.5f;
            profile.distantFogDistanceDensity = 0.0052f;
            profile.distantFogMaxHeight = 16f;
            profile.distantFogHeightDensity = 0.018f;
            profile.distantFogColor = new Color(0.62f, 0.56f, 0.48f, 1f);
            profile.distantFogDiffusionIntensity = 0.18f;
            profile.distantFogNoise = true;
            profile.distantFogDistanceNoiseScale = 0.18f;
            profile.distantFogDistanceNoiseStrength = 0.33f;
            profile.ValidateSettings();

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static VolumetricLightProfile CreateOrUpdateVolumetricLightProfile(string name, float density, float brightness, float noiseStrength, float diffusionIntensity, Color mediumAlbedo)
        {
            string path = $"{ArtDir}/{name}.asset";
            VolumetricLightProfile profile = AssetDatabase.LoadAssetAtPath<VolumetricLightProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumetricLightProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.raymarchPreset = RaymarchPresets.UserDefined;
            profile.raymarchQuality = 8;
            profile.raymarchMinStep = 0.16f;
            profile.raymarchMaxSteps = 180;
            profile.jittering = 0.8f;
            profile.dithering = 1f;
            profile.alwaysOn = true;
            profile.useNoise = true;
            profile.noiseStrength = noiseStrength;
            profile.noiseScale = 7.5f;
            profile.noiseFinalMultiplier = 1f;
            profile.density = density;
            profile.brightness = brightness;
            profile.mediumAlbedo = mediumAlbedo;
            profile.diffusionIntensity = diffusionIntensity;
            profile.penumbra = 0.75f;
            profile.windDirection = new Vector3(0.018f, 0.006f, 0.002f);
            profile.enableDustParticles = true;
            profile.dustBrightness = 0.32f;
            profile.dustMinSize = 0.014f;
            profile.dustMaxSize = 0.034f;
            profile.dustWindSpeed = 0.45f;
            profile.dustDistanceAttenuation = 18f;
            profile.enableShadows = false;

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void CreateVolumetricSpot(string name, Transform parent, VolumetricLightProfile profile, Vector3 position, Vector3 euler, float range, float spotAngle, float intensity, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = range;
            light.spotAngle = spotAngle;
            light.innerSpotAngle = spotAngle * 0.58f;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;

            VolumetricLight volumetric = go.AddComponent<VolumetricLight>();
            volumetric.profile = profile;
            volumetric.profileSync = true;
            volumetric.alwaysOn = true;
            volumetric.customRange = range;
            volumetric.targetCamera = Camera.main != null ? Camera.main.transform : null;
            volumetric.Init();
        }

        private static void CreateVolumetricPoint(string name, Transform parent, VolumetricLightProfile profile, Vector3 position, float range, float intensity, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;

            VolumetricLight volumetric = go.AddComponent<VolumetricLight>();
            volumetric.profile = profile;
            volumetric.profileSync = true;
            volumetric.alwaysOn = true;
            volumetric.customRange = range;
            volumetric.targetCamera = Camera.main != null ? Camera.main.transform : null;
            volumetric.Init();
        }

        private static Light FindOrCreateDirectionalLight(Transform parent)
        {
            Light sun = null;
            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }
            }

            if (sun == null)
            {
                GameObject go = new GameObject("Directional Light");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.gameObject.SetActive(true);
            sun.transform.SetParent(parent, true);
            sun.transform.position = new Vector3(-2f, 5f, -3f);
            sun.transform.rotation = Quaternion.Euler(46f, -31f, 9f);
            sun.color = new Color(1f, 0.78f, 0.52f, 1f);
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.42f;
            return sun;
        }

        private static void ConfigureCameraAndLighting()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.70f, 0.62f, 0.51f, 1f);
            camera.transform.position = new Vector3(0.08f, 2.55f, -6.75f);
            camera.transform.rotation = Quaternion.Euler(14.5f, 0f, 0f);
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 120f;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.43f, 0.39f, 0.33f, 1f);
            RenderSettings.fog = false;
        }

        private static void EnsureFogRenderFeatures()
        {
            EnsureRendererFeature<VolumetricFogRenderFeature>("Assets/Settings/PC_Renderer.asset", "Volumetric Fog 2", feature =>
            {
                feature.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                feature.renderPassEventOrder = (int)RenderPassEvent.BeforeRenderingTransparents;
                feature.fogLayerMask = -1;
                feature.cameraLayerMask = -1;
                feature.ignoreReflectionProbes = true;
            });

            EnsureRendererFeature<VolumetricFogRenderFeature>("Assets/Settings/Mobile_Renderer.asset", "Volumetric Fog 2", feature =>
            {
                feature.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                feature.renderPassEventOrder = (int)RenderPassEvent.BeforeRenderingTransparents;
                feature.fogLayerMask = -1;
                feature.cameraLayerMask = -1;
                feature.ignoreReflectionProbes = true;
            });

            EnsureRendererFeature<DepthRenderPrePassFeature>("Assets/Settings/PC_Renderer.asset", "Volumetric Fog 2 Depth PrePass", feature =>
            {
                feature.cameraLayerMask = -1;
                feature.ignoreReflectionProbes = true;
                feature.useOptimizedDepthOnlyShader = true;
            });

            EnsureRendererFeature<DepthRenderPrePassFeature>("Assets/Settings/Mobile_Renderer.asset", "Volumetric Fog 2 Depth PrePass", feature =>
            {
                feature.cameraLayerMask = -1;
                feature.ignoreReflectionProbes = true;
                feature.useOptimizedDepthOnlyShader = true;
            });
        }

        private static void EnsureRendererFeature<T>(string rendererPath, string displayName, Action<T> configure) where T : ScriptableRendererFeature
        {
            ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (rendererData == null)
            {
                Debug.LogWarning($"[ForestCutoutLevelSceneBuilder] Renderer data not found: {rendererPath}");
                return;
            }

            T feature = null;
            foreach (ScriptableRendererFeature existing in rendererData.rendererFeatures)
            {
                if (existing is T typed)
                {
                    feature = typed;
                    break;
                }
            }

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<T>();
                feature.name = displayName;
                AssetDatabase.AddObjectToAsset(feature, rendererData);

                SerializedObject featuresSo = new SerializedObject(rendererData);
                SerializedProperty rendererFeatures = featuresSo.FindProperty("m_RendererFeatures");
                rendererFeatures.arraySize++;
                rendererFeatures.GetArrayElementAtIndex(rendererFeatures.arraySize - 1).objectReferenceValue = feature;
                featuresSo.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject mapSo = new SerializedObject(rendererData);
                SerializedProperty featureMap = mapSo.FindProperty("m_RendererFeatureMap");
                if (featureMap != null)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                    featureMap.arraySize++;
                    featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
                    mapSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            feature.name = displayName;
            configure?.Invoke(feature);
            feature.SetActive(true);
            feature.Create();
            rendererData.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
        }

        private static void OrganizeLegacyVisuals(GameObject legacyRoot)
        {
            HashSet<GameObject> moved = new HashSet<GameObject>();
            foreach (SpriteRenderer sr in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (sr == null || sr.gameObject == null)
                {
                    continue;
                }

                GameObject go = sr.gameObject;
                if (IsGeneratedObject(go) || IsPartOfPlayer(go))
                {
                    continue;
                }

                string spritePath = sr.sprite != null ? AssetDatabase.GetAssetPath(sr.sprite) : string.Empty;
                if (moved.Add(go))
                {
                    go.transform.SetParent(legacyRoot.transform, true);
                    go.SetActive(false);
                }
            }
        }

        private static void HidePrototypeRenderers()
        {
            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer is SpriteRenderer || renderer == null || renderer.gameObject == null)
                {
                    continue;
                }

                if (IsGeneratedObject(renderer.gameObject) || IsPartOfPlayer(renderer.gameObject))
                {
                    continue;
                }

                renderer.enabled = false;
                EditorUtility.SetDirty(renderer);
            }

            string[] names =
            {
                "PrototypeGround_50x20",
                "LeftBoundary",
                "RightBoundary",
                "TopBoundary",
                "BottomBoundary",
                "BackBoundary",
                "FrontBoundary",
                "PrototypeWall",
                "CaveBlocker"
            };

            foreach (string name in names)
            {
                GameObject go = GameObject.Find(name);
                if (go == null)
                {
                    continue;
                }

                foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }

        private static void EnsurePlayerVisual()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                return;
            }

            Transform visual = player.transform.Find("Visual");
            if (visual != null)
            {
                visual.gameObject.SetActive(true);
                visual.localScale = Vector3.one * 1.8f;
                foreach (SpriteRenderer sr in visual.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.sortingOrder = 72;
                    sr.color = Color.white;
                    EditorUtility.SetDirty(sr);
                }
            }
        }

        private static bool IsLegacyDecorName(string name)
        {
            switch (name)
            {
                case "房子1":
                case "房子2":
                case "大石头":
                case "中石头":
                case "矮石头":
                case "树":
                case "灌木-中":
                case "灌木低":
                case "灌木低-暗":
                case "di":
                case "di上":
                case "天":
                case "远山":
                case "雾":
                case "right":
                case "IDLE":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsPartOfPlayer(GameObject go)
        {
            GameObject player = GameObject.Find("Player");
            return player != null && go.transform.IsChildOf(player.transform);
        }

        private static bool IsGeneratedObject(GameObject go)
        {
            Transform t = go.transform;
            while (t != null)
            {
                if (t.name == RootName || t.name == AtmosphereRootName || t.name == LegacyRootName)
                {
                    return true;
                }

                t = t.parent;
            }

            return false;
        }

        private static Material LoadMaterial(string assetName)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{ArtDir}/Materials/{assetName}.mat");
        }

        private static Material CreateOrUpdateSpriteLitMaterial(string name)
        {
            string path = $"{ArtDir}/Materials/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_Cull"))
            {
                mat.SetFloat("_Cull", (float)CullMode.Off);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateGroundMaterial(string name, Color color)
        {
            string path = $"{ArtDir}/Materials/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }

            bool transparent = color.a < 0.99f;
            mat.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            mat.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", transparent ? 1f : 0f);
            }

            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 0f);
            }

            if (mat.HasProperty("_SrcBlend"))
            {
                mat.SetFloat("_SrcBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha : (float)UnityEngine.Rendering.BlendMode.One);
            }

            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetFloat("_DstBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (float)UnityEngine.Rendering.BlendMode.Zero);
            }

            if (mat.HasProperty("_SrcBlendAlpha"))
            {
                mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            }

            if (mat.HasProperty("_DstBlendAlpha"))
            {
                mat.SetFloat("_DstBlendAlpha", transparent ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (float)UnityEngine.Rendering.BlendMode.Zero);
            }

            if (mat.HasProperty("_ZWrite"))
            {
                mat.SetFloat("_ZWrite", transparent ? 0f : 1f);
            }

            if (mat.HasProperty("_Cull"))
            {
                mat.SetFloat("_Cull", (float)CullMode.Off);
            }

            if (transparent)
            {
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateGroundTextureMaterial(string name, Texture2D texture, Color color)
        {
            string path = $"{ArtDir}/Materials/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", texture);
            }

            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", texture);
            }

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }

            ConfigureSurfaceBlend(mat, color.a < 0.99f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureSurfaceBlend(Material mat, bool transparent)
        {
            mat.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            mat.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", transparent ? 1f : 0f);
            }

            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 0f);
            }

            if (mat.HasProperty("_SrcBlend"))
            {
                mat.SetFloat("_SrcBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha : (float)UnityEngine.Rendering.BlendMode.One);
            }

            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetFloat("_DstBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (float)UnityEngine.Rendering.BlendMode.Zero);
            }

            if (mat.HasProperty("_SrcBlendAlpha"))
            {
                mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            }

            if (mat.HasProperty("_DstBlendAlpha"))
            {
                mat.SetFloat("_DstBlendAlpha", transparent ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (float)UnityEngine.Rendering.BlendMode.Zero);
            }

            if (mat.HasProperty("_ZWrite"))
            {
                mat.SetFloat("_ZWrite", transparent ? 0f : 1f);
            }

            if (mat.HasProperty("_Cull"))
            {
                mat.SetFloat("_Cull", (float)CullMode.Off);
            }

            if (transparent)
            {
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
        }

        private static GameObject EnsureRoot(string name)
        {
            GameObject root = FindSceneRoot(name);
            if (root == null)
            {
                root = new GameObject(name);
            }

            root.transform.SetParent(null, false);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.SetActive(true);
            return root;
        }

        private static GameObject CreateGroup(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;
            group.transform.localScale = Vector3.one;
            return group;
        }

        private static void DestroyRoot(string name)
        {
            GameObject root = FindSceneRoot(name);
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject FindSceneRoot(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }
    }
}
