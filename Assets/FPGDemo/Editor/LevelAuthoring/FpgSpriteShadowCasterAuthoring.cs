using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FPG.Demo.Editor.LevelAuthoring
{
    public static class FpgSpriteShadowCasterAuthoring
    {
        public const string ProxyName = "__ShadowCasterProxy";
        public const string GeneratedRoot =
            "Assets/FPGDemo/Presentation/Level/Environment/Generated/"
            + "SpriteShadowCasters";
        public const string MeshFolder = GeneratedRoot + "/Meshes";
        public const string MaterialFolder = GeneratedRoot + "/Materials";
        public const string TemplateFolder = GeneratedRoot + "/Templates";
        public const string TemplateMaterialPath = TemplateFolder
            + "/M_FPG_SpriteShadowCasterTemplate.mat";

        private const string MenuPath =
            "FPG Demo/Level Authoring/Generate Selected Sprite Shadow Casters";
        private const string UndoName = "Generate Sprite Shadow Casters";

        private static bool isRefreshingImportedSprites;

        public sealed class GenerationReport
        {
            private readonly List<string> messages = new List<string>();

            public int SelectedCount { get; internal set; }

            public int ProcessedCount { get; internal set; }

            public int SkippedCount { get; internal set; }

            public int CreatedProxyCount { get; internal set; }

            public int CreatedMeshCount { get; internal set; }

            public int CreatedMaterialCount { get; internal set; }

            public IReadOnlyList<string> Messages => messages;

            internal void Skip(string message)
            {
                SkippedCount++;
                messages.Add(message);
            }

            internal void AddMessage(string message)
            {
                messages.Add(message);
            }

            public override string ToString()
            {
                return $"Processed {ProcessedCount}/{SelectedCount}; "
                    + $"created {CreatedProxyCount} proxies, "
                    + $"{CreatedMeshCount} meshes, "
                    + $"{CreatedMaterialCount} materials; "
                    + $"skipped {SkippedCount}.";
            }
        }

        private sealed class SourceContext
        {
            public GameObject GameObject;
            public SpriteRenderer Renderer;
            public Sprite Sprite;
            public string TextureGuid;
            public long SpriteLocalId;
        }

        [MenuItem(MenuPath, priority = 140)]
        private static void GenerateSelected()
        {
            GenerationReport report = GenerateForObjects(Selection.gameObjects);
            Debug.Log("[FPG Sprite Shadow Caster] " + report);

            StringBuilder details = new StringBuilder(report.ToString());
            int messageCount = Math.Min(report.Messages.Count, 12);
            for (int index = 0; index < messageCount; index++)
            {
                details.AppendLine();
                details.Append("- ");
                details.Append(report.Messages[index]);
            }

            if (report.Messages.Count > messageCount)
            {
                details.AppendLine();
                details.Append("- ");
                details.Append(report.Messages.Count - messageCount);
                details.Append(" more messages are in the Console.");
            }

            EditorUtility.DisplayDialog(
                "Sprite Shadow Casters",
                details.ToString(),
                "OK");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateGenerateSelected()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            GameObject[] selectedObjects = Selection.gameObjects;
            for (int index = 0; index < selectedObjects.Length; index++)
            {
                GameObject selected = selectedObjects[index];
                SpriteRenderer renderer = selected == null
                    ? null
                    : selected.GetComponent<SpriteRenderer>();
                if (renderer != null && renderer.sprite != null)
                {
                    return true;
                }
            }

            return false;
        }

        public static GenerationReport GenerateForObjects(
            GameObject[] selectedObjects)
        {
            GenerationReport report = new GenerationReport
            {
                SelectedCount = selectedObjects?.Length ?? 0
            };

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                report.Skip("Generation is unavailable while entering or in Play Mode.");
                return report;
            }

            List<SourceContext> sources = CollectSources(
                selectedObjects,
                report);
            if (sources.Count == 0)
            {
                return report;
            }

            Material template = AssetDatabase.LoadAssetAtPath<Material>(
                TemplateMaterialPath);
            if (template == null)
            {
                for (int index = 0; index < sources.Count; index++)
                {
                    report.Skip(
                        $"'{sources[index].GameObject.name}' was skipped because "
                        + $"the template material is missing at '{TemplateMaterialPath}'.");
                }

                return report;
            }

            if (!TryEnsureGeneratedFolders(out string folderError))
            {
                for (int index = 0; index < sources.Count; index++)
                {
                    report.Skip(
                        $"'{sources[index].GameObject.name}' was skipped: "
                        + folderError);
                }

                return report;
            }

            Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>(
                StringComparer.Ordinal);
            Dictionary<string, Material> materials =
                new Dictionary<string, Material>(StringComparer.Ordinal);

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                for (int index = 0; index < sources.Count; index++)
                {
                    SourceContext source = sources[index];
                    try
                    {
                        Mesh mesh = GetOrCreateMesh(
                            source,
                            meshes,
                            report);
                        Material material = GetOrCreateMaterial(
                            source,
                            template,
                            materials,
                            report);
                        bool proxyCreated = CreateOrUpdateProxy(
                            source,
                            mesh,
                            material);
                        report.ProcessedCount++;
                        if (proxyCreated)
                        {
                            report.CreatedProxyCount++;
                        }
                    }
                    catch (Exception exception)
                    {
                        string message = exception.GetBaseException().Message;
                        report.Skip(
                            $"'{source.GameObject.name}' was skipped: {message}");
                        Debug.LogException(exception, source.GameObject);
                    }
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            return report;
        }

        public static bool TryPopulateMesh(
            Mesh mesh,
            Sprite sprite,
            out string error)
        {
            if (mesh == null)
            {
                error = "Target Mesh is missing.";
                return false;
            }

            if (sprite == null)
            {
                error = "Source Sprite is missing.";
                return false;
            }

            Vector2[] spriteVertices = sprite.vertices;
            Vector2[] spriteUv = sprite.uv;
            ushort[] spriteTriangles = sprite.triangles;
            if (spriteVertices == null || spriteVertices.Length < 3)
            {
                error = $"Sprite '{sprite.name}' has fewer than three vertices.";
                return false;
            }

            if (spriteUv == null || spriteUv.Length != spriteVertices.Length)
            {
                error = $"Sprite '{sprite.name}' has invalid UV data.";
                return false;
            }

            if (spriteTriangles == null
                || spriteTriangles.Length < 3
                || spriteTriangles.Length % 3 != 0)
            {
                error = $"Sprite '{sprite.name}' has invalid triangle data.";
                return false;
            }

            Vector3[] vertices = new Vector3[spriteVertices.Length];
            Vector3[] normals = new Vector3[spriteVertices.Length];
            for (int index = 0; index < spriteVertices.Length; index++)
            {
                vertices[index] = new Vector3(
                    spriteVertices[index].x,
                    spriteVertices[index].y,
                    0f);
                normals[index] = Vector3.back;
            }

            int[] triangles = new int[spriteTriangles.Length];
            for (int index = 0; index < spriteTriangles.Length; index++)
            {
                triangles[index] = spriteTriangles[index];
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = spriteUv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            error = string.Empty;
            return true;
        }

        public static int RefreshExistingGeneratedAssetsForTexture(
            string textureAssetPath)
        {
            if (isRefreshingImportedSprites
                || string.IsNullOrWhiteSpace(textureAssetPath))
            {
                return 0;
            }

            Material template = AssetDatabase.LoadAssetAtPath<Material>(
                TemplateMaterialPath);
            if (template == null)
            {
                return 0;
            }

            string textureGuid = AssetDatabase.AssetPathToGUID(
                textureAssetPath);
            if (string.IsNullOrWhiteSpace(textureGuid))
            {
                return 0;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(
                textureAssetPath);
            List<Sprite> sprites = new List<Sprite>();
            for (int index = 0; index < assets.Length; index++)
            {
                if (assets[index] is Sprite sprite)
                {
                    sprites.Add(sprite);
                }
            }

            if (sprites.Count == 0)
            {
                return 0;
            }

            int refreshedCount = 0;
            isRefreshingImportedSprites = true;
            try
            {
                string materialPath = GetMaterialPath(textureGuid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    materialPath);
                if (material != null)
                {
                    CopyTemplateToMaterial(
                        template,
                        material,
                        sprites[0].texture,
                        textureGuid);
                    EditorUtility.SetDirty(material);
                    AssetDatabase.SaveAssetIfDirty(material);
                    refreshedCount++;
                }

                for (int index = 0; index < sprites.Count; index++)
                {
                    Sprite sprite = sprites[index];
                    if (!TryGetSpriteIdentity(
                            sprite,
                            out string spriteGuid,
                            out long localId,
                            out _))
                    {
                        continue;
                    }

                    string meshPath = GetMeshPath(spriteGuid, localId);
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (mesh == null)
                    {
                        continue;
                    }

                    if (!TryPopulateMesh(mesh, sprite, out string error))
                    {
                        Debug.LogWarning(
                            $"Could not refresh sprite shadow Mesh "
                            + $"'{meshPath}': {error}");
                        continue;
                    }

                    mesh.name = GetMeshName(spriteGuid, localId);
                    EditorUtility.SetDirty(mesh);
                    AssetDatabase.SaveAssetIfDirty(mesh);
                    refreshedCount++;
                }
            }
            finally
            {
                isRefreshingImportedSprites = false;
            }

            if (refreshedCount > 0)
            {
                Debug.Log(
                    $"[FPG Sprite Shadow Caster] Refreshed {refreshedCount} "
                    + $"existing generated assets for '{textureAssetPath}'.");
            }

            return refreshedCount;
        }

        public static string GetMeshAssetPath(Sprite sprite)
        {
            if (!TryGetSpriteIdentity(
                    sprite,
                    out string guid,
                    out long localId,
                    out _))
            {
                return string.Empty;
            }

            return GetMeshPath(guid, localId);
        }

        public static string GetMaterialAssetPath(Sprite sprite)
        {
            if (sprite == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(sprite);
            string guid = AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrWhiteSpace(guid)
                ? string.Empty
                : GetMaterialPath(guid);
        }

        private static List<SourceContext> CollectSources(
            GameObject[] selectedObjects,
            GenerationReport report)
        {
            List<SourceContext> sources = new List<SourceContext>();
            if (selectedObjects == null)
            {
                return sources;
            }

            HashSet<int> seenObjects = new HashSet<int>();
            for (int index = 0; index < selectedObjects.Length; index++)
            {
                GameObject selected = selectedObjects[index];
                if (selected == null || !seenObjects.Add(selected.GetInstanceID()))
                {
                    continue;
                }

                if (EditorUtility.IsPersistent(selected)
                    || !selected.scene.IsValid()
                    || !selected.scene.isLoaded)
                {
                    report.Skip(
                        $"'{selected.name}' is not a loaded scene object.");
                    continue;
                }

                SpriteRenderer renderer = selected.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    report.Skip(
                        $"'{selected.name}' has no direct SpriteRenderer.");
                    continue;
                }

                if (renderer.sprite == null)
                {
                    report.Skip(
                        $"'{selected.name}' has no Sprite assigned.");
                    continue;
                }

                if (renderer.drawMode != SpriteDrawMode.Simple)
                {
                    report.Skip(
                        $"'{selected.name}' uses {renderer.drawMode} draw mode; "
                        + "only Simple sprites are supported.");
                    continue;
                }

                if (!TryGetSpriteIdentity(
                        renderer.sprite,
                        out string textureGuid,
                        out long localId,
                        out string identityError))
                {
                    report.Skip(
                        $"'{selected.name}' was skipped: {identityError}");
                    continue;
                }

                sources.Add(new SourceContext
                {
                    GameObject = selected,
                    Renderer = renderer,
                    Sprite = renderer.sprite,
                    TextureGuid = textureGuid,
                    SpriteLocalId = localId
                });
            }

            return sources;
        }

        private static bool TryGetSpriteIdentity(
            Sprite sprite,
            out string textureGuid,
            out long localId,
            out string error)
        {
            textureGuid = string.Empty;
            localId = 0L;
            if (sprite == null)
            {
                error = "Source Sprite is missing.";
                return false;
            }

            string spritePath = AssetDatabase.GetAssetPath(sprite);
            textureGuid = AssetDatabase.AssetPathToGUID(spritePath);
            if (string.IsNullOrWhiteSpace(textureGuid))
            {
                error = $"Sprite '{sprite.name}' is not a persistent asset.";
                return false;
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    sprite,
                    out string objectGuid,
                    out localId)
                || string.IsNullOrWhiteSpace(objectGuid))
            {
                error = $"Sprite '{sprite.name}' has no stable local file ID.";
                return false;
            }

            textureGuid = objectGuid;
            error = string.Empty;
            return true;
        }

        private static Mesh GetOrCreateMesh(
            SourceContext source,
            Dictionary<string, Mesh> cache,
            GenerationReport report)
        {
            string meshPath = GetMeshPath(
                source.TextureGuid,
                source.SpriteLocalId);
            if (cache.TryGetValue(meshPath, out Mesh cachedMesh))
            {
                return cachedMesh;
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            bool created = mesh == null;
            if (created && AssetDatabase.LoadMainAssetAtPath(meshPath) != null)
            {
                throw new InvalidOperationException(
                    $"Generated Mesh path '{meshPath}' contains a non-Mesh asset.");
            }

            if (created)
            {
                mesh = new Mesh();
            }
            else
            {
                Undo.RecordObject(mesh, UndoName);
            }

            string meshName = GetMeshName(
                source.TextureGuid,
                source.SpriteLocalId);
            if (!TryPopulateMesh(mesh, source.Sprite, out string error))
            {
                if (created)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }

                throw new InvalidOperationException(error);
            }

            mesh.name = meshName;
            if (created)
            {
                AssetDatabase.CreateAsset(mesh, meshPath);
                report.CreatedMeshCount++;
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }

            AssetDatabase.SaveAssetIfDirty(mesh);
            cache.Add(meshPath, mesh);
            return mesh;
        }

        private static Material GetOrCreateMaterial(
            SourceContext source,
            Material template,
            Dictionary<string, Material> cache,
            GenerationReport report)
        {
            string materialPath = GetMaterialPath(source.TextureGuid);
            if (cache.TryGetValue(materialPath, out Material cachedMaterial))
            {
                return cachedMaterial;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                materialPath);
            bool created = material == null;
            if (created && AssetDatabase.LoadMainAssetAtPath(materialPath) != null)
            {
                throw new InvalidOperationException(
                    $"Generated material path '{materialPath}' contains a "
                    + "non-Material asset.");
            }

            if (created)
            {
                material = new Material(template);
            }
            else
            {
                Undo.RecordObject(material, UndoName);
            }

            CopyTemplateToMaterial(
                template,
                material,
                source.Sprite.texture,
                source.TextureGuid);
            if (created)
            {
                AssetDatabase.CreateAsset(material, materialPath);
                report.CreatedMaterialCount++;
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssetIfDirty(material);
            cache.Add(materialPath, material);
            return material;
        }

        private static void CopyTemplateToMaterial(
            Material template,
            Material material,
            Texture texture,
            string textureGuid)
        {
            EditorUtility.CopySerialized(template, material);
            material.name = textureGuid;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

        private static bool CreateOrUpdateProxy(
            SourceContext source,
            Mesh mesh,
            Material material)
        {
            Transform sourceTransform = source.GameObject.transform;
            Transform proxyTransform = FindDirectProxy(sourceTransform);
            bool created = proxyTransform == null;
            if (created)
            {
                GameObject proxyObject = new GameObject(ProxyName);
                Undo.RegisterCreatedObjectUndo(proxyObject, UndoName);
                Undo.SetTransformParent(proxyObject.transform, sourceTransform, UndoName);
                proxyTransform = proxyObject.transform;
            }

            GameObject proxy = proxyTransform.gameObject;
            bool changed = created;
            changed |= ApplyProxyObjectSettings(proxy, source.GameObject);
            changed |= ApplyProxyTransformSettings(proxyTransform);

            MeshFilter filter = proxy.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = Undo.AddComponent<MeshFilter>(proxy);
                changed = true;
            }

            MeshRenderer renderer = proxy.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = Undo.AddComponent<MeshRenderer>(proxy);
                changed = true;
            }

            if (filter.sharedMesh != mesh)
            {
                Undo.RecordObject(filter, UndoName);
                filter.sharedMesh = mesh;
                changed = true;
            }

            changed |= ApplyProxyRendererSettings(
                renderer,
                source.Renderer,
                material);

            if (PrefabUtility.IsPartOfPrefabInstance(proxy))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    proxyTransform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }

            if (changed && source.GameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(source.GameObject.scene);
            }

            return created;
        }

        private static Transform FindDirectProxy(Transform source)
        {
            for (int index = 0; index < source.childCount; index++)
            {
                Transform child = source.GetChild(index);
                if (string.Equals(
                        child.name,
                        ProxyName,
                        StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static bool ApplyProxyObjectSettings(
            GameObject proxy,
            GameObject source)
        {
            bool changed = false;
            if (!proxy.activeSelf)
            {
                Undo.RecordObject(proxy, UndoName);
                proxy.SetActive(true);
                changed = true;
            }

            if (proxy.layer != source.layer)
            {
                Undo.RecordObject(proxy, UndoName);
                proxy.layer = source.layer;
                changed = true;
            }

            return changed;
        }

        private static bool ApplyProxyTransformSettings(Transform proxy)
        {
            if (proxy.localPosition == Vector3.zero
                && proxy.localRotation == Quaternion.identity
                && proxy.localScale == Vector3.one)
            {
                return false;
            }

            Undo.RecordObject(proxy, UndoName);
            proxy.localPosition = Vector3.zero;
            proxy.localRotation = Quaternion.identity;
            proxy.localScale = Vector3.one;
            return true;
        }

        private static bool ApplyProxyRendererSettings(
            MeshRenderer renderer,
            SpriteRenderer source,
            Material material)
        {
            bool requiresUpdate = !renderer.enabled
                || renderer.shadowCastingMode != ShadowCastingMode.On
                || renderer.receiveShadows
                || renderer.lightProbeUsage != LightProbeUsage.Off
                || renderer.reflectionProbeUsage != ReflectionProbeUsage.Off
                || renderer.motionVectorGenerationMode
                    != MotionVectorGenerationMode.ForceNoMotion
                || renderer.allowOcclusionWhenDynamic
                || renderer.forceRenderingOff
                || renderer.renderingLayerMask != source.renderingLayerMask
                || renderer.sortingLayerID != source.sortingLayerID
                || renderer.sortingOrder != source.sortingOrder
                || renderer.rendererPriority != source.rendererPriority
                || renderer.sharedMaterial != material;
            if (!requiresUpdate)
            {
                return false;
            }

            Undo.RecordObject(renderer, UndoName);
            renderer.enabled = true;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.forceRenderingOff = false;
            renderer.renderingLayerMask = source.renderingLayerMask;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            renderer.rendererPriority = source.rendererPriority;
            renderer.sharedMaterial = material;
            return true;
        }

        private static bool TryEnsureGeneratedFolders(out string error)
        {
            string[] folders =
            {
                GeneratedRoot,
                MeshFolder,
                MaterialFolder,
                TemplateFolder
            };
            for (int index = 0; index < folders.Length; index++)
            {
                string folder = folders[index];
                if (AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                string parent = Path.GetDirectoryName(folder)
                    ?.Replace('\\', '/');
                string name = Path.GetFileName(folder);
                if (string.IsNullOrWhiteSpace(parent)
                    || !AssetDatabase.IsValidFolder(parent)
                    || string.IsNullOrWhiteSpace(
                        AssetDatabase.CreateFolder(parent, name)))
                {
                    error = $"Could not create generated folder '{folder}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static string GetMeshName(string guid, long localId)
        {
            return guid + "_" + localId.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetMeshPath(string guid, long localId)
        {
            return MeshFolder + "/" + GetMeshName(guid, localId) + ".asset";
        }

        private static string GetMaterialPath(string textureGuid)
        {
            return MaterialFolder + "/" + textureGuid + ".mat";
        }
    }

    internal sealed class FpgSpriteShadowCasterAssetPostprocessor
        : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            for (int index = 0; index < importedAssets.Length; index++)
            {
                string path = importedAssets[index];
                if (AssetImporter.GetAtPath(path) is TextureImporter)
                {
                    FpgSpriteShadowCasterAuthoring
                        .RefreshExistingGeneratedAssetsForTexture(path);
                }
            }
        }
    }
}
