using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0EntitySceneAssetContractTests
    {
        private const string CombatLabScenePath =
            "Assets/FPGDemo/Scenes/CombatLab.unity";

        private const string BootScenePath =
            "Assets/FPGDemo/Scenes/Boot.unity";

        private const string FeiEntityPrefabPath =
            "Assets/FPGDemo/Presentation/Actors/Fei/PF_D0_FeiEntity.prefab";

        private static readonly string[] ActorPrefabFolders =
        {
            "Assets/FPGDemo/Presentation/Actors/",
            "Assets/FPGDemo/Presentation/D0Slice/Spine/",
            "Assets/FPGDemo/Presentation/Luan/Prefabs/",
            "Assets/FPGDemo/Presentation/Hudie/Prefabs/"
        };

        private static readonly string[] GeneratedActorPrefabPaths =
        {
            "Assets/FPGDemo/Presentation/D0Slice/Spine/D0_Fei_30048_StraightAlpha.prefab",
            "Assets/FPGDemo/Presentation/D0Slice/Spine/D0_Burstbug_1001003_StraightAlpha.prefab",
            "Assets/FPGDemo/Presentation/Luan/Prefabs/PF_D0_Luan.prefab",
            "Assets/FPGDemo/Presentation/Hudie/Prefabs/PF_D0_Hudie.prefab"
        };

        private static readonly string[] ForbiddenLegacyObjectNames =
        {
            "PlayerAnchor",
            "EnemyAnchor",
            "D0Actors",
            "LuanHudieShowcase",
            "D0ActorEffects",
            "D0BurstbugCznFx",
            "FastThreatPool",
            "InterceptableVolleyPool",
            "DeathLayerF4Pool",
            "DeathLayerF3Pool",
            "DeathLayerF2Pool",
            "DeathLayerF1Pool"
        };

        private static readonly string[] ForbiddenLegacyComponentNames =
        {
            "D0BurstbugCznFxPresenter",
            "LuanHudiePresentationController"
        };

        [Test]
        public void CombatLabDirectlyInstantiatesOnlyTheCompleteFeiEntity()
        {
            WithPreviewScene(
                CombatLabScenePath,
                scene =>
                {
                    AssertOnlyDirectActorPrefabIsFei(scene);

                    List<D0PlayerEntityView> players =
                        FindComponents<D0PlayerEntityView>(scene);
                    Assert.That(players, Has.Count.EqualTo(1));
                    Assert.That(
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                            players[0].gameObject),
                        Is.EqualTo(FeiEntityPrefabPath));
                    Assert.That(
                        FindComponents<D0EnemyEntityView>(scene),
                        Is.Empty,
                        "CombatLab enemies must be spawned by D0EnemyEntityWorld.");
                });
        }

