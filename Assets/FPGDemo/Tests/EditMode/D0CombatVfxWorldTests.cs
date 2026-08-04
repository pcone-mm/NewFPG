using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0CombatVfxWorldTests
    {
        [Test]
        public void ScenarioKeysAreRegisteredBeforePrepareWithoutRequiringPrefabAssets()
        {
            GameObject root = new GameObject("D0CombatVfxWorldTestRoot");
            try
            {
                D0CombatVfxWorld world = root.AddComponent<D0CombatVfxWorld>();
                D0CombatVfxAssetReference reference = new D0CombatVfxAssetReference(
                    "test.logical.attack",
                    null,
                    3,
                    0.25f,
                    "animation",
                    0,
                    D0CombatVfxCategory.EnemyAttack);

                Assert.That(world.TryPrepareForScenario(
                    new[] { reference },
                    out string error), Is.True, error);
                Assert.That(world.IsPrepared, Is.True);
                Assert.That(world.PoolCount, Is.EqualTo(1));
                Assert.That(world.PrewarmedInstanceCount, Is.Zero);
                Assert.That(world.TryAcquire(
                    "test.logical.attack",
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one,
                    out _), Is.False);
                Assert.That(
                    world.TryPresent(
                        "test.logical.attack",
                        root.transform,
                        out GameObject logicalInstance),
                    Is.True);
                Assert.That(logicalInstance, Is.Null);
                Assert.That(world.HotPathInstantiateCount, Is.Zero);
                Assert.That(world.HotPathDestroyCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AcquireAndAdvanceReusePrewarmedObjectsWithoutHotPathAllocation()
        {
            GameObject root = new GameObject("D0CombatVfxWorldTestRoot");
            GameObject prefab = new GameObject("D0CombatVfxWorldTestPrefab");
            try
            {
                D0CombatVfxWorld world = root.AddComponent<D0CombatVfxWorld>();
                D0CombatVfxAssetReference reference = new D0CombatVfxAssetReference(
                    "test.concrete.attack",
                    prefab,
                    1,
                    0.1f,
                    "animation",
                    0,
                    D0CombatVfxCategory.EnemyAttack);

                Assert.That(world.TryPrepareForScenario(
                    new[] { reference },
                    out string error), Is.True, error);
                Assert.That(world.PrewarmedInstanceCount, Is.EqualTo(1));
                world.BeginCombat();

                Assert.That(world.TryAcquire(
                    "test.concrete.attack",
                    Vector3.one,
                    Quaternion.identity,
                    Vector3.one,
                    out GameObject instance), Is.True);
                Assert.That(instance, Is.Not.Null);
                Assert.That(instance.activeSelf, Is.True);
                Assert.That(world.ActiveInstanceCount, Is.EqualTo(1));

                world.Advance(0.2f);
                Assert.That(instance.activeSelf, Is.False);
                Assert.That(world.ActiveInstanceCount, Is.Zero);
                Assert.That(world.HotPathInstantiateCount, Is.Zero);
                Assert.That(world.HotPathDestroyCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void AcquireRecyclesOldestTransientInstanceWhenPoolIsFull()
        {
            GameObject root = new GameObject("D0CombatVfxRecycleTestRoot");
            GameObject prefab = new GameObject("D0CombatVfxRecycleTestPrefab");
            try
            {
                D0CombatVfxWorld world = root.AddComponent<D0CombatVfxWorld>();
                D0CombatVfxAssetReference reference =
                    new D0CombatVfxAssetReference(
                        "test.recycled",
                        prefab,
                        1,
                        10f,
                        "presentation",
                        0,
                        D0CombatVfxCategory.SkillPresentation);
                Assert.That(
                    world.TryPrepareForScenario(
                        new[] { reference },
                        out string error),
                    Is.True,
                    error);
                world.BeginCombat();

                Assert.That(
                    world.TryAcquire(
                        "test.recycled",
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one,
                        out GameObject first),
                    Is.True);
                Assert.That(
                    world.TryAcquire(
                        "test.recycled",
                        Vector3.one,
                        Quaternion.identity,
                        Vector3.one,
                        out GameObject second),
                    Is.True);

                Assert.That(second, Is.SameAs(first));
                Assert.That(second.transform.position, Is.EqualTo(Vector3.one));
                Assert.That(world.ActiveInstanceCount, Is.EqualTo(1));
                Assert.That(world.RecycledInstanceCount, Is.EqualTo(1));
                Assert.That(world.AcquireRejectCount, Is.Zero);
                Assert.That(world.HotPathInstantiateCount, Is.Zero);
                Assert.That(world.HotPathDestroyCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void HeldInstancesIgnoreDurationAndReleaseExplicitly()
        {
            GameObject root = new GameObject("D0CombatVfxHeldTestRoot");
            GameObject prefab = new GameObject("D0CombatVfxHeldTestPrefab");
            try
            {
                D0CombatVfxWorld world = root.AddComponent<D0CombatVfxWorld>();
                D0CombatVfxAssetReference reference =
                    new D0CombatVfxAssetReference(
                        "test.flight",
                        prefab,
                        1,
                        0.05f,
                        "presentation",
                        0,
                        D0CombatVfxCategory.SkillPresentation);
                Assert.That(
                    world.TryPrepareForScenario(
                        new[] { reference },
                        out string error),
                    Is.True,
                    error);
                world.BeginCombat();

                Assert.That(
                    world.TryAcquireHeld(
                        "test.flight",
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one,
                        out GameObject instance),
                    Is.True);
                world.Advance(5f);
                Assert.That(instance.activeSelf, Is.True);
                Assert.That(world.ActiveInstanceCount, Is.EqualTo(1));
                Assert.That(
                    world.TryAcquire(
                        "test.flight",
                        Vector3.one,
                        Quaternion.identity,
                        Vector3.one,
                        out _),
                    Is.False);
                Assert.That(world.RecycledInstanceCount, Is.Zero);

                Assert.That(world.TryRelease(instance), Is.True);
                Assert.That(instance.activeSelf, Is.False);
                Assert.That(world.ActiveInstanceCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void GlobalActiveCapacityRejectsWithoutAllocating()
        {
            GameObject root = new GameObject("D0CombatVfxCapacityTestRoot");
            GameObject prefab = new GameObject("D0CombatVfxCapacityTestPrefab");
            try
            {
                D0CombatVfxWorld world = root.AddComponent<D0CombatVfxWorld>();
                Assert.That(
                    world.TrySetGlobalActiveCapacity(1, out string error),
                    Is.True,
                    error);
                Assert.That(
                    world.TryPrepareForScenario(
                        new[]
                        {
                            new D0CombatVfxAssetReference(
                                "test.capacity",
                                prefab,
                                2,
                                1f,
                                "presentation",
                                0,
                                D0CombatVfxCategory.SkillPresentation)
                        },
                        out error),
                    Is.True,
                    error);
                world.BeginCombat();

                Assert.That(
                    world.TryAcquire(
                        "test.capacity",
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one,
                        out GameObject first),
                    Is.True);
                Assert.That(
                    world.TryAcquire(
                        "test.capacity",
                        Vector3.one,
                        Quaternion.identity,
                        Vector3.one,
                        out _),
                    Is.False);
                Assert.That(world.HotPathInstantiateCount, Is.Zero);

                Assert.That(world.TryRelease(first), Is.True);
                Assert.That(
                    world.TryAcquire(
                        "test.capacity",
                        Vector3.one,
                        Quaternion.identity,
                        Vector3.one,
                        out _),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(prefab);
            }
        }

    }
}
