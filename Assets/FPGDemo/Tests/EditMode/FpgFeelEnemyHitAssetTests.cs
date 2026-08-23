using System;
using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgFeelEnemyHitAssetTests
    {
        private const string SharedPrefabPath =
            "Assets/FPGDemo/Integrations/Feel/Prefabs/PF_FPG_EnemyHitFeel.prefab";
        private const string FormalRoomScenePath =
            "Assets/FPGDemo/Scenes/FormalRoom.unity";

        private static readonly string[] EnemyPrefabPaths =
        {
            "Assets/FPGDemo/Presentation/Characters/Enemies/Burstbug/Prefabs/PF_FPG_BurstbugEntity.prefab",
            "Assets/FPGDemo/Presentation/Characters/Enemies/Luan/Prefabs/PF_FPG_LuanEntity.prefab",
            "Assets/FPGDemo/Presentation/Characters/Enemies/Hudie/Prefabs/PF_FPG_HudieEntity.prefab"
        };

        [Test]
        public void SharedEnemyHitPrefabUsesOneSpringFloatEffect()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                SharedPrefabPath);
            Assert.That(prefab, Is.Not.Null, SharedPrefabPath);

            MonoBehaviour feedback = FindComponent(
                prefab,
                "FPG.Demo.Unity.FpgFeelEnemyHitFeedback");
            MonoBehaviour springTarget = FindComponent(
                prefab,
                "FPG.Demo.Unity.FpgFeelRenderScaleSpringTarget");
            MonoBehaviour player = FindComponent(
                prefab,
                "MoreMountains.Feedbacks.MMF_Player");
            Assert.That(feedback, Is.Not.Null);
            Assert.That(springTarget, Is.Not.Null);
            Assert.That(player, Is.Not.Null);

            SerializedObject feedbackData = new SerializedObject(feedback);
            Assert.That(
                feedbackData.FindProperty("hitPlayer").objectReferenceValue,
                Is.SameAs(player));
            Assert.That(
                feedbackData.FindProperty("renderScaleSpring")
                    .objectReferenceValue,
                Is.SameAs(springTarget));
            Assert.That(
                feedbackData.FindProperty("oneShotDuration").floatValue,
                Is.EqualTo(0.06f).Within(0.0001f));
            Assert.That(
                feedbackData.FindProperty("cooldownDuration").floatValue,
                Is.EqualTo(0.06f).Within(0.0001f));

            SerializedObject springData = new SerializedObject(springTarget);
            Assert.That(
                springData.FindProperty("minimumScale").floatValue,
                Is.EqualTo(0.985f).Within(0.0001f));
            Assert.That(
                springData.FindProperty("maximumScale").floatValue,
                Is.EqualTo(1.035f).Within(0.0001f));

            SerializedObject playerData = new SerializedObject(player);
            SerializedProperty feedbacks = playerData.FindProperty(
                "FeedbacksList");
            Assert.That(feedbacks, Is.Not.Null);
            Assert.That(feedbacks.arraySize, Is.EqualTo(1));
            SerializedProperty spring = feedbacks.GetArrayElementAtIndex(0);
            Assert.That(
                spring.managedReferenceFullTypename,
                Does.Contain("MMF_SpringFloat"));
            Assert.That(
                spring.FindPropertyRelative("DeclaredDuration").floatValue,
                Is.EqualTo(0.06f).Within(0.0001f));
            Assert.That(
                spring.FindPropertyRelative("BumpAmount").floatValue,
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(
                spring.FindPropertyRelative("OverrideDamping").boolValue,
                Is.True);
            Assert.That(
                spring.FindPropertyRelative("NewDamping").floatValue,
                Is.EqualTo(0.82f).Within(0.0001f));
            Assert.That(
                spring.FindPropertyRelative("OverrideFrequency").boolValue,
                Is.True);
            Assert.That(
                spring.FindPropertyRelative("NewFrequency").floatValue,
                Is.EqualTo(14f).Within(0.0001f));
        }

        [Test]
        public void EveryFormalEnemyContainsTheSharedFeelPrefab()
        {
            for (int index = 0; index < EnemyPrefabPaths.Length; index++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    EnemyPrefabPaths[index]);
                Assert.That(prefab, Is.Not.Null, EnemyPrefabPaths[index]);
                Transform nested = prefab.transform.Find(
                    "PF_FPG_EnemyHitFeel");
                Assert.That(nested, Is.Not.Null, EnemyPrefabPaths[index]);
                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                        nested.gameObject),
                    Is.EqualTo(SharedPrefabPath),
                    EnemyPrefabPaths[index]);
                Assert.That(
                    FindComponent(
                        nested.gameObject,
                        "FPG.Demo.Unity.FpgFeelEnemyHitFeedback"),
                    Is.Not.Null,
                    EnemyPrefabPaths[index]);
            }
        }

        [Test]
        public void BodyAndWeakpointKindsRouteThroughTheSameLocalEffect()
        {
            Type routerType = FindType(
                "FPG.Demo.Unity.FpgFeelEnemyHitRouter");
            Assert.That(routerType, Is.Not.Null);
            MethodInfo method = routerType.GetMethod(
                "IsEnemyHit",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            Assert.That(
                method.Invoke(
                    null,
                    new object[] { FpgSupplementalFeedbackKind.BodyHit }),
                Is.True);
            Assert.That(
                method.Invoke(
                    null,
                    new object[] { FpgSupplementalFeedbackKind.WeakpointHit }),
                Is.True);
            Assert.That(
                method.Invoke(
                    null,
                    new object[]
                    {
                        FpgSupplementalFeedbackKind.ProjectileIntercept
                    }),
                Is.False);
        }

        [Test]
        public void FormalRoomContainsOnlyTheEnemyHitFeelRouter()
        {
            Scene scene = SceneManager.GetSceneByPath(FormalRoomScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(
                    FormalRoomScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                GameObject root = FindSceneObject(
                    scene,
                    "__FormalRoom/Presentation/FeelFeedbackRoot");
                Assert.That(root, Is.Not.Null);
                Assert.That(root.transform.childCount, Is.Zero);

                MonoBehaviour router = FindComponent(
                    root,
                    "FPG.Demo.Unity.FpgFeelEnemyHitRouter");
                Assert.That(router, Is.Not.Null);
                SerializedObject routerData = new SerializedObject(router);
                Assert.That(
                    routerData.FindProperty("combatFeedbackBridge")
                        .objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    routerData.FindProperty("enemyEntityPool")
                        .objectReferenceValue,
                    Is.Not.Null);

                Assert.That(
                    FindSceneObject(
                        scene,
                        "__FormalRoom/Presentation/FormalPlayerHud/FeelFlashOverlay"),
                    Is.Null);
                Assert.That(
                    HasSceneObjectNamed(scene, "FeelURPVolume"),
                    Is.False);
                Assert.That(
                    CountSceneComponents(
                        scene,
                        "FPG.Demo.Unity.FpgFeelFeedbackAdapter"),
                    Is.Zero);
                Assert.That(
                    CountSceneComponents(
                        scene,
                        "MoreMountains.Feedbacks.MMScaleShaker"),
                    Is.Zero);
                Assert.That(
                    CountSceneComponents(
                        scene,
                        "MoreMountains.Feedbacks.MMFlash"),
                    Is.Zero);
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static MonoBehaviour FindComponent(
            GameObject root,
            string fullTypeName)
        {
            MonoBehaviour[] components =
                root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < components.Length; index++)
            {
                MonoBehaviour component = components[index];
                if (component != null
                    && component.GetType().FullName == fullTypeName)
                {
                    return component;
                }
            }

            return null;
        }

        private static Type FindType(string fullTypeName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullTypeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static int CountSceneComponents(
            Scene scene,
            string fullTypeName)
        {
            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] components = roots[rootIndex]
                    .GetComponentsInChildren<MonoBehaviour>(true);
                for (int componentIndex = 0;
                    componentIndex < components.Length;
                    componentIndex++)
                {
                    MonoBehaviour component = components[componentIndex];
                    if (component != null
                        && component.GetType().FullName == fullTypeName)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool HasSceneObjectNamed(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex]
                    .GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                {
                    if (transforms[index].name == name)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static GameObject FindSceneObject(Scene scene, string path)
        {
            string[] segments = path.Split('/');
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].name != segments[0])
                {
                    continue;
                }

                Transform current = roots[index].transform;
                for (int segment = 1;
                    segment < segments.Length && current != null;
                    segment++)
                {
                    current = current.Find(segments[segment]);
                }

                return current == null ? null : current.gameObject;
            }

            return null;
        }
    }
}