[Test]
        public void BootContainsOneVisualChoiceAndNoGameplayPlayerEntity()
        {
            WithPreviewScene(
                BootScenePath,
                scene =>
                {
                    Assert.That(
                        FindComponents<D0PlayerEntityView>(scene),
                        Is.Empty,
                        "Boot must not author a gameplay player entity.");

                    List<FpgBootCharacterChoice> choices =
                        FindComponents<FpgBootCharacterChoice>(scene);
                    Assert.That(choices, Has.Count.EqualTo(1));
                    FpgBootCharacterChoice choice = choices[0];
                    Assert.That(choice.Character, Is.Not.Null);
                    Assert.That(choice.PreviewRoot, Is.Not.Null);
                    Assert.That(
                        choice.PreviewRoot.GetComponentInChildren<
                            D0ActorEntityView>(true),
                        Is.Null,
                        "Boot selection preview must remain visual-only.");

                    Transform characterPreview =
                        choice.PreviewRoot.transform.Find("Fei_SelectionPreview");
                    Assert.That(characterPreview, Is.Not.Null);
                    Transform triggerTransform =
                        characterPreview.Find("SelectionTrigger");
                    Assert.That(
                        triggerTransform,
                        Is.Not.Null,
                        "The selection trigger must move with the character preview.");

                    BoxCollider trigger =
                        triggerTransform.GetComponent<BoxCollider>();
                    Assert.That(trigger, Is.Not.Null);
                    Assert.That(trigger.isTrigger, Is.True);
                    Assert.That(choice.OwnsCollider(trigger), Is.True);

                    Assert.That(trigger.size.x, Is.GreaterThan(0f));
                    Assert.That(trigger.size.y, Is.GreaterThan(0f));
                    Assert.That(trigger.size.z, Is.GreaterThan(0f));

                    Collider[] colliders =
                        choice.GetComponentsInChildren<Collider>(true);
                    for (int index = 0; index < colliders.Length; index++)
                    {
                        if (colliders[index].enabled)
                        {
                            Assert.That(
                                colliders[index],
                                Is.SameAs(trigger),
                                "Only the character-mounted trigger may be selectable.");
                        }
                    }
                });
        }

        [Test]
        public void CombatLabContainsNoLegacyActorScaffoldingOrCharacterPools()
        {
            WithPreviewScene(
                CombatLabScenePath,
                scene =>
                {
                    List<GameObject> objects = GetSceneObjects(scene);
                    for (int objectIndex = 0;
                         objectIndex < objects.Count;
                         objectIndex++)
                    {
                        GameObject value = objects[objectIndex];
                        for (int nameIndex = 0;
                             nameIndex < ForbiddenLegacyObjectNames.Length;
                             nameIndex++)
                        {
                            Assert.That(
                                value.name,
                                Is.Not.EqualTo(
                                    ForbiddenLegacyObjectNames[nameIndex]),
                                "Legacy scene object remains at "
                                + GetHierarchyPath(value.transform)
                                + ".");
                        }

                        Component[] components = value.GetComponents<Component>();
                        for (int componentIndex = 0;
                             componentIndex < components.Length;
                             componentIndex++)
                        {
                            Component component = components[componentIndex];
                            if (component == null)
                            {
                                continue;
                            }

                            string componentName = component.GetType().Name;
                            for (int typeIndex = 0;
                                 typeIndex < ForbiddenLegacyComponentNames.Length;
                                 typeIndex++)
                            {
                                Assert.That(
                                    componentName,
                                    Is.Not.EqualTo(
                                        ForbiddenLegacyComponentNames[typeIndex]),
                                    "Legacy component remains at "
                                    + GetHierarchyPath(value.transform)
                                    + ".");
                            }
                        }
                    }

                    List<Actor2DPresenter> presenters =
                        FindComponents<Actor2DPresenter>(scene);
                    Assert.That(presenters, Is.Not.Empty);
                    for (int index = 0; index < presenters.Count; index++)
                    {
                        Assert.That(
                            presenters[index].GetComponentInParent<D0ActorEntityView>(
                                true),
                            Is.Not.Null,
                            "Actor2DPresenter must be owned by an Entity Prefab: "
                            + GetHierarchyPath(presenters[index].transform));
                    }
                });
        }

        [Test]
        public void CombatLabLeavesActorSkillAndThreeCContentForRuntimeInjection()
        {
            WithPreviewScene(
                CombatLabScenePath,
                scene =>
                {
                    PlayerWeaponPresentationController weapon =
                        FindComponents<PlayerWeaponPresentationController>(scene)[0];
                    Assert.That(weapon.SessionHost, Is.Null);
                    Assert.That(weapon.PlayerEntity, Is.Null);
                    Assert.That(weapon.WeaponDefinition, Is.Null);
                    Assert.That(weapon.PresentationCamera, Is.Null);
                    Assert.That(weapon.ActorPresenter, Is.Null);
                    Assert.That(weapon.SocketRegistry, Is.Null);

                    D0EnemyBehaviorController behavior =
                        FindComponents<D0EnemyBehaviorController>(scene)[0];
                    Assert.That(behavior.SessionHost, Is.Null);
                    Assert.That(behavior.BehaviorProfile, Is.Null);
                    Assert.That(behavior.Encounter, Is.Null);
                    Assert.That(behavior.VisualRoot, Is.Null);
                    Assert.That(behavior.GameplayAnchor, Is.Null);
                    Assert.That(behavior.AnimationMotionSource, Is.Null);

                    SerializedObject serializedBehavior =
                        new SerializedObject(behavior);
                    Assert.That(
                        serializedBehavior.FindProperty("summonAnimationMotionSkill"),
                        Is.Null,
                        "Summon presentation is runtime-injected and must not be serialized.");

                    D0ShotCameraFeedbackController cameraFeedback =
                        FindComponents<D0ShotCameraFeedbackController>(scene)[0];
                    Assert.That(cameraFeedback.ThreeCProfile, Is.Null);

                    CombatAimReticle reticle =
                        FindComponents<CombatAimReticle>(scene)[0];
                    Assert.That(reticle.ThreeCProfile, Is.Null);
                });
        }

        [Test]
        public void FeiEntityPrefabOwnsExactlyOneBarrierController()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(FeiEntityPrefabPath);
            Assert.That(prefab, Is.Not.Null, FeiEntityPrefabPath);

            D0PlayerEntityView entity =
                prefab.GetComponent<D0PlayerEntityView>();
            Assert.That(entity, Is.Not.Null, FeiEntityPrefabPath);

            D0PlayerBarrierPresentationController[] barriers =
                prefab.GetComponentsInChildren<
                    D0PlayerBarrierPresentationController>(true);
            Assert.That(barriers, Has.Length.EqualTo(1));
            Assert.That(entity.Barrier, Is.SameAs(barriers[0]));
        }

        [Test]
        public void CombatLabStaticHitboxBindingsContainEnvironmentOnly()
        {
            WithPreviewScene(
                CombatLabScenePath,
                scene =>
                {
                    List<HitboxRegistry> registries =
                        FindComponents<HitboxRegistry>(scene);
                    Assert.That(registries, Has.Count.EqualTo(1));

                    SerializedObject serializedRegistry =
                        new SerializedObject(registries[0]);
                    SerializedProperty bindings =
                        serializedRegistry.FindProperty("staticBindings");
                    Assert.That(bindings, Is.Not.Null);
                    Assert.That(bindings.isArray, Is.True);
                    Assert.That(
                        bindings.arraySize,
                        Is.GreaterThan(0),
                        "CombatLab must retain authored environment blockers.");

                    for (int index = 0; index < bindings.arraySize; index++)
                    {
                        SerializedProperty binding =
                            bindings.GetArrayElementAtIndex(index);
                        SerializedProperty enabled =
                            binding.FindPropertyRelative("enabled");
                        SerializedProperty collider =
                            binding.FindPropertyRelative("collider");
                        SerializedProperty targetReference =
                            binding.FindPropertyRelative("targetReference");
                        SerializedProperty targetKind =
                            binding.FindPropertyRelative("targetKind");

                        Assert.That(enabled, Is.Not.Null);
                        Assert.That(enabled.boolValue, Is.True);
                        Assert.That(collider, Is.Not.Null);
                        Assert.That(
                            collider.objectReferenceValue,
                            Is.InstanceOf<Collider>());
                        Assert.That(targetReference, Is.Not.Null);
                        Assert.That(
                            targetReference.enumValueIndex,
                            Is.EqualTo(
                                (int)HitboxTargetReference.Environment),
                            "Static player/enemy hitboxes must come from active Entity Prefabs.");
                        Assert.That(targetKind, Is.Not.Null);
                        Assert.That(
                            targetKind.enumValueIndex,
                            Is.EqualTo(
                                (int)QueryTargetKind.EnvironmentBlocker));
                    }
                });
        }

        private static void AssertOnlyDirectActorPrefabIsFei(Scene scene)
        {
            List<string> prefabPaths = GetOutermostPrefabInstancePaths(scene);
            List<string> actorPrefabPaths = new List<string>();
            for (int index = 0; index < prefabPaths.Count; index++)
            {
                string path = prefabPaths[index];
                if (IsActorOwnedPrefabPath(path))
                {
                    actorPrefabPaths.Add(path);
                }
            }

            Assert.That(
                actorPrefabPaths,
                Is.EqualTo(new[] { FeiEntityPrefabPath }),
                "A scene may directly instantiate only the complete Fei Entity Prefab.");

            for (int index = 0;
                 index < GeneratedActorPrefabPaths.Length;
                 index++)
            {
                Assert.That(
                    prefabPaths,
                    Does.Not.Contain(GeneratedActorPrefabPaths[index]),
                    "Generated actor prefabs are internal Entity Prefab dependencies.");
            }
        }

        private static bool IsActorOwnedPrefabPath(string path)
        {
            for (int index = 0; index < ActorPrefabFolders.Length; index++)
            {
                if (path.StartsWith(
                        ActorPrefabFolders[index],
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> GetOutermostPrefabInstancePaths(
            Scene scene)
        {
            List<GameObject> objects = GetSceneObjects(scene);
            HashSet<GameObject> instanceRoots = new HashSet<GameObject>();
            for (int index = 0; index < objects.Count; index++)
            {
                GameObject root =
                    PrefabUtility.GetOutermostPrefabInstanceRoot(
                        objects[index]);
                if (root != null && root.scene == scene)
                {
                    instanceRoots.Add(root);
                }
            }

            List<string> paths = new List<string>();
            foreach (GameObject root in instanceRoots)
            {
                string path =
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                        root);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path.Replace('\\', '/'));
                }
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        private static List<T> FindComponents<T>(Scene scene)
            where T : Component
        {
            List<GameObject> objects = GetSceneObjects(scene);
            List<T> components = new List<T>();
            for (int index = 0; index < objects.Count; index++)
            {
                components.AddRange(objects[index].GetComponents<T>());
            }

            return components;
        }

        private static List<GameObject> GetSceneObjects(Scene scene)
        {
            List<GameObject> values = new List<GameObject>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms =
                    roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0;
                     transformIndex < transforms.Length;
                     transformIndex++)
                {
                    values.Add(transforms[transformIndex].gameObject);
                }
            }

            return values;
        }

        private static string GetHierarchyPath(Transform value)
        {
            string path = value.name;
            while (value.parent != null)
            {
                value = value.parent;
                path = value.name + "/" + path;
            }

            return path;
        }

        private static void WithPreviewScene(
            string scenePath,
            Action<Scene> assertion)
        {
            SceneAsset sceneAsset =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            Assert.That(sceneAsset, Is.Not.Null, scenePath);

            Scene scene = EditorSceneManager.OpenPreviewScene(scenePath);
            Assert.That(scene.IsValid(), Is.True, scenePath);
            try
            {
                assertion(scene);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
