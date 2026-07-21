using System.Reflection;
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
    public sealed class PlayerWeaponPresentationControllerTests
    {
        [Test]
        public void InitialFeedBindingPresentsAShotCommittedBeforeTheFirstLateUpdate()
        {
            GameObject root = new GameObject("PlayerWeaponPresentationTestRoot");
            GameObject hostObject = new GameObject("PlayerWeaponPresentationHost");
            BattleSessionHost host = hostObject.AddComponent<BattleSessionHost>();
            GameObject viewRootObject = new GameObject("PlayerShotViews");
            PlayerWeaponPresentationController controller =
                root.AddComponent<PlayerWeaponPresentationController>();
            FixedPlayerShotPresentationFeed feed = new FixedPlayerShotPresentationFeed(4);
            try
            {
                viewRootObject.transform.SetParent(root.transform, false);

                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/FPGDemo/Presentation/M_FPG_Projectile.mat");
                Assert.That(material, Is.Not.Null, "The authored CombatLab presentation material is required for this integration test.");

                ConfigureController(
                    controller,
                    host,
                    viewRootObject.transform,
                    material);
                SetHostFeed(host, feed);
                Assert.That(feed.TryRecordCommitted(CreatePrimaryCapture(1), WeaponReleaseKind.Primary), Is.True);
                Assert.That(controller.TryInitialize(out string initializeError), Is.True, initializeError);

                InvokeLateUpdate(controller);

                Assert.That(controller.BoundFeed, Is.SameAs(feed));
                Assert.That(controller.PresentedShotCount, Is.EqualTo(1),
                    "The first valid shot must remain visible when its feed first binds during LateUpdate.");
                Assert.That(controller.ActiveTracerCount, Is.EqualTo(1));
                Assert.That(controller.ShotFeedGapCount, Is.Zero);
                Assert.That(controller.PresentationFaultCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void PrimaryShotAggregatesFrozenTrajectoriesToOneRepresentativeTrail()
        {
            ControllerFixture fixture = new ControllerFixture(8, 1, 8);
            try
            {
                PlayerShotQueryCapture capture = CreatePrimaryCaptureWithTerminalKinds(
                    11,
                    PlayerShotTerminalKind.Miss,
                    PlayerShotTerminalKind.EnvironmentBlocker,
                    PlayerShotTerminalKind.Combatant,
                    PlayerShotTerminalKind.Combatant,
                    PlayerShotTerminalKind.Projectile,
                    PlayerShotTerminalKind.Miss,
                    PlayerShotTerminalKind.EnvironmentBlocker,
                    PlayerShotTerminalKind.Combatant);
                Assert.That(WeaponDefinition.PrimaryPelletCount, Is.EqualTo(8));
                Assert.That(
                    capture.TrajectoryCount,
                    Is.EqualTo(WeaponDefinition.PrimaryPelletCount));
                Assert.That(fixture.Feed.TryRecordCommitted(capture, WeaponReleaseKind.Primary), Is.True);

                fixture.Tick();

                Assert.That(fixture.Controller.PresentedShotCount, Is.EqualTo(1));
                Assert.That(fixture.Controller.ActiveTracerCount, Is.EqualTo(1));
                Assert.That(fixture.Controller.PresentationFaultCount, Is.Zero);
                PlayerShotTrajectory representative = capture.GetTrajectory(3);
                PlayerShotTracerView tracer = FindActiveTracerEndingAt(
                    fixture.ViewRoot,
                    ToPosition(representative.TerminalPoint));
                LineRenderer line = tracer.GetComponent<LineRenderer>();
                AssertVector(line.GetPosition(0), fixture.VisualMuzzlePosition);
                AssertVector(line.GetPosition(1), ToPosition(representative.TerminalPoint));
                Assert.That(
                    line.startWidth,
                    Is.EqualTo(fixture.Controller.WeaponDefinition
                        .PrimaryPresentation.TracerWidth).Within(0.0001f));
                AssertColor(line.startColor, ExpectedTrajectoryColor(representative));

                Assert.That(fixture.Root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(fixture.Root.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(fixture.Root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(fixture.Root.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SecondaryShotUsesTheFrozenDirectPathAndTargetLocalBurst()
        {
            ControllerFixture fixture = new ControllerFixture(1, 1, 4);
            try
            {
                PlayerShotQueryCapture capture = CreateSecondaryCapture(12);
                Assert.That(fixture.Feed.TryRecordCommitted(capture, WeaponReleaseKind.Secondary), Is.True);

                fixture.Tick();

                Assert.That(fixture.Controller.ActiveTracerCount, Is.EqualTo(1));
                Assert.That(fixture.Controller.ActiveAreaCount, Is.Zero);
                Assert.That(fixture.Controller.ActiveTargetBurstCount, Is.EqualTo(1));
                Assert.That(fixture.Controller.ActiveSecondaryChargeVisualCount, Is.EqualTo(1));
                Assert.That(fixture.Controller.SecondaryHitMarkerCount, Is.EqualTo(1));
                PlayerShotTrajectory trajectory = capture.GetTrajectory(0);
                PlayerShotTracerView tracer = FindActiveTracerEndingAt(
                    fixture.ViewRoot,
                    ToPosition(trajectory.TerminalPoint));
                LineRenderer tracerLine = tracer.GetComponent<LineRenderer>();
                AssertVector(tracerLine.GetPosition(0), fixture.VisualMuzzlePosition);
                AssertVector(tracerLine.GetPosition(1), ToPosition(trajectory.TerminalPoint));
                Assert.That(
                    tracerLine.startWidth,
                    Is.EqualTo(fixture.Controller.WeaponDefinition
                        .SecondaryPresentation.Shot.TracerWidth).Within(0.0001f));

                PlayerShotTargetBurstView burst = FindActiveTargetBurst(fixture.ViewRoot);
                LineRenderer burstLine = burst.GetComponent<LineRenderer>();
                Vector3 center = ToPosition(capture.SecondaryAreaCenter);
                float radius = capture.SecondaryAreaRadiusKey
                    / (float)SpatialContract.PositionUnitsPerMeter;
                Assert.That(burstLine.positionCount, Is.EqualTo(21));
                Assert.That(
                    Vector3.Distance(burstLine.GetPosition(0), center),
                    Is.EqualTo(Mathf.Clamp(radius * 0.32f, 0.42f, 1.4f) * 0.22f).Within(0.001f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void PrimaryTrajectoryUsesTheProfiledWeakpointColor()
        {
            ControllerFixture fixture = new ControllerFixture(4, 1, 4);
            CombatPresentationProfile profile = ScriptableObject.CreateInstance<CombatPresentationProfile>();
            Color expected = new Color(0.95f, 0.2f, 0.72f, 1f);
            try
            {
                SerializedObject profileSerialized = new SerializedObject(profile);
                profileSerialized.FindProperty("hitDefinitions")
                    .GetArrayElementAtIndex(1)
                    .FindPropertyRelative("primaryColor").colorValue = expected;
                profileSerialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(profile.TryValidateStatic(out string profileError), Is.True, profileError);

                SerializedObject controllerSerialized = new SerializedObject(fixture.Controller);
                controllerSerialized.FindProperty("presentationProfile").objectReferenceValue = profile;
                controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

                PlayerShotQueryCapture capture = CreatePrimaryCaptureWithTerminalKinds(
                    120,
                    PlayerShotTerminalKind.Miss,
                    PlayerShotTerminalKind.EnvironmentBlocker,
                    PlayerShotTerminalKind.Combatant,
                    PlayerShotTerminalKind.Combatant);
                Assert.That(fixture.Feed.TryRecordCommitted(capture, WeaponReleaseKind.Primary), Is.True);
                fixture.Tick();

                PlayerShotTracerView tracer = FindActiveTracerEndingAt(
                    fixture.ViewRoot,
                    ToPosition(capture.GetTrajectory(3).TerminalPoint));
                AssertColor(tracer.GetComponent<LineRenderer>().startColor, expected);
            }
            finally
            {
                Object.DestroyImmediate(profile);
                fixture.Dispose();
            }
        }

        [Test]
        public void SecondaryChargeViewPrewarmsLockStrandsAndMarkerDrivenReleaseWithoutPhysics()
        {
            GameObject root = new GameObject("SecondaryChargeViewRoot");
            try
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/FPGDemo/Presentation/M_FPG_Projectile.mat");
                Assert.That(material, Is.Not.Null);

                D0SecondaryChargeView view = root.AddComponent<D0SecondaryChargeView>();
                Assert.That(view.TryPrepare(material, null, out string prepareError), Is.True, prepareError);
                int prewarmedChildCount = root.transform.childCount;
                Assert.That(prewarmedChildCount, Is.EqualTo(8),
                    "The fixed view owns four lock corners, three convergence strands and one release core.");

                view.BeginCharge(
                    new Vector3(-1f, 1f, 2f),
                    new Vector3(2f, 1.5f, 8f),
                    Color.cyan,
                    0.18f);
                Assert.That(view.IsCharging, Is.True);
                Assert.That(view.IsActive, Is.True);
                view.Advance(0.09f);

                LineRenderer[] lines = root.GetComponentsInChildren<LineRenderer>(true);
                Assert.That(lines, Has.Length.EqualTo(8));
                for (int index = 0; index < lines.Length; index++)
                {
                    Assert.That(lines[index].alignment, Is.EqualTo(LineAlignment.View));
                }

                view.Release(
                    new Vector3(-1f, 1f, 2f),
                    new Vector3(2f, 1.5f, 8f),
                    Color.cyan,
                    0.36f,
                    0.166f);
                Assert.That(view.IsReleasing, Is.True);
                Assert.That(view.HitMarkerCount, Is.EqualTo(1));
                view.Advance(0.17f);
                Assert.That(view.StopMarkerCount, Is.EqualTo(1),
                    "The CZN STOP boundary may alter only the visual cadence after the committed release.");
                Assert.That(root.transform.childCount, Is.EqualTo(prewarmedChildCount),
                    "Charge, release and marker handling must not create hot-path objects.");

                view.Clear();
                Assert.That(view.IsActive, Is.False);
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FixedTracerPoolRejectsOnlyTheVisualOverflow()
        {
            ControllerFixture fixture = new ControllerFixture(1, 1, 4);
            try
            {
                Assert.That(
                    fixture.Feed.TryRecordCommitted(CreatePrimaryCapture(13), WeaponReleaseKind.Primary),
                    Is.True);
                Assert.That(
                    fixture.Feed.TryRecordCommitted(CreatePrimaryCapture(14), WeaponReleaseKind.Primary),
                    Is.True);

                fixture.Tick();

                Assert.That(fixture.Controller.ActiveTracerCount, Is.EqualTo(1));
                Assert.That(fixture.Controller.TracerPoolRejectCount, Is.EqualTo(1));
                Assert.That(fixture.Controller.PresentedShotCount, Is.EqualTo(2));
                Assert.That(fixture.Controller.PresentationFaultCount, Is.Zero);
                Assert.That(fixture.Feed.LastSequence, Is.EqualTo(2));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void FeedGapClearsTransientViewsInsteadOfReplayingStaleShots()
        {
            ControllerFixture fixture = new ControllerFixture(1, 1, 1);
            try
            {
                Assert.That(
                    fixture.Feed.TryRecordCommitted(CreatePrimaryCapture(15), WeaponReleaseKind.Primary),
                    Is.True);
                fixture.Tick();
                Assert.That(fixture.Controller.ActiveTracerCount, Is.EqualTo(1));

                Assert.That(
                    fixture.Feed.TryRecordCommitted(CreatePrimaryCapture(16), WeaponReleaseKind.Primary),
                    Is.True);
                Assert.That(
                    fixture.Feed.TryRecordCommitted(CreatePrimaryCapture(17), WeaponReleaseKind.Primary),
                    Is.True);
                fixture.Tick();

                Assert.That(fixture.Controller.ShotFeedGapCount, Is.EqualTo(1));
                Assert.That(fixture.Controller.ActiveTracerCount, Is.Zero);
                Assert.That(fixture.Controller.ActiveAreaCount, Is.Zero);
                Assert.That(FindMuzzleFlash(fixture.ViewRoot).IsActive, Is.False);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void FeedReplacementAndCompletedSessionClearAllPlayerShotViews()
        {
            ControllerFixture fixture = new ControllerFixture(1, 1, 4);
            BattleSession session = CombatLabHarness.CreateSession();
            try
            {
                Assert.That(
                    session.ApplyControl(new SessionControlCommand(
                        new ControlSequence(1),
                        SessionControlCommandType.Start)).IsSuccess,
                    Is.True);
                SetHostSession(fixture.Host, session);
                Assert.That(
                    fixture.Feed.TryRecordCommitted(CreatePrimaryCapture(18), WeaponReleaseKind.Primary),
                    Is.True);
                fixture.Tick();
                Assert.That(fixture.Controller.ActiveTracerCount, Is.EqualTo(1));

                FixedPlayerShotPresentationFeed replacement = new FixedPlayerShotPresentationFeed(2);
                SetHostFeed(fixture.Host, replacement);
                fixture.Tick();
                Assert.That(fixture.Controller.BoundFeed, Is.SameAs(replacement));
                AssertNoActivePlayerShotViews(fixture);

                Assert.That(
                    replacement.TryRecordCommitted(CreatePrimaryCapture(19), WeaponReleaseKind.Primary),
                    Is.True);
                fixture.Tick();
                Assert.That(fixture.Controller.ActiveTracerCount, Is.EqualTo(1));
                Assert.That(
                    session.ApplyControl(new SessionControlCommand(
                        new ControlSequence(2),
                        SessionControlCommandType.Complete)).IsSuccess,
                    Is.True);
                fixture.Tick();

                AssertNoActivePlayerShotViews(fixture);
            }
            finally
            {
                session.Dispose();
                fixture.Dispose();
            }
        }

        [Test]
        public void PresentationFaultCommitsTheEventAndDoesNotBlockLaterFeedReads()
        {
            ControllerFixture fixture = new ControllerFixture(2, 1, 4);
            try
            {
                FieldInfo socketsField = typeof(PlayerWeaponPresentationController).GetField(
                    "actorSockets",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(socketsField, Is.Not.Null);
                socketsField.SetValue(fixture.Controller, null);
                Assert.That(
                    fixture.Feed.TryRecordCommitted(CreatePrimaryCapture(20), WeaponReleaseKind.Primary),
                    Is.True);

                fixture.Tick();
                Assert.That(fixture.Controller.PresentationFaultCount, Is.EqualTo(1));
                Assert.That(
                    fixture.Feed.TryRecordCommitted(CreatePrimaryCapture(21), WeaponReleaseKind.Primary),
                    Is.True);
                fixture.Tick();

                Assert.That(
                    fixture.Controller.PresentationFaultCount,
                    Is.EqualTo(2),
                    "Each failed visual event must be committed exactly once rather than retried forever.");
                fixture.Tick();
                Assert.That(fixture.Controller.PresentationFaultCount, Is.EqualTo(2));
                Assert.That(fixture.Controller.ShotFeedGapCount, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void PresentationConsumptionLeavesTheRunningSessionSnapshotAndReplayUntouched()
        {
            ControllerFixture fixture = new ControllerFixture(1, 1, 4);
            BattleSession session = CombatLabHarness.CreateSession();
            try
            {
                Assert.That(
                    session.ApplyControl(new SessionControlCommand(
                        new ControlSequence(1),
                        SessionControlCommandType.Start)).IsSuccess,
                    Is.True);
                SetHostSession(fixture.Host, session);
                Assert.That(
                    fixture.Feed.TryRecordCommitted(CreatePrimaryCapture(22), WeaponReleaseKind.Primary),
                    Is.True);
                ReplaySummary before = session.GetReplaySummary();

                fixture.Tick();

                ReplaySummary after = session.GetReplaySummary();
                Assert.That(after.CanonicalDigest, Is.EqualTo(before.CanonicalDigest));
                Assert.That(after.SpatialDecisionDigest, Is.EqualTo(before.SpatialDecisionDigest));
                Assert.That(after.FinalSnapshot.State, Is.EqualTo(before.FinalSnapshot.State));
                Assert.That(after.FinalSnapshot.PlayerLife, Is.EqualTo(before.FinalSnapshot.PlayerLife));
                Assert.That(after.FinalSnapshot.PlayerBarrier, Is.EqualTo(before.FinalSnapshot.PlayerBarrier));
                Assert.That(after.FinalSnapshot.PlayerAmmo, Is.EqualTo(before.FinalSnapshot.PlayerAmmo));
                Assert.That(after.FinalSnapshot.EnemyLife, Is.EqualTo(before.FinalSnapshot.EnemyLife));
                Assert.That(after.FinalSnapshot.EnemyBreak, Is.EqualTo(before.FinalSnapshot.EnemyBreak));
                Assert.That(fixture.Controller.ActiveTracerCount, Is.EqualTo(1));
            }
            finally
            {
                session.Dispose();
                fixture.Dispose();
            }
        }

        private static void ConfigureController(
            PlayerWeaponPresentationController controller,
            BattleSessionHost host,
            Transform shotViewRoot,
            Material shotMaterial,
            int tracerCapacity = 4,
            int areaCapacity = 1)
        {
            CombatPresentationProfile profile =
                AssetDatabase.LoadAssetAtPath<CombatPresentationProfile>(
                    "Assets/FPGDemo/Config/D0Slice/CombatPresentationProfile.asset");
            D0CharacterDefinition character =
                AssetDatabase.LoadAssetAtPath<D0CharacterDefinition>(
                    "Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei.asset");
            Assert.That(profile, Is.Not.Null);
            Assert.That(character, Is.Not.Null);
            Assert.That(character.EntityPrefab, Is.Not.Null);
            Assert.That(character.Weapon, Is.Not.Null);

            D0PlayerEntityView playerEntity =
                Object.Instantiate(character.EntityPrefab, host.transform);
            GameObject cameraObject = new GameObject("PlayerWeaponPresentationCamera");
            cameraObject.transform.SetParent(host.transform, false);
            Camera presentationCamera = cameraObject.AddComponent<Camera>();

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("shotViewRoot").objectReferenceValue = shotViewRoot;
            serialized.FindProperty("presentationProfile").objectReferenceValue = profile;
            serialized.FindProperty("shotMaterial").objectReferenceValue = shotMaterial;
            serialized.FindProperty("tracerCapacity").intValue = tracerCapacity;
            serialized.FindProperty("areaCapacity").intValue = areaCapacity;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                controller.TryBindPlayerEntity(
                    playerEntity,
                    character.Weapon,
                    out string entityError),
                Is.True,
                entityError);
            Assert.That(
                controller.TryBindSceneServices(
                    host,
                    presentationCamera,
                    out string serviceError),
                Is.True,
                serviceError);
        }

        private static void SetHostFeed(BattleSessionHost host, IPlayerShotPresentationFeed feed)
        {
            FieldInfo field = typeof(BattleSessionHost).GetField(
                "playerShotPresentationFeed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(host, feed);
        }

        private static void SetHostSession(BattleSessionHost host, BattleSession session)
        {
            FieldInfo field = typeof(BattleSessionHost).GetField(
                "<Session>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(host, session);
        }

        private static void InvokeLateUpdate(PlayerWeaponPresentationController controller)
        {
            MethodInfo method = typeof(PlayerWeaponPresentationController).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, null);
        }

        private static PlayerShotQueryCapture CreatePrimaryCaptureWithTerminalKinds(
            long identity,
            params PlayerShotTerminalKind[] terminalKinds)
        {
            Assert.That(terminalKinds, Is.Not.Null);
            Assert.That(terminalKinds.Length, Is.InRange(1, AttackQueryRequest.MaxPelletCount));
            TickIndex tick = new TickIndex(identity);
            AttackSnapshot attack = new AttackSnapshot(
                new AttackId(identity),
                new ShotId(identity),
                101,
                new RuntimeId(1),
                Team.Player,
                tick,
                new DamageSpec(10, 5),
                QueryPolicy.PelletRays,
                terminalKinds.Length,
                terminalKinds.Length,
                1,
                1);
            PelletSample[] samples = new PelletSample[terminalKinds.Length];
            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] = new PelletSample(
                    attack.ShotId,
                    index,
                    0x7FFFFF,
                    0x7FFFFF);
            }

            AttackQueryRequest request = new AttackQueryRequest(
                CreateTickInput(tick, identity),
                attack,
                samples,
                samples.Length);
            PlayerShotQueryCapture capture = new PlayerShotQueryCapture(
                request,
                terminalKinds.Length,
                SpatialVectorKey.Zero,
                0);
            for (int index = 0; index < terminalKinds.Length; index++)
            {
                PlayerShotTerminalKind terminalKind = terminalKinds[index];
                RuntimeId targetId = RuntimeId.Invalid;
                HitPart hitPart = HitPart.Body;
                GeometryId geometryId = GeometryId.Invalid;
                SpatialVectorKey terminalPoint = terminalKind == PlayerShotTerminalKind.Miss
                    ? new SpatialVectorKey(1000 + index * 50, 0, 20000)
                    : new SpatialVectorKey(2000 + index * 700, 500 + index * 100, 4000 + index * 650);
                if (terminalKind == PlayerShotTerminalKind.EnvironmentBlocker)
                {
                    geometryId = new GeometryId(1000 + index);
                }
                else if (terminalKind == PlayerShotTerminalKind.Combatant)
                {
                    targetId = new RuntimeId(100 + index);
                    hitPart = index == 3 ? HitPart.Weakpoint : HitPart.Body;
                    geometryId = new GeometryId(1000 + index);
                }
                else if (terminalKind == PlayerShotTerminalKind.Projectile)
                {
                    targetId = new RuntimeId(100 + index);
                    hitPart = HitPart.Projectile;
                    geometryId = new GeometryId(1000 + index);
                }

                capture.SetTrajectory(index, new PlayerShotTrajectory(
                    index,
                    request.TickInput.AimPose.Origin,
                    terminalPoint,
                    terminalKind,
                    targetId,
                    hitPart,
                    geometryId));
            }

            return capture;
        }

        private static PlayerShotQueryCapture CreateSecondaryCapture(long identity)
        {
            TickIndex tick = new TickIndex(identity);
            AttackSnapshot attack = new AttackSnapshot(
                new AttackId(identity),
                new ShotId(identity),
                101,
                new RuntimeId(1),
                Team.Player,
                tick,
                new DamageSpec(24, 12),
                QueryPolicy.DirectThenArea,
                1,
                4,
                2,
                1);
            AttackQueryRequest request = new AttackQueryRequest(
                CreateTickInput(tick, identity),
                attack,
                null,
                0);
            PlayerShotQueryCapture capture = new PlayerShotQueryCapture(
                request,
                1,
                new SpatialVectorKey(5000, 1000, 7000),
                2500);
            capture.SetTrajectory(0, new PlayerShotTrajectory(
                -1,
                request.TickInput.AimPose.Origin,
                new SpatialVectorKey(5000, 1000, 7000),
                PlayerShotTerminalKind.Miss,
                RuntimeId.Invalid,
                HitPart.Body,
                GeometryId.Invalid));
            return capture;
        }

        private static BattleTickInput CreateTickInput(TickIndex tick, long poseVersion)
        {
            return new BattleTickInput(
                PlayerInputFrame.Empty(tick, aimHeld: true, primaryHeld: true),
                new AimPoseSnapshot(
                    tick,
                    new SpatialVectorKey(1000, 0, 0),
                    new SpatialVectorKey(0, 0, SpatialContract.DirectionUnits),
                    new SpatialVectorKey(SpatialContract.DirectionUnits, 0, 0),
                    new SpatialVectorKey(0, SpatialContract.DirectionUnits, 0),
                    poseVersion));
        }

        private static PlayerShotTracerView FindActiveTracerEndingAt(Transform root, Vector3 expectedEnd)
        {
            PlayerShotTracerView[] tracers = root.GetComponentsInChildren<PlayerShotTracerView>(true);
            for (int index = 0; index < tracers.Length; index++)
            {
                if (!tracers[index].IsActive)
                {
                    continue;
                }

                LineRenderer line = tracers[index].GetComponent<LineRenderer>();
                if (line != null && Vector3.Distance(line.GetPosition(1), expectedEnd) <= 0.001f)
                {
                    return tracers[index];
                }
            }

            Assert.Fail($"No active tracer ended at {expectedEnd}.");
            return null;
        }

        private static PlayerShotTargetBurstView FindActiveTargetBurst(Transform root)
        {
            PlayerShotTargetBurstView[] bursts = root.GetComponentsInChildren<PlayerShotTargetBurstView>(true);
            for (int index = 0; index < bursts.Length; index++)
            {
                if (bursts[index].IsActive)
                {
                    return bursts[index];
                }
            }

            Assert.Fail("No active secondary target-burst view was found.");
            return null;
        }

        private static PlayerMuzzleFlashView FindMuzzleFlash(Transform root)
        {
            PlayerMuzzleFlashView muzzle = root.GetComponentInChildren<PlayerMuzzleFlashView>(true);
            Assert.That(muzzle, Is.Not.Null);
            return muzzle;
        }

        private static void AssertNoActivePlayerShotViews(ControllerFixture fixture)
        {
            Assert.That(fixture.Controller.ActiveTracerCount, Is.Zero);
            Assert.That(fixture.Controller.ActiveAreaCount, Is.Zero);
            Assert.That(fixture.Controller.ActiveTargetBurstCount, Is.Zero);
            Assert.That(fixture.Controller.ActiveSecondaryChargeVisualCount, Is.Zero);
            Assert.That(FindMuzzleFlash(fixture.ViewRoot).IsActive, Is.False);
        }

        private static Color ExpectedTrajectoryColor(PlayerShotTrajectory trajectory)
        {
            switch (trajectory.TerminalKind)
            {
                case PlayerShotTerminalKind.EnvironmentBlocker:
                    return new Color(1f, 0.67f, 0.2f, 0.9f);
                case PlayerShotTerminalKind.Combatant:
                    return trajectory.HitPart == HitPart.Weakpoint
                        ? new Color(1f, 0.9f, 0.22f, 1f)
                        : new Color(0.42f, 0.9f, 1f, 0.96f);
                case PlayerShotTerminalKind.Projectile:
                    return new Color(0.32f, 1f, 0.92f, 1f);
                default:
                    return new Color(0.62f, 0.84f, 1f, 0.48f);
            }
        }

        private static Vector3 ToPosition(SpatialVectorKey key)
        {
            float inverseScale = 1f / SpatialContract.PositionUnitsPerMeter;
            return new Vector3(key.X * inverseScale, key.Y * inverseScale, key.Z * inverseScale);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThanOrEqualTo(0.001f));
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.003f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.003f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.003f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.003f));
        }

        private sealed class ControllerFixture
        {
            public readonly GameObject Root;
            public readonly GameObject HostObject;
            public readonly BattleSessionHost Host;
            public readonly Transform ViewRoot;
            public readonly PlayerWeaponPresentationController Controller;
            public readonly FixedPlayerShotPresentationFeed Feed;

            public Vector3 VisualMuzzlePosition
            {
                get
                {
                    Assert.That(
                        Controller.SocketRegistry.TryResolve(
                            Controller.WeaponDefinition.PrimaryPresentation.SocketId,
                            out Transform visualMuzzle),
                        Is.True);
                    return visualMuzzle.position;
                }
            }

            public ControllerFixture(int tracerCapacity, int areaCapacity, int feedCapacity)
            {
                Root = new GameObject("PlayerWeaponPresentationFixture");
                HostObject = new GameObject("PlayerWeaponPresentationHost");
                Host = HostObject.AddComponent<BattleSessionHost>();
                GameObject viewRootObject = new GameObject("PlayerShotViews");
                Controller = Root.AddComponent<PlayerWeaponPresentationController>();
                Feed = new FixedPlayerShotPresentationFeed(feedCapacity);
                viewRootObject.transform.SetParent(Root.transform, false);
                ViewRoot = viewRootObject.transform;

                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/FPGDemo/Presentation/M_FPG_Projectile.mat");
                Assert.That(material, Is.Not.Null);
                ConfigureController(
                    Controller,
                    Host,
                    ViewRoot,
                    material,
                    tracerCapacity,
                    areaCapacity);
                SetHostFeed(Host, Feed);
                Assert.That(Controller.TryInitialize(out string error), Is.True, error);
            }

            public void Tick()
            {
                InvokeLateUpdate(Controller);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(HostObject);
            }
        }

        private static PlayerShotQueryCapture CreatePrimaryCapture(long identity)
        {
            TickIndex tick = new TickIndex(identity);
            AttackSnapshot attack = new AttackSnapshot(
                new AttackId(identity),
                new ShotId(identity),
                101,
                new RuntimeId(1),
                Team.Player,
                tick,
                new DamageSpec(10, 5),
                QueryPolicy.PelletRays,
                1,
                1,
                1,
                1);
            AttackQueryRequest request = new AttackQueryRequest(
                new BattleTickInput(
                    PlayerInputFrame.Empty(tick, aimHeld: true, primaryHeld: true),
                    new AimPoseSnapshot(
                        tick,
                        new SpatialVectorKey(1000, 0, 0),
                        new SpatialVectorKey(0, 0, SpatialContract.DirectionUnits),
                        new SpatialVectorKey(SpatialContract.DirectionUnits, 0, 0),
                        new SpatialVectorKey(0, SpatialContract.DirectionUnits, 0),
                        identity)),
                attack,
                new[] { new PelletSample(attack.ShotId, 0, 0x7FFFFF, 0x7FFFFF) },
                1);
            PlayerShotQueryCapture capture = new PlayerShotQueryCapture(
                request,
                1,
                SpatialVectorKey.Zero,
                0);
            capture.SetTrajectory(0, new PlayerShotTrajectory(
                0,
                request.TickInput.AimPose.Origin,
                new SpatialVectorKey(1000, 0, 20000),
                PlayerShotTerminalKind.Miss,
                RuntimeId.Invalid,
                HitPart.Body,
                GeometryId.Invalid));
            return capture;
        }
    }
}
