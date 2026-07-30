using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using FPG.Demo.Editor.LevelAuthoring;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgRoomDuplicationContractTests
    {
        private const string ForestRoomPath =
            "Assets/FPGDemo/Config/Level/Rooms/Room_forest.asset";
        private const string ForestScenePath =
            "Assets/FPGDemo/Presentation/Level/Rooms/Forest/ART_Forest.unity";

        [Test]
        public void DuplicateOperationCreatesIndependentBoundArtScene()
        {
            string folder = CreateTemporaryFolder();
            string roomPath = folder + "/RoomCopy.asset";
            string createdScenePath = string.Empty;
            FpgRoomDefinition source =
                AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(ForestRoomPath);
            FpgRoomDefinition copy = null;
            try
            {
                Assert.That(source, Is.Not.Null);
                Assert.That(
                    FpgRoomAuthoringOperations.TryDuplicateRoomWithArtScene(
                        source,
                        roomPath,
                        false,
                        out copy,
                        out string error),
                    Is.True,
                    error);

                createdScenePath = copy.ArtScenePath;
                Assert.That(copy, Is.Not.SameAs(source));
                Assert.That(copy.RoomId, Is.Not.EqualTo(source.RoomId));
                Assert.That(copy.ArtScenePath, Is.Not.EqualTo(source.ArtScenePath));
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(copy.ArtScenePath),
                    Is.Not.Null);
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        copy,
                        out string contractError),
                    Is.True,
                    contractError);

                AssertRendererMaterialSemanticsMatch(
                    source.ArtScenePath,
                    copy.ArtScenePath);

                Scene scene = EditorSceneManager.OpenScene(
                    copy.ArtScenePath,
                    OpenSceneMode.Additive);
                try
                {
                    FpgRoomArtRoot root = scene.GetRootGameObjects()[0]
                        .GetComponent<FpgRoomArtRoot>();
                    Assert.That(root.RoomDefinition, Is.SameAs(copy));
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            finally
            {
                Scene loadedScene = SceneManager.GetSceneByPath(createdScenePath);
                if (loadedScene.IsValid() && loadedScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(loadedScene, true);
                }

                if (!string.IsNullOrWhiteSpace(createdScenePath))
                {
                    AssetDatabase.DeleteAsset(createdScenePath);
                }

                AssetDatabase.DeleteAsset(
                    FpgCoverCameraProfileAuthoring.DefaultProfileRoot
                    + "/RoomCopy");
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void BindingRepairRepointsCopiedArtRoot()
        {
            string folder = CreateTemporaryFolder();
            string scenePath = folder + "/Copied.unity";
            string roomPath = folder + "/CopiedRoom.asset";
            try
            {
                Assert.That(
                    AssetDatabase.CopyAsset(ForestScenePath, scenePath),
                    Is.True);
                Assert.That(
                    AssetDatabase.CopyAsset(ForestRoomPath, roomPath),
                    Is.True);
                FpgRoomDefinition room =
                    AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(roomPath);
                SetArtSceneReference(
                    room,
                    AssetDatabase.AssetPathToGUID(scenePath),
                    scenePath);
                EditorUtility.SetDirty(room);
                AssetDatabase.SaveAssetIfDirty(room);

                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        room,
                        out string initialError),
                    Is.False);
                StringAssert.Contains("references room", initialError);

                Assert.That(
                    FpgRoomAuthoringOperations.TryBindArtSceneRoot(
                        room,
                        out string repairError),
                    Is.True,
                    repairError);
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        room,
                        out string repairedError),
                    Is.True,
                    repairedError);

                Scene scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
                try
                {
                    FpgRoomArtRoot root = scene.GetRootGameObjects()[0]
                        .GetComponent<FpgRoomArtRoot>();
                    Assert.That(root.RoomDefinition, Is.SameAs(room));
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            finally
            {
                Scene loadedScene = SceneManager.GetSceneByPath(scenePath);
                if (loadedScene.IsValid() && loadedScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(loadedScene, true);
                }

                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void RootResolutionIgnoresTransientEditorPreviewRoots()
        {
            FpgRoomDefinition room =
                AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(
                    ForestRoomPath);
            Assert.That(room, Is.Not.Null);

            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ForestScenePath);
            bool openedByTest = !scene.IsValid() || !scene.isLoaded;
            GameObject transientPreview = null;
            try
            {
                if (openedByTest)
                {
                    scene = EditorSceneManager.OpenScene(
                        ForestScenePath,
                        OpenSceneMode.Additive);
                }

                transientPreview = new GameObject(
                    "__Transient Room Editor Preview__")
                {
                    hideFlags = HideFlags.HideInHierarchy
                        | HideFlags.DontSaveInEditor
                        | HideFlags.NotEditable
                };
                SceneManager.MoveGameObjectToScene(
                    transientPreview,
                    scene);

                Assert.That(
                    FpgRoomArtRoot.TryResolve(
                        scene,
                        room,
                        out FpgRoomArtRoot root,
                        out string error),
                    Is.True,
                    error);
                Assert.That(root.RoomDefinition, Is.SameAs(room));
            }
            finally
            {
                if (transientPreview != null)
                {
                    UnityEngine.Object.DestroyImmediate(transientPreview);
                }

                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }

                if (openedByTest && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void AssertRendererMaterialSemanticsMatch(
            string sourceScenePath,
            string copyScenePath)
        {
            Scene previousActive = SceneManager.GetActiveScene();
            Scene sourceScene = SceneManager.GetSceneByPath(sourceScenePath);
            Scene copyScene = SceneManager.GetSceneByPath(copyScenePath);
            bool openedSource = !sourceScene.IsValid() || !sourceScene.isLoaded;
            bool openedCopy = !copyScene.IsValid() || !copyScene.isLoaded;
            try
            {
                if (openedSource)
                {
                    sourceScene = EditorSceneManager.OpenScene(
                        sourceScenePath,
                        OpenSceneMode.Additive);
                }

                if (openedCopy)
                {
                    copyScene = EditorSceneManager.OpenScene(
                        copyScenePath,
                        OpenSceneMode.Additive);
                }

                Dictionary<string, string> sourceMaterials =
                    CaptureRendererMaterialSemantics(sourceScene);
                Dictionary<string, string> copyMaterials =
                    CaptureRendererMaterialSemantics(copyScene);
                CollectionAssert.AreEquivalent(
                    sourceMaterials.Keys,
                    copyMaterials.Keys,
                    "Duplicating a room changed renderer or material-slot structure.");
                foreach (KeyValuePair<string, string> source in sourceMaterials)
                {
                    Assert.That(
                        copyMaterials[source.Key],
                        Is.EqualTo(source.Value),
                        $"Duplicating a room changed visual material semantics at '{source.Key}'.");
                }
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }

                if (openedCopy && copyScene.IsValid() && copyScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(copyScene, true);
                }

                if (openedSource && sourceScene.IsValid() && sourceScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(sourceScene, true);
                }
            }
        }

        private static Dictionary<string, string>
            CaptureRendererMaterialSemantics(Scene scene)
        {
            Dictionary<string, string> result =
                new Dictionary<string, string>(StringComparer.Ordinal);
            GameObject[] roots = scene.GetRootGameObjects()
                .Where(root =>
                    (root.hideFlags & HideFlags.DontSaveInEditor) == 0)
                .ToArray();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Renderer[] renderers =
                    roots[rootIndex].GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    Renderer[] components =
                        renderer.gameObject.GetComponents<Renderer>();
                    int componentIndex = Array.IndexOf(components, renderer);
                    string rendererKey = GetStableTransformPath(
                            renderer.transform,
                            roots[rootIndex].transform)
                        + "/"
                        + renderer.GetType().FullName
                        + "["
                        + componentIndex
                        + "]";
                    Material[] materials = renderer.sharedMaterials;
                    for (int materialIndex = 0;
                         materialIndex < materials.Length;
                         materialIndex++)
                    {
                        result.Add(
                            rendererKey + "/material[" + materialIndex + "]",
                            DescribeMaterial(materials[materialIndex]));
                    }
                }
            }

            return result;
        }

        private static string GetStableTransformPath(
            Transform target,
            Transform sceneRoot)
        {
            Stack<string> segments = new Stack<string>();
            Transform current = target;
            while (current != null)
            {
                segments.Push(
                    current.name + "[" + current.GetSiblingIndex() + "]");
                if (current == sceneRoot)
                {
                    break;
                }

                current = current.parent;
            }

            return string.Join("/", segments.ToArray());
        }

        private static string DescribeMaterial(Material material)
        {
            if (material == null)
            {
                return "<null>";
            }

            Shader shader = material.shader;
            StringBuilder value = new StringBuilder();
            value.Append(material.name)
                .Append('|')
                .Append(shader == null ? "<no shader>" : shader.name)
                .Append('|')
                .Append(material.renderQueue)
                .Append('|')
                .Append(material.enableInstancing)
                .Append('|')
                .Append(material.doubleSidedGI);
            string[] keywords = material.enabledKeywords
                .Select(keyword => keyword.name)
                .OrderBy(keyword => keyword, StringComparer.Ordinal)
                .ToArray();
            value.Append("|keywords=")
                .Append(string.Join(",", keywords));
            if (shader == null)
            {
                return value.ToString();
            }

            int propertyCount = shader.GetPropertyCount();
            for (int propertyIndex = 0;
                 propertyIndex < propertyCount;
                 propertyIndex++)
            {
                string propertyName = shader.GetPropertyName(propertyIndex);
                if (!material.HasProperty(propertyName))
                {
                    continue;
                }

                ShaderPropertyType propertyType =
                    shader.GetPropertyType(propertyIndex);
                value.Append('|').Append(propertyName).Append('=');
                switch (propertyType)
                {
                    case ShaderPropertyType.Color:
                        AppendVector(value, material.GetColor(propertyName));
                        break;
                    case ShaderPropertyType.Vector:
                        AppendVector(value, material.GetVector(propertyName));
                        break;
                    case ShaderPropertyType.Texture:
                        Texture texture = material.GetTexture(propertyName);
                        value.Append(texture == null
                            ? "<null>"
                            : AssetDatabase.GetAssetPath(texture)
                                + "#"
                                + texture.name);
                        AppendVector(
                            value,
                            material.GetTextureScale(propertyName));
                        AppendVector(
                            value,
                            material.GetTextureOffset(propertyName));
                        break;
                    default:
                        value.Append(material.GetFloat(propertyName).ToString(
                            "R",
                            CultureInfo.InvariantCulture));
                        break;
                }
            }

            return value.ToString();
        }

        private static void AppendVector(
            StringBuilder target,
            Vector4 value)
        {
            target.Append('(')
                .Append(value.x.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(value.y.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(value.z.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(value.w.ToString("R", CultureInfo.InvariantCulture))
                .Append(')');
        }

        private static string CreateTemporaryFolder()
        {
            const string parent = "Assets/FPGDemo/Tests/EditMode";
            string name = "__RoomDuplicationTemp_"
                + Guid.NewGuid().ToString("N");
            Assert.That(AssetDatabase.CreateFolder(parent, name), Is.Not.Empty);
            return parent + "/" + name;
        }

        private static void SetArtSceneReference(
            FpgRoomDefinition room,
            string guid,
            string path)
        {
            SerializedObject data = new SerializedObject(room);
            SerializedProperty artScene = data.FindProperty("artScene");
            artScene.FindPropertyRelative("sceneGuid").stringValue = guid;
            artScene.FindPropertyRelative("scenePath").stringValue = path;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
