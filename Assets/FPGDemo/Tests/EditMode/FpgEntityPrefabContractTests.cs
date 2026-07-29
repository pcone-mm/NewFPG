using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using Spine.Unity;
using UnityEngine;

using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Unity;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgEntityPrefabContractTests
    {
        private readonly List<GameObject> createdObjects =
            new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void FormalEntityPrefabsSatisfyTheirContracts()
        {
            FpgPlayerEntityView player = LoadEntity<FpgPlayerEntityView>(
                "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_FeiEntity.prefab");
            Assert.That(player.TryValidate(out string error), Is.True, error);

            string[] enemyPaths =
            {
                "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_BurstbugEntity.prefab",
                "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_LuanEntity.prefab",
                "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_HudieEntity.prefab"
            };
            for (int index = 0; index < enemyPaths.Length; index++)
            {
                FpgEnemyEntityView enemy = LoadEntity<FpgEnemyEntityView>(enemyPaths[index]);
                Assert.That(enemy.TryValidate(out error), Is.True, enemyPaths[index] + ": " + error);
            }
        }

        [Test]
        public void FormalEnemyHitboxDebugDefaultsRemainBackwardCompatible()
        {
            string[] enemyPaths =
            {
                "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_BurstbugEntity.prefab",
                "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_LuanEntity.prefab",
                "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_HudieEntity.prefab"
            };

            for (int pathIndex = 0; pathIndex < enemyPaths.Length; pathIndex++)
            {
                FpgEnemyEntityView enemy =
                    LoadEntity<FpgEnemyEntityView>(enemyPaths[pathIndex]);
                Assert.That(
                    enemy.PreviewHitboxesInPlayMode,
                    Is.True,
                    enemyPaths[pathIndex]);
                for (int hitPartIndex = 0;
                    hitPartIndex < enemy.HitPartCount;
                    hitPartIndex++)
                {
                    Assert.That(
                        enemy.TryGetHitPartFollowSettings(
                            hitPartIndex,
                            out D0EnemyHitboxFollowSettings settings),
                        Is.True,
                        enemyPaths[pathIndex]);
                    Assert.That(settings.PositionOffset, Is.EqualTo(Vector3.zero));
                    Assert.That(
                        settings.RotationOffsetEuler,
                        Is.EqualTo(Vector3.zero));
                    Assert.That(settings.HasFiniteOffsets, Is.True);
                }
            }
        }

        [Test]
        public void FormalPlayerCoverUsesAuthoredWallAndPeekBranches()
        {
            FpgPlayerEntityView player = LoadEntity<FpgPlayerEntityView>(
                "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_FeiEntity.prefab");
            FpgPlayerBarrierPresentationController cover = player.Barrier;

            Assert.That(cover, Is.Not.Null);
            Assert.That(cover.transform.parent, Is.SameAs(player.transform));
            Assert.That(cover.PeekRoot.parent, Is.SameAs(player.transform));
            Assert.That(player.VisualRoot.parent, Is.SameAs(cover.PeekRoot));
            Assert.That(
                cover.CoverVisualRoot.parent,
                Is.SameAs(cover.transform));
            Assert.That(
                cover.CoverVisualRoot.localPosition,
                Is.EqualTo(new Vector3(0.3f, 1.075f, 0.25f)));
            Assert.That(
                cover.CoverVisualRoot.localScale,
                Is.EqualTo(new Vector3(1.6f, 2.15f, 0.28f)));
            Assert.That(
                cover.PeekLocalOffset,
                Is.EqualTo(new Vector3(1.35f, 0f, 0f)));
            Assert.That(
                AssetDatabase.GetAssetPath(cover.CoverRenderer.sharedMaterial),
                Is.EqualTo(
                    "Assets/FPGDemo/Presentation/Materials/M_FPG_Cover.mat"));

            LineRenderer outline = cover.GetComponent<LineRenderer>();
            Assert.That(outline, Is.Not.Null);
            Assert.That(outline.loop, Is.True);
            Assert.That(outline.positionCount, Is.EqualTo(4));
            Assert.That(
                outline.GetPosition(0).z,
                Is.EqualTo(0.11f).Within(0.0001f));
            Assert.That(
                AssetDatabase.GetAssetPath(outline.sharedMaterial),
                Is.EqualTo(
                    "Assets/FPGDemo/Presentation/M_FPG_Feedback.mat"));
            Assert.That(
                cover.CoverVisualRoot.GetComponentsInChildren<Collider>(true),
                Is.Empty);
            Assert.That(
                cover.CoverVisualRoot.GetComponentsInChildren<Rigidbody>(true),
                Is.Empty);
            Assert.That(
                cover.PrimaryPresentationMuzzle.IsChildOf(cover.PeekRoot),
                Is.True);
            Assert.That(
                cover.SecondaryPresentationMuzzle.IsChildOf(cover.PeekRoot),
                Is.True);
            Assert.That(
                player.SocketRegistry.transform.IsChildOf(cover.PeekRoot),
                Is.False);

            D0ThreeCProfile threeC =
                AssetDatabase.LoadAssetAtPath<D0ThreeCProfile>(
                    "Assets/FPGDemo/Config/FormalEncounter/Characters/FPG_Fei_ThreeC.asset");
            Assert.That(threeC, Is.Not.Null);
            Assert.That(
                threeC.CoverLocalPosition,
                Is.EqualTo(cover.CoverVisualRoot.localPosition));
        }

        [Test]
        public void SocketRegistryUsesStableIdsAndRejectsDuplicateTransforms()
        {
            GameObject root = CreateObject("SocketRegistryRoot");
            D0ActorSocketRegistry registry = root.AddComponent<D0ActorSocketRegistry>();
            Transform muzzle = CreateChild(root.transform, "PrimaryMuzzle");
            Transform duplicate = CreateChild(root.transform, "DuplicateMuzzle");

            Assert.That(
                registry.TryRegister(
                    D0ActorSocketRegistry.PrimaryMuzzleId,
                    muzzle,
                    out string error),
                Is.True,
                error);
            Assert.That(
                registry.TryResolve(
                    D0ActorSocketRegistry.PrimaryMuzzleId,
                    out Transform resolved),
                Is.True);
            Assert.That(resolved, Is.SameAs(muzzle));

            Assert.That(
                registry.TryRegister(
                    D0ActorSocketRegistry.PrimaryMuzzleId,
                    duplicate,
                    out error),
                Is.False);
            Assert.That(error, Does.Contain("duplicated"));

            Assert.That(
                registry.TryRegister(
                    "weapon.secondary.muzzle",
                    muzzle,
                    out error),
                Is.False);
            Assert.That(error, Does.Contain("Transform"));
            Assert.That(registry.TryValidate(out error), Is.True, error);
        }

        [Test]
        public void SocketRegistryRequiresBoneMetadataOnlyForBoneFollowing()
        {
            GameObject root = CreateObject("SocketRegistryRoot");
            D0ActorSocketRegistry registry = root.AddComponent<D0ActorSocketRegistry>();
            Transform anchor = CreateChild(root.transform, "AttackOrigin");

            Assert.That(
                registry.TryRegister(
                    D0ActorSocketRegistry.DefaultAttackOriginId,
                    anchor,
                    D0ActorSocketFollowMode.SpineBone,
                    string.Empty,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("bone name"));

            Assert.That(
                registry.TryRegister(
                    D0ActorSocketRegistry.DefaultAttackOriginId,
                    anchor,
                    D0ActorSocketFollowMode.SpineBone,
                    "weapon_tip",
                    out error),
                Is.True,
                error);
            Assert.That(registry.Bindings[0].FollowsSpineBone, Is.True);
            Assert.That(registry.Bindings[0].BoneName, Is.EqualTo("weapon_tip"));
        }

        [Test]
        public void FormalEnemyEntityValidatesHitPartContract()
        {
            FpgEnemyEntityView enemy = CreateValidEnemy();

            Assert.That(enemy.TryValidate(out string error), Is.True, error);
            Assert.That(enemy.HitPartCount, Is.EqualTo(2));
            Assert.That(enemy.TryGetHitPart(0, out Collider body, out HitPart bodyKind), Is.True);
            Assert.That(body, Is.Not.Null);
            Assert.That(bodyKind, Is.EqualTo(HitPart.Body));

            SetField(enemy, "hitParts", Array.Empty<Collider>());
            Assert.That(enemy.TryValidate(out error), Is.False);
            Assert.That(error, Does.Contain("at least one hit part"));
        }

        [Test]
        public void FormalEnemyRejectsNonFiniteBoneFollowOffset()
        {
            FpgEnemyEntityView enemy = CreateValidEnemy();
            D0EnemyHitboxFollowSettings invalid = CreateHitboxFollowSettings(
                "root",
                new Vector3(float.NaN, 0f, 0f),
                Vector3.zero,
                true);
            SetField(
                enemy,
                "hitPartFollowSettings",
                new[] { invalid, default(D0EnemyHitboxFollowSettings) });

            Assert.That(enemy.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("finite"));
        }

        [Test]
        public void HudieBoneFollowAppliesExtraPositionAndUnbindRestoresPose()
        {
            const string definitionPath =
                "Assets/FPGDemo/Config/FormalEncounter/FPG_Hudie_Enemy.asset";
            const string prefabPath =
                "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_HudieEntity.prefab";
            FpgEnemyDefinition definition =
                AssetDatabase.LoadAssetAtPath<FpgEnemyDefinition>(definitionPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            Assert.That(definition, Is.Not.Null, definitionPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);

            GameObject baselineObject = UnityEngine.Object.Instantiate(prefab);
            GameObject offsetObject = UnityEngine.Object.Instantiate(prefab);
            createdObjects.Add(baselineObject);
            createdObjects.Add(offsetObject);
            FpgEnemyEntityView baseline =
                baselineObject.GetComponent<FpgEnemyEntityView>();
            FpgEnemyEntityView offset =
                offsetObject.GetComponent<FpgEnemyEntityView>();
            Assert.That(
                baseline.TryGetHitPart(0, out Collider baselineBody, out _),
                Is.True);
            Assert.That(
                offset.TryGetHitPart(0, out Collider offsetBody, out _),
                Is.True);
            Vector3 authoredLocalPosition = offsetBody.transform.localPosition;
            Quaternion authoredLocalRotation = offsetBody.transform.localRotation;
            var serializedOffset = new SerializedObject(offset);
            Vector3 extraOffset = new Vector3(0f, 0f, 0.4f);
            serializedOffset.FindProperty("hitPartFollowSettings")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("positionOffset")
                .vector3Value = extraOffset;
            serializedOffset.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                Assert.That(
                    baseline.TryBindFormalRuntime(
                        new RuntimeId(801L),
                        0,
                        definition,
                        out string baselineError),
                    Is.True,
                    baselineError);
                Assert.That(
                    offset.TryBindFormalRuntime(
                        new RuntimeId(802L),
                        1,
                        definition,
                        out string offsetError),
                    Is.True,
                    offsetError);

                Vector3 expectedDelta = offset.SkeletonAnimation.transform
                    .TransformDirection(Vector3.forward).normalized
                    * extraOffset.z;
                Vector3 actualDelta =
                    offsetBody.transform.position - baselineBody.transform.position;
                Assert.That(
                    Vector3.Distance(expectedDelta, actualDelta),
                    Is.LessThan(0.001f));
            }
            finally
            {
                baseline.UnbindFormalRuntime();
                offset.UnbindFormalRuntime();
            }

            Assert.That(
                Vector3.Distance(
                    authoredLocalPosition,
                    offsetBody.transform.localPosition),
                Is.LessThan(0.0001f));
            Assert.That(
                Mathf.Abs(Quaternion.Dot(
                    authoredLocalRotation,
                    offsetBody.transform.localRotation)),
                Is.GreaterThan(0.999999f));
        }

        [Test]
        public void PlayerEntityRequiresPlayerComponentsAndAnchors()
        {
            FpgPlayerEntityView player = CreateValidPlayer();

            Assert.That(player.TryValidate(out string error), Is.True, error);
            Assert.That(player.CharacterController, Is.Not.Null);
            Assert.That(player.Bounds, Is.Not.Null);
            Assert.That(player.Barrier, Is.Not.Null);

            Assert.That(
                player.TryResolvePresentationSocket(
                    D0ActorSocketRegistry.PrimaryMuzzleId,
                    out Transform presentationMuzzle),
                Is.True);
            Assert.That(
                player.TryResolveSocket(
                    D0ActorSocketRegistry.PrimaryMuzzleId,
                    out Transform authoritativeMuzzle),
                Is.True);
            Assert.That(
                presentationMuzzle,
                Is.SameAs(player.Barrier.PrimaryPresentationMuzzle));
            Assert.That(presentationMuzzle, Is.Not.SameAs(authoritativeMuzzle));
            Assert.That(
                player.TryResolvePresentationSocket(
                    D0ActorSocketRegistry.DefaultAttackOriginId,
                    out Transform fallbackOrigin),
                Is.True);
            Assert.That(
                player.TryResolveSocket(
                    D0ActorSocketRegistry.DefaultAttackOriginId,
                    out Transform authoritativeOrigin),
                Is.True);
            Assert.That(fallbackOrigin, Is.SameAs(authoritativeOrigin));

            SetField(player, "cameraPivot", player.AimAnchor);
            Assert.That(player.TryValidate(out error), Is.False);
            Assert.That(error, Does.Contain("CameraPivot"));
        }

        [Test]
        public void SpineSocketFollowerRejectsMissingBindingBeforeRuntime()
        {
            GameObject root = CreateObject("Follower");
            D0SpineSocketFollower follower = root.AddComponent<D0SpineSocketFollower>();

            Assert.That(follower.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("SkeletonAnimation"));
        }

        private FpgEnemyEntityView CreateValidEnemy()
        {
            GameObject root = CreateObject("EnemyEntity");
            FpgEnemyEntityView enemy = root.AddComponent<FpgEnemyEntityView>();
            Transform gameplay = CreateChild(root.transform, "GameplayRoot");
            Transform socketsRoot = CreateChild(root.transform, "Sockets");
            D0ActorSocketRegistry sockets =
                socketsRoot.gameObject.AddComponent<D0ActorSocketRegistry>();
            Transform projectile = CreateChild(gameplay, "ProjectileSpawn");
            Transform weakpoint = CreateChild(gameplay, "Weakpoint");
            Transform overhead = CreateChild(root.transform, "OverheadHealthBar");
            BoxCollider body = CreateChild(gameplay, "BodyHitbox")
                .gameObject.AddComponent<BoxCollider>();
            SphereCollider weakpointCollider = CreateChild(weakpoint, "WeakpointHitbox")
                .gameObject.AddComponent<SphereCollider>();

            SetField(enemy, "gameplayAnchor", gameplay);
            SetField(enemy, "projectileAnchor", projectile);
            SetField(enemy, "weakpointAnchor", weakpoint);
            SetField(enemy, "overheadHealthBarAnchor", overhead);
            SetField(enemy, "socketRegistry", sockets);
            SetField(enemy, "hitParts", new Collider[] { body, weakpointCollider });
            SetField(enemy, "hitPartKinds", new[] { HitPart.Body, HitPart.Weakpoint });
            return enemy;
        }

        private FpgPlayerEntityView CreateValidPlayer()
        {
            GameObject root = CreateObject("PlayerEntity");
            FpgPlayerEntityView player = root.AddComponent<FpgPlayerEntityView>();
            CharacterController characterController = root.AddComponent<CharacterController>();
            FpgPlayerBounds bounds = root.AddComponent<FpgPlayerBounds>();
            Transform gameplay = CreateChild(root.transform, "GameplayRoot");
            Transform peekRoot = CreateChild(root.transform, "PeekRoot");
            Transform visual = CreateChild(peekRoot, "VisualRoot");
            Transform primaryPresentationMuzzle = CreateChild(
                peekRoot,
                "PrimaryPresentationMuzzle");
            Transform secondaryPresentationMuzzle = CreateChild(
                peekRoot,
                "SecondaryPresentationMuzzle");
            Transform coverRoot = CreateChild(root.transform, "CoverRoot");
            coverRoot.gameObject.AddComponent<LineRenderer>();
            FpgPlayerBarrierPresentationController barrier =
                coverRoot.gameObject.AddComponent<
                    FpgPlayerBarrierPresentationController>();
            Transform coverVisualRoot = CreateChild(
                coverRoot,
                "CoverVisualRoot");
            MeshRenderer coverRenderer =
                coverVisualRoot.gameObject.AddComponent<MeshRenderer>();
            Transform socketsRoot = CreateChild(root.transform, "Sockets");
            D0ActorSocketRegistry sockets = socketsRoot.gameObject.AddComponent<D0ActorSocketRegistry>();
            Transform primaryMuzzle = CreateChild(socketsRoot, "PrimaryMuzzle");
            Transform secondaryMuzzle = CreateChild(socketsRoot, "SecondaryMuzzle");
            Transform attackOrigin = CreateChild(socketsRoot, "AttackOrigin");
            Actor2DPresenter presenter = root.AddComponent<Actor2DPresenter>();
            SkeletonAnimation skeleton = visual.gameObject.AddComponent<SkeletonAnimation>();
            Transform aim = CreateChild(root.transform, "AimAnchor");
            Transform ground = CreateChild(root.transform, "GroundAnchor");
            Transform camera = CreateChild(root.transform, "CameraPivot");
            Transform bodyTransform = CreateChild(gameplay, "BodyHitbox");
            BoxCollider body = bodyTransform.gameObject.AddComponent<BoxCollider>();

            Assert.That(
                sockets.TryRegister(
                    D0ActorSocketRegistry.PrimaryMuzzleId,
                    primaryMuzzle,
                    out string socketError),
                Is.True,
                socketError);
            Assert.That(
                sockets.TryRegister(
                    D0ActorSocketRegistry.SecondaryMuzzleId,
                    secondaryMuzzle,
                    out socketError),
                Is.True,
                socketError);
            Assert.That(
                sockets.TryRegister(
                    D0ActorSocketRegistry.DefaultAttackOriginId,
                    attackOrigin,
                    out socketError),
                Is.True,
                socketError);

            Material lineMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/FPGDemo/Presentation/M_FPG_Feedback.mat");
            Assert.That(lineMaterial, Is.Not.Null);
            SetField(barrier, "peekRoot", peekRoot);
            SetField(barrier, "coverVisualRoot", coverVisualRoot);
            SetField(barrier, "coverRenderer", coverRenderer);
            SetField(
                barrier,
                "primaryPresentationMuzzle",
                primaryPresentationMuzzle);
            SetField(
                barrier,
                "secondaryPresentationMuzzle",
                secondaryPresentationMuzzle);
            SetField(barrier, "lineMaterial", lineMaterial);
            barrier.ResetPresentation();

            SetField(player, "gameplayAnchor", gameplay);
            SetField(player, "visualRoot", visual);
            SetField(player, "socketRegistry", sockets);
            SetField(player, "actorPresenter", presenter);
            SetField(player, "skeletonAnimation", skeleton);
            SetField(player, "characterController", characterController);
            SetField(player, "bounds", bounds);
            SetField(player, "aimAnchor", aim);
            SetField(player, "groundAnchor", ground);
            SetField(player, "cameraPivot", camera);
            SetField(player, "bodyHitbox", body);
            SetField(player, "barrier", barrier);
            return player;
        }

        private static T LoadEntity<T>(string path)
            where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            T entity = prefab.GetComponent<T>();
            Assert.That(entity, Is.Not.Null, path);
            return entity;
        }

        private GameObject CreateObject(string name)
        {
            GameObject value = new GameObject(name);
            createdObjects.Add(value);
            return value;
        }

        private Transform CreateChild(Transform parent, string name)
        {
            GameObject child = CreateObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static D0EnemyHitboxFollowSettings CreateHitboxFollowSettings(
            string boneName,
            Vector3 positionOffset,
            Vector3 rotationOffsetEuler,
            bool followBoneRotation)
        {
            object boxed = default(D0EnemyHitboxFollowSettings);
            SetField(
                boxed,
                "followMode",
                D0EnemyHitboxFollowMode.SpineBone);
            SetField(boxed, "boneName", boneName);
            SetField(
                boxed,
                "keepAuthoredRotation",
                !followBoneRotation);
            SetField(boxed, "positionOffset", positionOffset);
            SetField(boxed, "rotationOffsetEuler", rotationOffsetEuler);
            return (D0EnemyHitboxFollowSettings)boxed;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }

            Assert.That(field, Is.Not.Null, "Could not find field " + fieldName + ".");
            field.SetValue(target, value);
        }
    }
}
