using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class UnityProjectileWorldPortTests
    {
        private const int HitboxLayer = 29;
        private const int BlockerLayer = 28;
        private const int SweepRadiusKey = 250;

        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(objects[index]);
                }
            }

            objects.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void RegisterRequiresBoundEndpointsAndFreezesTheirQuantizedPositions()
        {
            HitboxRegistry registry = CreateRegistry();
            Transform playerAnchor = CreateAnchor("PlayerAnchor", new Vector3(1.25f, 2f, 3.5f));
            Transform enemyAnchor = CreateAnchor("EnemyAnchor", new Vector3(-1f, 0.5f, -2.25f));
            FakePhysicsBackend physics = new FakePhysicsBackend();
            UnityProjectileWorldPort port = CreatePort(
                registry,
                playerAnchor,
                enemyAnchor,
                physics,
                2);
            ProjectileSpawnRequest request = CreateSpawnRequest(
                projectileId: 1,
                runtimeId: 101,
                playerId: 10,
                enemyId: 20);

            Assert.That(port.Register(request, out ProjectilePathSnapshot unboundPath).RejectReason,
                Is.EqualTo(RejectReason.InvalidState));
            Assert.That(unboundPath.ProjectileId.IsValid, Is.False);
            Assert.That(port.BindSession(new RuntimeId(10), new RuntimeId(20), out string error),
                Is.True,
                error);

            Assert.That(port.Register(request, out ProjectilePathSnapshot path).IsSuccess, Is.True);
            Assert.That(path.Start, Is.EqualTo(new SpatialVectorKey(-1000, 500, -2250)));
            Assert.That(path.End, Is.EqualTo(new SpatialVectorKey(1250, 2000, 3500)));
            Assert.That(path.Matches(request), Is.True);
            Assert.That(port.ActiveCount, Is.EqualTo(1));

            playerAnchor.position = new Vector3(100f, 100f, 100f);
            enemyAnchor.position = new Vector3(-100f, -100f, -100f);
            Assert.That(path.Start, Is.EqualTo(new SpatialVectorKey(-1000, 500, -2250)));
            Assert.That(path.End, Is.EqualTo(new SpatialVectorKey(1250, 2000, 3500)));
            Assert.That(port.Register(request, out ProjectilePathSnapshot duplicate).RejectReason,
                Is.EqualTo(RejectReason.InvalidState));
            Assert.That(duplicate.ProjectileId.IsValid, Is.False);
            Assert.That(port.BindSession(new RuntimeId(10), new RuntimeId(20)), Is.True);
            Assert.That(port.BindSession(new RuntimeId(11), new RuntimeId(21), out error), Is.False);
            Assert.That(error, Does.Contain("reset"));
        }

        [Test]
        public void EnemyProjectileSamplesSpawnSocketAndFrozenPathSurvivesAnchorDestruction()
        {
            HitboxRegistry registry = CreateRegistry();
            Transform playerAnchor = CreateAnchor(
                "PlayerAnchor",
                new Vector3(0f, 1f, 20f));
            Transform enemyAnchor = CreateAnchor(
                "EnemyAnchor",
                new Vector3(10f, 0f, 0f));
            Transform enemySpawnAnchor = CreateAnchor(
                "EnemyProjectileSpawnAnchor",
                Vector3.zero);
            enemySpawnAnchor.SetParent(enemyAnchor, false);
            enemySpawnAnchor.localPosition = new Vector3(2f, 3f, 4f);
            FakePhysicsBackend physics = new FakePhysicsBackend();
            UnityProjectileWorldPort port = new UnityProjectileWorldPort(
                registry,
                playerAnchor,
                enemyAnchor,
                enemySpawnAnchor,
                new UnityProjectileWorldSettings(
                    1 << HitboxLayer,
                    1 << BlockerLayer),
                1,
                physics);
            Assert.That(port.BindSession(new RuntimeId(10), new RuntimeId(20)), Is.True);

            ProjectileSpawnRequest request = CreateSpawnRequest(1, 101, 10, 20);
            Assert.That(port.Register(request, out ProjectilePathSnapshot path).IsSuccess, Is.True);
            Assert.That(path.Start, Is.EqualTo(new SpatialVectorKey(12000, 3000, 4000)));
            Assert.That(path.End, Is.EqualTo(new SpatialVectorKey(0, 1000, 20000)));

            UnityEngine.Object.DestroyImmediate(enemyAnchor.gameObject);
            DomainResult swept = port.Sweep(
                CreateSweep(path, new TickIndex(1), SweepRadiusKey),
                out ProjectileSweepHit hit);

            Assert.That(swept.IsSuccess, Is.True);
            Assert.That(hit.Kind, Is.EqualTo(ProjectileSweepHitKind.None));
            Assert.That(physics.SphereCastCallCount, Is.EqualTo(1));
            Assert.That(path.Start, Is.EqualTo(new SpatialVectorKey(12000, 3000, 4000)));
        }

        [Test]
        public void RegisterRejectsAnchorsThatCollapseToTheSameQuantizedPoint()
        {
            HitboxRegistry registry = CreateRegistry();
            Transform playerAnchor = CreateAnchor("PlayerAnchor", new Vector3(1f, 2f, 3f));
            Transform enemyAnchor = CreateAnchor("EnemyAnchor", new Vector3(1.0004f, 2f, 3f));
            UnityProjectileWorldPort port = CreatePort(
                registry,
                playerAnchor,
                enemyAnchor,
                new FakePhysicsBackend(),
                1);
            Assert.That(port.BindSession(new RuntimeId(1), new RuntimeId(2)), Is.True);

            DomainResult result = port.Register(
                CreateSpawnRequest(1, 101, 1, 2),
                out ProjectilePathSnapshot ignored);

            Assert.That(result.RejectReason, Is.EqualTo(RejectReason.InvalidState));
            Assert.That(ignored.ProjectileId.IsValid, Is.False);
            Assert.That(port.ActiveCount, Is.Zero);
        }

        [Test]
        public void ConstructorRejectsCollisionPoolWithDifferentCapacityOrHitboxLayer()
        {
            HitboxRegistry registry = CreateRegistry();
            Transform playerAnchor = CreateAnchor("PlayerAnchor", new Vector3(0f, 0f, 2f));
            Transform enemyAnchor = CreateAnchor("EnemyAnchor", Vector3.zero);

            using (ProjectileCollisionProxyPool capacityMismatch = new ProjectileCollisionProxyPool(
                1,
                1 << HitboxLayer,
                registry.transform))
            {
                Assert.That(() => CreatePort(
                        registry,
                        playerAnchor,
                        enemyAnchor,
                        new FakePhysicsBackend(),
                        2,
                        capacityMismatch),
                    Throws.TypeOf<ArgumentException>());
            }

            using (ProjectileCollisionProxyPool layerMismatch = new ProjectileCollisionProxyPool(
                2,
                1 << BlockerLayer,
                registry.transform))
            {
                Assert.That(() => CreatePort(
                        registry,
                        playerAnchor,
                        enemyAnchor,
                        new FakePhysicsBackend(),
                        2,
                        layerMismatch),
                    Throws.TypeOf<ArgumentException>());
            }
        }

        [Test]
        public void ResetClearsOldSlotsAndReleaseRejectsRepeatedTerminalRequests()
        {
            HitboxRegistry registry = CreateRegistry();
            Transform playerAnchor = CreateAnchor("PlayerAnchor", new Vector3(0f, 0f, 2f));
            Transform enemyAnchor = CreateAnchor("EnemyAnchor", Vector3.zero);
            UnityProjectileWorldPort port = CreatePort(
                registry,
                playerAnchor,
                enemyAnchor,
                new FakePhysicsBackend(),
                1);
            Assert.That(port.ResetForSession(new RuntimeId(1), new RuntimeId(2)), Is.True);

            ProjectileSpawnRequest first = CreateSpawnRequest(1, 101, 1, 2);
            ProjectileSpawnRequest second = CreateSpawnRequest(2, 102, 1, 2);
            Assert.That(port.Register(first, out ProjectilePathSnapshot firstPath).IsSuccess, Is.True);
            Assert.That(port.Register(second, out ProjectilePathSnapshot ignored).RejectReason,
                Is.EqualTo(RejectReason.BufferCapacity));

            ProjectileReleaseRequest release = new ProjectileReleaseRequest(
                first.Tick,
                first.ProjectileId,
                first.RuntimeId,
                ProjectileTerminalReason.OwnerCanceled);
            Assert.That(port.Release(release).IsSuccess, Is.True);
            Assert.That(port.Release(release).RejectReason, Is.EqualTo(RejectReason.AlreadyTerminal));
            Assert.That(port.ActiveCount, Is.Zero);
            Assert.That(port.Register(second, out ProjectilePathSnapshot secondPath).IsSuccess, Is.True);
            Assert.That(secondPath.ProjectileId, Is.EqualTo(second.ProjectileId));
            Assert.That(port.ResetForSession(
                new RuntimeId(11),
                new RuntimeId(22),
                out string activeResetError), Is.False);
            Assert.That(activeResetError, Does.Contain("released"));
            Assert.That(port.ActiveCount, Is.EqualTo(1));
            Assert.That(port.PlayerRuntimeId, Is.EqualTo(new RuntimeId(1)));
            Assert.That(port.EnemyRuntimeId, Is.EqualTo(new RuntimeId(2)));
            Assert.That(port.Release(new ProjectileReleaseRequest(
                second.Tick,
                second.ProjectileId,
                second.RuntimeId,
                ProjectileTerminalReason.SessionEnded)).IsSuccess, Is.True);

            Assert.That(port.ResetForSession(
                new RuntimeId(11),
                new RuntimeId(22),
                out string resetError), Is.True, resetError);
            Assert.That(port.ActiveCount, Is.Zero);
            Assert.That(port.PlayerRuntimeId, Is.EqualTo(new RuntimeId(11)));
            Assert.That(port.EnemyRuntimeId, Is.EqualTo(new RuntimeId(22)));
            Assert.That(port.Release(new ProjectileReleaseRequest(
                second.Tick,
                second.ProjectileId,
                second.RuntimeId,
                ProjectileTerminalReason.SessionEnded)).RejectReason,
                Is.EqualTo(RejectReason.InvalidTarget));

            ProjectileSpawnRequest rebound = CreateSpawnRequest(3, 103, 11, 22);
            Assert.That(port.Register(rebound, out ignored).IsSuccess, Is.True);
            Assert.That(port.ResetForSession(RuntimeId.Invalid, new RuntimeId(22), out resetError), Is.False);
            Assert.That(port.ActiveCount, Is.EqualTo(1));
            Assert.That(port.IsSessionBound, Is.True);
            Assert.That(port.PlayerRuntimeId, Is.EqualTo(new RuntimeId(11)));
            Assert.That(port.EnemyRuntimeId, Is.EqualTo(new RuntimeId(22)));
            Assert.That(port.Release(new ProjectileReleaseRequest(
                rebound.Tick,
                rebound.ProjectileId,
                rebound.RuntimeId,
                ProjectileTerminalReason.SessionEnded)).IsSuccess, Is.True);
        }

        [Test]
        public void SweepAcceptsOnlyFrozenTargetOrBlockerAndCanonicalizesEqualDistanceHits()
        {
            HitboxRegistry registry = CreateRegistry();
            Transform playerAnchor = CreateAnchor("PlayerAnchor", new Vector3(0f, 0f, 2f));
            Transform enemyAnchor = CreateAnchor("EnemyAnchor", Vector3.zero);

            BoxCollider target = CreateCollider("Target", HitboxLayer);
            BoxCollider owner = CreateCollider("Owner", HitboxLayer);
            BoxCollider other = CreateCollider("Other", HitboxLayer);
            BoxCollider blockerHighGeometry = CreateCollider("BlockerHigh", BlockerLayer);
            BoxCollider blockerLowGeometry = CreateCollider("BlockerLow", BlockerLayer);
            BoxCollider wrongLayer = CreateCollider("WrongLayer", BlockerLayer);
            BoxCollider disallowedTrigger = CreateCollider("DisallowedTrigger", HitboxLayer, true);

            Assert.That(registry.Register(CombatantBinding(
                target,
                1,
                1,
                Team.Player,
                HitPart.Weakpoint)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(owner, 2, 2, Team.Enemy)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(other, 3, 3, Team.Player)).IsSuccess, Is.True);
            Assert.That(registry.Register(BlockerBinding(blockerHighGeometry, 30)).IsSuccess, Is.True);
            Assert.That(registry.Register(BlockerBinding(blockerLowGeometry, 20)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(wrongLayer, 1, 4, Team.Player)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(
                disallowedTrigger,
                1,
                5,
                Team.Player)).IsSuccess, Is.True);

            FakePhysicsBackend physics = new FakePhysicsBackend
            {
                SphereCastHits = new[]
                {
                    Hit(owner, 0.1f),
                    Hit(other, 0.2f),
                    Hit(target, 0.5f),
                    Hit(blockerHighGeometry, 0.5f),
                    Hit(wrongLayer, 0.15f),
                    Hit(disallowedTrigger, 0.12f),
                    Hit(blockerLowGeometry, 0.5f)
                }
            };
            UnityProjectileWorldPort port = CreatePort(
                registry,
                playerAnchor,
                enemyAnchor,
                physics,
                2);
            Assert.That(port.BindSession(new RuntimeId(1), new RuntimeId(2)), Is.True);
            ProjectileSpawnRequest spawn = CreateSpawnRequest(1, 101, 1, 2);
            Assert.That(port.Register(spawn, out ProjectilePathSnapshot path).IsSuccess, Is.True);
            ProjectileSweepRequest sweep = CreateSweep(path, new TickIndex(1), spawn.SweepRadiusKey);

            DomainResult swept = port.Sweep(sweep, out ProjectileSweepHit hit);

            Assert.That(swept.IsSuccess, Is.True);
            Assert.That(hit.Kind, Is.EqualTo(ProjectileSweepHitKind.EnvironmentBlocked));
            Assert.That(hit.GeometryId, Is.EqualTo(new GeometryId(20)));
            Assert.That(hit.DistanceKey, Is.EqualTo(500));
            Assert.That(hit.TargetId.IsValid, Is.False);
            Assert.That(physics.SphereCastCallCount, Is.EqualTo(1));
            Assert.That(physics.LastOrigin, Is.EqualTo(Vector3.zero));
            Assert.That(physics.LastDirection, Is.EqualTo(Vector3.forward));
            Assert.That(physics.LastRadius, Is.EqualTo(0.25f).Within(0.000001f));
            Assert.That(physics.LastMaxDistance, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(physics.LastLayerMask, Is.EqualTo((1 << HitboxLayer) | (1 << BlockerLayer)));
            Assert.That(physics.LastTriggerInteraction, Is.EqualTo(QueryTriggerInteraction.Collide));
        }

        [Test]
        public void SweepReturnsFrozenTargetAndRejectsCallerSuppliedPathChanges()
        {
            HitboxRegistry registry = CreateRegistry();
            Transform playerAnchor = CreateAnchor("PlayerAnchor", new Vector3(0f, 0f, 2f));
            Transform enemyAnchor = CreateAnchor("EnemyAnchor", Vector3.zero);
            BoxCollider target = CreateCollider("Target", HitboxLayer);
            BoxCollider owner = CreateCollider("Owner", HitboxLayer);
            Assert.That(registry.Register(CombatantBinding(
                target,
                1,
                10,
                Team.Player,
                HitPart.Weakpoint)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(owner, 2, 11, Team.Enemy)).IsSuccess, Is.True);

            FakePhysicsBackend physics = new FakePhysicsBackend
            {
                SphereCastHits = new[] { Hit(owner, 0.1f), Hit(target, 0.4f) }
            };
            UnityProjectileWorldPort port = CreatePort(
                registry,
                playerAnchor,
                enemyAnchor,
                physics,
                2);
            Assert.That(port.BindSession(new RuntimeId(1), new RuntimeId(2)), Is.True);
            ProjectileSpawnRequest spawn = CreateSpawnRequest(1, 101, 1, 2);
            Assert.That(port.Register(spawn, out ProjectilePathSnapshot path).IsSuccess, Is.True);
            ProjectileSweepRequest sweep = CreateSweep(path, new TickIndex(1), spawn.SweepRadiusKey);

            Assert.That(port.Sweep(sweep, out ProjectileSweepHit hit).IsSuccess, Is.True);
            Assert.That(hit.Kind, Is.EqualTo(ProjectileSweepHitKind.Target));
            Assert.That(hit.TargetId, Is.EqualTo(new RuntimeId(1)));
            Assert.That(hit.HitPart, Is.EqualTo(HitPart.Weakpoint));
            Assert.That(hit.GeometryId, Is.EqualTo(new GeometryId(10)));
            Assert.That(hit.DistanceKey, Is.EqualTo(400));

            ProjectileSweepRequest spoofed = new ProjectileSweepRequest(
                sweep.Tick,
                sweep.ProjectileId,
                sweep.RuntimeId,
                sweep.From,
                new SpatialVectorKey(sweep.To.X, sweep.To.Y, sweep.To.Z + 1),
                sweep.SweepRadiusKey);
            Assert.That(port.Sweep(spoofed, out ProjectileSweepHit rejected).RejectReason,
                Is.EqualTo(RejectReason.InvalidState));
            Assert.That(rejected.Kind, Is.EqualTo(ProjectileSweepHitKind.None));
            Assert.That(physics.SphereCastCallCount, Is.EqualTo(1),
                "A non-frozen segment must be rejected before querying Physics.");
        }

        [Test]
        public void FullOrPotentiallyTruncatedSphereCastRejectsTheWholeSweep()
        {
            HitboxRegistry registry = CreateRegistry();
            Transform playerAnchor = CreateAnchor("PlayerAnchor", new Vector3(0f, 0f, 2f));
            Transform enemyAnchor = CreateAnchor("EnemyAnchor", Vector3.zero);
            FakePhysicsBackend physics = new FakePhysicsBackend();
            UnityProjectileWorldPort port = CreatePort(
                registry,
                playerAnchor,
                enemyAnchor,
                physics,
                1);
            Assert.That(port.BindSession(new RuntimeId(1), new RuntimeId(2)), Is.True);
            ProjectileSpawnRequest spawn = CreateSpawnRequest(1, 101, 1, 2);
            Assert.That(port.Register(spawn, out ProjectilePathSnapshot path).IsSuccess, Is.True);
            ProjectileSweepRequest sweep = CreateSweep(path, new TickIndex(1), spawn.SweepRadiusKey);

            physics.ForcedSphereCastResult = new NonAllocPhysicsQueryResult(
                SpatialContract.AttackQueryCandidateCapacity - 1,
                false);
            Assert.That(port.Sweep(sweep, out ProjectileSweepHit accepted).IsSuccess, Is.True);
            Assert.That(accepted.Kind, Is.EqualTo(ProjectileSweepHitKind.None));

            physics.ForcedSphereCastResult = new NonAllocPhysicsQueryResult(
                SpatialContract.AttackQueryCandidateCapacity,
                false);
            Assert.That(port.Sweep(sweep, out ProjectileSweepHit full).RejectReason,
                Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(full.Kind, Is.EqualTo(ProjectileSweepHitKind.None));

            physics.ForcedSphereCastResult = new NonAllocPhysicsQueryResult(1, true);
            Assert.That(port.Sweep(sweep, out ProjectileSweepHit truncated).RejectReason,
                Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(truncated.Kind, Is.EqualTo(ProjectileSweepHitKind.None));
        }

        [Test]
        public void InterceptableProjectileUsesCollisionProxyAndReleasesItExactlyOnce()
        {
            HitboxRegistry registry = CreateRegistry();
            Transform playerAnchor = CreateAnchor("PlayerAnchor", new Vector3(0f, 0f, 2f));
            Transform enemyAnchor = CreateAnchor("EnemyAnchor", Vector3.zero);
            ProjectileCollisionProxyPool pool = new ProjectileCollisionProxyPool(
                2,
                1 << HitboxLayer,
                registry.transform);
            FakePhysicsBackend physics = new FakePhysicsBackend();
            UnityProjectileWorldPort port = CreatePort(
                registry,
                playerAnchor,
                enemyAnchor,
                physics,
                2,
                pool);
            Assert.That(port.BindSession(new RuntimeId(1), new RuntimeId(2)), Is.True);

            ProjectileSpawnRequest nonInterceptable = CreateSpawnRequest(1, 101, 1, 2);
            Assert.That(port.Register(nonInterceptable, out ProjectilePathSnapshot ignored).IsSuccess, Is.True);
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(port.Release(new ProjectileReleaseRequest(
                nonInterceptable.Tick,
                nonInterceptable.ProjectileId,
                nonInterceptable.RuntimeId,
                ProjectileTerminalReason.OwnerCanceled)).IsSuccess, Is.True);

            ProjectileSpawnRequest interceptable = CreateSpawnRequest(2, 102, 1, 2, true);
            Assert.That(port.Register(interceptable, out ProjectilePathSnapshot path).IsSuccess, Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.TryGetActiveProxy(interceptable.RuntimeId, out ProjectileCollisionProxySnapshot proxy), Is.True);
            Assert.That(proxy.GeometryId, Is.EqualTo(new GeometryId(ProjectileCollisionProxyPool.FirstGeometryId)));
            Assert.That(proxy.Position, Is.EqualTo(path.Start));
            Assert.That(proxy.Collider.radius, Is.EqualTo(SweepRadiusKey / (float)SpatialContract.DistanceUnitsPerMeter));
            Assert.That(proxy.Collider.GetComponent<Renderer>(), Is.Null);
            Assert.That(proxy.Collider.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(registry.TryResolve(proxy.GeometryId, out RegisteredHitbox registered), Is.True);
            Assert.That(registered.RuntimeId, Is.EqualTo(interceptable.RuntimeId));
            Assert.That(registered.TargetKind, Is.EqualTo(QueryTargetKind.Projectile));
            Assert.That(registered.HitPart, Is.EqualTo(HitPart.Projectile));
            Assert.That(registered.Team, Is.EqualTo(Team.Enemy));

            ProjectileSweepRequest sweep = CreateSweep(path, new TickIndex(1), SweepRadiusKey);
            Assert.That(port.Sweep(sweep, out ProjectileSweepHit hit).IsSuccess, Is.True);
            Assert.That(hit.Kind, Is.EqualTo(ProjectileSweepHitKind.None));
            Assert.That(pool.TryGetActiveProxy(interceptable.RuntimeId, out proxy), Is.True);
            Assert.That(proxy.Position, Is.EqualTo(sweep.To));

            ProjectileReleaseRequest release = new ProjectileReleaseRequest(
                new TickIndex(1),
                interceptable.ProjectileId,
                interceptable.RuntimeId,
                ProjectileTerminalReason.OwnerCanceled);
            Assert.That(port.Release(release).IsSuccess, Is.True);
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(proxy.Collider.enabled, Is.False);
            Assert.That(registry.TryResolve(proxy.GeometryId, out RegisteredHitbox ignoredRegistered), Is.False);
            Assert.That(port.Release(release).RejectReason, Is.EqualTo(RejectReason.AlreadyTerminal));
        }

        private HitboxRegistry CreateRegistry()
        {
            GameObject gameObject = Track(new GameObject("HitboxRegistry"));
            HitboxRegistry registry = gameObject.AddComponent<HitboxRegistry>();
            Assert.That(registry.TryInitialize(out string error), Is.True, error);
            return registry;
        }

        private Transform CreateAnchor(string name, Vector3 position)
        {
            GameObject gameObject = Track(new GameObject(name));
            gameObject.transform.position = position;
            return gameObject.transform;
        }

        private BoxCollider CreateCollider(string name, int layer, bool isTrigger = false)
        {
            GameObject gameObject = Track(new GameObject(name));
            gameObject.layer = layer;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = isTrigger;
            return collider;
        }

        private GameObject Track(GameObject gameObject)
        {
            objects.Add(gameObject);
            return gameObject;
        }

        private static UnityProjectileWorldPort CreatePort(
            HitboxRegistry registry,
            Transform playerAnchor,
            Transform enemyAnchor,
            IUnityPhysicsQueryBackend physics,
            int capacity,
            ProjectileCollisionProxyPool collisionProxyPool = null)
        {
            return new UnityProjectileWorldPort(
                registry,
                playerAnchor,
                enemyAnchor,
                new UnityProjectileWorldSettings(
                    1 << HitboxLayer,
                    1 << BlockerLayer),
                capacity,
                physics,
                collisionProxyPool);
        }

        private static ProjectileSpawnRequest CreateSpawnRequest(
            long projectileId,
            long runtimeId,
            long playerId,
            long enemyId,
            bool interceptable = false)
        {
            return new ProjectileSpawnRequest(
                new TickIndex(0),
                new TickIndex(2),
                new ProjectileId(projectileId),
                new RuntimeId(runtimeId),
                new AttackId(projectileId),
                new RuntimeId(enemyId),
                new RuntimeId(playerId),
                Team.Enemy,
                301,
                SweepRadiusKey,
                9,
                interceptable);
        }

        private static ProjectileSweepRequest CreateSweep(
            in ProjectilePathSnapshot path,
            TickIndex tick,
            int sweepRadiusKey)
        {
            Assert.That(path.TryGetSegment(
                tick,
                out SpatialVectorKey from,
                out SpatialVectorKey to).IsSuccess, Is.True);
            return new ProjectileSweepRequest(
                tick,
                path.ProjectileId,
                path.RuntimeId,
                from,
                to,
                sweepRadiusKey);
        }

        private static HitboxBinding CombatantBinding(
            Collider collider,
            long runtimeId,
            int geometryId,
            Team team,
            HitPart hitPart = HitPart.Body,
            bool allowTrigger = false)
        {
            return new HitboxBinding(
                collider,
                new RuntimeId(runtimeId),
                QueryTargetKind.Combatant,
                hitPart,
                new GeometryId(geometryId),
                team,
                allowTrigger);
        }

        private static HitboxBinding BlockerBinding(Collider collider, int geometryId)
        {
            return new HitboxBinding(
                collider,
                RuntimeId.Invalid,
                QueryTargetKind.EnvironmentBlocker,
                HitPart.Body,
                new GeometryId(geometryId),
                Team.Neutral);
        }

        private static UnityPhysicsHit Hit(Collider collider, float distance)
        {
            return new UnityPhysicsHit(
                collider,
                new Vector3(0f, 0f, distance),
                Vector3.back,
                distance);
        }

        private sealed class FakePhysicsBackend : IUnityPhysicsQueryBackend
        {
            public int Capacity => SpatialContract.AttackQueryCandidateCapacity;
            public UnityPhysicsHit[] SphereCastHits { get; set; } = Array.Empty<UnityPhysicsHit>();
            public NonAllocPhysicsQueryResult? ForcedSphereCastResult { get; set; }
            public int SphereCastCallCount { get; private set; }
            public Vector3 LastOrigin { get; private set; }
            public float LastRadius { get; private set; }
            public Vector3 LastDirection { get; private set; }
            public float LastMaxDistance { get; private set; }
            public int LastLayerMask { get; private set; }
            public QueryTriggerInteraction LastTriggerInteraction { get; private set; }

            public void SyncTransforms()
            {
            }

            public NonAllocPhysicsQueryResult RaycastNonAlloc(
                Vector3 origin,
                Vector3 direction,
                UnityPhysicsHit[] output,
                float maxDistance,
                int layerMask,
                QueryTriggerInteraction triggerInteraction)
            {
                return new NonAllocPhysicsQueryResult(0, false);
            }

            public NonAllocPhysicsQueryResult SphereCastNonAlloc(
                Vector3 origin,
                float radius,
                Vector3 direction,
                UnityPhysicsHit[] output,
                float maxDistance,
                int layerMask,
                QueryTriggerInteraction triggerInteraction)
            {
                SphereCastCallCount++;
                LastOrigin = origin;
                LastRadius = radius;
                LastDirection = direction;
                LastMaxDistance = maxDistance;
                LastLayerMask = layerMask;
                LastTriggerInteraction = triggerInteraction;
                if (ForcedSphereCastResult.HasValue)
                {
                    return ForcedSphereCastResult.Value;
                }

                int count = Math.Min(SphereCastHits.Length, output.Length);
                Array.Copy(SphereCastHits, output, count);
                return new NonAllocPhysicsQueryResult(count, SphereCastHits.Length > output.Length);
            }

            public NonAllocPhysicsQueryResult OverlapSphereNonAlloc(
                Vector3 position,
                float radius,
                Collider[] output,
                int layerMask,
                QueryTriggerInteraction triggerInteraction)
            {
                return new NonAllocPhysicsQueryResult(0, false);
            }
        }
    }
}
