using System;
using System.Collections.Generic;
using System.IO;
using NewFPG.Combat.SkillIndicators;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NewFPG.EditorTools
{
    public static class SkillIndicatorTemporaryArtGenerator
    {
        private const string Root = "Assets/Art/SkillIndicators/Temporary";
        private const string TextureDir = Root + "/Textures";
        private const string MaterialDir = Root + "/Materials";
        private const string MeshDir = Root + "/Meshes";
        private const string PrefabDir = Root + "/Prefabs";
        private const string AudioDir = Root + "/Audio";
        private const string IndexPath = Root + "/SO_IND_TemporaryArtIndex.asset";

        private static readonly Color OwnerValid = new Color(0.10f, 0.72f, 1.00f, 0.82f);
        private static readonly Color Invalid = new Color(1.00f, 0.12f, 0.08f, 0.84f);
        private static readonly Color EnemyDanger = new Color(1.00f, 0.32f, 0.08f, 0.86f);
        private static readonly Color AllyBuff = new Color(0.18f, 1.00f, 0.48f, 0.80f);
        private static readonly Color Persistent = new Color(0.52f, 0.35f, 1.00f, 0.76f);
        private static readonly Color Tether = new Color(0.72f, 0.92f, 1.00f, 0.96f);
        private static readonly Color Ghost = new Color(0.45f, 0.80f, 1.00f, 0.55f);
        private static readonly Color White = new Color(1f, 1f, 1f, 1f);
        private static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);

        [MenuItem("NewFPG/技能指示器/生成临时美术包")]
        public static void Generate()
        {
            EnsureFolders();

            Dictionary<string, Texture2D> textures = CreateTextures();
            Dictionary<string, Material> materials = CreateMaterials(textures);
            Dictionary<string, Mesh> meshes = CreateMeshes();
            Dictionary<string, AudioClip> audioClips = CreateAudioClips();
            Dictionary<string, GameObject> prefabs = CreatePrefabs(textures, materials, meshes);
            CreateIndex(textures, materials, meshes, prefabs, audioClips);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("已生成技能指示器临时美术包：" + Root);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "SkillIndicators");
            EnsureFolder("Assets/Art/SkillIndicators", "Temporary");
            EnsureFolder(Root, "Textures");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Meshes");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(Root, "Audio");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Dictionary<string, Texture2D> CreateTextures()
        {
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
            AddTexture(textures, "T_IND_RingCircle", DrawRing(256, OwnerValid, 0.36f, 0.46f));
            AddTexture(textures, "T_IND_SoftDisc", DrawRadialDisc(256, OwnerValid));
            AddTexture(textures, "T_IND_DashedLine", DrawDashedLine(256, 64, White));
            AddTexture(textures, "T_IND_Arrow", DrawArrow(256, White));
            AddTexture(textures, "T_IND_Cross", DrawCross(256, Invalid));
            AddTexture(textures, "T_IND_Check", DrawCheck(256, AllyBuff));
            AddTexture(textures, "T_IND_ConeMask", DrawCone(256, OwnerValid, 90f));
            AddTexture(textures, "T_IND_Noise", DrawNoise(256, Persistent));
            AddTexture(textures, "T_IND_FlowStripe", DrawFlowStripe(256, Tether));
            AddTexture(textures, "T_IND_TargetLock", DrawTargetLock(256, Tether));
            AddTexture(textures, "T_IND_HUDHoldProgress", DrawProgressRing(256, OwnerValid, 0.72f));
            return textures;
        }

        private static void AddTexture(Dictionary<string, Texture2D> textures, string name, Texture2D texture)
        {
            string path = TextureDir + "/" + name + ".png";
            File.WriteAllBytes(ProjectPath(path), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spritePixelsPerUnit = 128f;
                importer.SaveAndReimport();
            }

            Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            textures[name] = imported;
        }

        private static Dictionary<string, Material> CreateMaterials(Dictionary<string, Texture2D> textures)
        {
            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            AddMaterial(materials, "M_IND_OwnerValid", OwnerValid, textures["T_IND_SoftDisc"]);
            AddMaterial(materials, "M_IND_Invalid", Invalid, textures["T_IND_Cross"]);
            AddMaterial(materials, "M_IND_EnemyDanger", EnemyDanger, textures["T_IND_RingCircle"]);
            AddMaterial(materials, "M_IND_AllyBuff", AllyBuff, textures["T_IND_Check"]);
            AddMaterial(materials, "M_IND_PersistentZone", Persistent, textures["T_IND_Noise"]);
            AddMaterial(materials, "M_IND_TetherLine", Tether, textures["T_IND_FlowStripe"]);
            AddMaterial(materials, "M_IND_PlacementGhost", Ghost, textures["T_IND_SoftDisc"]);
            return materials;
        }

        private static void AddMaterial(Dictionary<string, Material> materials, string name, Color color, Texture2D texture)
        {
            string path = MaterialDir + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            ConfigureTransparentMaterial(material, color, texture);
            EditorUtility.SetDirty(material);
            materials[name] = material;
        }

        private static void ConfigureTransparentMaterial(Material material, Color color, Texture2D texture)
        {
            material.color = color;
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
        }

        private static Dictionary<string, Mesh> CreateMeshes()
        {
            Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
            AddMesh(meshes, "MS_IND_UnitCircle", CreateCircleMesh("MS_IND_UnitCircle", 96));
            AddMesh(meshes, "MS_IND_UnitRectangle", CreateRectangleMesh("MS_IND_UnitRectangle"));
            AddMesh(meshes, "MS_IND_Cone90", CreateConeMesh("MS_IND_Cone90", 90f, 64));
            AddMesh(meshes, "MS_IND_FootprintBox", CreateRectangleMesh("MS_IND_FootprintBox"));
            return meshes;
        }

        private static void AddMesh(Dictionary<string, Mesh> meshes, string name, Mesh mesh)
        {
            string path = MeshDir + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                meshes[name] = existing;
                return;
            }

            AssetDatabase.CreateAsset(mesh, path);
            meshes[name] = mesh;
        }

        private static Dictionary<string, GameObject> CreatePrefabs(Dictionary<string, Texture2D> textures, Dictionary<string, Material> materials, Dictionary<string, Mesh> meshes)
        {
            Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
            AddPrefab(prefabs, "PF_IND_TargetReticle", BuildSpritePrefab("PF_IND_TargetReticle", textures["T_IND_TargetLock"], materials["M_IND_TetherLine"], "屏幕或世界空间的目标锁定标记。"));
            AddPrefab(prefabs, "PF_IND_GroundCircle", BuildMeshIndicator("PF_IND_GroundCircle", meshes["MS_IND_UnitCircle"], materials["M_IND_OwnerValid"], true, "地面圆形范围预览。"));
            AddPrefab(prefabs, "PF_IND_LineRect", BuildLineRectPrefab(meshes, materials));
            AddPrefab(prefabs, "PF_IND_Cone", BuildMeshIndicator("PF_IND_Cone", meshes["MS_IND_Cone90"], materials["M_IND_OwnerValid"], true, "扇形预览；缩放控制半径，旋转控制朝向。"));
            AddPrefab(prefabs, "PF_IND_ArcTrajectory", BuildArcTrajectoryPrefab(materials));
            AddPrefab(prefabs, "PF_IND_Footprint", BuildFootprintPrefab(meshes, materials));
            AddPrefab(prefabs, "PF_IND_TetherLine", BuildTetherPrefab(materials));
            AddPrefab(prefabs, "PF_IND_CountdownDanger", BuildCountdownPrefab(meshes, materials));
            AddPrefab(prefabs, "PF_IND_PersistentZone", BuildMeshIndicator("PF_IND_PersistentZone", meshes["MS_IND_UnitCircle"], materials["M_IND_PersistentZone"], true, "持续区域占位预览。"));
            AddPrefab(prefabs, "PF_IND_PlacementGhost", BuildPlacementGhostPrefab(materials));
            AddPrefab(prefabs, "PF_IND_MonsterWarningCircle", BuildMeshIndicator("PF_IND_MonsterWarningCircle", meshes["MS_IND_UnitCircle"], materials["M_IND_EnemyDanger"], true, "怪物攻击预警圆形范围。"));
            AddPrefab(prefabs, "PF_IND_HUDHoldProgress", BuildHudPrefab(textures));
            return prefabs;
        }

        private static Dictionary<string, AudioClip> CreateAudioClips()
        {
            Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>();
            AddAudioClip(audioClips, "S_IND_PreviewEnter", 660f, 0.10f, 0.18f);
            AddAudioClip(audioClips, "S_IND_TargetLock", 880f, 0.09f, 0.16f);
            AddAudioClip(audioClips, "S_IND_Invalid", 180f, 0.12f, 0.22f);
            AddAudioClip(audioClips, "S_IND_ConfirmRelease", 1040f, 0.11f, 0.20f);
            AddAudioClip(audioClips, "S_IND_DangerTick", 520f, 0.08f, 0.22f);
            AddAudioClip(audioClips, "S_IND_ZoneExpire", 300f, 0.16f, 0.18f);
            return audioClips;
        }

        private static void AddAudioClip(Dictionary<string, AudioClip> audioClips, string name, float frequency, float duration, float volume)
        {
            string path = AudioDir + "/" + name + ".wav";
            WriteSineWave(ProjectPath(path), frequency, duration, volume);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            audioClips[name] = clip;
        }

        private static void AddPrefab(Dictionary<string, GameObject> prefabs, string name, GameObject instance)
        {
            string path = PrefabDir + "/" + name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            UnityEngine.Object.DestroyImmediate(instance);
            prefabs[name] = prefab;
        }

        private static GameObject BuildMeshIndicator(string name, Mesh mesh, Material material, bool addRing, string note)
        {
            GameObject root = new GameObject(name);
            root.AddComponent<SkillIndicatorTemporaryArtNote>().note = note;
            MeshFilter filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (addRing)
            {
                AddCircleLine(root.transform, "BoundaryRing", material, 1f, 96, 0.035f);
            }

            return root;
        }

        private static GameObject BuildSpritePrefab(string name, Texture2D texture, Material material, string note)
        {
            GameObject root = new GameObject(name);
            root.AddComponent<SkillIndicatorTemporaryArtNote>().note = note;
            SpriteRenderer spriteRenderer = root.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = SpriteFromTexture(texture);
            spriteRenderer.sharedMaterial = material;
            spriteRenderer.color = White;
            spriteRenderer.sortingOrder = 220;
            return root;
        }

        private static GameObject BuildLineRectPrefab(Dictionary<string, Mesh> meshes, Dictionary<string, Material> materials)
        {
            GameObject root = BuildMeshIndicator("PF_IND_LineRect", meshes["MS_IND_UnitRectangle"], materials["M_IND_OwnerValid"], false, "直线或矩形预览；缩放 Z 控制长度，缩放 X 控制宽度。");
            GameObject arrow = new GameObject("DirectionArrow");
            arrow.transform.SetParent(root.transform, false);
            arrow.transform.localPosition = new Vector3(0f, 0.01f, 0.55f);
            MeshFilter filter = arrow.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateRuntimeArrowMesh();
            MeshRenderer renderer = arrow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials["M_IND_TetherLine"];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return root;
        }

        private static GameObject BuildArcTrajectoryPrefab(Dictionary<string, Material> materials)
        {
            GameObject root = new GameObject("PF_IND_ArcTrajectory");
            root.AddComponent<SkillIndicatorTemporaryArtNote>().note = "抛物线轨迹占位；运行时可用采样后的投射物路径替换 LineRenderer 点位。";
            LineRenderer line = root.AddComponent<LineRenderer>();
            ConfigureLine(line, materials["M_IND_TetherLine"], 0.045f, false);
            line.positionCount = 16;
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (line.positionCount - 1f);
                line.SetPosition(i, new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 1.2f, t * 3f));
            }

            GameObject landing = new GameObject("LandingCircle");
            landing.transform.SetParent(root.transform, false);
            landing.transform.localPosition = new Vector3(0f, 0f, 3f);
            AddCircleLine(landing.transform, "LandingBoundary", materials["M_IND_EnemyDanger"], 0.55f, 64, 0.035f);
            return root;
        }

        private static GameObject BuildFootprintPrefab(Dictionary<string, Mesh> meshes, Dictionary<string, Material> materials)
        {
            GameObject root = BuildMeshIndicator("PF_IND_Footprint", meshes["MS_IND_FootprintBox"], materials["M_IND_PlacementGhost"], false, "放置占位预览，带前向箭头和可替换的有效性颜色材质槽。");
            root.transform.localScale = new Vector3(1.5f, 1f, 1f);
            GameObject arrow = new GameObject("ForwardArrow");
            arrow.transform.SetParent(root.transform, false);
            arrow.transform.localPosition = new Vector3(0f, 0.02f, 0.55f);
            MeshFilter filter = arrow.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateRuntimeArrowMesh();
            MeshRenderer renderer = arrow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials["M_IND_OwnerValid"];
            return root;
        }

        private static GameObject BuildTetherPrefab(Dictionary<string, Material> materials)
        {
            GameObject root = new GameObject("PF_IND_TetherLine");
            root.AddComponent<SkillIndicatorTemporaryArtNote>().note = "连线占位；运行时应根据施法者和目标挂点设置两端位置。";
            LineRenderer line = root.AddComponent<LineRenderer>();
            ConfigureLine(line, materials["M_IND_TetherLine"], 0.06f, false);
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(-0.5f, 0f, 0f));
            line.SetPosition(1, new Vector3(0.5f, 0f, 0f));
            return root;
        }

        private static GameObject BuildCountdownPrefab(Dictionary<string, Mesh> meshes, Dictionary<string, Material> materials)
        {
            GameObject root = BuildMeshIndicator("PF_IND_CountdownDanger", meshes["MS_IND_UnitCircle"], materials["M_IND_EnemyDanger"], true, "倒计时危险区占位；运行时可动画材质透明度或旋转扫掠子物体。");
            GameObject sweep = new GameObject("CountdownSweep");
            sweep.transform.SetParent(root.transform, false);
            sweep.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            MeshFilter filter = sweep.AddComponent<MeshFilter>();
            filter.sharedMesh = meshes["MS_IND_Cone90"];
            MeshRenderer renderer = sweep.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials["M_IND_Invalid"];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return root;
        }

        private static GameObject BuildPlacementGhostPrefab(Dictionary<string, Material> materials)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "PF_IND_PlacementGhost";
            UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());
            root.AddComponent<SkillIndicatorTemporaryArtNote>().note = "透明放置虚影，可用于炮塔、陷阱、墙体、召唤物或交互物占位。";
            root.transform.localScale = new Vector3(1f, 0.35f, 1f);
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = materials["M_IND_PlacementGhost"];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return root;
        }

        private static GameObject BuildHudPrefab(Dictionary<string, Texture2D> textures)
        {
            GameObject root = new GameObject("PF_IND_HUDHoldProgress", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.AddComponent<SkillIndicatorTemporaryArtNote>().note = "屏幕空间 HUD 长按进度占位；后续可替换为正式 HUD 美术。";
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject imageObject = new GameObject("HoldProgressRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(root.transform, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(96f, 96f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            Image image = imageObject.GetComponent<Image>();
            image.sprite = SpriteFromTexture(textures["T_IND_HUDHoldProgress"]);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillAmount = 0.72f;
            image.color = OwnerValid;
            return root;
        }

        private static void AddCircleLine(Transform parent, string name, Material material, float radius, int segments, float width)
        {
            GameObject ring = new GameObject(name);
            ring.transform.SetParent(parent, false);
            LineRenderer line = ring.AddComponent<LineRenderer>();
            ConfigureLine(line, material, width, true);
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.018f, Mathf.Sin(angle) * radius));
            }
        }

        private static void ConfigureLine(LineRenderer line, Material material, float width, bool loop)
        {
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = loop;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
        }

        private static void CreateIndex(Dictionary<string, Texture2D> textures, Dictionary<string, Material> materials, Dictionary<string, Mesh> meshes, Dictionary<string, GameObject> prefabs, Dictionary<string, AudioClip> audioClips)
        {
            SkillIndicatorTemporaryArtIndex index = AssetDatabase.LoadAssetAtPath<SkillIndicatorTemporaryArtIndex>(IndexPath);
            if (index == null)
            {
                index = ScriptableObject.CreateInstance<SkillIndicatorTemporaryArtIndex>();
                AssetDatabase.CreateAsset(index, IndexPath);
            }

            List<SkillIndicatorTemporaryArtEntry> entries = new List<SkillIndicatorTemporaryArtEntry>();
            entries.Add(PrefabEntry("PF_IND_TargetReticle", "预制体", "目标锁定、准星、弱点提示和辅助瞄准占位。", prefabs));
            entries.Add(PrefabEntry("PF_IND_GroundCircle", "预制体", "地面圆形范围、爆炸、治疗、光环和区域预览。", prefabs));
            entries.Add(PrefabEntry("PF_IND_LineRect", "预制体", "直线、矩形、冲刺路径、光束和墙体方向占位。", prefabs));
            entries.Add(PrefabEntry("PF_IND_Cone", "预制体", "扇形、横扫、吐息和怪物正面攻击占位。", prefabs));
            entries.Add(PrefabEntry("PF_IND_ArcTrajectory", "预制体", "投射物抛物线路径和预计落点占位。", prefabs));
            entries.Add(PrefabEntry("PF_IND_Footprint", "预制体", "放置占位和前向方向提示占位。", prefabs));
            entries.Add(PrefabEntry("PF_IND_TetherLine", "预制体", "吸取、拉拽、锁链、连接和断链阈值占位。", prefabs));
            entries.Add(PrefabEntry("PF_IND_CountdownDanger", "预制体", "延迟爆发、怪物蓄力和倒计时预警占位。", prefabs));
            entries.Add(PrefabEntry("PF_IND_PersistentZone", "预制体", "持续区域、周期伤害区域和淡出占位。", prefabs));
            entries.Add(PrefabEntry("PF_IND_PlacementGhost", "预制体", "放置类技能的透明模型虚影。", prefabs));
            entries.Add(PrefabEntry("PF_IND_MonsterWarningCircle", "预制体", "普通、精英和 Boss 攻击的怪物预警圆。", prefabs));
            entries.Add(PrefabEntry("PF_IND_HUDHoldProgress", "预制体", "HUD 武器长按进度和取消反馈占位。", prefabs));

            foreach (KeyValuePair<string, Material> pair in materials)
            {
                entries.Add(new SkillIndicatorTemporaryArtEntry { resourceId = pair.Key, category = "材质", usage = "阶段和颜色反馈用的临时材质。", material = pair.Value });
            }

            foreach (KeyValuePair<string, Texture2D> pair in textures)
            {
                entries.Add(new SkillIndicatorTemporaryArtEntry { resourceId = pair.Key, category = "贴图", usage = "透明灰度或彩色精灵占位贴图。", texture = pair.Value });
            }

            foreach (KeyValuePair<string, Mesh> pair in meshes)
            {
                entries.Add(new SkillIndicatorTemporaryArtEntry { resourceId = pair.Key, category = "网格", usage = "单位尺寸网格占位，由运行时按指示器几何缩放。", mesh = pair.Value });
            }

            foreach (KeyValuePair<string, AudioClip> pair in audioClips)
            {
                entries.Add(new SkillIndicatorTemporaryArtEntry { resourceId = pair.Key, category = "音效", usage = "技能指示器反馈用的短音效占位。", audioClip = pair.Value });
            }

            index.SetEntries(entries);
            EditorUtility.SetDirty(index);
        }

        private static SkillIndicatorTemporaryArtEntry PrefabEntry(string id, string category, string usage, Dictionary<string, GameObject> prefabs)
        {
            return new SkillIndicatorTemporaryArtEntry { resourceId = id, category = category, usage = usage, prefab = prefabs[id] };
        }

        private static Sprite SpriteFromTexture(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static string ProjectPath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath).Replace('\\', '/');
        }

        private static Texture2D NewTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Transparent;
            }

            texture.SetPixels(pixels);
            return texture;
        }

        private static Texture2D DrawRing(int size, Color color, float inner, float outer)
        {
            Texture2D texture = NewTexture(size, size);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / half;
                    if (d >= inner && d <= outer)
                    {
                        texture.SetPixel(x, y, color);
                    }
                    else if (d < inner)
                    {
                        Color fill = color;
                        fill.a *= 0.18f * (1f - d / inner);
                        texture.SetPixel(x, y, fill);
                    }
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D DrawRadialDisc(int size, Color color)
        {
            Texture2D texture = NewTexture(size, size);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / half;
                    if (d <= 0.95f)
                    {
                        Color c = color;
                        float fill = d <= 0.72f
                            ? Mathf.Lerp(0.95f, 0.62f, d / 0.72f)
                            : Mathf.SmoothStep(0.62f, 0f, Mathf.InverseLerp(0.72f, 0.95f, d));
                        c.a *= fill;
                        texture.SetPixel(x, y, c);
                    }
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D DrawDashedLine(int width, int height, Color color)
        {
            Texture2D texture = NewTexture(width, height);
            int lineHeight = Mathf.Max(3, height / 5);
            int y0 = height / 2 - lineHeight / 2;
            int dash = width / 10;
            int gap = width / 18;
            for (int x = 0; x < width; x++)
            {
                bool on = x % (dash + gap) < dash;
                if (!on)
                {
                    continue;
                }

                for (int y = y0; y < y0 + lineHeight; y++)
                {
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D DrawArrow(int size, Color color)
        {
            Texture2D texture = NewTexture(size, size);
            Rect stem = new Rect(size * 0.43f, size * 0.15f, size * 0.14f, size * 0.50f);
            Vector2 a = new Vector2(size * 0.50f, size * 0.88f);
            Vector2 b = new Vector2(size * 0.20f, size * 0.55f);
            Vector2 c = new Vector2(size * 0.80f, size * 0.55f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    if (stem.Contains(p) || PointInTriangle(p, a, b, c))
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D DrawCross(int size, Color color)
        {
            Texture2D texture = NewTexture(size, size);
            float thickness = size * 0.055f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d1 = Mathf.Abs(y - x);
                    float d2 = Mathf.Abs(y - (size - x));
                    if (d1 < thickness || d2 < thickness)
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D DrawCheck(int size, Color color)
        {
            Texture2D texture = NewTexture(size, size);
            Vector2 a = new Vector2(size * 0.22f, size * 0.48f);
            Vector2 b = new Vector2(size * 0.42f, size * 0.28f);
            Vector2 c = new Vector2(size * 0.80f, size * 0.72f);
            DrawThickSegment(texture, a, b, size * 0.055f, color);
            DrawThickSegment(texture, b, c, size * 0.055f, color);
            texture.Apply();
            return texture;
        }

        private static Texture2D DrawCone(int size, Color color, float angleDegrees)
        {
            Texture2D texture = NewTexture(size, size);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float halfAngle = angleDegrees * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 v = new Vector2(x, y) - center;
                    float dist = v.magnitude / (size * 0.46f);
                    if (dist > 1f || dist <= 0.02f)
                    {
                        continue;
                    }

                    float angle = Vector2.SignedAngle(Vector2.up, v.normalized);
                    if (Mathf.Abs(angle) <= halfAngle)
                    {
                        Color c = color;
                        c.a *= Mathf.Lerp(0.90f, 0.38f, dist);
                        texture.SetPixel(x, y, c);
                    }
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D DrawNoise(int size, Color color)
        {
            Texture2D texture = NewTexture(size, size);
            System.Random random = new System.Random(42);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = (float)random.NextDouble();
                    Color c = color;
                    c.a *= Mathf.Lerp(0.08f, 0.55f, n);
                    texture.SetPixel(x, y, c);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D DrawFlowStripe(int size, Color color)
        {
            Texture2D texture = NewTexture(size, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int stripe = (x + y) % 48;
                    if (stripe < 16)
                    {
                        Color c = color;
                        c.a *= Mathf.Lerp(0.2f, 0.9f, 1f - stripe / 16f);
                        texture.SetPixel(x, y, c);
                    }
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D DrawTargetLock(int size, Color color)
        {
            Texture2D texture = NewTexture(size, size);
            float thick = size * 0.035f;
            float min = size * 0.18f;
            float max = size * 0.82f;
            float arm = size * 0.18f;
            DrawRect(texture, min, max - thick, arm, thick, color);
            DrawRect(texture, min, max - arm, thick, arm, color);
            DrawRect(texture, max - arm, max - thick, arm, thick, color);
            DrawRect(texture, max - thick, max - arm, thick, arm, color);
            DrawRect(texture, min, min, arm, thick, color);
            DrawRect(texture, min, min, thick, arm, color);
            DrawRect(texture, max - arm, min, arm, thick, color);
            DrawRect(texture, max - thick, min, thick, arm, color);
            texture.Apply();
            return texture;
        }

        private static Texture2D DrawProgressRing(int size, Color color, float progress)
        {
            Texture2D texture = NewTexture(size, size);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 v = new Vector2(x, y) - center;
                    float d = v.magnitude / half;
                    if (d < 0.35f || d > 0.48f)
                    {
                        continue;
                    }

                    float angle = Mathf.Atan2(v.y, v.x) / (Mathf.PI * 2f);
                    angle = (angle + 1.25f) % 1f;
                    if (angle <= progress)
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }

            texture.Apply();
            return texture;
        }

        private static Mesh CreateCircleMesh(string name, int segments)
        {
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i == segments - 1 ? 1 : i + 2;
            }

            Mesh mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = CreateRadialUvs(vertices);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateRectangleMesh(string name)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateConeMesh(string name, float angleDegrees, int segments)
        {
            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            float start = -angleDegrees * Mathf.Deg2Rad * 0.5f;
            float step = angleDegrees * Mathf.Deg2Rad / segments;
            for (int i = 0; i <= segments; i++)
            {
                float angle = start + step * i;
                vertices[i + 1] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            Mesh mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = CreateRadialUvs(vertices);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateRuntimeArrowMesh()
        {
            Mesh mesh = new Mesh { name = "RuntimeArrowMesh" };
            mesh.vertices = new[]
            {
                new Vector3(-0.10f, 0f, -0.30f), new Vector3(0.10f, 0f, -0.30f), new Vector3(-0.10f, 0f, 0.10f), new Vector3(0.10f, 0f, 0.10f),
                new Vector3(-0.28f, 0f, 0.10f), new Vector3(0.28f, 0f, 0.10f), new Vector3(0f, 0f, 0.42f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1, 4, 6, 5 };
            mesh.uv = CreateRadialUvs(mesh.vertices);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2[] CreateRadialUvs(Vector3[] vertices)
        {
            Vector2[] uvs = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                uvs[i] = new Vector2(vertices[i].x * 0.5f + 0.5f, vertices[i].z * 0.5f + 0.5f);
            }

            return uvs;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float area = 0.5f * (-b.y * c.x + a.y * (-b.x + c.x) + a.x * (b.y - c.y) + b.x * c.y);
            float s = 1f / (2f * area) * (a.y * c.x - a.x * c.y + (c.y - a.y) * p.x + (a.x - c.x) * p.y);
            float t = 1f / (2f * area) * (a.x * b.y - a.y * b.x + (a.y - b.y) * p.x + (b.x - a.x) * p.y);
            return s >= 0f && t >= 0f && 1f - s - t >= 0f;
        }

        private static void WriteSineWave(string filePath, float frequency, float durationSeconds, float volume)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                int dataSize = sampleCount * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);

                for (int i = 0; i < sampleCount; i++)
                {
                    float t = i / (float)sampleRate;
                    float envelope = Mathf.Clamp01(1f - i / (float)sampleCount);
                    float sample = Mathf.Sin(Mathf.PI * 2f * frequency * t) * volume * envelope;
                    writer.Write((short)Mathf.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue));
                }
            }
        }

        private static void DrawThickSegment(Texture2D texture, Vector2 a, Vector2 b, float thickness, Color color)
        {
            int width = texture.width;
            int height = texture.height;
            Vector2 ab = b - a;
            float lenSqr = ab.sqrMagnitude;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSqr);
                    Vector2 closest = a + ab * t;
                    if (Vector2.Distance(p, closest) <= thickness)
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }
        }

        private static void DrawRect(Texture2D texture, float x, float y, float width, float height, Color color)
        {
            for (int yy = Mathf.RoundToInt(y); yy < Mathf.RoundToInt(y + height); yy++)
            {
                for (int xx = Mathf.RoundToInt(x); xx < Mathf.RoundToInt(x + width); xx++)
                {
                    if (xx >= 0 && xx < texture.width && yy >= 0 && yy < texture.height)
                    {
                        texture.SetPixel(xx, yy, color);
                    }
                }
            }
        }
    }

}
