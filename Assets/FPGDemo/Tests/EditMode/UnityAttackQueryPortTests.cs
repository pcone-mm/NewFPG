using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class UnityAttackQueryPortTests
    {
        private const int HitboxLayer = 29;
        private const int BlockerLayer = 28;
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
        public void RegistryRequiresExplicitUniqueColliderAndGeometryBindings()
        {
            HitboxRegistry registry = CreateRegistry();
            BoxCollider first = CreateCollider("First", Vector3.zero);
            BoxCollider second = CreateCollider("Second", Vector3.right);
            HitboxBinding binding = CombatantBinding(
                first,
                20,
                100,
                Team.Enemy,
                HitPart.Weakpoint);

            Assert.That(registry.Register(binding).IsSuccess, Is.True);
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.TryResolve(first, out RegisteredHitbox resolved), Is.True);
            Assert.That(resolved.RuntimeId.Value, Is.EqualTo(20));
            Assert.That(resolved.GeometryId.Value, Is.EqualTo(100));
            Assert.That(registry.TryResolve(new GeometryId(100), out RegisteredHitbox byGeometry), Is.True);
            Assert.That(byGeometry.Collider, Is.SameAs(first));

            Assert.That(registry.Register(CombatantBinding(first, 21, 101, Team.Enemy)).RejectReason,
                Is.EqualTo(RejectReason.InvalidDefinition));

            HitboxBinding sessionEnemy = new HitboxBinding(
                second,
                HitboxTargetReference.Enemy,
                QueryTargetKind.Combatant,
                HitPart.Body,
                new GeometryId(102));
            Assert.That(registry.Register(sessionEnemy).RejectReason,
                Is.EqualTo(RejectReason.InvalidDefinition));
            Assert.That(registry.Register(
                sessionEnemy,
                new RuntimeId(700),
                new RuntimeId(900)).IsSuccess,
                Is.True);
            Assert.That(registry.TryResolve(second, out RegisteredHitbox sessionResolved), Is.True);
            Assert.That(sessionResolved.RuntimeId.Value, Is.EqualTo(900));
            Assert.That(sessionResolved.Team, Is.EqualTo(Team.Enemy));
            Assert.That(registry.Register(CombatantBinding(second, 21, 100, Team.Enemy)).RejectReason,
                Is.EqualTo(RejectReason.InvalidDefinition));
            Assert.That(registry.Register(new HitboxBinding(
                second,
                new RuntimeId(21),
                QueryTargetKind.EnvironmentBlocker,
                HitPart.Body,
                new GeometryId(102),
                Team.Enemy)).RejectReason,
                Is.EqualTo(RejectReason.InvalidDefinition));

            Assert.That(registry.Unregister(first).IsSuccess, Is.True);
            Assert.That(registry.TryResolve(first, out resolved), Is.False);
            Assert.That(registry.TryResolve(new GeometryId(100), out byGeometry), Is.False);

            Assert.That(registry.ResetForSession(
                new RuntimeId(701),
                new RuntimeId(901),
                out string resetError), Is.True, resetError);
            Assert.That(registry.Count, Is.Zero);
            Assert.That(registry.Register(
                sessionEnemy,
                new RuntimeId(701),
                new RuntimeId(901)).IsSuccess, Is.True);
            Assert.That(registry.TryResolve(second, out RegisteredHitbox rebound), Is.True);
            Assert.That(rebound.RuntimeId.Value, Is.EqualTo(901));
        }

        [Test]
        public void PelletQueryFiltersBindingsAndCanonicalizesBeforeReturning()
        {
            HitboxRegistry registry = CreateRegistry();
            BoxCollider farLowGeometry = CreateCollider("FarLowGeometry", Vector3.zero);
            BoxCollider near = CreateCollider("Near", Vector3.zero);
            BoxCollider farHighGeometry = CreateCollider("FarHighGeometry", Vector3.zero);
            BoxCollider friendly = CreateCollider("Friendly", Vector3.zero);
            BoxCollider trigger = CreateCollider("Trigger", Vector3.zero, true);
            BoxCollider allowedTrigger = CreateCollider("AllowedTrigger", Vector3.zero, true);
            BoxCollider unregistered = CreateCollider("Unregistered", Vector3.zero);
            BoxCollider wrongLayer = CreateCollider("WrongLayer", Vector3.zero, false, BlockerLayer);

            Assert.That(registry.Register(CombatantBinding(farLowGeometry, 30, 1, Team.Enemy)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(near, 20, 9, Team.Enemy)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(farHighGeometry, 10, 2, Team.Enemy)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(friendly, 40, 4, Team.Player)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(trigger, 50, 5, Team.Enemy)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(
                allowedTrigger,
                60,
                6,
                Team.Enemy,
                HitPart.Body,
                true)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(wrongLayer, 70, 7, Team.Enemy)).IsSuccess, Is.True);

            FakePhysicsBackend physics = new FakePhysicsBackend();
            physics.RaycastHits = new[]
            {
                Hit(farHighGeometry, 5f),
                Hit(unregistered, 1f),
                Hit(friendly, 2f),
                Hit(farLowGeometry, 5f),
                Hit(trigger, 1.5f),
                Hit(allowedTrigger, 2.5f),
                Hit(wrongLayer, 2.25f),
                Hit(near, 3f)
            };
            UnityAttackQueryPort port = CreatePort(registry, physics);
            AttackQueryRequest request = CreatePelletRequest();
            QueryCandidate[] output = new QueryCandidate[8];

            DomainResult queried = port.Query(request, output, out AttackQueryResult result);

            Assert.That(queried.IsSuccess, Is.True);
            Assert.That(result.CandidateCount, Is.EqualTo(4));
            Assert.That(result.DroppedCandidateCount, Is.Zero);
            Assert.That(output[0].TargetId.Value, Is.EqualTo(60));
            Assert.That(output[0].DistanceKey, Is.EqualTo(2500));
            Assert.That(output[1].TargetId.Value, Is.EqualTo(20));
            Assert.That(output[1].DistanceKey, Is.EqualTo(3000));
            Assert.That(output[2].GeometryId.Value, Is.EqualTo(1));
            Assert.That(output[3].GeometryId.Value, Is.EqualTo(2));
            Assert.That(output[0].QueryOrdinal, Is.EqualTo(0));
            Assert.That(output[1].QueryOrdinal, Is.EqualTo(1));
            Assert.That(output[2].QueryOrdinal, Is.EqualTo(2));
            Assert.That(output[3].QueryOrdinal, Is.EqualTo(3));
            Assert.That(physics.RaycastCallCount, Is.EqualTo(1));
            Assert.That(physics.OverlapCallCount, Is.Zero);
        }

        [Test]
        public void QuerySynchronizesPhysicsOncePerValidTopLevelQuery()
        {
            HitboxRegistry registry = CreateRegistry();
            FakePhysicsBackend physics = new FakePhysicsBackend();
            UnityAttackQueryPort port = CreatePort(registry, physics);
            QueryCandidate[] output = new QueryCandidate[SpatialContract.AttackQueryCandidateCapacity];

            Assert.That(port.Query(
                CreatePelletRequest(),
                output,
                out AttackQueryResult pelletResult).IsSuccess,
                Is.True);
            Assert.That(pelletResult.CandidateCount, Is.Zero);
            Assert.That(port.Query(
                CreateSecondaryRequest(),
                output,
                out AttackQueryResult secondaryResult).IsSuccess,
                Is.True);
            Assert.That(secondaryResult.CandidateCount, Is.Zero);
            Assert.That(physics.SyncCallCount, Is.EqualTo(2));

            Assert.That(port.Query(
                default(AttackQueryRequest),
                output,
                out AttackQueryResult invalidResult).RejectReason,
                Is.EqualTo(RejectReason.InvalidState));
            Assert.That(invalidResult.CandidateCount, Is.Zero);
            Assert.That(physics.SyncCallCount, Is.EqualTo(2));
        }

        [Test]
        public void AimSolutionUsesFormalEligibilityAndNearestBlocker()
        {
            HitboxRegistry registry = CreateRegistry();
            BoxCollider friendly = CreateCollider("AimFriendly", Vector3.zero);
            BoxCollider enemy = CreateCollider("AimEnemy", Vector3.zero);
            BoxCollider blocker = CreateCollider(
                "AimBlocker",
                Vector3.zero,
                false,
                BlockerLayer);
            Assert.That(
                registry.Register(CombatantBinding(
                    friendly,
                    10,
                    10,
                    Team.Player)).IsSuccess,
                Is.True);
            Assert.That(
                registry.Register(CombatantBinding(
                    enemy,
                    20,
                    20,
                    Team.Enemy,
                    HitPart.Weakpoint)).IsSuccess,
                Is.True);
            Assert.That(
                registry.Register(new HitboxBinding(
                    blocker,
                    RuntimeId.Invalid,
                    QueryTargetKind.EnvironmentBlocker,
                    HitPart.Body,
                    new GeometryId(30),
                    Team.Neutral)).IsSuccess,
                Is.True);

            FakePhysicsBackend physics = new FakePhysicsBackend
            {
                RaycastHits = new[]
                {
                    Hit(enemy, 4f),
                    Hit(friendly, 1f),
                    Hit(blocker, 3f)
                }
            };
            UnityAttackQueryPort port = CreatePort(registry, physics);

            DomainResult blockedResult = port.SolveAim(
                Vector3.zero,
                Vector3.forward,
                new RuntimeId(1),
                Team.Player,
                AttackTargetKinds.Combatant,
                out FpgFormalAimSolution blocked);

            Assert.That(blockedResult.IsSuccess, Is.True);
            Assert.That(blocked.Kind, Is.EqualTo(FpgAimSolutionKind.Blocked));
            Assert.That(blocked.GeometryId, Is.EqualTo(new GeometryId(30)));

            physics.RaycastHits = new[]
            {
                Hit(blocker, 3f),
                Hit(enemy, 2f)
            };
            Assert.That(
                port.SolveAim(
                    Vector3.zero,
                    Vector3.forward,
                    new RuntimeId(1),
                    Team.Player,
                    AttackTargetKinds.Combatant,
                    out FpgFormalAimSolution hittable).IsSuccess,
                Is.True);
            Assert.That(hittable.Kind, Is.EqualTo(FpgAimSolutionKind.Hittable));
            Assert.That(hittable.TargetId, Is.EqualTo(new RuntimeId(20)));
            Assert.That(hittable.HitPart, Is.EqualTo(HitPart.Weakpoint));

            Assert.That(
                port.SolveAim(
                    Vector3.zero,
                    Vector3.forward,
                    new RuntimeId(1),
                    Team.Player,
                    AttackTargetKinds.Projectile,
                    out FpgFormalAimSolution projectileOnly).IsSuccess,
                Is.True);
            Assert.That(
                projectileOnly.Kind,
                Is.EqualTo(FpgAimSolutionKind.Blocked));
            Assert.That(physics.SyncCallCount, Is.EqualTo(3));
        }

        [Test]
        public void AimSolutionPrefersBlockerAtTheSameQuantizedDistance()
        {
            HitboxRegistry registry = CreateRegistry();
            BoxCollider enemy = CreateCollider("EqualDistanceAimEnemy", Vector3.zero);
            BoxCollider blocker = CreateCollider(
                "EqualDistanceAimBlocker",
                Vector3.zero,
                false,
                BlockerLayer);
            Assert.That(
                registry.Register(
                    CombatantBinding(enemy, 20, 10, Team.Enemy)).IsSuccess,
                Is.True);
            Assert.That(
                registry.Register(new HitboxBinding(
                    blocker,
                    RuntimeId.Invalid,
                    QueryTargetKind.EnvironmentBlocker,
                    HitPart.Body,
                    new GeometryId(30),
                    Team.Neutral)).IsSuccess,
                Is.True);

            FakePhysicsBackend physics = new FakePhysicsBackend
            {
                RaycastHits = new[]
                {
                    Hit(enemy, 4f),
                    Hit(blocker, 4f)
                }
            };
            UnityAttackQueryPort port = CreatePort(registry, physics);

            DomainResult result = port.SolveAim(
                Vector3.zero,
                Vector3.forward,
                new RuntimeId(1),
                Team.Player,
                AttackTargetKinds.Combatant,
                out FpgFormalAimSolution solution);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(solution.Kind, Is.EqualTo(FpgAimSolutionKind.Blocked));
            Assert.That(solution.GeometryId, Is.EqualTo(new GeometryId(30)));
        }

        [Test]
        public void AreaAtFirstSurfacePrefersBlockerAsEqualDistanceExplosionAnchor()
        {
            HitboxRegistry registry = CreateRegistry();
            BoxCollider enemy = CreateCollider("EqualDistanceAreaEnemy", Vector3.zero);
            BoxCollider blocker = CreateCollider(
                "EqualDistanceAreaBlocker",
                Vector3.zero,
                false,
                BlockerLayer);
            Assert.That(
                registry.Register(
                    CombatantBinding(enemy, 20, 10, Team.Enemy)).IsSuccess,
                Is.True);
            Assert.That(
                registry.Register(new HitboxBinding(
                    blocker,
                    RuntimeId.Invalid,
                    QueryTargetKind.EnvironmentBlocker,
                    HitPart.Body,
                    new GeometryId(30),
                    Team.Neutral)).IsSuccess,
                Is.True);

            Vector3 enemyPoint = new Vector3(1f, 0f, 4f);
            Vector3 blockerPoint = new Vector3(0f, 0f, 4f);
            FakePhysicsBackend physics = new FakePhysicsBackend
            {
                RaycastHits = new[]
                {
                    new UnityPhysicsHit(enemy, enemyPoint, Vector3.back, 4f),
                    new UnityPhysicsHit(blocker, blockerPoint, Vector3.back, 4f)
                }
            };
            UnityAttackQueryPort port = CreatePort(registry, physics);

            DomainResult result = port.Query(
                CreateAreaAtFirstSurfaceRequest(),
                new QueryCandidate[8],
                out AttackQueryResult queryResult);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(queryResult.CandidateCount, Is.EqualTo(2));
            Assert.That(physics.LastOverlapPosition, Is.EqualTo(blockerPoint));
        }

        [Test]
        public void SuccessfulQueryCapturesTheFrozenMissPathWithoutExtraPhysicsQueries()
        {
            HitboxRegistry registry = CreateRegistry();
            FakePhysicsBackend physics = new FakePhysicsBackend();
            RecordingPlayerShotCaptureSink captureSink = new RecordingPlayerShotCaptureSink();
            UnityAttackQueryPort port = new UnityAttackQueryPort(
                registry,
                new UnityAttackQuerySettings(
                    20f,
                    0.05f,
                    3f,
                    1 << HitboxLayer,
                    1 << BlockerLayer),
                physics,
                captureSink);

            DomainResult result = port.Query(
                CreatePelletRequest(),
                new QueryCandidate[SpatialContract.AttackQueryCandidateCapacity],
                out AttackQueryResult queryResult);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(queryResult.CandidateCount, Is.Zero);
            Assert.That(captureSink.CaptureCount, Is.EqualTo(1));
            Assert.That(captureSink.Capture.IsValidFor(WeaponReleaseKind.Primary), Is.True);
            PlayerShotTrajectory trajectory = captureSink.Capture.GetTrajectory(0);
            Assert.That(trajectory.Start, Is.EqualTo(SpatialVectorKey.Zero));
            Assert.That(trajectory.TerminalKind, Is.EqualTo(PlayerShotTerminalKind.Miss));
            Assert.That(trajectory.TerminalPoint, Is.EqualTo(new SpatialVectorKey(0, 0, 20000)));
            Assert.That(physics.SyncCallCount, Is.EqualTo(1));
            Assert.That(physics.RaycastCallCount, Is.EqualTo(1));
            Assert.That(physics.OverlapCallCount, Is.Zero);
            Assert.That(port.PresentationCaptureFaultCount, Is.Zero);
        }

        [Test]
        public void ActiveEnemyProjectileProxyIsQueryableAndDisappearsAfterRelease()
        {
            HitboxRegistry registry = CreateRegistry();
            FakePhysicsBackend physics = new FakePhysicsBackend();
            UnityAttackQueryPort port = CreatePort(registry, physics);
            ProjectileCollisionProxyPool pool = new ProjectileCollisionProxyPool(
                1,
                1 << HitboxLayer,
                registry.transform);
            try
            {
                Assert.That(pool.TryPrepare(registry, out string error), Is.True, error);
                ProjectileSpawnRequest request = new ProjectileSpawnRequest(
                    new TickIndex(0),
                    new TickIndex(2),
                    new ProjectileId(11),
                    new RuntimeId(101),
                    new AttackId(11),
                    new RuntimeId(2),
                    new RuntimeId(1),
                    Team.Enemy,
                    301,
                    250,
                    true);
                ProjectilePathSnapshot path = new ProjectilePathSnapshot(
                    request.ProjectileId,
                    request.RuntimeId,
                    request.Tick,
                    request.ArrivalTick,
                    SpatialVectorKey.Zero,
                    new SpatialVectorKey(0, 0, 2000));
                Assert.That(pool.Acquire(request, path).IsSuccess, Is.True);
                Assert.That(pool.TryGetActiveProxy(request.RuntimeId, out ProjectileCollisionProxySnapshot proxy), Is.True);

                physics.RaycastHits = new[] { Hit(proxy.Collider, 2f) };
                QueryCandidate[] output = new QueryCandidate[2];
                Assert.That(port.Query(CreatePelletRequest(), output, out AttackQueryResult activeResult).IsSuccess, Is.True);
                Assert.That(activeResult.CandidateCount, Is.EqualTo(1));
                Assert.That(output[0].TargetId, Is.EqualTo(request.RuntimeId));
                Assert.That(output[0].TargetKind, Is.EqualTo(QueryTargetKind.Projectile));
                Assert.That(output[0].HitPart, Is.EqualTo(HitPart.Projectile));
                Assert.That(output[0].GeometryId, Is.EqualTo(proxy.GeometryId));

                Assert.That(pool.Release(request.RuntimeId).IsSuccess, Is.True);
                Assert.That(port.Query(CreatePelletRequest(), output, out AttackQueryResult releasedResult).IsSuccess, Is.True);
                Assert.That(releasedResult.CandidateCount, Is.Zero);
            }
            finally
            {
                pool.Dispose();
            }
        }

        [Test]
        public void DirectThenAreaUsesCanonicalNearestDirectHitAsAreaAnchor()
        {
            HitboxRegistry registry = CreateRegistry();
            BoxCollider far = CreateCollider("Far", new Vector3(0f, 0f, 8f));
            BoxCollider near = CreateCollider("Near", new Vector3(0f, 0f, 4f));
            BoxCollider area = CreateCollider("Area", new Vector3(1f, 0f, 4f));
            Assert.That(registry.Register(CombatantBinding(far, 30, 30, Team.Enemy)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(near, 20, 20, Team.Enemy)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(area, 10, 10, Team.Enemy)).IsSuccess, Is.True);

            FakePhysicsBackend physics = new FakePhysicsBackend
            {
                RaycastHits = new[] { Hit(far, 8f), Hit(near, 4f) },
                OverlapColliders = new Collider[] { area, near }
            };
            UnityAttackQueryPort port = CreatePort(registry, physics);
            QueryCandidate[] output = new QueryCandidate[8];

            DomainResult queried = port.Query(
                CreateSecondaryRequest(),
                output,
                out AttackQueryResult result);

            Assert.That(queried.IsSuccess, Is.True);
            Assert.That(result.CandidateCount, Is.EqualTo(3));
            Assert.That(physics.LastOverlapPosition, Is.EqualTo(new Vector3(0f, 0f, 4f)));
            Assert.That(CountStage(output, result.CandidateCount, AttackQueryStage.Direct), Is.EqualTo(1));
            Assert.That(CountStage(output, result.CandidateCount, AttackQueryStage.Area), Is.EqualTo(2));
            Assert.That(Contains(
                output,
                result.CandidateCount,
                AttackQueryStage.Direct,
                new RuntimeId(30)),
                Is.False,
                "Direct candidates beyond the nearest anchor must not pass through it.");
            for (int index = 1; index < result.CandidateCount; index++)
            {
                Assert.That(output[index - 1].DistanceKey, Is.LessThanOrEqualTo(output[index].DistanceKey));
                Assert.That(output[index].QueryOrdinal, Is.EqualTo(index));
            }
        }

        [Test]
        public void AreaAtFirstSurfaceUsesRayOnlyForAnchorAndHitboxOnlyOverlap()
        {
            HitboxRegistry registry = CreateRegistry();
            BoxCollider blocker = CreateCollider(
                nameof(AreaAtFirstSurfaceUsesRayOnlyForAnchorAndHitboxOnlyOverlap),
                new Vector3(0f, 0f, 4f),
                false,
                BlockerLayer);
            BoxCollider far = CreateCollider(
                nameof(UnityAttackQueryPortTests),
                new Vector3(0f, 0f, 8f));
            BoxCollider area = CreateCollider(
                nameof(AttackQueryMode),
                new Vector3(1f, 0f, 4f));
            Assert.That(registry.Register(new HitboxBinding(
                blocker,
                RuntimeId.Invalid,
                QueryTargetKind.EnvironmentBlocker,
                HitPart.Body,
                new GeometryId(40),
                Team.Neutral)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(far, 30, 30, Team.Enemy)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(area, 10, 10, Team.Enemy)).IsSuccess, Is.True);

            FakePhysicsBackend physics = new FakePhysicsBackend
            {
                RaycastHits = new[] { Hit(far, 8f), Hit(blocker, 4f) },
                OverlapColliders = new Collider[] { blocker, area }
            };
            UnityAttackQueryPort port = CreatePort(registry, physics);
            AttackQueryRequest request = CreateAreaAtFirstSurfaceRequest();
            QueryCandidate[] candidates = new QueryCandidate[8];

            DomainResult queried = port.Query(
                request,
                candidates,
                out AttackQueryResult queryResult);
            QueryCandidate[] selected = new QueryCandidate[3];
            DomainResult selectedResult = TargetSelector.Select(
                request.Attack,
                candidates,
                queryResult,
                selected,
                out int selectedCount);

            Assert.That(queried.IsSuccess, Is.True);
            Assert.That(physics.LastOverlapPosition, Is.EqualTo(new Vector3(0f, 0f, 4f)));
            Assert.That(physics.LastOverlapLayerMask, Is.EqualTo(1 << HitboxLayer));
            Assert.That(selectedResult.IsSuccess, Is.True);
            Assert.That(selectedCount, Is.EqualTo(1));
            Assert.That(selected[0].QueryStage, Is.EqualTo(AttackQueryStage.Area));
            Assert.That(selected[0].TargetId, Is.EqualTo(new RuntimeId(10)));
        }

        [Test]
        public void FullNonAllocBufferRejectsWholeQueryAsPotentiallyTruncated()
        {
            HitboxRegistry registry = CreateRegistry();
            FakePhysicsBackend physics = new FakePhysicsBackend
            {
                ForcedRaycastResult = new NonAllocPhysicsQueryResult(
                    SpatialContract.AttackQueryCandidateCapacity,
                    false)
            };
            UnityAttackQueryPort port = CreatePort(registry, physics);

            physics.ForcedRaycastResult = new NonAllocPhysicsQueryResult(
                SpatialContract.AttackQueryCandidateCapacity - 1,
                false);
            DomainResult accepted = port.Query(
                CreatePelletRequest(),
                new QueryCandidate[SpatialContract.AttackQueryCandidateCapacity],
                out AttackQueryResult acceptedResult);
            Assert.That(accepted.IsSuccess, Is.True);
            Assert.That(acceptedResult.DroppedCandidateCount, Is.Zero);

            physics.ForcedRaycastResult = new NonAllocPhysicsQueryResult(
                SpatialContract.AttackQueryCandidateCapacity,
                false);

            DomainResult queried = port.Query(
                CreatePelletRequest(),
                new QueryCandidate[SpatialContract.AttackQueryCandidateCapacity],
                out AttackQueryResult result);

            Assert.That(queried.RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(result.CandidateCount, Is.Zero);
            Assert.That(result.DroppedCandidateCount, Is.EqualTo(1));
        }

        [Test]
        public void CanonicalCandidatesAndTranscriptDigestIgnorePhysicsReturnOrder()
        {
            HitboxRegistry registry = CreateRegistry();
            BoxCollider first = CreateCollider("FirstCanonical", Vector3.zero);
            BoxCollider second = CreateCollider("SecondCanonical", Vector3.zero);
            BoxCollider third = CreateCollider("ThirdCanonical", Vector3.zero);
            Assert.That(registry.Register(CombatantBinding(first, 30, 3, Team.Enemy)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(second, 20, 2, Team.Enemy)).IsSuccess, Is.True);
            Assert.That(registry.Register(CombatantBinding(third, 10, 1, Team.Enemy)).IsSuccess, Is.True);

            UnityPhysicsHit[] observations =
            {
                Hit(first, 5f),
                Hit(second, 3f),
                Hit(third, 3f)
            };
            AttackQueryRequest request = CreatePelletRequest();

            SpatialPortTranscript firstTranscript = new SpatialPortTranscript(
                operationCapacity: 1,
                queryCandidateCapacity: SpatialContract.AttackQueryCandidateCapacity);
            FakePhysicsBackend firstPhysics = new FakePhysicsBackend { RaycastHits = observations };
            RecordingAttackQueryPort firstRecording = new RecordingAttackQueryPort(
                CreatePort(registry, firstPhysics),
                firstTranscript);
            QueryCandidate[] firstOutput = new QueryCandidate[8];
            Assert.That(firstRecording.Query(
                request,
                firstOutput,
                out AttackQueryResult firstResult).IsSuccess, Is.True);

            SpatialPortTranscript secondTranscript = new SpatialPortTranscript(
                operationCapacity: 1,
                queryCandidateCapacity: SpatialContract.AttackQueryCandidateCapacity);
            FakePhysicsBackend secondPhysics = new FakePhysicsBackend
            {
                RaycastHits = new[] { observations[2], observations[0], observations[1] }
            };
            RecordingAttackQueryPort secondRecording = new RecordingAttackQueryPort(
                CreatePort(registry, secondPhysics),
                secondTranscript);
            QueryCandidate[] secondOutput = new QueryCandidate[8];
            Assert.That(secondRecording.Query(
                request,
                secondOutput,
                out AttackQueryResult secondResult).IsSuccess, Is.True);

            Assert.That(secondResult.CandidateCount, Is.EqualTo(firstResult.CandidateCount));
            for (int index = 0; index < firstResult.CandidateCount; index++)
            {
                AssertCandidateEqual(firstOutput[index], secondOutput[index]);
            }

            Assert.That(secondTranscript.CanonicalDigest, Is.EqualTo(firstTranscript.CanonicalDigest));
        }

        [Test]
        public void PelletSpreadMapsSquareSamplesIntoTheCameraSpaceCone()
        {
            HitboxRegistry registry = CreateRegistry();
            FakePhysicsBackend physics = new FakePhysicsBackend();
            UnityAttackQueryPort port = CreatePort(registry, physics);
            TickIndex tick = new TickIndex(0);
            AttackSnapshot attack = CreateAttack(tick, QueryPolicy.PelletRays, 4, 4);
            PelletSample[] pellets =
            {
                new PelletSample(attack.ShotId, 0, 0xFFFFFF, 0xFFFFFF),
                new PelletSample(attack.ShotId, 1, 0, 0),
                new PelletSample(attack.ShotId, 2, 0xFFFFFF, 0),
                new PelletSample(attack.ShotId, 3, 0, 0xFFFFFF)
            };

            DomainResult queried = port.Query(
                new AttackQueryRequest(CreateTickInput(tick), attack, pellets, pellets.Length),
                new QueryCandidate[8],
                out AttackQueryResult result);

            Assert.That(queried.IsSuccess, Is.True);
            Assert.That(result.CandidateCount, Is.Zero);
            Assert.That(physics.CapturedRayDirections.Count, Is.EqualTo(4));
            for (int index = 0; index < physics.CapturedRayDirections.Count; index++)
            {
                Vector3 direction = physics.CapturedRayDirections[index];
                float cameraX = direction.x / direction.z;
                float cameraY = direction.y / direction.z;
                float radius = Mathf.Sqrt(cameraX * cameraX + cameraY * cameraY);
                Assert.That(radius, Is.EqualTo(0.05f).Within(0.00001f));
            }

            Vector3 first = physics.CapturedRayDirections[0];
            Vector3 opposite = physics.CapturedRayDirections[1];
            Assert.That(first.x / first.z + opposite.x / opposite.z, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(first.y / first.z + opposite.y / opposite.z, Is.EqualTo(0f).Within(0.00001f));
        }

        [Test]
        public void UnityPhysicsBackendExposesRaySphereAndOverlapNonAllocQueries()
        {
            Vector3 root = new Vector3(12000f, 3000f, -9000f);
            BoxCollider collider = CreateCollider("PhysicsTarget", root + new Vector3(0f, 0f, 4f));
            Physics.SyncTransforms();
            UnityPhysicsQueryBackend backend = new UnityPhysicsQueryBackend();
            UnityPhysicsHit[] hits = new UnityPhysicsHit[SpatialContract.AttackQueryCandidateCapacity];
            Collider[] overlaps = new Collider[SpatialContract.AttackQueryCandidateCapacity];
            int mask = 1 << HitboxLayer;

            NonAllocPhysicsQueryResult ray = backend.RaycastNonAlloc(
                root,
                Vector3.forward,
                hits,
                10f,
                mask,
                QueryTriggerInteraction.Ignore);
            Assert.That(ray.MayBeTruncated, Is.False);
            Assert.That(ray.Count, Is.EqualTo(1));
            Assert.That(hits[0].Collider, Is.SameAs(collider));

            NonAllocPhysicsQueryResult sphere = backend.SphereCastNonAlloc(
                root,
                0.25f,
                Vector3.forward,
                hits,
                10f,
                mask,
                QueryTriggerInteraction.Ignore);
            Assert.That(sphere.MayBeTruncated, Is.False);
            Assert.That(sphere.Count, Is.EqualTo(1));
            Assert.That(hits[0].Collider, Is.SameAs(collider));

            NonAllocPhysicsQueryResult overlap = backend.OverlapSphereNonAlloc(
                collider.transform.position,
                1f,
                overlaps,
                mask,
                QueryTriggerInteraction.Ignore);
            Assert.That(overlap.MayBeTruncated, Is.False);
            Assert.That(overlap.Count, Is.EqualTo(1));
            Assert.That(overlaps[0], Is.SameAs(collider));
        }

        private HitboxRegistry CreateRegistry()
        {
            GameObject gameObject = Track(new GameObject("HitboxRegistry"));
            HitboxRegistry registry = gameObject.AddComponent<HitboxRegistry>();
            Assert.That(registry.TryInitialize(out string error), Is.True, error);
            return registry;
        }

        private BoxCollider CreateCollider(
            string name,
            Vector3 position,
            bool isTrigger = false,
            int layer = HitboxLayer)
        {
            GameObject gameObject = Track(new GameObject(name));
            gameObject.layer = layer;
            gameObject.transform.position = position;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = isTrigger;
            return collider;
        }

        private GameObject Track(GameObject gameObject)
        {
            objects.Add(gameObject);
            return gameObject;
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

        private static UnityPhysicsHit Hit(Collider collider, float distance)
        {
            return new UnityPhysicsHit(
                collider,
                new Vector3(0f, 0f, distance),
                Vector3.back,
                distance);
        }

        private static UnityAttackQueryPort CreatePort(
            HitboxRegistry registry,
            IUnityPhysicsQueryBackend physics)
        {
            return new UnityAttackQueryPort(
                registry,
                new UnityAttackQuerySettings(
                    20f,
                    0.05f,
                    3f,
                    1 << HitboxLayer,
                    1 << BlockerLayer),
                physics);
        }

        private static AttackQueryRequest CreatePelletRequest()
        {
            TickIndex tick = new TickIndex(0);
            AttackSnapshot attack = CreateAttack(tick, QueryPolicy.PelletRays, 1, 1);
            return new AttackQueryRequest(
                CreateTickInput(tick),
                attack,
                new[] { new PelletSample(attack.ShotId, 0, 0x7FFFFF, 0x7FFFFF) },
                1);
        }

        private static AttackQueryRequest CreateSecondaryRequest()
        {
            TickIndex tick = new TickIndex(0);
            return new AttackQueryRequest(
                CreateTickInput(tick),
                CreateAttack(tick, QueryPolicy.DirectThenArea, 1, 4),
                null,
                0);
        }

        private static AttackQueryRequest CreateAreaAtFirstSurfaceRequest()
        {
            TickIndex tick = new TickIndex(0);
            AttackSnapshot attack = new AttackSnapshot(
                new AttackId(1),
                new ShotId(1),
                1,
                new RuntimeId(1),
                Team.Player,
                tick,
                new DamageSpec(10, 2),
                QueryPolicy.DirectThenArea,
                1,
                3,
                1,
                1,
                AttackQueryMode.AreaAtFirstSurface,
                0,
                2,
                1);
            return new AttackQueryRequest(
                CreateTickInput(tick),
                attack,
                null,
                0);
        }

        private static AttackSnapshot CreateAttack(
            TickIndex tick,
            QueryPolicy policy,
            int payloadCount,
            int maxImpactCount)
        {
            return new AttackSnapshot(
                new AttackId(1),
                new ShotId(1),
                1,
                new RuntimeId(1),
                Team.Player,
                tick,
                new DamageSpec(10, 2),
                policy,
                payloadCount,
                maxImpactCount,
                1,
                1);
        }

        private static BattleTickInput CreateTickInput(TickIndex tick)
        {
            return new BattleTickInput(
                PlayerInputFrame.Empty(tick),
                new AimPoseSnapshot(
                    tick,
                    SpatialVectorKey.Zero,
                    new SpatialVectorKey(0, 0, SpatialContract.DirectionUnits),
                    new SpatialVectorKey(SpatialContract.DirectionUnits, 0, 0),
                    new SpatialVectorKey(0, SpatialContract.DirectionUnits, 0),
                    1));
        }

        private static int CountStage(
            QueryCandidate[] candidates,
            int count,
            AttackQueryStage stage)
        {
            int matches = 0;
            for (int index = 0; index < count; index++)
            {
                if (candidates[index].QueryStage == stage)
                {
                    matches++;
                }
            }

            return matches;
        }

        private static void AssertCandidateEqual(
            in QueryCandidate expected,
            in QueryCandidate actual)
        {
            Assert.That(actual.QueryStage, Is.EqualTo(expected.QueryStage));
            Assert.That(actual.SampleIndex, Is.EqualTo(expected.SampleIndex));
            Assert.That(actual.TargetId, Is.EqualTo(expected.TargetId));
            Assert.That(actual.TargetKind, Is.EqualTo(expected.TargetKind));
            Assert.That(actual.HitPart, Is.EqualTo(expected.HitPart));
            Assert.That(actual.GeometryId, Is.EqualTo(expected.GeometryId));
            Assert.That(actual.DistanceKey, Is.EqualTo(expected.DistanceKey));
            Assert.That(actual.ImpactPointKey, Is.EqualTo(expected.ImpactPointKey));
            Assert.That(actual.QueryOrdinal, Is.EqualTo(expected.QueryOrdinal));
        }

        private static bool Contains(
            QueryCandidate[] candidates,
            int count,
            AttackQueryStage stage,
            RuntimeId targetId)
        {
            for (int index = 0; index < count; index++)
            {
                if (candidates[index].QueryStage == stage
                    && candidates[index].TargetId == targetId)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class FakePhysicsBackend : IUnityPhysicsQueryBackend
        {
            public int Capacity => SpatialContract.AttackQueryCandidateCapacity;
            public UnityPhysicsHit[] RaycastHits { get; set; } = Array.Empty<UnityPhysicsHit>();
            public Collider[] OverlapColliders { get; set; } = Array.Empty<Collider>();
            public NonAllocPhysicsQueryResult? ForcedRaycastResult { get; set; }
            public int SyncCallCount { get; private set; }
            public int RaycastCallCount { get; private set; }
            public int SphereCastCallCount { get; private set; }
            public int OverlapCallCount { get; private set; }
            public Vector3 LastOverlapPosition { get; private set; }
            public int LastOverlapLayerMask { get; private set; }
            public List<Vector3> CapturedRayDirections { get; } = new List<Vector3>();

            public void SyncTransforms()
            {
                SyncCallCount++;
            }

            public NonAllocPhysicsQueryResult RaycastNonAlloc(
                Vector3 origin,
                Vector3 direction,
                UnityPhysicsHit[] output,
                float maxDistance,
                int layerMask,
                QueryTriggerInteraction triggerInteraction)
            {
                RaycastCallCount++;
                CapturedRayDirections.Add(direction);
                if (ForcedRaycastResult.HasValue)
                {
                    return ForcedRaycastResult.Value;
                }

                int count = Math.Min(RaycastHits.Length, output.Length);
                Array.Copy(RaycastHits, output, count);
                return new NonAllocPhysicsQueryResult(count, RaycastHits.Length > output.Length);
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
                return new NonAllocPhysicsQueryResult(0, false);
            }

            public NonAllocPhysicsQueryResult OverlapSphereNonAlloc(
                Vector3 position,
                float radius,
                Collider[] output,
                int layerMask,
                QueryTriggerInteraction triggerInteraction)
            {
                OverlapCallCount++;
                LastOverlapPosition = position;
                LastOverlapLayerMask = layerMask;
                int count = Math.Min(OverlapColliders.Length, output.Length);
                Array.Copy(OverlapColliders, output, count);
                return new NonAllocPhysicsQueryResult(count, OverlapColliders.Length > output.Length);
            }
        }

        private sealed class RecordingPlayerShotCaptureSink : IPlayerShotQueryCaptureSink
        {
            public int CaptureCount { get; private set; }
            public PlayerShotQueryCapture Capture { get; private set; }

            public bool TryCaptureSuccessfulQuery(in PlayerShotQueryCapture capture)
            {
                CaptureCount++;
                Capture = capture;
                return true;
            }
        }
    }
}
