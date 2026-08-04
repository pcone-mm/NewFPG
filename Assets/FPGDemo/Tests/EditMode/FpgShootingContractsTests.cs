using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgShootingContractsTests
    {
        private const int HitboxLayer = 29;
        private const int BlockerLayer = 28;
        private const string ForestRoomPath =
            "Assets/FPGDemo/Config/Level/Rooms/Room_forest.asset";

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
        public void ResolvedAimFreezePreservesTheCompleteAuthoritativeContext()
        {
            FpgResolvedAimContext live = CreateEnvironmentAim(
                targetCoverId: "cover-left",
                currentCoverId: "cover-right",
                version: 41L);

            FpgResolvedAimContext frozen = live.Freeze();

            AssertAll(() =>
            {
                Assert.That(live.IsValid, Is.True);
                Assert.That(live.IsFrozen, Is.False);
                Assert.That(frozen.IsValid, Is.True);
                Assert.That(frozen.IsFrozen, Is.True);
                Assert.That(frozen.FrozenVersion, Is.EqualTo(live.Version));
                Assert.That(frozen.ReticleViewport, Is.EqualTo(live.ReticleViewport));
                Assert.That(frozen.CameraOrigin, Is.EqualTo(live.CameraOrigin));
                Assert.That(frozen.CameraDirection, Is.EqualTo(live.CameraDirection));
                Assert.That(frozen.TargetPoint, Is.EqualTo(live.TargetPoint));
                Assert.That(frozen.ShotOrigin, Is.EqualTo(live.ShotOrigin));
                Assert.That(frozen.CenterDirection, Is.EqualTo(live.CenterDirection));
                Assert.That(frozen.SurfacePoint, Is.EqualTo(live.SurfacePoint));
                Assert.That(
                    frozen.ReticleTargetType,
                    Is.EqualTo(live.ReticleTargetType));
                Assert.That(
                    frozen.ReticleTargetId,
                    Is.EqualTo(live.ReticleTargetId));
                Assert.That(
                    frozen.ReticleTargetKind,
                    Is.EqualTo(live.ReticleTargetKind));
                Assert.That(
                    frozen.ReticleHitPart,
                    Is.EqualTo(live.ReticleHitPart));
                Assert.That(
                    frozen.ReticleGeometryId,
                    Is.EqualTo(live.ReticleGeometryId));
                Assert.That(frozen.TargetType, Is.EqualTo(live.TargetType));
                Assert.That(frozen.TargetId, Is.EqualTo(live.TargetId));
                Assert.That(frozen.TargetKind, Is.EqualTo(live.TargetKind));
                Assert.That(frozen.HitPart, Is.EqualTo(live.HitPart));
                Assert.That(frozen.GeometryId, Is.EqualTo(live.GeometryId));
                Assert.That(frozen.TargetCoverId, Is.EqualTo(live.TargetCoverId));
                Assert.That(frozen.CurrentCoverId, Is.EqualTo(live.CurrentCoverId));
                Assert.That(frozen.Distance, Is.EqualTo(live.Distance));
                Assert.That(frozen.Freeze().FrozenVersion, Is.EqualTo(41L));
            });
        }

        [Test]
        public void CurrentCoverBlockingRequiresAnExactEnvironmentCoverMatch()
        {
            FpgResolvedAimContext environment = CreateEnvironmentAim(
                targetCoverId: "cover-left",
                currentCoverId: "cover-left");
            FpgResolvedAimContext otherCover = environment.WithCurrentCover(
                "cover-right");
            FpgResolvedAimContext differentCase = environment.WithCurrentCover(
                "Cover-Left");
            FpgResolvedAimContext enemy = CreateEnemyAim().WithCurrentCover(
                "cover-left");

            AssertAll(() =>
            {
                Assert.That(environment.IsCurrentCoverBlocked, Is.True);
                Assert.That(otherCover.IsCurrentCoverBlocked, Is.False);
                Assert.That(differentCase.IsCurrentCoverBlocked, Is.False);
                Assert.That(enemy.IsCurrentCoverBlocked, Is.False);
                Assert.That(otherCover.TargetCoverId, Is.EqualTo("cover-left"));
                Assert.That(otherCover.Version, Is.EqualTo(environment.Version));
                Assert.That(
                    environment.Freeze().WithCurrentCover("cover-left").IsFrozen,
                    Is.True);
            });
        }

        [Test]
        public void AttackAvailabilityReportsEveryStartGateWithStablePrecedence()
        {
            AssertReason(
                ResolveAvailability(playerValid: false),
                FpgAttackUnavailableReason.InvalidPlayer);
            AssertReason(
                ResolveAvailability(playerDead: true),
                FpgAttackUnavailableReason.PlayerDead);
            AssertReason(
                ResolveAvailability(encounterActive: false),
                FpgAttackUnavailableReason.EncounterInactive);
            AssertReason(
                ResolveAvailability(coverMoving: true),
                FpgAttackUnavailableReason.CoverMoving);
            AssertReason(
                ResolveAvailability(weaponState: WeaponState.Disabled),
                FpgAttackUnavailableReason.WeaponDisabled);
            AssertReason(
                ResolveAvailability(weaponState: WeaponState.Reloading),
                FpgAttackUnavailableReason.Reloading);
            AssertReason(
                ResolveAvailability(aim: FpgResolvedAimContext.Invalid),
                FpgAttackUnavailableReason.InvalidAim);
            AssertReason(
                ResolveAvailability(
                    aim: CreateEnvironmentAim("cover-left", "cover-left")),
                FpgAttackUnavailableReason.CurrentCoverBlocked);
            AssertReason(
                ResolveAvailability(weaponState: WeaponState.PrimaryRecovery),
                FpgAttackUnavailableReason.WeaponBusy);
            AssertReason(
                ResolveAvailability(recastLockedUntilTick: 11L, tick: 10L),
                FpgAttackUnavailableReason.Cooldown);

            FpgAttackAvailability ready = ResolveAvailability();
            AssertAll(() =>
            {
                Assert.That(ready.Ready, Is.True);
                Assert.That(ready.Reason, Is.EqualTo(FpgAttackUnavailableReason.None));
                Assert.That(ready.ShouldAutoReload, Is.False);
            });
        }

        [Test]
        public void AttackAvailabilityUsesCompleteAmmoCostAndQueuesReloadOnlyOnce()
        {
            FpgResolvedAimContext blockedAim = CreateEnvironmentAim(
                "cover-left",
                "cover-left");
            FpgAttackAvailability insufficient = ResolveAvailability(
                ammo: 1,
                requiredAmmo: 2,
                aim: blockedAim);
            FpgAttackAvailability alreadyReloading = ResolveAvailability(
                weaponState: WeaponState.Reloading,
                ammo: 0,
                requiredAmmo: 2);
            FpgAttackAvailability exactCost = ResolveAvailability(
                ammo: 2,
                requiredAmmo: 2);

            AssertAll(() =>
            {
                Assert.That(
                    insufficient.Reason,
                    Is.EqualTo(FpgAttackUnavailableReason.NotEnoughAmmo));
                Assert.That(insufficient.ShouldAutoReload, Is.True);
                Assert.That(insufficient.Ammo, Is.EqualTo(1));
                Assert.That(insufficient.RequiredAmmo, Is.EqualTo(2));
                Assert.That(
                    alreadyReloading.Reason,
                    Is.EqualTo(FpgAttackUnavailableReason.Reloading));
                Assert.That(alreadyReloading.ShouldAutoReload, Is.False);
                Assert.That(exactCost.Ready, Is.True);
            });
        }

        [Test]
        public void FinalReleaseValidationAllowsOnlyTheMatchingActiveWeaponState()
        {
            FpgAttackAvailability primary = ResolveAvailability(
                slot: FpgPlayerSkillSlot.Primary,
                weaponState: WeaponState.PrimaryRecovery,
                allowActiveReleaseState: true);
            FpgAttackAvailability secondaryCharging = ResolveAvailability(
                slot: FpgPlayerSkillSlot.Secondary,
                weaponState: WeaponState.AltCharging,
                allowActiveReleaseState: true);
            FpgAttackAvailability secondaryRecovery = ResolveAvailability(
                slot: FpgPlayerSkillSlot.Secondary,
                weaponState: WeaponState.AltRecovery,
                allowActiveReleaseState: true);
            FpgAttackAvailability wrongSlotState = ResolveAvailability(
                slot: FpgPlayerSkillSlot.Primary,
                weaponState: WeaponState.AltCharging,
                allowActiveReleaseState: true);

            AssertAll(() =>
            {
                Assert.That(primary.Ready, Is.True);
                Assert.That(secondaryCharging.Ready, Is.True);
                Assert.That(secondaryRecovery.Ready, Is.True);
                Assert.That(
                    wrongSlotState.Reason,
                    Is.EqualTo(FpgAttackUnavailableReason.WeaponBusy));
            });
        }

        [Test]
        public void EightPelletPatternIsDeterministicAndRemainsInsideTheAuthoredCone()
        {
            const ulong seed = 0x1020304050607080UL;
            const float spreadTangent = 0.04f;
            int pelletCount = WeaponDefinition.PrimaryPelletCount;
            ShotId shotId = new ShotId(27L);
            PelletSample[] first = new PelletSample[pelletCount];
            PelletSample[] repeated = new PelletSample[pelletCount];
            PelletPatternGenerator.Fill(seed, shotId, first, pelletCount);
            PelletPatternGenerator.Fill(seed, shotId, repeated, pelletCount);

            for (int index = 0; index < pelletCount; index++)
            {
                AssertAll(() =>
                {
                    Assert.That(first[index].ShotId, Is.EqualTo(shotId));
                    Assert.That(first[index].PelletIndex, Is.EqualTo(index));
                    Assert.That(
                        repeated[index].SpreadU24,
                        Is.EqualTo(first[index].SpreadU24));
                    Assert.That(
                        repeated[index].SpreadV24,
                        Is.EqualTo(first[index].SpreadV24));
                });
            }

            RecordingPhysicsBackend physics = new RecordingPhysicsBackend();
            UnityAttackQueryPort port = new UnityAttackQueryPort(
                CreateRegistry(),
                CreateQuerySettings(spreadTangent),
                physics);
            TickIndex tick = new TickIndex(0L);
            AttackSnapshot attack = new AttackSnapshot(
                new AttackId(1L),
                shotId,
                1,
                new RuntimeId(1L),
                Team.Player,
                tick,
                new DamageSpec(1, 0),
                QueryPolicy.PelletRays,
                pelletCount,
                pelletCount,
                1,
                DeterministicRandomV1.Version);

            DomainResult queried = port.Query(
                new AttackQueryRequest(
                    CreateTickInput(tick),
                    attack,
                    first,
                    pelletCount),
                new QueryCandidate[pelletCount],
                out AttackQueryResult result);

            AssertAll(() =>
            {
                Assert.That(queried.IsSuccess, Is.True);
                Assert.That(result.CandidateCount, Is.Zero);
                Assert.That(pelletCount, Is.EqualTo(8));
                Assert.That(physics.RaycastDirections, Has.Count.EqualTo(8));
            });

            for (int index = 0; index < physics.RaycastDirections.Count; index++)
            {
                Vector3 direction = physics.RaycastDirections[index];
                float spreadU = direction.x / direction.z;
                float spreadV = direction.y / direction.z;
                float radialTangent = Mathf.Sqrt(
                    spreadU * spreadU + spreadV * spreadV);
                Assert.That(
                    radialTangent,
                    Is.LessThanOrEqualTo(spreadTangent + 0.000001f),
                    $"Pellet {index} escaped the authored spread cone.");
            }
        }

        [Test]
        public void TwoStageAimKeepsCameraIntentButUsesTheMuzzleFirstSurface()
        {
            HitboxRegistry registry = CreateRegistry();
            BoxCollider enemy = CreateCollider("AimEnemy", HitboxLayer);
            BoxCollider blocker = CreateCollider("CurrentCover", BlockerLayer);
            Assert.That(
                registry.Register(new HitboxBinding(
                    enemy,
                    new RuntimeId(200L),
                    QueryTargetKind.Combatant,
                    HitPart.Weakpoint,
                    new GeometryId(201),
                    Team.Enemy)).IsSuccess,
                Is.True);
            Assert.That(
                registry.Register(new HitboxBinding(
                    blocker,
                    HitboxTargetReference.Environment,
                    QueryTargetKind.EnvironmentBlocker,
                    HitPart.Body,
                    new GeometryId(202))).IsSuccess,
                Is.True);

            Vector3 cameraOrigin = new Vector3(0f, 1f, 0f);
            Vector3 cameraTarget = new Vector3(0f, 1f, 10f);
            Vector3 shotOrigin = new Vector3(1f, 0f, 0f);
            Vector3 centerDirection = (cameraTarget - shotOrigin).normalized;
            Vector3 muzzleSurface = shotOrigin + centerDirection * 4f;
            RecordingPhysicsBackend physics = new RecordingPhysicsBackend();
            physics.EnqueueRaycast(new UnityPhysicsHit(
                enemy,
                cameraTarget,
                Vector3.back,
                10f));
            physics.EnqueueRaycast(new UnityPhysicsHit(
                blocker,
                muzzleSurface,
                -centerDirection,
                4f));
            DictionaryCoverResolver covers = new DictionaryCoverResolver();
            covers.Add(new GeometryId(202), "cover-left");
            UnityAttackQueryPort port = new UnityAttackQueryPort(
                registry,
                CreateQuerySettings(0.04f),
                physics);

            DomainResult resolved = port.ResolveAimContext(
                new Vector2(0.65f, 0.4f),
                cameraOrigin,
                Vector3.forward,
                shotOrigin,
                new RuntimeId(1L),
                Team.Player,
                AttackTargetKinds.Combatant,
                "cover-left",
                covers,
                73L,
                out FpgResolvedAimContext context);

            AssertAll(() =>
            {
                Assert.That(resolved.IsSuccess, Is.True);
                Assert.That(context.IsValid, Is.True);
                Assert.That(context.TargetPoint, Is.EqualTo(cameraTarget));
                Assert.That(
                    Vector3.Distance(context.SurfacePoint, muzzleSurface),
                    Is.LessThan(0.002f));
                Assert.That(
                    Vector3.Distance(context.CenterDirection, centerDirection),
                    Is.LessThan(0.000001f));
                Assert.That(
                    context.TargetType,
                    Is.EqualTo(FpgResolvedAimTargetType.Environment));
                Assert.That(
                    context.ReticleTargetType,
                    Is.EqualTo(FpgResolvedAimTargetType.Enemy));
                Assert.That(
                    context.ReticleTargetId,
                    Is.EqualTo(new RuntimeId(200L)));
                Assert.That(
                    context.ReticleTargetKind,
                    Is.EqualTo(QueryTargetKind.Combatant));
                Assert.That(
                    context.ReticleHitPart,
                    Is.EqualTo(HitPart.Weakpoint));
                Assert.That(
                    context.ReticleGeometryId,
                    Is.EqualTo(new GeometryId(201)));
                Assert.That(context.IsReticleEnemy, Is.True);
                Assert.That(context.IsEnemy, Is.False);
                Assert.That(context.GeometryId, Is.EqualTo(new GeometryId(202)));
                Assert.That(context.TargetCoverId, Is.EqualTo("cover-left"));
                Assert.That(context.IsCurrentCoverBlocked, Is.True);
                Assert.That(context.Version, Is.EqualTo(73L));
                Assert.That(context.FrozenVersion, Is.Zero);
                Assert.That(physics.RaycastOrigins, Has.Count.EqualTo(2));
                Assert.That(physics.RaycastOrigins[0], Is.EqualTo(cameraOrigin));
                Assert.That(physics.RaycastOrigins[1], Is.EqualTo(shotOrigin));
                Assert.That(
                    Vector3.Distance(
                        physics.RaycastDirections[1],
                        centerDirection),
                    Is.LessThan(0.000001f));
                Assert.That(
                    physics.RaycastDistances[1],
                    Is.GreaterThan(Vector3.Distance(
                        shotOrigin,
                        cameraTarget)));
                Assert.That(
                    physics.RaycastDistances[1],
                    Is.LessThanOrEqualTo(Vector3.Distance(
                        shotOrigin,
                        cameraTarget) + 0.002001f));
            });
        }

        [Test]
        public void CoverGeometryIdUsesTheFrozenStableDerivation()
        {
            GeometryId left = FpgRoomInstance.DeriveCoverGeometryId(
                "room-forest",
                "cover-left",
                0);
            GeometryId repeated = FpgRoomInstance.DeriveCoverGeometryId(
                "room-forest",
                "cover-left",
                0);
            GeometryId nextCollider = FpgRoomInstance.DeriveCoverGeometryId(
                "room-forest",
                "cover-left",
                1);

            AssertAll(() =>
            {
                Assert.That(left, Is.EqualTo(repeated));
                Assert.That(left.Value, Is.EqualTo(996468375));
                Assert.That(nextCollider.Value, Is.EqualTo(979690756));
                Assert.That(nextCollider, Is.Not.EqualTo(left));
                Assert.That(
                    FpgRoomInstance.DeriveCoverGeometryId(null, "cover-left", 0),
                    Is.EqualTo(GeometryId.Invalid));
                Assert.That(
                    FpgRoomInstance.DeriveCoverGeometryId("room-forest", "", 0),
                    Is.EqualTo(GeometryId.Invalid));
                Assert.That(
                    FpgRoomInstance.DeriveCoverGeometryId(
                        "room-forest",
                        "cover-left",
                        -1),
                    Is.EqualTo(GeometryId.Invalid));
            });
        }

        [Test]
        public void RoomRegistersEveryCoverBlockerAndResolvesItsCoverId()
        {
            HitboxRegistry registry = CreateRegistry();
            FpgRoomInstance room = CreateForestRoomInstance();

            Assert.That(
                room.TryRegisterCoverBlockers(
                    registry,
                    UnityAttackQuerySettings.Default,
                    out string error),
                Is.True,
                error);

            int expectedBlockerCount = 0;
            IReadOnlyList<FpgRoomCoverSlot> slots = room.RoomDefinition.CoverSlots;
            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                FpgRoomCoverSlot slot = slots[slotIndex];
                Assert.That(
                    room.TryGetCoverView(slot.MarkerId, out FpgCoverEntityView view),
                    Is.True);
                for (int colliderIndex = 0;
                    colliderIndex < view.BlockingColliderCount;
                    colliderIndex++)
                {
                    expectedBlockerCount++;
                    GeometryId geometryId = FpgRoomInstance.DeriveCoverGeometryId(
                        room.RoomDefinition.RoomId,
                        slot.MarkerId,
                        colliderIndex);
                    Assert.That(
                        registry.TryResolve(geometryId, out RegisteredHitbox registered),
                        Is.True);
                    Assert.That(
                        registered.TargetKind,
                        Is.EqualTo(QueryTargetKind.EnvironmentBlocker));
                    Assert.That(
                        room.TryResolveCoverId(geometryId, out string coverId),
                        Is.True);
                    Assert.That(coverId, Is.EqualTo(slot.MarkerId));
                }
            }

            Assert.That(expectedBlockerCount, Is.GreaterThan(0));
            Assert.That(registry.Count, Is.EqualTo(expectedBlockerCount));
        }

        [Test]
        public void CoverRegistrationRejectsGeometryConflictsBeforePartialRegistration()
        {
            HitboxRegistry registry = CreateRegistry();
            FpgRoomInstance room = CreateForestRoomInstance();
            FpgRoomCoverSlot firstSlot = room.RoomDefinition.CoverSlots[0];
            GeometryId conflictId = FpgRoomInstance.DeriveCoverGeometryId(
                room.RoomDefinition.RoomId,
                firstSlot.MarkerId,
                0);
            BoxCollider external = CreateCollider(
                "ExternalGeometryConflict",
                BlockerLayer);
            Assert.That(
                registry.Register(new HitboxBinding(
                    external,
                    HitboxTargetReference.Environment,
                    QueryTargetKind.EnvironmentBlocker,
                    HitPart.Body,
                    conflictId)).IsSuccess,
                Is.True);

            bool registered = room.TryRegisterCoverBlockers(
                registry,
                UnityAttackQuerySettings.Default,
                out string error);

            AssertAll(() =>
            {
                Assert.That(registered, Is.False);
                Assert.That(error, Does.Contain("duplicates"));
                Assert.That(registry.Count, Is.EqualTo(1));
                Assert.That(
                    registry.TryResolve(conflictId, out RegisteredHitbox existing),
                    Is.True);
                Assert.That(existing.Collider, Is.SameAs(external));
                Assert.That(room.TryResolveCoverId(conflictId, out _), Is.False);
            });

            IReadOnlyList<FpgRoomCoverSlot> slots = room.RoomDefinition.CoverSlots;
            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                FpgRoomCoverSlot slot = slots[slotIndex];
                Assert.That(
                    room.TryGetCoverView(slot.MarkerId, out FpgCoverEntityView view),
                    Is.True);
                for (int colliderIndex = 0;
                    colliderIndex < view.BlockingColliderCount;
                    colliderIndex++)
                {
                    Assert.That(view.TryGetBlockingCollider(
                        colliderIndex,
                        out Collider collider), Is.True);
                    Assert.That(registry.TryResolve(collider, out _), Is.False);
                }
            }
        }

        private static FpgAttackAvailability ResolveAvailability(
            FpgPlayerSkillSlot slot = FpgPlayerSkillSlot.Primary,
            bool playerValid = true,
            bool playerDead = false,
            bool encounterActive = true,
            bool coverMoving = false,
            WeaponState weaponState = WeaponState.Ready,
            long recastLockedUntilTick = -1L,
            long tick = 10L,
            int ammo = 8,
            int requiredAmmo = 1,
            FpgResolvedAimContext? aim = null,
            bool allowActiveReleaseState = false)
        {
            FpgResolvedAimContext resolvedAim = aim ?? CreateEnemyAim();
            return FpgAttackAvailability.Resolve(
                slot,
                playerValid,
                playerDead,
                encounterActive,
                coverMoving,
                weaponState,
                new TickIndex(recastLockedUntilTick),
                new TickIndex(tick),
                ammo,
                requiredAmmo,
                resolvedAim,
                allowActiveReleaseState);
        }

        private static void AssertReason(
            FpgAttackAvailability availability,
            FpgAttackUnavailableReason expected)
        {
            AssertAll(() =>
            {
                Assert.That(availability.Ready, Is.False);
                Assert.That(availability.Reason, Is.EqualTo(expected));
                Assert.That(availability.ShouldAutoReload, Is.False);
            });
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }

        private static FpgResolvedAimContext CreateEnemyAim()
        {
            return new FpgResolvedAimContext(
                new Vector2(0.5f, 0.5f),
                new Vector3(0f, 1f, -1f),
                Vector3.forward,
                new Vector3(0f, 1f, 10f),
                new Vector3(0f, 1f, 0f),
                Vector3.forward,
                new Vector3(0f, 1f, 10f),
                FpgResolvedAimTargetType.Enemy,
                new RuntimeId(99L),
                QueryTargetKind.Combatant,
                HitPart.Body,
                new GeometryId(701),
                string.Empty,
                string.Empty,
                17L,
                0L,
                10f);
        }

        private static FpgResolvedAimContext CreateEnvironmentAim(
            string targetCoverId,
            string currentCoverId,
            long version = 17L)
        {
            return new FpgResolvedAimContext(
                new Vector2(0.35f, 0.65f),
                new Vector3(0f, 2f, -1f),
                Vector3.forward,
                new Vector3(0f, 2f, 12f),
                new Vector3(0.5f, 1f, 0f),
                new Vector3(-0.5f, 1f, 12f).normalized,
                new Vector3(0.25f, 1.5f, 6f),
                FpgResolvedAimTargetType.Environment,
                RuntimeId.Invalid,
                QueryTargetKind.EnvironmentBlocker,
                HitPart.Body,
                new GeometryId(702),
                targetCoverId,
                currentCoverId,
                version,
                0L,
                6.03f);
        }

        private static UnityAttackQuerySettings CreateQuerySettings(
            float spreadTangent)
        {
            return new UnityAttackQuerySettings(
                50f,
                spreadTangent,
                3f,
                1 << HitboxLayer,
                1 << BlockerLayer);
        }

        private static BattleTickInput CreateTickInput(TickIndex tick)
        {
            return new BattleTickInput(
                PlayerInputFrame.Empty(tick),
                new AimPoseSnapshot(
                    tick,
                    SpatialVectorKey.Zero,
                    new SpatialVectorKey(
                        0,
                        0,
                        SpatialContract.DirectionUnits),
                    new SpatialVectorKey(
                        SpatialContract.DirectionUnits,
                        0,
                        0),
                    new SpatialVectorKey(
                        0,
                        SpatialContract.DirectionUnits,
                        0),
                    1));
        }

        private HitboxRegistry CreateRegistry()
        {
            GameObject gameObject = Track(new GameObject("HitboxRegistry"));
            HitboxRegistry registry = gameObject.AddComponent<HitboxRegistry>();
            Assert.That(registry.TryInitialize(out string error), Is.True, error);
            return registry;
        }

        private FpgRoomInstance CreateForestRoomInstance()
        {
            FpgRoomDefinition definition =
                AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(ForestRoomPath);
            Assert.That(definition, Is.Not.Null, ForestRoomPath);
            GameObject gameObject = Track(new GameObject("ForestRoomInstance"));
            FpgRoomInstance instance = gameObject.AddComponent<FpgRoomInstance>();
            Assert.That(
                instance.TryInitialize(definition, out string error),
                Is.True,
                error);
            return instance;
        }

        private BoxCollider CreateCollider(string name, int layer)
        {
            GameObject gameObject = Track(new GameObject(name));
            gameObject.layer = layer;
            return gameObject.AddComponent<BoxCollider>();
        }

        private GameObject Track(GameObject gameObject)
        {
            objects.Add(gameObject);
            return gameObject;
        }

        private sealed class DictionaryCoverResolver : IFpgCoverGeometryResolver
        {
            private readonly Dictionary<int, string> coverIds =
                new Dictionary<int, string>();

            public void Add(GeometryId geometryId, string coverId)
            {
                coverIds.Add(geometryId.Value, coverId);
            }

            public bool TryResolveCoverId(
                GeometryId geometryId,
                out string coverId)
            {
                if (geometryId.IsValid
                    && coverIds.TryGetValue(geometryId.Value, out coverId))
                {
                    return true;
                }

                coverId = string.Empty;
                return false;
            }
        }

        private sealed class RecordingPhysicsBackend : IUnityPhysicsQueryBackend
        {
            private readonly Queue<UnityPhysicsHit[]> queuedRaycasts =
                new Queue<UnityPhysicsHit[]>();

            public int Capacity => SpatialContract.AttackQueryCandidateCapacity;
            public List<Vector3> RaycastOrigins { get; } = new List<Vector3>();
            public List<Vector3> RaycastDirections { get; } = new List<Vector3>();
            public List<float> RaycastDistances { get; } = new List<float>();

            public void EnqueueRaycast(params UnityPhysicsHit[] hits)
            {
                queuedRaycasts.Enqueue(hits ?? Array.Empty<UnityPhysicsHit>());
            }

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
                RaycastOrigins.Add(origin);
                RaycastDirections.Add(direction);
                RaycastDistances.Add(maxDistance);
                UnityPhysicsHit[] hits = queuedRaycasts.Count > 0
                    ? queuedRaycasts.Dequeue()
                    : Array.Empty<UnityPhysicsHit>();
                int count = Math.Min(hits.Length, output.Length);
                Array.Copy(hits, output, count);
                return new NonAllocPhysicsQueryResult(
                    count,
                    hits.Length > output.Length);
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
                return new NonAllocPhysicsQueryResult(0, false);
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
