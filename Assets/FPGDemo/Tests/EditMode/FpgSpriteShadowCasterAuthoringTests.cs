using System;
using FPG.Demo.Editor.LevelAuthoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FPG.Demo.Tests.EditMode
{
    [TestFixture]
    public sealed class FpgSpriteShadowCasterAuthoringTests
    {
        private const string TemporaryRoot =
            "Assets/FPGDemo/Tests/EditMode/__SpriteShadowCasterTemp";

        private GameObject sourceObject;
        private string temporaryFolderPath;
        private string texturePath;
        private Sprite sprite;

        [SetUp]
        public void SetUp()
        {
            temporaryFolderPath = TemporaryRoot + Guid.NewGuid().ToString("N");
            Assert.That(
                AssetDatabase.CreateFolder(
                    "Assets/FPGDemo/Tests/EditMode",
                    temporaryFolderPath.Substring(
                        "Assets/FPGDemo/Tests/EditMode/".Length)),
                Is.Not.Empty);

            texturePath = temporaryFolderPath + "/Sprite.asset";
            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.name = "TemporarySpriteTexture";
            texture.SetPixels(new[]
            {
                Color.white, Color.white, Color.white, Color.white,
                Color.white, Color.white, Color.white, Color.white,
                Color.white, Color.white, Color.white, Color.white,
                Color.white, Color.white, Color.white, Color.white
            });
            texture.Apply();
            AssetDatabase.CreateAsset(texture, texturePath);
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "TemporarySprite";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.SaveAssets();

            sourceObject = new GameObject("TemporarySpriteSource");
            SpriteRenderer renderer = sourceObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
        }

        [TearDown]
        public void TearDown()
        {
            if (sourceObject != null)
            {
                UnityEngine.Object.DestroyImmediate(sourceObject);
            }

            string meshPath = FpgSpriteShadowCasterAuthoring
                .GetMeshAssetPath(sprite);
            string materialPath = FpgSpriteShadowCasterAuthoring
                .GetMaterialAssetPath(sprite);
            if (!string.IsNullOrWhiteSpace(meshPath))
            {
                AssetDatabase.DeleteAsset(meshPath);
            }

            if (!string.IsNullOrWhiteSpace(materialPath))
            {
                AssetDatabase.DeleteAsset(materialPath);
            }

            if (!string.IsNullOrWhiteSpace(texturePath))
            {
                AssetDatabase.DeleteAsset(texturePath);
            }

            if (!string.IsNullOrWhiteSpace(temporaryFolderPath))
            {
                AssetDatabase.DeleteAsset(temporaryFolderPath);
            }
        }

        [Test]
        public void MeshUsesSpriteVerticesUvAndTriangles()
        {
            Mesh mesh = new Mesh();
            try
            {
                Assert.That(
                    FpgSpriteShadowCasterAuthoring.TryPopulateMesh(
                        mesh,
                        sprite,
                        out string error),
                    Is.True,
                    error);
                Assert.That(mesh.vertices.Length, Is.EqualTo(sprite.vertices.Length));
                Assert.That(mesh.uv.Length, Is.EqualTo(sprite.uv.Length));
                Assert.That(mesh.triangles.Length, Is.EqualTo(sprite.triangles.Length));
                Assert.That(mesh.normals.Length, Is.EqualTo(sprite.vertices.Length));
                Assert.That(mesh.bounds.size.z, Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void GenerationCreatesVisibleOnShadowProxyAndKeepsSourceRenderer()
        {
            SpriteRenderer sourceRenderer = sourceObject.GetComponent<SpriteRenderer>();
            Assert.That(sourceRenderer.enabled, Is.True);

            FpgSpriteShadowCasterAuthoring.GenerationReport report =
                FpgSpriteShadowCasterAuthoring.GenerateForObjects(
                    new[] { sourceObject });

            Assert.That(report.ProcessedCount, Is.EqualTo(1), report.ToString());
            Assert.That(report.CreatedProxyCount, Is.EqualTo(1));
            Assert.That(sourceRenderer.enabled, Is.True);

            Transform proxy = sourceObject.transform.Find(
                FpgSpriteShadowCasterAuthoring.ProxyName);
            Assert.That(proxy, Is.Not.Null);
            MeshFilter filter = proxy.GetComponent<MeshFilter>();
            MeshRenderer renderer = proxy.GetComponent<MeshRenderer>();
            Assert.That(filter.sharedMesh, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(renderer.enabled, Is.True);
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
            Assert.That(renderer.receiveShadows, Is.False);
            Assert.That(renderer.lightProbeUsage, Is.EqualTo(LightProbeUsage.Off));
            Assert.That(renderer.reflectionProbeUsage, Is.EqualTo(ReflectionProbeUsage.Off));
            Assert.That(
                renderer.motionVectorGenerationMode,
                Is.EqualTo(MotionVectorGenerationMode.ForceNoMotion));
            Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"),
                Is.SameAs(sprite.texture));
        }

        [Test]
        public void RepeatedGenerationReusesProxyAndGeneratedAssets()
        {
            FpgSpriteShadowCasterAuthoring.GenerateForObjects(
                new[] { sourceObject });
            Transform proxy = sourceObject.transform.Find(
                FpgSpriteShadowCasterAuthoring.ProxyName);
            Mesh mesh = proxy.GetComponent<MeshFilter>().sharedMesh;
            Material material = proxy.GetComponent<MeshRenderer>().sharedMaterial;

            FpgSpriteShadowCasterAuthoring.GenerationReport report =
                FpgSpriteShadowCasterAuthoring.GenerateForObjects(
                    new[] { sourceObject });

            Assert.That(report.ProcessedCount, Is.EqualTo(1), report.ToString());
            Assert.That(report.CreatedProxyCount, Is.EqualTo(0));
            Assert.That(sourceObject.transform.childCount, Is.EqualTo(1));
            Assert.That(
                proxy.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(mesh));
            Assert.That(
                proxy.GetComponent<MeshRenderer>().sharedMaterial,
                Is.SameAs(material));
        }

        [Test]
        public void SelectionDoesNotRecurseIntoChildren()
        {
            GameObject parent = new GameObject("ParentWithoutSpriteRenderer");
            GameObject child = new GameObject("ChildWithSpriteRenderer");
            child.transform.SetParent(parent.transform, false);
            SpriteRenderer childRenderer = child.AddComponent<SpriteRenderer>();
            childRenderer.sprite = sprite;

            FpgSpriteShadowCasterAuthoring.GenerationReport report =
                FpgSpriteShadowCasterAuthoring.GenerateForObjects(
                    new[] { parent });

            Assert.That(report.ProcessedCount, Is.EqualTo(0));
            Assert.That(parent.transform.Find(
                FpgSpriteShadowCasterAuthoring.ProxyName), Is.Null);
            Assert.That(child.transform.Find(
                FpgSpriteShadowCasterAuthoring.ProxyName), Is.Null);
            UnityEngine.Object.DestroyImmediate(parent);
        }

        [Test]
        public void TextureRefreshOnlyUpdatesExistingGeneratedAssets()
        {
            FpgSpriteShadowCasterAuthoring.GenerateForObjects(
                new[] { sourceObject });
            string meshPath = FpgSpriteShadowCasterAuthoring
                .GetMeshAssetPath(sprite);
            string materialPath = FpgSpriteShadowCasterAuthoring
                .GetMaterialAssetPath(sprite);
            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(meshPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(materialPath), Is.Not.Null);

            int refreshed = FpgSpriteShadowCasterAuthoring
                .RefreshExistingGeneratedAssetsForTexture(texturePath);

            Assert.That(refreshed, Is.GreaterThanOrEqualTo(2));
            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(meshPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(materialPath), Is.Not.Null);
        }
    }
}
