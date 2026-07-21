using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class ProjectileCollisionProxyPoolTests
    {
        private const int HitboxLayer = 29;

        private readonly List<GameObject> objects = new List<GameObject>();
        private ProjectileCollisionProxyPool pool;

        [TearDown]
        public void TearDown()
        {
            pool?.Dispose();
            pool = null;
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index] != null)
                {
                    Object.DestroyImmediate(objects[index]);
                }
            }

            objects.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void PrewarmsStableGeometryIdsAndRejectsOverflow()
        {
            HitboxRegistry registry = CreateRegistry();
            pool = new ProjectileCollisionProxyPool(2, 1 << HitboxLayer, registry.transform);
            Assert.That(pool.TryPrepare(registry, out string error), Is.True, error);
            Assert.That(pool.Capacity, Is.EqualTo(2));

            ProjectileSpawnRequest first = CreateRequest(101, 1);
            ProjectileSpawnRequest second = CreateRequest(102, 2);
            ProjectileSpawnRequest overflow = CreateRequest(103, 3);
            Assert.That(pool.Acquire(first, CreatePath(first, 0)).IsSuccess, Is.True);
            Assert.That(pool.Acquire(second, CreatePath(second, 10)).IsSuccess, Is.True);
            Assert.That(pool.Acquire(overflow, CreatePath(overflow, 20)).RejectReason,
                Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(pool.ActiveCount, Is.EqualTo(2));

            Assert.That(pool.TryGetActiveProxy(first.RuntimeId, out ProjectileCollisionProxySnapshot firstProxy), Is.True);
            Assert.That(pool.TryGetActiveProxy(second.RuntimeId, out ProjectileCollisionProxySnapshot secondProxy), Is.True);
            Assert.That(firstProxy.GeometryId, Is.EqualTo(new GeometryId(ProjectileCollisionProxyPool.FirstGeometryId)));
            Assert.That(secondProxy.GeometryId,
                Is.EqualTo(new GeometryId(ProjectileCollisionProxyPool.FirstGeometryId + 1)));
            Assert.That(firstProxy.GeometryId, Is.Not.EqualTo(secondProxy.GeometryId));
            Assert.That(firstProxy.Collider.enabled, Is.True);
            Assert.That(secondProxy.Collider.enabled, Is.True);

            Assert.That(pool.Release(first.RuntimeId).IsSuccess, Is.True);
            Assert.That(firstProxy.Collider.enabled, Is.False);
            Assert.That(pool.TryGetActiveProxy(first.RuntimeId, out ProjectileCollisionProxySnapshot ignored), Is.False);
            Assert.That(pool.Release(first.RuntimeId).RejectReason, Is.EqualTo(RejectReason.InvalidTarget));
        }

        [Test]
        public void PrepareRejectsCollisionBudgetThatCouldFillTheAttackQueryBuffer()
        {
            HitboxRegistry registry = CreateRegistry();
            GameObject existingObject = Track(new GameObject("ExistingHitbox"));
            existingObject.layer = HitboxLayer;
            BoxCollider existingCollider = existingObject.AddComponent<BoxCollider>();
            Assert.That(registry.Register(new HitboxBinding(
                existingCollider,
                new RuntimeId(9001),
                QueryTargetKind.Combatant,
                HitPart.Body,
                new GeometryId(9001),
                Team.Enemy)).IsSuccess, Is.True);

            pool = new ProjectileCollisionProxyPool(
                SpatialContract.AttackQueryCandidateCapacity - 1,
                1 << HitboxLayer,
                registry.transform);
            Assert.That(pool.TryPrepare(registry, out string error), Is.False);
            Assert.That(error, Does.Contain("below the attack-query candidate capacity"));
            Assert.That(pool.IsPrepared, Is.False);
        }

        [Test]
        public void ReleaseRebindsTheSameReservedGeometryToTheNextProjectile()
        {
            HitboxRegistry registry = CreateRegistry();
            pool = new ProjectileCollisionProxyPool(1, 1 << HitboxLayer, registry.transform);
            Assert.That(pool.TryPrepare(registry, out string error), Is.True, error);

            ProjectileSpawnRequest first = CreateRequest(101, 1);
            Assert.That(pool.Acquire(first, CreatePath(first, 0)).IsSuccess, Is.True);
            Assert.That(pool.TryGetActiveProxy(first.RuntimeId, out ProjectileCollisionProxySnapshot firstProxy), Is.True);
            Assert.That(pool.Release(first.RuntimeId).IsSuccess, Is.True);

            ProjectileSpawnRequest replacement = CreateRequest(102, 2);
            Assert.That(pool.Acquire(replacement, CreatePath(replacement, 10)).IsSuccess, Is.True);
            Assert.That(pool.TryGetActiveProxy(replacement.RuntimeId, out ProjectileCollisionProxySnapshot replacementProxy), Is.True);
            Assert.That(replacementProxy.GeometryId, Is.EqualTo(firstProxy.GeometryId));
            Assert.That(registry.TryResolve(replacementProxy.GeometryId, out RegisteredHitbox rebound), Is.True);
            Assert.That(rebound.RuntimeId, Is.EqualTo(replacement.RuntimeId));

            pool.ForceReleaseAll();
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(replacementProxy.Collider.enabled, Is.False);
            Assert.That(registry.TryResolve(replacementProxy.GeometryId, out RegisteredHitbox ignored), Is.False);
        }

        [Test]
        public void RejectsNonEnemyOrReservedGeometryConflictsBeforeAProxyCanBeRegistered()
        {
            HitboxRegistry registry = CreateRegistry();
            pool = new ProjectileCollisionProxyPool(1, 1 << HitboxLayer, registry.transform);
            Assert.That(pool.TryPrepare(registry, out string error), Is.True, error);

            ProjectileSpawnRequest playerProjectile = CreateRequest(101, 1, Team.Player);
            Assert.That(pool.Acquire(playerProjectile, CreatePath(playerProjectile, 0)).RejectReason,
                Is.EqualTo(RejectReason.InvalidDefinition));
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(registry.Count, Is.Zero);

            pool.Dispose();
            pool = null;
            GameObject conflictingObject = Track(new GameObject("ReservedGeometryConflict"));
            conflictingObject.layer = HitboxLayer;
            BoxCollider conflictingCollider = conflictingObject.AddComponent<BoxCollider>();
            Assert.That(registry.Register(new HitboxBinding(
                conflictingCollider,
                new RuntimeId(9001),
                QueryTargetKind.Combatant,
                HitPart.Body,
                new GeometryId(ProjectileCollisionProxyPool.FirstGeometryId),
                Team.Enemy)).IsSuccess, Is.True);

            pool = new ProjectileCollisionProxyPool(1, 1 << HitboxLayer, registry.transform);
            Assert.That(pool.TryPrepare(registry, out error), Is.False);
            Assert.That(error, Does.Contain("reserved projectile collision proxy GeometryId"));
            Assert.That(pool.IsPrepared, Is.False);
        }

        private HitboxRegistry CreateRegistry()
        {
            GameObject gameObject = Track(new GameObject("HitboxRegistry"));
            HitboxRegistry registry = gameObject.AddComponent<HitboxRegistry>();
            Assert.That(registry.TryInitialize(out string error), Is.True, error);
            return registry;
        }

        private GameObject Track(GameObject gameObject)
        {
            objects.Add(gameObject);
            return gameObject;
        }

        private static ProjectileSpawnRequest CreateRequest(
            long runtimeId,
            long projectileId,
            Team team = Team.Enemy)
        {
            return new ProjectileSpawnRequest(
                new TickIndex(0),
                new TickIndex(2),
                new ProjectileId(projectileId),
                new RuntimeId(runtimeId),
                new AttackId(projectileId),
                new RuntimeId(2),
                new RuntimeId(1),
                team,
                301,
                250,
                9,
                true);
        }

        private static ProjectilePathSnapshot CreatePath(
            in ProjectileSpawnRequest request,
            int offset)
        {
            return new ProjectilePathSnapshot(
                request.ProjectileId,
                request.RuntimeId,
                request.Tick,
                request.ArrivalTick,
                new SpatialVectorKey(offset, 0, 0),
                new SpatialVectorKey(offset + 1000, 0, 0));
        }
    }
}
