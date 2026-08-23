#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace FPG.Demo.Tests.PlayMode
{
    public sealed class FpgCoverProjectileBlockerPlayModeTests
    {
        private const string ProjectileBlockerProxyName =
            "__ProjectileBlockerProxy";
        private const string FormalCoverPrefabPath =
            "Assets/FPGDemo/Presentation/Level/Covers/Prefabs/PF_FPG_Root1TreeCover.prefab";

        [Test]
        public void ValidatingPrefabAssetDoesNotCreateRuntimeBlockers()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FormalCoverPrefabPath);
            Assert.That(prefab, Is.Not.Null, FormalCoverPrefabPath);
            FpgCoverEntityView view = prefab.GetComponent<FpgCoverEntityView>();
            Assert.That(view, Is.Not.Null, FormalCoverPrefabPath);
            int proxyCountBefore = CountProjectileBlockerProxies();

            Assert.That(view.TryValidate(out string error), Is.True, error);

            Assert.That(
                CountProjectileBlockerProxies(),
                Is.EqualTo(proxyCountBefore));
        }

        [UnityTest]
        public IEnumerator EnemyProjectileHitsCoverBeforePlayerBody()
        {
            Mesh blockerMesh = CreateVerticalQuad();
            GameObject coverRoot = new GameObject("CoverRoot");
            GameObject playerRoot = new GameObject("PlayerBody");
            coverRoot.SetActive(false);

            try
            {
                GameObject intactRoot = CreateChild(coverRoot, "IntactRoot");
                GameObject destroyedRoot = CreateChild(
                    coverRoot,
                    "DestroyedRoot");
                GameObject source = CreateChild(
                    intactRoot,
                    "__ShadowCasterProxy");
                source.transform.localPosition = new Vector3(0f, 0f, 0.1f);
                MeshFilter meshFilter = source.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = blockerMesh;
                MeshCollider authoredCollider =
                    source.AddComponent<MeshCollider>();
                authoredCollider.sharedMesh = blockerMesh;

                FpgCoverEntityView view =
                    coverRoot.AddComponent<FpgCoverEntityView>();
                SetPrivateField(view, "intactRoot", intactRoot);
                SetPrivateField(view, "destroyedRoot", destroyedRoot);

                CapsuleCollider playerBody =
                    playerRoot.AddComponent<CapsuleCollider>();
                playerBody.radius = 0.45f;
                playerBody.height = 1.8f;
                playerBody.direction = 1;

                coverRoot.SetActive(true);
                yield return null;
                Physics.SyncTransforms();

                Assert.That(
                    view.TryGetBlockingCollider(0, out Collider blocker),
                    Is.True);
                Assert.That(blocker.name, Is.EqualTo(ProjectileBlockerProxyName));
                Assert.That(blocker.enabled, Is.True);
                Assert.That(authoredCollider.enabled, Is.False);

                bool didHit = Physics.SphereCast(
                    new Vector3(0f, 0f, 3f),
                    0.25f,
                    Vector3.back,
                    out RaycastHit hit,
                    5f,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Collide);

                Assert.That(didHit, Is.True);
                Assert.That(hit.collider, Is.SameAs(blocker));
                Assert.That(hit.collider, Is.Not.SameAs(playerBody));
            }
            finally
            {
                Object.Destroy(coverRoot);
                Object.Destroy(playerRoot);
                Object.Destroy(blockerMesh);
            }
        }

        private static int CountProjectileBlockerProxies()
        {
            return Resources.FindObjectsOfTypeAll<Transform>()
                .Count(candidate => candidate.name == ProjectileBlockerProxyName);
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static Mesh CreateVerticalQuad()
        {
            Mesh mesh = new Mesh
            {
                name = "CoverProjectileBlockerTestMesh",
                vertices = new[]
                {
                    new Vector3(-1f, -1f, 0f),
                    new Vector3(1f, -1f, 0f),
                    new Vector3(1f, 1f, 0f),
                    new Vector3(-1f, 1f, 0f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
#endif
